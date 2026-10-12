using System;
using System.Text.Json;

namespace DnsClientX.Tests;

/// <summary>Protects evidence provenance and JSON-safe diagnostics without additional network work.</summary>
public sealed class DnsQueryExplanationTests {
    /// <summary>Hidden wire aliases and actual DNSSEC material survive a projected response explanation.</summary>
    [Fact]
    public void Explanation_UsesOriginalAliasesAndRecordedDependencies() {
        var response = new DnsResponse {
            Status = DnsResponseCode.NoError, ResponseSource = DnsResponseSource.Cache, DnsSecValidationStatus = DnsSecValidationStatus.Secure,
            Answers = new[] { new DnsAnswer { Name = "target.example", Type = DnsRecordType.A, DataRaw = "192.0.2.1" } },
            WireAnswers = new[] { new DnsWireResourceRecord("alias.example", DnsRecordType.CNAME, 1, 60, 60, 0, 0, "target.example") },
            DnsSecValidationDependencies = new[] { new DnsSecValidationDependency("example", DnsRecordType.DNSKEY, new DnsResponse { Status = DnsResponseCode.NoError }, null) }
        };
        var audit = new AuditEntry("alias.example", DnsRecordType.A) { Exception = new InvalidOperationException("transport failed"), RequestFormat = DnsRequestFormat.DnsOverTCP };
        DnsQueryExplanation explanation = DnsQueryExplanation.Create(response, new[] { audit });
        Assert.Single(explanation.Aliases);
        Assert.Equal("alias.example", explanation.Aliases[0].Name);
        Assert.Single(explanation.DnsSecValidationDependencies);
        Assert.False(explanation.ResolverAuthenticData);
        Assert.Equal(DnsSecValidationStatus.Secure, explanation.DnsSecValidationStatus);
        Assert.Equal(DnsResponseSource.Cache, explanation.ResponseSource);
        Assert.NotEmpty(explanation.UnavailableEvidence);
        using JsonDocument json = JsonDocument.Parse(DnsClientXJsonSerializer.Serialize(explanation));
        Assert.Equal("transport failed", json.RootElement.GetProperty("Attempts")[0].GetProperty("Error").GetString());
    }
}
