using System;
using System.Linq;

namespace DnsClientX.Cli;

internal static partial class Program {
    private static bool ValidateProviderOptions(CliOptions options, out string? error) {
        error = null;
        bool privacy = options.PrivacyProviderPath != null || options.PrivacyResolver != null || options.PrivacyRelay != null;
        bool metadata = options.ShowCapabilities || options.StampInfo != null || options.ResolverValidate || options.ResolverSelectPath != null;
        if (privacy && (string.IsNullOrWhiteSpace(options.PrivacyProviderPath) || string.IsNullOrWhiteSpace(options.PrivacyResolver) ||
            options.EndpointInputs.Count != 0 || options.ProviderTransport.HasValue || options.HasCustomEndpointInputs || options.ResolverUsePath != null ||
            options.ProfilePath != null || options.CompareBaselinePath != null || options.Benchmark || options.Probe || options.DoUpdate || options.ZoneTransfer ||
            options.WirePost || options.BootstrapResolver != null || metadata)) {
            error = "Privacy queries require --privacy-provider and --privacy-resolver as the exclusive query source; an optional --privacy-relay supplies the relay.";
            return false;
        }
        if (options.ProviderTransport.HasValue && (options.HasCustomEndpointInputs || options.ResolverUsePath != null || options.ProfilePath != null ||
            options.CompareBaselinePath != null || options.ZoneTransfer || options.DoUpdate || options.WirePost || metadata)) {
            error = "--provider-transport applies to built-in provider queries, probes and benchmarks only.";
            return false;
        }
        if (options.ProviderTransport.HasValue) {
            foreach (string name in options.EndpointInputs.DefaultIfEmpty(options.Endpoint.ToString())) {
                if (!Enum.TryParse(name, true, out DnsEndpoint endpoint) ||
                    !DnsResolverEndpointFactory.GetSupportedRequestFormats(endpoint).Contains(options.ProviderTransport.Value)) {
                    error = "The selected provider does not publish " + options.ProviderTransport.Value + ": " + name;
                    return false;
                }
            }
        }
        return true;
    }
}
