using System.Data.Common;
using System.Text.Json;

namespace EcomAE.Platform.Erp;

public sealed record ErpIndustryField(string Key, string Label, string Type, string[]? Options = null);

public sealed record ErpIndustryBlueprint(
    string Key,
    string Label,
    string ItemType,
    int TrackExpiry,
    string DefaultUnit,
    string[] Units,
    string TaxCategory,
    ErpIndustryField[] Fields,
    string Notes);

public sealed record ErpIndustryApplyResult(
    bool Status,
    string Message,
    string IndustryKey,
    int FieldsSeeded,
    string ItemType,
    string DefaultUnit,
    int TrackExpiry,
    string TaxCategory);

public sealed record ErpIndustryCurrent(string Key, string Label, string ItemType, string DefaultUnit, string TrackExpiry);

public interface IErpIndustryApplyWriteService
{
    Task<ErpIndustryApplyResult> ApplyAsync(string key, long adminId, CancellationToken cancellationToken = default);

    Task<ErpIndustryCurrent> CurrentAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Live twin of PHP <c>epc_erp_industry_apply()</c> / <c>epc_erp_industry_current()</c>
/// (content/shop/finance/epc_erp_industry.php): seeds the blueprint's inventory custom-field
/// definitions, writes the ERP defaults into <c>epc_price_settings</c> and records an
/// <c>epc_erp_industry_state</c> audit row. Additive and idempotent, tenant-DB scoped.
/// </summary>
public sealed class ErpIndustryApplyWriteService : IErpIndustryApplyWriteService
{
    private static ErpIndustryField T(string key, string label) => new(key, label, "text");
    private static ErpIndustryField N(string key, string label) => new(key, label, "number");
    private static ErpIndustryField D(string key, string label) => new(key, label, "date");
    private static ErpIndustryField S(string key, string label, params string[] options) => new(key, label, "select", options);

