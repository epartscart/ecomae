using System.Data.Common;
using System.Globalization;

namespace EcomAE.Platform.Erp;

/// <summary>Verbatim row-count/error report of PHP epc_jw_seed_sample_data.</summary>
public sealed record ErpJwSeedResult(
    int Warehouses,
    int Items,
    int Suppliers,
    int Customers,
    int Purchases,
    int Sales,
    int Repairs,
    int GlEntries,
    int Compliance,
    IReadOnlyList<string> Errors);

/// <summary>
/// Live PHP <c>epc_jw_seed_sample_data</c> / ajax <c>jw_seed_sample_data</c> twin.
/// Ensures the jewellery-integration schema (jw_* column ALTERs on inv_items/purchase_orders/
/// inv_movements/inv_stock/sales_order_lines + jw_repairs/jw_weight_ledger CREATEs), flips
/// erp_industry_profile to 'jewellery', then seeds warehouses/items/contacts/POs/SOs/repairs/
/// weight-ledger GL rows with per-row try/catch into <c>seeded.errors</c> exactly like PHP.
/// </summary>
public interface IErpJwSeedWriteService
{
    Task<ErpJwSeedResult> SeedAsync(int adminId, CancellationToken cancellationToken = default);
}

public sealed class ErpJwSeedWriteService : IErpJwSeedWriteService
{
    private readonly IErpWriteConnectionFactory _connections;
    private readonly IErpVoucherNumberService _vouchers;
    private readonly IErpBosComplianceFetchService _compliance;
    private readonly TimeProvider _clock;

    public ErpJwSeedWriteService(
        IErpWriteConnectionFactory connections,
        IErpVoucherNumberService vouchers,
        IErpBosComplianceFetchService compliance,
        TimeProvider clock)
    {
        _connections = connections;
        _vouchers = vouchers;
        _compliance = compliance;
        _clock = clock;
    }

