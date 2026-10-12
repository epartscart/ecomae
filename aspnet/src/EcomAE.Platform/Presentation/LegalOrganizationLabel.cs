namespace EcomAE.Platform.Presentation;

/// <summary>
/// Display name for the active legal organization. Generic placeholders such as
/// <c>Main Company</c> are replaced with the tenant legal / trade name.
/// </summary>
public static class LegalOrganizationLabel
{
    private static readonly HashSet<string> GenericNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "main",
        "main company",
        "company",
        "default company",
        "legal organization",
    };

    public static bool IsGeneric(string? name)
    {
        var value = (name ?? string.Empty).Trim();
        return value.Length == 0 || GenericNames.Contains(value);
    }

    public static string Resolve(string? entityName, string? profileLegalName, string? brandLabel = null)
    {
        if (!IsGeneric(entityName))
        {
            return entityName!.Trim();
        }

        if (!IsGeneric(profileLegalName))
        {
            return profileLegalName!.Trim();
        }

        if (!IsGeneric(brandLabel))
        {
            return brandLabel!.Trim();
        }

        return "Legal organization";
    }

    public static string Display(string? code, string? entityName, string? profileLegalName = null, string? brandLabel = null)
    {
        var name = Resolve(entityName, profileLegalName, brandLabel);
        if (string.IsNullOrWhiteSpace(code)
            || code.Equals("MAIN", StringComparison.OrdinalIgnoreCase)
            || name.Contains(code, StringComparison.OrdinalIgnoreCase))
        {
            return name;
        }

        return name;
    }
}
