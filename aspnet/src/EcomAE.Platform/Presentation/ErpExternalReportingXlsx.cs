using System.Globalization;
using System.IO.Compression;
using System.Net;
using System.Text;

namespace EcomAE.Platform.Presentation;

/// <summary>PHP <c>epc_ext_xlsx_write</c> / <c>epc_ext_finmodel_xlsx</c> / <c>epc_ext_audit_xlsx</c> twin.</summary>
public static class ErpExternalReportingXlsx
{
    public static byte[] FinModel(ErpExtFinDataset d)
    {
        var proj = ErpExternalReportingFin.Project(d);
        var a = proj.Assume;
        var years = proj.Years;
        var baseY = proj.Base;
        var cur = d.Cur;
        const decimal evEbitdaMult = 7.0m;
        const decimal peMult = 12.0m;
        var netDebt = decimal.Round((cur.BorrowCur + cur.BorrowNon + cur.Lease) - cur.Cash, 2);
        decimal N(decimal v) => decimal.Round(v, 6);

        var A = new List<IReadOnlyList<object?>>
        {
            new object?[] { "Financial Model & Valuation — Assumptions", "Value", "Notes" },
            new object?[] { "Driver", "Value", "Notes" },
            new object?[] { "Base-year revenue (FY" + baseY.Year + " actual)", Num(N(baseY.Rev)), "from the general ledger" },
            new object?[] { "Revenue growth (CAGR)", Num(N(a.Growth)), "applied to each forecast year" },
            new object?[] { "Gross margin", Num(N(a.GrossMargin)), "× revenue = gross profit" },
            new object?[] { "Operating expenses (% revenue)", Num(N(a.OpexPct)), "" },
            new object?[] { "Depreciation (% revenue)", Num(N(a.DeprPct)), "" },
            new object?[] { "Finance cost (% revenue)", Num(N(a.InterestPct)), "" },
            new object?[] { "Capex (% revenue)", Num(N(a.CapexPct)), "cash outflow in FCF" },
            new object?[] { "Incremental working capital (% of Δrevenue)", Num(N(a.WcPct)), "" },
            new object?[] { "Corporate tax rate", Num(N(a.TaxRate)), "on profit above threshold" },
            new object?[] { "Tax-free threshold", Num(N(a.TaxFree)), "UAE CT 0% band" },
            new object?[] { "WACC (discount rate)", Num(N(a.Wacc)), "DCF discounting" },
            new object?[] { "Terminal growth rate", Num(N(a.TerminalGrowth)), "Gordon growth" },
            new object?[] { "EV/EBITDA multiple (comparable)", Num(N(evEbitdaMult)), "market multiple" },
            new object?[] { "P/E multiple (comparable)", Num(N(peMult)), "market multiple" },
            new object?[] { "Net debt (borrowings + leases − cash)", Num(N(netDebt)), "EV → equity bridge" },
            new object?[] { "Total assets (actual)", Num(N(cur.TotalAssets)), "net-asset method" },
            new object?[] { "Total liabilities (actual)", Num(N(cur.TotalLiab)), "net-asset method" },
            new object?[] { "Base-year EBITDA (actual)", Num(N(baseY.Ebitda)), "EV/EBITDA method" },
            new object?[] { "Base-year net profit (actual)", Num(N(baseY.Profit)), "P/E method" },
        };

        object?[] MkRow(string label, Func<int, string, object?> cellFn)
        {
            var row = new object?[6];
            row[0] = label;
            var cols = new[] { "B", "C", "D", "E", "F" };
            for (var i = 0; i < 5; i++) row[i + 1] = cellFn(i, cols[i]);
            return row;
        }

        var hdr = new object?[] { "Line \\ Year", "FY" + years[0].Year, "FY" + years[1].Year, "FY" + years[2].Year, "FY" + years[3].Year, "FY" + years[4].Year };
        var rev = MkRow("Revenue", (i, col) =>
        {
            var f = i == 0 ? "Assumptions!$B$3*(1+Assumptions!$B$4)" : ((char)('B' + i - 1)) + "3*(1+Assumptions!$B$4)";
            return Fml(f, years[i].Rev);
        });
        var gross = MkRow("Gross profit", (i, col) => Fml(col + "3*Assumptions!$B$5", years[i].Gross));
        var opex = MkRow("Operating expenses", (i, col) => Fml(col + "3*Assumptions!$B$6", years[i].Opex));
        var ebitda = MkRow("EBITDA", (i, col) => Fml(col + "4-" + col + "5", years[i].Ebitda));
        var depr = MkRow("Depreciation & amortisation", (i, col) => Fml(col + "3*Assumptions!$B$7", years[i].Depr));
        var ebit = MkRow("Operating profit (EBIT)", (i, col) => Fml(col + "6-" + col + "7", years[i].Ebit));
        var fin = MkRow("Finance costs", (i, col) => Fml(col + "3*Assumptions!$B$8", years[i].Interest));
        var pbt = MkRow("Profit before tax", (i, col) => Fml(col + "8-" + col + "9", years[i].Pbt));
        var tax = MkRow("Income tax", (i, col) => Fml("MAX(0," + col + "10-Assumptions!$B$12)*Assumptions!$B$11", years[i].Tax));
        var profit = MkRow("Net profit", (i, col) => Fml(col + "10-" + col + "11", years[i].Profit));
        var nopat = MkRow("NOPAT (EBIT × (1−t))", (i, col) => Fml(col + "8*(1-Assumptions!$B$11)", years[i].Nopat));
        var deprAdd = MkRow("add: depreciation", (i, col) => Fml(col + "7", years[i].Depr));
        var capex = MkRow("less: capex", (i, col) => Fml(col + "3*Assumptions!$B$9", years[i].Capex));
        var dWc = MkRow("less: Δ working capital", (i, col) =>
        {
            var prev = i == 0 ? "Assumptions!$B$3" : ((char)('B' + i - 1)) + "3";
            return Fml("(" + col + "3-" + prev + ")*Assumptions!$B$10", years[i].DWc);
        });
        var fcf = MkRow("Free cash flow", (i, col) => Fml(col + "13+" + col + "14-" + col + "15-" + col + "16", years[i].Fcf));
        var dfRow = MkRow("Discount factor 1/(1+WACC)^n", (i, col) =>
        {
            var n = i + 1;
            return Fml("1/(1+Assumptions!$B$13)^" + n, 1m / (decimal)Math.Pow(1 + (double)a.Wacc, n));
        });
        var pvRow = MkRow("PV of free cash flow", (i, col) =>
        {
            var n = i + 1;
            return Fml(col + "17*" + col + "18", years[i].Fcf / (decimal)Math.Pow(1 + (double)a.Wacc, n));
        });

        var wacc = (double)a.Wacc;
        var g = (double)a.TerminalGrowth;
        var pvSum = 0m;
        foreach (var y in years) pvSum += y.Fcf / (decimal)Math.Pow(1 + wacc, y.N);
        var fcf5 = years[4].Fcf;
        var terminal = (fcf5 * (1 + a.TerminalGrowth)) / (a.Wacc - a.TerminalGrowth);
        var pvTerm = terminal / (decimal)Math.Pow(1 + wacc, 5);
        var evDcf = pvSum + pvTerm;
        var equityDcf = evDcf - netDebt;

        var C = new List<IReadOnlyList<object?>>
        {
            new object?[] { "Calculations — projected P&L, free cash flow & DCF (all forecast cells are live formulas → Assumptions)" },
            hdr, rev, gross, opex, ebitda, depr, ebit, fin, pbt, tax, profit,
            nopat, deprAdd, capex, dWc, fcf, dfRow, pvRow,
            new object?[] { "" },
            new object?[] { "Sum of PV of explicit FCF", Fml("SUM(B19:F19)", pvSum) },
            new object?[] { "Terminal value = FCF5×(1+g)/(WACC−g)", Fml("F17*(1+Assumptions!$B$14)/(Assumptions!$B$13-Assumptions!$B$14)", terminal) },
            new object?[] { "PV of terminal value", Fml("B22/(1+Assumptions!$B$13)^5", pvTerm) },
            new object?[] { "Enterprise value (DCF)", Fml("B21+B23", evDcf) },
            new object?[] { "less: net debt", Fml("Assumptions!$B$17", netDebt) },
            new object?[] { "Equity value (DCF)", Fml("B24-B25", equityDcf) },
        };

        var equityMult = baseY.Ebitda * evEbitdaMult - netDebt;
        var equityPe = baseY.Profit * peMult;
        var netAssets = cur.TotalAssets - cur.TotalLiab;
        var R = new List<IReadOnlyList<object?>>
        {
            new object?[] { "Results — model summary & valuation", "" },
            new object?[] { "Model summary", "" },
            new object?[] { "Base-year revenue", Fml("Assumptions!$B$3", baseY.Rev) },
            new object?[] { "Year-5 revenue", Fml("Calculations!F3", years[4].Rev) },
            new object?[] { "Year-5 EBITDA", Fml("Calculations!F6", years[4].Ebitda) },
            new object?[] { "Year-1 free cash flow", Fml("Calculations!B17", years[0].Fcf) },
            new object?[] { "Year-5 free cash flow", Fml("Calculations!F17", years[4].Fcf) },
            new object?[] { "", "" },
            new object?[] { "Valuation summary", "Equity value" },
            new object?[] { "Discounted cash flow (DCF)", Fml("Calculations!B26", equityDcf) },
            new object?[] { "Market multiples — EV/EBITDA", Fml("Assumptions!$B$20*Assumptions!$B$15-Assumptions!$B$17", equityMult) },
            new object?[] { "Market multiples — P/E", Fml("Assumptions!$B$21*Assumptions!$B$16", equityPe) },
            new object?[] { "Net assets / book value", Fml("Assumptions!$B$18-Assumptions!$B$19", netAssets) },
            new object?[] { "Indicative range — low", Fml("MIN(B10:B13)", Math.Min(Math.Min(equityDcf, equityMult), Math.Min(equityPe, netAssets))) },
            new object?[] { "Indicative range — high", Fml("MAX(B10:B13)", Math.Max(Math.Max(equityDcf, equityMult), Math.Max(equityPe, netAssets))) },
            new object?[] { "Central estimate (average)", Fml("AVERAGE(B10:B13)", (equityDcf + equityMult + equityPe + netAssets) / 4) },
        };

        return Write(new Dictionary<string, IReadOnlyList<IReadOnlyList<object?>>>
        {
            ["Assumptions"] = A,
            ["Calculations"] = C,
            ["Results"] = R,
        });
    }

