using System.Globalization;

namespace EcomAE.Platform.Erp;

/// <summary>One rendered row of a data-backed PIM workspace tab (already stringified).</summary>
public sealed record ErpPimWorkspaceData(
    IReadOnlyList<string> Headers,
    IReadOnlyList<IReadOnlyList<string>> Rows,
    string EmptyHint,
    string? ManageHref);

/// <summary>
/// Real read model for the "Product information management" workspace tabs.
/// Tabs not in <see cref="ErpPimWorkspaceReadService.Specs"/> keep the generic
/// workspace record grid (cases, paper setup, and other genuine scratch data).
/// </summary>
public interface IErpPimWorkspaceReadService
{
    bool IsDataTab(string? tab);
    Task<ErpPimWorkspaceData?> LoadAsync(string? tab, CancellationToken cancellationToken = default);
}

public sealed class ErpPimWorkspaceReadService : IErpPimWorkspaceReadService
{
    private const string Items = "`epc_erp_inv_items`";
    private const string Bom = "`epc_erp_inv_item_bom`";
    private const string Variants = "`epc_erp_prod_variants`";
    private const string FieldDefs = "`epc_erp_inv_field_defs`";
    private const string ItemFields = "`epc_erp_inv_item_fields`";

    public sealed record PimSpec(IReadOnlyList<string> Headers, string Sql, string EmptyHint, string? ManageHref);

    private const string ItemCols =
        "IFNULL(i.`sku`,''), IFNULL(i.`name`,''), IFNULL(i.`item_type`,'standard'), IFNULL(i.`item_group`,''), " +
        "IFNULL(i.`unit`,'pcs'), IFNULL(i.`sales_price`,0), IFNULL(i.`active`,1)";

    private const string ItemColsCsv =
        "IFNULL(i.`sku`,''), IFNULL(i.`search_name`,''), IFNULL(i.`name`,''), IFNULL(i.`item_type`,'standard'), " +
        "IFNULL(i.`product_type`,'item'), IFNULL(i.`item_group`,''), IFNULL(i.`unit`,'pcs'), " +
        "IFNULL(i.`purchase_unit`,''), IFNULL(i.`sales_unit`,''), IFNULL(i.`sales_price`,0), " +
        "IFNULL(i.`costing_method`,''), IFNULL(i.`barcode`,''), IFNULL(i.`reorder_level`,0), " +
        "IFNULL(i.`track_expiry`,0), IFNULL(i.`active`,1), IFNULL(i.`product_id`,0), IFNULL(i.`time_created`,0)";

    private const string BomJoin =
        " FROM `epc_erp_inv_item_bom` b JOIN `epc_erp_inv_items` i ON i.`id` = b.`assembly_item_id`";

