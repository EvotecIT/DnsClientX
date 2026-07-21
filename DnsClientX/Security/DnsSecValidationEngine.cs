using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace DnsClientX {
    internal sealed class DnsSecValidationEngine {
        private readonly Func<string, DnsRecordType, CancellationToken, Task<DnsResponse>> _lookup;
        private readonly DateTimeOffset _now;
        private readonly Dictionary<string, Task<ZoneKeysResult>> _zoneCache = new(StringComparer.Ordinal);
        private readonly Rfc5011Store? _trustAnchorStore;
        private readonly IDnsSecSignatureVerifier? _signatureVerifier;

        internal DnsSecValidationEngine(Func<string, DnsRecordType, CancellationToken, Task<DnsResponse>> lookup,
            DateTimeOffset? now = null, string? trustAnchorStorePath = null,
            IDnsSecSignatureVerifier? signatureVerifier = null) {
            _lookup = lookup ?? throw new ArgumentNullException(nameof(lookup));
            _now = now ?? DateTimeOffset.UtcNow;
            _signatureVerifier = signatureVerifier;
            if (!string.IsNullOrWhiteSpace(trustAnchorStorePath)) {
                _trustAnchorStore = new Rfc5011Store(trustAnchorStorePath!);
            }
        }

        internal async Task<DnsSecValidationResult> ValidateAsync(DnsResponse response, string name,
            DnsRecordType type, CancellationToken cancellationToken) {
            if (response.WireMessage == null || response.WireMessage.Length == 0) {
                return DnsSecValidationResult.Indeterminate("Local DNSSEC validation requires a DNS wire-format response.");
            }
            if (response.Status != DnsResponseCode.NoError && response.Status != DnsResponseCode.NXDomain) {
                return DnsSecValidationResult.Indeterminate($"DNSSEC validation is not defined for the {response.Status} response status.");
            }

            DnsWireResourceRecord[] answerRecords = response.WireAnswers ?? Array.Empty<DnsWireResourceRecord>();
            var rrsets = answerRecords
                .Where(record => record.Type != DnsRecordType.RRSIG && record.Type != DnsRecordType.OPT)
                .GroupBy(record => new RrsetKey(DnsWireNameCodec.Canonical(record.Name), record.Type, record.Class))
                .ToArray();

            if (rrsets.Length > 0) {
                return await ValidatePositiveAsync(response, answerRecords, rrsets, name, type,
                    requireTerminal: true, cancellationToken).ConfigureAwait(false);
            }

            return await ValidateNegativeAsync(response, name, type, cancellationToken).ConfigureAwait(false);
        }

        internal async Task<DnsSecValidationResult> ValidateAliasAsync(DnsResponse response, string name,
            DnsRecordType type, CancellationToken cancellationToken) {
            if (response.WireMessage == null || response.WireMessage.Length == 0) {
                return DnsSecValidationResult.Indeterminate("Local DNSSEC validation requires a DNS wire-format response.");
            }
            DnsWireResourceRecord[] answers = response.WireAnswers ?? Array.Empty<DnsWireResourceRecord>();
            var rrsets = answers
                .Where(record => record.Type != DnsRecordType.RRSIG && record.Type != DnsRecordType.OPT)
                .GroupBy(record => new RrsetKey(DnsWireNameCodec.Canonical(record.Name), record.Type, record.Class))
                .ToArray();
            if (rrsets.Length == 0) {
                return DnsSecValidationResult.Indeterminate("The iterative alias response contained no answer RRset.");
            }
            return await ValidatePositiveAsync(response, answers, rrsets, name, type,
                requireTerminal: false, cancellationToken).ConfigureAwait(false);
        }

        private async Task<DnsSecValidationResult> ValidatePositiveAsync(
            DnsResponse response,
            DnsWireResourceRecord[] answerRecords,
            IGrouping<RrsetKey, DnsWireResourceRecord>[] rrsets,
            string name,
            DnsRecordType type,
            bool requireTerminal,
            CancellationToken cancellationToken) {
            bool insecure = false;
            foreach (IGrouping<RrsetKey, DnsWireResourceRecord> rrset in rrsets) {
                if (rrset.Key.Type == DnsRecordType.CNAME
                    && ReadSignatures(response, rrset.Key.Name, rrset.Key.Type, rrset.Key.Class).Count == 0
                    && rrset.All(record => IsSynthesizedDnameCname(answerRecords, record))) {
                    continue;
                }
                DnsSecValidationResult result = await ValidateAnswerRrsetAsync(response, rrset.Key, cancellationToken).ConfigureAwait(false);
                if (result.Status == DnsSecValidationStatus.Bogus || result.Status == DnsSecValidationStatus.Indeterminate) return result;
                insecure |= result.Status == DnsSecValidationStatus.Insecure;
            }

            if (!TryFollowAnswerChain(answerRecords, name, type, out string finalName, out bool terminal, out string? chainError)) {
                return DnsSecValidationResult.Indeterminate(chainError ?? "The answer did not contain a usable canonical-name chain.");
            }
            if (!terminal && !requireTerminal) {
                if (string.Equals(DnsWireNameCodec.Canonical(name), finalName, StringComparison.Ordinal)) {
                    return DnsSecValidationResult.Indeterminate("The answer did not redirect the iterative query to another name.");
                }
                return insecure
                    ? DnsSecValidationResult.Insecure("The authenticated alias crosses an unsigned delegation.")
                    : DnsSecValidationResult.Secure("The iterative alias segment was authenticated.");
            }
            if (!terminal) {
                DnsSecValidationResult denial = await ValidateNegativeAsync(response, finalName, type, cancellationToken).ConfigureAwait(false);
                if (denial.Status == DnsSecValidationStatus.Bogus || denial.Status == DnsSecValidationStatus.Indeterminate) return denial;
                insecure |= denial.Status == DnsSecValidationStatus.Insecure;
            }

            return insecure
                ? DnsSecValidationResult.Insecure("A secure delegation chain proved that at least one answer zone is unsigned.")
                : DnsSecValidationResult.Secure(terminal
                    ? "The complete answer chain and delegation chain were validated to a root trust anchor."
                    : "The canonical-name chain and authenticated denial for its final target were validated.");
        }

        private async Task<DnsSecValidationResult> ValidateAnswerRrsetAsync(DnsResponse response, RrsetKey rrset,
            CancellationToken cancellationToken) {
            List<DnsSecSignature> signatures = ReadSignatures(response, rrset.Name, rrset.Type, rrset.Class);
            if (signatures.Count == 0) {
                return await FindUnsignedDelegationAsync(rrset.Name, cancellationToken).ConfigureAwait(false);
            }

            DnsSecValidationResult? unsupported = null;
            foreach (IGrouping<string, DnsSecSignature> signer in signatures.GroupBy(item => item.SignerName, StringComparer.Ordinal)) {
                if (!IsNameWithinZone(rrset.Name, signer.Key)) {
                    return DnsSecValidationResult.Bogus(
                        $"RRSIG signer {signer.Key} is not an ancestor of the {rrset.Name} RRset owner.");
                }
                ZoneKeysResult keys = await GetZoneKeysAsync(signer.Key, cancellationToken).ConfigureAwait(false);
                if (keys.Status == DnsSecValidationStatus.Insecure) return DnsSecValidationResult.Insecure(keys.Message);
                if (keys.Status != DnsSecValidationStatus.Secure) {
                    if (keys.Status == DnsSecValidationStatus.Bogus) return DnsSecValidationResult.Bogus(keys.Message);
                    unsupported = DnsSecValidationResult.Indeterminate(keys.Message);
                    continue;
                }
                DnsSecValidationResult verification = VerifyRrset(response, rrset, signer, keys.Keys);
                if (verification.Status == DnsSecValidationStatus.Secure) return verification;
                if (verification.Status == DnsSecValidationStatus.Bogus) return verification;
                unsupported = verification;
            }
            return unsupported ?? DnsSecValidationResult.Bogus($"No signature validated the {rrset.Name} {rrset.Type} RRset.");
        }

        private async Task<DnsSecValidationResult> ValidateNegativeAsync(DnsResponse response, string name,
            DnsRecordType type, CancellationToken cancellationToken) {
            DnsWireResourceRecord[] proofRecords = DnsSecWire.Records(response)
                .Where(record => record.Type == DnsRecordType.NSEC || record.Type == DnsRecordType.NSEC3)
                .ToArray();
            if (proofRecords.Length == 0) return await FindUnsignedDelegationAsync(name, cancellationToken).ConfigureAwait(false);

            return await ValidateDenialBySignerAsync(response, proofRecords, name, cancellationToken,
                candidate => response.Status == DnsResponseCode.NXDomain
                    ? DnsSecProof.ProvesNameError(candidate, name)
                    : DnsSecProof.ProvesNoData(candidate, name, type),
                "The authenticated denial records prove the requested name or type does not exist.").ConfigureAwait(false);
        }

        private Task<ZoneKeysResult> GetZoneKeysAsync(string zone, CancellationToken cancellationToken) {
            zone = DnsWireNameCodec.Canonical(zone);
            lock (_zoneCache) {
                if (!_zoneCache.TryGetValue(zone, out Task<ZoneKeysResult>? task)) {
                    task = LoadZoneKeysAsync(zone, cancellationToken);
                    _zoneCache[zone] = task;
                }
                return task;
            }
        }

        private async Task<ZoneKeysResult> LoadZoneKeysAsync(string zone, CancellationToken cancellationToken) {
            DnsResponse keyResponse = await _lookup(zone, DnsRecordType.DNSKEY, cancellationToken).ConfigureAwait(false);
            DnsWireResourceRecord[] records = DnsSecWire.Records(keyResponse);
            DnsWireResourceRecord[] keyRecords = records.Where(record => record.Type == DnsRecordType.DNSKEY &&
                string.Equals(DnsWireNameCodec.Canonical(record.Name), zone, StringComparison.Ordinal)).ToArray();
            var keys = new List<DnsSecKey>();
            foreach (DnsWireResourceRecord record in keyRecords) {
                if (DnsSecWire.TryReadKey(keyResponse.WireMessage, record, out DnsSecKey key) &&
                    (key.Flags & 0x0100) != 0 && (key.Flags & 0x0080) == 0) keys.Add(key);
            }
            if (keys.Count == 0) return ZoneKeysResult.Indeterminate($"No usable DNSKEY RRset was returned for {zone}");

            RrsetKey keyRrset = new(zone, DnsRecordType.DNSKEY, keyRecords[0].Class);
            List<DnsSecSignature> keySignatures = ReadSignatures(keyResponse, zone, DnsRecordType.DNSKEY, keyRecords[0].Class);
            if (zone == ".") {
                DnsSecKey[] configuredAnchors;
                try {
                    configuredAnchors = _trustAnchorStore?.ActiveKeys(_now)
                        ?? RootTrustAnchors.DnsKeys.Where(key => DnsSecTrustAnchors.Current.Any(anchor =>
                            anchor.IsValidAt(_now) && anchor.KeyTag == key.KeyTag
                            && (byte)anchor.Algorithm == key.Algorithm)).ToArray();
                } catch (Exception ex) when (IsTrustAnchorStoreFailure(ex)) {
                    return ZoneKeysResult.Indeterminate($"RFC 5011 trust-anchor state could not be loaded: {ex.Message}");
                }

                var validatingAnchors = new List<DnsSecKey>();
                foreach (DnsSecKey anchor in configuredAnchors) {
                    DnsSecValidationResult anchorSignature = VerifyRrset(
                        keyResponse, keyRrset, keySignatures, new[] { anchor });
                    if (anchorSignature.Status == DnsSecValidationStatus.Secure) validatingAnchors.Add(anchor);
                }
                if (validatingAnchors.Count == 0) {
                    return configuredAnchors.Any(key => DnsSecCrypto.IsSupportedAlgorithm(key.Algorithm, _signatureVerifier))
                        ? ZoneKeysResult.Bogus("The root DNSKEY RRset was not signed by a configured trust-anchor key.")
                        : ZoneKeysResult.Indeterminate("No supported configured root trust-anchor key was available.");
                }

                if (_trustAnchorStore != null) {
                    var verifiedRevocations = new List<string>();
                    foreach (DnsSecKey key in keys.Where(key => (key.Flags & 0x0080) != 0)) {
                        if (VerifyRrset(keyResponse, keyRrset, keySignatures, new[] { key }).Status
                            == DnsSecValidationStatus.Secure) {
                            verifiedRevocations.Add(Rfc5011Store.KeyIdentity(key));
                        }
                    }
                    uint originalTtl = keyRecords.Min(record => record.RawTtl);
                    DateTimeOffset signatureExpiration = keySignatures.Count == 0
                        ? _now
                        : DateTimeOffset.FromUnixTimeSeconds(keySignatures.Min(signature => (long)signature.Expiration));
                    try {
                        _trustAnchorStore.ObserveAuthenticated(
                            keys,
                            validatingAnchors.Select(Rfc5011Store.KeyIdentity).ToArray(),
                            verifiedRevocations,
                            originalTtl,
                            signatureExpiration,
                            _now);
                    } catch (Exception ex) when (IsTrustAnchorStoreFailure(ex)) {
                        return ZoneKeysResult.Indeterminate($"RFC 5011 trust-anchor state could not be updated: {ex.Message}");
                    }
                }
                return ZoneKeysResult.Secure(keys, _trustAnchorStore == null
                    ? "The root DNSKEY RRset was signed by a bundled IANA trust-anchor key."
                    : "The root DNSKEY RRset was signed by an active RFC 5011 trust-anchor key.");
            }

            DnsResponse dsResponse = await _lookup(zone, DnsRecordType.DS, cancellationToken).ConfigureAwait(false);
            DnsWireResourceRecord[] dsRecords = (dsResponse.WireAnswers ?? Array.Empty<DnsWireResourceRecord>())
                .Where(record => record.Type == DnsRecordType.DS && string.Equals(DnsWireNameCodec.Canonical(record.Name), zone, StringComparison.Ordinal))
                .ToArray();
            if (dsRecords.Length == 0) return await ValidateUnsignedDelegationAsync(zone, dsResponse, cancellationToken).ConfigureAwait(false);

            List<DnsSecSignature> dsSignatures = ReadSignatures(dsResponse, zone, DnsRecordType.DS, dsRecords[0].Class);
            if (dsSignatures.Count == 0) return ZoneKeysResult.Bogus($"The DS RRset for {zone} is missing its parent signature.");
            ZoneKeysResult parent = await GetZoneKeysAsync(dsSignatures[0].SignerName, cancellationToken).ConfigureAwait(false);
            if (parent.Status != DnsSecValidationStatus.Secure) return parent;
            DnsSecValidationResult dsSignature = VerifyRrset(dsResponse,
                new RrsetKey(zone, DnsRecordType.DS, dsRecords[0].Class), dsSignatures, parent.Keys);
            if (dsSignature.Status != DnsSecValidationStatus.Secure) return new ZoneKeysResult(dsSignature.Status, Array.Empty<DnsSecKey>(), dsSignature.Message);

            bool supportedCombination = false;
            var dsMatchedKeys = new List<DnsSecKey>();
            foreach (DnsWireResourceRecord dsRecord in dsRecords) {
                if (!TryReadDs(dsResponse.WireMessage, dsRecord, out ushort keyTag, out byte algorithm, out byte digestType, out byte[] expected)) continue;
                if ((digestType == 1 || digestType == 2 || digestType == 4)
                    && DnsSecCrypto.IsSupportedAlgorithm(algorithm, _signatureVerifier)) {
                    supportedCombination = true;
                }
                foreach (DnsSecKey key in keys.Where(item => item.KeyTag == keyTag && item.Algorithm == algorithm)) {
                    if (!DnsSecCrypto.TryComputeDsDigest(zone, key, digestType, out byte[] actual)) continue;
                    if (actual.SequenceEqual(expected) && !dsMatchedKeys.Any(candidate =>
                            Rfc5011Store.KeyIdentity(candidate) == Rfc5011Store.KeyIdentity(key))) {
                        dsMatchedKeys.Add(key);
                    }
                }
            }
            if (dsMatchedKeys.Count > 0) {
                DnsSecValidationResult keySignature = VerifyRrset(
                    keyResponse, keyRrset, keySignatures, dsMatchedKeys);
                if (keySignature.Status == DnsSecValidationStatus.Secure) {
                    return ZoneKeysResult.Secure(keys,
                        $"The {zone} DNSKEY RRset is signed by a key matching its authenticated DS record.");
                }
                return new ZoneKeysResult(keySignature.Status, Array.Empty<DnsSecKey>(), keySignature.Message);
            }
            return supportedCombination
                ? ZoneKeysResult.Bogus($"No DNSKEY for {zone} matched the authenticated DS digest.")
                : ZoneKeysResult.Indeterminate($"The authenticated DS RRset for {zone} uses no supported digest/key combination.");
        }

        private static bool IsTrustAnchorStoreFailure(Exception exception) =>
            exception is IOException
            || exception is UnauthorizedAccessException
            || exception is InvalidOperationException
            || exception is FormatException
            || exception is System.Security.SecurityException;

        private async Task<ZoneKeysResult> ValidateUnsignedDelegationAsync(string zone, DnsResponse response,
            CancellationToken cancellationToken) {
            DnsWireResourceRecord[] proofRecords = DnsSecWire.Records(response)
                .Where(record => record.Type == DnsRecordType.NSEC || record.Type == DnsRecordType.NSEC3)
                .ToArray();
            if (proofRecords.Length == 0) return ZoneKeysResult.Indeterminate($"No authenticated DS denial proof was returned for {zone}");
            DnsSecValidationResult result = await ValidateDenialBySignerAsync(response, proofRecords, zone,
                cancellationToken, candidate => DnsSecProof.ProvesUnsignedDelegation(candidate, zone),
                $"The secure parent proves that {zone} has no DS record.").ConfigureAwait(false);
            return result.Status == DnsSecValidationStatus.Secure
                ? ZoneKeysResult.Insecure(result.Message)
                : new ZoneKeysResult(result.Status, Array.Empty<DnsSecKey>(), result.Message);
        }

        private async Task<DnsSecValidationResult> ValidateDenialBySignerAsync(DnsResponse response,
            DnsWireResourceRecord[] proofRecords, string name, CancellationToken cancellationToken,
            Func<DnsResponse, bool> proves, string successMessage) {
            var candidates = new Dictionary<string, List<DnsWireResourceRecord>>(StringComparer.Ordinal);
            foreach (DnsWireResourceRecord proof in proofRecords) {
                string owner = DnsWireNameCodec.Canonical(proof.Name);
                foreach (DnsSecSignature signature in ReadSignatures(response, owner, proof.Type, proof.Class)) {
                    if (!IsNameWithinZone(name, signature.SignerName)) continue;
                    if (!candidates.TryGetValue(signature.SignerName, out List<DnsWireResourceRecord>? records)) {
                        records = new List<DnsWireResourceRecord>();
                        candidates.Add(signature.SignerName, records);
                    }
                    if (!records.Contains(proof)) records.Add(proof);
                }
            }
            if (candidates.Count == 0) return DnsSecValidationResult.Bogus("No single in-bailiwick signer authenticates the DNSSEC denial records.");

            DnsSecValidationResult? bestFailure = null;
            foreach (KeyValuePair<string, List<DnsWireResourceRecord>> candidate in candidates) {
                ZoneKeysResult keys = await GetZoneKeysAsync(candidate.Key, cancellationToken).ConfigureAwait(false);
                if (keys.Status == DnsSecValidationStatus.Insecure) return DnsSecValidationResult.Insecure(keys.Message);
                if (keys.Status != DnsSecValidationStatus.Secure) {
                    bestFailure = new DnsSecValidationResult(keys.Status, keys.Message);
                    continue;
                }

                bool authenticated = true;
                foreach (IGrouping<RrsetKey, DnsWireResourceRecord> rrset in candidate.Value.GroupBy(record =>
                    new RrsetKey(DnsWireNameCodec.Canonical(record.Name), record.Type, record.Class))) {
                    List<DnsSecSignature> signatures = ReadSignatures(response, rrset.Key.Name, rrset.Key.Type, rrset.Key.Class)
                        .Where(signature => string.Equals(signature.SignerName, candidate.Key, StringComparison.Ordinal))
                        .ToList();
                    DnsSecValidationResult verified = VerifyRrset(response, rrset.Key, signatures, keys.Keys);
                    if (verified.Status == DnsSecValidationStatus.Secure) continue;
                    authenticated = false;
                    bestFailure = verified;
                    break;
                }
                if (!authenticated) continue;

                DnsResponse restricted = RestrictDenialResponse(response, candidate.Value, candidate.Key);
                if (proves(restricted)) return DnsSecValidationResult.Secure(successMessage);
            }

            return bestFailure ?? DnsSecValidationResult.Indeterminate(
                "Authenticated denial records were present but no single signer fully proved the negative response.");
        }

        private static DnsResponse RestrictDenialResponse(DnsResponse response,
            IReadOnlyCollection<DnsWireResourceRecord> proofRecords, string signer) {
            var selected = new HashSet<DnsWireResourceRecord>(proofRecords);
            DnsWireResourceRecord[] Filter(DnsWireResourceRecord[]? section) => (section ?? Array.Empty<DnsWireResourceRecord>())
                .Where(record => selected.Contains(record) ||
                    record.Type == DnsRecordType.RRSIG &&
                    DnsSecWire.TryReadSignature(response.WireMessage, record, out DnsSecSignature signature) &&
                    string.Equals(signature.SignerName, signer, StringComparison.Ordinal) &&
                    selected.Any(proof => proof.Type == signature.TypeCovered && proof.Class == record.Class &&
                        string.Equals(DnsWireNameCodec.Canonical(proof.Name), DnsWireNameCodec.Canonical(record.Name), StringComparison.Ordinal)))
                .ToArray();

            return new DnsResponse {
                Status = response.Status,
                WireMessage = response.WireMessage,
                WireAnswers = Filter(response.WireAnswers),
                WireAuthorities = Filter(response.WireAuthorities),
                WireAdditional = Filter(response.WireAdditional)
            };
        }

        internal static bool TryFollowAnswerChain(DnsWireResourceRecord[] answers, string requestedName,
            DnsRecordType requestedType, out string finalName, out bool terminal, out string? error) {
            finalName = DnsWireNameCodec.Canonical(requestedName);
            terminal = false;
            error = null;
            var visited = new HashSet<string>(StringComparer.Ordinal);
            for (int hop = 0; hop <= answers.Length; hop++) {
                if (!visited.Add(finalName)) {
                    error = "The DNS answer contains a canonical-name loop.";
                    return false;
                }
                string currentName = finalName;
                if (answers.Any(record => (record.Type == requestedType
                                             || requestedType == DnsRecordType.ANY
                                             && record.Type != DnsRecordType.RRSIG
                                             && record.Type != DnsRecordType.OPT) &&
                    string.Equals(DnsWireNameCodec.Canonical(record.Name), currentName, StringComparison.Ordinal))) {
                    terminal = true;
                    return true;
                }
                DnsWireResourceRecord? alias = answers.FirstOrDefault(record => record.Type == DnsRecordType.CNAME &&
                    string.Equals(DnsWireNameCodec.Canonical(record.Name), currentName, StringComparison.Ordinal));
                if (alias.HasValue && alias.Value.Type == DnsRecordType.CNAME) {
                    finalName = DnsWireNameCodec.Canonical(alias.Value.Data);
                    continue;
                }
                if (!TryGetDnameTarget(answers, currentName, out string? dnameTarget)) return true;
                finalName = dnameTarget!;
            }
            error = "The DNS answer contains an excessively long canonical-name chain.";
            return false;
        }

        internal static bool IsSynthesizedDnameCname(
            DnsWireResourceRecord[] answers,
            DnsWireResourceRecord cname) {
            if (cname.Type != DnsRecordType.CNAME) return false;
            string owner = DnsWireNameCodec.Canonical(cname.Name);
            return TryGetDnameTarget(answers, owner, out string? target)
                && string.Equals(DnsWireNameCodec.Canonical(cname.Data), target, StringComparison.Ordinal);
        }

        private static bool TryGetDnameTarget(
            DnsWireResourceRecord[] answers,
            string currentName,
            out string? target) {
            target = null;
            string canonicalName = DnsWireNameCodec.Canonical(currentName);
            DnsWireResourceRecord? dname = answers
                .Where(record => record.Type == DnsRecordType.DNAME)
                .Where(record => IsStrictSubdomain(canonicalName, DnsWireNameCodec.Canonical(record.Name)))
                .OrderByDescending(record => DnsWireNameCodec.Canonical(record.Name).Length)
                .Cast<DnsWireResourceRecord?>()
                .FirstOrDefault();
            if (!dname.HasValue) return false;

            string owner = DnsWireNameCodec.Canonical(dname.Value.Name);
            string replacement = DnsWireNameCodec.Canonical(dname.Value.Data);
            string prefix = canonicalName.Substring(0, canonicalName.Length - owner.Length).TrimEnd('.');
            target = prefix.Length == 0 ? replacement : $"{prefix}.{replacement}";
            return true;
        }

        private static bool IsStrictSubdomain(string name, string parent) {
            return parent == "."
                ? name != "."
                : name.Length > parent.Length
                  && name.EndsWith("." + parent, StringComparison.Ordinal);
        }

        internal static bool IsNameWithinZone(string name, string zone) {
            string canonicalName = DnsWireNameCodec.Canonical(name);
            string canonicalZone = DnsWireNameCodec.Canonical(zone);
            return canonicalZone == "." || string.Equals(canonicalName, canonicalZone, StringComparison.Ordinal) ||
                   canonicalName.EndsWith("." + canonicalZone, StringComparison.Ordinal);
        }

        private async Task<DnsSecValidationResult> FindUnsignedDelegationAsync(string name, CancellationToken cancellationToken) {
            string current = DnsWireNameCodec.Canonical(name);
            while (current != ".") {
                DnsResponse response = await _lookup(current, DnsRecordType.DS, cancellationToken).ConfigureAwait(false);
                if (!(response.WireAnswers ?? Array.Empty<DnsWireResourceRecord>()).Any(record => record.Type == DnsRecordType.DS)) {
                    ZoneKeysResult result = await ValidateUnsignedDelegationAsync(current, response, cancellationToken).ConfigureAwait(false);
                    if (result.Status == DnsSecValidationStatus.Insecure) return DnsSecValidationResult.Insecure(result.Message);
                    if (result.Status == DnsSecValidationStatus.Bogus) return DnsSecValidationResult.Bogus(result.Message);
                }
                int dot = current.IndexOf('.');
                current = dot < 0 || dot == current.Length - 1 ? "." : current.Substring(dot + 1);
            }
            return DnsSecValidationResult.Indeterminate("The answer was unsigned and no secure unsigned delegation proof was found.");
        }

        private DnsSecValidationResult VerifyRrset(DnsResponse response, RrsetKey rrset,
            IEnumerable<DnsSecSignature> signatures, IReadOnlyCollection<DnsSecKey> keys) {
            bool supported = false;
            bool timeFailure = false;
            bool signerScopeFailure = false;
            foreach (DnsSecSignature signature in signatures) {
                if (!IsNameWithinZone(rrset.Name, signature.SignerName)) {
                    signerScopeFailure = true;
                    continue;
                }
                DnsSecKey[] candidates = keys.Where(key => key.KeyTag == signature.KeyTag && key.Algorithm == signature.Algorithm).ToArray();
                if (candidates.Length == 0
                    || !DnsSecCrypto.IsSupportedAlgorithm(signature.Algorithm, _signatureVerifier)) continue;
                supported = true;
                if (!DnsSecWire.SignatureTimeIsValid(signature, _now)) {
                    timeFailure = true;
                    continue;
                }
                byte[] data;
                try {
                    data = DnsSecWire.BuildSignedData(response.WireMessage, signature, DnsSecWire.Records(response));
                } catch (DnsClientException ex) {
                    return DnsSecValidationResult.Bogus(ex.Message);
                }
                if (candidates.Any(key => DnsSecCrypto.Verify(key, data, signature.Signature, _signatureVerifier))) {
                    return DnsSecValidationResult.Secure($"Validated {rrset.Name} {rrset.Type} with key tag {signature.KeyTag}.");
                }
            }
            if (timeFailure) return DnsSecValidationResult.Bogus($"Every supported signature for {rrset.Name} {rrset.Type} is outside its validity interval.");
            if (signerScopeFailure) return DnsSecValidationResult.Bogus(
                $"No RRSIG signer is an ancestor of the {rrset.Name} RRset owner.");
            return supported
                ? DnsSecValidationResult.Bogus($"The signature for {rrset.Name} {rrset.Type} failed cryptographic verification.")
                : DnsSecValidationResult.Indeterminate($"No supported signing algorithm/key was available for {rrset.Name} {rrset.Type}.");
        }

        private static List<DnsSecSignature> ReadSignatures(DnsResponse response, string owner,
            DnsRecordType type, ushort recordClass) {
            var result = new List<DnsSecSignature>();
            foreach (DnsWireResourceRecord record in DnsSecWire.Records(response)) {
                if (record.Type != DnsRecordType.RRSIG || record.Class != recordClass ||
                    !string.Equals(DnsWireNameCodec.Canonical(record.Name), owner, StringComparison.Ordinal)) continue;
                if (DnsSecWire.TryReadSignature(response.WireMessage, record, out DnsSecSignature signature) && signature.TypeCovered == type) result.Add(signature);
            }
            return result;
        }

        private static bool TryReadDs(byte[] message, DnsWireResourceRecord record, out ushort keyTag,
            out byte algorithm, out byte digestType, out byte[] digest) {
            keyTag = 0;
            algorithm = 0;
            digestType = 0;
            digest = Array.Empty<byte>();
            if (record.RdataLength < 5) return false;
            var reader = new DnsWireReader(message, record.RdataOffset, record.RdataOffset + record.RdataLength);
            keyTag = reader.ReadUInt16();
            algorithm = reader.ReadByte();
            digestType = reader.ReadByte();
            digest = reader.ReadBytes(reader.End - reader.Position);
            return digest.Length > 0;
        }

        private static string ToHex(byte[] value) => BitConverter.ToString(value).Replace("-", string.Empty);

        private readonly struct RrsetKey : IEquatable<RrsetKey> {
            internal RrsetKey(string name, DnsRecordType type, ushort recordClass) {
                Name = name;
                Type = type;
                Class = recordClass;
            }
            internal string Name { get; }
            internal DnsRecordType Type { get; }
            internal ushort Class { get; }
            public bool Equals(RrsetKey other) => Type == other.Type && Class == other.Class && string.Equals(Name, other.Name, StringComparison.Ordinal);
            public override bool Equals(object? obj) => obj is RrsetKey other && Equals(other);
            public override int GetHashCode() => (Name, Type, Class).GetHashCode();
        }

        private readonly struct ZoneKeysResult {
            internal ZoneKeysResult(DnsSecValidationStatus status, IReadOnlyCollection<DnsSecKey> keys, string message) {
                Status = status;
                Keys = keys;
                Message = message;
            }
            internal DnsSecValidationStatus Status { get; }
            internal IReadOnlyCollection<DnsSecKey> Keys { get; }
            internal string Message { get; }
            internal static ZoneKeysResult Secure(IReadOnlyCollection<DnsSecKey> keys, string message) => new(DnsSecValidationStatus.Secure, keys, message);
            internal static ZoneKeysResult Insecure(string message) => new(DnsSecValidationStatus.Insecure, Array.Empty<DnsSecKey>(), message);
            internal static ZoneKeysResult Bogus(string message) => new(DnsSecValidationStatus.Bogus, Array.Empty<DnsSecKey>(), message);
            internal static ZoneKeysResult Indeterminate(string message) => new(DnsSecValidationStatus.Indeterminate, Array.Empty<DnsSecKey>(), message);
        }
    }
}
