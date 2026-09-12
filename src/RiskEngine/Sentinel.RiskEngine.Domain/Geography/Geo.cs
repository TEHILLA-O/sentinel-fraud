namespace Sentinel.RiskEngine.Domain.Geography;

/// <summary>
/// Approximate country/city centroids used by impossible-travel reasoning.
/// Assumptions are documented in docs/RULES.md.
/// </summary>
public static class Geo
{
    private static readonly Dictionary<string, GeoPoint> CountryCentroids = new(StringComparer.OrdinalIgnoreCase)
    {
        ["GB"] = new(51.5074, -0.1278, "London"),
        ["US"] = new(40.7128, -74.0060, "New York"),
        ["DE"] = new(52.5200, 13.4050, "Berlin"),
        ["FR"] = new(48.8566, 2.3522, "Paris"),
        ["ES"] = new(40.4168, -3.7038, "Madrid"),
        ["IT"] = new(41.9028, 12.4964, "Rome"),
        ["NL"] = new(52.3676, 4.9041, "Amsterdam"),
        ["IE"] = new(53.3498, -6.2603, "Dublin"),
        ["AU"] = new(-33.8688, 151.2093, "Sydney"),
        ["CA"] = new(43.6532, -79.3832, "Toronto"),
        ["JP"] = new(35.6762, 139.6503, "Tokyo"),
        ["SG"] = new(1.3521, 103.8198, "Singapore"),
        ["AE"] = new(25.2048, 55.2708, "Dubai"),
        ["IN"] = new(19.0760, 72.8777, "Mumbai"),
        ["BR"] = new(-23.5505, -46.6333, "Sao Paulo"),
        ["ZA"] = new(-26.2041, 28.0473, "Johannesburg"),
        ["NG"] = new(6.5244, 3.3792, "Lagos"),
        ["SE"] = new(59.3293, 18.0686, "Stockholm"),
        ["NO"] = new(59.9139, 10.7522, "Oslo"),
        ["PL"] = new(52.2297, 21.0122, "Warsaw")
    };

    private static readonly Dictionary<string, GeoPoint> Cities = new(StringComparer.OrdinalIgnoreCase)
    {
        ["london"] = new(51.5074, -0.1278, "London"),
        ["manchester"] = new(53.4808, -2.2426, "Manchester"),
        ["new york"] = new(40.7128, -74.0060, "New York"),
        ["los angeles"] = new(34.0522, -118.2437, "Los Angeles"),
        ["chicago"] = new(41.8781, -87.6298, "Chicago"),
        ["berlin"] = new(52.5200, 13.4050, "Berlin"),
        ["paris"] = new(48.8566, 2.3522, "Paris"),
        ["madrid"] = new(40.4168, -3.7038, "Madrid"),
        ["sydney"] = new(-33.8688, 151.2093, "Sydney"),
        ["tokyo"] = new(35.6762, 139.6503, "Tokyo"),
        ["dubai"] = new(25.2048, 55.2708, "Dubai")
    };

    public static GeoPoint? Resolve(string country, string? city)
    {
        if (!string.IsNullOrWhiteSpace(city) && Cities.TryGetValue(city.Trim(), out var cityPoint))
        {
            return cityPoint;
        }

        return CountryCentroids.TryGetValue(country, out var countryPoint) ? countryPoint : null;
    }

    public static double DistanceKm(GeoPoint a, GeoPoint b)
    {
        const double earthRadiusKm = 6371d;
        var dLat = DegreesToRadians(b.Latitude - a.Latitude);
        var dLon = DegreesToRadians(b.Longitude - a.Longitude);
        var lat1 = DegreesToRadians(a.Latitude);
        var lat2 = DegreesToRadians(b.Latitude);

        var h = Math.Pow(Math.Sin(dLat / 2), 2) +
                Math.Cos(lat1) * Math.Cos(lat2) * Math.Pow(Math.Sin(dLon / 2), 2);

        return 2 * earthRadiusKm * Math.Asin(Math.Min(1, Math.Sqrt(h)));
    }

    private static double DegreesToRadians(double degrees) => degrees * Math.PI / 180d;
}

public readonly record struct GeoPoint(double Latitude, double Longitude, string Label);
