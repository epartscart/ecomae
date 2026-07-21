<?php
/**
 * Unified Insights Suite — Financial, Business, and CP-level insights.
 *
 * World-class pattern: KPI + health + period delta + narrative + drill-down.
 * Used by CP Command Centre and ERP home so both surfaces share one engine.
 */
defined('_ASTEXE_') or die('No access');

/**
 * Resolve project root for requires (works in web + CLI tests).
 */
function epc_insights_doc_root(): string
{
	$root = rtrim((string) ($_SERVER['DOCUMENT_ROOT'] ?? ''), '/');
	if ($root !== '' && is_file($root . '/content/shop/finance/epc_erp_helpers.php')) {
		return $root;
	}
	return dirname(__DIR__, 3);
}

require_once epc_insights_doc_root() . '/content/shop/finance/epc_erp_helpers.php';

/**
 * @return array<string,mixed>
 */
function epc_insights_suite_build(PDO $db, array $opts = array()): array
{
	static $memo = array();
	$light = !empty($opts['light']);
	$backend = trim((string) ($opts['backend'] ?? 'cp'), '/');
	if ($backend === '') {
		$backend = 'cp';
	}
	$base = '/' . $backend;
	$currency = trim((string) ($opts['currency'] ?? 'AED'));
	if ($currency === '') {
		$currency = 'AED';
	}

	$to = time();
	$from = strtotime(date('Y-m-01 00:00:00'));
	$prevTo = $from - 1;
	$prevFrom = strtotime(date('Y-m-01 00:00:00', $prevTo));
	$driver = 'mysql';
	try {
		$driver = (string) $db->getAttribute(PDO::ATTR_DRIVER_NAME);
	} catch (Throwable $e) {
	}
	$memoKey = $driver . ':' . $from . ':' . $to . ':' . ($light ? '1' : '0');
	if (isset($memo[$memoKey])) {
		$cached = $memo[$memoKey];
		$cached['urls'] = epc_insights_suite_urls($base);
		$cached['currency'] = $currency;
		return $cached;
	}

	$urls = epc_insights_suite_urls($base);

	$dash = array();
	$prevDash = array();
	try {
		$dash = epc_erp_dashboard($db, $from, $to, $light);
	} catch (Throwable $e) {
		$dash = array();
	}
	try {
		$prevDash = epc_erp_dashboard($db, $prevFrom, $prevTo, true);
	} catch (Throwable $e) {
		$prevDash = array();
	}

	$intel = array();
	$intelFile = epc_insights_doc_root() . '/content/shop/finance/epc_bos_intelligence.php';
	if (is_file($intelFile)) {
		require_once $intelFile;
		if (function_exists('epc_bos_intel_kpis')) {
			try {
				$intel = epc_bos_intel_kpis($db, $from, $to, is_array($dash) ? $dash : null, null);
			} catch (Throwable $e) {
				$intel = array();
			}
		}
	}

	$intelMap = array();
	foreach ($intel as $row) {
		if (!empty($row['key'])) {
			$intelMap[(string) $row['key']] = $row;
		}
	}

	$commerce = epc_insights_suite_commerce_stats($db);
	$agingOverduePct = epc_insights_suite_ar_overdue_pct($db);

	$revenue = (float) ($dash['revenue_ex_vat'] ?? 0);
	$prevRevenue = (float) ($prevDash['revenue_ex_vat'] ?? 0);
	$cash = (float) ($dash['cash_bank_total'] ?? ($intelMap['cash']['value'] ?? 0));
	$ar = (float) ($dash['customer_ledger_balance'] ?? ($intelMap['ar']['value'] ?? 0));
	$ap = (float) ($dash['payable_balance'] ?? ($intelMap['ap']['value'] ?? 0));
	$margin = (float) ($intelMap['gross_margin']['value'] ?? 0);
	$dso = (float) ($intelMap['dso']['value'] ?? 0);
	$dpo = (float) ($intelMap['dpo']['value'] ?? 0);
	$currentRatio = (float) ($intelMap['current_ratio']['value'] ?? 0);
	$invValue = (float) ($intelMap['inventory']['value'] ?? 0);
	$wc = $cash + $ar + $invValue - $ap;

	$financial = array(
		epc_insights_card(
			'revenue',
			'Revenue (MTD)',
			$revenue,
			'money',
			$prevRevenue,
			true,
			$revenue > 0 ? 'good' : 'warn',
			$revenue > 0
				? 'Period sales excl. VAT — compare to last month for momentum.'
				: 'No MTD sales yet — check price lists and channel sync.',
			$urls['pl'],
			'Open P&L'
		),
		epc_insights_card(
			'margin',
			'Gross margin',
			$margin,
			'pct',
			null,
			true,
			(string) ($intelMap['gross_margin']['health'] ?? ($margin >= 25 ? 'good' : ($margin >= 12 ? 'warn' : 'bad'))),
			$margin >= 25
				? 'Healthy margin for the period.'
				: ($margin >= 12 ? 'Margin is soft — review cost and discounting.' : 'Margin pressure — investigate COGS and pricing.'),
			$urls['pl'],
			'Review income statement'
		),
		epc_insights_card(
			'cash',
			'Cash & bank',
			$cash,
			'money',
			(float) ($prevDash['cash_bank_total'] ?? 0),
			true,
			$cash >= 0 ? 'good' : 'bad',
			$ap > 0
				? ('Liquidity covers ~' . number_format(max(0, $cash / max(1, $ap / 30)), 0) . ' days of payables at current AP run-rate.')
				: 'Cash position across bank accounts.',
			$urls['cash'],
			'Cash & bank'
		),
		epc_insights_card(
			'ar_dso',
			'Receivables / DSO',
			$ar,
			'money',
			null,
			false,
			(string) ($intelMap['dso']['health'] ?? 'info'),
			'DSO ' . number_format($dso, 0) . ' days'
				. ($agingOverduePct !== null ? ' · ' . number_format($agingOverduePct, 0) . '% of AR past due' : '')
				. '. Faster collection lifts cash.',
			$urls['aging'],
			'AR aging'
		),
		epc_insights_card(
			'ap_dpo',
			'Payables / DPO',
			$ap,
			'money',
			null,
			false,
			(string) ($intelMap['dpo']['health'] ?? 'info'),
			'DPO ' . number_format($dpo, 0) . ' days — balance supplier terms with cash preservation.',
			$urls['aging'],
			'AP aging'
		),
		epc_insights_card(
			'working_capital',
			'Working capital (approx)',
			$wc,
			'money',
			null,
			true,
			$currentRatio >= 1.5 ? 'good' : ($currentRatio >= 1.0 ? 'warn' : 'bad'),
			'Current ratio ~' . number_format($currentRatio, 2) . 'x (cash + AR + inventory ÷ AP).',
			$urls['erp_home'],
			'ERP home'
		),
	);

	$ordersWeek = (int) ($commerce['orders_week'] ?? 0);
	$ordersPrev = (int) ($commerce['orders_prev_week'] ?? 0);
	$ordersToday = (int) ($commerce['orders_today'] ?? 0);
	$openOrders = (int) ($commerce['open_orders'] ?? 0);
	$returnsOpen = (int) ($commerce['returns_open'] ?? 0);
	$aov = ($ordersWeek > 0 && $revenue > 0) ? ($revenue / max(1, (int) ($dash['order_count'] ?? $ordersWeek))) : 0.0;

	$business = array(
		epc_insights_card(
			'orders_week',
			'Orders (7 days)',
			(float) $ordersWeek,
			'number',
			(float) $ordersPrev,
			true,
			$ordersWeek >= $ordersPrev ? 'good' : 'warn',
			$ordersPrev > 0
				? 'Volume ' . ($ordersWeek >= $ordersPrev ? 'up' : 'down') . ' vs prior week.'
				: 'Track weekly order velocity as your demand pulse.',
			$urls['orders'],
			'Open OMS'
		),
		epc_insights_card(
			'orders_today',
			'Orders today',
			(float) $ordersToday,
			'number',
			null,
			true,
			$ordersToday > 0 ? 'good' : 'info',
			$ordersToday > 0 ? 'New demand landed today — keep fulfilment moving.' : 'No orders yet today.',
			$urls['orders'],
			'Orders'
		),
		epc_insights_card(
			'open_orders',
			'Open fulfilment',
			(float) $openOrders,
			'number',
			null,
			false,
			$openOrders > 20 ? 'warn' : ($openOrders > 0 ? 'info' : 'good'),
			$openOrders > 0
				? 'Backlog needs attention — prioritise pick/pack/ship.'
				: 'No open fulfilment backlog.',
			$urls['orders'],
			'Fulfilment queue'
		),
		epc_insights_card(
			'aov',
			'Avg order value (MTD)',
			$aov,
			'money',
			null,
			true,
			$aov > 0 ? 'info' : 'warn',
			$aov > 0 ? 'Basket size indicator from period sales.' : 'AOV appears once orders post revenue.',
			$urls['orders'],
			'Orders'
		),
		epc_insights_card(
			'returns',
			'Open returns',
			(float) $returnsOpen,
			'number',
			null,
			false,
			$returnsOpen > 5 ? 'warn' : 'info',
			$returnsOpen > 0 ? 'Resolve returns to protect margin and stock accuracy.' : 'Returns queue is clear.',
			$urls['returns'],
			'Returns'
		),
	);

	$products = (int) ($commerce['products'] ?? 0);
	$clients = (int) ($commerce['clients'] ?? 0);
	$warehouses = (int) ($commerce['warehouses'] ?? 0);
	$priceLists = (int) ($commerce['price_lists'] ?? 0);
	$vinOpen = (int) ($commerce['vin_open'] ?? 0);

	$cp = array(
		epc_insights_card(
			'catalogue',
			'Published products',
			(float) $products,
			'number',
			null,
			true,
			$products > 0 ? 'good' : 'warn',
			$products > 0 ? 'Catalogue depth available on the storefront.' : 'Publish products or sync price-list SKUs.',
			$urls['catalogue'],
			'Catalogue'
		),
		epc_insights_card(
			'clients',
			'Customers',
			(float) $clients,
			'number',
			null,
			true,
			$clients > 0 ? 'good' : 'info',
			'Active storefront / CRM customer base.',
			$urls['clients'],
			'Customers'
		),
		epc_insights_card(
			'warehouses',
			'Warehouses',
			(float) $warehouses,
			'number',
			null,
			true,
			$warehouses > 0 ? 'good' : 'warn',
			$warehouses > 0 ? 'Logistics nodes ready for stock & delivery.' : 'Create warehouses for inventory & multivendor.',
			$urls['warehouses'],
			'Warehouses'
		),
		epc_insights_card(
			'price_lists',
			'Price lists',
			(float) $priceLists,
			'number',
			null,
			true,
			$priceLists > 0 ? 'good' : 'warn',
			$priceLists > 0 ? 'Pricing channels configured.' : 'Upload commerce or multivendor prices.',
			$urls['prices'],
			'Price lists'
		),
	);
	if ($vinOpen > 0 || (isset($opts['industry']) && $opts['industry'] === 'auto_parts')) {
		$cp[] = epc_insights_card(
			'vin',
			'VIN / parts requests',
			(float) $vinOpen,
			'number',
			null,
			false,
			$vinOpen > 0 ? 'warn' : 'good',
			$vinOpen > 0 ? 'Unread requests waiting for a quote response.' : 'No open VIN requests.',
			$urls['requests'],
			'Requests'
		);
	}
	$cp[] = epc_insights_card(
		'sku_media',
		'SKU media ready',
		1.0,
		'text',
		null,
		true,
		'info',
		'Enrich SKUs with photos & multi-type specs for higher conversion.',
		$urls['sku_media'],
		'SKU photos & specs'
	);

	$alerts = epc_insights_suite_alerts($financial, $business, $cp, $urls);

	$out = array(
		'currency' => $currency,
		'period' => array(
			'from' => $from,
			'to' => $to,
			'label' => 'MTD',
			'from_label' => date('Y-m-d', $from),
			'to_label' => date('Y-m-d', $to),
		),
		'financial' => $financial,
		'business' => $business,
		'cp' => $cp,
		'alerts' => $alerts,
		'urls' => $urls,
		'has_finance' => !empty($dash),
	);
	$memo[$memoKey] = $out;
	return $out;
}

