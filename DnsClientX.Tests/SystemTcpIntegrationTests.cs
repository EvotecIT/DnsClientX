using System.Threading.Tasks;

namespace DnsClientX.Tests {
    /// <summary>
    /// Real-host integration coverage for the system TCP resolver endpoint.
    /// </summary>
    public class SystemTcpIntegrationTests {
        /// <summary>
        /// Confirms the system TCP resolver can resolve a real A record when live-host tests are enabled.
        /// </summary>
        [RealDnsFact]
        public async Task SystemTcp_ShouldResolveARecord() {
            var response = await ClientX.QueryDns("example.com", DnsRecordType.A, DnsEndpoint.SystemTcp,
                timeOutMilliseconds: 5000);

            Assert.Equal(DnsResponseCode.NoError, response.Status);
            Assert.NotEmpty(response.Answers);
            foreach (var answer in response.Answers) {
                Assert.Equal(DnsRecordType.A, answer.Type);
            }
        }
    }
}
