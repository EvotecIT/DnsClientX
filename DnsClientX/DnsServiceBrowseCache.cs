using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;

namespace DnsClientX;
/// <summary>Owns RFC 6762 record lifetimes and RFC 6763 instance projections for a single browse session.</summary>
internal sealed class DnsServiceBrowseCache {
    internal sealed class Entry {
        internal DnsAnswer Record;
        internal double ReceivedAt;
        internal double ExpiresAt;
        internal bool Goodbye;
        internal int RefreshIndex;
        internal double RefreshJitter;
    }

    private sealed class ServiceState {
        internal DnsService Service = null!;
        internal IPAddress[] Addresses = null!;
        internal string PtrKey = string.Empty;
        internal string SrvKey = string.Empty;
        internal string Fingerprint = string.Empty;
    }

    private readonly string _serviceType;
    private readonly int _maximumRecords;
    private readonly int _maximumServices;
    private readonly int? _interfaceIndex;
    private readonly Random _random = new();
    private readonly Dictionary<string, Entry> _records = new(StringComparer.Ordinal);
    private Dictionary<string, ServiceState> _services = new(StringComparer.Ordinal);
    private readonly Dictionary<string, bool> _removed = new(StringComparer.Ordinal);
    internal IEnumerable<Entry> Entries => _records.Values;

    internal DnsServiceBrowseCache(string serviceType, int maximumRecords, int maximumServices, int? interfaceIndex = null) {
        _serviceType = Canonical(serviceType);
        _maximumRecords = maximumRecords;
        _maximumServices = maximumServices;
        _interfaceIndex = interfaceIndex;
    }

    internal void Apply(DnsResponse response, double now) {
        DnsAnswer[] incoming = (response.Answers ?? Array.Empty<DnsAnswer>()).Concat(response.Additional ?? Array.Empty<DnsAnswer>()).Where(record => record.Class == 1 && record.Type is DnsRecordType.PTR or DnsRecordType.SRV or DnsRecordType.TXT or DnsRecordType.A or DnsRecordType.AAAA).ToArray();
        var instances = new HashSet<string>(_records.Values.Select(entry => entry.Record).Concat(incoming).Where(record => record.Type == DnsRecordType.PTR && Canonical(record.Name) == _serviceType).Select(record => Canonical(record.Data)), StringComparer.Ordinal);
        var targets = new HashSet<string>(_records.Values.Select(entry => entry.Record).Concat(incoming).Where(record => record.Type == DnsRecordType.SRV && instances.Contains(Canonical(record.Name))).Select(record => Srv(record)?.Target).Where(target => target != null).Select(target => Canonical(target!)), StringComparer.Ordinal);
        foreach (DnsAnswer record in incoming.Where(record => Relevant(record, instances, targets))) {
            string key = Key(record);
            if (record.TTL <= 0) {
                if (_records.TryGetValue(key, out Entry? goodbye)) {
                    goodbye.ExpiresAt = Math.Min(goodbye.ExpiresAt, now + 1);
                    goodbye.Goodbye = true;
                }

                continue;
            }

            if (record.CacheFlush == true) {
                foreach (Entry older in _records.Values.Where(entry => entry.Record.Type == record.Type && Canonical(entry.Record.Name) == Canonical(record.Name) && entry.Record.Class == record.Class && now - entry.ReceivedAt > 1)) {
                    older.ExpiresAt = Math.Min(older.ExpiresAt, now + 1);
                }
            }

            if (!_records.ContainsKey(key) && _records.Count >= _maximumRecords) {
                throw new DnsClientException("Continuous DNS-SD record cache exceeded MaximumRecords.");
            }

            _records[key] = new Entry {
                Record = record,
                ReceivedAt = now,
                ExpiresAt = now + record.TTL,
                RefreshJitter = _random.NextDouble() * 0.02
            };
        }
    }

