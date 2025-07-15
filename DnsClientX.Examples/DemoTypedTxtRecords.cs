using System.Threading.Tasks;

namespace DnsClientX.Examples {
    internal class DemoTypedTxtRecords {
        public static async Task Example() {
            using var client = new ClientX(DnsEndpoint.Cloudflare);
            var response = await client.Resolve("_dmarc.google.com", DnsRecordType.TXT, typedRecords: true);
            foreach (var typed in response.TypedAnswers!) {
                Settings.Logger.WriteInformation(typed.GetType().Name);
            }
        }
    }
}
