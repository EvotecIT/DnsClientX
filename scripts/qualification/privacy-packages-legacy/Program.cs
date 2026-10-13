using System;
using System.Net;
using DnsClientX.DnsCrypt;

internal static class Program {
    private static void Main() {
        using (var provider = new DnsCryptClient(new DnsCryptOptions(
            new IPEndPoint(IPAddress.Loopback, 1), "2.dnscrypt-cert.example", new byte[32]))) {
            if (provider.CreateConfiguration().WireTransport != provider) {
                throw new Exception("The package-only provider did not register with the core.");
            }
            Console.WriteLine("PASS non-SDK packages.config consumer; native libsodium "
                + DnsCryptClient.NativeLibraryVersion + " loads.");
        }
    }
}
