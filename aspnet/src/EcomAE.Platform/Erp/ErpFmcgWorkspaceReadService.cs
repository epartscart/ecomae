using System.Data.Common;

namespace EcomAE.Platform.Erp;

public sealed record ErpFmcgItemDigest(
    long Id, string Sku, string Name, string Brand, string Category, string Gtin,
    string BaseUom, int UnitsPerCase, int CasesPerPallet, int ShelfLifeDays,
    bool BatchTracked, bool ExpiryTracked, bool Fefo, int MinShelfLifePct,
    string ExciseCategory, long CoaSalesId, long CoaCogsId, long CoaInventoryId,
    decimal Cost, decimal Price, string Channel);

public sealed record ErpFmcgBatchDigest(
    long Id, long ItemId, string Sku, string ItemName, string BatchNo,
    long MfgDate, long ExpiryDate, decimal QtyOnHand, string Warehouse,
    string Status, bool RecallFlag, int DaysToExpiry);

public sealed record ErpFmcgChannelDigest(long Id, string Code, string Name, string Kind, long SalesAccountId);
public sealed record ErpFmcgPromotionDigest(
    long Id, string Code, string Name, string Kind, string ItemSku,
    decimal BuyQty, decimal FreeQty, decimal DiscountPct, long StartDate, long EndDate, bool Active);
public sealed record ErpFmcgRouteDigest(long Id, string Code, string Name, string Salesperson, string Vehicle, string Warehouse);

public sealed record ErpFmcgWorkspaceSnapshot(
    IReadOnlyList<ErpFmcgItemDigest> Items,
    IReadOnlyList<ErpFmcgBatchDigest> Batches,
    IReadOnlyList<ErpFmcgChannelDigest> Channels,
    IReadOnlyList<ErpFmcgPromotionDigest> Promotions,
    IReadOnlyList<ErpFmcgRouteDigest> Routes,
    long LegalEntityCount,
    long BusinessUnitCount,
    long CostCentreCount,
    string CountryCode,
    string Source,
    string Message);

public interface IErpFmcgWorkspaceReadService
{
    Task<ErpFmcgWorkspaceSnapshot> SnapshotAsync(string countryCode, CancellationToken cancellationToken = default);
}

public sealed class ErpFmcgWorkspaceReadService : IErpFmcgWorkspaceReadService
{
    private readonly IErpWriteConnectionFactory _connections;

    public ErpFmcgWorkspaceReadService(IErpWriteConnectionFactory connections) => _connections = connections;

