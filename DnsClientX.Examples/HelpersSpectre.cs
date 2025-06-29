using System;
using System.Linq;
using System.Net;
using Spectre.Console;

namespace DnsClientX.Examples {
    public static class HelpersSpectre {
        public static void AddLine(string queryType, string name, DnsRecordType recordType, DnsEndpoint endpoint, DnsRequestFormat? dnsRequestFormat = null) {
            if (dnsRequestFormat == null) {
                AnsiConsole.Write(new Rule($"[blue]{queryType}[/] on [yellow]{endpoint}[/] => [red]{name}[/] => [green]{recordType}[/]"));
            } else {
                AnsiConsole.Write(new Rule($"[blue]{queryType}[/] on [yellow]{endpoint}[/] => [red]{name}[/] => [green]{recordType}[/] => [yellow]{dnsRequestFormat}[/]"));
            }
        }

        public static void AddLine(string queryType, string name, string recordType, DnsEndpoint endpoint, DnsRequestFormat? dnsRequestFormat = null) {
            if (dnsRequestFormat == null) {
                AnsiConsole.Write(new Rule($"[blue]{queryType}[/] on [yellow]{endpoint}[/] => [red]{name}[/] => [green]{recordType}[/]"));
            } else {
                AnsiConsole.Write(new Rule($"[blue]{queryType}[/] on [yellow]{endpoint}[/] => [red]{name}[/] => [green]{recordType}[/] => [yellow]{dnsRequestFormat}[/]"));
            }
        }

        public static void AddLine(string queryType, string name, DnsRecordType recordType, string hostName, DnsRequestFormat? dnsRequestFormat = null) {
            if (dnsRequestFormat == null) {
                AnsiConsole.Write(new Rule($"[blue]{queryType}[/] on [yellow]{hostName}[/] => [red]{name}[/] => [green]{recordType}[/]"));
            } else {
                AnsiConsole.Write(new Rule($"[blue]{queryType}[/] on [yellow]{hostName}[/] => [red]{name}[/] => [green]{recordType}[/] => [yellow]{dnsRequestFormat}[/]"));
            }
        }

        public static void AddLine(string queryType, string name, DnsRecordType recordType, Uri uri, DnsRequestFormat? dnsRequestFormat = null) {
            if (dnsRequestFormat == null) {
                AnsiConsole.Write(new Rule($"[blue]{queryType}[/] on [yellow]{uri}[/] => [red]{name}[/] => [green]{recordType}[/]"));
            } else {
                AnsiConsole.Write(new Rule($"[blue]{queryType}[/] on [yellow]{uri}[/] => [red]{name}[/] => [green]{recordType}[/] => [yellow]{dnsRequestFormat}[/]"));
            }
        }

        public static void DisplayTableAlternative(this DnsResponse response) {
            var table = new Table().Border(TableBorder.Rounded);
            table.AddColumn("Status");
            table.AddColumn("Questions");
            table.AddColumn("Answers");

            var questions = response.Questions == null
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


        public static void DisplayTable(this DnsResponse response) {
            var table = new Table().Border(TableBorder.Rounded);
            table.AddColumn("Status");
            table.AddColumn("Questions");
            table.AddColumn("Server");
            table.AddColumn("Answers");

            var questions = response.Questions == null
                ? string.Empty
                : string.Join(", ", response.Questions.Select(q => $"{q.Name} => {q.Type}"));
            var server = response.Questions == null
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

        public static void DisplayTable(this DnsResponse[] responses) {
            foreach (var response in responses) {
                response.DisplayTable();
            }
        }

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
        public static void DisplayTable(this DnsAnswer answer) {
            var table = new Table().Border(TableBorder.Rounded);
            table.AddColumn("Type");
            table.AddColumn("TTL");
            table.AddColumn("Name");
            table.AddColumn("Data");

            table.AddRow(new Markup(answer.Type.ToString()), new Markup(answer.TTL.ToString()), new Markup(answer.Name), new Markup(answer.Data));

            AnsiConsole.Write(table);
        }

        public static void DisplayTable(this DnsQuestion[] questions) {
            var table = new Table().Border(TableBorder.Rounded);
            table.AddColumn("Name");
            table.AddColumn("Type");
            table.AddColumn("HostName");
            table.AddColumn("Port");
            table.AddColumn("RequestFormat");
            table.AddColumn("BaseUri");
            foreach (var question in questions) {
                table.AddRow(new Markup(question.Name), new Markup(question.Type.ToString()), new Markup(question.HostName), new Markup(question.Port.ToString()), new Markup(question.RequestFormat.ToString()), new Markup(question.BaseUri.ToString()));
            }
            AnsiConsole.Write(table);
        }
    }
}
