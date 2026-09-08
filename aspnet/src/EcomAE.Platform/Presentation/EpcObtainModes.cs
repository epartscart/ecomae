using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace EcomAE.Platform.Presentation;

/// <summary>
/// Classic <c>content/shop/order_process/checkout_how_get.php</c> +
/// <c>content/shop/obtaining_modes/*/customer_interface.php</c> twins.
/// Only handlers that still have a customer_interface on disk are offered.
/// </summary>
public static class EpcObtainModes
{
    public const string GetInOffice = "get_in_office";
    public const string EpcCarriers = "epc_carriers";

    private static readonly Regex HandlerSafe = new("[^a-zA-Z0-9_-]", RegexOptions.Compiled);

    public static IReadOnlyList<ObtainModeOption> FallbackModes { get; } =
    [
        new(1, "Collect from warehouse", GetInOffice),
        new(2, "Courier delivery", EpcCarriers),
    ];

    /// <summary>PHP <c>epc_channel_carriers_catalog()</c> plus demo_base used by <c>epc_channel_demo_rate</c>.</summary>
    public static IReadOnlyList<EpcCarrierOption> Carriers { get; } =
    [
        new("dhl", "DHL Express", 45m, [("EXPRESS", "Express Worldwide"), ("ECONOMY", "Economy Select")]),
        new("fedex", "FedEx", 42m, [("PRIORITY", "International Priority"), ("ECONOMY", "International Economy")]),
        new("ups", "UPS", 38m, [("STANDARD", "Worldwide Saver"), ("EXPRESS", "Worldwide Express")]),
        new("tnt", "TNT Express", 40m, [("EXPRESS", "Global Express"), ("ECONOMY", "Economy Express")]),
        new("aramex", "Aramex", 28m, [("PPX", "Parcel Express"), ("DOM", "Domestic")]),
        new("smsa", "SMSA Express", 26m, [("EXPRESS", "Express"), ("DOM", "Domestic")]),
        new("naqel", "Naqel Express", 25m, [("EXPRESS", "Express"), ("STANDARD", "Standard")]),
        new("emirates_post", "Emirates Post", 22m, [("EMS", "EMS Express"), ("PARCEL", "International Parcel")]),
        new("imile", "iMile", 24m, [("EXPRESS", "Express"), ("STANDARD", "Standard")]),
        new("dpd", "DPD", 32m, [("CLASSIC", "Classic"), ("EXPRESS", "Express")]),
        new("gls", "GLS", 31m, [("BUSINESS", "BusinessParcel"), ("EXPRESS", "ExpressParcel")]),
        new("postnl", "PostNL", 30m, [("STANDARD", "Standard"), ("EU", "EU Parcel")]),
        new("royal_mail", "Royal Mail", 33m, [("TRACKED", "Tracked 48"), ("INTL", "International Tracked")]),
        new("chronopost", "Chronopost", 34m, [("CHRONO", "Chrono 13"), ("EUROPE", "Chrono Europe")]),
        new("usps", "USPS", 36m, [("PRIORITY", "Priority Mail Intl"), ("EXPRESS", "Priority Express Intl")]),
        new("canada_post", "Canada Post", 35m, [("XPRESS", "Xpresspost"), ("INTL", "International Parcel")]),
        new("sf_express", "SF Express", 37m, [("STANDARD", "Standard Express"), ("INTL", "International")]),
        new("jt_express", "J&T Express", 23m, [("EXPRESS", "Express"), ("ECONOMY", "Economy")]),
        new("yamato", "Yamato Transport", 39m, [("TAQBIN", "TA-Q-BIN"), ("INTL", "International")]),
        new("bluedart", "Blue Dart", 29m, [("EXPRESS", "Domestic Express"), ("INTL", "International")]),
    ];

    public static string SanitizeHandler(string? handler)
        => HandlerSafe.Replace(handler ?? "", "");

    public static bool HasCustomerInterface(string? handler)
    {
        var name = SanitizeHandler(handler);
        return name is GetInOffice or EpcCarriers;
    }

    public static int ParseObtainModeCookie(string? cookie)
    {
        if (string.IsNullOrWhiteSpace(cookie))
        {
            return 0;
        }

        var raw = UnescapeCookie(cookie);
        if (int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n))
        {
            return n;
        }

