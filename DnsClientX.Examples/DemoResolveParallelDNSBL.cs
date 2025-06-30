using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace DnsClientX.Examples {
    public static class DemoResolveParallelBlackList {
        public static List<string> dnsBlacklist { get; } = [
            "all.s5h.net",
            "auth.spamrats.com",
            "b.barracudacentral.org",
            "bad.virusfree.cz",
            "badconf.rhsbl.sorbs.net",
            "bip.virusfree.cz",
            "bl.0spam.org",
            "bl.blocklist.de",
            "bl.deadbeef.com",
            "bl.mailspike.org",
            "bl.nordspam.com",
            "bl.spamcop.net",
            "black.dnsbl.brukalai.lt",
            "black.mail.abusix.zone",
            "blackholes.five-ten-sg.com",
            "blacklist.woody.ch",
            "block.dnsbl.sorbs.net",
            "bogons.cymru.com",
            "cbl.abuseat.org",
            "combined.abuse.ch",
            "combined.mail.abusix.zone",
            "combined.rbl.msrbl.net",
            "db.wpbl.info",
            "dbl.0spam.org",
            "dbl.nordspam.com",
            "dbl.spamhaus.org",
            "dblack.mail.abusix.zone",
            "diskhash.mail.abusix.zone",
            "dnsbl.cyberlogic.net",
            "dnsbl.dronebl.org",
            "dnsbl.inps.de",
            "dnsbl.justspam.org",
            "dnsbl.sorbs.net",
            "dnsbl-1.uceprotect.net",
            "dnsbl-2.uceprotect.net",
            "dnsbl-3.uceprotect.net",
            "drone.abuse.ch",
            "duinv.aupads.org",
            "dul.dnsbl.sorbs.net",
            "dul.ru",
            "dyna.spamrats.com",
            "dynamic.mail.abusix.zone",
            "escalations.dnsbl.sorbs.net",
            "exploit.mail.abusix.zone",
            "hbl.spamhaus.org",
            "hostkarma.junkemailfilter.com",
            "http.dnsbl.sorbs.net",
            "images.rbl.msrbl.net",
            "ips.backscatterer.org",
            "ix.dnsbl.manitu.net",
            "key.authbl.dq.spamhaus.net",
            "korea.services.net",
            "misc.dnsbl.sorbs.net",
            "nbl.0spam.org",
            "new.spam.dnsbl.sorbs.net",
            "nod.mail.abusix.zone",
            "nomail.rhsbl.sorbs.net",
            "noptr.spamrats.com",
            "noservers.dnsbl.sorbs.net",
            "ohps.dnsbl.net.au",
            "old.spam.dnsbl.sorbs.net",
            "omrs.dnsbl.net.au",
            "orvedb.aupads.org",
            "osps.dnsbl.net.au",
            "osrs.dnsbl.net.au",
            "owfs.dnsbl.net.au",
            "owps.dnsbl.net.au",
            "pbl.spamhaus.org",
            "phishing.rbl.msrbl.net",
            "probes.dnsbl.net.au",
            "proxy.bl.gweep.ca",
            "proxy.block.transip.nl",
            "psbl.surriel.com",
            "rbl.0spam.org",
            "rbl.interserver.net",
            "rbl.metunet.com",
            "rdts.dnsbl.net.au",
            "recent.spam.dnsbl.sorbs.net",
            "relays.bl.gweep.ca",
            "relays.bl.kundenserver.de",
            "relays.nether.net",
            "residential.block.transip.nl",
            "rhsbl.sorbs.net",
            "ricn.dnsbl.net.au",
            "rmst.dnsbl.net.au",
            "safe.dnsbl.sorbs.net",
            "sbl.spamhaus.org",
            "short.rbl.jp",
            "shorthash.mail.abusix.zone",
            "singular.ttk.pte.hu",
            "smtp.dnsbl.sorbs.net",
            "socks.dnsbl.sorbs.net",
            "spam.abuse.ch",
            "spam.dnsbl.anonmails.de",
            "spam.dnsbl.sorbs.net",
            "spam.rbl.msrbl.net",
            "spam.spamrats.com",
            "spambot.bls.digibase.ca",
            "spamlist.or.kr",
            "spamrbl.imp.ch",
            "spamsources.fabel.dk",
            "t3direct.dnsbl.net.au",
            "ubl.lashback.com",
            "ubl.unsubscore.com",
            "virbl.bit.nl",
            "virus.rbl.jp",
            "virus.rbl.msrbl.net",
            "web.dnsbl.sorbs.net",
            "wormrbl.imp.ch",
            "xbl.spamhaus.org",
            "z.mailspike.net",
            "zen.spamhaus.org",
            "zombie.dnsbl.sorbs.net"
        ];

        public static async Task Example() {
            var endpoint = DnsEndpoint.OpenDNS;
            string ipAddress = "89.74.48.96";

            // Reverse the IP address and append the DNSBL list
            string reversedIp = string.Join(".", ipAddress.Split('.').Reverse());

            List<string> queries = new List<string>();
            foreach (var dnsbl in dnsBlacklist) {
                string query = $"{reversedIp}.{dnsbl}";
                queries.Add(query);
            }

            Stopwatch stopwatch = new Stopwatch();

            // Start the stopwatch before the operation
            stopwatch.Start();

            using var client = new ClientX(endpoint) {
                Debug = false
            };

            HelpersSpectre.AddLine("Resolve (Parallel)", $"{ipAddress} => {queries.Count} queries", DnsRecordType.A, endpoint);
            var responses = await client.Resolve(queries.ToArray(), DnsRecordType.A);
            stopwatch.Stop();
            HelpersSpectre.AddLine($"Time to resolve {stopwatch.ElapsedMilliseconds} ms", $"{ipAddress} => {queries.Count} queries", DnsRecordType.A, endpoint);
            responses.DisplayTable();
        }
    }
}
