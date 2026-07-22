#if NET8_0_OR_GREATER
using DnsClientX.LoadTests;
using Xunit;

namespace DnsClientX.Tests {
    /// <summary>Ensures load measurements reject semantically incomplete DNS successes.</summary>
    public sealed class LoadTestRunnerTests {
        /// <summary>A successful RCODE is insufficient when the requested answer was lost.</summary>
        [Fact]
        public void ClassifyFailure_RejectsMissingRequestedAnswer() {
            var response = new DnsResponse {
                Status = DnsResponseCode.NoError,
                RequestedAnswerPresent = false
            };

            Assert.Equal("MissingRequestedAnswer", LoadTestRunner.ClassifyFailure(response));
        }

        /// <summary>Response errors remain failures even when the RCODE and answer-presence flags look successful.</summary>
        [Fact]
        public void ClassifyFailure_RejectsResponseError() {
            var response = new DnsResponse {
                Status = DnsResponseCode.NoError,
                RequestedAnswerPresent = true,
                Error = "projection failed"
            };

            Assert.Equal("ResponseError", LoadTestRunner.ClassifyFailure(response));
        }

        /// <summary>A complete positive response is counted as successful.</summary>
        [Fact]
        public void ClassifyFailure_AcceptsRequestedAnswer() {
            var response = new DnsResponse {
                Status = DnsResponseCode.NoError,
                RequestedAnswerPresent = true
            };

            Assert.Null(LoadTestRunner.ClassifyFailure(response));
        }
    }
}
#endif
