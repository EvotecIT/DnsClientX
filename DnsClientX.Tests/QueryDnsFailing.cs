namespace DnsClientX.Tests {
    /// <summary>
    /// Tests scenarios expected to fail such as unreachable servers.
    /// </summary>
    public class QueryDnsFailing {
        /// <summary>
        /// Queries an invalid server expecting a timeout.
        /// </summary>
        [RealDnsTheory]
        [InlineData("8.8.1.1", DnsRequestFormat.DnsOverUDP)]
        [InlineData("a1akam1.net", DnsRequestFormat.DnsOverUDP)]
        public async Task ShouldFailWithTimeout(string hostName, DnsRequestFormat requestFormat) {
            var response = await ClientX.QueryDns("github.com", DnsRecordType.A, hostName, requestFormat, timeOutMilliseconds: 500);
            Assert.Equal(DnsResponseCode.ServerFailure, response.Status);
            Assert.Contains(response.ErrorCode, new[] { DnsQueryErrorCode.Timeout, DnsQueryErrorCode.Network });
            if (response.ErrorCode == DnsQueryErrorCode.Timeout) Assert.NotNull(response.Exception);
            Assert.False(string.IsNullOrEmpty(response.Error));
            DnsQuestion question = Assert.Single(response.Questions);
            Assert.Equal("github.com", question.Name);
            Assert.Equal(DnsRecordType.A, question.Type);
        }


        /// <summary>
        /// Resolves using a <see cref="ClientX"/> instance pointing at an invalid server expecting failure.
        /// </summary>
        [RealDnsTheory]
        [InlineData("8.8.1.1", DnsRequestFormat.DnsOverUDP)]
        [InlineData("a1akam1.net", DnsRequestFormat.DnsOverUDP)]
        public async Task ShouldFailWithTimeoutResolve(string hostName, DnsRequestFormat requestFormat) {
            using ClientX client = new ClientX(hostName, requestFormat, timeOutMilliseconds: 500) {
                Debug = true
            };
            var response = await client.Resolve("github.com", DnsRecordType.A);
            Assert.Equal(DnsResponseCode.ServerFailure, response.Status);
            Assert.Contains(response.ErrorCode, new[] { DnsQueryErrorCode.Timeout, DnsQueryErrorCode.Network });
            if (response.ErrorCode == DnsQueryErrorCode.Timeout) Assert.NotNull(response.Exception);
            Assert.False(string.IsNullOrEmpty(response.Error));
            DnsQuestion question = Assert.Single(response.Questions);
            Assert.Equal("github.com", question.Name);
            Assert.Equal(DnsRecordType.A, question.Type);
        }
    }
}
