using DnsClientX;
using System.Net.Quic;
using System.Runtime.InteropServices;
using System.Text.Json;
#if NET8_0
[assembly: System.Runtime.Versioning.RequiresPreviewFeatures]
#endif

var rows = new List<object>();
int failures = 0;
bool nrptOnly = args.Contains("--nrpt");
int deadlineMilliseconds = 45000;
string? deadlineOption = args.FirstOrDefault(a => a.StartsWith("--deadline-ms=", StringComparison.Ordinal));
if (deadlineOption != null && (!int.TryParse(deadlineOption[14..], out deadlineMilliseconds) || deadlineMilliseconds < 1)) {
    Console.Error.WriteLine("--deadline-ms requires a positive integer.");
    return 2;
}
bool quicSupported = (OperatingSystem.IsWindows() || OperatingSystem.IsLinux() || OperatingSystem.IsMacOS()) && QuicConnection.IsSupported;
rows.Add(new { Platform = RuntimeInformation.OSDescription, Runtime = RuntimeInformation.FrameworkDescription, QuicSupported = quicSupported });
if (!nrptOnly) {
    await Capture("native-default", async () => {
        using var client = new ClientX(timeOutMilliseconds: 5000);
        var configuration = client.EndpointConfiguration;
        using var guard = new CancellationTokenSource(TimeSpan.FromMilliseconds(deadlineMilliseconds));
        var result = await client.Resolve("example.com.", retryOnTransient: false, cancellationToken: guard.Token);
        bool passed = result.Status == DnsResponseCode.NoError && result.Answers.Any(a => a.Type == DnsRecordType.A);
        if (!passed) failures++;
        rows.Add(new { Case = "native-default", configuration.BuiltInEndpoint, configuration.Hostname, Configuration = configuration.SystemDnsConfiguration, Status = result.Status.ToString(), Answers = result.Answers.Count(a => a.Type == DnsRecordType.A), result.Error, ExpectedOutcomeObserved = passed });
    });
    foreach (var endpoint in new[] { DnsEndpoint.CloudflareWireFormat, DnsEndpoint.GoogleWireFormat }) {
        foreach (var name in new[] { "cloudflare.com.", "dnssec-failed.org." }) {
            await Capture($"live-dnssec:{endpoint}:{name}", async () => {
                using var client = new ClientX(endpoint, timeOutMilliseconds: 5000);
                client.EndpointConfiguration.CheckingDisabled = true;
                using var guard = new CancellationTokenSource(TimeSpan.FromMilliseconds(deadlineMilliseconds));
                var result = await client.Resolve(name, requestDnsSec: true, validateDnsSec: true, retryOnTransient: false, cancellationToken: guard.Token);
                bool passed = name == "cloudflare.com." ? result.DnsSecValidationStatus == DnsSecValidationStatus.Secure : result.DnsSecValidationStatus == DnsSecValidationStatus.Bogus;
                if (!passed) failures++;
                rows.Add(new { Case = "live-dnssec", Endpoint = endpoint.ToString(), Name = name, Status = result.Status.ToString(), Validation = result.DnsSecValidationStatus.ToString(), Answers = result.Answers.Length, result.Error, ExpectedOutcomeObserved = passed });
            });
        }
    }
    foreach (var format in new[] { DnsRequestFormat.DnsOverQuic, DnsRequestFormat.DnsOverHttp3 }) {
        await Capture($"live-transport:{format}", async () => {
            using var client = format == DnsRequestFormat.DnsOverQuic
                ? new ClientX("dns.adguard-dns.com", format, timeOutMilliseconds: 5000)
                : new ClientX(new Uri("https://cloudflare-dns.com/dns-query"), format, timeOutMilliseconds: 5000);
            using var guard = new CancellationTokenSource(TimeSpan.FromMilliseconds(deadlineMilliseconds));
            var result = await client.Resolve("example.com.", retryOnTransient: false, cancellationToken: guard.Token);
            bool passed = result.Status == DnsResponseCode.NoError && result.Answers.Any(a => a.Type == DnsRecordType.A);
            if (!passed) failures++;
            rows.Add(new { Case = "live-transport", Transport = format.ToString(), Status = result.Status.ToString(), Answers = result.Answers.Length, result.Error, Supported = quicSupported, ExpectedOutcomeObserved = passed });
        });
    }
} else {
    await Capture("native-nrpt-route", async () => {
        using var client = new ClientX(timeOutMilliseconds: 3000);
        var policy = client.EndpointConfiguration.SystemDnsConfiguration!.MatchPolicy("host.qualification.invalid.");
        using var guard = new CancellationTokenSource(TimeSpan.FromMilliseconds(deadlineMilliseconds));
        var result = await client.Resolve("host.qualification.invalid.", retryOnTransient: false, cancellationToken: guard.Token);
        bool passed = policy?.NameServers.SequenceEqual(new[] { "127.0.0.1" }) == true
            && policy.CanApply && result.Status == DnsResponseCode.NoError
            && result.Answers.Any(a => a.Type == DnsRecordType.A && a.Data == "192.0.2.99");
        if (!passed) failures++;
        rows.Add(new { Case = "native-nrpt-route", Policy = policy, Status = result.Status.ToString(), Answers = result.Answers.Select(a => a.Data), result.Error, ExpectedOutcomeObserved = passed });
    });
}
Console.WriteLine(JsonSerializer.Serialize(rows, new JsonSerializerOptions { WriteIndented = true }));
return failures == 0 ? 0 : 1;

async Task Capture(string name, Func<Task> probe) {
    try {
        await probe();
    } catch (Exception exception) {
        failures++;
        rows.Add(new { Case = name, Error = exception.ToString(), ExpectedOutcomeObserved = false });
    }
}