    public static readonly IReadOnlyList<ErpIndustryBlueprint> Catalog =
    [
        new("general", "General retail / trading", "standard", 0, "pcs", ["pcs", "box", "set", "kg", "m"], "goods",
            [T("barcode", "Barcode / UPC"), T("brand", "Brand")],
            "Balanced default that suits most resellers and trading businesses."),
        new("auto_parts", "Auto parts & accessories", "standard", 0, "pcs", ["pcs", "set", "pair", "litre", "box"], "goods",
            [T("oem_number", "OEM / article number"), T("brand", "Brand / manufacturer"), T("fits_make", "Fits make"), T("fits_model", "Fits model"), T("fits_year", "Fits year range"),
             S("side_position", "Position", "Front", "Rear", "Left", "Right", "Upper", "Lower", "N/A")],
            "Brand + article (cross-reference) driven, matching the spare-parts workflow."),
        new("electronics", "Consumer electronics", "serialized", 0, "pcs", ["pcs", "box", "set"], "goods",
            [T("brand", "Brand"), T("model_no", "Model number"), N("warranty_months", "Warranty (months)"), T("imei_serial", "IMEI / serial"), T("voltage", "Voltage / power")],
            "Serialized tracking with warranty for phones, laptops, appliances."),
        new("fashion", "Fashion & apparel", "standard", 0, "pcs", ["pcs", "pair", "set"], "goods",
            [T("brand", "Brand"), S("size", "Size", "XS", "S", "M", "L", "XL", "XXL", "3XL"), T("colour", "Colour"), T("material", "Material / fabric"),
             S("gender", "Gender", "Men", "Women", "Unisex", "Kids"), T("season", "Season")],
            "Size / colour / material variants for clothing and footwear."),
        new("jewellery", "Jewellery & watches", "serialized", 0, "pcs", ["pcs", "gram", "carat", "pair"], "goods",
            [S("metal", "Metal", "Gold", "Silver", "Platinum", "White Gold", "Rose Gold", "Steel"), T("purity", "Purity / karat"), N("gross_weight", "Gross weight (g)"),
             T("stone_type", "Stone type"), N("stone_carat", "Stone carat"), T("hallmark", "Hallmark / cert no.")],
            "Weight, purity and certificate tracking with per-piece serialization."),
        new("food_perishable", "Food, grocery & perishables", "perishable", 1, "kg", ["kg", "gram", "litre", "ml", "pcs", "pack", "box"], "goods",
            [T("brand", "Brand"), T("batch_no", "Batch / lot number"), D("expiry_date", "Expiry / best-before"), T("storage_temp", "Storage temperature"), S("is_halal", "Halal certified", "Yes", "No")],
            "Batch + expiry (FEFO) tracking for grocery, bakery and FMCG."),
        new("pharma", "Pharmacy & medical supplies", "perishable", 1, "pcs", ["pcs", "box", "strip", "bottle", "ml", "gram"], "goods",
            [T("generic_name", "Generic name"), T("batch_no", "Batch / lot number"), D("expiry_date", "Expiry date"), T("dosage", "Strength / dosage"), T("reg_no", "Drug registration no."),
             S("rx_required", "Prescription required", "Yes", "No")],
            "Regulatory + batch/expiry control for pharmacies and clinics."),
        new("cosmetics_beauty", "Cosmetics & beauty", "perishable", 1, "pcs", ["pcs", "ml", "gram", "box", "set"], "goods",
            [T("brand", "Brand"), T("shade", "Shade / variant"), T("volume", "Volume / size"), D("expiry_date", "Expiry / PAO"), T("skin_type", "Skin / hair type")],
            "Shade / volume variants with shelf-life tracking."),
        new("furniture_home", "Furniture & home", "standard", 0, "pcs", ["pcs", "set", "m", "sqm"], "goods",
            [T("brand", "Brand"), T("material", "Material"), T("dimensions", "Dimensions (WxDxH)"), T("colour", "Colour / finish"), S("assembly", "Assembly required", "Yes", "No")],
            "Dimension and material driven, bulky-goods friendly."),
        new("building_construction", "Building materials & hardware", "standard", 0, "pcs", ["pcs", "kg", "ton", "bag", "m", "sqm", "cum", "litre"], "goods",
            [T("grade", "Grade / spec"), T("dimensions", "Size / dimensions"), T("material", "Material"), T("standard", "Standard (ASTM/EN/BS)")],
            "Bulk units (ton, bag, cubic metre) for hardware and construction."),
        new("industrial_manufacturing", "Manufacturing & industrial", "standard", 0, "pcs", ["pcs", "kg", "m", "litre", "roll", "set", "box"], "goods",
            [T("part_no", "Part number"), T("material_grade", "Material grade"), T("tolerance", "Tolerance / spec"),
             S("item_role", "Item role", "Raw material", "Component", "WIP", "Finished good", "Consumable")],
            "Raw-material / component / finished-good classification for BOM-style ops."),
        new("agriculture", "Agriculture & farming", "perishable", 1, "kg", ["kg", "ton", "bag", "litre", "pcs", "crate"], "goods",
            [T("variety", "Variety / breed"), T("grade", "Grade / quality"), D("harvest_date", "Harvest / pack date"), T("origin", "Origin / farm")],
            "Grade and harvest-date tracking for produce and inputs."),
        new("books_media", "Books, stationery & media", "standard", 0, "pcs", ["pcs", "set", "pack", "box"], "goods",
            [T("isbn", "ISBN / barcode"), T("author", "Author / publisher"), T("language", "Language"), S("format", "Format", "Paperback", "Hardcover", "Digital", "Audio")],
            "ISBN / title driven catalogue for books and stationery."),
        new("hospitality_fnb", "Restaurant & hospitality", "perishable", 1, "pcs", ["pcs", "plate", "kg", "gram", "litre", "ml", "portion"], "goods",
            [T("category", "Menu category"), S("is_veg", "Vegetarian", "Veg", "Non-veg", "Vegan"), T("allergens", "Allergens"), D("expiry_date", "Prep / expiry")],
            "Menu items + raw ingredients with allergen and prep tracking."),
        new("services_professional", "Professional / advisory services", "standard", 0, "hour", ["hour", "day", "project", "retainer", "unit"], "services",
            [T("service_code", "Service code"), S("billing_basis", "Billing basis", "Hourly", "Fixed", "Retainer", "Milestone"), T("sla", "SLA / turnaround")],
            "Time / project billing for consulting, tax advisory, agencies."),
    ];