    internal DnsServiceBrowseEvent[] Advance(double now, DateTimeOffset observedAt) {
        _removed.Clear();
        foreach (var pair in _records.Where(pair => pair.Value.ExpiresAt <= now).ToArray()) {
            _removed[pair.Key] = pair.Value.Goodbye;
            _records.Remove(pair.Key);
        }

        var current = new Dictionary<string, ServiceState>(StringComparer.Ordinal);
        foreach (var ptr in _records.Where(pair => pair.Value.Record.Type == DnsRecordType.PTR && Canonical(pair.Value.Record.Name) == _serviceType)) {
            string instance = Canonical(ptr.Value.Record.Data);
            Dictionary<string, string>? metadata = Metadata(instance);
            foreach (var srv in _records.Where(pair => pair.Value.Record.Type == DnsRecordType.SRV && Canonical(pair.Value.Record.Name) == instance).OrderByDescending(pair => pair.Value.ReceivedAt).ThenBy(pair => pair.Key, StringComparer.Ordinal).Take(1)) {
                SrvRecord? endpoint = Srv(srv.Value.Record);
                if (endpoint == null || endpoint.Target == ".") {
                    continue;
                }

                string key = instance;
                IPAddress[] addresses = _records.Values.Where(entry => Canonical(entry.Record.Name) == Canonical(endpoint.Target)
                    && entry.Record.Type is DnsRecordType.A or DnsRecordType.AAAA)
                    .Select(entry => ReadAddress(entry.Record)).Where(address => address != null).Cast<IPAddress>()
                    .Distinct().OrderBy(address => address.ToString(), StringComparer.Ordinal).ToArray();
                var service = new DnsService {
                    ServiceName = ClientX.GetPtrTarget(ptr.Value.Record),
                    ServiceType = DnsWireNameCodec.TrimTrailingRootDot(_serviceType),
                    Target = endpoint.Target,
                    Port = endpoint.Port,
                    Priority = endpoint.Priority,
                    Weight = endpoint.Weight,
                    Metadata = metadata
                };
                if (current.Count >= _maximumServices) {
                    throw new DnsClientException("Continuous DNS-SD live instances exceeded MaximumServices.");
                }

                current[key] = new ServiceState {
                    Service = service,
                    Addresses = addresses,
                    PtrKey = ptr.Key,
                    SrvKey = srv.Key,
                    Fingerprint = Fingerprint(service, addresses)
                };
            }
        }

        var events = new List<DnsServiceBrowseEvent>();
        foreach (var previous in _services.Where(pair => !current.ContainsKey(pair.Key))) {
            bool goodbye = _removed.TryGetValue(previous.Value.PtrKey, out bool ptrGoodbye) && ptrGoodbye || _removed.TryGetValue(previous.Value.SrvKey, out bool srvGoodbye) && srvGoodbye;
            events.Add(Event(goodbye ? DnsServiceBrowseChange.Removed : DnsServiceBrowseChange.Expired, previous.Value, observedAt));
        }

        foreach (var pair in current) {
            if (!_services.TryGetValue(pair.Key, out ServiceState? previous)) {
                events.Add(Event(DnsServiceBrowseChange.Added, pair.Value, observedAt));
            }
            else if (previous.Fingerprint != pair.Value.Fingerprint) {
                events.Add(Event(DnsServiceBrowseChange.Updated, pair.Value, observedAt));
            }
        }

        _services = current;
        return events.ToArray();
    }

    internal IEnumerable<(string Name, DnsRecordType Type)> Questions(double now) {
        yield return (_serviceType, DnsRecordType.PTR);
        foreach (string instance in _records.Values.Where(entry => entry.Record.Type == DnsRecordType.PTR && Canonical(entry.Record.Name) == _serviceType).Select(entry => Canonical(entry.Record.Data)).Distinct()) {
            foreach (DnsRecordType type in new[] {
                DnsRecordType.SRV,
                DnsRecordType.TXT
            }

            ) {
                if (!Has(instance, type)) {
                    yield return (instance, type);
                }
            }
        }

        foreach (string target in _services.Values.Select(state => Canonical(state.Service.Target)).Distinct()) {
            foreach (DnsRecordType type in new[] {
                DnsRecordType.A,
                DnsRecordType.AAAA
            }

            ) {
                if (!Has(target, type)) {
                    yield return (target, type);
                }
            }
        }
    }

