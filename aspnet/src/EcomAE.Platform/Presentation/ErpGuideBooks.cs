namespace EcomAE.Platform.Presentation;

/// <summary>
/// Extra ERP operator books beyond the module catalog in <c>erp-guide-modules.json</c>.
/// PHP: <c>erp_guide.php</c>, <c>erp_full_guide.php</c>, <c>erp_advanced_guide.php</c>,
/// <c>erp_only_operator_guide.php</c>, <c>custom_shipping_guide.php</c>.
/// </summary>
public static class ErpGuideBooks
{
    public const string Modules = "modules";
    public const string Howto = "howto";
    public const string Full = "full";
    public const string Advanced = "advanced";
    public const string ErpOnly = "erp-only";
    public const string Customs = "customs";

    public sealed record Book(string Key, string Title, string Summary, string PhpPath, IReadOnlyList<OperatorGuidesCatalog.Chapter> Chapters);

    public static IReadOnlyList<Book> All { get; } =
    [
        new(Modules, "ERP module book",
            "Step-by-step module guide from epc_guide_modules() — What it is, Setup, Daily work, Accounting impact, Tips.",
            "/ERP/?epc_erp_shell=1&area=overview&tab=guide",
            []),
        new(Howto, "How to use ERP",
            "Clear path from day-to-day work to the books: Sales → Revenue → Receivable → Balance, Purchase → Payable → Balance, Cash & Bank, COA → GL → P&L → Balance sheet.",
            "/CP/shop/finance/erp/guide",
            HowtoChapters()),
        new(Full, "ERP user guide (full)",
            "Entitlement-aware modules plus first-time setup and industry document chains (LPO/PO → GRN → SO → DO → Invoice).",
            "/CP/shop/finance/erp/erp_full_guide",
            FullChapters()),
        new(Advanced, "Advanced ERP — complete user guide",
            "Industry-agnostic end-to-end workflow plus per-module reference (tax, inventory, CRM, RFQ, landed cost, payroll).",
            "/CP/shop/finance/erp/erp_advanced_guide",
            AdvancedChapters()),
        new(ErpOnly, "ERP-only operator guide",
            "Daily use after onboarding when commerce is hidden. Sign in, switch company, enabled modules, users, no storefront.",
            "/CP/shop/finance/erp/erp_only_operator_guide",
            ErpOnlyChapters()),
        new(Customs, "Customs & logistics declarations",
            "UAE customs declaration tracking — declaration types from the C&L workbook, dashboard KPIs, category lists, line items, reports.",
            "/CP/shop/finance/erp/custom_shipping/custom_shipping_guide",
            CustomsChapters()),
    ];

    public static Book Get(string? key)
    {
        var needle = string.IsNullOrWhiteSpace(key) ? Modules : key.Trim();
        foreach (var book in All)
        {
            if (book.Key.Equals(needle, StringComparison.OrdinalIgnoreCase))
            {
                return book;
            }
        }

        return All[0];
    }

    public static bool IsKnown(string? key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return true;
        }

