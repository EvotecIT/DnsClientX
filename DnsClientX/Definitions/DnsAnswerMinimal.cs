using System.Linq;

namespace DnsClientX {
    /// <summary>
    /// Provides a lightweight representation of a DNS resource record.
    /// Useful for reducing the payload size returned to callers.
    /// See <a href="https://www.rfc-editor.org/rfc/rfc1035">RFC 1035</a>
    /// for the resource record definition.
    /// </summary>
    public struct DnsAnswerMinimal {
        /// <summary>
        /// Gets the name.
        /// </summary>
        /// <value>
        /// The name.
        /// </value>
        public string Name { get; internal set; }

        /// <summary>
        /// Gets the type.
        /// </summary>
        /// <value>
        /// The type.
        /// </value>
        public DnsRecordType Type { get; internal set; }

        /// <summary>
        /// Gets the TTL.
        /// </summary>
        /// <value>
        /// The TTL.
        /// </value>
        public int TTL { get; internal set; }

        /// <summary>
        /// Gets the data.
        /// </summary>
        /// <value>
        /// The data.
        /// </value>
        public string Data { get; internal set; }

        /// <summary>
        /// Gets the port of the DNS server that provided the answer.
        /// </summary>
        public int Port { get; internal set; }

        /// <summary>
        /// Gets the protocol used to obtain the DNS answer.
        /// </summary>
        public DnsRequestFormat RequestFormat { get; internal set; }

        /// <summary>
        /// Performs an explicit conversion from <see cref="DnsAnswer"/> to <see cref="DnsAnswerMinimal"/>.
        /// </summary>
        /// <param name="dnsAnswer">The DNS answer.</param>
        /// <returns>
        /// The result of the conversion.
        /// </returns>
        public static explicit operator DnsAnswerMinimal(DnsAnswer dnsAnswer) {
            return new DnsAnswerMinimal {
                Name = dnsAnswer.Name,
                TTL = dnsAnswer.TTL,
                Type = dnsAnswer.Type,
                Data = dnsAnswer.Data
            };
        }
    }

    /// <summary>
    /// Mini helper for DnsAnswer.
    /// </summary>
    public static class DnsAnswerMinimalHelper {
        /// <summary>
        /// Converts from DnsAnswer[] to DnsAnswerMinimal[].
        /// </summary>
        /// <param name="dnsAnswers">The DNS answers.</param>
        /// <returns>Array of minimal DNS answers.</returns>
        public static DnsAnswerMinimal[] ConvertFromDnsAnswer(this DnsAnswer[] dnsAnswers) {
            return dnsAnswers.Select(answer => new DnsAnswerMinimal {
                Name = answer.Name,
                TTL = answer.TTL,
                Type = answer.Type,
                Data = answer.Data
            }).ToArray();
        }
    }
}
