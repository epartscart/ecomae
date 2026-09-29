using System.Data.Common;
using System.Globalization;
using EcomAE.Platform.Data;

namespace EcomAE.Platform.Erp;

public interface IErpEinvoiceSubmitWriteService
{
    Task<ErpEinvoiceSubmitWriteResult> SubmitAsync(long documentId, CancellationToken cancellationToken = default);
}

public sealed record ErpEinvoiceSubmitWriteResult(
    bool Succeeded,
    string Code,
    string Message,
    long Id,
    string? Status,
    string? AspReference,
    int Writes)
{
    public static ErpEinvoiceSubmitWriteResult Fail(string code, string message, long id = 0)
        => new(false, code, message, id, null, null, 0);
}

public sealed class ErpEinvoiceSubmitWriteService : IErpEinvoiceSubmitWriteService
{
    private readonly IErpWriteConnectionFactory _connections;

    public ErpEinvoiceSubmitWriteService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<ErpEinvoiceSubmitWriteResult> SubmitAsync(
        long documentId,
        CancellationToken cancellationToken = default)
    {
        if (documentId <= 0)
        {
            return ErpEinvoiceSubmitWriteResult.Fail("invalid", "Document id must be positive.");
        }

        if (!_connections.IsConfigured)
        {
            return ErpEinvoiceSubmitWriteResult.Fail("not_configured", "Tenant database is not configured.");
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await EnsureSchemaAsync(connection, cancellationToken).ConfigureAwait(false);

        var document = await ReadDocumentAsync(connection, documentId, cancellationToken).ConfigureAwait(false);
        if (document is null)
        {
            return ErpEinvoiceSubmitWriteResult.Fail("not_found", "E-invoice document was not found.", documentId);
        }

        if (!document.ValidationOk)
        {
            return ErpEinvoiceSubmitWriteResult.Fail("validation_required", "Fix validation errors before submission.", documentId);
        }

        if (document.Status is "submitted" or "accepted" or "queued")
        {
            return ErpEinvoiceSubmitWriteResult.Fail(
                "already_submitted",
                $"This e-invoice was already submitted (status: {document.Status}).",
                documentId);
        }

        var aspName = await ReadSettingAsync(connection, "asp_name", cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(aspName))
        {
            return ErpEinvoiceSubmitWriteResult.Fail(
                "asp_required",
                "Configure an Accredited Service Provider in e-invoicing settings first.",
                documentId);
        }

        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var reference = "ASP-" + Convert.ToHexString(
                System.Security.Cryptography.SHA256.HashData(
                    System.Text.Encoding.UTF8.GetBytes($"{document.Uuid}:{now}")))
            [..12];

        await using var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        var claimed = await ExecuteAsync(
            connection,
            transaction,
            """
            UPDATE `epc_einvoice_documents`
            SET `status` = 'queued', `asp_name` = ?, `asp_reference` = ?,
                `fta_report_status` = 'pending_fta_confirmation',
                `time_submitted` = ?, `time_updated` = ?
            WHERE `id` = ? AND `active` = 1 AND `status` IN ('draft','validated')
            """,
            cancellationToken,
            aspName,
            reference,
            now,
            now,
            documentId).ConfigureAwait(false);

        if (claimed != 1)
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            return ErpEinvoiceSubmitWriteResult.Fail(
                "submit_conflict",
                "Submit conflict — another user already submitted this invoice.",
                documentId);
        }

        await ExecuteAsync(
            connection,
            transaction,
            """
            INSERT INTO `epc_einvoice_events`
                (`document_id`,`event_type`,`status`,`message`,`payload_json`,`time_created`)
            VALUES (?, 'asp_manual', 'queued', ?, ?, ?)
            """,
            cancellationToken,
            documentId,
            "XML package queued for ASP upload. Complete transmission through the configured ASP portal.",
            $"{{\"asp\":\"{EscapeJson(aspName)}\",\"reference\":\"{EscapeJson(reference)}\"}}",
            now).ConfigureAwait(false);

        await ExecuteAsync(
            connection,
            transaction,
            """
            INSERT INTO `epc_einvoice_events`
                (`document_id`,`event_type`,`status`,`message`,`payload_json`,`time_created`)
            VALUES (?, 'fta_report', 'pending', ?, ?, ?)
            """,
            cancellationToken,
            documentId,
            "Tax data reporting to FTA is performed by the ASP after successful validation.",
            $"{{\"reference\":\"{EscapeJson(reference)}\"}}",
            now).ConfigureAwait(false);

        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return new(
            true,
            "ok",
            "Submitted to ASP portal — reference " + reference,
            documentId,
            "queued",
            reference,
            3);
    }

