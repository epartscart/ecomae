<?php
/**
 * Register Channels hub + carrier obtaining mode + schema.
 * Run: https://www.epartscart.com/epc-channels-setup.php?token=epartscart-deploy-2026
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
require_once __DIR__ . '/content/shop/channels/epc_channel_helpers.php';
require_once __DIR__ . '/epc_cp_mainstream_menu.php';

$cfg = new DP_Config();
$pdo = new PDO('mysql:host=' . $cfg->host . ';dbname=' . $cfg->db . ';charset=utf8', $cfg->user, $cfg->password);
$pdo->setAttribute(PDO::ATTR_ERRMODE, PDO::ERRMODE_EXCEPTION);

function epc_ch_lang($pdo, $key, $en, $ru)
{
	$pdo->prepare('INSERT IGNORE INTO `lang_text_strings` (`str_key`, `description`, `same`, `is_error`, `is_custom`, `used_found`) VALUES (?, ?, NULL, 0, 1, 1)')->execute(array($key, $en));
	$pdo->prepare('INSERT INTO `lang_text_strings_translation` (`str_key`, `lang_code`, `value`) VALUES (?, ?, ?) ON DUPLICATE KEY UPDATE `value` = VALUES(`value`)')->execute(array($key, 'en', $en));
	$pdo->prepare('INSERT INTO `lang_text_strings_translation` (`str_key`, `lang_code`, `value`) VALUES (?, ?, ?) ON DUPLICATE KEY UPDATE `value` = VALUES(`value`)')->execute(array($key, 'ru', $ru));
}

epc_ch_lang($pdo, 'epc_channels_cp', 'Channels', 'Каналы');
epc_ch_lang($pdo, 'epc_channels_guide_cp', 'Channels guide', 'Гид по каналам');

epc_channel_ensure_schema($pdo);
epc_channel_seed_defaults($pdo);

function epc_ch_register_content(PDO $pdo, $parentUrl, $url, $alias, $valueKey, $phpPath, $title, $order = 85)
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

$contentId = epc_ch_register_content(
	$pdo,
	'shop',
	'shop/channels/channels',
	'channels',
	'epc_channels_cp',
	'/<backend_dir>/content/shop/channels/channels_main_page.php',
	'Channels',
	86
);

$guideContentId = epc_ch_register_content(
	$pdo,
	'shop/channels/channels',
	'shop/channels/guide',
	'guide',
	'epc_channels_guide_cp',
	'/<backend_dir>/content/shop/channels/channels_guide_page.php',
	'Channels guide',
	1
);

$menu = epc_cp_mainstream_menu_apply($pdo);
$controlId = (int)$menu['items']['channels_hub'];

$sample = null;
if (!empty($_GET['sample']) && $_GET['sample'] !== '0') {
	epc_channel_seed_sample_data($pdo);
	$sample = epc_channel_demo_report($pdo);
}

$base = rtrim($cfg->domain_path, '/');
echo json_encode(array(
	'status' => true,
	'message' => 'Channels module registered (marketplace only — logistics is separate)',
	'content_id' => $contentId,
	'control_items_id' => $controlId,
	'menu_group_id' => $menu['channels_group'],
	'logistics_setup' => $base . '/epc-logistics-setup.php?token=epartscart-deploy-2026',
	'urls' => array(
		'cp_channels' => $base . '/' . $cfg->backend_dir . '/shop/channels/channels',
		'cp_guide' => $base . '/' . $cfg->backend_dir . '/shop/channels/guide',
		'demo_json' => $base . '/epc-channels-demo.php?token=epartscart-deploy-2026',
		'setup_sample' => $base . '/epc-channels-setup.php?token=epartscart-deploy-2026&sample=1',
	),
	'guide_content_id' => $guideContentId,
	'sample' => $sample,
), JSON_PRETTY_PRINT | JSON_UNESCAPED_UNICODE);
