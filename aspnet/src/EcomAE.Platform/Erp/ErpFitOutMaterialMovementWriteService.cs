namespace EcomAE.Platform.Erp;

public interface IErpFitOutMaterialMovementWriteService
{
    Task<ErpSimpleWriteResult> SaveAsync(
        ErpFitOutMaterialMovementSaveRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record ErpFitOutMaterialMovementSaveRequest(
    long Id = 0,
    long ProjectId = 0,
    long CostCodeId = 0,
    string? ItemCode = null,
    string? Description = null,
    decimal Quantity = 0,
    decimal UnitCost = 0,
    string? Movement = null,
    string? Reference = null);

public sealed class ErpFitOutMaterialMovementWriteService : IErpFitOutMaterialMovementWriteService
{
    private readonly IErpWriteConnectionFactory _connections;

    public ErpFitOutMaterialMovementWriteService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<ErpSimpleWriteResult> SaveAsync(
        ErpFitOutMaterialMovementSaveRequest request,
        CancellationToken cancellationToken = default)
    {
        var itemCode = Clip(request.ItemCode, 80);
        var description = Clip(request.Description, 240);
        var reference = Clip(request.Reference, 120);
        var movement = request.Movement?.Trim().ToLowerInvariant() switch
        {
            "return" => "return",
            _ => "issue"
        };
        if (request.ProjectId <= 0
            || itemCode.Length == 0
            || description.Length == 0
            || request.Quantity <= 0m)
        {
            return ErpSimpleWriteResult.Fail(
                "invalid",
                "Project, item, description, and a positive quantity are required.");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail(
                "db",
                "TenantRegistry DB is not configured.");
        }

        await using var connection = await _connections
            .OpenAsync(cancellationToken)
            .ConfigureAwait(false);
        await ErpDb.ExecuteAsync(connection, null, """
            CREATE TABLE IF NOT EXISTS `ecomae_fitout_material_movements` (
                `id` bigint NOT NULL AUTO_INCREMENT,
                `project_id` bigint NOT NULL,
                `cost_code_id` bigint NOT NULL DEFAULT 0,
                `item_code` varchar(80) NOT NULL,
                `description` varchar(240) NOT NULL,
                `quantity` decimal(14,3) NOT NULL,
                `unit_cost` decimal(14,2) NOT NULL DEFAULT 0.00,
                `movement` varchar(8) NOT NULL DEFAULT 'issue',
                `reference` varchar(120) NOT NULL DEFAULT '',
                `created_at_utc` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
                `updated_at_utc` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
                PRIMARY KEY (`id`),
                KEY `ix_ecomae_fitout_material_project` (`project_id`),
                KEY `ix_ecomae_fitout_material_item` (`project_id`,`item_code`)
            ) ENGINE=InnoDB
            """, cancellationToken).ConfigureAwait(false);

        if (request.Id > 0)
        {
            await ErpDb.ExecuteAsync(
                connection,
                null,
                ErpDb.Positional("""
                    UPDATE `ecomae_fitout_material_movements`
                    SET `project_id`=?,`cost_code_id`=?,`item_code`=?,
                        `description`=?,`quantity`=?,`unit_cost`=?,
                        `movement`=?,`reference`=?,`updated_at_utc`=CURRENT_TIMESTAMP
                    WHERE `id`=?
                    """),
                cancellationToken,
                request.ProjectId,
                Math.Max(0, request.CostCodeId),
                itemCode,
                description,
                request.Quantity,
                Math.Max(0m, request.UnitCost),
                movement,
                reference,
                request.Id).ConfigureAwait(false);
            return ErpSimpleWriteResult.Ok("Material movement saved", request.Id);
        }

        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional("""
                INSERT INTO `ecomae_fitout_material_movements`
                    (`project_id`,`cost_code_id`,`item_code`,`description`,`quantity`,
                     `unit_cost`,`movement`,`reference`)
                VALUES (?,?,?,?,?,?,?,?)
                """),
            cancellationToken,
            request.ProjectId,
            Math.Max(0, request.CostCodeId),
            itemCode,
            description,
            request.Quantity,
            Math.Max(0m, request.UnitCost),
            movement,
            reference).ConfigureAwait(false);
        var id = await ErpDb.LastInsertIdAsync(
            connection,
            null,
            cancellationToken).ConfigureAwait(false);
        return ErpSimpleWriteResult.Ok("Material movement saved", id);
    }

    private static string Clip(string? value, int max)
    {
        var text = (value ?? string.Empty).Trim();
        return text.Length <= max ? text : text[..max];
    }
}
