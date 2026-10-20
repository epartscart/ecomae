using System.Data.Common;
using System.Globalization;

namespace EcomAE.Platform.Erp;

/// <summary>
/// Loads/removes DEMO auto-parts sample data for the workspace: a parts
/// catalogue (OEM numbers, fitment, pricing), purchase receipts, stock
/// movements and customer sales orders — then posts the corresponding GL
/// journals so the dashboard, income statement and balance sheet show the
/// demo numbers. Clear removes only registered demo rows, never live data.
/// Mirrors <see cref="ErpFitOutDemoSeedWriteService"/>.
/// </summary>
public interface IErpAutoPartsDemoSeedWriteService
{
    Task<string> SeedDemoAsync(CancellationToken cancellationToken = default);
    Task<string> ClearDemoAsync(CancellationToken cancellationToken = default);
}

public sealed class ErpAutoPartsDemoSeedWriteService : IErpAutoPartsDemoSeedWriteService
{
    private const string RegistryTable = "ecomae_autoparts_demo_registry";

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
    private readonly IErpAutoPartsGlPostWriteService _glPost;

    public ErpAutoPartsDemoSeedWriteService(
        IErpWriteConnectionFactory connections,
        IErpAutoPartsGlPostWriteService glPost)
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
        await ErpAutoPartsWorkspaceSchema.EnsureAsync(connection, cancellationToken).ConfigureAwait(false);
        await EnsureRegistryAsync(connection, cancellationToken).ConfigureAwait(false);
        await ErpAutoPartsGl.EnsureCoaAsync(connection, cancellationToken).ConfigureAwait(false);

        // Idempotent: clear any earlier demo run first so re-seeding is clean.
        await ClearDemoAsync(cancellationToken).ConfigureAwait(false);

        var marks = new DemoMarks();
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var day = 86400L;
        var thisMonth = DateTimeOffset.UtcNow;

        var parts = new (string Sku, string Name, string Brand, string Oem, string Category, string Fitment, decimal Cost, decimal Price, decimal Qty, decimal Reorder)[]
        {
            ("BP-GOLF7-OEM", "Brake Pad Set Front", "Bosch", "5Q0698151", "Braking", "VW Golf VII 1.4 TSI 2012-2020", 145m, 260m, 40, 8),
            ("BF-CAMRY-16", "Oil Filter", "Mann", "90915-YZZD2", "Filters", "Toyota Camry 2.5 2015-2024", 18m, 35m, 120, 25),
            ("ALT-PATROL-TB", "Alternator 12V 130A", "Denso", "23100-3JA0A", "Electrical", "Nissan Patrol Y62 5.6 V8", 980m, 1650m, 6, 2),
            ("SHK-LX570-RR", "Rear Shock Absorber", "KYB", "48530-69155", "Suspension", "Toyota Land Cruiser 200 2007-2021", 320m, 540m, 14, 4),
            ("RAD-ACCORD-18", "Radiator Assembly", "Valeo", "19010-R70-A51", "Cooling", "Honda Accord 2.4 2013-2017", 410m, 720m, 9, 3),
            ("CLT-HILUX-21", "Clutch Kit", "LUK", "31210-0K190", "Transmission", "Toyota Hilux 2.7 2016-2024", 540m, 950m, 5, 2),
            ("SPK-L643-PL", "Spark Plug Iridium", "NGK", "ILZKR7B11", "Ignition", "Land Cruiser 300 3.5 TT", 28m, 55m, 200, 40),
            ("PMP-X5-F15", "Water Pump", "Gates", "11518635089", "Cooling", "BMW X5 F15 40e 2014-2018", 230m, 420m, 12, 3),
        };

        foreach (var p in parts)
        {
            var id = await InsertAsync(connection,
                "INSERT INTO `ecomae_autoparts_parts` (`sku`,`name`,`brand`,`oem_no`,`category`,`fitment`,`purchase_cost`,`sale_price`,`stock_qty`,`reorder_level`,`active`)"
                + " VALUES (?,?,?,?,?,?,?,?,?,?,1)",
                cancellationToken,
                p.Sku, p.Name, p.Brand, p.Oem, p.Category, p.Fitment, p.Cost, p.Price, p.Qty, p.Reorder);
            marks.Add("ecomae_autoparts_parts", id);

            var moveId = await InsertAsync(connection,
                "INSERT INTO `ecomae_autoparts_movements` (`part_id`,`movement`,`qty`,`unit_cost`,`reference`,`note`,`created_at_utc`)"
                + " VALUES (?,?,?,?,?,?,?)",
                cancellationToken,
                id, "receipt", p.Qty, p.Cost, "DEMO-OPENING", "Opening stock receipt (DEMO)", now - 20 * day);
            marks.Add("ecomae_autoparts_movements", moveId);
        }

