using System.Globalization;

namespace EcomAE.Platform.Mail;

public static class MailboxTime
{
    public static string Rel(DateTimeOffset utc)
    {
        var span = DateTimeOffset.UtcNow - utc;
        if (span.TotalSeconds < 45)
        {
            return "just now";
        }

        if (span.TotalMinutes < 60)
        {
            return ((int)span.TotalMinutes) + "m";
        }

        if (span.TotalHours < 24)
        {
            return ((int)span.TotalHours) + "h";
        }

        if (span.TotalDays < 7)
        {
            return ((int)span.TotalDays) + "d";
        }

        return utc.ToLocalTime().ToString("dd MMM", CultureInfo.InvariantCulture);
    }

    public static string Stamp(DateTimeOffset utc)
        => utc.ToLocalTime().ToString("dd MMM yyyy HH:mm", CultureInfo.InvariantCulture);

    public static string DnsTone(string? status)
    {
        var value = (status ?? string.Empty).Trim().ToLowerInvariant();
        return value switch
        {
            "added" or "on_purelymail" or "rechecked" or "listed" => "ok",
            "needs_cloudflare" or "planned" => "warn",
            _ => "info",
        };
    }

    public static string DnsLabel(string? status)
    {
        var value = (status ?? string.Empty).Trim();
        return value.ToLowerInvariant() switch
        {
            "needs_cloudflare" => "Cloudflare",
            "on_purelymail" => "Live",
            "added" => "Added",
            "rechecked" => "Rechecked",
            "planned" => "Waiting",
            "local" => "Listed",
            "" => "—",
            _ => value,
        };
    }
}
