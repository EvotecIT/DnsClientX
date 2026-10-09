using System;
using System.Linq;

namespace DnsClientX;

public partial class DnsResponse {
    internal void PopulateTypedRecords(bool parseTypedTxtRecords) {
        TypedAnswers = Parse(Answers);
        TypedAuthorities = Parse(Authorities);
        TypedAdditional = Parse(Additional);

        object[] Parse(DnsAnswer[]? section) => (section ?? Array.Empty<DnsAnswer>())
            .Select(answer => DnsRecordFactory.Create(answer, parseTypedTxtRecords))
            .Where(record => record != null).ToArray()!;
    }
}
