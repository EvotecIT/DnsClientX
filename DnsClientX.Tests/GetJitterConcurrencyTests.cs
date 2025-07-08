using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Xunit;

namespace DnsClientX.Tests {
    public class GetJitterConcurrencyTests {
        [Fact]
        public async Task GetJitter_ShouldBeThreadSafe() {
            MethodInfo method = typeof(ClientX).GetMethod("GetJitter", BindingFlags.NonPublic | BindingFlags.Static)!;
            var tasks = Enumerable.Range(0, 50)
                .Select(_ => Task.Run(() => (int)method.Invoke(null, new object[] { 100 })!));
            var results = await Task.WhenAll(tasks);
            Assert.Equal(50, results.Length);
            Assert.All(results, r => Assert.InRange(r, 0, 100));
        }
    }
}
