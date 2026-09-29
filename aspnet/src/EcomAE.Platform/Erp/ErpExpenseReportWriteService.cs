using System.Globalization;

namespace EcomAE.Platform.Erp;

/// <summary>Live PHP <c>epc_erp_expense_report_save</c> twin.</summary>
public interface IErpExpenseReportWriteService
{
    Task<ErpSimpleWriteResult> SaveAsync(
        ErpExpenseReportWriteRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record ErpExpenseReportWriteRequest(
    long StaffUserId,
    string? Title,
    decimal TotalAmount,
    string? PeriodFrom,
    string? PeriodTo,
    string? Notes,
    long AdminId);

public sealed class ErpExpenseReportWriteService : IErpExpenseReportWriteService
{
    private readonly IErpWriteConnectionFactory _connections;

    public ErpExpenseReportWriteService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<ErpSimpleWriteResult> SaveAsync(
        ErpExpenseReportWriteRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var title = Clip(request.Title, 255);
        if (title.Length == 0 || request.TotalAmount <= 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Title and amount required");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var periodFrom = ParseDate(request.PeriodFrom, now, endOfDay: false);
        var periodTo = ParseDate(request.PeriodTo, now, endOfDay: true);
        var notes = (request.Notes ?? string.Empty).Trim();

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await EnsureSchemaAsync(connection, cancellationToken).ConfigureAwait(false);
        var lastReport = await ErpDb.StringAsync(
            connection,
            null,
            "SELECT `report_no` FROM `epc_erp_expense_reports` ORDER BY `id` DESC LIMIT 1",
            cancellationToken).ConfigureAwait(false);
        var reportNo = NextReportNo(lastReport);

        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional(
                "INSERT INTO `epc_erp_expense_reports` (`report_no`, `staff_user_id`, `title`, `total_amount`, `status`, `period_from`, `period_to`, `notes`, `admin_id`, `time_created`, `time_updated`) VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)"),
            cancellationToken,
            reportNo,
            request.StaffUserId > 0 ? request.StaffUserId : 0,
            title,
            request.TotalAmount,
            "submitted",
            periodFrom,
            periodTo,
            notes,
            request.AdminId > 0 ? request.AdminId : 0,
            now,
            now).ConfigureAwait(false);

        var id = await ErpDb.LastInsertIdAsync(connection, null, cancellationToken).ConfigureAwait(false);
        return ErpSimpleWriteResult.Ok("Expense report submitted", id);
    }

    private static Task EnsureSchemaAsync(
        System.Data.Common.DbConnection connection,
        CancellationToken cancellationToken)
        => ErpDb.TryExecuteAsync(
            connection,
            """
            CREATE TABLE IF NOT EXISTS `epc_erp_expense_reports` (
                `id` int(11) NOT NULL AUTO_INCREMENT,
                `report_no` varchar(32) NOT NULL,
                `staff_user_id` int(11) NOT NULL DEFAULT 0,
                `title` varchar(255) NOT NULL,
                `total_amount` decimal(14,2) NOT NULL DEFAULT 0.00,
                `status` enum('draft','submitted','approved','paid','rejected') NOT NULL DEFAULT 'draft',
                `period_from` int(11) NOT NULL DEFAULT 0,
                `period_to` int(11) NOT NULL DEFAULT 0,
                `notes` text,
                `cash_entry_id` int(11) NOT NULL DEFAULT 0,
                `admin_id` int(11) NOT NULL DEFAULT 0,
                `time_created` int(11) NOT NULL DEFAULT 0,
                `time_updated` int(11) NOT NULL DEFAULT 0,
                PRIMARY KEY (`id`),
                UNIQUE KEY `x_report_no` (`report_no`),
                KEY `x_staff` (`staff_user_id`),
                KEY `x_status` (`status`)
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8
            """,
            cancellationToken);

    private static long ParseDate(string? value, long fallback, bool endOfDay)
    {
        if (DateTime.TryParseExact(
                (value ?? string.Empty).Trim(),
                "yyyy-MM-dd",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var date))
        {
            var time = endOfDay ? new TimeSpan(23, 59, 59) : TimeSpan.Zero;
            return new DateTimeOffset(date.Date + time, TimeSpan.Zero).ToUnixTimeSeconds();
        }

        return fallback;
    }

    public static string NextReportNo(string? last)
    {
        var digits = new string((last ?? string.Empty).Reverse().TakeWhile(char.IsDigit).Reverse().ToArray());
        var next = int.TryParse(digits, NumberStyles.None, CultureInfo.InvariantCulture, out var number)
            ? number + 1
            : 1;
        return "EXP-" + next.ToString("D5", CultureInfo.InvariantCulture);
    }

    private static string Clip(string? value, int max)
    {
        var text = (value ?? string.Empty).Trim();
        return text.Length <= max ? text : text[..max];
    }
}
