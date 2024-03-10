using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DnsClient;
using DnsClientX;
using DnsClientX.Converter;

namespace DnsClientX.Examples {
    internal class ConvertToDnsClient {
        public static async Task ExampleConvertToDnsClientFromX() {
            var data = await ClientX.QueryDns("evotec.pl", DnsRecordType.A, DnsEndpoint.Cloudflare);

            // lets convert it to DnsClient format
            var dnsClientAnswers = data.Answers.ToDnsClientAnswer();

            // lets display it
            foreach (var answer in dnsClientAnswers) {
                Console.WriteLine(answer);
            }
        }


        public static async Task ExampleConvertFromDnsClientToX() {
            // Query DNS using DnsClient library
            var lookup = new LookupClient();
            var result = await lookup.QueryAsync("evotec.pl", QueryType.A);

            // lets convert it to DnsClientX format
            var dnsAnswers = result.Answers.ToDnsClientAnswerX();

            // lets display it
            foreach (var answer in dnsAnswers) {
                Console.WriteLine(answer);
            }
        }
    }
}
