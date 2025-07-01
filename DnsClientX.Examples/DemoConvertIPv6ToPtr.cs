using System;

namespace DnsClientX.Examples {
    internal class DemoConvertIPv6ToPtr {
        public static void Example() {
            const string ipv6 = "2001:db8::1";
            string ptr = ClientX.ConvertIPv6ToPtr(ipv6);
            Settings.Logger.WriteInformation($"{ipv6} => {ptr}");
        }
    }
}
