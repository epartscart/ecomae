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
