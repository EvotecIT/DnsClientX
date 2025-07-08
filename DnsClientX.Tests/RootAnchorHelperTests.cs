using DnsClientX;
using Xunit;

namespace DnsClientX.Tests;

public class RootAnchorHelperTests
{
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
}
