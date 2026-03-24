using System;
using Xunit;

namespace DnsClientX.Tests {
    /// <summary>
    /// Unit tests for <see cref="AuditEntry"/>.
    /// </summary>
    public class AuditEntryTests {
        /// <summary>
        /// Verifies that the constructor correctly initializes all properties.
        /// </summary>
        [Fact]
        public void Constructor_SetsProperties() {
            var entry = new AuditEntry("example.com", DnsRecordType.A);
            var response = new DnsResponse { Status = DnsResponseCode.NoError };
            var ex = new InvalidOperationException();
            entry.StartedAtUtc = DateTimeOffset.UtcNow;
            entry.Duration = TimeSpan.FromMilliseconds(10);
            entry.SelectionStrategy = DnsSelectionStrategy.Failover;
            entry.ResolverHost = "1.1.1.1";
            entry.ResolverPort = 53;
            entry.RequestFormat = DnsRequestFormat.DnsOverUDP;
            entry.UsedTransport = Transport.Tcp;
            entry.ServedFromCache = true;
            entry.AttemptNumber = 2;
            entry.RetryReason = "transient response: ServerFailure";
            entry.Response = response;
            entry.Exception = ex;

            Assert.Equal("example.com", entry.Name);
            Assert.Equal(DnsRecordType.A, entry.RecordType);
            Assert.True(entry.StartedAtUtc > DateTimeOffset.MinValue);
            Assert.Equal(TimeSpan.FromMilliseconds(10), entry.Duration);
            Assert.Equal(DnsSelectionStrategy.Failover, entry.SelectionStrategy);
            Assert.Equal("1.1.1.1", entry.ResolverHost);
            Assert.Equal(53, entry.ResolverPort);
            Assert.Equal(DnsRequestFormat.DnsOverUDP, entry.RequestFormat);
            Assert.Equal(Transport.Tcp, entry.UsedTransport);
            Assert.True(entry.ServedFromCache);
            Assert.Equal(2, entry.AttemptNumber);
            Assert.Equal("transient response: ServerFailure", entry.RetryReason);
            Assert.Same(response, entry.Response);
            Assert.Same(ex, entry.Exception);
        }
    }
}
