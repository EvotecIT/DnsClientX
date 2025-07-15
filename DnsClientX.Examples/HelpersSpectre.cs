using System;
using System.Linq;
using System.Net;
using Spectre.Console;

namespace DnsClientX.Examples {
    /// <summary>
    /// Helper methods for rendering output using Spectre.Console.
    /// </summary>
    public static class HelpersSpectre {
        /// <summary>
        /// Writes a formatted rule describing a DNS query.
        /// </summary>
        /// <param name="queryType">Query type description.</param>
        /// <param name="name">Record name.</param>
        /// <param name="recordType">DNS record type.</param>
        /// <param name="endpoint">DNS endpoint.</param>
        /// <param name="dnsRequestFormat">Optional request format.</param>
        public static void AddLine(string queryType, string name, DnsRecordType recordType, DnsEndpoint endpoint, DnsRequestFormat? dnsRequestFormat = null) {
            if (dnsRequestFormat == null) {
                AnsiConsole.Write(new Rule($"[blue]{queryType}[/] on [yellow]{endpoint}[/] => [red]{name}[/] => [green]{recordType}[/]"));
            } else {
                AnsiConsole.Write(new Rule($"[blue]{queryType}[/] on [yellow]{endpoint}[/] => [red]{name}[/] => [green]{recordType}[/] => [yellow]{dnsRequestFormat}[/]"));
            }
        }

        /// <summary>
        /// Writes a formatted rule describing a DNS query when the record type is provided as a string.
        /// </summary>
        /// <param name="queryType">Query type description.</param>
        /// <param name="name">Record name.</param>
        /// <param name="recordType">DNS record type as string.</param>
        /// <param name="endpoint">DNS endpoint.</param>
        /// <param name="dnsRequestFormat">Optional request format.</param>
        public static void AddLine(string queryType, string name, string recordType, DnsEndpoint endpoint, DnsRequestFormat? dnsRequestFormat = null) {
            if (dnsRequestFormat == null) {
                AnsiConsole.Write(new Rule($"[blue]{queryType}[/] on [yellow]{endpoint}[/] => [red]{name}[/] => [green]{recordType}[/]"));
            } else {
                AnsiConsole.Write(new Rule($"[blue]{queryType}[/] on [yellow]{endpoint}[/] => [red]{name}[/] => [green]{recordType}[/] => [yellow]{dnsRequestFormat}[/]"));
            }
        }

        /// <summary>
        /// Writes a rule describing a DNS query to a specific host.
        /// </summary>
        /// <param name="queryType">Query type description.</param>
        /// <param name="name">Record name.</param>
        /// <param name="recordType">DNS record type.</param>
        /// <param name="hostName">Target host name.</param>
        /// <param name="dnsRequestFormat">Optional request format.</param>
        public static void AddLine(string queryType, string name, DnsRecordType recordType, string hostName, DnsRequestFormat? dnsRequestFormat = null) {
            if (dnsRequestFormat == null) {
                AnsiConsole.Write(new Rule($"[blue]{queryType}[/] on [yellow]{hostName}[/] => [red]{name}[/] => [green]{recordType}[/]"));
            } else {
                AnsiConsole.Write(new Rule($"[blue]{queryType}[/] on [yellow]{hostName}[/] => [red]{name}[/] => [green]{recordType}[/] => [yellow]{dnsRequestFormat}[/]"));
            }
        }

        /// <summary>
        /// Writes a rule describing a DNS query sent to a specified URI.
        /// </summary>
        /// <param name="queryType">Query type description.</param>
        /// <param name="name">Record name.</param>
        /// <param name="recordType">DNS record type.</param>
        /// <param name="uri">Destination URI.</param>
        /// <param name="dnsRequestFormat">Optional request format.</param>
        public static void AddLine(string queryType, string name, DnsRecordType recordType, Uri uri, DnsRequestFormat? dnsRequestFormat = null) {
            if (dnsRequestFormat == null) {
                AnsiConsole.Write(new Rule($"[blue]{queryType}[/] on [yellow]{uri}[/] => [red]{name}[/] => [green]{recordType}[/]"));
            } else {
                AnsiConsole.Write(new Rule($"[blue]{queryType}[/] on [yellow]{uri}[/] => [red]{name}[/] => [green]{recordType}[/] => [yellow]{dnsRequestFormat}[/]"));
            }
        }

