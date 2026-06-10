<?php
/**
 * Shop quote flow probe — tables, routes, CP module, recent quotes.
 * GET ?token=…&site_key=epartscart  (omit site_key for all Model C tenants)
 */
declare(strict_types=1);

require_once __DIR__ . '/epc_deploy_auth.php';
epc_deploy_require_token();
header('Content-Type: application/json; charset=utf-8');
set_time_limit(120);

define('_ASTEXE_', 1);
require_once __DIR__ . '/config.php';
require_once __DIR__ . '/content/general_pages/epc_portal.php';
require_once __DIR__ . '/content/general_pages/epc_portal_tenant.php';

$onlySite = preg_replace('/[^a-z0-9_]/', '', strtolower(trim((string) ($_GET['site_key'] ?? ''))));

$tenants = array(
	'epartscart' => 'www.epartscart.com',
	'electronicae' => 'www.electronicae.com',
	'taxofinca' => 'www.taxofinca.com',
	'stylenlook' => 'www.stylenlook.com',
	'thejewellerytrend' => 'www.thejewellerytrend.com',
);

function epc_quote_flow_file_ok(string $rel): bool
{
	$path = ($_SERVER['DOCUMENT_ROOT'] ?? __DIR__) . '/' . ltrim($rel, '/');
	return is_file($path);
}

