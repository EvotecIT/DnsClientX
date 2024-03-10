using DnsClient;
using DnsClient.Protocol;
using DnsClientX;
using System.Collections.Generic;

namespace DnsClientX.Converter {
    public static partial class DnsClientXExtensions {
        public static List<DnsAnswer> ToDnsClientAnswerX(this IEnumerable<DnsResourceRecord> dnsResourceRecords) {
            var dnsAnswers = new List<DnsAnswer>();
            foreach (var dnsResourceRecord in dnsResourceRecords) {
                var dnsAnswer = dnsResourceRecord.ToDnsClientAnswerX();
                dnsAnswers.Add(dnsAnswer);
            }
            return dnsAnswers;
        }


        public static DnsAnswer ToDnsClientAnswerX(this DnsResourceRecord dnsResourceRecord) {
            // Here's a basic example for an A record
            if (dnsResourceRecord.RecordType == ResourceRecordType.A) {
                var aRecord = dnsResourceRecord as ARecord;
                var dnsAnswer = new DnsAnswer {
                    Name = aRecord.DomainName,
                    Type = (DnsRecordType)aRecord.RecordType,
                    TTL = aRecord.InitialTimeToLive,
                    DataRaw = aRecord.Address.ToString()
                };
                return dnsAnswer;
            } else {
                throw new System.NotImplementedException("This record type is not yet implemented");
            }
        }
    }
}
