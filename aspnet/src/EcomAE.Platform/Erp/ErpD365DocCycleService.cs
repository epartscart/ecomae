using System.Data.Common;
using System.Globalization;
using System.Text;

namespace EcomAE.Platform.Erp;

/// <summary>
/// D365 F&O document-cycle fields: per-legal-entity document profiles (header/footer/TRN/logo
/// used by invoice + delivery-note PDFs), the SalesTableDetails header columns, the
/// PurchTableListPage header columns, the ASAP_* delivery-note fields, the released-product
/// SHAM attributes, and the customer/vendor master fields. Schema evolution uses the
/// <see cref="ErpLazySchema.AddColumnIfMissingAsync"/> guard so the same code runs against
/// live and sandbox databases.
/// </summary>
public interface IErpD365DocCycleService
{
    Task EnsureSchemaAsync(DbConnection connection, CancellationToken cancellationToken = default);

    /// <summary>Load the document profile for one legal entity (0 = tenant-wide fallback row).</summary>
    Task<IReadOnlyDictionary<string, string?>> EntityDocProfileAsync(DbConnection connection, long legalEntityId, CancellationToken cancellationToken = default);

    /// <summary>List every stored entity document profile.</summary>
    Task<IReadOnlyList<IReadOnlyDictionary<string, string?>>> EntityDocProfilesAsync(DbConnection connection, CancellationToken cancellationToken = default);

    /// <summary>Upsert a legal entity's document header/footer profile.</summary>
    Task<ErpSimpleWriteResult> SaveEntityDocProfileAsync(
        DbConnection connection,
        long legalEntityId,
        IReadOnlyDictionary<string, string?> fields,
        int adminId,
        CancellationToken cancellationToken = default);

    /// <summary>Save D365 header fields onto an existing sales order row.</summary>
    Task<ErpSimpleWriteResult> SaveSalesOrderHeaderAsync(
        DbConnection connection,
        long salesOrderId,
        IReadOnlyDictionary<string, string?> fields,
        int adminId,
        CancellationToken cancellationToken = default);

    /// <summary>Save D365 header fields onto an existing purchase order row.</summary>
    Task<ErpSimpleWriteResult> SavePurchaseOrderHeaderAsync(
        DbConnection connection,
        long purchaseOrderId,
        IReadOnlyDictionary<string, string?> fields,
        int adminId,
        CancellationToken cancellationToken = default);

    /// <summary>Save the ASAP_* delivery-note fields onto an existing delivery note.</summary>
    Task<ErpSimpleWriteResult> SaveDeliveryNoteFieldsAsync(
        DbConnection connection,
        long deliveryNoteId,
        IReadOnlyDictionary<string, string?> fields,
        int adminId,
        CancellationToken cancellationToken = default);

    /// <summary>Save released-product SHAM attributes (epc_erp_inv_item_fields key/value).</summary>
    Task<ErpSimpleWriteResult> SaveProductFieldsAsync(
        DbConnection connection,
        long itemId,
        IReadOnlyDictionary<string, string?> fields,
        int adminId,
        CancellationToken cancellationToken = default);

    /// <summary>Save customer-master fields onto an epc_erp_contacts row.</summary>
    Task<ErpSimpleWriteResult> SaveCustomerFieldsAsync(
        DbConnection connection,
        long contactId,
        IReadOnlyDictionary<string, string?> fields,
        int adminId,
        CancellationToken cancellationToken = default);

    /// <summary>Save vendor-master fields onto an epc_erp_suppliers row.</summary>
    Task<ErpSimpleWriteResult> SaveVendorFieldsAsync(
        DbConnection connection,
        long supplierId,
        IReadOnlyDictionary<string, string?> fields,
        int adminId,
        CancellationToken cancellationToken = default);
}

public sealed class ErpD365DocCycleService : IErpD365DocCycleService
{
    // ---- column allowlists -------------------------------------------------

    /// <summary>Per-legal-entity document profile columns (epc_erp_entity_doc_profiles).</summary>
    public static readonly string[] EntityDocProfileColumns =
    [
        "legal_name", "trade_name", "address_line1", "address_line2", "city", "emirate",
        "country_code", "phone", "fax", "email", "website", "trn", "logo_path",
        "bank_name", "bank_iban", "bank_account", "legal_footer", "doc_header_note",
        "doc_footer_note", "invoice_title", "registration_no",
    ];

