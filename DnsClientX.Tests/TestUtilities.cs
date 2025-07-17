using System.Net;
using System.Net.Sockets;

namespace DnsClientX.Tests {
    internal static class TestUtilities {
        public static int GetFreePort() {
            TcpListener listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            int port = ((IPEndPoint)listener.LocalEndpoint).Port;
            listener.Stop();
            return port;
        }
    }
}
