namespace EcomAE.Platform.Erp;

/// <summary>
/// PHP <c>epc_jewel_voc_types</c> codes plus the older ASP.NET aliases that already
/// exist on live rows. New writes use the PHP code; reads accept both families.
/// </summary>
public static class ErpJwVocTypes
{
    public const string MetalPurchase = "RMP";
    public const string DiamondPurchase = "RDP";
    public const string RetailInvoice = "RIN";
    public const string MetalSale = "MSL";
    public const string SalesReturn = "SRT";
    public const string PosAdvance = "ADV";
    public const string Journal = "JVL";
    public const string PettyCash = "PCV";
    public const string RepairSale = "RSL";
    public const string Manufacture = "MFG";
    public const string BranchTransfer = "BTR";

    public static readonly IReadOnlyList<string> MetalPurchaseAliases = ["RMP", "MMP", "MLP"];
    public static readonly IReadOnlyList<string> DiamondPurchaseAliases = ["RDP", "DMP", "DLP"];
    public static readonly IReadOnlyList<string> RetailAliases = ["RIN", "RSI", "RSC"];
    public static readonly IReadOnlyList<string> MetalSaleAliases = ["MSL", "MSI", "MSC"];
    public static readonly IReadOnlyList<string> SalesReturnAliases = ["SRT", "SRN", "SRC"];
    public static readonly IReadOnlyList<string> AdvanceAliases = ["ADV", "PAD", "PAR"];
    public static readonly IReadOnlyList<string> JournalAliases = ["JVL", "JVG", "JVA"];
    public static readonly IReadOnlyList<string> ManufactureAliases = ["MFG"];
    public static readonly IReadOnlyList<string> BranchTransferAliases = ["BTR"];
    public static readonly IReadOnlyList<string> SalesAnalysisAliases = ["RIN", "RSI", "MSL", "MSI", "RSL"];

    public static string Canonical(string? vocType, string? action = null)
    {
        var raw = (vocType ?? string.Empty).Trim().ToUpperInvariant();
        if (raw.Length > 0)
        {
            return CanonicalOrSelf(raw);
        }

        return FromAction(action);
    }

    public static string CanonicalOrSelf(string raw) => raw switch
    {
        "MMP" or "MLP" or "RMP" => MetalPurchase,
        "DMP" or "DLP" or "RDP" => DiamondPurchase,
        "RSI" or "RSC" or "RIN" => RetailInvoice,
        "MSI" or "MSC" or "MSL" => MetalSale,
        "SRN" or "SRC" or "SRT" => SalesReturn,
        "PAD" or "PAR" or "ADV" => PosAdvance,
        "JVG" or "JVA" or "JVL" => Journal,
        "PCV" => PettyCash,
        "RSL" => RepairSale,
        "MFG" => Manufacture,
        "BTR" => BranchTransfer,
        _ => raw
    };

    public static string FromAction(string? action) => (action ?? string.Empty).Trim().ToLowerInvariant() switch
    {
        "jw_metal_purchase_save" or "jw_metal_purchase" => MetalPurchase,
        "jw_diamond_purchase_save" or "jw_diamond_purchase" => DiamondPurchase,
        "jw_retail_sale_save" or "jw_retail_sales_save" or "jw_retail_sales" or "jewellery" or "retail_commerce" => RetailInvoice,
        "jw_metal_sale_save" or "jw_metal_sales" => MetalSale,
        "jw_sales_return_save" or "jw_sales_return" => SalesReturn,
        "jw_pos_advance_save" or "jw_pos_advance" => PosAdvance,
        "jw_journal_voucher_save" or "jw_journal_voucher" => Journal,
        "jw_repair_sale_save" or "jw_repair_sale" => RepairSale,
        "jw_manufacture_save" or "jw_manufacture" => Manufacture,
        "jw_branch_transfer_save" or "jw_branch_transfer" => BranchTransfer,
        _ => string.Empty
    };

    public static IReadOnlyList<string> Aliases(string family) => family.Trim().ToUpperInvariant() switch
    {
        "METAL" or "RMP" or "METAL_PURCHASE" => MetalPurchaseAliases,
        "DIAMOND" or "RDP" or "DIAMOND_PURCHASE" => DiamondPurchaseAliases,
        "RETAIL" or "RIN" => RetailAliases,
        "METAL_SALE" or "MSL" => MetalSaleAliases,
        "RETURN" or "SRT" or "SALE_RETURN" => SalesReturnAliases,
        "ADVANCE" or "ADV" => AdvanceAliases,
        "JOURNAL" or "JVL" => JournalAliases,
        "MANUFACTURE" or "MFG" => ManufactureAliases,
        "TRANSFER" or "BTR" or "BRANCH_TRANSFER" => BranchTransferAliases,
        "SALES_ANALYSIS" => SalesAnalysisAliases,
        _ => []
    };

    public static IReadOnlyList<string> AliasesForTab(string? tab) => (tab ?? string.Empty).Trim().ToLowerInvariant() switch
    {
        "jw_metal_purchase" => MetalPurchaseAliases,
        "jw_diamond_purchase" => DiamondPurchaseAliases,
        "jw_metal_sales" => MetalSaleAliases,
        "jw_sales_return" => SalesReturnAliases,
        "jw_pos_advance" => AdvanceAliases,
        "jw_journal_voucher" => JournalAliases,
        "jw_manufacture" => ManufactureAliases,
        "jw_branch_transfer" => BranchTransferAliases,
        "jw_retail_sales" or "jewellery" or "retail_commerce" => RetailAliases,
        _ => []
    };

    public static bool Matches(string? vocType, IReadOnlyList<string> aliases)
    {
        var raw = (vocType ?? string.Empty).Trim();
        if (raw.Length == 0 || aliases.Count == 0)
        {
            return false;
        }

        foreach (var alias in aliases)
        {
            if (string.Equals(alias, raw, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    public static bool IsSale(string? vocType)
        => Matches(vocType, RetailAliases) || Matches(vocType, MetalSaleAliases);

    public static bool IsReturn(string? vocType)
        => Matches(vocType, SalesReturnAliases);

    public static string SqlInList(IReadOnlyList<string> aliases)
        => string.Join(",", aliases.Select(code => "'" + code.Replace("'", string.Empty, StringComparison.Ordinal) + "'"));
}
