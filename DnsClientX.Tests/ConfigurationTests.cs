using System;
using Xunit;

namespace DnsClientX.Tests {
    public class ConfigurationTests {
        [Fact]
        public void ShouldCreateConfigurationFromHostname() {
            var config = new Configuration("1.1.1.1", DnsRequestFormat.DnsOverHttpsJSON);
            Assert.Equal(new Uri("https://1.1.1.1/dns-query"), config.BaseUri);
            Assert.Equal(443, config.Port);
        }
    }
}
