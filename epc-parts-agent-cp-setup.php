<?php
/**
 * Register CP page shop/parts_agent_chats (AI Parts Expert chat review).
 * Run once: https://www.epartscart.com/epc-parts-agent-cp-setup.php?token=epartscart-deploy-2026
 */
error_reporting(E_ALL);
ini_set('display_errors', 1);
header('Content-Type: text/plain; charset=utf-8');

if (!isset($_GET['token']) || $_GET['token'] !== 'epartscart-deploy-2026') {
	exit("Forbidden\n");
}

define('_ASTEXE_', 1);
require_once __DIR__ . '/config.php';

$cfg = new DP_Config();
$pdo = new PDO('mysql:host=' . $cfg->host . ';dbname=' . $cfg->db . ';charset=utf8', $cfg->user, $cfg->password);
$pdo->setAttribute(PDO::ATTR_ERRMODE, PDO::ERRMODE_EXCEPTION);

function epc_pa_lang($pdo, $key, $en, $ru)
{
	$pdo->prepare('INSERT IGNORE INTO `lang_text_strings` (`str_key`, `description`, `same`, `is_error`, `is_custom`, `used_found`) VALUES (?, ?, NULL, 0, 1, 1)')->execute(array($key, $en));
	$pdo->prepare('INSERT INTO `lang_text_strings_translation` (`str_key`, `lang_code`, `value`) VALUES (?, ?, ?) ON DUPLICATE KEY UPDATE `value` = VALUES(`value`)')->execute(array($key, 'en', $en));
	$pdo->prepare('INSERT INTO `lang_text_strings_translation` (`str_key`, `lang_code`, `value`) VALUES (?, ?, ?) ON DUPLICATE KEY UPDATE `value` = VALUES(`value`)')->execute(array($key, 'ru', $ru));
}

epc_pa_lang($pdo, 'epc_parts_agent_chats_cp', 'AI agent chats', 'Чаты AI агента');
epc_pa_lang($pdo, '4756', 'Page script missing', 'Скрипт страницы отсутствует');

$parentUrl = 'shop';
$parent = $pdo->prepare('SELECT `id`, `level` FROM `content` WHERE `url` = ? AND `is_frontend` = 0 LIMIT 1');
$parent->execute(array($parentUrl));
$parentRow = $parent->fetch(PDO::FETCH_ASSOC);
if (!$parentRow) {
	exit("Parent content shop not found\n");
}
$parentId = (int)$parentRow['id'];
$level = (int)$parentRow['level'] + 1;

$url = 'shop/parts_agent_chats';
$phpPath = '/<backend_dir>/content/shop/parts_agent/parts_agent_chats.php';
$now = time();

$existing = $pdo->prepare('SELECT `id` FROM `content` WHERE `url` = ? AND `is_frontend` = 0 LIMIT 1');
$existing->execute(array($url));
$contentId = (int)$existing->fetchColumn();

if ($contentId > 0) {
	$pdo->prepare(
		'UPDATE `content` SET `published_flag` = 1, `content_type` = \'php\', `is_frontend` = 0, `system_flag` = 0,
		 `content` = ?, `title_tag` = ?, `parent` = ?, `level` = ?, `alias` = \'parts_agent_chats\', `url` = ?, `value` = ?
		 WHERE `id` = ?'
	)->execute(array(
		$phpPath,
		'AI Parts Expert chats — eParts Cart',
		$parentId,
		$level,
		$url,
		'epc_parts_agent_chats_cp',
		$contentId,
	));
} else {
	$pdo->prepare(
		'INSERT INTO `content`
		(`count`, `url`, `level`, `alias`, `value`, `parent`, `description`, `is_frontend`, `content_type`, `content`,
		 `title_tag`, `description_tag`, `keywords_tag`, `author_tag`, `main_flag`, `modules_array`, `css_js`, `robots_tag`,
		 `system_flag`, `published_flag`, `open`, `time_created`, `time_edited`, `order`)
		 VALUES (0, ?, ?, ?, ?, ?, ?, 0, \'php\', ?, ?, \'0\', \'0\', \'0\', 0, \'[]\', \'\', \'\', 0, 1, 0, ?, ?, 86)'
	)->execute(array(
		$url,
		$level,
		'parts_agent_chats',
		'epc_parts_agent_chats_cp',
		$parentId,
		'Review AI Parts Expert storefront chat sessions',
		$phpPath,
		'AI Parts Expert chats — eParts Cart',
		$now,
		$now,
	));
	$contentId = (int)$pdo->lastInsertId();
}

function epc_pa_backend_group_ids(PDO $pdo)
{
	$ids = array();
	$st = $pdo->query('SELECT `id` FROM `groups` WHERE `for_backend` = 1 LIMIT 1');
	$root = $st->fetch(PDO::FETCH_ASSOC);
	if (!$root) {
		return array(1, 3);
	}
	$collect = function ($parentId) use ($pdo, &$collect, &$ids) {
		$ids[(int)$parentId] = true;
		$ch = $pdo->prepare('SELECT `id`, `count` FROM `groups` WHERE `parent` = ?');
		$ch->execute(array($parentId));
		while ($row = $ch->fetch(PDO::FETCH_ASSOC)) {
			$ids[(int)$row['id']] = true;
			if ((int)$row['count'] > 0) {
				$collect((int)$row['id']);
			}
		}
	};
	$collect((int)$root['id']);
	return array_keys($ids);
}

$refGroups = array();
foreach (array('shop/crosses', 'shop/demand_countries', 'shop/orders/orders', 'shop') as $refUrl) {
	$ref = $pdo->prepare(
		'SELECT DISTINCT ca.`group_id` FROM `content_access` ca
		 INNER JOIN `content` c ON c.`id` = ca.`content_id`
		 WHERE c.`url` = ? AND c.`is_frontend` = 0'
	);
	$ref->execute(array($refUrl));
	while ($gid = $ref->fetchColumn()) {
		$refGroups[(int)$gid] = true;
	}
}
foreach (epc_pa_backend_group_ids($pdo) as $gid) {
	$refGroups[(int)$gid] = true;
}
if ($contentId > 0 && $refGroups) {
	$pdo->prepare('DELETE FROM `content_access` WHERE `content_id` = ?')->execute(array($contentId));
	$ins = $pdo->prepare('INSERT IGNORE INTO `content_access` (`content_id`, `group_id`) VALUES (?, ?)');
	foreach (array_keys($refGroups) as $gid) {
		$ins->execute(array($contentId, (int)$gid));
	}
}

require_once __DIR__ . '/content/shop/docpart/epc_parts_agent.php';
require_once __DIR__ . '/epc_cp_mainstream_menu.php';
epc_agent_ensure_db_schema($pdo);
epc_agent_config_ensure_schema($pdo);
$synced = epc_agent_cp_sync_file_sessions($pdo, 200);

$menu = epc_cp_mainstream_menu_apply($pdo);
$controlId = (int)$menu['items']['ai_hub'];

echo "AI Parts Expert chat review CP page registered.\n";
echo "content_id: {$contentId}\n";
echo "control_items_id: {$controlId}\n";
echo "CP menu: AI Agent group {$menu['ai_group']}\n";
echo "URL: /" . $cfg->backend_dir . "/{$url}\n";
echo "DB schema ensured. Synced {$synced} temp session file(s).\n";
echo "content_access groups: " . count($refGroups) . "\n";
