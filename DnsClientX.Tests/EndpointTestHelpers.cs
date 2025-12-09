using System;
using System.Linq;
using DnsClientX;

namespace DnsClientX.Tests {
    internal static class EndpointTestHelpers {
        internal static DnsEndpoint[] AllEndpoints() =>
            Enum.GetValues(typeof(DnsEndpoint)).Cast<DnsEndpoint>().ToArray();
    }
}
