using System.Data.Common;
using System.Text.Json;
using EcomAE.Platform.Erp;

namespace EcomAE.Platform.Bos;

/// <summary>
/// Live PHP <c>ajax_epc_bos.php</c> <c>soc2_compliance</c> <c>add_evidence</c> / <c>epc_soc2_add_evidence</c>
/// and <c>update_control</c> / <c>epc_soc2_update_control</c>.
/// Create-policy, seed, and schema-ensure stay Classic. This service does not invent a send.
/// It does not emit CREATE/ALTER.
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

    Task<ErpSimpleWriteResult> UpdateControlAsync(
        string? controlId,
        IReadOnlyDictionary<string, string?>? data,
        CancellationToken cancellationToken = default);
}

public sealed class BosSoc2WriteService : IBosSoc2WriteService
{
    public static readonly string[] AllowedControlFields =
    [
        "status",
        "implementation",
        "owner",
        "frequency",
        "last_tested",
        "next_review"
    ];

    private readonly IErpWriteConnectionFactory _connections;

    public BosSoc2WriteService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    /// <summary>PHP <c>json_decode((string)($_POST['control_data'] ?? '{}'), true) ?: array()</c>.</summary>
    public static Dictionary<string, string?> ParseControlData(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return new Dictionary<string, string?>(StringComparer.Ordinal);
        }

        try
        {
            using var doc = JsonDocument.Parse(raw);
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
            {
                return new Dictionary<string, string?>(StringComparer.Ordinal);
            }

            var parsed = new Dictionary<string, string?>(StringComparer.Ordinal);
            foreach (var property in doc.RootElement.EnumerateObject())
            {
                if (property.Value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
                {
                    continue;
                }

                parsed[property.Name] = property.Value.ValueKind == JsonValueKind.String
                    ? property.Value.GetString()
                    : property.Value.GetRawText();
            }

            return parsed;
        }
        catch (JsonException)
        {
            return new Dictionary<string, string?>(StringComparer.Ordinal);
        }
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

    public async Task<ErpSimpleWriteResult> UpdateControlAsync(
        string? controlId,
        IReadOnlyDictionary<string, string?>? data,
        CancellationToken cancellationToken = default)
    {
        var sets = new List<string>();
        var args = new List<object?>();
        foreach (var name in AllowedControlFields)
        {
            if (data is not null && data.ContainsKey(name))
            {
                sets.Add("`" + name + "` = ?");
                args.Add(data[name]);
            }
        }

        if (sets.Count == 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "No fields to update");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "Database unavailable");
        }

        args.Add(controlId ?? "");

        try
        {
            await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
            await ErpDb.ExecuteAsync(
                connection, null,
                ErpDb.Positional("UPDATE `epc_soc2_controls` SET " + string.Join(", ", sets) + " WHERE `control_id`=?"),
                cancellationToken,
                args.ToArray()).ConfigureAwait(false);
            return ErpSimpleWriteResult.Ok("SOC 2 control updated", 0);
        }
        catch (DbException)
        {
            return ErpSimpleWriteResult.Fail("db", "SOC 2 controls table is missing — schema-ensure stays Classic.");
        }
    }
}
