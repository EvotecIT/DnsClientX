using System;
using System.Collections.Generic;

namespace DnsClientX {
    /// <summary>
    /// Formats DNS responses into human-readable CLI-friendly text lines.
    /// </summary>
    public static class DnsResponseTextFormatter {
        /// <summary>
        /// Builds short output lines containing answer values only.
        /// </summary>
        public static string[] BuildShortLines(DnsResponse response, bool txtConcat) {
            if (response == null) {
                throw new ArgumentNullException(nameof(response));
            }

            var lines = new List<string>();
            foreach (DnsAnswer answer in response.Answers ?? Array.Empty<DnsAnswer>()) {
                lines.Add(FormatAnswerData(answer, txtConcat));
            }

            return lines.ToArray();
        }

        /// <summary>
        /// Builds pretty output lines for a response.
        /// </summary>
        public static string[] BuildPrettyLines(
            DnsResponse response,
            bool showQuestions,
            bool showAnswers,
            bool showAuthorities,
            bool showAdditional,
            bool txtConcat) {
            if (response == null) {
                throw new ArgumentNullException(nameof(response));
            }

            var lines = new List<string> {
                $"Status: {response.Status} (retries {response.RetryCount})"
            };

            if (showQuestions) {
                lines.AddRange(BuildQuestionSectionLines("Questions", response.Questions ?? Array.Empty<DnsQuestion>()));
            }

            if (showAnswers) {
                lines.AddRange(BuildAnswerSectionLines("Answers", response.Answers ?? Array.Empty<DnsAnswer>(), includeTtl: true, txtConcat));
            }

            if (showAuthorities) {
                lines.AddRange(BuildAnswerSectionLines("Authorities", response.Authorities ?? Array.Empty<DnsAnswer>(), includeTtl: true, txtConcat));
            }

            if (showAdditional) {
                lines.AddRange(BuildAnswerSectionLines("Additional", response.Additional ?? Array.Empty<DnsAnswer>(), includeTtl: true, txtConcat));
            }

            return lines.ToArray();
        }

        /// <summary>
        /// Builds raw output lines for a response.
        /// </summary>
        public static string[] BuildRawLines(
            DnsResponse response,
            TimeSpan elapsed,
            bool showQuestions,
            bool showAnswers,
            bool showAuthorities,
            bool showAdditional,
            bool txtConcat,
            Func<TimeSpan, string> durationFormatter) {
            if (response == null) {
                throw new ArgumentNullException(nameof(response));
            }

            if (durationFormatter == null) {
                throw new ArgumentNullException(nameof(durationFormatter));
            }

            DnsQuestion[] questions = response.Questions ?? Array.Empty<DnsQuestion>();
            DnsAnswer[] answers = response.Answers ?? Array.Empty<DnsAnswer>();
            DnsAnswer[] authorities = response.Authorities ?? Array.Empty<DnsAnswer>();
            DnsAnswer[] additional = response.Additional ?? Array.Empty<DnsAnswer>();

            var lines = new List<string> {
                $";; status: {response.Status}",
                $";; transport: {response.UsedTransport}",
                $";; query time: {durationFormatter(response.RoundTripTime > TimeSpan.Zero ? response.RoundTripTime : elapsed)}",
                $";; sections: question {questions.Length}, answer {answers.Length}, authority {authorities.Length}, additional {additional.Length}"
            };

            if (showQuestions) {
                lines.Add(string.Empty);
                lines.Add(";; QUESTION SECTION:");
                foreach (DnsQuestion question in questions) {
                    lines.Add($";{question.Name}\tIN\t{question.Type}");
                }
            }

            if (showAnswers) {
                lines.Add(string.Empty);
                lines.Add(";; ANSWER SECTION:");
                foreach (DnsAnswer answer in answers) {
                    lines.Add($"{answer.Name}\t{answer.TTL}\tIN\t{answer.Type}\t{FormatAnswerData(answer, txtConcat)}");
                }
            }

            if (showAuthorities) {
                lines.Add(string.Empty);
                lines.Add(";; AUTHORITY SECTION:");
                foreach (DnsAnswer answer in authorities) {
                    lines.Add($"{answer.Name}\t{answer.TTL}\tIN\t{answer.Type}\t{FormatAnswerData(answer, txtConcat)}");
                }
            }

            if (showAdditional) {
                lines.Add(string.Empty);
                lines.Add(";; ADDITIONAL SECTION:");
                foreach (DnsAnswer answer in additional) {
                    lines.Add($"{answer.Name}\t{answer.TTL}\tIN\t{answer.Type}\t{FormatAnswerData(answer, txtConcat)}");
                }
            }

            return lines.ToArray();
        }

        private static IEnumerable<string> BuildQuestionSectionLines(string title, IEnumerable<DnsQuestion> questions) {
            yield return $"{title}:";
            foreach (DnsQuestion question in questions) {
                yield return $"  {question.Name}\t{question.Type}";
            }
        }

        private static IEnumerable<string> BuildAnswerSectionLines(string title, IEnumerable<DnsAnswer> answers, bool includeTtl, bool txtConcat) {
            yield return $"{title}:";
            foreach (DnsAnswer answer in answers) {
                if (includeTtl) {
                    yield return $"  {answer.Name}\t{answer.Type}\t{answer.TTL}\t{FormatAnswerData(answer, txtConcat)}";
                } else {
                    yield return $"  {answer.Name}\t{answer.Type}\t{FormatAnswerData(answer, txtConcat)}";
                }
            }
        }

        private static string FormatAnswerData(DnsAnswer answer, bool txtConcat) {
            return txtConcat ? answer.TxtConcatenatedData : answer.Data;
        }
    }
}
