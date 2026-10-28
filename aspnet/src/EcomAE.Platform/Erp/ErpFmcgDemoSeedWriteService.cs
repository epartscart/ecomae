using System.Data.Common;
using System.Globalization;

namespace EcomAE.Platform.Erp;

/// <summary>
/// Loads/removes DEMO FMCG sample data for the workspace — a Qatar FMCG
/// structure (legal entities, business units, cost centres, departments),
/// FMCG SKUs with GTIN/batch/expiry, channels, routes, promotions — then
/// posts the demo sales/COGS journals so TB, P&L and BS show the numbers.
/// Clear removes only registered demo rows, never live data.
/// Mirrors <see cref="ErpAutoPartsDemoSeedWriteService"/>.
/// </summary>
public interface IErpFmcgDemoSeedWriteService
{
    Task<string> SeedDemoAsync(CancellationToken cancellationToken = default);
    Task<string> ClearDemoAsync(CancellationToken cancellationToken = default);
}

public sealed class ErpFmcgDemoSeedWriteService : IErpFmcgDemoSeedWriteService
{
    private readonly IErpWriteConnectionFactory _connections;
    private readonly IErpGlPostingService _gl;

    public ErpFmcgDemoSeedWriteService(IErpWriteConnectionFactory connections, IErpGlPostingService gl)
    {
        _connections = connections;
        _gl = gl;
    }

