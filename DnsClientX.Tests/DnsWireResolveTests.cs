using System;
using System.Globalization;
using System.Reflection;
using System.Threading;
using Xunit;

namespace DnsClientX.Tests {
    /// <summary>
    /// Tests for core DNS wire resolution helpers.
    /// </summary>
    public class DnsWireResolveTests {
        /// <summary>
        /// Ensures DS record parsing is culture invariant.
        /// </summary>
        [Theory]
        [InlineData("en-US")]
        [InlineData("tr-TR")]
        public void ProcessRecordData_DsRecord_ConsistentAcrossCultures(string culture) {
            byte[] digest = HexToBytes("E06D44B80B8F1D39A95C0B0D7C65D08458E880409BBC683457104237C7F8EC8D");
            byte[] rdata = new byte[4 + digest.Length];
            rdata[0] = 0x4F; // key tag high byte 20326
            rdata[1] = 0x66; // key tag low byte
            rdata[2] = 0x08; // algorithm
            rdata[3] = 0x02; // digest type
            Array.Copy(digest, 0, rdata, 4, digest.Length);

            Type wireType = typeof(ClientX).Assembly.GetType("DnsClientX.DnsWire")!;
            MethodInfo method = wireType.GetMethod("ProcessRecordData", BindingFlags.NonPublic | BindingFlags.Static)!;

            CultureInfo original = Thread.CurrentThread.CurrentCulture;
            try {
                Thread.CurrentThread.CurrentCulture = new CultureInfo(culture);
                string result = (string)method.Invoke(null, new object?[] { Array.Empty<byte>(), 0, DnsRecordType.DS, rdata, (ushort)rdata.Length, 0L })!;
                Assert.Equal("20326 RSASHA256 2 e06d44b80b8f1d39a95c0b0d7c65d08458e880409bbc683457104237c7f8ec8d", result);
            } finally {
                Thread.CurrentThread.CurrentCulture = original;
            }
        }

        private static byte[] HexToBytes(string hex) {
#if NET5_0_OR_GREATER
            return Convert.FromHexString(hex);
#else
            if (hex.Length % 2 != 0) throw new ArgumentException("Hex string must have an even length", nameof(hex));
            byte[] bytes = new byte[hex.Length / 2];
            for (int i = 0; i < bytes.Length; i++) {
                bytes[i] = Convert.ToByte(hex.Substring(i * 2, 2), 16);
            }
            return bytes;
#endif
        }
    }
}