using System;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace DnsClientX;

internal static partial class DnsWireResolveMulticast {
    /// <summary>Retains the packet destination and receiving interface needed to enforce the mDNS local-link boundary.</summary>
    internal readonly struct ReceivedPacket {
        internal byte[] Buffer { get; }
        internal IPEndPoint RemoteEndPoint { get; }
        internal IPAddress DestinationAddress { get; }
        internal int InterfaceIndex { get; }

        internal ReceivedPacket(byte[] buffer, IPEndPoint remoteEndPoint, IPAddress destinationAddress, int interfaceIndex) {
            Buffer = buffer;
            RemoteEndPoint = remoteEndPoint;
            DestinationAddress = destinationAddress;
            InterfaceIndex = interfaceIndex;
        }
    }

    /// <summary>Receives one datagram with packet information; the owning session cancels and drains it by closing its socket.</summary>
    internal static async Task<ReceivedPacket> ReceivePacketAsync(UdpClient client, byte[] receiveBuffer) {
        IPAddress any = client.Client.AddressFamily == AddressFamily.InterNetworkV6 ? IPAddress.IPv6Any : IPAddress.Any;
        SocketReceiveMessageFromResult received = await client.Client.ReceiveMessageFromAsync(
            new ArraySegment<byte>(receiveBuffer), SocketFlags.None, new IPEndPoint(any, 0)).ConfigureAwait(false);
        var bytes = new byte[received.ReceivedBytes];
        System.Buffer.BlockCopy(receiveBuffer, 0, bytes, 0, bytes.Length);
        return new ReceivedPacket(bytes, (IPEndPoint)received.RemoteEndPoint,
            received.PacketInformation.Address, received.PacketInformation.Interface);
    }

    /// <summary>Accepts responses on the selected link, preserving multicast from overlay prefixes and checking unicast sources.</summary>
    internal static bool IsLocalResponse(ReceivedPacket packet, IPAddress group, int port, int? selectedInterfaceIndex) {
        if (packet.RemoteEndPoint.Port != port || selectedInterfaceIndex.HasValue && packet.InterfaceIndex != selectedInterfaceIndex.Value) {
            return false;
        }

        // RFC 6762 section 11 treats multicast received on this link as local, even when peers use different IP prefixes.
        if (SameAddress(packet.DestinationAddress, group)) {
            return true;
        }

        if (IsMulticast(packet.DestinationAddress) || packet.InterfaceIndex <= 0) {
            return false;
        }

        IPAddress source = packet.RemoteEndPoint.Address;
        foreach (NetworkInterface networkInterface in NetworkInterface.GetAllNetworkInterfaces()) {
            NetworkInterfaceComponent component = source.AddressFamily == AddressFamily.InterNetworkV6
                ? NetworkInterfaceComponent.IPv6 : NetworkInterfaceComponent.IPv4;
            if (!networkInterface.Supports(component)) {
                continue;
            }

            try {
                IPInterfaceProperties properties = networkInterface.GetIPProperties();
                int? index = source.AddressFamily == AddressFamily.InterNetworkV6
                    ? properties.GetIPv6Properties()?.Index : properties.GetIPv4Properties()?.Index;
                if (index != packet.InterfaceIndex) {
                    continue;
                }

                foreach (UnicastIPAddressInformation local in properties.UnicastAddresses) {
                    if (local.Address.AddressFamily != source.AddressFamily) {
                        continue;
                    }

                    if (source.AddressFamily == AddressFamily.InterNetwork) {
                        byte[] address = source.GetAddressBytes();
                        byte[] prefix = local.Address.GetAddressBytes();
                        byte[] mask = local.IPv4Mask.GetAddressBytes();
                        bool matches = true;
                        for (int octet = 0; octet < address.Length; octet++) {
                            matches &= (address[octet] & mask[octet]) == (prefix[octet] & mask[octet]);
                        }

                        if (matches) {
                            return true;
                        }
                    }
                    else if (MatchesPrefix(source, local.Address, local.PrefixLength)) {
                        return true;
                    }
                }
            }
            catch (NetworkInformationException) {
                // An interface can disappear during a persistent session. Unknown source links are not admitted.
            }
        }

        return false;
    }

    private static bool SameAddress(IPAddress first, IPAddress second) => MatchesPrefix(first, second,
        first.AddressFamily == AddressFamily.InterNetwork ? 32 : 128);

    private static bool MatchesPrefix(IPAddress address, IPAddress prefix, int prefixLength) {
        byte[] addressBytes = address.GetAddressBytes();
        byte[] prefixBytes = prefix.GetAddressBytes();
        if (addressBytes.Length != prefixBytes.Length || prefixLength < 0 || prefixLength > addressBytes.Length * 8) {
            return false;
        }

        for (int bit = 0; bit < prefixLength; bit += 8) {
            int mask = 0xff << Math.Max(0, bit + 8 - prefixLength);
            if ((addressBytes[bit / 8] & mask) != (prefixBytes[bit / 8] & mask)) {
                return false;
            }
        }

        return true;
    }
}
