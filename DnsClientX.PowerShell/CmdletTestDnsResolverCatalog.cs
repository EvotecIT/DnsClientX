using System;
using System.Management.Automation;
using System.Threading.Tasks;
using DnsClientX;

namespace DnsClientX.PowerShell {
    /// <summary>
    /// <para type="synopsis">Validates resolver endpoint catalog inputs without querying DNS.</para>
    /// <para type="description">Checks inline resolver endpoints, resolver endpoint files, and resolver endpoint URLs using the same parser used by probe and benchmark workflows.</para>
    /// <example>
    ///   <para>Validate inline resolver endpoint syntax</para>
    ///   <code>Test-DnsResolverCatalog -ResolverEndpoint udp@1.1.1.1:53,doh@https://dns.google/dns-query</code>
    /// </example>
    /// <example>
    ///   <para>Validate resolver endpoints from a file</para>
    ///   <code>Test-DnsResolverCatalog -ResolverEndpointFile .\resolvers.txt</code>
    /// </example>
    /// </summary>
    [Cmdlet(VerbsDiagnostic.Test, "DnsResolverCatalog")]
    [OutputType(typeof(ResolverEndpointValidationResult))]
    public sealed class CmdletTestDnsResolverCatalog : AsyncPSCmdlet {
        /// <summary>
        /// Inline resolver endpoint entries.
        /// </summary>
        [Parameter(Mandatory = false, ValueFromPipeline = true)]
        public string[] ResolverEndpoint { get; set; } = Array.Empty<string>();

        /// <summary>
        /// Files containing resolver endpoint entries.
        /// </summary>
        [Parameter(Mandatory = false)]
        public string[] ResolverEndpointFile { get; set; } = Array.Empty<string>();

        /// <summary>
        /// HTTP or HTTPS URLs containing resolver endpoint entries.
        /// </summary>
        [Parameter(Mandatory = false)]
        public string[] ResolverEndpointUrl { get; set; } = Array.Empty<string>();

        /// <inheritdoc />
        protected override async Task ProcessRecordAsync() {
            if ((ResolverEndpoint?.Length ?? 0) == 0 &&
                (ResolverEndpointFile?.Length ?? 0) == 0 &&
                (ResolverEndpointUrl?.Length ?? 0) == 0) {
                throw new PSArgumentException(
                    "At least one resolver endpoint, resolver endpoint file, or resolver endpoint URL must be specified.",
                    nameof(ResolverEndpoint));
            }

            ResolverEndpointValidationResult[] results = await EndpointParser.ValidateManyAsync(
                ResolverEndpoint,
                ResolverEndpointFile,
                ResolverEndpointUrl,
                CancelToken).ConfigureAwait(false);

            WriteObject(results, true);
        }
    }
}
