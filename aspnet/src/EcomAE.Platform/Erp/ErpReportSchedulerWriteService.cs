using System.Data.Common;
using System.Globalization;
using System.Text;
using System.Text.Json;

namespace EcomAE.Platform.Erp;

/// <summary>
/// Live PHP <c>epc_report_sched_create</c> twin. Schema-ensure and send/email stay PHP.
/// </summary>
public interface IErpReportSchedulerWriteService
{
    Task<ErpSimpleWriteResult> CreateAsync(
        ErpReportScheduleCreateRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record ErpReportScheduleCreateRequest(
    int CompanyId = 0,
    string? ReportName = null,
    string? ReportType = null,
    string? Frequency = null,
    int DayOfWeek = 1,
    int DayOfMonth = 1,
    string? TimeOfDay = null,
    string? Format = null,
    string? Recipients = null,
    string? CcRecipients = null,
    string? SubjectTemplate = null,
    string? BodyTemplate = null,
    string? Filters = null,
    int CreatedBy = 0);

public sealed class ErpReportSchedulerWriteService : IErpReportSchedulerWriteService
{
    private static readonly HashSet<string> ReportTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "pl", "balance_sheet", "aging", "sales", "inventory", "vat", "custom"
    };

    private static readonly HashSet<string> Frequencies = new(StringComparer.OrdinalIgnoreCase)
    {
        "daily", "weekly", "monthly", "quarterly"
    };

    private static readonly HashSet<string> Formats = new(StringComparer.OrdinalIgnoreCase)
    {
        "pdf", "excel", "csv", "html"
    };

    private readonly IErpWriteConnectionFactory _connections;

    public ErpReportSchedulerWriteService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<ErpSimpleWriteResult> CreateAsync(
        ErpReportScheduleCreateRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var name = Clip((request.ReportName ?? string.Empty).Trim(), 200);
        if (name.Length == 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Report name is required.");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        if (!await TableExistsAsync(connection, "epc_report_schedules", cancellationToken).ConfigureAwait(false)
            || !await ColumnExistsAsync(connection, "epc_report_schedules", "report_name", cancellationToken).ConfigureAwait(false))
        {
            return ErpSimpleWriteResult.Fail("invalid", "Report schedule tables are not provisioned");
        }

        var hasRecipients = await ColumnExistsAsync(connection, "epc_report_schedules", "recipients", cancellationToken)
            .ConfigureAwait(false);
        var hasCc = await ColumnExistsAsync(connection, "epc_report_schedules", "cc_recipients", cancellationToken)
            .ConfigureAwait(false);
        var hasSubject = await ColumnExistsAsync(connection, "epc_report_schedules", "subject_template", cancellationToken)
            .ConfigureAwait(false);
        var hasBody = await ColumnExistsAsync(connection, "epc_report_schedules", "body_template", cancellationToken)
            .ConfigureAwait(false);
        var hasFilters = await ColumnExistsAsync(connection, "epc_report_schedules", "filters", cancellationToken)
            .ConfigureAwait(false);
        var hasCreatedBy = await ColumnExistsAsync(connection, "epc_report_schedules", "created_by", cancellationToken)
            .ConfigureAwait(false);

        var columns = new StringBuilder(
            "`company_id`,`report_name`,`report_type`,`frequency`,`day_of_week`,`day_of_month`,`time_of_day`,`format`");
        var placeholders = new StringBuilder("?, ?, ?, ?, ?, ?, ?, ?");
        var values = new List<object?>
        {
            request.CompanyId < 0 ? 0 : request.CompanyId,
            name,
            NormalizeType(request.ReportType),
            NormalizeFrequency(request.Frequency),
            ClampDay(request.DayOfWeek, 1, 7, 1),
            ClampDay(request.DayOfMonth, 1, 31, 1),
            NormalizeTime(request.TimeOfDay),
            NormalizeFormat(request.Format)
        };

        if (hasRecipients)
        {
            columns.Append(",`recipients`");
            placeholders.Append(", ?");
            values.Add(EncodeList(request.Recipients));
        }

        if (hasCc)
        {
            columns.Append(",`cc_recipients`");
            placeholders.Append(", ?");
            values.Add(EncodeList(request.CcRecipients));
        }

        if (hasSubject)
        {
            columns.Append(",`subject_template`");
            placeholders.Append(", ?");
            values.Add(Clip((request.SubjectTemplate ?? string.Empty).Trim(), 300));
        }

        if (hasBody)
        {
            columns.Append(",`body_template`");
            placeholders.Append(", ?");
            values.Add(request.BodyTemplate ?? string.Empty);
        }

        if (hasFilters)
        {
            columns.Append(",`filters`");
            placeholders.Append(", ?");
            values.Add(EncodeObject(request.Filters));
        }

        columns.Append(",`is_active`");
        placeholders.Append(", ?");
        values.Add(1);

        if (hasCreatedBy)
        {
            columns.Append(",`created_by`");
            placeholders.Append(", ?");
            values.Add(request.CreatedBy < 0 ? 0 : request.CreatedBy);
        }

        columns.Append(",`time_created`");
        placeholders.Append(", ?");
        values.Add(UnixNow());

        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional("INSERT INTO `epc_report_schedules` (" + columns + ") VALUES (" + placeholders + ")"),
            cancellationToken,
            values.ToArray()).ConfigureAwait(false);

        var id = await ErpDb.LastInsertIdAsync(connection, null, cancellationToken).ConfigureAwait(false);
        if (id <= 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Failed");
        }

        return ErpSimpleWriteResult.Ok("Report schedule " + name + " created", id);
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

    private static string NormalizeType(string? raw)
    {
        var value = (raw ?? string.Empty).Trim().ToLowerInvariant().Replace('-', '_');
        return ReportTypes.Contains(value) ? value : "custom";
    }

    private static string NormalizeFrequency(string? raw)
    {
        var value = (raw ?? string.Empty).Trim().ToLowerInvariant();
        return Frequencies.Contains(value) ? value : "monthly";
    }

    private static string NormalizeFormat(string? raw)
    {
        var value = (raw ?? string.Empty).Trim().ToLowerInvariant();
        return Formats.Contains(value) ? value : "pdf";
    }

    private static string NormalizeTime(string? raw)
    {
        var value = (raw ?? string.Empty).Trim();
        if (TimeSpan.TryParse(value, CultureInfo.InvariantCulture, out var parsed))
        {
            return parsed.ToString(@"hh\:mm", CultureInfo.InvariantCulture);
        }

        return "08:00";
    }

    private static int ClampDay(int value, int min, int max, int fallback)
    {
        if (value < min || value > max)
        {
            return fallback;
        }

        return value;
    }

    private static string EncodeList(string? raw)
    {
        var items = SplitList(raw);
        return JsonSerializer.Serialize(items);
    }

    private static string EncodeObject(string? raw)
    {
        var value = (raw ?? string.Empty).Trim();
        if (value.Length == 0)
        {
            return "[]";
        }

        try
        {
            using var doc = JsonDocument.Parse(value);
            return value;
        }
        catch (JsonException)
        {
            return JsonSerializer.Serialize(value);
        }
    }

    private static List<string> SplitList(string? raw)
    {
        var items = new List<string>();
        var value = (raw ?? string.Empty).Trim();
        if (value.Length == 0)
        {
            return items;
        }

        if (value.StartsWith('[') || value.StartsWith('{'))
        {
            try
            {
                using var doc = JsonDocument.Parse(value);
                if (doc.RootElement.ValueKind == JsonValueKind.Array)
                {
                    foreach (var el in doc.RootElement.EnumerateArray())
                    {
                        var item = el.ValueKind == JsonValueKind.String ? el.GetString() : el.ToString();
                        if (!string.IsNullOrWhiteSpace(item))
                        {
                            items.Add(item.Trim());
                        }
                    }

                    return items;
                }
            }
            catch (JsonException)
            {
                // fall through to delimiter split
            }
        }

        foreach (var part in value.Split([',', ';', '\n', '\r', '\t'], StringSplitOptions.RemoveEmptyEntries))
        {
            var item = part.Trim();
            if (item.Length > 0)
            {
                items.Add(item);
            }
        }

        return items;
    }

    private static int UnixNow()
        => (int)DateTimeOffset.UtcNow.ToUnixTimeSeconds();

    private static string Clip(string value, int maxLen)
        => value.Length <= maxLen ? value : value[..maxLen];
}