    public async Task<string> SeedDemoAsync(CancellationToken cancellationToken = default)
    {
        if (!_connections.IsConfigured)
        {
            return "TenantRegistry DB is not configured — demo seed skipped.";
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await ErpFmcgWorkspaceSchema.EnsureAsync(connection, cancellationToken).ConfigureAwait(false);
        await ErpLazySchema.EnsureTenantOrganizationAsync(connection, cancellationToken).ConfigureAwait(false);
        await ErpGlPostingService.EnsureSchemaAsync(connection, cancellationToken).ConfigureAwait(false);

        await ClearDemoAsync(cancellationToken).ConfigureAwait(false);
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var day = 86400L;

        // --- Structure: Qatar FMCG company, 2 legal entities, 3 BUs, cost centres, departments ---
        var entities = new[] { ("QFMCG", "Qatar FMCG Holding W.L.L.", "QA", "QAR"), ("QDIST", "Gulf Distribution & Retail Co.", "QA", "QAR") };
        foreach (var (code, name, country, ccy) in entities)
        {
            var id = await InsertIfAbsentAsync(connection,
                "SELECT `id` FROM `epc_erp_pm_legal_entities` WHERE `code`=@p0",
                "INSERT INTO `epc_erp_pm_legal_entities` (`code`,`name`,`country_code`,`currency_code`,`active`,`time_created`,`time_updated`) VALUES (@p0,@p1,@p2,@p3,1,@p4,@p4)",
                cancellationToken, code, name, country, ccy, now);
            await MarkAsync(connection, "epc_erp_pm_legal_entities", id, cancellationToken).ConfigureAwait(false);
        }

        var bus = new (string Code, string Name, string Ent)[]
        {
            ("FMCG-MFG", "Manufacturing", "QFMCG"),
            ("FMCG-DIST", "Distribution & Wholesale", "QFMCG"),
            ("FMCG-RTL", "Retail & Van Sales", "QDIST"),
        };
        foreach (var (code, name, ent) in bus)
        {
            var entId = await ScalarAsync(connection, "SELECT `id` FROM `epc_erp_pm_legal_entities` WHERE `code`='" + ent + "'", cancellationToken);
            var id = await InsertIfAbsentAsync(connection,
                "SELECT `id` FROM `epc_erp_pm_business_units` WHERE `code`=@p0",
                "INSERT INTO `epc_erp_pm_business_units` (`legal_entity_id`,`code`,`name`,`active`,`time_created`,`time_updated`) VALUES (@p0,@p1,@p2,1,@p3,@p3)",
                cancellationToken, entId, code, name, now);
            await MarkAsync(connection, "epc_erp_pm_business_units", id, cancellationToken).ConfigureAwait(false);
        }

        var ccs = new[] { ("CC-PLANT", "Doha plant"), ("CC-DC", "Distribution centre"), ("CC-VAN", "Van sales fleet"), ("CC-HO", "Head office") };
        foreach (var (code, name) in ccs)
        {
            var id = await InsertIfAbsentAsync(connection,
                "SELECT `id` FROM `epc_erp_pm_cost_centres` WHERE `code`=@p0",
                "INSERT INTO `epc_erp_pm_cost_centres` (`code`,`name`,`active`,`time_created`,`time_updated`) VALUES (@p0,@p1,1,@p2,@p2)",
                cancellationToken, code, name, now);
            await MarkAsync(connection, "epc_erp_pm_cost_centres", id, cancellationToken).ConfigureAwait(false);
        }

        // --- FMCG items ---
        var items = new (string Sku, string Name, string Brand, string Cat, string Gtin, string Uom, int Upc, int Cpp, int Shelf, string Excise, decimal Cost, decimal Price, string Chan)[]
        {
            ("FM-WTR-330", "Drinking Water 330ml ×24", "AquaGulf", "Beverages", "6281001000017", "CS", 24, 60, 730, "", 9.60m, 18.00m, "modern_trade"),
            ("FM-JCE-MNG1L", "Mango Nectar 1L ×12", "AquaGulf", "Beverages", "6281001000024", "CS", 12, 80, 365, "sugary", 14.40m, 26.00m, "horeca"),
            ("FM-MLK-WHL1L", "Whole Milk UHT 1L ×12", "DohaDairy", "Dairy", "6281001000031", "CS", 12, 50, 180, "", 21.60m, 33.00m, "retail"),
            ("FM-YGT-PLN170", "Plain Yoghurt 170g ×48", "DohaDairy", "Dairy", "6281001000048", "CS", 48, 40, 45, "", 19.20m, 31.00m, "modern_trade"),
            ("FM-RSN-500", "Basmati Rice 5kg", "PearlFoods", "Staples", "6281001000055", "BG", 1, 100, 1095, "", 38.00m, 55.00m, "wholesale"),
            ("FM-OIL-SF1.8", "Sunflower Oil 1.8L ×6", "PearlFoods", "Staples", "6281001000062", "CS", 6, 70, 730, "", 24.00m, 39.00m, "modern_trade"),
            ("FM-BSK-CHO300", "Chocolate Biscuits 300g ×20", "QatarSnax", "Snacks", "6281001000079", "CS", 20, 90, 365, "", 12.00m, 22.00m, "van_sales"),
            ("FM-ENR-250", "Energy Drink 250ml ×24", "QatarSnax", "Beverages", "6281001000086", "CS", 24, 48, 540, "energy", 30.00m, 52.00m, "retail"),
        };
        var itemIds = new List<long>();
        foreach (var it in items)
        {
            var id = await InsertIfAbsentAsync(connection,
                "SELECT `id` FROM `" + ErpFmcgWorkspaceSchema.Items + "` WHERE `sku`=@p0",
                "INSERT INTO `" + ErpFmcgWorkspaceSchema.Items + "` (`sku`,`name`,`brand`,`category`,`gtin`,`base_uom`,`units_per_case`,`cases_per_pallet`,`shelf_life_days`,`batch_tracked`,`expiry_tracked`,`fefo`,`min_shelf_life_pct`,`excise_category`,`cost`,`price`,`channel`,`active`,`time_created`,`time_modified`)"
                + " VALUES (@p0,@p1,@p2,@p3,@p4,@p5,@p6,@p7,@p8,1,1,1,30,@p9,@p10,@p11,@p12,1,@p13,@p13)",
                cancellationToken, it.Sku, it.Name, it.Brand, it.Cat, it.Gtin, it.Uom, it.Upc, it.Cpp, it.Shelf, it.Excise, it.Cost, it.Price, it.Chan, now);
            itemIds.Add(id);
            await MarkAsync(connection, ErpFmcgWorkspaceSchema.Items, id, cancellationToken).ConfigureAwait(false);
        }

        // --- Batches with mfg/expiry (one near-expiry + one recall-flagged for demo) ---
        for (var i = 0; i < itemIds.Count; i++)
        {
            var shelf = items[i].Shelf;
            var batches = new (string No, long Mfg, long Exp, decimal Qty, string Wh, string St, int Recall)[]
            {
                ("B" + (i + 1).ToString("D3") + "-A", now - 30 * day, now + (shelf - 30) * day, 120, "DC-Doha", "available", 0),
                ("B" + (i + 1).ToString("D3") + "-B", now - (shelf - 20) * day, now + 20 * day, 35, "DC-Doha", "available", 0),
            };
            if (i == 0) batches[1].Recall = 1;
            foreach (var b in batches)
            {
                var id = await InsertIfAbsentAsync(connection,
                    "SELECT `id` FROM `" + ErpFmcgWorkspaceSchema.Batches + "` WHERE `item_id`=@p0 AND `batch_no`=@p1",
                    "INSERT INTO `" + ErpFmcgWorkspaceSchema.Batches + "` (`item_id`,`batch_no`,`mfg_date`,`expiry_date`,`qty_on_hand`,`warehouse`,`status`,`recall_flag`,`time_created`,`time_modified`) VALUES (@p0,@p1,@p2,@p3,@p4,@p5,@p6,@p7,@p8,@p8)",
                    cancellationToken, itemIds[i], b.No, b.Mfg, b.Exp, b.Qty, b.Wh, b.St, b.Recall, now);
                await MarkAsync(connection, ErpFmcgWorkspaceSchema.Batches, id, cancellationToken).ConfigureAwait(false);
            }
        }

        // --- Channels / routes / promotion ---
        var chans = new (string Code, string Name, string Kind)[]
        {
            ("MT", "Modern trade (hypermarkets)", "modern_trade"),
            ("HOR", "HORECA (hotels, restaurants, catering)", "horeca"),
            ("WS", "Wholesale / traditional trade", "wholesale"),
            ("VAN", "Van sales routes", "van_sales"),
            ("RTL", "Own retail", "retail"),
        };
        foreach (var (code, name, kind) in chans)
        {
            var id = await InsertIfAbsentAsync(connection,
                "SELECT `id` FROM `" + ErpFmcgWorkspaceSchema.Channels + "` WHERE `code`=@p0",
                "INSERT INTO `" + ErpFmcgWorkspaceSchema.Channels + "` (`code`,`name`,`kind`,`active`,`time_created`,`time_modified`) VALUES (@p0,@p1,@p2,1,@p3,@p3)",
                cancellationToken, code, name, kind, now);
            await MarkAsync(connection, ErpFmcgWorkspaceSchema.Channels, id, cancellationToken).ConfigureAwait(false);
        }

        var routes = new (string Code, string Name, string Sp, string Veh)[]
        {
            ("R-01", "Doha central", "Ahmed K.", "Van QA-1021"),
            ("R-02", "Al Rayyan / Al Wakrah", "Salim H.", "Van QA-2044"),
        };
        var routeIds = new List<long>();
        foreach (var (code, name, sp, veh) in routes)
        {
            var id = await InsertIfAbsentAsync(connection,
                "SELECT `id` FROM `" + ErpFmcgWorkspaceSchema.Routes + "` WHERE `code`=@p0",
                "INSERT INTO `" + ErpFmcgWorkspaceSchema.Routes + "` (`code`,`name`,`salesperson`,`vehicle`,`warehouse`,`active`,`time_created`,`time_modified`) VALUES (@p0,@p1,@p2,@p3,'DC-Doha',1,@p4,@p4)",
                cancellationToken, code, name, sp, veh, now);
            routeIds.Add(id);
            await MarkAsync(connection, ErpFmcgWorkspaceSchema.Routes, id, cancellationToken).ConfigureAwait(false);
        }

        var promoId = await InsertIfAbsentAsync(connection,
            "SELECT `id` FROM `" + ErpFmcgWorkspaceSchema.Promotions + "` WHERE `code`=@p0",
            "INSERT INTO `" + ErpFmcgWorkspaceSchema.Promotions + "` (`code`,`name`,`kind`,`item_id`,`buy_qty`,`free_qty`,`discount_pct`,`start_date`,`end_date`,`active`,`time_created`,`time_modified`)"
            + " VALUES (@p0,@p1,'free_goods',@p2,10,1,0,@p3,@p4,1,@p5,@p5)",
            cancellationToken, "PROMO-WTR-10+1", "Water case 10+1 free", itemIds[0], now - 7 * day, now + 60 * day, now);
        await MarkAsync(connection, ErpFmcgWorkspaceSchema.Promotions, promoId, cancellationToken).ConfigureAwait(false);

        // --- Landed cost shipments: supplier price + freight + customs + insurance → landed unit cost ---
        var landed = new (string Ship, int ItemIdx, decimal Qty, decimal Unit, decimal Freight, decimal Customs, decimal Ins)[]
        {
            ("SHP-QA-001", 4, 500, 38.00m, 950m, 950m, 120m),   // rice — GCC CET 5%
            ("SHP-QA-002", 5, 300, 24.00m, 620m, 360m, 80m),    // oil
        };
        foreach (var l in landed)
        {
            var it = items[l.ItemIdx];
            var landedUnit = l.Unit + (l.Freight + l.Customs + l.Ins) / l.Qty;
            var id = await InsertIfAbsentAsync(connection,
                "SELECT `id` FROM `" + ErpFmcgWorkspaceSchema.LandedCosts + "` WHERE `shipment`=@p0 AND `item_id`=@p1",
                "INSERT INTO `" + ErpFmcgWorkspaceSchema.LandedCosts + "` (`shipment`,`item_id`,`qty`,`unit_cost`,`freight`,`customs`,`insurance`,`landed_unit_cost`,`time_created`,`time_modified`)"
                + " VALUES (@p0,@p1,@p2,@p3,@p4,@p5,@p6,@p7,@p8,@p8)",
                cancellationToken, l.Ship, itemIds[l.ItemIdx], l.Qty, l.Unit, l.Freight, l.Customs, l.Ins, landedUnit, now);
            await MarkAsync(connection, ErpFmcgWorkspaceSchema.LandedCosts, id, cancellationToken).ConfigureAwait(false);
        }

        // --- Route reconciliation: two days on route R-01 ---
        if (routeIds.Count > 0)
        {
            var reconRows = new (long D, decimal Load, decimal Sold, decimal Ret, decimal Cash, decimal Expect)[]
            {
                (now - day, 200m, 168m, 12m, 3696m, 3696m),
                (now, 200m, 171m, 9m, 3542m, 3762m), // cash short — variance flags shrinkage
            };
            foreach (var rc in reconRows)
            {
                var id = await InsertIfAbsentAsync(connection,
                    "SELECT `id` FROM `" + ErpFmcgWorkspaceSchema.RouteRecon + "` WHERE `route_id`=@p0 AND `recon_date`=@p1",
                    "INSERT INTO `" + ErpFmcgWorkspaceSchema.RouteRecon + "` (`route_id`,`recon_date`,`qty_loaded`,`qty_sold`,`qty_returned`,`cash_collected`,`expected_cash`,`variance`,`time_created`,`time_modified`)"
                    + " VALUES (@p0,@p1,@p2,@p3,@p4,@p5,@p6,@p7,@p8,@p8)",
                    cancellationToken, routeIds[0], rc.D, rc.Load, rc.Sold, rc.Ret, rc.Cash, rc.Expect, rc.Cash - rc.Expect, now);
                await MarkAsync(connection, ErpFmcgWorkspaceSchema.RouteRecon, id, cancellationToken).ConfigureAwait(false);
            }
        }

        // --- Demo GL: sales + COGS so TB / P&L / BS carry the numbers ---
        var salesCoa = await EnsureCoaAsync(connection, "4000", "Revenue — FMCG sales", "revenue", "credit", cancellationToken);
        var cogsCoa = await EnsureCoaAsync(connection, "5000", "Cost of goods sold — FMCG", "expense", "debit", cancellationToken);
        var arCoa = await EnsureCoaAsync(connection, "1200", "Trade receivables", "asset", "debit", cancellationToken);
        var invCoa = await EnsureCoaAsync(connection, "1300", "Finished-goods inventory — FMCG", "asset", "debit", cancellationToken);
        var exciseCoa = await EnsureCoaAsync(connection, "6100", "Excise tax expense", "expense", "debit", cancellationToken);
        var excisePay = await EnsureCoaAsync(connection, "2200", "Excise tax payable", "liability", "credit", cancellationToken);
        var wdCoa = await EnsureCoaAsync(connection, "6200", "Inventory write-down loss — expiry/damage/shrinkage", "expense", "debit", cancellationToken);

        var sales = items.Sum(i => i.Price * 20m);
        var cogs = items.Sum(i => i.Cost * 20m);
        var excise = items.Where(i => i.Excise == "energy").Sum(i => i.Price * 20m)
                   + items.Where(i => i.Excise == "sugary").Sum(i => i.Price * 20m) * 0.5m;

        var j1 = await _gl.PostJournalAsync(connection,
            new ErpGlJournalHeader
            {
                JournalDate = now,
                Reference = "FMCG-DEMO-SALES",
                Description = "Demo FMCG sales across channels (QAR)",
                SourceType = "manual",
                CurrencyCode = "QAR",
                ExchangeRate = 1m,
            },
            new List<ErpGlLine>
            {
                new(arCoa, sales, 0m, "Trade receivables — demo sales"),
                new(salesCoa, 0m, sales, "FMCG channel sales — demo"),
            },
            0, cancellationToken).ConfigureAwait(false);
        await MarkAsync(connection, "epc_erp_gl_journals", j1, cancellationToken).ConfigureAwait(false);

        var j2 = await _gl.PostJournalAsync(connection,
            new ErpGlJournalHeader
            {
                JournalDate = now,
                Reference = "FMCG-DEMO-COGS",
                Description = "Demo COGS + excise accrual (QAR)",
                SourceType = "manual",
                CurrencyCode = "QAR",
                ExchangeRate = 1m,
            },
            new List<ErpGlLine>
            {
                new(cogsCoa, cogs, 0m, "COGS — demo goods sold"),
                new(exciseCoa, excise, 0m, "Excise on energy/sugary SKUs — demo"),
                new(invCoa, 0m, cogs, "Finished goods relieved — demo"),
                new(excisePay, 0m, excise, "Excise payable — demo"),
            },
            0, cancellationToken).ConfigureAwait(false);
        await MarkAsync(connection, "epc_erp_gl_journals", j2, cancellationToken).ConfigureAwait(false);

        // Write-down demo: 12 cases of the near-expiry yoghurt batch written off.
        var wdBatch = await ScalarAsync(connection,
            "SELECT b.`id` FROM `" + ErpFmcgWorkspaceSchema.Batches + "` b JOIN `" + ErpFmcgWorkspaceSchema.Items + "` i ON i.`id`=b.`item_id`"
            + " WHERE i.`sku`='FM-YGT-PLN170' ORDER BY b.`expiry_date` LIMIT 1", cancellationToken);
        var wdAmount = 12m * items[3].Cost;
        var j3 = await _gl.PostJournalAsync(connection,
            new ErpGlJournalHeader
            {
                JournalDate = now,
                Reference = "FMCG-DEMO-WD",
                Description = "Expiry write-down — yoghurt batch (demo, QAR)",
                SourceType = "manual",
                CurrencyCode = "QAR",
                ExchangeRate = 1m,
            },
            new List<ErpGlLine>
            {
                new(wdCoa, wdAmount, 0m, "Expiry write-down — demo"),
                new(invCoa, 0m, wdAmount, "Inventory relieved — expiry write-down"),
            },
            0, cancellationToken).ConfigureAwait(false);
        await MarkAsync(connection, "epc_erp_gl_journals", j3, cancellationToken).ConfigureAwait(false);
        var wdId = await InsertIfAbsentAsync(connection,
            "SELECT `id` FROM `" + ErpFmcgWorkspaceSchema.WriteDowns + "` WHERE `batch_id`=@p0 AND `reason`='expiry'",
            "INSERT INTO `" + ErpFmcgWorkspaceSchema.WriteDowns + "` (`batch_id`,`reason`,`qty`,`amount`,`loss_account_id`,`gl_journal_id`,`write_date`,`time_created`,`time_modified`)"
            + " VALUES (@p0,'expiry',12,@p1,@p2,@p3,@p4,@p4,@p4)",
            cancellationToken, wdBatch, wdAmount, wdCoa, j3, now);
        await MarkAsync(connection, ErpFmcgWorkspaceSchema.WriteDowns, wdId, cancellationToken).ConfigureAwait(false);

        // --- Manufacturing: one BOM, two production orders, variance journal ---
        var wipCoa = await EnsureCoaAsync(connection, "1320", "Work-in-progress — production", "asset", "debit", cancellationToken);
        var pvarCoa = await EnsureCoaAsync(connection, "5300", "Production variance — materials/labour", "expense", "debit", cancellationToken);

        var bomId = await InsertIfAbsentAsync(connection,
            "SELECT `id` FROM `" + ErpFmcgWorkspaceSchema.Boms + "` WHERE `bom_code`='BOM-JCE1000'",
            "INSERT INTO `" + ErpFmcgWorkspaceSchema.Boms + "` (`bom_code`,`name`,`finished_item_id`,`output_qty`,`output_uom`,`wip_coa_id`,`variance_coa_id`,`active`,`time_created`,`time_modified`)"
            + " VALUES ('BOM-JCE1000','Orange juice 1L — fill & pack',@p0,1000,'bottle',@p1,@p2,1,@p3,@p3)",
            cancellationToken, itemIds[0], wipCoa, pvarCoa, now);
        await MarkAsync(connection, ErpFmcgWorkspaceSchema.Boms, bomId, cancellationToken).ConfigureAwait(false);

        var bomLines = new (int No, string Comp, decimal Qty, string Uom, decimal Waste, decimal Cost)[]
        {
            (1, "Orange concentrate (bulk)", 0.12m, "kg", 2m, 3.85m),
            (2, "Purified water", 0.88m, "l", 0m, 0.02m),
            (3, "PET bottle 1L + cap", 1m, "ea", 1m, 0.32m),
            (4, "Label + carton (per 12)", 0.09m, "ea", 0m, 0.15m),
        };
        foreach (var bl in bomLines)
        {
            var blId = await InsertIfAbsentAsync(connection,
                "SELECT `id` FROM `" + ErpFmcgWorkspaceSchema.BomLines + "` WHERE `bom_id`=@p0 AND `line_no`=@p1",
                "INSERT INTO `" + ErpFmcgWorkspaceSchema.BomLines + "` (`bom_id`,`line_no`,`component_name`,`qty_per`,`uom`,`waste_pct`,`std_cost`,`time_created`)"
                + " VALUES (@p0,@p1,@p2,@p3,@p4,@p5,@p6,@p7)",
                cancellationToken, bomId, bl.No, bl.Comp, bl.Qty, bl.Uom, bl.Waste, bl.Cost, now);
            await MarkAsync(connection, ErpFmcgWorkspaceSchema.BomLines, blId, cancellationToken).ConfigureAwait(false);
        }

        var stdCostPerBottle = bomLines.Sum(x => x.Qty * x.Cost); // ~0.52 QAR/bottle
        var mfgBu = await ScalarAsync(connection, "SELECT `id` FROM `epc_erp_pm_business_units` WHERE `code`='FMCG-MFG'", cancellationToken);
        var mfgEnt = await ScalarAsync(connection, "SELECT `id` FROM `epc_erp_pm_legal_entities` WHERE `code`='QFMCG'", cancellationToken);
        var prodRows = new (string No, decimal Planned, decimal Produced, decimal ActFactor, string Status, long Sched, long Done)[]
        {
            ("PO-0001", 12000m, 11850m, 1.04m, "completed", now - 14 * day, now - 12 * day),
            ("PO-0002", 15000m, 0m, 0m, "in_progress", now + 3 * day, 0),
        };
        foreach (var po in prodRows)
        {
            var std = po.Produced * stdCostPerBottle;
            var act = po.Status == "completed" ? std * po.ActFactor : 0m;
            // actual = std + variance after fil rounding, so order and GL journal agree to the fil.
            var stdR = Math.Round(std, 2);
            var varR = Math.Round(act - std, 2);
            var actR = stdR + varR;
            var orderId = await InsertIfAbsentAsync(connection,
                "SELECT `id` FROM `" + ErpFmcgWorkspaceSchema.ProductionOrders + "` WHERE `order_no`=@p0",
                "INSERT INTO `" + ErpFmcgWorkspaceSchema.ProductionOrders + "` (`order_no`,`bom_id`,`finished_item_id`,`legal_entity_id`,`business_unit_id`,`qty_planned`,`qty_produced`,`std_cost`,`actual_cost`,`variance`,`status`,`scheduled_date`,`completed_date`,`time_created`,`time_modified`)"
                + " VALUES (@p0,@p1,@p2,@p3,@p4,@p5,@p6,@p7,@p8,@p9,@p10,@p11,@p12,@p13,@p13)",
                cancellationToken, po.No, bomId, itemIds[0], mfgEnt, mfgBu,
                po.Planned, po.Produced, stdR, actR, varR,
                po.Status, po.Sched, po.Done, now);
            await MarkAsync(connection, ErpFmcgWorkspaceSchema.ProductionOrders, orderId, cancellationToken).ConfigureAwait(false);
        }

        // Production variance journal: unfavourable material usage on PO-0001.
        // Same fil rounding as the order row: variance = Round(act-std), WIP credit = std + variance.
        var fgOut = Math.Round(11850m * stdCostPerBottle, 2);
        var pvar = Math.Round(11850m * stdCostPerBottle * 0.04m, 2);
        var j4 = await _gl.PostJournalAsync(connection,
            new ErpGlJournalHeader
            {
                JournalDate = now - 12 * day,
                Reference = "FMCG-DEMO-PROD",
                Description = "Production order PO-0001 — output + unfavourable usage variance (demo, QAR)",
                SourceType = "manual",
                CurrencyCode = "QAR",
                ExchangeRate = 1m,
            },
            new List<ErpGlLine>
            {
                new(invCoa, fgOut, 0m, "Finished goods from production — PO-0001"),
                new(pvarCoa, pvar, 0m, "Unfavourable material/labour variance — PO-0001"),
                new(wipCoa, 0m, fgOut + pvar, "WIP relieved — PO-0001"),
            },
            0, cancellationToken).ConfigureAwait(false);
        await MarkAsync(connection, "epc_erp_gl_journals", j4, cancellationToken).ConfigureAwait(false);

        // Quarantine one yoghurt batch for inspection.
        // (single-table UPDATE — MariaDB rejects LIMIT on multi-table UPDATE.)
        var ygtBatch = await ScalarAsync(connection,
            "SELECT b.`id` FROM `" + ErpFmcgWorkspaceSchema.Batches + "` b JOIN `" + ErpFmcgWorkspaceSchema.Items + "` i ON i.`id`=b.`item_id`"
            + " WHERE i.`sku`='FM-YGT-PLN170' ORDER BY b.`id` LIMIT 1", cancellationToken);
        if (ygtBatch > 0)
        {
            await ErpDb.ExecuteAsync(connection, null,
                "UPDATE `" + ErpFmcgWorkspaceSchema.Batches + "` SET `quality_status`='quarantine' WHERE `id`=@p0",
                cancellationToken, ygtBatch).ConfigureAwait(false);
        }

        // --- Customer claims: quality return + shortage credit ---
        var claimRows = new (string No, string Cust, int ItemIdx, string Type, decimal Qty, decimal Credit, string Status, long D, string Res)[]
        {
            ("CLM-0001", "Al Meera Consumer Goods", 3, "quality", 24m, 156m, "open", now - 5 * day, ""),
            ("CLM-0002", "Lulu Hypermarket", 0, "shortage", 60m, 288m, "resolved", now - 9 * day, "Credit note CN-220 issued"),
        };
        foreach (var cl in claimRows)
        {
            var clId = await InsertIfAbsentAsync(connection,
                "SELECT `id` FROM `" + ErpFmcgWorkspaceSchema.Claims + "` WHERE `claim_no`=@p0",
                "INSERT INTO `" + ErpFmcgWorkspaceSchema.Claims + "` (`claim_no`,`customer_name`,`item_id`,`claim_type`,`qty`,`credit_amount`,`status`,`claim_date`,`resolution`,`time_created`,`time_modified`)"
                + " VALUES (@p0,@p1,@p2,@p3,@p4,@p5,@p6,@p7,@p8,@p9,@p9)",
                cancellationToken, cl.No, cl.Cust, itemIds[cl.ItemIdx], cl.Type, cl.Qty, cl.Credit, cl.Status, cl.D, cl.Res, now);
            await MarkAsync(connection, ErpFmcgWorkspaceSchema.Claims, clId, cancellationToken).ConfigureAwait(false);
        }

        // --- Operational records: orders, invoices, bills, cash & stock —
        // the layer the dashboard KPI tiles and the AR/AP/order pages read.
        await SeedDemoOpsAsync(connection, items, itemIds, now, day, cancellationToken).ConfigureAwait(false);

        return $"FMCG demo data loaded: {entities.Length} entities, {bus.Length} BUs, {ccs.Length} cost centres, {items.Length} SKUs, batches/channels/routes/promotion, BOM + production orders + claims, 4 GL journals incl. write-down & production variance, plus operational orders/invoices/bills/cash/stock (QAR).";
    }

    private static async Task EnsureDemoOpsSchemaAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        // Minimal legacy-compatible tables the dashboard KPI queries read. ERP-only
        // tenant DBs do not carry the storefront schema, so the seed creates only
        // the columns the KPI/AR/AP/order read-paths use.
        var ddl = new[]
        {
            "CREATE TABLE IF NOT EXISTS `shop_orders_statuses_ref` (`id` int(11) NOT NULL AUTO_INCREMENT, `name` varchar(60) NOT NULL DEFAULT '', `for_finish` tinyint(1) NOT NULL DEFAULT 0, `count_flag` tinyint(1) NOT NULL DEFAULT 1, PRIMARY KEY (`id`), UNIQUE KEY `ux_sosr_name` (`name`)) ENGINE=InnoDB DEFAULT CHARSET=utf8",
            "CREATE TABLE IF NOT EXISTS `shop_orders_items_statuses_ref` (`id` int(11) NOT NULL AUTO_INCREMENT, `name` varchar(60) NOT NULL DEFAULT '', `for_finish` tinyint(1) NOT NULL DEFAULT 0, `count_flag` tinyint(1) NOT NULL DEFAULT 1, PRIMARY KEY (`id`), UNIQUE KEY `ux_soisr_name` (`name`)) ENGINE=InnoDB DEFAULT CHARSET=utf8",
            "CREATE TABLE IF NOT EXISTS `shop_orders` (`id` int(11) NOT NULL AUTO_INCREMENT, `user_id` int(11) NOT NULL DEFAULT 0, `session_id` int(11) NOT NULL DEFAULT 0, `successfully_created` tinyint(1) NOT NULL DEFAULT 0, `status` int(11) NOT NULL DEFAULT 0, `time` int(11) NOT NULL DEFAULT 0, `price_total_wt` decimal(14,2) NOT NULL DEFAULT 0, `price_total_wt_vat` decimal(14,2) NOT NULL DEFAULT 0, `paid` tinyint(1) NOT NULL DEFAULT 0, `paid_type` int(11) NOT NULL DEFAULT 0, `how_get` int(11) NOT NULL DEFAULT 0, `how_get_json` text, `phone_not_auth` varchar(120) NOT NULL DEFAULT '', `email_not_auth` varchar(190) NOT NULL DEFAULT '', `office_id` int(11) NOT NULL DEFAULT 0, `currency_code` varchar(8) NOT NULL DEFAULT 'QAR', PRIMARY KEY (`id`), KEY `ix_sou` (`user_id`), KEY `ix_sot` (`time`)) ENGINE=InnoDB DEFAULT CHARSET=utf8",
            "CREATE TABLE IF NOT EXISTS `shop_orders_items` (`id` int(11) NOT NULL AUTO_INCREMENT, `order_id` int(11) NOT NULL DEFAULT 0, `title` varchar(190) NOT NULL DEFAULT '', `price` decimal(14,2) NOT NULL DEFAULT 0, `count_need` decimal(14,2) NOT NULL DEFAULT 0, `count_issued` decimal(14,2) NOT NULL DEFAULT 0, `count_reserved` decimal(14,2) NOT NULL DEFAULT 0, `t2_price_purchase` decimal(14,2) NOT NULL DEFAULT 0, `status` int(11) NOT NULL DEFAULT 0, PRIMARY KEY (`id`), KEY `ix_soi_o` (`order_id`)) ENGINE=InnoDB DEFAULT CHARSET=utf8",
            "CREATE TABLE IF NOT EXISTS `epc_invoices` (`id` int(11) NOT NULL AUTO_INCREMENT, `invoice_number` varchar(40) NOT NULL DEFAULT '', `customer_name` varchar(190) NOT NULL DEFAULT '', `total_amount` decimal(14,2) NOT NULL DEFAULT 0, `paid_amount` decimal(14,2) NOT NULL DEFAULT 0, `status` varchar(20) NOT NULL DEFAULT 'sent', `currency_code` varchar(8) NOT NULL DEFAULT 'QAR', `active` tinyint(1) NOT NULL DEFAULT 1, `time_created` int(11) NOT NULL DEFAULT 0, PRIMARY KEY (`id`), UNIQUE KEY `ux_inv_no` (`invoice_number`)) ENGINE=InnoDB DEFAULT CHARSET=utf8",
            "CREATE TABLE IF NOT EXISTS `epc_bills` (`id` int(11) NOT NULL AUTO_INCREMENT, `bill_number` varchar(40) NOT NULL DEFAULT '', `supplier_name` varchar(190) NOT NULL DEFAULT '', `total_amount` decimal(14,2) NOT NULL DEFAULT 0, `paid_amount` decimal(14,2) NOT NULL DEFAULT 0, `status` varchar(20) NOT NULL DEFAULT 'received', `currency_code` varchar(8) NOT NULL DEFAULT 'QAR', `active` tinyint(1) NOT NULL DEFAULT 1, `time_created` int(11) NOT NULL DEFAULT 0, PRIMARY KEY (`id`), UNIQUE KEY `ux_bill_no` (`bill_number`)) ENGINE=InnoDB DEFAULT CHARSET=utf8",
            "CREATE TABLE IF NOT EXISTS `shop_users_accounting` (`id` int(11) NOT NULL AUTO_INCREMENT, `user_id` int(11) NOT NULL DEFAULT 0, `order_id` int(11) NOT NULL DEFAULT 0, `income` tinyint(1) NOT NULL DEFAULT 0, `amount` decimal(14,2) NOT NULL DEFAULT 0, `operation_code` varchar(40) NOT NULL DEFAULT '', `office_id` int(11) NOT NULL DEFAULT 0, `active` tinyint(1) NOT NULL DEFAULT 1, `time` int(11) NOT NULL DEFAULT 0, PRIMARY KEY (`id`), KEY `ix_sua_u` (`user_id`), KEY `ix_sua_o` (`order_id`)) ENGINE=InnoDB DEFAULT CHARSET=utf8",
            "CREATE TABLE IF NOT EXISTS `epc_einvoice_documents` (`id` int(11) NOT NULL AUTO_INCREMENT, `uuid` char(36) NOT NULL, `invoice_number` varchar(64) NOT NULL DEFAULT '', `order_id` int(11) NOT NULL DEFAULT 0, `user_id` int(11) NOT NULL DEFAULT 0, `doc_category` varchar(32) NOT NULL DEFAULT 'tax_invoice', `issue_date` int(11) NOT NULL DEFAULT 0, `payment_due_date` int(11) NOT NULL DEFAULT 0, `currency_code` varchar(8) NOT NULL DEFAULT 'QAR', `seller_json` mediumtext, `buyer_json` mediumtext, `subtotal_ex_vat` decimal(14,2) NOT NULL DEFAULT 0, `total_vat` decimal(14,2) NOT NULL DEFAULT 0, `total_incl_vat` decimal(14,2) NOT NULL DEFAULT 0, `paid_amount` decimal(14,2) NOT NULL DEFAULT 0, `amount_due` decimal(14,2) NOT NULL DEFAULT 0, `status` varchar(20) NOT NULL DEFAULT 'draft', `active` tinyint(1) NOT NULL DEFAULT 1, `time_created` int(11) NOT NULL DEFAULT 0, PRIMARY KEY (`id`), UNIQUE KEY `ux_ein_no` (`invoice_number`)) ENGINE=InnoDB DEFAULT CHARSET=utf8",
        };
        foreach (var sql in ddl)
        {
            await ErpDb.ExecuteAsync(connection, null, sql, cancellationToken).ConfigureAwait(false);
        }

        // Columns other read-paths select that a pre-existing minimal table may
        // lack (dashboard batch reads `epc_erp_suppliers.balance`; order readers
        // select shop_orders.how_get_json etc.).
        await EnsureColumnAsync(connection, "epc_erp_suppliers", "balance", "`balance` decimal(14,2) NOT NULL DEFAULT 0", cancellationToken).ConfigureAwait(false);
        await EnsureColumnAsync(connection, "shop_orders", "session_id", "`session_id` int(11) NOT NULL DEFAULT 0", cancellationToken).ConfigureAwait(false);
        await EnsureColumnAsync(connection, "shop_orders", "paid_type", "`paid_type` int(11) NOT NULL DEFAULT 0", cancellationToken).ConfigureAwait(false);
        await EnsureColumnAsync(connection, "shop_orders", "how_get", "`how_get` int(11) NOT NULL DEFAULT 0", cancellationToken).ConfigureAwait(false);
        await EnsureColumnAsync(connection, "shop_orders", "how_get_json", "`how_get_json` text", cancellationToken).ConfigureAwait(false);
        await EnsureColumnAsync(connection, "shop_orders", "phone_not_auth", "`phone_not_auth` varchar(120) NOT NULL DEFAULT ''", cancellationToken).ConfigureAwait(false);
        await EnsureColumnAsync(connection, "shop_orders", "email_not_auth", "`email_not_auth` varchar(190) NOT NULL DEFAULT ''", cancellationToken).ConfigureAwait(false);
        await EnsureColumnAsync(connection, "shop_orders", "office_id", "`office_id` int(11) NOT NULL DEFAULT 0", cancellationToken).ConfigureAwait(false);
        await EnsureColumnAsync(connection, "shop_orders_items", "count_issued", "`count_issued` decimal(14,2) NOT NULL DEFAULT 0", cancellationToken).ConfigureAwait(false);
        await EnsureColumnAsync(connection, "shop_orders_items", "count_reserved", "`count_reserved` decimal(14,2) NOT NULL DEFAULT 0", cancellationToken).ConfigureAwait(false);
    }