        /// <summary>
        /// Renders a table summarizing the DNS response without server details.
        /// </summary>
        /// <param name="response">DNS response to display.</param>
        public static void DisplayTableAlternative(this DnsResponse response) {
            var table = new Table().Border(TableBorder.Rounded);
            table.AddColumn("Status");
            table.AddColumn("Questions");
            table.AddColumn("Answers");

            var questions = response.Questions.Length == 0
                ? string.Empty
                : string.Join(", ", response.Questions.Select(q => $"{q.Name} => {q.Type}"));

            foreach (var answer in response.Answers) {
                var answerTable = new Table().Border(TableBorder.Rounded);
                answerTable.AddColumn("Type");
                answerTable.AddColumn("TTL");
                answerTable.AddColumn("Name");
                answerTable.AddColumn("Data");
                answerTable.AddRow(new Markup(answer.Type.ToString()), new Markup(answer.TTL.ToString()), new Markup(answer.Name), new Markup(answer.Data));
                if (response.Status == DnsResponseCode.NoError) {
                    table.AddRow(new Markup($"[green]{response.Status}[/]"), new Markup(questions), answerTable);
                } else {
                    table.AddRow(new Markup($"[red]{response.Status}[/]"), new Markup(questions), answerTable);
                }
            }

            AnsiConsole.Write(table);
        }


        /// <summary>
        /// Renders a detailed table for a single DNS response including server information.
        /// </summary>
        /// <param name="response">DNS response to display.</param>
        public static void DisplayTable(this DnsResponse response) {
            var table = new Table().Border(TableBorder.Rounded);
            table.AddColumn("Status");
            table.AddColumn("Questions");
            table.AddColumn("Server");
            table.AddColumn("Answers");

            var questions = response.Questions.Length == 0
                ? string.Empty
                : string.Join(", ", response.Questions.Select(q => $"{q.Name} => {q.Type}"));
            var server = response.Questions.Length == 0
                ? string.Empty
                : string.Join(Environment.NewLine, response.Questions.Select(q => $"HostName: {q.HostName}{Environment.NewLine}Port: {q.Port}{Environment.NewLine}RequestFormat: {q.RequestFormat}{Environment.NewLine}BaseUri: {q.BaseUri}"));

            var answerTable = new Table().Border(TableBorder.Rounded);
            answerTable.AddColumn("Type");
            answerTable.AddColumn("TTL");
            answerTable.AddColumn("Name");
            answerTable.AddColumn("Data");

            foreach (var answer in response.Answers) {
                answerTable.AddRow(new Markup(answer.Type.ToString()), new Markup(answer.TTL.ToString()), new Markup(answer.Name), new Markup(answer.Data));
            }

            if (response.Status == DnsResponseCode.NoError) {
                table.AddRow(new Markup($"[green]{response.Status}[/]"), new Markup(questions), new Markup(server), answerTable);
            } else {
                table.AddRow(new Markup($"[red]{response.Status}[/]"), new Markup(questions), new Markup(server), answerTable);
            }
            AnsiConsole.Write(table);
        }

        /// <summary>
        /// Displays each DNS response in an individual table.
        /// </summary>
        public static void DisplayTable(this DnsResponse[] responses) {
            foreach (var response in responses) {
                response.DisplayTable();
            }
        }

        /// <summary>
        /// Renders a table for a collection of DNS answers.
        /// </summary>
        public static void DisplayTable(this DnsAnswer[] answers) {
            var table = new Table().Border(TableBorder.Rounded);
            table.AddColumn("Type");
            table.AddColumn("TTL");
            table.AddColumn("Name");
            table.AddColumn("Data");

            foreach (var answer in answers) {
                table.AddRow(new Markup(answer.Type.ToString()), new Markup(answer.TTL.ToString()), new Markup(answer.Name), new Markup(answer.Data));
            }

            AnsiConsole.Write(table);
        }
        /// <summary>
        /// Renders a table for a single DNS answer.
        /// </summary>
        public static void DisplayTable(this DnsAnswer answer) {
            var table = new Table().Border(TableBorder.Rounded);
            table.AddColumn("Type");
            table.AddColumn("TTL");
            table.AddColumn("Name");
            table.AddColumn("Data");

            table.AddRow(new Markup(answer.Type.ToString()), new Markup(answer.TTL.ToString()), new Markup(answer.Name), new Markup(answer.Data));

            AnsiConsole.Write(table);
        }

        /// <summary>
        /// Displays a table listing DNS questions that were executed.
        /// </summary>
        public static void DisplayTable(this DnsQuestion[] questions) {
            var table = new Table().Border(TableBorder.Rounded);
            table.AddColumn("Name");
            table.AddColumn("Type");
            table.AddColumn("HostName");
            table.AddColumn("Port");
            table.AddColumn("RequestFormat");
            table.AddColumn("BaseUri");
            foreach (var question in questions) {
                table.AddRow(
                    new Markup(question.Name),
                    new Markup(question.Type.ToString()),
                    new Markup(question.HostName),
                    new Markup(question.Port.ToString()),
                    new Markup(question.RequestFormat.ToString()),
                    new Markup(question.BaseUri?.ToString() ?? string.Empty));
            }
            AnsiConsole.Write(table);
        }
    }
}
