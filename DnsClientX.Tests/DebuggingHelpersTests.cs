using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Xunit;

namespace DnsClientX.Tests {
    /// <summary>
    /// Tests for helper methods used when debugging DNS wire traffic.
    /// </summary>
    public class DebuggingHelpersTests {
        private class CapturingLogger : InternalLogger {
            public string? LastMessage { get; private set; }
            public List<string> Messages { get; } = new();
            private readonly EventHandler<LogEventArgs> _handler;

            public CapturingLogger() {
                _handler = (_, e) => {
                    LastMessage = e.FullMessage;
                    Messages.Add(e.FullMessage);
                };
                OnDebugMessage += _handler;
            }

            public void Freeze() => OnDebugMessage -= _handler;
        }

        private static void SetLogger(InternalLogger logger) {
            FieldInfo field = typeof(Settings).GetField("_logger", BindingFlags.NonPublic | BindingFlags.Static)!;
            field.SetValue(null, logger);
        }

        /// <summary>
        /// Verifies that <see cref="DebuggingHelpers.TroubleshootingDnsWire2"/> reads two bytes and logs them.
        /// </summary>
        [Fact]
        public void TroubleshootingDnsWire2_ReadsValueAndLogs() {
            var logger = new CapturingLogger();
            SetLogger(logger);
            using var ms = new MemoryStream(new byte[] { 0x01, 0x02 });
            using var reader = new BinaryReader(ms);
            ushort result = DebuggingHelpers.TroubleshootingDnsWire2(reader, "test");
            logger.Freeze();
            Assert.Equal(0x0102, result);
            Assert.Contains("01-02", logger.LastMessage);
        }

        /// <summary>
        /// Verifies that <see cref="DebuggingHelpers.TroubleshootingDnsWire4"/> reads four bytes and logs them.
        /// </summary>
        [Fact]
        public void TroubleshootingDnsWire4_ReadsValueAndLogs() {
            var logger = new CapturingLogger();
            SetLogger(logger);
            using var ms = new MemoryStream(new byte[] { 0x01, 0x02, 0x03, 0x04 });
            using var reader = new BinaryReader(ms);
            uint result = DebuggingHelpers.TroubleshootingDnsWire4(reader, "test");
            logger.Freeze();
            Assert.Equal(0x01020304u, result);
            Assert.Contains(logger.Messages, m => m.Contains("01-02-03-04"));
        }
    }
}
