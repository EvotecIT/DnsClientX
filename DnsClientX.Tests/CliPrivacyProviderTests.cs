using System;
using System.IO;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using DnsClientX.DnsCrypt;
using Xunit;

namespace DnsClientX.Tests;

/// <summary>Real command execution through an explicitly loaded DNSCrypt provider and controlled encrypted socket peer.</summary>
[Collection("NoParallel")]
public sealed class CliPrivacyProviderTests {
    /// <summary>The command loads its provider, authenticates a DNS reply and retains the normal JSON explanation contract.</summary>
    [Fact]
    public async Task PrivacyQueryUsesLoadedProviderAndCommonJsonExplanation() {
        using var server = new DnsCryptTestServer();
        using var output = new StringWriter();
        TextWriter original = Console.Out;
        try {
            Console.SetOut(output);
            int exit = await InvokeCli("--privacy-provider", typeof(DnsCryptClient).Assembly.Location,
                "--privacy-resolver", ResolverStamp(server), "--format", "json", "--explain", "www.example.com");
            Assert.Equal(0, exit);
            using JsonDocument json = JsonDocument.Parse(output.ToString());
            Assert.Equal("93.184.216.34", json.RootElement.GetProperty("Response").GetProperty("Answer")[0].GetProperty("data_normalized").GetString());
            Assert.Equal("DnsCrypt", json.RootElement.GetProperty("RequestFormat").GetString());
            Assert.Equal(1, server.ApplicationQueries);
            Assert.Equal(1, server.CertificateQueries);
        } finally { Console.SetOut(original); }
    }

    /// <summary>Conflicting resolver sources fail before loading any provider DLL or sending a query.</summary>
    [Theory]
    [InlineData("--endpoint", "Google")]
    [InlineData("--resolver", "udp@127.0.0.1:53")]
    [InlineData("--provider-transport", "DnsOverTLS")]
    public async Task PrivacyQueryRejectsConflictingSourcesBeforeLoading(string option, string value) {
        using var errors = new StringWriter();
        using var output = new StringWriter();
        TextWriter originalError = Console.Error, originalOutput = Console.Out;
        try {
            Console.SetError(errors); Console.SetOut(output);
            Assert.Equal(1, await InvokeCli("--privacy-provider", "missing.dll", "--privacy-resolver", "sdns://AQ", option, value, "example.com"));
            Assert.Contains("exclusive query source", errors.ToString());
            Assert.DoesNotContain("Could not load", errors.ToString());
        } finally { Console.SetError(originalError); Console.SetOut(originalOutput); }
    }

    private static Task<int> InvokeCli(params string[] args) => (Task<int>)Assembly.Load("DnsClientX.Cli")
        .GetType("DnsClientX.Cli.Program")!.GetMethod("Main", BindingFlags.NonPublic | BindingFlags.Static)!.Invoke(null, new object[] { args })!;

    private static string ResolverStamp(DnsCryptTestServer server) {
        using var stream = new MemoryStream();
        stream.WriteByte(1);
        stream.Write(new byte[8], 0, 8);
        WriteField(stream, Encoding.UTF8.GetBytes(server.Endpoint.ToString()));
        WriteField(stream, DnsCryptVectorsTests.ProviderKey);
        WriteField(stream, Encoding.UTF8.GetBytes("2.dnscrypt-cert.example.com"));
        return "sdns://" + Convert.ToBase64String(stream.ToArray()).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    private static void WriteField(Stream stream, byte[] bytes) {
        stream.WriteByte((byte)bytes.Length); stream.Write(bytes, 0, bytes.Length);
    }
}
