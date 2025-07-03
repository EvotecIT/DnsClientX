using System;

namespace DnsClientX {
    internal class DnsCryptCertificate {
        public byte[] ResolverPublicKey { get; set; }
        public byte[] ClientMagic { get; set; }
    }
}
