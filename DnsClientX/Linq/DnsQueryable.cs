using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;

namespace DnsClientX.Linq {
    /// <summary>
    /// Represents a LINQ queryable source of <see cref="DnsAnswer"/>.
    /// </summary>
    public class DnsQueryable : IQueryable<DnsAnswer> {
        internal DnsQueryProvider ProviderInternal { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="DnsQueryable"/> class.
        /// </summary>
        /// <param name="client">DNS client.</param>
        /// <param name="names">Domain names to resolve.</param>
        /// <param name="type">Record type.</param>
        public DnsQueryable(ClientX client, IEnumerable<string> names, DnsRecordType type) {
            ProviderInternal = new DnsQueryProvider(client, names, type);
            Expression = Expression.Constant(this);
        }

        internal DnsQueryable(DnsQueryProvider provider, Expression expression) {
            ProviderInternal = provider;
            Expression = expression;
        }

        /// <inheritdoc />
        public Type ElementType => typeof(DnsAnswer);

        /// <inheritdoc />
        public Expression Expression { get; }

        /// <inheritdoc />
        public IQueryProvider Provider => ProviderInternal;

        /// <inheritdoc />
        public IEnumerator<DnsAnswer> GetEnumerator() =>
            Provider.Execute<IEnumerable<DnsAnswer>>(Expression).GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        /// <summary>
        /// Executes the query asynchronously.
        /// </summary>
        /// <returns>Query result as a list.</returns>
        public Task<List<DnsAnswer>> ToListAsync() =>
            ProviderInternal.ExecuteAsync<List<DnsAnswer>>(Expression);
    }
}
