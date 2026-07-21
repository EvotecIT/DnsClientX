using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Threading;

namespace DnsClientX {
    /// <summary>
    /// Provides access to DNS resolver settings exposed by the operating system.
    /// </summary>
    public static class SystemInformation {
        private static readonly object DnsConfigurationLock = new();
        private static Lazy<SystemDnsConfiguration> cachedDnsConfiguration = CreateLazyConfiguration();
        private static Func<List<string>>? dnsServerProvider;

        internal static void SetDnsServerProvider(Func<List<string>>? provider) {
            lock (DnsConfigurationLock) {
                dnsServerProvider = provider;
                cachedDnsConfiguration = CreateLazyConfiguration();
            }
        }

        /// <summary>
        /// Gets the DNS resolver configuration exposed by the operating system.
        /// </summary>
        /// <param name="refresh">Set to <c>true</c> to force cache refresh.</param>
        /// <param name="fallback">Optional fallback used only when no system resolver was discovered.</param>
        /// <returns>The discovered resolver configuration.</returns>
        public static SystemDnsConfiguration GetDnsConfiguration(
            bool refresh = false,
            SystemDnsFallback fallback = SystemDnsFallback.None) {
            if (refresh) {
                lock (DnsConfigurationLock) {
                    cachedDnsConfiguration = CreateLazyConfiguration();
                }
            }

            SystemDnsConfiguration discovered = cachedDnsConfiguration.Value;
            if (discovered.HasDnsServers || fallback == SystemDnsFallback.None) {
                return discovered;
            }

            return new SystemDnsConfiguration(
                new[] { "1.1.1.1", "8.8.8.8" },
                discovered.SearchDomains,
                discovered.Ndots,
                SystemDnsDiscoverySource.PublicFallback,
                discovered.Error);
        }

        /// <summary>
        /// Gets system DNS server addresses in operating-system preference order.
        /// </summary>
        /// <param name="refresh">Set to <c>true</c> to force cache refresh.</param>
        /// <param name="fallback">Optional fallback used only when no system resolver was discovered.</param>
        /// <returns>A copy of the DNS server list.</returns>
        public static List<string> GetDnsFromActiveNetworkCard(
            bool refresh = false,
            SystemDnsFallback fallback = SystemDnsFallback.None) {
            return new List<string>(GetDnsConfiguration(refresh, fallback).DnsServers);
        }

        internal static SystemDnsConfiguration ParseResolvConf(
            string path,
            Action<string>? debugPrint = null) {
            var servers = new List<string>();
            var searchDomains = new List<string>();
            int ndots = 1;

            if (!File.Exists(path)) {
                debugPrint?.Invoke($"Skipping {path}; file not found");
                return new SystemDnsConfiguration(servers, searchDomains, ndots, SystemDnsDiscoverySource.None);
            }

            debugPrint?.Invoke($"Reading {path}");
            try {
                foreach (string rawLine in File.ReadAllLines(path)) {
                    string line = StripInlineComment(rawLine).Trim();
                    if (line.Length == 0) {
                        continue;
                    }

                    string[] parts = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length < 2) {
                        continue;
                    }

                    if (parts[0].Equals("nameserver", StringComparison.OrdinalIgnoreCase)) {
                        if (IPAddress.TryParse(parts[1], out IPAddress? address) && IsUsableDnsAddress(address)) {
                            servers.Add(FormatDnsAddress(address));
                        } else {
                            debugPrint?.Invoke($"[resolv.conf] Ignoring invalid nameserver: {parts[1]}");
                        }
                    } else if (parts[0].Equals("search", StringComparison.OrdinalIgnoreCase)) {
                        searchDomains.Clear();
                        searchDomains.AddRange(parts.Skip(1));
                    } else if (parts[0].Equals("domain", StringComparison.OrdinalIgnoreCase) && searchDomains.Count == 0) {
                        searchDomains.Add(parts[1]);
                    } else if (parts[0].Equals("options", StringComparison.OrdinalIgnoreCase)) {
                        foreach (string option in parts.Skip(1)) {
                            if (option.StartsWith("ndots:", StringComparison.OrdinalIgnoreCase)
                                && int.TryParse(option.Substring("ndots:".Length), out int parsedNdots)) {
                                ndots = Math.Max(0, Math.Min(15, parsedNdots));
                            }
                        }
                    }
                }

                return new SystemDnsConfiguration(
                    servers,
                    searchDomains,
                    ndots,
                    servers.Count > 0 || searchDomains.Count > 0
                        ? SystemDnsDiscoverySource.ResolvConf
                        : SystemDnsDiscoverySource.None);
            } catch (Exception ex) {
                debugPrint?.Invoke($"Exception reading {path}: {ex.Message}");
                return new SystemDnsConfiguration(servers, searchDomains, ndots, SystemDnsDiscoverySource.None, ex.Message);
            }
        }

