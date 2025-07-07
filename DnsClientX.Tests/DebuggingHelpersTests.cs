using System.IO;
using System.Reflection;
using Xunit;

namespace DnsClientX.Tests {
    public class DebuggingHelpersTests {
        private class CapturingLogger : InternalLogger {
            public string? LastMessage { get; private set; }
            public CapturingLogger() => OnDebugMessage += (_, e) => LastMessage = e.FullMessage;
        }

        private static void SetLogger(InternalLogger logger) {
            FieldInfo field = typeof(Settings).GetField("_logger", BindingFlags.NonPublic | BindingFlags.Static)!;
            field.SetValue(null, logger);
        }

        [Fact]
        public void TroubleshootingDnsWire2_ReadsValueAndLogs() {
            var logger = new CapturingLogger();
            SetLogger(logger);
            using var ms = new MemoryStream(new byte[] { 0x01, 0x02 });
            using var reader = new BinaryReader(ms);
            ushort result = DebuggingHelpers.TroubleshootingDnsWire2(reader, "test");
            Assert.Equal(0x0102, result);
            Assert.Contains("01-02", logger.LastMessage);
        }

        [Fact]
        public void TroubleshootingDnsWire4_ReadsValueAndLogs() {
            var logger = new CapturingLogger();
            SetLogger(logger);
            using var ms = new MemoryStream(new byte[] { 0x01, 0x02, 0x03, 0x04 });
            using var reader = new BinaryReader(ms);
            uint result = DebuggingHelpers.TroubleshootingDnsWire4(reader, "test");
            Assert.Equal(0x01020304u, result);
            Assert.Contains("01-02-03-04", logger.LastMessage);
        }
    }
}
