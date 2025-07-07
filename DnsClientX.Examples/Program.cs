using System.Threading.Tasks;

namespace DnsClientX.Examples {
    /// <summary>
    /// Entry point for executing various example scenarios.
    /// </summary>
    public static class Program {
        /// <summary>
        /// Runs the example routines sequentially.
        /// </summary>
        public static async Task Main() {
            // await DemoQuery.ExamplePTR();
            // await DemoQuery.ExamplePTR1();
            // await DemoQuery.ExamplePTR2();
            // await DemoQuery.ExamplePTR3();

            await DemoDnsAnswer.ExampleDnsAnswerParsing();
            await DemoServiceDiscovery.Example();

            //await DemoQuery.ExampleTXTAll();
            //await DemoQuery.ExampleTXT();
            //await DemoQuery.ExampleSPF();
            //await DemoQuery.ExampleTXTQuad();
            //await DemoQuery.ExampleSPFQuad();
            return;

            // The lines below are retained for reference and intentionally
            // disabled to avoid unreachable code warnings.
            // await ConvertToDnsClient.ExampleConvertToDnsClientFromX();
            // await ConvertToDnsClient.ExampleConvertFromDnsClientToX();
            // await DemoQuery.ExampleTesting();
            // await DemoByManualUrl.ExampleTesting();
            // await DemoByManualUrl.ExampleTestingHttpOverPost();
            // await DemoByManualUrl.ExampleTestingUdp();
            // await DemoByManualUrl.ExampleTestingTcp();
            // await DemoByManualUrl.ExampleGoogle();

            // await GetSystemDns.Example1();
            // await DemoQuery.Example0();
            // await DemoQuery.ExampleTLSA();

            // await DemoResolveUdpTcp.ExampleTestingUdpWrongServer();
            // await DemoResolveUdpTcp.ExampleTestingUdpWrongServer1();

            // await DemoQuery.Example1();
            // await DemoQuery.ExampleGoogleOverWire();
            // await DemoQuery.ExampleGoogleOverWirePost();
            // await DemoQuery.ExampleCloudflareSelection();
            // await DemoQuery.ExampleSystemDns();



            //await DemoResolveReturn.Example();

            //await DemoQuery.Example1();

            //await DemoQuery.Example2();

            //await DemoResolve.Example();

            //await DemoRecords.Demo("evotec.pl", DnsRecordType.A, DnsEndpoint.CloudflareFamily);

            //await DemoQuery.Example1();

            //await DemoQuery.Example2();

            //await DemoQuery.Example3();

            //await DemoQuery.ExampleHttpsOverPost();

            //await DemoRecords.Demo("evotec.pl", DnsRecordType.A, DnsEndpoint.CloudflareFamily);

            //await DemoRecords.Demo("evotec.pl", DnsRecordType.TXT, DnsEndpoint.CloudflareSecurity);

            //await DemoRecords.Demo("not.evotec.pl", DnsRecordType.TXT, DnsEndpoint.Quad9);

            //await DemoRecords.Demo("microsoft.com", DnsRecordType.MX, DnsEndpoint.CloudflareFamily);

            //await DemoRecords.Demo("microsoft.com", DnsRecordType.MX, DnsEndpoint.CloudflareWireFormat);

            //await DemoRecords.Demo("evotec.pl", DnsRecordType.NSEC, DnsEndpoint.CloudflareFamily);
            //await DemoRecords.Demo("evotec.pl", DnsRecordType.NSEC, DnsEndpoint.Google);
            //await DemoRecords.Demo("evotec.pl", DnsRecordType.NSEC, DnsEndpoint.Quad9);
            //await DemoRecords.Demo("evotec.pl", DnsRecordType.NSEC, DnsEndpoint.OpenDNS);
            //await DemoRecords.Demo("evotec.pl", DnsRecordType.NSEC, DnsEndpoint.OpenDNSFamily);
            //await DemoRecords.Demo("evotec.pl", DnsRecordType.NSEC, DnsEndpoint.Quad9Unsecure);

            //await DemoByManualUrl.Example();

            //await DemoByManualUrl.Example2();

            //await DemoResolveAll.Example();

            //await DemoResolveFirst.Example();

            //await DemoResolveParallel.Example();

            //await DemoResolve.Example();
        }
    }
}
