using System.Data.Common;
using System.Globalization;

namespace EcomAE.Platform.Erp;

/// <summary>
/// Loads/removes DEMO jewellery sample data for the workspace: metal-stock
/// items (karat/purity/gram pricing), a metal purchase voucher, retail sale
/// vouchers and a repair job — written into the real epc_jewel_* tables with a
/// registry so "Remove sample data" deletes only demo rows, then posts the
/// corresponding GL journals. Mirrors <see cref="ErpFitOutDemoSeedWriteService"/>.
/// </summary>
public interface IErpJewelleryDemoSeedWriteService
{
    Task<string> SeedDemoAsync(CancellationToken cancellationToken = default);
    Task<string> ClearDemoAsync(CancellationToken cancellationToken = default);
}

public sealed class ErpJewelleryDemoSeedWriteService : IErpJewelleryDemoSeedWriteService
{
    private const string RegistryTable = "ecomae_jw_demo_registry";

    private sealed class DemoMarks
    {
        private readonly Dictionary<string, List<long>> _ids = new(StringComparer.Ordinal);
        public void Add(string table, long id)
        {
            if (id <= 0) return;
            if (!_ids.TryGetValue(table, out var list)) _ids[table] = list = [];
            list.Add(id);
        }
        public IReadOnlyDictionary<string, List<long>> All => _ids;
    }

    private readonly IErpWriteConnectionFactory _connections;
    private readonly IErpJewelleryGlPostWriteService _glPost;

    public ErpJewelleryDemoSeedWriteService(
        IErpWriteConnectionFactory connections,
        IErpJewelleryGlPostWriteService glPost)
    {
        _connections = connections;
        _glPost = glPost;
    }

