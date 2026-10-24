using System.Data.Common;
using System.Globalization;

namespace EcomAE.Platform.Erp;

/// <summary>
/// Bridge from the manufacturing workspace into the general ledger: posts the
/// work-order journals the production pages produce so the income statement,
/// balance sheet and dashboard insight figures include factory activity —
/// material issue (Dr WIP / Cr raw material), conversion cost (Dr WIP /
/// Cr labour + overhead applied) and finished-goods receipt (Dr FG / Cr WIP),
/// per IAS 2 cost-capsule accounting.
/// Idempotent — rows already journaled under their MFG-* reference are skipped.
/// Mirrors <see cref="ErpAutoPartsGlPostWriteService"/>.
/// </summary>
public interface IErpMfgGlPostWriteService
{
    Task<string> PostAsync(long workOrderId, string? period, CancellationToken cancellationToken = default);
}

public static class ErpMfgIfrs
{
    public const string RawMaterialAccount = "1300";
    public const string WipAccount = "1310";
    public const string FinishedGoodsAccount = "1320";
    public const string LabourPayableAccount = "2030";
    public const string OverheadAppliedAccount = "2040";

    /// <summary>Accounts the manufacturing bridge ensures exist in the shared COA.</summary>
    public static readonly IReadOnlyList<(string Code, string Name, string Type, string Side, string Description, string Statement)> Accounts =
    [
        (WipAccount, "Work-in-progress inventory", "asset", "debit", "Materials + conversion cost absorbed into open work orders", "balance_sheet"),
        (FinishedGoodsAccount, "Finished goods inventory", "asset", "debit", "Completed production at absorbed cost", "balance_sheet"),
        (LabourPayableAccount, "Direct labour payable", "liability", "credit", "Labour cost absorbed by production, payable to payroll/expense", "balance_sheet"),
        (OverheadAppliedAccount, "Manufacturing overhead applied", "liability", "credit", "Overhead absorbed into WIP, cleared against actual overhead", "balance_sheet"),
    ];

    /// <summary>Material issue to the floor: Dr WIP / Cr raw material inventory.</summary>
    public static IReadOnlyList<ErpAutoPartsIfrs.JournalLine> MaterialIssue(decimal amount)
        =>
        [
            new(WipAccount, amount, 0m, "Material issued to work order"),
            new(RawMaterialAccount, 0m, amount, "Raw material consumed"),
        ];

    /// <summary>Conversion cost absorption: Dr WIP / Cr labour payable / Cr overhead applied.</summary>
    public static IReadOnlyList<ErpAutoPartsIfrs.JournalLine> ConversionCost(decimal labour, decimal overhead)
    {
        var lines = new List<ErpAutoPartsIfrs.JournalLine>();
        var total = labour + overhead;
        if (total <= 0)
        {
            return lines;
        }

        lines.Add(new ErpAutoPartsIfrs.JournalLine(WipAccount, total, 0m, "Conversion cost absorbed"));
        if (labour > 0)
        {
            lines.Add(new ErpAutoPartsIfrs.JournalLine(LabourPayableAccount, 0m, labour, "Direct labour applied"));
        }
        if (overhead > 0)
        {
            lines.Add(new ErpAutoPartsIfrs.JournalLine(OverheadAppliedAccount, 0m, overhead, "Overhead applied"));
        }
        return lines;
    }

    /// <summary>Finished-goods receipt: Dr FG inventory / Cr WIP.</summary>
    public static IReadOnlyList<ErpAutoPartsIfrs.JournalLine> FinishedReceipt(decimal totalCost)
        =>
        [
            new(FinishedGoodsAccount, totalCost, 0m, "Completed goods received into stock"),
            new(WipAccount, 0m, totalCost, "WIP relieved on completion"),
        ];
}

public sealed class ErpMfgGlPostWriteService : IErpMfgGlPostWriteService
{
    private readonly IErpWriteConnectionFactory _connections;
    private readonly IErpGlPostingService _gl;

    public ErpMfgGlPostWriteService(IErpWriteConnectionFactory connections, IErpGlPostingService gl)
    {
        _connections = connections;
        _gl = gl;
    }

