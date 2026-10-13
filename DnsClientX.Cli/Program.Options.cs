using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace DnsClientX.Cli {
    internal static partial class Program {
        private enum QueryOutputFormat {
            Pretty,
            Json,
            Raw
        }

        private sealed class CliOptions {
            public HashSet<string> ProvidedSwitches { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            public string? ProfilePath { get; set; }
            public string? ProfileSavePath { get; set; }
            public string? CompareBaselinePath { get; set; }
            public string? CompareCurrentPath { get; set; }
            public string? ReferenceResponsePath { get; set; }
            public string? Domain { get; set; }
            public DnsRecordType RecordType { get; set; } = DnsRecordType.A;
            public bool RecordTypeSpecified { get; set; }
            public DnsEndpoint Endpoint { get; set; } = DnsEndpoint.System;
            public QueryOutputFormat OutputFormat { get; set; } = QueryOutputFormat.Pretty;
            public bool ShortOutput { get; set; }
            public bool TxtConcatOutput { get; set; }
            public bool ShowQuestionSection { get; set; }
            public bool ShowAnswerSection { get; set; }
            public bool ShowAuthoritySection { get; set; }
            public bool ShowAdditionalSection { get; set; }
            public bool ReverseLookup { get; set; }
            public bool ZoneTransfer { get; set; }
            public bool TransferSummary { get; set; }
            public bool Benchmark { get; set; }
            public int BenchmarkAttempts { get; set; } = 3;
            public int BenchmarkTimeoutMs { get; set; } = 2000;
            public int BenchmarkConcurrency { get; set; } = 4;
            public ResolverQueryConnectionMode BenchmarkConnectionMode { get; set; }
            public int? BenchmarkMinSuccessPercent { get; set; }
            public int? BenchmarkMinSuccessfulCandidates { get; set; }
            public bool BenchmarkSummaryOnly { get; set; }
            public bool BenchmarkSummaryLine { get; set; }
            public List<DnsEndpoint> BenchmarkEndpoints { get; } = new List<DnsEndpoint>();
            public List<string> BenchmarkDomains { get; } = new List<string>();
            public List<DnsRecordType> BenchmarkRecordTypes { get; } = new List<DnsRecordType>();
            public List<string> EndpointInputs { get; } = new List<string>();
            public List<string> DomainInputs { get; } = new List<string>();
            public List<string> RecordTypeInputs { get; } = new List<string>();
            public List<string> ProbeEndpointFiles { get; } = new List<string>();
            public List<string> ProbeEndpointUrls { get; } = new List<string>();
            public bool RequestDnsSec { get; set; }
            public bool ValidateDnsSec { get; set; }
            public string? DnsSecVerifierPath { get; set; }
            public IDnsSecSignatureVerifier? DnsSecSignatureVerifier { get; set; }
            public bool WirePost { get; set; }
            public DnsResolverEndpoint? BootstrapResolver { get; set; }
            public bool RequestNsid { get; set; }
            public bool Probe { get; set; }
            public List<string> ProbeEndpoints { get; } = new List<string>();
            public string? ProbeSavePath { get; set; }
            public string? ResolverSelectPath { get; set; }
            public string? ResolverUsePath { get; set; }
            public bool ProbeSummaryOnly { get; set; }
            public bool ProbeSummaryLine { get; set; }
            public bool ProbeRequireConsensus { get; set; }
            public int? ProbeMinConsensusPercent { get; set; }
            public int? ProbeMinSuccessCount { get; set; }
            public int? ProbeMinSuccessPercent { get; set; }
            public string? BenchmarkSavePath { get; set; }
            public bool Explain { get; set; }
            public bool Trace { get; set; }
            public bool ShowCapabilities { get; set; }
            public string? StampInfo { get; set; }
            public bool ResolverValidate { get; set; }
            public bool DoUpdate { get; set; }
            public string? Zone { get; set; }
            public string? UpdateName { get; set; }
            public string? UpdateData { get; set; }
            public int Ttl { get; set; } = 300;

            public bool HasExplicitSectionSelection =>
                ShowQuestionSection ||
                ShowAnswerSection ||
                ShowAuthoritySection ||
                ShowAdditionalSection;

            public bool HasCustomEndpointInputs =>
                ProbeEndpoints.Count > 0 ||
                ProbeEndpointFiles.Count > 0 ||
                ProbeEndpointUrls.Count > 0;
        }

    }
}
