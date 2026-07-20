<?php
/**
 * Advanced ERP — full step-by-step in-app guide (CP body).
 *
 * High-contrast blue + white guide for the professional ERP shell.
 * Entitlement-aware module sections + industry document chains.
 */
defined('_ASTEXE_') or die('No access');

$doc = $_SERVER['DOCUMENT_ROOT'];
require_once $doc . '/content/shop/finance/epc_erp_guide_content.php';
require_once $doc . '/content/shop/finance/epc_erp_process_flows.php';
@require_once $doc . '/content/shop/finance/epc_erp_modules.php';

$backend = '/' . htmlspecialchars((string) $GLOBALS['DP_Config']->backend_dir, ENT_QUOTES, 'UTF-8');

if (!isset($db_link) || !($db_link instanceof PDO)) {
	try {
		$db_link = new PDO(
			'mysql:host=' . $GLOBALS['DP_Config']->host . ';dbname=' . $GLOBALS['DP_Config']->db,
			$GLOBALS['DP_Config']->user,
			$GLOBALS['DP_Config']->password,
			array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION)
		);
	} catch (Exception $e) {
		$db_link = null;
	}
}

$enabled = array();
if ($db_link instanceof PDO && function_exists('epc_mod_enabled_list')) {
	try {
		$enabled = epc_mod_enabled_list($db_link, true);
	} catch (Exception $e) {
		$enabled = array();
	}
}

$sections = epc_guide_for_entitlements($enabled);

$esc = function ($v) {
	return htmlspecialchars((string) $v, ENT_QUOTES, 'UTF-8');
};

$flows = array();
try {
	$flows = epc_flow_registry();
} catch (Exception $e) {
	$flows = array();
}

$erpHome = $backend . '/shop/finance/erp?epc_erp_shell=1';
?>
<style id="epc-erp-full-guide-css">
/* Self-contained high-contrast guide — blue + white (readable in ERP shell) */
.epc-erp-full-guide {
	--g-ink: #0f172a;
	--g-body: #1e293b;
	--g-muted: #334155;
	--g-soft: #f1f5f9;
	--g-white: #ffffff;
	--g-blue: #1d4ed8;
	--g-blue-deep: #1e3a8a;
	--g-blue-soft: #dbeafe;
	--g-border: #cbd5e1;
	--g-green: #047857;
	font-family: "Sora", "Segoe UI", system-ui, sans-serif;
	color: var(--g-body);
	font-size: 14px;
	line-height: 1.55;
	max-width: 1100px;
	margin: 0 auto;
	padding: 4px 2px 28px;
}
.epc-erp-full-guide * { box-sizing: border-box; }
.epc-erp-full-guide a { color: var(--g-blue); font-weight: 600; }
.epc-erp-full-guide a:hover { color: var(--g-blue-deep); }

