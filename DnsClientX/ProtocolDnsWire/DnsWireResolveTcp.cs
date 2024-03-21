using System;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace DnsClientX {
    internal class DnsWireResolveTcp {
        internal static async Task<DnsResponse> ResolveWireFormatTcp(string dnsServer, int port, string name, DnsRecordType type, bool requestDnsSec, bool validateDnsSec, bool debug = false) {
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

            //string dnsServer = "8.8.8.8"; // Google's DNS endpoint
            //int port = 53;

            // Send the DNS query over UDP and receive the response
            var responseBuffer = await SendQueryOverTcp(queryBytes, dnsServer, port);

            // At this point, responseBuffer contains the full DNS response
            if (debug) {
                Console.WriteLine($"Received response: {BitConverter.ToString(responseBuffer)}");
            }

            // Deserialize the response from DNS wire format
            var response = await DnsWire.DeserializeDnsWireFormat(null, debug, responseBuffer);

            return response;
        }

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
                await stream.ReadAsync(lengthBytes, 0, lengthBytes.Length);
                if (BitConverter.IsLittleEndian) {
                    Array.Reverse(lengthBytes); // Ensure big-endian order
                }
                var responseLength = BitConverter.ToUInt16(lengthBytes, 0);

                // Read the response
                var responseBuffer = new byte[responseLength];
                await stream.ReadAsync(responseBuffer, 0, responseBuffer.Length);

                return responseBuffer;
            }
        }
    }
}
