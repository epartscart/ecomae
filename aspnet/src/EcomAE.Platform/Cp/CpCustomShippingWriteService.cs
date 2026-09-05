using System.Globalization;
using System.Text.Json;
using EcomAE.Platform.Erp;

namespace EcomAE.Platform.Cp;

/// <summary>
/// Live PHP <c>epc_cs_save_declaration</c> / <c>epc_cs_submit_declaration</c> twins
/// for core declaration + line-item SQL. PDF attach, box autofill, LGP, and
/// schema-ensure stay PHP.
/// </summary>
public interface ICpCustomShippingWriteService
{
    Task<ErpSimpleWriteResult> SaveAsync(
        CpCustomShippingSaveRequest request,
        int userId,
        CancellationToken cancellationToken = default);

    Task<ErpSimpleWriteResult> SubmitAsync(
        long declarationId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CpCustomShippingDeclarationRow>> ListRecentAsync(
        int limit,
        CancellationToken cancellationToken = default);
}

public sealed class CpCustomShippingWriteService : ICpCustomShippingWriteService
{
    public static readonly string[] AllowedCategories =
        ["import", "export", "transit", "temp_admission", "transfer"];

    public static readonly string[] AllowedStatuses = ["draft", "submitted", "cleared"];

    public static readonly IReadOnlyDictionary<string, string[]> TypesByCategory =
        new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            ["import"] =
            [
                "Import to Local from ROW",
                "Import to local from FZ",
                "Import to Local from CW",
                "Import Statistical Declaration",
                "Import for Re Export to Local from ROW",
                "Import for Re Export to Local from FZ",
                "Import for Re Export to Local from CW",
                "Import for CW from ROW",
                "Import to CW from FZ",
                "Import to CW from Local (after temporary admission)",
                "Courier Import",
                "Import to Local After Temporary Admission",
            ],
            ["export"] =
            [
                "Export from Local to ROW",
                "Export from Local to FZ",
                "Export statisitical Declaration",
                "Temporary Export from local to ROW",
                "Temporay Export from local to FZ",
                "Export from CW to ROW",
                "Export from CW to FZ",
                "Re Export to ROW (after import for re export)",
                "Re Export to FZ (after import for Re Export)",
                "Return to FZ after temporary Admission",
                "Return to ROW after Temporary Admission",
                "Courier Export",
                "Goods Consumption within FZ",
            ],
            ["transit"] =
            [
                "Transit (ROW to ROW)",
                "FZ transit in",
                "FZ transit Out",
                "FZ transit in from GCC and other Emirates FZ and GCC local Market",
                "FZ Transit Between Dubai based FZ",
                "Courier Transit",
            ],
            ["temp_admission"] =
            [
                "Temporary Admission from ROW to Local",
                "Temporary Admission from FZ to Local",
                "Temporary Admission from CW to Local",
            ],
            ["transfer"] =
            [
                "Transfer of Cargo by Dubai Based CW",
                "Transfer within a FZ",
            ],
        };

    private static readonly string[] Units = ["PCS", "KG", "SET", "PAIR", "M", "L", "BOX", "CTN"];
    private static readonly string[] VolumeUnits = ["CBM", "CFT", "L"];

    private readonly IErpWriteConnectionFactory _connections;

