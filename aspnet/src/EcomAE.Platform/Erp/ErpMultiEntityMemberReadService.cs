using System.Data.Common;

namespace EcomAE.Platform.Erp;

public sealed record ErpMultiEntityMemberOption(
    string SiteKey,
    string Name,
    string Currency);

public interface IErpMultiEntityMemberReadService
{
    Task<IReadOnlyList<ErpMultiEntityMemberOption>> LoadAsync(
        CancellationToken cancellationToken = default);
}

public sealed class ErpMultiEntityMemberReadService : IErpMultiEntityMemberReadService
{
    private readonly IErpWriteConnectionFactory _connections;

    public ErpMultiEntityMemberReadService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<IReadOnlyList<ErpMultiEntityMemberOption>> LoadAsync(
        CancellationToken cancellationToken = default)
    {
        if (!_connections.IsConfigured)
        {
            return [];
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        if (!await TableExistsAsync(connection, cancellationToken).ConfigureAwait(false))
        {
            return [];
        }

        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT `site_key`, MAX(`entity_name`) AS entity_name, MAX(`local_currency`) AS local_currency
            FROM `epc_entity_members`
            WHERE `site_key` IS NOT NULL AND `site_key` <> ''
            GROUP BY `site_key`
            ORDER BY `site_key`
            LIMIT 500
            """;
        var rows = new List<ErpMultiEntityMemberOption>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            rows.Add(new(
                Convert.ToString(reader.GetValue(0)) ?? string.Empty,
                Convert.ToString(reader.GetValue(1)) ?? string.Empty,
                Convert.ToString(reader.GetValue(2)) ?? string.Empty));
        }

        return rows;
    }

    private static async Task<bool> TableExistsAsync(
        DbConnection connection,
        CancellationToken cancellationToken)
    {
        var count = await ErpDb.LongAsync(
            connection,
            null,
            ErpDb.Positional(
                "SELECT COUNT(*) FROM information_schema.TABLES WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = ?"),
            cancellationToken,
            "epc_entity_members").ConfigureAwait(false);
        return count > 0;
    }
}
