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
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_bos_intelligence.php';

// ---- Operational KPIs (BOS industry-intelligence engine, tenant-scoped) ----
// Reuse $dashboard / $dashboard_pl already computed in erp_main.php.
$opKpis = array();
try {
	$opKpis = epc_bos_intel_kpis(
		$db_link,
		(int) $date_from,
		(int) $date_to,
		isset($dashboard) && is_array($dashboard) ? $dashboard : null,
		isset($dashboard_pl) && is_array($dashboard_pl) ? $dashboard_pl : null
	);
} catch (Throwable $e) {
	$opKpis = array();
}

$nsCurrency = '';
$nsCo = array();
if (function_exists('epc_co_profile_get')) {
	try {
		$nsCo = epc_co_profile_get($db_link);
		if (!is_array($nsCo)) { $nsCo = array(); }
		$nsCurrency = isset($nsCo['base_currency']) ? (string) $nsCo['base_currency'] : '';
	} catch (Exception $e) {
		$nsCurrency = '';
		$nsCo = array();
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
		// Light path: trend arrows need order/cash/AR/AP only — skip VAT + CC tiles.
		$nsPrev = epc_erp_dashboard($db_link, $nsPrevFrom, $nsPrevTo, true);
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
/** Deep-link straight into an External Reporting report (cat + rep, auto-fetch). */
$nsExtUrl = function ($cat, $rep) use ($nsUrl) {
	return $nsUrl('ext_reports', 'regrep')
		. '&amp;cat=' . urlencode($cat)
		. '&amp;rep=' . urlencode($rep)
		. '&amp;fetch=1';
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
		array('label' => 'Items', 'icon' => 'fa-cubes', 'url' => $nsUrl('inventory', 'operations')),
		array('label' => 'Customers', 'icon' => 'fa-users', 'url' => $nsUrl('receivables', 'sales')),
		array('label' => 'Vendors', 'icon' => 'fa-truck', 'url' => $nsUrl('payables', 'purchasing')),
		array('label' => 'Contacts', 'icon' => 'fa-address-book-o', 'url' => $nsUrl('contacts', 'collaboration')),
	),
	'Transactions' => array(
		array('label' => 'Sales order', 'icon' => 'fa-shopping-cart', 'url' => $nsUrl('sales_orders', 'sales')),
		array('label' => 'Purchase order', 'icon' => 'fa-clipboard', 'url' => $nsUrl('purchase_orders', 'purchasing')),
		array('label' => 'Receipt voucher', 'icon' => 'fa-money', 'url' => $nsUrl('cash_bank', 'finance')),
		array('label' => 'General ledger', 'icon' => 'fa-book', 'url' => $nsUrl('gl', 'finance')),
	),
	'Reports' => array(
		array('label' => 'Financial report (IFRS)', 'icon' => 'fa-file-text-o', 'url' => $nsExtUrl('audit', 'audit__external_audit_report')),
		array('label' => 'VAT return (VAT 201)', 'icon' => 'fa-percent', 'url' => $nsExtUrl('tax', 'tax__vat_return')),
		array('label' => 'Corporate tax return', 'icon' => 'fa-balance-scale', 'url' => $nsExtUrl('tax', 'tax__corporate_income_tax_return')),
		array('label' => 'Profit &amp; loss', 'icon' => 'fa-line-chart', 'url' => $nsUrl('pl', 'insights')),
	),
);

// ---- Quick actions (visual icon cards, role-aware first row) ----
// Statutory reports first — quick-open straight into the External Reporting pack
// (tenant-country-driven: UAE -> IFRS/ISA + FTA VAT 201 / CT; auto-localises).
$quickActions = array(
	array('label' => 'Financial Report (IFRS)', 'icon' => 'fa-file-text-o', 'tone' => 'qa-indigo', 'url' => $nsExtUrl('audit', 'audit__external_audit_report')),
	array('label' => 'VAT Return (VAT 201)', 'icon' => 'fa-percent', 'tone' => 'qa-green', 'url' => $nsExtUrl('tax', 'tax__vat_return')),
	array('label' => 'Corporate Tax Return', 'icon' => 'fa-balance-scale', 'tone' => 'qa-rust', 'url' => $nsExtUrl('tax', 'tax__corporate_income_tax_return')),
	array('label' => 'New Sales Order', 'icon' => 'fa-shopping-cart', 'tone' => 'qa-blue', 'url' => $nsUrl('sales_orders', 'sales')),
	array('label' => 'New Purchase Order', 'icon' => 'fa-clipboard', 'tone' => 'qa-indigo', 'url' => $nsUrl('purchase_orders', 'purchasing')),
	array('label' => 'New Item', 'icon' => 'fa-cubes', 'tone' => 'qa-amber', 'url' => $nsUrl('inventory', 'operations')),
	array('label' => 'New Customer', 'icon' => 'fa-user-plus', 'tone' => 'qa-pink', 'url' => $nsUrl('receivables', 'sales')),
	array('label' => 'New Vendor', 'icon' => 'fa-truck', 'tone' => 'qa-teal', 'url' => $nsUrl('payables', 'purchasing')),
	array('label' => 'Receipt Voucher', 'icon' => 'fa-money', 'tone' => 'qa-green', 'url' => $nsUrl('cash_bank', 'finance')),
	array('label' => 'General Ledger', 'icon' => 'fa-book', 'tone' => 'qa-slate', 'url' => $nsUrl('gl', 'finance')),
	array('label' => 'VAT Return', 'icon' => 'fa-percent', 'tone' => 'qa-rust', 'url' => $nsUrl('vat_return', 'finance')),
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

// ---- Hero metric strip (primary financial pulse) ----
$nsHeroEntity = 'Operations';
if (!empty($nsCo) && is_array($nsCo)) {
	$nsHeroEntity = (string) (($nsCo['legal_name'] ?? '') !== '' ? $nsCo['legal_name'] : (($nsCo['trade_name'] ?? '') !== '' ? $nsCo['trade_name'] : $nsHeroEntity));
}
$nsHeroMetrics = array(
	array('label' => 'Cash & bank', 'cur' => (float) ($dashboard['cash_bank_total'] ?? 0), 'prev' => (float) ($nsPrev['cash_bank_total'] ?? 0), 'goodUp' => true, 'money' => true),
	array('label' => 'Sales (ex VAT)', 'cur' => (float) ($dashboard['revenue_ex_vat'] ?? 0), 'prev' => (float) ($nsPrev['revenue_ex_vat'] ?? 0), 'goodUp' => true, 'money' => true),
	array('label' => 'Receivables', 'cur' => (float) ($dashboard['customer_ledger_balance'] ?? 0), 'prev' => (float) ($nsPrev['customer_ledger_balance'] ?? 0), 'goodUp' => true, 'money' => true),
	array('label' => 'Payables', 'cur' => (float) ($dashboard['payable_balance'] ?? 0), 'prev' => (float) ($nsPrev['payable_balance'] ?? 0), 'goodUp' => false, 'money' => true),
);
$nsRoleLabel = array('finance' => 'Finance centre', 'sales' => 'Sales centre', 'purchasing' => 'Purchasing centre');
// Theme URL relative to docroot (works under /erp/ shell and CP).
$nsCssCandidates = array(
	'/cp/content/shop/finance/erp/theme/erp_dashboard_premium.css',
	'/content/shop/finance/erp/theme/erp_dashboard_premium.css',
);
$nsCssHref = '/cp/content/shop/finance/erp/theme/erp_dashboard_premium.css';
foreach ($nsCssCandidates as $c) {
	$abs = rtrim((string) ($_SERVER['DOCUMENT_ROOT'] ?? ''), '/') . $c;
	if (is_file($abs)) { $nsCssHref = $c; break; }
}
?>
<link rel="stylesheet" href="<?php echo epc_erp_h($nsCssHref); ?>?v=20260720">

<div class="ns-dash">
	<div class="ns-hero">
		<div class="ns-hero-panel">
			<div class="ns-hero-kicker">Command centre</div>
			<h2 class="ns-hero-title"><?php echo epc_erp_h($nsHeroEntity); ?></h2>
			<p class="ns-hero-sub">Premium operational view — live KPIs, receivables aging and executive trends for the selected period, presented with depth and clarity.</p>
			<div class="ns-hero-meta">
				<span class="ns-chip"><i class="fa fa-calendar"></i> <?php echo epc_erp_h($date_from_str); ?> → <?php echo epc_erp_h($date_to_str); ?></span>
				<span class="ns-chip"><i class="fa fa-money"></i> <?php echo epc_erp_h($nsCurrency); ?></span>
				<span class="ns-chip"><i class="fa fa-user"></i> <?php echo epc_erp_h($nsRoleLabel[$nsRole] ?? 'Finance centre'); ?></span>
			</div>
		</div>
		<div class="ns-hero-metrics">
			<?php foreach ($nsHeroMetrics as $hm): ?>
				<div class="ns-metric3d">
					<div class="ml"><?php echo epc_erp_h($hm['label']); ?></div>
					<div class="mv" data-ns-count="<?php echo epc_erp_h((string) $hm['cur']); ?>" data-ns-prefix=""><?php echo $nsMoney($hm['cur']); ?></div>
					<div class="md"><?php echo $nsChange($hm['cur'], $hm['prev'], $hm['goodUp']); ?> vs prior period</div>
				</div>
			<?php endforeach; ?>
		</div>
	</div>

	<div class="ns-tiles">
		<?php foreach ($tiles as $t): ?>
			<a class="ns-tile <?php echo epc_erp_h($t['tone']); ?>" href="<?php echo $t['url']; ?>">
				<i class="fa <?php echo epc_erp_h($t['icon']); ?> ic"></i>
				<span class="tl"><?php echo epc_erp_h($t['label']); ?></span>
			</a>
		<?php endforeach; ?>
	</div>

	<div class="ns-port">
		<h4><i class="fa fa-bolt"></i> Quick actions</h4>
		<div class="bd">
			<div class="ns-qa-grid">
				<?php foreach ($quickActions as $qa): ?>
					<a class="ns-qa" href="<?php echo $qa['url']; ?>">
						<span class="qa-ic <?php echo epc_erp_h($qa['tone']); ?>"><i class="fa <?php echo epc_erp_h($qa['icon']); ?>"></i></span>
						<span class="qa-lb"><?php echo epc_erp_h($qa['label']); ?></span>
					</a>
				<?php endforeach; ?>
			</div>
		</div>
	</div>

	<?php if (!empty($opKpis)): ?>
	<div class="ns-port">
		<h4><i class="fa fa-tachometer"></i> Operational KPIs <span style="font-weight:400;color:var(--ns-muted);font-size:11px;">— live, period <?php echo epc_erp_h($date_from_str); ?> to <?php echo epc_erp_h($date_to_str); ?></span>
			<a href="#ns-industry-controls" style="float:right;font-weight:400;font-size:11px;">Industry controls &darr;</a>
		</h4>
		<div class="bd">
			<div class="ns-kpi-grid">
				<?php foreach ($opKpis as $k): $hc = epc_erp_h($k['health']); ?>
					<div class="ns-kpi-card <?php echo $hc; ?>" title="<?php echo epc_erp_h($k['hint']); ?>">
						<span class="dot"></span>
						<div class="kl"><?php echo epc_erp_h($k['label']); ?></div>
						<div class="kv"><?php echo epc_erp_h(epc_bos_intel_format((float) $k['value'], (string) $k['format'])); ?></div>
						<div class="kh"><?php echo epc_erp_h($k['hint']); ?></div>
					</div>
				<?php endforeach; ?>
			</div>
		</div>
	</div>
	<?php endif; ?>

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
					<?php
					$navTones = array('Lists' => 'ns-mi-teal', 'Transactions' => 'ns-mi-blue', 'Reports' => 'ns-mi-amber');
					foreach ($navGroups as $grp => $links):
						$tone = $navTones[$grp] ?? 'ns-mi-blue';
					?>
						<h5><?php echo $grp; ?></h5>
						<div class="ns-mini-grid">
							<?php foreach ($links as $l): ?>
								<a class="ns-mini" href="<?php echo $l['url']; ?>"><span class="mi <?php echo $tone; ?>"><i class="fa <?php echo epc_erp_h($l['icon']); ?>"></i></span><span class="ml"><?php echo $l['label']; ?></span></a>
							<?php endforeach; ?>
						</div>
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
							<defs>
								<linearGradient id="nsGaugeGrad" x1="0%" y1="0%" x2="100%" y2="0%">
									<stop offset="0%" stop-color="#0d9488"/>
									<stop offset="55%" stop-color="#0b6e99"/>
									<stop offset="100%" stop-color="#c2811a"/>
								</linearGradient>
							</defs>
							<path d="M20 110 A80 80 0 0 1 180 110" fill="none" stroke="#d9e4ec" stroke-width="16" stroke-linecap="round"/>
							<path d="M20 110 A80 80 0 0 1 180 110" fill="none" stroke="url(#nsGaugeGrad)" stroke-width="16" stroke-linecap="round"
								stroke-dasharray="<?php echo number_format($gaugeFrac * 251.3, 1); ?> 251.3"/>
							<g transform="rotate(<?php echo number_format($gaugeAngle, 1); ?> 100 110)">
								<line x1="100" y1="110" x2="100" y2="40" stroke="#102a43" stroke-width="3"/>
							</g>
							<circle cx="100" cy="110" r="7" fill="#102a43"/>
							<circle cx="100" cy="110" r="3" fill="#0d9488"/>
						</svg>
						<div class="gval" style="color:<?php echo $gaugeVal >= 0 ? '#1a7f4b' : '#b42318'; ?>"><?php echo $nsMoney($gaugeVal); ?></div>
						<div class="gsub"><?php echo $nsCurrency; ?> · live cash &amp; bank position</div>
					</div>
				</div>
			</div>
			<div class="ns-port">
				<h4><i class="fa fa-bar-chart"></i> A/R aging — graphical</h4>
				<div class="bd">
					<div class="ns-chart-wrap"><canvas id="nsChartAr" aria-label="A/R aging chart"></canvas></div>
					<div class="ns-total">Total receivable: <strong><?php echo $nsMoney($nsAr['grand']); ?> <?php echo $nsCurrency; ?></strong></div>
				</div>
			</div>
		</div>
	</div>
</div>

<?php
/* ------------------------------------------------------------------ *
 * Unified executive + industry-intelligence cockpit.
 * Folds the former Executive dashboard and Industry intelligence tabs
 * into the main dashboard so /erp/ is the single analytics view.
 * All figures are tenant-scoped; empty tenants render empty, not error.
 * ------------------------------------------------------------------ */
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_exec_dashboard.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_bos_intelligence.php';

$nsCsrf = isset($csrf) ? (string) $csrf : '';
$nsTrend = array();
$nsTopSup = array();
$nsAlerts = array('danger' => 0, 'warning' => 0, 'info' => 0, 'default' => 0, 'total' => 0);
$nsControls = array();
$nsCtrlState = array();
$nsIntelCtx = array('pack_label' => '', 'profile_label' => '');
try { $nsTrend = epc_exec_trend($db_link, 6); } catch (Throwable $e) { $nsTrend = array(); }
try { $nsTopSup = epc_exec_top_suppliers($db_link, 5); } catch (Throwable $e) { $nsTopSup = array(); }
try { $nsAlerts = epc_exec_planning_alerts($db_link); } catch (Throwable $e) {}
try {
	$nsIntelCtx = epc_bos_intel_context($db_link);
	$nsControls = epc_bos_intel_controls($db_link, $nsIntelCtx);
	$nsCtrlState = epc_bos_intel_control_state($db_link);
} catch (Throwable $e) { $nsControls = array(); }

$nsTrendMax = 0.0;
foreach ($nsTrend as $t) {
	$nsTrendMax = max($nsTrendMax, (float) $t['revenue'], (float) $t['profit']);
}
if ($nsTrendMax <= 0) { $nsTrendMax = 1.0; }

$nsCtrlDone = 0;
foreach ($nsControls as $c) {
	if (!empty($nsCtrlState[$c['code']])) { $nsCtrlDone++; }
}
$nsIndustryLabel = '';
if (!empty($nsIntelCtx['pack_label'])) { $nsIndustryLabel = (string) $nsIntelCtx['pack_label']; }
elseif (!empty($nsIntelCtx['profile_label'])) { $nsIndustryLabel = (string) $nsIntelCtx['profile_label']; }
if ($nsIndustryLabel === '') { $nsIndustryLabel = 'General (no industry pack applied)'; }
$nsCtrlHealth = array('good' => '#107c10', 'warn' => '#c19c00', 'bad' => '#a4262c', 'info' => '#0078d4');

/* ---- Process-flow task analytics (defensive; degrades to empty) ---- */
$pfSummary = array('open' => 0, 'done' => 0, 'overdue' => 0, 'avg_cycle_hours' => 0.0, 'by_department' => array(), 'headcount' => 0);
$pfTop = array();
$pfBusy = 0;
$pfDoneTotal = 0;
$pfDeptName = function ($code) { return $code === '' ? 'Unassigned' : ucfirst((string) $code); };
try {
	require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_processflow.php';
	if (function_exists('epc_erp_staff_department_name')) {
		$pfDeptName = function ($code) {
			$code = (string) $code;
			return $code === '' ? 'Unassigned' : (epc_erp_staff_department_name($code) ?: ucfirst($code));
		};
	}
	$pfRange = array('from' => (int) $date_from, 'to' => (int) $date_to);
	if (function_exists('epc_pf_monitor_summary')) {
		$pfSummary = epc_pf_monitor_summary($db_link, $pfRange);
	}
	if (function_exists('epc_pf_workforce_data')) {
		$wf = epc_pf_workforce_data($db_link, $pfRange);
		$pfBusy = (int) ($wf['busy'] ?? 0);
		$pfDoneTotal = (int) ($wf['doneTotal'] ?? 0);
		$people = $wf['staff'] ?? array();
		usort($people, function ($a, $b) { return (int) $b['done'] <=> (int) $a['done']; });
		foreach ($people as $p) {
			if ((int) $p['done'] <= 0) { continue; }
			$pfTop[] = $p;
			if (count($pfTop) >= 8) { break; }
		}
	}
} catch (Throwable $e) {
}
$pfDeptRows = $pfSummary['by_department'] ?? array();
arsort($pfDeptRows);
$pfDeptMax = $pfDeptRows ? max($pfDeptRows) : 0;
$pfTopMax = $pfTop ? max(array_map(function ($p) { return (int) $p['done']; }, $pfTop)) : 0;
$pfHasTasks = (((int) $pfSummary['open']) + ((int) $pfSummary['done']) + ((int) $pfSummary['overdue']) + $pfDoneTotal) > 0;
$pfUrl = function ($view = 'monitor') use ($erpUrl, $date_from_str, $date_to_str) {
	return epc_erp_h(epc_erp_tab_url($erpUrl, 'processflow', $date_from_str, $date_to_str) . '&pf_view=' . $view);
};
$nsTrendLabels = array();
$nsTrendRev = array();
$nsTrendProf = array();
foreach ($nsTrend as $t) {
	$nsTrendLabels[] = (string) ($t['label'] ?? '');
	$nsTrendRev[] = (float) ($t['revenue'] ?? 0);
	$nsTrendProf[] = (float) ($t['profit'] ?? 0);
}
?>

<div class="ns-dash ns-exec">
	<h3 class="ns-exec-h"><i class="fa fa-dashboard"></i> Executive cockpit — full-system analytics</h3>
	<div id="epc_erp_msg" class="alert" style="display:none;"></div>
	<div style="margin-bottom:14px;">
		<form data-bos-action="demo_seed_sales" style="display:inline-block;margin:0;">
			<input type="hidden" name="csrf_guard_key" value="<?php echo epc_erp_h($nsCsrf); ?>">
			<button type="submit" class="btn btn-sm btn-primary"><i class="fa fa-database"></i> Generate sample sales</button>
		</form>
		<form data-bos-action="demo_clear_sales" style="display:inline-block;margin:0;">
			<input type="hidden" name="csrf_guard_key" value="<?php echo epc_erp_h($nsCsrf); ?>">
			<button type="submit" class="btn btn-sm btn-default"><i class="fa fa-eraser"></i> Clear sample sales</button>
		</form>
		<span class="text-muted" style="margin-left:8px;font-size:12px;">Seeds 6 months of completed orders (tagged, re-runnable) so the revenue trend and KPIs populate.</span>
	</div>

	<div class="ns-exec-grid">
		<div class="ns-port">
			<h4><i class="fa fa-line-chart"></i> Revenue &amp; profit — last 6 months</h4>
			<div class="bd">
				<div class="ns-chart-wrap tall"><canvas id="nsChartTrend" aria-label="Revenue and profit trend"></canvas></div>
				<div class="ns-leg"><span class="sq" style="background:#0b6e99;"></span>Revenue &nbsp; <span class="sq" style="background:#0d9488;"></span>Profit (ex-VAT)</div>
			</div>
		</div>
		<div class="ns-port">
			<h4><i class="fa fa-exclamation-triangle"></i> Planning alerts</h4>
			<div class="bd">
				<table class="table table-condensed table-bordered" style="margin-bottom:8px;">
					<tbody>
						<tr class="danger"><th>Stock-out / critical</th><td class="text-right"><?php echo (int) $nsAlerts['danger']; ?></td></tr>
						<tr class="warning"><th>Below safety stock</th><td class="text-right"><?php echo (int) $nsAlerts['warning']; ?></td></tr>
						<tr class="info"><th>Excess stock</th><td class="text-right"><?php echo (int) $nsAlerts['info']; ?></td></tr>
						<tr><th>Dead stock</th><td class="text-right"><?php echo (int) $nsAlerts['default']; ?></td></tr>
						<tr><th>Total exceptions</th><td class="text-right"><strong><?php echo (int) $nsAlerts['total']; ?></strong></td></tr>
					</tbody>
				</table>
				<a href="<?php echo epc_erp_h(epc_erp_tab_url($erpUrl, 'order_planning', $date_from_str, $date_to_str) . '&opl_view=exceptions'); ?>">View exceptions &raquo;</a>
			</div>
		</div>
	</div>

	<div class="ns-exec-grid" style="margin-top:16px;">
		<div class="ns-port">
			<h4><i class="fa fa-truck"></i> Top suppliers by spend</h4>
			<div class="bd">
				<?php if (empty($nsTopSup)): ?>
					<p class="text-muted">No supplier spend recorded yet.</p>
				<?php else: ?>
				<table class="table table-condensed table-bordered table-hover" style="margin-bottom:0;">
					<thead><tr><th>Supplier</th><th class="text-center">Rating</th><th class="text-right">Spend</th><th class="text-right">POs</th><th class="text-right">Score</th></tr></thead>
					<tbody>
					<?php foreach ($nsTopSup as $s): ?>
						<tr>
							<td><?php echo epc_erp_h($s['name']); ?></td>
							<td class="text-center"><?php echo epc_erp_h($s['rating']); ?></td>
							<td class="text-right"><?php echo epc_erp_money($s['spend']); ?></td>
							<td class="text-right"><?php echo (int) $s['po_count']; ?></td>
							<td class="text-right"><?php echo epc_erp_money($s['score']); ?></td>
						</tr>
					<?php endforeach; ?>
					</tbody>
				</table>
				<?php endif; ?>
			</div>
		</div>
		<div class="ns-port">
			<h4><i class="fa fa-link"></i> Quick links</h4>
			<div class="bd">
				<div class="list-group" style="margin-bottom:0;">
					<a class="list-group-item" href="<?php echo epc_erp_h(epc_erp_tab_url($erpUrl, 'ai_advisor', $date_from_str, $date_to_str)); ?>"><i class="fa fa-magic"></i> AI advisor &amp; forecasts</a>
					<a class="list-group-item" href="<?php echo epc_erp_h(epc_erp_tab_url($erpUrl, 'pl', $date_from_str, $date_to_str)); ?>"><i class="fa fa-bar-chart"></i> Profit &amp; loss</a>
					<a class="list-group-item" href="<?php echo epc_erp_h(epc_erp_tab_url($erpUrl, 'order_planning', $date_from_str, $date_to_str)); ?>"><i class="fa fa-cubes"></i> Order planning</a>
					<a class="list-group-item" href="<?php echo epc_erp_h(epc_erp_tab_url($erpUrl, 'supplier_portal', $date_from_str, $date_to_str)); ?>"><i class="fa fa-handshake-o"></i> Supplier portal</a>
				</div>
			</div>
		</div>
	</div>

	<h3 class="ns-exec-h" id="ns-task-analytics" style="margin-top:22px;"><i class="fa fa-sitemap"></i> Task analytics — process flow across every department
		<small class="text-muted" style="font-weight:400;">· auto-tracked customer orders, purchase orders &amp; your own processes</small>
		<a href="<?php echo $pfUrl('monitor'); ?>" class="btn btn-xs btn-default" style="float:right;"><i class="fa fa-external-link"></i> Open process flow</a>
	</h3>
	<?php if (!$pfHasTasks): ?>
	<div class="ns-port"><div class="bd">
		<p class="text-muted" style="margin:0;">No tasks tracked yet. Customer orders, purchase orders and your own processes auto-create cases and appear here as soon as work starts flowing. <a href="<?php echo $pfUrl('monitor'); ?>">Open process flow &raquo;</a></p>
	</div></div>
	<?php else: ?>
	<div class="ns-pf-kpis">
		<div class="ns-pf-card"><div class="v"><?php echo (int) $pfSummary['open']; ?></div><div class="l">Open tasks</div></div>
		<div class="ns-pf-card <?php echo ((int) $pfSummary['overdue'] > 0) ? 'bad' : ''; ?>"><div class="v"><?php echo (int) $pfSummary['overdue']; ?></div><div class="l">Overdue (SLA)</div></div>
		<div class="ns-pf-card good"><div class="v"><?php echo (int) $pfDoneTotal; ?></div><div class="l">Tasks completed in period</div></div>
		<div class="ns-pf-card"><div class="v"><?php echo (int) round((float) $pfSummary['avg_cycle_hours']); ?><small>h</small></div><div class="l">Avg cycle time</div></div>
		<div class="ns-pf-card"><div class="v"><?php echo (int) $pfBusy; ?><small>/<?php echo (int) ($pfSummary['headcount'] ?: 0); ?></small></div><div class="l">Staff busy now</div></div>
	</div>
	<div class="ns-exec-grid" style="margin-top:16px;">
		<div class="ns-port">
			<h4><i class="fa fa-building-o"></i> Open workload by department</h4>
			<div class="bd">
				<?php if (empty($pfDeptRows)): ?>
					<p class="text-muted" style="margin:0;">No open tasks.</p>
				<?php else: foreach ($pfDeptRows as $code => $cnt): $w = $pfDeptMax > 0 ? max(4, round(($cnt / $pfDeptMax) * 100)) : 0; ?>
					<div class="ns-pf-bar">
						<div class="nm"><?php echo epc_erp_h($pfDeptName((string) $code)); ?></div>
						<div class="tr"><div class="fl" style="width:<?php echo (int) $w; ?>%;"></div></div>
						<div class="ct"><?php echo (int) $cnt; ?></div>
					</div>
				<?php endforeach; endif; ?>
			</div>
		</div>
		<div class="ns-port">
			<h4><i class="fa fa-trophy"></i> Top performers <small class="text-muted" style="font-weight:400;">(tasks completed)</small></h4>
			<div class="bd">
				<?php if (empty($pfTop)): ?>
					<p class="text-muted" style="margin:0;">No completed tasks in this period yet.</p>
				<?php else: $rank = 0; foreach ($pfTop as $p): $rank++; $w = $pfTopMax > 0 ? max(4, round(((int) $p['done'] / $pfTopMax) * 100)) : 0; ?>
					<div class="ns-pf-perf">
						<span class="rk"><?php echo (int) $rank; ?></span>
						<img src="<?php echo epc_erp_h((string) $p['avatar']); ?>" alt="" />
						<div class="who"><span class="n"><?php echo epc_erp_h((string) $p['name']); ?></span><span class="d"><?php echo epc_erp_h((string) $p['deptName'] . ($p['location'] !== '' ? ' · ' . $p['location'] : '')); ?></span></div>
						<div class="tr"><div class="fl" style="width:<?php echo (int) $w; ?>%;"></div></div>
						<span class="ct"><?php echo (int) $p['done']; ?></span>
					</div>
				<?php endforeach; endif; ?>
			</div>
		</div>
	</div>
	<?php endif; ?>

	<?php if (!empty($nsControls)): ?>
	<h3 class="ns-exec-h" id="ns-industry-controls" style="margin-top:22px;"><i class="fa fa-check-square-o"></i> Industry intelligence — recommended controls
		<small class="text-muted" style="font-weight:400;">· <?php echo epc_erp_h($nsIndustryLabel); ?> · <?php echo $nsCtrlDone; ?>/<?php echo count($nsControls); ?> in place</small>
	</h3>
	<div class="ns-port">
		<div class="bd">
			<p class="text-muted" style="margin-top:0;">Best-practice operational controls for your industry. Tick the ones you have in place; the checklist is saved per tenant.</p>
			<table class="table table-bordered table-condensed" style="margin-bottom:0;">
				<thead><tr><th style="width:70px;">In place</th><th>Control</th><th>What to do</th></tr></thead>
				<tbody>
				<?php foreach ($nsControls as $c): $checked = !empty($nsCtrlState[$c['code']]); ?>
					<tr class="<?php echo $checked ? 'success' : ''; ?>">
						<td style="text-align:center;">
							<form data-bos-action="bos_intel_toggle_control" style="margin:0;">
								<input type="hidden" name="csrf_guard_key" value="<?php echo epc_erp_h($nsCsrf); ?>">
								<input type="hidden" name="code" value="<?php echo epc_erp_h($c['code']); ?>">
								<input type="hidden" name="checked" value="<?php echo $checked ? '0' : '1'; ?>">
								<button type="submit" class="btn btn-xs <?php echo $checked ? 'btn-success' : 'btn-default'; ?>">
									<i class="fa fa-<?php echo $checked ? 'check-square-o' : 'square-o'; ?>"></i>
								</button>
							</form>
						</td>
						<td><strong><?php echo epc_erp_h($c['title']); ?></strong></td>
						<td><small class="text-muted"><?php echo epc_erp_h($c['desc']); ?></small></td>
					</tr>
				<?php endforeach; ?>
				</tbody>
			</table>
		</div>
	</div>
	<?php endif; ?>
</div>

<script src="https://cdn.jsdelivr.net/npm/chart.js@4.4.1/dist/chart.umd.min.js" crossorigin="anonymous"></script>
<script>
(function () {
	var arLabels = <?php echo json_encode(array_values($nsAr['labels'])); ?>;
	var arTotals = <?php echo json_encode(array_map('floatval', array_values($nsAr['totals']))); ?>;
	var trendLabels = <?php echo json_encode($nsTrendLabels); ?>;
	var trendRev = <?php echo json_encode($nsTrendRev); ?>;
	var trendProf = <?php echo json_encode($nsTrendProf); ?>;
	var currency = <?php echo json_encode($nsCurrency); ?>;

	function nsFmt(n) {
		try { return Number(n).toLocaleString(undefined, { maximumFractionDigits: 0 }); }
		catch (e) { return String(n); }
	}

	function drawCharts() {
		if (typeof Chart === 'undefined') { return; }
		Chart.defaults.font.family = 'Sora, ui-sans-serif, sans-serif';
		Chart.defaults.color = '#5c6b7a';
		var grid = 'rgba(16,42,67,0.06)';

		var arEl = document.getElementById('nsChartAr');
		if (arEl) {
			new Chart(arEl.getContext('2d'), {
				type: 'bar',
				data: {
					labels: arLabels,
					datasets: [{
						data: arTotals,
						backgroundColor: ['#1a7f4b', '#0b6e99', '#0d9488', '#c2811a', '#b42318'],
						borderRadius: 8,
						borderSkipped: false,
						maxBarThickness: 36
					}]
				},
				options: {
					responsive: true,
					maintainAspectRatio: false,
					plugins: {
						legend: { display: false },
						tooltip: {
							callbacks: {
								label: function (ctx) { return currency + ' ' + nsFmt(ctx.parsed.y); }
							}
						}
					},
					scales: {
						x: { grid: { display: false } },
						y: { grid: { color: grid }, ticks: { callback: function (v) { return nsFmt(v); } } }
					},
					animation: { duration: 1100, easing: 'easeOutCubic' }
				}
			});
		}

		var trEl = document.getElementById('nsChartTrend');
		if (trEl) {
			var ctx = trEl.getContext('2d');
			var g = ctx.createLinearGradient(0, 0, 0, 200);
			g.addColorStop(0, 'rgba(11,110,153,0.28)');
			g.addColorStop(1, 'rgba(11,110,153,0.02)');
			new Chart(ctx, {
				type: 'line',
				data: {
					labels: trendLabels,
					datasets: [
						{
							label: 'Revenue',
							data: trendRev,
							borderColor: '#0b6e99',
							backgroundColor: g,
							fill: true,
							tension: 0.35,
							borderWidth: 2.5,
							pointRadius: 3,
							pointBackgroundColor: '#0b6e99'
						},
						{
							label: 'Profit',
							data: trendProf,
							borderColor: '#0d9488',
							backgroundColor: 'transparent',
							fill: false,
							tension: 0.35,
							borderWidth: 2.5,
							pointRadius: 3,
							pointBackgroundColor: '#0d9488'
						}
					]
				},
				options: {
					responsive: true,
					maintainAspectRatio: false,
					plugins: {
						legend: { display: false },
						tooltip: {
							callbacks: {
								label: function (ctx) { return ctx.dataset.label + ': ' + currency + ' ' + nsFmt(ctx.parsed.y); }
							}
						}
					},
					scales: {
						x: { grid: { color: grid } },
						y: { grid: { color: grid }, ticks: { callback: function (v) { return nsFmt(v); } } }
					},
					animation: { duration: 1200, easing: 'easeOutCubic' }
				}
			});
		}
	}

	if (document.readyState === 'loading') {
		document.addEventListener('DOMContentLoaded', drawCharts);
	} else {
		drawCharts();
	}
})();
</script>
