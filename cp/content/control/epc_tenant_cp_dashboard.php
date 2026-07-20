<?php
/**
 * Tenant CP home — Command Centre dashboard (red + black + white).
 * Mirrors the ERP overview dashboard structure for /cp/control.
 */
defined('_ASTEXE_') or die('No access');

if (function_exists('epc_portal_is_super_cp_host') && epc_portal_is_super_cp_host()) {
	return;
}

$portalFile = $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_portal.php';
if (is_file($portalFile)) {
	require_once $portalFile;
}
$brandFile = $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_branding.php';
if (is_file($brandFile)) {
	require_once $brandFile;
}

$backend = trim((string) ($GLOBALS['DP_Config']->backend_dir ?? 'cp'), '/');
$base = '/' . $backend;

function epc_tcp_dash_h($v): string
{
	return htmlspecialchars((string) $v, ENT_QUOTES, 'UTF-8');
}

function epc_tcp_dash_stats(PDO $db): array
{
	$compute = static function () use ($db): array {
		$stats = array(
			'orders_today' => 0,
			'orders_week' => 0,
			'orders_prev_week' => 0,
			'products' => 0,
			'clients' => 0,
			'pending_tasks' => 0,
			'returns_open' => 0,
			'vin_open' => 0,
			'day_labels' => array(),
			'day_counts' => array(),
		);
		try {
			$stats['orders_today'] = (int) $db->query(
				'SELECT COUNT(*) FROM `shop_orders` WHERE `successfully_created` = 1 AND `time` >= UNIX_TIMESTAMP(CURDATE())'
			)->fetchColumn();
		} catch (Exception $e) {
		}
		try {
			$stats['orders_week'] = (int) $db->query(
				'SELECT COUNT(*) FROM `shop_orders` WHERE `successfully_created` = 1 AND `time` >= UNIX_TIMESTAMP(CURDATE() - INTERVAL 6 DAY)'
			)->fetchColumn();
		} catch (Exception $e) {
		}
		try {
			$stats['orders_prev_week'] = (int) $db->query(
				'SELECT COUNT(*) FROM `shop_orders` WHERE `successfully_created` = 1
				 AND `time` >= UNIX_TIMESTAMP(CURDATE() - INTERVAL 13 DAY)
				 AND `time` < UNIX_TIMESTAMP(CURDATE() - INTERVAL 6 DAY)'
			)->fetchColumn();
		} catch (Exception $e) {
		}
		try {
			$stats['products'] = (int) $db->query(
				'SELECT COUNT(*) FROM `shop_catalogue_products` WHERE `published_flag` = 1'
			)->fetchColumn();
		} catch (Exception $e) {
		}
		try {
			$stats['clients'] = (int) $db->query(
				'SELECT COUNT(*) FROM `users` u
				 WHERE u.`user_id` > 0
				 AND NOT EXISTS (
					SELECT 1 FROM `users_groups_bind` b
					INNER JOIN `groups` g ON g.`id` = b.`group_id`
					WHERE b.`user_id` = u.`user_id` AND g.`for_backend` = 1
				 )'
			)->fetchColumn();
		} catch (Exception $e) {
			try {
				$stats['clients'] = (int) $db->query(
					'SELECT COUNT(*) FROM `users` WHERE `user_id` > 0'
				)->fetchColumn();
			} catch (Exception $e2) {
			}
		}
		try {
			$openStatuses = array();
			$q = $db->query("SELECT `id` FROM `shop_orders_statuses_ref` WHERE `for_inverse` != 1 AND `for_finish` != 1 AND `for_created` != 1");
			while ($r = $q->fetch(PDO::FETCH_ASSOC)) {
				$openStatuses[] = (int) $r['id'];
			}
			if (count($openStatuses) > 0) {
				$sp = implode(',', array_fill(0, count($openStatuses), '?'));
				$st = $db->prepare("SELECT COUNT(*) FROM `shop_orders` WHERE `successfully_created` = 1 AND `status` IN ($sp)");
				$st->execute($openStatuses);
				$stats['pending_tasks'] = (int) $st->fetchColumn();
			}
		} catch (Exception $e) {
		}
		try {
			if ($db->query("SHOW TABLES LIKE 'shop_orders_returns'")->fetchColumn()) {
				$stats['returns_open'] = (int) $db->query(
					"SELECT COUNT(*) FROM `shop_orders_returns` WHERE COALESCE(`status`,0) NOT IN (2,3,9)"
				)->fetchColumn();
			}
		} catch (Exception $e) {
		}
		try {
			if ($db->query("SHOW TABLES LIKE 'shop_docpart_vin'")->fetchColumn()) {
				$stats['vin_open'] = (int) $db->query(
					"SELECT COUNT(*) FROM `shop_docpart_vin` WHERE COALESCE(`viewed`,0) = 0"
				)->fetchColumn();
			} elseif ($db->query("SHOW TABLES LIKE 'shop_docpart_requests'")->fetchColumn()) {
				$stats['vin_open'] = (int) $db->query(
					"SELECT COUNT(*) FROM `shop_docpart_requests` WHERE COALESCE(`viewed`,0) = 0"
				)->fetchColumn();
			}
		} catch (Exception $e) {
		}

		// Last 7 days order counts for chart.
		$labels = array();
		$counts = array();
		for ($i = 6; $i >= 0; $i--) {
			$day = date('Y-m-d', strtotime('-' . $i . ' days'));
			$labels[] = date('D j', strtotime($day));
			$counts[$day] = 0;
		}
		try {
			$q = $db->query(
				"SELECT FROM_UNIXTIME(`time`, '%Y-%m-%d') AS d, COUNT(*) AS c
				 FROM `shop_orders`
				 WHERE `successfully_created` = 1 AND `time` >= UNIX_TIMESTAMP(CURDATE() - INTERVAL 6 DAY)
				 GROUP BY d"
			);
			while ($r = $q->fetch(PDO::FETCH_ASSOC)) {
				$d = (string) ($r['d'] ?? '');
				if (isset($counts[$d])) {
					$counts[$d] = (int) $r['c'];
				}
			}
		} catch (Exception $e) {
		}
		$stats['day_labels'] = $labels;
		$stats['day_counts'] = array_values($counts);
		return $stats;
	};

	$perfCache = $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_perf_cache.php';
	if (is_file($perfCache)) {
		require_once $perfCache;
		if (function_exists('epc_perf_cache_remember')) {
			$dbName = 'default';
			try {
				$dbName = (string) $db->query('SELECT DATABASE()')->fetchColumn();
			} catch (Throwable $e) {
			}
			return epc_perf_cache_remember('epc_tcp_dash_stats:v4:' . $dbName, 180, $compute);
		}
	}
	return $compute();
}

