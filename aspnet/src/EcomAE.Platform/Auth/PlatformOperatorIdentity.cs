namespace EcomAE.Platform.Auth;

/// <summary>
/// Canonical Super CP / Super BOS / Super ERP operator. The password hash is never rewritten
/// when the login email moves from the legacy Gmail address to the first working mailbox.
/// </summary>
public static class PlatformOperatorIdentity
{
    public const string CanonicalEmail = "yawer@ecomae.com";
    public const string LegacyEmail = "ecomaedxb@gmail.com";

    public static readonly IReadOnlyList<string> Surfaces =
    [
        "super-cp",
        "super-bos",
        "super-erp"
    ];

    public static bool IsAlias(string? email)
    {
        var value = (email ?? string.Empty).Trim();
        return string.Equals(value, CanonicalEmail, StringComparison.OrdinalIgnoreCase)
            || string.Equals(value, LegacyEmail, StringComparison.OrdinalIgnoreCase);
    }

    public static string Canonicalize(string? email)
        => IsAlias(email) ? CanonicalEmail : (email ?? string.Empty).Trim().ToLowerInvariant();

    public static IReadOnlyList<string> LookupEmails(string? contact)
        => IsAlias(contact) ? [CanonicalEmail, LegacyEmail] : [(contact ?? string.Empty).Trim()];

    public static LegacyAdminIdentity WithCanonicalEmail(LegacyAdminIdentity identity)
        => IsAlias(identity.Email)
            ? identity with { Email = CanonicalEmail }
            : identity;
}
