using System;

namespace DnsClientX.Examples {
    /// <summary>
    /// Provides helper methods for displaying DNS responses and answers to the console.
    /// </summary>
    public static class Helpers {
        /// <summary>
        /// Displays an array of DNS responses to the console.
        /// </summary>
        /// <param name="responses">The array of DNS responses to display.</param>
        public static void DisplayToConsole(this DnsResponse[] responses) {
            Settings.Logger.WriteInformation($"Result:");
            if (responses.Length == 0) {
                Settings.Logger.WriteInformation("\tResponses: No responses");
                return;
            }
            foreach (DnsResponse response in responses) {
                DisplayToConsole(response);
            }
        }

        /// <summary>
        /// Displays a single DNS response to the console.
        /// </summary>
        /// <param name="response">The DNS response to display.</param>
        public static void DisplayToConsole(this DnsResponse? response) {
            Settings.Logger.WriteInformation($"Result:");
            if (response is null) {
                Settings.Logger.WriteInformation("\tResponse: Null");
                return;
            }
            Settings.Logger.WriteInformation($"\tResponse: {response.Value.Status}");
            if (response.Value.Answers is null) {
                Settings.Logger.WriteInformation("\tAnswers: No answers");
                return;
            }
            if (response.Value.Questions != null) {
                foreach (DnsQuestion question in response.Value.Questions) {
                    Settings.Logger.WriteInformation($"\tQuestion: {question.Name} => {question.Type}");
                }
            }
            Settings.Logger.WriteInformation($"\tAnswers: ");
            foreach (DnsAnswer answer in response.Value.Answers) {
                DisplayToConsole(answer);
            }
        }

        /// <summary>
        /// Displays an array of DNS answers to the console.
        /// </summary>
        /// <param name="answers">The array of DNS answers to display.</param>
        public static void DisplayToConsole(this DnsAnswer[] answers) {
            Settings.Logger.WriteInformation($"Result:");
            if (answers.Length == 0) {
                Settings.Logger.WriteInformation("\tAnswers: No answers");
                return;
            }
            foreach (DnsAnswer answer in answers) {
                DisplayToConsole(answer);
            }
        }

        /// <summary>
        /// Displays a single DNS answer to the console.
        /// </summary>
        /// <param name="answer">The DNS answer to display.</param>
        /// <param name="announce">If set to true, an additional "Result:" line is printed before the answer.</param>
        public static void DisplayToConsole(this DnsAnswer? answer, bool announce = false) {
            if (announce) {
                Settings.Logger.WriteInformation($"Result:");
            }

            if (answer == null) {
                Settings.Logger.WriteInformation("\tAnswer: Null");
                return;
            }
            Settings.Logger.WriteInformation($"\tType: {answer.Value.Type}; TTL: '{answer.Value.TTL}'; Name: '{answer.Value.Name}' => '{answer.Value.Data}'");
            if (answer.Value.DataStrings.Length > 1) {
                Settings.Logger.WriteInformation($"\t\tDataStrings: ");
                foreach (string dataString in answer.Value.DataStrings) {
                    Settings.Logger.WriteInformation($"\t\t{dataString}");
                }
            }
        }
    }
}