function epc_tcp_dash_change(float $cur, float $prev, bool $goodWhenUp = true): string
{
	if (abs($prev) < 0.005) {
		return '<span class="cp-dash-chg flat">—</span>';
	}
	$pct = (($cur - $prev) / abs($prev)) * 100.0;
	$up = $pct >= 0;
	$good = $up === $goodWhenUp;
	$arrow = $up ? '&#9650;' : '&#9660;';
	$cls = $good ? 'up' : 'down';
	return '<span class="cp-dash-chg ' . $cls . '">' . $arrow . ' ' . number_format(abs($pct), 1) . '%</span>';
}

$stats = array(
	'orders_today' => 0,
	'orders_week' => 0,
	'orders_prev_week' => 0,
	'products' => 0,
	'clients' => 0,
	'pending_tasks' => 0,
	'returns_open' => 0,
	'vin_open' => 0,
	'day_labels' => array('Mon', 'Tue', 'Wed', 'Thu', 'Fri', 'Sat', 'Sun'),
	'day_counts' => array(0, 0, 0, 0, 0, 0, 0),
);
global $db_link;
if (isset($db_link) && $db_link instanceof PDO) {
	$stats = epc_tcp_dash_stats($db_link);
}

// Optional finance pulse from ERP helpers (light path).
$finance = array(
	'revenue_ex_vat' => 0.0,
	'cash_bank_total' => 0.0,
	'customer_ledger_balance' => 0.0,
	'payable_balance' => 0.0,
	'order_count' => 0,
	'has_finance' => false,
);
$helpers = $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_helpers.php';
if (is_file($helpers) && isset($db_link) && $db_link instanceof PDO) {
	require_once $helpers;
	if (function_exists('epc_erp_dashboard')) {
		try {
			$from = strtotime(date('Y-m-01 00:00:00'));
			$to = time();
			$dash = epc_erp_dashboard($db_link, $from, $to, true);
			if (is_array($dash)) {
				$finance['revenue_ex_vat'] = (float) ($dash['revenue_ex_vat'] ?? 0);
				$finance['cash_bank_total'] = (float) ($dash['cash_bank_total'] ?? 0);
				$finance['customer_ledger_balance'] = (float) ($dash['customer_ledger_balance'] ?? 0);
				$finance['payable_balance'] = (float) ($dash['payable_balance'] ?? 0);
				$finance['order_count'] = (int) ($dash['order_count'] ?? 0);
				$finance['has_finance'] = true;
			}
		} catch (Throwable $e) {
		}
	}
}

