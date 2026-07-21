using System.Collections.Generic;

namespace DnsClientX;

/// <summary>
/// Provides options for constructing a <see cref="DnsMessage"/>.
/// </summary>
public readonly record struct DnsMessageOptions(
    bool RequestDnsSec = false,
    bool EnableEdns = false,
    int UdpBufferSize = 4096,
    EdnsClientSubnetOption? Subnet = null,
    bool CheckingDisabled = false,
    IEnumerable<EdnsOption>? Options = null,
    bool RecursionDesired = true,
    ushort? TransactionId = null,
    ushort QueryClass = 1);
