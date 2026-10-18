using System.Data.Common;
using System.Globalization;

namespace EcomAE.Platform.Erp;

/// <summary>
/// Tourist VAT refund tag, Planet validation, and the retailer's Planet invoice.
/// The tag table matches <c>epc_tourist_refund_create</c>. Planet columns are added beside it.
/// </summary>
public interface IErpTouristRefundWriteService
{
    Task<ErpSimpleWriteResult> CreateAsync(
        ErpTouristRefundCreateRequest request,
        CancellationToken cancellationToken = default);

    Task<ErpSimpleWriteResult> ValidateAsync(
        ErpTouristRefundValidateRequest request,
        CancellationToken cancellationToken = default);

    Task<ErpSimpleWriteResult> RecordPlanetInvoiceAsync(
        ErpTouristRefundPlanetInvoiceRequest request,
        CancellationToken cancellationToken = default);

    Task<ErpSimpleWriteResult> PayPlanetInvoiceAsync(
        ErpTouristRefundPlanetPayRequest request,
        CancellationToken cancellationToken = default);

    Task<ErpTouristRefundPosition> PositionAsync(CancellationToken cancellationToken = default);
}

public sealed record ErpTouristRefundValidateRequest(string? Barcode = null);

public sealed record ErpTouristRefundCreateRequest(
    int CompanyId = 0,
    long InvoiceId = 0,
    string? InvoiceNo = null,
    string? TouristName = null,
    string? PassportNo = null,
    string? Nationality = null,
    string? DepartureDate = null,
    decimal TotalAmount = 0,
    decimal VatAmount = 0,
    decimal RefundPct = 87,
    string? RefundProvider = null,
    string? SupplyKind = null,
    string? RefundMethod = null,
    string? SupplyDate = null);

public sealed record ErpTouristRefundPlanetInvoiceRequest(
    int CompanyId = 0,
    string? InvoiceNo = null,
    string? InvoiceDate = null,
    string? Barcodes = null);

public sealed record ErpTouristRefundPlanetPayRequest(
    string? InvoiceNo = null,
    string? PayDate = null,
    string? PayFrom = null);

public sealed record ErpTouristRefundTagRow(
    long Id,
    string InvoiceNo,
    string Tourist,
    string Barcode,
    string Status,
    decimal Total,
    decimal Vat,
    decimal Refund,
    string PlanetInvoice);

public sealed record ErpTouristRefundPosition(
    decimal ValidatedExclusive,
    decimal ValidatedVat,
    decimal PendingVat,
    decimal OpenPayable,
    IReadOnlyList<ErpTouristRefundTagRow> Tags)
{
    public static ErpTouristRefundPosition Empty { get; } = new(0, 0, 0, 0, []);
}

public sealed class ErpTouristRefundWriteService : IErpTouristRefundWriteService
{
    private readonly IErpWriteConnectionFactory _connections;
    private readonly IErpGlPostingService? _journals;

    public ErpTouristRefundWriteService(IErpWriteConnectionFactory connections, IErpGlPostingService? journals = null)
    {
        _connections = connections;
        _journals = journals;
    }

