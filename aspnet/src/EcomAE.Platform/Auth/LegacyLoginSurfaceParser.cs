namespace EcomAE.Platform.Auth;

public static class LegacyLoginSurfaceParser
{
    public static LegacyLoginSurface Parse(string? surface) => (surface ?? "cp").Trim().ToLowerInvariant() switch
    {
        "erp" => LegacyLoginSurface.Erp,
        "bos" => LegacyLoginSurface.Bos,
        "ip" => LegacyLoginSurface.Ip,
        "lifeos" => LegacyLoginSurface.LifeOs,
        "storefront" => LegacyLoginSurface.Storefront,
        _ => LegacyLoginSurface.ControlPanel
    };

    public static string Key(string? surface) => (surface ?? "cp").Trim().ToLowerInvariant() switch
    {
        "erp" => "erp",
        "bos" => "bos",
        "ip" => "ip",
        "lifeos" => "lifeos",
        "storefront" => "storefront",
        _ => "cp"
    };
}
