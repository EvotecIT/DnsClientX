using System.Linq;
using System.Linq.Expressions;

namespace DnsClientX.Linq {
    internal class ExpressionTreeModifier : ExpressionVisitor {
        private readonly IQueryable<DnsAnswer> _queryable;

        public ExpressionTreeModifier(IQueryable<DnsAnswer> queryable) => _queryable = queryable;

        protected override Expression VisitConstant(ConstantExpression node) {
            return node.Type == typeof(DnsQueryable)
                ? Expression.Constant(_queryable)
                : base.VisitConstant(node);
        }
    }
}
