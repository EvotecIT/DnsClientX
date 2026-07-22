using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices;
using System.Text;

namespace DnsClientX {
    internal sealed class SystemDnsPolicyDiscoveryResult {
        internal SystemDnsPolicyDiscoveryResult(IEnumerable<SystemDnsPolicyRule>? rules, string? error = null) {
            Rules = (rules ?? Array.Empty<SystemDnsPolicyRule>()).ToArray();
            Error = error;
        }

        internal IReadOnlyList<SystemDnsPolicyRule> Rules { get; }
        internal string? Error { get; }
    }

    internal static class WindowsNrptPolicyReader {
        private const string GroupPolicyPath = @"SOFTWARE\Policies\Microsoft\Windows NT\DNSClient\DnsPolicyConfig";
        private const string LocalPolicyPath = @"SYSTEM\CurrentControlSet\Services\Dnscache\Parameters\DnsPolicyConfig";
        private const int DnsSecOptions = 0x02;
        private const int DirectAccessOptions = 0x04;
        private const int GenericDnsOptions = 0x08;
        private const int IdnOptions = 0x10;
        private const int KnownOptions = DnsSecOptions | DirectAccessOptions | GenericDnsOptions | IdnOptions;
        private const int ErrorSuccess = 0;
        private const int ErrorMoreData = 234;
        private const int ErrorNoMoreItems = 259;
        private const int KeyRead = 0x20019;
        private const int KeyWow64_64Key = 0x0100;
        private const int RegSz = 1;
        private const int RegDword = 4;
        private const int RegMultiSz = 7;
        private static readonly IntPtr HKeyLocalMachine = new(unchecked((int)0x80000002));
        private static readonly string[] KnownValueNames = {
            "Name",
            "ConfigOptions",
            "GenericDNSServers",
            "IDNConfig",
            "DNSSECValidationRequired",
            "DNSSECQueryIPsecRequired",
            "VpnRequired",
            "ProxyName"
        };

        internal static SystemDnsPolicyDiscoveryResult Discover() {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) {
                return new SystemDnsPolicyDiscoveryResult(Array.Empty<SystemDnsPolicyRule>());
            }

            return DiscoverWindows();
        }

        private static SystemDnsPolicyDiscoveryResult DiscoverWindows() {
            try {
                IntPtr groupPolicy = OpenKey(GroupPolicyPath);
                try {
                    string[] groupRuleIds = EnumerateSubKeys(groupPolicy);
                    if (groupRuleIds.Length > 0) {
                        return ReadRules(groupPolicy, groupRuleIds, SystemDnsPolicySource.GroupPolicy);
                    }
                } finally {
                    CloseKey(groupPolicy);
                }

                IntPtr localPolicy = OpenKey(LocalPolicyPath);
                try {
                    return ReadRules(localPolicy, EnumerateSubKeys(localPolicy), SystemDnsPolicySource.Local);
                } finally {
                    CloseKey(localPolicy);
                }
            } catch (Exception ex) when (ex is UnauthorizedAccessException || ex is Win32Exception) {
                return new SystemDnsPolicyDiscoveryResult(Array.Empty<SystemDnsPolicyRule>(), ex.Message);
            }
        }