function epc_quote_flow_probe_tenant(string $siteKey, string $host): array
{
	$_SERVER['HTTP_HOST'] = $host;
	$_SERVER['SERVER_NAME'] = $host;
	unset($GLOBALS['epc_portal_config_applied'], $GLOBALS['epc_db_link']);

	$cfg = new DP_Config();
	epc_portal_apply_config($cfg);

	$site = epc_portal_site_profile();
	$backend = (string) ($cfg->backend_dir ?? 'cp');

	$out = array(
		'site_key' => $siteKey,
		'host' => $host,
		'docroot' => $_SERVER['DOCUMENT_ROOT'] ?? '',
		'cp_db' => (string) ($cfg->db ?? ''),
		'profile_db' => (string) ($site['db'] ?? ''),
		'db_ok' => false,
		'tables' => array(
			'shop_quote_requests' => false,
			'shop_quote_items' => false,
		),
		'quotes_total' => 0,
		'quotes_by_status' => array(),
		'recent_quotes' => array(),
		'routes' => array(
			'storefront_quotes' => null,
			'cp_quote_requests' => null,
		),
		'cp_menu_quote_requests' => false,
		'files' => array(),
		'part_search_quote_mode' => null,
		'urls' => array(
			'storefront_quotes' => 'https://' . $host . '/en/shop/quotes',
			'cp_quote_requests' => 'https://' . $host . '/' . $backend . '/shop/quote-requests',
			'ajax_add_to_quote' => 'https://' . $host . '/content/shop/order_process/ajax_add_to_quote.php',
		),
		'registry_db' => null,
		'error' => null,
		'status' => 'check',
	);

	$requiredFiles = array(
		'content/shop/order_process/ajax_add_to_quote.php',
		'content/shop/order_process/ajax_add_to_quote_manual.php',
		'content/shop/order_process/ajax_quote_submit.php',
		'content/shop/order_process/ajax_quote_accept.php',
		'content/shop/order_process/my_quotes.php',
		'cp/content/shop/quote_requests/quote_requests.php',
		'content/shop/docpart/part_search_page_1.php',
		'content/shop/docpart/part_search_page_2.php',
	);
	foreach ($requiredFiles as $rel) {
		$out['files'][$rel] = epc_quote_flow_file_ok($rel);
	}

	$ps1 = ($_SERVER['DOCUMENT_ROOT'] ?? __DIR__) . '/content/shop/docpart/part_search_page_1.php';
	if (is_file($ps1)) {
		$src = (string) file_get_contents($ps1);
		if (strpos($src, "epcProductActionsHTML(Product.aid, p_exist, p_min_order, 'both'") !== false) {
			$out['part_search_quote_mode'] = 'both';
		} elseif (strpos($src, "epcProductActionsHTML(Product.aid, p_exist, p_min_order, 'cart_only'") !== false) {
			$out['part_search_quote_mode'] = 'cart_only';
		} else {
			$out['part_search_quote_mode'] = 'unknown';
		}
	}

	try {
		$pdo = new PDO(
			'mysql:host=' . $cfg->host . ';dbname=' . $cfg->db . ';charset=utf8',
			$cfg->user,
			$cfg->password,
			array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION)
		);
		$out['db_ok'] = true;

		foreach (array_keys($out['tables']) as $table) {
			$chk = $pdo->query("SHOW TABLES LIKE " . $pdo->quote($table));
			$out['tables'][$table] = (bool) $chk->fetchColumn();
		}

		if ($out['tables']['shop_quote_requests']) {
			$out['quotes_total'] = (int) $pdo->query('SELECT COUNT(*) FROM `shop_quote_requests`')->fetchColumn();
			$st = $pdo->query('SELECT `status`, COUNT(*) AS `c` FROM `shop_quote_requests` GROUP BY `status`');
			while ($row = $st->fetch(PDO::FETCH_ASSOC)) {
				$out['quotes_by_status'][(string) $row['status']] = (int) $row['c'];
			}
			$rq = $pdo->query('SELECT `id`, `user_id`, `status`, `time_updated` FROM `shop_quote_requests` ORDER BY `id` DESC LIMIT 5');
			while ($row = $rq->fetch(PDO::FETCH_ASSOC)) {
				$out['recent_quotes'][] = array(
					'id' => (int) $row['id'],
					'user_id' => (int) $row['user_id'],
					'status' => (string) $row['status'],
					'time_updated' => $row['time_updated'] ? date('Y-m-d H:i', (int) $row['time_updated']) : null,
				);
			}
		}

		$routeQ = $pdo->prepare("SELECT `id`, `url`, `content`, `is_frontend`, `published_flag` FROM `content` WHERE `url` IN ('shop/quotes', 'shop/quote-requests')");
		$routeQ->execute();
		while ($row = $routeQ->fetch(PDO::FETCH_ASSOC)) {
			$isFrontend = (int) ($row['is_frontend'] ?? 0) === 1;
			$key = $isFrontend ? 'storefront_quotes' : 'cp_quote_requests';
			$out['routes'][$key] = array(
				'id' => (int) $row['id'],
				'url' => (string) $row['url'],
				'content' => (string) $row['content'],
				'is_frontend' => $isFrontend,
				'published_flag' => (int) ($row['published_flag'] ?? 0),
			);
		}

		$menuQ = $pdo->prepare("SELECT COUNT(*) FROM `control_items` WHERE `url` LIKE ?");
		$menuQ->execute(array('%shop/quote-requests%'));
		$out['cp_menu_quote_requests'] = ((int) $menuQ->fetchColumn()) > 0;
	} catch (Throwable $e) {
		$out['error'] = $e->getMessage();
	}

	$platformPdo = epc_portal_platform_pdo();
	if ($platformPdo instanceof PDO) {
		require_once __DIR__ . '/content/general_pages/epc_portal_db.php';
		epc_portal_db_ensure($platformPdo);
		$st = $platformPdo->prepare('SELECT `db_name` FROM `epc_portal_tenants` WHERE `site_key` = ? LIMIT 1');
		$st->execute(array($siteKey));
		$row = $st->fetch(PDO::FETCH_ASSOC);
		if ($row) {
			$out['registry_db'] = (string) ($row['db_name'] ?? '');
		}
	}

	$filesOk = !in_array(false, $out['files'], true);
	$tablesOk = $out['tables']['shop_quote_requests'] && $out['tables']['shop_quote_items'];
	$routesOk = is_array($out['routes']['storefront_quotes']) && is_array($out['routes']['cp_quote_requests']);
	$modeOk = $out['part_search_quote_mode'] === 'both';

	if ($out['db_ok'] && $filesOk && $tablesOk && $routesOk && $modeOk) {
		$out['status'] = 'ok';
	} elseif ($out['db_ok'] && $tablesOk && $routesOk) {
		$out['status'] = 'warn';
	} else {
		$out['status'] = 'check';
	}

	return $out;
}

$results = array();
foreach ($tenants as $siteKey => $host) {
	if ($onlySite !== '' && $onlySite !== $siteKey) {
		continue;
	}
	$results[$siteKey] = epc_quote_flow_probe_tenant($siteKey, $host);
}

echo json_encode(
	array(
		'probe' => 'epc-quote-flow-verify',
		'ts' => gmdate('c'),
		'flow' => array(
			'customer_add' => 'Part search → Add to Quote → ajax_add_to_quote.php → draft quote',
			'customer_submit' => '/shop/quotes → Submit for quote → ajax_quote_submit.php → submitted',
			'cp_price' => '/cp/shop/quote-requests → Save prices → Publish → quoted',
			'customer_accept' => '/shop/quotes → Accept → ajax_quote_accept.php → cart → checkout',
		),
		'notes' => array(
			'email_on_publish' => 'Not implemented — customer must revisit /shop/quotes',
			'guest_quotes' => 'Login required — no guest quote basket',
		),
		'tenants' => $results,
	),
	JSON_PRETTY_PRINT | JSON_UNESCAPED_SLASHES
);
