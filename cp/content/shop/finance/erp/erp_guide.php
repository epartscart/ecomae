<?php
/**
 * ERP module — step-by-step guide (CP documentation).
 */
defined('_ASTEXE_') or die('No access');

require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_helpers.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_access.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_staff.php';

if (!isset($db_link) || !($db_link instanceof PDO)) {
	try {
		$db_link = new PDO(
			'mysql:host=' . $DP_Config->host . ';dbname=' . $DP_Config->db,
			$DP_Config->user,
			$DP_Config->password,
			array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION)
		);
		$db_link->query('SET NAMES utf8;');
	} catch (Exception $e) {
		echo '<div class="alert alert-danger">Database connection failed.</div>';
		return;
	}
}

$snapshot = epc_erp_guide_snapshot($db_link);
if (!isset($epc_erp_portal)) {
	extract(epc_erp_configure_portal_urls('cp'));
} else {
	extract(epc_erp_configure_portal_urls($epc_erp_portal));
}
$dash = $snapshot['dashboard'];
$pl = isset($snapshot['pl']) ? $snapshot['pl'] : array('net_profit' => 0, 'total_revenue' => 0, 'total_expenses' => 0);
$bs = isset($snapshot['balance_sheet']) ? $snapshot['balance_sheet'] : array('total_assets' => 0, 'total_liabilities_equity' => 0);
$staffDash = epc_erp_staff_dashboard($db_link);
$deptCfg = epc_erp_departments_config();
?>

