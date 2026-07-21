using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace DnsClientX {
    /// <summary>
    /// Sends RFC 6762 multicast DNS queries and collects every response received during the query window.
    /// </summary>
    internal static class DnsWireResolveMulticast {
        private const int MaxCollectedResponses = 256;
        internal static async Task<DnsResponse> ResolveWireFormatMulticast(
            string dnsServer,
            int port,
            string name,
            DnsRecordType type,
            bool requestDnsSec,
            bool validateDnsSec,
            bool debug,
            Configuration endpointConfiguration,
            CancellationToken cancellationToken) {
            DnsResponse[] responses = await ResolveWireFormatMulticastAll(
                dnsServer,
                port,
                name,
                type,
                debug,
                endpointConfiguration,
                cancellationToken).ConfigureAwait(false);

            if (responses.Length == 0) {
                var timeout = new DnsResponse {
                    Questions = [new DnsQuestion {
                        Name = name,
                        RequestFormat = DnsRequestFormat.Multicast,
                        Type = type,
                        OriginalName = name
                    }],
                    Status = DnsResponseCode.ServerFailure,
                    Error = $"No multicast DNS response was received within {endpointConfiguration.TimeOut} milliseconds."
                };
                timeout.AddServerDetails(endpointConfiguration, Transport.Multicast);
                return timeout;
            }

            return MergeResponses(responses, endpointConfiguration);
        }

        internal static async Task<DnsResponse[]> ResolveWireFormatMulticastAll(
            string dnsServer,
            int port,
            string name,
            DnsRecordType type,
            bool debug,
            Configuration endpointConfiguration,
            CancellationToken cancellationToken) {
            if (string.IsNullOrWhiteSpace(name)) {
                throw new ArgumentNullException(nameof(name));
            }
            if (!IPAddress.TryParse(dnsServer, out IPAddress? multicastAddress) || !IsMulticast(multicastAddress)) {
                throw new ArgumentException("The multicast DNS server must be a multicast IP address.", nameof(dnsServer));
            }

            var query = new DnsMessage(
                name,
                type,
                new DnsMessageOptions(
                    RequestDnsSec: false,
                    EnableEdns: false,
                    RecursionDesired: false,
                    TransactionId: 0));
            byte[] queryBytes = query.SerializeDnsWireFormat();
            if (debug) {
                Settings.Logger.WriteDebug($"Sending mDNS query {name} {type} to {multicastAddress}%{endpointConfiguration.MulticastInterfaceIndex?.ToString() ?? "default"}");
            }

            using var client = CreateClient(multicastAddress, endpointConfiguration.MulticastInterfaceIndex);
            JoinGroup(client, multicastAddress, endpointConfiguration.MulticastInterfaceIndex);
            try {
                var target = new IPEndPoint(multicastAddress, port);
#if NET5_0_OR_GREATER
                await client.SendAsync(queryBytes, target, cancellationToken).ConfigureAwait(false);
#else
                await client.SendAsync(queryBytes, queryBytes.Length, target).ConfigureAwait(false);
#endif
                return await CollectResponses(
                    client,
                    query,
                    debug,
                    endpointConfiguration,
                    cancellationToken).ConfigureAwait(false);
            } finally {
                try {
                    client.DropMulticastGroup(multicastAddress);
                } catch (SocketException) {
                    // The socket may already have left the group while it was being disposed.
                }
            }
        }

        private static async Task<DnsResponse[]> CollectResponses(
            UdpClient client,
            DnsMessage query,
            bool debug,
            Configuration configuration,
            CancellationToken cancellationToken) {
            var responses = new List<DnsResponse>();
            var packets = new HashSet<string>(StringComparer.Ordinal);
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            deadline.CancelAfter(Math.Max(1, configuration.TimeOut));

            while (!deadline.IsCancellationRequested && responses.Count < MaxCollectedResponses) {
                try {
                    UdpReceiveResult received = await Receive(client, deadline.Token).ConfigureAwait(false);
                    DnsResponse response = await DnsWire.DeserializeDnsWireFormat(null, debug, received.Buffer).ConfigureAwait(false);
                    if (!response.IsResponse
                        || response.TransactionId != query.TransactionId
                        || !IsRelevantResponse(response, query.Name, query.Type)) {
                        continue;
                    }
                    string packetKey = Convert.ToBase64String(received.Buffer);
                    if (!packets.Add(packetKey)) continue;

                    RetainRelevantRecords(response, query.Name, query.Type);
                    response.AddServerDetails(configuration, Transport.Multicast);
                    responses.Add(response);
                } catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested) {
                    break;
                } catch (TimeoutException) when (!cancellationToken.IsCancellationRequested) {
                    break;
                } catch (DnsClientException ex) {
                    if (debug) {
                        Settings.Logger.WriteDebug($"Ignoring malformed mDNS response: {ex.Message}");
                    }
                }
            }

            cancellationToken.ThrowIfCancellationRequested();
            return responses.ToArray();
        }

        internal static bool IsRelevantResponse(DnsResponse response, string name, DnsRecordType type) {
            return response.Status == DnsResponseCode.NoError
                && ClientX.HasRequestedAnswer(response, DnsWireNameCodec.Normalize(name), type);
        }

        internal static void RetainRelevantRecords(DnsResponse response, string name, DnsRecordType type) {
            const int maxRelatedNames = 512;
            var relatedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase) {
                NormalizeOwner(name)
            };
            DnsAnswer[] allRecords = (response.Answers ?? Array.Empty<DnsAnswer>())
                .Concat(response.Authorities ?? Array.Empty<DnsAnswer>())
                .Concat(response.Additional ?? Array.Empty<DnsAnswer>())
                .ToArray();

            bool changed;
            do {
                changed = false;
                string[] currentNames = relatedNames.ToArray();
                foreach (string currentName in currentNames) {
                    string? aliasTarget = ClientX.FindAliasTarget(response, currentName);
                    if (aliasTarget != null && relatedNames.Count < maxRelatedNames) {
                        changed |= relatedNames.Add(NormalizeOwner(aliasTarget));
                    }
                }
                foreach (DnsAnswer record in allRecords) {
                    if (relatedNames.Count >= maxRelatedNames) break;
                    if (!IsRelatedRecord(record, relatedNames)) continue;
                    string? target = record.Type switch {
                        DnsRecordType.CNAME or DnsRecordType.DNAME or DnsRecordType.PTR => record.Data,
                        DnsRecordType.SRV => LastPresentationField(record.Data),
                        _ => null
                    };
                    if (!string.IsNullOrWhiteSpace(target)) {
                        changed |= relatedNames.Add(NormalizeOwner(target!));
                    }
                }
            } while (changed && relatedNames.Count < maxRelatedNames);

            DnsAnswer[] Filter(DnsAnswer[]? records) => (records ?? Array.Empty<DnsAnswer>())
                .Where(record => IsRelatedRecord(record, relatedNames))
                .ToArray();
            response.Answers = Filter(response.Answers);
            response.Authorities = Filter(response.Authorities);
            response.Additional = Filter(response.Additional);
        }

        private static string NormalizeOwner(string name) => name.Trim().TrimEnd('.');

        private static bool IsRelatedRecord(DnsAnswer record, HashSet<string> relatedNames) {
            string owner = NormalizeOwner(record.Name);
            if (relatedNames.Contains(owner)) return true;
            return record.Type == DnsRecordType.DNAME
                && relatedNames.Any(candidate => candidate.EndsWith("." + owner, StringComparison.OrdinalIgnoreCase));
        }

        private static string? LastPresentationField(string data) {
            if (string.IsNullOrWhiteSpace(data)) return null;
            string[] fields = data.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            return fields.Length == 0 ? null : fields[fields.Length - 1];
        }

        private static async Task<UdpReceiveResult> Receive(UdpClient client, CancellationToken cancellationToken) {
#if NET5_0_OR_GREATER
            return await client.ReceiveAsync(cancellationToken).ConfigureAwait(false);
#else
            Task<UdpReceiveResult> receiveTask = client.ReceiveAsync();
            Task completed = await Task.WhenAny(receiveTask, Task.Delay(Timeout.Infinite, cancellationToken)).ConfigureAwait(false);
            if (completed == receiveTask) {
                return await receiveTask.ConfigureAwait(false);
            }

            ObserveFault(receiveTask);
            cancellationToken.ThrowIfCancellationRequested();
            throw new TimeoutException("The multicast DNS collection window elapsed.");
#endif
        }

        private static UdpClient CreateClient(IPAddress multicastAddress, int? interfaceIndex) {
            var client = new UdpClient(multicastAddress.AddressFamily) {
                ExclusiveAddressUse = false
            };
            client.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            IPAddress bindAddress = FindInterfaceAddress(multicastAddress.AddressFamily, interfaceIndex)
                ?? (multicastAddress.AddressFamily == AddressFamily.InterNetworkV6 ? IPAddress.IPv6Any : IPAddress.Any);
            client.Client.Bind(new IPEndPoint(bindAddress, 0));
            return client;
        }

        private static void JoinGroup(UdpClient client, IPAddress multicastAddress, int? interfaceIndex) {
            if (interfaceIndex.HasValue) {
                if (multicastAddress.AddressFamily == AddressFamily.InterNetworkV6) {
                    client.JoinMulticastGroup(interfaceIndex.Value, multicastAddress);
                    return;
                }

                IPAddress? localAddress = FindInterfaceAddress(AddressFamily.InterNetwork, interfaceIndex);
                if (localAddress != null) {
                    client.JoinMulticastGroup(multicastAddress, localAddress);
                    return;
                }
            }

#if NET5_0_OR_GREATER
            client.JoinMulticastGroup(multicastAddress);
#else
            client.JoinMulticastGroup(multicastAddress, 50);
#endif
        }

        private static IPAddress? FindInterfaceAddress(AddressFamily family, int? interfaceIndex) {
            if (!interfaceIndex.HasValue) {
                return null;
            }

            foreach (NetworkInterface networkInterface in NetworkInterface.GetAllNetworkInterfaces()) {
                IPInterfaceProperties properties = networkInterface.GetIPProperties();
                int? index = family == AddressFamily.InterNetwork
                    ? properties.GetIPv4Properties()?.Index
                    : properties.GetIPv6Properties()?.Index;
                if (index != interfaceIndex) {
                    continue;
                }

                return properties.UnicastAddresses
                    .Select(item => item.Address)
                    .FirstOrDefault(address => address.AddressFamily == family);
            }

            return null;
        }

        private static bool IsMulticast(IPAddress address) {
            byte[] bytes = address.GetAddressBytes();
            return address.AddressFamily == AddressFamily.InterNetwork
                ? bytes[0] >= 224 && bytes[0] <= 239
                : address.AddressFamily == AddressFamily.InterNetworkV6 && bytes[0] == 0xff;
        }

        private static DnsResponse MergeResponses(DnsResponse[] responses, Configuration configuration) {
            DnsResponse merged = responses[0];
            merged.Answers = responses.SelectMany(response => response.Answers ?? Array.Empty<DnsAnswer>()).Distinct().ToArray();
            merged.Authorities = responses.SelectMany(response => response.Authorities ?? Array.Empty<DnsAnswer>()).Distinct().ToArray();
            merged.Additional = responses.SelectMany(response => response.Additional ?? Array.Empty<DnsAnswer>()).Distinct().ToArray();
            merged.RefreshDerivedData(configuration.Port, DnsRequestFormat.Multicast);
            return merged;
        }

        private static void ObserveFault(Task task) {
            _ = task.ContinueWith(
                completed => _ = completed.Exception,
                TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously);
        }
    }
}
