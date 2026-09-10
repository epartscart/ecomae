using System.Data.Common;
using EcomAE.Platform.Erp;

namespace EcomAE.Platform.Bos;

/// <summary>
/// Live PHP <c>ajax_epc_bos.php</c> <c>dealer_portal</c> <c>auto_tier</c> / <c>epc_dealer_auto_tier</c>.
/// Register, place-order, and schema-ensure stay Classic. This service does not invent a send.
/// It does not emit CREATE/ALTER.
/// </summary>
public interface IBosDealerWriteService
{
    Task<ErpSimpleWriteResult> AutoTierAsync(
        long dealerId,
        CancellationToken cancellationToken = default);
}

public sealed class BosDealerWriteService : IBosDealerWriteService
{
    private static readonly (string Tier, decimal MinRevenue, int Discount)[] Tiers =
    [
        ("platinum", 500000m, 20),
        ("gold", 200000m, 15),
        ("silver", 50000m, 10),
        ("bronze", 0m, 5),
    ];

    private readonly IErpWriteConnectionFactory _connections;

    public BosDealerWriteService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<ErpSimpleWriteResult> AutoTierAsync(
        long dealerId,
        CancellationToken cancellationToken = default)
    {
        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "Database unavailable");
        }

        try
        {
            await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
            var foundId = await ErpDb.LongAsync(
                connection, null,
                ErpDb.Positional("SELECT `id` FROM `epc_dealers` WHERE `id` = ?"),
                cancellationToken, dealerId).ConfigureAwait(false);
            if (foundId == 0)
            {
                return ErpSimpleWriteResult.Fail("invalid", "Dealer not found");
            }

            var revenue = await ErpDb.DecimalAsync(
                connection, null,
                ErpDb.Positional("SELECT `ytd_revenue` FROM `epc_dealers` WHERE `id` = ?"),
                cancellationToken, dealerId).ConfigureAwait(false);
            var currentTier = await ErpDb.StringAsync(
                connection, null,
                ErpDb.Positional("SELECT `tier` FROM `epc_dealers` WHERE `id` = ?"),
                cancellationToken, dealerId).ConfigureAwait(false) ?? string.Empty;

            var newTier = "bronze";
            var discount = 5;
            foreach (var row in Tiers)
            {
                if (revenue >= row.MinRevenue)
                {
                    newTier = row.Tier;
                    discount = row.Discount;
                    break;
                }
            }

            if (!string.Equals(newTier, currentTier, StringComparison.Ordinal))
            {
                await ErpDb.ExecuteAsync(
                    connection, null,
                    ErpDb.Positional(
                        """
                        UPDATE `epc_dealers` SET `tier` = ?, `discount_pct` = ? WHERE `id` = ?
                        """),
                    cancellationToken, newTier, discount, dealerId).ConfigureAwait(false);
            }

            return ErpSimpleWriteResult.Ok("Dealer auto-tier applied", dealerId);
        }
        catch (DbException)
        {
            return ErpSimpleWriteResult.Fail("db", "Dealers table is missing — schema-ensure stays Classic.");
        }
    }
}
