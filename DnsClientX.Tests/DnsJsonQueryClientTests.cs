using System.Net;
using System.Net.Http;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace DnsClientX.Tests;

/// <summary>Verifies the public caller-owned HTTP DNS JSON client.</summary>
public sealed class DnsJsonQueryClientTests {
    /// <summary>Maps extended RCODEs and preserves query metadata.</summary>
    [Fact]
    public async Task QueryAsyncMapsExtendedRcodeAndAddsQuestionMetadata() {
        using var client = new HttpClient(new StubHandler(new HttpResponseMessage(HttpStatusCode.OK) {
            Content = new StringContent("{\"Status\":16,\"AD\":false,\"Answer\":[],\"Authority\":[]}", Encoding.UTF8, "application/dns-json")
        }));

        DnsResponse response = await DnsJsonQueryClient.QueryAsync(
            client, new Uri("https://resolver.example/resolve"), "example.com.", DnsRecordType.A);

        Assert.Equal(DnsResponseCode.BadVersion, response.Status);
        DnsQuestion question = Assert.Single(response.Questions);
        Assert.Equal("example.com", question.Name);
        Assert.Equal("example.com.", question.OriginalName);
        Assert.Equal(DnsRequestFormat.DnsOverHttps, question.RequestFormat);
        Assert.Equal(Transport.Doh, response.UsedTransport);
    }

    /// <summary>Rejects HTTP failures before attempting JSON parsing.</summary>
    [Fact]
    public async Task QueryAsyncRejectsHttpErrorsBeforeJsonParsing() {
        using var client = new HttpClient(new StubHandler(new HttpResponseMessage(HttpStatusCode.BadGateway) {
            Content = new StringContent("<html>proxy failure</html>", Encoding.UTF8, "text/html")
        }));

        DnsClientException exception = await Assert.ThrowsAsync<DnsClientException>(() =>
            DnsJsonQueryClient.QueryAsync(client, new Uri("https://resolver.example/resolve"), "example.com", DnsRecordType.A));

        Assert.Contains("HTTP 502", exception.Message);
    }

    /// <summary>Rejects successful responses whose body is not declared as JSON.</summary>
    [Fact]
    public async Task QueryAsyncRejectsNonJsonMediaType() {
        using var client = new HttpClient(new StubHandler(new HttpResponseMessage(HttpStatusCode.OK) {
            Content = new StringContent("{}", Encoding.UTF8, "text/html")
        }));

        DnsClientException exception = await Assert.ThrowsAsync<DnsClientException>(() =>
            DnsJsonQueryClient.QueryAsync(client, new Uri("https://resolver.example/resolve"), "example.com", DnsRecordType.A));

        Assert.Contains("unsupported media type", exception.Message);
    }

    /// <summary>Accepts valid streamed JSON when Content-Length is not supplied.</summary>
    [Fact]
    public async Task QueryAsyncAcceptsStreamWithoutContentLength() {
        var content = new StreamContent(new MemoryStream(Encoding.UTF8.GetBytes(
            "{\"Status\":0,\"Answer\":[],\"Authority\":[]}")));
        content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/dns-json");
        using var client = new HttpClient(new StubHandler(new HttpResponseMessage(HttpStatusCode.OK) { Content = content }));

        DnsResponse response = await DnsJsonQueryClient.QueryAsync(
            client, new Uri("https://resolver.example/resolve?source=test"), "example.com", DnsRecordType.A);

        Assert.Equal(DnsResponseCode.NoError, response.Status);
    }

    private sealed class StubHandler : HttpMessageHandler {
        private readonly HttpResponseMessage _response;

        public StubHandler(HttpResponseMessage response) {
            _response = response;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) {
            return Task.FromResult(_response);
        }
    }
}
