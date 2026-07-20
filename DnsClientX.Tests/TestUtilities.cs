using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace DnsClientX.Tests {
    internal static class TestUtilities {
        public static byte[] CreateResponseFromQuery(byte[] query, ushort flags = 0x8180) {
            if (query == null || query.Length < 17) throw new System.ArgumentException("A complete one-question DNS query is required.", nameof(query));
            int offset = 12;
            while (true) {
                if (offset >= query.Length) throw new System.ArgumentException("Query name is truncated.", nameof(query));
                int length = query[offset++];
                if (length == 0) break;
                if (length > 63 || offset + length > query.Length) throw new System.ArgumentException("Query name is malformed.", nameof(query));
                offset += length;
            }
            int questionEnd = checked(offset + 4);
            if (questionEnd > query.Length) throw new System.ArgumentException("Query question is truncated.", nameof(query));
            var response = new byte[questionEnd];
            response[0] = query[0];
            response[1] = query[1];
            response[2] = (byte)(flags >> 8);
            response[3] = (byte)flags;
            response[4] = 0;
            response[5] = 1;
            System.Buffer.BlockCopy(query, 12, response, 12, questionEnd - 12);
            return response;
        }

        public static async Task<byte[]> ReadDnsQueryAsync(HttpRequestMessage request) {
            if (request.Method == HttpMethod.Post) {
                if (request.Content == null) throw new ArgumentException("DNS POST request is missing content.", nameof(request));
                return await request.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
            }

            string query = request.RequestUri?.Query ?? string.Empty;
            foreach (string item in query.TrimStart('?').Split('&')) {
                int separator = item.IndexOf('=');
                if (separator <= 0 || !string.Equals(item.Substring(0, separator), "dns", StringComparison.Ordinal)) continue;
                string encoded = Uri.UnescapeDataString(item.Substring(separator + 1)).Replace('-', '+').Replace('_', '/');
                switch (encoded.Length % 4) {
                    case 2: encoded += "=="; break;
                    case 3: encoded += "="; break;
                }
                return Convert.FromBase64String(encoded);
            }

            throw new ArgumentException("DNS GET request is missing the dns query parameter.", nameof(request));
        }

        public static byte[] CreateGrpcResponseFromRequest(byte[] request) {
            if (request == null || request.Length < 6 || request[0] != 0) {
                throw new ArgumentException("A complete uncompressed gRPC DNS request is required.", nameof(request));
            }

            int length = (request[1] << 24) | (request[2] << 16) | (request[3] << 8) | request[4];
            if (length < 1 || length != request.Length - 5) {
                throw new ArgumentException("The gRPC DNS request length prefix is invalid.", nameof(request));
            }

            var query = new byte[length];
            Buffer.BlockCopy(request, 5, query, 0, length);
            byte[] dnsResponse = CreateResponseFromQuery(query);
            var response = new byte[dnsResponse.Length + 5];
            response[0] = 0;
            response[1] = (byte)(dnsResponse.Length >> 24);
            response[2] = (byte)(dnsResponse.Length >> 16);
            response[3] = (byte)(dnsResponse.Length >> 8);
            response[4] = (byte)dnsResponse.Length;
            Buffer.BlockCopy(dnsResponse, 0, response, 5, dnsResponse.Length);
            return response;
        }

        public static int GetFreePort() {
            return GetFreeTcpPort();
        }

        public static int GetFreeTcpPort() {
            TcpListener listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            int port = ((IPEndPoint)listener.LocalEndpoint).Port;
            listener.Stop();
            return port;
        }

        public static int GetFreeUdpPort() {
            using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            socket.Bind(new IPEndPoint(IPAddress.Loopback, 0));
            return ((IPEndPoint)socket.LocalEndPoint!).Port;
        }

        public static async Task ReadExactlyAsync(Stream stream, byte[] buffer, int length, CancellationToken token) {
#if NET6_0_OR_GREATER
            await stream.ReadExactlyAsync(buffer.AsMemory(0, length), token);
#else
            int offset = 0;
            while (offset < length) {
                int bytesRead = await stream.ReadAsync(buffer, offset, length - offset, token);
                if (bytesRead == 0) {
                    throw new EndOfStreamException();
                }

                offset += bytesRead;
            }
#endif
        }
    }
}
