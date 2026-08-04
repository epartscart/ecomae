namespace EcomAE.Platform.Presentation;

/// <summary>Webfonts that match PHP chrome (Homer Open Sans, modex PT Sans, ERP premium Fraunces/Sora).</summary>
public static class LegacyPhpFontAssets
{
    public const string OpenSans =
        "https://fonts.googleapis.com/css2?family=Open+Sans:wght@400;600;700&display=swap";

    public const string PtSans =
        "https://fonts.googleapis.com/css2?family=PT+Sans:wght@400;700&display=swap";

    public const string FrauncesSora =
        "https://fonts.googleapis.com/css2?family=Fraunces:wght@600;700&family=Sora:wght@400;600;700&display=swap";

    /// <summary>Matches bos/epc_bos_shell.css --bos-font / --bos-font-mono.</summary>
    public const string InterJetBrains =
        "https://fonts.googleapis.com/css2?family=Inter:wght@400;500;600;700&family=JetBrains+Mono:wght@400;600&display=swap";

    /// <summary>Matches PHP epc_ecomae_home_sections_enqueue / marketing headline fonts.</summary>
    public const string SyneDmSans =
        "https://fonts.googleapis.com/css2?family=Syne:wght@400;600;700;800&family=DM+Sans:opsz,wght@9..40,300;9..40,400;9..40,500;9..40,600;9..40,700&display=swap";

    public const string FontAwesomeCdn =
        "https://cdnjs.cloudflare.com/ajax/libs/font-awesome/4.7.0/css/font-awesome.min.css";

    public static string StackFor(string surfaceKey) => surfaceKey.Trim().ToLowerInvariant() switch
    {
        "storefront" => "\"PT Sans\", \"Open Sans\", \"Segoe UI\", Tahoma, sans-serif",
        "erp" => "\"Open Sans\", \"Segoe UI\", Tahoma, sans-serif",
        "bos" => "Inter, \"Open Sans\", \"Segoe UI\", Tahoma, sans-serif",
        "marketing" => "\"DM Sans\", system-ui, -apple-system, \"Segoe UI\", Roboto, sans-serif",
        _ => "\"Open Sans\", \"Segoe UI\", Tahoma, sans-serif"
    };
}
