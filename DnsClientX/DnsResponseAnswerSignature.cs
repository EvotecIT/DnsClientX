using System;
using System.Linq;

namespace DnsClientX {
    /// <summary>
    /// Builds stable answer signatures from DNS responses for comparison and grouping.
    /// </summary>
    public static class DnsResponseAnswerSignature {
        /// <summary>
        /// Builds a stable signature string for the answer section of the supplied response.
        /// </summary>
        public static string Build(DnsResponse? response) {
            if (response?.Answers == null || response.Answers.Length == 0) {
                return "(no answers)";
            }

            string[] values = response.Answers
                .Select(answer => Encode(DnsWireNameCodec.Canonical(answer.Name)) + Encode(answer.Type.ToString()) + Encode(answer.Data))
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();

            // Record data may itself contain '|', ';', or newlines. Delimiter-only signatures
            // can report one TXT record and multiple different records as the same answer set.
            return Encode(values.Length.ToString(System.Globalization.CultureInfo.InvariantCulture)) + string.Concat(values.Select(Encode));
        }

        private static string Encode(string value) => value.Length.ToString(System.Globalization.CultureInfo.InvariantCulture) + ":" + value;
    }
}