    public async Task<ErpSimpleWriteResult> CreateAsync(
        ErpTouristRefundCreateRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var tourist = Clip((request.TouristName ?? string.Empty).Trim(), 200);
        if (tourist.Length == 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Tourist name is required.");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await EnsureSchemaAsync(connection, cancellationToken).ConfigureAwait(false);
        if (!await TableExistsAsync(connection, "epc_tourist_refund_invoices", cancellationToken).ConfigureAwait(false)
            || !await ColumnExistsAsync(connection, "epc_tourist_refund_invoices", "tourist_name", cancellationToken).ConfigureAwait(false)
            || !await ColumnExistsAsync(connection, "epc_tourist_refund_invoices", "barcode", cancellationToken).ConfigureAwait(false))
        {
            return ErpSimpleWriteResult.Fail("invalid", "Tourist refund tables are not provisioned");
        }

        var total = Round2(request.TotalAmount < 0 ? 0 : request.TotalAmount);
        var vat = Round2(request.VatAmount < 0 ? 0 : request.VatAmount);
        if (vat <= 0 && total > 0)
        {
            vat = Round2(total - (total / 1.05m));
        }

        var supply = ParseDate(request.SupplyDate) ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var departure = ParseDate(request.DepartureDate);
        var method = NormalizeMethod(request.RefundMethod);
        var cashAlready = 0m;
        if (method == "cash"
            && await ColumnExistsAsync(connection, "epc_tourist_refund_invoices", "refund_method", cancellationToken).ConfigureAwait(false))
        {
            var since = DateTimeOffset.UtcNow.ToUnixTimeSeconds() - (24L * 60L * 60L);
            cashAlready = await ErpDb.DecimalAsync(
                connection,
                null,
                ErpDb.Positional(
                    "SELECT COALESCE(SUM(`refund_amount`), 0) FROM `epc_tourist_refund_invoices` WHERE `passport_no` = ? AND `refund_method` = 'cash' AND `time_created` >= ? AND `refund_status` <> 'rejected'"),
                cancellationToken,
                Clip((request.PassportNo ?? string.Empty).Trim(), 50),
                since).ConfigureAwait(false);
        }

        var refusal = ErpTouristRefundLaw.Refusal(new ErpTouristRefundClaim(
            request.PassportNo,
            request.Nationality,
            total,
            vat,
            string.IsNullOrWhiteSpace(request.SupplyKind) ? "goods" : request.SupplyKind,
            method,
            supply,
            departure,
            cashAlready));
        if (refusal is not null)
        {
            return ErpSimpleWriteResult.Fail("invalid", refusal);
        }

        var quote = ErpTouristRefundLaw.Calculate(vat);
        var companyId = request.CompanyId < 0 ? 0 : request.CompanyId;
        var barcode = await NextBarcodeAsync(connection, companyId, cancellationToken).ConfigureAwait(false);
        var provider = Clip((request.RefundProvider ?? string.Empty).Trim(), 50);
        if (provider.Length == 0)
        {
            provider = "Planet";
        }

        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional(
                "INSERT INTO `epc_tourist_refund_invoices` (`company_id`,`invoice_id`,`invoice_no`,`tourist_name`,`passport_no`,`nationality`,`departure_date`,`total_amount`,`vat_amount`,`refund_amount`,`refund_pct`,`barcode`,`refund_provider`,`time_created`) VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)"),
            cancellationToken,
            companyId,
            request.InvoiceId < 0 ? 0 : request.InvoiceId,
            Clip((request.InvoiceNo ?? string.Empty).Trim(), 64),
            tourist,
            Clip((request.PassportNo ?? string.Empty).Trim(), 50),
            Clip((request.Nationality ?? string.Empty).Trim(), 50),
            departure?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            total,
            vat,
            quote.Refund,
            decimal.Round(ErpTouristRefundLaw.TouristShare * 100m, 2, MidpointRounding.AwayFromZero),
            barcode,
            provider,
            UnixNow()).ConfigureAwait(false);

        var id = await ErpDb.LastInsertIdAsync(connection, null, cancellationToken).ConfigureAwait(false);
        if (id <= 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Failed");
        }

        await TryStampTagAsync(connection, id, quote, request, supply, method, cancellationToken).ConfigureAwait(false);
        return ErpSimpleWriteResult.Ok(
            "Tourist refund " + barcode + " created. Tourist receives " + ErpTouristRefundLaw.Money(quote.Refund)
            + ". Planet later invoices the VAT " + ErpTouristRefundLaw.Money(quote.Vat) + ".",
            id);
    }

    public async Task<ErpSimpleWriteResult> ValidateAsync(
        ErpTouristRefundValidateRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var barcode = Clip((request.Barcode ?? string.Empty).Trim(), 100);
        if (barcode.Length == 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Barcode is required.");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await EnsureSchemaAsync(connection, cancellationToken).ConfigureAwait(false);
        if (!await TableExistsAsync(connection, "epc_tourist_refund_invoices", cancellationToken).ConfigureAwait(false)
            || !await ColumnExistsAsync(connection, "epc_tourist_refund_invoices", "barcode", cancellationToken).ConfigureAwait(false)
            || !await ColumnExistsAsync(connection, "epc_tourist_refund_invoices", "refund_status", cancellationToken).ConfigureAwait(false))
        {
            return ErpSimpleWriteResult.Fail("invalid", "Tourist refund tables are not provisioned");
        }

        var id = await ErpDb.LongAsync(
            connection,
            null,
            ErpDb.Positional("SELECT `id` FROM `epc_tourist_refund_invoices` WHERE `barcode` = ? LIMIT 1"),
            cancellationToken,
            barcode).ConfigureAwait(false);
        if (id <= 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Barcode not found");
        }

        var status = await ErpDb.StringAsync(
            connection,
            null,
            ErpDb.Positional("SELECT `refund_status` FROM `epc_tourist_refund_invoices` WHERE `id` = ? LIMIT 1"),
            cancellationToken,
            id).ConfigureAwait(false);
        if (!string.Equals(status, "pending", StringComparison.OrdinalIgnoreCase))
        {
            return ErpSimpleWriteResult.Fail("invalid", "Already processed: " + (status ?? string.Empty));
        }

        var supplyUnix = await ErpDb.LongAsync(
            connection,
            null,
            ErpDb.Positional("SELECT `time_created` FROM `epc_tourist_refund_invoices` WHERE `id` = ? LIMIT 1"),
            cancellationToken,
            id).ConfigureAwait(false);
        if (supplyUnix > 0)
        {
            var age = DateTimeOffset.UtcNow - DateTimeOffset.FromUnixTimeSeconds(supplyUnix);
            if (age.TotalDays > ErpTouristRefundLaw.ExportDays)
            {
                await ErpDb.ExecuteAsync(
                    connection,
                    null,
                    ErpDb.Positional("UPDATE `epc_tourist_refund_invoices` SET `refund_status` = 'expired' WHERE `id` = ? AND `refund_status` = 'pending'"),
                    cancellationToken,
                    id).ConfigureAwait(false);
                return ErpSimpleWriteResult.Fail("invalid", "The goods must be exported within 90 days of the supply date.");
            }
        }

        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional("UPDATE `epc_tourist_refund_invoices` SET `refund_status` = 'validated', `validated_at` = NOW() WHERE `id` = ?"),
            cancellationToken,
            id).ConfigureAwait(false);

        var vat = await ErpDb.DecimalAsync(
            connection,
            null,
            ErpDb.Positional("SELECT `vat_amount` FROM `epc_tourist_refund_invoices` WHERE `id` = ? LIMIT 1"),
            cancellationToken,
            id).ConfigureAwait(false);
        return ErpSimpleWriteResult.Ok(
            "Tourist refund validated. VAT " + ErpTouristRefundLaw.Money(vat) + " is ready for VAT 201 Box 2 when Planet invoices it.",
            id);
    }

