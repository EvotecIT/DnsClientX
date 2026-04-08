namespace DnsClientX.Tests {
    /// <summary>
    /// Tests transport and request-format mapping helpers.
    /// </summary>
    public class DnsRequestFormatMapperTests {
        /// <summary>
        /// Ensures default transport mapping returns the expected request format.
        /// </summary>
        [Fact]
        public void FromTransport_Doh_ReturnsDnsOverHttps() {
            Assert.Equal(DnsRequestFormat.DnsOverHttps, DnsRequestFormatMapper.FromTransport(Transport.Doh));
        }

        /// <summary>
        /// Ensures non-HTTP transports still map to their native request formats.
        /// </summary>
        [Fact]
        public void FromTransport_Dot_ReturnsDnsOverTls() {
            Assert.Equal(DnsRequestFormat.DnsOverTLS, DnsRequestFormatMapper.FromTransport(Transport.Dot));
        }

        /// <summary>
        /// Ensures HTTP-based request formats map back to DoH transport.
        /// </summary>
        [Fact]
        public void ToTransport_DnsOverHttpsJsonPost_ReturnsDoh() {
            Assert.Equal(Transport.Doh, DnsRequestFormatMapper.ToTransport(DnsRequestFormat.DnsOverHttpsJSONPOST));
        }

        /// <summary>
        /// Ensures non-HTTP request formats map back to their native transports.
        /// </summary>
        [Fact]
        public void ToTransport_DnsOverTcp_ReturnsTcp() {
            Assert.Equal(Transport.Tcp, DnsRequestFormatMapper.ToTransport(DnsRequestFormat.DnsOverTCP));
        }
    }
}
