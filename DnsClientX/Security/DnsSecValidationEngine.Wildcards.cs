using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace DnsClientX {
    internal sealed partial class DnsSecValidationEngine {
        private static bool IsStrictAncestor(string name, string zone) =>
            IsNameWithinZone(name, zone)
            && !string.Equals(DnsWireNameCodec.Canonical(name), DnsWireNameCodec.Canonical(zone), StringComparison.Ordinal);

        private static bool IsWildcardExpansion(DnsSecSignature signature) {
            if (signature.Owner == ".") return false;
            int labels = signature.Owner.TrimEnd('.').Split('.').Length;
            // A literal wildcard is exempt only when this is the original signed owner.
            return labels > signature.Labels
                && !(signature.Owner.StartsWith("*.", StringComparison.Ordinal) && labels == signature.Labels + 1);
        }

        private async Task<DnsSecValidationResult> ValidateWildcardAsync(DnsResponse response,
            DnsSecSignature signature, CancellationToken cancellationToken) {
            var proofs = DnsSecWire.Records(response)
                .Where(record => record.Type == DnsRecordType.NSEC || record.Type == DnsRecordType.NSEC3).ToArray();
            if (proofs.Length == 0) return DnsSecValidationResult.Bogus(
                "The wildcard answer is missing authenticated denial of a closer match.");
            bool optOut = false;
            DnsSecValidationResult result = await ValidateDenialBySignerAsync(response, proofs, signature.Owner,
                cancellationToken,
                candidate => DnsSecProof.ProvesWildcardExpansion(candidate, signature.Owner, signature.Labels, out optOut),
                "The wildcard expansion and absence of a closer match were authenticated.",
                requiredSigner: signature.SignerName).ConfigureAwait(false);
            // RFC 5155 section 9.2: an Opt-Out span cannot establish a Secure answer.
            return result.Status == DnsSecValidationStatus.Secure && optOut
                ? DnsSecValidationResult.Insecure("The wildcard's authenticated next-closer proof uses NSEC3 Opt-Out.")
                : result;
        }
    }
}
