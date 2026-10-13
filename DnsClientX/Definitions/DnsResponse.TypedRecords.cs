using System;
using System.Linq;

namespace DnsClientX;

public partial class DnsResponse {
    /// <summary>Parses answers while retaining metadata for each matching payload.</summary>
    public DnsTypedRecord<TRecord>[] GetTypedAnswers<TRecord>(bool parseTypedTxtRecords = false) where TRecord : class =>
        GetTypedRecords<TRecord>(Answers, parseTypedTxtRecords);

    /// <summary>Parses authority records while retaining metadata for each matching payload.</summary>
    public DnsTypedRecord<TRecord>[] GetTypedAuthorities<TRecord>(bool parseTypedTxtRecords = false) where TRecord : class =>
        GetTypedRecords<TRecord>(Authorities, parseTypedTxtRecords);

    /// <summary>Parses additional records while retaining metadata for each matching payload.</summary>
    public DnsTypedRecord<TRecord>[] GetTypedAdditional<TRecord>(bool parseTypedTxtRecords = false) where TRecord : class =>
        GetTypedRecords<TRecord>(Additional, parseTypedTxtRecords);

    private static DnsTypedRecord<TRecord>[] GetTypedRecords<TRecord>(DnsAnswer[]? section, bool parseTypedTxtRecords) where TRecord : class =>
        (section ?? Array.Empty<DnsAnswer>())
            .Select(record => (Record: record, Value: DnsRecordFactory.Create(record, parseTypedTxtRecords)))
            .Where(parsed => parsed.Value is TRecord)
            .Select(parsed => new DnsTypedRecord<TRecord>(parsed.Record, (TRecord)parsed.Value!)).ToArray();

    internal void PopulateTypedRecords(bool parseTypedTxtRecords) {
        TypedAnswers = Parse(Answers);
        TypedAuthorities = Parse(Authorities);
        TypedAdditional = Parse(Additional);

        object[] Parse(DnsAnswer[]? section) => (section ?? Array.Empty<DnsAnswer>())
            .Select(answer => DnsRecordFactory.Create(answer, parseTypedTxtRecords))
            .Where(record => record != null).ToArray()!;
    }
}
