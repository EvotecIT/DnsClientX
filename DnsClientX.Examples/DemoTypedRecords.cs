using System;
using System.Threading.Tasks;

namespace DnsClientX.Examples {
    internal class DemoTypedRecords {
        public static async Task Example() {
            using var client = new ClientX(DnsEndpoint.Cloudflare);
            var response = await client.Resolve("example.com", DnsRecordType.A, typedRecords: true);
            foreach (var typed in response.TypedAnswers!) {
                switch (typed) {
                    case TxtRecord txt:
                        Settings.Logger.WriteInformation($"TXT: {txt.Text}");
                        break;
                    default:
                        Settings.Logger.WriteInformation(typed.ToString()!);
                        break;
                }
            }
        }
    }
}
