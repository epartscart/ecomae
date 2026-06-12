<?php
/**
 * NetSuite-style ERP home dashboard (portlet layout).
 *
 * Renders the /erp/ door home as role/config-driven portlets:
 *   - colour Tiles (quick documents)
 *   - Reminders portlet (live counts from this tenant DB)
 *   - Navigation Shortcut Group
 *   - Key Performance Indicators table (current vs previous period + change)
 *   - Financials snapshot
 *   - KPI Meter gauge (cash & bank)
 *   - A/R Aging bar chart
 *
 * All figures come from the existing tenant-scoped data layer ($dashboard,
 * $dashboard_pl and epc_erp_ar_aging); nothing is hard-coded. The portlet set
 * is resolved per user role so an accounting role and a sales role can see a
 * different home (NetSuite-style role centres).
 *
 * Expected in scope from erp_main.php: $db_link, $dashboard, $dashboard_pl,
 * $erpUrl, $date_from, $date_to, $date_from_str, $date_to_str, $csrf.
 */
defined('_ASTEXE_') or die('No access');

require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_aging.php';

$nsCurrency = '';
if (function_exists('epc_co_profile_get')) {
	try {
		$nsCo = epc_co_profile_get($db_link);
		$nsCurrency = isset($nsCo['base_currency']) ? (string) $nsCo['base_currency'] : '';
	} catch (Exception $e) {
		$nsCurrency = '';
	}
}
if ($nsCurrency === '') {
	$nsCurrency = 'AED';
}

/** Resolve the dashboard role centre for the signed-in user (config-driven). */
$nsRole = 'finance';
if (function_exists('epc_erp_staff_primary_department')) {
	try {
		$dep = strtolower((string) epc_erp_staff_primary_department($db_link));
		if (strpos($dep, 'sales') !== false || strpos($dep, 'crm') !== false) {
			$nsRole = 'sales';
		} elseif (strpos($dep, 'purchas') !== false || strpos($dep, 'procure') !== false) {
			$nsRole = 'purchasing';
		}
	} catch (Exception $e) {
	}
}

/** Previous equal-length period, for trend arrows. */
$nsSpan = max(1, (int) $date_to - (int) $date_from);
$nsPrevTo = (int) $date_from - 1;
$nsPrevFrom = $nsPrevTo - $nsSpan;
$nsPrev = array();
if (function_exists('epc_erp_dashboard')) {
	try {
		$nsPrev = epc_erp_dashboard($db_link, $nsPrevFrom, $nsPrevTo);
	} catch (Exception $e) {
		$nsPrev = array();
	}
}

/** A/R aging buckets (real, tenant-scoped). */
$nsAr = array('labels' => array('Not due', '1-30', '31-60', '61-90', '90+'), 'totals' => array(0, 0, 0, 0, 0), 'grand' => 0.0);
try {
	$nsArFull = epc_erp_ar_aging($db_link);
	$nsAr['labels'] = $nsArFull['labels'];
	$nsAr['totals'] = array_map('floatval', array_values($nsArFull['totals']));
	$nsAr['grand'] = (float) $nsArFull['grand'];
} catch (Exception $e) {
}

$nsMoney = function ($v) {
	return number_format((float) $v, 2);
};
$nsUrl = function ($tab, $area = '') use ($erpUrl, $date_from_str, $date_to_str) {
	return epc_erp_h(epc_erp_tab_url($erpUrl, $tab, $date_from_str, $date_to_str, $area));
};
/** Change badge: returns html for % change current vs previous. */
$nsChange = function ($cur, $prev, $goodWhenUp = true) {
	$cur = (float) $cur;
	$prev = (float) $prev;
	if (abs($prev) < 0.005) {
		return '<span class="ns-chg ns-flat">—</span>';
	}
	$pct = (($cur - $prev) / abs($prev)) * 100.0;
	$up = $pct >= 0;
	$good = $up === $goodWhenUp;
	$arrow = $up ? '&#9650;' : '&#9660;';
	$cls = $good ? 'ns-up' : 'ns-down';
	return '<span class="ns-chg ' . $cls . '">' . $arrow . ' ' . number_format(abs($pct), 1) . '%</span>';
};

