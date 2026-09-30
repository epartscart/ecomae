using System.Data.Common;

namespace EcomAE.Platform.Erp;

public interface IErpFitOutEstimateWriteService
{
    Task<ErpSimpleWriteResult> SaveEstimateAsync(
        ErpFitOutEstimateSaveRequest request,
        CancellationToken cancellationToken = default);

    Task<ErpSimpleWriteResult> SaveLineAsync(
        ErpFitOutBoqLineSaveRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record ErpFitOutEstimateSaveRequest(
    long Id = 0,
    long ProjectId = 0,
    string? Code = null,
    string? Title = null,
    int Revision = 1,
    decimal MarkupPercent = 0,
    string? Status = null);

public sealed record ErpFitOutBoqLineSaveRequest(
    long Id = 0,
    long EstimateId = 0,
    string? Section = null,
    string? Description = null,
    string? CostType = null,
    decimal Quantity = 0,
    string? Unit = null,
    decimal UnitRate = 0,
    int SortOrder = 0,
    decimal MaterialRate = 0,
    decimal LabourRate = 0,
    decimal SubcontractRate = 0,
    decimal EquipmentRate = 0,
    decimal OverheadRate = 0);

public sealed class ErpFitOutEstimateWriteService : IErpFitOutEstimateWriteService
{
    private readonly IErpWriteConnectionFactory _connections;
    private readonly IErpVoucherNumberService _vouchers;

    public ErpFitOutEstimateWriteService(
        IErpWriteConnectionFactory connections,
        IErpVoucherNumberService vouchers)
    {
        _connections = connections;
        _vouchers = vouchers;
    }

    public async Task<ErpSimpleWriteResult> SaveEstimateAsync(
        ErpFitOutEstimateSaveRequest request,
        CancellationToken cancellationToken = default)
    {
        var code = Clip(request.Code, 60);
        var title = Clip(request.Title, 200);
        if (request.ProjectId <= 0
            || (request.Id > 0 && code.Length == 0)
            || title.Length == 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Project, estimate code, and title are required.");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await EnsureSchemaAsync(connection, cancellationToken).ConfigureAwait(false);
        if (request.Id == 0 && code.Length == 0)
        {
            code = await _vouchers
                .NextAsync(connection, null, "EST", cancellationToken)
                .ConfigureAwait(false);
        }
        var revision = Math.Max(1, request.Revision);
        var markup = decimal.Round(Math.Max(0, request.MarkupPercent), 4, MidpointRounding.AwayFromZero);
        var status = NormalizeStatus(request.Status);

        if (request.Id > 0)
        {
            await ErpDb.ExecuteAsync(
                connection,
                null,
                ErpDb.Positional(
                    "UPDATE `ecomae_fitout_estimates` SET `project_id`=?, `code`=?, `title`=?, `revision`=?, `markup_percent`=?, `status`=?, `updated_at_utc`=UTC_TIMESTAMP() WHERE `id`=?"),
                cancellationToken,
                request.ProjectId, code, title, revision, markup, status, request.Id).ConfigureAwait(false);
            return ErpSimpleWriteResult.Ok("Fit-out estimate saved", request.Id);
        }

        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional(
                "INSERT INTO `ecomae_fitout_estimates` (`project_id`,`code`,`title`,`revision`,`markup_percent`,`status`) VALUES (?,?,?,?,?,?)"),
            cancellationToken,
            request.ProjectId, code, title, revision, markup, status).ConfigureAwait(false);
        var inserted = await ErpDb.LastInsertIdAsync(connection, null, cancellationToken).ConfigureAwait(false);
        return ErpSimpleWriteResult.Ok("Fit-out estimate saved", inserted);
    }

    public async Task<ErpSimpleWriteResult> SaveLineAsync(
        ErpFitOutBoqLineSaveRequest request,
        CancellationToken cancellationToken = default)
    {
        var section = Clip(request.Section, 120);
        var description = Clip(request.Description, 500);
        var costType = NormalizeCostType(request.CostType);
        var unit = Clip(request.Unit, 24);
        if (request.EstimateId <= 0 || description.Length == 0 || unit.Length == 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Estimate, description, and unit are required.");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await EnsureSchemaAsync(connection, cancellationToken).ConfigureAwait(false);
        var quantity = decimal.Round(Math.Max(0, request.Quantity), 4, MidpointRounding.AwayFromZero);
        var rate = decimal.Round(Math.Max(0, request.UnitRate), 4, MidpointRounding.AwayFromZero);
        var materialRate = decimal.Round(Math.Max(0, request.MaterialRate), 4, MidpointRounding.AwayFromZero);
        var labourRate = decimal.Round(Math.Max(0, request.LabourRate), 4, MidpointRounding.AwayFromZero);
        var subcontractRate = decimal.Round(Math.Max(0, request.SubcontractRate), 4, MidpointRounding.AwayFromZero);
        var equipmentRate = decimal.Round(Math.Max(0, request.EquipmentRate), 4, MidpointRounding.AwayFromZero);
        var overheadRate = decimal.Round(Math.Max(0, request.OverheadRate), 4, MidpointRounding.AwayFromZero);
        if (materialRate + labourRate + subcontractRate + equipmentRate + overheadRate == 0)
        {
            (materialRate, labourRate, subcontractRate, equipmentRate, overheadRate) = costType switch
            {
                "labour" => (0m, rate, 0m, 0m, 0m),
                "subcontract" => (0m, 0m, rate, 0m, 0m),
                "equipment" => (0m, 0m, 0m, rate, 0m),
                "overhead" => (0m, 0m, 0m, 0m, rate),
                _ => (rate, 0m, 0m, 0m, 0m)
            };
        }
        var componentRate = decimal.Round(materialRate + labourRate + subcontractRate + equipmentRate + overheadRate, 4, MidpointRounding.AwayFromZero);
        var estimateMarkup = await ReadMarkupAsync(connection, request.EstimateId, cancellationToken).ConfigureAwait(false);
        var totalCost = decimal.Round(componentRate * quantity, 4, MidpointRounding.AwayFromZero);
        var sellingRate = decimal.Round(componentRate * (1 + estimateMarkup / 100m), 4, MidpointRounding.AwayFromZero);
        var sellingAmount = decimal.Round(sellingRate * quantity, 4, MidpointRounding.AwayFromZero);
        var sortOrder = Math.Max(0, request.SortOrder);

        if (request.Id > 0)
        {
            await ErpDb.ExecuteAsync(
                connection,
                null,
                ErpDb.Positional(
                    "UPDATE `ecomae_fitout_boq_lines` SET `estimate_id`=?, `section`=?, `description`=?, `cost_type`=?, `quantity`=?, `unit`=?, `unit_rate`=?, `material_rate`=?, `labour_rate`=?, `subcontract_rate`=?, `equipment_rate`=?, `overhead_rate`=?, `total_cost`=?, `selling_rate`=?, `selling_amount`=?, `sort_order`=? WHERE `id`=?"),
                cancellationToken,
                request.EstimateId, section, description, costType, quantity, unit, componentRate, materialRate, labourRate, subcontractRate, equipmentRate, overheadRate, totalCost, sellingRate, sellingAmount, sortOrder, request.Id).ConfigureAwait(false);
            return ErpSimpleWriteResult.Ok("BOQ line saved", request.Id);
        }

        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional(
                "INSERT INTO `ecomae_fitout_boq_lines` (`estimate_id`,`section`,`description`,`cost_type`,`quantity`,`unit`,`unit_rate`,`material_rate`,`labour_rate`,`subcontract_rate`,`equipment_rate`,`overhead_rate`,`total_cost`,`selling_rate`,`selling_amount`,`sort_order`) VALUES (?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?)"),
            cancellationToken,
            request.EstimateId, section, description, costType, quantity, unit, componentRate, materialRate, labourRate, subcontractRate, equipmentRate, overheadRate, totalCost, sellingRate, sellingAmount, sortOrder).ConfigureAwait(false);
        var inserted = await ErpDb.LastInsertIdAsync(connection, null, cancellationToken).ConfigureAwait(false);
        return ErpSimpleWriteResult.Ok("BOQ line saved", inserted);
    }

    private static async Task EnsureSchemaAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        await ErpDb.ExecuteAsync(
            connection,
            null,
            """
            CREATE TABLE IF NOT EXISTS `ecomae_fitout_estimates` (
                `id` bigint NOT NULL AUTO_INCREMENT,
                `project_id` bigint NOT NULL,
                `code` varchar(60) NOT NULL,
                `title` varchar(200) NOT NULL,
                `revision` int NOT NULL DEFAULT 1,
                `markup_percent` decimal(12,4) NOT NULL DEFAULT 0,
                `status` varchar(24) NOT NULL DEFAULT 'draft',
                `created_at` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
                `updated_at_utc` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
                PRIMARY KEY (`id`),
                UNIQUE KEY `uq_ecomae_fitout_estimate_code` (`code`),
                KEY `ix_ecomae_fitout_estimate_project` (`project_id`)
            ) ENGINE=InnoDB
            """,
            cancellationToken).ConfigureAwait(false);
        await ErpDb.ExecuteAsync(
            connection,
            null,
            """
            CREATE TABLE IF NOT EXISTS `ecomae_fitout_boq_lines` (
                `id` bigint NOT NULL AUTO_INCREMENT,
                `estimate_id` bigint NOT NULL,
                `section` varchar(120) NOT NULL DEFAULT '',
                `description` varchar(500) NOT NULL,
                `cost_type` varchar(24) NOT NULL DEFAULT 'material',
                `quantity` decimal(16,4) NOT NULL DEFAULT 0,
                `unit` varchar(24) NOT NULL,
                `unit_rate` decimal(16,4) NOT NULL DEFAULT 0,
                `material_rate` decimal(16,4) NOT NULL DEFAULT 0,
                `labour_rate` decimal(16,4) NOT NULL DEFAULT 0,
                `subcontract_rate` decimal(16,4) NOT NULL DEFAULT 0,
                `equipment_rate` decimal(16,4) NOT NULL DEFAULT 0,
                `overhead_rate` decimal(16,4) NOT NULL DEFAULT 0,
                `total_cost` decimal(16,4) NOT NULL DEFAULT 0,
                `selling_rate` decimal(16,4) NOT NULL DEFAULT 0,
                `selling_amount` decimal(16,4) NOT NULL DEFAULT 0,
                `sort_order` int NOT NULL DEFAULT 0,
                PRIMARY KEY (`id`),
                KEY `ix_ecomae_fitout_boq_estimate` (`estimate_id`)
            ) ENGINE=InnoDB
            """,
            cancellationToken).ConfigureAwait(false);
        foreach (var column in new[]
        {
            "material_rate", "labour_rate", "subcontract_rate", "equipment_rate",
            "overhead_rate", "total_cost", "selling_rate", "selling_amount"
        })
        {
            await ErpDb.ExecuteAsync(
                connection,
                null,
                $"ALTER TABLE `ecomae_fitout_boq_lines` ADD COLUMN IF NOT EXISTS `{column}` decimal(16,4) NOT NULL DEFAULT 0",
                cancellationToken).ConfigureAwait(false);
        }
    }

    private static async Task<decimal> ReadMarkupAsync(
        DbConnection connection,
        long estimateId,
        CancellationToken cancellationToken)
    {
        var value = await ErpDb.ScalarAsync(
            connection,
            null,
            ErpDb.Positional("SELECT `markup_percent` FROM `ecomae_fitout_estimates` WHERE `id`=? LIMIT 1"),
            cancellationToken,
            estimateId).ConfigureAwait(false);
        return value is null or DBNull
            ? 0
            : decimal.Round(Math.Max(0, Convert.ToDecimal(value, System.Globalization.CultureInfo.InvariantCulture)), 4, MidpointRounding.AwayFromZero);
    }

    private static string NormalizeStatus(string? value)
        => value?.Trim().ToLowerInvariant() switch
        {
            "issued" => "issued",
            "approved" => "approved",
            "superseded" => "superseded",
            _ => "draft"
        };

    private static string NormalizeCostType(string? value)
        => value?.Trim().ToLowerInvariant() switch
        {
            "labour" => "labour",
            "subcontract" => "subcontract",
            "equipment" => "equipment",
            "overhead" => "overhead",
            _ => "material"
        };

    private static string Clip(string? value, int max)
    {
        var text = (value ?? string.Empty).Trim();
        return text.Length <= max ? text : text[..max];
    }
}
