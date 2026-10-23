using System.Data.Common;
using System.Globalization;
using System.Text;
using System.Text.Json;

namespace EcomAE.Platform.Erp;

/// <summary>
/// Tenant-generic CSV bulk load / export for opening data (items, stock, customers, vendors,
/// COA, sales/purchase orders incl. lines, quotations). Import is upload → header-validate →
/// commit (insert-or-update on natural keys); export emits the same columns so a tenant can
/// round-trip data. Everything is entity/BU-scoped like D365 data packages.
/// </summary>
public static class ErpD365BulkCsv
{
    public sealed record Area(string Key, string Label, string[] Columns, string[] Required);

    public static readonly IReadOnlyDictionary<string, Area> Areas =
        new Dictionary<string, Area>(StringComparer.OrdinalIgnoreCase)
        {
            ["items"] = new("items", "Released products",
                ["sku", "name", "item_type", "product_subtype", "unit", "category", "brand", "car_maker", "engine_model", "vehicle_model", "oem_part_no", "stock_no", "rus_stock_no", "size", "size_stock", "sku_item_code", "old_item_code", "box_qty", "carton_qty", "gross_weight", "net_weight", "hs_code", "origin", "article_ref", "reference", "mk_ref", "msp", "retail_msp", "rack_no", "item_model_group", "storage_dim_group", "tracking_dim_group", "reservation_hierarchy", "cost_price", "sell_price", "active"],
                ["sku", "name"]),
            ["stock"] = new("stock", "Stock on hand",
                ["sku", "warehouse", "location", "qty_on_hand", "avg_unit_cost", "batch_no", "expiry_date", "variant_label"],
                ["sku", "warehouse", "qty_on_hand"]),
            ["customers"] = new("customers", "Customers",
                ["account_code", "name", "email", "phone", "fax", "address", "area", "city", "country", "trn", "registration_no", "payment_terms", "credit_limit", "sales_tax_group", "delivery_terms", "mode_of_delivery", "warehouse", "salesman", "buyer_name", "buyer_address", "buyer_country", "buyer_phone", "customer_group", "on_hold"],
                ["name"]),
            ["vendors"] = new("vendors", "Vendors",
                ["vendor_account", "name", "contact_email", "contact_phone", "fax", "address", "area", "city", "country_code", "trn", "registration_no", "payment_terms", "payment_method", "delivery_terms", "delivery_mode", "credit_limit", "item_sales_tax_group", "buyer_group", "vendor_group", "vendor_reference", "bank_name", "bank_account_number", "iban", "swift_bic", "on_hold"],
                ["name"]),
            ["coa"] = new("coa", "Chart of accounts / main accounts",
                ["account_code", "account_name", "account_type", "category", "parent_code", "currency", "db_cr_default", "db_cr_requirement", "offset_account", "opening_account", "no_manual_entry", "active_from", "active_to", "suspended", "close_type", "consolidation_account", "default_currency", "fx_revaluation", "exchange_rate_type", "opening_debit", "opening_credit"],
                ["account_code", "account_name"]),
            ["sales_orders"] = new("sales_orders", "Sales orders with lines",
                ["so_no", "customer_account", "customer_name", "invoice_account", "order_type", "currency_code", "status", "order_date", "requested_receipt_date", "confirmed_receipt_date", "requested_ship_date", "confirmed_ship_date", "mode_of_delivery", "delivery_terms", "warehouse", "site", "customer_reference", "customer_requisition", "total_discount_pct", "lpo_no", "salesman", "item_number", "item_name", "variant", "sales_category", "qty", "unit", "unit_price", "discount_pct", "vat_pct", "foc"],
                ["so_no", "item_number"]),
            ["purchase_orders"] = new("purchase_orders", "Purchase orders with lines",
                ["po_no", "vendor_account", "vendor_name", "invoice_account", "order_type", "currency_code", "status", "order_date", "requested_receipt_date", "confirmed_receipt_date", "mode_of_delivery", "delivery_terms", "warehouse", "site", "vendor_reference", "vendor_requisition", "buyer_group", "item_number", "item_name", "vendor_item_number", "procurement_category", "qty", "unit", "unit_price", "discount_pct", "vat_pct"],
                ["po_no", "item_number"]),
            ["quotations"] = new("quotations", "Sales quotations with lines",
                ["quote_no", "quote_name", "quote_type", "quote_order_type", "prospect", "customer_account", "customer_name", "status", "currency_code", "requested_receipt_date", "requested_ship_date", "customer_reference", "customer_requisition", "total_discount_pct", "price_locked", "item_number", "item_name", "variant", "sales_category", "qty", "unit", "unit_price", "discount_pct", "vat_pct"],
                ["quote_no", "item_number"]),
            ["warehouses"] = new("warehouses", "Warehouses (InventLocations)",
                ["code", "name", "entity_id", "site", "type", "cost_centre_id", "business_unit_id", "address", "city", "country_code"],
                ["code", "name"]),
            ["customer_invoice_journal"] = new("customer_invoice_journal", "Customer invoice journal (CustInvoiceJournal)",
                ["invoice_number", "issue_date", "order_id", "payment_terms", "currency_code", "subtotal_ex_vat", "total_vat", "total_incl_vat", "paid_amount"],
                []),
            ["vendor_invoice_journal"] = new("vendor_invoice_journal", "Vendor invoice journal (VendInvoiceJournal)",
                ["invoice_number", "vendor_account", "vendor_name", "status", "currency_code", "total_incl_vat"],
                []),
            ["customer_statement"] = new("customer_statement", "Customer account statement — entity wise",
                ["account_code", "name", "trn", "balance"],
                []),
            ["sales_analysis_by_invoice"] = new("sales_analysis_by_invoice", "Sales analysis by invoice (margin)",
                ["invoice_number", "issue_date", "currency_code", "subtotal_ex_vat", "total_vat", "total_incl_vat", "revenue", "cost"],
                []),
            ["gl_opening"] = new("gl_opening", "GL opening balances",
                ["account_code", "business_unit", "cost_centre", "debit", "credit", "currency", "reference", "date"],
                ["account_code"]),
        };

