using System;
using System.Collections.Generic;
using System.Linq;

namespace DnsClientX {
    /// <summary>
    /// Applies RFC 2782 priority and weighted selection to SRV records.
    /// </summary>
    public static class DnsServiceSelection {
#if NET6_0_OR_GREATER
#else
        private static readonly object RandomLock = new();
        private static readonly Random Random = new();
#endif

        /// <summary>
        /// Produces an SRV connection-attempt order. Lower priorities are exhausted first and
        /// records at the same priority are selected proportionally to their weight.
        /// </summary>
        /// <param name="records">The SRV records to order.</param>
        /// <returns>A new array in connection-attempt order.</returns>
        public static DnsSrvRecord[] OrderForConnection(IEnumerable<DnsSrvRecord> records) {
            if (records == null) {
                throw new ArgumentNullException(nameof(records));
            }

            return OrderForConnection(records, NextRandomValue);
        }

        internal static DnsSrvRecord[] OrderForConnection(
            IEnumerable<DnsSrvRecord> records,
            Func<long, long> randomValue) {
            if (randomValue == null) {
                throw new ArgumentNullException(nameof(randomValue));
            }

            var ordered = new List<DnsSrvRecord>();
            foreach (IGrouping<int, DnsSrvRecord> priorityGroup in records.OrderBy(record => record.Priority).GroupBy(record => record.Priority)) {
                var remaining = priorityGroup.ToList();
                while (remaining.Count > 0) {
                    long totalWeight = remaining.Sum(record => (long)Math.Max(0, record.Weight));
                    int selectedIndex;
                    if (totalWeight == 0) {
                        long selected = randomValue(remaining.Count);
                        selectedIndex = (int)Math.Max(0, Math.Min(remaining.Count - 1, selected));
                    } else {
                        // RFC 2782 puts zero-weight records at the beginning before calculating the
                        // running sum so they retain the specified small chance when the draw is zero.
                        remaining = remaining.OrderBy(record => record.Weight == 0 ? 0 : 1).ToList();
                        long draw = randomValue(totalWeight + 1);
                        long running = 0;
                        selectedIndex = remaining.Count - 1;
                        for (int index = 0; index < remaining.Count; index++) {
                            running += Math.Max(0, remaining[index].Weight);
                            if (running >= draw) {
                                selectedIndex = index;
                                break;
                            }
                        }
                    }

                    ordered.Add(remaining[selectedIndex]);
                    remaining.RemoveAt(selectedIndex);
                }
            }

            return ordered.ToArray();
        }

        private static long NextRandomValue(long exclusiveMaximum) {
            if (exclusiveMaximum <= 1) {
                return 0;
            }

#if NET6_0_OR_GREATER
            return Random.Shared.NextInt64(exclusiveMaximum);
#else
            lock (RandomLock) {
                return (long)(Random.NextDouble() * exclusiveMaximum);
            }
#endif
        }
    }
}
