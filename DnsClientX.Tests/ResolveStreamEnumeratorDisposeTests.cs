using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace DnsClientX.Tests {
    /// <summary>
    /// Tests that enumerators returned from <see cref="ClientX.ResolveStream"/> are disposed correctly.
    /// </summary>
    public class ResolveStreamEnumeratorDisposeTests {
        private class TrackingEnumerable<T> : IEnumerable<T> {
            private readonly T[] _items;
            public int DisposeCount { get; private set; }

            public TrackingEnumerable(params T[] items) => _items = items;

            public IEnumerator<T> GetEnumerator() => new TrackingEnumerator(this, _items);

            IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

            private class TrackingEnumerator : IEnumerator<T> {
                private readonly TrackingEnumerable<T> _parent;
                private readonly T[] _items;
                private int _index = -1;

                public TrackingEnumerator(TrackingEnumerable<T> parent, T[] items) {
                    _parent = parent;
                    _items = items;
                }

                public T Current => _items[_index];

                object IEnumerator.Current => Current!;

                public bool MoveNext() => ++_index < _items.Length;

                public void Reset() => _index = -1;

                public void Dispose() {
                    _parent.DisposeCount++;
                }
            }
        }

        /// <summary>
        /// Enumerates results and ensures underlying enumerators are disposed.
        /// </summary>
        [Fact]
        public async Task ResolveStream_ShouldDisposeEnumerators() {
            using var client = new ClientX(DnsEndpoint.System);
            var names = new TrackingEnumerable<string>("example.com");
            var types = new TrackingEnumerable<DnsRecordType>(DnsRecordType.A);

            MethodInfo method = typeof(ClientX).GetMethod(
                "ResolveStream",
                BindingFlags.NonPublic | BindingFlags.Instance,
                null,
                new[] {
                    typeof(IEnumerable<string>),
                    typeof(IEnumerable<DnsRecordType>),
                    typeof(bool),
                    typeof(bool),
                    typeof(bool),
                    typeof(bool),
                    typeof(int),
                    typeof(int),
                    typeof(CancellationToken)
                },
                null)!;

            var enumerable = (IAsyncEnumerable<DnsResponse>)method.Invoke(
                client,
                new object[] { names, types, false, false, false, false, 3, 200, CancellationToken.None })!;

            await foreach (var _ in enumerable) {
            }

            Assert.Equal(1, names.DisposeCount);
            Assert.Equal(1, types.DisposeCount);
        }
    }
}