    /// <summary>SalesTableDetails header fasttab columns (epc_erp_sales_orders).</summary>
    public static readonly string[] SalesOrderHeaderColumns =
    [
        "entity_id", "bu_id", "customer_account", "customer_name", "invoice_account",
        "order_type", "source", "continuity", "currency_code", "lpo_no", "ref_no",
        "payment_terms", "user_id_text", "salesman", "mode_of_delivery", "delivery_terms",
        "requested_ship_date", "requested_receipt_date", "confirmed_ship_date",
        "confirmed_receipt_date", "delivery_name", "delivery_address", "delivery_city",
        "delivery_country_code", "customer_reference", "customer_requisition",
        "total_discount_pct", "release_status", "order_fulfillment_policy",
        "default_fulfillment_policy", "outbound_shipment_processing", "routes",
        "carrier_customer_account", "dom_status", "intercompany_order",
        "customer_order_reference", "pickup_warehouse", "direct_delivery_by_wh",
        "bg_document", "lc_document", "registration_number", "special_notes",
        "trn", "customer_phone", "customer_fax", "customer_area", "customer_country",
    ];

    /// <summary>SalesTableDetails line-grid columns (epc_erp_sales_order_lines).</summary>
    public static readonly string[] SalesOrderLineColumns =
    [
        "line_type", "variant_number", "product_number", "foc", "item_number",
        "product_name", "size_stock_number", "sales_category", "cw_quantity", "cw_unit",
        "supplementary", "unit", "vat_rate", "discount_pct", "gross_amount",
        "vat_amount", "net_amount", "brand", "oem_part_no",
    ];

    /// <summary>PurchTableListPage header columns (epc_erp_purchase_orders).</summary>
    public static readonly string[] PurchaseOrderHeaderColumns =
    [
        "entity_id", "bu_id", "vendor_account", "vendor_name", "invoice_account",
        "order_type", "currency_code", "lpo_no", "ref_no", "payment_terms",
        "mode_of_delivery", "delivery_terms", "requested_delivery_date",
        "confirmed_delivery_date", "delivery_name", "delivery_address", "delivery_city",
        "delivery_country_code", "buyer_group", "vendor_reference", "vendor_requisition",
        "total_discount_pct", "trn", "vendor_phone", "vendor_fax", "vendor_area",
        "vendor_country", "memo_ext", "release_status", "intercompany_order",
    ];

    /// <summary>PurchTable line-grid columns (epc_erp_po_lines).</summary>
    public static readonly string[] PurchaseOrderLineColumns =
    [
        "item_number", "product_name", "procurement_category", "unit", "unit_price",
        "discount_pct", "vat_rate", "gross_amount", "vat_amount", "net_amount",
        "warehouse", "site", "delivery_date", "brand", "vendor_item_no",
    ];

    /// <summary>ASAP_DeliveryListFormMenu fields (epc_erp_delivery_notes).</summary>
    public static readonly string[] DeliveryNoteColumns =
    [
        "entity_id", "warehouse", "site", "customer_account", "customer_name",
        "sales_order_no", "invoice_name", "invoice_address", "buyer_name",
        "buyer_address", "buyer_country", "buyer_phone", "cargo_company",
        "contact_person", "contact_phone", "location", "remarks", "carton_mark",
        "carton_qty", "package_type", "container_number", "delivery_by",
        "delivery_phone", "delivery_date", "receiver_name", "receiver_phone",
        "helper_name", "vehicle_number", "status_ext",
    ];

    /// <summary>Released-product SHAM attributes (epc_erp_inv_item_fields keys).</summary>
    public static readonly string[] ProductFieldKeys =
    [
        "search_name", "product_subtype", "product_service_type", "product_number",
        "storage_dimension_group", "tracking_dimension_group", "item_model_group",
        "reservation_hierarchy", "product_lifecycle_state", "msp", "retail_msp",
        "rack_no", "box_qty", "brand", "car_maker", "carton_qty", "engine_model",
        "gross_weight", "net_weight", "hs_code", "origin", "oem_part_no",
        "rus_stock_no", "size", "size_stock_no", "sku_item_code", "stock_no",
        "old_item_code", "vehicle_model", "article_reference", "reference",
        "mk_reference", "item_price_tolerance", "approved_vendor", "gtin", "barcode",
    ];

