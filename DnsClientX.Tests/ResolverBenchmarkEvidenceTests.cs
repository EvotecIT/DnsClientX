using System;

namespace DnsClientX.Tests;

/// <summary>Protects correlations for independently produced benchmark attempts.</summary>
public sealed class ResolverBenchmarkEvidenceTests {
    /// <summary>Result input order is not fabricated into name/type evidence.</summary>
    [Fact]
    public void Report_UsesCapturedQueryMetadataAndKeepsUnrecordedScopeUnavailable() {
        var attempts = new[] {
            new ResolverQueryAttemptResult { Target = "System", Name = "second.example", RecordType = DnsRecordType.AAAA, Error = "timeout" },
            new ResolverQueryAttemptResult { Target = "System", Name = "first.example", RecordType = DnsRecordType.A, Response = new DnsResponse { Status = DnsResponseCode.NoError } },
            new ResolverQueryAttemptResult { Target = "System", Error = "unattributed attempt" }
        };
        ResolverBenchmarkReport report = ResolverBenchmarkReportBuilder.Build(attempts, new[] { "first.example", "second.example" }, new[] { DnsRecordType.A, DnsRecordType.AAAA }, 1, 1, 2000, new ResolverBenchmarkPolicy());
        Assert.Equal("second.example", report.Attempts[0].Name);
        Assert.Equal(DnsRecordType.AAAA, report.Attempts[0].RecordType);
        Assert.Equal("timeout", report.Attempts[0].Error);
        Assert.Null(report.Attempts[2].RecordType);
        Assert.Equal(2, report.Snapshot.Answers.Length);
    }
}