    private static async Task EnsureColumnAsync(DbConnection connection, string table, string column, string definition, CancellationToken ct)
    {
        var present = await ScalarAsync(connection,
            "SELECT COUNT(*) FROM information_schema.COLUMNS WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME='" + table + "' AND COLUMN_NAME='" + column + "'",
            ct).ConfigureAwait(false);
        if (present == 0)
        {
            await ErpDb.TryExecuteAsync(connection,
                "ALTER TABLE `" + table + "` ADD COLUMN " + definition,
                ct).ConfigureAwait(false);
        }
    }

    private async Task SeedDemoOpsAsync(
        DbConnection connection,
        (string Sku, string Name, string Brand, string Cat, string Gtin, string Uom, int Upc, int Cpp, int Shelf, string Excise, decimal Cost, decimal Price, string Chan)[] items,
        List<long> itemIds,
        long now,
        long day,
        CancellationToken cancellationToken)
    {
        await EnsureDemoOpsSchemaAsync(connection, cancellationToken).ConfigureAwait(false);

        // Dashboard resolves the MAIN company as active — make it Qatar/QAR so the
        // currency chip and amounts read correctly for this tenant.
        await ErpDb.TryExecuteAsync(connection,
            "UPDATE `epc_erp_pm_legal_entities` SET `country_code`='QA', `currency_code`='QAR' WHERE `code`='MAIN' AND (`currency_code` IS NULL OR `currency_code` <> 'QAR' OR `country_code` <> 'QA')",
            cancellationToken).ConfigureAwait(false);

        // Finished order/item statuses (the KPI queries only count for_finish rows).
        foreach (var t in new[] { "shop_orders_statuses_ref", "shop_orders_items_statuses_ref" })
        {
            var sid = await InsertIfAbsentAsync(connection,
                "SELECT `id` FROM `" + t + "` WHERE `name`='delivered'",
                "INSERT INTO `" + t + "` (`name`,`for_finish`,`count_flag`) VALUES ('delivered',1,1)",
                cancellationToken);
            await MarkAsync(connection, t, sid, cancellationToken).ConfigureAwait(false);
        }
        var finStatus = await ScalarAsync(connection,
            "SELECT `id` FROM `shop_orders_statuses_ref` WHERE `name`='delivered'", cancellationToken);

        // Demo customers (no password — cannot log in; receivables page rows).
        // users.reg_variant is varchar on some tenants, int on others — match the type.
        var regVariantNumeric = (await ScalarAsync(connection,
            "SELECT COUNT(*) FROM information_schema.COLUMNS WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME='users' AND COLUMN_NAME='reg_variant' AND DATA_TYPE IN ('int','bigint','smallint','tinyint','mediumint')",
            cancellationToken).ConfigureAwait(false)) > 0;
        var regVariant = regVariantNumeric ? "1" : "customer";
        var customers = new[] { "demo-lulu-hypermarket@fmcg.ecomae.com", "demo-carrefour-qatar@fmcg.ecomae.com", "demo-almeera-retail@fmcg.ecomae.com", "demo-monoprix-qatar@fmcg.ecomae.com", "demo-mezmoon-grocery@fmcg.ecomae.com" };
        var custIds = new List<long>();
        foreach (var email in customers)
        {
            var uid = await InsertIfAbsentAsync(connection,
                "SELECT `user_id` FROM `users` WHERE `email`=@p0",
                "INSERT INTO `users` (`email`,`email_confirmed`,`phone_confirmed`,`password`,`unlocked`,`reg_variant`,`time_registered`) VALUES (@p0,0,0,'',0,@p1,@p2)",
                cancellationToken, email, regVariant, now);
            custIds.Add(uid);
            await MarkAsync(connection, "users", uid, cancellationToken).ConfigureAwait(false);
        }

        // Completed shop orders across the last ~90 days — revenue tiles, monthly
        // chart, order counts, purchase (COGS) ex-VAT, VAT-out and due-order AR.
        const decimal vatRate = 0.05m;
        var orderPlans = new (int Cust, int DaysAgo, int Paid, (int I, decimal Qty)[] L)[]
        {
            (0, 3, 1, new[] { (0, 60m), (4, 12m) }),
            (1, 5, 1, new[] { (1, 40m), (2, 24m) }),
            (2, 8, 1, new[] { (5, 30m), (7, 20m) }),
            (0, 12, 0, new[] { (0, 80m), (3, 40m) }),
            (3, 15, 0, new[] { (6, 60m), (7, 30m) }),
            (4, 18, 1, new[] { (6, 50m), (0, 20m) }),
            (1, 24, 1, new[] { (2, 36m), (5, 18m) }),
            (0, 33, 1, new[] { (4, 24m), (1, 30m) }),
            (2, 41, 1, new[] { (0, 45m), (7, 12m) }),
            (3, 55, 1, new[] { (3, 30m), (6, 40m) }),
            (4, 62, 1, new[] { (7, 24m), (0, 30m) }),
            (1, 75, 1, new[] { (1, 28m), (4, 10m) }),
        };
        foreach (var o in orderPlans)
        {
            var otime = now - o.DaysAgo * day;
            var wtEx = o.L.Sum(l => items[l.I].Price * l.Qty);
            var vat = Math.Round(wtEx * vatRate, 2);
            var wt = wtEx + vat;
            var oid = await InsertIfAbsentAsync(connection,
                "SELECT `id` FROM `shop_orders` WHERE `user_id`=@p0 AND `time`=@p1",
                "INSERT INTO `shop_orders` (`user_id`,`successfully_created`,`status`,`time`,`price_total_wt`,`price_total_wt_vat`,`paid`,`currency_code`) VALUES (@p0,1,@p1,@p2,@p3,@p4,@p5,'QAR')",
                cancellationToken, custIds[o.Cust], finStatus, otime, wt, vat, o.Paid);
            await MarkAsync(connection, "shop_orders", oid, cancellationToken).ConfigureAwait(false);

            foreach (var l in o.L)
            {
                var iid = await InsertIfAbsentAsync(connection,
                    "SELECT `id` FROM `shop_orders_items` WHERE `order_id`=@p0 AND `title`=@p1",
                    "INSERT INTO `shop_orders_items` (`order_id`,`title`,`price`,`count_need`,`t2_price_purchase`,`status`) VALUES (@p0,@p1,@p2,@p3,@p4,@p5)",
                    cancellationToken, oid, items[l.I].Name, items[l.I].Price, l.Qty, items[l.I].Cost, finStatus);
                await MarkAsync(connection, "shop_orders_items", iid, cancellationToken).ConfigureAwait(false);
            }

            // Customer ledger: billed amount, then a payment when the order is paid.
            var a1 = await InsertIfAbsentAsync(connection,
                "SELECT `id` FROM `shop_users_accounting` WHERE `order_id`=@p0 AND `income`=1",
                "INSERT INTO `shop_users_accounting` (`user_id`,`order_id`,`income`,`amount`,`operation_code`,`active`,`time`) VALUES (@p0,@p1,1,@p2,'invoice',1,@p3)",
                cancellationToken, custIds[o.Cust], oid, wt, otime);
            await MarkAsync(connection, "shop_users_accounting", a1, cancellationToken).ConfigureAwait(false);
            if (o.Paid == 1)
            {
                var a2 = await InsertIfAbsentAsync(connection,
                    "SELECT `id` FROM `shop_users_accounting` WHERE `order_id`=@p0 AND `income`=0",
                    "INSERT INTO `shop_users_accounting` (`user_id`,`order_id`,`income`,`amount`,`operation_code`,`active`,`time`) VALUES (@p0,@p1,0,@p2,'payment',1,@p3)",
                    cancellationToken, custIds[o.Cust], oid, wt, otime + 7200);
                await MarkAsync(connection, "shop_users_accounting", a2, cancellationToken).ConfigureAwait(false);
            }
        }

        // AR e-invoice documents (open balances feed the receivables tile + due count).
        var invs = new (string No, int Cust, string CustName, decimal Total, decimal PaidAmt, string St, string DocSt, int DaysAgo)[]
        {
            ("INV-D-1001", 0, "Lulu Hypermarket", 14175.00m, 0m, "sent", "submitted", 6),
            ("INV-D-1002", 1, "Carrefour Qatar", 9828.00m, 0m, "issued", "submitted", 10),
            ("INV-D-1003", 2, "Al Meera Retail", 7455.00m, 4000m, "part_paid", "accepted", 16),
            ("INV-D-1004", 3, "Monoprix Qatar", 5428.50m, 0m, "overdue", "accepted", 34),
        };
        foreach (var v in invs)
        {
            var vid = await InsertIfAbsentAsync(connection,
                "SELECT `id` FROM `epc_invoices` WHERE `invoice_number`=@p0",
                "INSERT INTO `epc_invoices` (`invoice_number`,`customer_name`,`total_amount`,`paid_amount`,`status`,`currency_code`,`active`,`time_created`) VALUES (@p0,@p1,@p2,@p3,@p4,'QAR',1,@p5)",
                cancellationToken, v.No, v.CustName, v.Total, v.PaidAmt, v.St, now - v.DaysAgo * day);
            await MarkAsync(connection, "epc_invoices", vid, cancellationToken).ConfigureAwait(false);

            var eid = await InsertIfAbsentAsync(connection,
                "SELECT `id` FROM `epc_einvoice_documents` WHERE `invoice_number`=@p0",
                "INSERT INTO `epc_einvoice_documents` (`uuid`,`invoice_number`,`user_id`,`doc_category`,`issue_date`,`payment_due_date`,`currency_code`,`buyer_json`,`subtotal_ex_vat`,`total_vat`,`total_incl_vat`,`paid_amount`,`amount_due`,`status`,`active`) VALUES (UUID(),@p0,@p1,'tax_invoice',@p2,@p3,'QAR',@p4,@p5,@p6,@p7,@p8,@p9,@p10,1)",
                cancellationToken, v.No, custIds[v.Cust], now - v.DaysAgo * day, now - v.DaysAgo * day + 30 * day,
                "{\"name\":\"" + v.CustName + "\"}",
                v.Total / 1.05m, v.Total - v.Total / 1.05m, v.Total, v.PaidAmt, v.Total - v.PaidAmt, v.DocSt);
            await MarkAsync(connection, "epc_einvoice_documents", eid, cancellationToken).ConfigureAwait(false);
        }

        // Suppliers + AP bills + purchases (payables tile, open-PO count, VAT-in).
        var supplierNames = new[] { "Al Sidra Dairy Co.", "Gulf Pack Trading W.L.L.", "Qatar Foods Import & Export" };
        var supIds = new List<long>();
        foreach (var s in supplierNames)
        {
            var sid = await InsertIfAbsentAsync(connection,
                "SELECT `id` FROM `epc_erp_suppliers` WHERE `name`=@p0",
                "INSERT INTO `epc_erp_suppliers` (`name`,`trn`,`currency_code`,`country_code`,`vat_registered`,`active`,`time_created`) VALUES (@p0,'3100' + FLOOR(RAND()*899999 + 100000),'QAR','QA',1,1,@p1)",
                cancellationToken, s, now);
            supIds.Add(sid);
            await MarkAsync(connection, "epc_erp_suppliers", sid, cancellationToken).ConfigureAwait(false);
        }
        await ErpDb.TryExecuteAsync(connection,
            "UPDATE `epc_erp_suppliers` SET `balance`=14175.00 WHERE `name`='Al Sidra Dairy Co.'",
            cancellationToken).ConfigureAwait(false);
        await ErpDb.TryExecuteAsync(connection,
            "UPDATE `epc_erp_suppliers` SET `balance`=8783.25 WHERE `name`='Gulf Pack Trading W.L.L.'",
            cancellationToken).ConfigureAwait(false);

        var bills = new (string No, int Sup, decimal Total, decimal PaidAmt, string St, int DaysAgo)[]
        {
            ("BILL-D-501", 0, 22680.00m, 0m, "received", 5),
            ("BILL-D-502", 1, 9427.50m, 5000m, "part_paid", 12),
            ("BILL-D-503", 2, 15340.00m, 0m, "overdue", 28),
        };
        foreach (var b in bills)
        {
            var bid = await InsertIfAbsentAsync(connection,
                "SELECT `id` FROM `epc_bills` WHERE `bill_number`=@p0",
                "INSERT INTO `epc_bills` (`bill_number`,`supplier_name`,`total_amount`,`paid_amount`,`status`,`currency_code`,`active`,`time_created`) VALUES (@p0,@p1,@p2,@p3,@p4,'QAR',1,@p5)",
                cancellationToken, b.No, supplierNames[b.Sup], b.Total, b.PaidAmt, b.St, now - b.DaysAgo * day);
            await MarkAsync(connection, "epc_bills", bid, cancellationToken).ConfigureAwait(false);
        }

        var purchs = new (int Sup, string Inv, decimal ExVat, int DaysAgo)[]
        {
            (0, "SUP-INV-3310", 21600.00m, 5),
            (1, "SUP-INV-4188", 8978.00m, 12),
            (2, "SUP-INV-5502", 14610.00m, 28),
        };
        foreach (var p in purchs)
        {
            var pid = await InsertIfAbsentAsync(connection,
                "SELECT `id` FROM `epc_erp_purchases` WHERE `invoice_number`=@p0",
                "INSERT INTO `epc_erp_purchases` (`supplier_id`,`invoice_number`,`purchase_date`,`amount_ex_vat`,`vat_amount`,`total_amount`,`vat_applicable`,`vat_rate`,`status`,`active`,`time_created`) VALUES (@p0,@p1,@p2,@p3,@p4,@p5,1,5,'posted',1,@p2)",
                cancellationToken, supIds[p.Sup], p.Inv, now - p.DaysAgo * day, p.ExVat, Math.Round(p.ExVat * vatRate, 2), p.ExVat + Math.Round(p.ExVat * vatRate, 2));
            await MarkAsync(connection, "epc_erp_purchases", pid, cancellationToken).ConfigureAwait(false);
        }

        // Open purchase orders feed the open-PO reminder tile.
        var pos = new (string No, int Sup, decimal ExVat, string St)[]
        {
            ("PO-D-7001", 0, 12600.00m, "approved"),
            ("PO-D-7002", 1, 8365.00m, "approved"),
            ("PO-D-7003", 2, 4920.00m, "draft"),
        };
        foreach (var p in pos)
        {
            var id = await InsertIfAbsentAsync(connection,
                "SELECT `id` FROM `epc_erp_purchase_orders` WHERE `po_no`=@p0",
                "INSERT INTO `epc_erp_purchase_orders` (`po_no`,`supplier_id`,`title`,`amount_ex_vat`,`vat_amount`,`total_amount`,`status`,`time_created`,`time_updated`) VALUES (@p0,@p1,'FMCG restock — demo',@p2,@p3,@p4,@p5,@p6,@p6)",
                cancellationToken, p.No, supIds[p.Sup], p.ExVat, Math.Round(p.ExVat * vatRate, 2), p.ExVat + Math.Round(p.ExVat * vatRate, 2), p.St, now - 4 * day);
            await MarkAsync(connection, "epc_erp_purchase_orders", id, cancellationToken).ConfigureAwait(false);
        }

        // Confirmed sales orders feed the SO tile.
        var sos = new (string No, int Cust, decimal ExVat)[]
        {
            ("SO-D-3001", 0, 18900.00m),
            ("SO-D-3002", 1, 11270.00m),
            ("SO-D-3003", 4, 6540.00m),
        };
        foreach (var s in sos)
        {
            var id = await InsertIfAbsentAsync(connection,
                "SELECT `id` FROM `epc_erp_sales_orders` WHERE `so_no`=@p0",
                "INSERT INTO `epc_erp_sales_orders` (`so_no`,`customer_user_id`,`title`,`amount_ex_vat`,`vat_amount`,`total_amount`,`status`,`time_created`,`time_updated`) VALUES (@p0,@p1,'FMCG order — demo',@p2,@p3,@p4,'confirmed',@p5,@p5)",
                cancellationToken, s.No, custIds[s.Cust], s.ExVat, Math.Round(s.ExVat * vatRate, 2), s.ExVat + Math.Round(s.ExVat * vatRate, 2), now - 2 * day);
            await MarkAsync(connection, "epc_erp_sales_orders", id, cancellationToken).ConfigureAwait(false);
        }

        // Cash & bank accounts + entries → cash-position tile and cash-accounts page.
        var qnb = await InsertIfAbsentAsync(connection,
            "SELECT `id` FROM `epc_erp_cash_bank_accounts` WHERE `name`='QNB current — QAR (demo)'",
            "INSERT INTO `epc_erp_cash_bank_accounts` (`name`,`account_type`,`bank_name`,`account_number`,`currency_code`,`opening_balance`,`active`,`time_created`) VALUES ('QNB current — QAR (demo)','bank','Qatar National Bank','QA04QNBA000000000123456789','QAR',250000,1,@p0)",
            cancellationToken, now);
        await MarkAsync(connection, "epc_erp_cash_bank_accounts", qnb, cancellationToken).ConfigureAwait(false);
        var petty = await InsertIfAbsentAsync(connection,
            "SELECT `id` FROM `epc_erp_cash_bank_accounts` WHERE `name`='Petty cash — HQ (demo)'",
            "INSERT INTO `epc_erp_cash_bank_accounts` (`name`,`account_type`,`bank_name`,`account_number`,`currency_code`,`opening_balance`,`active`,`time_created`) VALUES ('Petty cash — HQ (demo)','cash','','PC-001','QAR',5000,1,@p0)",
            cancellationToken, now);
        await MarkAsync(connection, "epc_erp_cash_bank_accounts", petty, cancellationToken).ConfigureAwait(false);

        var entries = new (long Acc, int Dir, decimal Amt, string Ref, int DaysAgo)[]
        {
            (qnb, 1, 41370.00m, "Lulu settlement — demo", 6),
            (qnb, 1, 27930.00m, "Carrefour settlement — demo", 11),
            (qnb, 0, 22140.00m, "Supplier payment — Al Sidra (demo)", 9),
            (qnb, 0, 3500.00m, "Qatar excise payment (demo)", 4),
            (petty, 0, 420.00m, "Route petty expense — demo", 3),
        };
        foreach (var e in entries)
        {
            var id = await InsertIfAbsentAsync(connection,
                "SELECT `id` FROM `epc_erp_cash_bank_entries` WHERE `account_id`=@p0 AND `reference`=@p1",
                "INSERT INTO `epc_erp_cash_bank_entries` (`account_id`,`time`,`entry_type`,`direction`,`amount`,`reference`,`active`) VALUES (@p0,@p1,@p2,@p3,@p4,@p5,1)",
                cancellationToken, e.Acc, now - e.DaysAgo * day, e.Dir == 1 ? "receipt" : "payment", e.Dir, e.Amt, e.Ref);
            await MarkAsync(connection, "epc_erp_cash_bank_entries", id, cancellationToken).ConfigureAwait(false);
        }

        // Inventory on hand → stock-value tile.
        for (var i = 0; i < itemIds.Count; i++)
        {
            var id = await InsertIfAbsentAsync(connection,
                "SELECT `id` FROM `epc_erp_inv_stock` WHERE `item_id`=@p0 AND `warehouse_id`=@p1",
                "INSERT INTO `epc_erp_inv_stock` (`warehouse_id`,`item_id`,`qty_on_hand`,`avg_unit_cost`,`owner_company_id`,`time_updated`) VALUES (@p0,@p1,@p2,@p3,1,@p4)",
                cancellationToken, itemIds[i], 1, 150m + i * 25m, items[i].Cost, now);
            await MarkAsync(connection, "epc_erp_inv_stock", id, cancellationToken).ConfigureAwait(false);
        }
    }