    /// <summary>Customer-master columns (epc_erp_contacts).</summary>
    public static readonly string[] CustomerColumns =
    [
        "account_code", "fax", "area", "payment_terms", "credit_limit", "sales_tax_group",
        "delivery_terms", "mode_of_delivery", "warehouse", "salesman", "buyer_name",
        "buyer_address", "buyer_country", "buyer_phone", "registration_no",
    ];

    /// <summary>Vendor-master columns (epc_erp_suppliers).</summary>
    public static readonly string[] VendorColumns =
    [
        "vendor_account", "fax", "area", "payment_terms", "credit_limit",
        "item_sales_tax_group", "delivery_terms", "mode_of_delivery", "buyer_group",
        "vendor_reference", "registration_no",
    ];

    // ---- schema --------------------------------------------------------------

    public async Task EnsureSchemaAsync(DbConnection connection, CancellationToken cancellationToken = default)
    {
        await ErpDb.TryExecuteAsync(
            connection,
            "CREATE TABLE IF NOT EXISTS `epc_erp_entity_doc_profiles` ("
            + " `id` int(11) NOT NULL AUTO_INCREMENT,"
            + " `legal_entity_id` int(11) NOT NULL DEFAULT 0,"
            + " `legal_name` varchar(190) NOT NULL DEFAULT '',"
            + " `trade_name` varchar(190) NOT NULL DEFAULT '',"
            + " `address_line1` varchar(190) NOT NULL DEFAULT '',"
            + " `address_line2` varchar(190) NOT NULL DEFAULT '',"
            + " `city` varchar(60) NOT NULL DEFAULT '',"
            + " `emirate` varchar(60) NOT NULL DEFAULT '',"
            + " `country_code` varchar(4) NOT NULL DEFAULT '',"
            + " `phone` varchar(40) NOT NULL DEFAULT '',"
            + " `fax` varchar(40) NOT NULL DEFAULT '',"
            + " `email` varchar(120) NOT NULL DEFAULT '',"
            + " `website` varchar(160) NOT NULL DEFAULT '',"
            + " `trn` varchar(40) NOT NULL DEFAULT '',"
            + " `logo_path` varchar(255) NOT NULL DEFAULT '',"
            + " `bank_name` varchar(120) NOT NULL DEFAULT '',"
            + " `bank_iban` varchar(60) NOT NULL DEFAULT '',"
            + " `bank_account` varchar(60) NOT NULL DEFAULT '',"
            + " `legal_footer` varchar(500) NOT NULL DEFAULT '',"
            + " `doc_header_note` varchar(255) NOT NULL DEFAULT '',"
            + " `doc_footer_note` varchar(255) NOT NULL DEFAULT '',"
            + " `invoice_title` varchar(60) NOT NULL DEFAULT 'TAX INVOICE',"
            + " `registration_no` varchar(60) NOT NULL DEFAULT '',"
            + " `admin_id` int(11) NOT NULL DEFAULT 0,"
            + " `time_created` int(11) NOT NULL DEFAULT 0,"
            + " `time_updated` int(11) NOT NULL DEFAULT 0,"
            + " PRIMARY KEY (`id`),"
            + " UNIQUE KEY `x_entity` (`legal_entity_id`)"
            + ") ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='Per-legal-entity document header/footer profile'",
            cancellationToken).ConfigureAwait(false);

        // SalesTableDetails header columns on the manual SO table.
        await Add(connection, "epc_erp_sales_orders", "entity_id", "int(11) NOT NULL DEFAULT 0", cancellationToken).ConfigureAwait(false);
        await Add(connection, "epc_erp_sales_orders", "bu_id", "int(11) NOT NULL DEFAULT 0", cancellationToken).ConfigureAwait(false);
        await Add(connection, "epc_erp_sales_orders", "customer_account", "varchar(40) NOT NULL DEFAULT ''", cancellationToken).ConfigureAwait(false);
        await Add(connection, "epc_erp_sales_orders", "customer_name", "varchar(190) NOT NULL DEFAULT ''", cancellationToken).ConfigureAwait(false);
        await Add(connection, "epc_erp_sales_orders", "invoice_account", "varchar(40) NOT NULL DEFAULT ''", cancellationToken).ConfigureAwait(false);
        await Add(connection, "epc_erp_sales_orders", "order_type", "varchar(24) NOT NULL DEFAULT 'Sales order'", cancellationToken).ConfigureAwait(false);
        await Add(connection, "epc_erp_sales_orders", "source", "varchar(24) NOT NULL DEFAULT ''", cancellationToken).ConfigureAwait(false);
        await Add(connection, "epc_erp_sales_orders", "continuity", "varchar(24) NOT NULL DEFAULT ''", cancellationToken).ConfigureAwait(false);
        await Add(connection, "epc_erp_sales_orders", "currency_code", "varchar(8) NOT NULL DEFAULT 'AED'", cancellationToken).ConfigureAwait(false);
        await Add(connection, "epc_erp_sales_orders", "lpo_no", "varchar(60) NOT NULL DEFAULT ''", cancellationToken).ConfigureAwait(false);
        await Add(connection, "epc_erp_sales_orders", "ref_no", "varchar(60) NOT NULL DEFAULT ''", cancellationToken).ConfigureAwait(false);
        await Add(connection, "epc_erp_sales_orders", "payment_terms", "varchar(80) NOT NULL DEFAULT ''", cancellationToken).ConfigureAwait(false);
        await Add(connection, "epc_erp_sales_orders", "user_id_text", "varchar(40) NOT NULL DEFAULT ''", cancellationToken).ConfigureAwait(false);
        await Add(connection, "epc_erp_sales_orders", "salesman", "varchar(120) NOT NULL DEFAULT ''", cancellationToken).ConfigureAwait(false);
        await Add(connection, "epc_erp_sales_orders", "mode_of_delivery", "varchar(40) NOT NULL DEFAULT ''", cancellationToken).ConfigureAwait(false);
        await Add(connection, "epc_erp_sales_orders", "delivery_terms", "varchar(60) NOT NULL DEFAULT ''", cancellationToken).ConfigureAwait(false);
        await Add(connection, "epc_erp_sales_orders", "requested_ship_date", "varchar(20) NOT NULL DEFAULT ''", cancellationToken).ConfigureAwait(false);
        await Add(connection, "epc_erp_sales_orders", "requested_receipt_date", "varchar(20) NOT NULL DEFAULT ''", cancellationToken).ConfigureAwait(false);
        await Add(connection, "epc_erp_sales_orders", "confirmed_ship_date", "varchar(20) NOT NULL DEFAULT ''", cancellationToken).ConfigureAwait(false);
        await Add(connection, "epc_erp_sales_orders", "confirmed_receipt_date", "varchar(20) NOT NULL DEFAULT ''", cancellationToken).ConfigureAwait(false);
        await Add(connection, "epc_erp_sales_orders", "delivery_name", "varchar(190) NOT NULL DEFAULT ''", cancellationToken).ConfigureAwait(false);
        await Add(connection, "epc_erp_sales_orders", "delivery_address", "varchar(255) NOT NULL DEFAULT ''", cancellationToken).ConfigureAwait(false);
        await Add(connection, "epc_erp_sales_orders", "delivery_city", "varchar(60) NOT NULL DEFAULT ''", cancellationToken).ConfigureAwait(false);
        await Add(connection, "epc_erp_sales_orders", "delivery_country_code", "varchar(4) NOT NULL DEFAULT ''", cancellationToken).ConfigureAwait(false);
        await Add(connection, "epc_erp_sales_orders", "customer_reference", "varchar(80) NOT NULL DEFAULT ''", cancellationToken).ConfigureAwait(false);
        await Add(connection, "epc_erp_sales_orders", "customer_requisition", "varchar(80) NOT NULL DEFAULT ''", cancellationToken).ConfigureAwait(false);
        await Add(connection, "epc_erp_sales_orders", "total_discount_pct", "decimal(9,2) NOT NULL DEFAULT 0.00", cancellationToken).ConfigureAwait(false);
        await Add(connection, "epc_erp_sales_orders", "release_status", "varchar(24) NOT NULL DEFAULT 'Open'", cancellationToken).ConfigureAwait(false);
        await Add(connection, "epc_erp_sales_orders", "order_fulfillment_policy", "varchar(60) NOT NULL DEFAULT ''", cancellationToken).ConfigureAwait(false);
        await Add(connection, "epc_erp_sales_orders", "default_fulfillment_policy", "varchar(60) NOT NULL DEFAULT ''", cancellationToken).ConfigureAwait(false);
        await Add(connection, "epc_erp_sales_orders", "outbound_shipment_processing", "varchar(60) NOT NULL DEFAULT ''", cancellationToken).ConfigureAwait(false);
        await Add(connection, "epc_erp_sales_orders", "routes", "varchar(80) NOT NULL DEFAULT ''", cancellationToken).ConfigureAwait(false);
        await Add(connection, "epc_erp_sales_orders", "carrier_customer_account", "varchar(40) NOT NULL DEFAULT ''", cancellationToken).ConfigureAwait(false);
        await Add(connection, "epc_erp_sales_orders", "dom_status", "varchar(24) NOT NULL DEFAULT 'Not processed'", cancellationToken).ConfigureAwait(false);
        await Add(connection, "epc_erp_sales_orders", "intercompany_order", "tinyint(1) NOT NULL DEFAULT 0", cancellationToken).ConfigureAwait(false);
        await Add(connection, "epc_erp_sales_orders", "customer_order_reference", "varchar(60) NOT NULL DEFAULT ''", cancellationToken).ConfigureAwait(false);
        await Add(connection, "epc_erp_sales_orders", "pickup_warehouse", "varchar(60) NOT NULL DEFAULT ''", cancellationToken).ConfigureAwait(false);
        await Add(connection, "epc_erp_sales_orders", "direct_delivery_by_wh", "tinyint(1) NOT NULL DEFAULT 0", cancellationToken).ConfigureAwait(false);
        await Add(connection, "epc_erp_sales_orders", "bg_document", "varchar(60) NOT NULL DEFAULT ''", cancellationToken).ConfigureAwait(false);
        await Add(connection, "epc_erp_sales_orders", "lc_document", "varchar(60) NOT NULL DEFAULT ''", cancellationToken).ConfigureAwait(false);
        await Add(connection, "epc_erp_sales_orders", "registration_number", "varchar(60) NOT NULL DEFAULT ''", cancellationToken).ConfigureAwait(false);
        await Add(connection, "epc_erp_sales_orders", "special_notes", "varchar(255) NOT NULL DEFAULT ''", cancellationToken).ConfigureAwait(false);
        await Add(connection, "epc_erp_sales_orders", "trn", "varchar(40) NOT NULL DEFAULT ''", cancellationToken).ConfigureAwait(false);
        await Add(connection, "epc_erp_sales_orders", "customer_phone", "varchar(40) NOT NULL DEFAULT ''", cancellationToken).ConfigureAwait(false);
        await Add(connection, "epc_erp_sales_orders", "customer_fax", "varchar(40) NOT NULL DEFAULT ''", cancellationToken).ConfigureAwait(false);
        await Add(connection, "epc_erp_sales_orders", "customer_area", "varchar(80) NOT NULL DEFAULT ''", cancellationToken).ConfigureAwait(false);
        await Add(connection, "epc_erp_sales_orders", "customer_country", "varchar(80) NOT NULL DEFAULT ''", cancellationToken).ConfigureAwait(false);

        foreach (var col in SalesOrderLineColumns)
        {
            var def = col is "cw_quantity" ? "decimal(14,3) NOT NULL DEFAULT 0.000"
                : col is "foc" ? "tinyint(1) NOT NULL DEFAULT 0"
                : col is "vat_rate" or "discount_pct" ? "decimal(9,2) NOT NULL DEFAULT 0.00"
                : col is "gross_amount" or "vat_amount" or "net_amount" ? "decimal(14,2) NOT NULL DEFAULT 0.00"
                : col is "supplementary" ? "varchar(255) NOT NULL DEFAULT ''"
                : "varchar(80) NOT NULL DEFAULT ''";
            await Add(connection, "epc_erp_sales_order_lines", col, def, cancellationToken).ConfigureAwait(false);
        }

        foreach (var col in PurchaseOrderHeaderColumns)
        {
            var def = col is "entity_id" or "bu_id" ? "int(11) NOT NULL DEFAULT 0"
                : col is "intercompany_order" ? "tinyint(1) NOT NULL DEFAULT 0"
                : col is "total_discount_pct" ? "decimal(9,2) NOT NULL DEFAULT 0.00"
                : col is "currency_code" ? "varchar(8) NOT NULL DEFAULT 'AED'"
                : col is "order_type" ? "varchar(24) NOT NULL DEFAULT 'Purchase order'"
                : col is "release_status" ? "varchar(24) NOT NULL DEFAULT 'Open'"
                : "varchar(120) NOT NULL DEFAULT ''";
            await Add(connection, "epc_erp_purchase_orders", col, def, cancellationToken).ConfigureAwait(false);
        }

        foreach (var col in PurchaseOrderLineColumns)
        {
            var def = col is "unit_price" ? "decimal(14,4) NOT NULL DEFAULT 0.0000"
                : col is "discount_pct" or "vat_rate" ? "decimal(9,2) NOT NULL DEFAULT 0.00"
                : col is "gross_amount" or "vat_amount" or "net_amount" ? "decimal(14,2) NOT NULL DEFAULT 0.00"
                : col is "delivery_date" ? "varchar(20) NOT NULL DEFAULT ''"
                : "varchar(80) NOT NULL DEFAULT ''";
            await Add(connection, "epc_erp_po_lines", col, def, cancellationToken).ConfigureAwait(false);
        }

        foreach (var col in DeliveryNoteColumns)
        {
            var def = col is "entity_id" ? "int(11) NOT NULL DEFAULT 0"
                : col is "carton_qty" ? "decimal(14,2) NOT NULL DEFAULT 0.00"
                : col is "invoice_address" or "buyer_address" ? "varchar(500) NOT NULL DEFAULT ''"
                : col is "remarks" ? "varchar(500) NOT NULL DEFAULT ''"
                : "varchar(120) NOT NULL DEFAULT ''";
            await Add(connection, "epc_erp_delivery_notes", col, def, cancellationToken).ConfigureAwait(false);
        }

        foreach (var col in CustomerColumns)
        {
            var def = col is "credit_limit" ? "decimal(14,2) NOT NULL DEFAULT 0.00"
                : col is "buyer_address" ? "varchar(500) NOT NULL DEFAULT ''"
                : "varchar(120) NOT NULL DEFAULT ''";
            await Add(connection, "epc_erp_contacts", col, def, cancellationToken).ConfigureAwait(false);
        }

        foreach (var col in VendorColumns)
        {
            var def = col is "credit_limit" ? "decimal(14,2) NOT NULL DEFAULT 0.00"
                : "varchar(120) NOT NULL DEFAULT ''";
            await Add(connection, "epc_erp_suppliers", col, def, cancellationToken).ConfigureAwait(false);
        }
    }

