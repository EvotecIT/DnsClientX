using System;
using System.Collections.Generic;
using System.Linq;
using System.Management.Automation;
using System.Reflection;
using System.Net;
using System.Threading.Tasks;
using DnsClientX;

namespace DnsClientX.PowerShell {
    /// <summary>
    /// <para type="synopsis">Resolves a DNS query for a given name and type.</para>
    /// <para type="description">Resolves a DNS query for a given name and type. The query can be sent to a specific server or to a specific provider. If no server or provider is specified, the default provider System (UDP) is used.</para>
    /// <example>
    ///  <para>Resolve a DNS query for a given name and type</para>
    ///  <para></para>
    ///  <code>Resolve-Dns -Name "google.com"</code>
    /// </example>
    /// </summary>
    /// <seealso cref="DnsClientX.PowerShell.AsyncPSCmdlet" />
    [Alias("Resolve-DnsQuery")]
    [Cmdlet(VerbsDiagnostic.Resolve, "Dns", DefaultParameterSetName = "ServerName")]
    public sealed class CmdletResolveDnsQuery : AsyncPSCmdlet {
        /// <summary>
        /// <para type="description">The name of the DNS record to query for</para>
        /// </summary>
        [Parameter(Mandatory = true, Position = 0, ParameterSetName = "DnsProvider")]
        [Parameter(Mandatory = true, Position = 0, ParameterSetName = "ServerName")]
        public string[] Name { get; set; } = Array.Empty<string>();

        /// <summary>
        /// <para type="description">Pattern to expand into multiple DNS queries.</para>
        /// </summary>
        [Parameter(Mandatory = true, Position = 0, ParameterSetName = "PatternDnsProvider")]
        [Parameter(Mandatory = true, Position = 0, ParameterSetName = "PatternServerName")]
        public string? Pattern { get; set; }
        /// <summary>
        /// <para type="description">The type of the record to query for. If not specified, A record is queried.</para>
        /// </summary>
        [Parameter(Mandatory = false, Position = 1, ParameterSetName = "DnsProvider")]
        [Parameter(Mandatory = false, Position = 1, ParameterSetName = "ServerName")]
        [Parameter(Mandatory = false, Position = 1, ParameterSetName = "PatternDnsProvider")]
        [Parameter(Mandatory = false, Position = 1, ParameterSetName = "PatternServerName")]
        public DnsRecordType[] Type = [DnsRecordType.A];
        /// <summary>
        /// <para type="description">DnsProvider to use for the query. If not specified, the default provider System (UDP) is used.</para>
        /// </summary>
        [Parameter(Mandatory = false, ParameterSetName = "DnsProvider")]
        [Parameter(Mandatory = false, ParameterSetName = "PatternDnsProvider")]
        public DnsEndpoint? DnsProvider;

        /// <summary>
        /// <para type="description">Server to use for the query. If not specified, the default provider System (UDP) is used.</para>
        /// <para type="description">Once a server is specified, the query will be sent to that server.</para>
        /// </summary>
        [Alias("ServerName")]
        [Parameter(Mandatory = false, ParameterSetName = "ServerName")]
        [Parameter(Mandatory = false, ParameterSetName = "PatternServerName")]
        public List<string> Server = new List<string>();

        /// <summary>
        /// <para type="description">If specified, all servers listed in <see cref="Server"/> are queried sequentially and the responses are aggregated in server order.</para>
        /// <para type="description">When not specified, only the first server is queried for faster results.</para>
        /// </summary>
        [Parameter(Mandatory = false, ParameterSetName = "ServerName")]
        [Parameter(Mandatory = false, ParameterSetName = "PatternServerName")]
        public SwitchParameter AllServers;

        /// <summary>
        /// <para type="description">If specified, the cmdlet sequentially queries each server until a successful response is received.</para>
        /// <para type="description">This option stops on the first server that returns <c>DnsResponseCode.NoError</c>.</para>
        /// </summary>
        [Parameter(Mandatory = false, ParameterSetName = "ServerName")]
        [Parameter(Mandatory = false, ParameterSetName = "PatternServerName")]
        public SwitchParameter Fallback;

