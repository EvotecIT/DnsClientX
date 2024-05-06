using System.Collections.Generic;
using System.Management.Automation;
using System.Threading.Tasks;

namespace DnsClientX.PowerShell {
    [Alias("Resolve-DnsQuery")]
    [Cmdlet(VerbsDiagnostic.Resolve, "Dns", DefaultParameterSetName = "ServerName")]
    public sealed class CmdletResolveDnsQuery : AsyncPSCmdlet {
        [Parameter(Mandatory = true, Position = 0, ParameterSetName = "DnsProvider")]
        [Parameter(Mandatory = true, Position = 0, ParameterSetName = "ServerName")]
        public string[] Name;

        [Parameter(Mandatory = false, Position = 1, ParameterSetName = "DnsProvider")]
        [Parameter(Mandatory = false, Position = 1, ParameterSetName = "ServerName")]
        public DnsRecordType[] Type = [DnsRecordType.A];

        [Parameter(Mandatory = false, ParameterSetName = "DnsProvider")]
        public DnsEndpoint? DnsProvider;

        [Alias("ServerName")]
        [Parameter(Mandatory = false, ParameterSetName = "ServerName")]
        public List<string> Server = new List<string>();

        [Parameter(Mandatory = false, ParameterSetName = "DnsProvider")]
        [Parameter(Mandatory = false, ParameterSetName = "ServerName")]
        public SwitchParameter FullResponse;

        private InternalLogger _logger;

        protected override Task BeginProcessingAsync() {

            // Initialize the logger to be able to see verbose, warning, debug, error, progress, and information messages.
            _logger = new InternalLogger(false);
            var internalLoggerPowerShell = new InternalLoggerPowerShell(_logger, this.WriteVerbose, this.WriteWarning, this.WriteDebug, this.WriteError, this.WriteProgress, this.WriteInformation);
            // var searchEvents = new SearchEvents(internalLogger);
            return Task.CompletedTask;
        }
        protected override Task ProcessRecordAsync() {
            string names = string.Join(", ", Name);
            string types = string.Join(", ", Type);
            if (Server.Count > 0) {
                string myServer = Server[0];
                _logger.WriteVerbose("Querying DNS for {0} with type {1}, {2}", names, types, myServer);
                var result = ClientX.QueryDns(Name, Type, myServer, DnsRequestFormat.DnsOverUDP);
                foreach (var record in result.Result) {
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
                    result = ClientX.QueryDns(Name, Type);
                } else {
                    _logger.WriteVerbose("Querying DNS for {0} with type {1} and provider {2}", names, types, DnsProvider.Value);
                    result = ClientX.QueryDns(Name, Type, DnsProvider.Value);
                }

                foreach (var record in result.Result) {
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
