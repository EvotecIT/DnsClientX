using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace DnsClientX {
    internal static class DnsWire {

        public static async Task<byte[]> ReadResponseBytes(this HttpResponseMessage response) {
            using var stream = await response.Content.ReadAsStreamAsync();
            if (stream.Length == 0) throw new DnsClientException("Response content is empty, can't parse as DNS wire format.");

            using (var memoryStream = new MemoryStream()) {
                await stream.CopyToAsync(memoryStream);
                return memoryStream.ToArray();
            }
        }


        public static async Task<DnsResponse> DeserializeDnsWireFormat(this HttpResponseMessage res, bool debug = false, byte[] bytes = null) {
            try {
                byte[] dnsWireFormatBytes;
                if (bytes != null) {
                    dnsWireFormatBytes = bytes;
                } else {
                    using Stream stream = await res.Content.ReadAsStreamAsync();
                    if (stream.Length == 0) throw new DnsClientException("Response content is empty, can't parse as DNS wire format.");
                    // Ensure the stream's position is at the start
                    stream.Position = 0;


                    // option 1 - works
                    //List<byte> dnsWireFormatBytesList = new List<byte>();
                    //byte[] buffer = new byte[8096];
                    //int bytesRead;
                    //while ((bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length)) > 0) {
                    //    dnsWireFormatBytesList.AddRange(new ArraySegment<byte>(buffer, 0, bytesRead));
                    //}
                    //byte[] dnsWireFormatBytes = dnsWireFormatBytesList.ToArray();

                    // option 2 - works
                    //byte[] dnsWireFormatBytes = new byte[stream.Length];
                    //int bytesRead = 0;
                    //while (bytesRead < stream.Length) {
                    //    bytesRead += await stream.ReadAsync(dnsWireFormatBytes, bytesRead, (int)stream.Length - bytesRead);
                    //}

                    // option 3 - works
                    dnsWireFormatBytes = new byte[stream.Length];
                    await stream.ReadAsync(dnsWireFormatBytes, 0, dnsWireFormatBytes.Length);
                }

                if (debug) {
                    if (res != null) {
                        // Print the DNS wire format bytes to the console
                        Console.WriteLine("Response Uri: " + res.RequestMessage.RequestUri);
                    }

                    Console.WriteLine("Response DnsWireFormatBytes: " + BitConverter.ToString(dnsWireFormatBytes));
                }

                // Extract the RCODE from the DNS message header
                byte rcodeByte = dnsWireFormatBytes[3];
                DnsResponseCode rcode = (DnsResponseCode)(rcodeByte & 0x0F); // The RCODE is in the lower 4 bits of the byte

                // Check the RCODE and throw an exception if it indicates an error
                if (rcode != DnsResponseCode.NoError) {
                    throw new DnsClientException($"DNS query failed with RCODE: {rcode}");
                }

                // Create a BinaryReader to read the DNS wire format bytes
                using BinaryReader reader = new BinaryReader(new MemoryStream(dnsWireFormatBytes));
                long messageStart = reader.BaseStream.Position;

                //ushort transactionId = DebuggingHelpers.TroubleshootingDnsWire2(reader, "classCount", true);
                //ushort flags = DebuggingHelpers.TroubleshootingDnsWire2(reader, "classCount", true);
                //ushort questionCount = DebuggingHelpers.TroubleshootingDnsWire2(reader, "classCount", true);
                //ushort answerCount = DebuggingHelpers.TroubleshootingDnsWire2(reader, "answerCount", true);
                //ushort authorityCount = DebuggingHelpers.TroubleshootingDnsWire2(reader, "authorityCount", true);
                //ushort additionalCount = DebuggingHelpers.TroubleshootingDnsWire2(reader, "additionalCount", true);

                ushort transactionId = BinaryPrimitives.ReadUInt16BigEndian(reader.ReadBytes(2));
                ushort flags = BinaryPrimitives.ReadUInt16BigEndian(reader.ReadBytes(2));
                ushort questionCount = BinaryPrimitives.ReadUInt16BigEndian(reader.ReadBytes(2));
                ushort answerCount = BinaryPrimitives.ReadUInt16BigEndian(reader.ReadBytes(2));
                ushort authorityCount = BinaryPrimitives.ReadUInt16BigEndian(reader.ReadBytes(2));
                ushort additionalCount = BinaryPrimitives.ReadUInt16BigEndian(reader.ReadBytes(2));

                // Read the question section
                DnsQuestion[] questions = new DnsQuestion[questionCount];
                for (int i = 0; i < questionCount; i++) {
                    if (reader.BaseStream.Position + 4 > reader.BaseStream.Length) {
                        throw new DnsClientException("Not enough data in the stream to read the question.");
                    }
                    // Read the question name, type, and class from the reader and create a new Question object
                    string name = reader.ReadDnsName(messageStart);

                    //ResourceRecordType type = (ResourceRecordType)DebuggingHelpers.TroubleshootingDnsWire2(reader, "QuestionType", true);
                    DnsRecordType type = (DnsRecordType)BinaryPrimitives.ReadUInt16BigEndian(reader.ReadBytes(2));

                    //ushort @class = DebuggingHelpers.TroubleshootingDnsWire2(reader, "QuestionClass", true);
                    ushort @class = BinaryPrimitives.ReadUInt16BigEndian(reader.ReadBytes(2));

                    questions[i] = new DnsQuestion {
                        Name = name,
                        Type = type,
                        //Class = @class
                    };
                }

                // Read the answer section
                DnsAnswer[] answers = new DnsAnswer[answerCount];
                for (int i = 0; i < answerCount; i++) {
                    //Console.WriteLine("-----------------");
                    if (reader.BaseStream.Position + 6 > reader.BaseStream.Length) {
                        throw new DnsClientException("Not enough data in the stream to read the answer.");
                    }

                    // Read the answer name
                    string name = reader.ReadDnsName(messageStart);

                    // Read the answer type, class, TTL, and data length
                    //ResourceRecordType type = (ResourceRecordType)TroubleshootingDnsWire2(reader, "AnswerType", true);
                    DnsRecordType type = (DnsRecordType)BinaryPrimitives.ReadUInt16BigEndian(reader.ReadBytes(2));

                    //ushort @class = DebuggingHelpers.TroubleshootingDnsWire2(reader, "AnswerClass", true);
                    ushort @class = BinaryPrimitives.ReadUInt16BigEndian(reader.ReadBytes(2));

                    //uint ttl = DebuggingHelpers.TroubleshootingDnsWire4(reader, "AnswerTtl", true);
                    uint ttl = BinaryPrimitives.ReadUInt32BigEndian(reader.ReadBytes(4));

                    //ushort rdLength = DebuggingHelpers.TroubleshootingDnsWire2(reader, "AnswerRdlength", true);
                    ushort rdLength = BinaryPrimitives.ReadUInt16BigEndian(reader.ReadBytes(2));

                    //Console.WriteLine("rdlength: " + rdLength);

                    // Read the answer data
                    byte[] rdata = reader.ReadBytes(rdLength);

                    //Console.WriteLine("Position: " + reader.BaseStream.Position);
                    //Console.WriteLine("Length: " + reader.BaseStream.Length);
                    //Console.WriteLine("rdata: " + rdata);
                    //Console.WriteLine("Name: " + name);
                    //Console.WriteLine("Type: " + type);
                    //Console.WriteLine("Class: " + @class);
                    //Console.WriteLine("Ttl: " + ttl);

                    // Set recordStart to the current position of the reader
                    int recordStart = (int)reader.BaseStream.Position;

                    // Process the record data
                    string data = ProcessRecordData(dnsWireFormatBytes, recordStart, type, rdata, rdLength, messageStart);

                    //Console.WriteLine("Data: " + data);

                    //Create a new Answer object and fill in the properties based on the DNS wire format bytes
                    answers[i] = new DnsAnswer {
                        Name = name,
                        Type = type,
                        TTL = (int)ttl,
                        DataRaw = data,
                    };
                }

                // Read the authority section
                DnsAnswer[] authorities = new DnsAnswer[authorityCount];
                for (int i = 0; i < authorityCount; i++) {
                    if (reader.BaseStream.Position + 6 > reader.BaseStream.Length)
                        throw new DnsClientException("Not enough data in the stream to read the authority.");

                    // Read the authority record
                    authorities[i] = reader.ReadDnsRecord(messageStart);
                }

                // Read the additional section
                DnsAnswer[] additional = new DnsAnswer[additionalCount];
                for (int i = 0; i < additionalCount; i++) {
                    if (reader.BaseStream.Position + 6 > reader.BaseStream.Length)
                        throw new DnsClientException("Not enough data in the stream to read the additional.");

                    // Read the additional record
                    additional[i] = reader.ReadDnsRecord(messageStart);
                }

                // Create a new Response object and fill in the properties based on the DNS wire format bytes
                DnsResponse response = new DnsResponse {
                    Status = (DnsResponseCode)(flags & 0x000F), // RCODE is the last 4 bits of flags
                    IsTruncated = (flags & 0x0200) != 0, // TC is the 7th bit of flags
                    IsRecursionDesired = (flags & 0x0100) != 0, // RD is the 8th bit of flags
                    IsRecursionAvailable = (flags & 0x0080) != 0, // RA is the 9th bit of flags
                    AuthenticData = (flags & 0x0020) != 0, // AD is the 12th bit of flags
                    CheckingDisabled = (flags & 0x0010) != 0, // CD is the 13th bit of flags
                    Questions = questions,
                    Answers = answers,
                    Authorities = authorities,
                    Additional = additional,
                };
                return response;
            } catch (Exception ex) {
                throw new DnsClientException(ex.Message);
            }
        }

        private static string ReadDnsName(this BinaryReader reader, long messageStart) {
            var labels = new List<string>();

            while (true) {
                byte length = reader.ReadByte();

                if (length == 0) {
                    // This is the end of the name
                    break;
                }

                // Check if this is a pointer
                if ((length & 0xC0) == 0xC0) {
                    // The next byte combined with the last 6 bits of this byte form a pointer to the rest of the name
                    byte secondByte = reader.ReadByte();
                    ushort pointer = (ushort)(((length & 0x3F) << 8) | secondByte);

                    // Save the current position
                    long currentPosition = reader.BaseStream.Position;

                    // Jump to the pointer position
                    reader.BaseStream.Position = messageStart + pointer;

                    // Read the rest of the name
                    labels.Add(reader.ReadDnsName(messageStart));

                    // Jump back to the original position
                    reader.BaseStream.Position = currentPosition;

                    break;
                } else {
                    // This is a normal label, read the text
                    labels.Add(Encoding.UTF8.GetString(reader.ReadBytes(length)) + ".");
                }
            }

            return string.Join("", labels);
        }

        private static string ReadDnsName(this BinaryReader reader, byte[] dnsMessage, ushort rdlength, long messageStart) {
            var labels = new List<string>();

            while (true) {
                byte length = reader.ReadByte();

                if (length == 0) {
                    // This is the end of the name
                    break;
                }

                // Check if this is a pointer
                if ((length & 0xC0) == 0xC0) {
                    // The next byte combined with the last 6 bits of this byte form a pointer to the rest of the name
                    byte secondByte = reader.ReadByte();
                    ushort pointer = (ushort)(((length & 0x3F) << 8) | secondByte);

                    // Save the current position
                    long currentPosition = reader.BaseStream.Position;

                    // Create a new BinaryReader at the pointer position
                    using BinaryReader pointerReader = new BinaryReader(new MemoryStream(dnsMessage));
                    pointerReader.BaseStream.Position = messageStart + pointer;

                    // Read the rest of the name
                    labels.Add(pointerReader.ReadDnsName(messageStart));

                    // Jump back to the original position
                    reader.BaseStream.Position = currentPosition;

                    break;
                } else {
                    // This is a normal label, read the text
                    labels.Add(Encoding.UTF8.GetString(reader.ReadBytes(length)) + ".");
                }
            }

            return string.Join("", labels);
        }

        private static string ReadDnsNameNS(this BinaryReader reader, byte[] dnsMessage, int recordStart, long messageStart, ushort rdlength) {
            //Console.WriteLine($"Starting DNS name reading with RDLENGTH: {rdlength}, MessageStart: {messageStart}");
            var labels = new List<string>();

            byte[] rdata = reader.ReadBytes(rdlength);
            //Console.WriteLine($"RDLENGTH is {rdlength}, rdata: {BitConverter.ToString(rdata)}");

            int position = 0;
            while (position < rdlength) {
                byte length = rdata[position++];
                if ((length & 0xC0) == 0xC0) { // Check if this is a pointer
                    ushort pointer = (ushort)((length & 0x3F) << 8 | rdata[position++]);
                    //Console.WriteLine($"Detected pointer: {pointer}, jumping to process labels");

                    long pointerPosition = messageStart + pointer; // Correct pointer calculation
                    //Console.WriteLine($"Calculated pointer position: {pointerPosition}");

                    if (pointerPosition < dnsMessage.Length && pointerPosition >= messageStart) {
                        using BinaryReader dnsMessageReader = new BinaryReader(new MemoryStream(dnsMessage));
                        dnsMessageReader.BaseStream.Position = pointerPosition;
                        ReadLabels(dnsMessageReader, labels, messageStart, dnsMessageReader.BaseStream.Length);
                    } else {
                        //Console.WriteLine("Error: Pointer target is beyond the stream length or before message start.");
                    }
                    break;
                } else { // This is a label
                    string label = Encoding.UTF8.GetString(rdata, position, length);
                    labels.Add(label);
                    //Console.WriteLine($"Label read: {label}");
                    position += length;
                }
            }

            string fullName = string.Join(".", labels).TrimEnd('.') + ".";
            //Console.WriteLine($"Full name read: {fullName}");
            return fullName;
        }

        private static void ReadLabels(this BinaryReader reader, List<string> labels, long messageStart, long endPosition) {
            while (reader.BaseStream.Position < endPosition && reader.BaseStream.Position < reader.BaseStream.Length) {
                if (reader.BaseStream.Position >= reader.BaseStream.Length) {
                    //Console.WriteLine("Attempted to read beyond the end of the stream.");
                    break;
                }

                byte length = reader.ReadByte();
                //Console.WriteLine($"Byte read for length/pointer: {length:X2}, Position: {reader.BaseStream.Position - 1}");

                if ((length & 0xC0) == 0xC0) { // It's a pointer
                    byte secondByte = reader.ReadByte();
                    ushort pointer = (ushort)(((length & 0x3F) << 8) | secondByte);
                    long pointerPosition = messageStart + (pointer & 0x3FFF); // Ensure we're correctly interpreting the offset
                    //Console.WriteLine($"Pointer detected, calculated pointer position: {pointerPosition}");

                    if (pointerPosition < reader.BaseStream.Length && pointerPosition >= messageStart) {
                        long savedPosition = reader.BaseStream.Position;
                        reader.BaseStream.Position = pointerPosition;
                        ReadLabels(reader, labels, messageStart, reader.BaseStream.Length); // Recursive call with correct bounds
                        reader.BaseStream.Position = savedPosition;
                        break; // Exit the loop since we've followed the pointer
                    } else {
                        //Console.WriteLine("Pointer position is outside the allowed range.");
                        break;
                    }
                } else if (length == 0) {
                    //Console.WriteLine("End of name detected.");
                    break; // End of the name
                } else {
                    var labelBytes = reader.ReadBytes(length);
                    string label = Encoding.UTF8.GetString(labelBytes);
                    labels.Add(label);
                    //Console.WriteLine($"Label read: {label}");
                }
            }
        }

        private static DnsAnswer ReadDnsRecord(this BinaryReader reader, long messageStart) {
            // Read the record name
            string name = reader.ReadDnsName(messageStart);

            // Read the record type, class, TTL, and data length
            DnsRecordType type = (DnsRecordType)BinaryPrimitives.ReadUInt16BigEndian(reader.ReadBytes(2));
            ushort @class = BinaryPrimitives.ReadUInt16BigEndian(reader.ReadBytes(2));
            uint ttl = BinaryPrimitives.ReadUInt32BigEndian(reader.ReadBytes(4));
            ushort rdlength = BinaryPrimitives.ReadUInt16BigEndian(reader.ReadBytes(2));

            // Check if there's enough data left in the stream to read the record data
            if (reader.BaseStream.Position + rdlength > reader.BaseStream.Length)
                throw new DnsClientException("Not enough data in the stream to read the record data.");

            // Read the record data
            byte[] rdata = reader.ReadBytes(rdlength);

            return new DnsAnswer {
                Name = name,
                Type = type,
                TTL = (int)ttl,
                DataRaw = Convert.ToBase64String(rdata)
            };
        }

        private static string DecodeCAARecord(this BinaryReader reader, ushort rdLength) {
            byte flags = reader.ReadByte();
            byte tagLength = reader.ReadByte();
            string tag = Encoding.ASCII.GetString(reader.ReadBytes(tagLength));
            string value = Encoding.ASCII.GetString(reader.ReadBytes(rdLength - tagLength - 2)); // Subtract 2 for the flags and tag length bytes
            return $"{flags} {tag} \"{value}\""; // Add quotes around the value
        }

        /// <summary>
        /// The SOA record data is structured as follows:
        /// • MNAME: The domain name of the primary NS record for the zone.This is a DNS name which is encoded the same way as in other records.
        /// • RNAME: A domain name that represents the email address of the administrative contact for the zone. The "@" symbol is replaced with a dot.
        /// • SERIAL: A 32-bit integer in network byte order.
        /// • REFRESH: A 32-bit integer in network byte order.
        /// • RETRY: A 32-bit integer in network byte order.
        /// • EXPIRE: A 32-bit integer in network byte order.
        /// • MINIMUM: A 32-bit integer in network byte order.
        /// </summary>
        /// <param name="reader"></param>
        /// <param name="dnsMessage"></param>
        /// <param name="rdLength"></param>
        /// <param name="messageStart"></param>
        /// <returns></returns>
        private static string DecodeSOARecord(this BinaryReader reader, byte[] dnsMessage, ushort rdLength, long messageStart) {
            string mname = reader.ReadDnsName(dnsMessage, rdLength, messageStart);
            string rname = reader.ReadDnsName(dnsMessage, rdLength, messageStart);
            uint serial = BinaryPrimitives.ReadUInt32BigEndian(reader.ReadBytes(4));
            int refresh = BinaryPrimitives.ReadInt32BigEndian(reader.ReadBytes(4));
            int retry = BinaryPrimitives.ReadInt32BigEndian(reader.ReadBytes(4));
            int expire = BinaryPrimitives.ReadInt32BigEndian(reader.ReadBytes(4));
            int minimum = BinaryPrimitives.ReadInt32BigEndian(reader.ReadBytes(4));
            return $"{mname} {rname} {serial} {refresh} {retry} {expire} {minimum}";
        }

        /// <summary>
        /// Decodes the dnskey record.
        /// </summary>
        /// <param name="reader">The reader.</param>
        /// <param name="rdLength">Length of the rd.</param>
        /// <returns></returns>
        private static string DecodeDNSKEYRecord(this BinaryReader reader, ushort rdLength) {
            // For DNSKEY records, decode the flags, protocol, algorithm, and public key from the record data
            ushort flags = BinaryPrimitives.ReadUInt16BigEndian(reader.ReadBytes(2));
            byte protocol = reader.ReadByte();
            DnsKeyAlgorithm algorithm = (DnsKeyAlgorithm)reader.ReadByte();
            byte[] publicKey = reader.ReadBytes(rdLength - 4); // Subtract 4 for the flags, protocol, and algorithm bytes

            return $"{flags} {protocol} {algorithm} {Convert.ToBase64String(publicKey)}";
        }


        /// <summary>
        /// NSEC record data is structured as follows:
        /// - Next Domain Name: The next domain name in the zone. This is a DNS name which is encoded the same way as in other records.
        /// - Type Bit Maps: A variable-length field that contains a list of the types of records that exist for the next domain name.
        /// - The type bit maps are encoded as a series of windows, each of which contains a bitmap of the types of records that exist for the next domain name.
        /// - Each window contains a bitmap length byte followed by a bitmap of the types of records that exist for the next domain name.
        /// - The bitmap length byte is followed by a variable-length bitmap of the types of records that exist for the next domain name.
        /// - The bitmap length byte is a value from 0 to 32 that represents the number of bits in the bitmap.
        /// - The bitmap is a variable-length field that contains a list of the types of records that exist for the next domain name.
        /// - The types of records are encoded as a series of bit fields, each of which represents a type of record that exists for the next domain name.
        /// - The bit fields are encoded as a series of bytes, each of which contains a bitmap of the types of records that exist for the next domain name.
        /// - The bit fields are encoded from the most significant bit to the least significant bit, and from the most significant byte to the least significant byte.
        /// - The bit fields are encoded in network byte order.
        /// - The bit fields are encoded in the same way as the type bitmap in the NSEC3PARAM record.
        /// - The types of records are encoded as a series of bit fields, each of which represents a type of record that exists for the next domain name.
        /// </summary>
        /// <param name="reader"></param>
        /// <param name="rdLength"></param>
        /// <param name="messageStart"></param>
        /// <returns></returns>
        private static string DecodeNSECRecord(this BinaryReader reader, ushort rdLength, long messageStart) {
            string nextDomainName = reader.ReadDnsName(messageStart);
            // let's replace nulls with \0 to make sure it's visible and similar to how it's done via JSON API
            nextDomainName = nextDomainName.Replace("\0", "\\000");
            byte[] typeBitmaps = reader.ReadBytes(rdLength - (int)(reader.BaseStream.Position - messageStart));

            List<string> types = new List<string>();
            for (int i = 0; i < typeBitmaps.Length;) {
                byte windowNumber = typeBitmaps[i++];
                byte bitmapLength = typeBitmaps[i++];

                for (int j = 0; j < bitmapLength; j++) {
                    for (int bit = 0; bit < 8; bit++) {
                        if ((typeBitmaps[i + j] & (1 << (7 - bit))) != 0) { // bit order is from most significant to least significant
                            ushort typeValue = (ushort)((windowNumber * 256) + (j * 8) + bit);
                            string typeName = Enum.IsDefined(typeof(DnsRecordType), typeValue)
                                ? ((DnsRecordType)typeValue).ToString()
                                : typeValue.ToString();
                            types.Add(typeName);
                        }
                    }
                }

                i += bitmapLength;
            }

            return $"{nextDomainName} {string.Join(" ", types)}";
        }

        /// <summary>
        /// Processes the record data.
        /// </summary>
        /// <param name="dnsMessage">The DNS message.</param>
        /// <param name="recordStart">The record start.</param>
        /// <param name="type">The type.</param>
        /// <param name="rdata">The rdata.</param>
        /// <param name="rdLength">Length of the rd.</param>
        /// <param name="messageStart">The message start.</param>
        /// <returns></returns>
        /// <exception cref="DnsClientException">
        /// The record data for " + type + " is not long enough? " + ex.Message
        /// or
        /// Error processing record data for " + type + ": " + ex.Message
        /// </exception>
        private static string ProcessRecordData(byte[] dnsMessage, int recordStart, DnsRecordType type, byte[] rdata, ushort rdLength, long messageStart) {
            using (BinaryReader reader = new BinaryReader(new MemoryStream(rdata))) {
                try {
                    if (type == DnsRecordType.TXT) {
                        // For TXT records, read each string separately
                        StringBuilder sb = new StringBuilder();
                        while (reader.BaseStream.Position < reader.BaseStream.Length) {
                            byte length = reader.ReadByte();
                            byte[] stringBytes = reader.ReadBytes(length);
                            sb.Append('"');
                            sb.Append(Encoding.UTF8.GetString(stringBytes));
                            sb.Append('"');
                        }
                        return sb.ToString();
                    } else if (type == DnsRecordType.A || type == DnsRecordType.AAAA) {
                        // For A records, decode the IP address from the record data
                        return new IPAddress(rdata).ToString();
                    } else if (type == DnsRecordType.CNAME) {
                        // For NS and CNAME records, decode the domain name from the record data
                        return reader.ReadDnsName(messageStart);
                    } else if (type == DnsRecordType.NS) {
                        // For NS records, decode the domain name from the record data
                        return reader.ReadDnsNameNS(dnsMessage, recordStart, messageStart, rdLength);
                    } else if (type == DnsRecordType.MX) {
                        // For MX records, decode the preference and exchange from the record data
                        ushort preference = BinaryPrimitives.ReadUInt16BigEndian(reader.ReadBytes(2));
                        string exchange = reader.ReadDnsName(dnsMessage, rdLength, messageStart);
                        return $"{preference} {exchange}";
                    } else if (type == DnsRecordType.SOA) {
                        // For SOA records, decode the mname, rname, and other fields from the record data
                        return reader.DecodeSOARecord(dnsMessage, rdLength, messageStart);
                    } else if (type == DnsRecordType.CAA) {
                        return reader.DecodeCAARecord(rdLength);
                    } else if (type == DnsRecordType.DNSKEY) {
                        return reader.DecodeDNSKEYRecord(rdLength);
                    } else if (type == DnsRecordType.NSEC) {
                        return reader.DecodeNSECRecord(rdLength, messageStart);
                    } else {
                        return Convert.ToBase64String(rdata);
                    }
                } catch (EndOfStreamException ex) {
                    throw new DnsClientException("The record data for " + type + " is not long enough? " + ex.Message);
                } catch (Exception ex) {
                    throw new DnsClientException("Error processing record data for " + type + ": " + ex.Message);
                }
            }
        }
    }
}