    public CpCustomShippingWriteService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<ErpSimpleWriteResult> SaveAsync(
        CpCustomShippingSaveRequest request,
        int userId,
        CancellationToken cancellationToken = default)
    {
        request ??= new CpCustomShippingSaveRequest();
        var category = Normalize(request.Category);
        if (category == "lgp")
        {
            return ErpSimpleWriteResult.Fail("invalid", "LGP declarations stay PHP.");
        }

        if (!AllowedCategories.Contains(category, StringComparer.Ordinal))
        {
            category = "import";
        }

        var status = Normalize(request.Status);
        if (!AllowedStatuses.Contains(status, StringComparer.Ordinal))
        {
            status = "draft";
        }

        var declType = Clip(request.DeclarationType, 191);
        var company = Clip(request.Company, 255);
        var emirate = Clip(request.CustomsEmirate, 64);
        if (emirate.Length == 0)
        {
            emirate = "DUBAI";
        }

        var entryDate = Clip(request.EntryDate, 32);
        var declarationDate = Clip(request.DeclarationDate, 32);
        var missing = new List<string>();
        if (company.Length == 0) missing.Add("company");
        if (emirate.Length == 0) missing.Add("customs_emirate");
        if (declType.Length == 0) missing.Add("declaration_type");
        if (entryDate.Length == 0) missing.Add("entry_date");
        if (declarationDate.Length == 0) missing.Add("declaration_date");
        if (missing.Count > 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Required fields missing: " + string.Join(", ", missing));
        }

        if (!TypesByCategory.TryGetValue(category, out var types)
            || !types.Contains(declType, StringComparer.Ordinal))
        {
            return ErpSimpleWriteResult.Fail("invalid", "Invalid declaration type for category");
        }

        var items = request.Items is { Count: > 0 } ? request.Items : ParseItemsJson(request.ItemsJson);
        var lines = NormalizeItems(items);
        var lineError = ValidateItems(lines);
        if (lineError is not null)
        {
            return ErpSimpleWriteResult.Fail("invalid", lineError);
        }

        var declNo = Clip(request.DeclarationNumber, 64);
        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        if (declNo.Length > 0)
        {
            var existing = await ErpDb.LongAsync(
                connection,
                null,
                ErpDb.Positional("SELECT `id` FROM `epc_custom_shipping_declarations` WHERE `declaration_number` = ? LIMIT 1"),
                cancellationToken,
                declNo);
            if (existing > 0 && existing != request.Id)
            {
                return request.Id <= 0
                    ? ErpSimpleWriteResult.Fail("invalid", "Declaration already saved — open from Reports to edit")
                    : ErpSimpleWriteResult.Fail(
                        "invalid",
                        "Declaration number " + declNo + " already exists (record #" + existing.ToString(CultureInfo.InvariantCulture) + "). Each customs declaration copy must be unique.");
            }
        }

        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        object? entry = string.IsNullOrWhiteSpace(entryDate) ? null : entryDate;
        object? declDate = string.IsNullOrWhiteSpace(declarationDate) ? null : declarationDate;
        object? blDate = string.IsNullOrWhiteSpace(request.BlDate) ? null : Clip(request.BlDate, 32);
        var currency = Clip(request.Currency, 16);
        if (currency.Length == 0)
        {
            currency = "AED";
        }

        long id = request.Id;
        if (id > 0)
        {
            var exists = await ErpDb.LongAsync(
                connection,
                null,
                ErpDb.Positional("SELECT `id` FROM `epc_custom_shipping_declarations` WHERE `id` = ?"),
                cancellationToken,
                id);
            if (exists <= 0)
            {
                return ErpSimpleWriteResult.Fail("invalid", "Declaration not found");
            }

            await ErpDb.ExecuteAsync(
                connection,
                null,
                ErpDb.Positional(
                    """
                    UPDATE `epc_custom_shipping_declarations` SET
                     `category`=?, `declaration_type`=?, `status`=?, `company`=?, `customs_emirate`=?,
                     `entry_date`=?, `declaration_date`=?, `declaration_number`=?, `bl_number`=?, `bl_date`=?,
                     `srv_number`=?, `lc_dc_number`=?, `ld_po_number`=?, `supplier_detail`=?, `currency`=?,
                     `invoice_amount_aed`=?, `total_cost_aed`=?, `remarks`=?, `field_data`=?, `updated_at`=?
                    WHERE `id`=?
                    """),
                cancellationToken,
                category, declType, status, company, emirate, entry, declDate, declNo,
                Clip(request.BlNumber, 64), blDate, Clip(request.SrvNumber, 64),
                Clip(request.LcDcNumber, 128), Clip(request.LdPoNumber, 128),
                Clip(request.SupplierDetail, 255), currency, request.InvoiceAmountAed,
                request.TotalCostAed, request.Remarks ?? "", "{}", now, id);
        }
        else
        {
            await ErpDb.ExecuteAsync(
                connection,
                null,
                ErpDb.Positional(
                    """
                    INSERT INTO `epc_custom_shipping_declarations`
                    (`category`, `declaration_type`, `status`, `company`, `customs_emirate`, `entry_date`, `declaration_date`,
                     `declaration_number`, `bl_number`, `bl_date`, `srv_number`, `lc_dc_number`, `ld_po_number`,
                     `supplier_detail`, `currency`, `invoice_amount_aed`, `total_cost_aed`, `remarks`, `field_data`,
                     `created_at`, `updated_at`, `created_by`)
                    VALUES (?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?)
                    """),
                cancellationToken,
                category, declType, status, company, emirate, entry, declDate, declNo,
                Clip(request.BlNumber, 64), blDate, Clip(request.SrvNumber, 64),
                Clip(request.LcDcNumber, 128), Clip(request.LdPoNumber, 128),
                Clip(request.SupplierDetail, 255), currency, request.InvoiceAmountAed,
                request.TotalCostAed, request.Remarks ?? "", "{}", now, now, userId < 0 ? 0 : userId);
            id = await ErpDb.LastInsertIdAsync(connection, null, cancellationToken).ConfigureAwait(false);
        }

        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional("DELETE FROM `epc_custom_shipping_declaration_items` WHERE `declaration_id` = ?"),
            cancellationToken,
            id);
        var lineNo = 0;
        foreach (var line in lines)
        {
            lineNo++;
            await ErpDb.ExecuteAsync(
                connection,
                null,
                ErpDb.Positional(
                    """
                    INSERT INTO `epc_custom_shipping_declaration_items`
                    (`declaration_id`, `line_number`, `hs_code`, `country_of_origin`, `description`,
                     `quantity`, `unit`, `volume`, `volume_unit`, `amount`, `weight`)
                    VALUES (?,?,?,?,?,?,?,?,?,?,?)
                    """),
                cancellationToken,
                id, lineNo, line.HsCode, line.CountryOfOrigin, line.Description,
                line.Quantity, line.Unit, line.Volume, line.VolumeUnit, line.Amount, line.Weight);
        }