        /// <summary>
        /// <para type="description">If specified, the order of servers defined in <see cref="Server"/> is randomized before querying.</para>
        /// </summary>
        [Parameter(Mandatory = false, ParameterSetName = "ServerName")]
        [Parameter(Mandatory = false, ParameterSetName = "PatternServerName")]
        public SwitchParameter RandomServer;
        /// <summary>
        /// <para type="description">Provides the full response of the query. If not specified, only the minimal response is provided (just the answer).</para>
        /// <para type="description">If specified, the full response is provided (answer, authority, and additional sections).</para>
        /// </summary>
        [Parameter(Mandatory = false, ParameterSetName = "DnsProvider")]
        [Parameter(Mandatory = false, ParameterSetName = "ServerName")]
        [Parameter(Mandatory = false, ParameterSetName = "PatternDnsProvider")]
        [Parameter(Mandatory = false, ParameterSetName = "PatternServerName")]
        public SwitchParameter FullResponse;

        /// <summary>
        /// <para type="description">When set, attempts to parse answers into typed record objects.</para>
        /// </summary>
        [Parameter(Mandatory = false, ParameterSetName = "DnsProvider")]
        [Parameter(Mandatory = false, ParameterSetName = "ServerName")]
        [Parameter(Mandatory = false, ParameterSetName = "PatternDnsProvider")]
        [Parameter(Mandatory = false, ParameterSetName = "PatternServerName")]
        public SwitchParameter TypedRecords;

        /// <summary>
        /// Gets or sets a value indicating whether TXT records should be
        /// parsed into specialized types (DMARC, SPF, etc.) when <see cref="TypedRecords"/> is
        /// specified. When false, returns simple TXT records.
        /// </summary>
        [Parameter(Mandatory = false, ParameterSetName = "DnsProvider")]
        [Parameter(Mandatory = false, ParameterSetName = "ServerName")]
        [Parameter(Mandatory = false, ParameterSetName = "PatternDnsProvider")]
        [Parameter(Mandatory = false, ParameterSetName = "PatternServerName")]
        public SwitchParameter ParseTypedTxtRecords;

        /// <summary>
        /// <para type="description">Specifies the timeout for the DNS query, in milliseconds. If the DNS server does not respond within this time, the query will fail. Default is 2000 ms (2 seconds) as defined by <see cref="Configuration.DefaultTimeout"/>. Increase this value for slow networks or unreliable servers.</para>
        /// </summary>
        [Parameter(Mandatory = false, ParameterSetName = "ServerName")]
        [Parameter(Mandatory = false, ParameterSetName = "PatternServerName")]
        public int TimeOut = Configuration.DefaultTimeout;

        /// <summary>
        /// <para type="description">Number of retry attempts on transient errors.</para>
        /// </summary>
        [Parameter(Mandatory = false, ParameterSetName = "ServerName")]
        [Parameter(Mandatory = false, ParameterSetName = "DnsProvider")]
        [Parameter(Mandatory = false, ParameterSetName = "PatternServerName")]
        [Parameter(Mandatory = false, ParameterSetName = "PatternDnsProvider")]
        public int RetryCount = 3;

        /// <summary>
        /// <para type="description">Delay between retry attempts in milliseconds.</para>
        /// </summary>
        [Parameter(Mandatory = false, ParameterSetName = "ServerName")]
        [Parameter(Mandatory = false, ParameterSetName = "DnsProvider")]
        [Parameter(Mandatory = false, ParameterSetName = "PatternServerName")]
        [Parameter(Mandatory = false, ParameterSetName = "PatternDnsProvider")]
        public int RetryDelayMs = 200;

        /// <summary>
        /// <para type="description">Request DNSSEC data (sets the DO bit).</para>
        /// </summary>
        [Parameter(Mandatory = false, ParameterSetName = "DnsProvider")]
        [Parameter(Mandatory = false, ParameterSetName = "ServerName")]
        [Parameter(Mandatory = false, ParameterSetName = "PatternDnsProvider")]
        [Parameter(Mandatory = false, ParameterSetName = "PatternServerName")]
        public SwitchParameter RequestDnsSec;

        /// <summary>
        /// <para type="description">Validate DNSSEC signatures. Implies requesting DNSSEC data.</para>
        /// </summary>
        [Parameter(Mandatory = false, ParameterSetName = "DnsProvider")]
        [Parameter(Mandatory = false, ParameterSetName = "ServerName")]
        [Parameter(Mandatory = false, ParameterSetName = "PatternDnsProvider")]
        [Parameter(Mandatory = false, ParameterSetName = "PatternServerName")]
        public SwitchParameter ValidateDnsSec;

        private InternalLogger? _logger;

        private static readonly MethodInfo _isTransientResponse = typeof(ClientX).GetMethod("IsTransientResponse", BindingFlags.NonPublic | BindingFlags.Static)!;
        private static readonly MethodInfo _isTransientException = typeof(ClientX).GetMethod("IsTransient", BindingFlags.NonPublic | BindingFlags.Static)!;

