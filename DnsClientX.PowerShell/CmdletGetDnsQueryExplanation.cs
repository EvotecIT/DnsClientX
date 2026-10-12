using System.Management.Automation;

namespace DnsClientX.PowerShell {
    /// <summary>
    /// <para type="synopsis">Explains captured evidence from a completed DNS response.</para>
    /// <para type="description">Projects resolver, transport, cache, alias, authority and local DNSSEC evidence without additional DNS queries. UnavailableEvidence lists facts that the response cannot establish, including a complete delegation or DNSSEC trust-chain traversal.</para>
    /// </summary>
    /// <example><para>Explain a response returned by Resolve-Dns</para><code>Resolve-Dns example.com -FullResponse | Get-DnsQueryExplanation</code></example>
    [Cmdlet(VerbsCommon.Get, "DnsQueryExplanation")]
    [OutputType(typeof(DnsQueryExplanation))]
    public sealed class CmdletGetDnsQueryExplanation : PSCmdlet {
        /// <summary><para type="description">Completed DNS response, supplied directly or through the pipeline.</para></summary>
        [Parameter(Mandatory = true, Position = 0, ValueFromPipeline = true)]
        [Alias("Response")]
        [ValidateNotNull]
        public DnsResponse InputObject { get; set; } = null!;

        /// <summary><para type="description">Optional captured audit entries for the supplied response. Omit this parameter when individual attempt evidence was not captured.</para></summary>
        [Parameter]
        public AuditEntry[]? AuditTrail { get; set; }

        /// <inheritdoc />
        protected override void ProcessRecord() {
            WriteObject(DnsQueryExplanation.Create(InputObject, AuditTrail));
        }
    }
}
