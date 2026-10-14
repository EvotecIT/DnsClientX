using System.Security.Cryptography;
using System.Text;

namespace DnsClientX {
    /// <summary>Correlates configured resolver candidates independently of their readable labels.</summary>
    internal static class ResolverCandidateIdentity {
        internal static string ExecutionKey(ResolverExecutionTarget target) => target.ExplicitEndpoint != null
            ? ResolverEndpointClientFactory.GetExecutionKey(target.ExplicitEndpoint)
            : target.BuiltInEndpoint.HasValue ? "builtin:" + target.BuiltInEndpoint.Value : string.Empty;

        internal static string Create(ResolverExecutionTarget target) {
            string key = ExecutionKey(target);
            return FromExecutionKey(key);
        }

        internal static string FromExecutionKey(string key) {
            if (key.Length == 0) {
                return string.Empty;
            }
            using var hash = SHA256.Create();
            byte[] bytes = hash.ComputeHash(Encoding.UTF8.GetBytes(key));
            return "resolver-v1:" + System.BitConverter.ToString(bytes).Replace("-", string.Empty).ToLowerInvariant();
        }

        // Label fallback reads existing caller-produced observations and schema1/2 snapshots.
        internal static string GroupKey(string? candidateId, string label) => string.IsNullOrWhiteSpace(candidateId)
            ? "label:" + label : "id:" + candidateId;
    }
}
