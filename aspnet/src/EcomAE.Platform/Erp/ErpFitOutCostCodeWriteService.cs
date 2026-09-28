namespace EcomAE.Platform.Erp;

public interface IErpFitOutCostCodeWriteService
{
    Task<ErpSimpleWriteResult> SaveAsync(
        ErpFitOutCostCodeSaveRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record ErpFitOutCostCodeSaveRequest(
    long Id = 0,
    long ProjectId = 0,
    long ParentId = 0,
    string? Code = null,
    string? Name = null,
    string? CostType = null);

public sealed class ErpFitOutCostCodeWriteService : IErpFitOutCostCodeWriteService
{
    private readonly IErpWriteConnectionFactory _connections;

    public ErpFitOutCostCodeWriteService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<ErpSimpleWriteResult> SaveAsync(
        ErpFitOutCostCodeSaveRequest request,
        CancellationToken cancellationToken = default)
    {
        var code = Clip(request.Code, 60);
        var name = Clip(request.Name, 200);
        if (request.ProjectId <= 0 || code.Length == 0 || name.Length == 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Project, cost-code, and name are required.");
        }
        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        var parent = Math.Max(0, request.ParentId);
        var type = request.CostType?.Trim().ToLowerInvariant() switch
        {
            "labour" => "labour",
            "subcontract" => "subcontract",
            "equipment" => "equipment",
            "overhead" => "overhead",
            _ => "material"
        };
        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await ErpDb.ExecuteAsync(connection, null, """
            CREATE TABLE IF NOT EXISTS `ecomae_fitout_cost_codes` (
                `id` bigint NOT NULL AUTO_INCREMENT,
                `project_id` bigint NOT NULL,
                `parent_id` bigint NOT NULL DEFAULT 0,
                `code` varchar(60) NOT NULL,
                `name` varchar(200) NOT NULL,
                `cost_type` varchar(24) NOT NULL DEFAULT 'material',
                `created_at_utc` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
                PRIMARY KEY (`id`),
                UNIQUE KEY `uq_ecomae_fitout_cost_code` (`project_id`,`code`),
                KEY `ix_ecomae_fitout_cost_parent` (`project_id`,`parent_id`)
            ) ENGINE=InnoDB
            """, cancellationToken).ConfigureAwait(false);
        if (request.Id > 0)
        {
            await ErpDb.ExecuteAsync(connection, null,
                ErpDb.Positional("UPDATE `ecomae_fitout_cost_codes` SET `project_id`=?,`parent_id`=?,`code`=?,`name`=?,`cost_type`=? WHERE `id`=?"),
                cancellationToken, request.ProjectId, parent, code, name, type, request.Id).ConfigureAwait(false);
            return ErpSimpleWriteResult.Ok("Cost code saved", request.Id);
        }
        await ErpDb.ExecuteAsync(connection, null,
            ErpDb.Positional("INSERT INTO `ecomae_fitout_cost_codes` (`project_id`,`parent_id`,`code`,`name`,`cost_type`) VALUES (?,?,?,?,?)"),
            cancellationToken, request.ProjectId, parent, code, name, type).ConfigureAwait(false);
        var id = await ErpDb.LastInsertIdAsync(connection, null, cancellationToken).ConfigureAwait(false);
        return ErpSimpleWriteResult.Ok("Cost code saved", id);
    }

    private static string Clip(string? value, int max)
    {
        var text = (value ?? string.Empty).Trim();
        return text.Length <= max ? text : text[..max];
    }
}