/**
 * @return array<string,string>
 */
function epc_insights_suite_urls(string $base): array
{
	$erp = $base . '/shop/finance/erp?epc_erp_shell=1';
	return array(
		'erp_home' => $erp . '&area=overview&tab=dashboard',
		'pl' => $erp . '&area=finance&tab=pl',
		'aging' => $erp . '&area=finance&tab=aging',
		'cash' => $erp . '&area=banking&tab=cash_bank',
		'orders' => $base . '/shop/orders/orders',
		'returns' => $base . '/shop/returns-manager',
		'catalogue' => $base . '/shop/catalogue/products',
		'clients' => $base . '/shop/customer_mgmt/customer_mgmt',
		'warehouses' => $base . '/shop/logistics/storages',
		'prices' => $base . '/shop/prices',
		'multivendor' => $base . '/shop/prices/multivendor',
		'sku_media' => $base . '/shop/catalogue/sku_media',
		'requests' => $base . '/requests',
	);
}

/**
 * @return array<string,int>
 */
function epc_insights_suite_commerce_stats(PDO $db): array
{
	$stats = array(
		'orders_today' => 0,
		'orders_week' => 0,
		'orders_prev_week' => 0,
		'open_orders' => 0,
		'products' => 0,
		'clients' => 0,
		'returns_open' => 0,
		'vin_open' => 0,
		'warehouses' => 0,
		'price_lists' => 0,
	);
	$guardFile = epc_insights_doc_root() . '/content/general_pages/epc_tenant_data_guard.php';
	if (is_file($guardFile)) {
		require_once $guardFile;
		if (function_exists('epc_tenant_data_guard_active') && epc_tenant_data_guard_active()) {
			return $stats;
		}
	}
	try {
		$stats['orders_today'] = (int) $db->query(
			'SELECT COUNT(*) FROM `shop_orders` WHERE `successfully_created` = 1 AND `time` >= UNIX_TIMESTAMP(CURDATE())'
		)->fetchColumn();
	} catch (Throwable $e) {
	}
	try {
		$stats['orders_week'] = (int) $db->query(
			'SELECT COUNT(*) FROM `shop_orders` WHERE `successfully_created` = 1 AND `time` >= UNIX_TIMESTAMP(CURDATE() - INTERVAL 6 DAY)'
		)->fetchColumn();
	} catch (Throwable $e) {
	}
	try {
		$stats['orders_prev_week'] = (int) $db->query(
			'SELECT COUNT(*) FROM `shop_orders` WHERE `successfully_created` = 1
			 AND `time` >= UNIX_TIMESTAMP(CURDATE() - INTERVAL 13 DAY)
			 AND `time` < UNIX_TIMESTAMP(CURDATE() - INTERVAL 6 DAY)'
		)->fetchColumn();
	} catch (Throwable $e) {
	}
	try {
		$openStatuses = array();
		$q = $db->query("SELECT `id` FROM `shop_orders_statuses_ref` WHERE `for_inverse` != 1 AND `for_finish` != 1 AND `for_created` != 1");
		while ($r = $q->fetch(PDO::FETCH_ASSOC)) {
			$openStatuses[] = (int) $r['id'];
		}
		if ($openStatuses) {
			$sp = implode(',', array_fill(0, count($openStatuses), '?'));
			$st = $db->prepare("SELECT COUNT(*) FROM `shop_orders` WHERE `successfully_created` = 1 AND `status` IN ($sp)");
			$st->execute($openStatuses);
			$stats['open_orders'] = (int) $st->fetchColumn();
		}
	} catch (Throwable $e) {
	}
	try {
		$stats['products'] = (int) $db->query(
			'SELECT COUNT(*) FROM `shop_catalogue_products` WHERE `published_flag` = 1'
		)->fetchColumn();
	} catch (Throwable $e) {
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
	} catch (Throwable $e) {
	}
	try {
		if ($db->query("SHOW TABLES LIKE 'shop_orders_returns'")->fetchColumn()) {
			$stats['returns_open'] = (int) $db->query(
				"SELECT COUNT(*) FROM `shop_orders_returns` WHERE COALESCE(`status`,0) NOT IN (2,3,9)"
			)->fetchColumn();
		}
	} catch (Throwable $e) {
	}
	try {
		if ($db->query("SHOW TABLES LIKE 'shop_docpart_vin'")->fetchColumn()) {
			$stats['vin_open'] = (int) $db->query(
				"SELECT COUNT(*) FROM `shop_docpart_vin` WHERE COALESCE(`viewed`,0) = 0"
			)->fetchColumn();
		}
	} catch (Throwable $e) {
	}
	try {
		if ($db->query("SHOW TABLES LIKE 'shop_storages'")->fetchColumn()) {
			$stats['warehouses'] = (int) $db->query('SELECT COUNT(*) FROM `shop_storages`')->fetchColumn();
		}
	} catch (Throwable $e) {
	}
	try {
		if ($db->query("SHOW TABLES LIKE 'shop_docpart_prices'")->fetchColumn()) {
			$stats['price_lists'] = (int) $db->query('SELECT COUNT(*) FROM `shop_docpart_prices`')->fetchColumn();
		}
	} catch (Throwable $e) {
	}
	return $stats;
}

