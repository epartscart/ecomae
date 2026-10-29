using System.Data.Common;
using System.Globalization;

namespace EcomAE.Platform.Erp;

public sealed record ErpJwGoldKaratRow(string Karat, int Pcs, decimal GrossGrams, decimal PureGrams, decimal Value, decimal BoardRate);

public sealed record ErpJwGoldFlow(string Label, int Pcs, decimal GrossGrams, decimal PureGrams, decimal Value, string Href);

public sealed record ErpJwGoldDashboard(
    IReadOnlyList<ErpJwGoldKaratRow> Stock,
    IReadOnlyList<ErpJwGoldFlow> Flows,
    decimal OldGoldValue)
{
    public static readonly ErpJwGoldDashboard Empty = new([], [], 0);

    public decimal StockGross => Stock.Sum(s => s.GrossGrams);

    public decimal StockPure => Stock.Sum(s => s.PureGrams);

    public decimal StockValue => Stock.Sum(s => s.Value);

    /// <summary>Weighted-average carrying cost per pure gram.</summary>
    public decimal CostPerPureGram => StockPure > 0 ? Math.Round(StockValue / StockPure, 2) : 0;
}

/// <summary>Jewellery home-dashboard figures in weight (gross / pure grams) and value, by karat and period flow.</summary>
public interface IErpJwDashboardGoldReadService
{
    Task<ErpJwGoldDashboard> ReadAsync(long companyId, string? from, string? to, CancellationToken cancellationToken = default);
}

public sealed class ErpJwDashboardGoldReadService : IErpJwDashboardGoldReadService
{
    private readonly IErpWriteConnectionFactory _connections;

    public ErpJwDashboardGoldReadService(IErpWriteConnectionFactory connections) => _connections = connections;

