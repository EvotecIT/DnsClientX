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
        /// <param name="options">Optional shared client creation options.</param>
        /// <returns>A configured client instance for the supplied target.</returns>
        public static ClientX CreateClient(ResolverExecutionTarget target, ResolverExecutionClientOptions? options = null) {
            if (target == null) {
                throw new ArgumentNullException(nameof(target));
            }

            ClientX client = target.ExplicitEndpoint != null
                ? ResolverEndpointClientFactory.CreateClient(target.ExplicitEndpoint)
                : target.BuiltInEndpoint.HasValue
                    ? new ClientX(target.BuiltInEndpoint.Value)
                    : throw new InvalidOperationException("Resolver execution target did not resolve to a runnable client.");

            ApplyOptions(client, options);
            return client;
        }

        private static void ApplyOptions(ClientX client, ResolverExecutionClientOptions? options) {
            if (client == null) {
                throw new ArgumentNullException(nameof(client));
            }

            if (options == null) {
                return;
            }

            client.EnableAudit = options.EnableAudit;

            if (options.TimeoutMs.HasValue && options.TimeoutMs.Value > 0) {
                client.EndpointConfiguration.TimeOut = options.TimeoutMs.Value;
            }

            if (options.PortOverride.HasValue && options.PortOverride.Value > 0) {
                client.EndpointConfiguration.Port = options.PortOverride.Value;
            }

            if (options.ForceDohWirePost &&
                (client.EndpointConfiguration.RequestFormat == DnsRequestFormat.DnsOverHttps ||
                 client.EndpointConfiguration.RequestFormat == DnsRequestFormat.DnsOverHttpsPOST ||
                 client.EndpointConfiguration.RequestFormat == DnsRequestFormat.DnsOverHttpsJSON ||
                 client.EndpointConfiguration.RequestFormat == DnsRequestFormat.DnsOverHttpsJSONPOST)) {
                client.EndpointConfiguration.RequestFormat = DnsRequestFormat.DnsOverHttpsWirePost;
            }
        }
    }
}
