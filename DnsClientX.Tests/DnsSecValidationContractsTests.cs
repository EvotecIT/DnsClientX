using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace DnsClientX.Tests {
    /// <summary>Exercises authenticated proofs and cache lifetimes through a real root trust chain.</summary>
    public class DnsSecValidationContractsTests {
        /// <summary>Valid crypto alone cannot authenticate a wildcard's applicability.</summary>
        [Fact]
        public async Task WildcardRequiresAuthenticatedNextCloserProof() {
            using var fixture = new DnsSecSignedFixture();
            DnsResponse answer = fixture.Signed("www.example.com", DnsRecordType.A, new byte[] { 192, 0, 2, 1 }, labels: 2);
            var engine = fixture.Engine();
            Assert.Equal(DnsSecValidationStatus.Bogus, (await engine.ValidateAsync(answer, "www.example.com", DnsRecordType.A, default)).Status);
            DnsSecSignedFixture.WithProofs(answer, fixture.Nsec("example.com", "z.example.com", DnsRecordType.SOA, DnsRecordType.NS));
            Assert.Equal(DnsSecValidationStatus.Secure, (await engine.ValidateAsync(answer, "www.example.com", DnsRecordType.A, default)).Status);
        }

        /// <summary>NSEC3 wildcard proof needs only the next-closer cover; Opt-Out downgrades the verdict.</summary>
        [Theory]
        [InlineData(false, DnsSecValidationStatus.Secure)]
        [InlineData(true, DnsSecValidationStatus.Insecure)]
        public async Task WildcardNsec3ProofUsesCandidateClosestEncloser(bool optOut, DnsSecValidationStatus expected) {
            using var fixture = new DnsSecSignedFixture();
            DnsResponse answer = fixture.Signed("www.example.com", DnsRecordType.A, new byte[] { 192, 0, 2, 1 }, labels: 2);
            // A single-record circular hash span covers every name except its own owner.
            DnsSecSignedFixture.WithProofs(answer, fixture.Nsec3("example.com", "example.com", optOut, DnsRecordType.SOA));
            Assert.Equal(expected, (await fixture.Engine().ValidateAsync(answer, "www.example.com", DnsRecordType.A, default)).Status);
        }

        /// <summary>An authenticated delegation bitmap cannot deny ordinary child data.</summary>
        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task DelegationNoDataIsParentSideOnly(bool nsec3) {
            using var fixture = new DnsSecSignedFixture();
            DnsResponse proof = nsec3
                ? fixture.Nsec3("child.example.com", "z.example.com", false, DnsRecordType.NS)
                : fixture.Nsec("child.example.com", "z.example.com", DnsRecordType.NS);
            Assert.False(DnsSecProof.ProvesNoData(proof, "child.example.com", DnsRecordType.A));
            Assert.True(DnsSecProof.ProvesNoData(proof, "child.example.com", DnsRecordType.DS));
            Assert.NotEqual(DnsSecValidationStatus.Secure,
                (await fixture.Engine().ValidateAsync(proof, "child.example.com", DnsRecordType.A, default)).Status);
            Assert.Equal(DnsSecValidationStatus.Secure,
                (await fixture.Engine().ValidateAsync(proof, "child.example.com", DnsRecordType.DS, default)).Status);
        }

        /// <summary>A DNAME at the closest encloser redirects descendants rather than proving NXDOMAIN.</summary>
        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void NameErrorCannotCrossDname(bool nsec3) {
            using var fixture = new DnsSecSignedFixture();
            DnsResponse proof = nsec3
                ? fixture.Nsec3("example.com", "example.com", false, DnsRecordType.SOA, DnsRecordType.DNAME)
                : fixture.Nsec("example.com", "z.example.com", DnsRecordType.SOA, DnsRecordType.DNAME);
            Assert.False(DnsSecProof.ProvesNameError(proof, "missing.example.com"));
        }

        /// <summary>A child-signed DS must terminate with an error instead of waiting on its own keys.</summary>
        [Fact]
        public async Task SelfSignedDsCannotCreateAValidationCycle() {
            using var fixture = new DnsSecSignedFixture { DsSigner = "example.com" };
            DnsResponse answer = fixture.Signed("www.example.com", DnsRecordType.A, new byte[] { 192, 0, 2, 1 });
            using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(2));
            Task<DnsSecValidationResult> validation = fixture.Engine().ValidateAsync(answer, "www.example.com", DnsRecordType.A, cancellation.Token);
            Assert.Same(validation, await Task.WhenAny(validation, Task.Delay(TimeSpan.FromSeconds(3))));
            Assert.Equal(DnsSecValidationStatus.Bogus, (await validation).Status);
        }

        /// <summary>Replayed wildcard denial records cannot authenticate another owner's existence.</summary>
        [Fact]
        public async Task WildcardExpandedDenialCannotProveNoData() {
            using var fixture = new DnsSecSignedFixture();
            DnsResponse proof = fixture.Nsec("child.example.com", "z.example.com", DnsRecordType.NS);
            byte[] data = new byte[proof.WireAuthorities[0].RdataLength];
            Array.Copy(proof.WireMessage, data, data.Length);
            proof = fixture.Signed("child.example.com", DnsRecordType.NSEC, data, labels: 2, authority: true);
            Assert.NotEqual(DnsSecValidationStatus.Secure,
                (await fixture.Engine().ValidateAsync(proof, "child.example.com", DnsRecordType.DS, default)).Status);
        }

        /// <summary>A zone cannot supply its own unsigned-delegation proof and recurse into its pending key load.</summary>
        [Fact]
        public async Task ChildSideDsDenialIsRejectedBeforeKeyLoading() {
            using var fixture = new DnsSecSignedFixture();
            DnsResponse proof = fixture.Nsec("example.com", "z.example.com", DnsRecordType.NS);
            // No zone key lookup is needed: the claimed DS denial is signed by the child itself.
            var engine = new DnsSecValidationEngine((_, _, _) => throw new InvalidOperationException("Child signer must be rejected"), fixture.Now);
            Assert.Equal(DnsSecValidationStatus.Bogus,
                (await engine.ValidateAsync(proof, "example.com", DnsRecordType.DS, default)).Status);
        }

        /// <summary>Every RRset, RRSIG, original TTL, and trust-chain expiry bounds reusable authentication.</summary>
        [Theory]
        [InlineData(3600, 300, 3600, 1000, 3600, 3600, 300)]
        [InlineData(3600, 3600, 100, 1000, 3600, 3600, 100)]
        [InlineData(50, 3600, 3600, 1000, 3600, 3600, 50)]
        [InlineData(3600, 3600, 3600, 10, 3600, 3600, 10)]
        [InlineData(3600, 3600, 3600, 1000, 5, 3600, 5)]
        [InlineData(3600, 3600, 3600, 1000, 3600, 7, 7)]
        public async Task AuthenticatedLifetimeIncludesTrustChain(int ttl, int original, int sigTtl, int expiry, int dsExpiry, int keyExpiry, int expected) {
            using var fixture = new DnsSecSignedFixture { DsLifetime = dsExpiry, KeyLifetime = keyExpiry };
            var engine = fixture.Engine();
            DnsResponse answer = fixture.Signed("www.example.com", DnsRecordType.A, new byte[] { 192, 0, 2, 1 },
                ttl: (uint)ttl, originalTtl: (uint)original, signatureTtl: (uint)sigTtl, lifetime: expiry);
            Assert.Equal(DnsSecValidationStatus.Secure, (await engine.ValidateAsync(answer, "www.example.com", DnsRecordType.A, default)).Status);
            Assert.Equal(fixture.Now.AddSeconds(expected), engine.CacheExpiresAtUtc);
            Assert.Equal(Math.Min(Math.Min(ttl, original), Math.Min(sigTtl, expiry)), answer.Answers[0].TTL);
        }
    }
}
