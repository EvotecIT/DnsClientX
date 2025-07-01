using System;
using System.Reflection;
using System.Threading;
using Xunit;

namespace DnsClientX.Tests {
    public class DnsResponseCacheTests {
        [Fact]
        public void ShouldStoreAndRetrieve() {
            var cache = new DnsResponseCache();
            var response = new DnsResponse { Status = DnsResponseCode.NoError };
            cache.Set("a", response, TimeSpan.FromSeconds(1));
            Assert.True(cache.TryGet("a", out var cached));
            Assert.Same(response, cached);
        }

        [Fact]
        public void ShouldEvictAfterExpiration() {
            var cache = new DnsResponseCache();
            var response = new DnsResponse { Status = DnsResponseCode.NoError };
            cache.Set("a", response, TimeSpan.FromMilliseconds(100));
            Thread.Sleep(150);
            Assert.False(cache.TryGet("a", out _));
        }

        [Fact]
        public void ClientConstructorEnablesCache() {
            using var client = new ClientX(enableCache: true);
            PropertyInfo property = typeof(ClientX).GetProperty("CacheEnabled")!;
            Assert.True((bool)property.GetValue(client)!);
        }
    }
}
