using System;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using Xunit;

namespace DnsClientX.Tests {
    public class ResolveEdgeCaseTests {
        [Fact]
        public void ResolveAllSync_InvalidName_Throws() {
            using var client = new ClientX();
            Assert.Throws<ArgumentNullException>(() => client.ResolveAllSync(string.Empty));
        }

        [Fact]
        public async Task ResolveFilter_InvalidName_Throws() {
            using var client = new ClientX();
            await Assert.ThrowsAsync<ArgumentNullException>(
                () => client.ResolveFilter(string.Empty, DnsRecordType.A, "filter", retryOnTransient: false));
        }

        [Fact]
        public async Task ResolveFilterRegex_InvalidName_Throws() {
            using var client = new ClientX();
            await Assert.ThrowsAsync<ArgumentNullException>(
                () => client.ResolveFilter(string.Empty, DnsRecordType.A, new Regex("test"), retryOnTransient: false));
        }
    }
}
