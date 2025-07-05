using System;
using System.Net.Http;
using System.Reflection;
using System.Threading.Tasks;
using Xunit;

namespace DnsClientX.Tests {
    public class DnsWireDeserializeTests {
        [Fact]
        public async Task DeserializeDnsWireFormat_NullInputs_ThrowsArgumentNullException() {
            Type wireType = typeof(ClientX).Assembly.GetType("DnsClientX.DnsWire")!;
            MethodInfo method = wireType.GetMethod("DeserializeDnsWireFormat", BindingFlags.Static | BindingFlags.NonPublic)!;
            var task = (Task<DnsResponse>)method.Invoke(null, new object?[] { null, false, null })!;
            await Assert.ThrowsAsync<ArgumentNullException>(() => task);
        }

        [Fact]
        public async Task DeserializeDnsWireFormat_BytesOnly_DoesNotThrow() {
            Type wireType = typeof(ClientX).Assembly.GetType("DnsClientX.DnsWire")!;
            MethodInfo method = wireType.GetMethod("DeserializeDnsWireFormat", BindingFlags.Static | BindingFlags.NonPublic)!;
            var bytes = new byte[] { 0x00, 0x01, 0x81, 0x80, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 };
            var task = (Task<DnsResponse>)method.Invoke(null, new object?[] { null, false, bytes })!;
            DnsResponse response = await task;
            Assert.NotNull(response);
        }
    }
}
