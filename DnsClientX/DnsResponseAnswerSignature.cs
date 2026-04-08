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
                .Select(answer => $"{answer.Name}|{answer.Type}|{answer.Data}")
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();

            return string.Join(";", values);
        }
    }
}