    public static readonly IReadOnlyDictionary<string, PimSpec> Specs =
        new Dictionary<string, PimSpec>(StringComparer.OrdinalIgnoreCase)
        {
            ["pim_variant_model"] = new(
                ["Item", "Product", "Variants", "Variant labels"],
                "SELECT " + ItemCols + ", COUNT(v.`id`), LEFT(GROUP_CONCAT(v.`variant_label` ORDER BY v.`id` SEPARATOR ', '),200)" +
                " FROM `epc_erp_prod_variants` v JOIN `epc_erp_inv_items` i ON i.`id` = v.`item_id`" +
                " WHERE IFNULL(v.`active`,1) = 1 GROUP BY v.`item_id` ORDER BY i.`sku` LIMIT 200",
                "No variant models yet — create variants on a product master in Product information.",
                "/erp/product-info-app"),

            ["pim_readiness_discrete"] = new(
                ["Item", "Product", "Unit", "Sales price", "BOM lines", "Readiness"],
                "SELECT IFNULL(i.`sku`,''), IFNULL(i.`name`,''), IFNULL(i.`unit`,'pcs'), IFNULL(i.`sales_price`,0)," +
                " (SELECT COUNT(*) FROM `epc_erp_inv_item_bom` b WHERE b.`assembly_item_id` = i.`id`)," +
                " CASE WHEN IFNULL(i.`active`,1) <> 1 THEN 'Not released'" +
                "      WHEN IFNULL(i.`sales_price`,0) <= 0 THEN 'Missing price'" +
                "      WHEN NOT EXISTS (SELECT 1 FROM `epc_erp_inv_item_bom` b WHERE b.`assembly_item_id` = i.`id`) THEN 'Missing BOM'" +
                "      ELSE 'Ready' END" +
                " FROM `epc_erp_inv_items` i ORDER BY i.`sku` LIMIT 200",
                "No released products — release items in Product information first.",
                "/erp/product-info-app"),

            ["pim_readiness_process"] = new(
                ["Item", "Product", "Unit", "Purchase unit", "Sales price", "Formula lines", "Shelf life", "Readiness"],
                "SELECT IFNULL(i.`sku`,''), IFNULL(i.`name`,''), IFNULL(i.`unit`,'pcs'), IFNULL(i.`purchase_unit`,'')," +
                " IFNULL(i.`sales_price`,0)," +
                " (SELECT COUNT(*) FROM `epc_erp_inv_item_bom` b WHERE b.`assembly_item_id` = i.`id`)," +
                " IFNULL(i.`track_expiry`,0)," +
                " CASE WHEN IFNULL(i.`active`,1) <> 1 THEN 'Not released'" +
                "      WHEN IFNULL(i.`sales_price`,0) <= 0 THEN 'Missing price'" +
                "      WHEN NOT EXISTS (SELECT 1 FROM `epc_erp_inv_item_bom` b WHERE b.`assembly_item_id` = i.`id`) THEN 'Missing formula'" +
                "      WHEN IFNULL(i.`purchase_unit`,'') = '' THEN 'Missing purchase unit'" +
                "      ELSE 'Ready' END" +
                " FROM `epc_erp_inv_items` i ORDER BY i.`sku` LIMIT 200",
                "No released products — release items in Product information first.",
                "/erp/product-info-app"),

            ["pim_by_category"] = new(
                ["Category", "Products", "Released", "Items"],
                "SELECT IFNULL(NULLIF(i.`item_group`,''),'(uncategorized)'), COUNT(*), SUM(IFNULL(i.`active`,1) = 1)," +
                " LEFT(GROUP_CONCAT(i.`sku` ORDER BY i.`sku` SEPARATOR ', '),200)" +
                " FROM `epc_erp_inv_items` i GROUP BY 1 ORDER BY 1 LIMIT 200",
                "No products yet — items pick up their category from the item group.",
                "/erp/product-info-app"),

            ["pim_released_by_cat"] = new(
                ["Category", "Released products", "Items"],
                "SELECT IFNULL(NULLIF(i.`item_group`,''),'(uncategorized)'), COUNT(*)," +
                " LEFT(GROUP_CONCAT(i.`sku` ORDER BY i.`sku` SEPARATOR ', '),200)" +
                " FROM `epc_erp_inv_items` i WHERE IFNULL(i.`active`,1) = 1 GROUP BY 1 ORDER BY 1 LIMIT 200",
                "No released products — release items in Product information first.",
                "/erp/product-info-app"),

            ["pim_config_models"] = new(
                ["Field", "Label", "Type", "Role", "Active", "Options"],
                "SELECT IFNULL(f.`field_key`,''), IFNULL(f.`label`,''), IFNULL(f.`field_type`,'text')," +
                " IFNULL(f.`field_role`,'inventory'), IFNULL(f.`active`,1), IFNULL(f.`options_json`,'')" +
                " FROM `epc_erp_inv_field_defs` f ORDER BY f.`sort_order`, f.`id` LIMIT 200",
                "No configurable attributes yet — define PIM attributes in Product information.",
                "/erp/product-info-app"),

            ["pim_part_number"] = new(
                ["Item", "Barcode / part number", "Product", "Unit", "Category", "Released"],
                "SELECT IFNULL(i.`sku`,''), IFNULL(i.`barcode`,''), IFNULL(i.`name`,''), IFNULL(i.`unit`,'pcs')," +
                " IFNULL(i.`item_group`,''), IFNULL(i.`active`,1)" +
                " FROM `epc_erp_inv_items` i ORDER BY i.`sku` LIMIT 200",
                "No items yet.",
                "/erp/product-info-app"),

            ["pim_cross_part"] = new(
                ["Item", "Cross field", "Value"],
                "SELECT IFNULL(i.`sku`,''), fv.`field_key`, fv.`value`" +
                " FROM `epc_erp_inv_item_fields` fv JOIN `epc_erp_inv_items` i ON i.`id` = fv.`item_id`" +
                " ORDER BY i.`sku`, fv.`field_key` LIMIT 200",
                "No cross part numbers stored — item-level fields (barcodes, vendor part numbers) appear here once set.",
                "/erp/product-info-app"),

            ["pim_item_csv"] = new(
                ["sku", "search_name", "name", "item_type", "product_type", "item_group", "unit",
                 "purchase_unit", "sales_unit", "sales_price", "costing_method", "barcode",
                 "reorder_level", "track_expiry", "active", "product_id", "time_created"],
                "SELECT " + ItemColsCsv + " FROM `epc_erp_inv_items` i ORDER BY i.`sku` LIMIT 500",
                "No items yet — use Product upload to import a CSV.",
                null),

            ["pim_item_update"] = new(
                ["Item", "Product", "Type", "Category", "Unit", "Sales price", "Released"],
                "SELECT " + ItemCols + " FROM `epc_erp_inv_items` i ORDER BY i.`sku` LIMIT 200",
                "No items yet.",
                "/erp/product-info-app"),

            ["pim_track_storage"] = new(
                ["Item", "Product", "Unit", "Purchase unit", "Sales unit", "Shelf life", "Warehouse", "Reorder level", "Costing"],
                "SELECT IFNULL(i.`sku`,''), IFNULL(i.`name`,''), IFNULL(i.`unit`,'pcs'), IFNULL(i.`purchase_unit`,'')," +
                " IFNULL(i.`sales_unit`,''), IFNULL(i.`track_expiry`,0), IFNULL(i.`default_warehouse_id`,0)," +
                " IFNULL(i.`reorder_level`,0), IFNULL(i.`costing_method`,'')" +
                " FROM `epc_erp_inv_items` i ORDER BY i.`sku` LIMIT 200",
                "No items yet.",
                "/erp/product-info-app"),

            ["pim_cross_ref"] = new(
                ["Item", "Cross field", "Value"],
                "SELECT IFNULL(i.`sku`,''), fv.`field_key`, fv.`value`" +
                " FROM `epc_erp_inv_item_fields` fv JOIN `epc_erp_inv_items` i ON i.`id` = fv.`item_id`" +
                " ORDER BY i.`sku`, fv.`field_key` LIMIT 200",
                "No cross references stored — customer/vendor item numbers appear here once set on items.",
                "/erp/product-info-app"),

            ["pim_boms"] = new(
                ["Item", "Product", "Components", "Rolled-up cost", "Last line"],
                "SELECT IFNULL(i.`sku`,''), IFNULL(i.`name`,''), COUNT(b.`id`)," +
                " IFNULL(SUM(b.`qty` * b.`unit_cost`),0), IFNULL(MAX(b.`line_no`),0)" +
                BomJoin + " GROUP BY b.`assembly_item_id` ORDER BY i.`sku` LIMIT 200",
                "No bills of materials yet — add components to an assembly item.",
                "/erp/product-info-app"),

            ["pim_formulas"] = new(
                ["Item", "Product", "Ingredients", "Rolled-up cost", "Process units"],
                "SELECT IFNULL(i.`sku`,''), IFNULL(i.`name`,''), COUNT(b.`id`)," +
                " IFNULL(SUM(b.`qty` * b.`unit_cost`),0), LEFT(GROUP_CONCAT(DISTINCT b.`unit` SEPARATOR ', '),80)" +
                BomJoin + " WHERE b.`unit` <> 'pcs' GROUP BY b.`assembly_item_id` ORDER BY i.`sku` LIMIT 200",
                "No formulas yet — process formulas are BOMs whose lines use process units (kg, L, m).",
                "/erp/product-info-app"),

            ["pim_bom_versions"] = new(
                ["Item", "Product", "Lines", "Total qty", "Rolled-up cost"],
                "SELECT IFNULL(i.`sku`,''), IFNULL(i.`name`,''), COUNT(b.`id`)," +
                " IFNULL(SUM(b.`qty`),0), IFNULL(SUM(b.`qty` * b.`unit_cost`),0)" +
                BomJoin + " GROUP BY b.`assembly_item_id` ORDER BY i.`sku` LIMIT 200",
                "No BOM versions yet.",
                "/erp/product-info-app"),

            ["pim_bom_lines"] = new(
                ["Item", "Line", "Component", "Description", "Qty", "Unit", "Unit cost"],
                "SELECT IFNULL(i.`sku`,''), b.`line_no`, IFNULL(b.`component_sku`,''), IFNULL(b.`description`,'')," +
                " b.`qty`, IFNULL(b.`unit`,'pcs'), b.`unit_cost`" +
                BomJoin + " ORDER BY i.`sku`, b.`line_no` LIMIT 500",
                "No BOM lines yet.",
                "/erp/product-info-app"),

            ["pim_lean_schedule"] = new(
                ["Schedule group", "Items", "Released", "Units"],
                "SELECT IFNULL(NULLIF(i.`item_group`,''),'(uncategorized)'), COUNT(*), SUM(IFNULL(i.`active`,1) = 1)," +
                " LEFT(GROUP_CONCAT(DISTINCT i.`unit` SEPARATOR ', '),80)" +
                " FROM `epc_erp_inv_items` i GROUP BY 1 ORDER BY 1 LIMIT 200",
                "No lean schedule groups — item groups act as the schedule grouping.",
                "/erp/product-info-app"),

            ["pim_kanban_rules"] = new(
                ["Item", "Product", "Unit", "Reorder level", "Warehouse"],
                "SELECT IFNULL(i.`sku`,''), IFNULL(i.`name`,''), IFNULL(i.`unit`,'pcs')," +
                " IFNULL(i.`reorder_level`,0), IFNULL(i.`default_warehouse_id`,0)" +
                " FROM `epc_erp_inv_items` i WHERE IFNULL(i.`reorder_level`,0) > 0 ORDER BY i.`sku` LIMIT 200",
                "No replenishment rules — set a reorder level on an item to create a kanban rule.",
                "/erp/product-info-app"),

            ["pim_circulating"] = new(
                ["Card", "Label", "Base item"],
                "SELECT IFNULL(v.`variant_sku`,''), IFNULL(v.`variant_label`,''), IFNULL(i.`sku`,'')" +
                " FROM `epc_erp_prod_variants` v JOIN `epc_erp_inv_items` i ON i.`id` = v.`item_id`" +
                " WHERE IFNULL(v.`active`,1) = 1 ORDER BY v.`id` DESC LIMIT 200",
                "No circulating kanban cards.",
                "/erp/product-info-app"),

            ["pim_costing_versions"] = new(
                ["Costing method", "Items", "Released"],
                "SELECT IFNULL(NULLIF(i.`costing_method`,''),'(standard)'), COUNT(*), SUM(IFNULL(i.`active`,1) = 1)" +
                " FROM `epc_erp_inv_items` i GROUP BY 1 ORDER BY 1 LIMIT 50",
                "No costing versions — items use the standard costing method until one is set.",
                "/erp/product-info-app"),

            ["pim_cost_calc"] = new(
                ["Item", "Product", "Rolled-up cost", "Sales price", "Margin"],
                "SELECT IFNULL(i.`sku`,''), IFNULL(i.`name`,''), IFNULL(SUM(b.`qty` * b.`unit_cost`),0)," +
                " IFNULL(i.`sales_price`,0), IFNULL(i.`sales_price`,0) - IFNULL(SUM(b.`qty` * b.`unit_cost`),0)" +
                BomJoin + " GROUP BY b.`assembly_item_id` ORDER BY i.`sku` LIMIT 200",
                "Nothing to calculate — no BOMs defined.",
                "/erp/product-info-app"),

            ["pim_no_active_cost"] = new(
                ["Item", "Product", "Unit", "Category"],
                "SELECT IFNULL(i.`sku`,''), IFNULL(i.`name`,''), IFNULL(i.`unit`,'pcs'), IFNULL(i.`item_group`,'')" +
                " FROM `epc_erp_inv_items` i WHERE IFNULL(i.`active`,1) = 1 AND IFNULL(i.`sales_price`,0) <= 0" +
                " AND NOT EXISTS (SELECT 1 FROM `epc_erp_inv_item_bom` b WHERE b.`assembly_item_id` = i.`id`)" +
                " ORDER BY i.`sku` LIMIT 200",
                "Every released product has a price or a costed BOM — nothing missing.",
                "/erp/product-info-app"),

            ["pim_to_configure"] = new(
                ["Item", "Product", "Variants", "Pending"],
                "SELECT IFNULL(i.`sku`,''), IFNULL(i.`name`,''), COUNT(v.`id`), SUM(IFNULL(v.`active`,1) = 0)" +
                " FROM `epc_erp_prod_variants` v JOIN `epc_erp_inv_items` i ON i.`id` = v.`item_id`" +
                " GROUP BY v.`item_id` HAVING SUM(IFNULL(v.`active`,1) = 0) > 0 ORDER BY i.`sku` LIMIT 200",
                "All configured variants are released.",
                "/erp/product-info-app"),

            ["pim_calc_details"] = new(
                ["Item", "Component", "Qty", "Unit cost", "Extended cost"],
                "SELECT IFNULL(i.`sku`,''), IFNULL(b.`component_sku`,''), b.`qty`, b.`unit_cost`, b.`qty` * b.`unit_cost`" +
                BomJoin + " ORDER BY i.`sku`, b.`line_no` LIMIT 500",
                "No cost-calculation lines yet.",
                "/erp/product-info-app"),

            ["pim_lifecycle_hist"] = new(
                ["Action", "Entity", "Id", "Summary", "When"],
                "SELECT IFNULL(a.`action`,''), IFNULL(a.`entity_type`,''), IFNULL(a.`entity_id`,0)," +
                " LEFT(IFNULL(a.`summary`,''),120), FROM_UNIXTIME(IFNULL(a.`time_created`,0))" +
                " FROM `epc_erp_audit_log` a" +
                " WHERE a.`entity_type` IN ('item','pim_field','product','variant','bom')" +
                " ORDER BY a.`id` DESC LIMIT 100",
                "No product lifecycle history yet — item, attribute, and variant writes are logged here.",
                "/erp/product-info-app"),

            ["pim_inv_maker"] = new(
                ["Item", "Product", "Warehouse", "Unit", "Reorder level"],
                "SELECT IFNULL(i.`sku`,''), IFNULL(i.`name`,''), IFNULL(i.`default_warehouse_id`,0)," +
                " IFNULL(i.`unit`,'pcs'), IFNULL(i.`reorder_level`,0)" +
                " FROM `epc_erp_inv_items` i WHERE IFNULL(i.`default_warehouse_id`,0) > 0 ORDER BY i.`sku` LIMIT 200",
                "No stocked items — set a default warehouse on an item.",
                "/erp/product-info-app"),

            ["pim_haz_limits"] = new(
                ["Item", "Product", "Unit", "Stock limit"],
                "SELECT IFNULL(i.`sku`,''), IFNULL(i.`name`,''), IFNULL(i.`unit`,'pcs'), IFNULL(i.`reorder_level`,0)" +
                " FROM `epc_erp_inv_items` i WHERE IFNULL(i.`reorder_level`,0) > 0 ORDER BY i.`sku` LIMIT 200",
                "No stock limits set — set a reorder level on hazardous or controlled items.",
                "/erp/product-info-app"),

            ["pim_open_releases"] = new(
                ["Item", "Product", "Type", "Category", "Unit", "Sales price"],
                "SELECT " + ItemCols[..ItemCols.IndexOf(", IFNULL(i.`active`", StringComparison.Ordinal)] +
                " FROM `epc_erp_inv_items` i WHERE IFNULL(i.`active`,1) = 0 ORDER BY i.`sku` LIMIT 200",
                "No open releases — every product is released.",
                "/erp/product-info-app"),

            ["pim_retail_label"] = new(
                ["Item", "Barcode", "Product", "Sales price", "Unit"],
                "SELECT IFNULL(i.`sku`,''), IFNULL(i.`barcode`,''), IFNULL(i.`name`,'')," +
                " IFNULL(i.`sales_price`,0), IFNULL(i.`unit`,'pcs')" +
                " FROM `epc_erp_inv_items` i WHERE IFNULL(i.`barcode`,'') <> '' ORDER BY i.`sku` LIMIT 200",
                "No labelled items — set a barcode on an item to print labels.",
                "/erp/product-info-app"),

            ["pim_recalc_bom"] = new(
                ["Item", "Product", "Lines", "Total qty"],
                "SELECT IFNULL(i.`sku`,''), IFNULL(i.`name`,''), COUNT(b.`id`), IFNULL(SUM(b.`qty`),0)" +
                BomJoin + " GROUP BY b.`assembly_item_id` ORDER BY i.`sku` LIMIT 200",
                "No BOMs to recalculate.",
                "/erp/product-info-app"),

            ["pim_change_bom_item"] = new(
                ["Component", "Used on assemblies", "Total qty"],
                "SELECT IFNULL(b.`component_sku`,''), COUNT(DISTINCT b.`assembly_item_id`), IFNULL(SUM(b.`qty`),0)" +
                " FROM `epc_erp_inv_item_bom` b GROUP BY b.`component_sku` ORDER BY b.`component_sku` LIMIT 300",
                "No components yet — BOM lines appear here once assemblies have components.",
                "/erp/product-info-app"),

            ["pim_check_bom"] = new(
                ["Item", "Line", "Component", "Issue"],
                "SELECT IFNULL(i.`sku`,''), b.`line_no`, IFNULL(b.`component_sku`,''), 'Component is not an item'" +
                BomJoin +
                " WHERE NOT EXISTS (SELECT 1 FROM `epc_erp_inv_items` c WHERE c.`sku` = b.`component_sku`)" +
                " UNION ALL" +
                " SELECT IFNULL(i.`sku`,''), b.`line_no`, IFNULL(b.`component_sku`,''), 'Component is the assembly itself'" +
                BomJoin + " WHERE b.`component_sku` = i.`sku`" +
                " ORDER BY 1, 2 LIMIT 300",
                "Every BOM is clean — all components exist as items and none references itself.",
                "/erp/product-info-app"),

            ["pim_check_formula"] = new(
                ["Item", "Line", "Component", "Unit", "Issue"],
                "SELECT IFNULL(i.`sku`,''), b.`line_no`, IFNULL(b.`component_sku`,''), IFNULL(b.`unit`,'pcs')," +
                " CASE WHEN b.`qty` <= 0 THEN 'Quantity must be positive'" +
                "      WHEN NOT EXISTS (SELECT 1 FROM `epc_erp_inv_items` c WHERE c.`sku` = b.`component_sku`) THEN 'Component is not an item'" +
                "      ELSE 'OK' END" +
                BomJoin + " WHERE b.`unit` <> 'pcs' ORDER BY i.`sku`, b.`line_no` LIMIT 300",
                "No formula lines to validate — process formulas are BOMs whose lines use process units (kg, L, m).",
                "/erp/product-info-app"),

            ["pim_wh_hier"] = new(
                ["Item", "Product", "Warehouse", "Unit", "Shelf life"],
                "SELECT IFNULL(i.`sku`,''), IFNULL(i.`name`,''), IFNULL(i.`default_warehouse_id`,0)," +
                " IFNULL(i.`unit`,'pcs'), IFNULL(i.`track_expiry`,0)" +
                " FROM `epc_erp_inv_items` i WHERE IFNULL(i.`default_warehouse_id`,0) > 0 ORDER BY i.`sku` LIMIT 200",
                "No storage dimension assignments — set a default warehouse on an item.",
                "/erp/product-info-app"),

            ["pim_categories"] = new(
                ["Category", "Items", "Released", "Items (skus)"],
                "SELECT IFNULL(NULLIF(i.`item_group`,''),'(uncategorized)'), COUNT(*), SUM(IFNULL(i.`active`,1) = 1)," +
                " LEFT(GROUP_CONCAT(i.`sku` ORDER BY i.`sku` SEPARATOR ', '),200)" +
                " FROM `epc_erp_inv_items` i GROUP BY 1 ORDER BY 1 LIMIT 200",
                "No categories yet — the item group on each product forms the category hierarchy.",
                "/erp/product-info-app"),

            ["pim_attributes"] = new(
                ["Field", "Label", "Type", "Role", "Active"],
                "SELECT IFNULL(f.`field_key`,''), IFNULL(f.`label`,''), IFNULL(f.`field_type`,'text')," +
                " IFNULL(f.`field_role`,'inventory'), IFNULL(f.`active`,1)" +
                " FROM `epc_erp_inv_field_defs` f ORDER BY f.`sort_order`, f.`id` LIMIT 200",
                "No attributes yet — define PIM attributes in Product information.",
                "/erp/product-info-app"),

            ["pim_attr_groups"] = new(
                ["Group", "Attributes"],
                "SELECT IFNULL(NULLIF(f.`field_role`,''),'inventory'), COUNT(*)" +
                " FROM `epc_erp_inv_field_defs` f WHERE IFNULL(f.`active`,1) = 1 GROUP BY 1 ORDER BY 1 LIMIT 50",
                "No attribute groups yet.",
                "/erp/product-info-app"),

            ["pim_attr_types"] = new(
                ["Type", "Attributes", "Active"],
                "SELECT IFNULL(f.`field_type`,'text'), COUNT(*), SUM(IFNULL(f.`active`,1) = 1)" +
                " FROM `epc_erp_inv_field_defs` f GROUP BY 1 ORDER BY 1 LIMIT 50",
                "No attribute types yet.",
                "/erp/product-info-app"),

            ["pim_coo"] = new(
                ["Item", "Field", "Origin / value"],
                "SELECT IFNULL(i.`sku`,''), fv.`field_key`, fv.`value`" +
                " FROM `epc_erp_inv_item_fields` fv JOIN `epc_erp_inv_items` i ON i.`id` = fv.`item_id`" +
                " WHERE fv.`field_key` IN ('country_of_origin','origin','coo')" +
                " ORDER BY i.`sku` LIMIT 200",
                "No country-of-origin values — set an origin field on items that need one.",
                "/erp/product-info-app"),

            ["pim_obsolete_state"] = new(
                ["Item", "Product", "Type", "Category", "Released"],
                "SELECT IFNULL(i.`sku`,''), IFNULL(i.`name`,''), IFNULL(i.`item_type`,'standard')," +
                " IFNULL(i.`item_group`,''), IFNULL(i.`active`,1)" +
                " FROM `epc_erp_inv_items` i WHERE IFNULL(i.`active`,1) = 0 ORDER BY i.`sku` LIMIT 200",
                "No obsolete products.",
                "/erp/product-info-app"),
        };

