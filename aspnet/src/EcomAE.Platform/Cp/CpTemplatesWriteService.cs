using System.Data.Common;
using EcomAE.Platform.Erp;

namespace EcomAE.Platform.Cp;

public sealed record CpTemplatesSetCurrentRequest(long TemplateId);

public interface ICpTemplatesWriteService
{
    Task<ErpSimpleWriteResult> SetCurrentAsync(CpTemplatesSetCurrentRequest request, CancellationToken cancellationToken = default);
}

/// <summary>Live PHP <c>templates_manager.php</c> <c>templates_action_type=set_current</c>. Delete and generate_style stay Classic.</summary>
public sealed class CpTemplatesWriteService : ICpTemplatesWriteService
{
    private readonly IErpWriteConnectionFactory _connections;

    public CpTemplatesWriteService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<ErpSimpleWriteResult> SetCurrentAsync(CpTemplatesSetCurrentRequest request, CancellationToken cancellationToken = default)
    {
        if (request.TemplateId <= 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "A template id is required.");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        try
        {
            await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
            if (!await TableExistsAsync(connection, "templates", cancellationToken).ConfigureAwait(false))
            {
                return ErpSimpleWriteResult.Fail("invalid", "templates table is not provisioned. Schema ensure stays on the Classic twin.");
            }

            var exists = await ErpDb.LongAsync(
                connection,
                null,
                ErpDb.Positional("SELECT COUNT(*) FROM `templates` WHERE `id` = ?"),
                cancellationToken,
                request.TemplateId).ConfigureAwait(false);
            if (exists <= 0)
            {
                return ErpSimpleWriteResult.Fail("not_found", "Template was not found.");
            }

            var isFrontend = (int)await ErpDb.LongAsync(
                connection,
                null,
                ErpDb.Positional("SELECT IFNULL(`is_frontend`, 0) FROM `templates` WHERE `id` = ? LIMIT 1"),
                cancellationToken,
                request.TemplateId).ConfigureAwait(false);
            var already = await ErpDb.LongAsync(
                connection,
                null,
                ErpDb.Positional("SELECT IFNULL(`current`, 0) FROM `templates` WHERE `id` = ? LIMIT 1"),
                cancellationToken,
                request.TemplateId).ConfigureAwait(false);
            if (already != 0)
            {
                return ErpSimpleWriteResult.Ok("This template is already current.", request.TemplateId);
            }

            await using var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
            await ErpDb.ExecuteAsync(
                connection,
                transaction,
                ErpDb.Positional("UPDATE `templates` SET `current` = 0 WHERE `is_frontend` = ?"),
                cancellationToken,
                isFrontend).ConfigureAwait(false);
            var writes = await ErpDb.ExecuteAsync(
                connection,
                transaction,
                ErpDb.Positional("UPDATE `templates` SET `current` = 1 WHERE `id` = ?"),
                cancellationToken,
                request.TemplateId).ConfigureAwait(false);
            if (writes <= 0)
            {
                await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                return ErpSimpleWriteResult.Fail("unchanged", "Current template was not updated.");
            }

            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return ErpSimpleWriteResult.Ok("Current template updated.", request.TemplateId);
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
