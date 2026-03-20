using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace DnsClientX.Tests {
    internal static class TestUtilities {
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
