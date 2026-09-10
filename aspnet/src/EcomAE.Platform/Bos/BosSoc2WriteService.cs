using System.Data.Common;
using EcomAE.Platform.Erp;

namespace EcomAE.Platform.Bos;

/// <summary>
/// Live PHP <c>ajax_epc_bos.php</c> <c>soc2_compliance</c> <c>add_evidence</c> / <c>epc_soc2_add_evidence</c>.
/// Update-control, create-policy, seed, and schema-ensure stay Classic. This service does not invent a send.
/// It does not emit CREATE/ALTER. PHP always returns ok.
/// </summary>
public interface IBosSoc2WriteService
{
    Task<ErpSimpleWriteResult> AddEvidenceAsync(
        string? controlId,
        string? evidenceType,
        string? title,
        string? filePath,
        string? collectedBy,
        string? validFrom,
        string? validTo,
        string? notes,
        CancellationToken cancellationToken = default);
}

public sealed class BosSoc2WriteService : IBosSoc2WriteService
{
    private readonly IErpWriteConnectionFactory _connections;

    public BosSoc2WriteService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<ErpSimpleWriteResult> AddEvidenceAsync(
        string? controlId,
        string? evidenceType,
        string? title,
        string? filePath,
        string? collectedBy,
        string? validFrom,
        string? validTo,
        string? notes,
        CancellationToken cancellationToken = default)
    {
        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "Database unavailable");
        }

        try
        {
            await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
            await ErpDb.ExecuteAsync(
                connection, null,
                ErpDb.Positional(
                    """
                    INSERT INTO `epc_soc2_evidence` (`control_id`,`evidence_type`,`title`,`file_path`,`collected_by`,`valid_from`,`valid_to`,`notes`) VALUES (?,?,?,?,?,?,?,?)
                    """),
                cancellationToken,
                controlId ?? "",
                evidenceType ?? "document",
                title ?? "",
                filePath ?? "",
                collectedBy ?? "",
                validFrom,
                validTo,
                notes ?? "").ConfigureAwait(false);
            var id = await ErpDb.LastInsertIdAsync(connection, null, cancellationToken).ConfigureAwait(false);
            return ErpSimpleWriteResult.Ok("SOC 2 evidence added", id);
        }
        catch (DbException)
        {
            return ErpSimpleWriteResult.Fail("db", "SOC 2 evidence table is missing — schema-ensure stays Classic.");
        }
    }
}
