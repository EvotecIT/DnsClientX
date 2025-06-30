using System;
using System.Buffers.Binary;
using System.IO;

namespace DnsClientX {
    internal static class DebuggingHelpers {
        /// <summary>
        /// Troubleshooting the DNS wire with 2 bytes.
        /// </summary>
        /// <param name="reader">The reader.</param>
        /// <param name="description">The description.</param>
        /// <param name="display">if set to <c>true</c> [display].</param>
        /// <returns></returns>
        internal static ushort TroubleshootingDnsWire2(BinaryReader reader, string description, bool display = true) {
            byte[] bytes = reader.ReadBytes(2);
            if (display) {
                Settings.Logger.WriteDebug(description + ": " + BitConverter.ToString(bytes));
            }
            return BinaryPrimitives.ReadUInt16BigEndian(bytes);
        }

        /// <summary>
        /// Troubleshooting the DNS wire with 2 bytes.
        /// </summary>
        /// <param name="reader">The reader.</param>
        /// <param name="description">The description.</param>
        /// <param name="display">if set to <c>true</c> [display].</param>
        /// <returns></returns>
        internal static uint TroubleshootingDnsWire4(BinaryReader reader, string description, bool display = true) {
            byte[] bytes = reader.ReadBytes(4);
            if (display) {
                Settings.Logger.WriteDebug(description + ": " + BitConverter.ToString(bytes));
            }
            return BinaryPrimitives.ReadUInt32BigEndian(bytes);
        }
    }
}
