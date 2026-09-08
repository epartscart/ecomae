using System.Data.Common;
using System.Globalization;
using System.Text.Json;

namespace EcomAE.Platform.Erp;

/// <summary>
/// Live PHP <c>epc_hrt_applicant_set_stage</c> / ajax <c>hrt_applicant_stage</c> twin.
/// UPDATE <c>epc_hrt_applicant.stage</c>; hiring increments <c>epc_hrt_job.hired</c>
/// and may set status <c>filled</c>. Job save, applicant add, reviews, and schema
/// ensure stay PHP. Does not CREATE tables.
/// </summary>
public interface IErpHrtApplicantStageWriteService
{
    Task<ErpSimpleWriteResult> SetAsync(
        ErpHrtApplicantStageWriteRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record ErpHrtApplicantStageWriteRequest(
    long Id = 0,
    string? Stage = null,
    bool StageSpecified = false);

public sealed class ErpHrtApplicantStageWriteService : IErpHrtApplicantStageWriteService
{
    public const string InvalidStage = "Invalid applicant stage";
    public const string ApplicantNotFound = "Applicant not found";

    private static readonly HashSet<string> Stages = new(StringComparer.Ordinal)
    {
        "applied", "screening", "interview", "offer", "hired", "rejected",
    };

    private readonly IErpWriteConnectionFactory _connections;

    public ErpHrtApplicantStageWriteService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<ErpSimpleWriteResult> SetAsync(
        ErpHrtApplicantStageWriteRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!TryResolveStage(request.StageSpecified, request.Stage, out var writeStage, out var error))
        {
            return ErpSimpleWriteResult.Fail("invalid", error);
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        if (!await ColumnExistsAsync(connection, "epc_hrt_applicant", "stage", cancellationToken).ConfigureAwait(false))
        {
            return ErpSimpleWriteResult.Fail("invalid", "Applicant table is not provisioned");
        }

        var needsHire = writeStage == "hired";
        if (needsHire
            && !await ColumnExistsAsync(connection, "epc_hrt_job", "hired", cancellationToken).ConfigureAwait(false))
        {
            return ErpSimpleWriteResult.Fail("invalid", "Job table is not provisioned");
        }

        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        await using var tx = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        string? currentStage = null;
        var jobId = 0L;
        await using (var select = connection.CreateCommand())
        {
            select.Transaction = tx;
            select.CommandText = ErpDb.Positional("SELECT `stage`, `job_id` FROM `epc_hrt_applicant` WHERE `id`=? LIMIT 1");
            ErpDb.AddParameters(select, request.Id);
            await using var reader = await select.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                await tx.RollbackAsync(cancellationToken).ConfigureAwait(false);
                return ErpSimpleWriteResult.Fail("invalid", ApplicantNotFound);
            }

            currentStage = reader.IsDBNull(0)
                ? ""
                : Convert.ToString(reader.GetValue(0), CultureInfo.InvariantCulture) ?? "";
            jobId = reader.IsDBNull(1) ? 0 : Convert.ToInt64(reader.GetValue(1), CultureInfo.InvariantCulture);
        }

        await ErpDb.ExecuteAsync(
            connection,
            tx,
            ErpDb.Positional("UPDATE `epc_hrt_applicant` SET `stage`=?, `time_updated`=? WHERE `id`=?"),
            cancellationToken,
            writeStage,
            now,
            request.Id).ConfigureAwait(false);

        if (needsHire && currentStage != "hired" && jobId != 0)
        {
            var hiredObj = await ErpDb.ScalarAsync(
                connection,
                tx,
                ErpDb.Positional("SELECT `hired` FROM `epc_hrt_job` WHERE `id`=? LIMIT 1"),
                cancellationToken,
                jobId).ConfigureAwait(false);
            if (hiredObj is not null)
            {
                var headcountObj = await ErpDb.ScalarAsync(
                    connection,
                    tx,
                    ErpDb.Positional("SELECT `headcount` FROM `epc_hrt_job` WHERE `id`=? LIMIT 1"),
                    cancellationToken,
                    jobId).ConfigureAwait(false);
                var statusObj = await ErpDb.ScalarAsync(
                    connection,
                    tx,
                    ErpDb.Positional("SELECT `status` FROM `epc_hrt_job` WHERE `id`=? LIMIT 1"),
                    cancellationToken,
                    jobId).ConfigureAwait(false);
                var hired = Convert.ToInt32(hiredObj, CultureInfo.InvariantCulture);
                var headcount = Convert.ToInt32(headcountObj ?? 0, CultureInfo.InvariantCulture);
                var status = Convert.ToString(statusObj ?? "", CultureInfo.InvariantCulture) ?? "";
                var next = NextHire(hired, headcount, status);
                await ErpDb.ExecuteAsync(
                    connection,
                    tx,
                    ErpDb.Positional("UPDATE `epc_hrt_job` SET `hired`=?, `status`=? WHERE `id`=?"),
                    cancellationToken,
                    next.Hired,
                    next.Status,
                    jobId).ConfigureAwait(false);
            }
        }

        await tx.CommitAsync(cancellationToken).ConfigureAwait(false);
        return ErpSimpleWriteResult.Ok(SuccessMessage(request.StageSpecified, request.Stage), request.Id);
    }

    public static bool TryResolveStage(bool specified, string? stage, out string writeStage, out string error)
    {
        writeStage = specified ? (stage ?? "") : "applied";
        if (!Stages.Contains(writeStage))
        {
            error = InvalidStage;
            return false;
        }

        error = "";
        return true;
    }

    public static string SuccessMessage(bool specified, string? stage) =>
        "Applicant moved to " + (specified ? (stage ?? "") : "");

    public static (int Hired, string Status) NextHire(int hired, int headcount, string status)
    {
        hired += 1;
        if (hired >= headcount)
        {
            status = "filled";
        }

        return (hired, status);
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

    public static bool JsonHas(JsonElement root, params string[] names)
    {
        if (root.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        foreach (var name in names)
        {
            if (root.TryGetProperty(name, out _))
            {
                return true;
            }
        }

        return false;
    }

    public static string JsonText(JsonElement root, params string[] names)
    {
        if (root.ValueKind != JsonValueKind.Object)
        {
            return "";
        }

        foreach (var name in names)
        {
            if (!root.TryGetProperty(name, out var prop))
            {
                continue;
            }

            if (prop.ValueKind == JsonValueKind.String)
            {
                return prop.GetString() ?? "";
            }

            if (prop.ValueKind == JsonValueKind.Number)
            {
                return prop.GetRawText();
            }
        }

        return "";
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
