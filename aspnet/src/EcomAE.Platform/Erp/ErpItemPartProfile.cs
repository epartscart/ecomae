using System.Globalization;
using EcomAE.Platform.Migration;
using Microsoft.AspNetCore.Http;

namespace EcomAE.Platform.Erp;

/// <summary>Inventory part or assembly card, saved beside the item master.</summary>
public sealed record ErpItemBomLine(string Sku, string Description, decimal Qty, decimal Cost, string Unit);

public sealed record ErpItemPartProfile(
    string Kind,
    string ManufacturerPart,
    decimal Cost,
    decimal SalesPrice,
    string CogsAccount,
    string IncomeAccount,
    string AssetAccount,
    string PreferredVendor,
    decimal ReorderMin,
    decimal ReorderMax,
    decimal OnHand,
    bool Inactive,
    string PurchaseDescription,
    string SalesDescription,
    IReadOnlyList<ErpItemBomLine> Bom)
{
    public const string InventoryPart = "inventory_part";
    public const string InventoryAssembly = "inventory_assembly";

    public bool IsAssembly => string.Equals(Kind, InventoryAssembly, StringComparison.Ordinal);

    public string KindLabel => IsAssembly ? "Inventory assembly" : "Inventory part";

    public decimal MarkupPercent => Cost <= 0 ? 0 : Math.Round((SalesPrice - Cost) / Cost * 100m, 2, MidpointRounding.AwayFromZero);

    public decimal MarginPercent => SalesPrice <= 0 ? 0 : Math.Round((SalesPrice - Cost) / SalesPrice * 100m, 2, MidpointRounding.AwayFromZero);

    public decimal BomCost => Bom.Sum(line => line.Qty * line.Cost);

    public static bool IsKnownKind(string? kind)
        => string.Equals(kind, InventoryPart, StringComparison.Ordinal)
           || string.Equals(kind, InventoryAssembly, StringComparison.Ordinal);

    public static ErpItemPartProfile? FromForm(IFormCollection form)
    {
        var kind = LiveWriteFormBinder.Text(form, "part_kind", "partKind");
        if (!IsKnownKind(kind))
        {
            return null;
        }

        var skus = form["bom_sku"];
        var descriptions = form["bom_desc"];
        var qtys = form["bom_qty"];
        var costs = form["bom_cost"];
        var units = form["bom_unit"];
        var lines = new List<ErpItemBomLine>();
        var count = Math.Max(skus.Count, descriptions.Count);
        for (var i = 0; i < count; i++)
        {
            var sku = i < skus.Count ? skus[i]?.Trim() ?? "" : "";
            var desc = i < descriptions.Count ? descriptions[i]?.Trim() ?? "" : "";
            if (sku.Length == 0 && desc.Length == 0)
            {
                continue;
            }

            lines.Add(new ErpItemBomLine(
                Clip(sku, 64),
                Clip(desc, 255),
                Money(i < qtys.Count ? qtys[i] : null),
                Money(i < costs.Count ? costs[i] : null),
                Clip(string.IsNullOrWhiteSpace(i < units.Count ? units[i] : null) ? "pcs" : units[i]!.Trim(), 16)));
        }

        return new ErpItemPartProfile(
            kind!,
            Clip(LiveWriteFormBinder.Text(form, "manufacturer_part"), 128),
            Money(LiveWriteFormBinder.Text(form, "cost")),
            Money(LiveWriteFormBinder.Text(form, "sales_price", "salesPrice")),
            Clip(LiveWriteFormBinder.Text(form, "cogs_account"), 120),
            Clip(LiveWriteFormBinder.Text(form, "income_account"), 120),
            Clip(LiveWriteFormBinder.Text(form, "asset_account"), 120),
            Clip(LiveWriteFormBinder.Text(form, "preferred_vendor"), 255),
            Money(LiveWriteFormBinder.Text(form, "reorder_min")),
            Money(LiveWriteFormBinder.Text(form, "reorder_max")),
            Money(LiveWriteFormBinder.Text(form, "on_hand")),
            LiveWriteFormBinder.Flag(form, "inactive", "part_inactive"),
            Clip(LiveWriteFormBinder.Text(form, "purchase_description"), 500),
            Clip(LiveWriteFormBinder.Text(form, "sales_description"), 500),
            lines);
    }

    private static decimal Money(string? raw)
        => decimal.TryParse(raw, NumberStyles.Any, CultureInfo.InvariantCulture, out var n) ? n : 0;

    private static string Clip(string? raw, int max)
    {
        var text = (raw ?? string.Empty).Trim();
        return text.Length <= max ? text : text[..max];
    }
}