<style>
.epc-erp-guide-intro { background: linear-gradient(135deg, #0f172a 0%, #1e4d3a 100%); color: #fff; border-radius: 8px; padding: 20px 22px; margin-bottom: 18px; }
.epc-erp-guide-intro h3 { margin: 0 0 8px; color: #fff; }
.epc-erp-guide-step { border-left: 4px solid #27ae60; padding: 12px 16px; margin: 14px 0; background: #f8fafc; border-radius: 0 6px 6px 0; }
.epc-erp-guide-step h5 { margin: 0 0 8px; font-weight: 700; color: #0f172a; }
.epc-erp-flow { font-size: 13px; line-height: 1.7; }
</style>

<div class="col-lg-12">
	<div class="hpanel">
		<div class="panel-heading hbuilt">
			ERP — step-by-step guide
			<span class="pull-right">
				<a class="btn btn-primary btn-xs" href="<?php echo epc_erp_h($erpUrl); ?>"><i class="fa fa-calculator"></i> Open ERP module</a>
				<?php if (!empty($epc_erp_cp_links)): ?>
				<a class="btn btn-default btn-xs" href="<?php echo epc_erp_h($ordersUrl); ?>"><i class="fa fa-shopping-cart"></i> Orders</a>
				<?php endif; ?>
			</span>
		</div>
		<div class="panel-body">

			<div class="epc-erp-guide-intro">
				<h3><i class="fa fa-book"></i> How to use ERP in the control panel</h3>
				<p style="margin:0;opacity:.92;">Complete workflow: <strong>Sales → Revenue → Receivable → Balance</strong>,
					<strong>Purchase → Supplier → Payable → Balance</strong>, <strong>Cash &amp; Bank</strong>,
					and <strong>COA → GL → P&amp;L → Balance Sheet</strong>.
					URL: <a href="<?php echo epc_erp_h($erpUrl); ?>" style="color:#a7f3d0;"><?php echo epc_erp_h($erpUrl); ?></a></p>
			</div>

			<div class="alert alert-info">
				<strong>Menu:</strong> Shop → <em>ERP Finance</em>. Tabs include <strong>Inventory</strong> (multi-warehouse, weighted average cost), <strong>Fixed assets</strong> (depreciation &amp; book value), <strong>Opening balances</strong> (migration date), plus Revenue, Purchases, Cash &amp; bank, <strong>COA</strong>, <strong>GL</strong>, <strong>P&amp;L</strong>, <strong>Balance sheet</strong>.
				Full doc: <code>docs/ERP_INVENTORY_ASSETS_GUIDE.md</code>.
				<strong>Customer data</strong> → <a href="/<?php echo epc_erp_h(isset($DP_Config) ? (string)$DP_Config->backend_dir : 'cp'); ?>/shop/customer_mgmt/customer_mgmt">Customers → Customer management</a>.
				<strong>Suppliers</strong> → <a href="/<?php echo epc_erp_h(isset($DP_Config) ? (string)$DP_Config->backend_dir : 'cp'); ?>/shop/procurement/procurement">Procurement panel</a>.
				Generated <?php echo epc_erp_h($snapshot['generated_at']); ?>.
			</div>

			<h4><i class="fa fa-bar-chart"></i> Live snapshot</h4>
			<table class="table table-striped table-bordered">
				<tbody>
					<tr><td>Revenue MTD (ex VAT)</td><td><strong><?php echo epc_erp_money($dash['revenue_ex_vat']); ?> AED</strong></td></tr>
					<tr><td>Margin MTD</td><td><strong><?php echo epc_erp_money($dash['profit_ex_vat']); ?> AED</strong></td></tr>
					<tr><td>Customer receivable (order due)</td><td><?php echo epc_erp_money($dash['receivable_due_orders']); ?> AED</td></tr>
					<tr><td>Customer ledger balance</td><td><?php echo epc_erp_money($dash['customer_ledger_balance']); ?> AED</td></tr>
					<tr><td>Supplier payable</td><td><?php echo epc_erp_money($dash['payable_balance']); ?> AED</td></tr>
					<tr><td>Cash &amp; bank total</td><td><?php echo epc_erp_money($dash['cash_bank_total']); ?> AED</td></tr>
					<tr><td>COA accounts / GL journals</td><td><?php echo (int)$snapshot['coa_count']; ?> / <?php echo (int)$snapshot['gl_journal_count']; ?></td></tr>
					<tr><td>GL net profit MTD (P&amp;L)</td><td><strong><?php echo epc_erp_money($pl['net_profit']); ?> AED</strong></td></tr>
					<tr><td>Balance sheet — total assets</td><td><?php echo epc_erp_money($bs['total_assets']); ?> AED</td></tr>
					<tr><td>Balance sheet — liabilities + equity</td><td><?php echo epc_erp_money($bs['total_liabilities_equity']); ?> AED</td></tr>
					<tr><td>Suppliers / purchases / cash accounts</td><td><?php echo (int)$snapshot['supplier_count']; ?> / <?php echo (int)$snapshot['purchase_count']; ?> / <?php echo (int)$snapshot['cash_account_count']; ?></td></tr>
					<tr><td>Warehouses (for supplier sync)</td><td><?php echo (int)$snapshot['storage_count']; ?></td></tr>
				</tbody>
			</table>

			<h4><i class="fa fa-star"></i> What&#39;s new — enterprise capabilities</h4>
			<div class="well well-sm">
				<p>The ERP is now <strong>tenant-country aware</strong>: one setting on the company profile (Country) localizes the whole suite together.</p>
				<ul>
					<li><strong>Country master switch</strong> — set the company country once and currency, language (incl. Arabic/Urdu RTL), tax regime (VAT/GST label + rate + e-invoice scheme), fiscal-year start and the HR labour-law pack all switch together. UAE&rarr;AED/VAT 5%/FTA, KSA&rarr;SAR/VAT 15%/ZATCA, Pakistan&rarr;PKR/Sales Tax 18%/FBR, India&rarr;INR/GST.</li>
					<li><strong>Country-aware HRMS</strong> — end-of-service <strong>gratuity</strong>, annual-leave entitlement and <strong>leave salary</strong> computed per each country&#39;s labour law. UAE: 21 days/yr first 5 years, 30 days/yr beyond, capped at 2 years&#39; pay (Federal Decree-Law 33/2021). See <a href="/erp/?area=people&amp;tab=hr">People &rarr; HR</a> for the live accrual table.</li>
					<li><strong>Corporate Tax provision</strong> — the <a href="/erp/?area=finance&amp;tab=pl">P&amp;L</a> applies the UAE 9% CT with the AED 375,000 small-business threshold automatically.</li>
					<li><strong>Accounting setup</strong> — <a href="/erp/?area=finance&amp;tab=coa">Chart of accounts</a>, <a href="/erp/?area=finance&amp;tab=opening_balances">Opening balances</a> (go-live migration), <a href="/erp/?area=finance&amp;tab=fixed_assets">Fixed assets</a> with depreciation, plus VAT return, Tax compliance and E-invoicing tabs.</li>
					<li><strong>Professional theming</strong> — Blue &amp; White across the platform (marketing, CP, ERP); Red &amp; Black for the consumer storefront — driven by one central token layer.</li>
					<li><strong>3-day live demo</strong> — shared credential on the marketing site opens storefront + ERP only (no CP), auto-expiring after 3 days.</li>
				</ul>
			</div>

			<h4><i class="fa fa-rocket"></i> What&#39;s new — latest release</h4>
			<div class="well well-sm">
				<p>Recent additions across navigation, presentation and the industry/inventory model:</p>
				<ul>
					<li><strong>Industry pack drives Product Information</strong> — applying an industry pack in <a href="/erp/?area=finance&amp;tab=setup">Accounting setup</a> now <em>releases that pack&#39;s product fields</em> into <a href="/erp/?area=operations&amp;tab=product_info&amp;pm_view=fields">Product Information &rsaquo; Field setup</a> (e.g. Jewellery &rarr; metal, purity, gross weight, stone type, stone carat, hallmark). Scales to 50&ndash;100 industries from the per-tenant industry catalogue.</li>
					<li><strong>Inventory vs non-inventory classification</strong> — on Product Information &rsaquo; Field setup each field can be flipped <strong>Inventory</strong> (stock + accounting / item master) &harr; <strong>Non-inventory</strong> (catalogue/info only), enabled/disabled, and shows its source pack. Defaults come from the pack at onboarding but are editable any time; re-applying a pack preserves your classification (idempotent, never overwrites). You can also add custom fields.</li>
					<li><strong>Period / as-of-date reporting</strong> — balances reconstruct to a chosen date from the dated movement ledger: <a href="/erp/?area=operations&amp;tab=inv_groups&amp;pm_view=stock">Inventory on hand as at [date]</a> (qty + weighted-avg cost + value for any day or fiscal year-end), with From/To period filters on flow reports and no date bar on master/setup screens.</li>
					<li><strong>Receivables / Payables / Inventory aging</strong> — <a href="/erp/?area=finance&amp;tab=aging&amp;aging_view=ar">AR</a>, <a href="/erp/?area=finance&amp;tab=aging&amp;aging_view=ap">AP</a> and <a href="/erp/?area=finance&amp;tab=aging&amp;aging_view=inventory">inventory</a> aging with configurable buckets and distribution bars.</li>
					<li><strong>Customer / Vendor Statements of Account</strong> — opening + invoices &minus; receipts = closing, with ageing, on tenant letterhead + TRN; open-item mode available.</li>
					<li><strong>14 enterprise (D365 F&amp;O) modules</strong> — <a href="/erp/?area=enterprise&amp;tab=business_units">Business unit</a>, <a href="/erp/?area=enterprise&amp;tab=listing">Listing</a>, Product Information, Inventory groups/status, <a href="/erp/?area=purchasing&amp;tab=ap_setup">A/P setup</a>, <a href="/erp/?area=sales&amp;tab=ar_setup">A/R setup</a>, <a href="/erp/?area=enterprise&amp;tab=budgeting">Budgeting</a>, <a href="/erp/?area=finance&amp;tab=bank_setup">Bank account</a>, <a href="/erp/?area=insights&amp;tab=consolidation_bu">Consolidation</a>, <a href="/erp/?area=insights&amp;tab=enterprise_reports">Report</a>, <a href="/erp/?area=purchasing&amp;tab=landed_cost">Landed cost</a>, <a href="/erp/?area=operations&amp;tab=master_planning">Master planning</a>, <a href="/erp/?area=operations&amp;tab=retail_barcode">Retail barcode</a> and <a href="/erp/?area=collaboration&amp;tab=doc_formats">Documents</a> — each per-tenant (writes to the tenant&#39;s own DB).</li>
					<li><strong>Standalone ERP</strong> — an ERP-only client can create customers, inventory items and sales orders with no e-commerce storefront dependency.</li>
					<li><strong>NetSuite-style home + D365 F&amp;O inside</strong> — portlet dashboard (tiles, KPI gauge, A/R aging chart, reminders, quick actions) with a D365-style grouped, collapsible left nav (Common / Journals / Setup / Periodic) and Action Pane + FastTab entry forms.</li>
				</ul>
			</div>

			<h4><i class="fa fa-cubes"></i> Ecom BOS — Business Operating System pillars</h4>
			<div class="well well-sm">
				<p>The suite is now a multi-tenant <strong>Business Operating System (BOS)</strong>: ERP is one pillar alongside commerce, compliance, workflows and industry intelligence. Three new operational pillars ship natively (config-driven, per-tenant &mdash; nothing hard-coded):</p>
				<ul>
					<li><strong>Compliance center</strong> (<a href="/erp/?area=finance&amp;tab=compliance">Finance &rsaquo; Compliance center</a>) &mdash; a config-driven obligations engine: filing obligations (VAT, corporate tax, ESR, e-invoicing, WPS&hellip;) seeded from your region, a live <strong>filing calendar</strong> with due dates and status (open / due soon / overdue / filed), and <strong>document-retention</strong> rules. Add, edit or disable any item; mark a return filed with a reference and it drops out of the overdue/due-soon counts.</li>
					<li><strong>Approvals engine</strong> (<a href="/erp/?area=overview&amp;tab=approvals">Overview &rsaquo; Approvals</a>) &mdash; a reusable threshold approval engine across documents. Define rules per document type (e.g. <em>PO &ge; 10,000 &rarr; Manager approval</em>) with multi-step approver chains; high-value sales orders, purchase orders and payment vouchers are routed automatically for sign-off with a full <strong>immutable audit trail</strong>. Approve/reject with comments from the queue.</li>
					<li><strong>Industry intelligence</strong> (<a href="/erp/?area=insights&amp;tab=industry_intel">Insights &rsaquo; Industry intelligence</a>) &mdash; live operational KPIs (gross margin %, DSO, DPO, inventory turnover, current ratio, AR/AP/cash/inventory) plus a best-practice <strong>recommended-controls checklist</strong> tailored to the active industry pack and saved per tenant.</li>
				</ul>
			</div>

			<h4><i class="fa fa-sitemap"></i> End-to-end flows</h4>
			<div class="well well-sm epc-erp-flow">
				<p><strong>A. Sales &amp; revenue (customer side)</strong></p>
				<ol>
					<li>Customer places order on the shop → order appears in <a href="<?php echo epc_erp_h($ordersUrl); ?>">Orders</a>.</li>
					<li><strong>Revenue</strong> is calculated from order line selling prices (ex VAT) — see ERP tab <strong>Revenue (Sales)</strong>.</li>
					<li>When customer pays (balance, card, bank transfer), post via <a href="<?php echo epc_erp_h($financeOpsUrl); ?>">Customer account operations</a> or mark order paid in CP.</li>
					<li><strong>Receivable</strong> = order total incl. VAT minus payments — only for <strong>Completed</strong> orders — ERP tab <strong>Customer receivables</strong>.</li>
					<li><strong>Payable</strong> from order-linked purchases posts only after the order is Completed; supplier balance excludes open-order AP.</li>
					<li><strong>Balance</strong> = customer prepaid ledger (<code>shop_users_accounting</code>) — credits minus debits; view per-customer statement in Receivables tab.</li>
				</ol>
				<p><strong>B. Purchase &amp; payables (supplier side)</strong></p>
				<ol>
					<li>Link suppliers to warehouses: ERP → Payables → <strong>Sync from warehouses</strong> (or add manually). Warehouses: <a href="<?php echo epc_erp_h($storagesUrl); ?>">Logistics → Storages</a>.</li>
					<li>When supplier invoice arrives: ERP → <strong>Purchases</strong> → record invoice (or <strong>Generate from order</strong> using order purchase cost).</li>
					<li>This increases <strong>supplier payable balance</strong> — Payables tab.</li>
					<li>When you pay supplier: Payables → <strong>Record supplier payment</strong> — reduces payable and posts cash/bank outflow.</li>
				</ol>
				<p><strong>C. Cash &amp; bank</strong></p>
				<ol>
					<li>Create accounts: ERP → <strong>Cash &amp; bank</strong> → Main cash, bank accounts (default accounts created on first setup).</li>
					<li>Post <strong>receipts</strong> (customer cash in) and <strong>payments</strong> (expenses, supplier payments auto-post here).</li>
					<li>Account balance = opening balance + receipts − payments.</li>
				</ol>
				<p><strong>D. Chart of accounts (COA) &amp; general ledger (GL)</strong></p>
				<ol>
					<li><strong>COA</strong> tab lists all GL accounts by code — assets (1xxx), liabilities (2xxx), equity (3xxx), revenue (4xxx), expenses (5xxx/6xxx).</li>
					<li>Purchase invoices auto-post: Dr COGS + VAT Input, Cr Accounts Payable.</li>
					<li>Cash/bank entries auto-post to linked COA (1000 Cash / 1010 Bank).</li>
					<li><strong>GL</strong> tab → <strong>Post sales orders to GL</strong> for the date range (Dr AR, Cr Revenue, Cr VAT Output).</li>
					<li><strong>Sync unposted</strong> catches any older sub-ledger entries not yet in GL.</li>
					<li>Manual journals: balanced debit/credit lines → <strong>Post journal</strong>.</li>
				</ol>
				<p><strong>E. P&amp;L &amp; balance sheet</strong></p>
				<ol>
					<li><strong>P&amp;L</strong> tab — total revenue minus expenses = net profit for the period.</li>
					<li><strong>Balance sheet</strong> tab — assets, liabilities, equity as of end date.</li>
					<li>Current period earnings from P&amp;L are included in equity until year-end close.</li>
					<li>Trial balance at bottom — debits must equal credits.</li>
				</ol>
			</div>

			<h4><i class="fa fa-list-ol"></i> Step-by-step (daily use)</h4>

			<div class="epc-erp-guide-step">
				<h5>Step 1 — Open ERP &amp; set date range</h5>
				<ol class="epc-erp-flow" style="margin-bottom:0;">
					<li>Log in to CP → Shop → <strong>ERP Finance</strong>.</li>
					<li>Set <strong>From / To</strong> dates → Apply (affects Dashboard and Revenue tabs).</li>
					<li>Review KPI cards: revenue, margin, receivable due, payable, cash total.</li>
				</ol>
			</div>

			<div class="epc-erp-guide-step">
				<h5>Step 2 — Fulfilment pipeline (operations)</h5>
				<ol class="epc-erp-flow" style="margin-bottom:0;">
					<li>Tab <strong>Fulfilment</strong> — visual pipeline: customer payment (advance or credit) → supplier payment → goods in stock → delivery → returns.</li>
					<li><strong>Stock movement</strong> shows reserved / issued from warehouse when supplier delivers; line status &quot;finished&quot; = delivered to customer.</li>
					<li><strong>Returns</strong> — customer return in storefront; process in CP returns module; goods go back to stock then return to supplier.</li>
					<li>Record supplier purchase &amp; payment in ERP only after order lines are ready; revenue/AP recognition when order is <strong>Completed</strong>.</li>
				</ol>
			</div>

			<div class="epc-erp-guide-step">
				<h5>Step 3 — Review sales revenue</h5>
				<ol class="epc-erp-flow" style="margin-bottom:0;">
					<li>Tab <strong>Revenue (Sales)</strong> — orders listed; sale, purchase, margin and due appear only after order status is <strong>Completed</strong> in CP (all lines finished).</li>
					<li>In-progress orders show zero financial amounts until completion.</li>
					<li>Click order number to open order card in CP.</li>
					<li>Unpaid orders show <span class="label label-warning">Open</span>; paid show <span class="label label-success">Paid</span>.</li>
					<li>For order-level revenue correction (discount, write-off, settlement): use <strong>Order revenue adjustment / settlement</strong> at the bottom of this tab — enter order ID, amount, and type.</li>
				</ol>
			</div>

			<div class="epc-erp-guide-step">
				<h5>Step 3 — Manage customer receivables</h5>
				<ol class="epc-erp-flow" style="margin-bottom:0;">
					<li>Tab <strong>Customer receivables</strong> — customers with ledger balance and order count.</li>
					<li>Click <strong>Statement</strong> to see credit/debit lines (top-ups, order payments, ERP adjustments).</li>
					<li>On a customer statement, use <strong>Customer adjustment / settlement</strong> for non-cash corrections (adjustment, settlement, write-off). Credit increases balance; debit decreases.</li>
					<li>Optional: tick <strong>Post to GL</strong> to mirror the entry in AR vs expense accounts.</li>
					<?php if (!empty($financeOpsUrl)): ?><li>Legacy manual ops: <a href="<?php echo epc_erp_h($financeOpsUrl); ?>">Finance → Account operations</a>.</li><?php endif; ?>
					<li>Order-level due amounts are on Revenue tab (includes 5% VAT on sale).</li>
				</ol>
			</div>

			<div class="epc-erp-guide-step">
				<h5>Step 4 — Set up suppliers &amp; record purchases</h5>
				<div class="alert alert-warning" style="margin-bottom:10px;">
					<strong>Warehouse ≠ supplier.</strong> Use the dedicated
					<a href="/<?php echo epc_erp_h((string)$DP_Config->backend_dir); ?>/shop/procurement/procurement">Procurement panel</a>
					for supplier master data (TRN, legal address, payment terms), purchase bills, payments, and advances.
					Warehouses (<a href="<?php echo epc_erp_h($storagesUrl); ?>">Logistics → Storages</a>) are price/stock sources only.
				</div>
				<ol class="epc-erp-flow" style="margin-bottom:0;">
					<li><strong>Procurement → Suppliers</strong> — create supplier with TRN and UAE address (required for input VAT).</li>
					<li>Optional: link warehouse to supplier for naming; prices still come from <strong>Price management</strong>.</li>
					<li><strong>Procurement → Purchase bills</strong> — record supplier invoice (amount ex VAT; VAT 5% for UAE VAT-registered suppliers).</li>
					<li>Shortcut: link order + supplier → <strong>Generate from order</strong> uses order line purchase cost.</li>
					<li>ERP tab <strong>Payables</strong> remains the GL ledger view; operational work is in Procurement.</li>
				</ol>
			</div>

			<div class="epc-erp-guide-step">
				<h5>Step 5 — Pay suppliers (reduce payable)</h5>
				<ol class="epc-erp-flow" style="margin-bottom:0;">
					<li>Tab <strong>Supplier payables</strong> → check balance per supplier.</li>
					<li>Form <strong>Record supplier payment</strong>: choose supplier, pay-from cash/bank account, amount, reference.</li>
					<li>Payable decreases; cash/bank account decreases automatically.</li>
					<li>Click <strong>Ledger</strong> on a supplier to audit invoice vs payment lines.</li>
					<li>Non-cash payable changes (credit note, net-off, write-off): on supplier ledger use <strong>Supplier adjustment / settlement</strong> — decrease or increase payable without a cash payment.</li>
				</ol>
			</div>

			<div class="epc-erp-guide-step">
				<h5>Step 6 — Cash &amp; bank management</h5>
				<ol class="epc-erp-flow" style="margin-bottom:0;">
					<li>Tab <strong>Cash &amp; bank</strong> — view all accounts and balances.</li>
					<li>Create new account (cash drawer or bank account with opening balance).</li>
					<li>Post <strong>Receipt (+)</strong> for customer cash/bank received; <strong>Payment (−)</strong> for expenses.</li>
					<li>Optional: link entry to order ID for traceability.</li>
					<li>Filter entries per account via <strong>Entries</strong> button on each account row.</li>
				</ol>
			</div>

			<div class="epc-erp-guide-step">
				<h5>Step 7 — Chart of accounts (COA)</h5>
				<ol class="epc-erp-flow" style="margin-bottom:0;">
					<li>Tab <strong>COA</strong> — review default accounts and opening balances.</li>
					<li>Add new accounts with unique code (e.g. 6200 Rent expense) if your business needs them.</li>
					<li>Each account shows current GL balance (opening + all posted journals).</li>
				</ol>
			</div>

			<div class="epc-erp-guide-step">
				<h5>Step 8 — General ledger (GL) posting</h5>
				<ol class="epc-erp-flow" style="margin-bottom:0;">
					<li>Tab <strong>GL</strong> — view all journal entries for the date range.</li>
					<li>Click <strong>Post sales orders to GL</strong> once per period (or after batch of orders).</li>
					<li>Click <strong>Sync unposted purchases &amp; cash</strong> if anything was recorded before GL was enabled.</li>
					<li>Click <strong>Lines</strong> on any journal to audit debit/credit detail.</li>
					<li>For adjustments (accruals, corrections): use manual journal form — debits must equal credits.</li>
				</ol>
			</div>

			<div class="epc-erp-guide-step">
				<h5>Step 9 — Profit &amp; loss (P&amp;L)</h5>
				<ol class="epc-erp-flow" style="margin-bottom:0;">
					<li>Tab <strong>P&amp;L</strong> — set date range to month/quarter/year.</li>
					<li>Revenue section: mainly 4000 Sales Revenue (from posted orders).</li>
					<li>Expenses section: mainly 5000 COGS (from purchase invoices) + 6100 General expenses.</li>
					<li>Net profit = Total revenue − Total expenses.</li>
				</ol>
			</div>

			<div class="epc-erp-guide-step">
				<h5>Step 10 — Balance sheet</h5>
				<ol class="epc-erp-flow" style="margin-bottom:0;">
					<li>Tab <strong>Balance sheet</strong> — as-of date = <strong>To</strong> date in filter.</li>
					<li>Assets: cash, bank, receivables, VAT recoverable.</li>
					<li>Liabilities: payables, VAT payable.</li>
					<li>Equity: owner funds + retained earnings + current period P&amp;L.</li>
					<li>Verify: Total assets = Total liabilities + equity (trial balance must tie).</li>
				</ol>
			</div>

			<div class="epc-erp-guide-step">
				<h5>Step 11 — Month-end checklist</h5>
				<ol class="epc-erp-flow" style="margin-bottom:0;">
					<li>Post all sales to GL; sync unposted purchases and cash.</li>
					<li>Reconcile AR (1100) with receivables tab; AP (2000) with payables tab.</li>
					<li>Reconcile cash/bank COA (1000/1010) with bank statements.</li>
					<li>Review P&amp;L and balance sheet; export via browser print/PDF.</li>
					<li>VAT: use ERP tab <strong>UAE VAT</strong> (output − input = payable/recoverable); cross-check GL 1150 / 2100.</li>
				</ol>
			</div>

			<h4><i class="fa fa-users"></i> Multi-department staff workflow</h4>
			<div class="alert alert-info">
				<strong><?php echo (int)$staffDash['staff_count']; ?></strong> staff ·
				<strong><?php echo (int)$staffDash['tasks_open']; ?></strong> open workflow tasks ·
				<strong><?php echo count($staffDash['departments']); ?></strong> departments.
				Each user sees only tabs for their department (Admin sees all).
				Setup: <code>epc-erp-staff-setup.php?token=epartscart-deploy-2026&amp;sample=1</code>
			</div>
			<table class="table table-bordered table-condensed epc-erp-flow">
				<thead><tr><th>Department</th><th>Who</th><th>ERP tabs</th><th>Daily workflow</th></tr></thead>
				<tbody>
				<?php foreach ($deptCfg as $code => $row): ?>
					<tr>
						<td><strong><?php echo epc_erp_h($row['name']); ?></strong></td>
						<td><small>Dummy: <code>erp.<?php echo epc_erp_h($code === 'accounts' ? 'accounts' : $code); ?>@epartscart.local</code></small></td>
						<td><small><?php echo epc_erp_h(in_array('*', $row['tabs'], true) ? 'All' : implode(', ', $row['tabs'])); ?></small></td>
						<td><small><?php echo epc_erp_h(implode(' → ', array_slice($row['workflows'], 0, 4))); ?></small></td>
					</tr>
				<?php endforeach; ?>
				</tbody>
			</table>
			<div class="well well-sm epc-erp-flow">
				<p><strong>Order lifecycle (all departments)</strong></p>
				<ol>
					<li><strong>Sales</strong> — confirm order &amp; payment → Revenue / Receivables tabs.</li>
					<li><strong>Purchase</strong> — supplier PO &amp; invoice → Purchases / Payables.</li>
					<li><strong>Logistics</strong> — stock, pick, carrier label → Fulfilment tab + Channels hub.</li>
					<li><strong>Finance</strong> — receipts &amp; supplier payments → Cash &amp; bank.</li>
					<li><strong>Accounts</strong> — GL, P&amp;L, balance sheet → COA / GL tabs.</li>
					<li><strong>Marketing</strong> — campaign ROI → Marketing tab.</li>
					<li><strong>HR</strong> — staff access &amp; leave → HR + Staff tabs.</li>
					<li><strong>Admin</strong> — month-end checklist → Workflow board (all departments).</li>
				</ol>
				<p>Open <a href="<?php echo epc_erp_h($erpUrl . '?tab=workflow'); ?>">Workflow board</a> ·
				<a href="<?php echo epc_erp_h($erpUrl . '?tab=staff'); ?>">Staff directory</a> ·
				<a href="<?php echo epc_erp_h($erpUrl . '?tab=payroll'); ?>">Payroll</a> ·
				Frontend login: <a href="/en/shop/erp">/en/shop/erp</a> (password <code>EpcStaff2026!</code> for dummy users).</p>
			</div>

			<h4><i class="fa fa-money"></i> Payroll process</h4>
			<div class="well well-sm epc-erp-flow">
				<ol>
					<li><strong>HR</strong> — maintain fixed monthly salary on HR tab (basic + allowances = full 30-day month) and set <em>days worked</em> per employee.</li>
					<li><strong>HR</strong> — Payroll tab → <em>Generate payroll</em>. Pay = <code>(basic + allowances) ÷ 30 × days worked</code>. Extra days above 30 paid at daily rate.</li>
					<li><strong>Finance</strong> — review line totals (edit days on run if needed) → <em>Approve</em>.</li>
					<li><strong>Finance</strong> — <em>Pay &amp; post to bank</em> — creates cash/bank payment and marks all lines paid.</li>
					<li><strong>Accounts</strong> — GL posts Dr 6100 General expenses / Cr 1010 Bank; view YTD by department on Payroll tab.</li>
				</ol>
				<p>Sample: Sales 28 days, Purchase 27 days, Logistics 31 days, IT Manager 32 days — April 2026 paid, May 2026 approved. Re-seed: <code>&amp;payroll_reseed=1</code>.</p>
			</div>

			<h4><i class="fa fa-cubes"></i> Inventory &amp; opening stock</h4>
			<div class="epc-erp-guide-step">
				<h5>Multi-warehouse + weighted average</h5>
				<p class="epc-erp-flow">Tab <a href="<?php echo epc_erp_h($erpUrl . '?tab=inventory'); ?>">Inventory</a>: sync warehouses, <strong>CSV bulk upload</strong>, <strong>paired warehouse transfers</strong>, purchase in / sale out, period closing. <strong>Purchases</strong> tab: tick <em>Receive into inventory</em> on supplier invoice to auto-post <strong>purchase_in</strong> lines. Perishables: batch + expiry on receipts.</p>
			</div>
			<div class="epc-erp-guide-step">
				<h5>Migration opening balances</h5>
				<p class="epc-erp-flow">Tab <a href="<?php echo epc_erp_h($erpUrl . '?tab=opening_balances'); ?>">Opening balances</a>: create batch with <em>as of date</em>, add COA and inventory lines, then <strong>Post</strong>. Register fixed assets with opening accumulated depreciation on the <a href="<?php echo epc_erp_h($erpUrl . '?tab=fixed_assets'); ?>">Fixed assets</a> tab.</p>
			</div>

			<h4><i class="fa fa-question-circle"></i> FAQ</h4>
			<dl class="epc-erp-flow">
				<dt>Inventory costing method?</dt>
				<dd>Weighted average per warehouse + SKU (+ batch/variant). Purchase receipts recalculate average; sales reduce qty at current average.</dd>
				<dt>Opening inventory for an established business?</dt>
				<dd>Use <strong>Opening balances</strong> batch or movement type <em>opening</em> with qty and unit cost on your go-live date.</dd>
				<dt>Fixed asset depreciation methods?</dt>
				<dd>Straight line, declining balance, double declining, and units of production (simplified monthly). One depreciation run per calendar month.</dd>
				<dt>Where is VAT handled?</dt>
				<dd>Sales revenue is ex VAT; customer invoice and due include 5% output VAT. UAE supplier purchases store ex VAT + input VAT. Net payable: ERP tab <strong>UAE VAT</strong>.</dd>
				<dt>When does revenue / receivable / payable count?</dt>
				<dd>Only when the order is in <strong>Completed</strong> status in CP (<code>for_finish</code> order status — typically after all lines are delivered/finished). Open or in-progress orders show in Revenue with no amounts until then.</dd>
				<dt>Difference between receivable due and customer balance?</dt>
				<dd><em>Due</em> = unpaid portion of specific orders. <em>Balance</em> = prepaid customer wallet from account operations.</dd>
				<dt>How do LPO e-mails relate to purchases?</dt>
				<dd>LPO is sent on order creation (warehouse e-mail). Record the supplier invoice in Purchases when goods are invoiced.</dd>
				<dt>What is the COA?</dt>
				<dd>Chart of Accounts — the master list of GL account codes used for double-entry bookkeeping (UAE 5% VAT accounts included).</dd>
				<dt>When is GL updated automatically?</dt>
				<dd>Purchase invoices, supplier payments, and cash/bank entries post to GL on save. Sales revenue requires clicking <strong>Post sales orders to GL</strong>.</dd>
				<dt>P&amp;L vs Dashboard margin?</dt>
				<dd>Dashboard margin is from order lines (operational). P&amp;L is from posted GL journals (accounting).</dd>
				<dt>First-time deploy?</dt>
				<dd>Run <code>epc-erp-cp-setup.php?token=epartscart-deploy-2026</code> once. For inventory &amp; assets: <code>epc-erp-inventory-assets-setup.php?token=epartscart-deploy-2026</code>. COA seeds on first ERP open.</dd>
				<dt>Dummy staff users?</dt>
				<dd>Run <code>epc-erp-staff-setup.php?token=epartscart-deploy-2026&amp;sample=1</code> — creates 9 department users including IT Manager. Rename in CP Users later.</dd>
				<dt>Payroll?</dt>
				<dd>Tab <strong>Payroll</strong> — fixed 30-day salary pro-rated by days worked → generate → approve → pay. Re-seed demo: add <code>&amp;payroll_reseed=1</code> to staff setup URL.</dd>
				<dt>Why don&apos;t I see all ERP tabs?</dt>
				<dd>Tabs are filtered by department group. Sales sees Revenue; Accounts sees GL/P&amp;L; Admin sees everything.</dd>
			</dl>

			<p class="text-muted m-t-md">Guide URL: <a href="<?php echo epc_erp_h($guideUrl); ?>"><?php echo epc_erp_h($guideUrl); ?></a></p>
		</div>
	</div>
</div>
