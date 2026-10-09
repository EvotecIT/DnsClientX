namespace DnsClientX;
using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Represents TXT or SPF character-strings and their combined text.
/// </summary>
/// <remarks>
/// TXT records are often used for miscellaneous domain metadata.
/// </remarks>
public sealed class TxtRecord {
    /// <summary>
    /// Gets decoded character-strings concatenated within this resource record, without
    /// inserting separators or removing payload whitespace, quotes, or line breaks.
    /// </summary>
    public string Text { get; }

    /// <summary>Gets decoded character-strings without DNS presentation quotes.</summary>
    public IReadOnlyList<string> Strings { get; }

    /// <summary>
    /// Gets the original server presentation, or a quoted presentation generated from
    /// decoded strings when this record is constructed directly.
    /// </summary>
    public string RawText { get; }

    /// <summary>Gets character-strings retaining their DNS presentation quotes and escapes.</summary>
    public IReadOnlyList<string> RawStrings { get; }

    /// <summary>Initializes a new instance of the <see cref="TxtRecord"/> class.</summary>
    /// <param name="text">Decoded character-strings to snapshot and concatenate. Null elements represent empty strings.</param>
    /// <exception cref="ArgumentNullException"><paramref name="text"/> is null.</exception>
    public TxtRecord(string[] text) {
        if (text == null) throw new ArgumentNullException(nameof(text));
        string[] strings = text.Select(value => value ?? string.Empty).ToArray();
        string[] rawStrings = strings.Select(DnsPresentationFormat.Quote).ToArray();
        Text = string.Concat(strings);
        Strings = Array.AsReadOnly(strings);
        RawStrings = Array.AsReadOnly(rawStrings);
        RawText = string.Join(" ", rawStrings);
    }

    /// <summary>Initializes a new instance of the <see cref="TxtRecord"/> class.</summary>
    /// <param name="text">One decoded character-string. Literal quotes and backslashes are payload content.</param>
    public TxtRecord(string text) : this(new[] { text }) { }

    /// <summary>Projects the shared answer decoding while retaining its original character-string boundaries.</summary>
    internal TxtRecord(DnsAnswer answer, string text) {
        Text = text;
        Strings = Array.AsReadOnly(answer.DataStringsEscaped);
        RawText = answer.DataRaw ?? string.Empty;
        RawStrings = Array.AsReadOnly(answer.DataStrings);
    }
}