    public static ErpIndustryBlueprint? Blueprint(string key) =>
        Catalog.FirstOrDefault(b => string.Equals(b.Key, key, StringComparison.Ordinal));

    private readonly IErpWriteConnectionFactory _connections;

    public ErpIndustryApplyWriteService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public static async Task EnsureSchemaAsync(DbConnection c, CancellationToken ct)
    {
        await ErpDb.TryExecuteAsync(c,
            "CREATE TABLE IF NOT EXISTS `epc_price_settings` (" +
            "`setting_key` varchar(128) NOT NULL," +
            "`setting_value` text," +
            "PRIMARY KEY (`setting_key`)" +
            ") ENGINE=InnoDB DEFAULT CHARSET=utf8", ct);
        await ErpDb.TryExecuteAsync(c,
            "CREATE TABLE IF NOT EXISTS `epc_erp_inv_field_defs` (" +
            "`id` int(11) NOT NULL AUTO_INCREMENT," +
            "`field_key` varchar(32) NOT NULL," +
            "`label` varchar(120) NOT NULL," +
            "`field_type` enum('text','number','date','select') NOT NULL DEFAULT 'text'," +
            "`options_json` text," +
            "`sort_order` int(11) NOT NULL DEFAULT 0," +
            "`active` tinyint(1) NOT NULL DEFAULT 1," +
            "PRIMARY KEY (`id`)," +
            "UNIQUE KEY `x_key` (`field_key`)" +
            ") ENGINE=InnoDB DEFAULT CHARSET=utf8", ct);
        await ErpDb.TryExecuteAsync(c,
            "CREATE TABLE IF NOT EXISTS `epc_erp_industry_state` (" +
            "`id` int(11) NOT NULL AUTO_INCREMENT," +
            "`industry_key` varchar(64) NOT NULL," +
            "`item_type` varchar(32) NOT NULL DEFAULT 'standard'," +
            "`default_unit` varchar(32) NOT NULL DEFAULT 'pcs'," +
            "`fields_seeded` int(11) NOT NULL DEFAULT 0," +
            "`applied_by` int(11) NOT NULL DEFAULT 0," +
            "`time_applied` int(11) NOT NULL DEFAULT 0," +
            "PRIMARY KEY (`id`)," +
            "KEY `x_industry` (`industry_key`)" +
            ") ENGINE=InnoDB DEFAULT CHARSET=utf8", ct);
    }