$brand = function_exists('epc_brand_cp_context') ? epc_brand_cp_context() : array();
$tenantName = trim((string) ($brand['company_name'] ?? $brand['product_name'] ?? 'Control Panel'));
$industryCode = function_exists('epc_portal_cp_active_industry') ? epc_portal_cp_active_industry() : 'auto_parts';
$industry = function_exists('epc_portal_industry') ? epc_portal_industry($industryCode) : array('name' => 'Commerce', 'icon' => 'fa-cog');
$industryLabel = trim((string) ($industry['name'] ?? 'Commerce'));
$industryIcon = trim((string) ($industry['icon'] ?? 'fa-cog'));
$storefrontUrl = trim((string) ($GLOBALS['DP_Config']->domain_path ?? ''));
$settingsUrl = $base . '/control/portal/industry_settings';
$ordersUrl = $base . '/shop/orders/orders';
$catalogueUrl = $base . '/shop/catalogue/products';
$clientsUrl = $base . '/shop/customer_mgmt/customer_mgmt';
$erpUrl = $base . '/shop/finance/erp?epc_erp_shell=1';
$currency = 'AED';
if (function_exists('epc_co_profile_get') && isset($db_link) && $db_link instanceof PDO) {
	try {
		$co = epc_co_profile_get($db_link);
		if (is_array($co) && !empty($co['base_currency'])) {
			$currency = (string) $co['base_currency'];
		}
	} catch (Throwable $e) {
	}
}

$tiles = array(
	array('label' => 'Orders (OMS)', 'icon' => 'fa-shopping-cart', 'url' => $ordersUrl, 'tone' => 'red'),
	array('label' => 'Catalogue', 'icon' => 'fa-th-large', 'url' => $catalogueUrl, 'tone' => 'black'),
	array('label' => 'Prices', 'icon' => 'fa-tags', 'url' => $base . '/shop/prices', 'tone' => 'crimson'),
	array('label' => 'Clients', 'icon' => 'fa-address-book', 'url' => $clientsUrl, 'tone' => 'stone'),
	array('label' => 'Warehouses', 'icon' => 'fa-building', 'url' => $base . '/shop/logistics/storages', 'tone' => 'black'),
	array('label' => 'ERP finance', 'icon' => 'fa-university', 'url' => $erpUrl, 'tone' => 'red'),
	array('label' => 'Documents', 'icon' => 'fa-file-text-o', 'url' => $base . '/shop/document_control/document_control', 'tone' => 'stone'),
	array('label' => 'Settings', 'icon' => 'fa-cog', 'url' => $settingsUrl, 'tone' => 'crimson'),
);

// Per-user customizable shortcuts (add/remove on the dashboard).
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_helpers.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_access.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_shortcut_icons.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_dash_shortcuts_ui.php';
$cpShortcutUrls = epc_erp_configure_portal_urls('cp');
$cpShortcutAjax = (string) ($cpShortcutUrls['erpAjaxUrl'] ?? ('/' . $backend . '/content/shop/finance/erp/ajax_erp.php'));
$cpShortcutCsrf = '';
if (class_exists('DP_User')) {
	$cpSess = DP_User::getAdminSession();
	if (is_array($cpSess) && !empty($cpSess['csrf_guard_key'])) {
		$cpShortcutCsrf = (string) $cpSess['csrf_guard_key'];
	}
}
$cpShortcutCatalog = epc_shortcuts_catalog_cp($base);
$cpShortcutDefaults = ($industryCode === 'auto_parts')
	? array('orders', 'prices', 'multivendor', 'crosses', 'procurement', 'pos', 'erp', 'stock')
	: array('orders', 'catalogue', 'prices', 'clients', 'accessories', 'erp', 'documents', 'settings');
