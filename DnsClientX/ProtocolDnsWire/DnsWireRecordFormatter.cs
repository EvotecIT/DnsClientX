using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net;
using System.Text;

namespace DnsClientX {
    internal static class DnsWireRecordFormatter {
        internal static string Format(byte[] message, DnsRecordType type, int offset, ushort length) {
            var reader = new DnsWireReader(message, offset, checked(offset + length));
            try {
                switch (type) {
                    case DnsRecordType.A:
                        RequireLength(length, 4, type);
                        return new IPAddress(reader.ReadBytes(4)).ToString();
                    case DnsRecordType.AAAA:
                        RequireLength(length, 16, type);
                        return new IPAddress(reader.ReadBytes(16)).ToString();
                    case DnsRecordType.NS:
                    case DnsRecordType.CNAME:
                    case DnsRecordType.DNAME:
                    case DnsRecordType.PTR:
                    case DnsRecordType.MB:
                    case DnsRecordType.MD:
                    case DnsRecordType.MF:
                    case DnsRecordType.MG:
                    case DnsRecordType.MR:
                        return ReadNameOnly(reader, type);
                    case DnsRecordType.MX:
                    case DnsRecordType.AFSDB:
                    case DnsRecordType.RT:
                    case DnsRecordType.KX:
                        return $"{reader.ReadUInt16()} {ReadNameOnly(reader, type)}";
                    case DnsRecordType.SOA:
                        return FormatSoa(reader);
                    case DnsRecordType.TXT:
                    case DnsRecordType.SPF:
                        return FormatCharacterStrings(reader);
                    case DnsRecordType.HINFO:
                        return FormatHinfo(reader);
                    case DnsRecordType.MINFO:
                    case DnsRecordType.RP:
                        return $"{reader.ReadName()} {ReadNameOnly(reader, type)}";
                    case DnsRecordType.SRV:
                        return $"{reader.ReadUInt16()} {reader.ReadUInt16()} {reader.ReadUInt16()} {ReadNameOnly(reader, type)}";
                    case DnsRecordType.NAPTR:
                        return FormatNaptr(reader);
                    case DnsRecordType.CAA:
                        return FormatCaa(reader);
                    case DnsRecordType.URI:
                        return FormatUri(reader);
                    case DnsRecordType.LOC:
                        return FormatLoc(reader);
                    case DnsRecordType.DNSKEY:
                    case DnsRecordType.CDNSKEY:
                        return FormatDnsKey(reader);
                    case DnsRecordType.DS:
                    case DnsRecordType.CDS:
                    case DnsRecordType.DLV:
                    case DnsRecordType.TA:
                        return FormatDs(reader);
                    case DnsRecordType.RRSIG:
                    case DnsRecordType.SIG:
                        return FormatRrsig(reader);
                    case DnsRecordType.NSEC:
                        return FormatNsec(reader);
                    case DnsRecordType.NSEC3:
                        return FormatNsec3(reader);
                    case DnsRecordType.NSEC3PARAM:
                        return FormatNsec3Param(reader);
                    case DnsRecordType.SSHFP:
                        return FormatSshfp(reader);
                    case DnsRecordType.TLSA:
                    case DnsRecordType.SMIMEA:
                        return FormatTlsa(reader);
                    case DnsRecordType.SVCB:
                    case DnsRecordType.HTTPS:
                        return FormatSvcb(reader);
                    case DnsRecordType.OPT:
                        return FormatUnknown(reader);
                    default:
                        return FormatUnknown(reader);
                }
            } catch (DnsClientException) {
                throw;
            } catch (Exception ex) {
                throw new DnsClientException($"Invalid {type} RDATA: {ex.Message}", ex);
            }
        }