// ---- Reminders (live counts; defensive) ----
$nsCount = function (string $sql) use ($db_link): int {
	try {
		return (int) $db_link->query($sql)->fetchColumn();
	} catch (Exception $e) {
		return 0;
	}
};
$remDraftSO = $nsCount("SELECT COUNT(*) FROM `epc_erp_sales_orders` WHERE `status` = 'draft'");
$remConfirmSO = $nsCount("SELECT COUNT(*) FROM `epc_erp_sales_orders` WHERE `status` = 'confirmed'");
$remOpenPO = $nsCount("SELECT COUNT(*) FROM `epc_erp_purchase_orders` WHERE `status` IN ('draft','sent','confirmed')");
$remInvDue = $nsCount("SELECT COUNT(*) FROM `epc_einvoice_documents` WHERE `active` = 1 AND `status` <> 'cancelled' AND (`total_incl_vat` - `paid_amount`) > 0.005");

$reminders = array(
	array('n' => $remDraftSO, 'label' => 'Draft sales orders to confirm', 'url' => $nsUrl('sales_orders', 'sales')),
	array('n' => $remConfirmSO, 'label' => 'Confirmed orders to invoice', 'url' => $nsUrl('sales_orders', 'sales')),
	array('n' => $remOpenPO, 'label' => 'Open purchase orders', 'url' => $nsUrl('purchase_orders', 'purchasing')),
	array('n' => $remInvDue, 'label' => 'Invoices with balance due', 'url' => $nsUrl('aging', 'finance') . '&amp;aging_view=ar'),
);

// ---- Tiles (role-aware) ----
$tilesByRole = array(
	'finance' => array(
		array('label' => 'Balance Sheet', 'icon' => 'fa-balance-scale', 'tone' => 'gold', 'url' => $nsUrl('balance_sheet', 'insights')),
		array('label' => 'General Journal', 'icon' => 'fa-book', 'tone' => 'green', 'url' => $nsUrl('gl', 'finance')),
		array('label' => 'Reconcile Bank', 'icon' => 'fa-university', 'tone' => 'rust', 'url' => $nsUrl('bank_recon', 'finance')),
		array('label' => 'Income Statement', 'icon' => 'fa-line-chart', 'tone' => 'slate', 'url' => $nsUrl('pl', 'insights')),
	),
	'sales' => array(
		array('label' => 'New Sales Order', 'icon' => 'fa-shopping-cart', 'tone' => 'gold', 'url' => $nsUrl('sales_orders', 'sales')),
		array('label' => 'CRM Pipeline', 'icon' => 'fa-handshake-o', 'tone' => 'green', 'url' => $nsUrl('crm', 'sales')),
		array('label' => 'Receivables', 'icon' => 'fa-users', 'tone' => 'rust', 'url' => $nsUrl('receivables', 'sales')),
		array('label' => 'A/R Aging', 'icon' => 'fa-hourglass-half', 'tone' => 'slate', 'url' => $nsUrl('aging', 'finance') . '&amp;aging_view=ar'),
	),
	'purchasing' => array(
		array('label' => 'New Purchase Order', 'icon' => 'fa-clipboard', 'tone' => 'gold', 'url' => $nsUrl('purchase_orders', 'purchasing')),
		array('label' => 'Payables', 'icon' => 'fa-truck', 'tone' => 'green', 'url' => $nsUrl('payables', 'purchasing')),
		array('label' => '3-way Match', 'icon' => 'fa-check-square-o', 'tone' => 'rust', 'url' => $nsUrl('three_way_match', 'purchasing')),
		array('label' => 'A/P Aging', 'icon' => 'fa-hourglass-half', 'tone' => 'slate', 'url' => $nsUrl('aging', 'finance') . '&amp;aging_view=ap'),
	),
);
$tiles = $tilesByRole[$nsRole];

