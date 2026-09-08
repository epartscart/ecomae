using System.Data.Common;
using System.Globalization;

namespace EcomAE.Platform.Erp;

/// <summary>
/// Live PHP <c>epc_ins_save</c> / ajax <c>ins_save</c> twin. INSERT/UPDATE
/// <c>epc_erp_ins_policies</c>. Does not CREATE tables. Policy delete, doc add,
/// expiry-tracker sync, and schema ensure stay PHP.
/// </summary>
public interface IErpInsSaveWriteService
{
    Task<ErpSimpleWriteResult> SaveAsync(
        ErpInsSaveWriteRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record ErpInsSaveWriteRequest(
    long Id = 0,
    long CompanyId = 0,
    string? PolicyNo = null,
    string? Class = null,
    string? Title = null,
    string? Insurer = null,
    string? Broker = null,
    string? InsuredName = null,
    decimal SumInsured = 0,
    decimal Premium = 0,
    decimal Deductible = 0,
    string? Currency = null,
    string? StartDate = null,
    string? ExpiryDate = null,
    string? ReminderDays = null,
    string? ContactEmail = null,
    string? Status = null,
    string? Note = null);

public sealed class ErpInsSaveWriteService : IErpInsSaveWriteService
{
    internal static readonly HashSet<string> Classes = new(StringComparer.Ordinal)
    {
        "marine", "property_air", "business_interruption", "public_liability",
        "product_liability", "professional_indemnity", "medical", "gpa", "workmen",
        "fidelity", "electronic_equipment", "machinery_breakdown", "warehouse",
        "assets", "motor", "money", "cyber", "directors", "travel", "credit", "other",
    };

    internal static readonly HashSet<string> Statuses = new(StringComparer.Ordinal)
    {
        "active", "cancelled", "lapsed", "expired",
    };

    private readonly IErpWriteConnectionFactory _connections;

    public ErpInsSaveWriteService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<ErpSimpleWriteResult> SaveAsync(
        ErpInsSaveWriteRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.Id < 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "A policy id must be >= 0.");
        }

        var policyNo = Clip((request.PolicyNo ?? string.Empty).Trim(), 120);
        if (policyNo.Length == 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Policy number is required");
        }

        var expiryRaw = (request.ExpiryDate ?? string.Empty).Trim();
        if (expiryRaw.Length == 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Expiry date is required");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        var companyId = request.CompanyId < 0 ? 0 : request.CompanyId;
        var klass = NormalizeClass(request.Class);
        var title = Clip((request.Title ?? string.Empty).Trim(), 200);
        var insurer = Clip((request.Insurer ?? string.Empty).Trim(), 200);
        var broker = Clip((request.Broker ?? string.Empty).Trim(), 200);
        var insured = Clip((request.InsuredName ?? string.Empty).Trim(), 200);
        var sum = decimal.Round(request.SumInsured, 2, MidpointRounding.AwayFromZero);
        var premium = decimal.Round(request.Premium, 2, MidpointRounding.AwayFromZero);
        var deductible = decimal.Round(request.Deductible, 2, MidpointRounding.AwayFromZero);
        var currency = NormalizeCurrency(request.Currency);
        var startUnix = ResolveDateUnix(request.StartDate);
        var expiryUnix = ResolveDateUnix(expiryRaw);
        var reminder = NormalizeReminderDays(request.ReminderDays);
        var email = Clip((request.ContactEmail ?? string.Empty).Trim(), 200);
        var status = NormalizeStatus(request.Status);
        var note = request.Note ?? string.Empty;
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        if (!await ColumnExistsAsync(connection, "epc_erp_ins_policies", "policy_no", cancellationToken).ConfigureAwait(false)
            || !await ColumnExistsAsync(connection, "epc_erp_ins_policies", "expiry_date", cancellationToken).ConfigureAwait(false))
        {
            return ErpSimpleWriteResult.Fail("invalid", "Insurance policy table is not provisioned");
        }

        if (request.Id > 0)
        {
            await ErpDb.ExecuteAsync(
                connection,
                null,
                ErpDb.Positional(
                    "UPDATE `epc_erp_ins_policies` SET `policy_no`=?, `class`=?, `title`=?, `insurer`=?, `broker`=?, `insured_name`=?, `sum_insured`=?, `premium`=?, `deductible`=?, `currency`=?, `start_date`=?, `expiry_date`=?, `reminder_days`=?, `contact_email`=?, `status`=?, `note`=?, `time_updated`=? WHERE `id`=?"),
                cancellationToken,
                policyNo, klass, title, insurer, broker, insured, sum, premium, deductible,
                currency, startUnix, expiryUnix, reminder, email, status, note, now, request.Id)
                .ConfigureAwait(false);
            return ErpSimpleWriteResult.Ok("Policy saved", request.Id);
        }

        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional(
                "INSERT INTO `epc_erp_ins_policies` (`company_id`,`policy_no`,`class`,`title`,`insurer`,`broker`,`insured_name`,`sum_insured`,`premium`,`deductible`,`currency`,`start_date`,`expiry_date`,`reminder_days`,`contact_email`,`status`,`note`,`time_created`,`time_updated`) VALUES (?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?)"),
            cancellationToken,
            companyId, policyNo, klass, title, insurer, broker, insured, sum, premium, deductible,
            currency, startUnix, expiryUnix, reminder, email, status, note, now, now)
            .ConfigureAwait(false);
        var inserted = await ErpDb.LastInsertIdAsync(connection, null, cancellationToken).ConfigureAwait(false);
        return ErpSimpleWriteResult.Ok("Policy saved", inserted);
    }