function epc_insights_suite_ar_overdue_pct(PDO $db): ?float
{
	$agingFile = epc_insights_doc_root() . '/content/shop/finance/epc_erp_aging.php';
	if (!is_file($agingFile)) {
		return null;
	}
	require_once $agingFile;
	if (!function_exists('epc_erp_ar_aging')) {
		return null;
	}
	try {
		$aging = epc_erp_ar_aging($db);
		$grand = (float) ($aging['grand'] ?? 0);
		if ($grand <= 0) {
			return 0.0;
		}
		$totals = (array) ($aging['totals'] ?? array());
		// Buckets typically: current, 1-30, 31-60, 61-90, 90+
		$overdue = 0.0;
		foreach ($totals as $idx => $amt) {
			if ((int) $idx >= 1) {
				$overdue += (float) $amt;
			}
		}
		return ($overdue / $grand) * 100.0;
	} catch (Throwable $e) {
		return null;
	}
}

/**
 * @param float|null $prev
 * @return array<string,mixed>
 */
function epc_insights_card(
	string $key,
	string $label,
	float $value,
	string $format,
	$prev,
	bool $goodUp,
	string $health,
	string $narrative,
	string $url,
	string $actionLabel
): array {
	$deltaPct = null;
	$deltaLabel = '';
	if ($prev !== null && (float) $prev != 0.0) {
		$deltaPct = (((float) $value - (float) $prev) / abs((float) $prev)) * 100.0;
		$deltaLabel = ($deltaPct >= 0 ? '+' : '') . number_format($deltaPct, 1) . '%';
	} elseif ($prev !== null && (float) $prev == 0.0 && (float) $value != 0.0) {
		$deltaLabel = 'new';
		$deltaPct = 100.0;
	}
	return array(
		'key' => $key,
		'label' => $label,
		'value' => $value,
		'format' => $format,
		'prev' => $prev,
		'delta_pct' => $deltaPct,
		'delta_label' => $deltaLabel,
		'good_up' => $goodUp,
		'health' => $health,
		'narrative' => $narrative,
		'url' => $url,
		'action' => $actionLabel,
	);
}

