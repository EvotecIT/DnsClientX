using System.Linq;
using Xunit;

namespace DnsClientX.Tests {
    /// <summary>
    /// Protects iterative resolution from unrelated answers, unsafe glue, and invalid alias substitution.
    /// </summary>
    public class RootResolutionSelectionTests {
        /// <summary>
        /// DNSSEC trust-anchor lookups must preserve the root label instead of producing an empty query name.
        /// </summary>
        [Fact]
        public void NormalizeIterativeName_PreservesRootLabel() {
            Assert.Equal(".", ClientX.NormalizeIterativeName("."));
            Assert.Equal("example.com", ClientX.NormalizeIterativeName("example.com."));
        }

        /// <summary>RFC 9156 reveals one additional label at each delegation and preserves the final question.</summary>
        [Theory]
        [InlineData("", "com", DnsRecordType.NS, false)]
        [InlineData("com", "example.com", DnsRecordType.NS, false)]
        [InlineData("example.com", "www.example.com", DnsRecordType.NS, false)]
        [InlineData("www.example.com", "api.www.example.com", DnsRecordType.NS, false)]
        [InlineData("api.www.example.com", "api.www.example.com", DnsRecordType.AAAA, true)]
        public void SelectIterativeQuestion_MinimizesDelegationDiscovery(string bailiwick,
            string expectedName, DnsRecordType expectedType, bool expectedFinal) {
            ClientX.IterativeQuestion question = ClientX.SelectIterativeQuestion(
                "api.www.example.com", DnsRecordType.AAAA, bailiwick, enabled: true);

            Assert.Equal(expectedName, question.Name);
            Assert.Equal(expectedType, question.Type);
            Assert.Equal(expectedFinal, question.IsFinal);
        }

        /// <summary>Disabling minimization and inconsistent referral state both fail safely to the original question.</summary>
        [Theory]
        [InlineData("com", false)]
        [InlineData("unrelated.test", true)]
        public void SelectIterativeQuestion_UsesFullQuestionWhenRequired(string bailiwick, bool enabled) {
            ClientX.IterativeQuestion question = ClientX.SelectIterativeQuestion(
                "www.example.com", DnsRecordType.A, bailiwick, enabled);

            Assert.Equal("www.example.com", question.Name);
            Assert.Equal(DnsRecordType.A, question.Type);
            Assert.True(question.IsFinal);
        }

        /// <summary>
        /// The closest authority ancestor is selected and unrelated authority data is ignored.
        /// </summary>
        [Fact]
        public void FindReferral_SelectsClosestAncestor() {
            var response = new DnsResponse {
                Authorities = [
                    Answer("example", DnsRecordType.NS, "ns.example."),
                    Answer("child.example", DnsRecordType.NS, "ns.child.example."),
                    Answer("attacker.invalid", DnsRecordType.NS, "ns.attacker.invalid.")
                ]
            };

            ClientX.Referral? referral = ClientX.FindReferral(response, "www.child.example");

            Assert.NotNull(referral);
            Assert.Equal("child.example", referral!.Zone);
            Assert.Equal(new[] { "ns.child.example" }, referral.NameServers);
        }

        /// <summary>
        /// Glue is accepted only when the name server itself is inside the responding parent's zone.
        /// </summary>
        [Fact]
        public void GetInBailiwickGlueAddresses_RejectsOutOfBailiwickData() {
            DnsAnswer[] additional = [
                Answer("ns.child.example", DnsRecordType.A, "192.0.2.10"),
                Answer("ns.external.test", DnsRecordType.A, "203.0.113.66"),
                Answer("ns.child.example", DnsRecordType.TXT, "not-an-address")
            ];

            string[] accepted = ClientX.GetInBailiwickGlueAddresses(
                "example",
                "ns.child.example",
                additional);
            string[] rejected = ClientX.GetInBailiwickGlueAddresses(
                "example",
                "ns.external.test",
                additional);

            Assert.Equal(new[] { "192.0.2.10" }, accepted);
            Assert.Empty(rejected);
        }

        /// <summary>
        /// RFC 9471 sibling glue remains usable when it is inside the responding root zone.
        /// </summary>
        [Fact]
        public void GetInBailiwickGlueAddresses_AcceptsRootSiblingGlue() {
            DnsAnswer[] additional = [
                Answer("l.gtld-servers.net", DnsRecordType.A, "192.41.162.30")
            ];

            string[] accepted = ClientX.GetInBailiwickGlueAddresses(
                ".",
                "l.gtld-servers.net",
                additional);

            Assert.Equal(new[] { "192.41.162.30" }, accepted);
        }

        /// <summary>
        /// An unrelated answer of the requested type cannot terminate iterative resolution.
        /// </summary>
        [Fact]
        public void HasRequestedAnswer_RejectsUnrelatedOwner() {
            var response = new DnsResponse {
                Answers = [Answer("attacker.invalid", DnsRecordType.A, "203.0.113.66")]
            };

            Assert.False(ClientX.HasRequestedAnswer(response, "www.example", DnsRecordType.A));
        }

        /// <summary>
        /// A requested answer reached through an in-message CNAME chain remains terminal.
        /// </summary>
        [Fact]
        public void HasRequestedAnswer_AcceptsCnameTarget() {
            var response = new DnsResponse {
                Answers = [
                    Answer("www.example", DnsRecordType.CNAME, "target.example."),
                    Answer("target.example", DnsRecordType.A, "192.0.2.20")
                ]
            };

            Assert.True(ClientX.HasRequestedAnswer(response, "www.example", DnsRecordType.A));
        }

        /// <summary>
        /// RFC 6672 DNAME substitution applies below the owner, not at the DNAME owner itself.
        /// </summary>
        [Fact]
        public void FindAliasTarget_DnameOnlyRewritesDescendants() {
            var response = new DnsResponse {
                Answers = [Answer("old.example", DnsRecordType.DNAME, "new.example.")]
            };

            Assert.Null(ClientX.FindAliasTarget(response, "old.example"));
            Assert.Equal("www.new.example", ClientX.FindAliasTarget(response, "www.old.example"));
        }

        /// <summary>RFC 9156 keeps DS on the parent side instead of following the child cut.</summary>
        [Fact]
        public void SelectIterativeQuestion_AsksParentForFinalDs() {
            ClientX.IterativeQuestion fromRoot = ClientX.SelectIterativeQuestion(
                "child.example", DnsRecordType.DS, ".", enabled: true);
            ClientX.IterativeQuestion fromParent = ClientX.SelectIterativeQuestion(
                "child.example", DnsRecordType.DS, "example", enabled: true);

            Assert.Equal("example", fromRoot.Name);
            Assert.Equal(DnsRecordType.NS, fromRoot.Type);
            Assert.False(fromRoot.IsFinal);
            Assert.Equal("child.example", fromParent.Name);
            Assert.Equal(DnsRecordType.DS, fromParent.Type);
            Assert.True(fromParent.IsFinal);
        }

        private static DnsAnswer Answer(string name, DnsRecordType type, string data) {
            return new DnsAnswer {
                Name = name,
                Type = type,
                TTL = 300,
                DataRaw = data
            };
        }
    }
}
