namespace EcomAE.Platform.Erp;

public sealed record ErpSupplierKycFinding(string Label, bool Met);

/// <summary>
/// Supplier due diligence under Federal Decree-Law No. 10 of 2025 and
/// Cabinet Resolution No. 134 of 2025. A buy at or above AED 55,000, including
/// a buy above AED 100,000 and linked buys with the same supplier, needs the
/// complete file before it is saved.
/// </summary>
public static class ErpSupplierKycLaw
{
    public const decimal CoveredAmountAed = 55_000m;

    public const decimal NamedBuyAed = 100_000m;

    public const decimal BeneficialOwnerShare = 25m;

    public const string Decree = "Federal Decree-Law No. 10 of 2025";

    public const string Resolution = "Cabinet Resolution No. 134 of 2025";

    public const string DocMemorandum = "memorandum";

    public const string DocArticles = "articles";

    public const string DocSeniorManagement = "senior_management";

    public const string DocBeneficialOwnerId = "beneficial_owner_id";

    public const string DocSignatoryId = "signatory_id";

    public const string DocSignatoryAuthority = "signatory_authority";

    public const int RetentionYears = 5;

    public static IReadOnlyList<string> FileMarks { get; } =
        Assess(new ErpSupplierOnboardState("", "", 0, "", "", "", "", "", "", "", "", "", false, "", [], [])).Select(item => item.Label).ToArray();

    public static bool RequiresCompleteFile(decimal amountAed)
        => amountAed >= CoveredAmountAed;

    public static IReadOnlyList<ErpSupplierKycFinding> Assess(ErpSupplierOnboardState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        var documents = state.Documents ?? [];
        var owners = state.Owners ?? [];
        var beneficialOwner = owners.Any(owner =>
            !string.IsNullOrWhiteSpace(owner.FullName)
            && !string.IsNullOrWhiteSpace(owner.Nationality)
            && !string.IsNullOrWhiteSpace(owner.IdNumber)
            && (owner.SharePct >= BeneficialOwnerShare
                || owner.Role.Equals("ubo", StringComparison.OrdinalIgnoreCase)));
        var address = !string.IsNullOrWhiteSpace(state.Address)
            || (!string.IsNullOrWhiteSpace(state.Street) && !string.IsNullOrWhiteSpace(state.City));
        var taxNumber = !state.VatRegistered || ErpSupplierOnboard.IsUaeTrn(state.Trn);
        return
        [
            new("Legal name", !string.IsNullOrWhiteSpace(state.Name)),
            new("Registered address", address),
            new("Trade license", Has(documents, ErpSupplierOnboard.DocTradeLicense)),
            new("Memorandum of association", Has(documents, DocMemorandum)),
            new("Articles of association", Has(documents, DocArticles)),
            new("Senior management", Has(documents, DocSeniorManagement) || !string.IsNullOrWhiteSpace(state.ContactPerson)),
            new("Beneficial owner at 25% with identity", beneficialOwner),
            new("Beneficial owner identity document", Has(documents, DocBeneficialOwnerId, ErpSupplierOnboard.DocPartnerId)),
            new("Authorized signatory identity", Has(documents, DocSignatoryId)),
            new("Authority to act", Has(documents, DocSignatoryAuthority)),
            new("Tax registration number when registered", taxNumber),
        ];
    }

    public static bool FileComplete(ErpSupplierOnboardState state)
        => Assess(state).All(item => item.Met);

    public static string MissingSummary(ErpSupplierOnboardState state)
        => string.Join("; ", Assess(state).Where(item => !item.Met).Select(item => item.Label));

    private static bool Has(IReadOnlyList<string> documents, params string[] keys)
        => keys.Any(key => documents.Any(document => document.Equals(key, StringComparison.OrdinalIgnoreCase)));
}
