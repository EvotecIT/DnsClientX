using System;
using System.Globalization;

namespace DnsClientX;

/// <summary>Reads the optional coordinate and precision fields in RFC 1876 LOC presentation.</summary>
internal static class DnsLocPresentation {
    internal static string Format(LocRecord record) => CoordinateText(record.Latitude, 'N', 'S') + " "
        + CoordinateText(record.Longitude, 'E', 'W') + " " + MeterText(record.AltitudeMeters) + " "
        + MeterText(record.SizeMeters) + " " + MeterText(record.HorizontalPrecisionMeters) + " " + MeterText(record.VerticalPrecisionMeters);

    private static string MeterText(double value) => value.ToString("0.00", CultureInfo.InvariantCulture) + "m";

    private static string CoordinateText(double value, char positive, char negative) {
        long milliseconds = (long)Math.Round(Math.Abs(value) * 3600000);
        return (milliseconds / 3600000).ToString(CultureInfo.InvariantCulture) + " "
            + (milliseconds / 60000 % 60).ToString(CultureInfo.InvariantCulture) + " "
            + ((milliseconds % 60000) / 1000d).ToString("0.000", CultureInfo.InvariantCulture) + " "
            + (value < 0 ? negative : positive);
    }

    internal static bool TryParse(string data, out LocRecord? record) {
        record = null;
        string[] parts = data.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        int index = 0;
        if (!Coordinate(parts, ref index, 90, 'N', 'S', out double latitude)
            || !Coordinate(parts, ref index, 180, 'E', 'W', out double longitude)
            || index >= parts.Length || !Meters(parts[index++], -100000, 42849672.95, out double altitude)) return false;
        double[] precision = { 1, 10000, 10 };
        int remaining = parts.Length - index;
        if (remaining > precision.Length) return false;
        for (int p = 0; p < remaining; p++) {
            if (!Meters(parts[index++], 0, 90000000, out precision[p])) return false;
        }
        record = new LocRecord(latitude, longitude, altitude, precision[0], precision[1], precision[2]);
        return true;
    }

    private static bool Coordinate(string[] parts, ref int index, int maximum, char positive, char negative, out double coordinate) {
        coordinate = 0;
        if (index >= parts.Length || !int.TryParse(parts[index++], NumberStyles.None, CultureInfo.InvariantCulture, out int degrees)
            || degrees > maximum) return false;
        int minutes = 0;
        double seconds = 0;
        if (index < parts.Length && int.TryParse(parts[index], NumberStyles.None, CultureInfo.InvariantCulture, out int parsedMinutes)) {
            minutes = parsedMinutes;
            index++;
            if (index < parts.Length && double.TryParse(parts[index], NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out double parsedSeconds)) {
                seconds = parsedSeconds;
                index++;
            }
        }
        if (minutes > 59 || seconds < 0 || seconds >= 60 || degrees == maximum && (minutes != 0 || seconds != 0)
            || index >= parts.Length || parts[index].Length != 1) return false;
        char direction = char.ToUpperInvariant(parts[index++][0]);
        if (direction != positive && direction != negative) return false;
        coordinate = degrees + minutes / 60d + seconds / 3600d;
        if (direction == negative) coordinate = -coordinate;
        return true;
    }

    private static bool Meters(string value, double minimum, double maximum, out double meters) =>
        double.TryParse(value.TrimEnd('m', 'M'), NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint,
            CultureInfo.InvariantCulture, out meters) && meters >= minimum && meters <= maximum;
}
