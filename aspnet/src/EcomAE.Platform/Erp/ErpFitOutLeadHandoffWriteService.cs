namespace EcomAE.Platform.Erp;

public interface IErpFitOutLeadHandoffWriteService
{
    Task<ErpSimpleWriteResult> SaveAsync(
        ErpFitOutLeadHandoffSaveRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record ErpFitOutLeadHandoffSaveRequest(
    long Id = 0,
    long ProjectId = 0,
    long LeadId = 0,
    long CustomerId = 0,
    long EstimateId = 0,
    string? Status = null,
    string? Notes = null);

public sealed class ErpFitOutLeadHandoffWriteService : IErpFitOutLeadHandoffWriteService
{
    private readonly IErpWriteConnectionFactory _connections;

    public ErpFitOutLeadHandoffWriteService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<ErpSimpleWriteResult> SaveAsync(
        ErpFitOutLeadHandoffSaveRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.ProjectId <= 0 || (request.LeadId <= 0 && request.CustomerId <= 0))
        {
            return ErpSimpleWriteResult.Fail(
                "invalid",
                "Project and lead or customer are required for handoff.");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        var status = request.Status?.Trim().ToLowerInvariant() switch
        {
            "qualified" => "qualified",
            "converted" => "converted",
            "rejected" => "rejected",
            _ => "proposed"
        };
        var notes = Clip(request.Notes, 1000);
        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await ErpDb.ExecuteAsync(connection, null, """
            CREATE TABLE IF NOT EXISTS `ecomae_fitout_lead_handoffs` (
                `id` bigint NOT NULL AUTO_INCREMENT,
                `project_id` bigint NOT NULL,
                `lead_id` bigint NOT NULL DEFAULT 0,
                `customer_id` bigint NOT NULL DEFAULT 0,
                `estimate_id` bigint NOT NULL DEFAULT 0,
                `status` varchar(24) NOT NULL DEFAULT 'proposed',
                `notes` varchar(1000) NOT NULL DEFAULT '',
                `created_at_utc` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
                `updated_at_utc` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
                PRIMARY KEY (`id`),
                UNIQUE KEY `uq_ecomae_fitout_handoff_project` (`project_id`),
                KEY `ix_ecomae_fitout_handoff_lead` (`lead_id`),
                KEY `ix_ecomae_fitout_handoff_customer` (`customer_id`)
            ) ENGINE=InnoDB
            """, cancellationToken).ConfigureAwait(false);

        if (request.Id > 0)
        {
            await ErpDb.ExecuteAsync(
                connection,
                null,
                ErpDb.Positional("""
                    UPDATE `ecomae_fitout_lead_handoffs`
                    SET `project_id`=?,`lead_id`=?,`customer_id`=?,`estimate_id`=?,
                        `status`=?,`notes`=?,`updated_at_utc`=CURRENT_TIMESTAMP
                    WHERE `id`=?
                    """),
                cancellationToken,
                request.ProjectId,
                request.LeadId,
                request.CustomerId,
                request.EstimateId,
                status,
                notes,
                request.Id).ConfigureAwait(false);
            return ErpSimpleWriteResult.Ok("Fit-out lead handoff saved", request.Id);
        }

        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional("""
                INSERT INTO `ecomae_fitout_lead_handoffs`
                    (`project_id`,`lead_id`,`customer_id`,`estimate_id`,`status`,`notes`)
                VALUES (?,?,?,?,?,?)
                ON DUPLICATE KEY UPDATE
                    `lead_id`=VALUES(`lead_id`),
                    `customer_id`=VALUES(`customer_id`),
                    `estimate_id`=VALUES(`estimate_id`),
                    `status`=VALUES(`status`),
                    `notes`=VALUES(`notes`),
                    `updated_at_utc`=CURRENT_TIMESTAMP
                """),
            cancellationToken,
            request.ProjectId,
            request.LeadId,
            request.CustomerId,
            request.EstimateId,
            status,
            notes).ConfigureAwait(false);
        var id = await ErpDb.ScalarAsync(
            connection,
            null,
            ErpDb.Positional("SELECT `id` FROM `ecomae_fitout_lead_handoffs` WHERE `project_id`=?"),
            cancellationToken,
            request.ProjectId).ConfigureAwait(false);
        return ErpSimpleWriteResult.Ok(
            "Fit-out lead handoff saved",
            id is null ? 0 : Convert.ToInt64(id, System.Globalization.CultureInfo.InvariantCulture));
    }

    private static string Clip(string? value, int max)
    {
        var text = (value ?? string.Empty).Trim();
        return text.Length <= max ? text : text[..max];
    }
}
