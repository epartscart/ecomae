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
public sealed record ErpTenantGuideBu(int Id, int EntityId, string Code, string Name, string CostCentre, string Warehouse, string WarehouseCostCentre = "");

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
        // `consolidated` is a SHAM-added column — read it when present, else the
        // entities still render (treated as consolidated: there is no perimeter flag).
        var hasConsolidated = false;
        try
        {
            await using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = "SELECT 1 FROM `epc_erp_pm_legal_entities` WHERE 1=0 LIMIT 0";
                await cmd.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
            }
            try
            {
                await using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = "SELECT `consolidated` FROM `epc_erp_pm_legal_entities` LIMIT 0";
                    await cmd.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
                }
                hasConsolidated = true;
            }
            catch { /* column absent on non-SHAM tenants */ }
        }
        catch { /* pm legal-entity table absent */ }

        // When the `consolidated` flag column is absent (non-SHAM tenants),
        // fall back to the same `epc_entity_members` membership the group
        // statements use — never mark everything consolidated.
        var fullMembers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (!hasConsolidated)
        {
            try
            {
                await using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = "SELECT `site_key` FROM `epc_entity_members` WHERE `consolidation`='full'";
                    await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                    while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                    {
                        fullMembers.Add(reader.GetString(0));
                    }
                }
            }
            catch { /* membership table absent → nothing consolidated */ }
        }

        try
        {
            await using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = hasConsolidated
                    ? "SELECT `id`,`code`,`name`,`country_code`,`currency_code`,`consolidated` FROM `epc_erp_pm_legal_entities` WHERE `active`=1 ORDER BY `id`"
                    : "SELECT `id`,`code`,`name`,`country_code`,`currency_code`,0 FROM `epc_erp_pm_legal_entities` WHERE `active`=1 ORDER BY `id`";
                await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    entities.Add(new ErpTenantGuideEntity(
                        reader.GetInt32(0),
                        reader.GetString(1),
                        reader.GetString(2),
                        reader.GetString(3),
                        reader.GetString(4),
                        (!reader.IsDBNull(5) && reader.GetInt32(5) == 1) || (!hasConsolidated && fullMembers.Contains(reader.GetString(1)))));
                }
            }
        }
        catch { /* org tables not seeded for this tenant */ }

        var bus = new List<ErpTenantGuideBu>();
        var warehouseCount = 0;
        // ERP-only org BUs (SHAM §3: carries cost centre + default warehouse).
        // `epc_erp_inv_warehouses.cost_centre` is a SHAM-added column — on
        // older tenants it is absent, so probe it first instead of letting the
        // join's failure discard every org BU.
        var hasWarehouses = false;
        var hasWarehouseCc = false;
        try
        {
            await using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = "SELECT 1 FROM `epc_erp_inv_warehouses` LIMIT 0";
                await cmd.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
            }
            hasWarehouses = true;
            try
            {
                await using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = "SELECT `cost_centre` FROM `epc_erp_inv_warehouses` LIMIT 0";
                    await cmd.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
                }
                hasWarehouseCc = true;
            }
            catch { /* cost_centre column absent on this tenant */ }
        }
        catch { /* warehouses table absent on this tenant */ }
        try
        {
            await using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = "SELECT bu.`id`, bu.`legal_entity_id`, bu.`code`, bu.`name`, bu.`cost_centre`, "
                    + (hasWarehouses ? "COALESCE(w.`name`,''), " : "'', ")
                    + (hasWarehouseCc ? "COALESCE(NULLIF(w.`cost_centre`,''), bu.`cost_centre`) " : "bu.`cost_centre` ")
                    + "FROM `epc_erp_pm_business_units` bu "
                    + (hasWarehouses ? "LEFT JOIN `epc_erp_inv_warehouses` w ON w.`id` = bu.`default_warehouse_id` " : string.Empty)
                    + "WHERE bu.`active`=1 ORDER BY bu.`legal_entity_id`, bu.`code`";
                await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    bus.Add(new ErpTenantGuideBu(
                        reader.GetInt32(0),
                        reader.GetInt32(1),
                        reader.GetString(2),
                        reader.GetString(3),
                        reader.GetString(4),
                        reader.GetString(5),
                        reader.FieldCount > 6 ? reader.GetString(6) : ""));
                }
            }
        }
        catch { /* org BU table absent on this tenant */ }

        // Standard ERP masters BUs (epc_erp_pm_business_units) — older tenants and
        // the operational mirror; no cost-centre/warehouse columns there.
        var known = new HashSet<string>(bus.Select(b => b.EntityId + "|" + b.Code), StringComparer.OrdinalIgnoreCase);
        // Mirror rows renamed after seeding keep the same name on the org side —
        // dedupe by name too so a code-edited mirror does not draw a phantom BU.
        var knownNames = new HashSet<string>(bus.Where(b => b.Name.Length > 0).Select(b => b.EntityId + "|" + b.Name), StringComparer.OrdinalIgnoreCase);
        try
        {
            await using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = "SELECT `id`,`legal_entity_id`,`code`,`name` FROM `epc_erp_pm_business_units` WHERE `active`=1 ORDER BY `legal_entity_id`, `code`";
                await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    var entityId = reader.GetInt32(1);
                    var code = reader.GetString(2);
                    var name = reader.GetString(3);
                    if (known.Add(entityId + "|" + code) && (name.Length == 0 || knownNames.Add(entityId + "|" + name)))
                    {
                        bus.Add(new ErpTenantGuideBu(reader.GetInt32(0), entityId, code, name, "", ""));
                    }
                }
            }
        }
        catch { /* pm BU table absent on this tenant */ }

        bus.Sort((a, b) => a.EntityId != b.EntityId ? a.EntityId.CompareTo(b.EntityId) : string.CompareOrdinal(a.Code, b.Code));

        try
        {
            await using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = "SELECT COUNT(*) FROM `epc_erp_inv_warehouses` WHERE `active`=1";
                warehouseCount = Convert.ToInt32(await cmd.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false));
            }
        }
        catch { /* warehouse table absent */ }

        return new ErpTenantGuideModel { Entities = entities, BusinessUnits = bus, WarehouseCount = warehouseCount };
    }
}
