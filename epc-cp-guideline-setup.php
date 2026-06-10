<?php
/**
 * Register / repair CP page control/guideline (Complete Control Panel guideline).
 * Note: URL must NOT contain "cp" — backend_dir is "cp" and str_replace strips it from routes.
 * Run: https://www.epartscart.com/epc-cp-guideline-setup.php?token=epartscart-deploy-2026
 * Diagnose only: add &diag=1
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

$url = 'control/cp-guideline';
$legacyUrl = 'control/guideline';
$phpPath = '/<backend_dir>/content/control/cp_guideline.php';
$now = time();

function epc_cpg_lang($pdo, $key, $en, $ru)
{
	$pdo->prepare('INSERT IGNORE INTO `lang_text_strings` (`str_key`, `description`, `same`, `is_error`, `is_custom`, `used_found`) VALUES (?, ?, NULL, 0, 1, 1)')->execute(array($key, $en));
	$pdo->prepare('INSERT INTO `lang_text_strings_translation` (`str_key`, `lang_code`, `value`) VALUES (?, ?, ?) ON DUPLICATE KEY UPDATE `value` = VALUES(`value`)')->execute(array($key, 'en', $en));
	$pdo->prepare('INSERT INTO `lang_text_strings_translation` (`str_key`, `lang_code`, `value`) VALUES (?, ?, ?) ON DUPLICATE KEY UPDATE `value` = VALUES(`value`)')->execute(array($key, 'ru', $ru));
}

function epc_cpg_backend_group_ids(PDO $pdo)
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

function epc_cpg_access_groups(PDO $pdo, array $refUrls)
{
	$groups = array();
	foreach ($refUrls as $refUrl) {
		$st = $pdo->prepare(
			'SELECT DISTINCT ca.`group_id` FROM `content_access` ca
			 INNER JOIN `content` c ON c.`id` = ca.`content_id`
			 WHERE c.`url` = ? AND c.`is_frontend` = 0'
		);
		$st->execute(array($refUrl));
		while ($gid = $st->fetchColumn()) {
			$groups[(int)$gid] = true;
		}
	}
	foreach (epc_cpg_backend_group_ids($pdo) as $gid) {
		$groups[(int)$gid] = true;
	}
	if (!$groups) {
		$groups[1] = true;
	}
	return array_keys($groups);
}

if (!empty($_GET['diag'])) {
	$rows = $pdo->prepare('SELECT `id`, `url`, `parent`, `level`, `published_flag`, `is_frontend`, `content_type`, `content`, `system_flag` FROM `content` WHERE `url` = ?');
	$rows->execute(array($url));
	$all = $rows->fetchAll(PDO::FETCH_ASSOC);
	echo "Rows for url {$url}:\n";
	print_r($all);
	$match = $pdo->prepare('SELECT `id` FROM `content` WHERE `url` = ? AND `published_flag` = 1 AND `is_frontend` = 0 LIMIT 1');
	$match->execute(array($url));
	echo "Route match id: " . (int)$match->fetchColumn() . "\n";
	$config = $pdo->query("SELECT `id`, `url`, `parent`, `level`, `published_flag`, `system_flag` FROM `content` WHERE `url` = 'control/config' AND `is_frontend` = 0 LIMIT 1")->fetch(PDO::FETCH_ASSOC);
	echo "control/config:\n";
	print_r($config);
	$control = $pdo->query("SELECT `id`, `url`, `parent`, `level` FROM `content` WHERE `url` = 'control' AND `is_frontend` = 0 LIMIT 1")->fetch(PDO::FETCH_ASSOC);
	echo "control root:\n";
	print_r($control);
	$cid = (int)$pdo->query("SELECT `id` FROM `content` WHERE `url` = '{$url}' AND `is_frontend` = 0 LIMIT 1")->fetchColumn();
	if ($cid > 0) {
		$acc = $pdo->query("SELECT `group_id` FROM `content_access` WHERE `content_id` = {$cid}")->fetchAll(PDO::FETCH_COLUMN);
		echo "content_access for {$cid}: " . implode(',', $acc) . "\n";
	}
	foreach (array('control/config', 'shop/price-management', 'control/notifications_settings') as $refUrl) {
		$st = $pdo->prepare('SELECT `id`, `url`, `parent`, `level`, `published_flag`, `system_flag`, `open`, `order`, `modules_array` FROM `content` WHERE `url` = ? AND `is_frontend` = 0 LIMIT 1');
		$st->execute(array($refUrl));
		$row = $st->fetch(PDO::FETCH_ASSOC);
		echo "{$refUrl}:\n";
		print_r($row);
		$rid = (int)($row['id'] ?? 0);
		if ($rid > 0) {
			$acc = $pdo->query("SELECT `group_id` FROM `content_access` WHERE `content_id` = {$rid}")->fetchAll(PDO::FETCH_COLUMN);
			echo "  access=" . implode(',', $acc) . "\n";
		}
	}
	require_once $_SERVER['DOCUMENT_ROOT'] . '/core/dp_helper.php';
	$_SERVER['REQUEST_URI'] = '/' . $cfg->backend_dir . '/' . $url;
	$_SERVER['SERVER_NAME'] = 'www.epartscart.com';
	$_SERVER['SERVER_PORT'] = '';
	$simRoute = urldecode(getPageRoute());
	$backend_prefix = (string)$cfg->backend_dir;
	if ($backend_prefix !== '' && strpos($simRoute, $backend_prefix . '/') === 0) {
		$simRoute = substr($simRoute, strlen($backend_prefix) + 1);
	} elseif ($backend_prefix !== '' && strpos($simRoute, '/' . $backend_prefix . '/') === 0) {
		$simRoute = substr($simRoute, strlen($backend_prefix) + 2);
	}
	if (!empty($simRoute) && $simRoute[0] === '/') {
		$simRoute = substr($simRoute, 1);
	}
	echo "simulated url_route=[{$simRoute}]\n";
	$match2 = $pdo->prepare('SELECT `id` FROM `content` WHERE `url` = ? AND `published_flag` = 1 AND `is_frontend` = 0 LIMIT 1');
	$match2->execute(array($simRoute));
	echo "lookup by simulated route: " . (int)$match2->fetchColumn() . "\n";
	$menu = $pdo->query("SELECT `id`, `url`, `caption`, `items_group` FROM `control_items` WHERE `url` LIKE '%guideline%' OR `caption` LIKE '%cp_guideline%'")->fetchAll(PDO::FETCH_ASSOC);
	echo "menu items:\n";
	print_r($menu);
	exit;
	echo "File exists: " . (is_file($_SERVER['DOCUMENT_ROOT'] . '/' . $cfg->backend_dir . '/content/control/cp_guideline.php') ? 'yes' : 'no') . "\n";
	exit;
}

epc_cpg_lang($pdo, 'epc_cp_guideline', 'CP guideline', 'Руководство CP');
epc_cpg_lang($pdo, 'epc_cp_guideline_desc', 'Complete control panel map and workflows', 'Полное руководство по панели управления');

$controlParent = $pdo->prepare('SELECT `id`, `level` FROM `content` WHERE `url` = ? AND `is_frontend` = 0 LIMIT 1');
$controlParent->execute(array('control'));
$controlRow = $controlParent->fetch(PDO::FETCH_ASSOC);
if (!$controlRow) {
	$configRef = $pdo->prepare('SELECT `id`, `level`, `parent` FROM `content` WHERE `url` = ? AND `is_frontend` = 0 LIMIT 1');
	$configRef->execute(array('control/config'));
	$configRow = $configRef->fetch(PDO::FETCH_ASSOC);
	if (!$configRow) {
		exit("Parent content control not found\n");
	}
	$parentId = (int)$configRow['parent'];
	$level = (int)$configRow['level'];
} else {
	$parentId = (int)$controlRow['id'];
	$level = (int)$controlRow['level'] + 1;
}

$existing = $pdo->prepare('SELECT `id` FROM `content` WHERE `url` = ? AND `is_frontend` = 0 LIMIT 1');
$existing->execute(array($url));
$contentId = (int)$existing->fetchColumn();
if ($contentId <= 0) {
	$legacy = $pdo->prepare('SELECT `id` FROM `content` WHERE `url` = ? AND `is_frontend` = 0 LIMIT 1');
	$legacy->execute(array($legacyUrl));
	$contentId = (int)$legacy->fetchColumn();
}

if ($contentId > 0) {
	$pdo->prepare(
		'UPDATE `content` SET `published_flag` = 1, `content_type` = \'php\', `is_frontend` = 0, `system_flag` = 1,
		 `content` = ?, `title_tag` = ?, `description` = ?, `parent` = ?, `level` = ?, `alias` = \'guideline\', `url` = ?, `value` = ?
		 WHERE `id` = ?'
	)->execute(array(
		$phpPath,
		'epc_cp_guideline',
		'epc_cp_guideline_desc',
		$parentId,
		$level,
		$url,
		'epc_cp_guideline',
		$contentId,
	));
} else {
	$pdo->prepare(
		'INSERT INTO `content`
		(`count`, `url`, `level`, `alias`, `value`, `parent`, `description`, `is_frontend`, `content_type`, `content`,
		 `title_tag`, `description_tag`, `keywords_tag`, `author_tag`, `main_flag`, `modules_array`, `css_js`, `robots_tag`,
		 `system_flag`, `published_flag`, `open`, `time_created`, `time_edited`, `order`)
		 VALUES (0, ?, ?, ?, ?, ?, ?, 0, \'php\', ?, ?, \'0\', \'0\', \'0\', 0, \'[]\', \'\', \'\', 1, 1, 0, ?, ?, 2)'
	)->execute(array(
		$url,
		$level,
		'guideline',
		'epc_cp_guideline',
		$parentId,
		'epc_cp_guideline_desc',
		$phpPath,
		'epc_cp_guideline',
		$now,
		$now,
	));
	$contentId = (int)$pdo->lastInsertId();
}

$accessGroups = epc_cpg_access_groups($pdo, array('control/config', 'control', 'shop/orders/orders'));
$pdo->prepare('DELETE FROM `content_access` WHERE `content_id` = ?')->execute(array($contentId));
$ins = $pdo->prepare('INSERT IGNORE INTO `content_access` (`content_id`, `group_id`) VALUES (?, ?)');
foreach ($accessGroups as $gid) {
	$ins->execute(array($contentId, (int)$gid));
}

$systemGroup = (int)$pdo->query('SELECT `id` FROM `control_groups` ORDER BY `order` ASC LIMIT 1')->fetchColumn();
if ($systemGroup <= 0) {
	$systemGroup = 1;
}
$menuOrder = 1;
$configRef = $pdo->prepare('SELECT `order` FROM `control_items` WHERE `url` LIKE ? LIMIT 1');
$configRef->execute(array('%/control/config%'));
$configOrder = $configRef->fetchColumn();
if ($configOrder !== false) {
	$menuOrder = max(1, (int)$configOrder - 1);
}

$menuUrl = '/<backend>/control/cp-guideline';
$legacyMenuUrl = '/<backend>/control/guideline';
$pdo->prepare('DELETE FROM `content` WHERE `url` = ? AND `is_frontend` = 0 AND `id` != ?')->execute(array($legacyUrl, $contentId));
$pdo->prepare('UPDATE `control_items` SET `url` = ? WHERE `url` = ?')->execute(array($menuUrl, $legacyMenuUrl));
$menuCheck = $pdo->prepare('SELECT `id` FROM `control_items` WHERE `url` = ? LIMIT 1');
$menuCheck->execute(array($menuUrl));
$controlId = (int)$menuCheck->fetchColumn();

if ($controlId <= 0) {
	$legacyMenu = $pdo->prepare('SELECT `id` FROM `control_items` WHERE `url` = ? LIMIT 1');
	$legacyMenu->execute(array($legacyMenuUrl));
	$controlId = (int)$legacyMenu->fetchColumn();
}

if ($controlId <= 0) {
	$pdo->prepare(
		'INSERT INTO `control_items` (`items_group`, `caption`, `url`, `img`, `order`, `background_color`, `fontawesome_class`, `target`, `show_anyway`)
		 VALUES (?, ?, ?, ?, ?, ?, ?, ?, 1)'
	)->execute(array($systemGroup, 'epc_cp_guideline', $menuUrl, '', $menuOrder, '#2563eb', 'fas fa-book', ''));
	$controlId = (int)$pdo->lastInsertId();
	echo "control_items created: {$controlId}\n";
} else {
	$pdo->prepare(
		'UPDATE `control_items` SET `items_group` = ?, `caption` = ?, `url` = ?, `order` = ?, `background_color` = ?, `fontawesome_class` = ?, `show_anyway` = 1 WHERE `id` = ?'
	)->execute(array($systemGroup, 'epc_cp_guideline', $menuUrl, $menuOrder, '#2563eb', 'fas fa-book', $controlId));
	echo "control_items updated: {$controlId}\n";
}

$match = $pdo->prepare('SELECT `id`, `parent`, `level`, `published_flag`, `system_flag` FROM `content` WHERE `url` = ? AND `published_flag` = 1 AND `is_frontend` = 0 LIMIT 1');
$match->execute(array($url));
$row = $match->fetch(PDO::FETCH_ASSOC);

echo "content_id: {$contentId}\n";
echo "parent_id: {$parentId}, level: {$level}\n";
echo "route_match: " . json_encode($row) . "\n";
echo "access_groups: " . implode(',', $accessGroups) . "\n";
echo "CP URL: /cp/control/cp-guideline\n";
echo "Menu group (SYSTEM): {$systemGroup}\n";
echo "Done.\n";
