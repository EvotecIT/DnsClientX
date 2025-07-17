using System.Threading.Tasks;

namespace DnsClientX.Examples {
    internal class DemoTypedTxtAsTxt {
        public static async Task Example() {
            using var client = new ClientX(DnsEndpoint.Cloudflare);
            var response = await client.Resolve("_dmarc.google.com", DnsRecordType.TXT, typedRecords: true, parseTypedTxtRecords: true);
            foreach (var txt in response.TypedAnswers!) {
                Settings.Logger.WriteInformation(txt.GetType().Name);
            }
        }
    }
}
