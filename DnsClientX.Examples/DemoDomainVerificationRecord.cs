using System.Threading.Tasks;

namespace DnsClientX.Examples {
    internal class DemoDomainVerificationRecord {
        public static async Task Example() {
            using var client = new ClientX(DnsEndpoint.Cloudflare);
            var response = await client.Resolve("evotec.xyz", DnsRecordType.TXT, typedRecords: true);
            foreach (var typed in response.TypedAnswers!) {
                if (typed is DomainVerificationRecord dv) {
                    Settings.Logger.WriteInformation($"{dv.Provider}: {dv.Token}");
                }
            }
        }
    }
}
