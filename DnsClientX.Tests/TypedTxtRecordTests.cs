using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace DnsClientX.Tests;

/// <summary>Protects the shared typed/untyped TXT contract across provider presentations and cache snapshots.</summary>
public class TypedTxtRecordTests {
    /// <summary>Provider separators and escaping cannot change decoded TXT/SPF content or fragment boundaries.</summary>
    [Theory]
    [InlineData(DnsRecordType.TXT, false)]
    [InlineData(DnsRecordType.TXT, true)]
    [InlineData(DnsRecordType.SPF, false)]
    [InlineData(DnsRecordType.SPF, true)]
    public void Factory_PreservesFragmentsAcrossPresentations(DnsRecordType type, bool specialized) {
        string[] presentations = {
            "\"Case\\\"Sensitive\" \"\" \" \\010B\"",
            "\"Case\\034Sensitive\"\"\"\" \\010B\"",
            " \"Case\\\"Sensitive\"\r\n\"\"\t\" \nB\" "
        };
        string[] expected = { "Case\"Sensitive", "", " \nB" };
        foreach (string raw in presentations) {
            var answer = new DnsAnswer { Type = type, DataRaw = raw };
            var typed = Assert.IsType<TxtRecord>(DnsRecordFactory.Create(answer, specialized));
            Assert.Equal("Case\"Sensitive \nB", typed.Text);
            Assert.Equal(expected, typed.Strings);
            Assert.Equal(answer.Data, typed.Text);
            Assert.Equal(answer.DataStringsEscaped, typed.Strings);
            Assert.Equal(answer.DataStrings, typed.RawStrings);
            Assert.Equal(raw, typed.RawText);
        }
    }

    /// <summary>Direct constructors accept decoded content, snapshot inputs, and expose immutable collections.</summary>
    [Fact]
    public void Constructors_PreserveLiteralPayloadAndSnapshotFragments() {
        string[] strings = { "\"literal\"", "", "\\010\r\n " };
        var typed = new TxtRecord(strings);
        strings[0] = "changed";
        Assert.Equal("\"literal\"\\010\r\n ", typed.Text);
        Assert.Equal(new[] { "\"literal\"", "", "\\010\r\n " }, typed.Strings);
        var parsed = new DnsAnswer { Type = DnsRecordType.TXT, DataRaw = typed.RawText };
        Assert.Equal(typed.Text, parsed.Data);
        Assert.Equal(typed.Strings, parsed.DataStringsEscaped);
        Assert.Throws<NotSupportedException>(() => ((IList<string>)typed.Strings)[0] = "changed");
        Assert.Throws<NotSupportedException>(() => ((IList<string>)typed.RawStrings)[0] = "changed");
        var single = new TxtRecord("\"literal\"\\010");
        Assert.Equal("\"literal\"\\010", single.Text);
        Assert.Equal(single.Text, Assert.Single(single.Strings));
    }

    /// <summary>Real JSON and wire parsers agree on payload bytes, empty chunks, and independent resource records.</summary>
    [Theory]
    [InlineData(DnsRecordType.TXT)]
    [InlineData(DnsRecordType.SPF)]
    public async Task JsonAndWire_ExposeTheSameDecodedRecords(DnsRecordType type) {
        string raw = "\"A\\\"\\\\\" \"\" \" \\013\\010B\"";
        string json = "{\"Status\":0,\"Answer\":[{\"name\":\"example.com.\",\"type\":" + (int)type
            + ",\"TTL\":60,\"data\":" + System.Text.Json.JsonSerializer.Serialize(raw)
            + "},{\"name\":\"example.com.\",\"type\":" + (int)type
            + ",\"TTL\":60,\"data\":\"\\\"independent\\\"\"}]}";
        using var http = new HttpResponseMessage(HttpStatusCode.OK) {
            Content = new StringContent(json, Encoding.UTF8, "application/dns-json")
        };
        DnsResponse jsonResponse = await http.DeserializeResponse();
        DnsResponse wireResponse = await DnsWire.DeserializeDnsWireFormat(null, false, CreateWireResponse(type));

        foreach (DnsResponse response in new[] { jsonResponse, wireResponse }) {
            Assert.Equal(2, response.Answers.Length);
            var typed = Assert.IsType<TxtRecord>(response.Answers[0].TypedRecord);
            Assert.Equal("A\"\\ \r\nB", typed.Text);
            Assert.Equal(new[] { "A\"\\", "", " \r\nB" }, typed.Strings);
            Assert.Equal("independent", Assert.IsType<TxtRecord>(response.Answers[1].TypedRecord).Text);
        }
        Assert.Equal(jsonResponse.Answers[0].Data, wireResponse.Answers[0].Data);
        Assert.Equal(jsonResponse.Answers[0].DataStringsEscaped, wireResponse.Answers[0].DataStringsEscaped);

        using var cache = new DnsResponseCache();
        wireResponse.TypedAnswers = wireResponse.Answers.Select(answer => answer.TypedRecord!).ToArray();
        cache.Set("txt-records", wireResponse, TimeSpan.FromMinutes(1));
        wireResponse.Answers[0].DataRaw = "changed";
        Assert.True(cache.TryGet("txt-records", out var cached));
        var cachedTxt = Assert.IsType<TxtRecord>(cached.TypedAnswers![0]);
        Assert.Equal("A\"\\ \r\nB", cachedTxt.Text);
        Assert.Equal(new[] { "A\"\\", "", " \r\nB" }, cachedTxt.Strings);
        Assert.Equal(cached.Answers[0].DataRaw, cachedTxt.RawText);
    }

    private static byte[] CreateWireResponse(DnsRecordType type) {
        var bytes = new List<byte> { 0x12, 0x34, 0x81, 0x80, 0, 0, 0, 2, 0, 0, 0, 0 };
        AddRecord(new[] { "A\"\\", "", " \r\nB" });
        AddRecord(new[] { "independent" });
        return bytes.ToArray();

        void AddRecord(string[] strings) {
            bytes.AddRange(new byte[] { 7, (byte)'e', (byte)'x', (byte)'a', (byte)'m', (byte)'p', (byte)'l', (byte)'e',
                3, (byte)'c', (byte)'o', (byte)'m', 0 });
            bytes.AddRange(new byte[] { (byte)((int)type >> 8), (byte)type, 0, 1, 0, 0, 0, 60 });
            var rdata = new List<byte>();
            foreach (string value in strings) {
                byte[] data = Encoding.ASCII.GetBytes(value);
                rdata.Add((byte)data.Length);
                rdata.AddRange(data);
            }
            bytes.Add((byte)(rdata.Count >> 8));
            bytes.Add((byte)rdata.Count);
            bytes.AddRange(rdata);
        }
    }
}
