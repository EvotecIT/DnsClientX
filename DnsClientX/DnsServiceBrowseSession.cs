using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace DnsClientX;
/// <summary>Runs and drains one persistent browse socket with a bounded change queue.</summary>
internal sealed class DnsServiceBrowseSession : IAsyncDisposable {
    private sealed class Schedule {
        internal double Due;
        internal double Interval = 1;
    }

    private readonly Configuration _configuration;
    private readonly DnsServiceBrowseCache _cache;
    private readonly bool _debug;
    private readonly CancellationTokenSource _stop;
    private readonly Queue<DnsServiceBrowseEvent> _events = new();
    private readonly SemaphoreSlim _slots;
    private readonly SemaphoreSlim _available = new(0);
    private readonly object _gate = new();
    private readonly Random _queryRandom = new();
    private Task _producer = Task.CompletedTask;
    private bool _completed;
    private bool _disposed;
    private Exception? _failure;
    internal DnsServiceBrowseSession(Configuration configuration, string serviceType, int maximumRecords, int maximumServices, int capacity, bool debug, CancellationToken cancellationToken) {
        _configuration = configuration;
        _cache = new DnsServiceBrowseCache(serviceType, maximumRecords, maximumServices, configuration.MulticastInterfaceIndex);
        _debug = debug;
        _stop = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _slots = new SemaphoreSlim(capacity, capacity);
    }

    internal void Start() => _producer = Task.Run(RunAsync);
    internal async IAsyncEnumerable<DnsServiceBrowseEvent> ReadAsync([EnumeratorCancellation] CancellationToken cancellationToken) {
        while (true) {
            await _available.WaitAsync(cancellationToken).ConfigureAwait(false);
            DnsServiceBrowseEvent? item = null;
            Exception? failure;
            bool completed;
            lock (_gate) {
                if (_events.Count > 0) {
                    item = _events.Dequeue();
                }

                completed = _completed;
                failure = _failure;
            }

            cancellationToken.ThrowIfCancellationRequested();
            if (item != null) {
                _slots.Release();
                yield return item;
                continue;
            }

            if (failure != null) {
                System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();
            }

            if (completed) {
                yield break;
            }
        }
    }

    internal Task StopAsync() {
        lock (_gate) {
            if (!_disposed) {
                _stop.Cancel();
            }

            return _producer;
        }
    }

    public async ValueTask DisposeAsync() {
        Task producer;
        lock (_gate) {
            if (_disposed) {
                return;
            }

            _disposed = true;
            _stop.Cancel();
            producer = _producer;
        }

        await producer.ConfigureAwait(false);
        _stop.Dispose();
    // A concurrent ReadAsync can still be unwinding after client disposal. Semaphores contain no OS handles.
    }

    private async Task RunAsync() {
        UdpClient? socket = null;
        Task<UdpReceiveResult>? receive = null;
        Exception? failure = null;
        try {
            if (!IPAddress.TryParse(_configuration.Hostname, out IPAddress? group) || !DnsWireResolveMulticast.IsMulticast(group)) {
                throw new ArgumentException("The continuous browse server must be a multicast IP address.");
            }

            socket = DnsWireResolveMulticast.CreateClient(group, _configuration.MulticastInterfaceIndex, _configuration.Port, listenForMulticast: true);
            DnsWireResolveMulticast.JoinGroup(socket, group, _configuration.MulticastInterfaceIndex);
            IPEndPoint target = DnsWireResolveMulticast.CreateTargetEndPoint(group, _configuration.Port, _configuration.MulticastInterfaceIndex);
            using CancellationTokenRegistration registration = _stop.Token.Register(socket.Dispose);
            var schedules = new Dictionary<(string, DnsRecordType), Schedule>();
            receive = socket.ReceiveAsync();
            double nextTick = 0;
            Task tick = Task.CompletedTask;
            while (true) {
                _stop.Token.ThrowIfCancellationRequested();
                double now = Now();
                if (now >= nextTick) {
                    await PublishAsync(_cache.Advance(now, DateTimeOffset.UtcNow)).ConfigureAwait(false);
                    var active = new HashSet<(string, DnsRecordType)>(_cache.Questions(now));
                    foreach (var old in schedules.Keys.Where(key => !active.Contains(key)).ToArray()) {
                        schedules.Remove(old);
                    }

                    var queries = new HashSet<(string, DnsRecordType)>(_cache.RefreshQuestions(now));
                    foreach (var question in active) {
                        if (!schedules.TryGetValue(question, out Schedule? schedule)) {
                            schedule = new Schedule {
                                Due = now + 0.02 + _queryRandom.NextDouble() * 0.1
                            };
                            schedules.Add(question, schedule);
                        }

                        if (schedule.Due > now) {
                            continue;
                        }

                        queries.Add(question);
                        schedule.Due = now + schedule.Interval;
                        schedule.Interval = Math.Min(3600, schedule.Interval * 2);
                    }

                    foreach (var question in queries) {
                        byte[] query = DnsWireResolveMulticast.CreateBrowseQuery(question.Item1, question.Item2, _cache.KnownAnswers(question.Item1, question.Item2, now));
#if NET8_0_OR_GREATER
                        await socket.SendAsync(query, target, _stop.Token).ConfigureAwait(false);
#else
                        await socket.SendAsync(query, query.Length, target).ConfigureAwait(false);
#endif
                    }

                    nextTick = now + 0.1;
                    tick = Task.Delay(TimeSpan.FromMilliseconds(100), _stop.Token);
                }

                Task completed = await Task.WhenAny(receive, tick).ConfigureAwait(false);
                if (completed != receive) {
                    continue;
                }

                UdpReceiveResult datagram = await receive.ConfigureAwait(false);
                receive = socket.ReceiveAsync();
                if (datagram.RemoteEndPoint.Port != _configuration.Port) {
                    continue;
                }

                DnsResponse response;
                try {
                    response = await DnsWire.DeserializeDnsMulticastResponse(datagram.Buffer, _debug).ConfigureAwait(false);
                }
                catch (DnsClientException exception) {
                    if (_debug) {
                        Settings.Logger.WriteDebug("Ignoring malformed mDNS browse response: " + exception.Message);
                    }

                    continue;
                }

                if (response.TransactionId != 0 || response.Status != DnsResponseCode.NoError || response.OperationCode != 0) {
                    continue;
                }

                _cache.Apply(response, Now());
                await PublishAsync(_cache.Advance(Now(), DateTimeOffset.UtcNow)).ConfigureAwait(false);
            }
        } catch (Exception exception) when (_stop.IsCancellationRequested
            && exception is OperationCanceledException or SocketException or ObjectDisposedException) {
            // Cancellation closes the socket, and the pending receive is drained below.
        } catch (Exception exception) {
            failure = exception;
        } finally {
            socket?.Dispose();
            if (receive != null) {
                try {
                    await receive.ConfigureAwait(false);
                }
                catch (SocketException) {
                }
                catch (ObjectDisposedException) {
                }
                catch (OperationCanceledException) {
                }
            }

            lock (_gate) {
                _failure = failure;
                _completed = true;
            }

            _available.Release();
        }
    }

    private async Task PublishAsync(DnsServiceBrowseEvent[] events) {
        foreach (DnsServiceBrowseEvent item in events) {
            await _slots.WaitAsync(_stop.Token).ConfigureAwait(false);
            lock (_gate)
                _events.Enqueue(item);
            _available.Release();
        }
    }

    private static double Now() => Stopwatch.GetTimestamp() / (double)Stopwatch.Frequency;
}