    public async Task<ErpJwGoldDashboard> ReadAsync(long companyId, string? from, string? to, CancellationToken cancellationToken = default)
    {
        if (!_connections.IsConfigured) return ErpJwGoldDashboard.Empty;
        try
        {
            await using var c = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
            if (!await ErpDb.TableKnownAsync(c, "epc_jewel_metal_stock", cancellationToken).ConfigureAwait(false)) return ErpJwGoldDashboard.Empty;
            if (companyId <= 0) companyId = await ErpLazySchema.ResolveActiveCompanyIdAsync(c, 0, cancellationToken).ConfigureAwait(false);
            var start = Date(from) ?? new DateOnly(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1);
            var end = Date(to) ?? DateOnly.FromDateTime(DateTime.UtcNow);

            var rates = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
            if (await ErpDb.TableKnownAsync(c, "epc_gold_rates", cancellationToken).ConfigureAwait(false))
            {
                await using var rc = c.CreateCommand();
                rc.CommandText = ErpDb.Positional(
                    "SELECT `karat`,`sell_rate`,IFNULL(`unit`,'gram') FROM `epc_gold_rates` WHERE `company_id` IN (0,?) AND `currency`='AED' AND `sell_rate`>0 AND `rate_date`<=?"
                    + " ORDER BY `rate_date` ASC,(`created_by`='system') DESC,`id` ASC");
                ErpDb.AddParameters(rc, companyId, end.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
                await using var rr = await rc.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                while (await rr.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    rates[ErpGoldRateBoard.NormalizeKarat(rr.GetString(0))] = D(rr, 1);
                }
            }

            var stock = new List<ErpJwGoldKaratRow>();
            await using (var cmd = c.CreateCommand())
            {
                cmd.CommandText = ErpDb.Positional(
                    "SELECT IFNULL(NULLIF(`karat`,''),'—'),SUM(IFNULL(`stock_pcs`,0)),SUM(IFNULL(`stock_gms`,0)),"
                    + "SUM(IFNULL(`stock_gms`,0)*IF(IFNULL(`purity`,0)>1,`purity`/1000,IFNULL(`purity`,0))),SUM(IFNULL(`stock_value`,0))"
                    + " FROM `epc_jewel_metal_stock` WHERE `company_id` IN (0,?) GROUP BY 1 HAVING SUM(IFNULL(`stock_gms`,0))<>0 OR SUM(IFNULL(`stock_value`,0))<>0 ORDER BY 1");
                ErpDb.AddParameters(cmd, companyId);
                await using var r = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                while (await r.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    var karat = r.GetString(0);
                    rates.TryGetValue(ErpGoldRateBoard.NormalizeKarat(karat), out var rate);
                    stock.Add(new(karat, Convert.ToInt32(D(r, 1)), Math.Round(D(r, 2), 3), Math.Round(D(r, 3), 3), Math.Round(D(r, 4), 2), rate));
                }
            }

            var flows = new List<ErpJwGoldFlow>();
            decimal oldGold = 0;
            if (await ErpDb.TableKnownAsync(c, "epc_jewel_voucher_lines", cancellationToken).ConfigureAwait(false))
            {
                var groups = new (string Label, IReadOnlyList<string> Types, string Href)[]
                {
                    ("Sold", ErpJwVocTypes.SalesAnalysisAliases.Concat(ErpJwVocTypes.RetailAliases).Concat(ErpJwVocTypes.MetalSaleAliases).Distinct().ToList(), "/erp/jewellery-retail-app?tab=jw_sales_analysis"),
                    ("Purchased", ErpJwVocTypes.MetalPurchaseAliases.Concat(ErpJwVocTypes.DiamondPurchaseAliases).Distinct().ToList(), "/erp/jewellery-fixing-app?tab=jw_metal_purchase"),
                    ("Returned by customers", ErpJwVocTypes.SalesReturnAliases, "/erp/jewellery-retail-app?tab=jw_sales_return"),
                };
                foreach (var g in groups)
                {
                    await using var cmd = c.CreateCommand();
                    cmd.CommandText = ErpDb.Positional(
                        "SELECT IFNULL(SUM(l.`pcs`),0),IFNULL(SUM(l.`gr_wt`),0),IFNULL(SUM(IF(l.`pure_wt`>0,l.`pure_wt`,l.`gr_wt`*IF(l.`purity`>1,l.`purity`/1000,l.`purity`))),0),IFNULL(SUM(l.`total_amount`),0)"
                        + " FROM `epc_jewel_voucher_lines` l JOIN `epc_jewel_voucher` v ON v.`id`=l.`voucher_id`"
                        + " WHERE v.`company_id` IN (0,?) AND v.`status` NOT IN ('draft','cancelled') AND v.`voc_type` IN (" + string.Join(",", g.Types.Select(_ => "?")) + ")"
                        + " AND DATE(v.`voc_date`)>=? AND DATE(v.`voc_date`)<=?");
                    ErpDb.AddParameters(cmd, new object?[] { companyId }.Concat(g.Types).Append(start.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)).Append(end.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)).ToArray());
                    await using var r = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                    if (await r.ReadAsync(cancellationToken).ConfigureAwait(false))
                    {
                        flows.Add(new(g.Label, Convert.ToInt32(D(r, 0)), Math.Round(D(r, 1), 3), Math.Round(D(r, 2), 3), Math.Round(D(r, 3), 2), g.Href));
                    }
                }

                oldGold = await ErpDb.DecimalAsync(c, null, ErpDb.Positional(
                    "SELECT IFNULL(SUM(`old_gold_exchange`),0) FROM `epc_jewel_voucher` WHERE `company_id` IN (0,?) AND `status` NOT IN ('draft','cancelled') AND DATE(`voc_date`)>=? AND DATE(`voc_date`)<=?"),
                    cancellationToken, companyId, start.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), end.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)).ConfigureAwait(false);
            }

            return new(stock, flows, Math.Round(oldGold, 2));
        }
        catch (DbException)
        {
            return ErpJwGoldDashboard.Empty;
        }
    }

    private static DateOnly? Date(string? raw)
        => DateOnly.TryParseExact((raw ?? "").Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d) ? d : null;

    private static decimal D(DbDataReader r, int i) => r.IsDBNull(i) ? 0m : Convert.ToDecimal(r.GetValue(i), CultureInfo.InvariantCulture);
}
