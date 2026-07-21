<?php
/**
 * Advanced ERP — step-by-step guide content (data layer).
 *
 * Returns structured, translation-friendly guide entries for EVERY module:
 * what it does, ordered setup steps, the daily workflow click-path, the
 * accounting impact, and tips. The CP guide page renders these entitlement-
 * aware (a payroll-only tenant sees only its modules) and pairs them with the
 * per-industry document chains from epc_flow_registry().
 *
 * Pure data (no DB), so it is unit-testable and reusable.
 */

declare(strict_types=1);

defined('_ASTEXE_') or die('No access');

if (!function_exists('epc_guide_entry')) {
    /**
     * @param array<int,string> $setup
     * @param array<int,string> $daily
     * @param array<int,string> $tips
     * @return array<string,mixed>
     */
    function epc_guide_entry(string $module, string $title, string $what, array $setup, array $daily, string $accounting, array $tips): array
    {
        return array(
            'module' => $module,
            'title' => $title,
            'what' => $what,
            'setup' => $setup,
            'daily' => $daily,
            'accounting' => $accounting,
            'tips' => $tips,
        );
    }
}

if (!function_exists('epc_guide_modules')) {
    /**
     * Full guide content, keyed by module code (matches epc_mod_registry()).
     *
     * @return array<string,array<string,mixed>>
     */
    function epc_guide_modules(): array
    {
        $E = 'epc_guide_entry';
        $g = array();

        $g['core'] = $E('core', 'Foundation & company setup',
            'The base layer: your company profile, industry, base currency, users and the chart of accounts everything else posts to.',
            array(
                'Open ERP setup and choose your industry — this configures product fields, default units and costing method.',
                'Enter your company profile (legal/trade name, logo, TRN/VAT, trade licence, address, bank details) — used on every printed document.',
                'Set your base/home currency and financial-year start.',
                'Create users and assign roles (see Roles & permissions).',
            ),
            array(
                'Day-to-day work happens in the module pages; the foundation is configured once and rarely changes.',
            ),
            'No direct postings — defines the ledger and document numbering other modules use.',
            array('Get the company profile and TRN right first — it flows onto all invoices and statements.'));

        $g['company'] = $E('company', 'Company profile, letterhead & statements',
            'Your legal identity on every document, plus customer and vendor Statements of Account.',
            array(
                'Fill the company profile: logo, legal & trade name, TRN/VAT (and label), trade licence, address, bank pay-to, base currency.',
                'Optionally set per-branch overrides (branch address/phone/TRN appear on that branch\'s documents).',
                'Add default invoice terms and letterhead header/footer.',
            ),
            array(
                'Print any document — it carries your logo, TRN and letterhead automatically, with the amount in words.',
                'Run a Customer Statement of Account for any date range (opening + invoices − receipts = closing) with ageing; email or print on letterhead.',
                'Run a Vendor (supplier) Statement the same way for payables.',
                'Use open-item mode to show only unsettled documents.',
            ),
            'Statements read existing AR/AP; no new postings.',
            array('Tax invoices legally need both seller and buyer TRN, a tax-invoice title, sequential number and VAT breakdown — all handled here + voucher sequences.'));

        $g['crm'] = $E('crm', 'CRM & sales pipeline',
            'Leads, contacts, opportunities and quotations feeding the sales process.',
            array('Import or add customers & contacts.', 'Define pipeline stages and your quotation template.'),
            array('Capture a lead → qualify → create opportunity → send quotation → convert won quote to a Sales Order.', 'Track activities, follow-ups and pipeline value on the CRM dashboard.'),
            'Quotations post nothing; conversion to SO/Invoice posts revenue when delivered/invoiced.',
            array('Use customer-specific price lists so quotes pull the right price automatically.'));

        $g['inventory'] = $E('inventory', 'Inventory & warehouses',
            'Multi-warehouse stock, valuation, ageing and turnover.',
            array('Create warehouses (and bins if needed).', 'Add items with the industry fields shown (batch/expiry, serial/IMEI, size/colour, weight/purity…).', 'Set reorder levels and costing method.'),
            array('Receive stock via GRN, issue via Delivery Order, transfer between warehouses, and count/adjust with stock takes.', 'Watch inventory ageing (0-30/31-60/61-90/90-180/180+), slow/dead stock and turnover on the dashboard.'),
            'GRN: Inventory +, GR/IR +. Delivery/Sale: Inventory −, COGS +. Adjustments post to a variance account.',
            array('Enable batch/expiry for pharma & F&B; serial/IMEI for electronics; weight & purity for jewellery.'));

        $g['product_info'] = $E('inventory', 'Product Information System (industry-pack fields)',
            'Industry-pack-driven product/inventory field structure with per-tenant inventory vs non-inventory classification. Applying an industry pack releases that pack\'s specialized product fields; each tenant decides which fields are stock-tracked (inventory) and which are catalogue-only (non-inventory). Scales to 50-100 industries from the per-tenant industry catalogue.',
            array(
                'In Accounting setup, apply your industry pack — it seeds that pack\'s product fields into Product Information (e.g. Jewellery: metal, purity, gross weight, stone type, stone carat, hallmark; Oil & gas: barrel/MT specs).',
                'Open Product Information > Field setup to review every released field and its source pack.',
                'Set each field\'s role: Inventory (part of item master, stock + accounting/valuation) or Non-inventory (descriptive/catalogue only). Defaults come from the pack but are fully editable.',
                'Add any custom fields (text/number/date/select) the tenant needs beyond the pack.',
            ),
            array(
                'Create items using the industry fields shown; inventory-classified fields feed stock, valuation and the item master, non-inventory fields stay descriptive.',
                'Re-classify a field any time (Inventory <-> Non-inventory) or enable/disable it — changes apply immediately, per tenant.',
                'Re-applying the same pack refreshes labels/types but never overwrites a classification you changed (idempotent, additive).',
            ),
            'Inventory-classified fields participate in stock movements and valuation (Inventory +/-, COGS, GR/IR); non-inventory fields carry no GL impact.',
            array('Onboarding sets sensible defaults from the pack so a new tenant starts correctly, then the client tailors the inventory/non-inventory split without any code change.'));

        $g['aging'] = $E('gl', 'Aging — receivables, payables & inventory',
            'Bucketed aging for AR, AP and inventory with configurable bucket sizes (from Accounting setup) and distribution bars, as-of any date.',
            array('Confirm your aging bucket sizes (e.g. 0-30 / 31-60 / 61-90 / 90+) in Accounting setup.'),
            array('Open Finance > Aging and switch the view: Receivables (by customer), Payables (by supplier) or Inventory (by item) — each shows per-bucket totals and a distribution bar as at the chosen date.'),
            'Read-only over AR/AP subledgers and the inventory ledger; no new postings.',
            array('Use AR aging for collections priority, AP aging for payment planning, and inventory aging to spot slow/dead stock.'));

        $g['procurement'] = $E('procurement', 'Procurement & purchasing',
            'The buy-side document chain: requisition → RFQ → PO/LPO → GRN → bill → payment.',
            array('Add suppliers and payment terms.', 'Enable approval workflow for POs above a threshold (optional).'),
            array('Raise a Purchase Requisition → send RFQ to suppliers → compare quotes → issue PO/LPO → receive goods (GRN) → book supplier bill → pay (Payment Voucher).'),
            'PO: commitment only. GRN: Inventory +, GR/IR +. Bill: GR/IR −, AP +, Input VAT +. Payment: AP −, Bank −.',
            array('Use RFQ comparison to document best-price selection for audit.'));

        $g['scm'] = $E('scm', 'Supply chain (forecasting, landed cost, logistics)',
            'Demand forecasting, landed-cost apportionment and logistics/shipment tracking.',
            array('Set lead times and reorder policy per item.', 'Define landed-cost types (freight, insurance, duty).'),
            array('Run demand forecast → generate reorder suggestions → add landed costs to a shipment so they flow into item cost → track shipments to delivery.'),
            'Landed costs capitalise into inventory cost (Inventory +, clearing −).',
            array('Link customs duty (Dubai Customs module) into landed cost so duty lands in item cost.'));

        $g['customs'] = $E('customs', 'Dubai Customs (import/export)',
            'UAE customs/Mirsal-style declarations: Bill of Entry, HS-code duty + 5% import VAT, deposits.',
            array('Maintain HS codes and duty rates.', 'Set your customs deposit/guarantee account.'),
            array('Create an import declaration → enter CIF + HS code → system computes customs duty + import VAT → post Bill of Entry → duty flows to landed cost.'),
            'Customs duty + import VAT booked; duty capitalised via landed cost.',
            array('Other countries can be added as customs packs the same way.'));

        $g['tax'] = $E('tax', 'Worldwide tax',
            'Correct VAT/GST/sales tax per country and per customer, by date.',
            array('Select your country tax toolkit.', 'Set registration number and filing frequency.'),
            array('Tax is applied automatically on invoices based on country/date; review the tax summary before filing.'),
            'Output VAT on sales, Input VAT on purchases, net to the VAT control account.',
            array('Cross-border B2B may be zero-rated/reverse-charge — the engine handles place-of-supply.'));

        $g['compliance'] = $E('compliance', 'Tax compliance engine (auto-update)',
            'Date-effective tax rules per country with FTA autofetch staging, so a new law applies automatically from its effective date.',
            array('Confirm your country and that baseline rules are loaded.', 'Optionally connect the regulator feed (needs the authority API key).'),
            array('When a rule changes, review the staged diff (old → new) and apply with one click; old documents keep old rates, new ones use the new rule.'),
            'No direct posting — drives VAT calc, returns and e-invoicing.',
            array('Apply is staged (not silent) so a bad feed can never corrupt live books; every version is logged and rollback-able.'));

        $g['einvoice'] = $E('einvoice', 'E-invoicing',
            'Compliant e-invoice generation (UAE/ZATCA/India-GST/EN16931 formats).',
            array('Pick your e-invoice format.', 'Add the required seller fields (TRN, address).'),
            array('Post a tax invoice → generate the e-invoice payload → submit/clear (live clearance needs the portal credentials).'),
            'Mirrors the tax invoice; no extra GL impact.',
            array('Live portal certification needs the authority\'s sandbox URL + signing certs — the code is ready for the keys.'));

        $g['finance'] = $E('gl', 'Finance suite (GL, bank rec, VAT, corporate tax)',
            'General ledger, AI bank reconciliation, VAT return, corporate tax and the reporting centre.',
            array('Set up your chart of accounts and bank/cash accounts.', 'Configure fiscal periods.'),
            array('Post journals, import the bank statement and auto-match (AI bank rec), produce the VAT return and corporate-tax computation, and run the reporting centre (P&L, BS, trial balance, cash flow).'),
            'Standard double-entry; trial balance must net to zero.',
            array('Use the AI bank-statement → P&L/BS cross-check to verify the books against real bank movements.'));

        $g['credit'] = $E('credit', 'Credit & collections',
            'Customer credit limits, terms, holds, AR ageing, dunning and statements.',
            array('Set a credit limit and payment terms per customer.', 'Define dunning levels.'),
            array('Sales over the limit are flagged/held → run AR ageing → send dunning reminders → issue statements.'),
            'No new postings; controls AR exposure.',
            array('Credit-limit checks also gate B2B e-commerce checkout.'));

        $g['treasury'] = $E('treasury', 'Treasury — daily cash & bank monitoring',
            'Daily cash report, live cash/bank position, alerts and the bank-statement P&L/BS cross-check.',
            array('Tag each cash/bank account and branch.'),
            array('Open the daily cash report (opening + receipts − payments = closing) per account/branch → monitor the live position with low-balance and large-transaction alerts.'),
            'Reads cash/bank ledgers; no new postings.',
            array('Unreconciled-item ageing flags stale entries early.'));

        $g['audit'] = $E('audit', 'Audit assurance tools',
            'High-level checks: trial-balance zero, GL-vs-subledger tie-outs, sequence-gap, duplicate detection, post-close changes.',
            array('Nothing to set up — runs over your live data.'),
            array('Run the exceptions dashboard before month/year-end → clear each flagged item.'),
            'Read-only assurance over the ledger + audit trail.',
            array('Sequence-gap detection uses your voucher sequences — keep them gapless.'));

        $g['org'] = $E('org', 'Organization structure',
            'Company → business unit → branch → warehouse, so transactions tag to a branch/BU.',
            array('Define legal entities, business units, branches and warehouses.'),
            array('Pick the branch/BU on transactions → filter reports and dashboards by branch/BU.'),
            'Tags postings with branch/BU dimensions.',
            array('Single-branch tenants can leave this default and never see branch fields.'));

        $g['vouchers'] = $E('vouchers', 'Voucher numbering sequences',
            'Per-document, per-branch numbering with prefix + year + padding, gapless option.',
            array('Define a sequence per document type (JV, PV, RV, INV, PO, RFQ, DO…) and branch.'),
            array('Numbers are assigned automatically when documents are created.'),
            'No posting; ensures legal sequential numbering.',
            array('Gapless sequences satisfy tax-authority invoice-numbering rules.'));

        $g['closing'] = $E('closing', 'Fiscal periods & year-end closing',
            'Period locks, P&L close to retained earnings, opening-balance carry-forward.',
            array('Define fiscal periods/year.'),
            array('Lock past periods → at year-end, run the close (P&L → retained earnings) → opening balances carry forward → reopen only with audit trail if needed.'),
            'Closing entry moves net profit to retained earnings; new year opens with carried balances.',
            array('Locking prevents back-dated posting into closed periods.'));

        $g['consolidation'] = $E('consolidation', 'Consolidation',
            'Roll up multiple branches/BUs/companies into one P&L + balance sheet.',
            array('Map entities and the group base currency.'),
            array('Run consolidation → inter-branch eliminations + currency translation → consolidated statements.'),
            'Eliminations and translation adjustments at group level only.',
            array('Use for multi-company groups and multi-branch roll-ups alike.'));

        $g['budgeting'] = $E('budgeting', 'Budgeting',
            'Budgets by account/branch/period with budget-vs-actual.',
            array('Enter budgets per account/branch/period.'),
            array('Review budget-vs-actual variance on the dashboard during the period.'),
            'No posting; comparison only.',
            array('Combine with cost centres for departmental budgets.'));

        $g['cost_accounting'] = $E('cost_accounting', 'Cost accounting',
            'Cost centres and allocations beyond landed cost.',
            array('Define cost centres and allocation rules.'),
            array('Tag costs to centres → allocate shared costs → analyse cost-centre P&L.'),
            'Reclassifies/allocates costs across centres.',
            array('Useful for departments, projects and product lines.'));

        $g['rebate'] = $E('rebate', 'Rebate management',
            'Supplier/customer rebate accruals on volumes.',
            array('Define rebate agreements (thresholds, %).'),
            array('System accrues rebates as volumes build → settle/claim at period end.'),
            'Accrues rebate receivable/payable; settles to cash.',
            array('Tier rebates by cumulative volume.'));

        $g['eam'] = $E('asset_maint', 'Enterprise asset maintenance (EAM)',
            'Maintenance schedules and work orders on assets.',
            array('Register assets and maintenance plans.'),
            array('Generate preventive work orders on schedule → record labour/parts → close.'),
            'Maintenance cost expensed (or capitalised if an upgrade).',
            array('Distinct from Fixed Assets depreciation — this is upkeep.'));

        $g['manufacturing'] = $E('manufacturing', 'Manufacturing',
            'BOM, work orders, material issue and finished-goods costing.',
            array('Create BOMs and routings.'),
            array('Open a work order → issue materials → record output (FG receipt) → cost rolls up from materials + making.'),
            'WIP +, materials −; on completion FG +, WIP −.',
            array('For jewellery, making charges capitalise into the finished piece.'));

        $g['aftersales'] = $E('aftersales', 'After-sales (RMA / warranty / service)',
            'Returns, warranty tracking and service jobs.',
            array('Define warranty terms and RMA reasons.'),
            array('Log an RMA → inspect → repair/replace/refund → track warranty status; customers see status in the portal.'),
            'Returns post credit notes / replacement stock movements.',
            array('Surface warranty status on the customer portal.'));

        $g['projects'] = $E('projects', 'Projects & timesheets',
            'Project budgets, timesheets and project accounting.',
            array('Create projects/phases and rates.'),
            array('Log timesheets and costs to projects → bill progress → track % complete and project P&L.'),
            'Costs and revenue tag to the project; WIP for unbilled.',
            array('Combine with contracts for milestone billing.'));

        $g['contracts'] = $E('contracts', 'Contracts management',
            'Customer/supplier contracts, milestones and recurring/subscription billing.',
            array('Record contracts, terms and milestones/recurrence.'),
            array('Auto-generate recurring invoices → track milestones → renewals/expiry alerts.'),
            'Recurring invoices post AR/Revenue on schedule.',
            array('Use for AMC, rentals and subscriptions.'));

        $g['pricing'] = $E('pricing', 'Price lists, promotions & loyalty',
            'Customer/qty-break price lists, promotions and loyalty points.',
            array('Create price lists (customer-specific, qty breaks, date-effective) and promotions.'),
            array('Prices resolve automatically on quotes/orders/storefront → loyalty points accrue on payment.'),
            'Discounts reduce revenue; loyalty is a liability until redeemed.',
            array('Storefront and ERP share one price-list engine.'));

        $g['hr'] = $E('hr', 'HR, payroll, attendance & leave',
            'Employees, payroll runs, attendance, leave and expense claims.',
            array('Add employees, salary structures, leave policies.'),
            array('Record attendance/leave → run payroll → post salaries → pay → employees claim expenses.'),
            'Payroll: salary expense +, payable +; payment clears payable.',
            array('Payroll-only tenants get exactly this module via entitlements.'));

        $g['expense'] = $E('expense', 'Expense management',
            'Employee expense claims and approvals.',
            array('Define expense categories and approval limits.'),
            array('Employee submits claim → approve → reimburse.'),
            'Expense +, employee payable +; reimbursement clears it.',
            array('Attach receipts via document attachments.'));

        $g['fixed_assets'] = $E('fixed_assets', 'Fixed assets',
            'Asset register, depreciation (straight-line/declining), disposals/revaluation.',
            array('Create asset categories with depreciation method/life.', 'Register assets with cost and in-service date.'),
            array('Run periodic depreciation → handle disposals/revaluations → reconcile the register to the GL.'),
            'Depreciation: expense +, accumulated depreciation +. Disposal posts gain/loss.',
            array('Keep the register tied to the GL with the audit tie-out check.'));

        $g['dashboard'] = $E('dashboard', 'Dashboards & KPIs',
            'Animated executive dashboard and per-module KPI tiles + charts.',
            array('Nothing to set up — reads your live, tenant-scoped data.'),
            array('Open the ERP dashboard → KPI tiles count up (cash, sales, AR/AP, stock) and charts animate (sales trend, ageing, cash flow, expenses).'),
            'Read-only aggregates.',
            array('Tiles are entitlement-aware — you see only the modules you run.'));

        $g['migration'] = $E('migration', 'Data migration toolkit',
            'Onboard from old systems: opening balances, masters and open documents with validation, dry-run and rollback.',
            array('Pick a source template (Tally, QuickBooks, Zoho, SAP B1, Excel) and map columns.'),
            array('Load a batch → validate (type/duplicate/FK, opening TB must net to zero) → dry-run → commit → rollback if needed.'),
            'Opening balances post once, balanced; reversible per batch.',
            array('Match on natural keys (item/customer code) so re-runs don\'t duplicate.'));

        $g['integration'] = $E('integration', 'Integration & API',
            'Inbound REST API (per-tenant keys + scopes), outbound signed connectors with retry, internal event bus.',
            array('Create an API key with scopes for an external app.', 'Configure outbound connectors (endpoint, secret).'),
            array('External systems read/write via the API → outbound events post to portals/gateways with retry → internal events link modules.'),
            'No GL impact; orchestration layer.',
            array('Keys + logs live in your own DB — tenant-isolated.'));

        $g['ecommerce'] = $E('ecommerce', 'E-commerce ↔ ERP bridge',
            'Connects the ecomae storefront to the ERP: web order → SO → DO → invoice, live stock/price, customer portal.',
            array('Enable the bridge and map the storefront catalog to ERP items.'),
            array('A web order auto-creates SO → DO → Tax Invoice → GL + stock deduction; storefront shows live stock + price-list price; B2B credit-limit-aware checkout; customers self-serve in My Account (orders, invoices, statements, loyalty, returns).'),
            'Web sale posts like any sale: AR/Cash +, Revenue +, VAT +, Inventory −, COGS +.',
            array('Clients can buy e-commerce + e-invoice only (no full ERP) via entitlements.'));

        $g['control'] = $E('control', 'Control tiers (user / admin / provider)',
            'Three levels of control over the platform.',
            array('Users set preferences (language, default branch/warehouse, landing dashboard).', 'Tenant admin manages modules, roles, org, sequences, periods, tax profile, price lists, workflows.', 'You (provider) provision tenants, assign plans, set expiry, suspend/reactivate, push compliance.'),
            array('User: personalise home. Admin: run the company console. Provider: run the fleet console (provision/suspend/overview) per-tenant.'),
            'No posting; governs access and lifecycle.',
            array('Provider console connects to each tenant DB one at a time — no cross-tenant data.'));

        $g['roles'] = $E('roles', 'Roles & permissions',
            'Granular module.action permissions, multi-role union, per-user checks.',
            array('Create roles and grant permissions (exact, module.*, or * superadmin).', 'Assign roles to users.'),
            array('Permissions are enforced on every action; users see only what they may do.'),
            'No posting; security layer.',
            array('Use module.* wildcards for department heads.'));

        $g['workflow'] = $E('workflow', 'Workflow & approvals',
            'Approval chains and inter-department messaging.',
            array('Define approval rules (e.g. PO > threshold needs manager).'),
            array('Documents route for approval → approvers act → messaging threads keep context.'),
            'No posting; gates document progression.',
            array('All intra-tenant — no global inbox across tenants.'));

        $g['localization'] = $E('localization', 'Country localization (one setting localizes the ERP)',
            'The tenant country is the master switch. Set it once on the company profile and the whole ERP localizes: currency, language + text direction (RTL/LTR), tax regime (label + rate + e-invoice scheme), fiscal-year start, date format and the HR labour-law pack — all from one source.',
            array('Open Company Profile → set Country. (Pakistan → PKR/Urdu/Sales Tax/Jul-Jun/FBR; UAE → AED/Arabic/VAT 5%/FTA; KSA → SAR/Arabic/VAT 15%/ZATCA.)', 'Optionally override currency/language for special cases.'),
            array('Every module reads the country profile automatically — invoices use the right tax label and rate, payroll uses the right gratuity/leave rules, reports use the right fiscal year, and the UI shows the right language and direction.', 'Cross-border staff or documents can override per-record.'),
            'No posting itself; it parameterizes tax, payroll and fiscal calendar so all figures follow the tenant country.',
            array('Set the country before transacting so document numbering and fiscal periods start correctly.'));

        $g['hr_law'] = $E('hr_law', 'HRMS — country labour law (gratuity, leave)',
            'Applies each tenant\'s statutory HR rules automatically by country: end-of-service gratuity, annual-leave entitlement, leave salary, notice/probation and overtime. Date-effective, so a law change takes effect from its date while past settlements keep the old rule.',
            array('Set the company country (company profile) — it selects the labour-law pack (UAE, KSA, Qatar, Oman, Bahrain, Kuwait, India, or generic).', 'Optionally override country per employee for cross-border staff.'),
            array('On an employee exit, run gratuity → e.g. UAE pays 21 days basic/year for the first 5 years, 30 days/year beyond, capped at 2 years\' pay; KSA uses half/full month with the resignation factor; India uses (15/26) × wage × years after 5 years.', 'Accrue annual leave (UAE: 2 days/month for months 6-12, then 30 days/year) and pay leave salary on basic.', 'Compute overtime per country (UAE 125% normal, 150% night/rest day).'),
            'Gratuity, leave salary and overtime post to payroll/GL; figures follow the country pack in force on the settlement date.',
            array('Keep basic salary correct — gratuity & leave salary are based on basic, not gross.'));

        $g['mrp'] = $E('mrp', 'MRP & demand planning',
            'Material Requirements Planning (SAP MRP equivalent): nets demand against stock and incoming supply and proposes planned purchase/production orders.',
            array('Maintain reorder qty (lot size), safety stock and lead time per item.', 'Enter or import demand (sales orders, forecast).'),
            array('Run MRP → it computes net requirement = demand − (on-hand − safety) − on-order, rounds up to the lot size, and lists planned purchase orders (buy items) and planned production orders (make items) with lead times.', 'Convert planned orders to real POs / work orders.'),
            'Planning only — postings happen when the resulting PO/work order is processed.',
            array('Set safety stock to avoid st-outs; set lot size to match supplier MOQ.'));

        $g['intercompany'] = $E('intercompany', 'Intercompany & consolidation',
            'Multi-company postings with automatic elimination (SAP AA/consolidation equivalent), plus available-to-promise and asset depreciation.',
            array('Set up your companies/branches and the financial year.', 'Define intercompany control accounts.'),
            array('Post an intercompany transaction → balanced entries are created in both companies and tagged.', 'Run consolidation → tagged intercompany balances are eliminated so the group view isn\'t double-counted.', 'Asset depreciation schedules (straight-line / reducing-balance) post monthly; disposals book gain/loss.', 'Sales orders run an Available-to-Promise check (on-hand − reserved + incoming) before confirming.'),
            'Depreciation and disposals post to the GL; intercompany pairs net to zero on consolidation.',
            array('ATP prevents promising stock you don\'t have; consolidation keeps the group P&L clean.'));

        $g['datalink'] = $E('datalink', 'Live data-link (storefront → ERP)',
            'Pulls your existing e-commerce/CRM data — customers, orders, catalogue, stock and the native customer ledger — straight into the ERP so dashboards, AR and the document chain reflect real shop activity. Read-only on your storefront tables.',
            array(
                'Open ERP → Data-link. It auto-detects your native tables (shop_orders, users, catalogue, storages, accounting).',
                'Review the coverage report (native rows vs linked) before syncing.',
            ),
            array(
                'Click "Sync orders" → each native web order is linked into the ERP bridge (idempotent — re-running never duplicates).',
                'Customers appear in CRM/AR from users + profiles; receivables come from the native customer ledger.',
                'The ERP sales dashboard now shows live revenue, paid vs AR-outstanding, gross margin and top products from real orders.',
            ),
            'No new postings on link; once an order is advanced it posts like any sale (AR/Cash +, Revenue +, VAT +, Inventory −, COGS +).',
            array('Linking is keyed on shoporder:<id> so a re-sync updates in place — safe to run repeatedly.',
                'It never writes to your storefront tables; only the ERP bridge map is written.'));

        $g['demo'] = $E('demo', 'Demo / sample data',
            'Isolated, realistic multi-industry sample data (jewellery, trading, construction, retail/POS, manufacturing) to preview the ERP and power the marketing live-demo — never mixed with live tenant data.',
            array('Pick an industry profile to load a demo dataset into an isolated demo space.'),
            array('Explore dashboards and the full document chain with realistic numbers; reset any time.'),
            'Demo only — no impact on any live tenant ledger.',
            array('Use it to train new staff before going live with real data.'));

        $g['mobile'] = $E('mobile', 'Mobile (Android & iOS / PWA)',
            'Use the storefront, tenant CP and ERP on phones: installable PWA (home-screen icon, offline shell, push-ready) plus an optional native Android/iOS wrapper, all driven by the secure mobile REST API.',
            array(
                'Enable the PWA for the tenant (adds manifest + service worker; nothing auto-published).',
                'For app stores, build the native wrapper and submit with your Apple Developer / Google Play accounts.',
                'Issue a scoped API key for the mobile app (see Integration & API).',
            ),
            array(
                'On a phone, open the site → "Add to Home Screen" to install; launch like an app, works offline for cached views.',
                'Native app: native push + camera/barcode scan for POS and stock counts.',
            ),
            'No GL impact; presentation + access layer over the existing API.',
            array('The crypto/fintech theme is responsive and RTL-aware, so the mobile UI flips correctly per language.'));

        // ---- BOS pillars (Phase 2) ----
        $g['bos_compliance'] = $E('compliance', 'Compliance center (BOS pillar)',
            'A config-driven obligations engine: tracks your filing obligations (VAT, corporate tax, ESR, etc.), a live filing calendar with due dates and status, and document-retention rules — all per tenant, seeded from your region and fully editable.',
            array(
                'Open Finance → Compliance center. Default obligations and retention rules are seeded once from your country/region.',
                'On the Obligations tab, add/disable obligations and set frequency, lead days and required documents.',
                'On Document retention, set retention years/basis/legal reference per document type.',
            ),
            array(
                'Filing calendar projects each period with a due date and status (open / due soon / overdue / filed).',
                'When a return is submitted, click "Mark filed" with a reference; the period drops out of the overdue/due-soon counts.',
            ),
            'No direct GL postings — a governance layer that tracks obligations and evidence alongside the ledger.',
            array('Defaults are starting points: every obligation, due date and retention rule is editable per tenant, nothing is hard-coded.'));

        $g['bos_approvals'] = $E('workflow', 'Approvals engine (BOS pillar)',
            'A reusable approval & sign-off engine across documents: define threshold rules (e.g. "PO ≥ 10,000 needs Manager approval"), and high-value sales orders, purchase orders and payment vouchers are routed for multi-step approval with a full immutable audit trail.',
            array(
                'Open Overview → Approvals → Rules. Add a rule: pick the document type, condition (amount ≥ / > / any) and threshold, then one or more approver roles (the steps).',
                'Use the Test rule tab to raise a sample request and confirm routing.',
            ),
            array(
                'Raise SO/PO/payment vouchers as normal — if a rule matches, an approval request is raised automatically.',
                'Approvers act on the Approval queue (Approve/Reject with a comment); multi-step rules advance to the next approver.',
                'History & audit shows every decision with actor, timestamp and comment.',
            ),
            'No GL impact itself; gates the documents that do post, so unauthorised high-value transactions are caught before completion.',
            array('Rules are per tenant and per document type — thresholds and approver chains are fully configurable, never hard-coded.'));

        $g['bos_intel'] = $E('dashboard', 'Industry intelligence (BOS pillar)',
            'Operational KPIs and recommended controls driven by your industry profile/pack: live financial KPIs (gross margin %, DSO, DPO, inventory turnover, current ratio) plus a best-practice control checklist tailored to your industry.',
            array(
                'Apply an industry pack in Accounting setup to unlock specialised controls (generic controls always apply).',
                'Open Insights → Industry intelligence. KPIs compute live for the selected From/To period.',
            ),
            array(
                'Review the KPI tiles (colour-coded against benchmarks) for the chosen period.',
                'Work the recommended-controls checklist; tick the controls you have in place — the checklist is saved per tenant.',
            ),
            'No GL impact; reads live GL, aging and inventory data to surface KPIs and control gaps.',
            array('KPIs and controls are selected by the active industry pack, so the same engine serves 50–100 industries without hard-coding.'));

        $g['order_planning'] = $E('inventory', 'Order planning — demand-driven replenishment',
            'A demand-driven replenishment engine over your inventory ledger: forecasts demand from sale-out history, computes safety stock and reorder point per item × warehouse, and recommends order quantities — plus ABC/XYZ inventory policy, inter-warehouse redistribution, exceptions/alerts and stock-analysis KPIs.',
            array(
                'Open Operations → Order planning. Tables auto-create on first open; nothing to configure.',
                'On any item worksheet, set the planning parameters (lead-time days, target service level, review period, min order qty, order multiple, manual buffer, supplier) and Save — the engine recalculates immediately.',
                'No demand history yet? Click "Generate sample demand" to seed 12 months of mixed-pattern sale-out history (re-runnable; tagged DEMO-DEMAND, clearable).',
            ),
            array(
                'Step 1 — Open the Recommended orders grid. Result: each item × warehouse shows forecast/month, lead-time demand, safety stock, reorder point (order level), recommended order qty (ROQ), days-of-cover, value, demand class (smooth/erratic/intermittent/lumpy) and status. Lines that need an order are highlighted.',
                'Step 2 — Open an item worksheet (click the item). Result: a 12-month demand chart, stock balances (on-hand / effective / shortfall / excess), the computed safety stock, ROP and ROQ, and the editable planning-parameter form.',
                'Step 3 — Confirm orders. Result: click Confirm on a line (or "Confirm all due") and the recommendation status flips to confirmed, ready to raise as a PO.',
                'Step 4 — Inventory policy (ABC/XYZ). Result: items are classified A/B/C by cumulative annual value and X/Y/Z by demand variability, each with a recommended service level that drives safety stock.',
                'Step 5 — Redistribution. Result: suggested inter-warehouse transfers (move excess in one branch to cover a shortfall of the same item in another) before raising a PO.',
                'Step 6 — Exceptions & alerts. Result: a severity-ranked list — stock-out risk, below-safety, dead stock, excess.',
                'Step 7 — Stock analysis & KPIs. Result: inventory value, inventory turns, average days-of-cover, fill rate and the ABC distribution.',
            ),
            'Planning only — no GL postings. Recommendations become real postings when you raise and receive the resulting purchase order.',
            array('Set the target service level per item: higher service level → more safety stock → fewer stock-outs but more capital tied up.',
                'Demand class tells you the forecastability: "smooth" items forecast well; "lumpy/erratic" items need more buffer.',
                'Use redistribution first — moving stock between your own warehouses is cheaper than buying more.'));

        $g['process_flow'] = $E('core', 'Process flow — workflow automation & live tracking',
            'Define any business process as a chain of steps that auto-routes from one person/department head to the next, then track where every case has reached on a GPS-style map, see the whole organization\'s flow, and view your entire team\'s workload — all with staff photos. Real ERP work tracks itself too: customer orders, purchase orders, supplier payments and staff expense claims each auto-create a case and advance through their stages automatically, so you can measure every employee by the actual tasks they handled.',
            array(
                'Open Overview → Process flow → Processes → Create process (e.g. "Customer credit request", "Goods delivery to customer"). Tables auto-create on first open; a brand-new tenant starts empty.',
                'On the process click Manage steps → Add step; for each step set the step name, Routes to (Specific person / Department head / Anyone in department / Back to initiator), Department and SLA hours. Steps run top-to-bottom.',
                'New tenant with no data? Click Seed to create demo staff across departments & branches plus example processes and running cases, including auto-tracked customer orders, purchase orders, supplier payments and expense claims (clearable via Clear).',
                'Click Sync to backfill the auto-tracked task types from your real records — it reports how many customer orders, purchase orders, supplier payments and expense claims are now tracked.',
            ),
            array(
                'Step 1 — Start a case on a process and click "Approve & route to next". Result: the case auto-hands off to the next step\'s person/department head; SLA/overdue is tracked.',
                'Step 2 — Monitor tab → open a case. Result: a GPS-style tracker (completed stops, pulsing "you are here" marker, staff photo at each step, full audit timeline) with a Zoom switch — Overall / Location / Department / Task.',
                'Step 3 — Org map tab. Result: every process flows through nodes with animated arrows and live case counts; switch View level (Overall / Legal entity / Business unit / Department / User / Task / Location). At User level each employee\'s photo and case count show inside their department node; the left list of cases is searchable/sortable and each opens its document.',
                'Step 4 — Workforce tab. Result: the entire team in one view with photos — who is busy and on which task plus tasks completed; group by Department / Location / Task / Business unit / Legal entity and filter by busy/idle; click a person to open their case. A top-performers leaderboard ranks staff by tasks managed.',
                'Step 5 — Hierarchy tab. Result: an expandable org tree (Legal entity ▸ Business unit ▸ Department ▸ Location ▸ Employee) with rolled-up open and completed task counts at every level, so you can drill from the whole company down to one person.',
                'Step 6 — Scope by date. Result: add &from=YYYY-MM-DD&to=YYYY-MM-DD to the URL (or use the date range) and the Monitor, Workforce, Org map and performance counts honor that reporting period, with a banner and Clear button.',
            ),
            'No GL impact — an operational workflow/tracking layer over staff, departments and locations.',
            array('Define routing per step: "Department head" lets work move without naming an individual; "Specific person" pins it to one employee.',
                'Use the Org map User/Workforce views to balance load — see who is overloaded vs idle at a glance.',
                'Auto-tracked task types (orders, POs, supplier payments, expense claims) advance on their own as the underlying record changes status — no manual approval needed — and each completed step is credited to the employee who acted, feeding the performance leaderboard.',
                'Scope any view to a reporting period with the date range to run a fair, period-bound performance review.',
                'Upload a staff photo on the employee profile and it replaces the generated avatar everywhere on the map.'));

        $g['hr_law'] = $E('core', 'Labour law & compliance — worldwide statutory engine',
            'A country-aware employment-law engine that localizes the statutory rules to your company country — working hours, overtime, probation cap, notice, annual/sick/maternity/paternity leave, public holidays, end-of-service and wage protection — across 25+ countries (UAE + GCC, South Asia, MENA, Europe, Americas, APAC, Africa) with a safe generic fallback. It also runs every employee through those rules and flags issues plus the accrued end-of-service liability.',
            array(
                'Open People → Labour law & compliance. The active country is taken automatically from the Company profile (Setup → Company); change the company country to switch the whole rule-set.',
                'Nothing to configure — it reads your existing HR/staff records (hire date, basic salary, leave balance) to compute entitlements and compliance.',
            ),
            array(
                'Step 1 — Read the statutory card for your company country. Result: working week, overtime rates, probation cap, notice, annual/sick/maternity/paternity leave, public holidays, end-of-service basis, wage-protection scheme (e.g. WPS) and the governing authority.',
                'Step 2 — Look up any country. Result: pick a country from the dropdown to preview its statutory rules; use the worldwide reference table (with filter) to compare jurisdictions.',
                'Step 3 — Employee compliance monitor. Result: every employee is checked against the company-country rules — probation status (with end date), excess leave balance, missing data, and accrued end-of-service liability — each finding cites its statutory basis.',
                'Step 4 — Read the KPI strip. Result: employees checked, in probation, "needs attention" count, and total end-of-service liability to provision.',
            ),
            'No GL postings — a compliance/advisory layer. End-of-service amounts feed your provisioning; book them via Payroll / journals.',
            array('It is auto-localized: a UAE tenant sees UAE Federal Decree-Law 33/2021 rules; a Saudi tenant sees KSA Labor Law; an India tenant the Payment of Gratuity Act, etc.',
                'End-of-service shows an accrued liability only where the jurisdiction uses accrual-based gratuity (GCC, India, Pakistan, etc.); severance-only jurisdictions (e.g. UK redundancy, US at-will) show the basis instead.',
                'Figures are representative statutory minimums — always confirm against current local law, collective agreements and qualified counsel before acting.'));

        $g['supplier_portal'] = $E('procurement', 'Supplier portal — performance scorecards',
            'Per-supplier performance scorecards computed from your procurement data (purchase orders, goods receipts, RFQs, payables), with a per-supplier activity drill-down.',
            array('Open Purchasing → Supplier portal. It reads your existing suppliers, POs and RFQs — nothing to set up.'),
            array(
                'Step 1 — Review the scorecard grid. Result: every active supplier shows a composite score (0–100) and A–D rating, # of POs, total spend, on-time delivery %, average delivery lead time, RFQ response %, RFQ win % and open payable balance.',
                'Step 2 — Click a supplier. Result: a detail view with the score breakdown, recent RFQs, and the full purchase-order list (with per-PO delivery lead time).',
            ),
            'No GL impact; an analytics layer over purchasing and payables.',
            array('The composite score weighs on-time delivery 40%, RFQ responsiveness 30%, purchasing activity 20% and RFQ win rate 10%.',
                'On-time and lead-time metrics populate once POs are received with approved/received dates recorded.'));

        $g['exec_dashboard'] = $E('dashboard', 'Executive dashboard — cross-module KPI cockpit',
            'A single executive cockpit that pulls cross-module KPIs into one view: revenue, gross margin, DSO/DPO, inventory turnover, current ratio, AR/AP/cash/inventory, a 6-month revenue & profit trend, planning alerts and top suppliers.',
            array('Open Insights → Executive dashboard. It reads your live, tenant-scoped data — nothing to configure.'),
            array(
                'Step 1 — Read the KPI cards. Result: ten headline KPIs, each colour-coded green/amber/red against benchmarks (e.g. gross margin, DSO, inventory turns, current ratio).',
                'Step 2 — Read the trend. Result: a 6-month revenue vs profit bar chart shows the direction of the business.',
                'Step 3 — Read planning alerts & top suppliers. Result: counts of stock-out/below-safety/excess/dead-stock exceptions and your highest-spend suppliers, with quick links to the underlying modules.',
                'No sales history yet? Click "Generate sample sales" to seed 6 months of completed orders (tagged, clearable) so the revenue KPIs and trend populate.',
            ),
            'Read-only aggregates; no GL postings.',
            array('KPIs are tenant-scoped, so a new tenant starts empty and fills in as real transactions are posted.'));

        $g['ext_reporting'] = $E('core', 'External Reporting — statutory returns (VAT, CT, AML & more)',
            'A country-driven statutory reporting centre that auto-builds filing-ready returns from your ERP data, localized to your registration country (UAE by default: FTA · MOHRE · goAML · MoEC). It covers the full FTA VAT 201, the full Corporate Tax computation with schedules, IFRS financial statements, WPS, goAML SAR/STR, ESR, UBO, CbCR and more — each with the official format, full sample data so nothing is ever blank, in-place drill-down to transaction level, a compliance engine, a professional colour PDF, and an off-system Excel import tool for checking other clients.',
            array(
                'Open External Reporting → Report centre. The registration country is taken automatically from the Company profile (Setup → Company) — it drives the format, authority links and compliance rules.',
                'Pick a report (e.g. UAE VAT Return, Corporate Income Tax Return) and a reporting period (basis: Monthly / Quarterly / Annual / Custom range), then click Run / Recalculate.',
                'Nothing else to configure — figures come from posted GL/transactions for the period; periods with no postings fall back to period-seeded sample data so the full return always renders.',
            ),
            array(
                'Step 1 — Choose the report + period. Result: the document rebuilds for that exact period and stamps "Reporting period: <from> — <to>" in the header.',
                'Step 2 — Drill down in place. Result: click any figure to expand its source — a box/line opens its breakdown, an item opens its invoices/entries, and a single invoice opens its supply/GL lines, recursively down to individual transaction level.',
                'Step 3 — Review compliance. Result: the built-in engine validates every figure against the law (e.g. VAT reverse-charge / 24kt gold 0%; CT add-backs, interest cap, loss relief) and shows PASS / REVIEW / FAIL.',
                'Step 4 — Download supporting schedules. Result: one-click Excel/CSV audit files (VAT: invoice-wise, TRN-wise, supplier-wise, adjustments; CT: adjustments, fixed-asset, exempt income, related-party, losses, FTC).',
                'Step 5 — Print / PDF. Result: a professional, colour, MIS-style filing pack with a cover page, the summary schedules and a plain-language commentary — invoice-level detail is excluded so a 10k-invoice tenant gets a clean summary.',
                'Step 6 — Off-system import (other clients). Result: open the Import tool → download the multi-sheet Excel template (Company & TRN, boxes/lines, invoice detail, compliance, instructions) → fill it → upload → it builds the full VAT/CT return + PDF entirely outside the ERP.',
            ),
            'No GL postings — a read/format layer over posted data (plus a fully off-system mode for the Excel import).',
            array('Everything is tenant-country-driven: a UAE tenant gets the FTA/UAE format automatically; the jurisdiction selector is preview/look-up only.',
                'Each report has its own statutory period model (VAT monthly/quarterly, CT financial year, WPS monthly, etc.) — change the basis at the top to match your filing.',
                'The PDF is summary-only by design; use the Excel/CSV downloads when you need the full invoice-level audit file.'));

        $g['vat_return'] = $E('core', 'VAT Return (FTA VAT 201) — guide',
            'The complete UAE FTA VAT 201: output VAT on sales (Boxes 1a–1g standard-rated per Emirate, Box 2 tourist refunds, Box 3 reverse charge, Box 4 zero-rated, Box 5 exempt, Box 6/7 imports, Box 8 totals) netted against recoverable input VAT (Box 9 expenses, Box 10 reverse-charge recoverable, Box 11 totals) to the net payable/refundable (Box 12/13/14). Includes special-scheme handling (investment-gold 0%, B2B gold/diamond reverse charge, profit-margin scheme), group/intercompany VAT, a field guide, a "Report explained" commentary and a compliance engine.',
            array(
                'Open External Reporting → UAE VAT Return. Set the basis (Monthly or Quarterly are the usual VAT filing cycles) and the period, then Run / Recalculate.',
                'Make sure the Company profile carries your Tax TRN and address — they print on the return; for a tax group, record the group TRN and members.',
            ),
            array(
                'Step 1 — Read the field guide ("what goes in each box and why") and the commentary so you understand how output VAT − input VAT = net due.',
                'Step 2 — Work each box top to bottom; click a box to drill to its invoices, then an invoice to its supply/GL lines (transaction level). The invoice list reconciles to the box total.',
                'Step 3 — Check the special schemes are right: 24kt investment gold 0%, B2B gold/diamonds reverse charge, exports 0%, residential lease exempt — the compliance panel flags any line taxed incorrectly.',
                'Step 4 — Confirm it reconciles: Box 12 (output) − Box 13 (input) = Box 14 (net payable / refundable).',
                'Step 5 — Download the audit files (invoice-wise, TRN-wise, supplier-wise, adjustments) and/or Print the summary PDF for filing.',
            ),
            'No GL postings — reads output/input VAT from posted sales & purchases for the period.',
            array('VAT is not always quarterly — monthly filers just switch the basis to Monthly.',
                'Reverse-charge and import VAT appear on both the output and input side and net to zero — that is correct.',
                'Intra-group (intercompany) supplies under one tax group are disregarded and excluded from the return.'));

        $g['ct_return'] = $E('core', 'Corporate Tax Return (Federal Decree-Law 47/2022) — guide',
            'The full UAE Corporate Tax return: taxpayer & period, elections/reliefs (Small Business Relief, QFZP 0%/9%, realisation basis, group transfers), the computation that reconciles accounting net profit to taxable income (add-backs: fines 100%, entertainment 50%, donations, provisions, accounting depreciation; deductions: tax depreciation, exempt dividends/participation; interest limitation 30% EBITDA / AED 12m; tax-loss relief 75% cap), then 0% up to AED 375,000 and 9% above. Includes six supporting schedules, tax-group/intercompany handling, a field guide, a "Report explained" commentary and a compliance engine.',
            array(
                'Open External Reporting → Corporate Income Tax Return. Set the basis to Annual (or Custom range for a non-calendar / transitional first year) and pick the financial year, then Run / Recalculate.',
                'Ensure the Company profile has the CT TRN, legal form and address — they print on the return.',
            ),
            array(
                'Step 1 — Read the field guide and the commentary to see how accounting profit becomes taxable income through statutory adjustments.',
                'Step 2 — Review Section 2 elections/reliefs (e.g. Small Business Relief eligibility, free-zone status) — these change the whole computation.',
                'Step 3 — Work Section 3 line by line; click any line to drill to its breakdown (e.g. depreciation by asset class), then a sub-line to reach the individual asset or journal entry (transaction level).',
                'Step 4 — Check Section 4 bands: 0% on the first AED 375,000, 9% above, giving CT payable.',
                'Step 5 — Expand Schedules 1–6 (adjustments, fixed-asset depreciation, exempt income, related-party/TP, tax losses, foreign tax credit) and download them as Excel/CSV.',
                'Step 6 — Clear the compliance panel (registration, non-deductibles, interest cap, exempt income, loss relief, transfer pricing) and Print the summary PDF.',
            ),
            'No GL postings — reconciles posted accounting profit to taxable income; figures map from tagged GL accounts and the fixed-asset / related-party registers.',
            array('Different companies have different financial years — use Custom range for a non-December year-end or a transitional first period.',
                'A CT Tax Group is one taxable person: intra-group (intercompany) transactions are eliminated on consolidation.',
                'Related-party transactions trigger a transfer-pricing review item — keep your master/local file and disclosure form.'));

        $g['audit_report'] = $E('core', 'External Audit Report (ISA 700, IFRS) — guide',
            'A complete IFRS assurance pack: a professional cover page + table of contents, the Independent Auditor\'s Report (opinion, basis for opinion, going concern, key audit matters and responsibilities under ISA 700/701/705/570/720), and the full set of IFRS financial statements with prior-year comparatives — Statement of Financial Position, Statement of Profit or Loss & Other Comprehensive Income, Statement of Changes in Equity and Statement of Cash Flows — followed by detailed notes referencing each IAS/IFRS standard. Period-aware, with drill-down to transaction level, a field guide, commentary, per-line IFRS references and a Fetch button to refresh against the live IFRS/ISA source.',
            array(
                'Open External Reporting → External Audit Report. The reporting framework and auditor register are taken from the Company profile country (UAE → SCA/IFRS + MoE-registered auditor; auto-localizes elsewhere).',
                'Set the basis to Annual and pick the financial year, then Run / Recalculate. Figures map from tagged GL accounts; a period with no postings falls back to a fully reconciling sample so every statement renders.',
            ),
            array(
                'Step 1 — Read the cover page and table of contents, then the Independent Auditor\'s Report (opinion → basis → going concern → key audit matters → responsibilities).',
                'Step 2 — Work the four primary statements; each shows the reporting period beside the comparative period, and every line carries its IAS/IFRS reference. The statements cross-foot (assets = equity + liabilities; SOCE rolls forward; cash flow reconciles).',
                'Step 3 — Drill any figure down to its source — line → ledger account → individual journal/transaction.',
                'Step 4 — Read the notes to the accounts (accounting policies + disclosures) and the "Report explained" commentary so a learner understands the pack.',
                'Step 5 — Use the Fetch button to refresh the standards references, then Print the professional colour PDF.',
            ),
            'No GL postings — a read/format/assurance layer over posted balances; localizes to the tenant country.',
            array('Tenant-country-driven: the framework, auditor oversight authority and presentation default from the company country.',
                'The four primary statements and the notes are all required under IFRS — the pack includes them with prior-year comparatives so nothing is missing.'));

        $g['fin_import'] = $E('core', 'IFRS financial statements — off-system Excel import — guide',
            'An off-system tool (like the VAT/CT import) to build a ready audit/IFRS report for any client without touching the ERP. Download a multi-sheet workbook (Instructions · Company & details/TRN · Financial data with Current + Prior columns · Notes inputs · Compliance checklist), fill it, upload it, and it produces the full Audited Financial Statements pack — cover page, Independent Auditor\'s Report, all four primary statements with comparatives, notes and compliance checks — stamped with the client\'s own TRN/address.',
            array(
                'Open External Reporting → Import tool → choose "IFRS Financial Statements & Audit Report".',
                'Download the .xlsx workbook (or CSV). Keep the Code column unchanged; enter Current-year and Prior-year amounts. Off-system: nothing is read from or written to your ERP.',
            ),
            array(
                'Step 1 — Fill the Company & details sheet (legal name, TRN, address, period, auditor) and the Financial data sheet (revenue, costs, assets, liabilities, equity — current + prior).',
                'Step 2 — Optionally complete the Notes inputs and Compliance checklist sheets.',
                'Step 3 — Save as .xlsx (or .csv) and upload it under "Build return".',
                'Step 4 — Review the built pack: it shows the client\'s TRN/address, all statements with comparatives, notes, and a compliance panel (SOFP balances, SOCE rolls forward, cash flow reconciles).',
                'Step 5 — Print the professional colour PDF for the client.',
            ),
            'Fully off-system — no ERP/GL is read or written; ideal for preparing or reviewing other clients\' accounts.',
            array('Enter amounts as positive numbers; the builder applies the correct sign on each statement.',
                'Cash is set so the sample workbook balances — replace all figures with the client\'s real numbers and the compliance panel will re-check that it still balances.'));

        $g['fin_model'] = $E('core', 'Financial Model & Five-Year Forecast — guide',
            'A five-year financial model built from the live GL actuals for the selected period, with an explicit assumptions block (revenue growth, margins, capex, working capital, tax, WACC), a projected profit & loss, an EBITDA and free-cash-flow build, and key ratios. The free-cash-flow line drives the Business Valuation report so the two stay consistent.',
            array(
                'Open External Reporting → Financial Model & Forecast. Pick the base year (the latest actuals anchor the model), then Run / Recalculate.',
            ),
            array(
                'Step 1 — Read the assumptions block; every projected figure is driven by these.',
                'Step 2 — Review the projected P&L (revenue grown at the CAGR; costs scaled by their margins; tax at the corporate rate).',
                'Step 3 — Read the EBITDA & free-cash-flow build (NOPAT + depreciation − capex − ΔWC).',
                'Step 4 — Check the key ratios (gross/EBITDA/net margin, revenue CAGR, year-5 figures).',
            ),
            'No GL postings — projects forward from posted actuals; feeds the valuation.',
            array('Tenant-country-driven tax (e.g. UAE CT 0% up to AED 375k, 9% above).',
                'Change any assumption and the whole model — and the valuation — moves with it.'));

        $g['valuation'] = $E('core', 'Business Valuation Report (DCF / multiples / net assets) — guide',
            'Values the business by three methods that all draw on the same financial model: discounted cash flow (WACC discounting of projected free cash flow + a Gordon-growth terminal value), market multiples (EV/EBITDA and P/E comparables) and net assets / book value. Produces an enterprise-value → equity-value bridge with an indicative range and a central estimate, plus a field guide and commentary.',
            array(
                'Open External Reporting → Business Valuation Report. It builds on the five-year financial model for the selected base year — Run / Recalculate.',
            ),
            array(
                'Step 1 — Read the valuation summary (the three methods + the indicative range and central equity value).',
                'Step 2 — Work through the DCF: projected free cash flows are discounted at the WACC; the terminal value captures cash beyond the forecast; the sum is enterprise value, and enterprise value − net debt = equity value.',
                'Step 3 — Cross-check with market multiples (EV/EBITDA, P/E) and net assets / book value.',
                'Step 4 — Print the professional colour PDF.',
            ),
            'No GL postings — an analytical layer over the financial model.',
            array('The methods rarely agree exactly; the range and central estimate communicate the uncertainty honestly.',
                'Net debt = borrowings + lease liabilities − cash; it bridges enterprise value to equity value.'));

        $g['purchase_requisitions'] = $E('procurement', 'Purchase requisitions (req → approval → PO)',
            'Internal demand capture: staff raise a requisition, it routes for approval, then converts to a purchase order — so spend is authorised before it is committed.',
            array(
                'Set approval limits/thresholds under Procurement and sourcing → Setup so requisitions route to the right approver.',
                'Define the requesting business units / departments that can raise requisitions.',
                'Confirm the requisition number sequence (Setup → number sequences).',
            ),
            array(
                'Procurement and sourcing → Purchase requisitions → New: add lines (item, qty, need-by date, business unit).',
                'Submit for approval; the approver approves or rejects with a note.',
                'Convert the approved requisition to a purchase order in one click.',
            ),
            'No GL posting at requisition stage; commitment/accrual begins when the resulting PO is received/invoiced.',
            array('Requisitions are the control point — approve here, not after the money is spent.'));

        $g['procurement_categories'] = $E('procurement', 'Procurement categories & policies',
            'A category tree and sourcing policies that classify what you buy and apply the right rules (preferred vendors, approval routing, default accounts) by category.',
            array(
                'Procurement and sourcing → Setup → Categories: build your category hierarchy.',
                'Attach preferred/approved vendors and default purchase accounts per category.',
                'Set per-category policies (approval thresholds, catalogue restrictions).',
            ),
            array(
                'Pick a category on requisition/PO lines so policy and default accounts apply automatically.',
                'Review category spend in Procurement reports & inquiries.',
            ),
            'Categories drive the default purchase/expense account used when the PO posts.',
            array('Keep the tree shallow and meaningful — categories are for policy and analysis, not a second item master.'));

        $g['budget_planning'] = $E('budgeting', 'Budget planning & forecast positions',
            'Prepare budgets by account/dimension and period, capture forecast positions, then track actual-vs-budget as the year runs.',
            array(
                'Budgeting → Setup: define budget models, cycles and the accounts/dimensions in scope.',
                'Choose the planning period (monthly/quarterly) and base currency.',
            ),
            array(
                'Budgeting → Budget planning → enter or import budget figures by account and period.',
                'Record forecast positions (planned headcount/spend) where used.',
                'Review budget-vs-actual variance in Budgeting reports & inquiries.',
            ),
            'Budgets do not post to the GL; they are the comparison baseline for actuals in reports.',
            array('Lock the budget once approved so variance analysis is against a stable plan.'));

        $g['recruitment'] = $E('hr', 'Recruitment (requisition → applicant → hire)',
            'The hiring funnel: open a job requisition, track applicants through stages, and convert the selected candidate into an employee.',
            array(
                'Human resources → Setup: define job positions, departments and recruitment stages.',
            ),
            array(
                'Human resources → Recruitment → raise a job requisition (role, business unit, count).',
                'Add applicants and move them through the stages (screen → interview → offer).',
                'Hire the selected applicant — this creates the employee master record.',
            ),
            'No GL posting; payroll cost begins once the hired worker is paid.',
            array('Capture the requisition first so headcount is approved before offers go out.'));

        $g['performance'] = $E('hr', 'Performance management (goals & reviews)',
            'Set employee goals and run review cycles with ratings, so appraisals and development are tracked in one place.',
            array(
                'Human resources → Setup: define the review cycle (period) and rating scale.',
            ),
            array(
                'Human resources → Performance → set goals per employee.',
                'Run the review cycle: managers record ratings and comments.',
                'Review completion and rating distribution in HR reports & inquiries.',
            ),
            'No GL posting; ratings can inform payroll/increment decisions outside the ledger.',
            array('Agree goals at the start of the cycle so reviews measure against something concrete.'));

        $g['cash_forecast'] = $E('treasury', 'Cash flow forecast',
            'A forward view of cash: expected inflows (receivables) and outflows (payables, payroll, tax) by period so you can see liquidity ahead of time.',
            array(
                'Cash and bank management → Setup: confirm bank accounts and opening cash positions.',
            ),
            array(
                'Cash and bank management → Cash flow forecast: review projected closing balance per period.',
                'Adjust assumptions (collection days, planned payments) and re-run.',
            ),
            'Analytical only — no GL posting; it reads ledger balances and open items.',
            array('Use it before committing large payments so you do not run the bank balance negative.'));

        $g['bank_instruments'] = $E('treasury', 'Bank instruments (LC / bank guarantee)',
            'Track letters of credit and bank guarantees through their lifecycle (issued → amended → utilised → expired/closed) with limits and expiry reminders.',
            array(
                'Cash and bank management → Setup: record your facility limits per bank.',
            ),
            array(
                'Cash and bank management → Bank instruments → New: capture type, beneficiary, amount, issue/expiry date.',
                'Update status as the instrument is amended, utilised or closed.',
                'Watch upcoming expiries in the instruments list / Document expiry tracker.',
            ),
            'Off-balance-sheet until called; margin/charges post to the relevant bank and expense accounts when incurred.',
            array('Expiry dates feed reminders — set them accurately to avoid lapsed guarantees.'));

        $g['withholding'] = $E('tax', 'Withholding tax (codes & certificates)',
            'Withhold tax at source on applicable payments using configurable codes/rates, and produce the withholding register for filing.',
            array(
                'Tax → Setup → Withholding: create codes with rate % and the GL account to accrue the withheld amount.',
            ),
            array(
                'Tax → Withholding tax → apply a code on a payment (vendor, base amount) to compute the withheld value.',
                'Review accrued vs settled in the withholding register; export for filing.',
            ),
            'Posts the withheld portion to the configured withholding liability account; the net is paid to the vendor.',
            array('Set the code rate from the tenant country’s rules — never hard-code a single jurisdiction.'));

        $g['elec_reporting'] = $E('tax', 'Electronic reporting (statutory file formats)',
            'Define output formats (CSV/XML/JSON) for statutory/electronic submissions so generated files match the authority’s required layout.',
            array(
                'Tax → Electronic reporting → New format: set code, name, output type, and root/row elements.',
            ),
            array(
                'Pick a format and generate the file for the period; download and submit to the authority/portal.',
            ),
            'No GL posting; it transforms ledger/return data into the required file layout.',
            array('Keep one format per authority requirement so generated files stay submission-ready.'));

        $g['report_center'] = $E('core', 'Reports & inquiries (per-module report center)',
            'Every module has its own “Reports & inquiries” tab listing its standard reports. Pick a report, filter rows live, and export to CSV.',
            array(
                'No setup needed — the report list appears automatically on each module that has reports.',
            ),
            array(
                'Open a module → Reports & inquiries (under “Inquiries and reports”).',
                'Click a report on the left to run it; type in the filter box to narrow rows.',
                'Click CSV to export the visible rows.',
            ),
            'Read-only — reports query existing data and never post to the GL.',
            array('Filter first, then export — the CSV contains exactly the rows you can see.'));

        $g['period_close'] = $E('period_close', 'GL period close & lock',
            'Lock accounting periods so no further postings can be made. Prevents accidental backdating after month-end close.',
            array(
                'Navigate to ERP Finance → Period Close.',
                'Review the checklist: all invoices posted, bank reconciled, depreciation run, accruals entered.',
                'Click Lock Period — this prevents any new journal entry in that date range.',
                'Bulk-lock all periods before a chosen date if catching up.',
            ),
            array(
                'At month-end, run the close checklist → verify all items green → lock the period.',
                'If an adjustment is needed after lock, an admin can temporarily unlock, post, and re-lock.',
            ),
            'Locked periods reject any GL posting attempt with a clear error. Unlock requires admin role.',
            array('Lock periods promptly after close — it is the single best control against post-close errors.'));

        $g['events'] = $E('events', 'ERP event bus & webhooks',
            'Automatically emit events when business actions happen (invoice posted, order placed, stock updated) and deliver them to external systems via webhooks with HMAC signing.',
            array(
                'Events are emitted automatically — no setup needed for built-in triggers.',
                'To add a webhook subscriber: BOS → Integrations Hub → Add webhook URL.',
                'Set the target URL, select which events to subscribe to, and save.',
                'The platform signs each payload with HMAC-SHA256 so the receiver can verify authenticity.',
            ),
            array(
                'Events fire automatically on: invoice.posted, order.placed, stock.updated, payment.received, customer.created, po.approved, shipment.dispatched, return.created, period.closed, user.login, tenant.config_changed.',
                'Failed deliveries retry with exponential backoff (3 attempts).',
                'After 3 failures, the payload moves to the dead-letter queue for manual review.',
            ),
            'No direct GL impact — events are observability/integration fabric.',
            array('Use webhooks to sync with NetSuite, Shopify, or any system that accepts HTTP callbacks.'));

        $g['mfa'] = $E('mfa', 'Multi-factor authentication (MFA)',
            'TOTP-based two-factor authentication for CP and ERP finance access. Protects against credential theft.',
            array(
                'Admin enables MFA requirement in CP → Auth Settings.',
                'Each user enrolls by scanning a QR code with an authenticator app (Google Authenticator, Authy, etc.).',
                'On next login, the user enters their password + 6-digit TOTP code.',
                'Finance roles can be required to use MFA even if other roles are optional.',
            ),
            array(
                'Login → enter email + password → enter TOTP code → access granted.',
                'Lost device: admin can reset MFA enrollment for the user.',
            ),
            'No GL impact — security layer only.',
            array('Require MFA for all finance and admin roles before going live with paying clients.'));

        $g['ai_service'] = $E('ai_service', 'AI service hub & PII firewall',
            'Central AI gateway with automatic PII redaction. Routes queries to the correct AI backend (copilot, classification, anomaly detection, NL reporting).',
            array(
                'AI service is enabled platform-wide — no per-tenant setup needed.',
                'Configure API keys for external AI providers in BOS → AI Service settings.',
                'The PII firewall automatically strips emails, phone numbers, TRN, credit card, and IBAN before any data leaves the platform.',
            ),
            array(
                'AI is embedded in other modules — copilot for natural-language queries, classification for HS codes, anomaly for isolation monitoring.',
                'All AI requests are logged with audit trail.',
            ),
            'No GL impact — AI augments existing data without creating postings.',
            array('The PII firewall runs before every AI request — you cannot accidentally send customer data to external APIs.'));

        $g['collections'] = $E('collections', 'Collections & dunning automation',
            'Automated 7-step collection sequences for overdue invoices. Tracks aging, sends reminders, escalates to management.',
            array(
                'Configure dunning sequences: reminder intervals, escalation rules, email templates.',
                'Assign customers to collection groups (standard, VIP, high-risk).',
                'Set thresholds for automatic hold (stop new orders when balance exceeds limit).',
            ),
            array(
                'System automatically sends reminders at configured intervals.',
                'Dashboard shows aging buckets (current, 30, 60, 90, 120+ days).',
                'Track promises-to-pay and payment arrangements.',
                'Escalate to management when thresholds are breached.',
            ),
            'Collections read AR aging data. Payment receipts post to AR as normal.',
            array('Start with gentle reminders — most overdue invoices are oversight, not intent.'));

        $g['warranty'] = $E('warranty', 'Warranty & RMA tracking',
            'Register product warranties, process return merchandise authorizations (RMA), and track claim status through a state machine.',
            array(
                'Enable warranty tracking in tenant config.',
                'Register warranties at point of sale (automatic) or manually.',
                'Configure RMA approval workflow and return shipping rules.',
            ),
            array(
                'Customer requests return → create RMA → approve/reject → receive goods → inspect → refund/replace/repair.',
                'Track warranty expiry dates and claim history per customer.',
                'Generate warranty certificate PDFs.',
            ),
            'RMA credit notes post to AR; replacement orders post to inventory and COGS.',
            array('Warranty registration at point of sale saves disputes later.'));

        $g['subscription'] = $E('subscription', 'Subscription & recurring billing',
            'Manage subscription plans with automatic recurring invoices. Track MRR, churn, and renewal dates.',
            array(
                'Create subscription plans with pricing tiers.',
                'Assign customers to plans with start date and billing cycle.',
                'Configure auto-invoice generation and payment collection.',
            ),
            array(
                'System generates invoices on each billing cycle.',
                'Dashboard shows MRR, ARR, churn rate, and upcoming renewals.',
                'Handle upgrades, downgrades, and cancellations with pro-rating.',
            ),
            'Recurring invoices post to AR and revenue recognition accounts per the billing cycle.',
            array('Set up dunning sequences for failed subscription payments to reduce churn.'));

        $g['accounting_automation'] = $E('gl', 'Accounting automation centre',
            'Graphical hub for every finance automation: Order→ERP posting, period/year-end close, bank reconciliation assist, collections dunning, report scheduler, VAT reminders, document→GL auto-post, AP payment due alerts and fixed-asset depreciation. Each automation shows a visual pipeline and can be enabled with one click.',
            array(
                'Open General ledger → Accounting automation (or Home → Automation Centre → Accounting).',
                'Review the KPI strip (Total / Active / Available) and each card\'s pipeline.',
                'Click Enable all accounting (or Enable on each card). Cards with a workflow template also install a runnable workflow.',
                'Open linked modules (Collections, Year-end, Bank recon, Report scheduler) to finish any per-tenant configuration.',
            ),
            array(
                'Step 1 — Hub overview. Result: every accounting automation is listed with Active / Available status and a chevron pipeline (e.g. Validate → AR Invoice → GL Journal → Inventory → VAT).',
                'Step 2 — Enable Order→ERP. Result: storefront orders post AR + GL + stock + VAT automatically when the pipeline runs.',
                'Step 3 — Enable Collections & dunning. Result: the scheduled tick advances overdue invoices through the 7-step reminder sequence.',
                'Step 4 — Enable Report scheduler / VAT reminder. Result: scheduled workflows appear under Workflow builder and Run history after the tick.',
                'Step 5 — Run history. Result: open Automation Centre → Run history (or click Run scheduled tick now) to see duration and status per run.',
            ),
            'Order→ERP and document→GL create balanced journals; collections read AR aging; period/year-end lock postings; depreciation posts expense/accumulated depreciation.',
            array(
                'Use Enable all accounting on a new tenant so finance automations are on before go-live.',
                'Scheduled workflows need the platform cron (erp_automation_tick job) or the Run scheduled tick now button.',
                'Open each linked module once after enabling — some engines create schema on first open.',
            ));

        $g['bpa_automation'] = $E('core', 'Business process automation (BPA)',
            'Catalogue of every operational automation — PO approval, invoice auto-send, low stock, onboarding, daily sales summary, AML alerts, process-flow routing, 3-way match, subscriptions, RMA/warranty, credit-limit gate and GRN notify — with graphical pipelines, one-click enable, and installable workflow templates (trigger → condition → action).',
            array(
                'Open Home → Automation Centre → Business processes (also under System administration → Automation Centre).',
                'Enable the processes you need; click Install workflow on cards that ship a template.',
                'Open Workflow builder to edit the graphical node canvas (trigger → steps) or create a custom workflow.',
                'Use Process flow for GPS-style live routing of human tasks across departments.',
            ),
            array(
                'Step 1 — Business processes grid. Result: each BPA shows status, description and a teal pipeline of stages.',
                'Step 2 — Install PO Approval / Invoice Auto-Send / Low Stock. Result: active workflows appear in the builder list with Run now.',
                'Step 3 — Workflow builder canvas. Result: a dark graphical chain shows Trigger and each Action/Condition node; edit steps in the table and Save.',
                'Step 4 — Run now / Run history. Result: actions create in-app notifications (and email when mail is configured); runs log duration and status.',
                'Step 5 — Process flow. Result: for human hand-offs, use Overview → Process flow monitor/org map instead of (or alongside) no-code workflows.',
            ),
            'Most BPA steps are operational (notifications, tasks, status). Finance actions such as credit check and GL journal post when configured with amounts/accounts.',
            array(
                'Prefer templates for common patterns — they are idempotent (installing twice does not duplicate by name).',
                'Conditions short-circuit the chain when they fail and on_failure=stop.',
                'Keep Automation Centre bookmarked for operators; finance owners use Accounting automation inside General ledger.',
            ));

        return $g;
    }
}

if (!function_exists('epc_guide_for_entitlements')) {
    /**
     * Filter the guide to the modules a tenant actually runs.
     *
     * @param array<int,string> $enabledCodes module codes enabled for the tenant
     * @return array<string,array<string,mixed>>
     */
    function epc_guide_for_entitlements(array $enabledCodes): array
    {
        $all = epc_guide_modules();
        if (empty($enabledCodes)) {
            return $all;
        }
        $set = array_flip($enabledCodes);
        // Map guide keys that differ from module codes.
        $alias = array('finance' => 'gl', 'eam' => 'asset_maint');
        $out = array();
        foreach ($all as $key => $entry) {
            $code = $entry['module'];
            if (isset($set[$code]) || isset($set[$key]) || ($key === 'core')) {
                $out[$key] = $entry;
            }
        }
        return $out;
    }
}
