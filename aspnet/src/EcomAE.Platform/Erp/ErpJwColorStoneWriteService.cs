using System.Data.Common;

namespace EcomAE.Platform.Erp;

/// <summary>
/// Live PHP <c>jw_color_stone_save</c> / <c>epc_jewel_color_stone_save</c> twin.
/// Schema-ensure stays PHP — missing <c>epc_jewel_color_stone_master</c> refuses instead of CREATE.
/// </summary>
public interface IErpJwColorStoneWriteService
{
    Task<ErpSimpleWriteResult> SaveAsync(
        ErpJwColorStoneSaveRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record ErpJwColorStoneSaveRequest(
    int CompanyId = 0,
    string? Code = null,
    string? Description = null,
    string? Category = null,
    string? Shape = null,
    string? Clarity = null,
    string? Size = null,
    string? Color = null,
    string? Finish = null,
    string? Country = null,
    string? CertificateNo = null,
    string? Vendor = null,
    string? CostCentre = null,
    string? Grade = null);

public sealed class ErpJwColorStoneWriteService : IErpJwColorStoneWriteService
{
    private readonly IErpWriteConnectionFactory _connections;

    public ErpJwColorStoneWriteService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<ErpSimpleWriteResult> SaveAsync(
        ErpJwColorStoneSaveRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var code = (request.Code ?? string.Empty).Trim();
        if (code.Length == 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Item code is required.");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        if (!await TableExistsAsync(connection, "epc_jewel_color_stone_master", cancellationToken).ConfigureAwait(false))
        {
            return ErpSimpleWriteResult.Fail("invalid", "Jewellery color stone tables are not provisioned");
        }

        var companyId = request.CompanyId < 0 ? 0 : request.CompanyId;
        code = Clip(code, 20);
        var category = (request.Category ?? string.Empty).Trim();
        if (category.Length == 0)
        {
            category = "Ruby";
        }

        category = Clip(category, 30);
        var shape = (request.Shape ?? string.Empty).Trim();
        if (shape.Length == 0)
        {
            shape = "Round";
        }

        shape = Clip(shape, 20);
        var clarity = (request.Clarity ?? string.Empty).Trim();
        if (clarity.Length == 0)
        {
            clarity = "VS";
        }

        clarity = Clip(clarity, 20);
        var finish = (request.Finish ?? string.Empty).Trim();
        if (finish.Length == 0)
        {
            finish = "None";
        }

        finish = Clip(finish, 20);
        var costCentre = (request.CostCentre ?? string.Empty).Trim();
        if (costCentre.Length == 0)
        {
            costCentre = "CSTN";
        }

        costCentre = Clip(costCentre, 20);

        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional(
                "INSERT INTO `epc_jewel_color_stone_master` (`company_id`,`code`,`description`,`category`,`shape`,`clarity`,`size`,`color`,`finish`,`country`,`certificate_no`,`vendor`,`cost_centre`,`grade`) VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?) ON DUPLICATE KEY UPDATE `description` = VALUES(`description`), `category` = VALUES(`category`), `shape` = VALUES(`shape`), `clarity` = VALUES(`clarity`), `size` = VALUES(`size`), `color` = VALUES(`color`), `finish` = VALUES(`finish`), `country` = VALUES(`country`), `certificate_no` = VALUES(`certificate_no`)"),
            cancellationToken,
            companyId,
            code,
            Clip((request.Description ?? string.Empty).Trim(), 120),
            category,
            shape,
            clarity,
            Clip((request.Size ?? string.Empty).Trim(), 20),
            Clip((request.Color ?? string.Empty).Trim(), 20),
            finish,
            Clip((request.Country ?? string.Empty).Trim(), 30),
            Clip((request.CertificateNo ?? string.Empty).Trim(), 40),
            Clip((request.Vendor ?? string.Empty).Trim(), 20),
            costCentre,
            Clip((request.Grade ?? string.Empty).Trim(), 20)).ConfigureAwait(false);

        var id = await ErpDb.LastInsertIdAsync(connection, null, cancellationToken).ConfigureAwait(false);
        if (id <= 0)
        {
            id = await ErpDb.LongAsync(
                connection,
                null,
                ErpDb.Positional("SELECT `id` FROM `epc_jewel_color_stone_master` WHERE `company_id` = ? AND `code` = ? LIMIT 1"),
                cancellationToken,
                companyId,
                code).ConfigureAwait(false);
        }

        if (id <= 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Failed");
        }

        return ErpSimpleWriteResult.Ok("Color stone " + code + " saved", id);
    }

    private static async Task<bool> TableExistsAsync(DbConnection connection, string table, CancellationToken cancellationToken)
    {
        var n = await ErpDb.LongAsync(
            connection,
            null,
            ErpDb.Positional("SELECT COUNT(*) FROM information_schema.TABLES WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = ?"),
            cancellationToken,
            table).ConfigureAwait(false);
        return n > 0;
    }

    private static string Clip(string value, int maxLen)
        => value.Length <= maxLen ? value : value[..maxLen];
}
