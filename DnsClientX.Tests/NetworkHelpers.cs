using System;
using System.Net.Sockets;
using Xunit;

namespace DnsClientX.Tests;

public static class NetworkHelpers
{
    private const string SkipEnvVar = "DNSCLIENTX_SKIP_NETWORK_TESTS";

    public static bool HasInternetAccess()
    {
        if (Environment.GetEnvironmentVariable(SkipEnvVar) != null)
        {
            return false;
        }

        try
        {
            using TcpClient client = new();
            var connectTask = client.ConnectAsync("1.1.1.1", 443);
            bool completed = connectTask.Wait(TimeSpan.FromSeconds(2));
            return completed && client.Connected;
        }
        catch
        {
            return false;
        }
    }
}

public abstract class NetworkTestBase
{
    protected NetworkTestBase()
    {
        bool noInternet = !NetworkHelpers.HasInternetAccess();
        if (noInternet)
        {
            throw Xunit.Sdk.SkipException.ForSkip("Network not available");
        }
    }
}
