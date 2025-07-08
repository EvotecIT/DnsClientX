using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;

namespace DnsClientX.Linq {
    internal class DnsQueryProvider : IQueryProvider {
        private readonly ClientX _client;
        private readonly IEnumerable<string> _names;
        private readonly DnsRecordType _type;

        public DnsQueryProvider(ClientX client, IEnumerable<string> names, DnsRecordType type) {
            _client = client;
            _names = names;
            _type = type;
        }

        public IQueryable CreateQuery(Expression expression) => new DnsQueryable(this, expression);

        public IQueryable<TElement> CreateQuery<TElement>(Expression expression) =>
            (IQueryable<TElement>)new DnsQueryable(this, expression);

        public object Execute(Expression expression) => Execute<IEnumerable<DnsAnswer>>(expression);

        public TResult Execute<TResult>(Expression expression) {
            return ExecuteAsync<TResult>(expression).GetAwaiter().GetResult();
        }

        public async Task<TResult> ExecuteAsync<TResult>(Expression expression) {
            var responses = await Task.WhenAll(_names.Select(n => _client.Resolve(n, _type)));
            var answers = responses.SelectMany(r => r.Answers ?? Array.Empty<DnsAnswer>()).AsQueryable();
            var visitor = new ExpressionTreeModifier(answers);
            var newExpression = visitor.Visit(expression);
            return answers.Provider.Execute<TResult>(newExpression);
        }
    }
}