    public sealed record ImportResult(bool Ok, int Inserted, int Updated, int Skipped, IReadOnlyList<string> Errors, string Message);
    public sealed record ExportResult(bool Ok, string FileName, string Csv, int Rows, string Message);

    /// <summary>Template CSV (header row only) for an area.</summary>
    public static string Template(string area)
        => Areas.TryGetValue(area, out var a)
            ? string.Join(",", a.Columns.Select(Escape)) + "\n"
            : "";

    /// <summary>Import a CSV into an area. Natural-key upsert; returns row counts.</summary>
    public static async Task<ImportResult> ImportAsync(
        DbConnection connection,
        string area,
        string csvText,
        int adminId,
        CancellationToken cancellationToken = default)
    {
        if (!Areas.TryGetValue(area, out var spec))
        {
            return new ImportResult(false, 0, 0, 0, ["unknown area"], "Unknown CSV area '" + area + "'.");
        }

        var rows = Parse(csvText);
        if (rows.Count < 2)
        {
            return new ImportResult(false, 0, 0, 0, ["no rows"], "Paste a CSV with a header row and at least one data row.");
        }

        var headers = rows[0].Select(Normalize).ToArray();
        var errors = new List<string>();
        foreach (var req in spec.Required)
        {
            if (!headers.Contains(req, StringComparer.OrdinalIgnoreCase))
            {
                return new ImportResult(false, 0, 0, 0, ["missing " + req], spec.Label + " import needs column '" + req + "'. Template columns: " + string.Join(", ", spec.Columns));
            }
        }

        await new ErpD365DocCycleService().EnsureSchemaAsync(connection, cancellationToken).ConfigureAwait(false);

        var inserted = 0; var updated = 0; var skipped = 0;
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        for (var i = 1; i < rows.Count; i++)
        {
            var cells = rows[i];
            var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            for (var c = 0; c < headers.Length && c < cells.Count; c++)
            {
                map[headers[c]] = cells[c].Trim();
            }

            try
            {
                var (ins, upd) = await CommitRowAsync(connection, area, map, adminId, now, cancellationToken).ConfigureAwait(false);
                inserted += ins; updated += upd;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                skipped++;
                if (errors.Count < 10)
                {
                    errors.Add("Row " + (i + 1).ToString(CultureInfo.InvariantCulture) + ": " + ex.Message);
                }
            }
        }

        var msg = spec.Label + ": " + inserted + " inserted, " + updated + " updated, " + skipped + " skipped.";
        return new ImportResult(true, inserted, updated, skipped, errors, msg);
    }

    /// <summary>Export an area's current rows as CSV (entity-agnostic; whole tenant scope).</summary>
    public static async Task<ExportResult> ExportAsync(
        DbConnection connection,
        string area,
        long entityId = 0,
        CancellationToken cancellationToken = default)
    {
        if (!Areas.TryGetValue(area, out var spec))
        {
            return new ExportResult(false, "", "", 0, "Unknown CSV area '" + area + "'.");
        }

        var sql = ExportSql(area).Replace("__ENTITY_WHERE__",
            entityId > 0 ? " WHERE c.`entity_id` = " + entityId.ToString(CultureInfo.InvariantCulture) : "");
        if (sql.Length == 0)
        {
            return new ExportResult(false, "", "", 0, "No export source for '" + area + "'.");
        }

        var rows = await ErpD365DocCycleService.RowsAsync(connection, sql, cancellationToken).ConfigureAwait(false);
        var sb = new StringBuilder();
        sb.AppendJoin(',', spec.Columns.Select(Escape)).Append('\n');
        foreach (var row in rows)
        {
            sb.AppendJoin(',', spec.Columns.Select(c => Escape(row.TryGetValue(c, out var v) ? v ?? "" : ""))).Append('\n');
        }

        return new ExportResult(true, area + "-" + DateTimeOffset.UtcNow.ToString("yyyyMMdd-HHmm", CultureInfo.InvariantCulture) + ".csv", sb.ToString(), rows.Count, "OK");
    }

    // ——— Synced D365 data: all-field template + export straight from staging ———

