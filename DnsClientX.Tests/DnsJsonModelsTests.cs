using System.Text.Json;
using DnsClientX;
using Xunit;

namespace DnsClientX.Tests {
    /// <summary>
    /// Verifies JSON serialization for request models used by DoH POST paths.
    /// </summary>
    public class DnsJsonModelsTests {
        /// <summary>
        /// ResolveRequest should serialize expected property names and omit null/zero values.
        /// </summary>
        [Fact]
        public void ResolveRequest_Serializes_WithExpectedNames() {
            var model = new ResolveRequest { Name = "example.com", Type = "A", Do = 1, Cd = 0 };
            string json = DnsJson.Serialize(model, DnsJsonContext.Default.ResolveRequest);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            Assert.Equal("example.com", root.GetProperty("name").GetString());
            Assert.Equal("A", root.GetProperty("type").GetString());
            Assert.Equal(1, root.GetProperty("do").GetInt32());
            Assert.False(root.TryGetProperty("cd", out _)); // cd omitted when zero/null
        }

        /// <summary>
        /// UpdateRequest should serialize all fields with expected JSON property names.
        /// </summary>
        [Fact]
        public void UpdateRequest_Serializes_WithExpectedNames() {
            var model = new UpdateRequest { Zone = "example.com", Name = "host", Type = "A", Data = "1.1.1.1", Ttl = 120 };
            string json = DnsJson.Serialize(model, DnsJsonContext.Default.UpdateRequest);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            Assert.Equal("example.com", root.GetProperty("zone").GetString());
            Assert.Equal("host", root.GetProperty("name").GetString());
            Assert.Equal("A", root.GetProperty("type").GetString());
            Assert.Equal("1.1.1.1", root.GetProperty("data").GetString());
            Assert.Equal(120, root.GetProperty("ttl").GetInt32());
        }
    }
}