    public async Task<string> PostAsync(long workOrderId, string? period, CancellationToken cancellationToken = default)
    {
        if (!_connections.IsConfigured)
        {
            return "TenantRegistry DB is not configured — GL posting skipped.";
        }

        if (!string.IsNullOrWhiteSpace(period)
            && !DateOnly.TryParseExact(period.Trim(), "yyyy-MM", CultureInfo.InvariantCulture, DateTimeStyles.None, out _))
        {
            return "Invalid period — use YYYY-MM or leave it empty.";
        }

        var (fromUtc, toUtc) = PeriodWindow(period);
        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await _gl.EnsureSchemaAsync(connection, cancellationToken).ConfigureAwait(false);
        await EnsureCoaAsync(connection, cancellationToken).ConfigureAwait(false);
        var companyId = await ErpLazySchema.ResolveActiveCompanyIdAsync(connection, 0, cancellationToken).ConfigureAwait(false);

        var issued = 0;
        var conversion = 0;
        var finished = 0;
        var skipped = 0;

        var orders = new List<(long Id, string No, string Status, decimal Material, decimal Labour, decimal Overhead, long Date)>();
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = ErpDb.Positional(
                "SELECT `id`,`wo_no`,`status`,`material_cost`,`labour_cost`,`overhead_cost`,`time_updated`"
                + " FROM `epc_mfg_work_orders`"
                + " WHERE `status` IN ('in_progress','completed')"
                + " AND (?<=0 OR `id`=?) AND (?=0 OR `time_updated`>=?) AND (?=0 OR `time_updated`<?) ORDER BY `id`");
            ErpDb.AddParameters(command, workOrderId, workOrderId, fromUtc, fromUtc, toUtc, toUtc);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                orders.Add((reader.GetInt64(0), reader.GetString(1), reader.GetString(2),
                    reader.GetDecimal(3), reader.GetDecimal(4), reader.GetDecimal(5), reader.GetInt64(6)));
            }
        }

        foreach (var wo in orders)
        {
            var baseRef = "MFG-WO-" + wo.Id.ToString(CultureInfo.InvariantCulture);

            if (wo.Material > 0)
            {
                var issueRef = baseRef + "-ISS";
                if (await ErpAutoPartsGl.PostedJournalIdAsync(connection, issueRef, cancellationToken).ConfigureAwait(false) > 0)
                {
                    skipped++;
                }
                else if (await ErpAutoPartsGl.PostAsync(_gl, connection, null, 0, companyId, wo.Id, issueRef,
                    "Material issued — " + wo.No, ErpMfgIfrs.MaterialIssue(wo.Material), wo.Date, cancellationToken).ConfigureAwait(false) > 0)
                {
                    issued++;
                }
            }

            if (wo.Status == "completed")
            {
                var convRef = baseRef + "-CONV";
                var convRecipe = ErpMfgIfrs.ConversionCost(wo.Labour, wo.Overhead);
                if (convRecipe.Count > 0)
                {
                    if (await ErpAutoPartsGl.PostedJournalIdAsync(connection, convRef, cancellationToken).ConfigureAwait(false) > 0)
                    {
                        skipped++;
                    }
                    else if (await ErpAutoPartsGl.PostAsync(_gl, connection, null, 0, companyId, wo.Id, convRef,
                        "Conversion cost — " + wo.No, convRecipe, wo.Date, cancellationToken).ConfigureAwait(false) > 0)
                    {
                        conversion++;
                    }
                }

                var total = wo.Material + wo.Labour + wo.Overhead;
                var fgRef = baseRef + "-FG";
                if (total > 0)
                {
                    if (await ErpAutoPartsGl.PostedJournalIdAsync(connection, fgRef, cancellationToken).ConfigureAwait(false) > 0)
                    {
                        skipped++;
                    }
                    else if (await ErpAutoPartsGl.PostAsync(_gl, connection, null, 0, companyId, wo.Id, fgRef,
                        "Finished goods receipt — " + wo.No, ErpMfgIfrs.FinishedReceipt(total), wo.Date, cancellationToken).ConfigureAwait(false) > 0)
                    {
                        finished++;
                    }
                }
            }
        }

        return "Posted to GL: " + issued + " material-issue journals, " + conversion + " conversion-cost journals, "
            + finished + " finished-goods journals"
            + (skipped > 0 ? " (" + skipped + " already posted skipped)" : "")
            + " — income statement, balance sheet and dashboard figures now include the manufacturing numbers.";
    }

    private static async Task EnsureCoaAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        foreach (var row in ErpMfgIfrs.Accounts)
        {
            await ErpDb.ExecuteAsync(
                connection,
                null,
                ErpDb.Positional(
                    "INSERT IGNORE INTO `epc_erp_coa_accounts` (`code`, `name`, `account_type`, `normal_side`, `description`, `system_flag`, `time_created`)"
                    + " VALUES (?, ?, ?, ?, ?, 1, ?)"),
                cancellationToken,
                row.Code,
                row.Name,
                row.Type,
                row.Side,
                row.Description,
                now).ConfigureAwait(false);
        }
    }

    private static (long FromUtc, long ToUtc) PeriodWindow(string? period)
    {
        if (!string.IsNullOrWhiteSpace(period)
            && DateOnly.TryParseExact(period.Trim(), "yyyy-MM", CultureInfo.InvariantCulture, DateTimeStyles.None, out var month))
        {
            var from = new DateTimeOffset(month.Year, month.Month, 1, 0, 0, 0, TimeSpan.Zero).ToUnixTimeSeconds();
            var to = new DateTimeOffset(month.Year, month.Month, 1, 0, 0, 0, TimeSpan.Zero).AddMonths(1).ToUnixTimeSeconds();
            return (from, to);
        }
        return (0, 0);
    }
}
