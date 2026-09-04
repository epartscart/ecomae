using EcomAE.Platform.Erp;

namespace EcomAE.Platform.Cp;

/// <summary>
/// Live PHP <c>ajax_workshop_endpoint.php</c> twins for assign / save_bay / save_tech.
/// Seed, create-job, status helpers, and appointments stay PHP.
/// </summary>
public interface ICpWorkshopWriteService
{
    Task<ErpSimpleWriteResult> AssignAsync(long jobId, long bayId, long techId, CancellationToken cancellationToken = default);

    Task<ErpSimpleWriteResult> SaveBayAsync(long id, string? code, string? name, int active, int sortOrder, CancellationToken cancellationToken = default);

    Task<ErpSimpleWriteResult> SaveTechAsync(long id, string? name, string? phone, string? skill, int active, CancellationToken cancellationToken = default);
}

public sealed class CpWorkshopWriteService : ICpWorkshopWriteService
{
    private readonly IErpWriteConnectionFactory _connections;

    public CpWorkshopWriteService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<ErpSimpleWriteResult> AssignAsync(
        long jobId,
        long bayId,
        long techId,
        CancellationToken cancellationToken = default)
    {
        if (jobId <= 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "A job id is required.");
        }

        if (bayId < 0 || techId < 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Bay and technician ids cannot be negative.");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional("UPDATE `epc_ws_jobs` SET `bay_id` = ?, `tech_id` = ?, `time_updated` = ? WHERE `id` = ?"),
            cancellationToken,
            bayId, techId, DateTimeOffset.UtcNow.ToUnixTimeSeconds(), jobId);
        return ErpSimpleWriteResult.Ok("Assignment saved.", jobId);
    }

    public async Task<ErpSimpleWriteResult> SaveBayAsync(
        long id,
        string? code,
        string? name,
        int active,
        int sortOrder,
        CancellationToken cancellationToken = default)
    {
        var bayCode = (code ?? string.Empty).Trim().ToUpperInvariant();
        var bayName = (name ?? string.Empty).Trim();
        if (bayCode.Length == 0 || bayName.Length == 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Bay code and name required.");
        }

        if (bayCode.Length > 32)
        {
            bayCode = bayCode[..32];
        }

        if (bayName.Length > 190)
        {
            bayName = bayName[..190];
        }

        if (active is not (0 or 1) || sortOrder < 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Bay active must be 0 or 1.");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        if (id > 0)
        {
            await ErpDb.ExecuteAsync(
                connection,
                null,
                ErpDb.Positional("UPDATE `epc_ws_bays` SET `code` = ?, `name` = ?, `active` = ?, `sort_order` = ? WHERE `id` = ?"),
                cancellationToken,
                bayCode, bayName, active, sortOrder, id);
            return ErpSimpleWriteResult.Ok("Bay saved.", id);
        }

        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional("INSERT INTO `epc_ws_bays` (`code`,`name`,`active`,`sort_order`) VALUES (?,?,?,?)"),
            cancellationToken,
            bayCode, bayName, 1, sortOrder);
        var newId = await ErpDb.LastInsertIdAsync(connection, null, cancellationToken).ConfigureAwait(false);
        return ErpSimpleWriteResult.Ok("Bay saved.", newId);
    }

    public async Task<ErpSimpleWriteResult> SaveTechAsync(
        long id,
        string? name,
        string? phone,
        string? skill,
        int active,
        CancellationToken cancellationToken = default)
    {
        var techName = (name ?? string.Empty).Trim();
        if (techName.Length == 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Technician name required.");
        }

        if (techName.Length > 190)
        {
            techName = techName[..190];
        }

        if (id > 0 && active is not (0 or 1))
        {
            return ErpSimpleWriteResult.Fail("invalid", "Technician active must be 0 or 1.");
        }

        var phoneText = (phone ?? string.Empty).Trim();
        var skillText = (skill ?? string.Empty).Trim();
        if (phoneText.Length > 64)
        {
            phoneText = phoneText[..64];
        }

        if (skillText.Length > 190)
        {
            skillText = skillText[..190];
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        if (id > 0)
        {
            await ErpDb.ExecuteAsync(
                connection,
                null,
                ErpDb.Positional("UPDATE `epc_ws_technicians` SET `name` = ?, `phone` = ?, `skill` = ?, `active` = ? WHERE `id` = ?"),
                cancellationToken,
                techName, phoneText, skillText, active, id);
            return ErpSimpleWriteResult.Ok("Technician saved.", id);
        }

        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional("INSERT INTO `epc_ws_technicians` (`name`,`phone`,`skill`,`active`) VALUES (?,?,?,1)"),
            cancellationToken,
            techName, phoneText, skillText);
        var newId = await ErpDb.LastInsertIdAsync(connection, null, cancellationToken).ConfigureAwait(false);
        return ErpSimpleWriteResult.Ok("Technician saved.", newId);
    }
}
