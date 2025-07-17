using System.Net.Http;
using System.Net.Security;
using System.Reflection;
using Xunit;

namespace DnsClientX.Tests {
    /// <summary>
    /// Tests ignoring certificate validation errors when constructing <see cref="ClientX"/>.
    /// </summary>
    public class IgnoreCertificateErrorsTests {
        /// <summary>
        /// Validates that certificate validation callbacks are registered when enabled.
        /// </summary>
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

