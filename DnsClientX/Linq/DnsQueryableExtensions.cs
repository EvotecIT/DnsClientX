using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DnsClientX.Linq {
    /// <summary>
    /// Helper extension methods for <see cref="DnsQueryable"/>.
    /// </summary>
    public static class DnsQueryableExtensions {
        /// <summary>
        /// Creates a DNS LINQ query for the specified domains and record type.
        /// </summary>
        /// <param name="client">DNS client.</param>
        /// <param name="names">Domain names.</param>
        /// <param name="type">Record type.</param>
        public static DnsQueryable AsQueryable(this ClientX client, IEnumerable<string> names, DnsRecordType type) =>
            new(client, names, type);

        /// <summary>
        /// Executes the query asynchronously and returns a list of answers.
        /// </summary>
        /// <param name="source">Queryable source.</param>
        /// <returns>List of answers.</returns>
        public static Task<List<DnsAnswer>> ToListAsync(this IQueryable<DnsAnswer> source) =>
            source is DnsQueryable dns ? dns.ToListAsync() : Task.FromResult(source.ToList());
    }
}
