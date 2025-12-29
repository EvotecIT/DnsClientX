using System;
using System.Net;
using Xunit.Abstractions;

namespace DnsClientX.Tests;

internal static class TestSkipHelpers
{
    internal static bool ShouldSkipEndpoint(DnsEndpoint endpoint, ITestOutputHelper? output = null)
    {
        return ShouldSkipSystemTcp(endpoint, output) || ShouldSkipOdoh(endpoint, output);
    }

    private static bool ShouldSkipSystemTcp(DnsEndpoint endpoint, ITestOutputHelper? output)
    {
        if (endpoint != DnsEndpoint.SystemTcp)
        {
            return false;
        }

        var servers = SystemInformation.GetDnsFromActiveNetworkCard();
        if (servers == null || servers.Count == 0)
        {
            output?.WriteLine("[Diagnostic] System TCP DNS skipped: no active DNS servers detected.");
            return true;
        }

        var allLoopback = true;
        foreach (var server in servers)
        {
            if (IPAddress.TryParse(server, out var ip) && !IPAddress.IsLoopback(ip))
            {
                allLoopback = false;
                break;
            }
        }

        if (allLoopback)
        {
            output?.WriteLine("[Diagnostic] System TCP DNS skipped: only loopback resolvers detected.");
            return true;
        }

        return false;
    }

    private static bool ShouldSkipOdoh(DnsEndpoint endpoint, ITestOutputHelper? output)
    {
        if (endpoint != DnsEndpoint.CloudflareOdoh)
        {
            return false;
        }

        var allow = Environment.GetEnvironmentVariable("DNSCLIENTX_RUN_ODOH_TESTS");
        if (string.Equals(allow, "1", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(allow, "true", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        output?.WriteLine("[Diagnostic] Cloudflare ODoH tests skipped unless DNSCLIENTX_RUN_ODOH_TESTS=1.");
        return true;
    }
}
