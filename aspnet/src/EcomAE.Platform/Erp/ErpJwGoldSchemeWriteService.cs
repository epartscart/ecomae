using System.Data.Common;
using System.Globalization;

namespace EcomAE.Platform.Erp;


// Legacy PHP references twinned by this file (scripts/php_reference_gap_inventory.py):
//   content/shop/finance/epc_erp_gold_scheme.php
/// <summary>
/// Live PHP <c>epc_gold_scheme_create</c> / <c>epc_gold_scheme_enroll</c> /
/// <c>epc_gold_scheme_pay_installment</c> twin. Missing tables are provisioned here.
/// </summary>
public interface IErpJwGoldSchemeWriteService
{
    Task<ErpSimpleWriteResult> CreateAsync(
        ErpJwGoldSchemeCreateRequest request,
        CancellationToken cancellationToken = default);

    Task<ErpSimpleWriteResult> EnrollAsync(
        ErpJwGoldSchemeEnrollRequest request,
        CancellationToken cancellationToken = default);

    Task<ErpSimpleWriteResult> PayAsync(
        ErpJwGoldSchemePayRequest request,
        CancellationToken cancellationToken = default);

    Task<ErpGoldSchemeBoardResult> LoadBoardAsync(
        int companyId,
        CancellationToken cancellationToken = default);
}

public sealed record ErpJwGoldSchemeCreateRequest(
    int CompanyId = 0,
    string? SchemeCode = null,
    string? SchemeName = null,
    string? SchemeType = null,
    int MaturityMonths = 11,
    string? BonusType = null,
    decimal BonusValue = 0,
    decimal MinInstallment = 500,
    decimal MaxInstallment = 50000,
    string? TermsText = null,
    string? CreatedBy = null);

public sealed record ErpJwGoldSchemeEnrollRequest(
    int CompanyId = 0,
    long SchemeId = 0,
    int CustomerId = 0,
    string? CustomerName = null,
    decimal InstallmentAmount = 0,
    string? StartDate = null);

public sealed record ErpGoldSchemeRow(
    long Id,
    string SchemeCode,
    string SchemeName,
    string SchemeType,
    int MaturityMonths,
    string BonusType,
    decimal MinInstallment,
    string CreatedAt,
    string CreatedBy);

public sealed record ErpGoldSchemeBoardResult(
    IReadOnlyList<ErpGoldSchemeRow> Schemes,
    string Source,
    string Message);

public sealed record ErpJwGoldSchemePayRequest(
    long EnrollmentId = 0,
    decimal Amount = 0,
    string? PaymentMode = null,
    decimal GoldRate = 0,
    string? ReceiptNo = null,
    string? PaymentDate = null,
    string? Notes = null);