        return ErpSimpleWriteResult.Ok("Declaration saved.", id);
    }

    public async Task<ErpSimpleWriteResult> SubmitAsync(long declarationId, CancellationToken cancellationToken = default)
    {
        if (declarationId <= 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "A declaration id is required.");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        var exists = await ErpDb.LongAsync(
            connection,
            null,
            ErpDb.Positional("SELECT `id` FROM `epc_custom_shipping_declarations` WHERE `id` = ?"),
            cancellationToken,
            declarationId);
        if (exists <= 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Declaration not found");
        }

        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional("UPDATE `epc_custom_shipping_declarations` SET `status` = ?, `updated_at` = ? WHERE `id` = ?"),
            cancellationToken,
            "submitted", now, declarationId);
        return ErpSimpleWriteResult.Ok("Declaration submitted.", declarationId);
    }

    public async Task<IReadOnlyList<CpCustomShippingDeclarationRow>> ListRecentAsync(
        int limit,
        CancellationToken cancellationToken = default)
    {
        if (!_connections.IsConfigured)
        {
            return [];
        }

        var take = limit < 1 ? 50 : Math.Min(limit, 200);
        try
        {
            await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
            await using var command = connection.CreateCommand();
            command.CommandText = ErpDb.Positional(
                """
                SELECT d.`id`, d.`category`, d.`declaration_type`, d.`company`, d.`entry_date`,
                       d.`status`, d.`declaration_number`,
                       (SELECT COUNT(*) FROM `epc_custom_shipping_declaration_items` i WHERE i.`declaration_id` = d.`id`) AS item_count
                FROM `epc_custom_shipping_declarations` d
                ORDER BY d.`id` DESC
                LIMIT ?
                """);
            ErpDb.AddParameters(command, take);
            var rows = new List<CpCustomShippingDeclarationRow>();
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                rows.Add(new CpCustomShippingDeclarationRow(
                    reader.IsDBNull(0) ? 0 : Convert.ToInt64(reader.GetValue(0), CultureInfo.InvariantCulture),
                    reader.IsDBNull(1) ? "" : reader.GetValue(1)?.ToString() ?? "",
                    reader.IsDBNull(2) ? "" : reader.GetValue(2)?.ToString() ?? "",
                    reader.IsDBNull(3) ? "" : reader.GetValue(3)?.ToString() ?? "",
                    FormatDate(reader.IsDBNull(4) ? null : reader.GetValue(4)),
                    reader.IsDBNull(5) ? "" : reader.GetValue(5)?.ToString() ?? "",
                    reader.IsDBNull(6) ? "" : reader.GetValue(6)?.ToString() ?? "",
                    reader.IsDBNull(7) ? 0 : Convert.ToInt32(reader.GetValue(7), CultureInfo.InvariantCulture)));
            }

            return rows;
        }
        catch (System.Data.Common.DbException)
        {
            return [];
        }
    }

    public static IReadOnlyList<CpCustomShippingLineInput> ParseItemsJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Array)
            {
                return [];
            }

            var list = new List<CpCustomShippingLineInput>();
            foreach (var el in doc.RootElement.EnumerateArray())
            {
                if (el.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                list.Add(new CpCustomShippingLineInput(
                    ReadString(el, "hs_code", "hsCode"),
                    ReadString(el, "country_of_origin", "countryOfOrigin", "origin"),
                    ReadString(el, "description"),
                    ReadDecimal(el, 0, "quantity", "qty"),
                    ReadString(el, "unit"),
                    ReadDecimal(el, 0, "volume"),
                    ReadString(el, "volume_unit", "volumeUnit"),
                    ReadDecimal(el, 0, "amount"),
                    ReadDecimal(el, 0, "weight")));
            }

            return list;
        }
        catch (JsonException)
        {
            return [];
        }
    }

    public static IReadOnlyList<CpCustomShippingLineInput> NormalizeItems(IReadOnlyList<CpCustomShippingLineInput>? raw)
    {
        var list = new List<CpCustomShippingLineInput>();
        if (raw is null)
        {
            return list;
        }

        foreach (var row in raw)
        {
            var hs = Clip(row.HsCode, 32);
            var origin = Clip(row.CountryOfOrigin, 64);
            var desc = Clip(row.Description, 512);
            if (hs.Length == 0 && origin.Length == 0 && desc.Length == 0
                && row.Quantity <= 0 && row.Volume <= 0 && row.Amount <= 0)
            {
                continue;
            }

            var unit = Clip(row.Unit, 16).ToUpperInvariant();
            if (!Units.Contains(unit, StringComparer.Ordinal))
            {
                unit = "PCS";
            }

            var volUnit = Clip(row.VolumeUnit, 16).ToUpperInvariant();
            if (!VolumeUnits.Contains(volUnit, StringComparer.Ordinal))
            {
                volUnit = "CBM";
            }

            list.Add(row with
            {
                HsCode = hs,
                CountryOfOrigin = origin,
                Description = desc,
                Quantity = row.Quantity,
                Unit = unit,
                VolumeUnit = volUnit,
            });
        }

        return list;
    }

    public static string? ValidateItems(IReadOnlyList<CpCustomShippingLineInput> items)
    {
        if (items.Count == 0)
        {
            return "Add at least one declaration line item (HS code, country of origin, quantity).";
        }

        var errors = new List<string>();
        var n = 0;
        foreach (var item in items)
        {
            n++;
            if (string.IsNullOrWhiteSpace(item.HsCode))
            {
                errors.Add("Line " + n.ToString(CultureInfo.InvariantCulture) + ": HS code is required");
            }

            if (string.IsNullOrWhiteSpace(item.CountryOfOrigin))
            {
                errors.Add("Line " + n.ToString(CultureInfo.InvariantCulture) + ": country of origin is required");
            }

            if (item.Quantity <= 0)
            {
                errors.Add("Line " + n.ToString(CultureInfo.InvariantCulture) + ": quantity must be greater than zero");
            }
        }

        return errors.Count == 0 ? null : string.Join("; ", errors);
    }

    private static string ReadString(JsonElement el, params string[] names)
    {
        foreach (var name in names)
        {
            if (el.TryGetProperty(name, out var prop) && prop.ValueKind == JsonValueKind.String)
            {
                return prop.GetString() ?? "";
            }
        }

        return "";
    }

    private static decimal ReadDecimal(JsonElement el, decimal fallback, params string[] names)
    {
        foreach (var name in names)
        {
            if (!el.TryGetProperty(name, out var prop))
            {
                continue;
            }

            if (prop.ValueKind == JsonValueKind.Number && prop.TryGetDecimal(out var n))
            {
                return n;
            }

            if (prop.ValueKind == JsonValueKind.String
                && decimal.TryParse(prop.GetString(), NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed))
            {
                return parsed;
            }
        }

        return fallback;
    }

    private static string FormatDate(object? value)
    {
        if (value is null or DBNull)
        {
            return "";
        }

        if (value is DateTime dt)
        {
            return dt.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        }

        var text = Convert.ToString(value, CultureInfo.InvariantCulture) ?? "";
        return text.Length >= 10 ? text[..10] : text;
    }

    private static string Normalize(string? value)
        => (value ?? string.Empty).Trim().ToLowerInvariant();

    private static string Clip(string? value, int max)
    {
        var text = (value ?? string.Empty).Trim();
        return text.Length <= max ? text : text[..max];
    }
}

