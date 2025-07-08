using System;
using System.Text.Json;
using Xunit;

namespace DnsClientX.Tests {
    public class DnsResponseTests {
        [Fact]
        public void AddServerDetailsPopulatesFields() {
            var response = new DnsResponse {
                Questions = new[] { new DnsQuestion { Name = "example.com", Type = DnsRecordType.A } },
                Answers = new[] { new DnsAnswer { Name = "example.com", Type = DnsRecordType.A, TTL = 60, DataRaw = "1.1.1.1" } }
            };
            var config = new Configuration("8.8.8.8", DnsRequestFormat.DnsOverHttps);

            response.AddServerDetails(config);

            Assert.Equal(config.Hostname, response.Questions[0].HostName);
            Assert.Equal(config.BaseUri, response.Questions[0].BaseUri);
            Assert.Equal(config.RequestFormat, response.Questions[0].RequestFormat);
            Assert.Equal(config.Port, response.Questions[0].Port);
            Assert.Single(response.AnswersMinimal);
            Assert.Equal(config.Port, response.AnswersMinimal[0].Port);
        }

        [Fact]
        public void AddServerDetailsLeavesBaseUriNullForUdp() {
            var response = new DnsResponse {
                Questions = new[] { new DnsQuestion { Name = "example.com", Type = DnsRecordType.A } }
            };
            var config = new Configuration("8.8.8.8", DnsRequestFormat.DnsOverUDP);

            response.AddServerDetails(config);

            Assert.Equal(config.Hostname, response.Questions[0].HostName);
            Assert.Null(response.Questions[0].BaseUri);
            Assert.Equal(config.RequestFormat, response.Questions[0].RequestFormat);
            Assert.Equal(config.Port, response.Questions[0].Port);
        }

        [Fact]
        public void AddServerDetailsLeavesBaseUriNullForTcp() {
            var response = new DnsResponse {
                Questions = new[] { new DnsQuestion { Name = "example.com", Type = DnsRecordType.A } }
            };
            var config = new Configuration("8.8.8.8", DnsRequestFormat.DnsOverTCP);

            response.AddServerDetails(config);

            Assert.Equal(config.Hostname, response.Questions[0].HostName);
            Assert.Null(response.Questions[0].BaseUri);
            Assert.Equal(config.RequestFormat, response.Questions[0].RequestFormat);
            Assert.Equal(config.Port, response.Questions[0].Port);
        }

        [Fact]
        public void CommentConverterReadsArray() {
            var json = "[\"a\",\"b\"]";
            var options = new JsonSerializerOptions();
            options.Converters.Add(new CommentConverter());
            string result = JsonSerializer.Deserialize<string>(json, options)!;
            Assert.Equal("a; b", result);
        }
    }
}