public sealed class ErpJwGoldSchemeWriteService : IErpJwGoldSchemeWriteService
{
    private static readonly HashSet<string> SchemeTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "value", "gram"
    };

    private static readonly HashSet<string> BonusTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "free_month", "free_making", "discount_pct", "bonus_gram", "none"
    };

    private readonly IErpWriteConnectionFactory _connections;

    public ErpJwGoldSchemeWriteService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<ErpSimpleWriteResult> CreateAsync(
        ErpJwGoldSchemeCreateRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var code = Clip((request.SchemeCode ?? string.Empty).Trim(), 32);
        var name = Clip((request.SchemeName ?? string.Empty).Trim(), 200);
        if (code.Length == 0 && name.Length == 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Scheme code or name is required.");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await ErpJewelSchema.EnsureAsync(connection, cancellationToken).ConfigureAwait(false);
        if (!await TableExistsAsync(connection, "epc_gold_schemes", cancellationToken).ConfigureAwait(false)
            || !await ColumnExistsAsync(connection, "epc_gold_schemes", "scheme_code", cancellationToken).ConfigureAwait(false))
        {
            return ErpSimpleWriteResult.Fail("invalid", "Gold scheme tables are not provisioned");
        }

        var months = request.MaturityMonths <= 0 ? 11 : request.MaturityMonths;
        var minInst = request.MinInstallment <= 0 ? 500m : RoundNonNeg(request.MinInstallment, 2);
        var maxInst = request.MaxInstallment <= 0 ? 50000m : RoundNonNeg(request.MaxInstallment, 2);
        if (maxInst < minInst)
        {
            maxInst = minInst;
        }

        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional(
                "INSERT INTO `epc_gold_schemes` (`company_id`,`scheme_code`,`scheme_name`,`scheme_type`,`maturity_months`,`bonus_type`,`bonus_value`,`min_installment`,`max_installment`,`terms_text`,`created_by`,`time_created`) VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)"),
            cancellationToken,
            request.CompanyId < 0 ? 0 : request.CompanyId,
            code,
            name,
            NormalizeSchemeType(request.SchemeType),
            months,
            NormalizeBonusType(request.BonusType),
            RoundNonNeg(request.BonusValue, 4),
            minInst,
            maxInst,
            request.TermsText ?? string.Empty,
            Clip((request.CreatedBy ?? string.Empty).Trim(), 80),
            UnixNow()).ConfigureAwait(false);

        var id = await ErpDb.LastInsertIdAsync(connection, null, cancellationToken).ConfigureAwait(false);
        if (id <= 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Failed");
        }

        return ErpSimpleWriteResult.Ok("Gold scheme " + (code.Length == 0 ? name : code) + " created", id);
    }

    public async Task<ErpSimpleWriteResult> EnrollAsync(
        ErpJwGoldSchemeEnrollRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.SchemeId <= 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Scheme id is required.");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await ErpJewelSchema.EnsureAsync(connection, cancellationToken).ConfigureAwait(false);
        if (!await TableExistsAsync(connection, "epc_gold_schemes", cancellationToken).ConfigureAwait(false)
            || !await ColumnExistsAsync(connection, "epc_gold_schemes", "scheme_code", cancellationToken).ConfigureAwait(false)
            || !await TableExistsAsync(connection, "epc_gold_scheme_enrollments", cancellationToken).ConfigureAwait(false))
        {
            return ErpSimpleWriteResult.Fail("invalid", "Gold scheme tables are not provisioned");
        }

        var schemeId = await ErpDb.LongAsync(
            connection,
            null,
            ErpDb.Positional("SELECT `id` FROM `epc_gold_schemes` WHERE `id` = ? LIMIT 1"),
            cancellationToken,
            request.SchemeId).ConfigureAwait(false);
        if (schemeId <= 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Scheme is missing.");
        }

        var companyId = (int)await ErpDb.LongAsync(
            connection,
            null,
            ErpDb.Positional("SELECT `company_id` FROM `epc_gold_schemes` WHERE `id` = ? LIMIT 1"),
            cancellationToken,
            schemeId).ConfigureAwait(false);
        var months = (int)await ErpDb.LongAsync(
            connection,
            null,
            ErpDb.Positional("SELECT `maturity_months` FROM `epc_gold_schemes` WHERE `id` = ? LIMIT 1"),
            cancellationToken,
            schemeId).ConfigureAwait(false);
        if (months <= 0)
        {
            months = 11;
        }

        var minInst = await ErpDb.DecimalAsync(
            connection,
            null,
            ErpDb.Positional("SELECT `min_installment` FROM `epc_gold_schemes` WHERE `id` = ? LIMIT 1"),
            cancellationToken,
            schemeId).ConfigureAwait(false);
        var installment = request.InstallmentAmount > 0
            ? RoundNonNeg(request.InstallmentAmount, 4)
            : RoundNonNeg(minInst, 4);
        var start = ParseDate(request.StartDate);
        var maturity = start.AddMonths(months);
        var seq = await ErpDb.LongAsync(
            connection,
            null,
            ErpDb.Positional("SELECT COUNT(*) FROM `epc_gold_scheme_enrollments` WHERE `company_id` = ?"),
            cancellationToken,
            companyId).ConfigureAwait(false);
        var enrollNo = "GS-" + start.ToString("yyyyMM", CultureInfo.InvariantCulture) + "-"
                       + (seq + 1).ToString("0000", CultureInfo.InvariantCulture);

        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional(
                "INSERT INTO `epc_gold_scheme_enrollments` (`company_id`,`scheme_id`,`customer_id`,`customer_name`,`enrollment_no`,`installment_amount`,`start_date`,`maturity_date`,`status`,`time_created`) VALUES (?, ?, ?, ?, ?, ?, ?, ?, 'active', ?)"),
            cancellationToken,
            companyId,
            schemeId,
            request.CustomerId < 0 ? 0 : request.CustomerId,
            Clip((request.CustomerName ?? string.Empty).Trim(), 200),
            enrollNo,
            installment,
            start.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            maturity.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            UnixNow()).ConfigureAwait(false);

        var id = await ErpDb.LastInsertIdAsync(connection, null, cancellationToken).ConfigureAwait(false);
        if (id <= 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Failed");
        }

        return ErpSimpleWriteResult.Ok("Enrollment " + enrollNo + " created", id);
    }

    public async Task<ErpGoldSchemeBoardResult> LoadBoardAsync(
        int companyId,
        CancellationToken cancellationToken = default)
    {
        if (!_connections.IsConfigured)
        {
            return new([], "migration", "TenantRegistry DB is not configured.");
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await ErpJewelSchema.EnsureAsync(connection, cancellationToken).ConfigureAwait(false);
        if (!await TableExistsAsync(connection, "epc_gold_schemes", cancellationToken).ConfigureAwait(false)
            || !await ColumnExistsAsync(connection, "epc_gold_schemes", "scheme_code", cancellationToken).ConfigureAwait(false))
        {
            return new([], "database", "Gold scheme tables are not provisioned");
        }

        var rows = new List<ErpGoldSchemeRow>();
        await using var command = connection.CreateCommand();
        command.CommandText = ErpDb.Positional(
            "SELECT `id`,`scheme_code`,`scheme_name`,`scheme_type`,`maturity_months`,`bonus_type`,`min_installment`,`time_created`,`created_by` FROM `epc_gold_schemes` "
            + "WHERE ? = 0 OR `company_id` = ? ORDER BY `id` DESC");
        var co = companyId < 0 ? 0 : companyId;
        ErpDb.AddParameters(command, co, co);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            rows.Add(new ErpGoldSchemeRow(
                reader[0] is DBNull ? 0 : Convert.ToInt64(reader[0], CultureInfo.InvariantCulture),
                Convert.ToString(reader[1], CultureInfo.InvariantCulture) ?? "",
                Convert.ToString(reader[2], CultureInfo.InvariantCulture) ?? "",
                Convert.ToString(reader[3], CultureInfo.InvariantCulture) ?? "",
                reader[4] is DBNull ? 0 : Convert.ToInt32(reader[4], CultureInfo.InvariantCulture),
                Convert.ToString(reader[5], CultureInfo.InvariantCulture) ?? "",
                reader[6] is DBNull ? 0 : Convert.ToDecimal(reader[6], CultureInfo.InvariantCulture),
                ErpJwMasterAudit.DisplayDate(reader[7]),
                ErpJwMasterAudit.DisplayUser(Convert.ToString(reader[8], CultureInfo.InvariantCulture))));
        }

        return new(rows, "database", string.Empty);
    }

    public async Task<ErpSimpleWriteResult> PayAsync(
        ErpJwGoldSchemePayRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.EnrollmentId <= 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Enrollment id is required.");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await ErpJewelSchema.EnsureAsync(connection, cancellationToken).ConfigureAwait(false);
        if (!await TableExistsAsync(connection, "epc_gold_scheme_enrollments", cancellationToken).ConfigureAwait(false)
            || !await TableExistsAsync(connection, "epc_gold_scheme_payments", cancellationToken).ConfigureAwait(false)
            || !await ColumnExistsAsync(connection, "epc_gold_scheme_payments", "amount", cancellationToken).ConfigureAwait(false))
        {
            return ErpSimpleWriteResult.Fail("invalid", "Gold scheme tables are not provisioned");
        }

        var status = await ErpDb.StringAsync(
            connection,
            null,
            ErpDb.Positional("SELECT `status` FROM `epc_gold_scheme_enrollments` WHERE `id` = ? LIMIT 1"),
            cancellationToken,
            request.EnrollmentId).ConfigureAwait(false);
        if (string.IsNullOrEmpty(status) || !string.Equals(status, "active", StringComparison.OrdinalIgnoreCase))
        {
            return ErpSimpleWriteResult.Fail("invalid", "Enrollment is missing or not active.");
        }

        var installment = await ErpDb.DecimalAsync(
            connection,
            null,
            ErpDb.Positional("SELECT `installment_amount` FROM `epc_gold_scheme_enrollments` WHERE `id` = ? LIMIT 1"),
            cancellationToken,
            request.EnrollmentId).ConfigureAwait(false);
        var paidCount = await ErpDb.LongAsync(
            connection,
            null,
            ErpDb.Positional("SELECT `installments_paid` FROM `epc_gold_scheme_enrollments` WHERE `id` = ? LIMIT 1"),
            cancellationToken,
            request.EnrollmentId).ConfigureAwait(false);
        var amount = request.Amount > 0 ? RoundNonNeg(request.Amount, 4) : RoundNonNeg(installment, 4);
        var goldRate = RoundNonNeg(request.GoldRate, 4);
        if (goldRate <= 0)
        {
            var companyId = await ErpDb.LongAsync(
                connection,
                null,
                ErpDb.Positional("SELECT `company_id` FROM `epc_gold_scheme_enrollments` WHERE `id` = ? LIMIT 1"),
                cancellationToken,
                request.EnrollmentId).ConfigureAwait(false);
            goldRate = RoundNonNeg(
                await ErpGoldRateBoard.ResolveSellAsync(
                    connection,
                    companyId > int.MaxValue ? 0 : (int)companyId,
                    "24K",
                    "AED",
                    request.PaymentDate,
                    cancellationToken).ConfigureAwait(false),
                4);
        }

        var grams = goldRate > 0
            ? decimal.Round(amount / goldRate, 6, MidpointRounding.AwayFromZero)
            : 0m;
        var mode = Clip((request.PaymentMode ?? string.Empty).Trim(), 50);
        if (mode.Length == 0)
        {
            mode = "cash";
        }

        var payDate = ParseDate(request.PaymentDate).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        var now = UnixNow();

        await using var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await ErpDb.ExecuteAsync(
                connection,
                transaction,
                ErpDb.Positional(
                    "INSERT INTO `epc_gold_scheme_payments` (`enrollment_id`,`installment_no`,`amount`,`payment_mode`,`gold_rate_at_payment`,`grams_equivalent`,`receipt_no`,`payment_date`,`time_created`) VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?)"),
                cancellationToken,
                request.EnrollmentId,
                paidCount + 1,
                amount,
                mode,
                goldRate,
                grams,
                Clip((request.ReceiptNo ?? string.Empty).Trim(), 64),
                payDate,
                now).ConfigureAwait(false);

            var id = await ErpDb.LastInsertIdAsync(connection, transaction, cancellationToken).ConfigureAwait(false);
            await ErpDb.ExecuteAsync(
                connection,
                transaction,
                ErpDb.Positional(
                    "UPDATE `epc_gold_scheme_enrollments` SET `installments_paid` = `installments_paid` + 1, `total_paid` = `total_paid` + ?, `time_updated` = ? WHERE `id` = ?"),
                cancellationToken,
                amount,
                now,
                request.EnrollmentId).ConfigureAwait(false);

            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            if (id <= 0)
            {
                return ErpSimpleWriteResult.Fail("invalid", "Failed");
            }

            return ErpSimpleWriteResult.Ok("Installment " + (paidCount + 1).ToString(CultureInfo.InvariantCulture) + " paid", id);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            throw;
        }
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

    private static string NormalizeSchemeType(string? raw)
    {
        var value = (raw ?? string.Empty).Trim().ToLowerInvariant();
        return SchemeTypes.Contains(value) ? value : "value";
    }

    private static string NormalizeBonusType(string? raw)
    {
        var value = (raw ?? string.Empty).Trim().ToLowerInvariant().Replace(' ', '_');
        return BonusTypes.Contains(value) ? value : "free_month";
    }

    private static DateTime ParseDate(string? raw)
    {
        var value = (raw ?? string.Empty).Trim();
        if (DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces, out var parsed)
            || DateTime.TryParse(value, CultureInfo.CurrentCulture, DateTimeStyles.AllowWhiteSpaces, out parsed))
        {
            return parsed.Date;
        }

        return DateTime.Now.Date;
    }

    private static int UnixNow()
        => (int)DateTimeOffset.UtcNow.ToUnixTimeSeconds();

    private static decimal RoundNonNeg(decimal value, int decimals)
        => decimal.Round(value < 0 ? 0 : value, decimals, MidpointRounding.AwayFromZero);

    private static string Clip(string value, int maxLen)
        => value.Length <= maxLen ? value : value[..maxLen];
}