    public async Task<ErpIndustryApplyResult> ApplyAsync(string key, long adminId, CancellationToken cancellationToken = default)
    {
        var bp = Blueprint(key ?? string.Empty);
        if (bp is null)
        {
            return new ErpIndustryApplyResult(false, "Unknown industry: " + key, key ?? string.Empty, 0, "", "", 0, "");
        }

        if (!_connections.IsConfigured)
        {
            return new ErpIndustryApplyResult(false, "Database unavailable", bp.Key, 0, bp.ItemType, bp.DefaultUnit, bp.TrackExpiry, bp.TaxCategory);
        }

        await using var c = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await EnsureSchemaAsync(c, cancellationToken).ConfigureAwait(false);

        var seeded = 0;
        var sort = 0;
        foreach (var f in bp.Fields)
        {
            sort += 10;
            var optionsJson = f.Type == "select" && f.Options is { Length: > 0 }
                ? JsonSerializer.Serialize(f.Options)
                : null;
            await ErpDb.ExecuteAsync(c, null, ErpDb.Positional(
                "INSERT INTO `epc_erp_inv_field_defs` (`field_key`,`label`,`field_type`,`options_json`,`sort_order`,`active`) VALUES (?,?,?,?,?,1) " +
                "ON DUPLICATE KEY UPDATE `label`=VALUES(`label`),`field_type`=VALUES(`field_type`),`options_json`=VALUES(`options_json`),`active`=1"),
                cancellationToken,
                f.Key.Length > 32 ? f.Key[..32] : f.Key,
                f.Label.Length > 120 ? f.Label[..120] : f.Label,
                f.Type,
                optionsJson,
                sort).ConfigureAwait(false);
            seeded++;
        }

        await SetSettingAsync(c, "erp_industry_profile", bp.Key, cancellationToken).ConfigureAwait(false);
        await SetSettingAsync(c, "erp_default_item_type", bp.ItemType, cancellationToken).ConfigureAwait(false);
        await SetSettingAsync(c, "erp_default_unit", bp.DefaultUnit, cancellationToken).ConfigureAwait(false);
        await SetSettingAsync(c, "erp_track_expiry", bp.TrackExpiry.ToString(), cancellationToken).ConfigureAwait(false);
        await SetSettingAsync(c, "erp_tax_category", bp.TaxCategory, cancellationToken).ConfigureAwait(false);

        await ErpDb.ExecuteAsync(c, null, ErpDb.Positional(
            "INSERT INTO `epc_erp_industry_state` (`industry_key`,`item_type`,`default_unit`,`fields_seeded`,`applied_by`,`time_applied`) VALUES (?,?,?,?,?,?)"),
            cancellationToken, bp.Key, bp.ItemType, bp.DefaultUnit, seeded, adminId, DateTimeOffset.UtcNow.ToUnixTimeSeconds()).ConfigureAwait(false);

        return new ErpIndustryApplyResult(true, "Industry \"" + bp.Label + "\" applied", bp.Key, seeded, bp.ItemType, bp.DefaultUnit, bp.TrackExpiry, bp.TaxCategory);
    }

    public async Task<ErpIndustryCurrent> CurrentAsync(CancellationToken cancellationToken = default)
    {
        if (!_connections.IsConfigured)
        {
            return new ErpIndustryCurrent("", "", "standard", "pcs", "0");
        }

        await using var c = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await EnsureSchemaAsync(c, cancellationToken).ConfigureAwait(false);
        var key = await GetSettingAsync(c, "erp_industry_profile", cancellationToken).ConfigureAwait(false) ?? "";
        var bp = key.Length > 0 ? Blueprint(key) : null;
        return new ErpIndustryCurrent(
            key,
            bp?.Label ?? "",
            await GetSettingAsync(c, "erp_default_item_type", cancellationToken).ConfigureAwait(false) ?? bp?.ItemType ?? "standard",
            await GetSettingAsync(c, "erp_default_unit", cancellationToken).ConfigureAwait(false) ?? bp?.DefaultUnit ?? "pcs",
            await GetSettingAsync(c, "erp_track_expiry", cancellationToken).ConfigureAwait(false) ?? (bp?.TrackExpiry ?? 0).ToString());
    }

    private static Task SetSettingAsync(DbConnection c, string key, string value, CancellationToken ct) =>
        ErpDb.ExecuteAsync(c, null, ErpDb.Positional(
            "INSERT INTO `epc_price_settings` (`setting_key`,`setting_value`) VALUES (?,?) ON DUPLICATE KEY UPDATE `setting_value`=VALUES(`setting_value`)"),
            ct, key, value);

    private static async Task<string?> GetSettingAsync(DbConnection c, string key, CancellationToken ct)
    {
        var v = await ErpDb.StringAsync(c, null, ErpDb.Positional("SELECT `setting_value` FROM `epc_price_settings` WHERE `setting_key`=? LIMIT 1"), ct, key).ConfigureAwait(false);
        return string.IsNullOrEmpty(v) ? null : v;
    }
}
