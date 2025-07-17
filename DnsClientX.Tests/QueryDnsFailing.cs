namespace DnsClientX.Tests {
    /// <summary>
    /// Tests scenarios expected to fail such as unreachable servers.
    /// </summary>
    public class QueryDnsFailing {
        /// <summary>
        /// Queries an invalid server expecting a timeout.
        /// </summary>
        [Theory]
        [InlineData("8.8.1.1", DnsRequestFormat.DnsOverUDP)]
        [InlineData("a1akam1.net", DnsRequestFormat.DnsOverUDP)]
        public async Task ShouldFailWithTimeout(string hostName, DnsRequestFormat requestFormat) {
            var response = await ClientX.QueryDns("github.com", DnsRecordType.A, hostName, requestFormat, timeOutMilliseconds: 500);
            Assert.True(response.Status != DnsResponseCode.NoError);
        }


        /// <summary>
        /// Resolves using a <see cref="ClientX"/> instance pointing at an invalid server expecting failure.
        /// </summary>
        [Theory]
        [InlineData("8.8.1.1", DnsRequestFormat.DnsOverUDP)]
        [InlineData("a1akam1.net", DnsRequestFormat.DnsOverUDP)]
        public async Task ShouldFailWithTimeoutResolve(string hostName, DnsRequestFormat requestFormat) {
            ClientX client = new ClientX(hostName, requestFormat) {
                Debug = true
            };
            var response = await client.Resolve("github.com", DnsRecordType.A);
            Assert.True(response.Status != DnsResponseCode.NoError);
        }
    }
}
