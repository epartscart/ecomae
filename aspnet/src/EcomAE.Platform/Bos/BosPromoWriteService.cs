using System.Data.Common;
using System.Text.RegularExpressions;
using EcomAE.Platform.Erp;

namespace EcomAE.Platform.Bos;

/// <summary>
/// Live PHP <c>ajax_epc_bos.php</c> <c>promotions_engine</c> <c>record_usage</c> / <c>epc_promo_record_usage</c>.
/// Create, apply, and schema-ensure stay Classic. This service does not invent a send.
/// It does not emit CREATE/ALTER. PHP always returns ok — this write does not invent id/not-found checks.
/// </summary>
public interface IBosPromoWriteService
{
    Task<ErpSimpleWriteResult> RecordUsageAsync(
        long promotionId,
        string? siteKey,
        long customerId,
        string? orderRef,
        decimal discount,
        CancellationToken cancellationToken = default);
}

public sealed class BosPromoWriteService : IBosPromoWriteService
{
    private static readonly Regex SiteKeySafe = new("[^a-z0-9_]", RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private readonly IErpWriteConnectionFactory _connections;

    public BosPromoWriteService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    /// <summary>PHP ajax <c>preg_replace('/[^a-z0-9_]/', '', strtolower(...))</c>.</summary>
    public static string PhpBosSiteKey(string? raw)
        => SiteKeySafe.Replace((raw ?? "").ToLowerInvariant(), "");

    public async Task<ErpSimpleWriteResult> RecordUsageAsync(
        long promotionId,
        string? siteKey,
        long customerId,
        string? orderRef,
        decimal discount,
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
                    INSERT INTO `epc_promotion_usage` (`promotion_id`,`site_key`,`customer_id`,`order_ref`,`discount_amount`) VALUES (?,?,?,?,?)
                    """),
                cancellationToken, promotionId, PhpBosSiteKey(siteKey), customerId, orderRef ?? "", discount).ConfigureAwait(false);
            await ErpDb.ExecuteAsync(
                connection, null,
                ErpDb.Positional("UPDATE `epc_promotions` SET `used_count`=`used_count`+1 WHERE `id`=?"),
                cancellationToken, promotionId).ConfigureAwait(false);
            return ErpSimpleWriteResult.Ok("Promotion usage recorded", promotionId);
        }
        catch (DbException)
        {
            return ErpSimpleWriteResult.Fail("db", "Promotions table is missing — schema-ensure stays Classic.");
        }
    }
}
