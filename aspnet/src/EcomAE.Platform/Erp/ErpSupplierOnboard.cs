namespace EcomAE.Platform.Erp;

/// <summary>
/// UAE e-invoice / FTA supplier onboarding rules: auto code, AP/COA link,
/// ownership shares, trade-license documents, then approve-before-purchase.
/// </summary>
public static class ErpSupplierOnboard
{
    public const string StatusDraft = "draft";
    public const string StatusApproved = "approved";
    public const string StatusRejected = "rejected";
    public const string DocTradeLicense = "trade_license";
    public const string DocPartnerId = "partner_id";
    public const string DocTrnCertificate = "trn_certificate";
    public const string UaeSchemeId = "0230";

    public static readonly string[] GroupTypes = ["Local", "Import", "Service", "Related"];
    public static readonly string[] Categories = ["Goods", "Services", "Jewellery", "Raw", "Expense", "Logistics"];
    public static readonly string[] Emirates =
    [
        "Abu Dhabi", "Dubai", "Sharjah", "Ajman", "Umm Al Quwain", "Ras Al Khaimah", "Fujairah",
    ];
    public static readonly string[] Currencies = ["AED", "USD", "EUR", "GBP", "SAR", "INR"];
    public static readonly string[] DocKinds =
    [
        DocTradeLicense,
        DocTrnCertificate,
        DocPartnerId,
        ErpSupplierKycLaw.DocMemorandum,
        ErpSupplierKycLaw.DocArticles,
        ErpSupplierKycLaw.DocSeniorManagement,
        ErpSupplierKycLaw.DocBeneficialOwnerId,
        ErpSupplierKycLaw.DocSignatoryId,
        ErpSupplierKycLaw.DocSignatoryAuthority,
    ];

    public static string NormalizeTrn(string? raw)
        => string.IsNullOrWhiteSpace(raw) ? "" : new string(raw.Where(char.IsDigit).ToArray());

    public static bool IsUaeTrn(string? raw)
    {
        var digits = NormalizeTrn(raw);
        return digits.Length == 15;
    }

    public static string NextSupplierCode(IEnumerable<string> existing)
    {
        var max = 0;
        foreach (var raw in existing)
        {
            var value = (raw ?? "").Trim();
            if (value.Length < 3 || (value[0] != 'V' && value[0] != 'v') || value[1] != '-')
            {
                continue;
            }

            if (int.TryParse(value.AsSpan(2), out var n) && n > max)
            {
                max = n;
            }
        }

        return "V-" + (max + 1).ToString("D4", System.Globalization.CultureInfo.InvariantCulture);
    }

    public static string NormalizeGroupType(string? raw)
    {
        var v = (raw ?? "").Trim();
        return GroupTypes.Contains(v, StringComparer.OrdinalIgnoreCase)
            ? GroupTypes.First(g => g.Equals(v, StringComparison.OrdinalIgnoreCase))
            : "";
    }

    public static string NormalizeCategory(string? raw)
    {
        var v = (raw ?? "").Trim();
        return Categories.Contains(v, StringComparer.OrdinalIgnoreCase)
            ? Categories.First(g => g.Equals(v, StringComparison.OrdinalIgnoreCase))
            : "";
    }

    public static IReadOnlyList<ErpSupplierOnboardCheck> Evaluate(ErpSupplierOnboardState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        var owners = state.Owners ?? [];
        var namedOwners = owners.Where(o => !string.IsNullOrWhiteSpace(o.FullName) && o.SharePct > 0m).ToArray();
        var shareSum = namedOwners.Sum(o => o.SharePct);
        var partnerDocs = state.Documents.Count(d => string.Equals(d, DocPartnerId, StringComparison.OrdinalIgnoreCase));
        var tradeDocs = state.Documents.Count(d => string.Equals(d, DocTradeLicense, StringComparison.OrdinalIgnoreCase));
        var addressOk = !string.IsNullOrWhiteSpace(state.Address)
            || (!string.IsNullOrWhiteSpace(state.Street) && !string.IsNullOrWhiteSpace(state.City));
        var vatOk = !state.VatRegistered || IsUaeTrn(state.Trn);
        var extraPartners = Math.Max(0, namedOwners.Length - 1);
        return
        [
            new("name", "Supplier name", !string.IsNullOrWhiteSpace(state.Name)),
            new("code", "Supplier code", !string.IsNullOrWhiteSpace(state.SupplierCode)),
            new("ap_coa", "Accounts payable / chart of account", state.ApCoaId > 0),
            new("currency", "Currency", !string.IsNullOrWhiteSpace(state.CurrencyCode)),
            new("address", "Address", addressOk),
            new("contact_person", "Contact person", !string.IsNullOrWhiteSpace(state.ContactPerson)),
            new("contact_phone", "Contact person phone", !string.IsNullOrWhiteSpace(state.ContactPhone)),
            new("group_type", "Supplier group type", NormalizeGroupType(state.GroupType).Length > 0),
            new("category", "Supplier category", NormalizeCategory(state.Category).Length > 0),
            new("country", "Country (UAE e-invoice)", !string.IsNullOrWhiteSpace(state.CountryCode)),
            new("vat", "TRN registered / not registered", true),
            new("trn", "UAE TRN (15 digits when VAT registered)", vatOk),
            new("owners", "Ownership rows with name and share", namedOwners.Length > 0),
            new("shares", "Ownership shares total 100%", namedOwners.Length > 0 && Math.Abs(shareSum - 100m) <= 0.05m),
            new("trade_license", "Trade license document", tradeDocs > 0),
            new("partner_docs", "Partner documents for additional owners", extraPartners == 0 || partnerDocs >= extraPartners),
        ];
    }

    public static bool CanApprove(IReadOnlyList<ErpSupplierOnboardCheck> checks)
        => checks.Count > 0 && checks.All(c => c.Ok);

    public static string MissingSummary(IReadOnlyList<ErpSupplierOnboardCheck> checks)
        => string.Join("; ", checks.Where(c => !c.Ok).Select(c => c.Label));
}

public sealed record ErpSupplierOnboardOwner(
    string FullName,
    decimal SharePct,
    string Nationality = "",
    string IdNumber = "",
    string Role = "owner");

public sealed record ErpSupplierOnboardState(
    string Name,
    string SupplierCode,
    long ApCoaId,
    string CurrencyCode,
    string Address,
    string Street,
    string City,
    string ContactPerson,
    string ContactPhone,
    string GroupType,
    string Category,
    string CountryCode,
    bool VatRegistered,
    string Trn,
    IReadOnlyList<ErpSupplierOnboardOwner> Owners,
    IReadOnlyList<string> Documents);

public sealed record ErpSupplierOnboardCheck(string Key, string Label, bool Ok);
