using System.Data.Common;
using System.Globalization;

namespace EcomAE.Platform.Erp;

public sealed record ErpD365WorkspaceRow(
    long Id,
    string Area,
    string Tab,
    string Code,
    string Name,
    string Worker,
    string Party,
    string Kind,
    string Status,
    string Period,
    string DateFrom,
    string DateTo,
    decimal Qty,
    decimal Amount,
    string Extra,
    string Note,
    long TimeCreated);

public interface IErpD365WorkspaceReadService
{
    Task<IReadOnlyList<ErpD365WorkspaceRow>> ListAsync(
        string area,
        string tab,
        int limit = 80,
        CancellationToken cancellationToken = default);
}

public sealed class ErpD365WorkspaceReadService : IErpD365WorkspaceReadService
{
    private readonly IErpWriteConnectionFactory _connections;

    public ErpD365WorkspaceReadService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<IReadOnlyList<ErpD365WorkspaceRow>> ListAsync(
        string area,
        string tab,
        int limit = 80,
        CancellationToken cancellationToken = default)
    {
        if (!_connections.IsConfigured)
        {
            return [];
        }

        if (limit < 1)
        {
            limit = 80;
        }

        if (limit > 200)
        {
            limit = 200;
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await ErpLazySchema.EnsureD365WorkspaceAsync(connection, cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = ErpDb.Positional(
            "SELECT `id`,IFNULL(`area`,''),IFNULL(`tab`,''),IFNULL(`code`,''),IFNULL(`name`,'')," +
            "IFNULL(`worker`,''),IFNULL(`party`,''),IFNULL(`kind`,''),IFNULL(`status`,''),IFNULL(`period`,'')," +
            "IFNULL(`date_from`,''),IFNULL(`date_to`,''),IFNULL(`qty`,0),IFNULL(`amount`,0)," +
            "IFNULL(`extra`,''),IFNULL(`note`,''),IFNULL(`time_created`,0) " +
            "FROM `epc_erp_d365_workspace` WHERE `area` = ? AND `tab` = ? ORDER BY `id` DESC LIMIT " +
            limit.ToString(CultureInfo.InvariantCulture));
        ErpDb.AddParameters(command, area, tab);
        var rows = new List<ErpD365WorkspaceRow>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            rows.Add(new(
                Convert.ToInt64(reader.GetValue(0), CultureInfo.InvariantCulture),
                Convert.ToString(reader.GetValue(1), CultureInfo.InvariantCulture) ?? string.Empty,
                Convert.ToString(reader.GetValue(2), CultureInfo.InvariantCulture) ?? string.Empty,
                Convert.ToString(reader.GetValue(3), CultureInfo.InvariantCulture) ?? string.Empty,
                Convert.ToString(reader.GetValue(4), CultureInfo.InvariantCulture) ?? string.Empty,
                Convert.ToString(reader.GetValue(5), CultureInfo.InvariantCulture) ?? string.Empty,
                Convert.ToString(reader.GetValue(6), CultureInfo.InvariantCulture) ?? string.Empty,
                Convert.ToString(reader.GetValue(7), CultureInfo.InvariantCulture) ?? string.Empty,
                Convert.ToString(reader.GetValue(8), CultureInfo.InvariantCulture) ?? string.Empty,
                Convert.ToString(reader.GetValue(9), CultureInfo.InvariantCulture) ?? string.Empty,
                Convert.ToString(reader.GetValue(10), CultureInfo.InvariantCulture) ?? string.Empty,
                Convert.ToString(reader.GetValue(11), CultureInfo.InvariantCulture) ?? string.Empty,
                Convert.ToDecimal(reader.GetValue(12), CultureInfo.InvariantCulture),
                Convert.ToDecimal(reader.GetValue(13), CultureInfo.InvariantCulture),
                Convert.ToString(reader.GetValue(14), CultureInfo.InvariantCulture) ?? string.Empty,
                Convert.ToString(reader.GetValue(15), CultureInfo.InvariantCulture) ?? string.Empty,
                Convert.ToInt64(reader.GetValue(16), CultureInfo.InvariantCulture)));
        }

        return rows;
    }
}
