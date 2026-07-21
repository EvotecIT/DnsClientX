namespace DnsClientX.Benchmarks;

/// <summary>Builds deterministic DNS responses for controlled benchmarks.</summary>
internal static class ControlledDnsMessages {
    internal static byte[] CreateAResponse(byte[] query, int answerCount = 1) {
        ArgumentNullException.ThrowIfNull(query);
        if (answerCount < 0 || answerCount > ushort.MaxValue) {
            throw new ArgumentOutOfRangeException(nameof(answerCount));
        }
        if (query.Length < 12) {
            throw new InvalidOperationException("The controlled resolver received a truncated DNS header.");
        }

        int questionEnd = 12;
        while (questionEnd < query.Length && query[questionEnd] != 0) {
            int labelLength = query[questionEnd];
            if (labelLength > 63 || questionEnd + labelLength >= query.Length) {
                throw new InvalidOperationException("The controlled resolver received an invalid question name.");
            }
            questionEnd += labelLength + 1;
        }
        questionEnd += 5; // Root label plus QTYPE and QCLASS.
        if (questionEnd > query.Length) {
            throw new InvalidOperationException("The controlled resolver received a truncated question.");
        }

        const int answerLength = 16;
        var response = new byte[checked(questionEnd + answerCount * answerLength)];
        Buffer.BlockCopy(query, 0, response, 0, questionEnd);
        response[2] = (byte)(response[2] | 0x80); // QR
        response[3] = (byte)(response[3] | 0x80); // RA
        response[6] = (byte)(answerCount >> 8);
        response[7] = (byte)answerCount;
        response[8] = 0;
        response[9] = 0;
        response[10] = 0;
        response[11] = 0;

        int offset = questionEnd;
        for (int index = 0; index < answerCount; index++) {
            response[offset++] = 0xc0;
            response[offset++] = 0x0c;
            response[offset++] = 0;
            response[offset++] = 1;
            response[offset++] = 0;
            response[offset++] = 1;
            response[offset++] = 0;
            response[offset++] = 0;
            response[offset++] = 0;
            response[offset++] = 60;
            response[offset++] = 0;
            response[offset++] = 4;
            response[offset++] = 192;
            response[offset++] = 0;
            response[offset++] = 2;
            response[offset++] = (byte)((index % 250) + 1);
        }
        return response;
    }
}
