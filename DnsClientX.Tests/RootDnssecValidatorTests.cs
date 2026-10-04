using Xunit;

namespace DnsClientX.Tests {
    /// <summary>
    /// Tests for the <see cref="DnsSecValidator"/> verifying root trust anchors.
    /// </summary>
    public class RootDnssecValidatorTests {
        /// <summary>
        /// Validates a DS record using the embedded root anchors.
        /// </summary>
        [Fact]
        public void ValidateAgainstRoot_DsRecord() {
            var response = new DnsResponse {
                Answers = new[] {
                    new DnsAnswer {
                        Name = ".",
                        Type = DnsRecordType.DS,
                        TTL = 3600,
                        DataRaw = "20326 8 2 E06D44B80B8F1D39A95C0B0D7C65D08458E880409BBC683457104237C7F8EC8D"
                    }
                }
            };
            Assert.True(DnsSecValidator.ValidateAgainstRoot(response, out string msg));
            Assert.Equal(string.Empty, msg);
        }

        /// <summary>A root anchor value attached to another owner is not a root DS.</summary>
        [Fact]
        public void ValidateAgainstRoot_DsRecordAtOtherOwner_ReturnsFalse() {
            var response = new DnsResponse {
                Answers = new[] {
                    new DnsAnswer {
                        Name = "example.com.",
                        Type = DnsRecordType.DS,
                        DataRaw = "20326 8 2 E06D44B80B8F1D39A95C0B0D7C65D08458E880409BBC683457104237C7F8EC8D"
                    }
                }
            };

            Assert.False(DnsSecValidator.ValidateAgainstRoot(response, out string message));
            Assert.Contains("root", message, System.StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>Incomplete answer owners are reported as invalid input.</summary>
        [Fact]
        public void ValidateAgainstRoot_EmptyDsOwner_ReturnsFalse() {
            var response = new DnsResponse {
                Answers = new[] {
                    new DnsAnswer { Name = string.Empty, Type = DnsRecordType.DS,
                        DataRaw = "20326 8 2 E06D44B80B8F1D39A95C0B0D7C65D08458E880409BBC683457104237C7F8EC8D" }
                }
            };

            Assert.False(DnsSecValidator.ValidateAgainstRoot(response, out string message));
            Assert.Contains("owner", message, System.StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>Extra non-hexadecimal DS text cannot be discarded after a known anchor digest.</summary>
        [Fact]
        public void ValidateAgainstRoot_DsWithTrailingGarbage_ReturnsFalse() {
            var response = new DnsResponse {
                Answers = new[] {
                    new DnsAnswer { Name = ".", Type = DnsRecordType.DS,
                        DataRaw = "20326 8 2 E06D44B80B8F1D39A95C0B0D7C65D08458E880409BBC683457104237C7F8EC8D garbage" }
                }
            };

            Assert.False(DnsSecValidator.ValidateAgainstRoot(response, out string message));
            Assert.Contains("parse DS", message);
        }

        /// <summary>Whitespace inside the hexadecimal presentation digest remains valid.</summary>
        [Fact]
        public void ValidateAgainstRoot_DsDigestWithWhitespace_Succeeds() {
            var response = new DnsResponse {
                Answers = new[] {
                    new DnsAnswer { Name = ".", Type = DnsRecordType.DS,
                        DataRaw = "20326 8 2 E06D44B80B8F1D39A95C0B0D7C65D084 58E880409BBC683457104237C7F8EC8D" }
                }
            };

            Assert.True(DnsSecValidator.ValidateAgainstRoot(response, out string message));
            Assert.Equal(string.Empty, message);
        }

        /// <summary>
        /// Validates a DNSKEY record using the embedded root anchors.
        /// </summary>
        [Fact]
        public void ValidateAgainstRoot_DnsKeyRecord() {
            var response = new DnsResponse {
                Answers = new[] {
                    new DnsAnswer {
                        Name = ".",
                        Type = DnsRecordType.DNSKEY,
                        TTL = 3600,
                        DataRaw = "257 3 8 AwEAAaz/tAm8yTn4Mfeh5eyI96WSVexTBAvkMgJzkKTOiW1vkIbzxeF3+/4RgWOq7HrxRixHlFlExOLAJr5emLvN7SWXgnLh4+B5xQlNVz8Og8kvArMtNROxVQuCaSnIDdD5LKyWbRd2n9WGe2R8PzgCmr3EgVLrjyBxWezF0jLHwVN8efS3rCj/EWgvIWgb9tarpVUDK/b58Da+sqqls3eNbuv7pr+eoZG+SrDK6nWeL3c6H5Apxz7LjVc1uTIdsIXxuOLYA4/ilBmSVIzuDWfdRUfhHdY6+cn8HFRm+2hM8AnXGXws9555KrUB5qihylGa8subX2Nn6UwNR1AkUTV74bU="
                    }
                }
            };
            Assert.True(DnsSecValidator.ValidateAgainstRoot(response, out string msg));
            Assert.Equal(string.Empty, msg);
        }

        /// <summary>
        /// Ensures validation fails when DS algorithm is not supported.
        /// </summary>
        [Fact]
        public void ValidateAgainstRoot_InvalidDsAlgorithm_ReturnsFalse() {
            var response = new DnsResponse {
                Answers = new[] {
                    new DnsAnswer {
                        Name = ".",
                        Type = DnsRecordType.DS,
                        TTL = 3600,
                        DataRaw = "20326 9 2 E06D44B80B8F1D39A95C0B0D7C65D08458E880409BBC683457104237C7F8EC8D"
                    }
                }
            };
            Assert.False(DnsSecValidator.ValidateAgainstRoot(response, out string msg));
            Assert.Contains("DS record", msg);
        }

        /// <summary>
        /// Ensures validation fails when DNSKEY algorithm is not supported.
        /// </summary>
        [Fact]
        public void ValidateAgainstRoot_InvalidDnsKeyAlgorithm_ReturnsFalse() {
            var response = new DnsResponse {
                Answers = new[] {
                    new DnsAnswer {
                        Name = ".",
                        Type = DnsRecordType.DNSKEY,
                        TTL = 3600,
                        DataRaw = "257 3 9 AwEAAaz/tAm8yTn4Mfeh5eyI96WSVexTBAvkMgJzkKTOiW1vkIbzxeF3+/4RgWOq7HrxRixHlFlExOLAJr5emLvN7SWXgnLh4+B5xQlNVz8Og8kvArMtNROxVQuCaSnIDdD5LKyWbRd2n9WGe2R8PzgCmr3EgVLrjyBxWezF0jLHwVN8efS3rCj/EWgvIWgb9tarpVUDK/b58Da+sqqls3eNbuv7pr+eoZG+SrDK6nWeL3c6H5Apxz7LjVc1uTIdsIXxuOLYA4/ilBmSVIzuDWfdRUfhHdY6+cn8HFRm+2hM8AnXGXws9555KrUB5qihylGa8subX2Nn6UwNR1AkUTV74bU="
                    }
                }
            };
            Assert.False(DnsSecValidator.ValidateAgainstRoot(response, out string msg));
            Assert.Contains("DNSKEY record", msg);
        }
    }
}
