<?php
/**
 * Audit and ensure all shop/orders CP pages have content rows and PHP scripts on disk.
 * https://www.ecomae.com/epc-orders-cp-setup.php?token=epartscart-deploy-2026
 * Apply: &apply=1
 */
declare(strict_types=1);

error_reporting(E_ALL);
ini_set('display_errors', '1');
header('Content-Type: application/json; charset=utf-8');

require_once __DIR__ . '/epc_deploy_auth.php';
epc_deploy_require_token();

define('_ASTEXE_', 1);
require_once __DIR__ . '/config.php';

$cfg = new DP_Config();
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

function epc_oc_lang(PDO $pdo, string $key, string $en, string $ru): void
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

function epc_oc_sync_access(PDO $pdo, int $contentId): int
{
	if ($contentId <= 0) {
		return 0;
	}
	$ref = $pdo->prepare('SELECT `id` FROM `content` WHERE `url` = ? AND `is_frontend` = 0 LIMIT 1');
	$refIds = array();
	foreach (array('shop/orders/orders', 'shop/orders/order', 'shop', 'shop/orders') as $refUrl) {
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

function epc_oc_css_js(string $url): string
{
	if ($url === 'shop/orders/statuses') {
		return '<link rel="stylesheet" href="/lib/webix/codebase/webix.css" type="text/css" />' . "\r\n"
			. '<script src="/lib/webix/codebase/webix.js" type="text/javascript"></script>' . "\r\n"
			. '<script src="/lib/jQuery/jQuery.js" type="text/javascript"></script>' . "\r\n";
	}
	if (in_array($url, array('shop/orders/orders', 'shop/orders/items'), true)) {
		return "\n<script src=\"/lib/datetimepicker/jquery.datetimepicker.js\" type=\"text/javascript\"></script>\n"
			. '<link href="/lib/datetimepicker/jquery.datetimepicker.css" rel="stylesheet">' . "\n"
			. '<script src="/lib/multiple_select/jquery.multiple.select.js"></script>' . "\n"
			. '<link href="/lib/multiple_select/multiple-select.css" rel="stylesheet">' . "\n";
	}
	if ($url === 'shop/orders/carts') {
		return '<script src="/lib/datetimepicker/jquery.datetimepicker.js" type="text/javascript"></script>' . "\r\n"
			. '<link href="/lib/datetimepicker/jquery.datetimepicker.css" rel="stylesheet">' . "\r\n";
	}
	return '';
}

function epc_oc_register_route(
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
	$cssJs = epc_oc_css_js($url);

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
			epc_oc_sync_access($pdo, $contentId);
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

function epc_oc_audit_db_route(PDO $pdo, string $backend, array $row): array
{
	$phpPath = (string) ($row['content'] ?? '');
	if ($phpPath === '' || $phpPath === '0') {
		return array(
			'url' => $row['url'],
			'content_id' => (int) $row['id'],
			'db_content_path' => $phpPath,
			'expected_path' => null,
			'resolved_path' => null,
			'file_exists' => false,
			'published_flag' => (int) $row['published_flag'],
			'changed' => false,
			'access_groups' => (int) $pdo->query(
				'SELECT COUNT(*) FROM `content_access` WHERE `content_id` = ' . (int) $row['id']
			)->fetchColumn(),
			'source' => 'db_only',
		);
	}
	$resolved = str_replace('<backend_dir>', $backend, $_SERVER['DOCUMENT_ROOT'] . $phpPath);
	return array(
		'url' => $row['url'],
		'content_id' => (int) $row['id'],
		'db_content_path' => $phpPath,
		'expected_path' => $phpPath,
		'resolved_path' => $resolved,
		'file_exists' => is_file($resolved),
		'published_flag' => (int) $row['published_flag'],
		'changed' => false,
		'access_groups' => (int) $pdo->query(
			'SELECT COUNT(*) FROM `content_access` WHERE `content_id` = ' . (int) $row['id']
		)->fetchColumn(),
		'source' => 'db_only',
	);
}

$canonicalRoutes = array(
	array('shop/orders', 'shop/orders/sao_states_statuses_link', 'sao_states_statuses_link', '561', '/<backend_dir>/content/shop/sao/states_statuses_link.php', '563', 66),
	array('shop/orders', 'shop/orders/statuses', 'statuses', '279', '/<backend_dir>/content/shop/order_process/statuses.php', '281', 67),
	array('shop/orders', 'shop/orders/orders', 'orders', '282', '/<backend_dir>/content/shop/order_process/orders.php', '284', 68),
	array('shop/orders', 'shop/orders/order', 'order', '285', '/<backend_dir>/content/shop/order_process/order_card.php', '287', 69),
	array('shop/orders', 'shop/orders/items', 'items', '288', '/<backend_dir>/content/shop/order_process/orders_items.php', '290', 70),
	array('shop/orders/items', 'shop/orders/items/add', 'add', '635', '/<backend_dir>/content/shop/order_process/orders_items_add.php', '637', 71),
	array('shop/orders/items', 'shop/orders/items/edit', 'edit', '631', '/<backend_dir>/content/shop/order_process/orders_items_edit.php', '633', 72),
	array('shop/orders', 'shop/orders/carts', 'carts', '291', '/<backend_dir>/content/shop/order_process/carts.php', '293', 73),
	array('shop/orders/orders', 'shop/orders/guide', 'guide', 'Order fulfilment guide', '/<backend_dir>/content/shop/order_process/order_fulfilment_guide_page.php', 'Order fulfilment guide — eParts Cart', 80),
	array('shop/orders/orders', 'shop/orders/whatsapp-guide', 'whatsapp-guide', 'epc_whatsapp_guide_cp', '/<backend_dir>/content/shop/order_process/whatsapp_guide_page.php', 'WhatsApp sharing guide', 82),
	array('shop/orders/orders', 'shop/orders/oms-guide', 'oms-guide', 'epc_oms_guide_cp', '/<backend_dir>/content/shop/order_process/oms_daily_guide_page.php', 'OMS daily guide — step by step', 81),
);

$report = array(
	'ok' => true,
	'apply' => $apply,
	'db' => $cfg->db,
	'hostname' => $_SERVER['HTTP_HOST'] ?? '',
	'backend_dir' => $backend,
	'routes' => array(),
	'db_extra_routes' => array(),
	'missing_files' => array(),
	'changes' => array(),
);

if ($apply) {
	$langPairs = array(
		array('4756', 'Page script missing', 'Скрипт страницы отсутствует'),
		array('279', 'Orders statuses', 'Статусы заказов'),
		array('281', 'Orders statuses', 'Статусы заказов'),
		array('288', 'Orders items', 'Позиции заказов'),
		array('290', 'Orders items', 'Позиции заказов'),
		array('282', 'Orders', 'Заказы'),
		array('284', 'Orders', 'Заказы'),
		array('285', 'Order card', 'Карточка заказа'),
		array('287', 'Order card', 'Карточка заказа'),
		array('291', 'Carts', 'Корзины'),
		array('293', 'Carts', 'Корзины'),
		array('631', 'Edit order item', 'Редактирование позиции'),
		array('633', 'Edit order item', 'Редактирование позиции'),
		array('635', 'Add order item', 'Добавление позиции'),
		array('637', 'Add order item', 'Добавление позиции'),
	);
	foreach ($langPairs as $pair) {
		epc_oc_lang($pdo, $pair[0], $pair[1], $pair[2]);
	}
	$report['changes'][] = 'lang strings ensured';
}

$seenUrls = array();
try {
	foreach ($canonicalRoutes as $route) {
		$result = epc_oc_register_route(
			$pdo,
			$backend,
			$route[0],
			$route[1],
			$route[2],
			$route[3],
			$route[4],
			$route[5],
			$route[6],
			$apply
		);
		$result['source'] = 'canonical';
		$report['routes'][] = $result;
		$seenUrls[$route[1]] = true;
		if ($result['changed']) {
			$report['changes'][] = 'registered ' . $route[1];
		}
	}
} catch (Throwable $e) {
	$report['ok'] = false;
	$report['error'] = $e->getMessage();
}

$dbRoutes = $pdo->query(
	"SELECT `id`, `url`, `content`, `published_flag`, `content_type`
	 FROM `content`
	 WHERE `is_frontend` = 0
	   AND `content_type` = 'php'
	   AND (`url` LIKE 'shop/orders%' OR `content` LIKE '%/order_process/%' OR `content` LIKE '%/sao/states_statuses_link%')
	 ORDER BY `url`"
)->fetchAll(PDO::FETCH_ASSOC);

foreach ($dbRoutes as $row) {
	$url = (string) $row['url'];
	if (isset($seenUrls[$url])) {
		continue;
	}
	$audit = epc_oc_audit_db_route($pdo, $backend, $row);
	$report['db_extra_routes'][] = $audit;
}

foreach (array_merge($report['routes'], $report['db_extra_routes']) as $route) {
	if (!$route['file_exists']) {
		$report['missing_files'][] = array(
			'url' => $route['url'],
			'resolved_path' => $route['resolved_path'],
			'expected_path' => $route['expected_path'] ?? $route['db_content_path'] ?? null,
		);
		$report['ok'] = false;
	}
	if ($route['published_flag'] !== null && !(int) $route['published_flag']) {
		$report['ok'] = false;
	}
}

$base = rtrim((string) $cfg->domain_path, '/');
$report['urls'] = array(
	'statuses' => $base . '/' . $backend . '/shop/orders/statuses',
	'orders' => $base . '/' . $backend . '/shop/orders/orders',
	'items' => $base . '/' . $backend . '/shop/orders/items',
	'order' => $base . '/' . $backend . '/shop/orders/order',
	'carts' => $base . '/' . $backend . '/shop/orders/carts',
);
$report['hint'] = $apply
	? 'Applied. Hard-refresh CP and open /cp/shop/orders/statuses while logged in.'
	: 'Dry run — add apply=1 to register content rows and sync access.';

if ($apply && function_exists('opcache_reset')) {
	@opcache_reset();
	$report['changes'][] = 'opcache_reset';
}

echo json_encode($report, JSON_UNESCAPED_UNICODE | JSON_PRETTY_PRINT);
