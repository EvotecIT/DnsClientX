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

/// <summary>
/// Opt-in fact for live DNS tests that specifically exercise modern transports such as DoH3 and DoQ.
/// </summary>
public sealed class RealModernDnsFactAttribute : FactAttribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="RealModernDnsFactAttribute"/> class.
    /// </summary>
    /// <param name="requestFormat">The modern transport that must be available for the test.</param>
    public RealModernDnsFactAttribute(DnsRequestFormat requestFormat)
    {
        Skip = TestSkipHelpers.GetModernTransportSkipReason(requestFormat);
    }
}

/// <summary>
/// Opt-in theory for live DNS tests that specifically exercise modern transports such as DoH3 and DoQ.
/// </summary>
public sealed class RealModernDnsTheoryAttribute : TheoryAttribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="RealModernDnsTheoryAttribute"/> class.
    /// </summary>
    /// <param name="requestFormat">The modern transport that must be available for the test.</param>
    public RealModernDnsTheoryAttribute(DnsRequestFormat requestFormat)
    {
        Skip = TestSkipHelpers.GetModernTransportSkipReason(requestFormat);
    }
}
