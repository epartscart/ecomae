namespace EcomAE.Platform.Erp;

/// <summary>
/// Read service for the in-app tenant guide (/erp/guide).
/// Pulls the tenant's real organisation structure — legal entities,
/// business units, warehouses — so the guide is a live picture, not
/// a static document. Empty for tenants with no seeded org data.
/// </summary>
public interface IErpTenantGuideReadService
{
    Task<ErpTenantGuideModel> ReadAsync(CancellationToken cancellationToken = default);
}

public sealed record ErpTenantGuideEntity(int Id, string Code, string Name, string Country, string Currency, bool Consolidated);
public sealed record ErpTenantGuideBu(int EntityId, string Code, string Name, string CostCentre, string Warehouse);

public sealed class ErpTenantGuideModel
{
    public IReadOnlyList<ErpTenantGuideEntity> Entities { get; init; } = Array.Empty<ErpTenantGuideEntity>();
    public IReadOnlyList<ErpTenantGuideBu> BusinessUnits { get; init; } = Array.Empty<ErpTenantGuideBu>();
    public int WarehouseCount { get; init; }
    public bool HasOrgData => Entities.Count > 0;
}

public sealed class ErpTenantGuideReadService : IErpTenantGuideReadService
{
    private readonly IErpWriteConnectionFactory _connections;

    public ErpTenantGuideReadService(IErpWriteConnectionFactory connections) => _connections = connections;

    public async Task<ErpTenantGuideModel> ReadAsync(CancellationToken cancellationToken = default)
    {
        await using var conn = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);

        var entities = new List<ErpTenantGuideEntity>();
        try
        {
            await using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = "SELECT `id`,`code`,`name`,`country_code`,`currency_code`,`consolidated` FROM `epc_erp_pm_legal_entities` WHERE `active`=1 ORDER BY `id`";
                await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    entities.Add(new ErpTenantGuideEntity(
                        reader.GetInt32(0),
                        reader.GetString(1),
                        reader.GetString(2),
                        reader.GetString(3),
                        reader.GetString(4),
                        !reader.IsDBNull(5) && reader.GetInt32(5) == 1));
                }
            }
        }
        catch { /* org tables not seeded for this tenant */ }

        var bus = new List<ErpTenantGuideBu>();
        var warehouseCount = 0;
        try
        {
            await using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = "SELECT bu.`entity_id`, bu.`code`, bu.`name`, bu.`cost_centre`, COALESCE(w.`name`,'') "
                    + "FROM `epc_erp_org_business_units` bu "
                    + "LEFT JOIN `epc_erp_inv_warehouses` w ON w.`id` = bu.`default_warehouse_id` "
                    + "WHERE bu.`status`='active' ORDER BY bu.`entity_id`, bu.`code`";
                await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    bus.Add(new ErpTenantGuideBu(
                        reader.GetInt32(0),
                        reader.GetString(1),
                        reader.GetString(2),
                        reader.GetString(3),
                        reader.GetString(4)));
                }
            }

            await using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = "SELECT COUNT(*) FROM `epc_erp_inv_warehouses` WHERE `active`=1";
                warehouseCount = Convert.ToInt32(await cmd.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false));
            }
        }
        catch { /* org tables not seeded for this tenant */ }

        return new ErpTenantGuideModel { Entities = entities, BusinessUnits = bus, WarehouseCount = warehouseCount };
    }
}
