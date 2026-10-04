using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace DnsClientX.Tests {
    /// <summary>Checks that static root entry points retain the same options as instance resolution.</summary>
    public class QueryDnsRootOptionsTests {
        /// <summary>Both public overloads request DNSSEC material, validate it, and project typed answers.</summary>
        [RealDnsTheory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task RootQueriesRetainDnssecAndTypedOptions(bool batch) {
            using var timeout = new CancellationTokenSource(System.TimeSpan.FromSeconds(30));
            DnsResponse response = batch
                ? Assert.Single(await ClientX.QueryDns(new[] { "." }, DnsRecordType.DNSKEY, DnsEndpoint.RootServer,
                    timeOutMilliseconds: 1500, retryOnTransient: false, requestDnsSec: true,
                    validateDnsSec: true, typedRecords: true, cancellationToken: timeout.Token))
                : await ClientX.QueryDns(".", DnsRecordType.DNSKEY, DnsEndpoint.RootServer,
                    timeOutMilliseconds: 1500, retryOnTransient: false, requestDnsSec: true,
                    validateDnsSec: true, typedRecords: true, cancellationToken: timeout.Token);
            Assert.Equal(DnsResponseCode.NoError, response.Status);
            Assert.True(response.DnsSecValidationAttempted);
            Assert.Equal(DnsSecValidationStatus.Secure, response.DnsSecValidationStatus);
            Assert.True(response.RequestedAnswerPresent);
            Assert.NotNull(response.TypedAnswers);
            Assert.NotEmpty(response.TypedAnswers!);
            Assert.All(response.Answers, answer => Assert.Equal(DnsRecordType.DNSKEY, answer.Type));
        }
    }
}
