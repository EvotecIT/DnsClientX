using System.Net.Http;

namespace DnsClientX.Tests {
    /// <summary>
    /// Tests shared query diagnostics helpers used by the tooling layers.
    /// </summary>
    public class DnsQueryDiagnosticsTests {
        /// <summary>
        /// Ensures empty untruncated envelopes are flagged as suspicious.
        /// </summary>
        [Fact]
        public void IsSuspiciousEmptySuccess_ReturnsTrueForEmptyUntruncatedEnvelope() {
            var response = new DnsResponse {
                Status = DnsResponseCode.NoError,
                Questions = System.Array.Empty<DnsQuestion>(),
                Answers = System.Array.Empty<DnsAnswer>(),
                Authorities = System.Array.Empty<DnsAnswer>(),
                Additional = System.Array.Empty<DnsAnswer>()
            };

            Assert.True(DnsQueryDiagnostics.IsSuspiciousEmptySuccess(response));
        }

        /// <summary>
        /// Ensures a populated answer section is treated as a real response.
        /// </summary>
        [Fact]
        public void IsSuspiciousEmptySuccess_ReturnsFalseWhenAnswerDataExists() {
            var response = new DnsResponse {
                Status = DnsResponseCode.NoError,
                Answers = new[] {
                    new DnsAnswer {
                        Name = "example.com",
                        Type = DnsRecordType.A,
                        DataRaw = "1.1.1.1"
                    }
                }
            };

            Assert.False(DnsQueryDiagnostics.IsSuspiciousEmptySuccess(response));
            Assert.True(DnsQueryDiagnostics.HasEnvelopeData(response));
        }

        /// <summary>
        /// Ensures negative resolver statuses are not treated as suspicious empty successes.
        /// </summary>
        [Fact]
        public void IsSuspiciousEmptySuccess_ReturnsFalseForNegativeStatusWithoutEnvelopeData() {
            var response = new DnsResponse {
                Status = DnsResponseCode.NXDomain,
                Questions = System.Array.Empty<DnsQuestion>(),
                Answers = System.Array.Empty<DnsAnswer>(),
                Authorities = System.Array.Empty<DnsAnswer>(),
                Additional = System.Array.Empty<DnsAnswer>()
            };

            Assert.False(DnsQueryDiagnostics.IsSuspiciousEmptySuccess(response));
        }

        /// <summary>
        /// Ensures server failures without answers are treated as transient.
        /// </summary>
        [Fact]
        public void IsTransient_ResponseReturnsTrueForServerFailureWithoutAnswers() {
            var response = new DnsResponse {
                Status = DnsResponseCode.ServerFailure,
                Answers = System.Array.Empty<DnsAnswer>()
            };

            Assert.True(DnsQueryDiagnostics.IsTransient(response));
        }

        /// <summary>
        /// Ensures unsupported transport capability failures are not treated as transient.
        /// </summary>
        [Fact]
        public void IsTransient_ResponseReturnsFalseForUnsupportedTransportCapability() {
            var response = new DnsResponse {
                Status = DnsResponseCode.NotImplemented,
                Questions = new[] {
                    new DnsQuestion {
                        Name = "example.com",
                        Type = DnsRecordType.A,
                        RequestFormat = DnsRequestFormat.DnsOverHttp3
                    }
                }
            };

#if NET8_0_OR_GREATER
            Assert.Equal(!DnsTransportCapabilities.Supports(DnsRequestFormat.DnsOverHttp3), DnsQueryDiagnostics.IsTransportCapabilityFailure(response));
#else
            Assert.True(DnsQueryDiagnostics.IsTransportCapabilityFailure(response));
            Assert.False(DnsQueryDiagnostics.IsTransient(response));
#endif
        }

        /// <summary>
        /// Ensures SSL request failures are treated as transient transport issues.
        /// </summary>
        [Fact]
        public void IsTransient_ExceptionReturnsTrueForSslRequestFailure() {
            var exception = new HttpRequestException("The SSL connection could not be established.");

            Assert.True(DnsQueryDiagnostics.IsTransient(exception));
        }

        /// <summary>
        /// Ensures argument validation errors are not treated as transient.
        /// </summary>
        [Fact]
        public void IsTransient_ExceptionReturnsFalseForArgumentError() {
            var exception = new System.ArgumentException("Bad input.");

            Assert.False(DnsQueryDiagnostics.IsTransient(exception));
        }
    }
}
