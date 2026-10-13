using System;
using System.Linq;
using System.Text;

namespace DnsClientX.Tests;
/// <summary>Protects RFC 6570 scalar expansion and the RFC 8484 GET/POST variable boundary.</summary>
public class DnsDohUriTemplateTests {
    /// <summary>Every current RFC 6570 operator expands the same independently specified Base64url scalar.</summary>
    [Theory]
    [InlineData("{dns}", "SGVsbG8gV29ybGQh")]
    [InlineData("{+dns}", "SGVsbG8gV29ybGQh")]
    [InlineData("{#dns}", "#SGVsbG8gV29ybGQh")]
    [InlineData("{.dns}", ".SGVsbG8gV29ybGQh")]
    [InlineData("{/dns}", "/SGVsbG8gV29ybGQh")]
    [InlineData("{;dns}", ";dns=SGVsbG8gV29ybGQh")]
    [InlineData("{?dns}", "?dns=SGVsbG8gV29ybGQh")]
    [InlineData("{&dns}", "&dns=SGVsbG8gV29ybGQh")]
    [InlineData("{?missing,dns*,other}", "?dns=SGVsbG8gV29ybGQh")]
    [InlineData("{dns:4,dns*}", "SGVs,SGVsbG8gV29ybGQh")]
    [InlineData("{;missing,dns:4,dns*}", ";dns=SGVs;dns=SGVsbG8gV29ybGQh")]
    [InlineData("{/missing,dns:4,dns*}", "/SGVs/SGVsbG8gV29ybGQh")]
    public void ExpandsAllCurrentOperatorsAndScalarModifiers(string expression, string expected) {
        DnsDohUriTemplate template = DnsDohUriTemplate.Parse("https://resolver.example/q" + expression);
        Assert.Equal("https://resolver.example/q" + expected, template.ExpandGet(Encoding.ASCII.GetBytes("Hello World!")));
        Assert.Equal("https://resolver.example/q", template.ExpandPost());
    }

    /// <summary>Unicode literals are UTF-8 encoded; literal percent triplets remain exact and encoded variable names stay undefined.</summary>
    [Fact]
    public void PreservesLiteralsAndDoesNotDecodeVariableNames() {
        var template = DnsDohUriTemplate.Parse("/é/漢/%2f{?%64ns,dns,other}");
        Assert.Equal("/%C3%A9/%E6%BC%A2/%2f?dns=-_8", template.ExpandGet(new byte[] { 251, 255 }));
        Assert.Equal("/%C3%A9/%E6%BC%A2/%2f", template.ExpandPost());
        Assert.Equal(new[] { "%64ns", "dns", "other" }, template.Variables);
        Assert.Throws<FormatException>(() => DnsDohUriTemplate.Parse("/q{?%64ns}").ExpandGet(new byte[] { 1 }));
    }

    /// <summary>Empty scalar values follow the named-operator rules, and POST never fills any variable.</summary>
    [Theory]
    [InlineData("{;dns}", ";dns")]
    [InlineData("{?dns}", "?dns=")]
    [InlineData("{&dns}", "&dns=")]
    [InlineData("{.dns}", ".")]
    public void ExpandsEmptyDefinedScalar(string expression, string expected) {
        var template = DnsDohUriTemplate.Parse("/q" + expression);
        Assert.Equal("/q" + expected, template.ExpandGet(Array.Empty<byte>()));
        Assert.Equal("/q", template.ExpandPost());
    }

    /// <summary>Malformed percent escapes, literal characters, operators, names, and modifiers are rejected before a request.</summary>
    [Theory]
    [InlineData("/q{}")]
    [InlineData("/q{dns")]
    [InlineData("/q}")]
    [InlineData("/q{dns{?x}")]
    [InlineData("/q{=dns}")]
    [InlineData("/q{dns-name}")]
    [InlineData("/q{.}")]
    [InlineData("/q{dns.}")]
    [InlineData("/q{dns..x}")]
    [InlineData("/q{dns:0}")]
    [InlineData("/q{dns:0001}")]
    [InlineData("/q{dns:10000}")]
    [InlineData("/q{dns:2*}")]
    [InlineData("/q{dns**}")]
    [InlineData("/q{dns,}")]
    [InlineData("/q%")]
    [InlineData("/q%0G")]
    [InlineData("/q{?%zz}")]
    [InlineData("/q path{?dns}")]
    [InlineData("/q\\path{?dns}")]
    public void RejectsMalformedTemplates(string template) => Assert.Throws<FormatException>(() => DnsDohUriTemplate.Parse(template));
    /// <summary>A malformed UTF-16 literal cannot be converted into a replacement character or silently transmitted.</summary>
    [Fact]
    public void RejectsUnpairedUnicodeSurrogates() => Assert.Throws<FormatException>(() => DnsDohUriTemplate.Parse("/q\ud800{?dns}"));
}
