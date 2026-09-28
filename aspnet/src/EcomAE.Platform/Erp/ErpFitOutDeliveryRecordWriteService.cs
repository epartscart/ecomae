namespace EcomAE.Platform.Erp;

public interface IErpFitOutDeliveryRecordWriteService
{
    Task<ErpSimpleWriteResult> SaveAsync(
        ErpFitOutDeliveryRecordSaveRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record ErpFitOutDeliveryRecordSaveRequest(
    long Id = 0,
    long ProjectId = 0,
    long CostCodeId = 0,
    long ParentId = 0,
    long SubcontractorId = 0,
    string? RecordType = null,
    string? Reference = null,
    string? Title = null,
    string? Description = null,
    decimal Quantity = 0,
    decimal Amount = 0,
    decimal CompletionPercent = 0,
    DateOnly? EventDate = null,
    string? Status = null,
    string? PhotoUrl = null);

public sealed class ErpFitOutDeliveryRecordWriteService : IErpFitOutDeliveryRecordWriteService
{
    private static readonly HashSet<string> RecordTypes = new(StringComparer.Ordinal)
    {
        "subcontract_order",
        "subcontract_measurement",
        "subcontract_certification",
        "site_daily_report",
        "site_photo",
        "variation",
        "progress_claim",
        "rfi",
        "drawing_revision",
        "qa_inspection",
        "snag",
        "equipment_usage",
        "timesheet",
        "weighted_progress",
        "retention_recovery",
        "advance_recovery",
        "approval_request"
    };

    private readonly IErpWriteConnectionFactory _connections;

    public ErpFitOutDeliveryRecordWriteService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<ErpSimpleWriteResult> SaveAsync(
        ErpFitOutDeliveryRecordSaveRequest request,
        CancellationToken cancellationToken = default)
    {
        var recordType = request.RecordType?.Trim().ToLowerInvariant() ?? string.Empty;
        var reference = Clip(request.Reference, 80);
        var title = Clip(request.Title, 180);
        var description = Clip(request.Description, 1000);
        var photoUrl = Clip(request.PhotoUrl, 500);
        var status = Clip(request.Status, 24).ToLowerInvariant();
        if (!RecordTypes.Contains(recordType)
            || request.ProjectId <= 0
            || reference.Length == 0
            || title.Length == 0)
        {
            return ErpSimpleWriteResult.Fail(
                "invalid",
                "Project, record type, reference, and title are required.");
        }

        if (recordType == "site_photo" && photoUrl.Length == 0)
        {
            return ErpSimpleWriteResult.Fail(
                "invalid",
                "Site photo records require a photo URL.");
        }

        if ((recordType == "subcontract_measurement"
                || recordType == "subcontract_certification")
            && request.ParentId <= 0)
        {
            return ErpSimpleWriteResult.Fail(
                "invalid",
                "Subcontract measurements and certifications require a parent subcontract record.");
        }

        if (recordType == "subcontract_certification" && request.Amount <= 0m)
        {
            return ErpSimpleWriteResult.Fail(
                "invalid",
                "Subcontract certifications require a positive certified amount.");
        }

        if (recordType == "progress_claim"
            && request.Amount <= 0m
            && request.CompletionPercent <= 0m)
        {
            return ErpSimpleWriteResult.Fail(
                "invalid",
                "Progress claims require an amount or completion percentage.");
        }

        if ((recordType == "weighted_progress"
                || recordType == "retention_recovery"
                || recordType == "advance_recovery")
            && request.Amount <= 0m
            && request.CompletionPercent <= 0m)
        {
            return ErpSimpleWriteResult.Fail(
                "invalid",
                "Progress and recovery records require an amount or completion percentage.");
        }

        if ((recordType == "equipment_usage" || recordType == "timesheet")
            && request.Quantity <= 0m)
        {
            return ErpSimpleWriteResult.Fail(
                "invalid",
                "Equipment usage and timesheet records require a positive quantity.");
        }

        if (recordType == "approval_request" && request.Amount <= 0m)
        {
            return ErpSimpleWriteResult.Fail(
                "invalid",
                "Approval requests require a positive approval amount.");
        }

        if (recordType == "site_daily_report" && description.Length == 0)
        {
            return ErpSimpleWriteResult.Fail(
                "invalid",
                "Site daily reports require a description.");
        }

        if ((recordType == "rfi"
                || recordType == "drawing_revision"
                || recordType == "qa_inspection"
                || recordType == "snag"
                || recordType == "equipment_usage"
                || recordType == "timesheet")
            && description.Length == 0)
        {
            return ErpSimpleWriteResult.Fail(
                "invalid",
                "Quality and site-control records require a description.");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail(
                "db",
                "TenantRegistry DB is not configured.");
        }

        var eventDate = request.EventDate ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var quantity = Math.Max(0m, request.Quantity);
        var amount = Math.Max(0m, request.Amount);
        var completion = Math.Clamp(request.CompletionPercent, 0m, 100m);
        if (status.Length == 0)
        {
            status = recordType switch
            {
                "subcontract_order" => "draft",
                "subcontract_measurement" => "draft",
                "subcontract_certification" => "pending",
                "site_daily_report" => "submitted",
                "site_photo" => "attached",
                "variation" => "draft",
                "progress_claim" => "draft",
                "rfi" => "open",
                "drawing_revision" => "issued",
                "qa_inspection" => "scheduled",
                "snag" => "open",
                "equipment_usage" => "draft",
                "timesheet" => "draft",
                "weighted_progress" => "draft",
                "retention_recovery" => "draft",
                "advance_recovery" => "draft",
                "approval_request" => "pending",
                _ => "draft"
            };
        }

        await using var connection = await _connections
            .OpenAsync(cancellationToken)
            .ConfigureAwait(false);
        await ErpDb.ExecuteAsync(connection, null, """
            CREATE TABLE IF NOT EXISTS `ecomae_fitout_delivery_records` (
                `id` bigint NOT NULL AUTO_INCREMENT,
                `project_id` bigint NOT NULL,
                `cost_code_id` bigint NOT NULL DEFAULT 0,
                `parent_id` bigint NOT NULL DEFAULT 0,
                `subcontractor_id` bigint NOT NULL DEFAULT 0,
                `record_type` varchar(32) NOT NULL,
                `reference` varchar(80) NOT NULL,
                `title` varchar(180) NOT NULL,
                `description` varchar(1000) NOT NULL DEFAULT '',
                `quantity` decimal(14,3) NOT NULL DEFAULT 0.000,
                `amount` decimal(14,2) NOT NULL DEFAULT 0.00,
                `completion_percent` decimal(7,3) NOT NULL DEFAULT 0.000,
                `event_date` date NOT NULL,
                `status` varchar(24) NOT NULL,
                `photo_url` varchar(500) NOT NULL DEFAULT '',
                `created_at_utc` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
                `updated_at_utc` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
                PRIMARY KEY (`id`),
                KEY `ix_ecomae_fitout_delivery_project` (`project_id`,`record_type`),
                KEY `ix_ecomae_fitout_delivery_parent` (`parent_id`),
                UNIQUE KEY `uq_ecomae_fitout_delivery_reference` (`project_id`,`record_type`,`reference`)
            ) ENGINE=InnoDB
            """, cancellationToken).ConfigureAwait(false);

        if (request.Id > 0)
        {
            await ErpDb.ExecuteAsync(
                connection,
                null,
                ErpDb.Positional("""
                    UPDATE `ecomae_fitout_delivery_records`
                    SET `project_id`=?,`cost_code_id`=?,`parent_id`=?,
                        `subcontractor_id`=?,`record_type`=?,`reference`=?,
                        `title`=?,`description`=?,`quantity`=?,`amount`=?,
                        `completion_percent`=?,`event_date`=?,`status`=?,
                        `photo_url`=?,`updated_at_utc`=CURRENT_TIMESTAMP
                    WHERE `id`=?
                    """),
                cancellationToken,
                request.ProjectId,
                Math.Max(0, request.CostCodeId),
                Math.Max(0, request.ParentId),
                Math.Max(0, request.SubcontractorId),
                recordType,
                reference,
                title,
                description,
                quantity,
                amount,
                completion,
                eventDate,
                status,
                photoUrl,
                request.Id).ConfigureAwait(false);
            return ErpSimpleWriteResult.Ok("Fit-out delivery record saved", request.Id);
        }

        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional("""
                INSERT INTO `ecomae_fitout_delivery_records`
                    (`project_id`,`cost_code_id`,`parent_id`,`subcontractor_id`,`record_type`,
                     `reference`,`title`,`description`,`quantity`,`amount`,`completion_percent`,
                     `event_date`,`status`,`photo_url`)
                VALUES (?,?,?,?,?,?,?,?,?,?,?,?,?,?)
                """),
            cancellationToken,
            request.ProjectId,
            Math.Max(0, request.CostCodeId),
            Math.Max(0, request.ParentId),
            Math.Max(0, request.SubcontractorId),
            recordType,
            reference,
            title,
            description,
            quantity,
            amount,
            completion,
            eventDate,
            status,
            photoUrl).ConfigureAwait(false);
        var id = await ErpDb.LastInsertIdAsync(
            connection,
            null,
            cancellationToken).ConfigureAwait(false);
        return ErpSimpleWriteResult.Ok("Fit-out delivery record saved", id);
    }

    private static string Clip(string? value, int max)
    {
        var text = (value ?? string.Empty).Trim();
        return text.Length <= max ? text : text[..max];
    }
}
