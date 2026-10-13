using System;
using System.Linq;
using System.Text;

namespace DnsClientX.Tests;
/// <summary>Protects RFC 9461 compatibility selection without conflating it with resolver authentication.</summary>
public class DnsServiceBindingSelectionTests {
    /// <summary>Priority, ALPN preference, per-protocol default ports, and address hints remain typed.</summary>
    [Fact]
    public void SelectsPriorityAndAlpnWithPerTransportDefaults() {
        SvcbRecord first = Parse("1 . alpn=dot,h2,doq,h3 dohpath=/q{?dns} ipv4hint=192.0.2.1");
        SvcbRecord second = Parse("2 resolver.example alpn=dot port=8853");
        DnsServiceBinding[] selected = DnsServiceBindingSelection.SelectDnsResolvers("_dns.example", new[] { second, first }, new[] { "h2", "dot", "doq", "h3" });
        Assert.Equal(new[] { "h2", "dot", "doq", "h3", "dot" }, selected.Select(binding => binding.Alpn));
        Assert.Equal(new[] { 443, 853, 853, 443, 8853 }, selected.Select(binding => binding.Port));
        Assert.Equal("_dns.example", selected[0].Target);
        Assert.Equal("192.0.2.1", Assert.Single(selected[0].AddressHints).ToString());
        Assert.Equal("/q?dns=AQ", selected[0].DohPath!.ExpandGet(new byte[] { 1 }));
    }

    /// <summary>Aliases, absent ALPN, unknown mandatory keys, and mandatory unsupported ECH do not produce usable bindings.</summary>
    [Theory]
    [InlineData("0 _dns.alias.example")]
    [InlineData("1 resolver.example port=443")]
    [InlineData("1 resolver.example alpn=dot mandatory=key99 key99=opaque")]
    [InlineData("1 resolver.example alpn=dot mandatory=ech ech=AQID")]
    [InlineData("1 resolver.example alpn=dot port=0")]
    [InlineData("1 resolver.example alpn=h2")]
    [InlineData("1 resolver.example alpn=h2 dohpath=/q")]
    [InlineData("1 resolver.example alpn=dot mandatory=dohpath dohpath=/q")]
    [InlineData("1 resolver.example alpn=h2 dohpath=//attacker.example/q{?dns}")]
    [InlineData("1 resolver.example alpn=h2 dohpath=/q{#dns}")]
    public void RejectsIncompatibleOrUnsafeBindings(string record) => Assert.Empty(DnsServiceBindingSelection.SelectDnsResolvers("_dns.example", new[] { Parse(record) }));
    /// <summary>The DoH path must be valid UTF-8, rather than replacement-decoded wire bytes.</summary>
    [Fact]
    public void RejectsMalformedDohPathUtf8() {
        var record = new SvcbRecord(1, "resolver.example", new[] { new SvcbParameter(1, new byte[] { 2, (byte)'h', (byte)'2' }), new SvcbParameter(7, new byte[] { (byte)'/', 0xc0, 0x80 }) });
        Assert.Empty(DnsServiceBindingSelection.SelectDnsResolvers("_dns.example", new[] { record }));
    }

    /// <summary>A mixed AliasMode RRset needs resolution even if it also carries lower priority ServiceMode data.</summary>
    [Fact]
    public void RequiresAliasResolutionBeforeSelectingServiceMode() => Assert.Empty(DnsServiceBindingSelection.SelectDnsResolvers("_dns.example", new[] { Parse("0 _dns.alias.example"), Parse("1 resolver.example alpn=dot") }));
    private static SvcbRecord Parse(string text) => Assert.IsType<SvcbRecord>(new DnsAnswer { Type = DnsRecordType.SVCB, DataRaw = text }.TypedRecord);
}
