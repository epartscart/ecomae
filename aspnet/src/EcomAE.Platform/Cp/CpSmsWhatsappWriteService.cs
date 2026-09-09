using System.Data.Common;
using System.Text.Encodings.Web;
using System.Text.Json;
using EcomAE.Platform.Erp;

namespace EcomAE.Platform.Cp;

public sealed record CpSmsActivateRequest(long SystemId, string? ParametersValues);

public interface ICpSmsWhatsappWriteService
{
    Task<ErpSimpleWriteResult> ActivateAsync(CpSmsActivateRequest request, CancellationToken cancellationToken = default);
}

/// <summary>Live PHP <c>sms_turning.php</c> <c>save_action</c>. Activate one operator or deactivate all. Blank / empty JSON keeps saved <c>parameters_values</c>.</summary>
public sealed class CpSmsWhatsappWriteService : ICpSmsWhatsappWriteService
{
    public const int ParametersValuesMaxLength = 32_768;

    private static readonly JsonSerializerOptions CompactJson = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        WriteIndented = false,
    };

    private readonly IErpWriteConnectionFactory _connections;

    public CpSmsWhatsappWriteService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    /// <summary>
    /// Blank, whitespace, or <c>{}</c> means keep the stored secret JSON.
    /// A JSON object with keys replaces the stored blob (PHP save_action).
    /// </summary>
    public static bool TryNormalizeParametersValues(string? raw, out string? json, out string? error)
    {
        json = null;
        error = null;
        var text = (raw ?? string.Empty).Trim();
        if (text.Length == 0)
        {
            return true;
        }

        if (text.Length > ParametersValuesMaxLength)
        {
            error = "parameters_values is too large.";
            return false;
        }

        try
        {
            using var doc = JsonDocument.Parse(text);
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
            {
                error = "parameters_values must be a JSON object.";
                return false;
            }

            if (!doc.RootElement.EnumerateObject().MoveNext())
            {
                return true;
            }

            json = JsonSerializer.Serialize(doc.RootElement, CompactJson);
            return true;
        }
        catch (JsonException)
        {
            error = "parameters_values is not valid JSON.";
            return false;
        }
    }

    public async Task<ErpSimpleWriteResult> ActivateAsync(CpSmsActivateRequest request, CancellationToken cancellationToken = default)
    {
        if (request.SystemId < 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "system_id must be 0 (none) or a positive operator id.");
        }

        if (!TryNormalizeParametersValues(request.ParametersValues, out var parametersJson, out var parametersError))
        {
            return ErpSimpleWriteResult.Fail("invalid", parametersError ?? "parameters_values is not valid JSON.");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        try
        {
            await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
            if (!await TableExistsAsync(connection, "sms_api", cancellationToken).ConfigureAwait(false))
            {
                return ErpSimpleWriteResult.Fail("invalid", "sms_api table is not provisioned. Schema ensure stays on the Classic twin.");
            }

            if (request.SystemId == 0)
            {
                await using var offTx = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
                await ErpDb.ExecuteAsync(
                    connection,
                    offTx,
                    "UPDATE `sms_api` SET `active` = 0",
                    cancellationToken).ConfigureAwait(false);
                await offTx.CommitAsync(cancellationToken).ConfigureAwait(false);
                return ErpSimpleWriteResult.Ok("SMS operator deactivated. Messages will not be sent until one is activated.", 0);
            }

            var found = await ErpDb.LongAsync(
                connection,
                null,
                ErpDb.Positional("SELECT `id` FROM `sms_api` WHERE `id` = ? AND IFNULL(`control_available`, 0) = 1 LIMIT 1"),
                cancellationToken,
                request.SystemId).ConfigureAwait(false);
            if (found <= 0)
            {
                return ErpSimpleWriteResult.Fail("not_found", "Operator was not found or is not available for control.");
            }

            await using var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
            await ErpDb.ExecuteAsync(
                connection,
                transaction,
                "UPDATE `sms_api` SET `active` = 0",
                cancellationToken).ConfigureAwait(false);

            int writes;
            if (parametersJson is null)
            {
                writes = await ErpDb.ExecuteAsync(
                    connection,
                    transaction,
                    ErpDb.Positional("UPDATE `sms_api` SET `active` = 1 WHERE `id` = ?"),
                    cancellationToken,
                    request.SystemId).ConfigureAwait(false);
            }
            else
            {
                writes = await ErpDb.ExecuteAsync(
                    connection,
                    transaction,
                    ErpDb.Positional("UPDATE `sms_api` SET `active` = 1, `parameters_values` = ? WHERE `id` = ?"),
                    cancellationToken,
                    parametersJson,
                    request.SystemId).ConfigureAwait(false);
            }

            if (writes <= 0)
            {
                await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                return ErpSimpleWriteResult.Fail("unchanged", "SMS operator was not updated.");
            }

            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return ErpSimpleWriteResult.Ok(
                parametersJson is null
                    ? "SMS operator activated. Saved credentials were kept."
                    : "SMS operator activated and credentials updated.",
                request.SystemId);
        }
        catch (Exception ex)
        {
            return ErpSimpleWriteResult.Fail("db", ex.Message);
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
}