/**
 * @param list<array<string,mixed>> $financial
 * @param list<array<string,mixed>> $business
 * @param list<array<string,mixed>> $cp
 * @param array<string,string> $urls
 * @return list<array<string,string>>
 */
function epc_insights_suite_alerts(array $financial, array $business, array $cp, array $urls): array
{
	$byKey = array();
	foreach (array_merge($financial, $business, $cp) as $card) {
		$byKey[(string) ($card['key'] ?? '')] = $card;
	}
	$alerts = array();
	$push = static function ($severity, $title, $body, $url, $tone) use (&$alerts) {
		$alerts[] = array(
			'title' => $title,
			'body' => $body,
			'url' => $url,
			'tone' => $tone,
		);
	};
	if (isset($byKey['ar_dso']) && in_array($byKey['ar_dso']['health'], array('warn', 'bad'), true)) {
		$push('ar', 'Collections focus', (string) $byKey['ar_dso']['narrative'], $urls['aging'], 'warn');
	}
	if (isset($byKey['margin']) && in_array($byKey['margin']['health'], array('warn', 'bad'), true)) {
		$push('margin', 'Margin watch', (string) $byKey['margin']['narrative'], $urls['pl'], 'warn');
	}
	if (isset($byKey['open_orders']) && (float) $byKey['open_orders']['value'] > 0) {
		$push('fulfil', 'Fulfilment backlog', (string) $byKey['open_orders']['narrative'], $urls['orders'], 'info');
	}
	if (isset($byKey['cash']) && (float) $byKey['cash']['value'] < 0) {
		$push('cash', 'Negative cash', 'Bank position is negative — reconcile and review outflows.', $urls['cash'], 'bad');
	}
	if (isset($byKey['price_lists']) && (float) $byKey['price_lists']['value'] <= 0) {
		$push('prices', 'Pricing not ready', 'No price lists yet — upload commerce or multivendor data.', $urls['multivendor'], 'warn');
	}
	return array_slice($alerts, 0, 4);
}

