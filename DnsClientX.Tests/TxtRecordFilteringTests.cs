using System;
using System.Threading.Tasks;
using Xunit;

namespace DnsClientX.Tests {
    /// <summary>
    /// Tests TXT record filtering behavior without inventing resource-record boundaries.
    /// </summary>
    public class TxtRecordFilteringTests {
        /// <summary>
        /// Ensures several character-strings in one TXT resource record remain one filter result.
        /// </summary>
        [Fact]
        public async Task ResolveFilter_PreservesWholeMultiStringTxtRecord() {
            var answer = new DnsAnswer {
                Name = "example.com",
                Type = DnsRecordType.TXT,
                DataRaw = "\"google-site-verification=abc\" \"v=spf1 include:example.com -all\" \"facebook-domain-verification=xyz\""
            };

            var response = new DnsResponse {
                Status = DnsResponseCode.NoError,
                Answers = new[] { answer },
                Questions = Array.Empty<DnsQuestion>()
            };

            using var client = new ClientX(DnsEndpoint.Cloudflare);
            client.ResolverOverride = (_, _, _) => Task.FromResult(response);

            var filtered = await client.ResolveFilter("example.com", DnsRecordType.TXT, "v=spf1");

            Assert.NotNull(filtered.Answers);
            Assert.Single(filtered.Answers!);
            Assert.Equal(
                "google-site-verification=abcv=spf1 include:example.com -allfacebook-domain-verification=xyz",
                filtered.Answers![0].Data);
            Assert.Equal(answer.DataRaw, filtered.Answers[0].DataRaw);
        }

        /// <summary>
        /// Ensures filtering several independent TXT resource records returns only the matching RR.
        /// </summary>
        [Fact]
        public async Task ResolveFilter_DoesNotJoinIndependentTxtRecords() {
            var spf = new DnsAnswer {
                Name = "example.com",
                Type = DnsRecordType.TXT,
                DataRaw = "\"v=spf1 include:example.com -all\""
            };
            var response = new DnsResponse {
                Status = DnsResponseCode.NoError,
                Answers = new[] {
                    new DnsAnswer {
                        Name = "example.com",
                        Type = DnsRecordType.TXT,
                        DataRaw = "\"google-site-verification=abc\""
                    },
                    spf,
                    new DnsAnswer {
                        Name = "example.com",
                        Type = DnsRecordType.TXT,
                        DataRaw = "\"facebook-domain-verification=xyz\""
                    }
                },
                Questions = Array.Empty<DnsQuestion>()
            };

            using var client = new ClientX(DnsEndpoint.Cloudflare);
            client.ResolverOverride = (_, _, _) => Task.FromResult(response);

            DnsResponse filtered = await client.ResolveFilter("example.com", DnsRecordType.TXT, "v=spf1");

            DnsAnswer answer = Assert.Single(filtered.Answers);
            Assert.Equal(spf.DataRaw, answer.DataRaw);
            Assert.Equal("v=spf1 include:example.com -all", answer.Data);
        }
    }
}
