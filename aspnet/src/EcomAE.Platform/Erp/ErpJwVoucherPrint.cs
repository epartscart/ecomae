using System.Data.Common;
using System.Globalization;
using System.Net;
using System.Text;

namespace EcomAE.Platform.Erp;

/// <summary>
/// INDUS-style print previews for jewellery documents: TAX INVOICE (POS / metal
/// sale), TAX CREDIT NOTE (sales return), purchase voucher, POS receipt
/// (advance), journal voucher, petty cash, fixing and repair receipt. Layout
/// follows the INDUS Crystal reports (TRN header, copy label, karat-rate band,
/// line grid, VAT summary, amount in words, signature row).
/// </summary>
public static class ErpJwVoucherPrint
{
    public const string CompanyNameKey = "jewellery_print_company_name";
    public const string TrnKey = "jewellery_print_trn";

    private sealed record Line(int No, string Code, string Description, string Details, int Pcs, decimal Gr, decimal Net, decimal StoneWt, decimal Purity, decimal Pure,
        decimal MkgRate, decimal MkgAmount, decimal MetalAmount, decimal StoneAmount, decimal Total, decimal VatPct, decimal Vat, decimal TotalVat);

    public static async Task<string?> RenderAsync(DbConnection connection, string? kind, long id, string? copy, CancellationToken ct)
    {
        await ErpJewelSchema.EnsureAsync(connection, ct).ConfigureAwait(false);
        return (kind ?? "voucher").Trim().ToLowerInvariant() switch
        {
            "fixing" => await FixingAsync(connection, id, ct).ConfigureAwait(false),
            "petty" => await PettyAsync(connection, id, ct).ConfigureAwait(false),
            "repair" => await RepairAsync(connection, id, ct).ConfigureAwait(false),
            _ => await VoucherAsync(connection, id, copy, ct).ConfigureAwait(false),
        };
    }

    private static string H(string? s) => WebUtility.HtmlEncode(s ?? string.Empty);

    private static string M(decimal v) => v.ToString("N2", CultureInfo.InvariantCulture);

    private static string W(decimal v) => v == 0 ? "" : v.ToString("N2", CultureInfo.InvariantCulture);

    private static async Task<(string Name, string Trn)> CompanyAsync(DbConnection c, long companyId, CancellationToken ct)
    {
        async Task<string> Setting(string sql, params object?[] args)
        {
            try
            {
                return (await ErpDb.StringAsync(c, null, ErpDb.Positional(sql), ct, args).ConfigureAwait(false) ?? "").Trim();
            }
            catch (DbException)
            {
                return "";
            }
        }

        var name = await Setting("SELECT `setting_value` FROM `epc_org_company_settings` WHERE `company_id`=? AND `setting_key`=? LIMIT 1", companyId, CompanyNameKey).ConfigureAwait(false);
        var trn = await Setting("SELECT `setting_value` FROM `epc_org_company_settings` WHERE `company_id`=? AND `setting_key`=? LIMIT 1", companyId, TrnKey).ConfigureAwait(false);
        if (name.Length == 0) name = await Setting("SELECT `setting_value` FROM `epc_einvoice_settings` WHERE `setting_key`=? LIMIT 1", "seller_name").ConfigureAwait(false);
        if (trn.Length == 0) trn = await Setting("SELECT `setting_value` FROM `epc_einvoice_settings` WHERE `setting_key`=? LIMIT 1", "seller_trn").ConfigureAwait(false);
        return (name, trn);
    }

