using System.Data.Common;
using System.Globalization;
using System.Text;

namespace EcomAE.Platform.Erp;

/// <summary>
/// Live PHP <c>jw_tourist_vat_save</c> / <c>epc_jewel_tourist_vat_save</c> twin.
/// Writes the provisioned <c>epc_jewel_tourist_vat_refund</c> schema. Schema-ensure stays PHP.
/// </summary>
public interface IErpJwTouristVatWriteService
{
    Task<ErpSimpleWriteResult> SaveAsync(
        ErpJwTouristVatSaveRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record ErpJwTouristVatSaveRequest(
    int CompanyId = 0,
    string? Branch = null,
    string? RefundDate = null,
    string? VocDate = null,
    string? TouristName = null,
    string? PassportNo = null,
    string? Nationality = null,
    string? Mobile = null,
    string? Email = null,
    string? FlightNo = null,
    string? DepartureDate = null,
    string? InvoiceNo = null,
    string? InvoiceDate = null,
    string? Salesman = null,
    string? Narration = null,
    decimal TotalVat = 0,
    decimal VatAmount = 0,
    decimal TotalRefund = 0,
    decimal RefundAmount = 0);

public sealed class ErpJwTouristVatWriteService : IErpJwTouristVatWriteService
{
    private readonly IErpWriteConnectionFactory _connections;

    public ErpJwTouristVatWriteService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<ErpSimpleWriteResult> SaveAsync(
        ErpJwTouristVatSaveRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var tourist = (request.TouristName ?? string.Empty).Trim();
        if (tourist.Length == 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Tourist name is required.");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        if (!await TableExistsAsync(connection, "epc_jewel_tourist_vat_refund", cancellationToken).ConfigureAwait(false))
        {
            return ErpSimpleWriteResult.Fail("invalid", "Jewellery tourist VAT tables are not provisioned");
        }

        var companyId = request.CompanyId < 0 ? 0 : request.CompanyId;
        var branch = (request.Branch ?? string.Empty).Trim();
        if (branch.Length == 0)
        {
            branch = "HO";
        }

        branch = Clip(branch, 10);
        var vocDate = NormalizeDate(FirstNonEmpty(request.RefundDate, request.VocDate, request.InvoiceDate));
        var passport = Clip((request.PassportNo ?? string.Empty).Trim(), 20);
        var phone = Clip((request.Mobile ?? string.Empty).Trim(), 20);
        var email = Clip((request.Email ?? string.Empty).Trim(), 100);
        var salesman = Clip((request.Salesman ?? string.Empty).Trim(), 20);
        var journal = Clip(FirstNonEmpty(request.InvoiceNo, request.FlightNo), 30);
        var dateFrom = NormalizeDateOrEmpty(request.InvoiceDate);
        var dateTo = NormalizeDateOrEmpty(request.DepartureDate);
        var vat = request.TotalVat != 0 ? request.TotalVat : request.VatAmount;
        var refund = request.TotalRefund != 0 ? request.TotalRefund : request.RefundAmount;
        var remarks = BuildRemarks(request.Narration, request.Nationality, request.FlightNo, vat, refund);

        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional(
                "INSERT INTO `epc_jewel_tourist_vat_refund` (`company_id`,`branch`,`voc_type`,`voc_date`,`voc_no`,`party_code`,`party_name`,`party_phone`,`party_email`,`salesman`,`journal_ref`,`date_from`,`date_to`,`remarks`,`status`) VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)"),
            cancellationToken,
            companyId,
            branch,
            "VRV",
            vocDate,
            0,
            passport,
            Clip(tourist, 120),
            phone,
            email,
            salesman,
            journal,
            string.IsNullOrWhiteSpace(dateFrom) ? null : dateFrom,
            string.IsNullOrWhiteSpace(dateTo) ? null : dateTo,
            remarks,
            "Pending").ConfigureAwait(false);

        var id = await ErpDb.LastInsertIdAsync(connection, null, cancellationToken).ConfigureAwait(false);
        if (id <= 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Failed");
        }

        return ErpSimpleWriteResult.Ok("Tourist VAT refund saved", id);
    }

    private static string BuildRemarks(string? narration, string? nationality, string? flightNo, decimal vat, decimal refund)
    {
        var sb = new StringBuilder();
        var note = (narration ?? string.Empty).Trim();
        if (note.Length > 0)
        {
            sb.Append(note);
        }

        var nation = (nationality ?? string.Empty).Trim();
        if (nation.Length > 0)
        {
            if (sb.Length > 0) sb.Append(" | ");
            sb.Append("nationality=").Append(nation);
        }

        var flight = (flightNo ?? string.Empty).Trim();
        if (flight.Length > 0)
        {
            if (sb.Length > 0) sb.Append(" | ");
            sb.Append("flight=").Append(flight);
        }

        if (vat != 0 || refund != 0)
        {
            if (sb.Length > 0) sb.Append(" | ");
            sb.Append("vat=").Append(vat.ToString("0.00", CultureInfo.InvariantCulture));
            sb.Append(" refund=").Append(refund.ToString("0.00", CultureInfo.InvariantCulture));
        }

        return sb.ToString();
    }

    private static string FirstNonEmpty(params string?[] values)
    {
        foreach (var value in values)
        {
            var raw = (value ?? string.Empty).Trim();
            if (raw.Length > 0)
            {
                return raw;
            }
        }

        return string.Empty;
    }

    private static string NormalizeDate(string? value)
    {
        var raw = (value ?? string.Empty).Trim();
        if (DateTime.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var parsed))
        {
            return parsed.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        }

        return DateTime.UtcNow.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
    }

    private static string NormalizeDateOrEmpty(string? value)
    {
        var raw = (value ?? string.Empty).Trim();
        if (raw.Length == 0)
        {
            return string.Empty;
        }

        if (DateTime.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var parsed))
        {
            return parsed.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        }

        return string.Empty;
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

    private static string Clip(string value, int maxLen)
        => value.Length <= maxLen ? value : value[..maxLen];
}