        internal static SystemDnsPolicyRule ParseRule(
            string id,
            SystemDnsPolicySource source,
            IReadOnlyDictionary<string, object?> values) {
            string[] namespaces = ReadStrings(values, "Name");
            int configOptions = ReadInt32(values, "ConfigOptions");
            string[] configuredNameServers = SplitServers(ReadString(values, "GenericDNSServers"));
            string[] nameServers = (configOptions & GenericDnsOptions) != 0
                ? configuredNameServers
                : Array.Empty<string>();
            bool dnsSecRequired = (configOptions & DnsSecOptions) != 0
                && ReadInt32(values, "DNSSECValidationRequired") != 0;
            var diagnostics = new List<string>();

            if (namespaces.Length == 0) {
                diagnostics.Add("The NRPT rule does not contain a Name namespace value.");
            }
            if (namespaces.Any(IsUnsupportedNamespace)) {
                diagnostics.Add("Subnet and wildcard NRPT namespaces are discovered but are not currently routed by DnsClientX.");
            }
            if ((configOptions & ~KnownOptions) != 0) {
                diagnostics.Add($"The NRPT rule uses unknown ConfigOptions bits 0x{configOptions & ~KnownOptions:X}.");
            }
            if ((configOptions & DirectAccessOptions) != 0) {
                diagnostics.Add("DirectAccess NRPT routing depends on Windows network-location and IPsec state.");
            }
            if (ReadInt32(values, "DNSSECQueryIPsecRequired") != 0) {
                diagnostics.Add("DNSSEC-over-IPsec requirements cannot be enforced by the managed DNS transport.");
            }
            if (ReadInt32(values, "VpnRequired") != 0 || !string.IsNullOrWhiteSpace(ReadString(values, "ProxyName"))) {
                diagnostics.Add("Auto-trigger VPN/proxy NRPT behavior must be handled by the Windows DNS client.");
            }
            if ((configOptions & GenericDnsOptions) != 0 && nameServers.Length == 0) {
                diagnostics.Add("Generic DNS routing is enabled but no valid DNS server was configured.");
            }
            if ((configOptions & IdnOptions) != 0) {
                if (!values.ContainsKey("IDNConfig")) {
                    diagnostics.Add("IDN routing is enabled but the required IDNConfig value is missing.");
                } else {
                    int idnConfig = ReadInt32(values, "IDNConfig");
                    if (idnConfig == 0 || idnConfig == 1) {
                        diagnostics.Add(
                            $"IDNConfig mode {idnConfig} requires Windows UTF-8 name handling that the managed Punycode DNS transport cannot reproduce.");
                    } else if (idnConfig != 2) {
                        diagnostics.Add($"IDNConfig mode {idnConfig} is not defined by the Windows NRPT protocol.");
                    }
                }
            }

            return new SystemDnsPolicyRule(
                id,
                source,
                namespaces,
                nameServers,
                dnsSecRequired,
                diagnostics.Count == 0,
                diagnostics.Count == 0 ? null : string.Join(" ", diagnostics));
        }

        private static SystemDnsPolicyDiscoveryResult ReadRules(
            IntPtr root,
            IEnumerable<string> ruleIds,
            SystemDnsPolicySource source) {
            var rules = new List<SystemDnsPolicyRule>();
            var errors = new List<string>();
            foreach (string ruleId in ruleIds.OrderBy(value => value, StringComparer.OrdinalIgnoreCase)) {
                try {
                    IntPtr ruleKey = OpenKey(root, ruleId);
                    try {
                        var values = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
                        foreach (string valueName in KnownValueNames) {
                            object? value = ReadValue(ruleKey, valueName);
                            if (value != null) values[valueName] = value;
                        }
                        rules.Add(ParseRule(ruleId, source, values));
                    } finally {
                        CloseKey(ruleKey);
                    }
                } catch (Exception ex) when (ex is UnauthorizedAccessException || ex is Win32Exception) {
                    errors.Add($"{ruleId}: {ex.Message}");
                }
            }
            return new SystemDnsPolicyDiscoveryResult(rules, errors.Count == 0 ? null : string.Join("; ", errors));
        }

        private static IntPtr OpenKey(string path) {
            int status = RegOpenKeyEx(HKeyLocalMachine, path, 0, KeyRead | KeyWow64_64Key, out IntPtr key);
            if (status != ErrorSuccess) {
                status = RegOpenKeyEx(HKeyLocalMachine, path, 0, KeyRead, out key);
            }
            if (status == 2) return IntPtr.Zero;
            if (status != ErrorSuccess) throw new Win32Exception(status);
            return key;
        }

        private static IntPtr OpenKey(IntPtr root, string path) {
            if (root == IntPtr.Zero) return IntPtr.Zero;
            int status = RegOpenKeyEx(root, path, 0, KeyRead, out IntPtr key);
            if (status == 2) return IntPtr.Zero;
            if (status != ErrorSuccess) throw new Win32Exception(status);
            return key;
        }

