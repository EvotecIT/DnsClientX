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
            entry.Response = response;
            entry.Exception = ex;

            Assert.Equal("example.com", entry.Name);
            Assert.Equal(DnsRecordType.A, entry.RecordType);
            Assert.Same(response, entry.Response);
            Assert.Same(ex, entry.Exception);
        }
    }
}
