using DnsClient;
using DnsClient.Protocol;
using System.Collections.Generic;

namespace DnsClientX.Converter {
    public static partial class DnsClientXExtensions {
        /// <summary>
        /// Converts  multiple DnsResourceRecords from DnsClient to DnsClientX.
        /// </summary>
        /// <param name="dnsResourceRecords">The DNS resource records.</param>
        /// <returns></returns>
        public static DnsAnswer[] ToDnsClientAnswerX(this IEnumerable<DnsResourceRecord> dnsResourceRecords) {
            var dnsAnswers = new List<DnsAnswer>();
            foreach (var dnsResourceRecord in dnsResourceRecords) {
                var dnsAnswer = dnsResourceRecord.ToDnsClientAnswerX();
                dnsAnswers.Add(dnsAnswer);
            }
            return dnsAnswers.ToArray();
        }


        /// <summary>
        /// Converts DnsResourceRecord from DnsClient to DnsClientX answer.
        /// </summary>
        /// <param name="dnsResourceRecord">The DNS resource record.</param>
        /// <returns></returns>
        /// <exception cref="System.NotImplementedException">The record type {dnsResourceRecord.RecordType} is not yet implemented</exception>
        public static DnsAnswer ToDnsClientAnswerX(this DnsResourceRecord dnsResourceRecord) {
            switch (dnsResourceRecord.RecordType) {
                case ResourceRecordType.A:
                    var aRecord = dnsResourceRecord as ARecord;
                    return new DnsAnswer {
                        Name = aRecord.DomainName,
                        Type = (DnsRecordType)aRecord.RecordType,
                        TTL = aRecord.InitialTimeToLive,
                        DataRaw = aRecord.Address.ToString()
                    };

                case ResourceRecordType.CNAME:
                    var cnameRecord = dnsResourceRecord as CNameRecord;
                    return new DnsAnswer {
                        Name = cnameRecord.DomainName,
                        Type = (DnsRecordType)cnameRecord.RecordType,
                        TTL = cnameRecord.InitialTimeToLive,
                        DataRaw = cnameRecord.CanonicalName.ToString()
                    };

                case ResourceRecordType.MX:
                    var mxRecord = dnsResourceRecord as MxRecord;
                    return new DnsAnswer {
                        Name = mxRecord.DomainName,
                        Type = (DnsRecordType)mxRecord.RecordType,
                        TTL = mxRecord.InitialTimeToLive,
                        DataRaw = $"{mxRecord.Preference} {mxRecord.Exchange}"
                    };
                case ResourceRecordType.NS:
                    var nsRecord = dnsResourceRecord as NsRecord;
                    return new DnsAnswer {
                        Name = nsRecord.DomainName,
                        Type = (DnsRecordType)nsRecord.RecordType,
                        TTL = nsRecord.InitialTimeToLive,
                        DataRaw = nsRecord.NSDName.ToString()
                    };

                case ResourceRecordType.SOA:
                    var soaRecord = dnsResourceRecord as SoaRecord;
                    return new DnsAnswer {
                        Name = soaRecord.DomainName,
                        Type = (DnsRecordType)soaRecord.RecordType,
                        TTL = soaRecord.InitialTimeToLive,
                        DataRaw = $"{soaRecord.MName} {soaRecord.RName} {soaRecord.Serial} {soaRecord.Refresh} {soaRecord.Retry} {soaRecord.Expire} {soaRecord.Minimum}"
                    };

                case ResourceRecordType.TXT:
                    var txtRecord = dnsResourceRecord as TxtRecord;
                    return new DnsAnswer {
                        Name = txtRecord.DomainName,
                        Type = (DnsRecordType)txtRecord.RecordType,
                        TTL = txtRecord.InitialTimeToLive,
                        DataRaw = string.Join(" ", txtRecord.Text)
                    };

                case ResourceRecordType.PTR:
                    var ptrRecord = dnsResourceRecord as PtrRecord;
                    return new DnsAnswer {
                        Name = ptrRecord.DomainName,
                        Type = (DnsRecordType)ptrRecord.RecordType,
                        TTL = ptrRecord.InitialTimeToLive,
                        DataRaw = ptrRecord.PtrDomainName.ToString()
                    };

                case ResourceRecordType.SRV:
                    var srvRecord = dnsResourceRecord as SrvRecord;
                    return new DnsAnswer {
                        Name = srvRecord.DomainName,
                        Type = (DnsRecordType)srvRecord.RecordType,
                        TTL = srvRecord.InitialTimeToLive,
                        DataRaw = $"{srvRecord.Priority} {srvRecord.Weight} {srvRecord.Port} {srvRecord.Target}"
                    };

                case ResourceRecordType.AAAA:
                    var aaaaRecord = dnsResourceRecord as AaaaRecord;
                    return new DnsAnswer {
                        Name = aaaaRecord.DomainName,
                        Type = (DnsRecordType)aaaaRecord.RecordType,
                        TTL = aaaaRecord.InitialTimeToLive,
                        DataRaw = aaaaRecord.Address.ToString()
                    };
                default:
                    throw new System.NotImplementedException($"The record type {dnsResourceRecord.RecordType} is not yet implemented");
            }
        }
    }
}
