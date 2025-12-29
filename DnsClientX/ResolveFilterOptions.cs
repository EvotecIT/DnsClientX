namespace DnsClientX {
    /// <summary>
    /// Provides options for <see cref="ClientX.ResolveFilter(string,DnsRecordType,string,ResolveFilterOptions,bool,bool,bool,int,int,System.Threading.CancellationToken)"/>
    /// and related overloads.
    /// </summary>
    public readonly record struct ResolveFilterOptions(bool IncludeAliases = false);
}
