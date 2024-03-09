using System;
using System.Collections.Generic;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Text;
using System.Threading.Tasks;

namespace DnsClientX {
    /// <summary>
    ///
    /// TODO - This method is not yet working correctly
    /// </summary>
    internal static class DnsWireResolveDot {
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
                Console.WriteLine($"Sending combined query: {BitConverter.ToString(combinedQueryBytes)}");
            }


            if (debug) {
                // Print the DNS wire format bytes to the console
                Console.WriteLine("Query    Name: " + name + " type: " + type);
                Console.WriteLine($"Sending query: {BitConverter.ToString(queryBytes)}");
            }

            string dnsServer = "1.1.1.1";
            int port = 853;
            var sslValidationCallback = new RemoteCertificateValidationCallback((sender, certificate, chain, sslPolicyErrors) => true);
            using (var client = new TcpClient(dnsServer, port))
            using (var sslStream = new SslStream(client.GetStream(), false, sslValidationCallback)) {
                //await sslStream.AuthenticateAsClientAsync(dnsServer);
                await sslStream.AuthenticateAsClientAsync(dnsServer, null, SslProtocols.Tls12, false);

                await sslStream.WriteAsync(combinedQueryBytes, 0, combinedQueryBytes.Length);
                await sslStream.FlushAsync();

                // Read response
                var buffer = new byte[4096];
                int bytesRead = await sslStream.ReadAsync(buffer, 0, buffer.Length);
                byte[] responseBytes = new byte[bytesRead];
                Array.Copy(buffer, responseBytes, bytesRead);

                // Deserialize the response from DNS wire format
                var response = await DnsWire.DeserializeDnsWireFormat(null, debug, responseBytes);
                return response;
            }
        }
    }
}