    private static string Page(string title, string body)
        => "<!doctype html><html><head><meta charset=\"utf-8\"><title>" + H(title) + "</title><style>"
           + "body{font-family:Arial,Helvetica,sans-serif;font-size:12px;color:#000;margin:0;background:#eee}"
           + ".sheet{width:190mm;min-height:270mm;margin:10px auto;background:#fff;padding:12mm 10mm;box-sizing:border-box}"
           + "h1{text-align:center;font-size:18px;margin:0 0 8px}.top{display:flex;justify-content:space-between;font-weight:bold}.copy{color:#888}"
           + ".boxes{display:flex;gap:8px;margin:6px 0}.box{flex:1;border:1px solid #000;border-radius:6px;padding:6px 8px}.box td{padding:2px 4px;border:0}"
           + ".rates{border:1px solid #000;text-align:center;font-weight:bold;padding:4px;margin:6px 0}"
           + "table.g{width:100%;border-collapse:collapse}table.g th{border:1px solid #000;background:#f2f2f2;font-size:11px;padding:4px}"
           + "table.g td{border-left:1px solid #000;border-right:1px solid #000;padding:3px 4px;vertical-align:top}table.g tr.tot td{border:1px solid #000;font-weight:bold;background:#f2f2f2}"
           + "table.g td.n{text-align:right}.sum{width:55%;margin-left:auto;border-collapse:collapse}.sum td{border:1px solid #000;padding:3px 6px}.sum td.n{text-align:right;font-weight:bold}"
           + ".words{font-style:italic;margin:4px 0}.sign{display:flex;justify-content:space-between;margin-top:50px;font-weight:bold}"
           + ".tools{text-align:center;margin:8px}@media print{.tools{display:none}body{background:#fff}.sheet{margin:0}}"
           + "</style></head><body><div class=\"tools\"><button onclick=\"window.print()\">Print</button></div><div class=\"sheet\">" + body + "</div></body></html>";

    private static async Task<string?> VoucherAsync(DbConnection c, long id, string? copy, CancellationToken ct)
    {
        var v = new Dictionary<string, object?>(StringComparer.Ordinal);
        await using (var command = c.CreateCommand())
        {
            command.CommandText = ErpDb.Positional("SELECT * FROM `epc_jewel_voucher` WHERE `id`=?");
            ErpDb.AddParameters(command, id);
            await using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
            if (!await reader.ReadAsync(ct).ConfigureAwait(false)) return null;
            for (var i = 0; i < reader.FieldCount; i++) v[reader.GetName(i)] = reader.IsDBNull(i) ? null : reader.GetValue(i);
        }

        string S(string k) => v.TryGetValue(k, out var o) && o is not null ? Convert.ToString(o, CultureInfo.InvariantCulture) ?? "" : "";
        decimal D(string k) => v.TryGetValue(k, out var o) && o is not null && o is not DateTime ? Convert.ToDecimal(o, CultureInfo.InvariantCulture) : 0m;

        var lines = new List<Line>();
        await using (var command = c.CreateCommand())
        {
            command.CommandText = ErpDb.Positional("SELECT `line_no`,`stock_code`,`description`,IFNULL(`line_details`,''),`pcs`,`gr_wt`,IFNULL(`net_wt`,0),IFNULL(`stone_wt`,0),`purity`,`pure_wt`,`mkg_rate`,`mkg_amount`,`metal_amount`,`stone_amount`,`total_amount`,IFNULL(`vat_pct`,0),IFNULL(`vat_amount`,0),`total_with_vat` FROM `epc_jewel_voucher_lines` WHERE `voucher_id`=? ORDER BY `line_no`,`id`");
            ErpDb.AddParameters(command, id);
            await using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
            while (await reader.ReadAsync(ct).ConfigureAwait(false))
            {
                lines.Add(new(reader.GetInt32(0), reader.GetString(1), reader.GetString(2), reader.GetString(3), reader.GetInt32(4), reader.GetDecimal(5), reader.GetDecimal(6), reader.GetDecimal(7), reader.GetDecimal(8), reader.GetDecimal(9),
                    reader.GetDecimal(10), reader.GetDecimal(11), reader.GetDecimal(12), reader.GetDecimal(13), reader.GetDecimal(14), reader.GetDecimal(15), reader.GetDecimal(16), reader.GetDecimal(17)));
            }
        }

        var receipts = new List<(string Mode, decimal Amount)>();
        await using (var command = c.CreateCommand())
        {
            command.CommandText = ErpDb.Positional("SELECT `receipt_mode`,`amount_lc` FROM `epc_jewel_voucher_receipts` WHERE `voucher_id`=? ORDER BY `id`");
            ErpDb.AddParameters(command, id);
            await using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
            while (await reader.ReadAsync(ct).ConfigureAwait(false)) receipts.Add((reader.GetString(0), reader.GetDecimal(1)));
        }

        var accounts = new List<(string Dr, string Cr, decimal Amount, string Note, string Description)>();
        await using (var command = c.CreateCommand())
        {
            command.CommandText = ErpDb.Positional("SELECT `debit_ac`,`credit_ac`,`amount`,`narration`,`description` FROM `epc_jewel_voucher_addins` WHERE `voucher_id`=? ORDER BY `line_no`,`id`");
            ErpDb.AddParameters(command, id);
            await using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
            while (await reader.ReadAsync(ct).ConfigureAwait(false)) accounts.Add((reader.GetString(0), reader.GetString(1), reader.GetDecimal(2), reader.GetString(3), reader.GetString(4)));
        }

        var (company, trn) = await CompanyAsync(c, (long)D("company_id"), ct).ConfigureAwait(false);
        var type = S("voc_type");
        var isSale = ErpJwVocTypes.SalesAnalysisAliases.Contains(type) || ErpJwVocTypes.RetailAliases.Contains(type) || ErpJwVocTypes.MetalSaleAliases.Contains(type);
        var isReturn = ErpJwVocTypes.SalesReturnAliases.Contains(type);
        var isPurchase = ErpJwVocTypes.MetalPurchaseAliases.Contains(type) || ErpJwVocTypes.DiamondPurchaseAliases.Contains(type);
        var isAdvance = ErpJwVocTypes.AdvanceAliases.Contains(type);
        var isJournal = ErpJwVocTypes.JournalAliases.Contains(type);
        var title = isSale ? "TAX INVOICE" : isReturn ? "TAX CREDIT NOTE" : isPurchase ? (ErpJwVocTypes.DiamondPurchaseAliases.Contains(type) ? "DIAMOND PURCHASE" : "METAL PURCHASE")
            : isAdvance ? "RECEIPT VOUCHER" : isJournal ? "JOURNAL VOUCHER" : "VOUCHER";
        var copyLabel = (copy ?? "").Trim().ToLowerInvariant() switch
        {
            "office" => "OFFICE COPY",
            "party" => "PARTY COPY",
            "customer" => "CUSTOMER COPY",
            _ => isReturn || isPurchase ? "PARTY COPY" : "CUSTOMER COPY",
        };
        var number = S("branch") + " - " + S("voc_no");
        var date = v.TryGetValue("voc_date", out var dv) && dv is DateTime dt ? dt.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture) : S("voc_date");
        var who = (S("customer_title") + " " + (S("customer_name").Length > 0 ? S("customer_name") : S("party_name"))).Trim();
        var mobile = S("mobile");