    /// <summary>Entity sets currently staged from D365, with row counts, for the studio.</summary>
    public static async Task<IReadOnlyList<(string Entity, long Rows)>> StagedEntitiesAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        var list = new List<(string, long)>();
        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT `entity`, COUNT(*) FROM `epc_d365_sync_objects` GROUP BY `entity` ORDER BY `entity`";
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                list.Add((reader.GetString(0), reader.GetInt64(1)));
            }
        }
        catch (DbException)
        {
            // staging table not created yet — nothing synced
        }
        return list;
    }

    /// <summary>
    /// Column set for a synced D365 entity: the canonical full schema (every field the entity
    /// can carry, from ErpD365EntityFieldCatalog — templates are complete even with staging
    /// empty), unioned with every top-level payload field seen in staging.
    /// </summary>
    public static async Task<IReadOnlyList<string>> D365EntityColumnsAsync(DbConnection connection, string entitySet, CancellationToken cancellationToken)
    {
        var cols = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var known in ErpD365EntityFieldCatalog.FieldsFor(entitySet))
        {
            if (seen.Add(known))
            {
                cols.Add(known);
            }
        }
        // The field store holds every field of every applied row — a field that appears only
        // outside the payload scan window below is still captured here.
        try
        {
            await using var fieldsCmd = connection.CreateCommand();
            fieldsCmd.CommandText = "SELECT `field_name` FROM `epc_d365_object_fields` WHERE `entity` = @e GROUP BY `field_name` ORDER BY MIN(`id`)";
            var fp = fieldsCmd.CreateParameter();
            fp.ParameterName = "@e";
            fp.Value = entitySet;
            fieldsCmd.Parameters.Add(fp);
            await using var fieldsReader = await fieldsCmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await fieldsReader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                if (seen.Add(fieldsReader.GetString(0)))
                {
                    cols.Add(fieldsReader.GetString(0));
                }
            }
        }
        catch (DbException)
        {
            // field store not provisioned yet — payload scan below still covers it
        }
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT `payload` FROM `epc_d365_sync_objects` WHERE `entity` = @e ORDER BY `id` DESC LIMIT 2000";
        var p = command.CreateParameter();
        p.ParameterName = "@e";
        p.Value = entitySet;
        command.Parameters.Add(p);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            JsonDocument doc;
            try { doc = JsonDocument.Parse(reader.GetString(0)); }
            catch (JsonException) { continue; }
            using (doc)
            {
                foreach (var prop in doc.RootElement.EnumerateObject())
                {
                    if (seen.Add(prop.Name))
                    {
                        cols.Add(prop.Name);
                    }
                }
            }
        }
        return cols;
    }

    /// <summary>Template CSV for a synced D365 entity: ext_key + every staged payload field.</summary>
    public static async Task<string> D365TemplateAsync(DbConnection connection, string entitySet, CancellationToken cancellationToken)
    {
        var cols = await D365EntityColumnsAsync(connection, entitySet, cancellationToken).ConfigureAwait(false);
        return string.Join(",", new[] { "ext_key" }.Concat(cols).Select(Escape)) + "\n";
    }

    /// <summary>
    /// Streams every staged row of an entity as CSV (ext_key + all payload fields; nested
    /// values stay compact JSON). Streams so million-row entities never buffer in memory.
    /// </summary>
    public static async Task StreamD365CsvAsync(DbConnection connection, string entitySet, Stream output, CancellationToken cancellationToken)
    {
        var cols = await D365EntityColumnsAsync(connection, entitySet, cancellationToken).ConfigureAwait(false);
        await using var writer = new StreamWriter(output, new UTF8Encoding(false), bufferSize: 64 * 1024, leaveOpen: true);
        await writer.WriteLineAsync(string.Join(",", new[] { "ext_key" }.Concat(cols).Select(Escape))).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT `ext_key`, `payload` FROM `epc_d365_sync_objects` WHERE `entity` = @e ORDER BY `id`";
        command.CommandTimeout = 300;
        var p = command.CreateParameter();
        p.ParameterName = "@e";
        p.Value = entitySet;
        command.Parameters.Add(p);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        var sinceFlush = 0;
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            JsonDocument doc;
            try { doc = JsonDocument.Parse(reader.GetString(1)); }
            catch (JsonException) { continue; }
            using (doc)
            {
                var line = new StringBuilder(Escape(reader.GetString(0)));
                foreach (var c in cols)
                {
                    line.Append(',').Append(Escape(JsonCell(doc.RootElement, c)));
                }
                await writer.WriteLineAsync(line.ToString()).ConfigureAwait(false);
            }
            if (++sinceFlush >= 500)
            {
                await writer.FlushAsync(cancellationToken).ConfigureAwait(false);
                sinceFlush = 0;
            }
        }
        await writer.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    private static string JsonCell(JsonElement root, string name)
    {
        if (!root.TryGetProperty(name, out var v) || v.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            return "";
        }
        return v.ValueKind == JsonValueKind.String ? v.GetString() ?? "" : v.GetRawText();
    }

    private static string ExportSql(string area) => area switch
    {
        "items" => "SELECT `sku`, `name`, `item_type`, `unit`, `active` FROM `epc_erp_inv_items` ORDER BY `sku`",
        "stock" => "SELECT s.`warehouse_id` AS warehouse, i.`sku`, s.`qty_on_hand`, s.`avg_unit_cost`, s.`batch_no`, s.`variant_label` FROM `epc_erp_inv_stock` s JOIN `epc_erp_inv_items` i ON i.`id` = s.`item_id` ORDER BY i.`sku`",
        "customers" => "SELECT * FROM `epc_erp_contacts` WHERE `party_type` IN ('customer','both') OR `party_type` IS NULL ORDER BY `id`",
        "vendors" => "SELECT * FROM `epc_erp_suppliers` ORDER BY `id`",
        "coa" => "SELECT `code` AS account_code, `name` AS account_name, `account_type`, `active` FROM `epc_erp_coa_accounts` ORDER BY `code`",
        "sales_orders" => "SELECT o.`so_no`, o.`status`, l.`item_code` AS item_number, l.`description` AS item_name, l.`qty`, l.`unit_price_ex_vat` AS unit_price FROM `epc_erp_sales_orders` o LEFT JOIN `epc_erp_sales_order_lines` l ON l.`sales_order_id` = o.`id` ORDER BY o.`so_no`, l.`id`",
        "purchase_orders" => "SELECT o.`po_no`, o.`status`, l.`item_code` AS item_number, l.`description` AS item_name, l.`qty`, l.`unit_cost_ex_vat` AS unit_price FROM `epc_erp_purchase_orders` o LEFT JOIN `epc_erp_po_lines` l ON l.`po_id` = o.`id` ORDER BY o.`po_no`, l.`id`",
        "quotations" => "SELECT q.`quote_no`, q.`status`, q.`customer_name`, q.`currency_code`, l.`item_number`, l.`product_name` AS item_name, l.`qty`, l.`unit_price` FROM `epc_erp_sales_quotations` q LEFT JOIN `epc_erp_sales_quotation_lines` l ON l.`quotation_id` = q.`id` ORDER BY q.`quote_no`, l.`line_no`",
        "warehouses" => "SELECT `code`, `name`, `entity_id`, `site`, `cost_centre_id`, `business_unit_id` FROM `epc_erp_inv_warehouses` ORDER BY `code`",
        "customer_invoice_journal" => "SELECT `invoice_number`, `issue_date`, `order_id`, `payment_terms`, `currency_code`, `subtotal_ex_vat`, `total_vat`, `total_incl_vat`, `paid_amount` FROM `epc_einvoice_documents` ORDER BY `id` DESC",
        "vendor_invoice_journal" => "SELECT o.`po_no` AS invoice_number, o.`vendor_account`, o.`vendor_name`, o.`status`, o.`currency_code`, IFNULL(o.`total_amount`,0) AS total_incl_vat FROM `epc_erp_purchase_orders` o ORDER BY o.`id` DESC",
        "customer_statement" => "SELECT c.`name`, c.`account_code`, IFNULL(c.`trn`,'') AS trn, IFNULL(SUM(l.`debit`),0)-IFNULL(SUM(l.`credit`),0) AS balance FROM `epc_erp_contacts` c LEFT JOIN `epc_erp_gl_lines` l ON l.`coa_id` IN (SELECT `id` FROM `epc_erp_coa_accounts` WHERE `code` = c.`account_code`)__ENTITY_WHERE__ GROUP BY c.`id` ORDER BY c.`name`",
        "sales_analysis_by_invoice" => "SELECT d.`invoice_number`, d.`issue_date`, d.`currency_code`, d.`subtotal_ex_vat`, d.`total_vat`, d.`total_incl_vat`, IFNULL((SELECT SUM(l.`quantity`*l.`unit_price`) FROM `epc_einvoice_lines` l WHERE l.`document_id` = d.`id`),0) AS revenue, IFNULL((SELECT SUM(l.`quantity`*IFNULL(i.`cost_price`,0)) FROM `epc_einvoice_lines` l JOIN `epc_erp_inv_items` i ON i.`sku` = l.`item_name` WHERE l.`document_id` = d.`id`),0) AS cost FROM `epc_einvoice_documents` d ORDER BY d.`id` DESC",
        "gl_opening" => "SELECT a.`code` AS account_code, j.`journal_date` AS `date`, l.`debit`, l.`credit` FROM `epc_erp_gl_lines` l JOIN `epc_erp_gl_journals` j ON j.`id` = l.`journal_id` JOIN `epc_erp_coa_accounts` a ON a.`id` = l.`coa_id` WHERE j.`source_type` = 'opening' ORDER BY a.`code`",
        _ => "",
    };

    private static async Task<(int ins, int upd)> CommitRowAsync(
        DbConnection c, string area, Dictionary<string, string> m, int adminId, long now, CancellationToken ct)
    {
        return area switch
        {
            "items" => await CommitItemAsync(c, m, now, ct).ConfigureAwait(false),
            "stock" => await CommitStockAsync(c, m, now, ct).ConfigureAwait(false),
            "customers" => await CommitPartyAsync(c, m, "epc_erp_contacts", "customer", ErpD365DocCycleService.CustomerColumns, "account_code", now, ct).ConfigureAwait(false),
            "vendors" => await CommitVendorAsync(c, m, now, ct).ConfigureAwait(false),
            "coa" => await CommitCoaAsync(c, m, now, ct).ConfigureAwait(false),
            "gl_opening" => await CommitGlOpeningAsync(c, m, adminId, now, ct).ConfigureAwait(false),
            "warehouses" => await CommitWarehouseAsync(c, m, now, ct).ConfigureAwait(false),
            "sales_orders" or "purchase_orders" or "quotations" => await CommitDocLineAsync(c, area, m, now, ct).ConfigureAwait(false),
            _ => (0, 0),
        };
    }

    private static async Task<(int, int)> CommitItemAsync(DbConnection c, Dictionary<string, string> m, long now, CancellationToken ct)
    {
        var sku = M(m, "sku"); var name = M(m, "name");
        if (sku.Length == 0 || name.Length == 0) return (0, 0);
        var id = await ErpDb.LongAsync(c, null, "SELECT `id` FROM `epc_erp_inv_items` WHERE `sku` = ? LIMIT 1", ct, sku).ConfigureAwait(false);
        var unit = M(m, "unit");
        var itype = M(m, "item_type");
        var upd = 0;
        if (id == 0)
        {
            decimal.TryParse(M(m, "cost_price"), System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out var cost);
            await ErpDb.ExecuteAsync(c, null,
                "INSERT INTO `epc_erp_inv_items` (`sku`, `name`, `item_type`, `unit`, `cost_price`, `active`, `time_created`) VALUES (?,?,?,?,?,?,?)",
                ct, sku, name, itype is "standard" or "perishable" or "serialized" ? itype : "standard",
                unit.Length > 0 ? unit : "pcs", cost, M(m, "active") is "0" or "no" ? 0 : 1, now).ConfigureAwait(false);
            id = await ErpDb.LastInsertIdAsync(c, null, ct).ConfigureAwait(false);
        }
        else
        {
            // Only overwrite the cost when the imported value actually parses — a malformed cell must not zero a recorded cost.
            var costSet = decimal.TryParse(M(m, "cost_price"), System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out var uc) && M(m, "cost_price").Length > 0
                ? ", `cost_price` = " + uc.ToString(System.Globalization.CultureInfo.InvariantCulture)
                : "";
            await ErpDb.ExecuteAsync(c, null,
                "UPDATE `epc_erp_inv_items` SET `name` = ?, `unit` = ?" + costSet + ", `active` = ? WHERE `id` = ?",
                ct, name, unit.Length > 0 ? unit : "pcs", M(m, "active") is "0" or "no" ? 0 : 1, id).ConfigureAwait(false);
            upd = 1;
        }

        // SHAM/industry attributes land in the KV field store — tenant picks which to use.
        var attrs = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        foreach (var kv in m)
        {
            if (kv.Key is not ("sku" or "name" or "unit" or "item_type" or "active" or "cost_price" or "sell_price") && kv.Value.Length > 0)
            {
                attrs[kv.Key] = kv.Value;
            }
        }
        if (attrs.Count > 0)
        {
            await new ErpD365DocCycleService().SaveProductFieldsAsync(c, id, attrs, 0, ct).ConfigureAwait(false);
        }
        return (upd == 0 ? 1 : 0, upd);
    }

    private static async Task<(int, int)> CommitStockAsync(DbConnection c, Dictionary<string, string> m, long now, CancellationToken ct)
    {
        var sku = M(m, "sku"); var wh = M(m, "warehouse");
        if (sku.Length == 0 || wh.Length == 0) return (0, 0);
        var itemId = await ErpDb.LongAsync(c, null, "SELECT `id` FROM `epc_erp_inv_items` WHERE `sku` = ? LIMIT 1", ct, sku).ConfigureAwait(false);
        if (itemId == 0) return (0, 0);
        var whId = await ErpDb.LongAsync(c, null, "SELECT `id` FROM `epc_erp_inv_warehouses` WHERE `code` = ? OR `name` = ? LIMIT 1", ct, wh, wh).ConfigureAwait(false);
        if (whId == 0) whId = ToLong(wh);
        if (whId == 0) throw new InvalidOperationException("Unknown warehouse '" + wh + "'.");
        if (!decimal.TryParse(M(m, "qty_on_hand"), NumberStyles.Number, CultureInfo.InvariantCulture, out var qty))
        {
            throw new InvalidOperationException("qty_on_hand '" + M(m, "qty_on_hand") + "' is not a number.");
        }
        var cost = ToDec(m, "avg_unit_cost");
        var batch = M(m, "batch_no"); var variant = M(m, "variant_label");
        var exists = await ErpDb.LongAsync(c, null,
            "SELECT `id` FROM `epc_erp_inv_stock` WHERE `warehouse_id` = ? AND `item_id` = ? AND IFNULL(`batch_no`,'') = ? AND IFNULL(`variant_label`,'') = ? LIMIT 1",
            ct, whId, itemId, batch, variant).ConfigureAwait(false);
        if (exists == 0)
        {
            await ErpDb.ExecuteAsync(c, null,
                "INSERT INTO `epc_erp_inv_stock` (`warehouse_id`, `item_id`, `qty_on_hand`, `avg_unit_cost`, `batch_no`, `variant_label`, `time_updated`) VALUES (?,?,?,?,?,?,?)",
                ct, whId, itemId, qty, cost, batch, variant, now).ConfigureAwait(false);
            return (1, 0);
        }
        await ErpDb.ExecuteAsync(c, null,
            "UPDATE `epc_erp_inv_stock` SET `qty_on_hand` = ?, `avg_unit_cost` = ?, `time_updated` = ? WHERE `id` = ?",
            ct, qty, cost, now, exists).ConfigureAwait(false);
        return (0, 1);
    }

    private static async Task<(int, int)> CommitPartyAsync(
        DbConnection c, Dictionary<string, string> m, string table, string partyType, string[] allowed, string acctCol, long now, CancellationToken ct)
    {
        var name = M(m, "name");
        if (name.Length == 0) return (0, 0);
        var acct = M(m, acctCol);
        var id = acct.Length > 0
            ? await ErpDb.LongAsync(c, null, "SELECT `id` FROM `" + table + "` WHERE `" + acctCol + "` = ? LIMIT 1", ct, acct).ConfigureAwait(false)
            : await ErpDb.LongAsync(c, null, "SELECT `id` FROM `" + table + "` WHERE `name` = ? LIMIT 1", ct, name).ConfigureAwait(false);
        if (id == 0)
        {
            var cols = new List<string> { "name" }; var vals = new List<object?> { name };
            if (table == "epc_erp_contacts") { cols.Add("party_type"); vals.Add(partyType); }
            foreach (var col in allowed)
            {
                if (m.TryGetValue(col, out var v) && v.Length > 0) { cols.Add('`' + col + '`'); vals.Add(v); }
            }
            // direct mapping for common names
            foreach (var (key, col) in new[] { ("email", "email"), ("phone", "phone"), ("country", "country_code"), ("trn", "trn"), ("address", "address"), ("city", "city"), ("customer_group", "customer_group"), ("on_hold", "on_hold") })
            {
                if (m.TryGetValue(key, out var v) && v.Length > 0 && !cols.Contains('`' + col + '`') && !cols.Contains(col)) { cols.Add('`' + col + '`'); vals.Add(v); }
            }
            var placeholders = string.Join(",", cols.Select((_, i) => "@p" + i.ToString(CultureInfo.InvariantCulture)));
            await ErpDb.ExecuteAsync(c, null,
                "INSERT INTO `" + table + "` (" + string.Join(",", cols.Select(x => x.StartsWith('`') ? x : '`' + x + '`')) + ") VALUES (" + placeholders + ")",
                ct, vals.ToArray()).ConfigureAwait(false);
            return (1, 0);
        }
        var fields = m.Where(kv => allowed.Contains(kv.Key, StringComparer.OrdinalIgnoreCase) && kv.Value.Length > 0)
            .ToDictionary(kv => kv.Key, kv => (string?)kv.Value, StringComparer.Ordinal);
        await new ErpD365DocCycleService().SaveFieldsAsync(c, table, "id", id, allowed, fields, 0, "", ct).ConfigureAwait(false);
        return (0, 1);
    }

    private static async Task<(int, int)> CommitVendorAsync(DbConnection c, Dictionary<string, string> m, long now, CancellationToken ct)
        => await CommitPartyAsync(c, m, "epc_erp_suppliers", "vendor", ErpD365DocCycleService.VendorColumns, "vendor_account", now, ct).ConfigureAwait(false);

    private static async Task<(int, int)> CommitCoaAsync(DbConnection c, Dictionary<string, string> m, long now, CancellationToken ct)
    {
        var code = M(m, "account_code"); var name = M(m, "account_name");
        if (code.Length == 0 || name.Length == 0) return (0, 0);
        var id = await ErpDb.LongAsync(c, null, "SELECT `id` FROM `epc_erp_coa_accounts` WHERE `code` = ? LIMIT 1", ct, code).ConfigureAwait(false);
        var type = M(m, "account_type");
        if (id == 0)
        {
            await ErpDb.ExecuteAsync(c, null,
                "INSERT INTO `epc_erp_coa_accounts` (`code`, `name`, `account_type`, `active`, `time_created`) VALUES (?,?,?,1,?)",
                ct, code, name, type is "asset" or "liability" or "equity" or "revenue" or "expense" ? type : "expense", now).ConfigureAwait(false);
            return (1, 0);
        }
        await ErpDb.ExecuteAsync(c, null,
            "UPDATE `epc_erp_coa_accounts` SET `name` = ? WHERE `id` = ?", ct, name, id).ConfigureAwait(false);
        return (0, 1);
    }

    private static async Task<(int, int)> CommitGlOpeningAsync(DbConnection c, Dictionary<string, string> m, int adminId, long now, CancellationToken ct)
    {
        var code = M(m, "account_code");
        if (code.Length == 0) return (0, 0);
        var coaId = await ErpDb.LongAsync(c, null, "SELECT `id` FROM `epc_erp_coa_accounts` WHERE `code` = ? LIMIT 1", ct, code).ConfigureAwait(false);
        if (coaId == 0) return (0, 0);
        var debit = ToDec(m, "debit"); var credit = ToDec(m, "credit");
        if (debit == 0 && credit == 0) return (0, 0);
        var jdate = ToUnix(m, "date");
        var jref = M(m, "reference");
        var jno = "OPEN-" + DateTimeOffset.FromUnixTimeSeconds(now).ToString("yyyyMMdd", CultureInfo.InvariantCulture) + "-" + code;
        var jid = await ErpDb.LongAsync(c, null, "SELECT `id` FROM `epc_erp_gl_journals` WHERE `journal_no` = ? LIMIT 1", ct, jno).ConfigureAwait(false);
        if (jid == 0)
        {
            await ErpDb.ExecuteAsync(c, null,
                "INSERT INTO `epc_erp_gl_journals` (`journal_no`, `journal_date`, `reference`, `description`, `source_type`, `admin_id`, `time_created`) VALUES (?,?,?,'Opening balance CSV','opening',?,?)",
                ct, jno, jdate > 0 ? jdate : now, jref, adminId, now).ConfigureAwait(false);
            jid = await ErpDb.LastInsertIdAsync(c, null, ct).ConfigureAwait(false);
        }
        var lineId = await ErpDb.LongAsync(c, null,
            "SELECT `id` FROM `epc_erp_gl_lines` WHERE `journal_id` = ? AND `coa_id` = ? LIMIT 1",
            ct, jid, coaId).ConfigureAwait(false);
        if (lineId > 0)
        {
            await ErpDb.ExecuteAsync(c, null,
                "UPDATE `epc_erp_gl_lines` SET `debit` = ?, `credit` = ?, `line_note` = ? WHERE `id` = ?",
                ct, debit, credit, "CSV opening" + (jref.Length > 0 ? " " + jref : ""), lineId).ConfigureAwait(false);
            return (0, 1);
        }
        await ErpDb.ExecuteAsync(c, null,
            "INSERT INTO `epc_erp_gl_lines` (`journal_id`, `coa_id`, `debit`, `credit`, `line_note`) VALUES (?,?,?,?,?)",
            ct, jid, coaId, debit, credit, "CSV opening" + (jref.Length > 0 ? " " + jref : "")).ConfigureAwait(false);
        return (1, 0);
    }

    /// <summary>Document CSV rows carry both header fields and one line each — a doc_no repeating across rows means multiple lines.</summary>
    private static async Task<(int, int)> CommitWarehouseAsync(DbConnection c, Dictionary<string, string> m, long now, CancellationToken ct)
    {
        var code = M(m, "code"); var name = M(m, "name");
        if (code.Length == 0 || name.Length == 0) return (0, 0);
        var id = await ErpDb.LongAsync(c, null, "SELECT `id` FROM `epc_erp_inv_warehouses` WHERE `code` = ? LIMIT 1", ct, code).ConfigureAwait(false);
        var fields = m.Where(kv => kv.Value.Length > 0).ToDictionary(kv => kv.Key, kv => (string?)kv.Value, StringComparer.Ordinal);
        if (id == 0)
        {
            await ErpDb.ExecuteAsync(c, null, "INSERT INTO `epc_erp_inv_warehouses` (`code`, `name`, `time_created`) VALUES (?,?,?)", ct, code, name, now).ConfigureAwait(false);
            id = await ErpDb.LastInsertIdAsync(c, null, ct).ConfigureAwait(false);
        }
        await new ErpD365DocCycleService().SaveWarehouseFieldsAsync(c, id, fields, 0, ct).ConfigureAwait(false);
        return (id > 0 ? 1 : 0, 0);
    }

    private static async Task<(int, int)> CommitDocLineAsync(DbConnection c, string area, Dictionary<string, string> m, long now, CancellationToken ct)
    {
        var (table, lineTable, noCol, orderFk) = area switch
        {
            "sales_orders" => ("epc_erp_sales_orders", "epc_erp_sales_order_lines", "so_no", "sales_order_id"),
            "purchase_orders" => ("epc_erp_purchase_orders", "epc_erp_po_lines", "po_no", "po_id"),
            "quotations" => ("epc_erp_sales_quotations", "epc_erp_sales_quotation_lines", "quote_no", "quotation_id"),
            _ => ("", "", "", ""),
        };
        var no = M(m, noCol);
        if (no.Length == 0) return (0, 0);

        var oid = await ErpDb.LongAsync(c, null,
            "SELECT `id` FROM `" + table + "` WHERE `" + noCol + "` = ? LIMIT 1",
            ct, no).ConfigureAwait(false);
        var upd = 0;
        if (oid == 0)
        {
            if (table == "epc_erp_sales_quotations")
            {
                await ErpDb.ExecuteAsync(c, null,
                    "INSERT INTO `epc_erp_sales_quotations` (`quote_no`, `quote_name`, `customer_name`, `status`, `currency_code`, `time_created`, `time_updated`) VALUES (?,?,?,'created',?,?,?)",
                    ct, no, M(m, "quote_name"), M(m, "customer_name"), M(m, "currency_code"), now, now).ConfigureAwait(false);
            }
            else
            {
                await ErpDb.ExecuteAsync(c, null,
                    "INSERT INTO `" + table + "` (`" + noCol + "`, `status`, `time_created`) VALUES (?, 'draft', ?)",
                    ct, no, now).ConfigureAwait(false);
            }
            oid = await ErpDb.LastInsertIdAsync(c, null, ct).ConfigureAwait(false);
        }
        else upd = 1;

        // D365 header fields (allowlisted by the doc-cycle service) — only known columns are written.
        var d365 = new ErpD365DocCycleService();
        var hdr = m.Where(kv => kv.Value.Length > 0).ToDictionary(kv => kv.Key, kv => (string?)kv.Value, StringComparer.Ordinal);
        if (area == "sales_orders")
            await d365.SaveSalesOrderHeaderAsync(c, oid, hdr, 0, ct).ConfigureAwait(false);
        else if (area == "purchase_orders")
            await d365.SavePurchaseOrderHeaderAsync(c, oid, hdr, 0, ct).ConfigureAwait(false);

        var item = M(m, "item_number"); var itemName = M(m, "item_name");
        var qty = ToDec(m, "qty"); var price = ToDec(m, "unit_price");
        if (item.Length > 0 || itemName.Length > 0)
        {
            if (lineTable == "epc_erp_sales_quotation_lines")
            {
                await new ErpD365DocCycleService().SaveQuotationLineAsync(c, oid,
                    new Dictionary<string, string?>
                    {
                        ["item_number"] = item, ["product_name"] = itemName,
                        ["qty"] = qty.ToString(CultureInfo.InvariantCulture),
                        ["unit_price"] = price.ToString(CultureInfo.InvariantCulture),
                        ["unit"] = M(m, "unit"), ["sales_category"] = M(m, "sales_category"),
                        ["discount_pct"] = M(m, "discount_pct"), ["vat_pct"] = M(m, "vat_pct"),
                        ["variant_number"] = M(m, "variant"),
                    }, 0, ct).ConfigureAwait(false);
            }
            else
            {
                await ErpDb.ExecuteAsync(c, null,
                    "INSERT INTO `" + lineTable + "` (`" + orderFk + "`, `item_code`, `description`, `qty`, `" + (lineTable == "epc_erp_po_lines" ? "unit_cost_ex_vat" : "unit_price_ex_vat") + "`, `line_ex_vat`) VALUES (?,?,?,?,?,?)",
                    ct, oid, item, itemName, qty, price, qty * price).ConfigureAwait(false);
            }
        }
        return (upd == 0 ? 1 : 0, 0);
    }

    private static string M(Dictionary<string, string> m, string k) => m.TryGetValue(k, out var v) ? v : "";
    private static decimal ToDec(Dictionary<string, string> m, string k)
        => m.TryGetValue(k, out var v) && decimal.TryParse(v, NumberStyles.Number, CultureInfo.InvariantCulture, out var n) ? n : 0;
    private static long ToLong(string v) => long.TryParse(v, out var n) ? n : 0;
    private static long ToUnix(Dictionary<string, string> m, string k)
    {
        if (!m.TryGetValue(k, out var v)) return 0;
        if (long.TryParse(v, out var n)) return n;
        return DateTimeOffset.TryParse(v, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var d) ? d.ToUnixTimeSeconds() : 0;
    }

    private static string Normalize(string h)
        => h.Trim().ToLowerInvariant().Replace(" ", "_").Replace("-", "_").Replace(".", "");

    private static string Escape(string v)
        => v.Contains(',') || v.Contains('"') || v.Contains('\n') ? "\"" + v.Replace("\"", "\"\"") + "\"" : v;

    /// <summary>Simple RFC4180 CSV parse (quoted cells, commas, newlines).</summary>
    public static List<List<string>> Parse(string text)
    {
        var rows = new List<List<string>>();
        var cur = new List<string>();
        var cell = new StringBuilder();
        var inQ = false;
        for (var i = 0; i < text.Length; i++)
        {
            var ch = text[i];
            if (inQ)
            {
                if (ch == '"' && i + 1 < text.Length && text[i + 1] == '"') { cell.Append('"'); i++; }
                else if (ch == '"') inQ = false;
                else cell.Append(ch);
                continue;
            }
            if (ch == '"') { inQ = true; continue; }
            if (ch == ',') { cur.Add(cell.ToString()); cell.Clear(); continue; }
            if (ch == '\n' || ch == '\r')
            {
                if (ch == '\r' && i + 1 < text.Length && text[i + 1] == '\n') i++;
                cur.Add(cell.ToString()); cell.Clear();
                rows.Add(cur); cur = new List<string>();
                continue;
            }
            cell.Append(ch);
        }
        cur.Add(cell.ToString());
        if (cur.Count > 1 || cur[0].Length > 0) rows.Add(cur);
        return rows;
    }
}
