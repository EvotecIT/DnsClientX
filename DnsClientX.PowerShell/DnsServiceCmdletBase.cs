using System;
using System.Management.Automation;
using System.Threading.Tasks;

namespace DnsClientX.PowerShell {
    /// <summary>Resolver binding shared by DNS service discovery commands.</summary>
    public abstract class DnsServiceCmdletBase : AsyncPSCmdlet {
        /// <summary><para type="description">Built-in resolver used for discovery. Defaults to the system resolver.</para></summary>
        [Parameter]
        public DnsEndpoint DnsProvider { get; set; } = DnsEndpoint.System;

        /// <summary><para type="description">One explicit resolver, such as udp@192.0.2.53:53, tcp@192.0.2.53:53, mdns@224.0.0.251:5353, or a DoH URL.</para></summary>
        [Parameter]
        [ValidateNotNullOrEmpty]
        public string? ResolverEndpoint { get; set; }

        /// <summary><para type="description">Saved resolver score snapshot whose recommended resolver is reused for discovery.</para></summary>
        [Parameter]
        [ValidateNotNullOrEmpty]
        public string? ResolverSelectionPath { get; set; }

        /// <summary><para type="description">Per-query timeout in milliseconds.</para></summary>
        [Parameter]
        [ValidateRange(1, int.MaxValue)]
        public int TimeOut { get; set; } = Configuration.DefaultTimeout;

        /// <summary><para type="description">IP-literal UDP/TCP resolver used to bootstrap endpoint hostnames.</para></summary>
        [Parameter]
        public string? BootstrapResolver { get; set; }

        /// <summary>Create the configured client through the shared resolver execution owner.</summary>
        protected async Task<ClientX> CreateDiscoveryClientAsync() {
            bool explicitEndpoint = !string.IsNullOrWhiteSpace(ResolverEndpoint);
            bool selection = !string.IsNullOrWhiteSpace(ResolverSelectionPath);
            if ((explicitEndpoint && selection) ||
                ((explicitEndpoint || selection) && MyInvocation.BoundParameters.ContainsKey(nameof(DnsProvider)))) {
                throw new PSArgumentException("Specify only one resolver source: DnsProvider, ResolverEndpoint, or ResolverSelectionPath.");
            }

            var source = new ResolverExecutionTargetSource {
                BuiltInEndpoints = explicitEndpoint || selection ? Array.Empty<DnsEndpoint>() : new[] { DnsProvider },
                ResolverEndpoints = explicitEndpoint ? new[] { ResolverEndpoint! } : Array.Empty<string>(),
                ResolverSelectionPath = selection
                    ? SessionState.Path.GetUnresolvedProviderPathFromPSPath(ResolverSelectionPath!) : null
            };
            var options = new ResolverExecutionClientOptions {
                TimeoutMs = TimeOut,
                BootstrapResolver = string.IsNullOrWhiteSpace(BootstrapResolver) ? null : EndpointParser.ParseBootstrap(BootstrapResolver!)
            };
            ResolverExecutionTarget target = await ResolverExecutionTargetResolver.ResolveSingleAsync(source, CancelToken).ConfigureAwait(false);
            return ResolverExecutionClientFactory.CreateClient(target, options);
        }
    }
}
