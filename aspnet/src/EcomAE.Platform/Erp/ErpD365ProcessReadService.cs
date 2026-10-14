using System.Globalization;
using EcomAE.Platform.Presentation;

namespace EcomAE.Platform.Erp;

public sealed record ErpD365ParameterRow(
    string Area,
    string Key,
    string Caption,
    string Value,
    string ValueKind,
    string Group,
    bool Customized);

public sealed record ErpD365LinkRow(
    long Id,
    string ProcessCode,
    string FromTab,
    long FromId,
    string FromCode,
    string ToTab,
    long ToId,
    string ToCode,
    string Relation,
    string FieldMap,
    string Note,
    long TimeCreated);

public interface IErpD365ProcessReadService
{
    Task<IReadOnlyList<ErpD365ParameterRow>> ListParametersAsync(
        string area,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ErpD365LinkRow>> ListLinksAsync(
        string tab,
        int limit = 40,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ErpD365StudyNoteRow>> ListStudyNotesAsync(
        string area,
        CancellationToken cancellationToken = default);
}

public sealed record ErpD365StudyNoteRow(
    string Area,
    string Topic,
    string Note,
    long TimeUpdated);

public sealed class ErpD365ProcessReadService : IErpD365ProcessReadService
{
    private readonly IErpWriteConnectionFactory _connections;

    public ErpD365ProcessReadService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<IReadOnlyList<ErpD365ParameterRow>> ListParametersAsync(
        string area,
        CancellationToken cancellationToken = default)
    {
        var defined = ErpD365ProcessCatalog.ParametersForArea(area);
        if (defined.Count == 0)
        {
            return [];
        }

        var stored = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (_connections.IsConfigured)
        {
            await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
            await ErpLazySchema.EnsureD365ParametersAsync(connection, cancellationToken).ConfigureAwait(false);
            await using var command = connection.CreateCommand();
            command.CommandText = ErpDb.Positional(
                "SELECT IFNULL(`param_key`,''),IFNULL(`param_value`,'') FROM `epc_erp_d365_parameters` WHERE `area` = ?");
            ErpDb.AddParameters(command, area);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                stored[Convert.ToString(reader.GetValue(0), CultureInfo.InvariantCulture) ?? string.Empty]
                    = Convert.ToString(reader.GetValue(1), CultureInfo.InvariantCulture) ?? string.Empty;
            }
        }

        return defined.Select(p =>
        {
            var customized = stored.TryGetValue(p.Key, out var value) && !string.IsNullOrWhiteSpace(value);
            return new ErpD365ParameterRow(
                p.Area,
                p.Key,
                p.Caption,
                customized ? value! : p.Default,
                p.ValueKind,
                p.Group,
                customized);
        }).ToList();
    }

    public async Task<IReadOnlyList<ErpD365LinkRow>> ListLinksAsync(
        string tab,
        int limit = 40,
        CancellationToken cancellationToken = default)
    {
        if (!_connections.IsConfigured)
        {
            return [];
        }

        if (limit < 1)
        {
            limit = 40;
        }

        if (limit > 200)
        {
            limit = 200;
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await ErpLazySchema.EnsureD365LinkAsync(connection, cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = ErpDb.Positional(
            "SELECT `id`,IFNULL(`process_code`,''),IFNULL(`from_tab`,''),IFNULL(`from_id`,0),IFNULL(`from_code`,'')," +
            "IFNULL(`to_tab`,''),IFNULL(`to_id`,0),IFNULL(`to_code`,''),IFNULL(`relation`,''),IFNULL(`field_map`,'')," +
            "IFNULL(`note`,''),IFNULL(`time_created`,0) FROM `epc_erp_d365_link` " +
            "WHERE `from_tab` = ? OR `to_tab` = ? ORDER BY `id` DESC LIMIT " +
            limit.ToString(CultureInfo.InvariantCulture));
        ErpDb.AddParameters(command, tab, tab);
        var rows = new List<ErpD365LinkRow>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            rows.Add(new(
                Convert.ToInt64(reader.GetValue(0), CultureInfo.InvariantCulture),
                Convert.ToString(reader.GetValue(1), CultureInfo.InvariantCulture) ?? string.Empty,
                Convert.ToString(reader.GetValue(2), CultureInfo.InvariantCulture) ?? string.Empty,
                Convert.ToInt64(reader.GetValue(3), CultureInfo.InvariantCulture),
                Convert.ToString(reader.GetValue(4), CultureInfo.InvariantCulture) ?? string.Empty,
                Convert.ToString(reader.GetValue(5), CultureInfo.InvariantCulture) ?? string.Empty,
                Convert.ToInt64(reader.GetValue(6), CultureInfo.InvariantCulture),
                Convert.ToString(reader.GetValue(7), CultureInfo.InvariantCulture) ?? string.Empty,
                Convert.ToString(reader.GetValue(8), CultureInfo.InvariantCulture) ?? string.Empty,
                Convert.ToString(reader.GetValue(9), CultureInfo.InvariantCulture) ?? string.Empty,
                Convert.ToString(reader.GetValue(10), CultureInfo.InvariantCulture) ?? string.Empty,
                Convert.ToInt64(reader.GetValue(11), CultureInfo.InvariantCulture)));
        }

        return rows;
    }

    public async Task<IReadOnlyList<ErpD365StudyNoteRow>> ListStudyNotesAsync(
        string area,
        CancellationToken cancellationToken = default)
    {
        if (!_connections.IsConfigured)
        {
            return [];
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await ErpLazySchema.EnsureD365StudyAsync(connection, cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = ErpDb.Positional(
            "SELECT IFNULL(`area`,''),IFNULL(`topic`,''),IFNULL(`note`,''),IFNULL(`time_updated`,0) " +
            "FROM `epc_erp_d365_study` WHERE `area` = ? ORDER BY `topic`");
        ErpDb.AddParameters(command, area);
        var rows = new List<ErpD365StudyNoteRow>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            rows.Add(new(
                Convert.ToString(reader.GetValue(0), CultureInfo.InvariantCulture) ?? string.Empty,
                Convert.ToString(reader.GetValue(1), CultureInfo.InvariantCulture) ?? string.Empty,
                Convert.ToString(reader.GetValue(2), CultureInfo.InvariantCulture) ?? string.Empty,
                Convert.ToInt64(reader.GetValue(3), CultureInfo.InvariantCulture)));
        }

        return rows;
    }
}
