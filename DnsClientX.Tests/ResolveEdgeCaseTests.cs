using System;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using Xunit;

namespace DnsClientX.Tests {
    /// <summary>
    /// Tests edge cases and invalid parameters for resolve APIs.
    /// </summary>
    public class ResolveEdgeCaseTests {
        /// <summary>
        /// Ensures
        /// <see cref="ClientX.ResolveAllSync(string,DnsRecordType,bool,bool,bool,int,int,System.Threading.CancellationToken)"/>
        /// throws when the name is invalid.
        /// </summary>
        [Fact]
        public void ResolveAllSync_InvalidName_Throws() {
            using var client = new ClientX();
            Assert.Throws<ArgumentNullException>(() => client.ResolveAllSync(string.Empty));
        }

        /// <summary>
        /// Ensures <see cref="ClientX.ResolveFilter(string,DnsRecordType,string,bool,int,int,bool,bool,int?,System.Threading.CancellationToken)"/> throws on invalid name.
        /// </summary>
        [Fact]
        public async Task ResolveFilter_InvalidName_Throws() {
            using var client = new ClientX();
            await Assert.ThrowsAsync<ArgumentNullException>(
                () => client.ResolveFilter(string.Empty, DnsRecordType.A, "filter", retryOnTransient: false));
        }

        /// <summary>
        /// Ensures resolving with a regex filter throws when the name is invalid.
        /// </summary>
        [Fact]
        public async Task ResolveFilterRegex_InvalidName_Throws() {
            using var client = new ClientX();
            await Assert.ThrowsAsync<ArgumentNullException>(
                () => client.ResolveFilter(string.Empty, DnsRecordType.A, new Regex("test"), retryOnTransient: false));
        }
    }
}