        internal static string FormatClientSubnet(byte[] data) {
            if (data.Length < 4) throw new DnsClientException("EDNS Client Subnet option is shorter than four bytes.");
            ushort family = (ushort)((data[0] << 8) | data[1]);
            byte sourcePrefix = data[2];
            byte scopePrefix = data[3];
            int addressLength = data.Length - 4;
            int maxBits = family == 1 ? 32 : family == 2 ? 128 : -1;
            if (maxBits < 0 || sourcePrefix > maxBits || scopePrefix > maxBits || addressLength != (sourcePrefix + 7) / 8) {
                throw new DnsClientException("EDNS Client Subnet option contains an invalid family, prefix, or address length.");
            }
            var address = new byte[family == 1 ? 4 : 16];
            Buffer.BlockCopy(data, 4, address, 0, addressLength);
            if (sourcePrefix % 8 != 0 && addressLength > 0) {
                int unusedMask = (1 << (8 - sourcePrefix % 8)) - 1;
                if ((address[addressLength - 1] & unusedMask) != 0) throw new DnsClientException("EDNS Client Subnet host bits must be zero.");
            }
            return $"{new IPAddress(address)}/{sourcePrefix}/{scopePrefix}";
        }

        private static string ReadNameOnly(DnsWireReader reader, DnsRecordType type) {
            string value = reader.ReadName();
            EnsureEnd(reader, type);
            return value;
        }

        private static string FormatSoa(DnsWireReader reader) {
            string mname = reader.ReadName();
            string rname = reader.ReadName();
            uint serial = reader.ReadUInt32();
            uint refresh = reader.ReadUInt32();
            uint retry = reader.ReadUInt32();
            uint expire = reader.ReadUInt32();
            uint minimum = reader.ReadUInt32();
            EnsureEnd(reader, DnsRecordType.SOA);
            return $"{mname} {rname} {serial} {refresh} {retry} {expire} {minimum}";
        }

        private static string FormatCharacterStrings(DnsWireReader reader) {
            var values = new List<string>();
            while (!reader.IsAtEnd) values.Add(ReadCharacterString(reader));
            return string.Join(string.Empty, values);
        }

        private static string ReadCharacterString(DnsWireReader reader) {
            int length = reader.ReadByte();
            return Quote(reader.ReadBytes(length));
        }

        private static string FormatHinfo(DnsWireReader reader) {
            string cpu = ReadCharacterString(reader);
            string os = ReadCharacterString(reader);
            EnsureEnd(reader, DnsRecordType.HINFO);
            return $"{cpu} {os}";
        }

        private static string FormatNaptr(DnsWireReader reader) {
            ushort order = reader.ReadUInt16();
            ushort preference = reader.ReadUInt16();
            string flags = ReadCharacterString(reader);
            string services = ReadCharacterString(reader);
            string regexp = ReadCharacterString(reader);
            string replacement = reader.ReadName();
            EnsureEnd(reader, DnsRecordType.NAPTR);
            return $"{order} {preference} {flags} {services} {regexp} {replacement}";
        }

        private static string FormatCaa(DnsWireReader reader) {
            byte flags = reader.ReadByte();
            int tagLength = reader.ReadByte();
            string tag = Encoding.ASCII.GetString(reader.ReadBytes(tagLength));
            string value = Quote(reader.ReadBytes(reader.End - reader.Position));
            return $"{flags} {tag} {value}";
        }

        private static string FormatUri(DnsWireReader reader) {
            ushort priority = reader.ReadUInt16();
            ushort weight = reader.ReadUInt16();
            string target = Quote(reader.ReadBytes(reader.End - reader.Position));
            return $"{priority} {weight} {target}";
        }

        private static string FormatLoc(DnsWireReader reader) {
            if (reader.End - reader.Position != 16) throw new DnsClientException("LOC version 0 RDATA must be exactly 16 bytes.");
            byte version = reader.ReadByte();
            if (version != 0) throw new DnsClientException($"Unsupported LOC version {version}.");
            byte size = reader.ReadByte();
            byte horizontalPrecision = reader.ReadByte();
            byte verticalPrecision = reader.ReadByte();
            uint latitude = reader.ReadUInt32();
            uint longitude = reader.ReadUInt32();
            uint altitude = reader.ReadUInt32();
            string lat = FormatCoordinate((long)latitude - 0x80000000L, 'N', 'S');
            string lon = FormatCoordinate((long)longitude - 0x80000000L, 'E', 'W');
            decimal altitudeMeters = (altitude - 10000000m) / 100m;
            return $"{lat} {lon} {altitudeMeters.ToString("0.00", CultureInfo.InvariantCulture)}m " +
                   $"{DecodeLocPrecision(size).ToString("0.00", CultureInfo.InvariantCulture)}m " +
                   $"{DecodeLocPrecision(horizontalPrecision).ToString("0.00", CultureInfo.InvariantCulture)}m " +
                   $"{DecodeLocPrecision(verticalPrecision).ToString("0.00", CultureInfo.InvariantCulture)}m";
        }

