using System.Threading.Tasks;
using Xunit;

namespace DnsClientX.Tests {
    /// <summary>
    /// Protects DNS presentation names used by DNS-SD instance labels.
    /// </summary>
    public class DnsWireEscapedNameTests {
        /// <summary>Escaped dots and spaces remain inside one wire-format label.</summary>
        [Fact]
        public async Task SerializesEscapedPresentationLabel() {
            const string name = @"Living\.Room\032Printer._ipp._tcp.local.";
            var query = new DnsMessage(name, DnsRecordType.PTR, requestDnsSec: false);

            DnsResponse parsed = await DnsWire.DeserializeDnsWireFormat(null, false, query.SerializeDnsWireFormat());

            Assert.Equal(name.TrimEnd('.'), parsed.Questions[0].Name);
        }
    }
}