    public async Task<string> ClearDemoAsync(CancellationToken cancellationToken = default)
    {
        if (!_connections.IsConfigured)
        {
            return "TenantRegistry DB is not configured — clear skipped.";
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await ErpFmcgWorkspaceSchema.EnsureAsync(connection, cancellationToken).ConfigureAwait(false);
        var removed = 0;
        var rows = new List<(string T, long Id)>();
        await using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = "SELECT `table_name`,`row_id` FROM `" + ErpFmcgWorkspaceSchema.DemoRegistry + "`";
            await using var r = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await r.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                rows.Add((Convert.ToString(r.GetValue(0)) ?? "", Convert.ToInt64(r.GetValue(1))));
            }
        }

        {
            foreach (var (t, id) in rows)
            {
                if (t is "epc_erp_gl_journals")
                {
                    await ErpDb.ExecuteAsync(connection, null, "DELETE FROM `epc_erp_gl_lines` WHERE `journal_id`=@p0", cancellationToken, id).ConfigureAwait(false);
                }
                var allowed = t is "epc_erp_pm_legal_entities" or "epc_erp_pm_business_units" or "epc_erp_pm_cost_centres" or "epc_erp_gl_journals" or "epc_erp_gl_coa"
                    or "users" or "shop_orders" or "shop_orders_items" or "shop_orders_statuses_ref" or "shop_orders_items_statuses_ref" or "shop_users_accounting"
                    or "epc_invoices" or "epc_bills" or "epc_einvoice_documents" or "epc_erp_suppliers" or "epc_erp_purchases"
                    or "epc_erp_purchase_orders" or "epc_erp_sales_orders" or "epc_erp_cash_bank_accounts" or "epc_erp_cash_bank_entries" or "epc_erp_inv_stock"
                    || t.StartsWith("epc_erp_fmcg_", StringComparison.Ordinal);
                if (!allowed) continue;
                await ErpDb.ExecuteAsync(connection, null, $"DELETE FROM `{t}` WHERE `id`=@p0", cancellationToken, id).ConfigureAwait(false);
                removed++;
            }
        }
        await ErpDb.TryExecuteAsync(connection, "DELETE FROM `" + ErpFmcgWorkspaceSchema.DemoRegistry + "`", cancellationToken).ConfigureAwait(false);
        return $"Cleared {removed} demo rows.";
    }

    private static async Task<long> EnsureCoaAsync(DbConnection c, string code, string name, string type, string side, CancellationToken ct)
    {
        var id = await ScalarAsync(c, "SELECT `id` FROM `epc_erp_coa_accounts` WHERE `code`='" + code + "'", ct);
        if (id > 0) return id;
        await ErpDb.ExecuteAsync(c, null,
            "INSERT INTO `epc_erp_coa_accounts` (`code`,`name`,`account_type`,`normal_side`,`active`,`time_created`) VALUES (@p0,@p1,@p2,@p3,1,@p4)",
            ct, code, name, type, side, DateTimeOffset.UtcNow.ToUnixTimeSeconds());
        return await ScalarAsync(c, "SELECT `id` FROM `epc_erp_coa_accounts` WHERE `code`='" + code + "'", ct);
    }

    private static async Task<long> InsertIfAbsentAsync(DbConnection c, string existsSql, string insertSql, CancellationToken ct, params object[] args)
    {
        var existing = await ErpDb.ScalarAsync(c, null, existsSql, ct, args).ConfigureAwait(false);
        if (existing is not null) return Convert.ToInt64(existing);
        await ErpDb.ExecuteAsync(c, null, insertSql, ct, args).ConfigureAwait(false);
        await using var idCmd = c.CreateCommand();
        idCmd.CommandText = "SELECT LAST_INSERT_ID()";
        var id = await idCmd.ExecuteScalarAsync(ct).ConfigureAwait(false);
        return id is null or DBNull ? 0 : Convert.ToInt64(id);
    }

    private static async Task MarkAsync(DbConnection c, string table, long id, CancellationToken ct)
    {
        if (id <= 0) return;
        await ErpDb.ExecuteAsync(c, null,
            "INSERT INTO `" + ErpFmcgWorkspaceSchema.DemoRegistry + "` (`table_name`,`row_id`,`time_created`) VALUES (@p0,@p1,@p2)",
            ct, table, id, DateTimeOffset.UtcNow.ToUnixTimeSeconds()).ConfigureAwait(false);
    }

    private static async Task<long> ScalarAsync(DbConnection c, string sql, CancellationToken ct)
    {
        await using var cmd = c.CreateCommand();
        cmd.CommandText = sql;
        var v = await cmd.ExecuteScalarAsync(ct).ConfigureAwait(false);
        return v is null or DBNull ? 0 : Convert.ToInt64(v);
    }
}
