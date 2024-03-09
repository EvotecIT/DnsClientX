using System.Threading.Tasks;

namespace DnsClientX.Examples {
    public static class Program {
        public static async Task Main() {
            await DemoQuery.Example1();

            await DemoQuery.Example2();

            await DemoQuery.Example3();

            //await DemoQuery.ExampleTesting();

            await DemoRecords.Demo("evotec.pl", DnsRecordType.A, DnsEndpoint.CloudflareFamily);

            await DemoRecords.Demo("evotec.pl", DnsRecordType.TXT, DnsEndpoint.CloudflareSecurity);

            await DemoRecords.Demo("not.evotec.pl", DnsRecordType.TXT, DnsEndpoint.Quad9);

            await DemoRecords.Demo("microsoft.com", DnsRecordType.MX, DnsEndpoint.CloudflareFamily);

            await DemoRecords.Demo("microsoft.com", DnsRecordType.MX, DnsEndpoint.CloudflareWireFormat);

            await DemoByManualUrl.Example();

            await DemoByManualUrl.Example2();

            await DemoResolve.Example();

            await DemoResolveAll.Example();

            await DemoResolveFirst.Example();

            await DemoResolveParallel.Example();
        }
    }
}
