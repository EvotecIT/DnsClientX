using System;
using System.Net.Sockets;
using System.Net;
using System.Threading.Tasks;

namespace DnsClientX {
    internal class DnsWireResolveUdp {
        /// <summary>
        /// Sends a DNS query in wire format using DNS over UDP (53) and returns the response.
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
        internal static async Task<DnsResponse> ResolveWireFormatUdp(string dnsServer, int port, string name, DnsRecordType type, bool requestDnsSec, bool validateDnsSec, bool debug, Configuration endpointConfiguration) {
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

            // Send the DNS query over UDP and receive the response
            var responseBuffer = await SendQueryOverUdp(queryBytes, dnsServer, port);

            // Deserialize the response from DNS wire format
            var response = await DnsWire.DeserializeDnsWireFormat(null, debug, responseBuffer);
            if (response.IsTruncated) {
                // If the response is truncated, retry the query over TCP
                response = await DnsWireResolveTcp.ResolveWireFormatTcp(dnsServer, port, name, type, requestDnsSec, validateDnsSec, debug, endpointConfiguration);
            }
            response.AddServerDetails(endpointConfiguration);
            return response;
        }

        /// <summary>
        /// Sends a DNS query over UDP and returns the response.
        /// </summary>
        /// <param name="query"></param>
        /// <param name="dnsServer"></param>
        /// <param name="port"></param>
        /// <returns></returns>
        private static async Task<byte[]> SendQueryOverUdp(byte[] query, string dnsServer, int port) {
            using (var udpClient = new UdpClient()) {
                // Set the server IP address and port number
                var serverEndpoint = new IPEndPoint(IPAddress.Parse(dnsServer), port);

                // Send the query
                await udpClient.SendAsync(query, query.Length, serverEndpoint);

                // Receive the response
                var response = await udpClient.ReceiveAsync();
                return response.Buffer;
            }
        }
    }
}
