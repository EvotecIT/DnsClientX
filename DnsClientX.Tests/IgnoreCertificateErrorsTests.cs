using System.Net.Http;
using System.Net.Security;
using System.Reflection;
using Xunit;

namespace DnsClientX.Tests {
    public class IgnoreCertificateErrorsTests {
        [Fact]
        public void ShouldEnableCertificateValidationCallback() {
            using var client = new ClientX(DnsEndpoint.Cloudflare, ignoreCertificateErrors: true);
            var handlerField = typeof(ClientX).GetField("handler", BindingFlags.NonPublic | BindingFlags.Instance)!;
            var handler = (HttpClientHandler)handlerField.GetValue(client)!;
            Assert.NotNull(handler.ServerCertificateCustomValidationCallback);
            var callback = handler.ServerCertificateCustomValidationCallback!;
            Assert.True(callback.Invoke(null!, null!, null!, SslPolicyErrors.RemoteCertificateNameMismatch));
        }
    }
}

