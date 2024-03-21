using System.Collections.Generic;
using System.Management.Automation;
using System.Threading.Tasks;
using DnsClientX;

namespace PowerDnsClient {
    [Cmdlet(VerbsDiagnostic.Resolve, "DnsQuery", DefaultParameterSetName = "ServerName")]
    public sealed class CmdletResolveDnsQuery : AsyncPSCmdlet {
        [Parameter(Mandatory = true, Position = 0, ParameterSetName = "DnsProvider")]
        [Parameter(Mandatory = true, Position = 0, ParameterSetName = "ServerName")]
        public string[] Name;

        [Parameter(Mandatory = false, Position = 1, ParameterSetName = "DnsProvider")]
        [Parameter(Mandatory = false, Position = 1, ParameterSetName = "ServerName")]
        public DnsRecordType[] Type = null;

        [Parameter(Mandatory = false, ParameterSetName = "DnsProvider")]
        public DnsEndpoint DnsProvider;

        [Alias("ServerName")]
        [Parameter(Mandatory = false, ParameterSetName = "ServerName")]
        public List<string> Server;

        public SwitchParameter FullResponse;


        private InternalLogger _logger;

        protected override Task BeginProcessingAsync() {

            // Initialize the logger to be able to see verbose, warning, debug, error, progress, and information messages.
            _logger = new InternalLogger(false);
            //var internalLoggerPowerShell = new InternalLoggerPowerShell(internalLogger, this.WriteVerbose, this.WriteWarning, this.WriteDebug, this.WriteError, this.WriteProgress, this.WriteInformation);
            // var searchEvents = new SearchEvents(internalLogger);
            return Task.CompletedTask;
        }
        protected override Task ProcessRecordAsync() {

            var result = ClientX.QueryDns(Name, Type, DnsProvider);
            foreach (var record in result.Result) {
                if (FullResponse.IsPresent) {
                    WriteObject(record);
                } else {
                    WriteObject(record.AnswersMinimal);
                }
            }

            return Task.CompletedTask;
        }
    }
}