    private static Task Add(DbConnection c, string table, string column, string definition, CancellationToken ct)
        => ErpLazySchema.AddColumnIfMissingAsync(c, table, column, definition, ct);

    // ---- entity doc profile --------------------------------------------------

    public async Task<IReadOnlyDictionary<string, string?>> EntityDocProfileAsync(DbConnection connection, long legalEntityId, CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAsync(connection, cancellationToken).ConfigureAwait(false);
        var row = await FirstRowAsync(
            connection,
            "SELECT * FROM `epc_erp_entity_doc_profiles` WHERE `legal_entity_id` = " + legalEntityId.ToString(CultureInfo.InvariantCulture) + " LIMIT 1",
            cancellationToken).ConfigureAwait(false);
        if (row is not null)
        {
            return row;
        }

        return await FirstRowAsync(
            connection,
            "SELECT * FROM `epc_erp_entity_doc_profiles` WHERE `legal_entity_id` = 0 LIMIT 1",
            cancellationToken).ConfigureAwait(false)
            ?? new Dictionary<string, string?>(StringComparer.Ordinal);
    }

    public async Task<IReadOnlyList<IReadOnlyDictionary<string, string?>>> EntityDocProfilesAsync(DbConnection connection, CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAsync(connection, cancellationToken).ConfigureAwait(false);
        return await RowsAsync(connection, "SELECT * FROM `epc_erp_entity_doc_profiles` ORDER BY `legal_entity_id`", cancellationToken).ConfigureAwait(false);
    }

