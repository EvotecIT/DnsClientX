using System;
using System.Buffers.Binary;
using System.IO;
using System.Collections.Generic;
using System.Net;
using System.Text;
using System.Security.Cryptography;
using System.Linq;

namespace DnsClientX {
    /// <summary>
    /// DnsMessage class
    /// </summary>
    public class DnsMessage {
        private readonly string _name;
        private readonly DnsRecordType _type;
        private readonly bool _requestDnsSec;
        private readonly bool _enableEdns;
        private readonly int _udpBufferSize;
        private readonly string? _subnet;
        private readonly bool _checkingDisabled;
        private readonly AsymmetricAlgorithm? _signingKey;
        private readonly EdnsOption[] _ednsOptions;

        /// <summary>
        /// Initializes a new instance of the <see cref="DnsMessage"/> class.
        /// </summary>
        /// <param name="name">The name.</param>
        /// <param name="type">The type.</param>
        /// <param name="requestDnsSec">if set to <c>true</c> [request DNS sec].</param>
        public DnsMessage(string name, DnsRecordType type, bool requestDnsSec)
            : this(name, type, requestDnsSec, requestDnsSec, 4096, null, false, null, null) {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="DnsMessage"/> class with advanced options.
        /// </summary>
        /// <param name="name">Domain name to query.</param>
        /// <param name="type">Record type to query.</param>
        /// <param name="requestDnsSec">Whether DNSSEC records should be requested.</param>
        /// <param name="enableEdns">Enable EDNS OPT record.</param>
        /// <param name="udpBufferSize">UDP buffer size for EDNS.</param>
        /// <param name="subnet">Optional EDNS client subnet.</param>
        /// <param name="checkingDisabled">Whether to set the CD bit in OPT TTL.</param>
        public DnsMessage(string name, DnsRecordType type, bool requestDnsSec, bool enableEdns, int udpBufferSize, string? subnet, bool checkingDisabled, AsymmetricAlgorithm? signingKey, System.Collections.Generic.IEnumerable<EdnsOption>? options = null) {
            _name = name;
            _type = type;
            _requestDnsSec = requestDnsSec;
            _ednsOptions = options?.ToArray() ?? Array.Empty<EdnsOption>();
            _enableEdns = enableEdns || requestDnsSec || !string.IsNullOrEmpty(subnet) || checkingDisabled || _ednsOptions.Length > 0;
            _udpBufferSize = udpBufferSize;
            _subnet = subnet;
            _checkingDisabled = checkingDisabled;
            _signingKey = signingKey;
        }

        /// <summary>
        /// Converts DNS message to base64url format
        /// </summary>
        /// <returns></returns>
        public string ToBase64Url() {
            using var stream = new MemoryStream();

            // Temporary buffer
            Span<byte> buffer = stackalloc byte[2];

            // Write the ID
            Random random = new Random();
            ushort randomId = (ushort)random.Next(ushort.MinValue, ushort.MaxValue);
            BinaryPrimitives.WriteUInt16BigEndian(buffer, randomId);
            //BinaryPrimitives.WriteUInt16BigEndian(buffer, 0xABCD);
            stream.Write(buffer.ToArray(), 0, buffer.Length);

            //BinaryPrimitives.WriteUInt16BigEndian(buffer, 1);
            //stream.Write(buffer.ToArray(), 0, buffer.Length);

            // Write the flags
            BinaryPrimitives.WriteUInt16BigEndian(buffer, 0x0100);
            stream.Write(buffer.ToArray(), 0, buffer.Length);

            // Write the flags
            //BinaryPrimitives.WriteUInt16BigEndian(buffer, 0x0000);
            //stream.Write(buffer.ToArray(), 0, buffer.Length);

            // Write the question count
            BinaryPrimitives.WriteUInt16BigEndian(buffer, 1);
            stream.Write(buffer.ToArray(), 0, buffer.Length);

            // Write the answer count
            BinaryPrimitives.WriteUInt16BigEndian(buffer, 0);
            stream.Write(buffer.ToArray(), 0, buffer.Length);

            // Write the authority count
            BinaryPrimitives.WriteUInt16BigEndian(buffer, 0);
            stream.Write(buffer.ToArray(), 0, buffer.Length);

            // Write the additional count
            ushort additional = (ushort)((_enableEdns ? 1 : 0) + (_signingKey != null ? 1 : 0));
            BinaryPrimitives.WriteUInt16BigEndian(buffer, additional);
            stream.Write(buffer.ToArray(), 0, buffer.Length);

            // Write the question name
            foreach (var label in _name.Split('.')) {
                var labelBytes = Encoding.ASCII.GetBytes(label);
                stream.WriteByte((byte)labelBytes.Length); // Write the length of the label
                stream.Write(labelBytes, 0, labelBytes.Length);
            }
            stream.WriteByte(0); // End of name

            // Write the question type
            BinaryPrimitives.WriteUInt16BigEndian(buffer, (ushort)_type);
            stream.Write(buffer.ToArray(), 0, buffer.Length);

            // Write the question class
            BinaryPrimitives.WriteUInt16BigEndian(buffer, 1);
            stream.Write(buffer.ToArray(), 0, buffer.Length);

            if (_enableEdns) {
                byte[] optionData = BuildOptions();
                stream.WriteByte(0);
                BinaryPrimitives.WriteUInt16BigEndian(buffer, (ushort)DnsRecordType.OPT);
                stream.Write(buffer.ToArray(), 0, buffer.Length);
                BinaryPrimitives.WriteUInt16BigEndian(buffer, (ushort)_udpBufferSize);
                stream.Write(buffer.ToArray(), 0, buffer.Length);
                Span<byte> ttl = stackalloc byte[4];
                uint ttlFlags = 0u;
                if (_requestDnsSec) ttlFlags |= 0x00008000u;
                if (_checkingDisabled) ttlFlags |= 0x00000010u;
                BinaryPrimitives.WriteUInt32BigEndian(ttl, ttlFlags);
                stream.Write(ttl.ToArray(), 0, ttl.Length);
                BinaryPrimitives.WriteUInt16BigEndian(buffer, (ushort)optionData.Length);
                stream.Write(buffer.ToArray(), 0, buffer.Length);
                if (optionData.Length > 0) {
                    stream.Write(optionData, 0, optionData.Length);
                }
            }

            if (_signingKey != null) {
                byte[] toSign = stream.ToArray();
                byte[] signature;
                if (_signingKey is RSA rsa) {
                    signature = rsa.SignData(toSign, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
                } else if (_signingKey is ECDsa ecdsa) {
                    signature = ecdsa.SignData(toSign, HashAlgorithmName.SHA256);
                } else {
                    signature = Array.Empty<byte>();
                }
                stream.WriteByte(0);
                BinaryPrimitives.WriteUInt16BigEndian(buffer, (ushort)DnsRecordType.SIG);
                stream.Write(buffer.ToArray(), 0, buffer.Length);
                BinaryPrimitives.WriteUInt16BigEndian(buffer, 255); // class ANY
                stream.Write(buffer.ToArray(), 0, buffer.Length);
                Span<byte> ttlSig = stackalloc byte[4];
                BinaryPrimitives.WriteUInt32BigEndian(ttlSig, 0u);
                stream.Write(ttlSig.ToArray(), 0, ttlSig.Length);
                BinaryPrimitives.WriteUInt16BigEndian(buffer, (ushort)signature.Length);
                stream.Write(buffer.ToArray(), 0, buffer.Length);
                if (signature.Length > 0) {
                    stream.Write(signature, 0, signature.Length);
                }
            }

            // Convert to base64url format
            var dnsMessageBytes = stream.ToArray();
            if (dnsMessageBytes.Length == 0) {
                return string.Empty;
            }

#if NETSTANDARD2_0 || NET472
            string base64Url = Convert.ToBase64String(dnsMessageBytes)
                .TrimEnd('=')
                .Replace('+', '-')
                .Replace('/', '_');

            return base64Url;
#else
            int base64Length = ((dnsMessageBytes.Length + 2) / 3) * 4;
            char[] base64 = new char[base64Length];
            Convert.TryToBase64Chars(dnsMessageBytes, base64, out int charsWritten);

            int padding = (3 - (dnsMessageBytes.Length % 3)) % 3;
            int finalLength = charsWritten - padding;

            return string.Create(finalLength, base64, static (span, value) => {
                for (int i = 0; i < span.Length; i++) {
                    char c = value[i];
                    span[i] = c switch { '+' => '-', '/' => '_', _ => c };
                }
            });
#endif
        }

        /// <summary>
        /// Serializes the DNS wire format
        /// </summary>
        /// <returns></returns>
        public byte[] SerializeDnsWireFormat() {
            using (var ms = new MemoryStream()) {
                // Transaction ID
                Random random = new Random();
                ushort randomId = (ushort)random.Next(ushort.MinValue, ushort.MaxValue);
                var bytes = BitConverter.GetBytes(IPAddress.HostToNetworkOrder((short)randomId));
                ms.Write(bytes, 0, bytes.Length);

                // Flags
                bytes = BitConverter.GetBytes(IPAddress.HostToNetworkOrder((short)0x0100)); // Standard query
                ms.Write(bytes, 0, bytes.Length);

                // Questions
                bytes = BitConverter.GetBytes(IPAddress.HostToNetworkOrder((short)1));
                ms.Write(bytes, 0, bytes.Length);

                // Answer RRs
                bytes = BitConverter.GetBytes(IPAddress.HostToNetworkOrder((short)0));
                ms.Write(bytes, 0, bytes.Length);

                // Authority RRs
                bytes = BitConverter.GetBytes(IPAddress.HostToNetworkOrder((short)0));
                ms.Write(bytes, 0, bytes.Length);

                // Additional RRs
                int additional = (_enableEdns ? 1 : 0) + (_signingKey != null ? 1 : 0);
                bytes = BitConverter.GetBytes(IPAddress.HostToNetworkOrder((short)additional));
                ms.Write(bytes, 0, bytes.Length);

                // Queries
                foreach (var part in _name.Split('.')) {
                    ms.WriteByte((byte)part.Length);
                    var partBytes = Encoding.ASCII.GetBytes(part);
                    ms.Write(partBytes, 0, partBytes.Length);
                }
                ms.WriteByte((byte)0); // End of name

                // Type
                bytes = BitConverter.GetBytes(IPAddress.HostToNetworkOrder((short)_type));
                ms.Write(bytes, 0, bytes.Length);

                // Class
                bytes = BitConverter.GetBytes(IPAddress.HostToNetworkOrder((short)1)); // IN
                ms.Write(bytes, 0, bytes.Length);

                if (_enableEdns)
                {
                    byte[] optionData = BuildOptions();
                    ms.WriteByte(0);
                    bytes = BitConverter.GetBytes(IPAddress.HostToNetworkOrder((short)DnsRecordType.OPT));
                    ms.Write(bytes, 0, bytes.Length);
                    bytes = BitConverter.GetBytes(IPAddress.HostToNetworkOrder((short)_udpBufferSize));
                    ms.Write(bytes, 0, bytes.Length);
                    uint ttlFlags = 0u;
                    if (_requestDnsSec) ttlFlags |= 0x00008000u;
                    if (_checkingDisabled) ttlFlags |= 0x00000010u;
                    bytes = BitConverter.GetBytes(IPAddress.HostToNetworkOrder((int)ttlFlags));
                    ms.Write(bytes, 0, bytes.Length);
                    bytes = BitConverter.GetBytes(IPAddress.HostToNetworkOrder((short)optionData.Length));
                    ms.Write(bytes, 0, bytes.Length);
                    if (optionData.Length > 0)
                    {
                        ms.Write(optionData, 0, optionData.Length);
                    }
                }

                if (_signingKey != null)
                {
                    byte[] toSign = ms.ToArray();
                    byte[] signature;
                    if (_signingKey is RSA rsa)
                    {
                        signature = rsa.SignData(toSign, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
                    }
                    else if (_signingKey is ECDsa ecdsa)
                    {
                        signature = ecdsa.SignData(toSign, HashAlgorithmName.SHA256);
                    }
                    else
                    {
                        signature = Array.Empty<byte>();
                    }
                    ms.WriteByte(0);
                    bytes = BitConverter.GetBytes(IPAddress.HostToNetworkOrder((short)DnsRecordType.SIG));
                    ms.Write(bytes, 0, bytes.Length);
                    bytes = BitConverter.GetBytes(IPAddress.HostToNetworkOrder((short)255));
                    ms.Write(bytes, 0, bytes.Length);
                    bytes = BitConverter.GetBytes(IPAddress.HostToNetworkOrder(0));
                    ms.Write(bytes, 0, bytes.Length);
                    bytes = BitConverter.GetBytes(IPAddress.HostToNetworkOrder((short)signature.Length));
                    ms.Write(bytes, 0, bytes.Length);
                    if (signature.Length > 0)
                    {
                        ms.Write(signature, 0, signature.Length);
                    }
                }

                return ms.ToArray();
            }
        }

        private byte[] BuildOptions() {
            using var ms = new MemoryStream();
            if (_subnet != null) {
                var ecs = new EcsOption(_subnet);
                byte[] bytes = ecs.ToByteArray();
                ms.Write(bytes, 0, bytes.Length);
            }

            foreach (var opt in _ednsOptions) {
                byte[] bytes = opt.ToByteArray();
                ms.Write(bytes, 0, bytes.Length);
            }

            return ms.ToArray();
        }
    }
}