    public static byte[] Audit(ErpExtFinDataset d, string ccy, string entity, string country)
    {
        var C = d.Cur;
        var P = d.Pri;
        var cyr = d.CurYear;
        var pyr = d.PriYear;
        var cyL = d.CurLabel;
        var pyL = d.PriLabel;
        var useIfrs18 = ErpExternalReportingCatalog.Ifrs18Applies(cyr);
        var eclC = decimal.Round(C.Receivables * 0.031m, 2);
        var eclP = decimal.Round(P.Receivables * 0.031m, 2);
        var grossRecC = decimal.Round(C.Receivables + eclC, 2);
        var grossRecP = decimal.Round(P.Receivables + eclP, 2);
        var priDiv = decimal.Round(P.Profit * 0.30m, 2);
        var openREC = P.Retained;
        var openREP = decimal.Round(P.Retained - P.Profit + priDiv, 2);
        var divC = d.Dividends;
        var ociC = decimal.Round(C.Reserves - P.Reserves, 2);
        var ociP = decimal.Round(P.Reserves * 0.10m, 2);
        var issue = decimal.Round(C.ShareCap - P.ShareCap, 2);
        string Tbc(int r) => "'Trial Balance'!C" + r;
        string Tbp(int r) => "'Trial Balance'!D" + r;

        var TB = new List<IReadOnlyList<object?>>
        {
            new object?[] { "Trial Balance — single source of truth (every figure links here · Dr +, Cr −)", "", "", "" },
            new object?[] { "Code", "Account", cyL, pyL },
            new object?[] { "A-PPE", "Property, plant & equipment", Num(C.Ppe), Num(P.Ppe) },
            new object?[] { "A-INT", "Intangible assets", Num(C.Intang), Num(P.Intang) },
            new object?[] { "A-INV", "Inventories", Num(C.Inventory), Num(P.Inventory) },
            new object?[] { "A-REC", "Trade receivables (gross)", Num(grossRecC), Num(grossRecP) },
            new object?[] { "A-ECL", "Less: ECL allowance", Num(-eclC), Num(-eclP) },
            new object?[] { "A-CASH", "Cash & cash equivalents", Num(C.Cash), Num(P.Cash) },
            new object?[] { "E-CAP", "Share capital", Num(-C.ShareCap), Num(-P.ShareCap) },
            new object?[] { "E-RES", "Other reserves", Num(-C.Reserves), Num(-P.Reserves) },
            new object?[] { "E-RE", "Retained earnings — opening", Num(-openREC), Num(-openREP) },
            new object?[] { "E-DIV", "Dividends paid", Num(divC), Num(priDiv) },
            new object?[] { "I-REV", "Revenue", Num(-C.Rev), Num(-P.Rev) },
            new object?[] { "X-COGS", "Cost of sales", Num(C.Cogs), Num(P.Cogs) },
            new object?[] { "X-OPEX", "Operating & administrative expenses", Num(C.Opex), Num(P.Opex) },
            new object?[] { "X-DEP", "Depreciation & amortisation", Num(C.Depr), Num(P.Depr) },
            new object?[] { "X-FIN", "Finance costs", Num(C.Interest), Num(P.Interest) },
            new object?[] { "X-TAX", "Income tax expense", Num(C.Tax), Num(P.Tax) },
            new object?[] { "L-PAY", "Trade & other payables", Num(-C.Payables), Num(-P.Payables) },
            new object?[] { "L-TAXP", "Current tax payable", Num(-C.Tax), Num(-P.Tax) },
            new object?[] { "L-BORN", "Borrowings — non-current", Num(-C.BorrowNon), Num(-P.BorrowNon) },
            new object?[] { "L-BORC", "Borrowings — current portion", Num(-C.BorrowCur), Num(-P.BorrowCur) },
            new object?[] { "L-LEASE", "Lease liabilities", Num(-C.Lease), Num(-P.Lease) },
            new object?[] { "L-EOS", "Employee end-of-service provision", Num(-C.Provisions), Num(-P.Provisions) },
            new object?[] { "", "Trial balance check (must equal 0)", Fml("SUM(C3:C24)", 0), Fml("SUM(D3:D24)", 0) },
        };

        var totNcaC = C.Ppe + C.Intang;
        var totNcaP = P.Ppe + P.Intang;
        var totCaC = C.Inventory + C.Receivables + C.Cash;
        var totCaP = P.Inventory + P.Receivables + P.Cash;
        var retEndC = decimal.Round(openREC + C.Profit - divC, 2);
        var retEndP = P.Retained;
        var totEqC = C.ShareCap + C.Reserves + retEndC;
        var totEqP = P.ShareCap + P.Reserves + retEndP;
        var totNclC = C.BorrowNon + C.Lease + C.Provisions;
        var totNclP = P.BorrowNon + P.Lease + P.Provisions;
        var totClC = C.Payables + C.Tax + C.BorrowCur;
        var totClP = P.Payables + P.Tax + P.BorrowCur;
        var SOFP = new List<IReadOnlyList<object?>>
        {
            new object?[] { "Statement of Financial Position — as at 31 Dec", cyL, pyL, "Note / source" },
            new object?[] { "Non-current assets", "", "", "" },
            new object?[] { "Property, plant & equipment", Fml(Tbc(3), C.Ppe), Fml(Tbp(3), P.Ppe), "TB A-PPE" },
            new object?[] { "Intangible assets", Fml(Tbc(4), C.Intang), Fml(Tbp(4), P.Intang), "TB A-INT" },
            new object?[] { "Total non-current assets", Fml("B3+B4", totNcaC), Fml("C3+C4", totNcaP), "" },
            new object?[] { "Current assets", "", "", "" },
            new object?[] { "Inventories", Fml(Tbc(5), C.Inventory), Fml(Tbp(5), P.Inventory), "TB A-INV" },
            new object?[] { "Trade & other receivables (net of ECL)", Fml(Tbc(6) + "+" + Tbc(7), C.Receivables), Fml(Tbp(6) + "+" + Tbp(7), P.Receivables), "TB A-REC + A-ECL" },
            new object?[] { "Cash & cash equivalents", Fml(Tbc(8), C.Cash), Fml(Tbp(8), P.Cash), "TB A-CASH" },
            new object?[] { "Total current assets", Fml("B7+B8+B9", totCaC), Fml("C7+C8+C9", totCaP), "" },
            new object?[] { "Total assets", Fml("B5+B10", C.TotalAssets), Fml("C5+C10", P.TotalAssets), "" },
            new object?[] { "Equity", "", "", "" },
            new object?[] { "Share capital", Fml("-" + Tbc(9), C.ShareCap), Fml("-" + Tbp(9), P.ShareCap), "TB E-CAP" },
            new object?[] { "Other reserves", Fml("-" + Tbc(10), C.Reserves), Fml("-" + Tbp(10), P.Reserves), "TB E-RES" },
            new object?[] { "Retained earnings", Fml("-" + Tbc(11) + "+'Profit & Loss OCI'!B11-" + Tbc(12), retEndC), Fml("-" + Tbp(11) + "+'Profit & Loss OCI'!C11-" + Tbp(12), retEndP), "opening + profit − dividends" },
            new object?[] { "Total equity", Fml("B13+B14+B15", totEqC), Fml("C13+C14+C15", totEqP), "" },
            new object?[] { "Non-current liabilities", "", "", "" },
            new object?[] { "Borrowings", Fml("-" + Tbc(21), C.BorrowNon), Fml("-" + Tbp(21), P.BorrowNon), "TB L-BORN" },
            new object?[] { "Lease liabilities", Fml("-" + Tbc(23), C.Lease), Fml("-" + Tbp(23), P.Lease), "TB L-LEASE" },
            new object?[] { "Employee end-of-service provision", Fml("-" + Tbc(24), C.Provisions), Fml("-" + Tbp(24), P.Provisions), "TB L-EOS" },
            new object?[] { "Total non-current liabilities", Fml("B18+B19+B20", totNclC), Fml("C18+C19+C20", totNclP), "" },
            new object?[] { "Current liabilities", "", "", "" },
            new object?[] { "Trade & other payables", Fml("-" + Tbc(19), C.Payables), Fml("-" + Tbp(19), P.Payables), "TB L-PAY" },
            new object?[] { "Current tax payable", Fml("-" + Tbc(20), C.Tax), Fml("-" + Tbp(20), P.Tax), "TB L-TAXP" },
            new object?[] { "Current portion of borrowings", Fml("-" + Tbc(22), C.BorrowCur), Fml("-" + Tbp(22), P.BorrowCur), "TB L-BORC" },
            new object?[] { "Total current liabilities", Fml("B23+B24+B25", totClC), Fml("C23+C24+C25", totClP), "" },
            new object?[] { "Total equity & liabilities", Fml("B16+B21+B26", totEqC + totNclC + totClC), Fml("C16+C21+C26", totEqP + totNclP + totClP), "" },
            new object?[] { "Balance check (assets − equity & liabilities)", Fml("B11-B27", 0), Fml("C11-C27", 0), "must be 0" },
        };

        var grossC = decimal.Round(C.Rev - C.Cogs, 2);
        var grossP = decimal.Round(P.Rev - P.Cogs, 2);
        var ebitC = decimal.Round(grossC - C.Opex - C.Depr, 2);
        var ebitP = decimal.Round(grossP - P.Opex - P.Depr, 2);
        var plTitle = useIfrs18
            ? "Statement of Profit or Loss (IFRS 18 — five categories; early applied)"
            : "Statement of Profit or Loss & Other Comprehensive Income";
        var PL = new List<IReadOnlyList<object?>>
        {
            new object?[] { plTitle, cyL, pyL, "Note / source" },
            new object?[] { "Revenue", Fml("-" + Tbc(13), C.Rev), Fml("-" + Tbp(13), P.Rev), useIfrs18 ? "Operating · TB I-REV" : "TB I-REV" },
            new object?[] { "Cost of sales", Fml("-" + Tbc(14), -C.Cogs), Fml("-" + Tbp(14), -P.Cogs), useIfrs18 ? "Operating · TB X-COGS" : "TB X-COGS" },
            new object?[] { "Gross profit", Fml("B2+B3", grossC), Fml("C2+C3", grossP), useIfrs18 ? "IFRS 18 additional subtotal" : "" },
            new object?[] { useIfrs18 ? "Operating expenses" : "Operating & administrative expenses", Fml("-" + Tbc(15), -C.Opex), Fml("-" + Tbp(15), -P.Opex), useIfrs18 ? "Operating · TB X-OPEX" : "TB X-OPEX" },
            new object?[] { "Depreciation & amortisation", Fml("-" + Tbc(16), -C.Depr), Fml("-" + Tbp(16), -P.Depr), useIfrs18 ? "Operating · TB X-DEP" : "TB X-DEP" },
            new object?[] { useIfrs18 ? "Operating profit or loss" : "Operating profit (EBIT)", Fml("B4+B5+B6", ebitC), Fml("C4+C5+C6", ebitP), useIfrs18 ? "IFRS 18 mandatory subtotal" : "" },
            new object?[] { useIfrs18 ? "Financing expenses (interest)" : "Finance costs", Fml("-" + Tbc(17), -C.Interest), Fml("-" + Tbp(17), -P.Interest), useIfrs18 ? "Financing · TB X-FIN" : "TB X-FIN" },
            new object?[] { useIfrs18 ? "Profit or loss before income taxes" : "Profit before tax", Fml("B7+B8", C.Pbt), Fml("C7+C8", P.Pbt), useIfrs18 ? "IFRS 18 additional subtotal" : "" },
            new object?[] { "Income tax expense", Fml("-" + Tbc(18), -C.Tax), Fml("-" + Tbp(18), -P.Tax), useIfrs18 ? "Income taxes · TB X-TAX" : "TB X-TAX" },
            new object?[] { useIfrs18 ? "Profit or loss" : "Profit for the year", Fml("B9+B10", C.Profit), Fml("C9+C10", P.Profit), useIfrs18 ? "IFRS 18 mandatory subtotal" : "row 11" },
            new object?[] { "Other comprehensive income — revaluation/FV", Fml("-" + Tbc(10) + "+" + Tbp(10), ociC), Num(ociP), "Δ reserves" },
            new object?[] { "Total comprehensive income", Fml("B11+B12", decimal.Round(C.Profit + ociC, 2)), Fml("C11+C12", decimal.Round(P.Profit + ociP, 2)), "" },
        };
        if (useIfrs18)
        {
            PL.Add(new object?[] { "IFRS 18 note: investing category nil in this pack; profit before financing and income taxes equals operating profit or loss.", "", "", "IFRS 18.69–71" });
        }

        var capex = decimal.Round((C.Ppe - P.Ppe) + C.Depr, 2);
        var dIntang = decimal.Round(C.Intang - P.Intang, 2);
        var dRecNet = decimal.Round(C.Receivables - P.Receivables, 2);
        var dInv = decimal.Round(C.Inventory - P.Inventory, 2);
        var dPay = decimal.Round(C.Payables - P.Payables, 2);
        var dProv = decimal.Round(C.Provisions - P.Provisions, 2);
        var taxPaid = decimal.Round(C.Tax - (C.Tax - P.Tax), 2);
        var cfOp = decimal.Round(C.Pbt + C.Depr - dRecNet - dInv + dPay + dProv - taxPaid, 2);
        var dBorrow = decimal.Round((C.BorrowCur + C.BorrowNon) - (P.BorrowCur + P.BorrowNon), 2);
        var dLease = decimal.Round(C.Lease - P.Lease, 2);
        var dReserves = decimal.Round(C.Reserves - P.Reserves, 2);
        var cfInv = decimal.Round(-capex - dIntang, 2);
        var cfFin = decimal.Round(dBorrow + issue + dLease + dReserves - divC, 2);
        var cfNet = decimal.Round(cfOp + cfInv + cfFin, 2);
        var CF = new List<IReadOnlyList<object?>>
        {
            new object?[] { "Statement of Cash Flows — year ended 31 Dec (indirect, IAS 7)", cyL, "Source" },
            new object?[] { "Operating activities", "", "" },
            new object?[] { "Profit before tax", Fml("'Profit & Loss OCI'!B9", C.Pbt), "P&L" },
            new object?[] { "Add: depreciation & amortisation", Fml(Tbc(16), C.Depr), "TB X-DEP" },
            new object?[] { "(Increase) / decrease in receivables", Fml("-((" + Tbc(6) + "+" + Tbc(7) + ")-(" + Tbp(6) + "+" + Tbp(7) + "))", -dRecNet), "Δ TB A-REC" },
            new object?[] { "(Increase) / decrease in inventories", Fml("-(" + Tbc(5) + "-" + Tbp(5) + ")", -dInv), "Δ TB A-INV" },
            new object?[] { "Increase / (decrease) in payables", Fml("-" + Tbc(19) + "+" + Tbp(19), dPay), "Δ TB L-PAY" },
            new object?[] { "Increase in provisions", Fml("-" + Tbc(24) + "+" + Tbp(24), dProv), "Δ TB L-EOS" },
            new object?[] { "Income tax paid", Fml("-(" + Tbc(18) + "+" + Tbc(20) + "-" + Tbp(20) + ")", -taxPaid), "TB X-TAX/L-TAXP" },
            new object?[] { "Net cash from operating activities", Fml("SUM(B3:B9)", cfOp), "" },
            new object?[] { "Investing activities", "", "" },
            new object?[] { "Purchase of property, plant & equipment", Fml("-((" + Tbc(3) + "-" + Tbp(3) + ")+" + Tbc(16) + ")", -capex), "Δ TB A-PPE + dep" },
            new object?[] { "Purchase of intangibles", Fml("-(" + Tbc(4) + "-" + Tbp(4) + ")", -dIntang), "Δ TB A-INT" },
            new object?[] { "Net cash used in investing activities", Fml("B12+B13", cfInv), "" },
            new object?[] { "Financing activities", "", "" },
            new object?[] { "Net movement in borrowings", Fml("-" + Tbc(21) + "-" + Tbc(22) + "+" + Tbp(21) + "+" + Tbp(22), dBorrow), "Δ TB L-BOR" },
            new object?[] { "Proceeds from share issue", Fml("-" + Tbc(9) + "+" + Tbp(9), issue), "Δ TB E-CAP" },
            new object?[] { "Movement in lease liabilities", Fml("-" + Tbc(23) + "+" + Tbp(23), dLease), "Δ TB L-LEASE" },
            new object?[] { "Movement in reserves", Fml("-" + Tbc(10) + "+" + Tbp(10), dReserves), "Δ TB E-RES" },
            new object?[] { "Dividends paid", Fml("-" + Tbc(12), -divC), "TB E-DIV" },
            new object?[] { "Net cash from financing activities", Fml("SUM(B16:B20)", cfFin), "" },
            new object?[] { "Net increase / (decrease) in cash", Fml("B10+B14+B21", cfNet), "" },
            new object?[] { "Cash & cash equivalents at 1 Jan", Fml(Tbp(8), P.Cash), "TB A-CASH (prior)" },
            new object?[] { "Cash & cash equivalents at 31 Dec", Fml("B22+B23", C.Cash), "" },
            new object?[] { "Reconciliation check (vs TB cash)", Fml("B24-" + Tbc(8), 0), "must be 0" },
        };

        var SOCE = new List<IReadOnlyList<object?>>
        {
            new object?[] { "Statement of Changes in Equity", "Share capital", "Other reserves", "Retained earnings", "Total" },
            new object?[] { "Balance at 1 Jan " + cyr, Fml("-" + Tbc(9), P.ShareCap), Fml("-" + Tbp(10), P.Reserves), Fml("-" + Tbc(11), openREC), Fml("B2+C2+D2", P.ShareCap + P.Reserves + openREC) },
            new object?[] { "Profit for the year", Num(0), Num(0), Fml("'Profit & Loss OCI'!B11", C.Profit), Fml("B3+C3+D3", C.Profit) },
            new object?[] { "Other comprehensive income", Num(0), Fml("'Profit & Loss OCI'!B12", ociC), Num(0), Fml("B4+C4+D4", ociC) },
            new object?[] { "Dividends declared", Num(0), Num(0), Fml("-" + Tbc(12), -divC), Fml("B5+C5+D5", -divC) },
            new object?[] { "Shares issued", Fml("-" + Tbc(9) + "+" + Tbp(9), issue), Num(0), Num(0), Fml("B6+C6+D6", issue) },
            new object?[] { "Balance at 31 Dec " + cyr, Fml("B2+B6", C.ShareCap), Fml("C2+C4", C.Reserves), Fml("D2+D3+D5", retEndC), Fml("B7+C7+D7", totEqC) },
        };

        var presStd = useIfrs18 ? "IFRS 18" : "IAS 1";
        var name = string.IsNullOrWhiteSpace(entity) ? "Company" : entity.Trim();
        var cc = ErpExternalReportingCatalog.NormalizeCountry(country);
        var isUae = cc is "AE" or "";
        var auditor = isUae ? "Gulf Audit & Assurance (Chartered Accountants)" : "Independent Registered Auditors";
        var fwk = useIfrs18
            ? "International Financial Reporting Standards (IFRS) as issued by the IASB, including early application of IFRS 18 Presentation and Disclosure in Financial Statements"
            : "International Financial Reporting Standards (IFRS) as issued by the IASB";
        var COVER = new List<IReadOnlyList<object?>>
        {
            new object?[] { name },
            new object?[] { "External Audit Report — linked Excel pack" },
            new object?[] { "Reporting period", cyL + " (comparative " + pyL + ")" },
            new object?[] { "Presentation currency", ccy },
            new object?[] { "Framework", fwk },
            new object?[] { "Auditor", auditor },
            new object?[] { "" },
            new object?[] { "Table of contents" },
            new object?[] { "1", "Independent auditor's report (ISA 700)" },
            new object?[] { "2", "Trial Balance — single source of truth" },
            new object?[] { "3", "Statement of Financial Position" },
            new object?[] { "4", "Statement of Profit or Loss & OCI" },
            new object?[] { "5", "Statement of Cash Flows" },
            new object?[] { "6", "Statement of Changes in Equity" },
            new object?[] { "7", "Notes — figures link back to the Trial Balance" },
        };
        var AUD = new List<IReadOnlyList<object?>>
        {
            new object?[] { "Independent Auditor's Report (ISA 700 unmodified)" },
            new object?[] { "To the shareholders of " + name },
            new object?[] { "Opinion", "We have audited the financial statements of " + name + " for " + cyL + ". In our opinion the statements present fairly, in all material respects, the financial position and performance in accordance with " + fwk + "." },
            new object?[] { "Basis for opinion", "We conducted our audit in accordance with International Standards on Auditing. We are independent and have fulfilled our other ethical responsibilities." },
            new object?[] { "Key audit matters", "Revenue recognition (IFRS 15) and expected credit losses (IFRS 9) were the matters of most significance." },
            new object?[] { "Responsibilities", "Management is responsible for the financial statements. Our responsibility is to express an opinion based on our audit." },
            new object?[] { "Signed", auditor + " · " + d.To.AddMonths(3).ToString("d MMMM yyyy", CultureInfo.InvariantCulture) },
        };
        var rvGoodsC = decimal.Round(C.Rev * 0.72m, 2);
        var rvServC = decimal.Round(C.Rev - rvGoodsC, 2);
        var rvGoodsP = decimal.Round(P.Rev * 0.72m, 2);
        var rvServP = decimal.Round(P.Rev - rvGoodsP, 2);
        var NOTES = new List<IReadOnlyList<object?>>
        {
            new object?[] { "Notes to the financial statements — figures + FY comparatives; totals link to the statements / Trial Balance", cyL, pyL, "Ties to / standard" },
            new object?[] { "", "", "", "" },
            new object?[] { "Note 1 — Reporting entity & basis of preparation (" + presStd + " / IAS 8)", "", "", "" },
            new object?[] { "Reporting entity", name + " — financial statements in " + ccy + ", year ended " + d.To.ToString("d MMMM yyyy", CultureInfo.InvariantCulture) + " with " + pyL + " comparatives.", "", "" },
            new object?[] { "Basis of preparation", useIfrs18
                ? "Prepared under IFRS as issued by the IASB, including early application of IFRS 18 (replaces IAS 1 for presentation & disclosure)."
                : "Prepared under IFRS as issued by the IASB on the historical-cost basis, going-concern basis.", "", "" },
            new object?[] { "", "", "", "" },
            new object?[] { "Note 2 — Revenue (IFRS 15)", "", "", "" },
            new object?[] { "Sale of goods (point in time)", Num(rvGoodsC), Num(rvGoodsP), "by category" },
            new object?[] { "Rendering of services (over time)", Num(rvServC), Num(rvServP), "by category" },
            new object?[] { "Total revenue (by category)", Fml("B8+B9", C.Rev), Fml("C8+C9", P.Rev), "='Profit & Loss OCI'!B2" },
            new object?[] { "", "", "", "" },
            new object?[] { "Note 5 — Property, plant & equipment (IAS 16)", "", "", "" },
            new object?[] { "Closing net book value", Fml("'Financial Position'!B3", C.Ppe), Fml(Tbp(3), P.Ppe), "='Financial Position'!B3" },
            new object?[] { "", "", "", "" },
            new object?[] { "Note 8 — Trade & other receivables and ECL (IFRS 9 / IFRS 7)", "", "", "" },
            new object?[] { "Gross trade receivables", Fml(Tbc(6), grossRecC), Fml(Tbp(6), grossRecP), "TB A-REC" },
            new object?[] { "Less: ECL allowance", Fml(Tbc(7), -eclC), Fml(Tbp(7), -eclP), "TB A-ECL" },
            new object?[] { "Net trade & other receivables", Fml("B16+B17", C.Receivables), Fml("C16+C17", P.Receivables), "='Financial Position'!B8" },
            new object?[] { "", "", "", "" },
            new object?[] { "Note 14 — Income tax (IAS 12 / UAE FDL 47/2022)", "", "", "" },
            new object?[] { "Accounting profit before tax", Fml("'Profit & Loss OCI'!B9", C.Pbt), Fml("'Profit & Loss OCI'!C9", P.Pbt), "P&L" },
            new object?[] { "Current tax expense", Fml(Tbc(18), C.Tax), Fml(Tbp(18), P.Tax), "='Profit & Loss OCI'!B10 (×−1)" },
            new object?[] { "0% band then 9% on taxable income above AED 375,000.", "", "", "FDL 47/2022" },
        };

        return Write(new Dictionary<string, IReadOnlyList<IReadOnlyList<object?>>>
        {
            ["Cover & Contents"] = COVER,
            ["Auditor's Report"] = AUD,
            ["Trial Balance"] = TB,
            ["Financial Position"] = SOFP,
            ["Profit & Loss OCI"] = PL,
            ["Cash Flows"] = CF,
            ["Changes in Equity"] = SOCE,
            ["Notes"] = NOTES,
        });
    }

