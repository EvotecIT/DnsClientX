using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace DnsClientX.Tests {
    public class QueryDnsDisposalTests {
        [Fact]
        public async Task QueryDns_CancelledToken_ShouldDisposeClient() {
            ClientX? created = null;
            ClientX.OnClientCreated = c => created = c;

            using var cts = new CancellationTokenSource();
            cts.Cancel();

            await Assert.ThrowsAsync<TaskCanceledException>(() => ClientX.QueryDns("example.com", DnsRecordType.A, cancellationToken: cts.Token));

            Assert.NotNull(created);
            var disposedField = typeof(ClientX).GetField("_disposed", BindingFlags.NonPublic | BindingFlags.Instance)!;
            Assert.True((bool)disposedField.GetValue(created!));

            ClientX.OnClientCreated = null;
        }
    }
}
