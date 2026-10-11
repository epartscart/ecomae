using System.Data.Common;
using System.Globalization;
using System.Text;
using System.Text.Json;

namespace EcomAE.Platform.Erp;

/// <summary>
/// Live PHP <c>custom_shipping_reports.php</c> twin (epc_custom_shipping.php Phase 2):
/// five reports over <c>epc_custom_shipping_declarations</c> — declaration search,
/// cost summary, duty lines, re-export tracking, document expiry — with CSV export.
/// Reuses <see cref="IErpCsDeclarationsReadService"/> so the PHP schema ensure and
/// field_data decoding are shared.
/// </summary>
public interface IErpCsReportsReadService
{
    Task<ErpCsDeclarationsResult> SearchResultsAsync(ErpCsDeclarationsFilters filters, CancellationToken cancellationToken = default);
    Task<ErpCsCostSummaryResult?> CostSummaryAsync(ErpCsDeclarationsFilters filters, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Dictionary<string, object?>>?> DutyLinesAsync(ErpCsDeclarationsFilters filters, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ErpCsReexportRow>?> ReexportTrackingAsync(ErpCsDeclarationsFilters filters, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ErpCsExpiryRow>?> DocumentExpiryAsync(ErpCsDeclarationsFilters filters, CancellationToken cancellationToken = default);
    string Csv(string reportKey, ErpCsCostSummaryResult? cost, IReadOnlyList<Dictionary<string, object?>>? rows, IReadOnlyList<ErpCsReexportRow>? reexport, IReadOnlyList<ErpCsExpiryRow>? expiry, ErpCsDeclarationsResult? search);
}

public sealed record ErpCsCostSummaryRow(
    string Category,
    string Company,
    string CustomsEmirate,
    long DeclCount,
    decimal SumInvoiceAed,
    decimal SumTotalCostAed,
    decimal SumLineAmountAed,
    decimal SumLineQty);

public sealed record ErpCsCostSummaryResult(
    IReadOnlyList<ErpCsCostSummaryRow> Rows,
    long DeclCount,
    decimal SumInvoiceAed,
    decimal SumTotalCostAed,
    decimal SumLineAmountAed);

public sealed record ErpCsReexportRow(
    long Id,
    string DeclarationType,
    string Category,
    string Company,
    string? EntryDate,
    string DeclarationNumber,
    string Status,
    string ImportRef,
    long? ImportLinkId,
    bool IsReexport,
    bool IsImportForReexport,
    string DocumentExpiryDate);

public sealed record ErpCsExpiryRow(
    long Id,
    string Category,
    string DeclarationType,
    string Company,
    string DeclarationNumber,
    string Status,
    string DocumentExpiryDate,
    int DaysUntilExpiry,
    string ExpiryStatus);

public sealed class ErpCsReportsReadService : IErpCsReportsReadService
{
    private static readonly string[] ImportForReexportTypes =
    {
        "Import for Re Export to Local from ROW",
        "Import for Re Export to Local from FZ",
        "Import for Re Export to Local from CW",
    };
    private static readonly string[] ReexportTypes =
    {
        "Re Export to ROW (after import for re export)",
        "Re Export to FZ (after import for Re Export)",
    };

    private readonly IErpCsDeclarationsReadService _declarations;
    private readonly IErpWriteConnectionFactory _connections;

    public ErpCsReportsReadService(IErpCsDeclarationsReadService declarations, IErpWriteConnectionFactory connections)
    {
        _declarations = declarations;
        _connections = connections;
    }

    public Task<ErpCsDeclarationsResult> SearchResultsAsync(ErpCsDeclarationsFilters filters, CancellationToken cancellationToken = default)
        => _declarations.ListAsync(filters with { Limit = 500 }, cancellationToken);

    public async Task<ErpCsCostSummaryResult?> CostSummaryAsync(ErpCsDeclarationsFilters filters, CancellationToken cancellationToken = default)
    {
        // Ensure schema via the declarations read, then run the PHP aggregate.
        var ensured = await _declarations.ListAsync(filters with { Limit = 1 }, cancellationToken).ConfigureAwait(false);
        if (!ensured.Ok || !_connections.IsConfigured)
        {
            return null;
        }
        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);

        var sql = "SELECT d.`category`, d.`company`, d.`customs_emirate`,"
            + " COUNT(DISTINCT d.`id`) AS decl_count,"
            + " COALESCE(SUM(d.`invoice_amount_aed`),0) AS sum_invoice_aed,"
            + " COALESCE(SUM(d.`total_cost_aed`),0) AS sum_total_cost_aed,"
            + " COALESCE(SUM(i.`amount`),0) AS sum_line_amount_aed,"
            + " COALESCE(SUM(i.`quantity`),0) AS sum_line_qty"
            + " FROM `epc_custom_shipping_declarations` d"
            + " LEFT JOIN `epc_custom_shipping_declaration_items` i ON i.`declaration_id` = d.`id`"
            + " WHERE 1=1";
        var args = new List<object?>();
        if (filters.Category.Length > 0) { sql += " AND d.`category` = ?"; args.Add(filters.Category); }
        if (filters.Status.Length > 0) { sql += " AND d.`status` = ?"; args.Add(filters.Status); }
        if (filters.From.Length > 0) { sql += " AND (d.`entry_date` IS NULL OR d.`entry_date` >= ?)"; args.Add(filters.From); }
        if (filters.To.Length > 0) { sql += " AND (d.`entry_date` IS NULL OR d.`entry_date` <= ?)"; args.Add(filters.To); }
        if (filters.Company.Length > 0) { sql += " AND d.`company` LIKE ?"; args.Add("%" + filters.Company + "%"); }
        if (filters.CustomsEmirate.Length > 0) { sql += " AND d.`customs_emirate` = ?"; args.Add(filters.CustomsEmirate); }
        sql += " GROUP BY d.`category`, d.`company`, d.`customs_emirate` ORDER BY sum_invoice_aed DESC, d.`company` ASC";

        var rows = new List<ErpCsCostSummaryRow>();
        await using var command = connection.CreateCommand();
        command.CommandText = ErpDb.Positional(sql);
        ErpDb.AddParameters(command, args.ToArray());
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        long declCount = 0;
        decimal sumInvoice = 0, sumTotal = 0, sumLine = 0;
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            var row = new ErpCsCostSummaryRow(
                Convert.ToString(reader.GetValue(0), CultureInfo.InvariantCulture) ?? "",
                Convert.ToString(reader.GetValue(1), CultureInfo.InvariantCulture) ?? "",
                Convert.ToString(reader.GetValue(2), CultureInfo.InvariantCulture) ?? "",
                Convert.ToInt64(reader.GetValue(3)),
                Convert.ToDecimal(reader.GetValue(4)),
                Convert.ToDecimal(reader.GetValue(5)),
                Convert.ToDecimal(reader.GetValue(6)),
                Convert.ToDecimal(reader.GetValue(7)));
            declCount += row.DeclCount;
            sumInvoice += row.SumInvoiceAed;
            sumTotal += row.SumTotalCostAed;
            sumLine += row.SumLineAmountAed;
            rows.Add(row);
        }
        return new ErpCsCostSummaryResult(rows, declCount, sumInvoice, sumTotal, sumLine);
    }

    public async Task<IReadOnlyList<Dictionary<string, object?>>?> DutyLinesAsync(ErpCsDeclarationsFilters filters, CancellationToken cancellationToken = default)
    {
        var ensured = await _declarations.ListAsync(filters with { Limit = 1 }, cancellationToken).ConfigureAwait(false);
        if (!ensured.Ok || !_connections.IsConfigured)
        {
            return null;
        }
        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);

        var sql = "SELECT d.*, i.`line_number`, i.`hs_code`, i.`country_of_origin`, i.`description` AS item_description,"
            + " i.`quantity`, i.`amount` AS line_amount, i.`total_duty_aed`, i.`duty_rate`"
            + " FROM `epc_custom_shipping_declarations` d"
            + " INNER JOIN `epc_custom_shipping_declaration_items` i ON i.`declaration_id` = d.`id`"
            + " WHERE 1=1";
        var args = new List<object?>();
        if (filters.Category.Length > 0) { sql += " AND d.`category` = ?"; args.Add(filters.Category); }
        if (filters.Status.Length > 0) { sql += " AND d.`status` = ?"; args.Add(filters.Status); }
        if (filters.From.Length > 0) { sql += " AND (d.`entry_date` IS NULL OR d.`entry_date` >= ?)"; args.Add(filters.From); }
        if (filters.To.Length > 0) { sql += " AND (d.`entry_date` IS NULL OR d.`entry_date` <= ?)"; args.Add(filters.To); }
        if (filters.Company.Length > 0) { sql += " AND d.`company` LIKE ?"; args.Add("%" + filters.Company + "%"); }
        sql += " ORDER BY d.`entry_date` DESC, d.`id` DESC, i.`line_number` ASC LIMIT 500";

        var rows = new List<Dictionary<string, object?>>();
        await using var command = connection.CreateCommand();
        command.CommandText = ErpDb.Positional(sql);
        ErpDb.AddParameters(command, args.ToArray());
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            var row = new Dictionary<string, object?>(StringComparer.Ordinal);
            for (var i = 0; i < reader.FieldCount; i++)
            {
                var v = reader.GetValue(i);
                row[reader.GetName(i)] = v is DBNull ? null : v;
            }
            rows.Add(row);
        }
        return rows;
    }

    public async Task<IReadOnlyList<ErpCsReexportRow>?> ReexportTrackingAsync(ErpCsDeclarationsFilters filters, CancellationToken cancellationToken = default)
    {
        var ensured = await _declarations.ListAsync(filters with { Limit = 1 }, cancellationToken).ConfigureAwait(false);
        if (!ensured.Ok || !_connections.IsConfigured)
        {
            return null;
        }
        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);

        var allTypes = ImportForReexportTypes.Concat(ReexportTypes).ToArray();
        var placeholders = string.Join(",", Enumerable.Range(0, allTypes.Length).Select(i => "@t" + i));
        var sql = "SELECT `id`,`declaration_type`,`category`,`company`,`entry_date`,`declaration_number`,`status`,CAST(`field_data` AS CHAR) AS `field_data`"
            + " FROM `epc_custom_shipping_declarations` WHERE `declaration_type` IN (" + placeholders + ")";
        var args = allTypes.Cast<object?>().ToList();
        if (filters.From.Length > 0) { sql += " AND (`entry_date` IS NULL OR `entry_date` >= ?)"; args.Add(filters.From); }
        if (filters.To.Length > 0) { sql += " AND (`entry_date` IS NULL OR `entry_date` <= ?)"; args.Add(filters.To); }
        if (filters.Company.Length > 0) { sql += " AND `company` LIKE ?"; args.Add("%" + filters.Company + "%"); }
        if (filters.Status.Length > 0) { sql += " AND `status` = ?"; args.Add(filters.Status); }
        sql += " ORDER BY `entry_date` DESC, `id` DESC LIMIT 300";

        var rows = new List<(long Id, string Type, string Category, string Company, string? Entry, string DeclNo, string Status, string ImportRef, string Expiry)>();
        var importRefs = new List<string>();
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = ErpDb.Positional(sql);
            ErpDb.AddParameters(command, args.ToArray());
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                var fd = FieldDataVal(reader.GetValue(7)?.ToString(), "import_reexport_declaration_ref");
                var expiry = FieldDataVal(reader.GetValue(7)?.ToString(), "document_expiry_date");
                if (fd.Length > 0)
                {
                    importRefs.Add(fd);
                }
                rows.Add((
                    Convert.ToInt64(reader.GetValue(0)),
                    Convert.ToString(reader.GetValue(1), CultureInfo.InvariantCulture) ?? "",
                    Convert.ToString(reader.GetValue(2), CultureInfo.InvariantCulture) ?? "",
                    Convert.ToString(reader.GetValue(3), CultureInfo.InvariantCulture) ?? "",
                    reader.GetValue(4) is DBNull ? null : Convert.ToString(reader.GetValue(4), CultureInfo.InvariantCulture),
                    Convert.ToString(reader.GetValue(5), CultureInfo.InvariantCulture) ?? "",
                    Convert.ToString(reader.GetValue(6), CultureInfo.InvariantCulture) ?? "",
                    fd,
                    expiry));
            }
        }

        // epc_cs_find_declaration_by_number for each import ref.
        var links = new Dictionary<string, long>(StringComparer.Ordinal);
        foreach (var refNo in importRefs.Distinct(StringComparer.Ordinal))
        {
            var id = await ErpDb.LongAsync(
                connection,
                null,
                ErpDb.Positional("SELECT `id` FROM `epc_custom_shipping_declarations` WHERE `declaration_number` = ? ORDER BY `id` DESC LIMIT 1"),
                cancellationToken,
                refNo).ConfigureAwait(false);
            if (id > 0)
            {
                links[refNo] = id;
            }
        }

        return rows.Select(r => new ErpCsReexportRow(
            r.Id, r.Type, r.Category, r.Company, r.Entry, r.DeclNo, r.Status,
            r.ImportRef,
            r.ImportRef.Length > 0 && links.TryGetValue(r.ImportRef, out var linkId) ? linkId : null,
            ReexportTypes.Contains(r.Type),
            ImportForReexportTypes.Contains(r.Type),
            r.Expiry)).ToList();
    }

    public async Task<IReadOnlyList<ErpCsExpiryRow>?> DocumentExpiryAsync(ErpCsDeclarationsFilters filters, CancellationToken cancellationToken = default)
    {
        var baseRows = await _declarations.ListAsync(filters with { Limit = 600 }, cancellationToken).ConfigureAwait(false);
        if (!baseRows.Ok)
        {
            return null;
        }
        var days = Math.Max(1, filters.ExpiryDays);
        var today = DateTime.UtcNow.Date;
        var output = new List<ErpCsExpiryRow>();
        foreach (var row in baseRows.Declarations)
        {
            var expiry = FieldDataVal(FieldDataRaw(row), "document_expiry_date");
            if (expiry.Length == 0)
            {
                continue;
            }
            if (!DateTime.TryParse(expiry, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var expDate))
            {
                continue;
            }
            var delta = (int)Math.Floor((expDate.Date - today).TotalDays);
            var status = delta < 0 ? "overdue" : delta <= days ? "upcoming" : null;
            if (status is null)
            {
                continue;
            }
            output.Add(new ErpCsExpiryRow(
                row.TryGetValue("id", out var id) ? Convert.ToInt64(id) : 0,
                Str(row, "category"),
                Str(row, "declaration_type"),
                Str(row, "company"),
                Str(row, "declaration_number"),
                Str(row, "status"),
                expiry,
                delta,
                status));
            if (output.Count >= 300)
            {
                break;
            }
        }
        output.Sort((a, b) => string.CompareOrdinal(a.DocumentExpiryDate, b.DocumentExpiryDate));
        return output;
    }

    private static string? FieldDataRaw(Dictionary<string, object?> row)
        => row.TryGetValue("field_data", out var fd) && fd is JsonElement el && el.ValueKind == JsonValueKind.Object
            ? el.GetRawText()
            : fd?.ToString();

    private static string FieldDataVal(string? fieldDataJson, string key)
    {
        if (string.IsNullOrWhiteSpace(fieldDataJson))
        {
            return "";
        }
        try
        {
            using var doc = JsonDocument.Parse(fieldDataJson);
            if (doc.RootElement.ValueKind == JsonValueKind.Object
                && doc.RootElement.TryGetProperty(key, out var v)
                && v.ValueKind != JsonValueKind.Null)
            {
                return v.ToString().Trim();
            }
        }
        catch (JsonException)
        {
        }
        return "";
    }

    private static string Str(Dictionary<string, object?> row, string key)
        => row.TryGetValue(key, out var v) ? Convert.ToString(v, CultureInfo.InvariantCulture) ?? "" : "";

    public string Csv(string reportKey, ErpCsCostSummaryResult? cost, IReadOnlyList<Dictionary<string, object?>>? rows, IReadOnlyList<ErpCsReexportRow>? reexport, IReadOnlyList<ErpCsExpiryRow>? expiry, ErpCsDeclarationsResult? search)
    {
        var sb = new StringBuilder();
        static string C(object? v)
        {
            var s = Convert.ToString(v, CultureInfo.InvariantCulture) ?? "";
            return s.Contains('"') || s.Contains(',') || s.Contains('\n')
                ? "\"" + s.Replace("\"", "\"\"") + "\""
                : s;
        }
        static void Line(StringBuilder b, params object?[] cells)
            => b.AppendLine(string.Join(",", cells.Select(C)));

        switch (reportKey)
        {
            case "cost_summary":
                Line(sb, "Category", "Company", "Emirate", "Decls", "Invoice AED", "Total cost AED", "Line amount AED");
                foreach (var r in cost?.Rows ?? [])
                {
                    Line(sb, r.Category, r.Company, r.CustomsEmirate, r.DeclCount, r.SumInvoiceAed, r.SumTotalCostAed, r.SumLineAmountAed);
                }
                break;
            case "duty_report":
                Line(sb, "ID", "Type", "Company", "Decl #", "HS code", "Origin", "Item", "Qty", "Line AED", "Duty rate", "Duty AED");
                foreach (var r in rows ?? [])
                {
                    Line(sb, Str(r, "id"), Str(r, "declaration_type"), Str(r, "company"), Str(r, "declaration_number"),
                        Str(r, "hs_code"), Str(r, "country_of_origin"), Str(r, "item_description"),
                        Str(r, "quantity"), Str(r, "line_amount"), Str(r, "duty_rate"), Str(r, "total_duty_aed"));
                }
                break;
            case "reexport_tracking":
                Line(sb, "ID", "Type", "Company", "Entry date", "Decl #", "Status", "Import ref", "Linked import id");
                foreach (var r in reexport ?? [])
                {
                    Line(sb, r.Id, r.DeclarationType, r.Company, r.EntryDate, r.DeclarationNumber, r.Status, r.ImportRef, r.ImportLinkId);
                }
                break;
            case "document_expiry":
                Line(sb, "ID", "Category", "Type", "Company", "Decl #", "Status", "Expiry date", "Days", "Expiry status");
                foreach (var r in expiry ?? [])
                {
                    Line(sb, r.Id, r.Category, r.DeclarationType, r.Company, r.DeclarationNumber, r.Status, r.DocumentExpiryDate, r.DaysUntilExpiry, r.ExpiryStatus);
                }
                break;
            default:
                Line(sb, "ID", "Category", "Type", "Company", "Emirate", "Entry date", "Decl #", "SRV #", "Supplier", "Status", "Invoice AED", "Total cost AED", "Items");
                foreach (var r in search?.Declarations ?? [])
                {
                    Line(sb, Str(r, "id"), Str(r, "category"), Str(r, "declaration_type"), Str(r, "company"), Str(r, "customs_emirate"),
                        Str(r, "entry_date"), Str(r, "declaration_number"), Str(r, "srv_number"), Str(r, "supplier_detail"),
                        Str(r, "status"), Str(r, "invoice_amount_aed"), Str(r, "total_cost_aed"), Str(r, "item_count"));
                }
                break;
        }
        return sb.ToString();
    }
}