    public async Task<ErpSimpleWriteResult> RecordPlanetInvoiceAsync(
        ErpTouristRefundPlanetInvoiceRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var invoiceNo = Clip((request.InvoiceNo ?? string.Empty).Trim(), 64);
        if (invoiceNo.Length == 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Planet invoice number is required.");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await EnsureSchemaAsync(connection, cancellationToken).ConfigureAwait(false);
        var existingId = await ErpDb.LongAsync(
            connection,
            null,
            ErpDb.Positional("SELECT `id` FROM `epc_tourist_refund_planet_invoices` WHERE `invoice_no` = ? ORDER BY `id` DESC LIMIT 1"),
            cancellationToken,
            invoiceNo).ConfigureAwait(false);
        if (existingId > 0)
        {
            var existingVat = await ErpDb.DecimalAsync(
                connection,
                null,
                ErpDb.Positional("SELECT `vat_amount` FROM `epc_tourist_refund_planet_invoices` WHERE `id` = ? LIMIT 1"),
                cancellationToken,
                existingId).ConfigureAwait(false);
            var existingJournal = await ErpDb.LongAsync(
                connection,
                null,
                ErpDb.Positional("SELECT `gl_journal_id` FROM `epc_tourist_refund_planet_invoices` WHERE `id` = ? LIMIT 1"),
                cancellationToken,
                existingId).ConfigureAwait(false);
            var note = "Planet invoice " + invoiceNo + " is already recorded for VAT " + ErpTouristRefundLaw.Money(existingVat) + ".";
            if (existingJournal <= 0)
            {
                var invoiceDateExisting = ParseDate(request.InvoiceDate) ?? DateOnly.FromDateTime(DateTime.UtcNow);
                note = note + " " + await PostPlanetJournalAsync(connection, existingId, invoiceNo, invoiceDateExisting, existingVat, pay: false, "bank", cancellationToken).ConfigureAwait(false);
            }

            return ErpSimpleWriteResult.Ok(note, existingId);
        }

        var invoiceDate = ParseDate(request.InvoiceDate) ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var due = invoiceDate.AddDays(ErpTouristRefundLaw.PlanetPaymentDays);
        var wanted = SplitBarcodes(request.Barcodes);
        var tags = await WaitingTagsAsync(connection, wanted, cancellationToken).ConfigureAwait(false);
        if (tags.Count == 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "No validated tags are waiting for a Planet invoice.");
        }

        var vat = Round2(tags.Sum(tag => tag.Vat));
        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional(
                "INSERT INTO `epc_tourist_refund_planet_invoices` (`company_id`,`invoice_no`,`invoice_date`,`due_date`,`vat_amount`,`tag_count`,`status`,`time_created`) VALUES (?,?,?,?,?,?, 'open', ?)"),
            cancellationToken,
            request.CompanyId < 0 ? 0 : request.CompanyId,
            invoiceNo,
            invoiceDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            due.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            vat,
            tags.Count,
            UnixNow()).ConfigureAwait(false);
        var id = await ErpDb.LastInsertIdAsync(connection, null, cancellationToken).ConfigureAwait(false);
        foreach (var tag in tags)
        {
            await ErpDb.ExecuteAsync(
                connection,
                null,
                ErpDb.Positional(
                    "UPDATE `epc_tourist_refund_invoices` SET `planet_invoice_no` = ?, `planet_invoice_id` = ?, `refund_status` = 'refunded' WHERE `id` = ?"),
                cancellationToken,
                invoiceNo,
                id,
                tag.Id).ConfigureAwait(false);
        }

