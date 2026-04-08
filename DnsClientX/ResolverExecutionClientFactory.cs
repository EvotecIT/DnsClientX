using System;

namespace DnsClientX {
    /// <summary>
    /// Creates runnable <see cref="ClientX"/> instances from normalized resolver execution targets.
    /// </summary>
    public static class ResolverExecutionClientFactory {
        /// <summary>
        /// Creates a client for the supplied execution target.
        /// </summary>
        /// <param name="target">The normalized execution target.</param>
        /// <param name="portOverride">Optional port override applied after client creation.</param>
        /// <returns>A configured client instance for the supplied target.</returns>
        public static ClientX CreateClient(ResolverExecutionTarget target, int? portOverride = null) {
            if (target == null) {
                throw new ArgumentNullException(nameof(target));
            }

            ClientX client = target.ExplicitEndpoint != null
                ? ResolverEndpointClientFactory.CreateClient(target.ExplicitEndpoint)
                : target.BuiltInEndpoint.HasValue
                    ? new ClientX(target.BuiltInEndpoint.Value)
                    : throw new InvalidOperationException("Resolver execution target did not resolve to a runnable client.");

            if (portOverride.HasValue && portOverride.Value > 0) {
                client.EndpointConfiguration.Port = portOverride.Value;
            }

            return client;
        }
    }
}
