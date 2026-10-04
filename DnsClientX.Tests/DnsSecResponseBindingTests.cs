using System;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Xunit;

namespace DnsClientX.Tests {
    /// <summary>Checks that DNSSEC verdicts and projected answers describe the same question.</summary>
    public class DnsSecResponseBindingTests {
        /// <summary>A signed positive RRset cannot authenticate an NXDOMAIN response status.</summary>
        [Theory]
        [InlineData(DnsResponseCode.NoError, DnsSecValidationStatus.Secure)]
        [InlineData(DnsResponseCode.NXDomain, DnsSecValidationStatus.Bogus)]
        public async Task TerminalAnswerMustAgreeWithResponseStatus(
            DnsResponseCode status, DnsSecValidationStatus expected) {
            using var fixture = new DnsSecSignedFixture();
            DnsResponse answer = fixture.Signed("www.example.com", DnsRecordType.A, new byte[] { 192, 0, 2, 1 });
            answer.Status = status;

            Assert.Equal(expected,
                (await fixture.Engine().ValidateAsync(answer, "www.example.com", DnsRecordType.A, default)).Status);
        }

        /// <summary>A signed alias may precede NXDOMAIN only with proof for its final target.</summary>
        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task AliasNxDomainRequiresFinalTargetDenial(bool validProof) {
            using var fixture = new DnsSecSignedFixture();
            DnsResponse alias = fixture.Signed("alias.example.com", DnsRecordType.CNAME,
                DnsWireNameCodec.ToCanonicalWire("missing.example.com"));
            DnsResponse proof = validProof
                ? fixture.Nsec3("example.com", "example.com", false, DnsRecordType.SOA)
                : fixture.Nsec("z.example.com", "zz.example.com", DnsRecordType.SOA);
            DnsSecSignedFixture.WithProofs(alias, proof);
            alias.Status = DnsResponseCode.NXDomain;

            DnsSecValidationResult ordinary = await fixture.Engine()
                .ValidateAsync(alias, "alias.example.com", DnsRecordType.A, default);
            DnsSecValidationResult segment = await fixture.Engine()
                .ValidateAliasAsync(alias, "alias.example.com", DnsRecordType.A, default);
            if (validProof) {
                Assert.Equal(DnsSecValidationStatus.Secure, ordinary.Status);
                Assert.Equal(DnsSecValidationStatus.Secure, segment.Status);
            } else {
                Assert.NotEqual(DnsSecValidationStatus.Secure, ordinary.Status);
                Assert.NotEqual(DnsSecValidationStatus.Secure, segment.Status);
            }
        }

        /// <summary>An unrelated signed RRset does not turn a name-error proof into a positive answer.</summary>
        [Fact]
        public async Task NxDomainCannotUseAnUnrelatedPositiveRrset() {
            using var fixture = new DnsSecSignedFixture();
            DnsResponse answer = fixture.Signed("other.example.com", DnsRecordType.A, new byte[] { 192, 0, 2, 2 });
            DnsSecSignedFixture.WithProofs(answer,
                fixture.Nsec3("example.com", "example.com", false, DnsRecordType.SOA));
            answer.Status = DnsResponseCode.NXDomain;

            Assert.Equal(DnsSecValidationStatus.Bogus,
                (await fixture.Engine().ValidateAsync(answer, "missing.example.com", DnsRecordType.A, default)).Status);
        }

        /// <summary>A name-error cannot coexist with any positive RRset at its denied final owner.</summary>
        [Fact]
        public async Task AliasNxDomainRejectsPositiveRrsetAtDeniedFinalOwner() {
            using var fixture = new DnsSecSignedFixture();
            DnsResponse answer = fixture.Signed("alias.example.com", DnsRecordType.CNAME,
                DnsWireNameCodec.ToCanonicalWire("missing.example.com"));
            DnsResponse positive = fixture.Signed("missing.example.com", DnsRecordType.A,
                new byte[] { 192, 0, 2, 3 });
            int offset = answer.WireMessage.Length;
            answer.WireMessage = answer.WireMessage.Concat(positive.WireMessage).ToArray();
            answer.WireAnswers = answer.WireAnswers.Concat(positive.WireAnswers.Select(record =>
                new DnsWireResourceRecord(record.Name, record.Type, record.Class, record.Ttl,
                    record.RawTtl, record.RdataOffset + offset, record.RdataLength, record.Data))).ToArray();
            answer.Answers = answer.Answers.Concat(positive.Answers).ToArray();
            DnsSecSignedFixture.WithProofs(answer,
                fixture.Nsec3("example.com", "example.com", false, DnsRecordType.SOA));
            answer.Status = DnsResponseCode.NXDomain;

            Assert.Equal(DnsSecValidationStatus.Bogus,
                (await fixture.Engine().ValidateAsync(answer, "alias.example.com", DnsRecordType.AAAA, default)).Status);
            Assert.Equal(DnsSecValidationStatus.Bogus,
                (await fixture.Engine().ValidateAliasAsync(answer, "alias.example.com", DnsRecordType.AAAA, default)).Status);
        }