$cpShortcutUid = epc_shortcuts_user_id();
if (isset($db_link) && $db_link instanceof PDO && $cpShortcutUid > 0) {
	epc_shortcuts_seed_defaults($db_link, $cpShortcutUid, 'cp', $cpShortcutDefaults, $cpShortcutCatalog);
	$cpShortcutItems = epc_shortcuts_as_tiles(epc_shortcuts_list_for_surface($db_link, $cpShortcutUid, 'cp'));
} else {
	$cpShortcutItems = array();
	foreach ($cpShortcutDefaults as $dk) {
		if (!isset($cpShortcutCatalog[$dk])) {
			continue;
		}
		$c = $cpShortcutCatalog[$dk];
		$cpShortcutItems[] = array(
			'id' => 0,
			'key' => $dk,
			'label' => $c['label'],
			'icon' => preg_replace('/^fa\s+/', '', $c['icon']),
			'color' => $c['color'],
			'url' => $c['url'],
			'tone' => $c['tone'] ?? 'blue',
		);
	}
}

$moreLinks = array(
	array('label' => 'CP brochure', 'icon' => 'fa-book', 'url' => $base . '/control/cp_brochure', 'tone' => 'black'),
	array('label' => 'Auto Price AI', 'icon' => 'fa-line-chart', 'url' => $base . '/control/portal/epc_auto_price_engine', 'tone' => 'red'),
	array('label' => 'Accessories', 'icon' => 'fa-puzzle-piece', 'url' => $base . '/shop/accessories', 'tone' => 'stone'),
	array('label' => 'Visual editor', 'icon' => 'fa-magic', 'url' => $base . '/control/portal/epc_visual_page_editor', 'tone' => 'black'),
	array('label' => 'Tax Toolkit', 'icon' => 'fa-balance-scale', 'url' => $base . '/control/portal/epc_tax_toolkit_manage', 'tone' => 'red'),
	array('label' => 'Social media', 'icon' => 'fa-share-alt', 'url' => $base . '/control/portal/epc_social_media_hub', 'tone' => 'stone'),
);

$reminders = array(
	array('n' => (int) $stats['pending_tasks'], 'label' => 'Open orders needing fulfilment', 'url' => $ordersUrl),
	array('n' => (int) $stats['orders_today'], 'label' => 'Orders created today', 'url' => $ordersUrl),
	array('n' => (int) $stats['vin_open'], 'label' => 'Unread VIN / parts requests', 'url' => $base . '/requests'),
	array('n' => (int) $stats['returns_open'], 'label' => 'Open returns', 'url' => $base . '/shop/returns-manager'),
);

$kpiRows = array(
	array('name' => 'Orders (7 days)', 'cur' => (float) $stats['orders_week'], 'prev' => (float) $stats['orders_prev_week'], 'goodUp' => true, 'money' => false),
	array('name' => 'Orders today', 'cur' => (float) $stats['orders_today'], 'prev' => 0.0, 'goodUp' => true, 'money' => false),
	array('name' => 'Open orders', 'cur' => (float) $stats['pending_tasks'], 'prev' => 0.0, 'goodUp' => false, 'money' => false),
	array('name' => 'Published products', 'cur' => (float) $stats['products'], 'prev' => 0.0, 'goodUp' => true, 'money' => false),
	array('name' => 'Storefront clients', 'cur' => (float) $stats['clients'], 'prev' => 0.0, 'goodUp' => true, 'money' => false),
);
if (!empty($finance['has_finance'])) {
	$kpiRows[] = array('name' => 'Sales ex VAT (MTD)', 'cur' => $finance['revenue_ex_vat'], 'prev' => 0.0, 'goodUp' => true, 'money' => true);
	$kpiRows[] = array('name' => 'Cash & bank', 'cur' => $finance['cash_bank_total'], 'prev' => 0.0, 'goodUp' => true, 'money' => true);
}

$cssHref = '/content/general_pages/epc_cp_command_dashboard_css.php?v=20260720cpqa1';
if (function_exists('epc_cp_shell_asset_href')) {
	$cssHref = epc_cp_shell_asset_href(
		'/' . $backend . '/templates/bootstrap_admin/css/epc_cp_command_dashboard.css',
		'/content/general_pages/epc_cp_command_dashboard_css.php'
	);
}
if (strpos($cssHref, '?') === false) {
	$cssHref .= '?v=20260720cpqa1';
} elseif (strpos($cssHref, 'v=') === false) {
	$cssHref .= '&v=20260720cpqa1';
} elseif (strpos($cssHref, '20260720qafmt1') !== false) {
	$cssHref = str_replace('20260720qafmt1', '20260720cpqa1', $cssHref);
}