    public async Task<string> SeedDemoAsync(CancellationToken cancellationToken = default)
    {
        if (!_connections.IsConfigured)
        {
            return "TenantRegistry DB is not configured — demo seed skipped.";
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await ErpJewelSchema.EnsureAsync(connection, cancellationToken).ConfigureAwait(false);
        await ErpJewelleryWorkspaceSchema.EnsureAsync(connection, cancellationToken).ConfigureAwait(false);
        await EnsureRegistryAsync(connection, cancellationToken).ConfigureAwait(false);

        // Idempotent: clear any earlier demo run first so re-seeding is clean.
        await ClearDemoAsync(cancellationToken).ConfigureAwait(false);

        var marks = new DemoMarks();
        var today = DateTimeOffset.UtcNow.UtcDateTime.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        var companyId = await ErpLazySchema.ResolveActiveCompanyIdAsync(connection, 0, cancellationToken).ConfigureAwait(false);

        var items = new (string Code, string Desc, string Metal, string Karat, decimal Purity, string Type, string Brand, string Cat, decimal PurCost, decimal SalePrice, decimal Qty, decimal Gms)[]
        {
            ("JW-DEMO-RING22", "22K Plain Gold Ring", "G", "22", 0.916m, "Ring", "DEMO Gold Co", "Plain", 285m, 310m, 12, 96m),
            ("JW-DEMO-CHAIN21", "21K Rope Chain 40cm", "G", "21", 0.875m, "Chain", "DEMO Gold Co", "Chains", 272m, 298m, 8, 120m),
            ("JW-DEMO-BANGLE22", "22K Bangle Set (4pc)", "G", "22", 0.916m, "Bangle", "DEMO Gold Co", "Bangles", 285m, 315m, 6, 84m),
            ("JW-DEMO-EARR18", "18K Diamond Studs", "G", "18", 0.750m, "Earring", "DEMO Diamonds", "Diamond", 320m, 390m, 10, 32m),
            ("JW-DEMO-BAR24", "24K Minted Bar 10g", "G", "24", 0.999m, "Bullion", "DEMO Bullion", "Bars", 295m, 302m, 15, 150m),
        };

        var itemIds = new List<long>();
        foreach (var i in items)
        {
            var id = await InsertAsync(connection,
                "INSERT INTO `epc_jewel_metal_stock` (`company_id`,`item_code`,`description`,`metal`,`karat`,`purity`,`type`,`brand`,`category`,`pur_cost_gms`,`sale_price_gms`,`stock_qty`,`stock_pcs`,`stock_gms`,`stock_value`,`created_by`)"
                + " VALUES (?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?)",
                cancellationToken,
                companyId, i.Code, i.Desc, i.Metal, i.Karat, i.Purity, i.Type, i.Brand, i.Cat,
                i.PurCost, i.SalePrice, i.Qty, (int)i.Qty, i.Gms, i.Gms * i.PurCost, "DEMO");
            marks.Add("epc_jewel_metal_stock", id);
            itemIds.Add(id);
        }

        // Metal purchase voucher covering the opening stock.
        var purchaseNet = items.Sum(i => i.Gms * i.PurCost);
        var purchaseVat = Math.Round(purchaseNet * 0.05m, 2);
        var purId = await InsertAsync(connection,
            "INSERT INTO `epc_jewel_voucher` (`company_id`,`voc_type`,`voc_date`,`voc_no`,`party_name`,`customer_name`,`status`,`authorized`,`net_amount`,`sub_total`,`gross_total`,`vat_amount`,`total_with_vat`,`remarks`,`created_by`)"
            + " VALUES (?,?,?,?,?,?,?,1,?,?,?,?,?,?,?)",
            cancellationToken,
            companyId, ErpJwVocTypes.MetalPurchase, today, 1, "DEMO Gold Bullion Trading LLC", "", "posted",
            purchaseNet, purchaseNet, purchaseNet, purchaseVat, purchaseNet + purchaseVat, "DEMO opening stock purchase", "DEMO");
        marks.Add("epc_jewel_voucher", purId);

        var lineNo = 1;
        foreach (var i in items)
        {
            var metal = i.Gms * i.PurCost;
            var lid = await InsertAsync(connection,
                "INSERT INTO `epc_jewel_voucher_lines` (`voucher_id`,`line_no`,`stock_code`,`description`,`pcs`,`gr_wt`,`purity`,`metal_amount`,`total_amount`,`total_with_vat`,`net_amount`)"
                + " VALUES (?,?,?,?,?,?,?,?,?,?,?)",
                cancellationToken,
                purId, lineNo++, i.Code, i.Desc, (int)i.Qty, i.Gms, i.Purity, metal, metal, metal, metal);
            marks.Add("epc_jewel_voucher_lines", lid);
        }

        // Two retail sales (RIN) at sale price per gram + making charge, 5% VAT.
        var sales = new (string Customer, (int Item, decimal Gms, decimal Making)[] Lines, int DaysAgo)[]
        {
            ("DEMO Customer — Aisha M", [(0, 8m, 240m), (3, 4m, 380m)], 9),
            ("DEMO Customer — Omar S", [(1, 15m, 320m), (4, 10m, 60m)], 3),
        };

        var vocNo = 1;
        foreach (var s in sales)
        {
            var net = s.Lines.Sum(l => items[l.Item].SalePrice * l.Gms + l.Making);
            var vat = Math.Round(net * 0.05m, 2);
            var vocDate = DateTimeOffset.UtcNow.AddDays(-s.DaysAgo).UtcDateTime.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            var vid = await InsertAsync(connection,
                "INSERT INTO `epc_jewel_voucher` (`company_id`,`voc_type`,`voc_date`,`voc_no`,`party_name`,`customer_name`,`status`,`authorized`,`net_amount`,`sub_total`,`gross_total`,`vat_amount`,`total_with_vat`,`metal_rate`,`remarks`,`created_by`)"
                + " VALUES (?,?,?,?,?,?,?,1,?,?,?,?,?,?,?,?)",
                cancellationToken,
                companyId, ErpJwVocTypes.RetailInvoice, vocDate, vocNo++, s.Customer, s.Customer, "posted",
                net, net, net, vat, net + vat, 302m, "DEMO retail sale", "DEMO");
            marks.Add("epc_jewel_voucher", vid);

            var ln = 1;
            foreach (var (itemIdx, gms, making) in s.Lines)
            {
                var it = items[itemIdx];
                var metal = it.SalePrice * gms;
                var lineNet = metal + making;
                var lid = await InsertAsync(connection,
                    "INSERT INTO `epc_jewel_voucher_lines` (`voucher_id`,`line_no`,`stock_code`,`description`,`pcs`,`gr_wt`,`purity`,`mkg_amount`,`metal_rate`,`metal_amount`,`total_amount`,`total_with_vat`,`net_amount`,`vat_pct`,`vat_amount`,`cost_amount`)"
                    + " VALUES (?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?)",
                    cancellationToken,
                    vid, ln++, it.Code, it.Desc, 1, gms, it.Purity, making, it.SalePrice, metal, lineNet, lineNet + Math.Round(lineNet * 0.05m, 2), lineNet,
                    5m, Math.Round(lineNet * 0.05m, 2), it.PurCost * gms);
                marks.Add("epc_jewel_voucher_lines", lid);

                // Issue the sold grams from stock at cost so the stock report,
                // inventory (GL 1300) and cost of sales (GL 5000) agree.
                await using var issue = connection.CreateCommand();
                issue.CommandText = ErpDb.Positional(
                    "UPDATE `epc_jewel_metal_stock` SET `stock_gms`=`stock_gms`-?,`stock_value`=`stock_value`-?,`stock_pcs`=GREATEST(`stock_pcs`-1,0),`stock_qty`=GREATEST(`stock_qty`-1,0) WHERE `id`=?");
                ErpDb.AddParameters(issue, gms, it.PurCost * gms, itemIds[itemIdx]);
                await issue.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }
        }

        // One repair job in the workshop.
        var repId = await InsertAsync(connection,
            "INSERT INTO `epc_jewel_repair` (`company_id`,`voc_type`,`voc_date`,`voc_no`,`customer_name`,`mobile`,`repair_narration`,`status`,`delivery_date`,`created_by`)"
            + " VALUES (?,?,?,?,?,?,?,?,?,?)",
            cancellationToken,
            companyId, ErpJwVocTypes.RepairReceipt, today, 1, "DEMO Customer — Fatima K", "0500000000",
            "DEMO | 22K chain — clasp repair, est AED 150", "in_workshop", today, "DEMO");
        marks.Add("epc_jewel_repair", repId);

        await PersistMarksAsync(connection, marks, cancellationToken).ConfigureAwait(false);

        // Demo lands on the financial statements too — post journals for the
        // demo vouchers only (never sweep every unposted live voucher).
        var glPosts = 0;
        if (marks.All.TryGetValue("epc_jewel_voucher", out var seededVouchers))
        {
            foreach (var id in seededVouchers)
            {
                await _glPost.PostAsync(id, null, cancellationToken).ConfigureAwait(false);
                glPosts++;
            }
        }
        var glNote = glPosts > 0
            ? "Posted to GL for " + glPosts + " demo voucher(s)."
            : "No demo journals to post.";

        return "Loaded DEMO jewellery data — " + items.Length + " metal-stock items, 1 purchase voucher, "
            + sales.Length + " retail sales and 1 repair job. " + glNote;
    }

    public async Task<string> ClearDemoAsync(CancellationToken cancellationToken = default)
    {
        if (!_connections.IsConfigured)
        {
            return "TenantRegistry DB is not configured — demo clear skipped.";
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await EnsureRegistryAsync(connection, cancellationToken).ConfigureAwait(false);
        var removed = 0;

        // Demo GL journals first — matched to the registered demo voucher rows.
        foreach (var sql in DemoJournalDeletes())
        {
            try { removed += await ErpDb.ExecuteAsync(connection, null, sql, cancellationToken).ConfigureAwait(false); }
            catch (DbException) { }
        }

        // Invoice links created from demo vouchers — and the draft e-invoice
        // documents they point at, so "Remove sample data" leaves no orphan
        // documents in the e-invoice list. Only draft documents created by the
        // demo issue flow can match (submitted/accepted are never deleted).
        foreach (var sql in new[]
        {
            "DELETE l FROM `epc_einvoice_lines` l INNER JOIN `ecomae_jw_invoice_links` k"
            + " ON k.`invoice_id`=l.`document_id` INNER JOIN `" + RegistryTable + "` r"
            + " ON r.`table_name`='epc_jewel_voucher' AND r.`row_id`=k.`voucher_id`"
            + " INNER JOIN `epc_einvoice_documents` d ON d.`id`=k.`invoice_id`"
            + " WHERE d.`status` NOT IN ('submitted','accepted','queued')",
            "DELETE e FROM `epc_einvoice_events` e INNER JOIN `ecomae_jw_invoice_links` k"
            + " ON k.`invoice_id`=e.`document_id` INNER JOIN `" + RegistryTable + "` r"
            + " ON r.`table_name`='epc_jewel_voucher' AND r.`row_id`=k.`voucher_id`"
            + " INNER JOIN `epc_einvoice_documents` d ON d.`id`=k.`invoice_id`"
            + " WHERE d.`status` NOT IN ('submitted','accepted','queued')",
            "DELETE d FROM `epc_einvoice_documents` d INNER JOIN `ecomae_jw_invoice_links` k"
            + " ON k.`invoice_id`=d.`id` INNER JOIN `" + RegistryTable + "` r"
            + " ON r.`table_name`='epc_jewel_voucher' AND r.`row_id`=k.`voucher_id`"
            + " WHERE d.`status` NOT IN ('submitted','accepted','queued')",
            // Links last — after the document row: drops links whose document
            // was deleted (LEFT JOIN null) or still draft, keeps links whose
            // document survives (submitted/accepted/queued).
            "DELETE k FROM `ecomae_jw_invoice_links` k INNER JOIN `" + RegistryTable + "` r"
            + " ON r.`table_name`='epc_jewel_voucher' AND r.`row_id`=k.`voucher_id`"
            + " LEFT JOIN `epc_einvoice_documents` d ON d.`id`=k.`invoice_id`"
            + " WHERE d.`id` IS NULL OR d.`status` NOT IN ('submitted','accepted','queued')",
        })
        {
            try
            {
                removed += await ErpDb.ExecuteAsync(connection, null, sql, cancellationToken).ConfigureAwait(false);
            }
            catch (DbException) { }
        }

        foreach (var table in new[]
        {
            "epc_jewel_voucher_lines",
            "epc_jewel_voucher",
            "epc_jewel_repair",
            "epc_jewel_metal_stock",
        })
        {
            try
            {
                removed += await ErpDb.ExecuteAsync(connection, null,
                    "DELETE t FROM `" + table + "` t INNER JOIN `" + RegistryTable + "` r"
                    + " ON r.`table_name`='" + table + "' AND r.`row_id`=t.`id`",
                    cancellationToken).ConfigureAwait(false);
            }
            catch (DbException) { }
        }

        try
        {
            await ErpDb.ExecuteAsync(connection, null, "DELETE FROM `" + RegistryTable + "`", cancellationToken).ConfigureAwait(false);
        }
        catch (DbException) { }

        return "Removed DEMO jewellery data — " + removed + " rows deleted. Live data was not touched.";
    }

    /// <summary>Demo GL journals carry source_id = the demo voucher id.</summary>
    private static IEnumerable<string> DemoJournalDeletes()
    {
        yield return
            "DELETE l FROM `epc_erp_gl_lines` l"
            + " INNER JOIN `epc_erp_gl_journals` j ON j.`id`=l.`journal_id`"
            + " WHERE j.`source_type`='adjustment' AND j.`reference` LIKE 'JW-%'"
            + " AND EXISTS (SELECT 1 FROM `" + RegistryTable + "` r"
            + "   WHERE r.`table_name`='epc_jewel_voucher' AND r.`row_id`=j.`source_id`)";
        yield return
            "DELETE j FROM `epc_erp_gl_journals` j"
            + " WHERE j.`source_type`='adjustment' AND j.`reference` LIKE 'JW-%'"
            + " AND EXISTS (SELECT 1 FROM `" + RegistryTable + "` r"
            + "   WHERE r.`table_name`='epc_jewel_voucher' AND r.`row_id`=j.`source_id`)";
    }

    private static async Task<long> InsertAsync(DbConnection connection, string sql, CancellationToken ct, params object[] args)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = ErpDb.Positional(sql);
        ErpDb.AddParameters(command, args);
        await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        await using var idCommand = connection.CreateCommand();
        idCommand.CommandText = "SELECT LAST_INSERT_ID()";
        return Convert.ToInt64(await idCommand.ExecuteScalarAsync(ct).ConfigureAwait(false), CultureInfo.InvariantCulture);
    }

    private static async Task EnsureRegistryAsync(DbConnection connection, CancellationToken ct)
        => await ErpDb.TryExecuteAsync(connection,
            "CREATE TABLE IF NOT EXISTS `" + RegistryTable + "` ("
            + "`id` bigint NOT NULL AUTO_INCREMENT,"
            + "`table_name` varchar(64) NOT NULL,"
            + "`row_id` bigint NOT NULL,"
            + "PRIMARY KEY (`id`), KEY `x_table` (`table_name`)"
            + ") ENGINE=InnoDB DEFAULT CHARSET=utf8", ct).ConfigureAwait(false);

    private static async Task PersistMarksAsync(DbConnection connection, DemoMarks marks, CancellationToken ct)
    {
        foreach (var (table, ids) in marks.All)
        {
            foreach (var id in ids)
            {
                try
                {
                    await ErpDb.ExecuteAsync(connection, null,
                        ErpDb.Positional("INSERT INTO `" + RegistryTable + "` (`table_name`,`row_id`) VALUES (?,?)"),
                        ct, table, id).ConfigureAwait(false);
                }
                catch (DbException) { }
            }
        }
    }
}