        var b = new StringBuilder();
        b.Append("<h1>").Append(H(title)).Append("</h1>");
        b.Append("<div class=\"top\"><span>").Append(trn.Length > 0 ? "TRN - " + H(trn) : "").Append("</span><span class=\"copy\">").Append(copyLabel).Append("</span></div>");
        b.Append("<div class=\"boxes\"><div class=\"box\"><table>");
        b.Append("<tr><td><b>").Append(isPurchase ? "Party" : "Customer Name").Append("</b></td><td>").Append(H(who)).Append("</td></tr>");
        if (isPurchase && S("party_code").Length > 0) b.Append("<tr><td><b>Party Code</b></td><td>").Append(H(S("party_code"))).Append(S("party_trn").Length > 0 ? " &nbsp; TRN " + H(S("party_trn")) : "").Append("</td></tr>");
        b.Append("<tr><td><b>Tel./ Mob. No.</b></td><td>").Append(mobile.Length > 0 ? "Mobile : " + H(mobile) : "").Append("</td></tr>");
        b.Append("</table></div><div class=\"box\"><table>");
        b.Append("<tr><td><b>").Append(isSale ? "Invoice No" : isReturn ? "PSR NO" : "Voc No").Append("</b></td><td>: ").Append(H(number)).Append("</td></tr>");
        b.Append("<tr><td><b>Date &amp; Time</b></td><td>: ").Append(H((date + " " + S("voc_time")).Trim())).Append("</td></tr>");
        b.Append("<tr><td><b>Salesman</b></td><td>: ").Append(H(S("salesman"))).Append("</td></tr>");
        if (isPurchase && S("supp_inv_no").Length > 0) b.Append("<tr><td><b>Supp. Inv No</b></td><td>: ").Append(H(S("supp_inv_no"))).Append("</td></tr>");
        b.Append("<tr><td><b>Page</b></td><td>: 1 of 1</td></tr></table></div></div>");
        if (S("karat_rates").Length > 0) b.Append("<div class=\"rates\">").Append(H(S("karat_rates"))).Append("</div>");
        if (isPurchase && D("metal_rate") > 0) b.Append("<div class=\"rates\">").Append(S("is_fixed") == "1" ? "FIXED" : "UNFIXED").Append(" METAL RATE : ").Append(D("metal_rate").ToString("N5", CultureInfo.InvariantCulture)).Append("</div>");