    public async Task<ErpFmcgWorkspaceSnapshot> SnapshotAsync(string countryCode, CancellationToken cancellationToken = default)
    {
        var pack = ErpGccLocalizationCatalog.Resolve(countryCode);
        var empty = new ErpFmcgWorkspaceSnapshot([], [], [], [], [], 0, 0, 0, pack.Code, "none", "");
        if (!_connections.IsConfigured)
        {
            return empty with { Source = "config", Message = "TenantRegistry DB is not configured." };
        }

        try
        {
            await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
            await ErpFmcgWorkspaceSchema.EnsureAsync(connection, cancellationToken).ConfigureAwait(false);
            await ErpLazySchema.EnsureTenantOrganizationAsync(connection, cancellationToken).ConfigureAwait(false);

            // Compliance always follows the tenant's registered country
            // (epc_price_settings.company_country_code); a `country` query
            // value is a preview lookup only.
            var tenantCountry = await ErpDb.StringAsync(connection, null,
                "SELECT `setting_value` FROM `epc_price_settings` WHERE `setting_key`='company_country_code' LIMIT 1",
                cancellationToken).ConfigureAwait(false);
            pack = string.IsNullOrWhiteSpace(countryCode)
                ? ErpGccLocalizationCatalog.Resolve(string.IsNullOrWhiteSpace(tenantCountry) ? "QA" : tenantCountry)
                : pack;

            var items = new List<ErpFmcgItemDigest>();
            await ReadAsync(connection, cancellationToken,
                "SELECT `id`,`sku`,`name`,`brand`,`category`,`gtin`,`base_uom`,`units_per_case`,`cases_per_pallet`,"
                + "`shelf_life_days`,`batch_tracked`,`expiry_tracked`,`fefo`,`min_shelf_life_pct`,`excise_category`,"
                + "`coa_sales_id`,`coa_cogs_id`,`coa_inventory_id`,`cost`,`price`,`channel`"
                + " FROM `" + ErpFmcgWorkspaceSchema.Items + "` WHERE `active`=1 ORDER BY `category`,`sku`",
                r => items.Add(new ErpFmcgItemDigest(
                    I64(r, 0), S(r, 1), S(r, 2), S(r, 3), S(r, 4), S(r, 5), S(r, 6),
                    I32(r, 7), I32(r, 8), I32(r, 9),
                    I32(r, 10) == 1, I32(r, 11) == 1, I32(r, 12) == 1, I32(r, 13),
                    S(r, 14), I64(r, 15), I64(r, 16), I64(r, 17), D(r, 18), D(r, 19), S(r, 20))));

            var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var batches = new List<ErpFmcgBatchDigest>();
            await ReadAsync(connection, cancellationToken,
                "SELECT b.`id`,b.`item_id`,i.`sku`,i.`name`,b.`batch_no`,b.`mfg_date`,b.`expiry_date`,"
                + "b.`qty_on_hand`,b.`warehouse`,b.`status`,b.`recall_flag`"
                + " FROM `" + ErpFmcgWorkspaceSchema.Batches + "` b"
                + " LEFT JOIN `" + ErpFmcgWorkspaceSchema.Items + "` i ON i.`id`=b.`item_id`"
                + " ORDER BY b.`expiry_date`,b.`batch_no` LIMIT 500",
                r => batches.Add(new ErpFmcgBatchDigest(
                    I64(r, 0), I64(r, 1), S(r, 2), S(r, 3), S(r, 4), I64(r, 5), I64(r, 6),
                    D(r, 7), S(r, 8), S(r, 9), I32(r, 10) == 1,
                    (int)((I64(r, 6) - now) / 86400))));

            var channels = new List<ErpFmcgChannelDigest>();
            await ReadAsync(connection, cancellationToken,
                "SELECT `id`,`code`,`name`,`kind`,`sales_account_id` FROM `" + ErpFmcgWorkspaceSchema.Channels + "` WHERE `active`=1 ORDER BY `code`",
                r => channels.Add(new ErpFmcgChannelDigest(I64(r, 0), S(r, 1), S(r, 2), S(r, 3), I64(r, 4))));

            var promotions = new List<ErpFmcgPromotionDigest>();
            await ReadAsync(connection, cancellationToken,
                "SELECT p.`id`,p.`code`,p.`name`,p.`kind`,IFNULL(i.`sku`,''),p.`buy_qty`,p.`free_qty`,p.`discount_pct`,p.`start_date`,p.`end_date`,p.`active`"
                + " FROM `" + ErpFmcgWorkspaceSchema.Promotions + "` p"
                + " LEFT JOIN `" + ErpFmcgWorkspaceSchema.Items + "` i ON i.`id`=p.`item_id`"
                + " ORDER BY p.`code`",
                r => promotions.Add(new ErpFmcgPromotionDigest(
                    I64(r, 0), S(r, 1), S(r, 2), S(r, 3), S(r, 4), D(r, 5), D(r, 6), D(r, 7),
                    I64(r, 8), I64(r, 9), I32(r, 10) == 1)));

            var routes = new List<ErpFmcgRouteDigest>();
            await ReadAsync(connection, cancellationToken,
                "SELECT `id`,`code`,`name`,`salesperson`,`vehicle`,`warehouse` FROM `" + ErpFmcgWorkspaceSchema.Routes + "` WHERE `active`=1 ORDER BY `code`",
                r => routes.Add(new ErpFmcgRouteDigest(I64(r, 0), S(r, 1), S(r, 2), S(r, 3), S(r, 4), S(r, 5))));

            var entities = await ScalarAsync(connection, "SELECT COUNT(*) FROM `epc_erp_pm_legal_entities` WHERE `active`=1", cancellationToken);
            var bus = await ScalarAsync(connection, "SELECT COUNT(*) FROM `epc_erp_pm_business_units`", cancellationToken);
            var ccs = await ScalarAsync(connection, "SELECT COUNT(*) FROM `epc_erp_pm_cost_centres`", cancellationToken);

            return new ErpFmcgWorkspaceSnapshot(
                items, batches, channels, promotions, routes, entities, bus, ccs,
                pack.Code, "database", string.Empty);
        }
        catch (Exception ex)
        {
            return empty with { Source = "database-error", Message = ex.Message };
        }
    }

    private static async Task ReadAsync(DbConnection c, CancellationToken ct, string sql, Action<DbDataReader> row)
    {
        await using var cmd = c.CreateCommand();
        cmd.CommandText = sql;
        await using var r = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await r.ReadAsync(ct).ConfigureAwait(false)) row(r);
    }

    private static async Task<long> ScalarAsync(DbConnection c, string sql, CancellationToken ct)
    {
        try
        {
            await using var cmd = c.CreateCommand();
            cmd.CommandText = sql;
            var v = await cmd.ExecuteScalarAsync(ct).ConfigureAwait(false);
            return v is null or DBNull ? 0 : Convert.ToInt64(v);
        }
        catch { return 0; }
    }

    private static long I64(DbDataReader r, int i) => r.IsDBNull(i) ? 0 : Convert.ToInt64(r.GetValue(i));
    private static int I32(DbDataReader r, int i) => r.IsDBNull(i) ? 0 : Convert.ToInt32(r.GetValue(i));
    private static decimal D(DbDataReader r, int i) => r.IsDBNull(i) ? 0m : Convert.ToDecimal(r.GetValue(i));
    private static string S(DbDataReader r, int i) => r.IsDBNull(i) ? string.Empty : Convert.ToString(r.GetValue(i)) ?? string.Empty;
}
