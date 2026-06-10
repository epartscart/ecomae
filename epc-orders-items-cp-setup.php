<?php
/**
 * Ensure CP route shop/orders/items (+ edit/add) and PHP script paths are registered.
 * https://www.ecomae.com/epc-orders-items-cp-setup.php?token=epartscart-deploy-2026
 * Apply: &apply=1
 */
declare(strict_types=1);

error_reporting(E_ALL);
ini_set('display_errors', '1');

require_once __DIR__ . '/epc_deploy_auth.php';
epc_deploy_require_token();
header('Content-Type: application/json; charset=utf-8');
set_time_limit(120);

define('_ASTEXE_', 1);
require_once __DIR__ . '/config.php';
require_once __DIR__ . '/content/general_pages/epc_portal.php';
require_once __DIR__ . '/content/general_pages/epc_portal_tenant.php';

$cfg = new DP_Config();
epc_portal_apply_config($cfg);
$apply = !empty($_GET['apply']);
$backend = trim((string) ($cfg->backend_dir ?? 'cp'), '/');

try {
	$pdo = new PDO(
		'mysql:host=' . $cfg->host . ';dbname=' . $cfg->db . ';charset=utf8',
		$cfg->user,
		$cfg->password,
		array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION)
	);
} catch (Throwable $e) {
	http_response_code(500);
	exit(json_encode(array('ok' => false, 'error' => 'DB: ' . $e->getMessage()), JSON_UNESCAPED_UNICODE | JSON_PRETTY_PRINT));
}

function epc_oi_lang(PDO $pdo, string $key, string $en, string $ru): void
{
	$pdo->prepare(
		'INSERT IGNORE INTO `lang_text_strings` (`str_key`, `description`, `same`, `is_error`, `is_custom`, `used_found`)
		 VALUES (?, ?, NULL, 0, 1, 1)'
	)->execute(array($key, $en));
	$pdo->prepare(
		'INSERT INTO `lang_text_strings_translation` (`str_key`, `lang_code`, `value`) VALUES (?, ?, ?)
		 ON DUPLICATE KEY UPDATE `value` = VALUES(`value`)'
	)->execute(array($key, 'en', $en));
	$pdo->prepare(
		'INSERT INTO `lang_text_strings_translation` (`str_key`, `lang_code`, `value`) VALUES (?, ?, ?)
		 ON DUPLICATE KEY UPDATE `value` = VALUES(`value`)'
	)->execute(array($key, 'ru', $ru));
}

function epc_oi_sync_access(PDO $pdo, int $contentId): int
{
	if ($contentId <= 0) {
		return 0;
	}
	$ref = $pdo->prepare('SELECT `id` FROM `content` WHERE `url` = ? AND `is_frontend` = 0 LIMIT 1');
	$refIds = array();
	foreach (array('shop/orders/orders', 'shop/orders/order', 'shop') as $refUrl) {
		$ref->execute(array($refUrl));
		$rid = (int) $ref->fetchColumn();
		if ($rid > 0) {
			$refIds[] = $rid;
		}
	}
	$added = 0;
	if (!empty($refIds)) {
		$pdo->prepare('DELETE FROM `content_access` WHERE `content_id` = ?')->execute(array($contentId));
		$placeholders = implode(',', array_fill(0, count($refIds), '?'));
		$groups = $pdo->prepare(
			'SELECT DISTINCT `group_id` FROM `content_access` WHERE `content_id` IN (' . $placeholders . ')'
		);
		$groups->execute($refIds);
		$ins = $pdo->prepare('INSERT IGNORE INTO `content_access` (`content_id`, `group_id`) VALUES (?, ?)');
		while ($g = $groups->fetch(PDO::FETCH_ASSOC)) {
			$ins->execute(array($contentId, (int) $g['group_id']));
			$added++;
		}
	}
	if ($added === 0) {
		$rootG = (int) $pdo->query('SELECT `id` FROM `groups` WHERE `for_backend` = 1 LIMIT 1')->fetchColumn();
		$pdo->prepare('INSERT IGNORE INTO `content_access` (`content_id`, `group_id`) VALUES (?, ?)')
			->execute(array($contentId, $rootG > 0 ? $rootG : 1));
		$added = 1;
	}
	return $added;
}

