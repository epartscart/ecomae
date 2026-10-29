using System.Data.Common;
using System.Globalization;

namespace EcomAE.Platform.Erp;

/// <summary>
/// Jewellery chart-of-accounts additions, GL recipes and the stock movement
/// (weight + value at average cost) that keep the metal stock report, the
/// jewellery stock valuation and the trial balance in agreement.
/// </summary>
public static class ErpJewelleryGl
{
    public const string CashAccount = "1000";
    public const string PettyCashAccount = "1020";
    public const string CardClearingAccount = "1030";
    public const string OldGoldAccount = "1320";
    public const string CustomerAdvanceAccount = "2300";
    public const string OpeningEquityAccount = "3900";
    public const string SalesReturnAccount = "4100";

    public static readonly IReadOnlyList<(string Code, string Name, string Type, string Side, string Description)> Accounts =
    [
        (CashAccount, "Cash on hand", "asset", "debit", "POS cash tenders (INDUS 120000 Cash On Hand)"),
        (PettyCashAccount, "Petty cash", "asset", "debit", "Petty cash float (INDUS 120001 Petty Cash)"),
        (CardClearingAccount, "Card / wallet clearing", "asset", "debit", "VISA / MASTER / NETWORK / TABBY tenders awaiting settlement"),
        (OldGoldAccount, "Old gold & scrap metal stock", "asset", "debit", "Old gold taken in exchange on POS sales"),
        (CustomerAdvanceAccount, "Customer advances (POS)", "liability", "credit", "POS receipts / advances from retail customers (INDUS 2801)"),
        (OpeningEquityAccount, "Opening balance equity", "equity", "credit", "Contra for migrated opening stock and balances"),
        (SalesReturnAccount, "Sales returns", "revenue", "debit", "Contra revenue — POS sales returns / tax credit notes"),
    ];

