using System.Threading.Tasks;

namespace DnsClientX.Examples {
    internal class DemoTypedTxtRecords {
        public static async Task Example() {
            using var client = new ClientX(DnsEndpoint.Cloudflare);
            var response = await client.Resolve("_dmarc.google.com", DnsRecordType.TXT, typedRecords: true);
            foreach (var typed in response.TypedAnswers!) {
                if (typed is TxtRecord txt) {
                    Settings.Logger.WriteInformation($"Combined TXT: {txt.Text}");
                    foreach (string fragment in txt.Strings) {
                        Settings.Logger.WriteInformation($"Decoded fragment: {fragment}");
                    }
                    Settings.Logger.WriteInformation($"Original presentation: {txt.RawText}");
                }
            }
        }
    }
}