        string Desc(Line l)
        {
            var parts = new List<string> { H(l.Description) };
            parts.AddRange(l.Details.Split(['|', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Select(H));
            return string.Join("<br>", parts);
        }

        if (isJournal || (lines.Count == 0 && accounts.Count > 0))
        {
            b.Append("<table class=\"g\"><tr><th>No.</th><th>A/C Code</th><th>Description</th><th>Debit</th><th>Credit</th></tr>");
            var n = 0;
            foreach (var a in accounts)
            {
                if (a.Dr.Length > 0) b.Append("<tr><td>").Append(++n).Append("</td><td>").Append(H(a.Dr)).Append("</td><td>").Append(H(a.Description.Length > 0 ? a.Description : a.Note)).Append("</td><td class=\"n\">").Append(M(a.Amount)).Append("</td><td></td></tr>");
                if (a.Cr.Length > 0) b.Append("<tr><td>").Append(++n).Append("</td><td>").Append(H(a.Cr)).Append("</td><td>").Append(H(a.Description.Length > 0 ? a.Description : a.Note)).Append("</td><td></td><td class=\"n\">").Append(M(a.Amount)).Append("</td></tr>");
            }

            var dr = accounts.Where(a => a.Dr.Length > 0).Sum(a => a.Amount);
            var cr = accounts.Where(a => a.Cr.Length > 0).Sum(a => a.Amount);
            b.Append("<tr class=\"tot\"><td colspan=\"3\">Total</td><td class=\"n\">").Append(M(dr)).Append("</td><td class=\"n\">").Append(M(cr)).Append("</td></tr></table>");
        }
        else if (isPurchase)
        {
            b.Append("<table class=\"g\"><tr><th>No.</th><th>Stock Code</th><th>Description</th><th>PCS</th><th>Gr. Wt.</th><th>Stone Wt.</th><th>Purity</th><th>Pure Wt.</th><th>Mkg. Rate</th><th>Mkg. Amount</th><th>Metal Amount</th><th>St. Amount</th><th>Total</th></tr>");
            foreach (var l in lines)
            {
                b.Append("<tr><td>").Append(l.No).Append("</td><td>").Append(H(l.Code)).Append("</td><td>").Append(Desc(l)).Append("</td><td class=\"n\">").Append(l.Pcs)
                    .Append("</td><td class=\"n\">").Append(M(l.Gr)).Append("</td><td class=\"n\">").Append(M(l.StoneWt)).Append("</td><td class=\"n\">").Append(l.Purity.ToString("0.000000", CultureInfo.InvariantCulture))
                    .Append("</td><td class=\"n\">").Append(M(l.Pure)).Append("</td><td class=\"n\">").Append(M(l.MkgRate)).Append("</td><td class=\"n\">").Append(M(l.MkgAmount))
                    .Append("</td><td class=\"n\">").Append(M(l.MetalAmount)).Append("</td><td class=\"n\">").Append(M(l.StoneAmount)).Append("</td><td class=\"n\">").Append(M(l.Total)).Append("</td></tr>");
            }

            b.Append("<tr class=\"tot\"><td colspan=\"3\">Item Total (").Append(H(S("party_curr").Length > 0 ? S("party_curr") : "AED")).Append(") --&gt;</td><td class=\"n\">").Append(lines.Sum(l => l.Pcs))
                .Append("</td><td class=\"n\">").Append(M(lines.Sum(l => l.Gr))).Append("</td><td class=\"n\">").Append(M(lines.Sum(l => l.StoneWt))).Append("</td><td></td><td class=\"n\">").Append(M(lines.Sum(l => l.Pure)))
                .Append("</td><td></td><td class=\"n\">").Append(M(lines.Sum(l => l.MkgAmount))).Append("</td><td class=\"n\">").Append(M(lines.Sum(l => l.MetalAmount))).Append("</td><td class=\"n\">").Append(M(lines.Sum(l => l.StoneAmount)))
                .Append("</td><td class=\"n\">").Append(M(lines.Sum(l => l.Total))).Append("</td></tr></table>");
            b.Append("<table class=\"sum\" style=\"margin-top:6px\">");
            b.Append("<tr><td>Amount</td><td class=\"n\">").Append(M(D("net_amount"))).Append("</td></tr>");
            b.Append("<tr><td>Rnd Off Amt.</td><td class=\"n\">").Append(M(D("rnd_off_amount"))).Append("</td></tr>");
            b.Append("<tr><td>Other Amt.</td><td class=\"n\">").Append(M(D("other_amount"))).Append("</td></tr>");
            b.Append("<tr><td>TRN Amt.</td><td class=\"n\">").Append(M(D("vat_amount"))).Append("</td></tr>");
            b.Append("<tr><td>Total Amt+TRN</td><td class=\"n\">").Append(M(D("total_with_vat"))).Append("</td></tr></table>");
        }
        else if (isReturn)
        {
            b.Append("<table class=\"g\"><tr><th>No.</th><th>Stock Code</th><th>Description</th><th>Pcs</th><th>Gms</th><th>Vat %</th><th>VAT Amt</th><th>Amount</th><th>Amount incl. VAT</th></tr>");
            foreach (var l in lines)
            {
                b.Append("<tr><td>").Append(l.No).Append("</td><td>").Append(H(l.Code)).Append("</td><td>").Append(Desc(l)).Append("</td><td class=\"n\">").Append(l.Pcs)
                    .Append("</td><td class=\"n\">").Append(M(l.Gr)).Append("</td><td class=\"n\">").Append(l.VatPct.ToString("0.##", CultureInfo.InvariantCulture)).Append("</td><td class=\"n\">").Append(M(l.Vat))
                    .Append("</td><td class=\"n\">").Append(M(l.Total)).Append("</td><td class=\"n\">").Append(M(l.TotalVat)).Append("</td></tr>");
            }

            b.Append("<tr class=\"tot\"><td colspan=\"2\">(").Append(lines.Count).Append(" Items)</td><td>Total</td><td class=\"n\">").Append(lines.Sum(l => l.Pcs)).Append("</td><td class=\"n\">").Append(M(lines.Sum(l => l.Gr)))
                .Append("</td><td></td><td class=\"n\">").Append(M(lines.Sum(l => l.Vat))).Append("</td><td class=\"n\">").Append(M(lines.Sum(l => l.Total))).Append("</td><td class=\"n\">").Append(M(lines.Sum(l => l.TotalVat))).Append("</td></tr></table>");
            var pct = lines.Select(l => l.VatPct).FirstOrDefault(p => p > 0);
            b.Append("<table class=\"sum\" style=\"margin-top:6px\"><tr><td>Vat %</td><td class=\"n\">").Append(M(pct)).Append("</td></tr>");
            b.Append("<tr><td>VAT Amt</td><td class=\"n\">").Append(M(D("vat_amount"))).Append("</td></tr>");
            b.Append("<tr><td>Amount incl. VAT</td><td class=\"n\">").Append(M(D("total_with_vat"))).Append("</td></tr></table>");
            b.Append("<p>Old Gold Purchase : ").Append(W(D("old_gold_exchange"))).Append("<br>Sales Return : ").Append(M(D("adjust_sale_return")))
                .Append(" &nbsp; &nbsp; <b>AMOUNT INCL VAT : ").Append(M(D("total_with_vat"))).Append("</b></p>");
        }
        else
        {
            b.Append("<table class=\"g\"><tr><th>No.</th><th>Stock Code</th><th>Description</th><th>Pcs</th><th>Gr.Wt</th><th>Net.Wt</th><th>Amount (Excl. VAT)</th><th>VAT %</th><th>VAT Amount</th><th>Amount (Incl. VAT)</th></tr>");
            foreach (var l in lines)
            {
                b.Append("<tr><td>").Append(l.No).Append("</td><td>").Append(H(l.Code)).Append("</td><td>").Append(Desc(l)).Append("</td><td class=\"n\">").Append(l.Pcs)
                    .Append("</td><td class=\"n\">").Append(M(l.Gr)).Append("</td><td class=\"n\">").Append(W(l.Net)).Append("</td><td class=\"n\">").Append(M(l.Total))
                    .Append("</td><td class=\"n\">").Append(M(l.VatPct)).Append("</td><td class=\"n\">").Append(M(l.Vat)).Append("</td><td class=\"n\">").Append(M(l.TotalVat)).Append("</td></tr>");
            }

            if (lines.Count > 0)
            {
                b.Append("<tr class=\"tot\"><td colspan=\"2\">(").Append(lines.Count).Append(" Items)</td><td>Total</td><td class=\"n\">").Append(lines.Sum(l => l.Pcs)).Append("</td><td class=\"n\">").Append(M(lines.Sum(l => l.Gr)))
                    .Append("</td><td class=\"n\">").Append(W(lines.Sum(l => l.Net))).Append("</td><td class=\"n\">").Append(M(lines.Sum(l => l.Total))).Append("</td><td></td><td class=\"n\">").Append(M(lines.Sum(l => l.Vat)))
                    .Append("</td><td class=\"n\">").Append(M(lines.Sum(l => l.TotalVat))).Append("</td></tr>");
            }

            b.Append("</table>");
            var net = D("total_with_vat") - D("old_gold_exchange") - D("adjust_sale_return") - D("gold_scheme_redeem");
            b.Append("<table class=\"sum\" style=\"margin-top:6px\">");
            b.Append("<tr><td>Total Before VAT</td><td class=\"n\">").Append(M(D("net_amount"))).Append("</td></tr>");
            b.Append("<tr><td>Total VAT 5%</td><td class=\"n\">").Append(M(D("vat_amount"))).Append("</td></tr>");
            if (D("old_gold_exchange") != 0) b.Append("<tr><td>Scrap Gold</td><td class=\"n\">").Append(M(D("old_gold_exchange"))).Append("</td></tr>");
            if (D("gold_scheme_redeem") != 0) b.Append("<tr><td>Gold Scheme</td><td class=\"n\">").Append(M(D("gold_scheme_redeem"))).Append("</td></tr>");
            if (isSale) b.Append("<tr><td>Sales Return</td><td class=\"n\">").Append(M(D("adjust_sale_return"))).Append("</td></tr>");
            b.Append("<tr><td><b>Net Amount</b></td><td class=\"n\">").Append(M(net)).Append("</td></tr></table>");
        }

        var words = isPurchase ? D("total_with_vat") : receipts.Count > 0 ? receipts.Sum(r => r.Amount) : D("total_with_vat");
        if (receipts.Count > 0) b.Append("<p><b>Received : ").Append(H(string.Join(", ", receipts.Select(r => r.Mode + " : " + M(r.Amount))))).Append("</b></p>");
        b.Append("<div class=\"words\">").Append(H(AmountInWords(words))).Append("</div>");
        var remarks = S("remarks").Length > 0 ? S("remarks") : S("narration");
        b.Append("<p>Remarks : &nbsp; <i>").Append(H(remarks)).Append("</i></p>");
        if (isPurchase && S("narration").Length > 0 && S("remarks").Length > 0) b.Append("<p>Narration : ").Append(H(S("narration"))).Append("</p>");
        b.Append("<div class=\"top\"><span><i>Confirmed on behalf of</i><br>").Append(H(who)).Append("</span><span>").Append(H(company)).Append("</span></div>");
        b.Append("<div class=\"sign\"><span>SIGNATURE</span>").Append(isSale ? "<span>CHECKED BY</span>" : "").Append("<span>AUTHORISED SIGNATORY</span></div>");
        return Page(title + " " + number, b.ToString());
    }

    private static async Task<Dictionary<string, object?>?> RowAsync(DbConnection c, string table, long id, CancellationToken ct)
    {
        await using var command = c.CreateCommand();
        command.CommandText = ErpDb.Positional("SELECT * FROM `" + table + "` WHERE `id`=?");
        ErpDb.AddParameters(command, id);
        await using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
        if (!await reader.ReadAsync(ct).ConfigureAwait(false)) return null;
        var row = new Dictionary<string, object?>(StringComparer.Ordinal);
        for (var i = 0; i < reader.FieldCount; i++) row[reader.GetName(i)] = reader.IsDBNull(i) ? null : reader.GetValue(i);
        return row;
    }

    private static string F(Dictionary<string, object?> r, string k)
        => r.TryGetValue(k, out var o) && o is not null
            ? o is DateTime d ? d.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture) : o is decimal m ? M(m) : Convert.ToString(o, CultureInfo.InvariantCulture) ?? ""
            : "";

