using System;
using System.Collections.Generic;
using Xunit;

namespace DnsClientX.Tests {
    /// <summary>
    /// Contract tests for authenticated denial and unsigned-delegation classification.
    /// </summary>
    public class DnsSecProofTests {
        /// <summary>
        /// Absence of DS is an insecure delegation only when the exact NSEC bitmap also
        /// proves a delegation (NS present and SOA absent).
        /// </summary>
        [Fact]
        public void UnsignedDelegationRequiresDelegationSemantics() {
            DnsResponse delegation = ResponseWithNsecBitmap(0x20); // NS (type 2)
            DnsResponse ordinaryName = ResponseWithNsecBitmap(0x40); // A (type 1)

            Assert.True(DnsSecProof.ProvesUnsignedDelegation(delegation, "child.example"));
            Assert.False(DnsSecProof.ProvesUnsignedDelegation(ordinaryName, "child.example"));
        }

        /// <summary>A signed alias alone is not a complete positive answer for its target type.</summary>
        [Fact]
        public void AnswerChainRequiresTerminalRequestedType() {
            var answers = new[] {
                new DnsWireResourceRecord("alias.example.", DnsRecordType.CNAME, 1, 300, 300,
                    0, 0, "target.example.")
            };

            bool valid = DnsSecValidationEngine.TryFollowAnswerChain(answers, "alias.example",
                DnsRecordType.A, out string finalName, out bool terminal, out string? error);

            Assert.True(valid);
            Assert.False(terminal);
            Assert.Equal("target.example.", finalName);
            Assert.Null(error);
        }

        /// <summary>Denial signers may authenticate only names at or below their own zone.</summary>
        [Theory]
        [InlineData("www.example.com", "example.com", true)]
        [InlineData("example.com", "example.com", true)]
        [InlineData("www.other.test", "example.com", false)]
        public void DenialSignerMustContainQueriedName(string name, string signer, bool expected) {
            Assert.Equal(expected, DnsSecValidationEngine.IsNameWithinZone(name, signer));
        }

        /// <summary>An ancestor zone cannot deny a name below an authenticated delegation cut.</summary>
        [Fact]
        public void NameErrorProofStopsAtDelegationCut() {
            DnsResponse response = ResponseWithNsecBitmap(0x20, "com", "z"); // NS without SOA

            Assert.False(DnsSecProof.ProvesNameError(response, "missing.example.com"));
        }

        private static DnsResponse ResponseWithNsecBitmap(byte bitmap, string owner = "child.example", string next = "z.example") {
            byte[] nextName = DnsWireNameCodec.ToCanonicalWire(next);
            var message = new List<byte>(nextName);
            message.Add(0);
            message.Add(1);
            message.Add(bitmap);
            byte[] wire = message.ToArray();
            return new DnsResponse {
                WireMessage = wire,
                WireAuthorities = new[] {
                    new DnsWireResourceRecord(DnsWireNameCodec.Canonical(owner), DnsRecordType.NSEC, 1, 300, 300,
                        0, (ushort)wire.Length, string.Empty)
                }
            };
        }
    }
}