        private static bool IsTransientResponse(DnsResponse response) => (bool)_isTransientResponse.Invoke(null, new object[] { response })!;
        private static bool IsTransient(Exception ex) => (bool)_isTransientException.Invoke(null, new object[] { ex })!;

        private async Task<DnsResponse[]> ExecuteWithRetry(Func<Task<DnsResponse[]>> query) {
            DnsResponse[] lastResults = Array.Empty<DnsResponse>();
            Exception? lastException = null;
            for (int attempt = 1; attempt <= RetryCount; attempt++) {
                try {
                    lastResults = await query();
                    if (!lastResults.Any(IsTransientResponse)) {
                        return lastResults;
                    }
                } catch (Exception ex) when (IsTransient(ex)) {
                    lastException = ex;
                }

                if (attempt < RetryCount) {
                    await Task.Delay(RetryDelayMs);
                }
            }

            if (lastException != null) {
                throw lastException;
            }

            return lastResults;
        }

        /// <inheritdoc />
        protected override Task BeginProcessingAsync() {

            // Initialize the logger to be able to see verbose, warning, debug, error, progress, and information messages.
            _logger = new InternalLogger(false);
            var internalLoggerPowerShell = new InternalLoggerPowerShell(_logger, this.WriteVerbose, this.WriteWarning, this.WriteDebug, this.WriteError, this.WriteProgress, this.WriteInformation);
            // var searchEvents = new SearchEvents(internalLogger);
            return Task.CompletedTask;
        }