    public async Task<ErpSimpleWriteResult> SaveEntityDocProfileAsync(
        DbConnection connection,
        long legalEntityId,
        IReadOnlyDictionary<string, string?> fields,
        int adminId,
        CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAsync(connection, cancellationToken).ConfigureAwait(false);
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var sets = BuildSets(EntityDocProfileColumns, fields);
        var existing = await ErpDb.LongAsync(
            connection,
            null,
            "SELECT `id` FROM `epc_erp_entity_doc_profiles` WHERE `legal_entity_id` = " + legalEntityId.ToString(CultureInfo.InvariantCulture) + " LIMIT 1",
            cancellationToken).ConfigureAwait(false);
        if (existing > 0)
        {
            await ErpDb.ExecuteAsync(
                connection,
                null,
                "UPDATE `epc_erp_entity_doc_profiles` SET " + sets.Sql + ", `admin_id`=?, `time_updated`=? WHERE `id`=?",
                cancellationToken,
                [.. sets.Args, adminId, now, existing]).ConfigureAwait(false);
        }
        else
        {
            var cols = string.Join("`, `", sets.Columns);
            var marks = string.Join(",", sets.Columns.Select(_ => "?"));
            await ErpDb.ExecuteAsync(
                connection,
                null,
                "INSERT INTO `epc_erp_entity_doc_profiles` (`legal_entity_id`, `" + cols + "`, `admin_id`, `time_created`, `time_updated`) VALUES (?," + marks + ",?,?,?)",
                cancellationToken,
                [legalEntityId, .. sets.Args, adminId, now, now]).ConfigureAwait(false);
        }

        return ErpSimpleWriteResult.Ok("Document profile saved for entity " + legalEntityId + ".", existing > 0 ? existing : legalEntityId);
    }