        private static Lazy<SystemDnsConfiguration> CreateLazyConfiguration() {
            return new Lazy<SystemDnsConfiguration>(LoadDnsConfiguration, LazyThreadSafetyMode.ExecutionAndPublication);
        }

        private static SystemDnsConfiguration LoadDnsConfiguration() {
            Func<List<string>>? provider = dnsServerProvider;
            if (provider != null) {
                try {
                    return new SystemDnsConfiguration(
                        provider.Invoke() ?? new List<string>(),
                        Array.Empty<string>(),
                        1,
                        SystemDnsDiscoverySource.CustomProvider);
                } catch (Exception ex) {
                    return new SystemDnsConfiguration(
                        Array.Empty<string>(),
                        Array.Empty<string>(),
                        1,
                        SystemDnsDiscoverySource.CustomProvider,
                        ex.Message);
                }
            }

            bool debug = Environment.GetEnvironmentVariable("DNSCLIENTX_DEBUG_SYSTEMDNS") == "1";
            void DebugPrint(string message) {
                if (debug) {
                    Settings.Logger.WriteDebug($"[DnsClientX:SystemDNS] {message}");
                }
            }

            string? discoveryError = null;
            var candidates = new List<InterfaceDnsCandidate>();
            var searchDomains = new List<string>();
            try {
                foreach (NetworkInterface networkInterface in NetworkInterface.GetAllNetworkInterfaces()) {
                    if (!ShouldInspectDnsInterface(networkInterface.OperationalStatus)) {
                        continue;
                    }

                    IPInterfaceProperties properties = networkInterface.GetIPProperties();
                    int interfaceIndex = GetInterfaceIndex(properties);
                    int metric = WindowsInterfaceMetric.TryGetMetric(interfaceIndex, out int discoveredMetric)
                        ? discoveredMetric
                        : int.MaxValue;
                    bool hasGateway = properties.GatewayAddresses.Any(gateway =>
                        IsUsableDnsAddress(gateway.Address));

                    if (!string.IsNullOrWhiteSpace(properties.DnsSuffix)) {
                        searchDomains.Add(properties.DnsSuffix);
                    }

                    int addressOrder = 0;
                    foreach (IPAddress address in properties.DnsAddresses) {
                        if (!IsUsableDnsAddress(address)) {
                            DebugPrint($"Ignoring unusable DNS address {address} on {networkInterface.Name}");
                            continue;
                        }

                        candidates.Add(new InterfaceDnsCandidate(
                            FormatDnsAddress(address),
                            hasGateway,
                            metric,
                            interfaceIndex,
                            addressOrder++));
                    }
                }
            } catch (Exception ex) {
                discoveryError = ex.Message;
                DebugPrint($"Network-interface discovery failed: {ex.Message}");
            }

            string[] interfaceServers = candidates
                .OrderBy(candidate => candidate.Metric)
                .ThenByDescending(candidate => candidate.HasGateway)
                .ThenBy(candidate => candidate.InterfaceIndex)
                .ThenBy(candidate => candidate.AddressOrder)
                .Select(candidate => candidate.Address)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

            SystemDnsConfiguration resolvConf = ParseResolvConf("/etc/resolv.conf", DebugPrint);
            if (interfaceServers.Length > 0) {
                IEnumerable<string> effectiveSearchDomains = searchDomains.Count > 0
                    ? searchDomains
                    : resolvConf.SearchDomains;
                return new SystemDnsConfiguration(
                    interfaceServers,
                    effectiveSearchDomains,
                    resolvConf.Ndots,
                    SystemDnsDiscoverySource.NetworkInterfaces,
                    discoveryError);
            }

            if (resolvConf.HasDnsServers) {
                return resolvConf;
            }

            return new SystemDnsConfiguration(
                Array.Empty<string>(),
                resolvConf.SearchDomains,
                resolvConf.Ndots,
                SystemDnsDiscoverySource.None,
                discoveryError ?? resolvConf.Error ?? "No DNS servers were exposed by the operating system.");
        }

        private static int GetInterfaceIndex(IPInterfaceProperties properties) {
            try {
                return properties.GetIPv4Properties()?.Index
                    ?? properties.GetIPv6Properties()?.Index
                    ?? int.MaxValue;
            } catch (NetworkInformationException) {
                return int.MaxValue;
            }
        }

