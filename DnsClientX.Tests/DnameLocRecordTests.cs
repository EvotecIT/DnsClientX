using System;
using System.Reflection;
using Xunit;

namespace DnsClientX.Tests {
    /// <summary>
    /// Tests for decoding DNAME and LOC record wire formats.
    /// </summary>
    public class DnameLocRecordTests {
        private static byte[] EncodeDnsName(string name) {
            name = name.TrimEnd('.');
            var parts = name.Split('.');
            using var ms = new System.IO.MemoryStream();
            foreach (var part in parts) {
                ms.WriteByte((byte)part.Length);
                var bytes = System.Text.Encoding.ASCII.GetBytes(part);
                ms.Write(bytes, 0, bytes.Length);
            }
            ms.WriteByte(0);
            return ms.ToArray();
        }

        /// <summary>
        /// Verifies that DNAME record wire data is decoded correctly.
        /// </summary>
        [Fact]
        public void ProcessRecordData_DecodesDnameWireFormat() {
            byte[] rdata = EncodeDnsName("target.example.com.");
            Type wireType = typeof(ClientX).Assembly.GetType("DnsClientX.DnsWire")!;
            MethodInfo method = wireType.GetMethod("ProcessRecordData", BindingFlags.NonPublic | BindingFlags.Static)!;
            var result = (string)method.Invoke(null, new object?[] { Array.Empty<byte>(), 0, DnsRecordType.DNAME, rdata, (ushort)rdata.Length, 0L })!;
            Assert.Equal("target.example.com.", result);
        }

        /// <summary>
        /// Verifies that LOC record wire data is decoded correctly.
        /// </summary>
        [Fact]
        public void ProcessRecordData_DecodesLocWireFormat() {
            byte[] rdata = new byte[16];
            rdata[0] = 0x00; // version
            rdata[1] = 0x12; // size 1m
            rdata[2] = 0x16; // horiz prec 10000m
            rdata[3] = 0x13; // vert prec 10m
            System.Buffers.Binary.BinaryPrimitives.WriteUInt32BigEndian(rdata.AsSpan(4), 0x80000000u);
            System.Buffers.Binary.BinaryPrimitives.WriteUInt32BigEndian(rdata.AsSpan(8), 0x80000000u);
            System.Buffers.Binary.BinaryPrimitives.WriteUInt32BigEndian(rdata.AsSpan(12), 0x00989680u);
            Type wireType = typeof(ClientX).Assembly.GetType("DnsClientX.DnsWire")!;
            MethodInfo method = wireType.GetMethod("ProcessRecordData", BindingFlags.NonPublic | BindingFlags.Static)!;
            var result = (string)method.Invoke(null, new object?[] { Array.Empty<byte>(), 0, DnsRecordType.LOC, rdata, (ushort)rdata.Length, 0L })!;
            Assert.Equal("0 0 0.000 N 0 0 0.000 E 0m 1m 10000m 10m", result);
        }
    }
}