// ---- Navigation shortcut group ----
$navGroups = array(
	'Lists' => array(
		array('label' => 'Items', 'url' => $nsUrl('inventory', 'operations')),
		array('label' => 'Customers', 'url' => $nsUrl('receivables', 'sales')),
		array('label' => 'Vendors', 'url' => $nsUrl('payables', 'purchasing')),
		array('label' => 'Contacts', 'url' => $nsUrl('contacts', 'collaboration')),
	),
	'Transactions' => array(
		array('label' => 'Sales order', 'url' => $nsUrl('sales_orders', 'sales')),
		array('label' => 'Purchase order', 'url' => $nsUrl('purchase_orders', 'purchasing')),
		array('label' => 'Receipt voucher', 'url' => $nsUrl('cash_bank', 'finance')),
		array('label' => 'General ledger', 'url' => $nsUrl('gl', 'finance')),
	),
	'Reports' => array(
		array('label' => 'A/R &amp; A/P aging', 'url' => $nsUrl('aging', 'finance')),
		array('label' => 'Profit &amp; loss', 'url' => $nsUrl('pl', 'insights')),
		array('label' => 'Balance sheet', 'url' => $nsUrl('balance_sheet', 'insights')),
		array('label' => 'VAT return', 'url' => $nsUrl('vat_return', 'finance')),
	),
);

// ---- KPI table values ----
$kpiRows = array(
	array('name' => 'Payables', 'cur' => (float) ($dashboard['payable_balance'] ?? 0), 'prev' => (float) ($nsPrev['payable_balance'] ?? 0), 'goodUp' => false),
	array('name' => 'Sales (ex VAT)', 'cur' => (float) ($dashboard['revenue_ex_vat'] ?? 0), 'prev' => (float) ($nsPrev['revenue_ex_vat'] ?? 0), 'goodUp' => true),
	array('name' => 'Expenses (purchases)', 'cur' => (float) ($dashboard['purchase_ex_vat'] ?? 0), 'prev' => (float) ($nsPrev['purchase_ex_vat'] ?? 0), 'goodUp' => false),
	array('name' => 'Receivables', 'cur' => (float) ($dashboard['customer_ledger_balance'] ?? 0), 'prev' => (float) ($nsPrev['customer_ledger_balance'] ?? 0), 'goodUp' => true),
	array('name' => 'Total bank balance', 'cur' => (float) ($dashboard['cash_bank_total'] ?? 0), 'prev' => (float) ($nsPrev['cash_bank_total'] ?? 0), 'goodUp' => true),
);

// ---- Gauge (cash & bank) ----
$gaugeVal = (float) ($dashboard['cash_bank_total'] ?? 0);
$gaugeScale = max(abs($gaugeVal), abs((float) ($dashboard['revenue_ex_vat'] ?? 0)), 1.0) * 1.25;
$gaugeFrac = max(0.0, min(1.0, ($gaugeVal + $gaugeScale) / (2 * $gaugeScale))); // -scale..+scale mapped 0..1
$gaugeAngle = -90 + ($gaugeFrac * 180); // degrees for needle

