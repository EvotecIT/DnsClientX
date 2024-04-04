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
        /// Sends a DNS query in wire format using DNS over TLS (DoT) and returns the response.
        /// </summary>
        /// <param name="dnsServer"></param>
        /// <param name="port"></param>
        /// <param name="name">The name.</param>
        /// <param name="type">The type.</param>
        /// <param name="requestDnsSec">if set to <c>true</c> [request DNS sec].</param>
        /// <param name="validateDnsSec">if set to <c>true</c> [validate DNS sec].</param>
        /// <param name="debug">if set to <c>true</c> [debug].</param>
        /// <param name="endpointConfiguration"></param>
        /// <returns></returns>
        /// <exception cref="System.ArgumentNullException">name - Name is null or empty.</exception>
        /// <exception cref="System.Exception">
        /// Failed to read the length prefix of the response.
        /// or
        /// The stream was closed before the entire response could be read.
        /// </exception>
        internal static async Task<DnsResponse> ResolveWireFormatDoT(string dnsServer, int port, string name, DnsRecordType type, bool requestDnsSec, bool validateDnsSec, bool debug, Configuration endpointConfiguration) {
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

            // Create a new TCP client and connect to the DNS server
            var client = new TcpClient(dnsServer, port);

            // Create a new SSL stream for the secure connection
            //var sslStream = new SslStream(client.GetStream(), false, (sender, certificate, chain, sslPolicyErrors) => true);

            var sslStream = new SslStream(client.GetStream(), false, (sender, certificate, chain, sslPolicyErrors) => {
                //Console.WriteLine($"SSL policy errors: {sslPolicyErrors}");
                return true; // Always accept the certificate for now
            });


            // Authenticate the client using the DNS server's name and the TLS protocol
            await sslStream.AuthenticateAsClientAsync(dnsServer, null, SslProtocols.Tls12, false);

            // Write the combined query bytes to the SSL stream and flush it
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

            // Deserialize the response from DNS wire format
            var response = await DnsWire.DeserializeDnsWireFormat(null, debug, responseBuffer);
            response.AddServerDetails(endpointConfiguration);
            // Close the SSL stream and the TCP client
            sslStream.Close();
            client.Close();

            return response;
        }
    }
}