    private static async Task<string> Header(DbConnection c, Dictionary<string, object?> r, string title)
    {
        var (company, trn) = await CompanyAsync(c, Convert.ToInt64(r.GetValueOrDefault("company_id") ?? 0L, CultureInfo.InvariantCulture), CancellationToken.None).ConfigureAwait(false);
        return "<h1>" + H(title) + "</h1><div class=\"top\"><span>" + (trn.Length > 0 ? "TRN - " + H(trn) : "") + "</span><span>" + H(company) + "</span></div>";
    }

    private static string Grid(IEnumerable<(string Label, string Value)> rows)
        => "<div class=\"boxes\"><div class=\"box\"><table>" + string.Concat(rows.Select(x => "<tr><td><b>" + H(x.Label) + "</b></td><td>: " + H(x.Value) + "</td></tr>")) + "</table></div></div>";

    private static async Task<string?> FixingAsync(DbConnection c, long id, CancellationToken ct)
    {
        var r = await RowAsync(c, "epc_jewel_fixing", id, ct).ConfigureAwait(false);
        if (r is null) return null;
        var title = F(r, "fix_type").StartsWith('S') ? "METAL SALE FIXING" : "METAL PURCHASE FIXING";
        var amount = r.GetValueOrDefault("fix_amount") is decimal a ? a : 0m;
        var body = await Header(c, r, title).ConfigureAwait(false) + Grid(
        [
            ("Voc No", F(r, "branch") + " - " + F(r, "fix_no")), ("Voc Date", F(r, "fix_date")), ("Party", F(r, "party_code") + " " + F(r, "party_name")),
            ("Invoice Ref.", F(r, "party_inv_ref") + " " + F(r, "party_inv_date")), ("Salesman", F(r, "salesman")), ("Metal / Karat", F(r, "metal") + " " + F(r, "karat")),
            ("Metal Rate", F(r, "rate_type") + " " + F(r, "fix_rate")), ("Pure Wt.", F(r, "fix_qty_gms")), ("Oz. Wt.", F(r, "oz_wt")), ("Total (AED)", F(r, "fix_amount")),
            ("Narration", F(r, "remarks")),
        ]) + "<div class=\"words\">" + H(AmountInWords(amount)) + "</div><div class=\"sign\"><span>PREPARED BY</span><span>AUTHORISED SIGNATORY</span></div>";
        return Page(title, body);
    }