    // ---- generic field saves -------------------------------------------------

    public Task<ErpSimpleWriteResult> SaveSalesOrderHeaderAsync(DbConnection c, long id, IReadOnlyDictionary<string, string?> f, int a, CancellationToken t = default)
        => SaveFieldsAsync(c, "epc_erp_sales_orders", "id", id, SalesOrderHeaderColumns, f, a, "Sales order header saved.", t);

    public Task<ErpSimpleWriteResult> SavePurchaseOrderHeaderAsync(DbConnection c, long id, IReadOnlyDictionary<string, string?> f, int a, CancellationToken t = default)
        => SaveFieldsAsync(c, "epc_erp_purchase_orders", "id", id, PurchaseOrderHeaderColumns, f, a, "Purchase order header saved.", t);

    public Task<ErpSimpleWriteResult> SaveDeliveryNoteFieldsAsync(DbConnection c, long id, IReadOnlyDictionary<string, string?> f, int a, CancellationToken t = default)
        => SaveFieldsAsync(c, "epc_erp_delivery_notes", "id", id, DeliveryNoteColumns, f, a, "Delivery note saved.", t);

    public Task<ErpSimpleWriteResult> SaveCustomerFieldsAsync(DbConnection c, long id, IReadOnlyDictionary<string, string?> f, int a, CancellationToken t = default)
        => SaveFieldsAsync(c, "epc_erp_contacts", "id", id, CustomerColumns, f, a, "Customer details saved.", t);

