using System.Globalization;

namespace DnsClientX;

/// <summary>
/// Represents a LOC record containing geographic information.
/// </summary>
/// <remarks>
/// The format of this record is specified in <a href="https://www.rfc-editor.org/rfc/rfc1876">RFC 1876</a>.
/// </remarks>
public sealed class LocRecord {
    /// <summary>Gets the geographic latitude.</summary>
    public double Latitude { get; }
    /// <summary>Gets the geographic longitude.</summary>
    public double Longitude { get; }
    /// <summary>Gets the altitude in meters.</summary>
    public double AltitudeMeters { get; }
    /// <summary>Gets the size of the location in meters.</summary>
    public double SizeMeters { get; }
    /// <summary>Gets the horizontal precision in meters.</summary>
    public double HorizontalPrecisionMeters { get; }
    /// <summary>Gets the vertical precision in meters.</summary>
    public double VerticalPrecisionMeters { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="LocRecord"/> class.
    /// </summary>
    /// <param name="latitude">Latitude.</param>
    /// <param name="longitude">Longitude.</param>
    /// <param name="altitudeMeters">Altitude in meters.</param>
    /// <param name="sizeMeters">Location size in meters.</param>
    /// <param name="horizontalPrecisionMeters">Horizontal precision in meters.</param>
    /// <param name="verticalPrecisionMeters">Vertical precision in meters.</param>
    public LocRecord(double latitude, double longitude, double altitudeMeters, double sizeMeters, double horizontalPrecisionMeters, double verticalPrecisionMeters) {
        Latitude = latitude;
        Longitude = longitude;
        AltitudeMeters = altitudeMeters;
        SizeMeters = sizeMeters;
        HorizontalPrecisionMeters = horizontalPrecisionMeters;
        VerticalPrecisionMeters = verticalPrecisionMeters;
    }
}
