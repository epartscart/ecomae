<?php
/**
 * Apply one OMS · Orders sidebar entry + register OMS daily guide route.
 * https://www.epartscart.com/epc-oms-menu-guide-setup.php?token=…&apply=1
 * Optional: &site_key=epartscart
 */
declare(strict_types=1);

require_once __DIR__ . '/epc_deploy_auth.php';
epc_deploy_require_token();
header('Content-Type: application/json; charset=utf-8');
set_time_limit(180);

$apply = !empty($_GET['apply']);
$onlySite = preg_replace('/[^a-z0-9_]/', '', strtolower(trim((string) ($_GET['site_key'] ?? ''))));

define('_ASTEXE_', 1);
require_once __DIR__ . '/config.php';
require_once __DIR__ . '/content/general_pages/epc_portal.php';
require_once __DIR__ . '/content/general_pages/epc_portal_tenant.php';
require_once __DIR__ . '/epc_cp_mainstream_menu.php';

if (is_file(__DIR__ . '/content/general_pages/epc_perf_cache.php')) {
	require_once __DIR__ . '/content/general_pages/epc_perf_cache.php';
}

$tenants = array(
	'epartscart' => 'www.epartscart.com',
	'electronicae' => 'www.electronicae.com',
	'taxofinca' => 'www.taxofinca.com',
	'stylenlook' => 'www.stylenlook.com',
	'thejewellerytrend' => 'www.thejewellerytrend.com',
);

function epc_oms_setup_pdo($cfg): PDO
{
	$host = trim((string) ($cfg->host ?? '127.0.0.1'));
	if ($host === 'localhost') {
		$host = '127.0.0.1';
	}
	return new PDO(
		'mysql:host=' . $host . ';dbname=' . $cfg->db . ';charset=utf8',
		(string) $cfg->user,
		(string) $cfg->password,
		array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION)
	);
}

function epc_oms_register_guide_route(PDO $pdo, string $backend): array
{
	$url = 'shop/orders/oms-guide';
	$php = '/' . $backend . '/content/shop/order_process/oms_daily_guide_page.php';
	$caption = 'epc_oms_guide_cp';
	$title = 'OMS daily guide — step by step';

	if (function_exists('epc_cp_mm_lang')) {
		epc_cp_mm_lang($pdo, $caption, 'OMS daily guide', 'OMS — ежедневный гид');
	}

	$parentId = 0;
	$pst = $pdo->prepare('SELECT `id` FROM `content` WHERE `url` = ? AND `is_frontend` = 0 LIMIT 1');
	$pst->execute(array('shop/orders/orders'));
	$parentId = (int) $pst->fetchColumn();

	$st = $pdo->prepare('SELECT `id` FROM `content` WHERE `url` = ? AND `is_frontend` = 0 LIMIT 1');
	$st->execute(array($url));
	$id = (int) $st->fetchColumn();

	$changed = false;
	if ($id > 0) {
		$pdo->prepare(
			'UPDATE `content` SET `content_type` = ?, `content` = ?, `published_flag` = 1, `title` = ?, `caption` = ?, `parent` = ? WHERE `id` = ?'
		)->execute(array('php', $php, $title, $caption, $parentId > 0 ? $parentId : null, $id));
		$changed = true;
	} else {
		$pdo->prepare(
			'INSERT INTO `content` (`url`, `is_frontend`, `content_type`, `content`, `published_flag`, `title`, `caption`, `parent`, `order`)
			 VALUES (?, 0, ?, ?, 1, ?, ?, ?, 81)'
		)->execute(array($url, 'php', $php, $title, $caption, $parentId > 0 ? $parentId : null));
		$id = (int) $pdo->lastInsertId();
		$changed = true;
	}

	// Access from orders
	if ($parentId > 0 && $id > 0) {
		$pdo->prepare('DELETE FROM `content_access` WHERE `content_id` = ?')->execute(array($id));
		$g = $pdo->prepare('SELECT DISTINCT `group_id` FROM `content_access` WHERE `content_id` = ?');
		$g->execute(array($parentId));
		$ins = $pdo->prepare('INSERT IGNORE INTO `content_access` (`content_id`, `group_id`) VALUES (?, ?)');
		while ($row = $g->fetch(PDO::FETCH_ASSOC)) {
			$ins->execute(array($id, (int) $row['group_id']));
		}
	}

	return array('content_id' => $id, 'url' => $url, 'php' => $php, 'changed' => $changed);
}

$report = array('ok' => true, 'apply' => $apply, 'tenants' => array());

foreach ($tenants as $siteKey => $host) {
	if ($onlySite !== '' && $onlySite !== $siteKey) {
		continue;
	}
	$row = array('site_key' => $siteKey, 'host' => $host, 'ok' => false);
	try {
		$_SERVER['HTTP_HOST'] = $host;
		$_SERVER['SERVER_NAME'] = $host;
		unset($GLOBALS['epc_portal_config_applied'], $GLOBALS['epc_db_link']);

		$cfg = new DP_Config();
		epc_portal_apply_config($cfg);
		$pdo = epc_oms_setup_pdo($cfg);
		$backend = trim((string) ($cfg->backend_dir ?? 'cp'), '/');
		if ($backend === '') {
			$backend = 'cp';
		}

		$menu = array('preview' => true);
		$guide = array('preview' => true);
		if ($apply) {
			$menu = epc_cp_shop_orders_menu_apply($pdo);
			$guide = epc_oms_register_guide_route($pdo, $backend);
			if (function_exists('epc_perf_cache_bust_prefix')) {
				epc_perf_cache_bust_prefix('epc_cp_menu_rows');
			}
		} else {
			$st = $pdo->query(
				"SELECT `id`, `caption`, `url` FROM `control_items`
				 WHERE `url` LIKE '%/shop/orders/%'
				 ORDER BY `items_group`, `order`, `id`"
			);
			$menu['current_items'] = $st->fetchAll(PDO::FETCH_ASSOC);
			$gst = $pdo->prepare('SELECT `id`, `url`, `content` FROM `content` WHERE `url` = ? AND `is_frontend` = 0 LIMIT 1');
			$gst->execute(array('shop/orders/oms-guide'));
			$guide['existing'] = $gst->fetch(PDO::FETCH_ASSOC) ?: null;
		}

		$row['ok'] = true;
		$row['db'] = $cfg->db;
		$row['menu'] = $menu;
		$row['guide'] = $guide;
	} catch (Throwable $e) {
		$row['error'] = $e->getMessage();
	}
	$report['tenants'][] = $row;
}

echo json_encode($report, JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES | JSON_PRETTY_PRINT);
