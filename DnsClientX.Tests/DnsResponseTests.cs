using System;
using System.Text.Json;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace DnsClientX.Tests {
    /// <summary>
    /// Tests for the <see cref="DnsResponse"/> type.
    /// </summary>
    public class DnsResponseTests {
        /// <summary>
        /// Validates that server details are copied to the response.
        /// </summary>
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
            Assert.Equal(config.Hostname, response.ServerAddress);
            Assert.Equal(Transport.Doh, response.UsedTransport);
            Assert.Single(response.AnswersMinimal);
            Assert.Equal(config.Port, response.AnswersMinimal[0].Port);
        }

        /// <summary>
        /// BaseUri should remain null for UDP responses.
        /// </summary>
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
            Assert.Equal(config.Hostname, response.ServerAddress);
            Assert.Equal(Transport.Udp, response.UsedTransport);
        }

        /// <summary>
        /// BaseUri should remain null for TCP responses.
        /// </summary>
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
            Assert.Equal(config.Hostname, response.ServerAddress);
            Assert.Equal(Transport.Tcp, response.UsedTransport);
        }

        /// <summary>
        /// Converter should transform arrays of comment strings.
        /// </summary>
        [Fact]
        public void CommentConverterReadsArray() {
            var json = "[\"a\",\"b\"]";
            var options = new JsonSerializerOptions();
            options.Converters.Add(new CommentConverter());
            string result = JsonSerializer.Deserialize<string>(json, options)!;
            Assert.Equal("a; b", result);
        }

        /// <summary>
        /// Extended error information should be accessible via helper property.
        /// </summary>
        [Fact]
        public void ExtendedDnsErrorInfoReflectsErrors() {
            var response = new DnsResponse {
                ExtendedDnsErrors = new[] {
                    new ExtendedDnsError { InfoCode = 10, ExtraText = "one" },
                    new ExtendedDnsError { InfoCode = 20, ExtraText = "two" }
                }
            };

            Assert.Equal(2, response.ExtendedDnsErrorInfo.Length);
            Assert.Equal(10, response.ExtendedDnsErrorInfo[0].Code);
            Assert.Equal("one", response.ExtendedDnsErrorInfo[0].Text);
            Assert.Equal(20, response.ExtendedDnsErrorInfo[1].Code);
            Assert.Equal("two", response.ExtendedDnsErrorInfo[1].Text);
        }

        /// <summary>
        /// When no answers are present, <see cref="DnsResponse.AnswersMinimal"/> should be empty.
        /// </summary>
        [Fact]
        public void AnswersMinimalReturnsEmptyWhenAnswersNull() {
            var response = new DnsResponse();

            Assert.Empty(response.AnswersMinimal);
        }

        /// <summary>A projected response must not mutate the source response's derived answer data.</summary>
        [Fact]
        public void WithAnswersKeepsSourceDerivedDataIndependent() {
            var response = new DnsResponse {
                Questions = new[] { new DnsQuestion { Name = "example.com", Type = DnsRecordType.A } },
                Answers = new[] {
                    new DnsAnswer { Name = "example.com", Type = DnsRecordType.A, TTL = 60, DataRaw = "192.0.2.1" }
                }
            };
            response.AddServerDetails(new Configuration("192.0.2.53", DnsRequestFormat.DnsOverUDP));

            DnsResponse projected = response.WithAnswers(new[] {
                new DnsAnswer { Name = "example.com", Type = DnsRecordType.A, TTL = 30, DataRaw = "192.0.2.2" }
            });

            Assert.NotSame(response.AnswersMinimal, projected.AnswersMinimal);
            Assert.Equal("192.0.2.1", response.AnswersMinimal[0].Data);
            Assert.Equal("192.0.2.2", projected.AnswersMinimal[0].Data);
        }

        /// <summary>
        /// Local DNSSEC validation is successful only for a secure result, while other
        /// terminal states still report that validation was attempted.
        /// </summary>
        [Theory]
        [InlineData(DnsSecValidationStatus.NotRequested, false, false)]
        [InlineData(DnsSecValidationStatus.Secure, true, true)]
        [InlineData(DnsSecValidationStatus.Insecure, true, false)]
        [InlineData(DnsSecValidationStatus.Bogus, true, false)]
        [InlineData(DnsSecValidationStatus.Indeterminate, true, false)]
        public void DnsSecValidationFlagsReflectActualOutcome(DnsSecValidationStatus status,
            bool attempted, bool validated) {
            var response = new DnsResponse { DnsSecValidationStatus = status };

            Assert.Equal(attempted, response.DnsSecValidationAttempted);
            Assert.Equal(validated, response.DnsSecValidatedLocally);
        }

        /// <summary>Untrusted answers are not emitted through short pipeline-oriented output.</summary>
        [Fact]
        public void ShortOutputSuppressesAnswersWhenResponseHasAnError() {
            var response = new DnsResponse {
                Error = "DNSSEC bogus",
                Answers = new[] { new DnsAnswer { Name = "example.com", Type = DnsRecordType.A, DataRaw = "192.0.2.1" } }
            };

            Assert.Empty(DnsResponseTextFormatter.BuildShortLines(response, txtConcat: false));
            Assert.Contains(DnsResponseTextFormatter.BuildPrettyLines(response, false, true, false, false, false),
                line => line.Contains("DNSSEC bogus", StringComparison.Ordinal));
        }

        private static async Task RunUdpServerAsync(int port, CancellationToken token) {
            using var udp = new UdpClient(new IPEndPoint(IPAddress.Loopback, port));
#if NET5_0_OR_GREATER
            UdpReceiveResult result = await udp.ReceiveAsync(token);
            byte[] response = TestUtilities.CreateResponseFromQuery(result.Buffer);
            await udp.SendAsync(response, result.RemoteEndPoint, token);
#else
            var receiveTask = udp.ReceiveAsync();
            var completed = await Task.WhenAny(receiveTask, Task.Delay(Timeout.Infinite, token));
            if (completed != receiveTask) {
                throw new OperationCanceledException(token);
            }
            UdpReceiveResult result = receiveTask.Result;
            byte[] response = TestUtilities.CreateResponseFromQuery(result.Buffer);
            await udp.SendAsync(response, response.Length, result.RemoteEndPoint);
#endif
        }

        /// <summary>
        /// Successful wire queries should record a non-zero round trip time.
        /// </summary>
        [Fact]
        public async Task Resolve_SetsRoundTripTime() {
            int port = TestUtilities.GetFreeUdpPort();
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            Task serverTask = RunUdpServerAsync(port, cts.Token);

            using var client = new ClientX("127.0.0.1", DnsRequestFormat.DnsOverUDP, timeOutMilliseconds: 2000);
            client.EndpointConfiguration.Port = port;

            DnsResponse response = await client.Resolve("example.com", DnsRecordType.A, retryOnTransient: false);

            Assert.Equal(DnsResponseCode.NoError, response.Status);
            Assert.True(response.RoundTripTime > TimeSpan.Zero, $"Expected positive round trip time, got {response.RoundTripTime}.");

            await serverTask;
        }
    }
}