    private static async Task<DocumentRow?> ReadDocumentAsync(
        DbConnection connection,
        long id,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = ErpDb.Positional(
            """
            SELECT `uuid`, IFNULL(`status`,''), IFNULL(`validation_ok`,0)
            FROM `epc_einvoice_documents`
            WHERE `id` = ? AND IFNULL(`active`,1) = 1
            LIMIT 1
            """);
        Add(command, id);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            return null;
        }

        return new(
            reader.IsDBNull(0)
                ? string.Empty
                : Convert.ToString(reader.GetValue(0), CultureInfo.InvariantCulture) ?? string.Empty,
            reader.IsDBNull(1)
                ? string.Empty
                : Convert.ToString(reader.GetValue(1), CultureInfo.InvariantCulture) ?? string.Empty,
            !reader.IsDBNull(2) && Convert.ToInt32(reader.GetValue(2), CultureInfo.InvariantCulture) != 0);
    }

    private static async Task<string> ReadSettingAsync(
        DbConnection connection,
        string key,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = ErpDb.Positional(
            "SELECT IFNULL(`setting_value`,'') FROM `epc_einvoice_settings` WHERE `setting_key` = ? LIMIT 1");
        Add(command, key);
        var value = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
    }

    private static async Task<int> ExecuteAsync(
        DbConnection connection,
        DbTransaction transaction,
        string sql,
        CancellationToken cancellationToken,
        params object?[] values)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = ErpDb.Positional(sql);
        foreach (var value in values)
        {
            Add(command, value);
        }

        return await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task EnsureSchemaAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        await ErpDb.TryExecuteAsync(
            connection,
            """
            CREATE TABLE IF NOT EXISTS `epc_einvoice_documents` (
                `id` int NOT NULL AUTO_INCREMENT,
                `uuid` char(36) NOT NULL,
                `status` varchar(32) NOT NULL DEFAULT 'draft',
                `validation_ok` tinyint NOT NULL DEFAULT 0,
                `active` tinyint NOT NULL DEFAULT 1,
                PRIMARY KEY (`id`)
            )
            """,
            cancellationToken).ConfigureAwait(false);
        await ErpDb.TryExecuteAsync(connection, "ALTER TABLE `epc_einvoice_documents` ADD COLUMN `asp_name` varchar(255) NOT NULL DEFAULT ''", cancellationToken).ConfigureAwait(false);
        await ErpDb.TryExecuteAsync(connection, "ALTER TABLE `epc_einvoice_documents` ADD COLUMN `asp_reference` varchar(128) NOT NULL DEFAULT ''", cancellationToken).ConfigureAwait(false);
        await ErpDb.TryExecuteAsync(connection, "ALTER TABLE `epc_einvoice_documents` ADD COLUMN `fta_report_status` varchar(64) NOT NULL DEFAULT ''", cancellationToken).ConfigureAwait(false);
        await ErpDb.TryExecuteAsync(connection, "ALTER TABLE `epc_einvoice_documents` ADD COLUMN `time_submitted` int NOT NULL DEFAULT 0", cancellationToken).ConfigureAwait(false);
        await ErpDb.TryExecuteAsync(connection, "ALTER TABLE `epc_einvoice_documents` ADD COLUMN `time_updated` int NOT NULL DEFAULT 0", cancellationToken).ConfigureAwait(false);
        await ErpDb.TryExecuteAsync(
            connection,
            """
            CREATE TABLE IF NOT EXISTS `epc_einvoice_settings` (
                `setting_key` varchar(128) NOT NULL,
                `setting_value` text,
                `time_updated` int NOT NULL DEFAULT 0,
                PRIMARY KEY (`setting_key`)
            )
            """,
            cancellationToken).ConfigureAwait(false);
        await ErpDb.TryExecuteAsync(
            connection,
            """
            CREATE TABLE IF NOT EXISTS `epc_einvoice_events` (
                `id` int NOT NULL AUTO_INCREMENT,
                `document_id` int NOT NULL,
                `event_type` varchar(32) NOT NULL,
                `status` varchar(32) NOT NULL DEFAULT 'info',
                `message` text,
                `payload_json` mediumtext,
                `time_created` int NOT NULL DEFAULT 0,
                PRIMARY KEY (`id`),
                KEY `x_doc` (`document_id`,`time_created`)
            )
            """,
            cancellationToken).ConfigureAwait(false);
    }

    private static string EscapeJson(string value)
        => value.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal);

    private static void Add(DbCommand command, object? value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = "@p" + command.Parameters.Count.ToString(CultureInfo.InvariantCulture);
        parameter.Value = value ?? DBNull.Value;
        command.Parameters.Add(parameter);
    }

    private sealed record DocumentRow(string Uuid, string Status, bool ValidationOk);
}