    public Task<ErpSimpleWriteResult> SaveVendorFieldsAsync(DbConnection c, long id, IReadOnlyDictionary<string, string?> f, int a, CancellationToken t = default)
        => SaveFieldsAsync(c, "epc_erp_suppliers", "id", id, VendorColumns, f, a, "Vendor details saved.", t);

    private async Task<ErpSimpleWriteResult> SaveFieldsAsync(
        DbConnection connection,
        string table,
        string idColumn,
        long id,
        string[] allowed,
        IReadOnlyDictionary<string, string?> fields,
        int adminId,
        string okMessage,
        CancellationToken cancellationToken)
    {
        await EnsureSchemaAsync(connection, cancellationToken).ConfigureAwait(false);
        var sets = BuildSets(allowed, fields);
        var sql = "UPDATE `" + table + "` SET " + sets.Sql + ", `admin_id`=?, `time_updated`=? WHERE `" + idColumn + "` = ?";
        if (!await ErpDb.ColumnKnownAsync(connection, table, "admin_id", cancellationToken).ConfigureAwait(false))
        {
            sql = "UPDATE `" + table + "` SET " + sets.Sql + " WHERE `" + idColumn + "` = ?";
        }

        var args = sets.Args.ToList();
        if (sql.Contains("admin_id", StringComparison.Ordinal))
        {
            args.Add(adminId);
            args.Add(DateTimeOffset.UtcNow.ToUnixTimeSeconds());
        }

        args.Add(id);
        var n = await ErpDb.ExecuteAsync(connection, null, sql, cancellationToken, [.. args]).ConfigureAwait(false);
        return n > 0 ? ErpSimpleWriteResult.Ok(okMessage, id) : ErpSimpleWriteResult.Fail("not_found", "Record " + id + " not found.");
    }

