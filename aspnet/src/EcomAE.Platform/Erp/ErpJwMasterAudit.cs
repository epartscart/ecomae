using System.Globalization;

namespace EcomAE.Platform.Erp;

/// <summary>Created date / created-by label for jewellery masters.</summary>
public static class ErpJwMasterAudit
{
    public static string Actor(string? email, int userId)
    {
        var value = (email ?? string.Empty).Trim();
        if (value.Length > 0)
        {
            return value.Length <= 80 ? value : value[..80];
        }

        return userId > 0 ? "user #" + userId.ToString(CultureInfo.InvariantCulture) : "";
    }

    public static string DisplayDate(object? value)
    {
        if (value is null or DBNull)
        {
            return "";
        }

        if (value is DateTime dateTime && dateTime != default)
        {
            return dateTime.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
        }

        var raw = Convert.ToString(value, CultureInfo.InvariantCulture)?.Trim() ?? "";
        if (raw.Length == 0)
        {
            return "";
        }

        if (long.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var unix) && unix > 1_000_000)
        {
            return DateTimeOffset.FromUnixTimeSeconds(unix).UtcDateTime.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
        }

        if (DateTime.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var parsed)
            || DateTime.TryParse(raw, out parsed))
        {
            return parsed.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
        }

        return raw.Length > 16 ? raw[..16] : raw;
    }

    public static string DisplayUser(string? raw)
        => string.IsNullOrWhiteSpace(raw) ? "—" : raw.Trim();
}