        private static string[] EnumerateSubKeys(IntPtr key) {
            if (key == IntPtr.Zero) return Array.Empty<string>();
            var names = new List<string>();
            for (uint index = 0; ; index++) {
                int capacity = 256;
                while (true) {
                    var builder = new StringBuilder(capacity);
                    uint length = (uint)capacity;
                    int status = RegEnumKeyEx(key, index, builder, ref length, IntPtr.Zero, null, IntPtr.Zero, IntPtr.Zero);
                    if (status == ErrorNoMoreItems) return names.ToArray();
                    if (status == ErrorMoreData) {
                        capacity *= 2;
                        continue;
                    }
                    if (status != ErrorSuccess) throw new Win32Exception(status);
                    names.Add(builder.ToString());
                    break;
                }
            }
        }

        private static object? ReadValue(IntPtr key, string valueName) {
            if (key == IntPtr.Zero) return null;
            uint size = 0;
            int status = RegQueryValueEx(key, valueName, IntPtr.Zero, out uint type, null, ref size);
            if (status == 2) return null;
            if (status != ErrorSuccess && status != ErrorMoreData) throw new Win32Exception(status);
            if (size == 0) return type == RegMultiSz ? Array.Empty<string>() : string.Empty;

            byte[] bytes = new byte[size];
            status = RegQueryValueEx(key, valueName, IntPtr.Zero, out type, bytes, ref size);
            if (status != ErrorSuccess) throw new Win32Exception(status);
            if (type == RegDword && bytes.Length >= 4) return BitConverter.ToInt32(bytes, 0);
            if (type != RegSz && type != RegMultiSz) return null;

            string text = Encoding.Unicode.GetString(bytes, 0, (int)size).TrimEnd('\0');
            return type == RegMultiSz
                ? text.Split(new[] { '\0' }, StringSplitOptions.RemoveEmptyEntries)
                : (object)text;
        }

        private static void CloseKey(IntPtr key) {
            if (key != IntPtr.Zero) RegCloseKey(key);
        }

        private static string[] ReadStrings(IReadOnlyDictionary<string, object?> values, string key) {
            if (!values.TryGetValue(key, out object? value) || value == null) return Array.Empty<string>();
            if (value is string[] strings) return strings.Where(item => !string.IsNullOrWhiteSpace(item)).ToArray();
            if (value is string text && !string.IsNullOrWhiteSpace(text)) return new[] { text };
            return Array.Empty<string>();
        }

        private static string? ReadString(IReadOnlyDictionary<string, object?> values, string key) {
            return values.TryGetValue(key, out object? value) ? value as string : null;
        }

        private static int ReadInt32(IReadOnlyDictionary<string, object?> values, string key) {
            if (!values.TryGetValue(key, out object? value) || value == null) return 0;
            try {
                return Convert.ToInt32(value, System.Globalization.CultureInfo.InvariantCulture);
            } catch (Exception ex) when (ex is FormatException || ex is InvalidCastException || ex is OverflowException) {
                return 0;
            }
        }

        private static string[] SplitServers(string? value) {
            if (string.IsNullOrWhiteSpace(value)) return Array.Empty<string>();
            return value!.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(server => server.Trim())
                .Where(IsValidServer)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        private static bool IsValidServer(string server) {
            if (IPAddress.TryParse(server, out _)) return true;
            return Uri.CheckHostName(server) == UriHostNameType.Dns;
        }

        private static bool IsUnsupportedNamespace(string value) {
            string candidate = value.Trim();
            return candidate.IndexOf('*') >= 0
                || candidate.IndexOf('/') >= 0
                || candidate.IndexOf('\\') >= 0;
        }

        [DllImport("advapi32.dll", CharSet = CharSet.Unicode, EntryPoint = "RegOpenKeyExW")]
        private static extern int RegOpenKeyEx(IntPtr key, string subKey, int options, int desiredAccess, out IntPtr result);

        [DllImport("advapi32.dll", CharSet = CharSet.Unicode, EntryPoint = "RegEnumKeyExW")]
        private static extern int RegEnumKeyEx(IntPtr key, uint index, StringBuilder name, ref uint nameLength,
            IntPtr reserved, StringBuilder? className, IntPtr classLength, IntPtr lastWriteTime);

        [DllImport("advapi32.dll", CharSet = CharSet.Unicode, EntryPoint = "RegQueryValueExW")]
        private static extern int RegQueryValueEx(IntPtr key, string valueName, IntPtr reserved,
            out uint type, byte[]? data, ref uint dataSize);

        [DllImport("advapi32.dll")]
        private static extern int RegCloseKey(IntPtr key);
    }
}
