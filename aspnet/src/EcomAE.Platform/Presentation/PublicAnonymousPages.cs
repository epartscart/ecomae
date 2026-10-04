namespace EcomAE.Platform.Presentation;

/// <summary>
/// Anonymous routes proven in PHP and still 404 on ASP.NET:
/// marketing storefront demo <c>/shop</c>, tenant CP brochure, public ERP demo dashboard.
/// </summary>
public static class PublicAnonymousPages
{
    public static string NormalizePath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return "/";
        }

        var value = path.Trim();
        var q = value.IndexOf('?', StringComparison.Ordinal);
        if (q >= 0)
        {
            value = value[..q];
        }

        if (value.Length > 1)
        {
            value = value.TrimEnd('/');
        }

        if (value.Length == 0 || value[0] != '/')
        {
            value = "/" + value.TrimStart('/');
        }

        return value.Length == 0 ? "/" : value;
    }

    /// <summary>PHP strips a leading /en /ru /ar before brochure and ERP demo matching.</summary>
    public static string StripLang(string path)
    {
        var value = NormalizePath(path);
        if (value.Length < 3)
        {
            return value;
        }

        var head = value.Length == 3 ? value : value[..3];
        if (!(head.Equals("/en", StringComparison.OrdinalIgnoreCase)
              || head.Equals("/ru", StringComparison.OrdinalIgnoreCase)
              || head.Equals("/ar", StringComparison.OrdinalIgnoreCase)))
        {
            return value;
        }

        if (value.Length == 3)
        {
            return "/";
        }

        return value[3] == '/' ? value[3..] : value;
    }

    /// <summary>PHP <c>epc_erp_portal_match_request</c> page <c>demo</c>.</summary>
    public static bool IsErpDemoPath(string? path)
    {
        var bare = StripLang(path ?? "/");
        return bare.Equals("/erp-demo", StringComparison.OrdinalIgnoreCase)
            || bare.Equals("/shop/erp-demo", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Marketing "Storefront demo" href. Exact <c>/shop</c> on www.ecomae.com.
    /// The public storefront on that host already renders at <c>/en</c>.
    /// </summary>
    public static bool IsMarketingStorefrontDemo(string? host, string? path)
    {
        if (!EcomaeMarketingSnapshots.IsMarketingHost(host))
        {
            return false;
        }

        return NormalizePath(path).Equals("/shop", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// PHP <c>index.php</c> serves <c>/brochure-cp</c> and <c>/brochure/cp</c>
    /// (optional /en|/ru|/ar) on non-marketing hosts from the epartscart brochure.
    /// </summary>
    public static bool IsTenantCpBrochurePath(string? host, string? path)
    {
        if (EcomaeMarketingSnapshots.IsMarketingHost(host)
            || EcomaeIndustryShowcaseSnapshots.TryResolveHostSlug(host, out _))
        {
            return false;
        }

        var bare = StripLang(path ?? "/");
        return bare.Equals("/brochure-cp", StringComparison.OrdinalIgnoreCase)
            || bare.Equals("/brochure/cp", StringComparison.OrdinalIgnoreCase);
    }
}
