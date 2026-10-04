namespace EcomAE.Platform.Storefront;

/// <summary>
/// PHP <c>prices/common_interface.php</c> storage caption + delivery days, and
/// <c>getProductRecordHTML</c> term text. Hidden / paused storages are dropped
/// in C# after the article query — the article SQL itself stays unfiltered
/// (PHP CHPU brand query does not put <c>storefront_temp_disabled</c> in WHERE).
/// </summary>
public static class StorefrontWarehouseOfferPolicy
{
    public static string Caption(bool managerSeesFullName, string? name, string? shortName)
    {
        var full = (name ?? string.Empty).Trim();
        var shortLabel = (shortName ?? string.Empty).Trim();
        if (managerSeesFullName && full.Length > 0)
        {
            return full;
        }

        if (shortLabel.Length > 0)
        {
            return shortLabel;
        }

        return full;
    }

    /// <summary>PHP <c>(int)(additional_time/24)</c> added to <c>time_to_exe</c>.</summary>
    public static int DeliveryDays(string? timeToExe, int additionalTimeHours)
    {
        var product = 0;
        if (int.TryParse((timeToExe ?? string.Empty).Trim(), out var parsed))
        {
            product = parsed;
        }

        var extra = additionalTimeHours / 24;
        if (extra < 0)
        {
            extra = 0;
        }

        var days = product + extra;
        return days < 0 ? 0 : days;
    }

    /// <summary>
    /// PHP term cell: <c>0</c> → "In warehouse" plus caption; otherwise "N days."
    /// Range when guaranteed days differ.
    /// </summary>
    public static string FormatTerm(int days, int guaranteedDays, string? caption)
    {
        if (days != guaranteedDays)
        {
            return $"{days}-{guaranteedDays} days.";
        }

        if (days <= 0)
        {
            var label = (caption ?? string.Empty).Trim();
            return label.Length == 0 || label == "—"
                ? "In warehouse"
                : $"In warehouse · {label}";
        }

        return days == 1 ? "1 day." : $"{days} days.";
    }

    public static bool IsPublicStorage(bool hidden, bool paused) => !hidden && !paused;

    /// <summary>PHP office manager test: <c>shop_offices.users LIKE '%"id"%'</c>.</summary>
    public static bool OfficeUsersContain(string? usersJson, int userId)
    {
        if (userId <= 0 || string.IsNullOrEmpty(usersJson))
        {
            return false;
        }

        return usersJson.Contains($"\"{userId}\"", StringComparison.Ordinal);
    }
}