        var journalNote = await PostPlanetJournalAsync(connection, id, invoiceNo, invoiceDate, vat, pay: false, "bank", cancellationToken).ConfigureAwait(false);
        return ErpSimpleWriteResult.Ok(
            "Planet invoice " + invoiceNo + " recorded for VAT " + ErpTouristRefundLaw.Money(vat)
            + ". Due " + due.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) + ". " + journalNote,
            id);
    }

    public async Task<ErpSimpleWriteResult> PayPlanetInvoiceAsync(
        ErpTouristRefundPlanetPayRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var invoiceNo = Clip((request.InvoiceNo ?? string.Empty).Trim(), 64);
        if (invoiceNo.Length == 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Planet invoice number is required.");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await EnsureSchemaAsync(connection, cancellationToken).ConfigureAwait(false);
        var id = await ErpDb.LongAsync(
            connection,
            null,
            ErpDb.Positional("SELECT `id` FROM `epc_tourist_refund_planet_invoices` WHERE `invoice_no` = ? ORDER BY `id` DESC LIMIT 1"),
            cancellationToken,
            invoiceNo).ConfigureAwait(false);
        if (id <= 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Planet invoice was not found.");
        }

        var status = await ErpDb.StringAsync(
            connection,
            null,
            ErpDb.Positional("SELECT `status` FROM `epc_tourist_refund_planet_invoices` WHERE `id` = ? LIMIT 1"),
            cancellationToken,
            id).ConfigureAwait(false);
        var vat = await ErpDb.DecimalAsync(
            connection,
            null,
            ErpDb.Positional("SELECT `vat_amount` FROM `epc_tourist_refund_planet_invoices` WHERE `id` = ? LIMIT 1"),
            cancellationToken,
            id).ConfigureAwait(false);
        var payJournal = await ErpDb.LongAsync(
            connection,
            null,
            ErpDb.Positional("SELECT `pay_journal_id` FROM `epc_tourist_refund_planet_invoices` WHERE `id` = ? LIMIT 1"),
            cancellationToken,
            id).ConfigureAwait(false);
        if (string.Equals(status, "paid", StringComparison.OrdinalIgnoreCase) && (payJournal > 0 || _journals is null))
        {
            return ErpSimpleWriteResult.Fail("invalid", "Planet invoice is already paid.");
        }

        var payDate = ParseDate(request.PayDate) ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var from = string.Equals((request.PayFrom ?? string.Empty).Trim(), "cash", StringComparison.OrdinalIgnoreCase) ? "cash" : "bank";
        var journalNote = await PostPlanetJournalAsync(connection, id, invoiceNo, payDate, vat, pay: true, from, cancellationToken).ConfigureAwait(false);
        var posted = await ErpDb.LongAsync(
            connection,
            null,
            ErpDb.Positional("SELECT `pay_journal_id` FROM `epc_tourist_refund_planet_invoices` WHERE `id` = ? LIMIT 1"),
            cancellationToken,
            id).ConfigureAwait(false);
        if (_journals is not null && posted <= 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", journalNote);
        }

        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional("UPDATE `epc_tourist_refund_planet_invoices` SET `status` = 'paid' WHERE `id` = ?"),
            cancellationToken,
            id).ConfigureAwait(false);
        return ErpSimpleWriteResult.Ok(
            "Planet invoice " + invoiceNo + " paid. VAT " + ErpTouristRefundLaw.Money(vat) + ". " + journalNote,
            id);
    }

    public async Task<ErpTouristRefundPosition> PositionAsync(CancellationToken cancellationToken = default)
    {
        if (!_connections.IsConfigured)
        {
            return ErpTouristRefundPosition.Empty;
        }

        try
        {
            await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
            if (!await TableExistsAsync(connection, "epc_tourist_refund_invoices", cancellationToken).ConfigureAwait(false))
            {
                return ErpTouristRefundPosition.Empty;
            }

            var validatedVat = await SumAsync(
                connection,
                "SELECT COALESCE(SUM(`vat_amount`),0) FROM `epc_tourist_refund_invoices` WHERE `refund_status` IN ('validated','refunded')",
                cancellationToken).ConfigureAwait(false);
            var validatedExclusive = await SumAsync(
                connection,
                "SELECT COALESCE(SUM(`total_amount` - `vat_amount`),0) FROM `epc_tourist_refund_invoices` WHERE `refund_status` IN ('validated','refunded')",
                cancellationToken).ConfigureAwait(false);
            var pendingVat = await SumAsync(
                connection,
                "SELECT COALESCE(SUM(`vat_amount`),0) FROM `epc_tourist_refund_invoices` WHERE `refund_status` = 'pending'",
                cancellationToken).ConfigureAwait(false);
            var openPayable = 0m;
            if (await TableExistsAsync(connection, "epc_tourist_refund_planet_invoices", cancellationToken).ConfigureAwait(false))
            {
                openPayable = await SumAsync(
                    connection,
                    "SELECT COALESCE(SUM(`vat_amount`),0) FROM `epc_tourist_refund_planet_invoices` WHERE `status` = 'open'",
                    cancellationToken).ConfigureAwait(false);
            }

            var tags = new List<ErpTouristRefundTagRow>();
            var planetColumn = await ColumnExistsAsync(connection, "epc_tourist_refund_invoices", "planet_invoice_no", cancellationToken).ConfigureAwait(false);
            var sql = planetColumn
                ? "SELECT `id`,`invoice_no`,`tourist_name`,`barcode`,`refund_status`,`total_amount`,`vat_amount`,`refund_amount`, IFNULL(`planet_invoice_no`,'') AS planet_invoice_no FROM `epc_tourist_refund_invoices` ORDER BY `id` DESC LIMIT 12"
                : "SELECT `id`,`invoice_no`,`tourist_name`,`barcode`,`refund_status`,`total_amount`,`vat_amount`,`refund_amount`, '' AS planet_invoice_no FROM `epc_tourist_refund_invoices` ORDER BY `id` DESC LIMIT 12";
            await using var command = connection.CreateCommand();
            command.CommandText = sql;
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                tags.Add(new ErpTouristRefundTagRow(
                    Convert.ToInt64(reader.GetValue(0), CultureInfo.InvariantCulture),
                    Convert.ToString(reader.GetValue(1), CultureInfo.InvariantCulture) ?? string.Empty,
                    Convert.ToString(reader.GetValue(2), CultureInfo.InvariantCulture) ?? string.Empty,
                    Convert.ToString(reader.GetValue(3), CultureInfo.InvariantCulture) ?? string.Empty,
                    Convert.ToString(reader.GetValue(4), CultureInfo.InvariantCulture) ?? string.Empty,
                    reader.IsDBNull(5) ? 0 : Convert.ToDecimal(reader.GetValue(5), CultureInfo.InvariantCulture),
                    reader.IsDBNull(6) ? 0 : Convert.ToDecimal(reader.GetValue(6), CultureInfo.InvariantCulture),
                    reader.IsDBNull(7) ? 0 : Convert.ToDecimal(reader.GetValue(7), CultureInfo.InvariantCulture),
                    Convert.ToString(reader.GetValue(8), CultureInfo.InvariantCulture) ?? string.Empty));
            }

            return new ErpTouristRefundPosition(validatedExclusive, validatedVat, pendingVat, openPayable, tags);
        }
        catch (DbException)
        {
            return ErpTouristRefundPosition.Empty;
        }
    }

    private async Task TryStampTagAsync(
        DbConnection connection,
        long id,
        ErpTouristRefundQuote quote,
        ErpTouristRefundCreateRequest request,
        DateOnly supply,
        string method,
        CancellationToken cancellationToken)
    {
        if (!await ColumnExistsAsync(connection, "epc_tourist_refund_invoices", "fee_amount", cancellationToken).ConfigureAwait(false))
        {
            return;
        }

        try
        {
            await ErpDb.ExecuteAsync(
                connection,
                null,
                ErpDb.Positional(
                    "UPDATE `epc_tourist_refund_invoices` SET `fee_amount` = ?, `retained_amount` = ?, `supply_kind` = ?, `export_deadline` = ?, `refund_method` = ? WHERE `id` = ?"),
                cancellationToken,
                quote.FixedFee,
                quote.Retained,
                Clip(string.IsNullOrWhiteSpace(request.SupplyKind) ? "goods" : request.SupplyKind.Trim(), 20),
                supply.AddDays(ErpTouristRefundLaw.ExportDays).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                method,
                id).ConfigureAwait(false);
        }
        catch (DbException)
        {
        }
    }

    private async Task<IReadOnlyList<ErpTouristRefundTagRow>> WaitingTagsAsync(
        DbConnection connection,
        IReadOnlyList<string> barcodes,
        CancellationToken cancellationToken)
    {
        var rows = new List<ErpTouristRefundTagRow>();
        var sql = barcodes.Count == 0
            ? "SELECT `id`,`invoice_no`,`tourist_name`,`barcode`,`refund_status`,`total_amount`,`vat_amount`,`refund_amount` FROM `epc_tourist_refund_invoices` WHERE `refund_status` = 'validated' AND (`planet_invoice_no` = '' OR `planet_invoice_no` IS NULL) ORDER BY `id`"
            : "SELECT `id`,`invoice_no`,`tourist_name`,`barcode`,`refund_status`,`total_amount`,`vat_amount`,`refund_amount` FROM `epc_tourist_refund_invoices` WHERE `refund_status` = 'validated' AND (`planet_invoice_no` = '' OR `planet_invoice_no` IS NULL) AND `barcode` = ? ORDER BY `id`";
        if (barcodes.Count == 0)
        {
            await ReadTagsAsync(connection, sql, rows, cancellationToken).ConfigureAwait(false);
            return rows;
        }

        foreach (var barcode in barcodes)
        {
            await ReadTagsAsync(connection, sql, rows, cancellationToken, barcode).ConfigureAwait(false);
        }

        return rows;
    }

    private static async Task ReadTagsAsync(
        DbConnection connection,
        string sql,
        List<ErpTouristRefundTagRow> rows,
        CancellationToken cancellationToken,
        string? barcode = null)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        if (barcode is not null)
        {
            ErpDb.AddParameters(command, barcode);
        }

        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            rows.Add(new ErpTouristRefundTagRow(
                Convert.ToInt64(reader.GetValue(0), CultureInfo.InvariantCulture),
                Convert.ToString(reader.GetValue(1), CultureInfo.InvariantCulture) ?? string.Empty,
                Convert.ToString(reader.GetValue(2), CultureInfo.InvariantCulture) ?? string.Empty,
                Convert.ToString(reader.GetValue(3), CultureInfo.InvariantCulture) ?? string.Empty,
                Convert.ToString(reader.GetValue(4), CultureInfo.InvariantCulture) ?? string.Empty,
                reader.IsDBNull(5) ? 0 : Convert.ToDecimal(reader.GetValue(5), CultureInfo.InvariantCulture),
                reader.IsDBNull(6) ? 0 : Convert.ToDecimal(reader.GetValue(6), CultureInfo.InvariantCulture),
                reader.IsDBNull(7) ? 0 : Convert.ToDecimal(reader.GetValue(7), CultureInfo.InvariantCulture),
                string.Empty));
        }
    }

    private async Task<string> PostPlanetJournalAsync(
        DbConnection connection,
        long sourceId,
        string invoiceNo,
        DateOnly date,
        decimal vat,
        bool pay,
        string payFrom,
        CancellationToken cancellationToken)
    {
        if (_journals is null || vat <= 0)
        {
            return "The VAT figure is on the tourist refund screen. Post the journal when the chart of accounts is open.";
        }

        var existingColumn = pay ? "pay_journal_id" : "gl_journal_id";
        var existingJournal = await ErpDb.LongAsync(
            connection,
            null,
            ErpDb.Positional("SELECT `" + existingColumn + "` FROM `epc_tourist_refund_planet_invoices` WHERE `id` = ? LIMIT 1"),
            cancellationToken,
            sourceId).ConfigureAwait(false);
        if (existingJournal > 0)
        {
            return pay
                ? "Payment journal is already posted."
                : "Journal debited output VAT 2100 and credited Planet payable 2000.";
        }

        try
        {
            await _journals.EnsureSchemaAsync(connection, cancellationToken).ConfigureAwait(false);
            var outputId = await CoaIdAsync(connection, "2100", cancellationToken).ConfigureAwait(false);
            var payableId = await CoaIdAsync(connection, "2000", cancellationToken).ConfigureAwait(false);
            var cashId = await CoaIdAsync(connection, payFrom == "cash" ? "1000" : "1010", cancellationToken).ConfigureAwait(false);
            if (outputId <= 0 || payableId <= 0 || (pay && cashId <= 0))
            {
                return "Chart accounts 2100 and 2000 are required before the Planet journal can post.";
            }

            var unix = new DateTimeOffset(date.Year, date.Month, date.Day, 0, 0, 0, TimeSpan.Zero).ToUnixTimeSeconds();
            IReadOnlyList<ErpGlLine> lines = pay
                ?
                [
                    new ErpGlLine(payableId, vat, 0, "Planet invoice " + invoiceNo + " paid"),
                    new ErpGlLine(cashId, 0, vat, payFrom == "cash" ? "Cash" : "Bank"),
                ]
                :
                [
                    new ErpGlLine(outputId, vat, 0, "Output VAT reduced for tourist refunds (Box 2)"),
                    new ErpGlLine(payableId, 0, vat, "Planet payable — VAT refunded to tourists"),
                ];
            var journalId = await _journals.PostJournalAsync(
                connection,
                new ErpGlJournalHeader
                {
                    JournalDate = unix,
                    Reference = "PLANET-" + invoiceNo,
                    Description = pay
                        ? "Payment of Planet tourist refund invoice " + invoiceNo
                        : "Planet tourist refund invoice " + invoiceNo + " — VAT 201 Box 2",
                    SourceType = "manual",
                    SourceId = sourceId,
                    LegislationRef = ErpTouristRefundLaw.LegislationRef,
                    CurrencyCode = "AED",
                },
                lines,
                0,
                cancellationToken).ConfigureAwait(false);
            var column = pay ? "pay_journal_id" : "gl_journal_id";
            await ErpDb.ExecuteAsync(
                connection,
                null,
                ErpDb.Positional("UPDATE `epc_tourist_refund_planet_invoices` SET `" + column + "` = ? WHERE `id` = ?"),
                cancellationToken,
                journalId,
                sourceId).ConfigureAwait(false);
            return pay
                ? "Journal debited Planet payable and credited " + (payFrom == "cash" ? "cash" : "bank") + "."
                : "Journal debited output VAT 2100 and credited Planet payable 2000.";
        }
        catch (ErpWriteException ex)
        {
            return ex.Message;
        }
        catch (DbException)
        {
            return "The Planet invoice is recorded. The journal was not posted.";
        }
    }

    private static async Task<long> CoaIdAsync(DbConnection connection, string code, CancellationToken cancellationToken)
    {
        try
        {
            return await ErpDb.LongAsync(
                connection,
                null,
                ErpDb.Positional("SELECT `id` FROM `epc_erp_coa_accounts` WHERE `code` = ? AND `active` = 1 LIMIT 1"),
                cancellationToken,
                code).ConfigureAwait(false);
        }
        catch (DbException)
        {
            return 0;
        }
    }

    private static async Task EnsureSchemaAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        await ErpDb.TryExecuteAsync(
            connection,
            "CREATE TABLE IF NOT EXISTS `epc_tourist_refund_invoices` ("
            + "`id` int(11) NOT NULL AUTO_INCREMENT,"
            + "`company_id` int(11) NOT NULL DEFAULT 0,"
            + "`invoice_id` int(11) NOT NULL DEFAULT 0,"
            + "`invoice_no` varchar(64) NOT NULL DEFAULT '',"
            + "`tourist_name` varchar(200) NOT NULL DEFAULT '',"
            + "`passport_no` varchar(50) NOT NULL DEFAULT '',"
            + "`nationality` varchar(50) NOT NULL DEFAULT '',"
            + "`departure_date` date DEFAULT NULL,"
            + "`total_amount` decimal(14,2) NOT NULL DEFAULT 0.00,"
            + "`vat_amount` decimal(14,2) NOT NULL DEFAULT 0.00,"
            + "`refund_amount` decimal(14,2) NOT NULL DEFAULT 0.00,"
            + "`refund_pct` decimal(5,2) NOT NULL DEFAULT 87.00,"
            + "`barcode` varchar(100) NOT NULL DEFAULT '',"
            + "`barcode_image_path` varchar(500) NOT NULL DEFAULT '',"
            + "`refund_status` enum('pending','validated','refunded','expired','rejected') NOT NULL DEFAULT 'pending',"
            + "`refund_provider` varchar(50) NOT NULL DEFAULT '',"
            + "`provider_reference` varchar(100) NOT NULL DEFAULT '',"
            + "`validated_at` datetime DEFAULT NULL,"
            + "`refunded_at` datetime DEFAULT NULL,"
            + "`time_created` int(11) NOT NULL DEFAULT 0,"
            + "PRIMARY KEY (`id`),"
            + "KEY `x_company` (`company_id`),"
            + "KEY `x_invoice` (`invoice_id`),"
            + "KEY `x_barcode` (`barcode`),"
            + "KEY `x_passport` (`passport_no`),"
            + "KEY `x_status` (`refund_status`)"
            + ") ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='Tourist VAT refund invoices'",
            cancellationToken).ConfigureAwait(false);
        await ErpDb.TryExecuteAsync(connection, "ALTER TABLE `epc_tourist_refund_invoices` ADD `fee_amount` decimal(14,2) NOT NULL DEFAULT 0.00", cancellationToken).ConfigureAwait(false);
        await ErpDb.TryExecuteAsync(connection, "ALTER TABLE `epc_tourist_refund_invoices` ADD `retained_amount` decimal(14,2) NOT NULL DEFAULT 0.00", cancellationToken).ConfigureAwait(false);
        await ErpDb.TryExecuteAsync(connection, "ALTER TABLE `epc_tourist_refund_invoices` ADD `supply_kind` varchar(20) NOT NULL DEFAULT 'goods'", cancellationToken).ConfigureAwait(false);
        await ErpDb.TryExecuteAsync(connection, "ALTER TABLE `epc_tourist_refund_invoices` ADD `export_deadline` date DEFAULT NULL", cancellationToken).ConfigureAwait(false);
        await ErpDb.TryExecuteAsync(connection, "ALTER TABLE `epc_tourist_refund_invoices` ADD `refund_method` varchar(20) NOT NULL DEFAULT 'card'", cancellationToken).ConfigureAwait(false);
        await ErpDb.TryExecuteAsync(connection, "ALTER TABLE `epc_tourist_refund_invoices` ADD `planet_invoice_no` varchar(64) NOT NULL DEFAULT ''", cancellationToken).ConfigureAwait(false);
        await ErpDb.TryExecuteAsync(connection, "ALTER TABLE `epc_tourist_refund_invoices` ADD `planet_invoice_id` int(11) NOT NULL DEFAULT 0", cancellationToken).ConfigureAwait(false);
        await ErpDb.TryExecuteAsync(
            connection,
            "CREATE TABLE IF NOT EXISTS `epc_tourist_refund_planet_invoices` ("
            + "`id` int(11) NOT NULL AUTO_INCREMENT,"
            + "`company_id` int(11) NOT NULL DEFAULT 0,"
            + "`invoice_no` varchar(64) NOT NULL DEFAULT '',"
            + "`invoice_date` date DEFAULT NULL,"
            + "`due_date` date DEFAULT NULL,"
            + "`vat_amount` decimal(14,2) NOT NULL DEFAULT 0.00,"
            + "`tag_count` int(11) NOT NULL DEFAULT 0,"
            + "`status` enum('open','paid') NOT NULL DEFAULT 'open',"
            + "`gl_journal_id` int(11) NOT NULL DEFAULT 0,"
            + "`pay_journal_id` int(11) NOT NULL DEFAULT 0,"
            + "`time_created` int(11) NOT NULL DEFAULT 0,"
            + "PRIMARY KEY (`id`),"
            + "KEY `x_invoice` (`invoice_no`)"
            + ") ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='Planet invoices to the retailer for tourist VAT'",
            cancellationToken).ConfigureAwait(false);
        await ErpDb.TryExecuteAsync(
            connection,
            "CREATE TABLE IF NOT EXISTS `epc_tourist_refund_config` ("
            + "`id` int(11) NOT NULL AUTO_INCREMENT,"
            + "`company_id` int(11) NOT NULL DEFAULT 0,"
            + "`min_purchase_amount` decimal(14,2) NOT NULL DEFAULT 250.00,"
            + "`vat_rate` decimal(5,2) NOT NULL DEFAULT 5.00,"
            + "`refund_percentage` decimal(5,2) NOT NULL DEFAULT 87.00,"
            + "`provider` varchar(50) NOT NULL DEFAULT 'Planet',"
            + "`provider_merchant_id` varchar(100) NOT NULL DEFAULT '',"
            + "`provider_api_key` varchar(300) NOT NULL DEFAULT '',"
            + "`barcode_prefix` varchar(10) NOT NULL DEFAULT 'TR',"
            + "`auto_print` tinyint(1) NOT NULL DEFAULT 1,"
            + "`time_updated` int(11) NOT NULL DEFAULT 0,"
            + "PRIMARY KEY (`id`),"
            + "KEY `x_company` (`company_id`)"
            + ") ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='Tourist refund configuration'",
            cancellationToken).ConfigureAwait(false);
    }

    private static async Task<string> NextBarcodeAsync(DbConnection connection, int companyId, CancellationToken cancellationToken)
    {
        var prefix = "TR";
        if (await TableExistsAsync(connection, "epc_tourist_refund_config", cancellationToken).ConfigureAwait(false)
            && await ColumnExistsAsync(connection, "epc_tourist_refund_config", "barcode_prefix", cancellationToken).ConfigureAwait(false))
        {
            var configured = await ErpDb.StringAsync(
                connection,
                null,
                ErpDb.Positional("SELECT `barcode_prefix` FROM `epc_tourist_refund_config` WHERE `company_id` = ? LIMIT 1"),
                cancellationToken,
                companyId).ConfigureAwait(false);
            var raw = configured?.Trim() ?? "";
            if (raw.Length > 0)
            {
                prefix = Clip(raw, 10);
            }
        }

        return prefix
               + DateTime.UtcNow.ToString("yyyyMMdd", CultureInfo.InvariantCulture)
               + Random.Shared.Next(1, 999999).ToString("D6", CultureInfo.InvariantCulture);
    }

    private static async Task<decimal> SumAsync(DbConnection connection, string sql, CancellationToken cancellationToken)
        => await ErpDb.DecimalAsync(connection, null, sql, cancellationToken).ConfigureAwait(false);

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

    private static async Task<bool> ColumnExistsAsync(DbConnection connection, string table, string column, CancellationToken cancellationToken)
    {
        var n = await ErpDb.LongAsync(
            connection,
            null,
            ErpDb.Positional("SELECT COUNT(*) FROM information_schema.COLUMNS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = ? AND COLUMN_NAME = ?"),
            cancellationToken,
            table,
            column).ConfigureAwait(false);
        return n > 0;
    }

    private static DateOnly? ParseDate(string? raw)
    {
        var value = (raw ?? string.Empty).Trim();
        if (value.Length == 0)
        {
            return null;
        }

        return DateOnly.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed)
            ? parsed
            : null;
    }

    private static string NormalizeMethod(string? raw)
    {
        var method = (raw ?? string.Empty).Trim().ToLowerInvariant();
        return method is "cash" ? "cash" : "card";
    }

    private static IReadOnlyList<string> SplitBarcodes(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return [];
        }

        return raw.Split([',', ' ', '\n', '\r', '\t'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(item => Clip(item, 100))
            .Where(item => item.Length > 0)
            .Distinct(StringComparer.Ordinal)
            .ToList();
    }

    private static decimal Round2(decimal value)
        => decimal.Round(value, 2, MidpointRounding.AwayFromZero);

    private static string Clip(string value, int maxLen)
        => value.Length <= maxLen ? value : value[..maxLen];

    private static long UnixNow()
        => DateTimeOffset.UtcNow.ToUnixTimeSeconds();
}
