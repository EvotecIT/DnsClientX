using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace DnsClientX {
    public partial class ClientX {
        private async Task<DnsResponse> ResolveWithSystemSearchDomains(
            string name,
            DnsRecordType type,
            bool requestDnsSec,
            bool validateDnsSec,
            bool returnAllTypes,
            int maxRetries,
            int retryDelayMs,
            bool typedRecords,
            bool parseTypedTxtRecords,
            CancellationToken cancellationToken) {
            SystemDnsConfiguration? systemConfiguration = EndpointConfiguration.SystemDnsConfiguration;
            IReadOnlyList<string> candidates = type != DnsRecordType.PTR
                && EndpointConfiguration.UseSystemSearchDomains
                && systemConfiguration != null
                ? systemConfiguration.BuildQueryCandidates(name)
                : new[] { name };

            DnsResponse? lastResponse = null;
            DnsResponse? bestNoDataResponse = null;
            foreach (string candidate in candidates) {
                cancellationToken.ThrowIfCancellationRequested();
                Configuration queryConfiguration = EndpointConfiguration.CreateQuerySnapshot(candidate);
                SystemDnsPolicyMatch? policyMatch = queryConfiguration.AppliedSystemDnsPolicy;
                if (policyMatch != null && !policyMatch.CanApply) {
                    throw new NotSupportedException(
                        $"Windows NRPT rule '{policyMatch.RuleId}' matched '{candidate}' but cannot be applied faithfully: {policyMatch.Diagnostic}");
                }
                bool effectiveRequestDnsSec = requestDnsSec || policyMatch?.DnsSecValidationRequired == true;
                bool effectiveValidateDnsSec = validateDnsSec || policyMatch?.DnsSecValidationRequired == true;
                try {
                    lastResponse = await ResolveInternal(
                        candidate,
                        type,
                        effectiveRequestDnsSec,
                        effectiveValidateDnsSec,
                        returnAllTypes,
                        maxRetries,
                        retryDelayMs,
                        typedRecords,
                        parseTypedTxtRecords,
                        cancellationToken,
                        queryConfigurationOverride: queryConfiguration).ConfigureAwait(false);
                } catch (DnsClientException ex) when (ex.Response?.Status == DnsResponseCode.NXDomain) {
                    lastResponse = ex.Response;
                }

                PreserveOriginalQuestionName(lastResponse, name);
                if (IsTerminalNoData(lastResponse, candidate, type)) {
                    bestNoDataResponse ??= lastResponse;
                    continue;
                }
                if (lastResponse.Status != DnsResponseCode.NXDomain) {
                    return lastResponse;
                }
            }

            return bestNoDataResponse ?? lastResponse
                ?? throw new DnsClientException("No DNS query candidate was produced.");
        }

        private static bool IsTerminalNoData(DnsResponse response, string candidate, DnsRecordType type) {
            return response.Status == DnsResponseCode.NoError
                && string.IsNullOrEmpty(response.Error)
                && !response.RequestedAnswerPresent
                && !HasRequestedAnswer(response, candidate.TrimEnd('.'), type);
        }

        private static void PreserveOriginalQuestionName(DnsResponse response, string originalName) {
            if (response.Questions == null) {
                return;
            }

            for (int index = 0; index < response.Questions.Length; index++) {
                DnsQuestion question = response.Questions[index];
                question.OriginalName = originalName;
                response.Questions[index] = question;
            }
        }

        private static bool IsHttpBasedTransport(DnsRequestFormat requestFormat) {
            return requestFormat == DnsRequestFormat.DnsOverHttps
                || requestFormat == DnsRequestFormat.DnsOverHttpsPOST
                || requestFormat == DnsRequestFormat.DnsOverHttpsWirePost
                || requestFormat == DnsRequestFormat.DnsOverHttpsJSON
                || requestFormat == DnsRequestFormat.DnsOverHttpsJSONPOST
                || requestFormat == DnsRequestFormat.DnsOverHttp2
                || requestFormat == DnsRequestFormat.DnsOverHttp3
                || requestFormat == DnsRequestFormat.DnsOverGrpc
                || requestFormat == DnsRequestFormat.ObliviousDnsOverHttps;
        }
    }
}
