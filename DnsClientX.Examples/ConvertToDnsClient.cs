using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DnsClient;
using DnsClientX;
using DnsClientX.Converter;

namespace DnsClientX.Examples {
    public class ConvertToDnsClient {
        public static async Task ExampleConvertToDnsClientFromX() {
            var recordTypes = new DnsRecordType[] { DnsRecordType.A, DnsRecordType.AAAA, DnsRecordType.CNAME, DnsRecordType.MX, DnsRecordType.TXT };
            foreach (var recordType in recordTypes) {
                // Query DNS using DnsClientX library
                Console.WriteLine($"Querying DNS for record type {recordType} using DnsClientX");

                var data = await ClientX.QueryDns("evotec.pl", recordType, DnsEndpoint.Cloudflare);
                foreach (var answer in data.Answers) {
                    Console.WriteLine("before> " + answer.Data);
                }

                var dnsClientAnswers = data.Answers.ToDnsClientAnswer();

                // lets display it
                foreach (var answer in dnsClientAnswers) {
                    Console.WriteLine("after > " + answer);
                }
            }
        }

        public static async Task ExampleConvertFromDnsClientToX() {

            var recordTypes = new QueryType[] { QueryType.A, QueryType.AAAA, QueryType.MX, QueryType.TXT };

            foreach (var recordType in recordTypes) {
                Console.WriteLine($"Querying DNS for record type {recordType} using DnsClient");
                // Query DNS using DnsClient library
                var lookup = new LookupClient();
                var result = await lookup.QueryAsync("evotec.pl", recordType);

                // lets convert it to DnsClientX format
                var dnsAnswers = result.Answers.ToDnsClientAnswerX();

                // lets display it
                dnsAnswers.DisplayToConsole();
            }
        }
    }
}
