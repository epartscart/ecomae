using System.Data.Common;
using System.Globalization;

namespace EcomAE.Platform.Erp;

/// <summary>
/// One generic workspace tab's data backing: the headers, the SQL that produces
/// real rows, the empty hint, an optional deep link to the dedicated page, and
/// an optional lazy-schema ensure to run first (self-heals missing tables).
/// </summary>
public sealed record ErpWorkspaceSpec(
    IReadOnlyList<string> Headers,
    string Sql,
    string EmptyHint,
    string? ManageHref,
    Func<DbConnection, CancellationToken, Task>? Ensure = null);

/// <summary>
/// Real read model for EVERY generic D365 workspace tab. Merges the per-area
/// spec maps (PIM, purchasing, warehouse, ...) — tabs without a spec keep the
/// legacy generic workspace grid (cases, setup masters, scratch actions).
/// </summary>
public interface IErpWorkspaceReadService
{
    bool IsDataTab(string? tab);
    Task<ErpPimWorkspaceData?> LoadAsync(string? tab, CancellationToken cancellationToken = default);
}

public sealed class ErpWorkspaceReadService : IErpWorkspaceReadService
{
    private readonly IErpWriteConnectionFactory _connections;

    public ErpWorkspaceReadService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public static readonly IReadOnlyDictionary<string, ErpWorkspaceSpec> Specs = BuildSpecs();

    private static IReadOnlyDictionary<string, ErpWorkspaceSpec> BuildSpecs()
    {
        var map = new Dictionary<string, ErpWorkspaceSpec>(StringComparer.OrdinalIgnoreCase);
        foreach (var (tab, spec) in ErpPimWorkspaceReadService.Specs)
        {
            map[tab] = new ErpWorkspaceSpec(spec.Headers, spec.Sql, spec.EmptyHint, spec.ManageHref,
                ErpLazySchema.EnsureInventoryAsync);
        }

        foreach (var (tab, spec) in ErpPurchasingWorkspaceSpecs.Tabs)
        {
            map[tab] = spec;
        }

        foreach (var (tab, spec) in ErpWarehouseWorkspaceSpecs.Tabs)
        {
            map[tab] = new ErpWorkspaceSpec(spec.Headers, spec.Sql, spec.EmptyHint, spec.ManageHref,
                spec.Ensure ?? ErpWarehouseWorkspaceSpecs.EnsureWarehouseAsync);
        }

        foreach (var (tab, spec) in ErpProductionWorkspaceSpecs.Tabs)
        {
            map[tab] = new ErpWorkspaceSpec(spec.Headers, spec.Sql, spec.EmptyHint, spec.ManageHref,
                spec.Ensure ?? ErpProductionWorkspaceSpecs.EnsureProductionAsync);
        }

        foreach (var (tab, spec) in ErpMasterPlanningWorkspaceSpecs.Tabs)
        {
            map[tab] = new ErpWorkspaceSpec(spec.Headers, spec.Sql, spec.EmptyHint, spec.ManageHref,
                spec.Ensure ?? ErpMasterPlanningWorkspaceSpecs.EnsurePlanningAsync);
        }

        foreach (var (tab, spec) in ErpApWorkspaceSpecs.Tabs)
        {
            map[tab] = new ErpWorkspaceSpec(spec.Headers, spec.Sql, spec.EmptyHint, spec.ManageHref,
                spec.Ensure ?? ErpApWorkspaceSpecs.EnsureApAsync);
        }

        foreach (var (tab, spec) in ErpArWorkspaceSpecs.Tabs)
        {
            map[tab] = new ErpWorkspaceSpec(spec.Headers, spec.Sql, spec.EmptyHint, spec.ManageHref,
                spec.Ensure ?? ErpArWorkspaceSpecs.EnsureArAsync);
        }

        return map;
    }

    public bool IsDataTab(string? tab) => tab is not null && Specs.ContainsKey(tab);

    public async Task<ErpPimWorkspaceData?> LoadAsync(string? tab, CancellationToken cancellationToken = default)
    {
        if (tab is null || !Specs.TryGetValue(tab, out var spec))
        {
            return null;
        }

        var rows = new List<IReadOnlyList<string>>();
        if (_connections.IsConfigured)
        {
            try
            {
                await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
                if (spec.Ensure is not null)
                {
                    await spec.Ensure(connection, cancellationToken).ConfigureAwait(false);
                }

                await using var command = connection.CreateCommand();
                command.CommandText = ErpDb.Positional(spec.Sql);
                await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    var cells = new string[reader.FieldCount];
                    for (var c = 0; c < cells.Length; c++)
                    {
                        cells[c] = Format(reader.GetValue(c));
                    }

                    rows.Add(cells);
                }
            }
            catch
            {
                // Optional backing table missing on this tenant — show the hint row instead of 500.
            }
        }

        return new ErpPimWorkspaceData(spec.Headers, rows, spec.EmptyHint, spec.ManageHref);
    }

    private static string Format(object? value) => value switch
    {
        null or DBNull => "",
        bool b => b ? "1" : "0",
        decimal d => d.ToString("0.####", CultureInfo.InvariantCulture),
        double d => d.ToString("0.####", CultureInfo.InvariantCulture),
        float f => f.ToString("0.####", CultureInfo.InvariantCulture),
        DateTime dt => dt.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture),
        _ => Convert.ToString(value, CultureInfo.InvariantCulture) ?? "",
    };
}
