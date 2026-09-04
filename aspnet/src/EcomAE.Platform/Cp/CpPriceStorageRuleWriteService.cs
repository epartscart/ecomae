using EcomAE.Platform.Erp;

namespace EcomAE.Platform.Cp;

/// <summary>
/// Live PHP <c>epc_pm_storage_panel.php</c> twins for rule DELETEs.
/// Save / upsert stay PHP (brand/article normalize + ON DUPLICATE KEY).
/// </summary>
public interface ICpPriceStorageRuleWriteService
{
    Task<ErpSimpleWriteResult> DeleteAsync(string? kind, long ruleId, CancellationToken cancellationToken = default);
}

public sealed class CpPriceStorageRuleWriteService : ICpPriceStorageRuleWriteService
{
    private readonly IErpWriteConnectionFactory _connections;

    public CpPriceStorageRuleWriteService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<ErpSimpleWriteResult> DeleteAsync(
        string? kind,
        long ruleId,
        CancellationToken cancellationToken = default)
    {
        if (ruleId <= 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "A rule id is required.");
        }

        if (!TryTable(kind, out var table, out var message))
        {
            return ErpSimpleWriteResult.Fail("invalid", "Unknown price-storage rule kind.");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional("DELETE FROM `" + table + "` WHERE `id` = ?"),
            cancellationToken,
            ruleId);
        return ErpSimpleWriteResult.Ok(message, ruleId);
    }

    internal static bool TryTable(string? kind, out string table, out string message)
    {
        switch ((kind ?? string.Empty).Trim().ToLowerInvariant())
        {
            case "storage":
            case "delete_storage_rule":
                table = "epc_price_storage_rules";
                message = "Supplier overall rule deleted";
                return true;
            case "brand":
            case "delete_storage_brand_rule":
                table = "epc_price_storage_brand_rules";
                message = "Supplier brand rule deleted";
                return true;
            case "article":
            case "delete_storage_article_rule":
                table = "epc_price_storage_article_rules";
                message = "Supplier article rule deleted";
                return true;
            default:
                table = string.Empty;
                message = string.Empty;
                return false;
        }
    }
}
