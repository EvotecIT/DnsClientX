using System;
using System.Linq;
using System.Net;
using System.Threading.Tasks;

using DnsClientX;

namespace DnsClientX.Examples;

/// <summary>
/// Example demonstrating DNS answer parsing helpers.
/// </summary>
internal class DemoDnsAnswer {
    /// <summary>Runs the demo.</summary>
    public static async Task ExampleDnsAnswerParsing() {
        await Task.CompletedTask;
        var dkimRecord = "v=DKIM1; k=rsa; p=MIGfMA0GCSqGSIb3DQEBAQUAA4GNADCBiQKBgQCqrIpQkyykYEQbNzvHfgGsiYfoyX3b3Z6CPMHa5aNn/Bd8skLaqwK9vj2fHn70DA+X67L/pV2U5VYDzb5AUfQeD6NPDwZ7zLRc0XtX+5jyHWhHueSQT8uo6acMA+9JrVHdRfvtlQo8Oag8SLIkhaUea3xqZpijkQR/qHmo3GIfnQIDAQAB;";
        var answer = new DnsAnswer {
            DataRaw = dkimRecord,
            Type = DnsRecordType.TXT
        };
        Settings.Logger.WriteInformation(answer.Data);
    }
}