        internal static bool ShouldInspectDnsInterface(OperationalStatus operationalStatus) {
            // Local resolvers commonly listen on loopback. Eligibility is based on whether the
            // interface is active, not its type, so configured stub resolvers are not discarded.
            return operationalStatus == OperationalStatus.Up;
        }

        internal static bool IsUsableDnsAddress(IPAddress? address) {
            if (address == null
                || IPAddress.Any.Equals(address)
                || IPAddress.IPv6Any.Equals(address)
                || IPAddress.None.Equals(address)
                || IPAddress.IPv6None.Equals(address)) {
                return false;
            }

            byte[] bytes = address.GetAddressBytes();
            return address.AddressFamily != AddressFamily.InterNetworkV6 || bytes[0] != 0xff;
        }

        internal static string FormatDnsAddress(IPAddress address) {
            // IPAddress.TryParse and socket APIs accept scoped IPv6 literals directly. Brackets are URI syntax
            // and would turn an otherwise valid resolver address into an unresolvable hostname here.
            return address.ToString();
        }

        private static string StripInlineComment(string line) {
            int hash = line.IndexOf('#');
            int semicolon = line.IndexOf(';');
            int comment = hash < 0 ? semicolon : semicolon < 0 ? hash : Math.Min(hash, semicolon);
            return comment < 0 ? line : line.Substring(0, comment);
        }

        private sealed class InterfaceDnsCandidate {
            internal InterfaceDnsCandidate(string address, bool hasGateway, int metric, int interfaceIndex, int addressOrder) {
                Address = address;
                HasGateway = hasGateway;
                Metric = metric;
                InterfaceIndex = interfaceIndex;
                AddressOrder = addressOrder;
            }

            internal string Address { get; }
            internal bool HasGateway { get; }
            internal int Metric { get; }
            internal int InterfaceIndex { get; }
            internal int AddressOrder { get; }
        }

        private static class WindowsInterfaceMetric {
            private const int ErrorSuccess = 0;

            internal static bool TryGetMetric(int interfaceIndex, out int metric) {
                metric = int.MaxValue;
                if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows) || interfaceIndex <= 0) {
                    return false;
                }

                try {
                    var row = new MibIpInterfaceRow {
                        Family = (ushort)AddressFamily.InterNetwork,
                        InterfaceIndex = (uint)interfaceIndex,
                        ZoneIndices = new uint[16]
                    };
                    if (GetIpInterfaceEntry(ref row) == ErrorSuccess) {
                        metric = row.Metric > int.MaxValue ? int.MaxValue : (int)row.Metric;
                        return true;
                    }
                } catch (DllNotFoundException) {
                    return false;
                } catch (EntryPointNotFoundException) {
                    return false;
                }

                return false;
            }

            [DllImport("iphlpapi.dll")]
            private static extern int GetIpInterfaceEntry(ref MibIpInterfaceRow row);

            [StructLayout(LayoutKind.Sequential)]
            private struct MibIpInterfaceRow {
                internal ushort Family;
                internal ulong InterfaceLuid;
                internal uint InterfaceIndex;
                internal uint MaxReassemblySize;
                internal ulong InterfaceIdentifier;
                internal uint MinRouterAdvertisementInterval;
                internal uint MaxRouterAdvertisementInterval;
                internal byte AdvertisingEnabled;
                internal byte ForwardingEnabled;
                internal byte WeakHostSend;
                internal byte WeakHostReceive;
                internal byte UseAutomaticMetric;
                internal byte UseNeighborUnreachabilityDetection;
                internal byte ManagedAddressConfigurationSupported;
                internal byte OtherStatefulConfigurationSupported;
                internal byte AdvertiseDefaultRoute;
                internal int RouterDiscoveryBehavior;
                internal uint DadTransmits;
                internal uint BaseReachableTime;
                internal uint RetransmitTime;
                internal uint PathMtuDiscoveryTimeout;
                internal int LinkLocalAddressBehavior;
                internal uint LinkLocalAddressTimeout;

                [MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)]
                internal uint[] ZoneIndices;

                internal uint SitePrefixLength;
                internal uint Metric;
                internal uint NlMtu;
                internal byte Connected;
                internal byte SupportsWakeUpPatterns;
                internal byte SupportsNeighborDiscovery;
                internal byte SupportsRouterDiscovery;
                internal uint ReachableTime;
                internal byte TransmitOffload;
                internal byte ReceiveOffload;
                internal byte DisableDefaultRoutes;
            }
        }
    }
}
