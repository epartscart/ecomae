namespace EcomAE.Platform.Presentation;

/// <summary>
/// Customer-package extension point: contributes a per-country storefront skin
/// (body class + display currency) for requests resolving to its owning tenant.
/// Implemented by customer packages (e.g. customer.auto247); Standard Core
/// stores no customer palettes.
/// </summary>
public interface ICustomerSkinContributor
{
    /// <summary>Owning tenant site_key ("auto247").</summary>
    string SiteKey { get; }

    /// <summary>True when this request resolves to the contributor's tenant.</summary>
    bool AppliesTo(HttpContext? context);

    /// <summary>Lowercase country code for the current request host ("pk", "ae", …).</summary>
    string CountryCodeFor(HttpContext? context);

    /// <summary>Display currency for a country code ("PKR", "AED", …).</summary>
    string CurrencyFor(string countryCode);

    /// <summary>Storefront stylesheet URL this skin contributes ("" = none).</summary>
    string StylesheetHref { get; }

    /// <summary>Body class fragment added to the storefront body ("" = none).</summary>
    string BodyClass { get; }
}