function epc_insights_format_value(float $value, string $format, string $currency = 'AED'): string
{
	if ($format === 'text') {
		return 'Ready';
	}
	if ($format === 'pct') {
		return number_format($value, 1) . '%';
	}
	if ($format === 'number') {
		return number_format($value, 0);
	}
	if ($format === 'days') {
		return number_format($value, 0) . ' d';
	}
	return number_format($value, 2) . ' ' . $currency;
}

/**
 * Render HTML panels (CP + ERP share markup; parent supplies wrapper class).
 *
 * @param array<string,mixed> $suite
 */
function epc_insights_suite_render(array $suite, string $variant = 'cp'): string
{
	$currency = (string) ($suite['currency'] ?? 'AED');
	$period = (array) ($suite['period'] ?? array());
	$periodLabel = (string) ($period['label'] ?? 'MTD');
	$fromLabel = (string) ($period['from_label'] ?? '');
	$toLabel = (string) ($period['to_label'] ?? '');
	$sections = array(
		'financial' => array('title' => 'Financial insights', 'icon' => 'fa-line-chart', 'items' => (array) ($suite['financial'] ?? array())),
		'business' => array('title' => 'Business insights', 'icon' => 'fa-briefcase', 'items' => (array) ($suite['business'] ?? array())),
		'cp' => array('title' => 'Control panel insights', 'icon' => 'fa-th-large', 'items' => (array) ($suite['cp'] ?? array())),
	);
	$alerts = (array) ($suite['alerts'] ?? array());
	$h = static function ($v): string {
		return htmlspecialchars((string) $v, ENT_QUOTES, 'UTF-8');
	};

	ob_start();
	?>
	<div class="epc-insights epc-insights--<?php echo $h($variant); ?>" id="epc-insights">
		<div class="epc-insights__head">
			<div>
				<strong><i class="fa fa-lightbulb-o"></i> Insights</strong>
				<span class="epc-insights__period"><?php echo $h($periodLabel); ?><?php
					if ($fromLabel !== '' && $toLabel !== '') {
						echo ' · ' . $h($fromLabel) . ' → ' . $h($toLabel);
					}
				?></span>
			</div>
			<a class="epc-insights__erp" href="<?php echo $h((string) (($suite['urls']['erp_home'] ?? '#'))); ?>">Open ERP Command Centre <i class="fa fa-arrow-right"></i></a>
		</div>
		<?php if ($alerts) { ?>
		<div class="epc-insights__alerts">
			<?php foreach ($alerts as $a) { ?>
			<a class="epc-insights__alert epc-insights__alert--<?php echo $h($a['tone'] ?? 'info'); ?>" href="<?php echo $h($a['url'] ?? '#'); ?>">
				<strong><?php echo $h($a['title'] ?? ''); ?></strong>
				<span><?php echo $h($a['body'] ?? ''); ?></span>
			</a>
			<?php } ?>
		</div>
		<?php } ?>
		<div class="epc-insights__grid">
			<?php foreach ($sections as $sec) { ?>
			<section class="epc-insights__col">
				<h4><i class="fa <?php echo $h($sec['icon']); ?>"></i> <?php echo $h($sec['title']); ?></h4>
				<div class="epc-insights__cards">
					<?php foreach ($sec['items'] as $card) {
						$health = (string) ($card['health'] ?? 'info');
						$delta = (string) ($card['delta_label'] ?? '');
						$goodUp = !empty($card['good_up']);
						$deltaPct = $card['delta_pct'];
						$deltaClass = '';
						if ($delta !== '') {
							$up = is_numeric($deltaPct) ? ((float) $deltaPct >= 0) : ($delta === 'new');
							$deltaClass = ($up === $goodUp) ? 'is-good' : 'is-bad';
						}
						?>
					<article class="epc-insights__card is-<?php echo $h($health); ?>">
						<div class="epc-insights__card-top">
							<span class="epc-insights__label"><?php echo $h($card['label'] ?? ''); ?></span>
							<span class="epc-insights__health"><?php echo $h($health); ?></span>
						</div>
						<div class="epc-insights__value">
							<?php echo $h(epc_insights_format_value((float) ($card['value'] ?? 0), (string) ($card['format'] ?? 'money'), $currency)); ?>
							<?php if ($delta !== '') { ?>
							<span class="epc-insights__delta <?php echo $h($deltaClass); ?>"><?php echo $h($delta); ?></span>
							<?php } ?>
						</div>
						<p class="epc-insights__narrative"><?php echo $h($card['narrative'] ?? ''); ?></p>
						<a class="epc-insights__action" href="<?php echo $h($card['url'] ?? '#'); ?>"><?php echo $h($card['action'] ?? 'Open'); ?> <i class="fa fa-angle-right"></i></a>
					</article>
					<?php } ?>
				</div>
			</section>
			<?php } ?>
		</div>
	</div>
	<?php
	return (string) ob_get_clean();
}
