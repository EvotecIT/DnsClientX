using System;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace DnsClientX {
    internal class DnsWireResolveTcp {
        /// <summary>
        /// Sends a DNS query in wire format using DNS over TCP (53) and returns the response.
        /// </summary>
        /// <param name="dnsServer"></param>
        /// <param name="port"></param>
        /// <param name="name"></param>
        /// <param name="type"></param>
        /// <param name="requestDnsSec"></param>
        /// <param name="validateDnsSec"></param>
        /// <param name="debug"></param>
        /// <param name="endpointConfiguration">Provide configuration so it can be added to Question for display purposes</param>
        /// <returns></returns>
        /// <exception cref="ArgumentNullException"></exception>
        internal static async Task<DnsResponse> ResolveWireFormatTcp(string dnsServer, int port, string name, DnsRecordType type, bool requestDnsSec, bool validateDnsSec, bool debug, Configuration endpointConfiguration) {
            if (string.IsNullOrEmpty(name)) throw new ArgumentNullException(nameof(name), "Name is null or empty.");

            var query = new DnsMessage(name, type, requestDnsSec);
            var queryBytes = query.SerializeDnsWireFormat();

            if (debug) {
                // Print the DNS query bytes to the console
                Console.WriteLine($"Query Name: " + name + " type: " + type);
                Console.WriteLine($"Sending query: {BitConverter.ToString(queryBytes)}");

                Console.WriteLine($"Transaction ID: {BitConverter.ToString(queryBytes, 0, 2)}");
                Console.WriteLine($"Flags: {BitConverter.ToString(queryBytes, 2, 2)}");
                Console.WriteLine($"Question count: {BitConverter.ToString(queryBytes, 4, 2)}");
                Console.WriteLine($"Answer count: {BitConverter.ToString(queryBytes, 6, 2)}");
                Console.WriteLine($"Authority records count: {BitConverter.ToString(queryBytes, 8, 2)}");
                Console.WriteLine($"Additional records count: {BitConverter.ToString(queryBytes, 10, 2)}");
                Console.WriteLine($"Question name: {BitConverter.ToString(queryBytes, 12, queryBytes.Length - 12 - 4)}");
                Console.WriteLine($"Question type: {BitConverter.ToString(queryBytes, queryBytes.Length - 4, 2)}");
                Console.WriteLine($"Question class: {BitConverter.ToString(queryBytes, queryBytes.Length - 2, 2)}");
            }

            try {
                // Send the DNS query over TCP and receive the response
                var responseBuffer = await SendQueryOverTcp(queryBytes, dnsServer, port);

                // Deserialize the response from DNS wire format
                var response = await DnsWire.DeserializeDnsWireFormat(null, debug, responseBuffer);
                response.AddServerDetails(endpointConfiguration);
                return response;
            } catch (Exception ex) {
                DnsResponseCode responseCode;
                if (ex is SocketException) {
                    responseCode = DnsResponseCode.Refused;
                } else if (ex is TimeoutException) {
                    responseCode = DnsResponseCode.ServerFailure;
                } else {
                    responseCode = DnsResponseCode.ServerFailure;
                }

                DnsResponse response = new DnsResponse {
                    Questions = [
                        new DnsQuestion() {
                            Name = name,
                            RequestFormat = DnsRequestFormat.DnsOverTCP,
                            Type = type,
                            OriginalName = name
                        }
                    ],
                    Status = responseCode
                };
                response.AddServerDetails(endpointConfiguration);
                response.Error = $"Failed to query type {type} of \"{name}\" => {ex.Message + " " + ex.InnerException?.Message}";
                return response;
            }
        }

        /// <summary>
        /// Sends a DNS query over TCP and returns the response.
        /// </summary>
        /// <param name="query"></param>
        /// <param name="dnsServer"></param>
        /// <param name="port"></param>
        /// <returns></returns>
        private static async Task<byte[]> SendQueryOverTcp(byte[] query, string dnsServer, int port) {
            using (var tcpClient = new TcpClient()) {
                // Connect to the server
                await tcpClient.ConnectAsync(dnsServer, port);

                // Get the stream
                var stream = tcpClient.GetStream();

                // Write the length of the query as a 16-bit big-endian integer
                var lengthBytes = BitConverter.GetBytes((ushort)query.Length);
                if (BitConverter.IsLittleEndian) {
                    Array.Reverse(lengthBytes); // Ensure big-endian order
                }
                await stream.WriteAsync(lengthBytes, 0, lengthBytes.Length);

                // Write the query
                await stream.WriteAsync(query, 0, query.Length);

                // Read the length of the response
                lengthBytes = new byte[2];
                await DnsWire.ReadExactAsync(stream, lengthBytes, 0, lengthBytes.Length);
                if (BitConverter.IsLittleEndian) {
                    Array.Reverse(lengthBytes); // Ensure big-endian order
                }
                var responseLength = BitConverter.ToUInt16(lengthBytes, 0);

                // Read the response
                var responseBuffer = new byte[responseLength];
                await DnsWire.ReadExactAsync(stream, responseBuffer, 0, responseBuffer.Length);

                return responseBuffer;
            }
        }
    }
}