        // Purchase order covering the opening stock.
        var purchaseSubtotal = parts.Sum(p => p.Cost * p.Qty);
        var poId = await InsertAsync(connection,
            "INSERT INTO `ecomae_autoparts_orders` (`order_no`,`order_type`,`party_name`,`status`,`subtotal`,`vat`,`total`,`event_date`,`created_at_utc`)"
            + " VALUES (?,?,?,?,?,?,?,?,?)",
            cancellationToken,
            "DEMO-PO-001", "purchase", "DEMO Gulf Parts Trading LLC", "received",
            purchaseSubtotal, 0m, purchaseSubtotal, now - 20 * day, now);
        marks.Add("ecomae_autoparts_orders", poId);

        var partIds = marks.All["ecomae_autoparts_parts"];
        for (var i = 0; i < parts.Length; i++)
        {
            var lid = await InsertAsync(connection,
                "INSERT INTO `ecomae_autoparts_order_lines` (`order_id`,`part_id`,`description`,`qty`,`unit_price`,`tax_rate`,`line_total`)"
                + " VALUES (?,?,?,?,?,?,?)",
                cancellationToken,
                poId, partIds[i], parts[i].Name + " — " + parts[i].Oem, parts[i].Qty, parts[i].Cost, 0m, parts[i].Cost * parts[i].Qty);
            marks.Add("ecomae_autoparts_order_lines", lid);
        }

        // Three demo sales orders at sale_price with 5% VAT.
        var sales = new (string No, string Customer, (int Part, decimal Qty)[] Lines, int DaysAgo, string Status)[]
        {
            ("DEMO-SO-001", "DEMO AutoFix Garage LLC", [(0, 6), (1, 12), (6, 24)], 12, "invoiced"),
            ("DEMO-SO-002", "DEMO Desert Fleet Services", [(3, 2), (4, 1), (7, 2)], 6, "invoiced"),
            ("DEMO-SO-003", "DEMO Emirates Motors", [(2, 1), (5, 1)], 2, "confirmed"),
        };

        foreach (var sale in sales)
        {
            var subtotal = sale.Lines.Sum(l => parts[l.Part].Price * l.Qty);
            var vat = Math.Round(subtotal * 0.05m, 2);
            var soId = await InsertAsync(connection,
                "INSERT INTO `ecomae_autoparts_orders` (`order_no`,`order_type`,`party_name`,`status`,`subtotal`,`vat`,`total`,`event_date`,`created_at_utc`)"
                + " VALUES (?,?,?,?,?,?,?,?,?)",
                cancellationToken,
                sale.No, "sale", sale.Customer, sale.Status,
                subtotal, vat, subtotal + vat, now - sale.DaysAgo * day, now);
            marks.Add("ecomae_autoparts_orders", soId);

            foreach (var (partIdx, qty) in sale.Lines)
            {
                var p = parts[partIdx];
                var lid = await InsertAsync(connection,
                    "INSERT INTO `ecomae_autoparts_order_lines` (`order_id`,`part_id`,`description`,`qty`,`unit_price`,`tax_rate`,`line_total`)"
                    + " VALUES (?,?,?,?,?,?,?)",
                    cancellationToken,
                    soId, partIds[partIdx], p.Name + " — " + p.Oem, qty, p.Price, 5m, p.Price * qty);
                marks.Add("ecomae_autoparts_order_lines", lid);

                var moveId = await InsertAsync(connection,
                    "INSERT INTO `ecomae_autoparts_movements` (`part_id`,`movement`,`qty`,`unit_cost`,`reference`,`note`,`created_at_utc`)"
                    + " VALUES (?,?,?,?,?,?,?)",
                    cancellationToken,
                    partIds[partIdx], "issue", -qty, p.Cost, sale.No, "Sale issue (DEMO)", now - sale.DaysAgo * day);
                marks.Add("ecomae_autoparts_movements", moveId);

                // Reduce stock to match the issue.
                await using (var upd = connection.CreateCommand())
                {
                    upd.CommandText = ErpDb.Positional("UPDATE `ecomae_autoparts_parts` SET `stock_qty`=`stock_qty`-? WHERE `id`=?");
                    ErpDb.AddParameters(upd, qty, partIds[partIdx]);
                    await upd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
                }
            }
        }

        await PersistMarksAsync(connection, marks, cancellationToken).ConfigureAwait(false);

        // Demo lands on the financial statements too — post the demo journals.
        // Demo lands on the financial statements too — post journals for the
        // demo orders only (never sweep every unposted live order).
        var glNotes = new List<string>();
        foreach (var orderId in marks.All.TryGetValue("ecomae_autoparts_orders", out var seededOrders) ? seededOrders : new List<long>())
        {
            glNotes.Add(await _glPost.PostAsync(orderId, null, cancellationToken).ConfigureAwait(false));
        }
        var glNote = glNotes.Count > 0
            ? "Posted to GL for " + glNotes.Count + " demo order(s)."
            : "No demo journals to post.";