        /// <inheritdoc />
        protected override async Task ProcessRecordAsync() {
            if (TimeOut <= 0) {
                throw new ArgumentOutOfRangeException(nameof(TimeOut), "TimeOut must be greater than zero.");
            }
            if (AllServers.IsPresent && Server.Count == 0) {
                throw new InvalidOperationException("AllServers requires at least one server.");
            }
            var namesToUse = Pattern is null ? Name : ClientX.ExpandPattern(Pattern).ToArray();
            string names = string.Join(", ", namesToUse);
            string types = string.Join(", ", Type);
            bool requestDnsSec = RequestDnsSec.IsPresent || ValidateDnsSec.IsPresent;
            bool validateDnsSec = ValidateDnsSec.IsPresent;
            if (Server.Count > 0) {
                var validServers = new List<string>();
                foreach (string serverEntry in Server) {
                    string trimmed = serverEntry.Trim();
                    if (IPAddress.TryParse(trimmed, out _)) {
                        validServers.Add(trimmed);
                    } else {
                    _logger?.WriteError("Malformed server address '{0}'.", serverEntry);
                    }
                }

                if (validServers.Count == 0) {
                    return;
                }

                List<string> serverOrder = validServers.Distinct().ToList();
                if (RandomServer.IsPresent) {
                    var random = new Random();
                    serverOrder = serverOrder.OrderBy(_ => random.Next()).ToList();
                }

                IEnumerable<DnsResponse> results;
                if (AllServers.IsPresent) {
                    if (Fallback.IsPresent && !RandomServer.IsPresent) {
                        var random = new Random();
                        serverOrder = serverOrder.OrderBy(_ => random.Next()).ToList();
                    }
                    var aggregatedResults = new List<DnsResponse>();
                    foreach (string serverName in serverOrder) {
                        _logger?.WriteVerbose("Querying DNS for {0} with type {1}, {2}", names, types, serverName);
                        var result = await ExecuteWithRetry(() => ClientX.QueryDns(namesToUse, Type, serverName, DnsRequestFormat.DnsOverUDP, timeOutMilliseconds: TimeOut, retryOnTransient: false, maxRetries: 1, retryDelayMs: RetryDelayMs, requestDnsSec: requestDnsSec, validateDnsSec: validateDnsSec, typedRecords: TypedRecords.IsPresent, parseTypedTxtRecords: ParseTypedTxtRecords.IsPresent));
                        aggregatedResults.AddRange(result);
                    }
                    results = aggregatedResults;
                } else if (Fallback.IsPresent) {
                    var aggregatedResults = new List<DnsResponse>();
                    foreach (string serverName in serverOrder) {
                        _logger?.WriteVerbose("Querying DNS for {0} with type {1}, {2}", names, types, serverName);
                        var result = await ExecuteWithRetry(() => ClientX.QueryDns(namesToUse, Type, serverName, DnsRequestFormat.DnsOverUDP, timeOutMilliseconds: TimeOut, retryOnTransient: false, maxRetries: 1, retryDelayMs: RetryDelayMs, requestDnsSec: requestDnsSec, validateDnsSec: validateDnsSec, typedRecords: TypedRecords.IsPresent, parseTypedTxtRecords: ParseTypedTxtRecords.IsPresent));
                        aggregatedResults.AddRange(result);
                        if (aggregatedResults.Any(r => r.Status == DnsResponseCode.NoError)) {
                            break;
                        }
                    }
                    results = aggregatedResults;
                } else {
                    string myServer = serverOrder.First();
                    _logger?.WriteVerbose("Querying DNS for {0} with type {1}, {2}", names, types, myServer);
                    var result = await ExecuteWithRetry(() => ClientX.QueryDns(namesToUse, Type, myServer, DnsRequestFormat.DnsOverUDP, timeOutMilliseconds: TimeOut, retryOnTransient: false, maxRetries: 1, retryDelayMs: RetryDelayMs, requestDnsSec: requestDnsSec, validateDnsSec: validateDnsSec, typedRecords: TypedRecords.IsPresent, parseTypedTxtRecords: ParseTypedTxtRecords.IsPresent));
                    results = result;
                }

                foreach (var record in results) {
                    string serverUsed = record.Questions.Length > 0 ? record.Questions[0].HostName ?? string.Empty : string.Empty;
                    if (record.Status == DnsResponseCode.NoError)
                    {
                        _logger?.WriteVerbose("Query successful for {0} with type {1}, {2} (retries {3})", names, types, serverUsed, record.RetryCount);
                    }
                    else
                    {
                        _logger?.WriteWarning("Query failed for {0} with type {1}, {2} and error: {3}", names, types, serverUsed, record.Error);
                    }

                    if (FullResponse.IsPresent) {
                        WriteObject(record);
                    } else if (TypedRecords.IsPresent && record.TypedAnswers != null) {
                        WriteObject(record.TypedAnswers, true);
                    } else {
                        WriteObject(record.AnswersMinimal);
                    }
                }
            } else {
                DnsResponse[] result;
                if (DnsProvider == null) {
                    _logger?.WriteVerbose("Querying DNS for {0} with type {1} and provider {2}", names, types, "Default");
                    result = await ExecuteWithRetry(() => ClientX.QueryDns(namesToUse, Type, timeOutMilliseconds: TimeOut, retryOnTransient: false, maxRetries: 1, retryDelayMs: RetryDelayMs, requestDnsSec: requestDnsSec, validateDnsSec: validateDnsSec, typedRecords: TypedRecords.IsPresent, parseTypedTxtRecords: ParseTypedTxtRecords.IsPresent));
                } else {
                    _logger?.WriteVerbose("Querying DNS for {0} with type {1} and provider {2}", names, types, DnsProvider.Value);
                    result = await ExecuteWithRetry(() => ClientX.QueryDns(namesToUse, Type, DnsProvider.Value, timeOutMilliseconds: TimeOut, retryOnTransient: false, maxRetries: 1, retryDelayMs: RetryDelayMs, requestDnsSec: requestDnsSec, validateDnsSec: validateDnsSec, typedRecords: TypedRecords.IsPresent, parseTypedTxtRecords: ParseTypedTxtRecords.IsPresent));
                }

                foreach (var record in result) {
                    if (record.Status == DnsResponseCode.NoError)
                    {
                        if (DnsProvider == null)
                        {
                            _logger?.WriteVerbose("Query successful for {0} with type {1}, {2} (retries {3})", names, types, "Default", record.RetryCount);
                        }
                        else
                        {
                            _logger?.WriteVerbose("Query successful for {0} with type {1}, {2} (retries {3})", names, types, DnsProvider.Value, record.RetryCount);
                        }
                    } else {
                        if (DnsProvider == null) {
                            _logger?.WriteWarning("Query failed for {0} with type {1}, {2} and error: {3}", names, types, "Default", record.Error);
                        } else {
                            _logger?.WriteWarning("Query failed for {0} with type {1}, {2} and error: {3}", names, types, DnsProvider.Value, record.Error);
                        }
                    }
                    if (FullResponse.IsPresent) {
                        WriteObject(record);
                    } else if (TypedRecords.IsPresent && record.TypedAnswers != null) {
                        WriteObject(record.TypedAnswers, true);
                    } else {
                        WriteObject(record.AnswersMinimal);
                    }
                }
            }

            return;
        }
    }
}
