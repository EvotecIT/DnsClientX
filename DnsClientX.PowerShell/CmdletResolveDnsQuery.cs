using System;
using System.Collections.Generic;
using System.Linq;
using System.Management.Automation;
using System.Threading.Tasks;

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
        public string[] Name;
        /// <summary>
        /// <para type="description">The type of the record to query for. If not specified, A record is queried.</para>
        /// </summary>
        [Parameter(Mandatory = false, Position = 1, ParameterSetName = "DnsProvider")]
        [Parameter(Mandatory = false, Position = 1, ParameterSetName = "ServerName")]
        public DnsRecordType[] Type = [DnsRecordType.A];
        /// <summary>
        /// <para type="description">DnsProvider to use for the query. If not specified, the default provider System (UDP) is used.</para>
        /// </summary>
        [Parameter(Mandatory = false, ParameterSetName = "DnsProvider")]
        public DnsEndpoint? DnsProvider;

        /// <summary>
        /// <para type="description">Server to use for the query. If not specified, the default provider System (UDP) is used.</para>
        /// <para type="description">Once a server is specified, the query will be sent to that server.</para>
        /// </summary>
        [Alias("ServerName")]
        [Parameter(Mandatory = false, ParameterSetName = "ServerName")]
        public List<string> Server = new List<string>();

        /// <summary>
        /// <para type="description">If specified, all servers listed in <paramref name="Server"/> are queried sequentially and the responses are aggregated in server order.</para>
        /// <para type="description">When not specified, only the first server is queried for faster results.</para>
        /// </summary>
        [Parameter(Mandatory = false, ParameterSetName = "ServerName")]
        public SwitchParameter AllServers;

        /// <summary>
        /// <para type="description">If specified, the cmdlet sequentially queries each server until a successful response is received.</para>
        /// <para type="description">This option stops on the first server that returns <c>DnsResponseCode.NoError</c>.</para>
        /// </summary>
        [Parameter(Mandatory = false, ParameterSetName = "ServerName")]
        public SwitchParameter Fallback;

        /// <summary>
        /// <para type="description">If specified, the order of servers defined in <paramref name="Server"/> is randomized before querying.</para>
        /// </summary>
        [Parameter(Mandatory = false, ParameterSetName = "ServerName")]
        public SwitchParameter RandomServer;
        /// <summary>
        /// <para type="description">Provides the full response of the query. If not specified, only the minimal response is provided (just the answer).</para>
        /// <para type="description">If specified, the full response is provided (answer, authority, and additional sections).</para>
        /// </summary>
        [Parameter(Mandatory = false, ParameterSetName = "DnsProvider")]
        [Parameter(Mandatory = false, ParameterSetName = "ServerName")]
        public SwitchParameter FullResponse;

        /// <summary>
        /// <para type="description">Specifies the timeout for the DNS query, in milliseconds. If the DNS server does not respond within this time, the query will fail. Default is 1000 ms (1 second). Increase this value for slow networks or unreliable servers.</para>
        /// </summary>
        [Parameter(Mandatory = false, ParameterSetName = "ServerName")]
        public int TimeOut = 1000;

        private InternalLogger _logger;

        /// <summary>
        /// Begin record asynchronously.
        /// </summary>
        /// <returns></returns>
        protected override Task BeginProcessingAsync() {

            // Initialize the logger to be able to see verbose, warning, debug, error, progress, and information messages.
            _logger = new InternalLogger(false);
            var internalLoggerPowerShell = new InternalLoggerPowerShell(_logger, this.WriteVerbose, this.WriteWarning, this.WriteDebug, this.WriteError, this.WriteProgress, this.WriteInformation);
            // var searchEvents = new SearchEvents(internalLogger);
            return Task.CompletedTask;
        }

        /// <summary>
        /// Process the record asynchronously.
        /// </summary>
        /// <returns></returns>
        protected override Task ProcessRecordAsync() {
            string names = string.Join(", ", Name);
            string types = string.Join(", ", Type);
            if (Server.Count > 0) {
                IEnumerable<string> serverOrder = Server;
                if (RandomServer.IsPresent) {
                    var random = new Random();
                    serverOrder = serverOrder.OrderBy(_ => random.Next()).ToList();
                }

                IEnumerable<DnsResponse> results;
                if (AllServers.IsPresent) {
                    var aggregatedResults = new List<DnsResponse>();
                    foreach (string serverName in serverOrder) {
                        _logger.WriteVerbose("Querying DNS for {0} with type {1}, {2}", names, types, serverName);
                        var result = ClientX.QueryDns(Name, Type, serverName, DnsRequestFormat.DnsOverUDP, timeOutMilliseconds: TimeOut);
                        aggregatedResults.AddRange(result.Result);
                    }
                    results = aggregatedResults;
                } else if (Fallback.IsPresent) {
                    var aggregatedResults = new List<DnsResponse>();
                    foreach (string serverName in serverOrder) {
                        _logger.WriteVerbose("Querying DNS for {0} with type {1}, {2}", names, types, serverName);
                        var result = ClientX.QueryDns(Name, Type, serverName, DnsRequestFormat.DnsOverUDP, timeOutMilliseconds: TimeOut);
                        aggregatedResults.AddRange(result.Result);
                        if (aggregatedResults.Any(r => r.Status == DnsResponseCode.NoError)) {
                            break;
                        }
                    }
                    results = aggregatedResults;
                } else {
                    string myServer = serverOrder.First();
                    _logger.WriteVerbose("Querying DNS for {0} with type {1}, {2}", names, types, myServer);
                    var result = ClientX.QueryDns(Name, Type, myServer, DnsRequestFormat.DnsOverUDP, timeOutMilliseconds: TimeOut);
                    results = result.Result;
                }

                foreach (var record in results) {
                    string serverUsed = record.Questions.FirstOrDefault().HostName;
                    if (record.Status == DnsResponseCode.NoError) {
                        _logger.WriteVerbose("Query successful for {0} with type {1}, {2}", names, types, serverUsed);
                    } else {
                        _logger.WriteWarning("Query failed for {0} with type {1}, {2} and error: {3}", names, types, serverUsed, record.Error);
                    }

                    if (FullResponse.IsPresent) {
                        WriteObject(record);
                    } else {
                        WriteObject(record.AnswersMinimal);
                    }
                }
            } else {
                Task<DnsResponse[]> result;
                if (DnsProvider == null) {
                    _logger.WriteVerbose("Querying DNS for {0} with type {1} and provider {2}", names, types, "Default");
                    result = ClientX.QueryDns(Name, Type, timeOutMilliseconds: TimeOut);
                } else {
                    _logger.WriteVerbose("Querying DNS for {0} with type {1} and provider {2}", names, types, DnsProvider.Value);
                    result = ClientX.QueryDns(Name, Type, DnsProvider.Value, timeOutMilliseconds: TimeOut);
                }

                foreach (var record in result.Result) {
                    if (record.Status == DnsResponseCode.NoError) {
                        if (DnsProvider == null) {
                            _logger.WriteVerbose("Query successful for {0} with type {1}, {2}", names, types, "Default");
                        } else {
                            _logger.WriteVerbose("Query successful for {0} with type {1}, {2}", names, types, DnsProvider.Value);
                        }
                    } else {
                        if (DnsProvider == null) {
                            _logger.WriteWarning("Query failed for {0} with type {1}, {2} and error: {3}", names, types, "Default", record.Error);
                        } else {
                            _logger.WriteWarning("Query failed for {0} with type {1}, {2} and error: {3}", names, types, DnsProvider.Value, record.Error);
                        }
                    }
                    if (FullResponse.IsPresent) {
                        WriteObject(record);
                    } else {
                        WriteObject(record.AnswersMinimal);
                    }
                }
            }

            return Task.CompletedTask;
        }
    }
}
