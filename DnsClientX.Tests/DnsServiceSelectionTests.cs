using Xunit;

namespace DnsClientX.Tests {
    /// <summary>
    /// Tests RFC 2782 SRV selection semantics.
    /// </summary>
    public class DnsServiceSelectionTests {
        /// <summary>A draw of zero preserves the RFC-specified small chance for a zero-weight record.</summary>
        [Fact]
        public void ZeroDrawCanSelectZeroWeightRecord() {
            var zero = new DnsSrvRecord { Target = "zero.example", Priority = 10, Weight = 0 };
            var weighted = new DnsSrvRecord { Target = "weighted.example", Priority = 10, Weight = 10 };

            DnsSrvRecord[] ordered = DnsServiceSelection.OrderForConnection(new[] { weighted, zero }, _ => 0);

            Assert.Same(zero, ordered[0]);
        }

        /// <summary>A nonzero draw selects proportionally from the cumulative weight.</summary>
        [Fact]
        public void WeightedDrawSelectsWeightedRecord() {
            var light = new DnsSrvRecord { Target = "light.example", Priority = 10, Weight = 1 };
            var heavy = new DnsSrvRecord { Target = "heavy.example", Priority = 10, Weight = 9 };

            DnsSrvRecord[] ordered = DnsServiceSelection.OrderForConnection(new[] { light, heavy }, maximum => maximum - 1);

            Assert.Same(heavy, ordered[0]);
        }

        /// <summary>Higher numeric priorities are not considered until lower priorities are exhausted.</summary>
        [Fact]
        public void ExhaustsLowestPriorityFirst() {
            var later = new DnsSrvRecord { Target = "later.example", Priority = 20, Weight = 100 };
            var first = new DnsSrvRecord { Target = "first.example", Priority = 10, Weight = 0 };

            DnsSrvRecord[] ordered = DnsServiceSelection.OrderForConnection(new[] { later, first }, _ => 0);

            Assert.Same(first, ordered[0]);
            Assert.Same(later, ordered[1]);
        }
    }
}