.epc-erp-full-guide__top {
	display: flex;
	align-items: center;
	justify-content: space-between;
	gap: 12px;
	flex-wrap: wrap;
	margin: 0 0 16px;
	padding: 14px 16px;
	background: var(--g-white);
	border: 1px solid var(--g-border);
	border-radius: 12px;
	border-left: 4px solid var(--g-blue);
}
.epc-erp-full-guide__brand {
	display: flex;
	align-items: center;
	gap: 12px;
}
.epc-erp-full-guide__mark {
	width: 40px; height: 40px; border-radius: 10px;
	display: flex; align-items: center; justify-content: center;
	background: linear-gradient(135deg, #1d4ed8, #2563eb);
	color: #fff; font-weight: 800; font-size: 18px;
}
.epc-erp-full-guide__title {
	margin: 0;
	font-size: 18px;
	font-weight: 800;
	color: var(--g-ink);
	letter-spacing: -0.01em;
}
.epc-erp-full-guide__title small {
	display: block;
	margin-top: 2px;
	font-size: 12px;
	font-weight: 600;
	color: var(--g-muted);
}
.epc-erp-full-guide__badge {
	display: inline-flex;
	align-items: center;
	gap: 6px;
	padding: 7px 12px;
	border-radius: 999px;
	background: var(--g-blue-soft);
	border: 1px solid #93c5fd;
	color: var(--g-blue-deep);
	font-size: 12px;
	font-weight: 700;
}

.epc-erp-full-guide__panel {
	background: var(--g-white);
	border: 1px solid var(--g-border);
	border-radius: 12px;
	padding: 18px 18px 16px;
	margin: 0 0 14px;
	box-shadow: 0 4px 14px rgba(15, 23, 42, 0.04);
}
.epc-erp-full-guide__panel > h3 {
	margin: 0 0 10px;
	font-size: 16px;
	font-weight: 800;
	color: var(--g-ink);
	line-height: 1.3;
}
.epc-erp-full-guide__panel > h3 span {
	display: inline;
	color: var(--g-muted);
	font-weight: 600;
	font-size: 13px;
}
.epc-erp-full-guide__lead {
	margin: 0;
	color: var(--g-body) !important;
	font-size: 14px;
	line-height: 1.7;
	font-weight: 500;
}
.epc-erp-full-guide__lead b,
.epc-erp-full-guide__lead strong {
	color: var(--g-ink);
	font-weight: 800;
}

.epc-erp-full-guide__chips {
	display: flex;
	flex-wrap: wrap;
	gap: 8px;
	margin-top: 14px;
}
.epc-erp-full-guide__chip {
	display: inline-flex;
	align-items: center;
	padding: 8px 12px;
	border-radius: 8px;
	background: var(--g-soft);
	border: 1px solid var(--g-border);
	color: var(--g-ink) !important;
	font-size: 12.5px;
	font-weight: 700;
	text-decoration: none !important;
	line-height: 1.25;
	transition: background .12s, border-color .12s, color .12s;
}
.epc-erp-full-guide__chip:hover {
	background: var(--g-blue-soft);
	border-color: #60a5fa;
	color: var(--g-blue-deep) !important;
}
.epc-erp-full-guide__chip--primary {
	background: #1d4ed8;
	border-color: #1e3a8a;
	color: #ffffff !important;
}
.epc-erp-full-guide__chip--primary:hover {
	background: #1e40af;
	color: #ffffff !important;
}

.epc-erp-full-guide__cols {
	display: grid;
	grid-template-columns: 1fr 1fr;
	gap: 16px;
	margin-top: 12px;
}
.epc-erp-full-guide__col-hd {
	font-size: 12px;
	font-weight: 800;
	letter-spacing: 0.04em;
	text-transform: uppercase;
	color: var(--g-blue-deep);
	margin: 0 0 8px;
}
.epc-erp-full-guide__col-hd--alt { color: var(--g-green); }
.epc-erp-full-guide ol,
.epc-erp-full-guide ul {
	margin: 0;
	padding-left: 20px;
	color: var(--g-body) !important;
	line-height: 1.75;
}
.epc-erp-full-guide li { margin: 0 0 4px; }
.epc-erp-full-guide li b { color: var(--g-ink); }

.epc-erp-full-guide__impact {
	margin-top: 14px;
	padding: 12px 14px;
	border-radius: 10px;
	background: #eff6ff;
	border: 1px solid #bfdbfe;
	color: var(--g-body);
	line-height: 1.6;
}
.epc-erp-full-guide__impact b {
	color: var(--g-blue-deep);
	display: inline-block;
	margin-right: 4px;
}
.epc-erp-full-guide__tips {
	margin: 12px 0 0;
	padding-left: 20px;
	color: var(--g-muted) !important;
	line-height: 1.7;
}
.epc-erp-full-guide__tips li { font-style: italic; }

.epc-erp-full-guide__flow-label {
	font-weight: 800;
	color: var(--g-blue-deep);
	margin: 0 0 8px;
	font-size: 14px;
}
.epc-erp-full-guide table {
	width: 100%;
	border-collapse: collapse;
	font-size: 13px;
	background: var(--g-white);
}
.epc-erp-full-guide th {
	text-align: left;
	padding: 8px 10px;
	background: #0f172a;
	color: #ffffff !important;
	font-size: 11px;
	font-weight: 800;
	letter-spacing: 0.04em;
	text-transform: uppercase;
	border-bottom: 0;
}
.epc-erp-full-guide td {
	padding: 8px 10px;
	border-bottom: 1px solid #e2e8f0;
	color: var(--g-body) !important;
	vertical-align: top;
}
.epc-erp-full-guide td b { color: var(--g-ink); }
.epc-erp-full-guide tbody tr:nth-child(even) td { background: #f8fafc; }

@media (max-width: 800px) {
	.epc-erp-full-guide__cols { grid-template-columns: 1fr; }
}
</style>

<div class="epc-erp-full-guide" id="epc-erp-full-guide">
	<div class="epc-erp-full-guide__top">
		<div class="epc-erp-full-guide__brand">
			<div class="epc-erp-full-guide__mark" aria-hidden="true">?</div>
			<div>
				<h2 class="epc-erp-full-guide__title">
					ERP User Guide
					<small>Clear steps for every module · industry document chains</small>
				</h2>
			</div>
		</div>
		<span class="epc-erp-full-guide__badge"><?php echo (int) count($sections); ?> modules in your plan</span>
	</div>

	<div class="epc-erp-full-guide__panel">
		<h3>How to use this guide</h3>
		<p class="epc-erp-full-guide__lead">
			Each module has four clear parts: <b>What it does</b>, <b>Set up (in order)</b>,
			the <b>Daily workflow</b> click-path, and the <b>Accounting impact</b>.
			You only see modules included in your plan. Below the modules, the
			<b>industry document chains</b> show which document is prepared at each stage
			(LPO/PO → GRN → SO → DO → Invoice).
		</p>
		<div class="epc-erp-full-guide__chips">
			<a href="#g-firstrun" class="epc-erp-full-guide__chip epc-erp-full-guide__chip--primary">First-time setup &amp; configuration</a>
			<?php foreach ($sections as $key => $e): ?>
			<a href="#g-<?php echo $esc($key); ?>" class="epc-erp-full-guide__chip"><?php echo $esc($e['title']); ?></a>
			<?php endforeach; ?>
			<?php if (!empty($flows)): ?>
			<a href="#g-workflows" class="epc-erp-full-guide__chip">Industry document workflows</a>
			<?php endif; ?>
			<a href="<?php echo $esc($erpHome); ?>" class="epc-erp-full-guide__chip">Open ERP home</a>
		</div>
	</div>

	<div class="epc-erp-full-guide__panel" id="g-firstrun">
		<h3>First-time setup &amp; configuration <span>— do these in order</span></h3>
		<p class="epc-erp-full-guide__lead" style="margin-bottom:12px">
			New tenant? Complete the list top to bottom once. Each step unlocks the next.
			After this, the modules below are ready for daily use.
		</p>
		<ol>
			<li><b>Company profile</b> — legal/trade name, logo, address, TRN/VAT, trade licence and bank pay-to details. These print on every document.</li>
			<li><b>Country</b> — set the company country once; currency, language (incl. RTL), tax regime, fiscal-year start and labour-law pack localise together.</li>
			<li><b>Financial year &amp; periods</b> — set the year start and open accounting periods (Fixed assets / Year-end closing → Setup).</li>
			<li><b>Chart of accounts</b> — load or adjust the COA every module posts to (General ledger → Setup).</li>
			<li><b>Number sequences</b> — confirm voucher/document sequences (invoices, POs, requisitions, journals).</li>
			<li><b>Tax setup</b> — confirm VAT/GST codes and rates; add withholding codes if needed (Tax → Setup).</li>
			<li><b>Business units / legal entities</b> — define units so masters attach to the right entity.</li>
			<li><b>Bank accounts</b> — create cash and bank accounts (IBAN, SWIFT/BIC, GL account, business unit).</li>
			<li><b>Master data</b> — vendors, customers, items and fixed assets.</li>
			<li><b>Opening balances</b> — enter go-live balances as at your migration date.</li>
			<li><b>Users &amp; roles</b> — create users, assign roles, set approval thresholds (System administration).</li>
			<li><b>Go live</b> — start daily transactions; use each module’s Reports &amp; inquiries tab to monitor.</li>
		</ol>
	</div>

	<?php foreach ($sections as $key => $e): ?>
	<div class="epc-erp-full-guide__panel" id="g-<?php echo $esc($key); ?>">
		<h3><?php echo $esc($e['title']); ?> <span>— <?php echo $esc($e['module']); ?></span></h3>
		<p class="epc-erp-full-guide__lead"><?php echo $esc($e['what']); ?></p>

		<div class="epc-erp-full-guide__cols">
			<div>
				<div class="epc-erp-full-guide__col-hd">Set up (in order)</div>
				<ol>
					<?php foreach ($e['setup'] as $s): ?><li><?php echo $esc($s); ?></li><?php endforeach; ?>
				</ol>
			</div>
			<div>
				<div class="epc-erp-full-guide__col-hd epc-erp-full-guide__col-hd--alt">Daily workflow</div>
				<ol>
					<?php foreach ($e['daily'] as $s): ?><li><?php echo $esc($s); ?></li><?php endforeach; ?>
				</ol>
			</div>
		</div>

		<div class="epc-erp-full-guide__impact">
			<b>Accounting impact:</b> <?php echo $esc($e['accounting']); ?>
		</div>
		<?php if (!empty($e['tips'])): ?>
		<ul class="epc-erp-full-guide__tips">
			<?php foreach ($e['tips'] as $t): ?><li><?php echo $esc($t); ?></li><?php endforeach; ?>
		</ul>
		<?php endif; ?>
	</div>
	<?php endforeach; ?>

	<?php if (!empty($flows)): ?>
	<div class="epc-erp-full-guide__panel" id="g-workflows">
		<h3>Industry document workflows <span>— which document at which stage</span></h3>
		<p class="epc-erp-full-guide__lead" style="margin-bottom:14px">
			End-to-end document chain per industry: who prepares it, the stage, and the posting impact.
		</p>
		<?php foreach ($flows as $ind => $flow): ?>
			<?php $steps = epc_flow_describe($ind); ?>
			<div style="margin-bottom:18px">
				<div class="epc-erp-full-guide__flow-label"><?php echo $esc($flow['label']); ?></div>
				<div style="overflow-x:auto">
					<table>
						<thead>
							<tr>
								<th>#</th>
								<th>Document</th>
								<th>Prepared by</th>
								<th>Stage</th>
								<th>Posting impact</th>
							</tr>
						</thead>
						<tbody>
						<?php foreach ($steps as $s): ?>
							<tr>
								<td><?php echo $esc($s['no']); ?></td>
								<td><b><?php echo $esc($s['doc_code']); ?></b> · <?php echo $esc($s['doc_name']); ?></td>
								<td><?php echo $esc($s['role']); ?></td>
								<td><?php echo $esc($s['stage']); ?></td>
								<td><?php echo $esc($s['posting']); ?></td>
							</tr>
						<?php endforeach; ?>
						</tbody>
					</table>
				</div>
			</div>
		<?php endforeach; ?>
	</div>
	<?php endif; ?>
</div>
<?php
// End full guide.