$GLOBALS['epc_tenant_cp_dashboard_shown'] = true;
$dayLabelsJson = json_encode(array_values((array) $stats['day_labels']));
$dayCountsJson = json_encode(array_map('intval', array_values((array) $stats['day_counts'])));
?>
<link rel="stylesheet" href="<?php echo epc_tcp_dash_h($cssHref); ?>">

<div class="col-lg-12 cp-dash" data-cp-dashboard="command">
	<div class="cp-dash-hero">
		<div class="cp-dash-hero-panel">
			<div class="cp-dash-kicker"><i class="fa <?php echo epc_tcp_dash_h($industryIcon); ?>"></i> Control Command Centre</div>
			<h2 class="cp-dash-title"><?php echo epc_tcp_dash_h($tenantName); ?></h2>
			<p class="cp-dash-sub"><?php echo $industryCode === 'auto_parts'
				? 'Live commerce pulse — orders, catalogue, prices, warehouses, and finance in one red-black command view.'
				: 'Your daily control dashboard — orders, catalogue, clients, and finance shortcuts in one place.'; ?></p>
			<div class="cp-dash-meta">
				<span class="cp-dash-chip cp-dash-chip--dark"><i class="fa fa-industry"></i> <?php echo epc_tcp_dash_h($industryLabel); ?></span>
				<span class="cp-dash-chip"><i class="fa fa-calendar"></i> <?php echo epc_tcp_dash_h(date('Y-m-d')); ?></span>
				<span class="cp-dash-chip"><i class="fa fa-money"></i> <?php echo epc_tcp_dash_h($currency); ?></span>
				<a class="cp-dash-chip" href="<?php echo epc_tcp_dash_h($erpUrl . '&area=overview&tab=dashboard'); ?>"><i class="fa fa-university"></i> Open ERP</a>
				<?php if ($storefrontUrl !== '') { ?>
				<a class="cp-dash-chip" href="<?php echo epc_tcp_dash_h($storefrontUrl); ?>" target="_blank" rel="noopener"><i class="fa fa-external-link"></i> View site</a>
				<?php } ?>
			</div>
		</div>
		<div class="cp-dash-metrics">
			<a class="cp-dash-metric" href="<?php echo epc_tcp_dash_h($ordersUrl); ?>">
				<div class="cp-dash-metric__label">Orders today</div>
				<div class="cp-dash-metric__val"><?php echo (int) $stats['orders_today']; ?></div>
				<div class="cp-dash-metric__hint">New successfully created orders</div>
			</a>
			<a class="cp-dash-metric" href="<?php echo epc_tcp_dash_h($ordersUrl); ?>">
				<div class="cp-dash-metric__label">Open orders</div>
				<div class="cp-dash-metric__val cp-dash-metric__val--warn"><?php echo (int) $stats['pending_tasks']; ?></div>
				<div class="cp-dash-metric__hint">Need fulfilment</div>
			</a>
			<a class="cp-dash-metric" href="<?php echo epc_tcp_dash_h($catalogueUrl); ?>">
				<div class="cp-dash-metric__label">Products</div>
				<div class="cp-dash-metric__val"><?php echo (int) $stats['products']; ?></div>
				<div class="cp-dash-metric__hint">Published catalogue</div>
			</a>
			<a class="cp-dash-metric" href="<?php echo epc_tcp_dash_h($clientsUrl); ?>">
				<div class="cp-dash-metric__label">Clients</div>
				<div class="cp-dash-metric__val"><?php echo (int) $stats['clients']; ?></div>
				<div class="cp-dash-metric__hint">Storefront customers</div>
			</a>
		</div>
	</div>

	<?php
	// Customizable shortcut tiles replace the old hard-coded tile strip.
	// Keep catalogue tones aligned with CP red/black command centre.
	$cpToneMap = array(
		'orders' => 'red', 'catalogue' => 'black', 'prices' => 'crimson', 'clients' => 'stone',
		'warehouses' => 'black', 'stock' => 'emerald', 'procurement' => 'indigo', 'erp' => 'red',
		'documents' => 'stone', 'pos' => 'rose', 'multivendor' => 'teal', 'crosses' => 'blue',
		'ai_chats' => 'violet', 'settings' => 'crimson', 'brochure' => 'indigo', 'accessories' => 'violet',
	);
	foreach ($cpShortcutItems as $ci => $cit) {
		$ck = (string) ($cit['key'] ?? '');
		if ($ck !== '' && isset($cpToneMap[$ck])) {
			$cpShortcutItems[$ci]['tone'] = $cpToneMap[$ck];
		} elseif (empty($cit['tone']) || !in_array((string) $cit['tone'], array('red', 'black', 'stone', 'crimson'), true)) {
			$cycle = array('red', 'black', 'crimson', 'stone');
			$cpShortcutItems[$ci]['tone'] = $cycle[$ci % 4];
		}
	}
	?>
	<div class="cp-dash-port">
		<div class="bd" style="padding-top:14px">
			<?php
			echo epc_dash_shortcuts_render(array(
				'surface' => 'cp',
				'variant' => 'cp',
				'title' => 'Quick actions',
				'ajax_url' => $cpShortcutAjax,
				'csrf' => $cpShortcutCsrf,
				'catalog' => $cpShortcutCatalog,
				'items' => $cpShortcutItems,
			));
			?>
		</div>
	</div>

	<div class="cp-dash-grid">
		<div class="cp-dash-col-left">
			<div class="cp-dash-port">
				<h4><i class="fa fa-bell-o"></i> Reminders</h4>
				<div class="bd">
					<ul class="cp-dash-rem">
						<?php foreach ($reminders as $r) { ?>
						<li>
							<span class="cnt<?php echo ((int) $r['n'] === 0) ? ' zero' : ''; ?>"><?php echo (int) $r['n']; ?></span>
							<a href="<?php echo epc_tcp_dash_h($r['url']); ?>"><?php echo epc_tcp_dash_h($r['label']); ?></a>
						</li>
						<?php } ?>
					</ul>
				</div>
			</div>
			<div class="cp-dash-port">
				<h4><i class="fa fa-pencil"></i> Customize shortcuts</h4>
				<div class="bd" style="font-size:13px;color:#475569;line-height:1.5">
					Use <strong>Edit shortcuts</strong> in Quick actions above — same graphical builder as ERP.
					Toggle catalogue modules, add a custom URL, or reset to defaults. Changes save immediately.
				</div>
			</div>
		</div>

		<div class="cp-dash-col-mid">
			<div class="cp-dash-port">
				<h4><i class="fa fa-tachometer"></i> Key performance indicators</h4>
				<div class="bd" style="padding:0">
					<table class="cp-dash-kpi-tbl">
						<thead><tr><th>Indicator</th><th>Current</th><th>Previous</th><th>Change</th></tr></thead>
						<tbody>
							<?php foreach ($kpiRows as $k) { ?>
							<tr>
								<td><?php echo epc_tcp_dash_h($k['name']); ?></td>
								<td><?php echo !empty($k['money']) ? number_format((float) $k['cur'], 2) : number_format((float) $k['cur'], 0); ?></td>
								<td><?php echo !empty($k['money']) ? number_format((float) $k['prev'], 2) : ((float) $k['prev'] > 0 ? number_format((float) $k['prev'], 0) : '—'); ?></td>
								<td><?php echo epc_tcp_dash_change((float) $k['cur'], (float) $k['prev'], (bool) $k['goodUp']); ?></td>
							</tr>
							<?php } ?>
						</tbody>
					</table>
				</div>
			</div>
			<?php if (!empty($finance['has_finance'])) { ?>
			<div class="cp-dash-port">
				<h4><i class="fa fa-money"></i> Finance pulse (MTD)</h4>
				<div class="bd">
					<div class="cp-dash-fin">
						<div class="cell"><div class="l">Sales ex VAT</div><div class="v"><?php echo number_format($finance['revenue_ex_vat'], 2) . ' ' . epc_tcp_dash_h($currency); ?></div></div>
						<div class="cell"><div class="l">Cash &amp; bank</div><div class="v"><?php echo number_format($finance['cash_bank_total'], 2) . ' ' . epc_tcp_dash_h($currency); ?></div></div>
						<div class="cell"><div class="l">Receivables</div><div class="v"><?php echo number_format($finance['customer_ledger_balance'], 2) . ' ' . epc_tcp_dash_h($currency); ?></div></div>
						<div class="cell"><div class="l">Payables</div><div class="v"><?php echo number_format($finance['payable_balance'], 2) . ' ' . epc_tcp_dash_h($currency); ?></div></div>
					</div>
				</div>
			</div>
			<?php } ?>
		</div>

		<div class="cp-dash-col-right">
			<div class="cp-dash-port">
				<h4><i class="fa fa-area-chart"></i> Orders — last 7 days</h4>
				<div class="bd">
					<div class="cp-dash-chart-wrap">
						<canvas id="cpDashOrdersChart" aria-label="Orders last 7 days"></canvas>
					</div>
				</div>
			</div>
			<div class="cp-dash-port">
				<h4><i class="fa fa-rocket"></i> Today’s focus</h4>
				<div class="bd">
					<div class="cp-dash-fin">
						<div class="cell"><div class="l">Week orders</div><div class="v"><?php echo (int) $stats['orders_week']; ?></div></div>
						<div class="cell"><div class="l">Open work</div><div class="v" style="color:#dc2626"><?php echo (int) $stats['pending_tasks']; ?></div></div>
						<div class="cell"><div class="l">VIN / requests</div><div class="v"><?php echo (int) $stats['vin_open']; ?></div></div>
						<div class="cell"><div class="l">Returns</div><div class="v"><?php echo (int) $stats['returns_open']; ?></div></div>
					</div>
				</div>
			</div>
		</div>
	</div>

	<details class="cp-dash-more">
		<summary>
			<span><i class="fa fa-ellipsis-h"></i> More tools</span>
			<span class="hint">POS, documents, pricing AI, accessories…</span>
		</summary>
		<div class="bd">
			<div class="cp-dash-qa-grid">
				<?php foreach ($moreLinks as $link) { ?>
				<a class="cp-dash-qa" href="<?php echo epc_tcp_dash_h($link['url']); ?>">
					<span class="qa-ic qa-ic--<?php echo epc_tcp_dash_h($link['tone']); ?>"><i class="fa <?php echo epc_tcp_dash_h($link['icon']); ?>"></i></span>
					<span class="qa-lb"><?php echo epc_tcp_dash_h($link['label']); ?></span>
				</a>
				<?php } ?>
			</div>
		</div>
	</details>

	<p class="cp-dash-help">
		Tip: use the top menu to jump across modules. Open the full
		<a href="<?php echo epc_tcp_dash_h($erpUrl . '&area=overview&tab=dashboard'); ?>">ERP Command Centre</a>
		for finance depth, or share the
		<a href="<?php echo epc_tcp_dash_h($base . '/control/cp_brochure'); ?>" target="_blank" rel="noopener">CP brochure</a>.
	</p>
