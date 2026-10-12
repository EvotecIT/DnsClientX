using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace DnsClientX.Tests;

/// <summary>Exercises CLI JSON automation through real loopback transports and saved workflow artifacts.</summary>
[Collection("NoParallel")]
public sealed class CliWorkflowJsonTests {
    private static Task<int> InvokeAsync(params string[] args) => (Task<int>)Assembly.Load("DnsClientX.Cli").GetType("DnsClientX.Cli.Program")!.GetMethod("Main", BindingFlags.NonPublic | BindingFlags.Static)!.Invoke(null, new object[] { args })!;

    /// <summary>Regression proof: JSON stdout remains one document when explain/trace accompany a direct resolver query.</summary>
    [Theory]
    [InlineData("--explain")]
    [InlineData("--trace")]
    public async Task DirectResolver_QueryJsonIncludesStructuredDiagnostics(string diagnosticSwitch) {
        using var udp = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        int port = ((IPEndPoint)udp.Client.LocalEndPoint!).Port;
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        Task server = Task.Run(async () => {
            Task<UdpReceiveResult> receive = udp.ReceiveAsync();
            Assert.Same(receive, await Task.WhenAny(receive, Task.Delay(5000, timeout.Token)));
            UdpReceiveResult query = await receive;
            byte[] response = TestUtilities.CreateResponseFromQuery(query.Buffer);
            await udp.SendAsync(response, response.Length, query.RemoteEndPoint);
        });
        TextWriter original = Console.Out;
        using var output = new StringWriter();
        try {
            Console.SetOut(output);
            Assert.Equal(0, await InvokeAsync("--resolver", $"udp@127.0.0.1:{port}", "--format", "json", diagnosticSwitch, "example.com"));
            using JsonDocument json = JsonDocument.Parse(output.ToString());
            Assert.Equal("example.com", json.RootElement.GetProperty("Response").GetProperty("Question")[0].GetProperty("name").GetString());
            Assert.Equal("Network", json.RootElement.GetProperty("Explanation").GetProperty("ResponseSource").GetString());
            Assert.NotEqual(0, json.RootElement.GetProperty("Explanation").GetProperty("Attempts").GetArrayLength());
            Assert.True(json.RootElement.TryGetProperty("ConfiguredResolverPort", out var configuredPort));
            Assert.Equal(port, configuredPort.GetInt32());
            await server;
        } finally { Console.SetOut(original); }
    }

    /// <summary>Full probe/benchmark reports are parseable and retain correlated snapshots rather than human prefixes.</summary>
    [Theory]
    [InlineData("--probe")]
    [InlineData("--benchmark")]
    public async Task ResolverReports_JsonContainsFullReport(string mode) {
        var server = new ResolverBenchmarkTcpFixture();
        TextWriter original = Console.Out;
        using var output = new StringWriter();
        try {
            Console.SetOut(output);
            string[] args = mode == "--probe"
                ? new[] { mode, "--resolver", $"tcp@127.0.0.1:{server.Port}", "--format", "json", "example.com" }
                : new[] { mode, "--resolver", $"tcp@127.0.0.1:{server.Port}", "--format", "json", "--benchmark-attempts", "1", "example.com" };
            Assert.Equal(0, await InvokeAsync(args));
            using JsonDocument json = JsonDocument.Parse(output.ToString());
            Assert.Equal(1, json.RootElement.GetProperty("Results").GetArrayLength());
            Assert.True(json.RootElement.GetProperty("Summary").GetProperty("PolicyPassed").GetBoolean());
            Assert.Equal("example.com", json.RootElement.GetProperty("Snapshot").GetProperty("Answers")[0].GetProperty("Name").GetString());
        } finally { Console.SetOut(original); await server.DisposeAsync(); }
    }

    /// <summary>A saved portable profile reaches the real CLI execution path and produces a complete reusable report.</summary>
    [Fact]
    public async Task Profile_ExecutesSavedSettingsAndSavesFullJsonReport() {
        var server = new ResolverBenchmarkTcpFixture();
        string profilePath = Path.GetTempFileName(), reportPath = Path.GetTempFileName();
        TextWriter original = Console.Out;
        using var output = new StringWriter();
        try {
            ResolverHealthProfileStore.Save(profilePath, new ResolverHealthProfile { ResolverEndpoints = new[] { $"tcp@127.0.0.1:{server.Port}" }, Names = new[] { "profile.example" }, AttemptsPerCombination = 1, ProbePolicy = new ResolverProbePolicy { MinSuccessCount = 1 } });
            Console.SetOut(output);
            Assert.Equal(0, await InvokeAsync("--profile", profilePath, "--benchmark", "--format", "json", "--profile-save", reportPath));
            Assert.Equal(output.ToString().Trim(), File.ReadAllText(reportPath).Trim());
            using JsonDocument json = JsonDocument.Parse(output.ToString());
            Assert.True(json.RootElement.GetProperty("PolicyPassed").GetBoolean());
            Assert.Equal("profile.example", json.RootElement.GetProperty("Queries")[0].GetProperty("Summary").GetProperty("Name").GetString());
            Assert.Equal(2, server.Queries);
        } finally { Console.SetOut(original); File.Delete(profilePath); File.Delete(reportPath); await server.DisposeAsync(); }
    }

    /// <summary>Saved snapshots reach the comparison workflow and expose missing evidence without claiming regression.</summary>
    [Fact]
    public async Task SnapshotComparison_JsonPreservesUnknownEvidence() {
        string before = Path.GetTempFileName(), after = Path.GetTempFileName();
        TextWriter original = Console.Out;
        using var output = new StringWriter();
        try {
            ResolverScoreStore.Save(before, new ResolverScoreSnapshot { Summary = new ResolverScoreSummary { PolicyPassed = true }, Results = new[] { new ResolverScoreEntry { Target = "Removed" } } });
            ResolverScoreStore.Save(after, new ResolverScoreSnapshot { Summary = new ResolverScoreSummary { PolicyPassed = false }, Results = new[] { new ResolverScoreEntry { Target = "Added" } } });
            Console.SetOut(output);
            Assert.Equal(0, await InvokeAsync("--resolver-compare", before, after, "--format", "json"));
            using JsonDocument json = JsonDocument.Parse(output.ToString());
            Assert.False(json.RootElement.GetProperty("ScopeComparable").GetBoolean());
            Assert.False(json.RootElement.GetProperty("HealthRegression").GetBoolean());
            Assert.Equal(2, json.RootElement.GetProperty("Candidates").GetArrayLength());
            Assert.NotEqual(0, json.RootElement.GetProperty("IncomparableReasons").GetArrayLength());
        } finally { Console.SetOut(original); File.Delete(before); File.Delete(after); }
    }

    /// <summary>Explicit CLI overrides cannot be silently ignored when settings come from a saved profile.</summary>
    [Theory]
    [InlineData("--benchmark-attempts")]
    [InlineData("--benchmark-timeout")]
    public async Task Profile_RejectsCliRunOverridesBeforeLoadingConfiguration(string option) {
        TextWriter original = Console.Out, originalError = Console.Error;
        using var output = new StringWriter(); using var error = new StringWriter();
        try {
            Console.SetOut(output); Console.SetError(error);
            Assert.Equal(1, await InvokeAsync("--profile", "unused-profile.json", "--benchmark", option, "1"));
            Assert.Contains("profile run settings come from the profile", error.ToString());
        } finally { Console.SetOut(original); Console.SetError(originalError); }
    }
}
