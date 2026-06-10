<?php
/**
 * Register Logistics CP pages, carriers hub, guide, obtaining mode, menu.
 * Run: https://www.epartscart.com/epc-logistics-setup.php?token=epartscart-deploy-2026
 * With sample: &sample=1
 */
error_reporting(E_ALL);
ini_set('display_errors', 1);
header('Content-Type: application/json; charset=utf-8');

if (($_GET['token'] ?? '') !== 'epartscart-deploy-2026') {
	http_response_code(403);
	exit(json_encode(array('status' => false, 'message' => 'Forbidden')));
}

define('_ASTEXE_', 1);
require_once __DIR__ . '/config.php';
require_once __DIR__ . '/content/shop/channels/epc_channel_schema.php';
require_once __DIR__ . '/content/shop/logistics/epc_logistics_helpers.php';
require_once __DIR__ . '/epc_cp_mainstream_menu.php';

$cfg = new DP_Config();
$pdo = new PDO('mysql:host=' . $cfg->host . ';dbname=' . $cfg->db . ';charset=utf8', $cfg->user, $cfg->password);
$pdo->setAttribute(PDO::ATTR_ERRMODE, PDO::ERRMODE_EXCEPTION);

function epc_log_lang($pdo, $key, $en, $ru)
{
	$pdo->prepare('INSERT IGNORE INTO `lang_text_strings` (`str_key`, `description`, `same`, `is_error`, `is_custom`, `used_found`) VALUES (?, ?, NULL, 0, 1, 1)')->execute(array($key, $en));
	$pdo->prepare('INSERT INTO `lang_text_strings_translation` (`str_key`, `lang_code`, `value`) VALUES (?, ?, ?) ON DUPLICATE KEY UPDATE `value` = VALUES(`value`)')->execute(array($key, 'en', $en));
	$pdo->prepare('INSERT INTO `lang_text_strings_translation` (`str_key`, `lang_code`, `value`) VALUES (?, ?, ?) ON DUPLICATE KEY UPDATE `value` = VALUES(`value`)')->execute(array($key, 'ru', $ru));
}

epc_log_lang($pdo, 'epc_channels_cp', 'Channels', 'Каналы');
epc_log_lang($pdo, 'epc_logistics_cp', 'Logistics hub', 'Логистика — обзор');
epc_log_lang($pdo, 'epc_logistics_carriers_cp', 'Carriers & shipments', 'Перевозчики и отправки');
epc_log_lang($pdo, 'epc_logistics_guide_cp', 'Logistics guide', 'Гид по логистике');
epc_log_lang($pdo, 'epc_obtain_epc_carriers', 'DHL / FedEx / Aramex / UPS', 'DHL / FedEx / Aramex / UPS');

epc_channel_ensure_schema($pdo);
epc_logistics_seed_defaults($pdo);