        var demoMonth = thisMonth.ToString("yyyy-MM", CultureInfo.InvariantCulture);
        return "Loaded DEMO auto-parts data — " + parts.Length + " catalogue parts, 1 purchase order, "
            + sales.Length + " sales orders (2 invoiced, 1 confirmed) for " + demoMonth + ". "
            + glNote;
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

        // Demo GL journals first — matched to registered demo order/movement rows,
        // so a live journal can never match a registry row.
        foreach (var sql in DemoJournalDeletes())
        {
            try { removed += await ErpDb.ExecuteAsync(connection, null, sql, cancellationToken).ConfigureAwait(false); }
            catch (DbException) { }
        }

        // Invoice links created from demo sales — and the draft e-invoice
        // documents they point at, so "Remove sample data" leaves no orphan
        // documents in the e-invoice list. Only draft documents created by the
        // demo issue flow can match (submitted/accepted are never deleted).
        foreach (var sql in new[]
        {
            "DELETE l FROM `epc_einvoice_lines` l INNER JOIN `ecomae_autoparts_invoice_links` k"
            + " ON k.`invoice_id`=l.`document_id` INNER JOIN `" + RegistryTable + "` r"
            + " ON r.`table_name`='ecomae_autoparts_orders' AND r.`row_id`=k.`order_id`"
            + " INNER JOIN `epc_einvoice_documents` d ON d.`id`=k.`invoice_id`"
            + " WHERE d.`status` NOT IN ('submitted','accepted','queued')",
            "DELETE e FROM `epc_einvoice_events` e INNER JOIN `ecomae_autoparts_invoice_links` k"
            + " ON k.`invoice_id`=e.`document_id` INNER JOIN `" + RegistryTable + "` r"
            + " ON r.`table_name`='ecomae_autoparts_orders' AND r.`row_id`=k.`order_id`"
            + " INNER JOIN `epc_einvoice_documents` d ON d.`id`=k.`invoice_id`"
            + " WHERE d.`status` NOT IN ('submitted','accepted','queued')",
            "DELETE d FROM `epc_einvoice_documents` d INNER JOIN `ecomae_autoparts_invoice_links` k"
            + " ON k.`invoice_id`=d.`id` INNER JOIN `" + RegistryTable + "` r"
            + " ON r.`table_name`='ecomae_autoparts_orders' AND r.`row_id`=k.`order_id`"
            + " WHERE d.`status` NOT IN ('submitted','accepted','queued')",
            "DELETE k FROM `ecomae_autoparts_invoice_links` k INNER JOIN `" + RegistryTable + "` r"
            + " ON r.`table_name`='ecomae_autoparts_orders' AND r.`row_id`=k.`order_id`"
            + " INNER JOIN `epc_einvoice_documents` d ON d.`id`=k.`invoice_id`"
            + " WHERE d.`status` NOT IN ('submitted','accepted','queued')",
        })
        {
            try
            {
                removed += await ErpDb.ExecuteAsync(connection, null, sql, cancellationToken).ConfigureAwait(false);
            }
            catch (DbException) { }
        }

        // Registered rows, children before parents.
        foreach (var table in new[]
        {
            "ecomae_autoparts_order_lines",
            "ecomae_autoparts_movements",
            "ecomae_autoparts_orders",
            "ecomae_autoparts_parts",
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

        return "Removed DEMO auto-parts data — " + removed + " rows deleted. Live data was not touched.";
    }

    /// <summary>Demo GL journals carry references built from registry rows (AP-…-id).</summary>
    private static IEnumerable<string> DemoJournalDeletes()
    {
        // Journal lines first via the journal id, then the journals. GL journals
        // posted by this workspace carry source_id = the order id, so matching
        // against registered demo order rows can never catch a live journal.
        yield return
            "DELETE l FROM `epc_erp_gl_lines` l"
            + " INNER JOIN `epc_erp_gl_journals` j ON j.`id`=l.`journal_id`"
            + " WHERE j.`source_type`='adjustment' AND j.`reference` LIKE 'AP-%'"
            + " AND EXISTS (SELECT 1 FROM `" + RegistryTable + "` r"
            + "   WHERE r.`table_name`='ecomae_autoparts_orders' AND r.`row_id`=j.`source_id`)";
        yield return
            "DELETE j FROM `epc_erp_gl_journals` j"
            + " WHERE j.`source_type`='adjustment' AND j.`reference` LIKE 'AP-%'"
            + " AND EXISTS (SELECT 1 FROM `" + RegistryTable + "` r"
            + "   WHERE r.`table_name`='ecomae_autoparts_orders' AND r.`row_id`=j.`source_id`)";
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
