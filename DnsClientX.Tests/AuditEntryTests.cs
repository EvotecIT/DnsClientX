using System;
using Xunit;

namespace DnsClientX.Tests {
    /// <summary>
    /// Unit tests for <see cref="AuditEntry"/>.
    /// </summary>
    public class AuditEntryTests {
        /// <summary>
        /// The public constructor retains the query identity used by diagnostic consumers.
        /// </summary>
        [Fact]
        public void Constructor_SetsProperties() {
            var entry = new AuditEntry("example.com", DnsRecordType.A);

            Assert.Equal("example.com", entry.Name);
            Assert.Equal(DnsRecordType.A, entry.RecordType);
        }
    }
}