function epc_log_register_content(PDO $pdo, $parentUrl, $url, $alias, $valueKey, $phpPath, $title, $order = 85)
{
	$parent = $pdo->prepare('SELECT `id`, `level` FROM `content` WHERE `url` = ? AND `is_frontend` = 0 LIMIT 1');
	$parent->execute(array($parentUrl));
	$parentRow = $parent->fetch(PDO::FETCH_ASSOC);
	if (!$parentRow) {
		throw new Exception('Parent not found: ' . $parentUrl);
	}
	$parentId = (int)$parentRow['id'];
	$level = (int)$parentRow['level'] + 1;
	$now = time();
	$existing = $pdo->prepare('SELECT `id` FROM `content` WHERE `url` = ? AND `is_frontend` = 0 LIMIT 1');
	$existing->execute(array($url));
	$contentId = (int)$existing->fetchColumn();
	if ($contentId > 0) {
		$pdo->prepare(
			'UPDATE `content` SET `published_flag` = 1, `content_type` = \'php\', `content` = ?, `title_tag` = ?, `parent` = ?, `level` = ?, `alias` = ?, `value` = ?, `time_edited` = ? WHERE `id` = ?'
		)->execute(array($phpPath, $title, $parentId, $level, $alias, $valueKey, $now, $contentId));
	} else {
		$pdo->prepare(
			'INSERT INTO `content`
			(`count`, `url`, `level`, `alias`, `value`, `parent`, `description`, `is_frontend`, `content_type`, `content`,
			 `title_tag`, `description_tag`, `keywords_tag`, `author_tag`, `main_flag`, `modules_array`, `css_js`, `robots_tag`,
			 `system_flag`, `published_flag`, `open`, `time_created`, `time_edited`, `order`)
			 VALUES (0, ?, ?, ?, ?, ?, ?, 0, \'php\', ?, ?, \'0\', \'0\', \'0\', 0, \'[]\', \'\', \'\', 0, 1, 0, ?, ?, ?)'
		)->execute(array($url, $level, $alias, $valueKey, $parentId, $title, $phpPath, $title, $now, $now, $order));
		$contentId = (int)$pdo->lastInsertId();
	}
	$pdo->prepare('DELETE FROM `content_access` WHERE `content_id` = ?')->execute(array($contentId));
	$st = $pdo->query('SELECT `id` FROM `groups` WHERE `for_backend` = 1 LIMIT 1');
	$root = (int)$st->fetchColumn();
	$groups = array($root > 0 ? $root : 1);
	$collect = function ($pid) use ($pdo, &$collect, &$groups) {
		$ch = $pdo->prepare('SELECT `id` FROM `groups` WHERE `parent` = ?');
		$ch->execute(array($pid));
		while ($r = $ch->fetch(PDO::FETCH_ASSOC)) {
			$groups[] = (int)$r['id'];
			$collect((int)$r['id']);
		}
	};
	if ($root > 0) {
		$collect($root);
	}
	$ins = $pdo->prepare('INSERT IGNORE INTO `content_access` (`content_id`, `group_id`) VALUES (?, ?)');
	foreach (array_unique($groups) as $gid) {
		$ins->execute(array($contentId, (int)$gid));
	}
	return $contentId;
}

// Logistics dashboard (shop/logistics)
$logisticsDashboardId = (int)$pdo->query("SELECT `id` FROM `content` WHERE `url` = 'shop/logistics' AND `is_frontend` = 0 LIMIT 1")->fetchColumn();
if ($logisticsDashboardId > 0) {
	$pdo->prepare("UPDATE `content` SET `content_type` = 'php', `content` = ?, `published_flag` = 1 WHERE `id` = ?")
		->execute(array('/<backend_dir>/content/shop/logistics/logistics.php', $logisticsDashboardId));
}

$carriersContentId = epc_log_register_content(
	$pdo,
	'shop/logistics',
	'shop/logistics/carriers',
	'carriers',
	'epc_logistics_carriers_cp',
	'/<backend_dir>/content/shop/logistics/logistics_carriers_page.php',
	'Carriers & shipments',
	2
);

$guideContentId = epc_log_register_content(
	$pdo,
	'shop/logistics',
	'shop/logistics/guide',
	'guide',
	'epc_logistics_guide_cp',
	'/<backend_dir>/content/shop/logistics/logistics_guide_page.php',
	'Logistics guide',
	3
);

$params = json_encode(array(
	array('key' => 'demo_mode', 'caption' => 'Demo mode', 'type' => 'checkbox'),
	array('key' => 'origin_city', 'caption' => 'Origin city', 'type' => 'text'),
));
$paramsVal = json_encode(array('demo_mode' => 1, 'origin_city' => 'Dubai'));
$obtainId = (int)$pdo->query("SELECT `id` FROM `shop_obtaining_modes` WHERE `handler` = 'epc_carriers' LIMIT 1")->fetchColumn();
$maxOrder = (int)$pdo->query('SELECT IFNULL(MAX(`order`), 0) FROM `shop_obtaining_modes`')->fetchColumn();
if ($obtainId <= 0) {
	$pdo->prepare(
		'INSERT INTO `shop_obtaining_modes` (`caption`, `order`, `available`, `handler`, `parameters`, `parameters_values`)
		 VALUES (?, ?, 1, ?, ?, ?)'
	)->execute(array('epc_obtain_epc_carriers', $maxOrder + 1, 'epc_carriers', $params, $paramsVal));
	$obtainId = (int)$pdo->lastInsertId();
} else {
	$pdo->prepare('UPDATE `shop_obtaining_modes` SET `available` = 1, `parameters` = ?, `parameters_values` = ? WHERE `id` = ?')
		->execute(array($params, $paramsVal, $obtainId));
}

$menu = epc_cp_mainstream_menu_apply($pdo);

$sample = null;
if (!empty($_GET['sample']) && $_GET['sample'] !== '0') {
	epc_logistics_seed_sample_data($pdo);
	$sample = epc_logistics_demo_report($pdo);
}

$base = rtrim($cfg->domain_path, '/');
echo json_encode(array(
	'status' => true,
	'message' => 'Logistics module registered (separate from Channels)',
	'carriers_content_id' => $carriersContentId,
	'guide_content_id' => $guideContentId,
	'obtaining_mode_id' => $obtainId,
	'menu' => array(
		'logistics_group' => $menu['logistics_group'],
		'channels_group' => $menu['channels_group'],
	),
	'urls' => array(
		'cp_logistics' => $base . '/' . $cfg->backend_dir . '/shop/logistics',
		'cp_carriers' => $base . '/' . $cfg->backend_dir . '/shop/logistics/carriers',
		'cp_guide' => $base . '/' . $cfg->backend_dir . '/shop/logistics/guide',
		'demo_json' => $base . '/epc-logistics-demo.php?token=epartscart-deploy-2026',
	),
	'sample' => $sample,
), JSON_PRETTY_PRINT | JSON_UNESCAPED_UNICODE);