    private static async Task<string?> PettyAsync(DbConnection c, long id, CancellationToken ct)
    {
        var r = await RowAsync(c, "epc_jewel_petty_cash", id, ct).ConfigureAwait(false);
        if (r is null) return null;
        var amount = r.GetValueOrDefault("debit_amount") is decimal a ? a : 0m;
        var body = await Header(c, r, "PETTY CASH VOUCHER").ConfigureAwait(false) + Grid(
        [
            ("Voc No", F(r, "branch") + " - " + F(r, "voc_type") + " - " + F(r, "voc_no")), ("Voc Date", F(r, "voc_date")),
            ("A/C Code", F(r, "account_code") + " " + F(r, "account_name")), ("Description", F(r, "description")),
            ("Amount", F(r, "debit_amount")), ("Currency", F(r, "currency")), ("Approved by", F(r, "approved_by")), ("Remarks", F(r, "remarks")),
        ]) + "<div class=\"words\">" + H(AmountInWords(amount)) + "</div><div class=\"sign\"><span>RECEIVED BY</span><span>AUTHORISED SIGNATORY</span></div>";
        return Page("PETTY CASH VOUCHER", body);
    }

    private static async Task<string?> RepairAsync(DbConnection c, long id, CancellationToken ct)
    {
        var r = await RowAsync(c, "epc_jewel_repair", id, ct).ConfigureAwait(false);
        if (r is null) return null;
        var items = new StringBuilder("<table class=\"g\"><tr><th>No.</th><th>Bag No</th><th>Stock Code</th><th>Description</th><th>Pcs</th><th>Gr. Wt</th><th>Remarks</th></tr>");
        await using (var command = c.CreateCommand())
        {
            command.CommandText = ErpDb.Positional("SELECT `line_no`,`bag_no`,`stock_code`,`description`,`pcs`,`gr_wt`,`item_remarks` FROM `epc_jewel_repair_items` WHERE `repair_id`=? ORDER BY `line_no`,`id`");
            ErpDb.AddParameters(command, id);
            await using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
            while (await reader.ReadAsync(ct).ConfigureAwait(false))
            {
                items.Append("<tr><td>").Append(reader.GetInt32(0)).Append("</td><td>").Append(H(reader.GetString(1))).Append("</td><td>").Append(H(reader.GetString(2))).Append("</td><td>").Append(H(reader.GetString(3)))
                    .Append("</td><td class=\"n\">").Append(reader.GetInt32(4)).Append("</td><td class=\"n\">").Append(M(reader.GetDecimal(5))).Append("</td><td>").Append(H(reader.GetString(6))).Append("</td></tr>");
            }
        }

        items.Append("</table>");
        var body = await Header(c, r, "REPAIR RECEIPT").ConfigureAwait(false) + Grid(
        [
            ("Voc No", F(r, "branch") + " - " + F(r, "voc_no")), ("Voc Date", F(r, "voc_date")), ("Customer", F(r, "customer_name")), ("Mobile", F(r, "mobile")),
            ("Salesman", F(r, "salesman")), ("Delivery date", F(r, "delivery_date")), ("Status", F(r, "status")),
        ]) + items + "<p>Repair narration : <i>" + H(F(r, "repair_narration")) + "</i></p><div class=\"sign\"><span>CUSTOMER SIGNATURE</span><span>AUTHORISED SIGNATORY</span></div>";
        return Page("REPAIR RECEIPT", body);
    }

