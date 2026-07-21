using System;
using System.Net;
using System.Net.Sockets;

namespace DnsClientX {
    internal static class SocketBinding {
        internal static void Bind(Socket socket, IPEndPoint? localEndPoint, AddressFamily remoteFamily) {
            if (localEndPoint == null) {
                return;
            }
            if (localEndPoint.AddressFamily != remoteFamily) {
                throw new ArgumentException(
                    $"Local endpoint family {localEndPoint.AddressFamily} does not match remote family {remoteFamily}.",
                    nameof(localEndPoint));
            }

            socket.Bind(new IPEndPoint(localEndPoint.Address, localEndPoint.Port));
        }
    }
}
