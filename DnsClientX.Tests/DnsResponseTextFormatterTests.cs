using System;
using Xunit;

namespace DnsClientX.Tests {
    /// <summary>
    /// Tests shared DNS response text formatting helpers.
    /// </summary>
    public class DnsResponseTextFormatterTests {
        private static readonly Func<TimeSpan, string> DurationFormatter =
            duration => $"{(int)Math.Round(duration.TotalMilliseconds, MidpointRounding.AwayFromZero)} ms";

        /// <summary>Supplementary format controls cannot hide terminal text while ordinary emoji remain intact.</summary>
        [Fact]
        public void TerminalTextEscapesSupplementaryFormatCharacters() {
            Assert.Equal("left\\uDB40\\uDC01right", DnsTerminalText.Escape("left\uDB40\uDC01right"));
            Assert.Equal("left\uD83D\uDE00right", DnsTerminalText.Escape("left\uD83D\uDE00right"));
        }

        /// <summary>Decoded DNS control octets cannot execute as terminal commands in text modes.</summary>
        [Theory]
        [InlineData(DnsResponsePresentationMode.Short)]
        [InlineData(DnsResponsePresentationMode.Pretty)]
        [InlineData(DnsResponsePresentationMode.Raw)]
        public void TextModesEscapeDecodedTxtControls(DnsResponsePresentationMode mode) {
            DnsAnswer answer = new() {
                Name = "example.com",
                Type = DnsRecordType.TXT,
                DataRaw = "\"red\\027[31m\\010next\""
            };
            DnsResponse response = new() { Answers = new[] { answer } };

            string[] lines = DnsResponseTextFormatter.BuildOutputLines(response,
                new DnsResponsePresentationOptions { Mode = mode, ShowAnswers = true },
                TimeSpan.Zero, DurationFormatter);

            Assert.DoesNotContain(lines, line => line.Contains('\u001b') || line.Contains('\n'));
            Assert.Contains(lines, line => line.Contains("\\u001B[31m\\u000Anext", StringComparison.Ordinal));
            Assert.Equal("red\u001b[31m\nnext", answer.Data);
            Assert.Equal("\"red\\027[31m\\010next\"", answer.DataRaw);

            string json = Assert.Single(DnsResponseTextFormatter.BuildOutputLines(response,
                new DnsResponsePresentationOptions { Mode = DnsResponsePresentationMode.Json },
                TimeSpan.Zero, DurationFormatter));
            Assert.Contains("red\\\\027", json, StringComparison.Ordinal);
        }

        /// <summary>Errors and DNS names cannot insert terminal controls or extra display lines.</summary>
        [Theory]
        [InlineData(DnsResponsePresentationMode.Pretty)]
        [InlineData(DnsResponsePresentationMode.Raw)]
        public void TextModesEscapeUntrustedMetadata(DnsResponsePresentationMode mode) {
            DnsResponse response = new() {
                Error = "resolver\u001b[2Jfailed\nnext",
                Questions = new[] { new DnsQuestion { Name = "q\u202E.example", Type = DnsRecordType.A } },
                Answers = new[] { new DnsAnswer { Name = "a\u001b[31m.example", DataRaw = "203.0.113.10" } }
            };

            string[] lines = DnsResponseTextFormatter.BuildOutputLines(response,
                new DnsResponsePresentationOptions { Mode = mode, ShowQuestions = true, ShowAnswers = true },
                TimeSpan.Zero, DurationFormatter);

            Assert.All(lines, line => {
                Assert.Equal(-1, line.IndexOf('\u001b'));
                Assert.Equal(-1, line.IndexOf('\u202e'));
                Assert.Equal(-1, line.IndexOf('\n'));
            });
            Assert.Contains(lines, line => line.Contains("resolver\\u001B[2Jfailed\\u000Anext", StringComparison.Ordinal));
            Assert.Contains(lines, line => line.Contains("q\\u202E.example", StringComparison.Ordinal));
            Assert.Contains(lines, line => line.Contains("a\\u001B[31m.example", StringComparison.Ordinal));
        }

        /// <summary>
        /// Ensures short output uses the requested answer projection.
        /// </summary>
        [Fact]
        public void BuildShortLines_UsesRequestedAnswerProjection() {
            DnsResponse response = new DnsResponse {
                Answers = new[] {
                    new DnsAnswer {
                        Name = "example.com",
                        Type = DnsRecordType.TXT,
                        TTL = 60,
                        DataRaw = "line1\nline2"
                    }
                }
            };

            string[] defaultLines = DnsResponseTextFormatter.BuildShortLines(response, txtConcat: false);
            string[] concatLines = DnsResponseTextFormatter.BuildShortLines(response, txtConcat: true);

            Assert.Equal("line1\\u000Aline2", defaultLines[0]);
            Assert.Equal("line1line2", concatLines[0]);
        }