    internal IEnumerable<(string Name, DnsRecordType Type)> RefreshQuestions(double now) {
        foreach (Entry entry in _records.Values) {
            if (entry.Goodbye || entry.RefreshIndex >= 4) {
                continue;
            }

            double due = entry.ReceivedAt + entry.Record.TTL * (0.80 + entry.RefreshIndex * 0.05 + entry.RefreshJitter);
            if (due > now) {
                continue;
            }

            entry.RefreshIndex++;
            yield return (entry.Record.Name, entry.Record.Type);
        }
    }

    internal DnsAnswer[] KnownAnswers(string name, DnsRecordType type, double now) => _records.Values.Where(entry => Canonical(entry.Record.Name) == Canonical(name) && entry.Record.Type == type && !entry.Goodbye && entry.ExpiresAt - now >= entry.Record.TTL / 2.0).Select(entry => {
        DnsAnswer record = entry.Record;
        record.TTL = (int)Math.Floor(entry.ExpiresAt - now);
        return record;
    }).ToArray();
    private bool Has(string name, DnsRecordType type) => _records.Values.Any(entry => Canonical(entry.Record.Name) == name && entry.Record.Type == type);
    private IPAddress? ReadAddress(DnsAnswer record) {
        if (!IPAddress.TryParse(record.Data, out IPAddress? address)) {
            return null;
        }
        if (_interfaceIndex.HasValue && address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6 && address.IsIPv6LinkLocal) {
            address.ScopeId = _interfaceIndex.Value;
        }
        return address;
    }
    private bool Relevant(DnsAnswer record, HashSet<string> instances, HashSet<string> targets) => record.Type switch {
        DnsRecordType.PTR => Canonical(record.Name) == _serviceType,
        DnsRecordType.SRV or DnsRecordType.TXT => instances.Contains(Canonical(record.Name)),
        DnsRecordType.A or DnsRecordType.AAAA => targets.Contains(Canonical(record.Name)),
        _ => false
    };
    private Dictionary<string, string>? Metadata(string instance) {
        var metadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (Entry entry in _records.Values.Where(entry => Canonical(entry.Record.Name) == instance && entry.Record.Type == DnsRecordType.TXT).OrderBy(entry => entry.ReceivedAt)) {
            foreach (string text in entry.Record.DataStringsEscaped) {
                int equal = text.IndexOf('=');
                string key = equal < 0 ? text : text.Substring(0, equal);
                if (key.Length > 0) {
                    metadata[key] = equal < 0 ? string.Empty : text.Substring(equal + 1);
                }
            }
        }

        return metadata.Count == 0 ? null : metadata;
    }

    private static DnsServiceBrowseEvent Event(DnsServiceBrowseChange change, ServiceState state, DateTimeOffset now) => new(change, new DnsService { ServiceName = state.Service.ServiceName, ServiceType = state.Service.ServiceType, Target = state.Service.Target, Port = state.Service.Port, Priority = state.Service.Priority, Weight = state.Service.Weight, Metadata = state.Service.Metadata == null ? null : new Dictionary<string, string>(state.Service.Metadata, StringComparer.OrdinalIgnoreCase) }, state.Addresses.Select(address => address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6 ? new IPAddress(address.GetAddressBytes(), address.ScopeId) : new IPAddress(address.GetAddressBytes())).ToArray(), now);
    private static string Fingerprint(DnsService service, IPAddress[] addresses) => service.Target + "|" + service.Port + "|" + service.Priority + "|" + service.Weight + "|" + string.Join(",", addresses.Select(address => address.ToString())) + "|" + string.Join("|", (service.Metadata ?? new Dictionary<string, string>()).OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase).Select(pair => pair.Key.Length + ":" + pair.Key.ToLowerInvariant() + pair.Value.Length + ":" + pair.Value));
    private static string Key(DnsAnswer record) => Canonical(record.Name) + "|" + (int)record.Type + "|" + record.Class + "|" + record.Data;
    private static string Canonical(string name) => DnsWireNameCodec.Canonical(name);
    private static SrvRecord? Srv(DnsAnswer answer) => answer.TypedRecord as SrvRecord;
}
