using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace DnsClientX {
    /// <summary>
    /// Partial <see cref="ClientX"/> class implementing non-recursive resolution from a root-server seed set.
    /// </summary>
    public partial class ClientX {
        /// <summary>
        /// Resolves a DNS name by following validated referrals from a root-server seed set.
        /// </summary>
        /// <param name="name">Domain name to resolve.</param>
        /// <param name="type">Record type to resolve.</param>
        /// <param name="servers">Optional root-server seed addresses.</param>
        /// <param name="maxHops">Maximum referral and alias hops.</param>
        /// <param name="port">Port used for every authoritative query.</param>
        /// <param name="cancellationToken">Token used to cancel the operation.</param>
        /// <returns>The terminal authoritative response.</returns>
        public Task<DnsResponse> ResolveFromRoot(
            string name,
            DnsRecordType type = DnsRecordType.A,
            IEnumerable<string>? servers = null,
            int maxHops = 10,
            int port = 53,
            CancellationToken cancellationToken = default) {
            return ResolveFromRootWithTelemetry(
                name,
                type,
                servers,
                maxHops,
                port,
                requestDnsSec: false,
                validateDnsSec: false,
                cancellationToken);
        }

        /// <summary>
        /// Resolves a DNS name from a root-server seed set and optionally validates DNSSEC locally.
        /// </summary>
        /// <param name="name">Domain name to resolve.</param>
        /// <param name="type">Record type to resolve.</param>
        /// <param name="servers">Root-server seed addresses.</param>
        /// <param name="maxHops">Maximum referral and alias hops.</param>
        /// <param name="port">Port used for every authoritative query.</param>
        /// <param name="requestDnsSec">Whether queries request DNSSEC records.</param>
        /// <param name="validateDnsSec">Whether the final answer is validated locally.</param>
        /// <param name="cancellationToken">Token used to cancel the operation.</param>
        /// <returns>The terminal authoritative response.</returns>
        public Task<DnsResponse> ResolveFromRoot(
            string name,
            DnsRecordType type,
            IEnumerable<string>? servers,
            int maxHops,
            int port,
            bool requestDnsSec,
            bool validateDnsSec,
            CancellationToken cancellationToken = default) {
            return ResolveFromRootWithTelemetry(
                name,
                type,
                servers,
                maxHops,
                port,
                requestDnsSec,
                validateDnsSec,
                cancellationToken);
        }

        private async Task<DnsResponse> ResolveFromRootWithTelemetry(
            string name,
            DnsRecordType type,
            IEnumerable<string>? servers,
            int maxHops,
            int port,
            bool requestDnsSec,
            bool validateDnsSec,
            CancellationToken cancellationToken,
            Configuration? queryConfiguration = null) {
            ThrowIfDisposed();
            queryConfiguration ??= EndpointConfiguration.CreateQuerySnapshot(name);
            using DnsClientTelemetry.DnsQueryTelemetryScope? telemetry = DnsClientTelemetry.StartQuery(name, type, EndpointConfiguration);
            try {
                DnsResponse response = await ResolveFromRootCore(
                    name,
                    type,
                    servers,
                    maxHops,
                    port,
                    requestDnsSec,
                    validateDnsSec,
                    queryConfiguration.EnableQNameMinimization,
                    queryConfiguration.Rfc5011TrustAnchorStorePath,
                    queryConfiguration.DnsSecSignatureVerifier,
                    cancellationToken).ConfigureAwait(false);
                telemetry?.Complete(response);
                return response;
            } catch (Exception ex) {
                telemetry?.Fail(ex);
                throw;
            }
        }

        private async Task<DnsResponse> ResolveFromRootCore(
            string name,
            DnsRecordType type,
            IEnumerable<string>? servers,
            int maxHops,
            int port,
            bool requestDnsSec,
            bool validateDnsSec,
            bool enableQNameMinimization,
            string? trustAnchorStorePath,
            IDnsSecSignatureVerifier? signatureVerifier,
            CancellationToken cancellationToken) {
            if (string.IsNullOrWhiteSpace(name)) throw new ArgumentNullException(nameof(name));
            if (maxHops <= 0) throw new ArgumentOutOfRangeException(nameof(maxHops));
            if (port <= 0 || port > ushort.MaxValue) throw new ArgumentOutOfRangeException(nameof(port));

            if (type == DnsRecordType.PTR) name = ConvertToPtrFormat(name);
            name = ConvertToPunycode(name);

            string[] rootServers = (servers ?? RootServers.Servers)
                .Select(server => (server ?? string.Empty).Trim().TrimEnd('.'))
                .Where(server => server.Length > 0)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            if (rootServers.Length == 0) throw new ArgumentException("At least one root-server seed is required.", nameof(servers));

            var state = new RootResolutionState(
                rootServers,
                maxHops,
                port,
                requestDnsSec || validateDnsSec,
                enableQNameMinimization);
            string normalizedName = NormalizeIterativeName(name);
            DnsResponse response = await ResolveIteratively(
                normalizedName,
                type,
                rootServers,
                state,
                cancellationToken).ConfigureAwait(false);
            response.QNameMinimizedQueryCount = state.MinimizedQueryCount;
            response.QNameMinimizationFallbackCount = state.MinimizationFallbackCount;
            if (validateDnsSec) {
                var validator = new DnsSecValidationEngine(async (materialName, materialType, token) => {
                    var materialState = new RootResolutionState(
                        rootServers,
                        maxHops,
                        port,
                        requestDnsSec: true,
                        enableQNameMinimization);
                    return await ResolveIteratively(
                        NormalizeIterativeName(materialName),
                        materialType,
                        rootServers,
                        materialState,
                        token).ConfigureAwait(false);
                }, trustAnchorStorePath: trustAnchorStorePath,
                    signatureVerifier: signatureVerifier);
                DnsSecValidationResult validation = DnsSecValidationResult.Indeterminate(
                    "The iterative resolver did not retain a terminal DNSSEC validation segment.");
                IReadOnlyList<RootDnsSecSegment> segments = state.ValidationSegments.Count == 0
                    ? new[] { new RootDnsSecSegment(response, normalizedName, type, aliasOnly: false) }
                    : state.ValidationSegments;
                bool insecureSegment = false;
                if (segments.Any(segment => !segment.AliasOnly)) {
                    foreach (RootDnsSecSegment segment in segments) {
                        validation = segment.AliasOnly
                            ? await validator.ValidateAliasAsync(segment.Response, segment.Name, segment.Type,
                                cancellationToken).ConfigureAwait(false)
                            : await validator.ValidateAsync(segment.Response, segment.Name, segment.Type,
                                cancellationToken).ConfigureAwait(false);
                        if (validation.Status == DnsSecValidationStatus.Bogus
                            || validation.Status == DnsSecValidationStatus.Indeterminate) break;
                        insecureSegment |= validation.Status == DnsSecValidationStatus.Insecure;
                    }
                }
                if (insecureSegment
                    && validation.Status != DnsSecValidationStatus.Bogus
                    && validation.Status != DnsSecValidationStatus.Indeterminate) {
                    validation = DnsSecValidationResult.Insecure(
                        "At least one authenticated iterative answer segment crosses an unsigned delegation.");
                }
                response.DnsSecValidationStatus = validation.Status;
                response.DnsSecValidationMessage = validation.Message;
                if (validation.Status == DnsSecValidationStatus.Bogus
                    || validation.Status == DnsSecValidationStatus.Indeterminate) {
                    string error = $"DNSSEC {validation.Status.ToString().ToLowerInvariant()}: {validation.Message}";
                    response.Error = string.IsNullOrEmpty(response.Error) ? error : $"{response.Error} {error}";
                }
            }

            return response;
        }

        internal static string NormalizeIterativeName(string name) {
            string trimmed = name.Trim();
            return trimmed == "." ? "." : trimmed.TrimEnd('.');
        }

        private async Task<DnsResponse> ResolveIteratively(
            string name,
            DnsRecordType type,
            IReadOnlyList<string> startingServers,
            RootResolutionState state,
            CancellationToken cancellationToken) {
            cancellationToken.ThrowIfCancellationRequested();
            if (state.Hops >= state.MaxHops || !state.ActiveQueries.Add($"{name}|{type}")) {
                return CreateRootFailure(name, type, "The iterative resolution hop limit or alias-cycle guard was reached.");
            }

            try {
                IReadOnlyList<string> currentServers = startingServers;
                string currentBailiwick = ".";
                DnsResponse? lastResponse = null;
                bool rejectedNonAuthoritativeTerminal = false;
                while (state.Hops < state.MaxHops) {
                    Referral? referral = null;
                    bool advancedMinimization = false;
                    foreach (string server in currentServers) {
                        cancellationToken.ThrowIfCancellationRequested();
                        IterativeQuestion question = SelectIterativeQuestion(
                            name, type, currentBailiwick, state.EnableQNameMinimization);
                        DnsResponse response = await QueryAuthoritativeServer(
                            server,
                            state.Port,
                            question.Name,
                            question.Type,
                            state.RequestDnsSec,
                            cancellationToken).ConfigureAwait(false);
                        if (!question.IsFinal) state.MinimizedQueryCount++;
                        lastResponse = response;

                        if (!question.IsFinal) {
                            Referral? minimizedReferral = FindReferral(response, name, currentBailiwick);
                            if (minimizedReferral != null) {
                                referral = minimizedReferral;
                                break;
                            }

                            if (response.Status == DnsResponseCode.NoError
                                && response.IsAuthoritativeAnswer) {
                                // An authenticated/authoritative NODATA result means there is no zone cut at
                                // this candidate. Keep the same servers and reveal just one more label.
                                currentBailiwick = question.Name;
                                state.Hops++;
                                advancedMinimization = true;
                                break;
                            }

                            // RFC 9156 permits a full-question fallback for incompatible authorities.
                            // Record the privacy downgrade rather than silently claiming full minimization.
                            state.MinimizationFallbackCount++;
                            response = await QueryAuthoritativeServer(
                                server,
                                state.Port,
                                name,
                                type,
                                state.RequestDnsSec,
                                cancellationToken).ConfigureAwait(false);
                            lastResponse = response;
                        }

                        bool hasRequestedAnswer = HasRequestedAnswer(response, name, type);
                        string? aliasTarget = FindAliasTarget(response, name);
                        if (response.IsAuthoritativeAnswer && hasRequestedAnswer) {
                            state.ValidationSegments.Add(new RootDnsSecSegment(response, name, type, aliasOnly: false));
                            return response;
                        }

                        if (response.IsAuthoritativeAnswer && aliasTarget != null) {
                            state.ValidationSegments.Add(new RootDnsSecSegment(response, name, type, aliasOnly: true));
                            state.Hops++;
                            DnsResponse aliasResponse = await ResolveIteratively(
                                aliasTarget,
                                type,
                                state.RootServers,
                                state,
                                cancellationToken).ConfigureAwait(false);
                            if (!aliasResponse.IsAuthoritativeAnswer
                                && aliasResponse.Status == DnsResponseCode.NoError
                                && string.IsNullOrEmpty(aliasResponse.Error)) {
                                aliasResponse.Status = DnsResponseCode.ServerFailure;
                                aliasResponse.Error = $"The iterative alias target {aliasTarget} did not produce a terminal answer or authenticated negative response.";
                            }
                            return MergeAliasResponse(response, aliasResponse);
                        }

                        if (response.IsAuthoritativeAnswer) {
                            state.ValidationSegments.Add(new RootDnsSecSegment(response, name, type, aliasOnly: false));
                            return response;
                        }

                        if (hasRequestedAnswer
                            || aliasTarget != null
                            || response.Status == DnsResponseCode.NXDomain
                            || HasNegativeSoa(response)) {
                            rejectedNonAuthoritativeTerminal = true;
                        }

                        Referral? candidate = FindReferral(response, name, currentBailiwick);
                        if (candidate != null) {
                            referral = candidate;
                            break;
                        }
                    }

                    if (advancedMinimization) continue;

                    if (referral == null) {
                        if (rejectedNonAuthoritativeTerminal) {
                            return CreateRootFailure(name, type,
                                "Iterative resolution rejected a non-authoritative terminal response.");
                        }
                        return lastResponse ?? CreateRootFailure(name, type, "No authoritative server returned a usable response.");
                    }

                    state.Hops++;
                    string referralKey = $"{name}|{type}|{referral.Zone}|{string.Join(",", referral.NameServers)}";
                    if (!state.VisitedReferrals.Add(referralKey)) {
                        return referral.Response;
                    }

                    string[] nextServers = await ResolveReferralServers(referral, state, cancellationToken).ConfigureAwait(false);
                    if (nextServers.Length == 0) {
                        return referral.Response;
                    }
                    currentServers = nextServers;
                    currentBailiwick = referral.Zone;
                }

                return lastResponse ?? CreateRootFailure(name, type, "The iterative resolution hop limit was reached.");
            } finally {
                state.ActiveQueries.Remove($"{name}|{type}");
            }
        }

        private async Task<string[]> ResolveReferralServers(
            Referral referral,
            RootResolutionState state,
            CancellationToken cancellationToken) {
            var addresses = new List<string>();
            var nameServersWithGlue = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string nameServer in referral.NameServers) {
                string[] glueAddresses = GetInBailiwickGlueAddresses(
                    referral.RespondingZone,
                    nameServer,
                    referral.Response.Additional);
                if (glueAddresses.Length > 0) {
                    addresses.AddRange(glueAddresses);
                    nameServersWithGlue.Add(nameServer);
                }
            }

            foreach (string nameServer in referral.NameServers) {
                if (nameServersWithGlue.Contains(nameServer)) {
                    continue;
                }
                if (state.NameServerAddressCache.TryGetValue(nameServer, out string[]? cachedAddresses)) {
                    addresses.AddRange(cachedAddresses);
                    continue;
                }

                if (!state.ActiveNameServerLookups.Add(nameServer)) {
                    continue;
                }

                try {
                    var resolvedAddresses = new List<string>();
                    foreach (DnsRecordType addressType in new[] { DnsRecordType.A, DnsRecordType.AAAA }) {
                        RootResolutionState addressState = state.CreateAddressLookupState();
                        DnsResponse addressResponse = await ResolveIteratively(
                            nameServer,
                            addressType,
                            state.RootServers,
                            addressState,
                            cancellationToken).ConfigureAwait(false);
                        foreach (DnsAnswer answer in addressResponse.Answers ?? Array.Empty<DnsAnswer>()) {
                            if (answer.Type == addressType
                                && answer.Name.TrimEnd('.').Equals(nameServer, StringComparison.OrdinalIgnoreCase)
                                && IPAddress.TryParse(answer.Data, out IPAddress? address)) {
                                resolvedAddresses.Add(address.ToString());
                            }
                        }
                    }

                    string[] result = resolvedAddresses
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .ToArray();
                    state.NameServerAddressCache[nameServer] = result;
                    addresses.AddRange(result);
                } finally {
                    state.ActiveNameServerLookups.Remove(nameServer);
                }
            }

            return addresses.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        }

        internal static string[] GetInBailiwickGlueAddresses(
            string respondingZone,
            string nameServer,
            IEnumerable<DnsAnswer>? additional) {
            // RFC 9471 sibling glue can be required to break cyclic dependencies. The
            // trust boundary is therefore the responding parent's zone, not only the
            // delegated child. Names outside that parent zone are resolved independently.
            if (!IsSubdomainOrEqual(nameServer, respondingZone)) {
                return Array.Empty<string>();
            }

            string canonicalNameServer = DnsWireNameCodec.Canonical(nameServer);
            return (additional ?? Array.Empty<DnsAnswer>())
                .Where(answer => string.Equals(
                    DnsWireNameCodec.Canonical(answer.Name),
                    canonicalNameServer,
                    StringComparison.Ordinal))
                .Where(answer => answer.Type == DnsRecordType.A || answer.Type == DnsRecordType.AAAA)
                .Select(answer => IPAddress.TryParse(answer.Data, out IPAddress? address) ? address.ToString() : null)
                .Where(address => address != null)
                .Cast<string>()
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        private async Task<DnsResponse> QueryAuthoritativeServer(
            string server,
            int port,
            string name,
            DnsRecordType type,
            bool requestDnsSec,
            CancellationToken cancellationToken) {
            var configuration = new Configuration(server, DnsRequestFormat.DnsOverUDP) {
                Port = port,
                UseTcpFallback = true,
                RecursionDesired = false,
                EnableEdns = requestDnsSec,
                TimeOut = EndpointConfiguration.TimeOut,
                LocalEndPoint = EndpointConfiguration.LocalEndPoint == null
                    ? null
                    : new IPEndPoint(EndpointConfiguration.LocalEndPoint.Address, EndpointConfiguration.LocalEndPoint.Port)
            };
            configuration.SelectHostNameStrategy();
            return await DnsWireResolveUdp.ResolveWireFormatUdp(
                server,
                port,
                name,
                type,
                requestDnsSec,
                validateDnsSec: false,
                Debug,
                configuration,
                maxRetries: 1,
                cancellationToken,
                _udpClientPool,
                configuration.EnableTcpConnectionReuse ? _streamConnectionPool : null).ConfigureAwait(false);
        }

        internal static Referral? FindReferral(
            DnsResponse response,
            string queryName,
            string respondingZone = ".") {
            IGrouping<string, DnsAnswer>? group = (response.Authorities ?? Array.Empty<DnsAnswer>())
                .Where(answer => answer.Type == DnsRecordType.NS)
                .Where(answer => IsSubdomainOrEqual(queryName, answer.Name.TrimEnd('.')))
                .GroupBy(answer => answer.Name.TrimEnd('.'), StringComparer.OrdinalIgnoreCase)
                .OrderByDescending(item => LabelCount(item.Key))
                .FirstOrDefault();
            if (group == null) {
                return null;
            }

            string[] nameServers = group
                .Select(answer => answer.Data.TrimEnd('.'))
                .Where(value => value.Length > 0)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            return nameServers.Length == 0
                ? null
                : new Referral(group.Key, nameServers, response, respondingZone);
        }

        internal static string? FindAliasTarget(DnsResponse response, string queryName) {
            DnsAnswer? cname = (response.Answers ?? Array.Empty<DnsAnswer>())
                .Where(answer => answer.Type == DnsRecordType.CNAME)
                .Cast<DnsAnswer?>()
                .FirstOrDefault(answer => answer!.Value.Name.TrimEnd('.').Equals(queryName, StringComparison.OrdinalIgnoreCase));
            if (cname.HasValue) {
                return cname.Value.Data.TrimEnd('.');
            }

            DnsAnswer? dname = (response.Answers ?? Array.Empty<DnsAnswer>())
                .Where(answer => answer.Type == DnsRecordType.DNAME)
                .Where(answer => IsStrictSubdomain(queryName, answer.Name.TrimEnd('.')))
                .OrderByDescending(answer => LabelCount(answer.Name))
                .Cast<DnsAnswer?>()
                .FirstOrDefault();
            if (!dname.HasValue) {
                return null;
            }

            string owner = dname.Value.Name.TrimEnd('.');
            string target = dname.Value.Data.TrimEnd('.');
            return queryName.Substring(0, queryName.Length - owner.Length).TrimEnd('.') + "." + target;
        }

        private static DnsResponse MergeAliasResponse(DnsResponse alias, DnsResponse terminal) {
            terminal.Answers = (alias.Answers ?? Array.Empty<DnsAnswer>())
                .Concat(terminal.Answers ?? Array.Empty<DnsAnswer>())
                .Distinct()
                .ToArray();
            terminal.RefreshDerivedData();
            return terminal;
        }

        internal static bool HasRequestedAnswer(DnsResponse response, string queryName, DnsRecordType type) {
            DnsAnswer[] answers = response.Answers ?? Array.Empty<DnsAnswer>();
            string candidate = queryName.TrimEnd('.');
            var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            while (visited.Add(candidate)) {
                if (answers.Any(answer =>
                    answer.Name.TrimEnd('.').Equals(candidate, StringComparison.OrdinalIgnoreCase)
                    && (type == DnsRecordType.ANY || answer.Type == type))) {
                    return true;
                }

                string? aliasTarget = FindAliasTarget(response, candidate);
                if (aliasTarget == null) {
                    return false;
                }
                candidate = aliasTarget;
            }

            return false;
        }

        private static bool HasNegativeSoa(DnsResponse response) {
            return (response.Authorities ?? Array.Empty<DnsAnswer>()).Any(answer => answer.Type == DnsRecordType.SOA);
        }

        private static bool IsSubdomainOrEqual(string name, string parent) {
            return DnsWireNameCodec.IsSubdomainOrEqual(name, string.IsNullOrWhiteSpace(parent) ? "." : parent);
        }

        private static bool IsStrictSubdomain(string name, string parent) {
            string normalizedName = name.TrimEnd('.');
            string normalizedParent = parent.TrimEnd('.');
            return normalizedParent.Length == 0
                ? normalizedName.Length > 0
                : normalizedName.EndsWith("." + normalizedParent, StringComparison.OrdinalIgnoreCase);
        }

        private static int LabelCount(string name) {
            return name.Trim('.').Length == 0 ? 0 : name.Trim('.').Split('.').Length;
        }

        internal static IterativeQuestion SelectIterativeQuestion(string name, DnsRecordType type,
            string currentBailiwick, bool enabled) {
            string normalizedName = NormalizeIterativeName(name);
            if (!enabled || normalizedName == ".") return new IterativeQuestion(normalizedName, type, true);
            string normalizedBailiwick = NormalizeIterativeName(
                string.IsNullOrWhiteSpace(currentBailiwick) ? "." : currentBailiwick);
            if (normalizedBailiwick != "." && !IsSubdomainOrEqual(normalizedName, normalizedBailiwick)) {
                return new IterativeQuestion(normalizedName, type, true);
            }

            string[] labels = normalizedName.Split('.');
            int bailiwickLabels = normalizedBailiwick == "." ? 0 : LabelCount(normalizedBailiwick);
            if (bailiwickLabels >= labels.Length) return new IterativeQuestion(normalizedName, type, true);
            // RFC 9156 step 3: DS data belongs to the parent side of a zone cut. Once the
            // closest known authority is exactly one label above QNAME, ask that parent the
            // original DS question instead of following an NS referral to the child.
            if (type == DnsRecordType.DS && bailiwickLabels + 1 == labels.Length) {
                return new IterativeQuestion(normalizedName, type, true);
            }
            string minimizedName = string.Join(".", labels.Skip(labels.Length - bailiwickLabels - 1));
            return new IterativeQuestion(minimizedName, DnsRecordType.NS, false);
        }

        private static DnsResponse CreateRootFailure(string name, DnsRecordType type, string error) {
            return new DnsResponse {
                Questions = [new DnsQuestion {
                    Name = name,
                    OriginalName = name,
                    Type = type,
                    RequestFormat = DnsRequestFormat.DnsOverUDP
                }],
                Status = DnsResponseCode.ServerFailure,
                Error = error
            };
        }

        private sealed class RootResolutionState {
            internal RootResolutionState(
                string[] rootServers,
                int maxHops,
                int port,
                bool requestDnsSec,
                bool enableQNameMinimization,
                Dictionary<string, string[]>? nameServerAddressCache = null,
                HashSet<string>? activeNameServerLookups = null) {
                RootServers = rootServers;
                MaxHops = maxHops;
                Port = port;
                RequestDnsSec = requestDnsSec;
                EnableQNameMinimization = enableQNameMinimization;
                NameServerAddressCache = nameServerAddressCache
                    ?? new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
                ActiveNameServerLookups = activeNameServerLookups
                    ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            }

            internal string[] RootServers { get; }
            internal int MaxHops { get; }
            internal int Port { get; }
            internal bool RequestDnsSec { get; }
            internal bool EnableQNameMinimization { get; }
            internal int Hops { get; set; }
            internal int MinimizedQueryCount { get; set; }
            internal int MinimizationFallbackCount { get; set; }
            internal HashSet<string> ActiveQueries { get; } = new(StringComparer.OrdinalIgnoreCase);
            internal HashSet<string> VisitedReferrals { get; } = new(StringComparer.OrdinalIgnoreCase);
            internal Dictionary<string, string[]> NameServerAddressCache { get; }
            internal HashSet<string> ActiveNameServerLookups { get; }
            internal List<RootDnsSecSegment> ValidationSegments { get; } = new();

            internal RootResolutionState CreateAddressLookupState() {
                return new RootResolutionState(
                    RootServers,
                    MaxHops,
                    Port,
                    RequestDnsSec,
                    EnableQNameMinimization,
                    NameServerAddressCache,
                    ActiveNameServerLookups);
            }
        }

        internal readonly struct IterativeQuestion {
            internal IterativeQuestion(string name, DnsRecordType type, bool isFinal) {
                Name = name;
                Type = type;
                IsFinal = isFinal;
            }
            internal string Name { get; }
            internal DnsRecordType Type { get; }
            internal bool IsFinal { get; }
        }

        private sealed class RootDnsSecSegment {
            internal RootDnsSecSegment(DnsResponse response, string name, DnsRecordType type, bool aliasOnly) {
                Response = response;
                Name = name;
                Type = type;
                AliasOnly = aliasOnly;
            }

            internal DnsResponse Response { get; }
            internal string Name { get; }
            internal DnsRecordType Type { get; }
            internal bool AliasOnly { get; }
        }

        internal sealed class Referral {
            internal Referral(
                string zone,
                string[] nameServers,
                DnsResponse response,
                string respondingZone) {
                Zone = zone;
                NameServers = nameServers;
                Response = response;
                RespondingZone = string.IsNullOrWhiteSpace(respondingZone) ? "." : respondingZone;
            }

            internal string Zone { get; }
            internal string[] NameServers { get; }
            internal DnsResponse Response { get; }
            internal string RespondingZone { get; }
        }
    }
}
