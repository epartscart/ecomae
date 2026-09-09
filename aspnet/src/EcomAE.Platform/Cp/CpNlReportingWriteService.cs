using System.Data.Common;
using System.Text.RegularExpressions;
using EcomAE.Platform.Erp;

namespace EcomAE.Platform.Cp;

/// <summary>
/// Live PHP <c>epc_nl_reporting.php</c> twin of <c>epc_nlr_create_definition</c>,
/// metadata <c>epc_nlr_update_definition</c>, and <c>epc_nlr_delete_definition</c>
/// (soft-deactivate). Query template, recipients, generate, and schema-ensure stay Classic.
/// This service does not invent a send.
/// </summary>
public interface ICpNlReportingWriteService
{
    Task<ErpSimpleWriteResult> SaveAsync(
        CpNlReportingSaveRequest request,
        CancellationToken cancellationToken = default);

    Task<ErpSimpleWriteResult> DeleteAsync(
        long id,
        CancellationToken cancellationToken = default);
}

public sealed record CpNlReportingSaveRequest(
    long Id,
    string? SiteKey,
    string? Name,
    string? Description,
    string? ReportType,
    string? Schedule,
    string? Format,
    bool Active);

public sealed class CpNlReportingWriteService : ICpNlReportingWriteService
{
    private static readonly Regex SiteKeySafe = new("[^a-z0-9_-]", RegexOptions.CultureInvariant | RegexOptions.Compiled);

    public static readonly IReadOnlyList<string> Schedules = ["manual", "hourly", "daily", "weekly", "monthly"];

    public static readonly IReadOnlyList<string> Formats = ["csv", "json", "html", "pdf"];

    private readonly IErpWriteConnectionFactory _connections;

    public CpNlReportingWriteService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public static string NormalizeSiteKey(string? raw)
        => SiteKeySafe.Replace((raw ?? string.Empty).Trim().ToLowerInvariant(), string.Empty);

    public static string NormalizeSchedule(string? raw)
    {
        var schedule = (raw ?? string.Empty).Trim().ToLowerInvariant();
        return Schedules.Contains(schedule, StringComparer.Ordinal) ? schedule : "manual";
    }

    public static string NormalizeFormat(string? raw)
    {
        var format = (raw ?? string.Empty).Trim().ToLowerInvariant();
        return Formats.Contains(format, StringComparer.Ordinal) ? format : "csv";
    }

    public static string Clip(string? raw, int max)
    {
        var text = (raw ?? string.Empty).Trim();
        return text.Length <= max ? text : text[..max];
    }

    public async Task<ErpSimpleWriteResult> SaveAsync(
        CpNlReportingSaveRequest request,
        CancellationToken cancellationToken = default)
    {
        var siteKey = NormalizeSiteKey(request.SiteKey);
        if (siteKey.Length == 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Site key is required");
        }

        var name = Clip(request.Name, 128);
        if (name.Length == 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Name is required");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        var description = Clip(request.Description, 512);
        var reportType = Clip(request.ReportType, 32);
        if (reportType.Length == 0)
        {
            reportType = "custom";
        }

        var schedule = NormalizeSchedule(request.Schedule);
        var format = NormalizeFormat(request.Format);
        var active = request.Active ? 1 : 0;

        try
        {
            await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
            if (request.Id > 0)
            {
                var existing = await ErpDb.LongAsync(
                    connection, null,
                    ErpDb.Positional("SELECT `id` FROM `epc_report_definitions` WHERE `id`=? LIMIT 1"),
                    cancellationToken, request.Id).ConfigureAwait(false);
                if (existing <= 0)
                {
                    return ErpSimpleWriteResult.Fail("not_found", "Report definition not found");
                }

                await ErpDb.ExecuteAsync(
                    connection, null,
                    ErpDb.Positional("UPDATE `epc_report_definitions` SET `site_key`=?, `name`=?, `description`=?, `report_type`=?, `schedule`=?, `format`=?, `active`=? WHERE `id`=?"),
                    cancellationToken,
                    siteKey, name, description, reportType, schedule, format, active, request.Id).ConfigureAwait(false);
                return ErpSimpleWriteResult.Ok("Report definition saved.", request.Id);
            }

            await ErpDb.ExecuteAsync(
                connection, null,
                ErpDb.Positional("INSERT INTO `epc_report_definitions` (`site_key`, `name`, `description`, `report_type`, `query_template`, `parameters`, `schedule`, `format`, `recipients`, `created_by`) VALUES (?, ?, ?, ?, '', '[]', ?, ?, '[]', 0)"),
                cancellationToken,
                siteKey, name, description, reportType, schedule, format).ConfigureAwait(false);
            var id = await ErpDb.LastInsertIdAsync(connection, null, cancellationToken).ConfigureAwait(false);
            return ErpSimpleWriteResult.Ok("Report definition saved.", id);
        }
        catch (DbException)
        {
            return ErpSimpleWriteResult.Fail("db", "NL reporting table is missing — schema-ensure stays Classic.");
        }
    }

    public async Task<ErpSimpleWriteResult> DeleteAsync(
        long id,
        CancellationToken cancellationToken = default)
    {
        if (id <= 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Report definition id is required");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        try
        {
            await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
            var existing = await ErpDb.LongAsync(
                connection, null,
                ErpDb.Positional("SELECT `id` FROM `epc_report_definitions` WHERE `id`=? LIMIT 1"),
                cancellationToken, id).ConfigureAwait(false);
            if (existing <= 0)
            {
                return ErpSimpleWriteResult.Fail("not_found", "Report definition not found");
            }

            await ErpDb.ExecuteAsync(
                connection, null,
                ErpDb.Positional("UPDATE `epc_report_definitions` SET `active`=0 WHERE `id`=?"),
                cancellationToken, id).ConfigureAwait(false);
            return ErpSimpleWriteResult.Ok("Report definition deactivated.", id);
        }
        catch (DbException)
        {
            return ErpSimpleWriteResult.Fail("db", "NL reporting table is missing — schema-ensure stays Classic.");
        }
    }
}
