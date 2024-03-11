using System.Collections.Generic;
using System.Text;
using DnsClient;
using DnsClient.Protocol;

namespace DnsClientX.Converter {
    /// <summary>
    /// Provides extension methods for DnsClientX.
    /// </summary>
    public static partial class DnsClientXExtensions {
        /// <summary>
        /// Converts a collection of DnsAnswer to a list of DnsResourceRecord.
        /// </summary>
        /// <param name="dnsAnswers">The collection of DnsAnswer to convert.</param>
        /// <returns>A list of DnsResourceRecord.</returns>
        public static List<DnsResourceRecord> ToDnsClientAnswer(this IEnumerable<DnsClientX.DnsAnswer> dnsAnswers) {
            var dnsClientAnswers = new List<DnsResourceRecord>();
            foreach (var dnsAnswer in dnsAnswers) {
                var record = dnsAnswer.ToDnsClientAnswer();
                dnsClientAnswers.Add(record);
            }
            return dnsClientAnswers;
        }

        /// <summary>
        /// Converts a DnsAnswer to a DnsResourceRecord.
        /// </summary>
        /// <param name="dnsAnswer">The DnsAnswer to convert.</param>
        /// <returns>A DnsResourceRecord.</returns>
        public static DnsResourceRecord ToDnsClientAnswer(this DnsClientX.DnsAnswer dnsAnswer) {
            var info = new ResourceRecordInfo(dnsAnswer.Name, (ResourceRecordType)dnsAnswer.Type, QueryClass.IN, dnsAnswer.TTL, dnsAnswer.DataRaw.Length);

            switch ((DnsClient.QueryType)dnsAnswer.Type) {
                case DnsClient.QueryType.A:
                    return new ARecord(info, System.Net.IPAddress.Parse(dnsAnswer.Data));
                case DnsClient.QueryType.AAAA:
                    return new AaaaRecord(info, System.Net.IPAddress.Parse(dnsAnswer.Data));
                case DnsClient.QueryType.CNAME:
                    return new CNameRecord(info, DnsString.FromResponseQueryString(dnsAnswer.Data));
                case DnsClient.QueryType.MX:
                    var parts = dnsAnswer.Data.Split(' ');
                    return new MxRecord(info, ushort.Parse(parts[0]), DnsString.FromResponseQueryString(parts[1]));
                case DnsClient.QueryType.TXT:
                    return new TxtRecord(info, dnsAnswer.Data.Split(' '), new string[0]);
                case DnsClient.QueryType.NS:
                    return new NsRecord(info, DnsString.FromResponseQueryString(dnsAnswer.Data));
                case DnsClient.QueryType.PTR:
                    return new PtrRecord(info, DnsString.FromResponseQueryString(dnsAnswer.Data));
                case DnsClient.QueryType.SRV:
                    var partsData = dnsAnswer.Data.Split(' ');
                    return new SrvRecord(info, ushort.Parse(partsData[0]), ushort.Parse(partsData[1]), ushort.Parse(partsData[2]), DnsString.FromResponseQueryString(partsData[3]));
                case DnsClient.QueryType.CAA:
                    var partsCaa = dnsAnswer.Data.Split(' ');
                    return new CaaRecord(info, byte.Parse(partsCaa[0]), partsCaa[1], partsCaa[2]);
                case DnsClient.QueryType.DNSKEY:
                    var partsDnsKey = dnsAnswer.Data.Split(' ');
                    return new DnsKeyRecord(info, ushort.Parse(partsDnsKey[0]), byte.Parse(partsDnsKey[1]), (byte)DnsKeyAlgorithmExtensions.FromValue(int.Parse(partsDnsKey[2])), Encoding.ASCII.GetBytes(partsDnsKey[3]));
                default:
                    throw new System.NotImplementedException($"The record type {dnsAnswer.Type} is not yet implemented");
            }
        }
    }
}
