using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using DnsClientX;
using Xunit;

namespace DnsClientX.Tests;

/// <summary>
/// Tests for helper methods dealing with DNS root trust anchors.
/// </summary>
[Collection("NoParallel")]
public class RootAnchorHelperTests
{
    /// <summary>Bundled anchors expose IANA lifecycle metadata for consumers and validation.</summary>
    [Fact]
    public void BundledTrustAnchorsExposeValidityMetadata()
    {
        DnsSecTrustAnchor current = Assert.Single(DnsSecTrustAnchors.Current, anchor => anchor.KeyTag == 38696);

        Assert.Equal(new System.DateTimeOffset(2024, 7, 18, 0, 0, 0, System.TimeSpan.Zero), current.ValidFrom);
        Assert.Null(current.ValidUntil);
        Assert.False(current.IsValidAt(new System.DateTimeOffset(2024, 7, 17, 23, 59, 59, System.TimeSpan.Zero)));
        Assert.True(current.IsValidAt(new System.DateTimeOffset(2024, 7, 18, 0, 0, 0, System.TimeSpan.Zero)));
    }

    /// <summary>
    /// Parses embedded XML and verifies returned records.
    /// </summary>
    [Fact]
    public void ParseFromXml_ParsesRecords()
    {
        const string xml = """
<TrustAnchor id="test">
  <Zone>.</Zone>
  <KeyDigest validFrom="2024-07-18T00:00:00+00:00">
    <KeyTag>38696</KeyTag>
    <Algorithm>8</Algorithm>
    <DigestType>2</DigestType>
    <Digest>683D2D0ACB8C9B712A1948B27F741219298D0A450D612C483AF444A4C0FB2B16</Digest>
  </KeyDigest>
  <KeyDigest validFrom="2017-02-02T00:00:00+00:00">
    <KeyTag>20326</KeyTag>
    <Algorithm>8</Algorithm>
    <DigestType>2</DigestType>
    <Digest>E06D44B80B8F1D39A95C0B0D7C65D08458E880409BBC683457104237C7F8EC8D</Digest>
  </KeyDigest>
</TrustAnchor>
""";
        RootDsRecord[] records = RootAnchorHelper.ParseFromXml(xml);
        Assert.Equal(2, records.Length);
        Assert.Contains(records, r => r.KeyTag == 38696);
        Assert.Contains(records, r => r.KeyTag == 20326);
    }

    private class ThrowingHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => throw new HttpRequestException("fail");
    }

    /// <summary>
    /// Simulates a download failure and ensures an empty array is returned.
    /// </summary>
    [Fact]
    public async Task FetchLatestAsync_Failure_ReturnsEmptyArrayAndLogsWarning()
    {
        using var client = new HttpClient(new ThrowingHandler());
        HttpClient? originalClient = RootAnchorHelper.ClientOverride;
        RootAnchorHelper.ClientOverride = client;
        LogEventArgs? logged = null;
        EventHandler<LogEventArgs> handler = (_, e) => {
            if (e.Message.IndexOf("root trust anchors", StringComparison.OrdinalIgnoreCase) >= 0) logged = e;
        };
        Settings.Logger.OnWarningMessage += handler;

        try
        {
            RootDsRecord[] records = await RootAnchorHelper.FetchLatestAsync();
            Assert.Empty(records);
            Assert.NotNull(logged);
            Assert.Contains("root trust anchors", logged.Message, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            Settings.Logger.OnWarningMessage -= handler;
            RootAnchorHelper.ClientOverride = originalClient;
        }
    }
}