</div>

<script src="https://cdn.jsdelivr.net/npm/chart.js@4.4.1/dist/chart.umd.min.js" defer></script>
<script>
(function () {
	function boot() {
		var canvas = document.getElementById('cpDashOrdersChart');
		if (!canvas || typeof Chart === 'undefined') { return; }
		var labels = <?php echo $dayLabelsJson ?: '[]'; ?>;
		var data = <?php echo $dayCountsJson ?: '[]'; ?>;
		new Chart(canvas, {
			type: 'bar',
			data: {
				labels: labels,
				datasets: [{
					label: 'Orders',
					data: data,
					backgroundColor: 'rgba(220, 38, 38, 0.75)',
					borderColor: '#0a0a0a',
					borderWidth: 1,
					borderRadius: 4,
					maxBarThickness: 28
				}]
			},
			options: {
				responsive: true,
				maintainAspectRatio: false,
				plugins: {
					legend: { display: false },
					tooltip: {
						backgroundColor: '#0a0a0a',
						titleColor: '#fff',
						bodyColor: '#fecaca'
					}
				},
				scales: {
					x: {
						grid: { display: false },
						ticks: { color: '#57534e', font: { size: 11, weight: '600' } }
					},
					y: {
						beginAtZero: true,
						ticks: { precision: 0, color: '#57534e' },
						grid: { color: 'rgba(0,0,0,0.06)' }
					}
				}
			}
		});
	}
	if (document.readyState === 'loading') {
		document.addEventListener('DOMContentLoaded', function () {
			setTimeout(boot, 60);
		});
	} else {
		setTimeout(boot, 60);
	}
	window.addEventListener('load', boot);
})();
</script>