        /// <summary>A signed DNAME and its synthesized CNAME can redirect to a denied final target.</summary>
        [Fact]
        public async Task DnameNxDomainAuthenticatesItsFinalTargetDenial() {
            using var fixture = new DnsSecSignedFixture();
            DnsResponse answer = fixture.Signed("old.example.com", DnsRecordType.DNAME,
                DnsWireNameCodec.ToCanonicalWire("new.example.com"));
            byte[] target = DnsWireNameCodec.ToCanonicalWire("www.new.example.com");
            int offset = answer.WireMessage.Length;
            answer.WireMessage = answer.WireMessage.Concat(target).ToArray();
            var synthesized = new DnsWireResourceRecord("www.old.example.com", DnsRecordType.CNAME,
                1, 3600, 3600, offset, (ushort)target.Length, "www.new.example.com");
            answer.WireAnswers = answer.WireAnswers.Concat(new[] { synthesized }).ToArray();
            answer.Answers = answer.Answers.Concat(new[] { new DnsAnswer {
                Name = synthesized.Name, Type = DnsRecordType.CNAME, TTL = synthesized.Ttl,
                DataRaw = synthesized.Data
            } }).ToArray();
            DnsSecSignedFixture.WithProofs(answer,
                fixture.Nsec3("example.com", "example.com", false, DnsRecordType.SOA));
            answer.Status = DnsResponseCode.NXDomain;

            Assert.Equal(DnsSecValidationStatus.Secure,
                (await fixture.Engine().ValidateAsync(answer, "www.old.example.com", DnsRecordType.A, default)).Status);
            Assert.Equal(DnsSecValidationStatus.Secure,
                (await fixture.Engine().ValidateAliasAsync(answer, "www.old.example.com", DnsRecordType.A, default)).Status);
        }

        /// <summary>A DNAME rewrite preserves an escaped data dot at the end of the first label.</summary>
        [Fact]
        public async Task EscapedDotDnameRetainsTheExactTargetAndAlias() {
            const string query = @"a\..old.example.com";
            const string targetName = @"a\..new.example.com";
            using var fixture = new DnsSecSignedFixture();
            DnsResponse answer = fixture.Signed("old.example.com", DnsRecordType.DNAME,
                DnsWireNameCodec.ToCanonicalWire("new.example.com"));
            Assert.Equal(DnsWireNameCodec.Canonical(targetName),
                DnsWireNameCodec.Canonical(ClientX.FindAliasTarget(answer, query)!));

            byte[] target = DnsWireNameCodec.ToCanonicalWire(targetName);
            int offset = answer.WireMessage.Length;
            answer.WireMessage = answer.WireMessage.Concat(target).ToArray();
            var synthesized = new DnsWireResourceRecord(query, DnsRecordType.CNAME,
                1, 3600, 3600, offset, (ushort)target.Length, targetName);
            answer.WireAnswers = answer.WireAnswers.Concat(new[] { synthesized }).ToArray();
            answer.Answers = answer.Answers.Concat(new[] { new DnsAnswer {
                Name = query, Type = DnsRecordType.CNAME, TTL = synthesized.Ttl,
                DataRaw = targetName
            } }).ToArray();
            DnsSecSignedFixture.WithProofs(answer,
                fixture.Nsec3("example.com", "example.com", false, DnsRecordType.SOA));
            answer.Status = DnsResponseCode.NXDomain;

            Assert.Equal(DnsSecValidationStatus.Secure,
                (await fixture.Engine().ValidateAsync(answer, query, DnsRecordType.A, default)).Status);
            using var client = new ClientX(DnsEndpoint.Cloudflare);
            client.ResolverOverride = (_, _, _) => Task.FromResult(answer);
            DnsResponse filtered = await client.ResolveFilter(query, DnsRecordType.A, "unmatched",
                new ResolveFilterOptions(true), retryOnTransient: false);
            Assert.Equal(2, filtered.Answers.Length);
            Assert.Contains(filtered.Answers, item => item.Type == DnsRecordType.DNAME);
            Assert.Contains(filtered.Answers, item => item.Type == DnsRecordType.CNAME);
        }

