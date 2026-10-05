using System;
using System.Threading;
using System.Threading.Tasks;

namespace DnsClientX {
    // A run owns these clients; workers finish before disposal. Laziness avoids unused clients
    // when overrides execute the query or a run is canceled before reaching a target.
    internal sealed class ResolverQueryClientSession {
        private readonly Lazy<ClientX>[] _clients;

        internal ResolverQueryClientSession(ResolverExecutionTarget[] targets, ResolverQueryRunOptions options) {
            _clients = new Lazy<ClientX>[targets.Length];
            for (int index = 0; index < targets.Length; index++) {
                ResolverExecutionTarget target = targets[index];
                _clients[index] = new Lazy<ClientX>(() => ResolverQueryExecutor.CreateClient(target, options),
                    LazyThreadSafetyMode.ExecutionAndPublication);
            }
        }

        internal ClientX GetClient(int targetIndex) => _clients[targetIndex].Value;

        internal async Task DisposeAsync() {
            System.Runtime.ExceptionServices.ExceptionDispatchInfo? failure = null;
            foreach (Lazy<ClientX> client in _clients) {
                if (client.IsValueCreated) {
                    try {
                        await client.Value.DisposeAsync().ConfigureAwait(false);
                    } catch (Exception ex) {
                        failure ??= System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(ex);
                    }
                }
            }
            failure?.Throw();
        }
    }
}