public sealed record CpCustomShippingSaveRequest(
    long Id = 0,
    string? Category = null,
    string? DeclarationType = null,
    string? Status = null,
    string? Company = null,
    string? CustomsEmirate = null,
    string? EntryDate = null,
    string? DeclarationDate = null,
    string? DeclarationNumber = null,
    string? BlNumber = null,
    string? BlDate = null,
    string? SrvNumber = null,
    string? LcDcNumber = null,
    string? LdPoNumber = null,
    string? SupplierDetail = null,
    string? Currency = null,
    decimal InvoiceAmountAed = 0,
    decimal TotalCostAed = 0,
    string? Remarks = null,
    string? ItemsJson = null,
    IReadOnlyList<CpCustomShippingLineInput>? Items = null);

public sealed record CpCustomShippingDeclarationRow(
    long Id,
    string Category,
    string DeclarationType,
    string Company,
    string EntryDate,
    string Status,
    string DeclarationNumber,
    int ItemCount);

public sealed record CpCustomShippingLineInput(
    string? HsCode = null,
    string? CountryOfOrigin = null,
    string? Description = null,
    decimal Quantity = 0,
    string? Unit = null,
    decimal Volume = 0,
    string? VolumeUnit = null,
    decimal Amount = 0,
    decimal Weight = 0);
