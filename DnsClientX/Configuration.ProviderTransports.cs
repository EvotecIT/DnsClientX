using System;
using System.Linq;

namespace DnsClientX;

public partial class Configuration {
    /// <summary>Creates a built-in provider configuration using a published transport instead of its default.</summary>
    /// <param name="endpoint">Provider profile; filtering policy is preserved.</param>
    /// <param name="requestFormat">A published format returned by DnsResolverEndpointFactory.GetSupportedRequestFormats.</param>
    /// <param name="selectionStrategy">Address selection for redundant provider addresses.</param>
    public Configuration(DnsEndpoint endpoint, DnsRequestFormat requestFormat, DnsSelectionStrategy selectionStrategy = DnsSelectionStrategy.First)
        : this(endpoint, selectionStrategy) {
        var endpoints = DnsResolverEndpointFactory.From(endpoint, requestFormat);
        if (endpoints.Length == 0) { throw new InvalidOperationException("The provider has no configured resolver addresses."); }
        hostnames = endpoints.Select(value => value.Host!).ToList();
        RequestFormat = requestFormat;
        TlsServerName = endpoints[0].TlsServerName;
        Port = endpoints[0].Port;
        baseUriFormat = endpoints[0].DohUrl == null ? null : "https://{0}" + endpoints[0].DohUrl!.AbsolutePath;
        SelectHostNameStrategy();
        BaseUri = baseUriFormat == null ? null : new Uri(string.Format(baseUriFormat, Hostname));
    }
}
