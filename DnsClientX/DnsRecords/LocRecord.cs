using System.Globalization;

namespace DnsClientX;

/// <summary>
/// Represents a LOC record containing geographic information.
/// </summary>
/// <remarks>
/// The format of this record is specified in <a href="https://www.rfc-editor.org/rfc/rfc1876">RFC 1876</a>.
/// </remarks>
public sealed class LocRecord {
    public double Latitude { get; }
    public double Longitude { get; }
    public double AltitudeMeters { get; }
    public double SizeMeters { get; }
    public double HorizontalPrecisionMeters { get; }
    public double VerticalPrecisionMeters { get; }

    public LocRecord(double latitude, double longitude, double altitudeMeters, double sizeMeters, double horizontalPrecisionMeters, double verticalPrecisionMeters) {
        Latitude = latitude;
        Longitude = longitude;
        AltitudeMeters = altitudeMeters;
        SizeMeters = sizeMeters;
        HorizontalPrecisionMeters = horizontalPrecisionMeters;
        VerticalPrecisionMeters = verticalPrecisionMeters;
    }
}
