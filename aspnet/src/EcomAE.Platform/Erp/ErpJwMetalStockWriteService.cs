using System.Data.Common;

namespace EcomAE.Platform.Erp;

/// <summary>
/// Live PHP <c>jw_metal_stock_save</c> / <c>epc_jewel_metal_stock_save</c> twin.
/// Schema-ensure stays PHP — missing <c>epc_jewel_metal_stock</c> refuses instead of CREATE.
/// </summary>
public interface IErpJwMetalStockWriteService
{
    Task<ErpSimpleWriteResult> SaveAsync(
        ErpJwMetalStockSaveRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record ErpJwMetalStockSaveRequest(
    int CompanyId = 0,
    string? Metal = null,
    string? ItemCode = null,
    string? Description = null,
    string? CcMaking = null,
    string? CcMetal = null,
    string? Karat = null,
    decimal Purity = 0,
    string? Type = null,
    string? Brand = null,
    string? Category = null,
    string? SubCategory = null,
    string? Vendor = null,
    string? VendorRef = null,
    string? Country = null,
    string? HsCode = null,
    string? McUnit = null,
    bool IncludeStoneWeight = false,
    bool PassPurityDiff = false,
    bool InPieces = true,
    decimal PcWeightGms = 0,
    decimal PcWeightOz = 0,
    bool CreateBarcodes = false,
    string? BarcodePrefix = null,
    bool BlockGrossWtSales = false,
    bool AskSupplier = false,
    bool AskWastage = false,
    bool ExcludeGstTrn = false,
    bool GstTrnOnMakingStone = true,
    bool AllowNegativeStock = false,
    bool AllowLessThanCost = false,
    decimal ConvFactorOz = 31.10347m,
    string? AbcCode = null,
    bool LoyaltyItem = false,
    decimal StdCost = 0,
    decimal DiscountPct = 0,
    decimal MinQty = 0,
    decimal MaxQty = 0,
    decimal ReorderLevel = 0,
    decimal ReorderQty = 0,
    decimal PurCostGms = 0,
    decimal SalePriceGms = 0,
    string? Price1Code = null,
    string? Price1Label = null,
    string? CreatedBy = null);

public sealed class ErpJwMetalStockWriteService : IErpJwMetalStockWriteService
{
    private readonly IErpWriteConnectionFactory _connections;

    public ErpJwMetalStockWriteService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<ErpSimpleWriteResult> SaveAsync(
        ErpJwMetalStockSaveRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var code = (request.ItemCode ?? string.Empty).Trim();
        if (code.Length == 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Item code is required.");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        if (!await TableExistsAsync(connection, "epc_jewel_metal_stock", cancellationToken).ConfigureAwait(false))
        {
            return ErpSimpleWriteResult.Fail("invalid", "Jewellery metal stock tables are not provisioned");
        }

        var companyId = request.CompanyId < 0 ? 0 : request.CompanyId;
        code = Clip(code, 20);
        var metal = (request.Metal ?? string.Empty).Trim().ToUpperInvariant();
        if (metal.Length == 0)
        {
            metal = "G";
        }

        metal = Clip(metal, 2);
        var mcUnit = (request.McUnit ?? string.Empty).Trim();
        if (mcUnit.Length == 0)
        {
            mcUnit = "GMS";
        }

        mcUnit = Clip(mcUnit, 10);
        var price1Code = (request.Price1Code ?? string.Empty).Trim();
        if (price1Code.Length == 0)
        {
            price1Code = "GEN";
        }

        price1Code = Clip(price1Code, 5);
        var price1Label = (request.Price1Label ?? string.Empty).Trim();
        if (price1Label.Length == 0)
        {
            price1Label = "General";
        }

        price1Label = Clip(price1Label, 20);
        var convOz = request.ConvFactorOz == 0 ? 31.10347m : request.ConvFactorOz;

        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional(
                "INSERT INTO `epc_jewel_metal_stock` (`company_id`,`metal`,`item_code`,`description`,`cc_making`,`cc_metal`,`karat`,`purity`,`type`,`brand`,`category`,`sub_category`,`vendor`,`vendor_ref`,`country`,`hs_code`,`mc_unit`,`include_stone_weight`,`pass_purity_diff`,`in_pieces`,`pc_weight_gms`,`pc_weight_oz`,`create_barcodes`,`barcode_prefix`,`block_gross_wt_sales`,`ask_supplier`,`ask_wastage`,`exclude_gst_trn`,`gst_trn_on_making_stone`,`allow_negative_stock`,`allow_less_than_cost`,`conv_factor_oz`,`abc_code`,`loyalty_item`,`std_cost`,`discount_pct`,`min_qty`,`max_qty`,`reorder_level`,`reorder_qty`,`pur_cost_gms`,`sale_price_gms`,`price1_code`,`price1_label`,`created_by`) VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?) ON DUPLICATE KEY UPDATE `metal` = VALUES(`metal`), `description` = VALUES(`description`), `cc_making` = VALUES(`cc_making`), `cc_metal` = VALUES(`cc_metal`), `karat` = VALUES(`karat`), `purity` = VALUES(`purity`), `type` = VALUES(`type`), `brand` = VALUES(`brand`), `category` = VALUES(`category`), `sub_category` = VALUES(`sub_category`), `vendor` = VALUES(`vendor`), `vendor_ref` = VALUES(`vendor_ref`), `country` = VALUES(`country`), `hs_code` = VALUES(`hs_code`), `mc_unit` = VALUES(`mc_unit`), `include_stone_weight` = VALUES(`include_stone_weight`), `pass_purity_diff` = VALUES(`pass_purity_diff`), `in_pieces` = VALUES(`in_pieces`), `pc_weight_gms` = VALUES(`pc_weight_gms`), `pc_weight_oz` = VALUES(`pc_weight_oz`), `create_barcodes` = VALUES(`create_barcodes`), `barcode_prefix` = VALUES(`barcode_prefix`), `block_gross_wt_sales` = VALUES(`block_gross_wt_sales`), `ask_supplier` = VALUES(`ask_supplier`), `ask_wastage` = VALUES(`ask_wastage`), `exclude_gst_trn` = VALUES(`exclude_gst_trn`), `gst_trn_on_making_stone` = VALUES(`gst_trn_on_making_stone`), `allow_negative_stock` = VALUES(`allow_negative_stock`), `allow_less_than_cost` = VALUES(`allow_less_than_cost`), `conv_factor_oz` = VALUES(`conv_factor_oz`), `abc_code` = VALUES(`abc_code`), `loyalty_item` = VALUES(`loyalty_item`), `std_cost` = VALUES(`std_cost`), `discount_pct` = VALUES(`discount_pct`), `min_qty` = VALUES(`min_qty`), `max_qty` = VALUES(`max_qty`), `reorder_level` = VALUES(`reorder_level`), `reorder_qty` = VALUES(`reorder_qty`), `pur_cost_gms` = VALUES(`pur_cost_gms`), `sale_price_gms` = VALUES(`sale_price_gms`), `price1_code` = VALUES(`price1_code`), `price1_label` = VALUES(`price1_label`), `created_by` = VALUES(`created_by`)"),
            cancellationToken,
            companyId,
            metal,
            code,
            Clip((request.Description ?? string.Empty).Trim(), 120),
            Clip((request.CcMaking ?? string.Empty).Trim(), 20),
            Clip((request.CcMetal ?? string.Empty).Trim(), 20),
            Clip((request.Karat ?? string.Empty).Trim(), 10),
            RoundNonNeg(request.Purity, 6),
            Clip((request.Type ?? string.Empty).Trim(), 30),
            Clip((request.Brand ?? string.Empty).Trim(), 60),
            Clip((request.Category ?? string.Empty).Trim(), 30),
            Clip((request.SubCategory ?? string.Empty).Trim(), 30),
            Clip((request.Vendor ?? string.Empty).Trim(), 20),
            Clip((request.VendorRef ?? string.Empty).Trim(), 20),
            Clip((request.Country ?? string.Empty).Trim(), 30),
            Clip((request.HsCode ?? string.Empty).Trim(), 20),
            mcUnit,
            request.IncludeStoneWeight ? 1 : 0,
            request.PassPurityDiff ? 1 : 0,
            request.InPieces ? 1 : 0,
            RoundNonNeg(request.PcWeightGms, 5),
            RoundNonNeg(request.PcWeightOz, 5),
            request.CreateBarcodes ? 1 : 0,
            Clip((request.BarcodePrefix ?? string.Empty).Trim(), 10),
            request.BlockGrossWtSales ? 1 : 0,
            request.AskSupplier ? 1 : 0,
            request.AskWastage ? 1 : 0,
            request.ExcludeGstTrn ? 1 : 0,
            request.GstTrnOnMakingStone ? 1 : 0,
            request.AllowNegativeStock ? 1 : 0,
            request.AllowLessThanCost ? 1 : 0,
            RoundNonNeg(convOz, 5),
            Clip((request.AbcCode ?? string.Empty).Trim().ToUpperInvariant(), 1),
            request.LoyaltyItem ? 1 : 0,
            RoundNonNeg(request.StdCost, 2),
            RoundNonNeg(request.DiscountPct, 2),
            RoundNonNeg(request.MinQty, 2),
            RoundNonNeg(request.MaxQty, 2),
            RoundNonNeg(request.ReorderLevel, 2),
            RoundNonNeg(request.ReorderQty, 2),
            RoundNonNeg(request.PurCostGms, 2),
            RoundNonNeg(request.SalePriceGms, 2),
            price1Code,
            price1Label,
            Clip((request.CreatedBy ?? string.Empty).Trim(), 40)).ConfigureAwait(false);

        var id = await ErpDb.LastInsertIdAsync(connection, null, cancellationToken).ConfigureAwait(false);
        if (id <= 0)
        {
            id = await ErpDb.LongAsync(
                connection,
                null,
                ErpDb.Positional("SELECT `id` FROM `epc_jewel_metal_stock` WHERE `company_id` = ? AND `item_code` = ? LIMIT 1"),
                cancellationToken,
                companyId,
                code).ConfigureAwait(false);
        }

        if (id <= 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Failed");
        }

        return ErpSimpleWriteResult.Ok("Metal stock " + code + " saved", id);
    }

    private static async Task<bool> TableExistsAsync(DbConnection connection, string table, CancellationToken cancellationToken)
    {
        var n = await ErpDb.LongAsync(
            connection,
            null,
            ErpDb.Positional("SELECT COUNT(*) FROM information_schema.TABLES WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = ?"),
            cancellationToken,
            table).ConfigureAwait(false);
        return n > 0;
    }

    private static decimal RoundNonNeg(decimal value, int decimals)
        => decimal.Round(value < 0 ? 0 : value, decimals, MidpointRounding.AwayFromZero);

    private static string Clip(string value, int maxLen)
        => value.Length <= maxLen ? value : value[..maxLen];
}
