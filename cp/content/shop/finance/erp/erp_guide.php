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

<style id="epc-erp-guide-contrast">
/* Self-contained readable guide (survives dark ERP portal shell) */
.epc-erp-guide-root,
.epc-erp-guide-root .panel-body,
.epc-erp-guide-root .epc-erp-flow {
	color: #1e293b !important;
	font-size: 14px;
	line-height: 1.55;
}
.epc-erp-guide-root h3,
.epc-erp-guide-root h4,
.epc-erp-guide-root h5 {
	color: #0f172a !important;
	font-weight: 700;
}
.epc-erp-guide-root p,
.epc-erp-guide-root li,
.epc-erp-guide-root td,
.epc-erp-guide-root th,
.epc-erp-guide-root dd,
.epc-erp-guide-root dt {
	color: #1e293b !important;
}
.epc-erp-guide-root a { color: #1d4ed8 !important; font-weight: 600; }
.epc-erp-guide-intro {
	background: linear-gradient(135deg, #1e3a8a 0%, #1d4ed8 55%, #0ea5e9 100%) !important;
	color: #fff !important;
	border-radius: 10px;
	padding: 20px 22px;
	margin-bottom: 18px;
}
.epc-erp-guide-intro,
.epc-erp-guide-intro h3,
.epc-erp-guide-intro p,
.epc-erp-guide-intro strong { color: #fff !important; }
.epc-erp-guide-intro a { color: #dbeafe !important; text-decoration: underline; }
.epc-erp-guide-step {
	border-left: 4px solid #2563eb !important;
	padding: 14px 16px;
	margin: 14px 0;
	background: #f8fafc !important;
	border-radius: 0 8px 8px 0;
	color: #1e293b !important;
}
.epc-erp-guide-step h5 { margin: 0 0 8px; font-weight: 700; color: #0f172a !important; }
.epc-erp-flow { font-size: 14px; line-height: 1.65; color: #1e293b !important; }
.epc-erp-guide-root .table > tbody > tr > td,
.epc-erp-guide-root .table > tbody > tr > th {
	color: #1e293b !important;
	background: #fff !important;
	border-color: #e2e8f0 !important;
}
.epc-erp-guide-root .well,
.epc-erp-guide-root .well-sm {
	background: #f8fafc !important;
	border: 1px solid #e2e8f0 !important;
	color: #1e293b !important;
}
.epc-erp-guide-root code {
	color: #0f172a !important;
	background: #e2e8f0 !important;
	padding: 1px 6px;
	border-radius: 4px;
}
.epc-erp-guide-toc {
	display: flex;
	flex-wrap: wrap;
	gap: 8px;
	margin: 0 0 18px;
}
.epc-erp-guide-toc a {
	display: inline-flex;
	align-items: center;
	gap: 6px;
	padding: 7px 12px;
	border-radius: 8px;
	border: 1px solid #cbd5e1;
	background: #fff !important;
	color: #1e293b !important;
	font-size: 12.5px;
	font-weight: 600;
	text-decoration: none !important;
}
.epc-erp-guide-toc a:hover { border-color: #2563eb; color: #1d4ed8 !important; }
</style>

<div class="col-lg-12 epc-erp-guide-root">
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
				<h3><i class="fa fa-book"></i> How to use ERP</h3>
				<p style="margin:0;">Clear path from day-to-day work to the books:
					<strong>Sales → Revenue → Receivable → Balance</strong>,
					<strong>Purchase → Payable → Balance</strong>,
					<strong>Cash &amp; Bank</strong>, and
					<strong>Chart of accounts → GL → P&amp;L → Balance sheet</strong>.</p>
			</div>

			<nav class="epc-erp-guide-toc" aria-label="Guide sections">
				<a href="#epc-guide-snapshot"><i class="fa fa-bar-chart"></i> Live numbers</a>
				<a href="#epc-guide-courier"><i class="fa fa-truck"></i> Courier &amp; VAT</a>
				<a href="#epc-guide-flow"><i class="fa fa-sitemap"></i> Daily flow</a>
				<a href="#epc-guide-setup"><i class="fa fa-cogs"></i> First-time setup</a>
			</nav>

			<div class="alert alert-info">
				<strong>Where to open ERP:</strong> Shop → <em>ERP Finance</em> (or the Open ERP button above).
				Main areas: Inventory, Fixed assets, Opening balances, Revenue, Purchases, Cash &amp; bank, Chart of accounts, GL, P&amp;L, Balance sheet.
				<strong>Customers</strong> → Customer management.
				<strong>Suppliers</strong> → Procurement.
				Snapshot generated <?php echo epc_erp_h($snapshot['generated_at']); ?>.
			</div>

			<div class="epc-erp-guide-step" id="epc-guide-courier">
				<h5><i class="fa fa-truck"></i> Courier charges, VAT &amp; document map</h5>
				<div class="epc-erp-flow">
					<p><strong>Who pays courier?</strong> The customer. The delivery fee is saved on the order, shown in OMS, and added as a line on the UAE tax invoice (Accounts Receivable).</p>
					<p><strong>VAT on courier:</strong> For UAE destinations, courier is taxable income (output VAT on the invoice). Outside the UAE it is <strong>zero-rated</strong> — no VAT on goods or courier. The shipping destination country drives this.</p>
					<p><strong>What to keep on file:</strong> UAE → tax invoice (PDF/XML), TRN, and payment proof. Export → shipping proof, commercial invoice, and buyer-country evidence for zero-rating.</p>
					<p><strong>Document chain (OMS → ERP):</strong></p>
					<ol>
						<li><strong>Shop order</strong> — customer commerce order</li>
						<li><strong>VAT treatment</strong> — UAE standard or export zero-rate</li>
						<li><strong>Sales order (SO)</strong> — revenue document in ERP</li>
						<li><strong>Purchase orders (PO)</strong> — one per supplier when buying stock</li>
						<li><strong>Supplier bills (AP)</strong> — cost side</li>
						<li><strong>Customer tax invoice (AR)</strong> — goods + courier + VAT</li>
					</ol>
					<p>On the OMS fulfilment panel use <strong>Link ERP / Sync / Auto-post</strong> so sales order, PO, bill and invoice stay aligned.</p>
				</div>
			</div>

			<h4 id="epc-guide-snapshot"><i class="fa fa-bar-chart"></i> Live snapshot</h4>
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
					<li><strong>14 enterprise modules</strong> — <a href="/erp/?area=enterprise&amp;tab=business_units">Business unit</a>, <a href="/erp/?area=enterprise&amp;tab=listing">Listing</a>, Product Information, Inventory groups/status, <a href="/erp/?area=purchasing&amp;tab=ap_setup">A/P setup</a>, <a href="/erp/?area=sales&amp;tab=ar_setup">A/R setup</a>, <a href="/erp/?area=enterprise&amp;tab=budgeting">Budgeting</a>, <a href="/erp/?area=banking&amp;tab=bank_setup">Bank account</a>, <a href="/erp/?area=insights&amp;tab=consolidation_bu">Consolidation</a>, <a href="/erp/?area=insights&amp;tab=enterprise_reports">Report</a>, <a href="/erp/?area=purchasing&amp;tab=landed_cost">Landed cost</a>, <a href="/erp/?area=operations&amp;tab=master_planning">Master planning</a>, <a href="/erp/?area=operations&amp;tab=retail_barcode">Retail barcode</a> and <a href="/erp/?area=collaboration&amp;tab=doc_formats">Documents</a> — each per-tenant (writes to the tenant&#39;s own DB).</li>
					<li><strong>Standalone ERP</strong> — an ERP-only client can create customers, inventory items and sales orders with no e-commerce storefront dependency.</li>
					<li><strong>NetSuite-style home + enterprise modules inside</strong> — portlet dashboard (tiles, KPI gauge, A/R aging chart, reminders, quick actions) with an enterprise-style grouped, collapsible left nav (Common / Journals / Setup / Periodic) and Action Pane + FastTab entry forms.</li>
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

			<h4><i class="fa fa-rocket"></i> Order planning, Supplier portal &amp; Executive dashboard — step by step</h4>
			<div class="well well-sm">
				<p>Three new enterprise modules. Each step below lists the <strong>action</strong> and the <strong>result</strong> you should see.</p>
			</div>

			<div class="epc-erp-guide-step">
				<h5><i class="fa fa-cubes"></i> Order planning — demand-driven replenishment (<a href="/erp/?area=operations&amp;tab=order_planning">Operations &rsaquo; Order planning</a>)</h5>
				<p style="margin:6px 0 8px;opacity:.85;">A demand-driven planning engine that forecasts demand from sale-out history, computes safety stock and reorder point per item × warehouse, and recommends order quantities.</p>
				<ol class="epc-erp-flow" style="margin-bottom:0;">
					<li><strong>Action:</strong> Open Operations → Order planning. <strong>Result:</strong> planning tables auto-create on first open (a brand-new tenant starts empty); the Recommended orders grid loads.</li>
					<li><strong>Action:</strong> No demand history yet? Click <strong>Generate sample demand</strong>. <strong>Result:</strong> 12 months of mixed-pattern sale-out history are seeded (tagged <code>DEMO-DEMAND</code>, re-runnable, clearable) and the engine produces live recommendations.</li>
					<li><strong>Action:</strong> Read the <strong>Recommended orders</strong> grid. <strong>Result:</strong> each item × warehouse shows forecast/month, lead-time demand, safety stock, reorder point (order level), recommended order qty (ROQ), days-of-cover, value and demand class (smooth / erratic / intermittent / lumpy); lines that need an order are highlighted.</li>
					<li><strong>Action:</strong> Click an item to open its <strong>worksheet</strong>; edit lead-time, target service level, review period, min order qty / multiple and Save. <strong>Result:</strong> the safety stock, ROP and ROQ recalculate immediately, with a 12-month demand chart and stock balances (on-hand / effective / shortfall / excess).</li>
					<li><strong>Action:</strong> Click <strong>Confirm</strong> on a line (or <strong>Confirm all due</strong>). <strong>Result:</strong> the recommendation status flips to confirmed, ready to raise as a purchase order.</li>
					<li><strong>Action:</strong> Open the <strong>Inventory policy (ABC/XYZ)</strong> sub-tab. <strong>Result:</strong> items are classified A/B/C by cumulative annual value and X/Y/Z by demand variability, each with a recommended service level.</li>
					<li><strong>Action:</strong> Open <strong>Redistribution</strong>. <strong>Result:</strong> suggested inter-warehouse transfers (move excess in one branch to cover a shortfall in another) before raising a PO.</li>
					<li><strong>Action:</strong> Open <strong>Exceptions &amp; alerts</strong> and <strong>Stock analysis &amp; KPIs</strong>. <strong>Result:</strong> a severity-ranked exception list (stock-out risk / below-safety / dead / excess) and KPIs (inventory value, turns, avg days-of-cover, fill rate, ABC distribution).</li>
				</ol>
			</div>

			<div class="epc-erp-guide-step">
				<h5><i class="fa fa-sitemap"></i> Process flow — workflow automation &amp; live tracking (<a href="/erp/?area=overview&amp;tab=processflow">Overview &rsaquo; Process flow</a>)</h5>
				<p style="margin:6px 0 8px;opacity:.85;">Define any business process as a chain of steps; each approval auto-hands the case to the next person or department head. Real ERP work tracks itself too — customer orders, purchase orders, supplier payments and staff expense claims each auto-create a case and advance through their stages automatically. Watch where every case has reached on a GPS-style map, see the whole organization's flow, view your entire team's workload, and measure each employee by the actual tasks they handled — all with staff photos.</p>
				<ol class="epc-erp-flow" style="margin-bottom:0;">
					<li><strong>Action:</strong> Open Overview → Process flow → <strong>Processes</strong> tab → <strong>Create process</strong> (e.g. "Customer credit request", "Goods delivery to customer"). <strong>Result:</strong> a new process is created (tables auto-create on first open; a brand-new tenant starts empty).</li>
					<li><strong>Action:</strong> On the process click <strong>Manage steps → Add step</strong>. For each step set the <em>step name</em>, <em>Routes to</em> (Specific person / Department head / Anyone in department / Back to initiator), <em>Department</em> and <em>SLA hours</em>. <strong>Result:</strong> the ordered step chain is saved; steps run top-to-bottom.</li>
					<li><strong>Action:</strong> New tenant with no data? Click <strong>Seed</strong>. <strong>Result:</strong> demo staff across departments &amp; branches, example processes and running cases — including auto-tracked customer orders, purchase orders, supplier payments and expense claims — are created (clearable via <strong>Clear</strong>) so you can explore immediately.</li>
					<li><strong>Action:</strong> Click <strong>Sync</strong>. <strong>Result:</strong> the auto-tracked task types are backfilled from your real records; a message reports how many customer orders, purchase orders, supplier payments and expense claims are now tracked.</li>
					<li><strong>Action:</strong> Start a case on a process, then <strong>Approve &amp; route to next</strong>. <strong>Result:</strong> the case automatically hands off to the next step's person/department head; SLA/overdue is tracked.</li>
					<li><strong>Action:</strong> Open the <strong>Monitor</strong> tab and click any case. <strong>Result:</strong> a GPS-style tracker shows the route — completed stops, a pulsing "you are here" marker, the staff photo at each step, and the full audit timeline. Use the <em>Zoom</em> switch (Overall / Location / Department / Task) to redraw the same journey at any level.</li>
					<li><strong>Action:</strong> Open the <strong>Org map</strong> tab. <strong>Result:</strong> every process flows left-to-right through nodes with animated arrows and live case counts. Switch <em>View level</em> (Overall / Legal entity / Business unit / Department / User / Task / Location); at User level each employee's photo and case count appear inside their department node. The left list shows all cases — searchable, sortable, and click any to open its document.</li>
					<li><strong>Action:</strong> Open the <strong>Workforce</strong> tab. <strong>Result:</strong> your entire team in one view with photos — who is busy and on which task plus tasks completed; group by Department / Location / Task / Business unit / Legal entity and filter by busy/idle. Click a person to open the case they are working on. A top-performers leaderboard ranks staff by tasks managed.</li>
					<li><strong>Action:</strong> Open the <strong>Hierarchy</strong> tab. <strong>Result:</strong> an expandable org tree (Legal entity &rsaquo; Business unit &rsaquo; Department &rsaquo; Location &rsaquo; Employee) with rolled-up open and completed task counts at every level — drill from the whole company down to one person.</li>
					<li><strong>Action:</strong> Scope by date — add <code>&amp;from=YYYY-MM-DD&amp;to=YYYY-MM-DD</code> to the URL. <strong>Result:</strong> the Monitor, Workforce, Org map and performance counts honor that reporting period (with a banner and Clear button), so performance reviews can be period-bound.</li>
				</ol>
			</div>

			<div class="epc-erp-guide-step">
				<h5><i class="fa fa-gavel"></i> Labour law &amp; compliance — worldwide statutory engine (<a href="/erp/?area=people&amp;tab=hr_law">People &rsaquo; Labour law &amp; compliance</a>)</h5>
				<p style="margin:6px 0 8px;opacity:.85;">A country-aware employment-law engine that localizes statutory rules to your company country — working hours, overtime, probation cap, notice, annual/sick/maternity/paternity leave, public holidays, end-of-service and wage protection — across 25+ countries (UAE + GCC, South Asia, MENA, Europe, Americas, APAC, Africa). It also runs every employee through those rules and flags issues plus the accrued end-of-service liability. Informational only — confirm with local counsel.</p>
				<ol class="epc-erp-flow" style="margin-bottom:0;">
					<li><strong>Action:</strong> Open People → Labour law &amp; compliance. <strong>Result:</strong> the active country is taken automatically from the Company profile; the statutory card shows working week, overtime, probation cap, notice, leave (annual/sick/maternity/paternity), public holidays, end-of-service basis, wage protection (e.g. WPS) and the governing authority.</li>
					<li><strong>Action:</strong> Pick a country from the dropdown, or filter the <strong>Worldwide statutory reference</strong> table. <strong>Result:</strong> preview and compare any jurisdiction's rules.</li>
					<li><strong>Action:</strong> Read the <strong>Employee compliance monitor</strong>. <strong>Result:</strong> every employee is checked against the company-country rules — probation status (with end date), excess leave balance, missing data and accrued end-of-service liability — each finding citing its statutory basis.</li>
					<li><strong>Action:</strong> Read the KPI strip. <strong>Result:</strong> employees checked, in probation, "needs attention" count and total end-of-service liability to provision.</li>
				</ol>
			</div>

			<div class="epc-erp-guide-step">
				<h5><i class="fa fa-handshake-o"></i> Supplier portal — performance scorecards (<a href="/erp/?area=purchasing&amp;tab=supplier_portal">Purchasing &rsaquo; Supplier portal</a>)</h5>
				<p style="margin:6px 0 8px;opacity:.85;">Per-supplier scorecards computed from your POs, goods receipts, RFQs and payables.</p>
				<ol class="epc-erp-flow" style="margin-bottom:0;">
					<li><strong>Action:</strong> Open Purchasing → Supplier portal. <strong>Result:</strong> the scorecard grid lists every active supplier with a composite score (0–100) and A–D rating, # POs, total spend, on-time delivery %, average lead time, RFQ response %, RFQ win % and open payable balance.</li>
					<li><strong>Action:</strong> Click a supplier. <strong>Result:</strong> a detail view with the score breakdown (on-time 40% + responsiveness 30% + activity 20% + win rate 10%), recent RFQs and the full PO list with per-PO lead time.</li>
				</ol>
			</div>

			<div class="epc-erp-guide-step">
				<h5><i class="fa fa-dashboard"></i> Main dashboard — full-system analytics cockpit (<a href="/erp/">ERP home</a>)</h5>
				<p style="margin:6px 0 8px;opacity:.85;">The main dashboard is the single analytics view for the whole system — the former Executive dashboard and Industry intelligence are folded in here.</p>
				<ol class="epc-erp-flow" style="margin-bottom:0;">
					<li><strong>Action:</strong> Open ERP (the home dashboard). <strong>Result:</strong> NetSuite-style portlets (quick tiles, reminders, financials, KPI meter, A/R aging) plus an Operational KPI ribbon (revenue, gross margin %, DSO, DPO, inventory turnover, current ratio, AR, AP, cash, inventory value), each colour-coded green/amber/red against benchmarks.</li>
					<li><strong>Action:</strong> No sales history yet? Click <strong>Generate sample sales</strong>. <strong>Result:</strong> 6 months of completed orders are seeded (tagged, clearable) so the revenue KPIs and the trend populate.</li>
					<li><strong>Action:</strong> Read the <strong>Revenue &amp; profit</strong> trend, <strong>Planning alerts</strong> and <strong>Top suppliers</strong>. <strong>Result:</strong> a 6-month revenue vs profit bar chart, exception counts (with a link to the exceptions view) and your highest-spend suppliers, plus quick links to AI advisor, P&amp;L, Order planning and Supplier portal.</li>
				</ol>
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

			<h4 id="epc-guide-flow"><i class="fa fa-list-ol"></i> Step-by-step (daily use)</h4>

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

			<h4 id="epc-guide-setup"><i class="fa fa-cogs"></i> Complete platform setup &amp; configuration — every module, step by step</h4>
			<div class="well well-sm">
				<p style="margin:0 0 6px;">This is the full walkthrough for a brand-new company: first do the <strong>one-time platform setup</strong> (Section&nbsp;A) in order, then configure and run <strong>each module</strong> (Section&nbsp;B). Everything is per-company and country-driven — set your country once and currency, tax, fiscal year and labour law follow. Each step lists the <strong>action</strong> and the <strong>result</strong> you should see.</p>
				<p style="margin:0;"><strong>Recommended order:</strong> Company profile &amp; country &rarr; Companies/legal entities &rarr; Industry pack &rarr; Chart of accounts &rarr; Number sequences &rarr; Financial dimensions &rarr; Currency &amp; tax &rarr; Product dimensions &amp; variants &rarr; Opening balances &rarr; Users, departments &amp; security roles. Then open each operational module.</p>
			</div>

			<h4 style="margin-top:4px;"><i class="fa fa-flag-checkered"></i> Section A — One-time platform setup (do these first, in order)</h4>

			<div class="epc-erp-guide-step">
				<h5><i class="fa fa-globe"></i> A1. Company profile &amp; country (the master switch)</h5>
				<ol class="epc-erp-flow" style="margin-bottom:0;">
					<li><strong>Action:</strong> Open <a href="/erp/?area=setup&amp;tab=erp_setup">Setup &amp; Data &rsaquo; Accounting setup</a> and set the company <strong>base currency, TRN/tax number and fiscal-year start</strong>; set the registration <strong>Country</strong> on the company profile. <strong>Result:</strong> currency, language (incl. Arabic/Urdu RTL), the tax regime (VAT/GST label, rate and e-invoice scheme), fiscal-year start and the HR labour-law pack all localize together (UAE&rarr;AED/VAT&nbsp;5%, KSA&rarr;SAR/VAT&nbsp;15%, Pakistan&rarr;PKR/Sales&nbsp;Tax&nbsp;18%, India&rarr;INR/GST).</li>
					<li><strong>Action:</strong> Confirm the inventory <strong>valuation method</strong> (weighted average) on the same screen. <strong>Result:</strong> all stock movements value consistently from go-live.</li>
				</ol>
			</div>

			<div class="epc-erp-guide-step">
				<h5><i class="fa fa-building"></i> A2. Companies / legal entities &amp; the company picker</h5>
				<ol class="epc-erp-flow" style="margin-bottom:0;">
					<li><strong>Action:</strong> Open <a href="/erp/?area=enterprise&amp;tab=business_units">Enterprise &rsaquo; Business unit &rsaquo; Legal entities</a> and add each legal entity (company) you operate. <strong>Result:</strong> a default <code>MAIN</code> company is seeded from your profile; new entities appear in the top-bar <strong>company picker</strong>.</li>
					<li><strong>Action:</strong> Use the <strong>company picker</strong> (top-right of the ERP header) to switch the active company; set a different industry pack / structure per company if needed. <strong>Result:</strong> the active configuration context (industry, structure, sequences) follows the selected company; unset values fall back to the tenant default.</li>
				</ol>
			</div>

			<div class="epc-erp-guide-step">
				<h5><i class="fa fa-industry"></i> A3. Industry pack (auto-configures your structure)</h5>
				<ol class="epc-erp-flow" style="margin-bottom:0;">
					<li><strong>Action:</strong> In <a href="/erp/?area=setup&amp;tab=erp_setup">Accounting setup &rsaquo; Industry</a> pick your industry pack (Jewellery, Oil &amp; Gas, Trading, Manufacturing, Auto parts&hellip;). <strong>Result:</strong> the pack releases its product fields into <a href="/erp/?area=operations&amp;tab=product_info&amp;pm_view=fields">Product Information &rsaquo; Field setup</a> (e.g. Jewellery &rarr; metal, purity, gross weight, stone type/carat, hallmark) plus suggested COA/sequences/dimensions.</li>
					<li><strong>Action:</strong> Review each released field and flip <strong>Inventory</strong> &harr; <strong>Non-inventory</strong>, enable/disable, or add custom fields. <strong>Result:</strong> your item master matches your trade; re-applying a pack is idempotent and never overwrites your edits.</li>
				</ol>
			</div>

			<div class="epc-erp-guide-step">
				<h5><i class="fa fa-list"></i> A4. Chart of accounts (COA)</h5>
				<ol class="epc-erp-flow" style="margin-bottom:0;">
					<li><strong>Action:</strong> Open <a href="/erp/?area=finance&amp;tab=coa">Finance &rsaquo; COA</a>. <strong>Result:</strong> a country-aware COA seeds on first open (with the correct VAT/GST accounts). Add, rename or deactivate accounts and set account types (asset / liability / equity / income / expense).</li>
				</ol>
			</div>

			<div class="epc-erp-guide-step">
				<h5><i class="fa fa-list-ol"></i> A5. Number sequences (document numbering)</h5>
				<ol class="epc-erp-flow" style="margin-bottom:0;">
					<li><strong>Action:</strong> Open <a href="/erp/?area=setup&amp;tab=erp_setup">Accounting setup &rsaquo; Number sequences</a> and set the prefix and next number per voucher type (sales order, purchase order, invoice, journal, payment&hellip;). <strong>Result:</strong> new documents number with those prefixes immediately, per company and per year.</li>
				</ol>
			</div>

			<div class="epc-erp-guide-step">
				<h5><i class="fa fa-sitemap"></i> A6. Financial dimensions</h5>
				<ol class="epc-erp-flow" style="margin-bottom:0;">
					<li><strong>Action:</strong> Open <a href="/erp/?area=enterprise&amp;tab=business_units">Enterprise &rsaquo; Business unit</a> and define your <strong>financial dimensions</strong> (and values) plus cost centres — e.g. Department, Branch, Project. <strong>Result:</strong> postings can be tagged for analytical reporting and consolidation.</li>
				</ol>
			</div>

			<div class="epc-erp-guide-step">
				<h5><i class="fa fa-percent"></i> A7. Currency &amp; tax / VAT</h5>
				<ol class="epc-erp-flow" style="margin-bottom:0;">
					<li><strong>Action:</strong> Confirm base currency in Accounting setup; review the tax regime under <a href="/erp/?area=finance&amp;tab=vat_return">Finance &rsaquo; UAE VAT</a> / <a href="/erp/?area=finance&amp;tab=tax_compliance">Tax compliance</a>. <strong>Result:</strong> output/input tax rates and the e-invoice scheme are set from your country; the filing calendar (<a href="/erp/?area=finance&amp;tab=compliance">Compliance center</a>) lists due dates.</li>
				</ol>
			</div>

			<div class="epc-erp-guide-step">
				<h5><i class="fa fa-cube"></i> A8. Product dimensions &amp; variants</h5>
				<ol class="epc-erp-flow" style="margin-bottom:0;">
					<li><strong>Action:</strong> Open <a href="/erp/?area=operations&amp;tab=product_info&amp;pm_view=dimensions">Operations &rsaquo; Product information &rsaquo; Dimensions &amp; variants</a>. Tick the product dimensions you use (Configuration / Size / Colour / Style / Version) and register their values (e.g. S/M/L, Red/Blue). <strong>Result:</strong> the dimension group and storage/tracking groups are saved.</li>
					<li><strong>Action:</strong> Pick an item and click <strong>Generate variants</strong>. <strong>Result:</strong> the Cartesian set of variants is created with deterministic SKUs (e.g. <code>SHIRT-M-RED</code>); re-running is idempotent (no duplicates).</li>
				</ol>
			</div>

			<div class="epc-erp-guide-step">
				<h5><i class="fa fa-flag-o"></i> A9. Opening balances (go-live migration)</h5>
				<ol class="epc-erp-flow" style="margin-bottom:0;">
					<li><strong>Action:</strong> Open <a href="/erp/?area=finance&amp;tab=opening_balances">Finance &rsaquo; Opening balances</a>, create a batch with your <em>as-of date</em>, add COA and inventory lines, then <strong>Post</strong>. Register fixed assets with opening accumulated depreciation in <a href="/erp/?area=operations&amp;tab=fixed_assets">Operations &rsaquo; Fixed assets</a>. <strong>Result:</strong> your migrated balances become the starting position for GL, stock and assets.</li>
				</ol>
			</div>

			<div class="epc-erp-guide-step">
				<h5><i class="fa fa-shield"></i> A10. Users, departments &amp; security roles</h5>
				<ol class="epc-erp-flow" style="margin-bottom:0;">
					<li><strong>Action:</strong> Set up staff and departments in <a href="/erp/?area=people&amp;tab=staff">People &rsaquo; Staff</a>; departments control which tabs each user sees. <strong>Result:</strong> staff see only their department's modules.</li>
					<li><strong>Action:</strong> Open <a href="/erp/?area=administration&amp;tab=security_roles">Administration &rsaquo; Security roles</a> and build <strong>privileges &rarr; duties &rarr; roles</strong>, then assign roles to users; use the effective-access explorer to verify. <strong>Result:</strong> fine-grained access control across the platform.</li>
				</ol>
			</div>

			<h4 style="margin-top:10px;"><i class="fa fa-th-list"></i> Section B — Each module: configuration &amp; daily use</h4>

			<div class="epc-erp-guide-step">
				<h5><i class="fa fa-th-large"></i> Overview — dashboard, workflow, process flow, approvals (<a href="/erp/?area=overview&amp;tab=dashboard">open</a>)</h5>
				<p style="margin:6px 0 6px;opacity:.85;"><strong>Setup:</strong> define approval rules in <a href="/erp/?area=overview&amp;tab=approvals">Approvals</a> (e.g. PO &ge; 10,000 &rarr; Manager) and processes in <a href="/erp/?area=overview&amp;tab=processflow">Process flow</a>.</p>
				<p style="margin:0;opacity:.85;"><strong>Daily:</strong> watch KPIs on the dashboard; clear your approval queue; track live cases on the Process-flow GPS map / Org map / Workforce views.</p>
			</div>

			<div class="epc-erp-guide-step">
				<h5><i class="fa fa-line-chart"></i> Sales — CRM, orders, receivables, collections (<a href="/erp/?area=sales&amp;tab=sales_orders">open</a>)</h5>
				<p style="margin:6px 0 6px;opacity:.85;"><strong>Setup:</strong> configure <a href="/erp/?area=sales&amp;tab=ar_setup">A/R setup</a> (payment terms, credit limits, posting profiles) and customers (Customers &rsaquo; Customer management).</p>
				<p style="margin:0;opacity:.85;"><strong>Daily:</strong> create a <a href="/erp/?area=sales&amp;tab=sales_orders">Sales order</a> (Lines/Header views, Action Pane) &rarr; deliver (<a href="/erp/?area=sales&amp;tab=delivery_notes">Delivery notes</a>) &rarr; raise <a href="/erp/?area=sales&amp;tab=invoices">Invoice (e-invoice)</a> &rarr; track <a href="/erp/?area=sales&amp;tab=receivables">Receivables</a> &rarr; chase overdue in <a href="/erp/?area=sales&amp;tab=collections">Collections</a> (cases, promise-to-pay, dunning, credit holds).</p>
			</div>

			<div class="epc-erp-guide-step">
				<h5><i class="fa fa-shopping-basket"></i> Purchasing — vendors, RFQ, POs, payables (<a href="/erp/?area=purchasing&amp;tab=purchase_orders">open</a>)</h5>
				<p style="margin:6px 0 6px;opacity:.85;"><strong>Setup:</strong> configure <a href="/erp/?area=purchasing&amp;tab=ap_setup">A/P setup</a> (terms, posting) and suppliers (Procurement panel).</p>
				<p style="margin:0;opacity:.85;"><strong>Daily:</strong> issue <a href="/erp/?area=purchasing&amp;tab=rfq">RFQ</a> &rarr; raise a <a href="/erp/?area=purchasing&amp;tab=purchase_orders">Purchase order</a> &rarr; receive goods &rarr; run <a href="/erp/?area=purchasing&amp;tab=three_way_match">3-way match</a> &rarr; record the supplier invoice in <a href="/erp/?area=purchasing&amp;tab=purchases">Purchases</a> &rarr; settle in <a href="/erp/?area=purchasing&amp;tab=payables">Payables</a>; apportion <a href="/erp/?area=purchasing&amp;tab=landed_cost">Landed cost</a> as needed.</p>
			</div>

			<div class="epc-erp-guide-step">
				<h5><i class="fa fa-shopping-cart"></i> Retail &amp; Commerce — channels, POS, statements (<a href="/erp/?area=retail&amp;tab=retail_commerce">open</a>)</h5>
				<p style="margin:6px 0 6px;opacity:.85;"><strong>Setup:</strong> define channels (store/online/call-center), build <strong>assortments</strong> (items sellable per channel) and <strong>periodic discounts</strong> (percent/amount, date-effective).</p>
				<p style="margin:0;opacity:.85;"><strong>Daily:</strong> ring up <strong>POS sales</strong> (best discount + country-driven tax auto-applied) and close the day with the <strong>end-of-day (Z) statement</strong> (gross/discount/net/tax/total by tender).</p>
			</div>

			<div class="epc-erp-guide-step">
				<h5><i class="fa fa-university"></i> Finance — GL, COA, tax, period close (<a href="/erp/?area=finance&amp;tab=gl">open</a>)</h5>
				<p style="margin:6px 0 6px;opacity:.85;"><strong>Setup:</strong> COA (A4), opening balances (A9), tax regime (A7); configure fiscal periods in <a href="/erp/?area=finance&amp;tab=fin_advanced">Financial depth</a>.</p>
				<p style="margin:0;opacity:.85;"><strong>Daily / periodic:</strong> post <a href="/erp/?area=finance&amp;tab=gl">General ledger</a> journals; review <a href="/erp/?area=finance&amp;tab=aging">Aging</a>; file <a href="/erp/?area=finance&amp;tab=vat_return">VAT</a> and track the <a href="/erp/?area=finance&amp;tab=compliance">Compliance center</a> calendar; run FX revaluation / allocations / accruals in <a href="/erp/?area=finance&amp;tab=fin_advanced">Financial depth</a>; close the year in <a href="/erp/?area=finance&amp;tab=year_end">Year-end closing</a> (close P&amp;L to retained earnings, carry opening balances forward).</p>
			</div>

			<div class="epc-erp-guide-step">
				<h5><i class="fa fa-money"></i> Cash &amp; Bank Management — accounts, payments, reconciliation (<a href="/erp/?area=banking&amp;tab=cash_bank">open</a>)</h5>
				<p style="margin:6px 0 6px;opacity:.85;"><strong>Setup:</strong> create bank accounts in <a href="/erp/?area=banking&amp;tab=bank_setup">Bank account</a>.</p>
				<p style="margin:0;opacity:.85;"><strong>Daily:</strong> record receipts/payments in <a href="/erp/?area=banking&amp;tab=cash_bank">Cash &amp; bank</a> and <a href="/erp/?area=banking&amp;tab=petty_cash">Petty cash</a>; run <a href="/erp/?area=banking&amp;tab=payment_batches">Payment batches</a> to pay suppliers; reconcile against statements in <a href="/erp/?area=banking&amp;tab=bank_recon">Bank recon</a>.</p>
			</div>

			<div class="epc-erp-guide-step">
				<h5><i class="fa fa-cubes"></i> Operations — inventory, manufacturing, WMS, quality (<a href="/erp/?area=operations&amp;tab=inventory">open</a>)</h5>
				<p style="margin:6px 0 6px;opacity:.85;"><strong>Setup:</strong> item master &amp; <a href="/erp/?area=operations&amp;tab=product_info">Product information</a> (fields, dimensions/variants), <a href="/erp/?area=operations&amp;tab=inv_groups">inventory groups/status</a>, warehouses/locations &amp; bins in <a href="/erp/?area=operations&amp;tab=wms">Advanced WMS</a>, work centers &amp; routes in <a href="/erp/?area=operations&amp;tab=mfg_planning">Manufacturing planning</a>, valuation models in <a href="/erp/?area=operations&amp;tab=cost_models">Costing value-models</a>, and test plans in <a href="/erp/?area=operations&amp;tab=quality">Quality management</a>.</p>
				<p style="margin:0;opacity:.85;"><strong>Daily:</strong> manage <a href="/erp/?area=operations&amp;tab=inventory">Inventory</a> movements; run <a href="/erp/?area=operations&amp;tab=order_planning">Order planning</a> / <a href="/erp/?area=operations&amp;tab=master_planning">Master planning</a> (MRP); process WMS work (put-away/pick/move/count via RF confirm); release production &amp; record quality orders / non-conformances; recalc costing at period end.</p>
			</div>

			<div class="epc-erp-guide-step">
				<h5><i class="fa fa-ship"></i> Custom &amp; Shipping — customs &amp; logistics (<a href="/erp/?area=custom_shipping&amp;tab=custom_shipping">open</a>)</h5>
				<p style="margin:0;opacity:.85;"><strong>Daily:</strong> capture import/export/transit declarations with the required customs fields (HS codes, origins, declaration refs) and link the ERP PO/SO references; document expiries feed the Risk &amp; Insurance tracker.</p>
			</div>

			<div class="epc-erp-guide-step">
				<h5><i class="fa fa-users"></i> People — HR, payroll, labour law (<a href="/erp/?area=people&amp;tab=hr">open</a>)</h5>
				<p style="margin:6px 0 6px;opacity:.85;"><strong>Setup:</strong> add employees in <a href="/erp/?area=people&amp;tab=hr">HR</a> / <a href="/erp/?area=people&amp;tab=staff">Staff</a>; the <a href="/erp/?area=people&amp;tab=hr_law">Labour law &amp; compliance</a> pack is country-driven (gratuity, leave entitlement, leave salary).</p>
				<p style="margin:0;opacity:.85;"><strong>Daily / monthly:</strong> manage <a href="/erp/?area=people&amp;tab=hr_ops">HR operations</a>; run <a href="/erp/?area=people&amp;tab=payroll">Payroll</a> (generate &rarr; approve &rarr; pay); process <a href="/erp/?area=people&amp;tab=expense_reports">Expenses</a>.</p>
			</div>

			<div class="epc-erp-guide-step">
				<h5><i class="fa fa-bar-chart"></i> Insights — reports, consolidation, audit (<a href="/erp/?area=insights&amp;tab=pl">open</a>)</h5>
				<p style="margin:0;opacity:.85;"><strong>Daily / periodic:</strong> read <a href="/erp/?area=insights&amp;tab=pl">P&amp;L</a> and <a href="/erp/?area=insights&amp;tab=balance_sheet">Balance sheet</a>, the trial balance &amp; <a href="/erp/?area=insights&amp;tab=enterprise_reports">reports</a>, <a href="/erp/?area=insights&amp;tab=consolidation_bu">Consolidation</a> across entities, <a href="/erp/?area=insights&amp;tab=multi_entity">Multi-entity</a> views, the <a href="/erp/?area=insights&amp;tab=audit">Audit trail</a>, and the <a href="/erp/?area=insights&amp;tab=ai_advisor">AI advisor</a>.</p>
			</div>

			<div class="epc-erp-guide-step">
				<h5><i class="fa fa-calendar"></i> Collaboration — projects, documents, contracts (<a href="/erp/?area=collaboration&amp;tab=projects">open</a>)</h5>
				<p style="margin:6px 0 6px;opacity:.85;"><strong>Setup:</strong> create projects and budgets in <a href="/erp/?area=collaboration&amp;tab=project_accounting">Project accounting</a>; define <a href="/erp/?area=collaboration&amp;tab=doc_formats">Document formats</a>.</p>
				<p style="margin:0;opacity:.85;"><strong>Daily:</strong> manage <a href="/erp/?area=collaboration&amp;tab=agenda">Agenda</a>, <a href="/erp/?area=collaboration&amp;tab=contacts">Contacts</a>, <a href="/erp/?area=collaboration&amp;tab=documents">Documents</a> and <a href="/erp/?area=collaboration&amp;tab=contracts">Contracts &amp; e-sign</a>; track project budget vs actual, WIP and revenue recognition.</p>
			</div>

			<div class="epc-erp-guide-step">
				<h5><i class="fa fa-shield"></i> Risk &amp; Insurance — policies, claims, document expiry (<a href="/erp/?area=risk&amp;tab=insurance">open</a>)</h5>
				<p style="margin:6px 0 6px;opacity:.85;"><strong>Setup:</strong> add policies in <a href="/erp/?area=risk&amp;tab=insurance">Insurance</a> (insurer, sum insured, premium, period, renewal reminder timeframe) across all classes (Marine, Property All-Risk, Public Liability, Medical, Fidelity&hellip;); register expiring documents in <a href="/erp/?area=risk&amp;tab=doc_expiry">Document expiry</a> with lead-time reminders.</p>
				<p style="margin:0;opacity:.85;"><strong>Daily:</strong> log and track claims (Notified &rarr; Survey &rarr; Documents &rarr; Assessed &rarr; Settled); run reminder emails (safe to schedule daily — each reminder fires once). Active policies auto-feed the expiry tracker.</p>
			</div>

			<div class="epc-erp-guide-step">
				<h5><i class="fa fa-building-o"></i> Enterprise — business units, dimensions, budgeting (<a href="/erp/?area=enterprise&amp;tab=business_units">open</a>)</h5>
				<p style="margin:0;opacity:.85;"><strong>Setup / periodic:</strong> maintain <a href="/erp/?area=enterprise&amp;tab=business_units">Business units</a> &amp; financial dimensions, the <a href="/erp/?area=enterprise&amp;tab=listing">Listing</a> reference, and <a href="/erp/?area=enterprise&amp;tab=budgeting">Budgeting</a> (budget vs actual).</p>
			</div>

			<div class="epc-erp-guide-step">
				<h5><i class="fa fa-file-text-o"></i> External Reporting — statutory &amp; regulatory (<a href="/erp/?area=regrep&amp;tab=ext_reports">open</a>)</h5>
				<p style="margin:0;opacity:.85;"><strong>Periodic:</strong> generate country-driven, auto-formatted statutory reports from the Report centre.</p>
			</div>

			<div class="epc-erp-guide-step">
				<h5><i class="fa fa-cogs"></i> Administration — org admin, security, platform (<a href="/erp/?area=administration&amp;tab=org_admin">open</a>)</h5>
				<p style="margin:0;opacity:.85;"><strong>Setup:</strong> <a href="/erp/?area=administration&amp;tab=org_admin">Organization administration</a> (legal entities &amp; org hierarchy, global address book, working calendars, number sequences), <a href="/erp/?area=administration&amp;tab=security_roles">Security roles</a> (privileges/duties/roles), and <a href="/erp/?area=administration&amp;tab=platform">Platform services</a> (batch jobs, feature management, workflow, data entities).</p>
			</div>

			<div class="epc-erp-guide-step">
				<h5><i class="fa fa-sliders"></i> Setup &amp; Data — accounting setup, import, integration (<a href="/erp/?area=setup&amp;tab=erp_setup">open</a>)</h5>
				<p style="margin:0;opacity:.85;"><strong>Setup:</strong> <a href="/erp/?area=setup&amp;tab=erp_setup">Accounting setup</a> (number sequences, valuation, company defaults, industry pack), <a href="/erp/?area=setup&amp;tab=data_import">Data import</a> (bulk load masters/transactions), and <a href="/erp/?area=setup&amp;tab=integration">Data &amp; integration</a> (data entities + an OData-style query layer + business events/webhooks).</p>
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