        try
        {
            using var doc = JsonDocument.Parse(raw);
            return doc.RootElement.ValueKind switch
            {
                JsonValueKind.Number when doc.RootElement.TryGetInt32(out n) => n,
                JsonValueKind.String when int.TryParse(doc.RootElement.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out n) => n,
                _ => 0
            };
        }
        catch (JsonException)
        {
            return 0;
        }
    }

    public static HowGetSelection? ParseHowGetCookie(string? cookie)
    {
        if (string.IsNullOrWhiteSpace(cookie))
        {
            return null;
        }

        try
        {
            using var doc = JsonDocument.Parse(UnescapeCookie(cookie));
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            var root = doc.RootElement;
            var mode = ReadInt(root, "mode");
            if (mode <= 0)
            {
                return null;
            }

            return new HowGetSelection(
                mode,
                ReadInt(root, "office_id"),
                ReadString(root, "carrier"),
                ReadString(root, "service"),
                ReadString(root, "city"),
                ReadString(root, "country"),
                ReadString(root, "address"),
                ReadString(root, "phone"),
                ReadString(root, "weight_kg"),
                ReadDecimal(root, "delivery_price", "rate"));
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public static decimal DemoRate(string carrier, decimal weightKg, string? country)
    {
        var weight = weightKg < 0.1m ? 0.1m : weightKg;
        var found = Carriers.FirstOrDefault(c => string.Equals(c.Code, carrier, StringComparison.OrdinalIgnoreCase));
        var baseline = found?.DemoBase ?? 35m;
        var dest = (country ?? "").Trim().ToUpperInvariant();
        var intl = dest.Length > 0 && dest != "AE" ? 1.35m : 1.0m;
        return Math.Round((baseline + (weight * 8.5m)) * intl, 2, MidpointRounding.AwayFromZero);
    }

    public static string CarrierName(string? code)
        => Carriers.FirstOrDefault(c => string.Equals(c.Code, code, StringComparison.OrdinalIgnoreCase))?.Name
           ?? (code ?? "").ToUpperInvariant();

    public static string ServiceName(string? carrier, string? service)
    {
        var found = Carriers.FirstOrDefault(c => string.Equals(c.Code, carrier, StringComparison.OrdinalIgnoreCase));
        if (found is null)
        {
            return service ?? "";
        }

        var hit = found.Services.FirstOrDefault(s => string.Equals(s.Code, service, StringComparison.OrdinalIgnoreCase));
        return string.IsNullOrWhiteSpace(hit.Label) ? (service ?? "") : hit.Label;
    }

    public static string DemoBasesJson()
        => JsonSerializer.Serialize(Carriers.ToDictionary(c => c.Code, c => c.DemoBase, StringComparer.Ordinal));

    private static string UnescapeCookie(string cookie)
    {
        var raw = cookie.Trim();
        if (!raw.Contains('%', StringComparison.Ordinal))
        {
            return raw;
        }

        try
        {
            return Uri.UnescapeDataString(raw);
        }
        catch (UriFormatException)
        {
            return raw;
        }
    }

    private static int ReadInt(JsonElement root, string name)
    {
        if (!root.TryGetProperty(name, out var el))
        {
            return 0;
        }

        return el.ValueKind switch
        {
            JsonValueKind.Number when el.TryGetInt32(out var n) => n,
            JsonValueKind.String when int.TryParse(el.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) => n,
            _ => 0
        };
    }

    private static decimal ReadDecimal(JsonElement root, params string[] names)
    {
        foreach (var name in names)
        {
            if (!root.TryGetProperty(name, out var el))
            {
                continue;
            }

            if (el.ValueKind == JsonValueKind.Number && el.TryGetDecimal(out var d))
            {
                return d;
            }

            if (el.ValueKind == JsonValueKind.String
                && decimal.TryParse(el.GetString(), NumberStyles.Number, CultureInfo.InvariantCulture, out d))
            {
                return d;
            }
        }

        return 0m;
    }

    private static string ReadString(JsonElement root, string name)
        => root.TryGetProperty(name, out var el) ? el.ToString() : "";
}

public sealed record ObtainModeOption(int Id, string Label, string Handler);

public sealed record HowGetSelection(
    int Mode,
    int OfficeId,
    string Carrier,
    string Service,
    string City,
    string Country,
    string Address,
    string Phone,
    string WeightKg,
    decimal Rate);

public sealed record EpcCarrierOption(
    string Code,
    string Name,
    decimal DemoBase,
    IReadOnlyList<(string Code, string Label)> Services);