    public static byte[] Write(IReadOnlyDictionary<string, IReadOnlyList<IReadOnlyList<object?>>> sheets)
    {
        using var ms = new MemoryStream();
        using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
        {
            var names = sheets.Keys.ToList();
            if (names.Count == 0)
            {
                names.Add("Sheet1");
                sheets = new Dictionary<string, IReadOnlyList<IReadOnlyList<object?>>> { ["Sheet1"] = [] };
            }

            var sheetOverrides = new StringBuilder();
            var wbSheets = new StringBuilder();
            var wbRels = new StringBuilder();
            var i = 0;
            foreach (var name in names)
            {
                i++;
                var rowsXml = new StringBuilder();
                var rn = 0;
                foreach (var row in sheets[name])
                {
                    rn++;
                    var cellsXml = new StringBuilder();
                    var ci = 0;
                    foreach (var cell in row)
                    {
                        var r = ColLetter(ci) + rn.ToString(CultureInfo.InvariantCulture);
                        cellsXml.Append(CellXml(r, cell));
                        ci++;
                    }

                    rowsXml.Append("<row r=\"").Append(rn).Append("\">").Append(cellsXml).Append("</row>");
                }

                var sheetXml = "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>"
                    + "<worksheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\">"
                    + "<sheetData>" + rowsXml + "</sheetData></worksheet>";
                Add(zip, "xl/worksheets/sheet" + i + ".xml", sheetXml);
                sheetOverrides.Append("<Override PartName=\"/xl/worksheets/sheet").Append(i)
                    .Append(".xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml\"/>");
                var safe = new string((name ?? "").Select(ch => ":\\/?*[]".Contains(ch) ? ' ' : ch).ToArray()).Trim();
                if (safe.Length > 31) safe = safe[..31];
                if (safe.Length == 0) safe = "Sheet" + i;
                wbSheets.Append("<sheet name=\"").Append(Esc(safe)).Append("\" sheetId=\"").Append(i)
                    .Append("\" r:id=\"rId").Append(i).Append("\"/>");
                wbRels.Append("<Relationship Id=\"rId").Append(i)
                    .Append("\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet\" Target=\"worksheets/sheet")
                    .Append(i).Append(".xml\"/>");
            }

            Add(zip, "[Content_Types].xml",
                "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>"
                + "<Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\">"
                + "<Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/>"
                + "<Default Extension=\"xml\" ContentType=\"application/xml\"/>"
                + "<Override PartName=\"/xl/workbook.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml\"/>"
                + sheetOverrides + "</Types>");
            Add(zip, "_rels/.rels",
                "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>"
                + "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">"
                + "<Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument\" Target=\"xl/workbook.xml\"/>"
                + "</Relationships>");
            Add(zip, "xl/workbook.xml",
                "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>"
                + "<workbook xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\" "
                + "xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\">"
                + "<sheets>" + wbSheets + "</sheets></workbook>");
            Add(zip, "xl/_rels/workbook.xml.rels",
                "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>"
                + "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">"
                + wbRels + "</Relationships>");
        }

        return ms.ToArray();
    }