    public async Task<ErpSimpleWriteResult> SaveProductFieldsAsync(
        DbConnection connection,
        long itemId,
        IReadOnlyDictionary<string, string?> fields,
        int adminId,
        CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAsync(connection, cancellationToken).ConfigureAwait(false);
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var wrote = 0;
        foreach (var key in ProductFieldKeys)
        {
            if (!fields.TryGetValue(key, out var value))
            {
                continue;
            }

            await ErpDb.ExecuteAsync(
                connection,
                null,
                ErpDb.Positional("INSERT INTO `epc_erp_inv_item_fields` (`item_id`,`field_key`,`value`) VALUES (?,?,?) ON DUPLICATE KEY UPDATE `value`=VALUES(`value`)"),
                cancellationToken,
                itemId,
                key,
                (value ?? string.Empty).Trim()).ConfigureAwait(false);
            wrote++;
        }

        if (wrote == 0)
        {
            return ErpSimpleWriteResult.Fail("no_fields", "No product fields supplied.");
        }

        await ErpDb.TryExecuteAsync(
            connection,
            "UPDATE `epc_erp_inv_items` SET `time_created` = `time_created` WHERE `id` = " + itemId.ToString(CultureInfo.InvariantCulture),
            cancellationToken).ConfigureAwait(false);
        return ErpSimpleWriteResult.Ok("Product attributes saved (" + wrote + " fields).", itemId);
    }

    // ---- helpers ---------------------------------------------------------------

    private sealed record SetClause(string Sql, IReadOnlyList<object?> Args, IReadOnlyList<string> Columns);

    private static SetClause BuildSets(string[] allowed, IReadOnlyDictionary<string, string?> fields)
    {
        var sql = new StringBuilder();
        var args = new List<object?>();
        var cols = new List<string>();
        foreach (var col in allowed)
        {
            if (!fields.TryGetValue(col, out var value))
            {
                continue;
            }

            if (sql.Length > 0)
            {
                sql.Append(", ");
            }

            sql.Append('`').Append(col).Append("` = ?");
            args.Add(value);
            cols.Add(col);
        }

        if (sql.Length == 0)
        {
            // No fields supplied — still satisfy SQL syntax with a no-op.
            sql.Append("`time_updated` = `time_updated`");
        }

        return new SetClause(sql.ToString(), args, cols);
    }

    internal static async Task<Dictionary<string, string?>?> FirstRowAsync(DbConnection connection, string sql, CancellationToken cancellationToken, params object?[] args)
    {
        var rows = await RowsAsync(connection, sql, cancellationToken, args).ConfigureAwait(false);
        return rows.Count == 0 ? null : rows[0];
    }

    internal static async Task<List<Dictionary<string, string?>>> RowsAsync(DbConnection connection, string sql, CancellationToken cancellationToken, params object?[] args)
    {
        var rows = new List<Dictionary<string, string?>>();
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        ErpDb.AddParameters(command, args);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            var row = new Dictionary<string, string?>(StringComparer.Ordinal);
            for (var i = 0; i < reader.FieldCount; i++)
            {
                row[reader.GetName(i)] = reader.IsDBNull(i) ? null : ErpDocumentControlRender.PdoString(reader, i);
            }

            rows.Add(row);
        }

        return rows;
    }
}
