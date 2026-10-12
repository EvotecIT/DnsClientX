using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

// A loopback DNS peer exercises the actual packaged cmdlets without public DNS dependencies.
public sealed class DnsWorkflowServer : IDisposable {
    private readonly UdpClient socket = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
    private readonly ConcurrentQueue<string> queries = new ConcurrentQueue<string>();
    private readonly ConcurrentBag<Task> replies = new ConcurrentBag<Task>();
    private readonly Task receiver;
    private volatile bool stopped;

    public int Port { get { return ((IPEndPoint)socket.Client.LocalEndPoint).Port; } }
    public string Endpoint { get { return "udp@127.0.0.1:" + Port; } }
    public string[] Queries { get { return queries.ToArray(); } }
    public string Address { get; set; }

    public DnsWorkflowServer() {
        Address = "192.0.2.10";
        socket.Client.ReceiveTimeout = 100;
        receiver = Task.Run((Action)Receive);
    }

    private void Receive() {
        while (!stopped) {
            try {
                IPEndPoint sender = null;
                byte[] query = socket.Receive(ref sender);
                // Closing a blocked socket can return an empty packet on some runtimes.
                if (stopped) {
                    return;
                }
                if (query.Length < 17) {
                    continue;
                }
                int offset = 12;
                string name;
                if (!TryReadName(query, ref offset, out name) || query.Length - offset < 4) {
                    continue;
                }
                int type = query[offset] * 256 + query[offset + 1];
                int questionEnd = offset + 4;
                queries.Enqueue(name);
                if (name.EndsWith("cancel.test", StringComparison.OrdinalIgnoreCase)
                    && !name.StartsWith("_services.", StringComparison.OrdinalIgnoreCase)) continue;
                IPEndPoint replyTarget = sender;
                replies.Add(Task.Run(() => Reply(query, questionEnd, name, type, replyTarget)));
            } catch (SocketException) {
                if (stopped) return;
            } catch (ObjectDisposedException) { return; }
        }
    }

    private void Reply(byte[] query, int questionEnd, string name, int type, IPEndPoint sender) {
        if (name == "slow.test") Thread.Sleep(300);
        if (stopped) return;
        var records = new List<byte[]>();
        bool failed = name == "fail.test";
        if (!failed && !name.EndsWith("empty.test", StringComparison.OrdinalIgnoreCase)) {
            if (type == 12) {
                if (name.StartsWith("_services.", StringComparison.OrdinalIgnoreCase)) {
                    string domain = name.Substring("_services._dns-sd._udp.".Length);
                    records.Add(Record(type, Name("_http._tcp." + domain)));
                    records.Add(Record(type, Name("_ssh._tcp." + domain)));
                } else {
                    records.Add(Record(type, Name("Console." + name)));
                }
            } else if (type == 33) {
                using (var data = new MemoryStream()) {
                    Write16(data, 0); Write16(data, 0); Write16(data, name.Contains("_ssh.") ? 22 : 8080);
                    byte[] target = Name("target.test"); data.Write(target, 0, target.Length);
                    records.Add(Record(type, data.ToArray()));
                }
            } else if (type == 16) {
                byte[] text = Encoding.ASCII.GetBytes("path=/status");
                var data = new byte[text.Length + 1]; data[0] = (byte)text.Length;
                Array.Copy(text, 0, data, 1, text.Length);
                records.Add(Record(type, data));
            } else if (type == 1) {
                records.Add(Record(type, IPAddress.Parse(Address).GetAddressBytes()));
                if (name == "rows.test") records.Add(Record(type, IPAddress.Parse("192.0.2.11").GetAddressBytes()));
            } else if (type == 28) {
                records.Add(Record(type, IPAddress.Parse("2001:db8::10").GetAddressBytes()));
            }
        }
        using (var response = new MemoryStream()) {
            response.WriteByte(query[0]); response.WriteByte(query[1]);
            response.WriteByte(0x81); response.WriteByte(failed ? (byte)0x82 : (byte)0x80);
            Write16(response, 1); Write16(response, records.Count); Write16(response, 0); Write16(response, 0);
            response.Write(query, 12, questionEnd - 12);
            foreach (byte[] record in records) response.Write(record, 0, record.Length);
            byte[] bytes = response.ToArray();
            try { socket.Send(bytes, bytes.Length, sender); }
            catch (ObjectDisposedException) { }
            catch (SocketException) { if (!stopped) throw; }
        }
    }

    private static byte[] Record(int type, byte[] data) {
        using (var stream = new MemoryStream()) {
            Write16(stream, 0xc00c); Write16(stream, type); Write16(stream, 1);
            stream.Write(new byte[] { 0, 0, 0, 120 }, 0, 4);
            Write16(stream, data.Length); stream.Write(data, 0, data.Length);
            return stream.ToArray();
        }
    }

    private static bool TryReadName(byte[] packet, ref int offset, out string name) {
        name = string.Empty;
        var parts = new List<string>();
        while (offset < packet.Length) {
            int length = packet[offset++];
            if (length == 0) {
                name = string.Join(".", parts);
                return true;
            }
            if (length > 63 || length > packet.Length - offset) {
                return false;
            }
            parts.Add(Encoding.ASCII.GetString(packet, offset, length)); offset += length;
        }
        return false;
    }

    private static byte[] Name(string name) {
        using (var stream = new MemoryStream()) {
            foreach (string part in name.Split('.')) {
                byte[] bytes = Encoding.ASCII.GetBytes(part); stream.WriteByte((byte)bytes.Length);
                stream.Write(bytes, 0, bytes.Length);
            }
            stream.WriteByte(0); return stream.ToArray();
        }
    }

    private static void Write16(Stream stream, int value) {
        stream.WriteByte((byte)(value >> 8)); stream.WriteByte((byte)value);
    }

    public void Dispose() {
        stopped = true;
        socket.Close();
        receiver.GetAwaiter().GetResult();
        Task.WaitAll(replies.ToArray());
        socket.Dispose();
    }
}
