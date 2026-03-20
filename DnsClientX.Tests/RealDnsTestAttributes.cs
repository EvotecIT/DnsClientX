using Xunit;

namespace DnsClientX.Tests;

/// <summary>
/// Opt-in fact for tests that query live DNS providers or require external network access.
/// </summary>
public sealed class RealDnsFactAttribute : FactAttribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="RealDnsFactAttribute"/> class.
    /// </summary>
    public RealDnsFactAttribute()
    {
        Skip = TestSkipHelpers.GetRealDnsSkipReason();
    }
}

/// <summary>
/// Opt-in theory for tests that query live DNS providers or require external network access.
/// </summary>
public sealed class RealDnsTheoryAttribute : TheoryAttribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="RealDnsTheoryAttribute"/> class.
    /// </summary>
    public RealDnsTheoryAttribute()
    {
        Skip = TestSkipHelpers.GetRealDnsSkipReason();
    }
}