function epc_oi_register_route(
	PDO $pdo,
	string $backend,
	string $parentUrl,
	string $url,
	string $alias,
	string $valueKey,
	string $phpPath,
	string $titleKey,
	int $order,
	bool $apply
): array {
	$parent = $pdo->prepare('SELECT `id`, `level` FROM `content` WHERE `url` = ? AND `is_frontend` = 0 LIMIT 1');
	$parent->execute(array($parentUrl));
	$parentRow = $parent->fetch(PDO::FETCH_ASSOC);
	if (!$parentRow) {
		throw new RuntimeException('Parent content row missing: ' . $parentUrl);
	}
	$parentId = (int) $parentRow['id'];
	$level = (int) $parentRow['level'] + 1;
	$now = time();
	$cssJs = '';

	$existing = $pdo->prepare('SELECT * FROM `content` WHERE `url` = ? AND `is_frontend` = 0 LIMIT 1');
	$existing->execute(array($url));
	$row = $existing->fetch(PDO::FETCH_ASSOC);
	$contentId = $row ? (int) $row['id'] : 0;
	$changed = false;

	if ($apply) {
		if ($contentId > 0) {
			$pdo->prepare(
				'UPDATE `content` SET `published_flag` = 1, `content_type` = \'php\', `is_frontend` = 0, `system_flag` = 0,
				 `content` = ?, `title_tag` = ?, `value` = ?, `parent` = ?, `level` = ?, `alias` = ?, `css_js` = ?, `time_edited` = ?, `order` = ?
				 WHERE `id` = ?'
			)->execute(array($phpPath, $titleKey, $valueKey, $parentId, $level, $alias, $cssJs, $now, $order, $contentId));
			$changed = true;
		} else {
			$pdo->prepare(
				'INSERT INTO `content`
				(`count`, `url`, `level`, `alias`, `value`, `parent`, `description`, `is_frontend`, `content_type`, `content`,
				 `title_tag`, `description_tag`, `keywords_tag`, `author_tag`, `main_flag`, `modules_array`, `css_js`, `robots_tag`,
				 `system_flag`, `published_flag`, `open`, `time_created`, `time_edited`, `order`)
				 VALUES (0, ?, ?, ?, ?, ?, ?, 0, \'php\', ?, ?, \'0\', \'0\', \'0\', 0, \'[]\', ?, \'\', 0, 1, 0, ?, ?, ?)'
			)->execute(array(
				$url,
				$level,
				$alias,
				$valueKey,
				$parentId,
				$titleKey,
				$phpPath,
				$titleKey,
				$cssJs,
				$now,
				$now,
				$order,
			));
			$contentId = (int) $pdo->lastInsertId();
			$changed = true;
		}
		if ($contentId > 0) {
			epc_oi_sync_access($pdo, $contentId);
		}
	}

	$resolved = str_replace('<backend_dir>', $backend, $_SERVER['DOCUMENT_ROOT'] . $phpPath);
	return array(
		'url' => $url,
		'content_id' => $contentId,
		'db_content_path' => $row['content'] ?? null,
		'expected_path' => $phpPath,
		'resolved_path' => $resolved,
		'file_exists' => is_file($resolved),
		'published_flag' => $row ? (int) $row['published_flag'] : null,
		'changed' => $changed,
		'access_groups' => $contentId > 0
			? (int) $pdo->query('SELECT COUNT(*) FROM `content_access` WHERE `content_id` = ' . $contentId)->fetchColumn()
			: 0,
	);
}

$report = array(
	'ok' => true,
	'apply' => $apply,
	'db' => $cfg->db,
	'hostname' => $_SERVER['HTTP_HOST'] ?? '',
	'backend_dir' => $backend,
	'routes' => array(),
	'changes' => array(),
);

if ($apply) {
	epc_oi_lang($pdo, '288', 'Orders items', 'Позиции заказов');
	epc_oi_lang($pdo, '290', 'Orders items', 'Позиции заказов');
	epc_oi_lang($pdo, '4756', 'Page script missing', 'Скрипт страницы отсутствует');
	$report['changes'][] = 'lang strings 288, 290, 4756 ensured';
}

try {
	$report['routes'][] = epc_oi_register_route(
		$pdo,
		$backend,
		'shop/orders',
		'shop/orders/items',
		'items',
		'288',
		'/<backend_dir>/content/shop/order_process/orders_items.php',
		'290',
		70,
		$apply
	);
	$report['routes'][] = epc_oi_register_route(
		$pdo,
		$backend,
		'shop/orders/items',
		'shop/orders/items/edit',
		'edit',
		'631',
		'/<backend_dir>/content/shop/order_process/orders_items_edit.php',
		'633',
		72,
		$apply
	);
	$report['routes'][] = epc_oi_register_route(
		$pdo,
		$backend,
		'shop/orders/items',
		'shop/orders/items/add',
		'add',
		'635',
		'/<backend_dir>/content/shop/order_process/orders_items_add.php',
		'637',
		71,
		$apply
	);
} catch (Throwable $e) {
	$report['ok'] = false;
	$report['error'] = $e->getMessage();
}

$ordersRef = $pdo->prepare('SELECT `id`, `published_flag`, `content` FROM `content` WHERE `url` = ? AND `is_frontend` = 0 LIMIT 1');
$ordersRef->execute(array('shop/orders/orders'));
$report['reference_shop_orders_orders'] = $ordersRef->fetch(PDO::FETCH_ASSOC) ?: null;

foreach ($report['routes'] as $route) {
	if (!$route['file_exists'] || ($route['published_flag'] !== null && !(int) $route['published_flag'])) {
		$report['ok'] = false;
	}
	if ($route['changed']) {
		$report['changes'][] = 'registered ' . $route['url'];
	}
}

$editPath = $_SERVER['DOCUMENT_ROOT'] . '/' . $backend . '/content/shop/order_process/orders_items_edit.php';
$report['edit_render_smoke'] = array('file' => $editPath, 'exists' => is_file($editPath));
if (is_file($editPath)) {
	if (!function_exists('translate_str_by_id')) {
		function translate_str_by_id($id) { return (string) $id; }
	}
	$GLOBALS['DP_Config'] = $cfg;
	$GLOBALS['db_link'] = $pdo;
	$db_link = $pdo;
	$DP_Config = $cfg;
	$user_session = array('csrf_guard_key' => 'verify');
	$sampleItemId = 0;
	try {
		$sampleItemId = (int) $pdo->query('SELECT `id` FROM `shop_orders_items` ORDER BY `id` DESC LIMIT 1')->fetchColumn();
	} catch (Throwable $e) {
		$sampleItemId = 0;
	}
	$report['edit_render_smoke']['sample_item_id'] = $sampleItemId;
	if ($sampleItemId > 0) {
		$_GET['id'] = $sampleItemId;
		require_once $_SERVER['DOCUMENT_ROOT'] . '/content/users/dp_user.php';
		$ordersBackground = $_SERVER['DOCUMENT_ROOT'] . '/' . $backend . '/content/shop/order_process/orders_background.php';
		if (is_file($ordersBackground)) {
			include $ordersBackground;
		}
		ob_start();
		try {
			include $editPath;
			$html = (string) ob_get_clean();
			$report['edit_render_smoke'] = array_merge($report['edit_render_smoke'], array(
				'include_ok' => true,
				'html_length' => strlen($html),
				'has_edit_table' => stripos($html, 'id="orders_items_table"') !== false,
				'has_save_form' => stripos($html, 'id="save_form"') !== false,
				'has_inp_price' => stripos($html, 'id="inp_price"') !== false,
				'inline_script_leak' => stripos($html, '<script') !== false,
				'storage_sql_bug' => stripos((string) file_get_contents($editPath), 'order_item_id` = ?), `t2_storage_id`') !== false,
				'php_fatal_in_html' => (bool) preg_match('/\b(Fatal error|Parse error|Uncaught)\b/i', $html),
				'ok' => stripos($html, 'id="orders_items_table"') !== false
					&& stripos($html, 'id="inp_price"') !== false
					&& stripos($html, '<script') === false
					&& !preg_match('/\b(Fatal error|Parse error|Uncaught)\b/i', $html),
			));
			if (empty($report['edit_render_smoke']['ok'])) {
				$report['ok'] = false;
			}
		} catch (Throwable $e) {
			if (ob_get_level() > 0) {
				ob_end_clean();
			}
			$report['edit_render_smoke']['include_ok'] = false;
			$report['edit_render_smoke']['error'] = $e->getMessage();
			$report['edit_render_smoke']['ok'] = false;
			$report['ok'] = false;
		}
		unset($_GET['id']);
	}
}

$base = rtrim((string) $cfg->domain_path, '/');
$report['urls'] = array(
	'items' => $base . '/' . $backend . '/shop/orders/items',
	'orders' => $base . '/' . $backend . '/shop/orders/orders',
);
$report['hint'] = $apply
	? 'Applied. Hard-refresh CP and open /cp/shop/orders/items while logged in.'
	: 'Dry run — add apply=1 to register content rows and sync access.';

if ($apply && function_exists('opcache_reset')) {
	@opcache_reset();
	$report['changes'][] = 'opcache_reset';
}

echo json_encode($report, JSON_UNESCAPED_UNICODE | JSON_PRETTY_PRINT);