    private readonly IErpWriteConnectionFactory _connections;

    public ErpPimWorkspaceReadService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public bool IsDataTab(string? tab)
        => tab is not null && Specs.ContainsKey(tab);

    public async Task<ErpPimWorkspaceData?> LoadAsync(string? tab, CancellationToken cancellationToken = default)
    {
        if (tab is null || !Specs.TryGetValue(tab, out var spec))
        {
            return null;
        }

        var rows = new List<IReadOnlyList<string>>();
        if (_connections.IsConfigured)
        {
            try
            {
                await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
                await using var command = connection.CreateCommand();
                command.CommandText = ErpDb.Positional(spec.Sql);
                await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    var cells = new string[reader.FieldCount];
                    for (var c = 0; c < cells.Length; c++)
                    {
                        cells[c] = Format(reader.GetValue(c));
                    }

                    rows.Add(cells);
                }
            }
            catch
            {
                // Optional backing table missing on this tenant — show the hint row instead of 500.
            }
        }

        return new ErpPimWorkspaceData(spec.Headers, rows, spec.EmptyHint, spec.ManageHref);
    }

    private static string Format(object? value) => value switch
    {
        null or DBNull => "",
        bool b => b ? "1" : "0",
        decimal d => d.ToString("0.####", CultureInfo.InvariantCulture),
        double d => d.ToString("0.####", CultureInfo.InvariantCulture),
        float f => f.ToString("0.####", CultureInfo.InvariantCulture),
        DateTime dt => dt.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture),
        _ => Convert.ToString(value, CultureInfo.InvariantCulture) ?? "",
    };
}