        /// <summary>Contradictory statuses on signed trust material cannot establish a secure chain.</summary>
        [Theory]
        [InlineData(DnsResponseCode.NXDomain, DnsResponseCode.NoError)]
        [InlineData(DnsResponseCode.NoError, DnsResponseCode.NXDomain)]
        public async Task PositiveTrustMaterialRequiresNoError(
            DnsResponseCode dnskeyStatus, DnsResponseCode dsStatus) {
            using var fixture = new DnsSecSignedFixture();
            DnsResponse answer = fixture.Signed("www.example.com", DnsRecordType.A, new byte[] { 192, 0, 2, 1 });

            Assert.NotEqual(DnsSecValidationStatus.Secure,
                (await fixture.Engine(dnskeyStatus: dnskeyStatus, dsStatus: dsStatus)
                    .ValidateAsync(answer, "www.example.com", DnsRecordType.A, default)).Status);
        }

        /// <summary>Ordinary answers expose only the requested owner, while explicit all-types stays diagnostic.</summary>
        [Fact]
        public async Task OrdinaryProjectionOmitsUnrelatedSignedRrset() {
            using var fixture = new DnsSecSignedFixture();
            DnsResponse answer = fixture.Signed("www.example.com", DnsRecordType.A, new byte[] { 192, 0, 2, 1 });
            DnsResponse unrelated = fixture.Signed("other.example.com", DnsRecordType.A, new byte[] { 192, 0, 2, 2 });
            int offset = answer.WireMessage.Length;
            answer.WireMessage = answer.WireMessage.Concat(unrelated.WireMessage).ToArray();
            answer.WireAnswers = answer.WireAnswers.Concat(unrelated.WireAnswers.Select(record =>
                new DnsWireResourceRecord(record.Name, record.Type, record.Class, record.Ttl,
                    record.RawTtl, record.RdataOffset + offset, record.RdataLength, record.Data))).ToArray();
            answer.Answers = answer.Answers.Concat(unrelated.Answers).ToArray();
            DnsAnswer[] completeAnswers = answer.Answers;

            Assert.Equal(DnsSecValidationStatus.Secure,
                (await fixture.Engine().ValidateAsync(answer, "www.example.com", DnsRecordType.A, default)).Status);
            ClientX.ApplyAnswerProjection(answer, "www.example.com", DnsRecordType.A, returnAllTypes: false);
            Assert.True(answer.RequestedAnswerPresent);
            Assert.Equal("www.example.com", Assert.Single(answer.Answers).Name);

            var diagnostic = new DnsResponse { Answers = completeAnswers };
            ClientX.ApplyAnswerProjection(diagnostic, "www.example.com", DnsRecordType.A, returnAllTypes: true);
            Assert.Equal(2, diagnostic.Answers.Length);
        }

        /// <summary>Equivalent escape spellings identify the same requested wire name.</summary>
        [Fact]
        public void OrdinaryProjectionRetainsAnEscapedNameAnswer() {
            var response = new DnsResponse { Answers = new[] {
                new DnsAnswer { Name = @"Living\.Room._ipp._tcp.local", Type = DnsRecordType.PTR,
                    TTL = 60, DataRaw = "printer.local" }
            } };

            ClientX.ApplyAnswerProjection(response, @"Living\046Room._ipp._tcp.local.",
                DnsRecordType.PTR, returnAllTypes: false);

            Assert.True(response.RequestedAnswerPresent);
            Assert.Single(response.Answers);
        }

        /// <summary>Malformed unrelated provider names do not prevent a valid answer projection.</summary>
        [Fact]
        public void OrdinaryProjectionIgnoresMalformedUnrelatedOwner() {
            var response = new DnsResponse { Answers = new[] {
                new DnsAnswer { Name = "www.example.com", Type = DnsRecordType.A,
                    TTL = 60, DataRaw = "192.0.2.1" },
                new DnsAnswer { Name = "bad..example.com", Type = DnsRecordType.A,
                    TTL = 60, DataRaw = "192.0.2.2" }
            } };

            ClientX.ApplyAnswerProjection(response, "www.example.com", DnsRecordType.A,
                returnAllTypes: false);

            Assert.True(response.RequestedAnswerPresent);
            Assert.Equal("www.example.com", Assert.Single(response.Answers).Name);
        }

