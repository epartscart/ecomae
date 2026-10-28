namespace EcomAE.Platform.Erp;

public enum ErpJwIndusColType
{
    Text,
    Decimal,
    Int,
    Date,
    Flag,
}

/// <summary>One CSV column: header (= target column unless <see cref="Column"/> is empty → loader-only), INDUS label and the ERP area it feeds.</summary>
public sealed record ErpJwIndusColumn(
    string Header,
    ErpJwIndusColType Type,
    bool Required,
    int MaxLength,
    string IndusField,
    string Links,
    string Column);

/// <summary>One INDUS export batch → one ecomae table, in load order.</summary>
public sealed record ErpJwIndusTemplate(
    string Key,
    int Step,
    string Title,
    string Table,
    string IndusScreen,
    string Module,
    string Workflow,
    string? ParentKey,
    string? ParentColumn,
    bool HasCompany,
    bool Upsert,
    string? CreatedByColumn,
    IReadOnlyList<ErpJwIndusColumn> Columns)
{
    public IEnumerable<string> Headers => new[] { "source_ref" }
        .Concat(ParentKey is null ? Array.Empty<string>() : new[] { "parent_ref" })
        .Concat(Columns.Select(c => c.Header));
}

/// <summary>
/// Single source of truth for the INDUS → ecomae jewellery CSV migration: every
/// template, its columns, the INDUS field each maps from and the module / GL
/// account it feeds. The loader, the downloadable templates, the field map and
/// the sample data are all driven from this list.
/// </summary>
public static class ErpJwIndusImportSpec
{
    public const string SourceRefHeader = "source_ref";
    public const string ParentRefHeader = "parent_ref";

