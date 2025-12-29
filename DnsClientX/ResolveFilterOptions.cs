namespace DnsClientX;

/// <summary>
/// Provides options for <see cref="ClientX.ResolveFilter(string,DnsRecordType,string,ResolveFilterOptions,bool,bool,bool,int,int,System.Threading.CancellationToken)"/>
/// and related overloads.
/// </summary>
/// <example>
/// <code>
/// var options = new ResolveFilterOptions(IncludeAliases: true);
/// var response = await client.ResolveFilter("example.com", DnsRecordType.TXT, "v=spf1", options);
/// </code>
/// </example>
public readonly record struct ResolveFilterOptions(bool IncludeAliases = false);