        /// <summary>Presentation escapes cannot create a false child-zone or DNAME relationship.</summary>
        [Fact]
        public void EscapedDotsDoNotCrossDnsLabelBoundaries() {
            const string query = @"a\.b.example.com";
            Assert.False(DnsWireNameCodec.IsSubdomainOrEqual(query, "b.example.com"));
            Assert.False(DnsSecValidationEngine.IsNameWithinZone(query, "b.example.com"));
            Assert.Equal(@"a\..", DnsWireNameCodec.RewriteDnameTarget(
                @"a\..old.example.com", "old.example.com", "."));

            using var fixture = new DnsSecSignedFixture();
            DnsResponse dname = fixture.Signed("b.example.com", DnsRecordType.DNAME,
                DnsWireNameCodec.ToCanonicalWire("attacker.example.com"));
            Assert.Null(ClientX.FindAliasTarget(dname, query));
            Assert.True(DnsSecValidationEngine.TryFollowAnswerChain(dname.WireAnswers,
                query, DnsRecordType.A, out string finalName, out bool terminal, out _));
            Assert.Equal(DnsWireNameCodec.Canonical(query), finalName);
            Assert.False(terminal);
        }

        /// <summary>Alias-inclusive filtered queries cannot surface unrelated signed owners.</summary>
        [Theory]
        [InlineData(false, false)]
        [InlineData(false, true)]
        [InlineData(true, false)]
        [InlineData(true, true)]
        public async Task AliasInclusiveFilterOnlyExposesRequestedOwner(bool useRegex, bool useArray) {
            using var fixture = new DnsSecSignedFixture();
            DnsResponse answer = fixture.Signed("www.example.com", DnsRecordType.A,
                new byte[] { 192, 0, 2, 1 });
            AppendSignedAnswer(answer, fixture.Signed("other.example.com", DnsRecordType.A,
                new byte[] { 192, 0, 2, 2 }));
            AppendSignedAnswer(answer, fixture.Signed("other.example.com", DnsRecordType.CNAME,
                DnsWireNameCodec.ToCanonicalWire("elsewhere.example.com")));
            Assert.Equal(DnsSecValidationStatus.Secure,
                (await fixture.Engine().ValidateAsync(answer, "www.example.com", DnsRecordType.A, default)).Status);
            answer.Answers = answer.Answers.Select(item => item.Type == DnsRecordType.A
                ? new DnsAnswer { Name = item.Name, Type = item.Type, TTL = item.TTL,
                    DataRaw = item.Name == "www.example.com" ? "192.0.2.1" : "192.0.2.2" }
                : item).ToArray();

            using var client = new ClientX(DnsEndpoint.Cloudflare);
            client.ResolverOverride = (_, _, _) => Task.FromResult(answer);
            var options = new ResolveFilterOptions(true);
            DnsResponse filtered = useArray
                ? useRegex
                    ? Assert.Single(await client.ResolveFilter(new[] { "www.example.com" }, DnsRecordType.A,
                        new Regex("192\\."), options, retryOnTransient: false))
                    : Assert.Single(await client.ResolveFilter(new[] { "www.example.com" }, DnsRecordType.A,
                        "192.", options, retryOnTransient: false))
                : useRegex
                    ? await client.ResolveFilter("www.example.com", DnsRecordType.A,
                        new Regex("192\\."), options, retryOnTransient: false)
                    : await client.ResolveFilter("www.example.com", DnsRecordType.A,
                        "192.", options, retryOnTransient: false);

            Assert.Equal("www.example.com", Assert.Single(filtered.Answers).Name);
        }

        private static void AppendSignedAnswer(DnsResponse answer, DnsResponse additional) {
            int offset = answer.WireMessage.Length;
            answer.WireMessage = answer.WireMessage.Concat(additional.WireMessage).ToArray();
            answer.WireAnswers = answer.WireAnswers.Concat(additional.WireAnswers.Select(record =>
                new DnsWireResourceRecord(record.Name, record.Type, record.Class, record.Ttl,
                    record.RawTtl, record.RdataOffset + offset, record.RdataLength, record.Data))).ToArray();
            answer.Answers = answer.Answers.Concat(additional.Answers).ToArray();
        }
    }
}