        foreach (var book in All)
        {
            if (book.Key.Equals(key.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static IReadOnlyList<OperatorGuidesCatalog.Chapter> HowtoChapters() =>
    [
        OperatorGuidesCatalog.Ch("How to use ERP",
            "Work left-to-right: Sales / Purchases → Cash & bank → Chart of accounts → GL → P&L → Balance sheet.",
            [
                "Open ERP module (Shop → ERP Finance, or the Open ERP button).",
                "Live numbers on the PHP page are this company’s current books — not demo placeholders. They need the tenant shop database; without it the snapshot stays empty.",
                "Red amounts are negative (money out / overdrawn / credit balance).",
            ]),
        OperatorGuidesCatalog.Ch("Live numbers — what each figure means",
            "PHP snapshot table (refreshed from the company DB).",
            [
                "Sales revenue (ex VAT) — completed shop orders this month, before VAT. Open / in-progress orders do not count until status is Completed.",
                "Gross margin — sales revenue minus purchase cost on those same completed orders (operational view — not the full P&L).",
                "Unpaid orders (due) — how much customers still owe on specific completed orders (invoice total minus payments).",
                "Customer wallet balance — prepaid customer account ledger (top-ups minus charges). Different from unpaid orders.",
                "Supplier bills unpaid — what you still owe suppliers (purchase invoices minus payments).",
                "Cash + bank total — opening + receipts − payments. Negative = overdrawn or mis-posted — reconcile before trusting this number.",
                "P&L net profit (MTD) — from posted general-ledger journals this month. Can differ from Gross margin until sales are posted to GL.",
                "Total assets / Liabilities + equity — should match. If they do not, post missing journals or fix opening balances.",
                "Chart of accounts / journals — how many GL account codes exist, and how many journal entries have been posted.",
                "Masters loaded — suppliers, purchase bills, cash accounts, warehouses (stock locations — not the same as suppliers).",
            ]),
        OperatorGuidesCatalog.Ch("Key terms (read once)",
            "PHP legend.",
            [
                "Revenue vs P&L — dashboard revenue / margin come from completed orders. P&L comes from posted GL journals. Post sales to GL to keep them aligned.",
                "Due vs wallet — Unpaid orders = money tied to invoices. Wallet balance = prepaid customer ledger. Both can be non-zero at once.",
                "COA / GL — COA = Chart of Accounts (account codes). GL = General Ledger (debit/credit journals that feed P&L and the balance sheet).",
                "When amounts appear — sales, receivable and order-linked payable figures update when the shop order status is Completed — not while fulfilment is still open.",
            ]),
        OperatorGuidesCatalog.Ch("Courier charges, VAT & document map",
            "Who pays courier? The customer.",
            [
                "The delivery fee is saved on the order, shown in OMS, and added as a line on the UAE tax invoice (Accounts Receivable).",
                "VAT on courier: UAE destinations — taxable income (output VAT). Outside the UAE — zero-rated. The shipping destination country drives this.",
                "What to keep on file: UAE → tax invoice (PDF/XML), TRN, and payment proof. Export → shipping proof, commercial invoice, and buyer-country evidence for zero-rating.",
                "Document chain: Shop order → VAT treatment → Sales order (SO) → Purchase orders (PO) → Supplier bills (AP) → Customer tax invoice (AR).",
                "On the OMS fulfilment panel use Link ERP / Sync / Auto-post so sales order, PO, bill and invoice stay aligned.",
            ]),
        OperatorGuidesCatalog.Ch("What's new — enterprise capabilities",
            "Tenant-country aware: one setting on the company profile (Country) localizes the whole suite.",
            [
                "Country master switch — currency, language (incl. Arabic/Urdu RTL), tax regime, fiscal-year start and HR labour-law pack switch together. UAE→AED/VAT 5%/FTA, KSA→SAR/VAT 15%/ZATCA, Pakistan→PKR/Sales Tax 18%/FBR, India→INR/GST.",
                "Country-aware HRMS — end-of-service gratuity, annual-leave entitlement and leave salary per labour law.",
                "Corporate Tax provision — P&L applies the UAE 9% CT with the AED 375,000 small-business threshold.",
                "Accounting setup — COA, opening balances, fixed assets with depreciation, VAT return, Tax compliance and E-invoicing tabs.",
                "Standalone ERP — an ERP-only client can create customers, inventory items and sales orders with no e-commerce storefront dependency.",
            ]),
        OperatorGuidesCatalog.Ch("Ecom BOS — Business Operating System pillars",
            "ERP is one pillar alongside commerce, compliance, workflows and industry intelligence.",
            [
                "Compliance center — filing obligations (VAT, corporate tax, ESR, e-invoicing, WPS), live filing calendar, document-retention rules.",
                "Approvals engine — threshold rules per document type with multi-step approver chains and an immutable audit trail.",
                "Industry intelligence — live operational KPIs plus a recommended-controls checklist tailored to the active industry pack.",
            ]),
        OperatorGuidesCatalog.Ch("End-to-end flow A — Sales & revenue",
            "Customer side.",
            [
                "Customer places order on the shop → order appears in Orders.",
                "Revenue is calculated from order line selling prices (ex VAT) — ERP tab Revenue (Sales).",
                "When the customer pays, post via Customer account operations or mark the order paid in CP.",
                "Receivable = order total incl. VAT minus payments — only for Completed orders.",
                "Balance = customer prepaid ledger (shop_users_accounting) — credits minus debits.",
            ]),
        OperatorGuidesCatalog.Ch("End-to-end flow B — Purchase & payables",
            "Supplier side.",
            [
                "Link suppliers to warehouses: ERP → Payables → Sync from warehouses (or add manually).",
                "When the supplier invoice arrives: ERP → Purchases → record invoice (or Generate from order).",
                "This increases supplier payable balance — Payables tab.",
                "When you pay the supplier: Payables → Record supplier payment — reduces payable and posts cash/bank outflow.",
            ]),
        OperatorGuidesCatalog.Ch("End-to-end flow C–E — Cash, COA/GL, P&L",
            "Books.",
            [
                "Cash & bank — create Main cash / bank accounts; post receipts and payments. Balance = opening + receipts − payments.",
                "COA lists GL accounts by code — assets (1xxx), liabilities (2xxx), equity (3xxx), revenue (4xxx), expenses (5xxx/6xxx).",
                "Purchase invoices auto-post: Dr COGS + VAT Input, Cr Accounts Payable.",
                "GL tab → Post sales orders to GL for the date range (Dr AR, Cr Revenue, Cr VAT Output). Sync unposted catches older sub-ledger entries.",
                "P&L — total revenue minus expenses = net profit. Balance sheet — assets, liabilities, equity. Current period earnings sit in equity until year-end close.",
            ]),
        OperatorGuidesCatalog.Ch("Order planning, Supplier portal & dashboard",
            "PHP “step by step” enterprise modules.",
            [
                "Order planning — forecast demand, safety stock and reorder point per item × warehouse; Confirm / Confirm all due; ABC/XYZ; Redistribution; Exceptions.",
                "Process flow — define step chains; Approve & route; Monitor GPS-style tracker; Org map; Workforce; Hierarchy.",
                "Accounting automation centre — Order→ERP posting, period/year-end close, bank recon, collections dunning, report scheduler, VAT reminders.",
                "Supplier portal — per-supplier scorecards (on-time 40% + responsiveness 30% + activity 20% + win rate 10%).",
                "Main dashboard — NetSuite-style portlets plus Operational KPI ribbon (revenue, gross margin %, DSO, DPO, inventory turnover, current ratio).",
            ]),
    ];

    private static IReadOnlyList<OperatorGuidesCatalog.Chapter> FullChapters() =>
    [
        OperatorGuidesCatalog.Ch("How to use this guide",
            "Each module has four parts: What it does, Set up (in order), Daily workflow, Accounting impact.",
            [
                "You only see modules included in your plan. The module book on this page lists every module from epc_guide_modules().",
                "Below the modules, industry document chains show which document is prepared at each stage (LPO/PO → GRN → SO → DO → Invoice).",
                "Open ERP home when you are ready to work the click-path.",
            ]),
        OperatorGuidesCatalog.Ch("First-time setup & configuration — do these in order",
            "New tenant? Complete the list top to bottom once. Each step unlocks the next.",
            [
                "Company profile — legal/trade name, logo, address, TRN/VAT, trade licence and bank pay-to details. These print on every document.",
                "Country — set the company country once; currency, language (incl. RTL), tax regime, fiscal-year start and labour-law pack localise together.",
                "Financial year & periods — set the year start and open accounting periods (Fixed assets / Year-end closing → Setup).",
                "Chart of accounts — load or adjust the COA every module posts to (General ledger → Setup).",
                "Number sequences — confirm voucher/document sequences (invoices, POs, requisitions, journals).",
                "Tax setup — confirm VAT/GST codes and rates; add withholding codes if needed (Tax → Setup).",
                "Business units / legal entities — define units so masters attach to the right entity.",
                "Bank accounts — create cash and bank accounts (IBAN, SWIFT/BIC, GL account, business unit).",
                "Master data — vendors, customers, items and fixed assets.",
                "Opening balances — enter go-live balances as at your migration date.",
                "Users & roles — create users, assign roles, set approval thresholds (System administration).",
                "Go live — start daily transactions; use each module’s Reports & inquiries tab to monitor.",
            ]),
        OperatorGuidesCatalog.Ch("Industry document workflows — which document at which stage",
            "End-to-end document chain per industry: who prepares it, the stage, and the posting impact. PHP epc_flow_registry().",
            [
                "General Trading / Retail — PR → RFQ → SQ → PO → GRN → BILL → PV → EST → SO → DO → INV → RV.",
                "Jewellery & Diamond — buy bullion → make → hallmark → sell (WO / MI / QC / FG plus old-gold CRN).",
                "Trading / Import-Export — LC + Bill of Entry / customs + landed cost on BILL.",
                "Construction / Contracting — BOQ → Contract → IPC progress claims → Retention.",
                "Retail + POS — SHIFT open, INV at POS, Z-Report / day close, then bank RV.",
                "Manufacturing — WO / MI / QC / FG (mat + labour + overhead).",
                "Rental / Leasing — contract then periodic invoices.",
                "Open the Module book tab to walk each entitled module’s Setup and Daily workflow.",
            ]),
    ];

    private static IReadOnlyList<OperatorGuidesCatalog.Chapter> AdvancedChapters() =>
    [
        OperatorGuidesCatalog.Ch("End-to-end workflow",
            "Follow these steps to run the business. Each step links to the module that does the work.",
            [
                "Set your industry — choose auto parts, electronics, fashion, jewellery, food, pharma, services, and more. This configures product fields, units and item type.",
                "Configure your tax profile — pick your country in the Tax Toolkit. The worldwide tax engine applies VAT / GST / sales-tax for the company and per customer country.",
                "Add warehouses & products — industry-specific fields (OEM number, size/colour, batch/expiry, IMEI) appear automatically.",
                "Record purchases (procurement) — suppliers and purchase invoices. Stock, weighted-average cost and input tax update; the general ledger is posted automatically.",
                "Sell & invoice — output tax is calculated, stock is reduced, revenue is posted. E-invoicing fields are included where required.",
                "Manage customers in CRM — leads, scoring, pipeline, tax-correct quotes, convert won deals into orders.",
                "Run payroll & track assets — both post to the ledger so the books stay complete.",
                "Review reports — P&L, balance sheet, tax due, inventory valuation and CRM forecast — in your language and currency.",
            ]),
        OperatorGuidesCatalog.Ch("Industry foundation",
            "Ready industry blueprints seed inventory custom fields, default unit of measure and item type.",
            [
                "Applying an industry only adds fields — it never deletes your data.",
                "Auto parts: OEM/article, fits make/model/year, position.",
                "Food / pharma: batch number and expiry (FEFO) tracking.",
                "Electronics / jewellery: per-unit serial / IMEI / certificate.",
                "Fashion: size, colour, material, gender variants.",
                "Services: hourly / fixed / retainer billing basis.",
            ]),
        OperatorGuidesCatalog.Ch("Products & inventory",
            "Multi-warehouse stock with weighted-average costing, batch / serial tracking, expiry dates and unlimited custom fields.",
            [
                "Every stock movement is journalled so inventory valuation always ties back to the ledger.",
                "Use warehouses for shops, vans or bonded stores.",
                "Opening balances can be imported when you go live.",
            ]),
        OperatorGuidesCatalog.Ch("Worldwide tax engine",
            "Per-tenant and per-customer tax profiles resolve treatment by country — UAE/GCC VAT, EU VAT, UK VAT, India GST, US sales tax, zero-rated / exempt.",
            [
                "Set the company country once; customers can override by their country.",
                "Quotes, invoices and purchases all use the same engine.",
                "Install more country kits any time from the Tax Toolkit.",
            ]),
        OperatorGuidesCatalog.Ch("Procurement, sales, RFQ, planning, landed cost",
            "PHP module reference.",
            [
                "Procurement & suppliers — input tax, stock and average cost, supplier balances.",
                "Sales & invoicing — tax-correct invoices and credit notes in any currency. Convert an accepted CRM quote straight into an order.",
                "Advanced purchasing (RFQ) — send lines to multiple suppliers, compare, award into a PO. Cheapest supplier per line is highlighted.",
                "Demand forecasting — average daily demand, trend, reorder point, suggested order qty. Items below reorder point are listed first.",
                "Landed cost — spread freight, customs duty, insurance by value, quantity or weight. Apply is idempotent — it cannot double-count.",
            ]),
        OperatorGuidesCatalog.Ch("Shipping, CRM, accounting",
            "Remaining advanced modules.",
            [
                "Shipping & logistics — carriers, tracking numbers, ETAs; receiving an inbound shipment books goods into the warehouse.",
                "Advanced CRM — hot / warm / cold scoring, weighted forecast, Customer-360, next-best-action list.",
                "Accounting, payroll & assets — double-entry GL, P&L, balance sheet. Chart of accounts is pre-seeded and extensible.",
                "This guide and the ERP screens work with the platform language selector; numbers, currency and RTL layouts adapt automatically.",
            ]),
    ];

    private static IReadOnlyList<OperatorGuidesCatalog.Chapter> ErpOnlyChapters() =>
    [
        OperatorGuidesCatalog.Ch("1. Sign in",
            "Open the ERP Suite for your company — Finance, CRM, HR, Custom & Shipping, and Document control.",
            [
                "Log in at https://www.ecomae.com/cp/ with your email and password.",
                "On a site that also has a storefront, commerce modules stay visible. For ERP-only tenants, commerce is hidden and login opens this shell directly.",
                "Shared hosting sessions are bound to your company database on www.ecomae.com.",
            ]),
        OperatorGuidesCatalog.Ch("2. Switch company (multi-entity)",
            "If your operator enabled Multi-entity.",
            [
                "Open ERP → Finance area → Multi-entity tab.",
                "Select the legal entity before posting journals, invoices, or payroll.",
                "Each entity can have its own TRN and bank accounts.",
            ]),
        OperatorGuidesCatalog.Ch("3. Your enabled modules",
            "Your operator enabled these ERP areas for this tenant.",
            [
                "Sidebar tabs you do not see are turned off at platform level — contact your administrator to request access.",
                "Walk each enabled module from the Module book tab (What it is / Setup / Daily work).",
            ]),
        OperatorGuidesCatalog.Ch("4. Users & access",
            "Administrators manage users in CP → Users.",
            [
                "ERP → Staff assigns department tabs (Finance-only, HR-only, etc.).",
                "Change your password after first login.",
            ]),
        OperatorGuidesCatalog.Ch("5. No storefront",
            "Shop, catalogue, and cart are disabled on ERP-only deployments.",
            [
                "There is no customer-facing web shop — all work happens in the ERP shell at www.ecomae.com/cp/.",
                "Hosted on ECOM AE · Shared ERP on www.ecomae.com · Support via your platform operator.",
            ]),
    ];

    private static IReadOnlyList<OperatorGuidesCatalog.Chapter> CustomsChapters() =>
    [
        OperatorGuidesCatalog.Ch("Customs & logistics declarations",
            "UAE customs declaration tracking — declaration types from the C&L Excel workbook, dashboard KPIs, category lists, core field capture, and Phase 2 reports.",
            [
                "Menu path: ERP Suite → Custom & Shipping (sidebar group) · or Shop → Finance → ERP → area custom_shipping.",
            ]),
        OperatorGuidesCatalog.Ch("Step 1 — Open the dashboard",
            "KPI tiles (total, draft, submitted, cleared), six category cards, quick-action buttons, recent-declarations table.",
            ["From CP sidebar: ERP Suite → Custom & Shipping."]),
        OperatorGuidesCatalog.Ch("Step 2 — Pick a declaration category",
            "Click a category card or use a quick action.",
            [
                "Import — local, FZ, CW, courier, re-export intake.",
                "Export — ROW, FZ, CW, re-export, courier.",
                "Transit — FZ transit in/out, ROW transit, courier.",
                "Temporary Admission — ROW, FZ, CW to local.",
                "Transfer — CW cargo transfer, FZ internal.",
                "LGP — warehouse intake form (dedicated fields, not in the type registry).",
            ]),
        OperatorGuidesCatalog.Ch("Step 3 — Fill required fields & line items",
            "Customs categories need Company, Customs emirate, Declaration type, Date, and Declaration date.",
            [
                "LGP uses warehouse intake fields (customer ref, warehouse, packing list, commercial invoice, etc.).",
                "Add multiple HS code rows (origin, qty, unit, volume, amount, weight).",
                "Required per row: HS code, country of origin, quantity.",
            ]),
        OperatorGuidesCatalog.Ch("Step 4 — Save draft, submit, reports",
            "Record stays editable with status draft until submitted to UAE customs (submitted → cleared).",
            [
                "Reports: declaration search, cost summary, duty stub, re-export tracking, document expiry.",
                "Filter, print, export CSV per report (cs_view=reports).",
                "Super CP deploy steps live in the CP Custom & Shipping portal guide.",
            ]),
    ];
}