    public static async Task EnsureCoaAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        foreach (var row in Accounts)
        {
            await ErpDb.ExecuteAsync(connection, null, ErpDb.Positional(
                "INSERT IGNORE INTO `epc_erp_coa_accounts` (`code`, `name`, `account_type`, `normal_side`, `description`, `system_flag`, `time_created`) VALUES (?, ?, ?, ?, ?, 1, ?)"),
                cancellationToken, row.Code, row.Name, row.Type, row.Side, row.Description, now).ConfigureAwait(false);
        }
    }

    /// <summary>POS tender → GL account.</summary>
    public static string TenderAccount(string? mode)
    {
        var m = (mode ?? string.Empty).Trim().ToUpperInvariant();
        if (m.Length == 0 || m is "CASH" or "NET") return CashAccount;
        if (m.Contains("BANK", StringComparison.Ordinal) || m.Contains("TRANSFER", StringComparison.Ordinal) || m.Contains("CHQ", StringComparison.Ordinal) || m.Contains("CHEQUE", StringComparison.Ordinal)) return ErpAutoPartsIfrs.BankAccount;
        if (m.Contains("ADV", StringComparison.Ordinal)) return CustomerAdvanceAccount;
        return CardClearingAccount;
    }

    public static IReadOnlyList<ErpAutoPartsIfrs.JournalLine> OpeningStock(decimal value)
        =>
        [
            new(ErpAutoPartsIfrs.InventoryAccount, value, 0m, "Opening jewellery stock"),
            new(OpeningEquityAccount, 0m, value, "Opening balance equity"),
        ];

    public static IReadOnlyList<ErpAutoPartsIfrs.JournalLine> CostOfSale(decimal cost)
        =>
        [
            new(ErpAutoPartsIfrs.CogsAccount, cost, 0m, "Cost of jewellery sold (average cost)"),
            new(ErpAutoPartsIfrs.InventoryAccount, 0m, cost, "Stock issued"),
        ];

    public static IReadOnlyList<ErpAutoPartsIfrs.JournalLine> CostReturned(decimal cost)
        =>
        [
            new(ErpAutoPartsIfrs.InventoryAccount, cost, 0m, "Returned item back to stock"),
            new(ErpAutoPartsIfrs.CogsAccount, 0m, cost, "Cost of sale reversed"),
        ];

    public static IReadOnlyList<ErpAutoPartsIfrs.JournalLine> OldGoldExchange(decimal amount)
        =>
        [
            new(OldGoldAccount, amount, 0m, "Old gold received in exchange"),
            new(ErpAutoPartsIfrs.ArAccount, 0m, amount, "Settled by old gold"),
        ];

    /// <summary>
    /// UAE domestic reverse charge on precious-metal purchases between registered dealers
    /// (Cabinet Decision 25/2018): the buyer self-accounts output VAT and recovers the same
    /// amount as input VAT. Nothing is paid to the supplier.
    /// </summary>
    public static IReadOnlyList<ErpAutoPartsIfrs.JournalLine> ReverseCharge(decimal rcmVat)
        => rcmVat <= 0
            ? []
            :
            [
                new(ErpAutoPartsIfrs.InputVatAccount, rcmVat, 0m, "Input VAT recoverable — reverse charge (precious metal)"),
                new(ErpAutoPartsIfrs.VatAccount, 0m, rcmVat, "Output VAT self-accounted — reverse charge (precious metal)"),
            ];

    /// <summary>IAS 2 purchase cost: the line costs that entered the stock sub-ledger; falls back to the header net when no line moved stock.</summary>
    public static decimal PurchaseInventoryCost(decimal stockLineCost, decimal headerNet)
        => stockLineCost > 0 ? stockLineCost : headerNet;

    public static IReadOnlyList<ErpAutoPartsIfrs.JournalLine> SalesReturn(decimal net, decimal vat)
    {
        var lines = new List<ErpAutoPartsIfrs.JournalLine> { new(SalesReturnAccount, net, 0m, "Sales return (tax credit note)") };
        if (vat != 0) lines.Add(new(ErpAutoPartsIfrs.VatAccount, vat, 0m, "Output VAT reversed"));
        lines.Add(new(ErpAutoPartsIfrs.ArAccount, 0m, net + vat, "Credit note payable to customer"));
        return lines;
    }

    public static IReadOnlyList<ErpAutoPartsIfrs.JournalLine> Advance(IReadOnlyList<(string Account, decimal Amount)> tenders, decimal gross, decimal vat)
    {
        var lines = new List<ErpAutoPartsIfrs.JournalLine>();
        var received = tenders.Sum(t => t.Amount);
        if (received == 0 && gross != 0) lines.Add(new(CashAccount, gross, 0m, "Advance received"));
        foreach (var t in tenders) lines.Add(new(t.Account, t.Amount, 0m, "Advance received"));
        var total = received == 0 ? gross : received;
        lines.Add(new(CustomerAdvanceAccount, 0m, total - vat, "Customer advance (POS receipt)"));
        if (vat != 0) lines.Add(new(ErpAutoPartsIfrs.VatAccount, 0m, vat, "VAT on advance"));
        return lines;
    }

    /// <summary>Tenders against a sale (Dr tender / Cr AR), a return refund (Dr AR / Cr tender) or a purchase payment (Dr AP / Cr tender).</summary>
    public static IReadOnlyList<ErpAutoPartsIfrs.JournalLine> Tenders(IReadOnlyList<(string Account, decimal Amount)> tenders, string counterAccount, bool tenderIsDebit)
    {
        var lines = new List<ErpAutoPartsIfrs.JournalLine>();
        var total = 0m;
        foreach (var t in tenders.GroupBy(t => t.Account))
        {
            var amount = t.Sum(x => x.Amount);
            if (amount == 0) continue;
            total += amount;
            lines.Add(tenderIsDebit ? new(t.Key, amount, 0m, "Tender") : new(t.Key, 0m, amount, "Tender"));
        }

        if (total != 0)
        {
            lines.Add(tenderIsDebit ? new(counterAccount, 0m, total, "Settled") : new(counterAccount, total, 0m, "Settled"));
        }

        return total == 0 ? [] : lines;
    }

    private sealed record StockLine(long Id, string Code, int Pcs, decimal Gr, decimal Total, decimal Metal, decimal Making, decimal Stone, string Tag);

    /// <summary>
    /// Moves metal-stock weight/pcs/value for one voucher: purchases add at line
    /// value, sales issue at average cost (line cost_amount → COGS), returns come
    /// back at average cost. Tags flip sold / in_stock. Returns lines moved.
    /// </summary>
    public static async Task<int> ApplyStockAsync(DbConnection connection, long voucherId, List<string> notes, CancellationToken ct, IReadOnlySet<long>? onlyLineIds = null)
    {
        string type;
        long companyId;
        string date;
        string mode;
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = ErpDb.Positional("SELECT `voc_type`,`company_id`,DATE_FORMAT(`voc_date`,'%Y-%m-%d'),IFNULL(`gl_mode`,'post') FROM `epc_jewel_voucher` WHERE `id`=?");
            ErpDb.AddParameters(command, voucherId);
            await using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
            if (!await reader.ReadAsync(ct).ConfigureAwait(false)) return 0;
            type = reader.GetString(0);
            companyId = reader.GetInt64(1);
            date = reader.IsDBNull(2) ? "" : reader.GetString(2);
            mode = reader.GetString(3);
        }

        if (mode == "history") return 0;
        var direction = ErpJwVocTypes.MetalPurchaseAliases.Contains(type) || ErpJwVocTypes.DiamondPurchaseAliases.Contains(type) ? 1
            : ErpJwVocTypes.SalesReturnAliases.Contains(type) ? 2
            : ErpJwVocTypes.SalesAnalysisAliases.Contains(type) || ErpJwVocTypes.RetailAliases.Contains(type) || ErpJwVocTypes.MetalSaleAliases.Contains(type) ? -1
            : 0;
        if (direction == 0) return 0;

        var lines = new List<StockLine>();
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = ErpDb.Positional("SELECT `id`,`stock_code`,`pcs`,`gr_wt`,`total_amount`,`metal_amount`,`mkg_amount`,`stone_amount`,IFNULL(`tag_no`,'') FROM `epc_jewel_voucher_lines` WHERE `voucher_id`=? ORDER BY `line_no`,`id`");
            ErpDb.AddParameters(command, voucherId);
            await using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
            while (await reader.ReadAsync(ct).ConfigureAwait(false))
            {
                lines.Add(new(reader.GetInt64(0), reader.GetString(1), reader.GetInt32(2), reader.GetDecimal(3), reader.GetDecimal(4), reader.GetDecimal(5), reader.GetDecimal(6), reader.GetDecimal(7), reader.GetString(8)));
            }
        }

        var moved = 0;
        foreach (var line in lines)
        {
            if (onlyLineIds is not null && !onlyLineIds.Contains(line.Id)) continue;
            long stockId = 0;
            int pcs = 0;
            decimal gms = 0, value = 0, costGms = 0;
            await using (var command = connection.CreateCommand())
            {
                command.CommandText = ErpDb.Positional("SELECT `id`,`stock_pcs`,`stock_gms`,`stock_value`,`pur_cost_gms` FROM `epc_jewel_metal_stock` WHERE `company_id`=? AND `item_code`=? LIMIT 1");
                ErpDb.AddParameters(command, companyId, line.Code);
                await using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
                if (await reader.ReadAsync(ct).ConfigureAwait(false))
                {
                    stockId = reader.GetInt64(0);
                    pcs = reader.GetInt32(1);
                    gms = reader.GetDecimal(2);
                    value = reader.GetDecimal(3);
                    costGms = reader.GetDecimal(4);
                }
            }

            if (stockId == 0)
            {
                notes.Add("Stock code " + line.Code + " is not in the stock master — line kept, stock not moved.");
                continue;
            }

            decimal cost;
            if (direction == 1)
            {
                cost = line.Total != 0 ? line.Total : line.Metal + line.Making + line.Stone;
                pcs += line.Pcs;
                gms += line.Gr;
                value += cost;
            }
            else
            {
                cost = Math.Round(AverageCost(pcs, gms, value, costGms, line.Pcs, line.Gr), 2, MidpointRounding.AwayFromZero);
                if (direction == -1)
                {
                    if (value > 0 && cost > value) cost = value;
                    pcs -= line.Pcs;
                    gms -= line.Gr;
                    value = Math.Max(0m, value - cost);
                }
                else
                {
                    pcs += line.Pcs;
                    gms += line.Gr;
                    value += cost;
                }
            }

            var newCostGms = gms > 0 ? Math.Round(value / gms, 4) : costGms;
            await ErpDb.ExecuteAsync(connection, null, ErpDb.Positional(
                "UPDATE `epc_jewel_metal_stock` SET `stock_pcs`=?,`stock_qty`=?,`stock_gms`=?,`stock_value`=?,`pur_cost_gms`=? WHERE `id`=?"),
                ct, pcs, pcs, gms, value, newCostGms, stockId).ConfigureAwait(false);
            await ErpDb.ExecuteAsync(connection, null, ErpDb.Positional("UPDATE `epc_jewel_voucher_lines` SET `cost_amount`=? WHERE `id`=?"), ct, cost, line.Id).ConfigureAwait(false);
            if (line.Tag.Length > 0)
            {
                if (direction == -1)
                {
                    await ErpDb.ExecuteAsync(connection, null, ErpDb.Positional(
                        "UPDATE `epc_jw_tags` SET `status`='sold',`sold_invoice_id`=?,`sold_date`=? WHERE `company_id`=? AND `tag_no`=?"),
                        ct, voucherId, date, companyId, line.Tag).ConfigureAwait(false);
                }
                else
                {
                    await ErpDb.ExecuteAsync(connection, null, ErpDb.Positional(
                        "UPDATE `epc_jw_tags` SET `status`='in_stock',`sold_invoice_id`=0,`sold_date`=NULL WHERE `company_id`=? AND `tag_no`=?"),
                        ct, companyId, line.Tag).ConfigureAwait(false);
                }
            }

            moved++;
        }

        return moved;
    }

    /// <summary>Average cost for the issued quantity: by grams when weighed, else per piece, else standard cost/gram.</summary>
    public static decimal AverageCost(int stockPcs, decimal stockGms, decimal stockValue, decimal costPerGram, int pcs, decimal grams)
    {
        if (stockGms > 0 && grams > 0) return stockValue / stockGms * grams;
        if (stockPcs > 0 && pcs > 0) return stockValue / stockPcs * pcs;
        return costPerGram * (grams > 0 ? grams : pcs);
    }

    public static string Money(decimal v) => v.ToString("N2", CultureInfo.InvariantCulture);
}
