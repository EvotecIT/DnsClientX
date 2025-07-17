using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using DnsClientX;
using Xunit;

namespace DnsClientX.Tests;

/// <summary>
/// Tests for helper methods dealing with DNS root trust anchors.
/// </summary>
public class RootAnchorHelperTests
{
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
        RootAnchorHelper.ClientOverride = client;
        LogEventArgs? logged = null;
        EventHandler<LogEventArgs> handler = (_, e) => logged = e;
        Settings.Logger.OnWarningMessage += handler;

        try
        {
            RootDsRecord[] records = await RootAnchorHelper.FetchLatestAsync();
            Assert.Empty(records);
            Assert.NotNull(logged);
        }
        finally
        {
            Settings.Logger.OnWarningMessage -= handler;
            RootAnchorHelper.ClientOverride = null;
        }
    }
}