        private static string FormatCoordinate(long milliseconds, char positive, char negative) {
            char direction = milliseconds < 0 ? negative : positive;
            long absolute = Math.Abs(milliseconds);
            long degrees = absolute / 3600000;
            long minutes = absolute / 60000 % 60;
            decimal seconds = (absolute % 60000) / 1000m;
            return $"{degrees} {minutes} {seconds.ToString("0.000", CultureInfo.InvariantCulture)} {direction}";
        }

        private static decimal DecodeLocPrecision(byte value) {
            int mantissa = (value >> 4) & 0x0F;
            int exponent = value & 0x0F;
            if (mantissa > 9 || exponent > 9) throw new DnsClientException("LOC precision has an invalid mantissa or exponent.");
            decimal centimeters = mantissa;
            for (int i = 0; i < exponent; i++) centimeters *= 10m;
            return centimeters / 100m;
        }

        private static string FormatDnsKey(DnsWireReader reader) {
            ushort flags = reader.ReadUInt16();
            byte protocol = reader.ReadByte();
            byte algorithm = reader.ReadByte();
            string key = Convert.ToBase64String(reader.ReadBytes(reader.End - reader.Position));
            return $"{flags} {protocol} {algorithm} {key}";
        }

        private static string FormatDs(DnsWireReader reader) {
            ushort keyTag = reader.ReadUInt16();
            byte algorithm = reader.ReadByte();
            byte digestType = reader.ReadByte();
            string digest = ToHex(reader.ReadBytes(reader.End - reader.Position));
            return $"{keyTag} {algorithm} {digestType} {digest}";
        }

        private static string FormatRrsig(DnsWireReader reader) {
            ushort typeValue = reader.ReadUInt16();
            byte algorithm = reader.ReadByte();
            byte labels = reader.ReadByte();
            uint originalTtl = reader.ReadUInt32();
            uint expiration = reader.ReadUInt32();
            uint inception = reader.ReadUInt32();
            ushort keyTag = reader.ReadUInt16();
            string signer = reader.ReadName();
            string signature = Convert.ToBase64String(reader.ReadBytes(reader.End - reader.Position));
            string type = Enum.IsDefined(typeof(DnsRecordType), typeValue)
                ? ((DnsRecordType)typeValue).ToString()
                : typeValue.ToString(CultureInfo.InvariantCulture);
            return $"{type} {algorithm} {labels} {originalTtl} {expiration} {inception} {keyTag} {signer} {signature}";
        }

        private static string FormatNsec(DnsWireReader reader) {
            string next = reader.ReadName();
            return next + FormatTypeBitmaps(reader);
        }

        private static string FormatNsec3(DnsWireReader reader) {
            byte algorithm = reader.ReadByte();
            byte flags = reader.ReadByte();
            ushort iterations = reader.ReadUInt16();
            int saltLength = reader.ReadByte();
            byte[] salt = reader.ReadBytes(saltLength);
            int hashLength = reader.ReadByte();
            byte[] nextHash = reader.ReadBytes(hashLength);
            return $"{algorithm} {flags} {iterations} {(salt.Length == 0 ? "-" : ToHex(salt))} {ToBase32Hex(nextHash)}{FormatTypeBitmaps(reader)}";
        }

        private static string FormatNsec3Param(DnsWireReader reader) {
            byte algorithm = reader.ReadByte();
            byte flags = reader.ReadByte();
            ushort iterations = reader.ReadUInt16();
            int saltLength = reader.ReadByte();
            byte[] salt = reader.ReadBytes(saltLength);
            EnsureEnd(reader, DnsRecordType.NSEC3PARAM);
            return $"{algorithm} {flags} {iterations} {(salt.Length == 0 ? "-" : ToHex(salt))}";
        }

