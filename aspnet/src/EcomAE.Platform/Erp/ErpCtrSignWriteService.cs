using System.Data.Common;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace EcomAE.Platform.Erp;

/// <summary>
/// Live PHP <c>epc_ctr_sign</c> / ajax <c>ctr_sign</c> twin.
/// INSERT <c>epc_erp_contract_signatures</c> then set status signed.
/// Schema ensure and vendor OCR stay PHP. Does not CREATE tables.
/// </summary>
public interface IErpCtrSignWriteService
{
    Task<ErpSimpleWriteResult> SignAsync(
        ErpCtrSignWriteRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record ErpCtrSignWriteRequest(
    long ContractId = 0,
    string? SignerName = null,
    string? SignerEmail = null,
    string? Ip = null);

public sealed class ErpCtrSignWriteService : IErpCtrSignWriteService
{
    private readonly IErpWriteConnectionFactory _connections;

    public ErpCtrSignWriteService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<ErpSimpleWriteResult> SignAsync(
        ErpCtrSignWriteRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var name = request.SignerName ?? "";
        if (string.IsNullOrWhiteSpace(name.Trim()))
        {
            return ErpSimpleWriteResult.Fail("invalid", "Signer name is required");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        var email = request.SignerEmail ?? "";
        var ip = request.Ip ?? "";
        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        if (!await ColumnExistsAsync(connection, "epc_erp_contracts", "body_text", cancellationToken).ConfigureAwait(false)
            || !await ColumnExistsAsync(connection, "epc_erp_contract_signatures", "signature_hash", cancellationToken).ConfigureAwait(false))
        {
            return ErpSimpleWriteResult.Fail("invalid", "Contracts table is not provisioned");
        }

        var found = await ErpDb.LongAsync(
            connection,
            null,
            ErpDb.Positional("SELECT COUNT(*) FROM `epc_erp_contracts` WHERE `id` = ?"),
            cancellationToken,
            request.ContractId).ConfigureAwait(false);
        if (found <= 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Contract not found");
        }

        var version = await ErpDb.LongAsync(
            connection,
            null,
            ErpDb.Positional("SELECT `version` FROM `epc_erp_contracts` WHERE `id` = ? LIMIT 1"),
            cancellationToken,
            request.ContractId).ConfigureAwait(false);
        var body = await ErpDb.StringAsync(
            connection,
            null,
            ErpDb.Positional("SELECT `body_text` FROM `epc_erp_contracts` WHERE `id` = ? LIMIT 1"),
            cancellationToken,
            request.ContractId).ConfigureAwait(false) ?? "";

        var ts = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var hash = Sha256Hex(request.ContractId + "|" + version + "|" + body + "|" + name + "|" + email + "|" + ts);

        await using var tx = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        await ErpDb.ExecuteAsync(
            connection,
            tx,
            ErpDb.Positional(
                "INSERT INTO `epc_erp_contract_signatures` (`contract_id`,`signer_name`,`signer_email`,`signed_at`,`signature_hash`,`ip`) VALUES (?,?,?,?,?,?)"),
            cancellationToken,
            request.ContractId,
            name,
            email,
            ts,
            hash,
            ip).ConfigureAwait(false);
        var sigId = await ErpDb.LastInsertIdAsync(connection, tx, cancellationToken).ConfigureAwait(false);
        await ErpDb.ExecuteAsync(
            connection,
            tx,
            ErpDb.Positional("UPDATE `epc_erp_contracts` SET `status` = ?, `time_updated` = ? WHERE `id` = ?"),
            cancellationToken,
            "signed",
            ts,
            request.ContractId).ConfigureAwait(false);
        await tx.CommitAsync(cancellationToken).ConfigureAwait(false);
        return ErpSimpleWriteResult.Ok("Signed — " + hash[..16] + "…", sigId);
    }

    public static string Sha256Hex(string payload)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(payload));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    public static bool JsonFlag(JsonElement root, params string[] names)
    {
        if (root.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        foreach (var name in names)
        {
            if (!root.TryGetProperty(name, out var prop))
            {
                continue;
            }

            if (prop.ValueKind == JsonValueKind.True)
            {
                return true;
            }

            if (prop.ValueKind == JsonValueKind.Number && prop.TryGetInt64(out var n) && n != 0)
            {
                return true;
            }

            if (prop.ValueKind == JsonValueKind.String
                && !string.IsNullOrEmpty(prop.GetString())
                && prop.GetString() is not "0")
            {
                return true;
            }
        }

        return false;
    }

    public static string JsonText(JsonElement root, params string[] names)
    {
        if (root.ValueKind != JsonValueKind.Object)
        {
            return "";
        }

        foreach (var name in names)
        {
            if (!root.TryGetProperty(name, out var prop))
            {
                continue;
            }

            if (prop.ValueKind == JsonValueKind.String)
            {
                return prop.GetString() ?? "";
            }

            if (prop.ValueKind == JsonValueKind.Number)
            {
                return prop.GetRawText();
            }
        }

        return "";
    }

    public static long JsonLong(JsonElement root, params string[] names)
    {
        if (root.ValueKind != JsonValueKind.Object)
        {
            return 0;
        }

        foreach (var name in names)
        {
            if (!root.TryGetProperty(name, out var prop))
            {
                continue;
            }

            if (prop.ValueKind == JsonValueKind.Number && prop.TryGetInt64(out var n))
            {
                return n;
            }

            if (prop.ValueKind == JsonValueKind.String
                && long.TryParse(prop.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var i))
            {
                return i;
            }
        }

        return 0;
    }

    private static async Task<bool> ColumnExistsAsync(DbConnection connection, string table, string column, CancellationToken cancellationToken)
    {
        var n = await ErpDb.LongAsync(
            connection,
            null,
            ErpDb.Positional("SELECT COUNT(*) FROM information_schema.COLUMNS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = ? AND COLUMN_NAME = ?"),
            cancellationToken,
            table,
            column).ConfigureAwait(false);
        return n > 0;
    }
}
