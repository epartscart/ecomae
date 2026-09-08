using System.Data.Common;
using System.Globalization;
using System.Text.Json;

namespace EcomAE.Platform.Erp;

/// <summary>
/// Live PHP <c>epc_bplan_advance_stage</c> / ajax <c>bplan_advance</c> twin.
/// draft→review→approved is a stage UPDATE. approved→published freezes
/// <c>SUM(line.amount) + SUM(headcount*annual_cost)</c>. Schema ensure stays PHP.
/// Does not CREATE tables.
/// </summary>
public interface IErpBplanAdvanceWriteService
{
    Task<ErpSimpleWriteResult> AdvanceAsync(
        ErpBplanAdvanceWriteRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record ErpBplanAdvanceWriteRequest(long Id = 0);

public sealed class ErpBplanAdvanceWriteService : IErpBplanAdvanceWriteService
{
    public const string PlanNotFound = "Plan not found";
    public const string AlreadyFinal = "Plan is already at the final stage";
    public const string PublishedMessage = "Budget plan published";

    public static readonly string[] Stages = ["draft", "review", "approved", "published"];

    private readonly IErpWriteConnectionFactory _connections;

    public ErpBplanAdvanceWriteService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<ErpSimpleWriteResult> AdvanceAsync(
        ErpBplanAdvanceWriteRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.Id <= 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", PlanNotFound);
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        if (!await ColumnExistsAsync(connection, "epc_bplan_plan", "stage", cancellationToken).ConfigureAwait(false)
            || !await ColumnExistsAsync(connection, "epc_bplan_line", "amount", cancellationToken).ConfigureAwait(false)
            || !await ColumnExistsAsync(connection, "epc_bplan_position", "annual_cost", cancellationToken).ConfigureAwait(false))
        {
            return ErpSimpleWriteResult.Fail("invalid", "Budget plan tables are not provisioned");
        }

        var stage = await ErpDb.StringAsync(
            connection,
            null,
            ErpDb.Positional("SELECT `stage` FROM `epc_bplan_plan` WHERE `id` = ? LIMIT 1"),
            cancellationToken,
            request.Id).ConfigureAwait(false);
        if (stage is null)
        {
            return ErpSimpleWriteResult.Fail("invalid", PlanNotFound);
        }

        var idx = Array.IndexOf(Stages, stage);
        if (idx < 0 || idx >= Stages.Length - 1)
        {
            return ErpSimpleWriteResult.Fail("invalid", AlreadyFinal);
        }

        var next = Stages[idx + 1];
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        if (next == "published")
        {
            var lineTotal = await ErpDb.DecimalAsync(
                connection,
                null,
                ErpDb.Positional("SELECT COALESCE(SUM(`amount`),0) FROM `epc_bplan_line` WHERE `plan_id` = ?"),
                cancellationToken,
                request.Id).ConfigureAwait(false);
            var positionTotal = await ErpDb.DecimalAsync(
                connection,
                null,
                ErpDb.Positional("SELECT COALESCE(SUM(`headcount` * `annual_cost`),0) FROM `epc_bplan_position` WHERE `plan_id` = ?"),
                cancellationToken,
                request.Id).ConfigureAwait(false);
            await ErpDb.ExecuteAsync(
                connection,
                null,
                ErpDb.Positional("UPDATE `epc_bplan_plan` SET `stage` = 'published', `published_total` = ?, `time_updated` = ? WHERE `id` = ?"),
                cancellationToken,
                lineTotal + positionTotal,
                now,
                request.Id).ConfigureAwait(false);
            return ErpSimpleWriteResult.Ok(PublishedMessage, request.Id);
        }

        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional("UPDATE `epc_bplan_plan` SET `stage` = ?, `time_updated` = ? WHERE `id` = ?"),
            cancellationToken,
            next,
            now,
            request.Id).ConfigureAwait(false);
        return ErpSimpleWriteResult.Ok("Budget plan advanced to " + next, request.Id);
    }

    public static long JsonLong(JsonElement root, params string[] names)
    {
        if (root.ValueKind != JsonValueKind.Object)
        {
            return 0;
        }

        foreach (var name in names)
        {
            if (!root.TryGetProperty(name, out var prop))
            {
                continue;
            }

            if (prop.ValueKind == JsonValueKind.Number && prop.TryGetInt64(out var n))
            {
                return n;
            }

            if (prop.ValueKind == JsonValueKind.String
                && long.TryParse(prop.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out n))
            {
                return n;
            }
        }

        return 0;
    }

    public static bool JsonFlag(JsonElement root, params string[] names)
    {
        if (root.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        foreach (var name in names)
        {
            if (!root.TryGetProperty(name, out var prop))
            {
                continue;
            }

            if (prop.ValueKind == JsonValueKind.True)
            {
                return true;
            }

            if (prop.ValueKind == JsonValueKind.Number && prop.TryGetInt64(out var n) && n != 0)
            {
                return true;
            }

            if (prop.ValueKind == JsonValueKind.String
                && !string.IsNullOrEmpty(prop.GetString())
                && prop.GetString() is not "0")
            {
                return true;
            }
        }

        return false;
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
}