        private static string FormatSshfp(DnsWireReader reader) {
            byte algorithm = reader.ReadByte();
            byte fingerprintType = reader.ReadByte();
            return $"{algorithm} {fingerprintType} {ToHex(reader.ReadBytes(reader.End - reader.Position))}";
        }

        private static string FormatTlsa(DnsWireReader reader) {
            byte usage = reader.ReadByte();
            byte selector = reader.ReadByte();
            byte matchingType = reader.ReadByte();
            return $"{usage} {selector} {matchingType} {ToHex(reader.ReadBytes(reader.End - reader.Position))}";
        }

        private static string FormatSvcb(DnsWireReader reader) => DnsSvcbCodec.Format(DnsSvcbCodec.Read(reader), normalizeTarget: false);

        private static string FormatTypeBitmaps(DnsWireReader reader) {
            var types = new List<string>();
            int lastWindow = -1;
            while (!reader.IsAtEnd) {
                int window = reader.ReadByte();
                int bitmapLength = reader.ReadByte();
                if (window <= lastWindow || bitmapLength < 1 || bitmapLength > 32) throw new DnsClientException("NSEC type bitmap has an invalid window or length.");
                lastWindow = window;
                byte[] bitmap = reader.ReadBytes(bitmapLength);
                if (bitmap[bitmap.Length - 1] == 0) throw new DnsClientException("NSEC type bitmap has non-canonical trailing zero octets.");
                for (int octet = 0; octet < bitmap.Length; octet++) {
                    for (int bit = 0; bit < 8; bit++) {
                        if ((bitmap[octet] & (1 << (7 - bit))) == 0) continue;
                        ushort value = (ushort)(window * 256 + octet * 8 + bit);
                        types.Add(Enum.IsDefined(typeof(DnsRecordType), value)
                            ? ((DnsRecordType)value).ToString()
                            : "TYPE" + value.ToString(CultureInfo.InvariantCulture));
                    }
                }
            }
            return types.Count == 0 ? string.Empty : " " + string.Join(" ", types);
        }

        private static string FormatUnknown(DnsWireReader reader) {
            byte[] value = reader.ReadBytes(reader.End - reader.Position);
            return $"\\# {value.Length} {ToHex(value)}";
        }

        private static string Quote(byte[] value) {
            var builder = new StringBuilder(value.Length + 2).Append('"');
            foreach (byte b in value) {
                if (b == (byte)'"' || b == (byte)'\\') builder.Append('\\').Append((char)b);
                else if (b < 0x20 || b > 0x7E) builder.Append('\\').Append(b.ToString("D3", CultureInfo.InvariantCulture));
                else builder.Append((char)b);
            }
            return builder.Append('"').ToString();
        }

        private static string ToBase32Hex(byte[] bytes) {
            const string alphabet = "0123456789ABCDEFGHIJKLMNOPQRSTUV";
            var builder = new StringBuilder((bytes.Length * 8 + 4) / 5);
            int buffer = 0;
            int bits = 0;
            foreach (byte value in bytes) {
                buffer = (buffer << 8) | value;
                bits += 8;
                while (bits >= 5) {
                    bits -= 5;
                    builder.Append(alphabet[(buffer >> bits) & 31]);
                }
            }
            if (bits > 0) builder.Append(alphabet[(buffer << (5 - bits)) & 31]);
            return builder.ToString();
        }

        private static string ToHex(byte[] value) => BitConverter.ToString(value).Replace("-", string.Empty);

        private static void RequireLength(int actual, int expected, DnsRecordType type) {
            if (actual != expected) throw new DnsClientException($"{type} RDATA must be exactly {expected} bytes; received {actual}.");
        }

        private static void EnsureEnd(DnsWireReader reader, DnsRecordType type) {
            if (!reader.IsAtEnd) throw new DnsClientException($"{type} RDATA contains trailing bytes.");
        }
    }
}
