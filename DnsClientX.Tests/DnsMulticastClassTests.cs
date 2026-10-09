namespace DnsClientX.Tests;

/// <summary>Separates mDNS flags from record identity without rewriting unicast evidence.</summary>
public class DnsMulticastClassTests {
    /// <summary>Only the mDNS parser strips class flags, in all sections and questions.</summary>
    [Fact]
    public async Task MulticastClassFlagsAreSeparateFromDnsClass() {
        byte[] message = SvcbRecordTests.Hex("000084000001000100010001 0000018001 "
            + "00000180010000003C0004C0000201 "
            + "00000180010000003C0004C0000201 "
            + "00000180010000003C0004C0000201");
        var multicast = await DnsWire.DeserializeDnsMulticastResponse(message);
        var unicast = await DnsWire.DeserializeDnsWireFormat(null, false, message);
        Assert.Equal((ushort)1, multicast.Questions[0].Class);
        Assert.True(multicast.Questions[0].UnicastResponseRequested);
        Assert.Equal((ushort)0x8001, unicast.Questions[0].Class);
        Assert.Null(unicast.Questions[0].UnicastResponseRequested);
        foreach (var answer in multicast.Answers.Concat(multicast.Authorities).Concat(multicast.Additional)) {
            Assert.Equal((ushort)1, answer.Class);
            Assert.True(answer.CacheFlush);
            Assert.Equal(message, answer.SourceWireMessage);
            Assert.Equal(new byte[] { 192, 0, 2, 1 }, answer.RawRdata);
        }
        foreach (var answer in unicast.Answers.Concat(unicast.Authorities).Concat(unicast.Additional)) {
            Assert.Equal((ushort)0x8001, answer.Class);
            Assert.Null(answer.CacheFlush);
        }
        var ordinary = multicast.Answers[0];
        ordinary.CacheFlush = false;
        Assert.Single(new[] { ordinary, multicast.Answers[0] }.Distinct());
    }

    /// <summary>The OPT CLASS field remains a payload size even in multicast messages.</summary>
    [Fact]
    public async Task MulticastOptKeepsPayloadSize() {
        byte[] message = SvcbRecordTests.Hex("000084000000000000000001 0000299C40000000000000");
        var response = await DnsWire.DeserializeDnsMulticastResponse(message);
        Assert.Equal(40000, response.EdnsUdpPayloadSize);
        Assert.Equal((ushort)40000, response.Additional[0].Class);
        Assert.Null(response.Additional[0].CacheFlush);
    }
}