    public async Task<ErpJwSeedResult> SeedAsync(int adminId, CancellationToken cancellationToken = default)
    {
        var errors = new List<string>();
        var seeded = new Dictionary<string, int>(StringComparer.Ordinal)
        {
            ["warehouses"] = 0, ["items"] = 0, ["suppliers"] = 0, ["customers"] = 0,
            ["purchases"] = 0, ["sales"] = 0, ["repairs"] = 0, ["gl_entries"] = 0, ["compliance"] = 0,
        };

        var now = _clock.GetUtcNow().ToUnixTimeSeconds();

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await EnsureSchemaAsync(connection, cancellationToken).ConfigureAwait(false);

        // epc_erp_adv_set_setting($db, 'erp_industry_profile', 'jewellery')
        await ErpDb.ExecuteAsync(
            connection, null,
            ErpDb.Positional("INSERT INTO `epc_price_settings` (`setting_key`, `setting_value`) VALUES ('erp_industry_profile', 'jewellery') ON DUPLICATE KEY UPDATE `setting_value` = VALUES(`setting_value`)"),
            cancellationToken).ConfigureAwait(false);

        // 1. Warehouses
        var warehouses = new (string Code, string Name)[]
        {
            ("WH-SHOWROOM", "Main Showroom"),
            ("WH-VAULT", "Vault / Safe Storage"),
            ("WH-WORKSHOP", "Workshop / Repair"),
        };
        foreach (var (code, name) in warehouses)
        {
            try
            {
                await ErpDb.ExecuteAsync(
                    connection, null,
                    ErpDb.Positional("INSERT IGNORE INTO `epc_erp_inv_warehouses` (`code`, `name`, `time_created`) VALUES (?,?,?)"),
                    cancellationToken, code, name, now).ConfigureAwait(false);
                seeded["warehouses"]++;
            }
            catch (Exception ex) { errors.Add("warehouse: " + ex.Message); }
        }

        var whShowroom = await ErpDb.LongAsync(
            connection, null,
            ErpDb.Positional("SELECT `id` FROM `epc_erp_inv_warehouses` WHERE `code`='WH-SHOWROOM' LIMIT 1"),
            cancellationToken).ConfigureAwait(false);
        if (whShowroom <= 0) whShowroom = 1;

        // 2. Inventory items — jewellery
        var items = new (string Sku, string Name, string Metal, string Karat, decimal Purity, decimal GrossWt, decimal NetWt, decimal StoneWt, string StoneTyp, int StonePcs, string Div, decimal Making)[]
        {
            ("GR-22K-001", "22K Gold Ring — Classic Band", "Gold", "22K", 0.916700m, 8.500m, 7.200m, 0m, "", 0, "G", 12.00m),
            ("GN-22K-001", "22K Gold Necklace — Rope Chain", "Gold", "22K", 0.916700m, 45.000m, 44.500m, 0m, "", 0, "G", 10.00m),
            ("GB-22K-001", "22K Gold Bangle — Floral", "Gold", "22K", 0.916700m, 32.000m, 31.200m, 0m, "", 0, "G", 15.00m),
            ("GE-22K-001", "22K Gold Earrings — Jhumka", "Gold", "22K", 0.916700m, 12.500m, 11.800m, 0m, "", 0, "G", 14.00m),
            ("GR-18K-001", "18K Gold Ring — Diamond Solitaire", "Gold", "18K", 0.750000m, 5.200m, 3.800m, 1.200m, "Diamond", 1, "G", 25.00m),
            ("GN-18K-001", "18K Gold Pendant — Heart", "Gold", "18K", 0.750000m, 6.800m, 5.500m, 0.400m, "Ruby", 1, "G", 20.00m),
            ("GP-24K-001", "24K Gold Bar — 10g PAMP", "Gold", "24K", 0.999900m, 10.000m, 10.000m, 0m, "", 0, "G", 0.00m),
            ("GP-24K-002", "24K Gold Coin — 5g", "Gold", "24K", 0.999900m, 5.000m, 5.000m, 0m, "", 0, "G", 0.00m),
            ("SR-925-001", "Sterling Silver Ring — Gemstone", "Silver", "925", 0.925000m, 6.000m, 5.200m, 0.500m, "Topaz", 1, "S", 3.00m),
            ("SN-925-001", "Sterling Silver Chain — Box Link", "Silver", "925", 0.925000m, 22.000m, 22.000m, 0m, "", 0, "S", 2.00m),
            ("DN-001", "Loose Diamond — 1.02ct Round Brilliant", "Diamond", "", 0m, 0.204m, 0.204m, 0m, "", 0, "D", 0.00m),
            ("DN-002", "Loose Diamond — 0.50ct Princess Cut", "Diamond", "", 0m, 0.100m, 0.100m, 0m, "", 0, "D", 0.00m),
            ("PN-001", "South Sea Pearl Strand — 18\"", "Pearl", "", 0m, 85.000m, 85.000m, 0m, "", 0, "P", 0.00m),
            ("GW-22K-001", "22K Gold Watch — Men's Dress", "Gold", "22K", 0.916700m, 65.000m, 28.000m, 0m, "", 0, "G", 18.00m),
            ("PT-950-001", "Platinum Band — Comfort Fit", "Platinum", "950", 0.950000m, 10.500m, 10.200m, 0m, "", 0, "T", 30.00m),
        };
        const decimal goldRate = 235.00m;
        foreach (var it in items)
        {
            var metalValue = it.NetWt * goldRate * it.Purity;
            if (it.Metal == "Silver") metalValue = it.NetWt * 3.50m;
            if (it.Metal == "Diamond") metalValue = it.NetWt * 5m * 45000m;
            if (it.Metal == "Pearl") metalValue = it.GrossWt * 150m;
            if (it.Metal == "Platinum") metalValue = it.NetWt * 120m * it.Purity;
            var makingTotal = it.GrossWt * it.Making;
            var totalCost = metalValue + makingTotal;
            var salesPrice = totalCost * 1.15m;
            var unit = it.Metal == "Diamond" ? "carat" : "gram";
            try
            {
                await ErpDb.ExecuteAsync(
                    connection, null,
                    ErpDb.Positional(
                        "INSERT IGNORE INTO `epc_erp_inv_items` (`sku`, `name`, `item_type`, `unit`, `active`, `time_created`, `jw_metal_type`, `jw_karat`, `jw_purity`, `jw_gross_wt`, `jw_net_wt`, `jw_stone_wt`, `jw_stone_type`, `jw_stone_pcs`, `jw_division`, `jw_making_charge_per_gm`, `standard_cost`, `sales_price`, `purchase_price`) VALUES (?,?,?,?,1,?,?,?,?,?,?,?,?,?,?,?,?,?,?)"),
                    cancellationToken,
                    it.Sku, it.Name, "serialized", unit, now,
                    it.Metal, it.Karat, it.Purity, it.GrossWt, it.NetWt,
                    it.StoneWt, it.StoneTyp, it.StonePcs, it.Div,
                    it.Making, Round2(totalCost), Round2(salesPrice), Round2(totalCost)).ConfigureAwait(false);
                seeded["items"]++;
                var itemId = await ErpDb.LastInsertIdAsync(connection, null, cancellationToken).ConfigureAwait(false);
                if (itemId > 0)
                {
                    await ErpDb.ExecuteAsync(
                        connection, null,
                        ErpDb.Positional("INSERT IGNORE INTO `epc_erp_inv_stock` (`warehouse_id`, `item_id`, `qty_on_hand`, `avg_unit_cost`, `jw_weight_on_hand`, `jw_karat`, `time_updated`) VALUES (?,?,?,?,?,?,?)"),
                        cancellationToken, whShowroom, itemId, 1, Round4(totalCost), it.GrossWt, it.Karat, now).ConfigureAwait(false);
                }
            }
            catch (Exception ex) { errors.Add("item " + it.Sku + ": " + ex.Message); }
        }

        // 3. Suppliers + 4. Customers (epc_erp_contacts)
        var suppliers = new (string Name, string Phone, string Email, string Country)[]
        {
            ("Dubai Gold Souk Trading LLC", "+971-4-226-1234", "goldsouk@example.com", "AE"),
            ("Rajesh Gems & Jewellery Pvt Ltd", "+91-22-2389-5678", "rajesh@example.com", "IN"),
            ("Antwerp Diamond Exchange BVBA", "+32-3-233-9012", "antwerp@example.com", "BE"),
            ("PAMP SA — Swiss Bullion", "+41-91-695-3456", "pamp@example.com", "CH"),
            ("Mikimoto Pearl Co. Ltd", "+81-3-3535-7890", "mikimoto@example.com", "JP"),
        };
        foreach (var s in suppliers)
        {
            try
            {
                await ErpDb.ExecuteAsync(
                    connection, null,
                    ErpDb.Positional("INSERT IGNORE INTO `epc_erp_contacts` (`name`, `party_type`, `phone`, `email`, `country_code`, `notes`, `time_created`) VALUES (?,?,?,?,?,?,?)"),
                    cancellationToken, s.Name, "supplier", s.Phone, s.Email, s.Country, "Sample jewellery supplier", now).ConfigureAwait(false);
                seeded["suppliers"]++;
            }
            catch (Exception ex) { errors.Add("supplier " + s.Name + ": " + ex.Message); }
        }

        var customers = new (string Name, string Phone, string Email, string Country)[]
        {
            ("Mrs. Fatima Al Maktoum", "+971-50-123-4567", "fatima@example.com", "AE"),
            ("Mr. Rajiv Sharma", "+971-55-234-5678", "rajiv@example.com", "IN"),
            ("Ms. Sarah Chen", "+971-52-345-6789", "sarah@example.com", "CN"),
            ("Mr. James Wilson", "+971-56-456-7890", "james@example.com", "GB"),
            ("Mrs. Aisha Bin Khalifa", "+971-50-567-8901", "aisha@example.com", "AE"),
        };
        var customerContactIds = new long[customers.Length];
        for (var ci = 0; ci < customers.Length; ci++)
        {
            var c = customers[ci];
            try
            {
                await ErpDb.ExecuteAsync(
                    connection, null,
                    ErpDb.Positional("INSERT IGNORE INTO `epc_erp_contacts` (`name`, `party_type`, `phone`, `email`, `country_code`, `notes`, `time_created`) VALUES (?,?,?,?,?,?,?)"),
                    cancellationToken, c.Name, "customer", c.Phone, c.Email, c.Country, "Sample jewellery customer", now).ConfigureAwait(false);
                var cid = await ErpDb.LastInsertIdAsync(connection, null, cancellationToken).ConfigureAwait(false);
                if (cid == 0)
                {
                    cid = await ErpDb.LongAsync(
                        connection, null,
                        ErpDb.Positional("SELECT `id` FROM `epc_erp_contacts` WHERE `email` = ? AND `party_type` = 'customer' LIMIT 1"),
                        cancellationToken, c.Email).ConfigureAwait(false);
                }
                customerContactIds[ci] = cid;
                seeded["customers"]++;
            }
            catch (Exception ex) { errors.Add("customer " + c.Name + ": " + ex.Message); }
        }

        // 5. Purchase orders with jewellery weight fields
        var poData = new (string PoNo, string Title, decimal MetalWtGm, decimal RatePerGram, string Karat, decimal Purity, decimal Making, decimal StoneVal)[]
        {
            ("PO-JW-001", "22K Gold — 500g Bar", 500.000m, 215.47m, "22K", 0.916700m, 5500.00m, 0m),
            ("PO-JW-002", "Finished 22K Bangles — 10 pcs", 320.000m, 215.47m, "22K", 0.916700m, 4800.00m, 2500.00m),
            ("PO-JW-003", "Loose Diamonds — 5ct total", 1.000m, 0m, "", 0m, 0m, 225000.00m),
            ("PO-JW-004", "24K Gold Bars — 100g", 100.000m, 235.00m, "24K", 0.999900m, 0m, 0m),
            ("PO-JW-005", "South Sea Pearls — 20 strands", 0m, 0m, "", 0m, 0m, 180000.00m),
        };
        var rng = new Random();
        foreach (var po in poData)
        {
            var metalValue = po.MetalWtGm * po.RatePerGram;
            var total = metalValue + po.Making + po.StoneVal;
            try
            {
                await ErpDb.ExecuteAsync(
                    connection, null,
                    ErpDb.Positional(
                        "INSERT IGNORE INTO `epc_erp_purchase_orders` (`po_no`, `title`, `amount_ex_vat`, `total_amount`, `status`, `jw_metal_weight_gm`, `jw_rate_per_gram`, `jw_metal_value`, `jw_making_charge`, `jw_stone_value`, `jw_karat`, `jw_purity`, `time_created`, `time_updated`) VALUES (?,?,?,?,?,?,?,?,?,?,?,?,?,?)"),
                    cancellationToken,
                    po.PoNo, po.Title, Round2(total), Round2(total * 1.05m), "received",
                    po.MetalWtGm, po.RatePerGram, Round2(metalValue), po.Making, po.StoneVal,
                    po.Karat, po.Purity, now - rng.Next(86400, 604800), now).ConfigureAwait(false);
                seeded["purchases"]++;
                var poId = await ErpDb.LastInsertIdAsync(connection, null, cancellationToken).ConfigureAwait(false);
                if (po.MetalWtGm > 0)
                {
                    await PostWeightLedgerAsync(connection, new WeightLedgerEntry(
                        "1300", "Metal Inventory", 0, "purchase", poId,
                        po.PoNo, po.Karat.Length > 0 ? "Gold" : "", po.Karat, po.MetalWtGm, 0m,
                        Round2(total * 1.05m), 0m, "Purchase: " + po.Title, 0), cancellationToken).ConfigureAwait(false);
                }
            }
            catch (Exception ex) { errors.Add("PO " + po.PoNo + ": " + ex.Message); }
        }

        // 6. Sales orders — header first, then line with correct FK
        var salesData = new (int CustIdx, string Title, decimal WeightGm, decimal RatePerGram, string Karat, decimal Making, decimal StoneVal, decimal Discount)[]
        {
            (0, "22K Gold Necklace — Rope Chain", 44.500m, 235.00m, "22K", 450.00m, 0m, 500.00m),
            (1, "22K Gold Ring — Classic Band + Diamond", 7.200m, 235.00m, "22K", 96.00m, 15000.00m, 0m),
            (2, "18K Gold Pendant — Heart with Ruby", 5.500m, 200.00m, "18K", 110.00m, 8000.00m, 0m),
            (3, "24K Gold Bar — 10g PAMP", 10.000m, 235.00m, "24K", 0m, 0m, 0m),
            (4, "22K Gold Bangles Set (4 pcs)", 128.000m, 235.00m, "22K", 1920.00m, 0m, 1000.00m),
        };
        foreach (var so in salesData)
        {
            var contactId = so.CustIdx < customerContactIds.Length ? customerContactIds[so.CustIdx] : 0;
            var metalVal = so.WeightGm * so.RatePerGram;
            var lineTotal = metalVal + so.Making + so.StoneVal - so.Discount;
            try
            {
                var soNo = await _vouchers.NextAsync(connection, null, "SO", cancellationToken).ConfigureAwait(false);
                await ErpDb.ExecuteAsync(
                    connection, null,
                    ErpDb.Positional(
                        "INSERT INTO `epc_erp_sales_orders` (`so_no`, `contact_id`, `title`, `amount_ex_vat`, `vat_amount`, `total_amount`, `status`, `admin_id`, `time_created`, `time_updated`) VALUES (?,?,?,?,?,?,?,?,?,?)"),
                    cancellationToken,
                    soNo, contactId, so.Title,
                    Round2(lineTotal), Round2(lineTotal * 0.05m), Round2(lineTotal * 1.05m),
                    "confirmed", adminId, now - rng.Next(86400, 604800), now).ConfigureAwait(false);
                var soId = await ErpDb.LastInsertIdAsync(connection, null, cancellationToken).ConfigureAwait(false);
                await ErpDb.ExecuteAsync(
                    connection, null,
                    ErpDb.Positional(
                        "INSERT INTO `epc_erp_sales_order_lines` (`sales_order_id`, `line_no`, `description`, `qty`, `unit_price_ex_vat`, `line_ex_vat`, `jw_weight_gm`, `jw_rate_per_gram`, `jw_metal_value`, `jw_making_charge`, `jw_stone_value`, `jw_karat`, `jw_discount`) VALUES (?,1,?,1,?,?,?,?,?,?,?,?,?)"),
                    cancellationToken,
                    soId, so.Title, Round4(lineTotal), Round2(lineTotal),
                    so.WeightGm, so.RatePerGram, Round2(metalVal), so.Making,
                    so.StoneVal, so.Karat, so.Discount).ConfigureAwait(false);
                seeded["sales"]++;
                if (so.WeightGm > 0)
                {
                    await PostWeightLedgerAsync(connection, new WeightLedgerEntry(
                        "4100", "Sales Revenue", 0, "sale", soId,
                        soNo, "Gold", so.Karat, 0m, so.WeightGm,
                        0m, Round2(lineTotal), "Sale: " + so.Title, 0), cancellationToken).ConfigureAwait(false);
                    await PostWeightLedgerAsync(connection, new WeightLedgerEntry(
                        "1300", "Metal Inventory", 0, "sale", soId,
                        soNo, "Gold", so.Karat, 0m, so.WeightGm,
                        0m, Round2(metalVal), "COGS: " + so.Title, 0), cancellationToken).ConfigureAwait(false);
                }
            }
            catch (Exception ex) { errors.Add("SO seed " + so.Title + ": " + ex.Message); }
        }

        // 7. Repair jobs — deterministic repair_no as PHP md5('seed_'.$ri.'_'.$name)
        var repairs = new (string Name, string Phone, string Desc, string Metal, string Karat, decimal GrossIn, decimal NetIn, string RepairType, decimal EstCost)[]
        {
            ("Mrs. Fatima Al Maktoum", "+971-50-123-4567", "22K Gold Ring — resize", "Gold", "22K", 8.5m, 7.2m, "Ring Resize", 150.00m),
            ("Mr. Rajiv Sharma", "+971-55-234-5678", "18K Chain — broken clasp repair", "Gold", "18K", 22.0m, 21.5m, "Clasp Repair", 250.00m),
            ("Ms. Sarah Chen", "+971-52-345-6789", "Diamond re-setting on platinum ring", "Platinum", "950", 10.5m, 10.2m, "Stone Setting", 450.00m),
        };
        for (var ri = 0; ri < repairs.Length; ri++)
        {
            var rp = repairs[ri];
            try
            {
                var repairNo = "RPR-" + Md5Hex("seed_" + ri + "_" + rp.Name)[..6].ToUpperInvariant();
                await ErpDb.ExecuteAsync(
                    connection, null,
                    ErpDb.Positional(
                        "INSERT INTO `epc_erp_jw_repairs` (`repair_no`, `customer_id`, `customer_name`, `customer_phone`, `item_description`, `metal_type`, `karat`, `gross_wt_in`, `net_wt_in`, `stone_details`, `repair_type`, `estimated_cost`, `status`, `received_date`, `promised_date`, `admin_id`, `time_created`) VALUES (?,?,?,?,?,?,?,?,?,?,?,?,'received',?,?,?,?)"),
                    cancellationToken,
                    repairNo, 0, rp.Name, rp.Phone, rp.Desc, rp.Metal, rp.Karat,
                    rp.GrossIn, rp.NetIn, "", rp.RepairType, rp.EstCost,
                    now - (ri + 1) * 86400, now + (ri + 1) * 86400 * 2, adminId, now).ConfigureAwait(false);
                seeded["repairs"]++;
            }
            catch (Exception ex) { errors.Add("repair #" + (ri + 1) + " (" + rp.Name + "): " + ex.Message); }
        }

        // 8. GL entries for dual trial balance
        var glEntries = new (string Code, string Name, string SrcType, long SrcId, string Metal, string Karat, decimal WIn, decimal WOut, decimal Debit, decimal Credit, string Narr)[]
        {
            ("1100", "Cash & Bank", "opening", 0, "", "", 0m, 0m, 500000.00m, 0m, "Opening cash balance"),
            ("1300", "Metal Inventory — Gold", "opening", 0, "Gold", "22K", 800.000m, 0m, 172376.00m, 0m, "Opening gold stock 800g 22K"),
            ("1310", "Metal Inventory — Silver", "opening", 0, "Silver", "925", 5000.000m, 0m, 17500.00m, 0m, "Opening silver stock 5kg"),
            ("1320", "Diamond Inventory", "opening", 0, "Diamond", "", 25.000m, 0m, 450000.00m, 0m, "Opening diamond inventory 25ct"),
            ("1400", "Stone Inventory", "opening", 0, "", "", 0m, 0m, 85000.00m, 0m, "Opening stone/pearl inventory"),
            ("2100", "Accounts Payable", "opening", 0, "", "", 0m, 0m, 0m, 125000.00m, "Opening AP balance"),
            ("3100", "Capital", "opening", 0, "", "", 0m, 0m, 0m, 1099876.00m, "Owner equity"),
        };
        foreach (var gl in glEntries)
        {
            try
            {
                await PostWeightLedgerAsync(connection, new WeightLedgerEntry(
                    gl.Code, gl.Name, 0, gl.SrcType, gl.SrcId, "", gl.Metal, gl.Karat,
                    gl.WIn, gl.WOut, gl.Debit, gl.Credit, gl.Narr, 0), cancellationToken).ConfigureAwait(false);
                seeded["gl_entries"]++;
            }
            catch (Exception ex) { errors.Add("GL " + gl.Code + ": " + ex.Message); }
        }

        // 9. Compliance — obligations + retention seed honours the new erp_industry_profile.
        try
        {
            await _compliance.FetchAsync(cancellationToken).ConfigureAwait(false);
            seeded["compliance"] = (int)await ErpDb.LongAsync(
                connection, null,
                ErpDb.Positional("SELECT COUNT(*) FROM `epc_bos_compliance_obligations` WHERE `active` = 1"),
                cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) { errors.Add("compliance seed: " + ex.Message); }

        return new ErpJwSeedResult(
            seeded["warehouses"], seeded["items"], seeded["suppliers"], seeded["customers"],
            seeded["purchases"], seeded["sales"], seeded["repairs"], seeded["gl_entries"],
            seeded["compliance"], errors);
    }

    private sealed record WeightLedgerEntry(
        string AccountCode, string AccountName, long TransactionDate, string SourceType, long SourceId,
        string Reference, string MetalType, string Karat, decimal WeightIn, decimal WeightOut,
        decimal ValueDebit, decimal ValueCredit, string Narration, long AdminId);

    /// <summary>PHP epc_jw_post_weight_ledger.</summary>
    private async Task PostWeightLedgerAsync(DbConnection connection, WeightLedgerEntry entry, CancellationToken cancellationToken)
    {
        await ErpDb.ExecuteAsync(
            connection, null,
            ErpDb.Positional(
                "INSERT INTO `epc_erp_jw_weight_ledger` (`account_code`, `account_name`, `transaction_date`, `source_type`, `source_id`, `reference`, `metal_type`, `karat`, `weight_in`, `weight_out`, `value_debit`, `value_credit`, `narration`, `admin_id`, `time_created`) VALUES (?,?,?,?,?,?,?,?,?,?,?,?,?,?,?)"),
            cancellationToken,
            entry.AccountCode, entry.AccountName,
            entry.TransactionDate > 0 ? entry.TransactionDate : _clock.GetUtcNow().ToUnixTimeSeconds(),
            entry.SourceType, entry.SourceId, entry.Reference, entry.MetalType, entry.Karat,
            entry.WeightIn, entry.WeightOut, entry.ValueDebit, entry.ValueCredit,
            entry.Narration, entry.AdminId, _clock.GetUtcNow().ToUnixTimeSeconds()).ConfigureAwait(false);
    }

    private static decimal Round2(decimal v) => Math.Round(v, 2, MidpointRounding.AwayFromZero);
    private static decimal Round4(decimal v) => Math.Round(v, 4, MidpointRounding.AwayFromZero);

    private static string Md5Hex(string input)
    {
        var bytes = System.Security.Cryptography.MD5.HashData(System.Text.Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    /// <summary>
    /// PHP epc_jw_ensure_integration_schema + epc_erp_inventory_ensure_schema +
    /// epc_erp_vouchers_ensure_schema + epc_erp_adv_settings_ensure + contacts/purchase_orders
    /// base tables the seeder writes to.
    /// </summary>
    private static async Task EnsureSchemaAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        var creates = new[]
        {
            @"CREATE TABLE IF NOT EXISTS `epc_erp_inv_warehouses` (
                `id` int(11) NOT NULL AUTO_INCREMENT,
                `storage_id` int(11) NOT NULL DEFAULT 0,
                `code` varchar(32) NOT NULL DEFAULT '',
                `name` varchar(255) NOT NULL,
                `active` tinyint(1) NOT NULL DEFAULT 1,
                `time_created` int(11) NOT NULL DEFAULT 0,
                PRIMARY KEY (`id`),
                UNIQUE KEY `x_code` (`code`),
                KEY `x_storage` (`storage_id`)
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8",
            @"CREATE TABLE IF NOT EXISTS `epc_erp_inv_field_defs` (
                `id` int(11) NOT NULL AUTO_INCREMENT,
                `field_key` varchar(32) NOT NULL,
                `label` varchar(120) NOT NULL,
                `field_type` enum('text','number','date','select') NOT NULL DEFAULT 'text',
                `options_json` text,
                `sort_order` int(11) NOT NULL DEFAULT 0,
                `active` tinyint(1) NOT NULL DEFAULT 1,
                PRIMARY KEY (`id`),
                UNIQUE KEY `x_key` (`field_key`)
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8",
            @"CREATE TABLE IF NOT EXISTS `epc_erp_inv_items` (
                `id` int(11) NOT NULL AUTO_INCREMENT,
                `sku` varchar(64) NOT NULL,
                `name` varchar(255) NOT NULL,
                `product_id` int(11) NOT NULL DEFAULT 0,
                `item_type` enum('standard','perishable','serialized') NOT NULL DEFAULT 'standard',
                `track_expiry` tinyint(1) NOT NULL DEFAULT 0,
                `unit` varchar(16) NOT NULL DEFAULT 'pcs',
                `active` tinyint(1) NOT NULL DEFAULT 1,
                `time_created` int(11) NOT NULL DEFAULT 0,
                PRIMARY KEY (`id`),
                UNIQUE KEY `x_sku` (`sku`),
                KEY `x_product` (`product_id`)
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8",
            @"CREATE TABLE IF NOT EXISTS `epc_erp_inv_item_fields` (
                `item_id` int(11) NOT NULL,
                `field_key` varchar(32) NOT NULL,
                `value` varchar(512) NOT NULL DEFAULT '',
                PRIMARY KEY (`item_id`,`field_key`)
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8",
            @"CREATE TABLE IF NOT EXISTS `epc_erp_inv_stock` (
                `id` int(11) NOT NULL AUTO_INCREMENT,
                `warehouse_id` int(11) NOT NULL,
                `item_id` int(11) NOT NULL,
                `qty_on_hand` decimal(14,3) NOT NULL DEFAULT 0.000,
                `avg_unit_cost` decimal(14,4) NOT NULL DEFAULT 0.0000,
                `expiry_date` date DEFAULT NULL,
                `batch_no` varchar(64) DEFAULT NULL,
                `variant_label` varchar(120) DEFAULT NULL,
                `time_updated` int(11) NOT NULL DEFAULT 0,
                PRIMARY KEY (`id`),
                UNIQUE KEY `x_wh_item_batch` (`warehouse_id`,`item_id`,`batch_no`,`variant_label`),
                KEY `x_item` (`item_id`),
                KEY `x_expiry` (`expiry_date`)
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8",
            @"CREATE TABLE IF NOT EXISTS `epc_erp_inv_movements` (
                `id` int(11) NOT NULL AUTO_INCREMENT,
                `movement_type` enum('opening','purchase_in','sale_out','transfer_in','transfer_out','adjustment','return_in','return_out') NOT NULL,
                `warehouse_id` int(11) NOT NULL,
                `item_id` int(11) NOT NULL,
                `qty` decimal(14,3) NOT NULL DEFAULT 0.000,
                `unit_cost` decimal(14,4) NOT NULL DEFAULT 0.0000,
                `total_cost` decimal(14,2) NOT NULL DEFAULT 0.00,
                `transfer_warehouse_id` int(11) NOT NULL DEFAULT 0,
                `purchase_id` int(11) NOT NULL DEFAULT 0,
                `order_id` int(11) NOT NULL DEFAULT 0,
                `batch_no` varchar(64) DEFAULT NULL,
                `expiry_date` date DEFAULT NULL,
                `reference` varchar(128) DEFAULT NULL,
                `note` text,
                `movement_date` int(11) NOT NULL DEFAULT 0,
                `admin_id` int(11) NOT NULL DEFAULT 0,
                `opening_batch_id` int(11) NOT NULL DEFAULT 0,
                `active` tinyint(1) NOT NULL DEFAULT 1,
                PRIMARY KEY (`id`),
                KEY `x_wh_item` (`warehouse_id`,`item_id`,`movement_date`),
                KEY `x_type` (`movement_type`)
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8",
            @"CREATE TABLE IF NOT EXISTS `epc_erp_inv_closing` (
                `id` int(11) NOT NULL AUTO_INCREMENT,
                `period_end` date NOT NULL,
                `warehouse_id` int(11) NOT NULL DEFAULT 0,
                `item_id` int(11) NOT NULL,
                `qty_closing` decimal(14,3) NOT NULL DEFAULT 0.000,
                `avg_unit_cost` decimal(14,4) NOT NULL DEFAULT 0.0000,
                `value_closing` decimal(14,2) NOT NULL DEFAULT 0.00,
                `time_created` int(11) NOT NULL DEFAULT 0,
                PRIMARY KEY (`id`),
                UNIQUE KEY `x_period` (`period_end`,`warehouse_id`,`item_id`)
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8",
            @"CREATE TABLE IF NOT EXISTS `epc_erp_contacts` (
                `id` int(11) NOT NULL AUTO_INCREMENT,
                `party_type` enum('customer','supplier','both','staff','other') NOT NULL DEFAULT 'customer',
                `name` varchar(255) NOT NULL,
                `company` varchar(255) DEFAULT NULL,
                `email` varchar(255) DEFAULT NULL,
                `phone` varchar(64) DEFAULT NULL,
                `trn` varchar(64) DEFAULT NULL,
                `address` text,
                `city` varchar(128) DEFAULT NULL,
                `country_code` varchar(8) NOT NULL DEFAULT 'AE',
                `linked_user_id` int(11) NOT NULL DEFAULT 0,
                `linked_supplier_id` int(11) NOT NULL DEFAULT 0,
                `notes` text,
                `active` tinyint(1) NOT NULL DEFAULT 1,
                `time_created` int(11) NOT NULL DEFAULT 0,
                `time_updated` int(11) NOT NULL DEFAULT 0,
                PRIMARY KEY (`id`),
                KEY `x_party` (`party_type`,`active`),
                KEY `x_user` (`linked_user_id`),
                KEY `x_supplier` (`linked_supplier_id`)
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='ERP unified contacts / third parties'",
            @"CREATE TABLE IF NOT EXISTS `epc_erp_purchase_orders` (
                `id` int(11) NOT NULL AUTO_INCREMENT,
                `po_no` varchar(32) NOT NULL,
                `supplier_id` int(11) NOT NULL DEFAULT 0,
                `title` varchar(255) NOT NULL DEFAULT '',
                `amount_ex_vat` decimal(14,2) NOT NULL DEFAULT 0.00,
                `vat_amount` decimal(14,2) NOT NULL DEFAULT 0.00,
                `total_amount` decimal(14,2) NOT NULL DEFAULT 0.00,
                `status` enum('draft','approved','partial','received','cancelled') NOT NULL DEFAULT 'draft',
                `purchase_id` int(11) NOT NULL DEFAULT 0,
                `order_id` int(11) NOT NULL DEFAULT 0,
                `approved_at` int(11) NOT NULL DEFAULT 0,
                `received_at` int(11) NOT NULL DEFAULT 0,
                `notes` text,
                `admin_id` int(11) NOT NULL DEFAULT 0,
                `time_created` int(11) NOT NULL DEFAULT 0,
                `time_updated` int(11) NOT NULL DEFAULT 0,
                PRIMARY KEY (`id`),
                UNIQUE KEY `x_po_no` (`po_no`),
                KEY `x_supplier` (`supplier_id`),
                KEY `x_status` (`status`)
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='Purchase orders with approval workflow'",
            @"CREATE TABLE IF NOT EXISTS `epc_erp_voucher_sequences` (
                `voucher_type` varchar(8) NOT NULL,
                `year` int(11) NOT NULL,
                `last_seq` int(11) NOT NULL DEFAULT 0,
                PRIMARY KEY (`voucher_type`, `year`)
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='ERP voucher number sequences'",
            @"CREATE TABLE IF NOT EXISTS `epc_erp_sales_orders` (
                `id` int(11) NOT NULL AUTO_INCREMENT,
                `so_no` varchar(32) NOT NULL,
                `customer_user_id` int(11) NOT NULL DEFAULT 0,
                `contact_id` int(11) NOT NULL DEFAULT 0,
                `title` varchar(255) NOT NULL DEFAULT '',
                `amount_ex_vat` decimal(14,2) NOT NULL DEFAULT 0.00,
                `vat_amount` decimal(14,2) NOT NULL DEFAULT 0.00,
                `total_amount` decimal(14,2) NOT NULL DEFAULT 0.00,
                `status` enum('draft','confirmed','invoiced','cancelled') NOT NULL DEFAULT 'draft',
                `sales_invoice_id` int(11) NOT NULL DEFAULT 0,
                `notes` text,
                `admin_id` int(11) NOT NULL DEFAULT 0,
                `time_created` int(11) NOT NULL DEFAULT 0,
                `time_updated` int(11) NOT NULL DEFAULT 0,
                PRIMARY KEY (`id`),
                UNIQUE KEY `x_so_no` (`so_no`),
                KEY `x_customer` (`customer_user_id`),
                KEY `x_status` (`status`)
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='ERP manual sales orders (no storefront)'",
            @"CREATE TABLE IF NOT EXISTS `epc_erp_sales_order_lines` (
                `id` int(11) NOT NULL AUTO_INCREMENT,
                `sales_order_id` int(11) NOT NULL,
                `line_no` int(11) NOT NULL DEFAULT 1,
                `description` varchar(255) NOT NULL,
                `qty` decimal(14,3) NOT NULL DEFAULT 1.000,
                `unit_price_ex_vat` decimal(14,4) NOT NULL DEFAULT 0.0000,
                `line_ex_vat` decimal(14,2) NOT NULL DEFAULT 0.00,
                PRIMARY KEY (`id`),
                KEY `x_so` (`sales_order_id`)
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='ERP sales order lines'",
            @"CREATE TABLE IF NOT EXISTS `epc_erp_jw_repairs` (
                `id` int(11) NOT NULL AUTO_INCREMENT,
                `repair_no` varchar(32) NOT NULL,
                `customer_id` int(11) NOT NULL DEFAULT 0,
                `customer_name` varchar(255) NOT NULL DEFAULT '',
                `customer_phone` varchar(30) NOT NULL DEFAULT '',
                `item_description` varchar(255) NOT NULL DEFAULT '',
                `metal_type` varchar(16) NOT NULL DEFAULT '',
                `karat` varchar(10) NOT NULL DEFAULT '',
                `gross_wt_in` decimal(12,3) NOT NULL DEFAULT 0.000,
                `net_wt_in` decimal(12,3) NOT NULL DEFAULT 0.000,
                `stone_details` text,
                `repair_type` varchar(60) NOT NULL DEFAULT '',
                `estimated_cost` decimal(14,2) NOT NULL DEFAULT 0.00,
                `actual_cost` decimal(14,2) NOT NULL DEFAULT 0.00,
                `status` enum('received','in_progress','ready','delivered','invoiced') NOT NULL DEFAULT 'received',
                `received_date` int(11) NOT NULL DEFAULT 0,
                `promised_date` int(11) NOT NULL DEFAULT 0,
                `delivered_date` int(11) NOT NULL DEFAULT 0,
                `gross_wt_out` decimal(12,3) NOT NULL DEFAULT 0.000,
                `workshop_notes` text,
                `invoice_id` int(11) NOT NULL DEFAULT 0,
                `admin_id` int(11) NOT NULL DEFAULT 0,
                `time_created` int(11) NOT NULL DEFAULT 0,
                `time_updated` int(11) NOT NULL DEFAULT 0,
                PRIMARY KEY (`id`),
                UNIQUE KEY `x_repair_no` (`repair_no`),
                KEY `x_status` (`status`),
                KEY `x_customer` (`customer_id`)
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8",
            @"CREATE TABLE IF NOT EXISTS `epc_erp_jw_weight_ledger` (
                `id` int(11) NOT NULL AUTO_INCREMENT,
                `account_code` varchar(20) NOT NULL,
                `account_name` varchar(120) NOT NULL DEFAULT '',
                `transaction_date` int(11) NOT NULL DEFAULT 0,
                `source_type` enum('purchase','sale','adjustment','transfer','repair','opening') NOT NULL DEFAULT 'purchase',
                `source_id` int(11) NOT NULL DEFAULT 0,
                `reference` varchar(64) NOT NULL DEFAULT '',
                `metal_type` varchar(16) NOT NULL DEFAULT '',
                `karat` varchar(10) NOT NULL DEFAULT '',
                `weight_in` decimal(12,3) NOT NULL DEFAULT 0.000,
                `weight_out` decimal(12,3) NOT NULL DEFAULT 0.000,
                `value_debit` decimal(14,2) NOT NULL DEFAULT 0.00,
                `value_credit` decimal(14,2) NOT NULL DEFAULT 0.00,
                `narration` varchar(255) NOT NULL DEFAULT '',
                `admin_id` int(11) NOT NULL DEFAULT 0,
                `time_created` int(11) NOT NULL DEFAULT 0,
                PRIMARY KEY (`id`),
                KEY `x_account` (`account_code`),
                KEY `x_date` (`transaction_date`),
                KEY `x_source` (`source_type`, `source_id`),
                KEY `x_metal` (`metal_type`, `karat`)
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8",
            @"CREATE TABLE IF NOT EXISTS `epc_price_settings` (
                `setting_key` varchar(128) NOT NULL,
                `setting_value` text,
                PRIMARY KEY (`setting_key`)
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8",
        };
        foreach (var ddl in creates)
        {
            await ErpDb.ExecuteAsync(connection, null, ddl, cancellationToken).ConfigureAwait(false);
        }

        // epc_jw_ensure_integration_schema column ALTERs (epc_erp_schema_add_column_if_missing).
        var columns = new (string Table, string Column, string Ddl)[]
        {
            ("epc_erp_inv_items", "jw_metal_type", "varchar(16) NOT NULL DEFAULT ''"),
            ("epc_erp_inv_items", "jw_karat", "varchar(10) NOT NULL DEFAULT ''"),
            ("epc_erp_inv_items", "jw_purity", "decimal(7,6) NOT NULL DEFAULT 0.000000"),
            ("epc_erp_inv_items", "jw_gross_wt", "decimal(12,3) NOT NULL DEFAULT 0.000"),
            ("epc_erp_inv_items", "jw_net_wt", "decimal(12,3) NOT NULL DEFAULT 0.000"),
            ("epc_erp_inv_items", "jw_stone_wt", "decimal(12,3) NOT NULL DEFAULT 0.000"),
            ("epc_erp_inv_items", "jw_stone_type", "varchar(60) NOT NULL DEFAULT ''"),
            ("epc_erp_inv_items", "jw_stone_pcs", "int(11) NOT NULL DEFAULT 0"),
            ("epc_erp_inv_items", "jw_hallmark", "varchar(40) NOT NULL DEFAULT ''"),
            ("epc_erp_inv_items", "jw_design_no", "varchar(30) NOT NULL DEFAULT ''"),
            ("epc_erp_inv_items", "jw_making_charge_per_gm", "decimal(12,2) NOT NULL DEFAULT 0.00"),
            ("epc_erp_inv_items", "jw_division", "char(2) NOT NULL DEFAULT ''"),
            ("epc_erp_purchase_orders", "jw_metal_weight_gm", "decimal(12,3) NOT NULL DEFAULT 0.000"),
            ("epc_erp_purchase_orders", "jw_rate_per_gram", "decimal(12,2) NOT NULL DEFAULT 0.00"),
            ("epc_erp_purchase_orders", "jw_metal_value", "decimal(14,2) NOT NULL DEFAULT 0.00"),
            ("epc_erp_purchase_orders", "jw_making_charge", "decimal(14,2) NOT NULL DEFAULT 0.00"),
            ("epc_erp_purchase_orders", "jw_stone_value", "decimal(14,2) NOT NULL DEFAULT 0.00"),
            ("epc_erp_purchase_orders", "jw_karat", "varchar(10) NOT NULL DEFAULT ''"),
            ("epc_erp_purchase_orders", "jw_purity", "decimal(7,6) NOT NULL DEFAULT 0.000000"),
            ("epc_erp_inv_movements", "jw_weight_gm", "decimal(12,3) NOT NULL DEFAULT 0.000"),
            ("epc_erp_inv_movements", "jw_rate_per_gram", "decimal(12,2) NOT NULL DEFAULT 0.00"),
            ("epc_erp_inv_movements", "jw_karat", "varchar(10) NOT NULL DEFAULT ''"),
            ("epc_erp_inv_stock", "jw_weight_on_hand", "decimal(12,3) NOT NULL DEFAULT 0.000"),
            ("epc_erp_inv_stock", "jw_karat", "varchar(10) NOT NULL DEFAULT ''"),
            ("epc_erp_sales_order_lines", "jw_weight_gm", "decimal(12,3) NOT NULL DEFAULT 0.000"),
            ("epc_erp_sales_order_lines", "jw_rate_per_gram", "decimal(12,2) NOT NULL DEFAULT 0.00"),
            ("epc_erp_sales_order_lines", "jw_metal_value", "decimal(14,2) NOT NULL DEFAULT 0.00"),
            ("epc_erp_sales_order_lines", "jw_making_charge", "decimal(14,2) NOT NULL DEFAULT 0.00"),
            ("epc_erp_sales_order_lines", "jw_stone_value", "decimal(14,2) NOT NULL DEFAULT 0.00"),
            ("epc_erp_sales_order_lines", "jw_karat", "varchar(10) NOT NULL DEFAULT ''"),
            ("epc_erp_sales_order_lines", "jw_discount", "decimal(14,2) NOT NULL DEFAULT 0.00"),
            ("epc_erp_sales_order_lines", "item_code", "varchar(32) NOT NULL DEFAULT ''"),
            ("epc_erp_purchase_orders", "voucher_no", "varchar(32) DEFAULT NULL"),
            ("epc_erp_purchases", "voucher_no", "varchar(32) DEFAULT NULL"),
            ("epc_erp_purchases", "po_id", "int(11) NOT NULL DEFAULT 0"),
            ("epc_erp_cash_bank_entries", "voucher_no", "varchar(32) DEFAULT NULL"),
            ("epc_erp_cash_bank_entries", "sales_order_id", "int(11) NOT NULL DEFAULT 0"),
            ("epc_erp_cash_bank_entries", "sales_invoice_id", "int(11) NOT NULL DEFAULT 0"),
        };
        foreach (var (table, column, ddl) in columns)
        {
            var exists = await ErpDb.LongAsync(
                connection, null,
                ErpDb.Positional("SELECT COUNT(*) FROM information_schema.COLUMNS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = ? AND COLUMN_NAME = ?"),
                cancellationToken, table, column).ConfigureAwait(false);
            if (exists == 0)
            {
                var tableExists = await ErpDb.LongAsync(
                    connection, null,
                    ErpDb.Positional("SELECT COUNT(*) FROM information_schema.TABLES WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = ?"),
                    cancellationToken, table).ConfigureAwait(false);
                if (tableExists > 0)
                {
                    await ErpDb.ExecuteAsync(
                        connection, null,
                        "ALTER TABLE `" + table + "` ADD COLUMN `" + column + "` " + ddl,
                        cancellationToken).ConfigureAwait(false);
                }
            }
        }

        await ErpDb.ExecuteAsync(
            connection, null,
            "UPDATE `epc_erp_purchase_orders` SET `voucher_no` = `po_no` WHERE (`voucher_no` IS NULL OR `voucher_no` = '') AND `po_no` != ''",
            cancellationToken).ConfigureAwait(false);
    }
}
