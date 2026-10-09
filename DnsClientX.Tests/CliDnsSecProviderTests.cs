using System;
using System.Reflection;
using DnsClientX.DnsSec.EdDsa;

namespace DnsClientX.Tests;

/// <summary>Protects explicit provider selection across CLI query and workflow modes.</summary>
[Collection("NoParallel")]
public class CliDnsSecProviderTests {
    private static Type Program => Assembly.Load("DnsClientX.Cli").GetType("DnsClientX.Cli.Program")!;

    /// <summary>Both option owners carry a single selected provider through to the actual client.</summary>
    [Theory]
    [InlineData("")]
    [InlineData("--probe")]
    [InlineData("--benchmark")]
    public void ProviderReachesQueryAndWorkflowClients(string mode) {
        string path = typeof(EdDsaDnsSecSignatureVerifier).Assembly.Location;
        string[] arguments = mode.Length == 0 ? new[] { "example.com", "--dnssec-verifier", path }
            : new[] { "example.com", mode, "--dnssec-verifier", path };
        object?[] parsed = { arguments, null, null, null };
        Assert.True((bool)Program.GetMethod("TryParseArgs", BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, parsed)!);
        var clientOptions = (ResolverExecutionClientOptions)Program.GetMethod("CreateExecutionClientOptions", BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, new[] { parsed[1], false })!;
        var runOptions = (ResolverQueryRunOptions)Program.GetMethod("CreateQueryRunOptions", BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, new[] { parsed[1] })!;
        Assert.IsType<EdDsaDnsSecSignatureVerifier>(clientOptions.DnsSecSignatureVerifier);
        Assert.Same(clientOptions.DnsSecSignatureVerifier, runOptions.DnsSecSignatureVerifier);
        var target = new ResolverExecutionTarget { BuiltInEndpoint = DnsEndpoint.Cloudflare, DisplayName = "Cloudflare" };
        using var client = ResolverQueryExecutor.CreateClient(target, runOptions);
        Assert.Same(clientOptions.DnsSecSignatureVerifier, client.EndpointConfiguration.DnsSecSignatureVerifier);
    }

    /// <summary>Utilities that do not validate signatures reject the option rather than silently ignore it.</summary>
    [Theory]
    [InlineData("--capabilities")]
    [InlineData("--stamp-info", "sdns://test")]
    [InlineData("--resolver-select", "selection.json")]
    [InlineData("--axfr", "example.com")]
    public void ProviderRejectedForOtherModes(params string[] mode) {
        var arguments = new string[mode.Length + 2];
        mode.CopyTo(arguments, 0);
        arguments[mode.Length] = "--dnssec-verifier";
        arguments[mode.Length + 1] = "provider.dll";
        object?[] parsed = { arguments, null, null, null };
        Assert.False((bool)Program.GetMethod("TryParseArgs", BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, parsed)!);
        Assert.Contains("--dnssec-verifier applies only", (string)parsed[2]!);
    }

    /// <summary>An explicitly requested provider cannot silently become the default verifier.</summary>
    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("\t")]
    public void BlankProviderPathIsRejected(string path) {
        object?[] parsed = { new[] { "example.com", "--dnssec-verifier", path }, null, null, null };
        Assert.False((bool)Program.GetMethod("TryParseArgs", BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, parsed)!);
        Assert.Contains("--dnssec-verifier", (string)parsed[2]!);
    }
}
