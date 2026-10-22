using System.Data;
using System.Data.Common;
using System.Globalization;
using System.Text.Json;

namespace EcomAE.Platform.Erp;

/// <summary>
/// Applies staged D365 F&amp;O payloads to the operational masters — this is where
/// the pull becomes visible data: customers land on the customer master, released
/// products on inventory items, sales/purchase order headers on the document tables,
/// main accounts on the chart of accounts. Upserts keep a D365-side unique key so a
/// re-pull refreshes rather than duplicates.
/// </summary>
public interface IErpD365MappingService
{
    /// <summary>Entities that have a staged → master mapping.</summary>
    IReadOnlyDictionary<string, string> MappedEntities { get; }
    Task<ErpD365ApplyResult> ApplyAsync(DbConnection connection, string entity, long adminId, CancellationToken cancellationToken = default);
}

public sealed record ErpD365ApplyResult(bool Succeeded, string Code, string Message, int Mapped, int Skipped);

public sealed class ErpD365MappingService : IErpD365MappingService
{
    public IReadOnlyDictionary<string, string> MappedEntities { get; } =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["ReleasedProductsV2"] = "Inventory items",
            ["CustomersV3"] = "Customers",
            ["VendorsV2"] = "Suppliers",
            ["SalesOrderHeadersV2"] = "Sales orders",
            ["PurchaseOrderHeadersV2"] = "Purchase orders",
            ["MainAccounts"] = "Chart of accounts",
        };

    public async Task<ErpD365ApplyResult> ApplyAsync(DbConnection connection, string entity, long adminId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connection);
        if (!MappedEntities.ContainsKey(entity))
        {
            return new ErpD365ApplyResult(false, "no_mapping", $"No staged → master mapping exists for '{entity}' yet.", 0, 0);
        }

        await ErpDb.TryExecuteAsync(connection,
            "CREATE TABLE IF NOT EXISTS `epc_d365_sync_objects` (" +
            "id BIGINT NOT NULL AUTO_INCREMENT PRIMARY KEY," +
            "entity VARCHAR(128) NOT NULL," +
            "ext_key VARCHAR(255) NOT NULL DEFAULT ''," +
            "payload MEDIUMTEXT NOT NULL," +
            "run_id BIGINT NOT NULL DEFAULT 0," +
            "synced_at BIGINT NOT NULL," +
            "UNIQUE KEY uq_entity_ext (entity, ext_key)" +
            ") ENGINE=InnoDB DEFAULT CHARSET=utf8mb4",
            cancellationToken).ConfigureAwait(false);

        await ErpDb.TryExecuteAsync(connection,
            "CREATE TABLE IF NOT EXISTS `epc_d365_object_fields` (" +
            "id BIGINT NOT NULL AUTO_INCREMENT PRIMARY KEY," +
            "entity VARCHAR(128) NOT NULL," +
            "ext_key VARCHAR(255) NOT NULL DEFAULT ''," +
            "field_name VARCHAR(191) NOT NULL," +
            "field_value MEDIUMTEXT NULL," +
            "UNIQUE KEY uq_entity_ext_field (entity, ext_key, field_name)," +
            "KEY ix_entity (entity, ext_key)" +
            ") ENGINE=InnoDB DEFAULT CHARSET=utf8mb4",
            cancellationToken).ConfigureAwait(false);

        var staged = new List<(string Key, string Payload)>();
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = "SELECT ext_key, payload FROM epc_d365_sync_objects WHERE entity = @entity ORDER BY id";
            var entityParam = command.CreateParameter();
            entityParam.ParameterName = "@entity";
            entityParam.Value = entity;
            command.Parameters.Add(entityParam);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                staged.Add((reader.GetString(0), reader.GetString(1)));
            }
        }

        if (staged.Count == 0)
        {
            return new ErpD365ApplyResult(false, "no_staged", $"No staged rows for {entity} — pull the entity first.", 0, 0);
        }

        var mapped = 0;
        var skipped = 0;
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        foreach (var (extKey, payload) in staged)
        {
            JsonDocument doc;
            try
            {
                doc = JsonDocument.Parse(payload);
            }
            catch (JsonException)
            {
                skipped++;
                continue;
            }

            using (doc)
            {
                var applied = entity switch
                {
                    "ReleasedProductsV2" => await ApplyProductAsync(connection, transaction, doc.RootElement, cancellationToken).ConfigureAwait(false),
                    "CustomersV3" => await ApplyCustomerAsync(connection, transaction, doc.RootElement, cancellationToken).ConfigureAwait(false),
                    "VendorsV2" => await ApplyVendorAsync(connection, transaction, doc.RootElement, cancellationToken).ConfigureAwait(false),
                    "SalesOrderHeadersV2" => await ApplySalesOrderAsync(connection, transaction, doc.RootElement, adminId, cancellationToken).ConfigureAwait(false),
                    "PurchaseOrderHeadersV2" => await ApplyPurchaseOrderAsync(connection, transaction, doc.RootElement, adminId, cancellationToken).ConfigureAwait(false),
                    "MainAccounts" => await ApplyMainAccountAsync(connection, transaction, doc.RootElement, cancellationToken).ConfigureAwait(false),
                    _ => false,
                };
                if (applied)
                {
                    await StoreFieldsAsync(connection, transaction, entity, extKey, doc.RootElement, cancellationToken).ConfigureAwait(false);
                    mapped++;
                }
                else
                {
                    skipped++;
                }
            }
        }
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);

        var label = MappedEntities[entity];
        return new ErpD365ApplyResult(true, "ok", $"Applied {mapped} row(s) into {label}.", mapped, skipped);
    }

    /// <summary>Stores every F&O payload field for a staged row in epc_d365_object_fields (one row per field) so the module screens can display the complete field set, not just the mapped columns.</summary>
    private static async Task StoreFieldsAsync(DbConnection connection, DbTransaction tx, string entity, string extKey, JsonElement row, CancellationToken ct)
    {
        await ErpDb.ExecuteAsync(connection, tx,
            ErpDb.Positional("DELETE FROM `epc_d365_object_fields` WHERE `entity` = ? AND `ext_key` = ?"),
            ct, entity, extKey).ConfigureAwait(false);
        foreach (var property in row.EnumerateObject())
        {
            var text = property.Value.ValueKind == JsonValueKind.String
                ? property.Value.GetString()
                : property.Value.GetRawText();
            await ErpDb.ExecuteAsync(connection, tx,
                ErpDb.Positional("INSERT INTO `epc_d365_object_fields` (`entity`, `ext_key`, `field_name`, `field_value`) VALUES (?, ?, ?, ?)"),
                ct, entity, extKey, Clip(property.Name, 191), Clip(text, 60000)).ConfigureAwait(false);
        }
    }

    // ---- field helpers ----

    private static string Str(JsonElement row, params string[] names)
    {
        foreach (var name in names)
        {
            if (row.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String)
            {
                var text = value.GetString() ?? string.Empty;
                if (text.Trim().Length > 0)
                {
                    return text.Trim();
                }
            }
        }
        return string.Empty;
    }

    private static decimal Dec(JsonElement row, params string[] names)
    {
        foreach (var name in names)
        {
            if (!row.TryGetProperty(name, out var value))
            {
                continue;
            }
            if (value.ValueKind == JsonValueKind.Number && value.TryGetDecimal(out var n))
            {
                return n;
            }
            if (value.ValueKind == JsonValueKind.String && decimal.TryParse(value.GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var s))
            {
                return s;
            }
        }
        return 0m;
    }

    private static string Clip(string? value, int max)
    {
        var text = (value ?? string.Empty).Trim();
        return text.Length <= max ? text : text[..max];
    }

    // ---- per-entity mappers ----

    /// <summary>ReleasedProductsV2 → epc_erp_inv_items (upsert by sku).</summary>
    private static async Task<bool> ApplyProductAsync(DbConnection connection, DbTransaction tx, JsonElement row, CancellationToken ct)
    {
        var sku = Str(row, "ItemNumber", "ItemId", "ProductNumber");
        if (sku.Length == 0)
        {
            return false;
        }

        var name = Str(row, "ASAP_ProductName", "ProductName", "ASAP_SearchName", "SearchName", "ItemDescription");
        if (name.Length == 0)
        {
            name = sku;
        }

        var unit = Str(row, "PrimaryUnitSymbol", "InventoryUnitSymbol", "UnitId");
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        await ErpDb.ExecuteAsync(connection, tx,
            "INSERT INTO `epc_erp_inv_items` (`sku`, `name`, `unit`, `active`, `time_created`) " +
            "VALUES (?, ?, ?, 1, ?) ON DUPLICATE KEY UPDATE `name` = VALUES(`name`)",
            ct, Clip(sku, 64), Clip(name, 255), Clip(unit.Length > 0 ? unit : "pcs", 16), now).ConfigureAwait(false);
        return true;
    }

    /// <summary>CustomersV3 → epc_erp_customers (upsert by name).</summary>
    private static async Task<bool> ApplyCustomerAsync(DbConnection connection, DbTransaction tx, JsonElement row, CancellationToken ct)
    {
        var name = Str(row, "OrganizationName", "CustomerName", "Name", "CustomerAccount");
        if (name.Length == 0)
        {
            return false;
        }

        var email = Str(row, "PrimaryContactEmail", "PrimaryContactEmailAddress", "InvoiceEmail");
        var existing = await ErpDb.LongAsync(connection, tx,
            ErpDb.Positional("SELECT `id` FROM `epc_erp_customers` WHERE `name` = ? LIMIT 1"), ct, name).ConfigureAwait(false);
        if (existing > 0)
        {
            await ErpDb.ExecuteAsync(connection, tx,
                ErpDb.Positional("UPDATE `epc_erp_customers` SET `email` = ? WHERE `id` = ?"), ct, Clip(email, 160), existing).ConfigureAwait(false);
        }
        else
        {
            await ErpDb.ExecuteAsync(connection, tx,
                ErpDb.Positional("INSERT INTO `epc_erp_customers` (`name`, `email`, `created_at_utc`) VALUES (?, ?, UTC_TIMESTAMP())"),
                ct, Clip(name, 160), Clip(email, 160)).ConfigureAwait(false);
        }
        return true;
    }

    /// <summary>VendorsV2 → epc_erp_suppliers (upsert by vendor_account).</summary>
    private static async Task<bool> ApplyVendorAsync(DbConnection connection, DbTransaction tx, JsonElement row, CancellationToken ct)
    {
        var name = Str(row, "VendorOrganizationName", "VendorName", "Name", "VendorAccountNumber");
        var account = Str(row, "VendorAccountNumber", "VendorAccount", "AccountNum");
        if (name.Length == 0)
        {
            return false;
        }

        var email = Str(row, "PrimaryContactEmail", "PrimaryContactEmailAddress");
        var phone = Str(row, "PrimaryContactPhone", "PrimaryContactTelephone");
        var trn = Str(row, "TaxExemptNumber", "VATNum", "TaxRegistrationNumber");
        var currency = Str(row, "CurrencyCode", "Currency");
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var hasVendorAccount = await ErpDb.ColumnKnownAsync(connection, "epc_erp_suppliers", "vendor_account", ct).ConfigureAwait(false);
        var hasCurrency = await ErpDb.ColumnKnownAsync(connection, "epc_erp_suppliers", "currency_code", ct).ConfigureAwait(false);
        var hasTrn = await ErpDb.ColumnKnownAsync(connection, "epc_erp_suppliers", "trn", ct).ConfigureAwait(false);
        var hasPhone = await ErpDb.ColumnKnownAsync(connection, "epc_erp_suppliers", "contact_phone", ct).ConfigureAwait(false);
        var existing = hasVendorAccount && account.Length > 0
            ? await ErpDb.LongAsync(connection, tx,
                ErpDb.Positional("SELECT `id` FROM `epc_erp_suppliers` WHERE `vendor_account` = ? LIMIT 1"), ct, account).ConfigureAwait(false)
            : await ErpDb.LongAsync(connection, tx,
                ErpDb.Positional("SELECT `id` FROM `epc_erp_suppliers` WHERE `name` = ? LIMIT 1"), ct, name).ConfigureAwait(false);
        if (existing > 0)
        {
            var sets = new List<string> { "`contact_email` = ?" };
            var vals = new List<object?> { Clip(email, 255) };
            if (hasPhone) { sets.Add("`contact_phone` = ?"); vals.Add(Clip(phone, 64)); }
            if (hasTrn) { sets.Add("`trn` = ?"); vals.Add(Clip(trn, 64)); }
            if (hasCurrency) { sets.Add("`currency_code` = ?"); vals.Add(Clip(currency.Length > 0 ? currency : "AED", 8)); }
            vals.Add(existing);
            await ErpDb.ExecuteAsync(connection, tx,
                ErpDb.Positional("UPDATE `epc_erp_suppliers` SET " + string.Join(", ", sets) + " WHERE `id` = ?"),
                ct, vals.ToArray()).ConfigureAwait(false);
        }
        else
        {
            var cols = new List<string> { "`name`", "`contact_email`" };
            var marks = new List<string> { "?", "?" };
            var vals = new List<object?> { Clip(name, 255), Clip(email, 255) };
            if (hasPhone) { cols.Add("`contact_phone`"); marks.Add("?"); vals.Add(Clip(phone, 64)); }
            if (hasTrn) { cols.Add("`trn`"); marks.Add("?"); vals.Add(Clip(trn, 64)); }
            if (hasCurrency) { cols.Add("`currency_code`"); marks.Add("?"); vals.Add(Clip(currency.Length > 0 ? currency : "AED", 8)); }
            if (hasVendorAccount) { cols.Add("`vendor_account`"); marks.Add("?"); vals.Add(Clip(account, 32)); }
            cols.Add("`active`"); marks.Add("1");
            cols.Add("`time_created`"); marks.Add("?"); vals.Add(now);
            await ErpDb.ExecuteAsync(connection, tx,
                ErpDb.Positional("INSERT INTO `epc_erp_suppliers` (" + string.Join(", ", cols) + ") VALUES (" + string.Join(", ", marks) + ")"),
                ct, vals.ToArray()).ConfigureAwait(false);
        }
        return true;
    }

    /// <summary>SalesOrderHeadersV2 → epc_erp_sales_orders (upsert by so_no).</summary>
    private static async Task<bool> ApplySalesOrderAsync(DbConnection connection, DbTransaction tx, JsonElement row, long adminId, CancellationToken ct)
    {
        var soNo = Str(row, "SalesOrderNumber", "SalesOrderId", "OrderId");
        if (soNo.Length == 0)
        {
            return false;
        }

        var statusRaw = Str(row, "SalesOrderStatus", "DocumentStatus");
        var status = statusRaw switch
        {
            "Invoiced" => "invoiced",
            "Canceled" or "Cancelled" => "cancelled",
            "Delivered" or "OpenOrder" => "confirmed",
            _ => "draft",
        };
        var title = Str(row, "OrderingCustomerAccountNumber", "InvoiceCustomerAccountNumber", "CustomersOrderingCustomerName");
        var total = Dec(row, "TotalAmount", "OrderTotalAmount", "InvoiceAmount", "SalesOrderBalance");
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        await ErpDb.ExecuteAsync(connection, tx,
            "INSERT INTO `epc_erp_sales_orders` (`so_no`, `title`, `total_amount`, `status`, `admin_id`, `time_created`, `time_updated`) " +
            "VALUES (?, ?, ?, ?, ?, ?, ?) ON DUPLICATE KEY UPDATE `title` = VALUES(`title`), `total_amount` = VALUES(`total_amount`), `status` = VALUES(`status`), `time_updated` = VALUES(`time_updated`)",
            ct, Clip(soNo, 32), Clip(title, 255), total, status, adminId, now, now).ConfigureAwait(false);
        return true;
    }

    /// <summary>PurchaseOrderHeadersV2 → epc_erp_purchase_orders (upsert by po_no).</summary>
    private static async Task<bool> ApplyPurchaseOrderAsync(DbConnection connection, DbTransaction tx, JsonElement row, long adminId, CancellationToken ct)
    {
        var poNo = Str(row, "PurchaseOrderNumber", "PurchaseOrderId", "OrderNumber");
        if (poNo.Length == 0)
        {
            return false;
        }

        var statusRaw = Str(row, "PurchaseOrderStatus", "DocumentStatus");
        var status = statusRaw switch
        {
            "Received" => "received",
            "Canceled" or "Cancelled" => "cancelled",
            "Approved" or "Confirmed" => "approved",
            "ReceivedAndInvoiced" => "received",
            _ => "draft",
        };
        var title = Str(row, "VendorAccountNumber", "OrderVendorAccountNumber", "VendorName");
        var total = Dec(row, "TotalAmount", "OrderTotalAmount", "InvoiceAmount");
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        await ErpDb.ExecuteAsync(connection, tx,
            "INSERT INTO `epc_erp_purchase_orders` (`po_no`, `title`, `total_amount`, `status`, `admin_id`, `time_created`, `time_updated`) " +
            "VALUES (?, ?, ?, ?, ?, ?, ?) ON DUPLICATE KEY UPDATE `title` = VALUES(`title`), `total_amount` = VALUES(`total_amount`), `status` = VALUES(`status`), `time_updated` = VALUES(`time_updated`)",
            ct, Clip(poNo, 32), Clip(title, 255), total, status, adminId, now, now).ConfigureAwait(false);
        return true;
    }

    /// <summary>MainAccounts → epc_erp_coa_accounts (upsert by code).</summary>
    private static async Task<bool> ApplyMainAccountAsync(DbConnection connection, DbTransaction tx, JsonElement row, CancellationToken ct)
    {
        var code = Str(row, "MainAccountId", "MainAccountRecId", "AccountCode", "AccountNum");
        var name = Str(row, "Name", "AccountName", "Description");
        if (code.Length == 0)
        {
            return false;
        }
        if (name.Length == 0)
        {
            name = code;
        }

        var typeRaw = Str(row, "MainAccountType", "Type");
        var (accountType, normalSide) = typeRaw switch
        {
            "Asset" => ("asset", "debit"),
            "Liability" => ("liability", "credit"),
            "Equity" => ("equity", "credit"),
            "Revenue" => ("revenue", "credit"),
            _ => ("expense", "debit"),
        };
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        await ErpDb.ExecuteAsync(connection, tx,
            "INSERT INTO `epc_erp_coa_accounts` (`code`, `name`, `account_type`, `normal_side`, `active`, `time_created`) " +
            "VALUES (?, ?, ?, ?, 1, ?) ON DUPLICATE KEY UPDATE `name` = VALUES(`name`), `account_type` = VALUES(`account_type`), `normal_side` = VALUES(`normal_side`)",
            ct, Clip(code, 16), Clip(name, 255), accountType, normalSide, now).ConfigureAwait(false);
        return true;
    }
}