    public static string NormalizeClass(string? raw)
    {
        var klass = (raw ?? string.Empty).Trim();
        return Classes.Contains(klass) ? klass : "other";
    }

    public static string NormalizeStatus(string? raw)
    {
        var status = (raw ?? string.Empty).Trim();
        return Statuses.Contains(status) ? status : "active";
    }

    public static string NormalizeCurrency(string? raw)
    {
        var text = (raw ?? string.Empty).Trim();
        if (text.Length == 0)
        {
            return "AED";
        }

        return Clip(text.ToUpperInvariant(), 3);
    }

    public static string NormalizeReminderDays(string? raw)
    {
        var parsed = ParseReminderDays(raw);
        return parsed.Count == 0 ? "90,60,30,7" : string.Join(",", parsed);
    }

    public static IReadOnlyList<int> ParseReminderDays(string? raw)
    {
        var seen = new HashSet<int>();
        foreach (var part in (raw ?? string.Empty).Split(','))
        {
            if (int.TryParse(part.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) && n > 0)
            {
                seen.Add(n);
            }
        }

        return seen.OrderByDescending(n => n).ToArray();
    }

    public static long ResolveDateUnix(string? raw)
    {
        var text = (raw ?? string.Empty).Trim();
        if (text.Length == 0)
        {
            return 0;
        }

        if (long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var unix))
        {
            return unix < 0 ? 0 : unix;
        }

        if (DateTime.TryParseExact(text, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
        {
            return new DateTimeOffset(date.Year, date.Month, date.Day, 0, 0, 0, TimeSpan.Zero).ToUnixTimeSeconds();
        }

        return 0;
    }

    private static string Clip(string value, int max)
        => value.Length <= max ? value : value[..max];

    private static async Task<bool> ColumnExistsAsync(
        DbConnection connection,
        string table,
        string column,
        CancellationToken cancellationToken)
    {
        var n = await ErpDb.LongAsync(
            connection,
            null,
            ErpDb.Positional(
                "SELECT COUNT(*) FROM information_schema.COLUMNS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = ? AND COLUMN_NAME = ?"),
            cancellationToken,
            table,
            column).ConfigureAwait(false);
        return n > 0;
    }
}
