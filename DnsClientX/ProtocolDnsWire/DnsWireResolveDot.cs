using System;
using System.Collections.Generic;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Text;
using System.Threading.Tasks;

namespace DnsClientX {
    internal static class DnsWireResolveDot {
        /// <summary>
        ///
        /// TODO - This method is not yet working correctly
        /// </summary>
        /// <param name="name">The name.</param>
        /// <param name="type">The type.</param>
        /// <param name="requestDnsSec">if set to <c>true</c> [request DNS sec].</param>
        /// <param name="validateDnsSec">if set to <c>true</c> [validate DNS sec].</param>
        /// <param name="debug">if set to <c>true</c> [debug].</param>
        /// <returns></returns>
        /// <exception cref="System.ArgumentNullException">name - Name is null or empty.</exception>
        /// <exception cref="System.Exception">
        /// Failed to read the length prefix of the response.
        /// or
        /// The stream was closed before the entire response could be read.
        /// </exception>
        internal static async Task<DnsResponse> ResolveWireFormatDoT(string name, DnsRecordType type, bool requestDnsSec, bool validateDnsSec, bool debug = false) {
            if (string.IsNullOrEmpty(name)) throw new ArgumentNullException(nameof(name), "Name is null or empty.");

            var query = new DnsMessage(name, type, requestDnsSec);
            var queryBytes = query.SerializeDnsWireFormat();

            // Calculate the length prefix for the query
            var lengthPrefix = BitConverter.GetBytes((ushort)queryBytes.Length);
            if (BitConverter.IsLittleEndian) {
                Array.Reverse(lengthPrefix); // Ensure big-endian order
            }

            // Combine the length prefix and the query bytes
            var combinedQueryBytes = new byte[lengthPrefix.Length + queryBytes.Length];
            Buffer.BlockCopy(lengthPrefix, 0, combinedQueryBytes, 0, lengthPrefix.Length);
            Buffer.BlockCopy(queryBytes, 0, combinedQueryBytes, lengthPrefix.Length, queryBytes.Length);

            if (debug) {
                // Print the combined DNS query bytes to the console
                Console.WriteLine($"Query Name: " + name + " type: " + type);
                Console.WriteLine($"Query before combination: {BitConverter.ToString(queryBytes)}");
                Console.WriteLine($"Sending combined query: {BitConverter.ToString(combinedQueryBytes)}");
            }

            string dnsServer = "1.1.1.1"; // Cloudflare's DoT endpoint
            int port = 853;

            using (var client = new TcpClient(dnsServer, port))
            using (var sslStream = new SslStream(client.GetStream(), false, (sender, certificate, chain, sslPolicyErrors) => true)) {
                await sslStream.AuthenticateAsClientAsync(dnsServer, null, SslProtocols.Tls12, false);

                await sslStream.WriteAsync(combinedQueryBytes, 0, combinedQueryBytes.Length);
                await sslStream.FlushAsync();

                // Prepare to read the response with handling for length prefix
                var lengthPrefixBuffer = new byte[2];
                int prefixBytesRead = await sslStream.ReadAsync(lengthPrefixBuffer, 0, 2);
                if (prefixBytesRead != 2) {
                    throw new Exception("Failed to read the length prefix of the response.");
                }
                int responseLength = (lengthPrefixBuffer[0] << 8) + lengthPrefixBuffer[1]; // Calculate total response length

                var responseBuffer = new byte[responseLength];
                int totalBytesRead = 0;
                while (totalBytesRead < responseLength) {
                    int bytesRead = await sslStream.ReadAsync(responseBuffer, totalBytesRead, responseLength - totalBytesRead);
                    if (bytesRead == 0) {
                        throw new Exception("The stream was closed before the entire response could be read.");
                    }
                    totalBytesRead += bytesRead;
                }

                // At this point, responseBuffer contains the full DNS response
                if (debug) {

                    Console.WriteLine($"Received response: {BitConverter.ToString(responseBuffer)}");
                }

                // Deserialize the response from DNS wire format
                var response = await DnsWire.DeserializeDnsWireFormat(null, debug, responseBuffer);
                return response;
            }
        }
    }
}