    /// <summary>INDUS voucher type → ecomae voc_type (INDUS code kept in source_voc_type).</summary>
    public static readonly IReadOnlyDictionary<string, string> VocTypeMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["POS"] = ErpJwVocTypes.RetailInvoice,
        ["RIN"] = ErpJwVocTypes.RetailInvoice,
        ["MSL"] = ErpJwVocTypes.MetalSale,
        ["SAL"] = ErpJwVocTypes.MetalSale,
        ["DSL"] = ErpJwVocTypes.MetalSale,
        ["PSR"] = ErpJwVocTypes.SalesReturn,
        ["SRT"] = ErpJwVocTypes.SalesReturn,
        ["SRN"] = ErpJwVocTypes.SalesReturn,
        ["PUR"] = ErpJwVocTypes.MetalPurchase,
        ["RMP"] = ErpJwVocTypes.MetalPurchase,
        ["MMP"] = ErpJwVocTypes.MetalPurchase,
        ["DPU"] = ErpJwVocTypes.DiamondPurchase,
        ["RDP"] = ErpJwVocTypes.DiamondPurchase,
        ["PCR"] = ErpJwVocTypes.PosAdvance,
        ["ADV"] = ErpJwVocTypes.PosAdvance,
        ["GJV"] = ErpJwVocTypes.Journal,
        ["JVL"] = ErpJwVocTypes.Journal,
        ["MFT"] = ErpJwVocTypes.Manufacture,
        ["MFG"] = ErpJwVocTypes.Manufacture,
        ["BTR"] = ErpJwVocTypes.BranchTransfer,
        ["MLC"] = ErpJwVocTypes.BranchTransfer,
        ["RSL"] = ErpJwVocTypes.RepairSale,
    };

    private static ErpJwIndusColumn S(string h, int len, string indus, string links = "", bool req = false, string? col = null)
        => new(h, ErpJwIndusColType.Text, req, len, indus, links, col ?? h);

    private static ErpJwIndusColumn N(string h, string indus, string links = "", bool req = false, string? col = null)
        => new(h, ErpJwIndusColType.Decimal, req, 0, indus, links, col ?? h);

    private static ErpJwIndusColumn I(string h, string indus, string links = "", bool req = false, string? col = null)
        => new(h, ErpJwIndusColType.Int, req, 0, indus, links, col ?? h);

    private static ErpJwIndusColumn D(string h, string indus, string links = "", bool req = false, string? col = null)
        => new(h, ErpJwIndusColType.Date, req, 0, indus, links, col ?? h);

    private static ErpJwIndusColumn F(string h, string indus, string links = "", string? col = null)
        => new(h, ErpJwIndusColType.Flag, false, 0, indus, links, col ?? h);

    public static readonly IReadOnlyList<ErpJwIndusTemplate> Templates =
    [
        new("coa", 1, "Chart of accounts + opening trial balance", "epc_erp_coa_accounts",
            "Masters › Account Master; Reports › Trial Balance (cut-off)", "Finance / GL", "Opening balances → trial balance, balance sheet, dashboard",
            null, null, false, true, null,
            [
                S("code", 16, "A/C Code", "GL account code (epc_erp_coa_accounts.code)", true),
                S("name", 120, "A/C Name", "GL account name", true),
                S("account_type", 20, "Group (Asset/Liability/Equity/Income/Expense)", "asset|liability|equity|revenue|expense → statement placement", true),
                S("normal_side", 10, "Dr/Cr nature", "debit|credit (defaults from account_type)"),
                S("indus_ac_code", 20, "INDUS A/C Code (if renumbered)", "kept in description for traceability", col: ""),
                N("opening_debit", "Trial balance debit", "Opening journal JW-OPEN-TB Dr", col: ""),
                N("opening_credit", "Trial balance credit", "Opening journal JW-OPEN-TB Cr", col: ""),
                D("opening_date", "Cut-off date", "Opening journal date", col: ""),
            ]),
        new("parties", 2, "Suppliers / trade customers (debtors & creditors)", "epc_jewel_party",
            "Masters › Debtor/Creditor (Party Code)", "Purchases / Sales / AP-AR", "Party on metal & diamond purchase, fixing, trade sale",
            null, null, true, true, "created_by",
            [
                S("party_code", 20, "Party Code", "voucher.party_code, fixing.party_code", true),
                S("party_name", 120, "Party Name", "printed on purchase / fixing", true),
                S("party_type", 10, "Debtor / Creditor", "SUPPLIER|CUSTOMER|BOTH"),
                S("trn", 20, "TRN", "VAT input/output report, print"),
                S("phone", 40, "Tel"), S("mobile", 20, "Mobile"), S("email", 100, "E-mail"),
                S("address", 200, "Address"), S("city", 40, "City"), S("country", 40, "Country"),
                S("currency", 5, "Party Curr.", "voucher.party_curr"),
                I("cr_days", "Cr. Days", "due date = voc_date + cr_days"),
                N("credit_limit", "Credit limit"),
                S("ac_code", 20, "Linked A/C Code", "GL sub-ledger account (AP 2000 / AR 1100 by default)"),
            ]),
        new("pos_customers", 3, "POS customers (KYC)", "epc_jewel_pos_customer",
            "Masters › POS Customer / KYC", "POS / Compliance (AML-KYC)", "Customer on retail sale, return, advance, repair",
            null, null, true, true, "created_by",
            [
                S("customer_code", 20, "Customer (code)", "voucher.customer_code", true),
                S("title", 10, "Mr./Mrs."), S("customer_name", 120, "Customer name", "printed on tax invoice", true),
                S("mobile_cc", 6, "Mobile country code"), S("mobile", 20, "Mobile", "invoice print / CRM"),
                S("tel1", 20, "Tel 1"), S("email", 100, "Email"),
                S("nationality", 30, "Nationality", "AML/KYC"), S("type_resi", 10, "Type (RESI/TOURIST)", "tourist VAT refund eligibility"),
                S("id_type", 20, "ID type"), S("id_no", 40, "ID#", "AML/KYC (Emirates ID / passport)"),
                D("id_expiry", "ID expiry"), D("dob", "Date of birth"),
                S("city", 30, "City"), S("state", 30, "State"), S("po_box", 20, "PO Box"), S("religion", 30, "Religion"),
                N("loyalty_points", "Loyalty points"),
            ]),
        new("currencies", 4, "Currencies", "epc_jewel_currency", "Masters › Currency", "Masters", "Party/item currency + conversion",
            null, null, true, true, "created_by",
            [
                S("curr_code", 5, "Currency", "voucher.party_curr / item_curr", true),
                S("description", 60, "Description"), S("symbol", 5, "Symbol"),
                N("conv_rate", "Conv. rate"), N("min_conv_rate", "Min rate"), N("max_conv_rate", "Max rate"),
                S("status", 10, "Multiply/Divide"),
            ]),
        new("karats", 5, "Karat / purity master", "epc_jewel_karat_master", "Masters › Metal › Karat", "Masters / Metal", "Purity → pure weight on every metal line",
            null, null, true, true, "created_by",
            [
                S("karat_code", 10, "Karat", "metal_stock.karat, voucher line purity", true),
                S("description", 60, "Description"),
                N("std_purity", "Std purity", "pure_wt = gr_wt × purity", true),
                N("range_from", "Purity from"), N("range_to", "Purity to"), N("sp_gravity", "Sp. gravity"),
                S("division", 2, "Division (G/S/P)"),
            ]),
        new("rate_types", 6, "Metal rate types", "epc_jewel_rate_type", "Masters › Metal › Rate Type", "Masters / Metal", "GMS / GOZ / TTB rate conversion on purchase, sale, fixing",
            null, null, true, true, "created_by",
            [
                S("metal", 2, "Metal", "", true), S("rate_type", 10, "Rate type (GMS/GOZ/TTB)", "voucher metal rate basis", true),
                N("conv_factor", "Conv. factor (grams)"), N("conv_factor_oz", "Conv. factor (oz)"),
                S("currency", 5, "Currency"), N("curr_rate", "Curr. rate"),
                N("rate_variance_pct", "Rate variance %"), N("pos_margin_min", "POS margin min"), N("pos_margin_max", "POS margin max"),
                F("is_default", "Default"),
            ]),
        new("daily_rates", 7, "Daily metal / karat rates", "epc_gold_rates", "Masters › Daily Rate / Daily Karat Rate", "Masters / Metal / POS", "Rate printed on invoice + valuation",
            null, null, true, true, "created_by",
            [
                D("rate_date", "Date", "", true), S("karat", 10, "Karat (e.g. 22K)", "", true),
                S("currency", 3, "Currency"), N("buy_rate", "Buy rate"), N("sell_rate", "Sell rate", "invoice karat rate line", true),
                S("unit", 10, "Unit (gram)"), S("source", 50, "Source"),
            ]),
        new("metal_stock", 8, "Stock master + opening stock balance (weight & value)", "epc_jewel_metal_stock",
            "Masters › Metal/Diamond Jewellery; Reports › Metal Stock Balance (on date)", "Inventory / Stock", "Opening stock → stock report, metal report, GL 1300 inventory",
            null, null, true, true, "created_by",
            [
                S("item_code", 20, "Stock Code", "voucher line stock_code", true),
                S("description", 120, "Description", "", true),
                S("metal", 2, "Metal (G=gold, D=diamond jewellery, S=silver)", "stock report grouping", true),
                S("karat", 10, "Karat", "metal report by karat", true),
                N("purity", "Purity", "pure weight", true),
                S("type", 30, "Type"), S("brand", 30, "Brand"), S("category", 30, "Category"), S("sub_category", 30, "Sub category"),
                S("vendor", 30, "Supplier"), S("country", 30, "Country"), S("hs_code", 20, "HS code"), S("mc_unit", 10, "Making charge unit"),
                F("in_pieces", "Stock in pieces"), S("barcode_prefix", 10, "Barcode prefix"),
                N("pur_cost_gms", "Cost / gram", "average cost for COGS"),
                N("sale_price_gms", "Sale price / gram"),
                N("stock_qty", "Qty"), I("stock_pcs", "PCS (on date)", "stock report pcs"),
                N("stock_gms", "Gross Wt (on date)", "stock report weight"),
                N("stock_value", "Stock value at cost (on date)", "GL 1300 inventory opening (Dr 1300 / Cr 3900)"),
                N("reorder_level", "Reorder level"),
                F("post_opening", "Post opening stock to GL (Y/N, default Y)", "JW-OPEN-STK journal", col: ""),
            ]),
        new("designs", 9, "Design master", "epc_jewel_design", "Masters › Design / Diamond Design", "Masters / Manufacture", "Design → tag, manufacture",
            null, null, true, true, "created_by",
            [
                S("design_code", 20, "Design code", "", true), S("description", 120, "Description", "", true),
                S("category", 30, "Category"), S("sub_category", 30, "Sub category"), S("brand", 30, "Brand"),
                S("metal_and_stone", 10, "Metal & stone"), S("type", 30, "Type"), S("vendor", 30, "Vendor"),
                N("cost_amount", "Cost amount"), N("price1_lc", "Price 1 (LC)"), S("image_path", 250, "Picture path"),
            ]),
        new("diamonds", 10, "Diamond jewellery master", "epc_jewel_diamond_master", "Masters › Diamond", "Masters / Diamond", "Diamond item → diamond purchase & sale",
            null, null, true, true, "created_by",
            [
                S("item_code", 20, "Stock Code", "", true), S("description", 120, "Description", "", true),
                S("design", 20, "Design"), S("category", 30, "Category"), S("color", 20, "Color"), S("clarity", 20, "Clarity"),
                S("type", 30, "Type"), S("certificate_no", 40, "Certificate no"), S("certificate_by", 40, "Certificate by"),
                N("item_gr_wt", "Gross Wt"), N("pure_wt", "Pure Wt"), N("metal_qty", "Metal qty"), N("stone_qty", "Stone ct"),
                N("cost_amount", "Cost"), N("price1_lc", "Price 1 (LC)"), S("vendor", 30, "Vendor"),
            ]),
        new("color_stones", 11, "Colour stone master", "epc_jewel_color_stone_master", "Masters › Colour Stone", "Masters / Stones", "Stone component on design/sale",
            null, null, true, true, "created_by",
            [
                S("code", 20, "Code", "", true), S("description", 120, "Description", "", true),
                S("shape", 20, "Shape"), S("size", 20, "Size"), S("color", 20, "Color"), S("clarity", 20, "Clarity"),
                S("category", 30, "Category"), S("vendor", 30, "Vendor"), N("cost_amount", "Cost"),
            ]),
        new("pearls", 12, "Pearl master", "epc_jewel_pearl_master", "Masters › Pearl", "Masters / Stones", "Pearl component on design/sale",
            null, null, true, true, "created_by",
            [
                S("code", 20, "Code", "", true), S("description", 120, "Description", "", true),
                S("nature", 20, "Nature"), S("type", 30, "Type"), S("color", 20, "Color"), S("shape", 20, "Shape"),
                S("size", 20, "Size"), S("luster", 20, "Luster"), S("grade", 20, "Grade"), S("vendor", 30, "Vendor"), N("cost_amount", "Cost"),
            ]),
        new("tags", 13, "Tagged items / barcodes in stock", "epc_jw_tags", "Metal BarCode Generation (MBC) / Tag list", "Inventory / Tagging / POS", "Tag scanned on POS sale → sold; return → in stock",
            null, null, true, true, "created_by",
            [
                S("tag_no", 50, "Tag / Stock Code", "voucher line tag_no", true), S("barcode", 100, "Barcode"),
                S("item_type", 30, "Item type (gold/diamond)"), S("karat", 10, "Karat"),
                N("gross_weight", "Gross Wt", "", true), N("net_weight", "Net Wt"), N("stone_weight", "St. Wt"), I("stone_count", "Stone pcs"),
                N("other_weight", "Chain Wt / other"), N("purity_ppt", "Purity (ppt)"), N("pure_weight", "Pure Wt"),
                N("making_charges", "Making rate / value"), S("making_type", 20, "per_gram|lumpsum|percentage"),
                N("cost_price", "Landed value"), N("sell_price", "Tag price"),
                S("design_no", 50, "Design"), S("category", 100, "Category"), S("subcategory", 100, "Sub category"),
                D("purchase_date", "Purchase date"), S("location", 100, "Loc. code"), S("status", 20, "in_stock|sold|…"),
                S("description", 500, "Description"),
            ]),
        new("vouchers", 14, "Voucher headers (purchase, sale, return, advance, JV, transfer, manufacture)", "epc_jewel_voucher",
            "PUR/DPU purchase, POS/MSL sale, PSR return, PCR POS receipt/advance, GJV journal, BTR/MLC transfer, MFT manufacture", "Purchases / Sales / POS / Finance",
            "Voucher → stock movement (weight & value) → GL journal → VAT, trial balance, dashboard",
            null, null, true, false, "created_by",
            [
                S("voc_type", 10, "Voc Type (INDUS code)", "mapped to ecomae voc_type; INDUS code kept in source_voc_type", true),
                S("branch", 10, "Branch (HO/GH…)", "branch filter on every report", true),
                I("voc_no", "Voc No", "printed as BRANCH - NO", true),
                D("voc_date", "Voc Date", "GL journal date, VAT period", true),
                S("voc_time", 12, "Time (print)"), S("fy_code", 10, "Financial year"),
                S("party_code", 20, "Party Code", "parties.party_code"), S("party_name", 120, "Party name"), S("party_trn", 20, "Party TRN", "VAT input report"),
                S("party_curr", 5, "Party Curr."), N("party_curr_rate", "Party curr. rate"),
                S("customer_code", 20, "Customer", "pos_customers.customer_code"), S("customer_title", 10, "Mr./Mrs."), S("customer_name", 120, "Customer name"),
                S("mobile", 20, "Mobile"), S("tel1", 20, "Tel 1"), S("email", 100, "Email"), S("nationality", 30, "Nationality"),
                S("type_resi", 10, "Type (RESI/TOURIST)"), S("id_no", 40, "ID#"), S("city", 30, "City"),
                S("salesman", 20, "Salesman / Sman", "sales analysis by salesman"),
                S("item_curr", 5, "Item Curr."), N("item_curr_rate", "Item curr. rate"),
                N("metal_rate", "Metal Rate", "fixing / valuation"), F("is_fixed", "Fixed (Y/N)", "fix/unfix position"),
                F("internal_unfix", "Internal Unfix"), I("cr_days", "Cr. Days"),
                S("supp_inv_no", 30, "Supp. Inv No"), D("supp_inv_date", "Supp. Inv Date"), S("order_ref", 30, "Order Ref"),
                S("place_of_supply", 80, "Place of Supply", "VAT return emirate"), F("tourist_vat_refund", "Tourist VAT Refund Y/N"),
                S("karat_rates", 200, "Karat rates printed on invoice (e.g. 18K RATE : 154.75, 22K RATE : 189.25)"),
                N("net_amount", "Amount / Total Before VAT", "GL revenue 4000 (sale), inventory 1300 (purchase), 2300 (advance)", true),
                N("vat_amount", "VAT / TRN Amt", "GL 2100 output VAT (sale) / 1150 input VAT (purchase)"),
                N("total_with_vat", "Total Amt+TRN / Amount incl. VAT", "GL 1100 receivable / 2000 payable", true),
                N("rnd_off_amount", "Rnd Off Amt."), N("other_amount", "Other Amt."),
                N("old_gold_exchange", "Old Gold Exchange / Scrap Gold", "GL Dr 1320 old-gold stock / Cr 1100"),
                N("adjust_sale_return", "Adjust Sal. Ret."), N("gold_scheme_redeem", "Gold-Scheme-Rdm"),
                N("receipt_total", "Receipt Total"), N("refund_due", "Refund / Due"),
                N("input_vat", "Input Vat"), N("rcm_vat", "RCM Vat"),
                S("remarks", 500, "Remarks"), S("narration", 1000, "Narration"),
                S("status", 20, "Status (default posted)"),
                S("gl_mode", 10, "post|history (history = keep record, no stock/GL)", "history rows stay off stock & GL"),
            ]),
        new("voucher_lines", 15, "Voucher lines (items)", "epc_jewel_voucher_lines",
            "Sale - Detail / purchase line grid", "Purchases / Sales / Inventory", "Line → stock weight & value, COGS, sales analysis, metal report",
            "vouchers", "voucher_id", false, false, null,
            [
                I("line_no", "No."), S("stock_code", 20, "Stock Code", "metal_stock.item_code (moves weight & value)", true),
                S("division", 2, "Division (G/D)"), S("description", 120, "Description"),
                S("line_details", 250, "Description details (G:/D:/quality lines)"), S("tag_no", 50, "Tag / barcode", "tags.tag_no → sold/returned"),
                I("pcs", "PCS"), N("gr_wt", "Gr. Wt / Gms", "stock weight"), N("stone_wt", "Stone Wt"), N("net_wt", "Net Wt"),
                N("chargeable_wt", "Chargable"), N("purity", "Purity"), N("pure_wt", "Pure Wt.", "metal report pure weight"),
                N("mkg_rate", "Mkg. Rate"), N("mkg_amount", "Mkg. Amount", "making revenue"),
                N("metal_rate", "Metal Rate"), N("metal_amount", "Metal Amount"),
                N("stone_rate", "Stone Rate"), N("stone_amount", "St. Amount"), N("stone_diff", "Stone Diff."),
                N("wastage_pct", "Wstg Per"), N("wastage_qty", "Wstg Qty"), N("disc_amount", "Discount"),
                N("total_amount", "Total / Amount (Excl. VAT)", "line value (purchase cost into stock)"),
                N("vat_pct", "TRN Per% / VAT %"), N("vat_amount", "TRN Amt / VAT Amount"),
                N("total_with_vat", "Total+TRN / Amount incl. VAT"), N("net_amount", "Net amount"),
                S("loc_code", 20, "Loc. Code"), S("supplier", 30, "Supplier"),
            ]),
        new("voucher_receipts", 16, "Voucher receipts / tenders", "epc_jewel_voucher_receipts",
            "Receipt Mode grid (POS) / POS Receipt detail", "POS / Cash & Bank", "Tender → GL cash 1000, card 1030, bank 1010 vs receivable 1100",
            "vouchers", "voucher_id", false, false, null,
            [
                S("receipt_mode", 20, "Receipt Mode (CASH/CARD/VISA/BANK/NET)", "CASH→1000, CARD/VISA→1030, BANK→1010", true),
                S("currency", 5, "Currency"), N("curr_rate", "Curr. rate"), N("amount_fc", "Amount FC"),
                N("amount_lc", "Amount LC", "GL tender amount", true),
            ]),
        new("voucher_accounts", 17, "Voucher account lines (JV detail, other amounts)", "epc_jewel_voucher_addins",
            "Journal Voucher Detail / Other Amounts tab", "Finance / GL", "Dr/Cr account lines → GL journal",
            "vouchers", "voucher_id", false, false, null,
            [
                I("line_no", "No."), S("addin_code", 20, "Exp / HSN code"), S("description", 120, "Description"),
                S("debit_ac", 20, "A/C Code when Amt Dr/Cr = Debit", "GL debit account"),
                S("credit_ac", 20, "A/C Code when Amt Dr/Cr = Credit", "GL credit account"),
                N("pct", "TRN Per%"), N("amount", "Amount LC", "", true), S("narration", 200, "Narration"),
            ]),
        new("fixing", 18, "Purchase / sales fixing", "epc_jewel_fixing", "Metal Purchase Fixing (PFX) / Metal Sale Fixing (SFX)", "Metal / Fix-Unfix", "Rate fixing on unfixed weight → fixing reports",
            null, null, true, false, "created_by",
            [
                S("fix_type", 10, "PF (purchase) / SF (sales)", "", true), S("branch", 10, "Branch"), I("fix_no", "Voc No", "", true),
                D("fix_date", "Voc Date", "", true), S("party_code", 20, "Party Code"), S("party_name", 120, "Party name"),
                S("metal", 2, "Metal"), S("karat", 10, "Karat"), S("rate_type", 10, "Metal Rate type (GMS/GOZ)"),
                N("fix_rate", "Metal Rate", "", true), N("fix_qty_gms", "Pure Wt.", "", true), N("oz_wt", "Oz. Wt."),
                N("fix_amount", "Total (AED)", "", true), N("unfixed_qty", "Unfixed balance"),
                S("party_inv_ref", 30, "Invoice Ref. Number"), D("party_inv_date", "Invoice Ref. Date"),
                S("salesman", 20, "Sman"), S("reference_voc", 30, "Linked voucher (GRV#)"), S("status", 20, "open|fixed"), S("remarks", 500, "Narration"),
            ]),
        new("petty_cash", 19, "Petty cash vouchers", "epc_jewel_petty_cash", "Petty Cash (PET)", "Finance / Expenses", "Expense Dr account / Cr petty cash 1020 → P&L",
            null, null, true, false, "created_by",
            [
                S("branch", 10, "Branch"), S("voc_type", 10, "Voc Type (PET)"), I("voc_no", "Voc No", "", true), D("voc_date", "Voc Date", "", true),
                S("description", 200, "Description / Narration"), S("account_code", 20, "A/C Code (expense)", "GL debit account", true),
                S("account_name", 60, "A/C name"), N("debit_amount", "Amount LC (expense)", "GL Dr expense / Cr 1020"),
                N("credit_amount", "Credit amount"), S("currency", 5, "Curr Code"), S("approved_by", 40, "Approved by"),
                S("remarks", 500, "Remarks"), S("status", 20, "Status (default posted)"),
            ]),
        new("repairs", 20, "Repair receipts", "epc_jewel_repair", "Repair › Repair Receipt (REP)", "Repairs", "Receipt → transfer → workshop → delivery",
            null, null, true, false, "created_by",
            [
                S("branch", 10, "Branch"), I("voc_no", "Voc No", "", true), D("voc_date", "Voc Date", "", true),
                S("customer_name", 120, "Customer", "", true), S("mobile", 20, "Mobile"), S("tel1", 20, "Tel"), S("email", 100, "Email"),
                S("nationality", 30, "Nationality"), S("salesman", 20, "Salesman"), D("delivery_date", "Delivery date"),
                S("repair_narration", 1000, "Repair narration"), S("remarks", 500, "Remarks"), S("status", 20, "received|in_workshop|delivered"),
            ]),
        new("repair_items", 21, "Repair items", "epc_jewel_repair_items", "Repair Receipt detail", "Repairs", "Bag / item under repair",
            "repairs", "repair_id", false, false, null,
            [
                I("line_no", "No."), S("division", 2, "Division"), S("stock_code", 20, "Stock code"), S("description", 120, "Description", "", true),
                S("item_remarks", 200, "Item remarks"), S("bag_no", 20, "Bag no"), I("pcs", "PCS"), N("gr_wt", "Gr. Wt"),
                S("repair_type", 10, "Repair type"), S("item_type", 10, "Item type"), S("repair_status", 20, "Repair status"), D("delivery_date", "Delivery date"),
            ]),
        new("stock_verifications", 22, "Stock verification headers", "epc_jewel_stock_verification", "Stock Verification (Metal) MSV", "Inventory / Audit", "Physical vs computer stock",
            null, null, true, false, "created_by",
            [
                S("branch", 10, "Branch"), I("voc_no", "Voc No", "", true), D("voc_date", "Voc Date", "", true),
                S("verified_by", 40, "By"), S("location", 40, "Location"), S("metal_stone", 2, "Metal/Stone"), S("division", 2, "Division"),
                S("batch", 20, "Batch"), I("total_pcs", "Total PCs"), N("total_wgt", "Total Wgt."), I("scanned_pcs", "Scanned PCs"), N("scanned_wgt", "Scanned Wgt."),
                S("remarks", 500, "Remarks"), S("status", 20, "Status"),
            ]),
        new("stock_verification_lines", 23, "Stock verification lines", "epc_jewel_stock_verification_lines", "Stock Verification detail grid", "Inventory / Audit", "Difference per stock code",
            "stock_verifications", "verification_id", false, false, null,
            [
                I("line_no", "No."), S("stock_code", 20, "Stock Code", "", true), S("description", 120, "Description"),
                I("computer_pcs", "Computer Stock Pcs"), N("computer_gr_wt", "Computer Stock Gross Wt"),
                I("physical_pcs", "Physical Stock Pcs"), N("physical_gr_wt", "Physical Stock Gross Wt"),
                I("diff_pcs", "Stock Difference Pcs"), N("diff_gr_wt", "Stock Difference Gross Wt"),
            ]),
    ];

    public static ErpJwIndusTemplate? Find(string? key)
        => Templates.FirstOrDefault(t => string.Equals(t.Key, key?.Trim(), StringComparison.OrdinalIgnoreCase));

    public static string HeaderLine(ErpJwIndusTemplate template) => string.Join(",", template.Headers);
}