    private static readonly string[] Ones = ["", "One", "Two", "Three", "Four", "Five", "Six", "Seven", "Eight", "Nine", "Ten", "Eleven", "Twelve", "Thirteen", "Fourteen", "Fifteen", "Sixteen", "Seventeen", "Eighteen", "Nineteen"];
    private static readonly string[] Tens = ["", "", "Twenty", "Thirty", "Forty", "Fifty", "Sixty", "Seventy", "Eighty", "Ninety"];

    /// <summary>INDUS wording: "One Thousand One Hundred U.A.E. Dirhams Only" (+ "and N Fils").</summary>
    public static string AmountInWords(decimal amount)
    {
        amount = Math.Abs(Math.Round(amount, 2, MidpointRounding.AwayFromZero));
        var whole = (long)Math.Floor(amount);
        var fils = (int)Math.Round((amount - whole) * 100m);
        var text = whole == 0 ? "Zero" : Words(whole);
        text += " U.A.E. Dirhams";
        if (fils > 0) text += " and " + Words(fils) + " Fils";
        return text + " Only";
    }

    private static string Words(long n)
    {
        if (n < 20) return Ones[n];
        if (n < 100) return (Tens[n / 10] + " " + Ones[n % 10]).Trim();
        if (n < 1000) return (Ones[n / 100] + " Hundred " + Words(n % 100)).Trim();
        foreach (var (size, name) in new[] { (1_000_000_000L, "Billion"), (1_000_000L, "Million"), (1_000L, "Thousand") })
        {
            if (n >= size) return (Words(n / size) + " " + name + " " + Words(n % size)).Trim();
        }

        return n.ToString(CultureInfo.InvariantCulture);
    }
}