        /// <summary>
        /// Ensures pretty output preserves section ordering and filtering.
        /// </summary>
        [Fact]
        public void BuildPrettyLines_PreservesRequestedSections() {
            DnsResponse response = new DnsResponse {
                Status = DnsResponseCode.NoError,
                RetryCount = 2,
                Questions = new[] {
                    new DnsQuestion {
                        Name = "example.com",
                        Type = DnsRecordType.A
                    }
                },
                Answers = new[] {
                    new DnsAnswer {
                        Name = "example.com",
                        Type = DnsRecordType.A,
                        TTL = 60,
                        DataRaw = "203.0.113.10"
                    }
                },
                Authorities = new[] {
                    new DnsAnswer {
                        Name = "example.com",
                        Type = DnsRecordType.A,
                        TTL = 300,
                        DataRaw = "203.0.113.53"
                    }
                },
                Additional = new[] {
                    new DnsAnswer {
                        Name = "ns1.example.com",
                        Type = DnsRecordType.A,
                        TTL = 300,
                        DataRaw = "203.0.113.53"
                    }
                }
            };

            string[] lines = DnsResponseTextFormatter.BuildPrettyLines(
                response,
                showQuestions: true,
                showAnswers: true,
                showAuthorities: false,
                showAdditional: true,
                txtConcat: false);

            Assert.Equal("Status: NoError (retries 2)", lines[0]);
            Assert.Contains("Questions:", lines);
            Assert.Contains("  example.com\tA", lines);
            Assert.Contains("Answers:", lines);
            Assert.Contains("  example.com\tA\t60\t203.0.113.10", lines);
            Assert.DoesNotContain("Authorities:", lines);
            Assert.Contains("Additional:", lines);
            Assert.Contains("  ns1.example.com\tA\t300\t203.0.113.53", lines);
        }

        /// <summary>
        /// Ensures raw output preserves dig-style headers and section formatting.
        /// </summary>
        [Fact]
        public void BuildRawLines_PreservesHeadersAndSectionFormatting() {
            DnsResponse response = new DnsResponse {
                Status = DnsResponseCode.NoError,
                UsedTransport = Transport.Udp,
                RoundTripTime = TimeSpan.FromMilliseconds(12),
                Questions = new[] {
                    new DnsQuestion {
                        Name = "example.com",
                        Type = DnsRecordType.A
                    }
                },
                Answers = new[] {
                    new DnsAnswer {
                        Name = "example.com",
                        Type = DnsRecordType.A,
                        TTL = 60,
                        DataRaw = "203.0.113.10"
                    }
                },
                Authorities = new[] {
                    new DnsAnswer {
                        Name = "example.com",
                        Type = DnsRecordType.A,
                        TTL = 300,
                        DataRaw = "203.0.113.53"
                    }
                }
            };

            string[] lines = DnsResponseTextFormatter.BuildRawLines(
                response,
                elapsed: TimeSpan.FromMilliseconds(99),
                showQuestions: true,
                showAnswers: true,
                showAuthorities: true,
                showAdditional: false,
                txtConcat: false,
                durationFormatter: DurationFormatter);

            Assert.Equal(";; status: NoError", lines[0]);
            Assert.Equal(";; transport: Udp", lines[1]);
            Assert.Equal(";; query time: 12 ms", lines[2]);
            Assert.Equal(";; sections: question 1, answer 1, authority 1, additional 0", lines[3]);
            Assert.Contains(";; QUESTION SECTION:", lines);
            Assert.Contains(";example.com\tIN\tA", lines);
            Assert.Contains(";; ANSWER SECTION:", lines);
            Assert.Contains("example.com\t60\tIN\tA\t203.0.113.10", lines);
            Assert.Contains(";; AUTHORITY SECTION:", lines);
            Assert.Contains("example.com\t300\tIN\tA\t203.0.113.53", lines);
            Assert.DoesNotContain(";; ADDITIONAL SECTION:", lines);
        }

        /// <summary>
        /// Ensures mode selection is handled by the shared presentation helper.
        /// </summary>
        [Fact]
        public void BuildOutputLines_UsesRequestedMode() {
            DnsResponse response = new DnsResponse {
                Status = DnsResponseCode.NoError,
                RetryCount = 1,
                Questions = new[] {
                    new DnsQuestion {
                        Name = "example.com",
                        Type = DnsRecordType.A
                    }
                },
                Answers = new[] {
                    new DnsAnswer {
                        Name = "example.com",
                        Type = DnsRecordType.A,
                        TTL = 60,
                        DataRaw = "203.0.113.10"
                    }
                }
            };

            string[] shortLines = DnsResponseTextFormatter.BuildOutputLines(
                response,
                new DnsResponsePresentationOptions {
                    Mode = DnsResponsePresentationMode.Short
                },
                TimeSpan.Zero,
                DurationFormatter);
            Assert.Equal(new[] { "203.0.113.10" }, shortLines);

            string[] prettyLines = DnsResponseTextFormatter.BuildOutputLines(
                response,
                new DnsResponsePresentationOptions {
                    Mode = DnsResponsePresentationMode.Pretty,
                    ShowQuestions = true,
                    ShowAnswers = true
                },
                TimeSpan.Zero,
                DurationFormatter);
            Assert.Contains("Status: NoError (retries 1)", prettyLines);

            string[] jsonLines = DnsResponseTextFormatter.BuildOutputLines(
                response,
                new DnsResponsePresentationOptions {
                    Mode = DnsResponsePresentationMode.Json
                },
                TimeSpan.Zero,
                DurationFormatter);
            Assert.Single(jsonLines);
            Assert.Contains("\"Status\": \"NoError\"", jsonLines[0], StringComparison.Ordinal);
        }
    }
}
