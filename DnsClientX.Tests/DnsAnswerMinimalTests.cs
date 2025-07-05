using Xunit;

namespace DnsClientX.Tests {
    public class DnsAnswerMinimalTests {
        [Fact]
        public void ExplicitConversionCopiesFields() {
            var answer = new DnsAnswer {
                Name = "example.com",
                Type = DnsRecordType.A,
                TTL = 3600,
                DataRaw = "1.1.1.1"
            };

            DnsAnswerMinimal minimal = (DnsAnswerMinimal)answer;

            Assert.Equal("example.com", minimal.Name);
            Assert.Equal(DnsRecordType.A, minimal.Type);
            Assert.Equal(3600, minimal.TTL);
            Assert.Equal("1.1.1.1", minimal.Data);
        }

        [Fact]
        public void ConvertFromDnsAnswerArrayConvertsAll() {
            var answers = new[] {
                new DnsAnswer { Name = "a.com", Type = DnsRecordType.A, TTL = 60, DataRaw = "1.1.1.1" },
                new DnsAnswer { Name = "b.com", Type = DnsRecordType.AAAA, TTL = 60, DataRaw = "::1" }
            };

            var result = answers.ConvertFromDnsAnswer();

            Assert.Equal(2, result.Length);
            Assert.Equal("a.com", result[0].Name);
            Assert.Equal("1.1.1.1", result[0].Data);
            Assert.Equal(DnsRecordType.AAAA, result[1].Type);
            Assert.Equal("::1", result[1].Data);
        }
    }
}