// ---- Aging chart geometry ----
$arMax = max(0.01, max($nsAr['totals']));
$arColors = array('#3aa76d', '#e0a83a', '#d98032', '#c0563b', '#9b3b3b');
?>
<style>
.ns-dash{--ns-bd:#d9e1ea;--ns-head:#1f3a52;--ns-muted:#6b7a8d;font-size:13px;color:#27313b}
.ns-dash *{box-sizing:border-box}
.ns-tiles{display:grid;grid-template-columns:repeat(4,1fr);gap:14px;margin:4px 0 16px}
.ns-tile{position:relative;display:flex;flex-direction:column;justify-content:flex-end;min-height:104px;border-radius:6px;padding:14px;color:#fff;text-decoration:none;box-shadow:0 1px 3px rgba(0,0,0,.18);overflow:hidden;transition:transform .12s ease,box-shadow .12s ease}
.ns-tile:hover{transform:translateY(-2px);box-shadow:0 6px 16px rgba(0,0,0,.22);color:#fff}
.ns-tile .ic{position:absolute;top:12px;right:14px;font-size:30px;opacity:.32}
.ns-tile .tl{font-size:15px;font-weight:600;line-height:1.2}
.ns-tile.gold{background:linear-gradient(135deg,#caa23a,#a8821f)}
.ns-tile.green{background:linear-gradient(135deg,#3f9b6d,#2f7d54)}
.ns-tile.rust{background:linear-gradient(135deg,#c2693a,#9c4f29)}
.ns-tile.slate{background:linear-gradient(135deg,#5a6b7b,#41505d)}
.ns-grid{display:grid;grid-template-columns:240px 1fr 320px;gap:16px;align-items:start}
.ns-port{background:#fff;border:1px solid var(--ns-bd);border-radius:6px;margin-bottom:16px}
.ns-port>h4{margin:0;padding:9px 13px;font-size:13px;font-weight:700;color:var(--ns-head);border-bottom:1px solid var(--ns-bd);background:#f5f8fb;border-radius:6px 6px 0 0}
.ns-port .bd{padding:11px 13px}
.ns-rem{list-style:none;margin:0;padding:0}
.ns-rem li{display:flex;align-items:center;gap:10px;padding:7px 0;border-bottom:1px dashed #e7edf3}
.ns-rem li:last-child{border-bottom:0}
.ns-rem .cnt{flex:0 0 34px;text-align:center;font-weight:700;color:#fff;background:#c0563b;border-radius:4px;padding:3px 0;font-size:13px}
.ns-rem .cnt.zero{background:#9fb0c0}
.ns-rem a{color:#27313b;text-decoration:none}
.ns-rem a:hover{color:#1f6fb2;text-decoration:underline}
.ns-nav h5{margin:10px 0 4px;font-size:11px;letter-spacing:.04em;text-transform:uppercase;color:var(--ns-muted)}
.ns-nav ul{list-style:none;margin:0 0 6px;padding:0}
.ns-nav li{padding:3px 0}
.ns-nav a{color:#1f6fb2;text-decoration:none}
.ns-nav a:hover{text-decoration:underline}
.ns-kpi-tbl{width:100%;border-collapse:collapse}
.ns-kpi-tbl th,.ns-kpi-tbl td{padding:8px 10px;border-bottom:1px solid #eef2f6;text-align:right}
.ns-kpi-tbl th{font-size:11px;text-transform:uppercase;letter-spacing:.03em;color:var(--ns-muted);background:#f9fbfd}
.ns-kpi-tbl td:first-child,.ns-kpi-tbl th:first-child{text-align:left}
.ns-chg{font-weight:600;font-size:12px;white-space:nowrap}
.ns-up{color:#2f7d54}.ns-down{color:#c0563b}.ns-flat{color:#9fb0c0}
.ns-fin{display:grid;grid-template-columns:repeat(4,1fr);gap:10px}
.ns-fin .cell{border:1px solid #eef2f6;border-radius:5px;padding:10px;background:#fbfdff}
.ns-fin .cell .l{font-size:11px;color:var(--ns-muted);text-transform:uppercase;letter-spacing:.03em}
.ns-fin .cell .v{font-size:17px;font-weight:700;margin-top:3px}
.ns-gauge{text-align:center;padding:4px 0 2px}
.ns-gauge .gval{font-size:22px;font-weight:800;margin-top:-6px}
.ns-gauge .gsub{font-size:11px;color:var(--ns-muted)}
.ns-bars{display:flex;align-items:flex-end;gap:10px;height:160px;padding:6px 4px 0}
.ns-bars .col{flex:1;display:flex;flex-direction:column;align-items:center;justify-content:flex-end;height:100%}
.ns-bars .bar{width:100%;max-width:34px;border-radius:3px 3px 0 0;min-height:2px}
.ns-bars .amt{font-size:10px;color:var(--ns-muted);margin-bottom:3px}
.ns-bars .lab{font-size:10px;color:#27313b;margin-top:5px;text-align:center}
.ns-total{text-align:right;font-size:12px;color:var(--ns-muted);margin-top:6px}
@media(max-width:1100px){.ns-grid{grid-template-columns:1fr}.ns-tiles{grid-template-columns:repeat(2,1fr)}}
</style>

<div class="ns-dash">
	<div class="ns-tiles">
		<?php foreach ($tiles as $t): ?>
			<a class="ns-tile <?php echo epc_erp_h($t['tone']); ?>" href="<?php echo $t['url']; ?>">
				<i class="fa <?php echo epc_erp_h($t['icon']); ?> ic"></i>
				<span class="tl"><?php echo epc_erp_h($t['label']); ?></span>
			</a>
		<?php endforeach; ?>
	</div>

	<div class="ns-grid">
		<!-- LEFT: reminders + nav shortcuts -->
		<div class="ns-col-left">
			<div class="ns-port">
				<h4><i class="fa fa-bell-o"></i> Reminders</h4>
				<div class="bd">
					<ul class="ns-rem">
						<?php foreach ($reminders as $r): ?>
							<li>
								<span class="cnt<?php echo $r['n'] === 0 ? ' zero' : ''; ?>"><?php echo (int) $r['n']; ?></span>
								<a href="<?php echo $r['url']; ?>"><?php echo epc_erp_h($r['label']); ?></a>
							</li>
						<?php endforeach; ?>
					</ul>
				</div>
			</div>
			<div class="ns-port ns-nav">
				<h4><i class="fa fa-bars"></i> Navigation shortcuts</h4>
				<div class="bd">
					<?php foreach ($navGroups as $grp => $links): ?>
						<h5><?php echo $grp; ?></h5>
						<ul>
							<?php foreach ($links as $l): ?>
								<li><a href="<?php echo $l['url']; ?>"><?php echo $l['label']; ?></a></li>
							<?php endforeach; ?>
						</ul>
					<?php endforeach; ?>
				</div>
			</div>
		</div>

		<!-- CENTER: KPI table + financials -->
		<div class="ns-col-mid">
			<div class="ns-port">
				<h4><i class="fa fa-tachometer"></i> Key performance indicators</h4>
				<div class="bd" style="padding:0">
					<table class="ns-kpi-tbl">
						<thead><tr><th>Indicator</th><th>Current</th><th>Previous</th><th>Change</th></tr></thead>
						<tbody>
							<?php foreach ($kpiRows as $k): ?>
								<tr>
									<td><?php echo epc_erp_h($k['name']); ?></td>
									<td><?php echo $nsMoney($k['cur']); ?></td>
									<td><?php echo $nsMoney($k['prev']); ?></td>
									<td><?php echo $nsChange($k['cur'], $k['prev'], $k['goodUp']); ?></td>
								</tr>
							<?php endforeach; ?>
						</tbody>
					</table>
				</div>
			</div>
			<div class="ns-port">
				<h4><i class="fa fa-money"></i> Financials</h4>
				<div class="bd">
					<?php
					$rev = (float) ($dashboard['revenue_ex_vat'] ?? 0);
					$prof = (float) ($dashboard['profit_ex_vat'] ?? 0);
					$gpPct = $rev > 0.005 ? ($prof / $rev) * 100.0 : 0.0;
					$netPl = (float) ($dashboard_pl['net_profit'] ?? 0);
					$fin = array(
						array('l' => 'Gross profit %', 'v' => number_format($gpPct, 1) . '%'),
						array('l' => 'Margin (ex VAT)', 'v' => $nsMoney($prof) . ' ' . $nsCurrency),
						array('l' => 'GL net profit', 'v' => $nsMoney($netPl) . ' ' . $nsCurrency),
						array('l' => 'Cash &amp; bank', 'v' => $nsMoney((float) ($dashboard['cash_bank_total'] ?? 0)) . ' ' . $nsCurrency),
						array('l' => 'Net VAT', 'v' => $nsMoney((float) ($dashboard['vat_net_payable'] ?? 0)) . ' ' . $nsCurrency),
						array('l' => 'Sales incl. VAT', 'v' => $nsMoney((float) ($dashboard['sales_incl_vat'] ?? 0)) . ' ' . $nsCurrency),
						array('l' => 'Completed orders', 'v' => (string) (int) ($dashboard['order_count'] ?? 0)),
						array('l' => 'Due on orders', 'v' => $nsMoney((float) ($dashboard['receivable_due_orders'] ?? 0)) . ' ' . $nsCurrency),
					);
					?>
					<div class="ns-fin">
						<?php foreach ($fin as $f): ?>
							<div class="cell"><div class="l"><?php echo $f['l']; ?></div><div class="v"><?php echo $f['v']; ?></div></div>
						<?php endforeach; ?>
					</div>
				</div>
			</div>
		</div>

		<!-- RIGHT: gauge + aging chart -->
		<div class="ns-col-right">
			<div class="ns-port">
				<h4><i class="fa fa-dashboard"></i> KPI meter — Total bank balance</h4>
				<div class="bd">
					<div class="ns-gauge">
						<svg viewBox="0 0 200 120" width="100%" height="120">
							<path d="M20 110 A80 80 0 0 1 180 110" fill="none" stroke="#eef2f6" stroke-width="16" stroke-linecap="round"/>
							<path d="M20 110 A80 80 0 0 1 180 110" fill="none" stroke="#3aa76d" stroke-width="16" stroke-linecap="round"
								stroke-dasharray="<?php echo number_format($gaugeFrac * 251.3, 1); ?> 251.3"/>
							<g transform="rotate(<?php echo number_format($gaugeAngle, 1); ?> 100 110)">
								<line x1="100" y1="110" x2="100" y2="40" stroke="#1f3a52" stroke-width="3"/>
							</g>
							<circle cx="100" cy="110" r="6" fill="#1f3a52"/>
						</svg>
						<div class="gval" style="color:<?php echo $gaugeVal >= 0 ? '#2f7d54' : '#c0563b'; ?>"><?php echo $nsMoney($gaugeVal); ?></div>
						<div class="gsub"><?php echo $nsCurrency; ?> · live cash &amp; bank position</div>
					</div>
				</div>
			</div>
			<div class="ns-port">
				<h4><i class="fa fa-bar-chart"></i> A/R aging — graph</h4>
				<div class="bd">
					<div class="ns-bars">
						<?php foreach ($nsAr['labels'] as $i => $lab): ?>
							<?php $amt = (float) ($nsAr['totals'][$i] ?? 0); $h = (int) round(($amt / $arMax) * 140); ?>
							<div class="col">
								<span class="amt"><?php echo $amt > 0 ? $nsMoney($amt) : ''; ?></span>
								<div class="bar" style="height:<?php echo max(2, $h); ?>px;background:<?php echo $arColors[$i % count($arColors)]; ?>"></div>
								<span class="lab"><?php echo epc_erp_h($lab); ?></span>
							</div>
						<?php endforeach; ?>
					</div>
					<div class="ns-total">Total receivable: <strong><?php echo $nsMoney($nsAr['grand']); ?> <?php echo $nsCurrency; ?></strong></div>
				</div>
			</div>
		</div>
	</div>
</div>