    private static Dictionary<string, object?> Num(decimal v) => new() { ["t"] = "n", ["v"] = v };

    private static Dictionary<string, object?> Fml(string f, decimal v) => new() { ["f"] = f, ["v"] = v };

    private static string CellXml(string r, object? cell)
    {
        if (cell is Dictionary<string, object?> typed)
        {
            if (typed.TryGetValue("f", out var f) && f is string fs)
            {
                var cv = typed.TryGetValue("v", out var vv)
                    ? "<v>" + Esc(Convert.ToString(vv, CultureInfo.InvariantCulture) ?? "") + "</v>"
                    : "";
                return "<c r=\"" + r + "\"><f>" + Esc(fs.TrimStart('=')) + "</f>" + cv + "</c>";
            }

            if (typed.TryGetValue("t", out var t) && t is string ts && ts == "n")
            {
                return "<c r=\"" + r + "\" t=\"n\"><v>" + Esc(Convert.ToString(typed.GetValueOrDefault("v") ?? 0, CultureInfo.InvariantCulture) ?? "0") + "</v></c>";
            }

            return "<c r=\"" + r + "\" t=\"inlineStr\"><is><t xml:space=\"preserve\">"
                + Esc(Convert.ToString(typed.GetValueOrDefault("v") ?? "", CultureInfo.InvariantCulture) ?? "") + "</t></is></c>";
        }

        return "<c r=\"" + r + "\" t=\"inlineStr\"><is><t xml:space=\"preserve\">" + Esc(cell?.ToString() ?? "") + "</t></is></c>";
    }

    private static void Add(ZipArchive zip, string path, string xml)
    {
        var e = zip.CreateEntry(path, CompressionLevel.Fastest);
        using var s = e.Open();
        using var w = new StreamWriter(s, new UTF8Encoding(false));
        w.Write(xml);
    }

    private static string Esc(string v) => WebUtility.HtmlEncode(v ?? "");

    public static string ColLetter(int idx)
    {
        var s = "";
        idx++;
        while (idx > 0)
        {
            var rem = (idx - 1) % 26;
            s = (char)('A' + rem) + s;
            idx = (idx - rem) / 26;
        }

        return s;
    }
}
