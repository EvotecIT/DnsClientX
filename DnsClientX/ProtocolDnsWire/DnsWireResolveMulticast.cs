using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace DnsClientX {
    /// <summary>
    /// Provides methods for sending DNS queries over multicast UDP.
    /// </summary>
    internal static class DnsWireResolveMulticast {
        internal static async Task<DnsResponse> ResolveWireFormatMulticast(string dnsServer, int port, string name, DnsRecordType type, bool requestDnsSec, bool validateDnsSec, bool debug, Configuration endpointConfiguration, CancellationToken cancellationToken) {
            if (string.IsNullOrEmpty(name)) throw new ArgumentNullException(nameof(name), "Name is null or empty.");

            var query = DnsWireQueryBuilder.BuildQuery(name, type, requestDnsSec, endpointConfiguration);
            var queryBytes = query.SerializeDnsWireFormat();

            if (debug) {
                Settings.Logger.WriteDebug($"Query Name: " + name + " type: " + type);
                Settings.Logger.WriteDebug($"Sending query: {BitConverter.ToString(queryBytes)}");
            }

            using var udpClient = new UdpClient(AddressFamily.InterNetwork);
            try {
                udpClient.ExclusiveAddressUse = false;
                udpClient.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                udpClient.Client.Bind(new IPEndPoint(IPAddress.Any, 0));
                var multicastAddress = IPAddress.Parse(dnsServer);
#if NET5_0_OR_GREATER
                udpClient.JoinMulticastGroup(multicastAddress);
#else
                udpClient.JoinMulticastGroup(multicastAddress, 50);
#endif
                var serverEndpoint = new IPEndPoint(multicastAddress, port);
#if NET5_0_OR_GREATER
                await udpClient.SendAsync(queryBytes, serverEndpoint, cancellationToken).ConfigureAwait(false);
#else
                await udpClient.SendAsync(queryBytes, queryBytes.Length, serverEndpoint).ConfigureAwait(false);
#endif
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                cts.CancelAfter(endpointConfiguration.TimeOut);
#if NET5_0_OR_GREATER
                try {
                    UdpReceiveResult response = await udpClient.ReceiveAsync(cts.Token).ConfigureAwait(false);
                    var responseBuffer = response.Buffer;
                    var responseParsed = await DnsWire.DeserializeDnsWireFormat(null, debug, responseBuffer).ConfigureAwait(false);
                    responseParsed.AddServerDetails(endpointConfiguration);
                    return responseParsed;
                } catch (OperationCanceledException) {
                    throw new TimeoutException("The UDP multicast query timed out.");
                }
#else
                var responseTask = udpClient.ReceiveAsync();
                var completedTask = await Task.WhenAny(responseTask, Task.Delay(Timeout.Infinite, cts.Token)).ConfigureAwait(false);
                if (completedTask == responseTask) {
                    var responseBuffer = responseTask.Result.Buffer;
                    var responseParsed = await DnsWire.DeserializeDnsWireFormat(null, debug, responseBuffer).ConfigureAwait(false);
                    responseParsed.AddServerDetails(endpointConfiguration);
                    return responseParsed;
                }
                ObserveFault(responseTask);
                throw new TimeoutException("The UDP multicast query timed out.");
#endif
            } catch (Exception ex) {
                DnsResponseCode responseCode = ex is TimeoutException ? DnsResponseCode.ServerFailure : DnsResponseCode.Refused;
                DnsResponse response = new DnsResponse {
                    Questions = [new DnsQuestion { Name = name, RequestFormat = DnsRequestFormat.Multicast, Type = type, OriginalName = name }],
                    Status = responseCode
                };
                response.AddServerDetails(endpointConfiguration);
                response.Error = $"Failed to query type {type} of \"{name}\" => {ex.Message + " " + ex.InnerException?.Message}";
                return response;
            } finally {
                try {
                    udpClient.DropMulticastGroup(IPAddress.Parse(dnsServer));
                } catch {
                    // ignore cleanup errors
                }
            }
        }

        private static void ObserveFault(Task task) {
            if (task == null) {
                return;
            }

            _ = task.ContinueWith(
                t => _ = t.Exception,
                TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously);
        }
    }
}
