using System.Collections.Generic;
using DnsClient;
using DnsClient.Protocol;

namespace DnsClientX.Converter {
    public static partial class DnsClientXExtensions {
        public static List<DnsResourceRecord> ToDnsClientAnswer(this IEnumerable<DnsClientX.DnsAnswer> dnsAnswers) {
            var dnsClientAnswers = new List<DnsResourceRecord>();
            foreach (var dnsAnswer in dnsAnswers) {
                var record = dnsAnswer.ToDnsClientAnswer();
                dnsClientAnswers.Add(record);
            }
            return dnsClientAnswers;
        }

        public static DnsResourceRecord ToDnsClientAnswer(this DnsClientX.DnsAnswer dnsAnswer) {
            // Here's a basic example for an A record
            if (dnsAnswer.Type.ToString() == DnsClient.QueryType.A.ToString()) {
                var info = new ResourceRecordInfo(dnsAnswer.Name, (ResourceRecordType)dnsAnswer.Type, QueryClass.IN, dnsAnswer.TTL, dnsAnswer.DataRaw.Length);
                var record = new ARecord(info, System.Net.IPAddress.Parse(dnsAnswer.DataRaw));
                return record;
            } else {
                throw new System.NotImplementedException("This record type is not yet implemented");
            }
        }
    }
}
