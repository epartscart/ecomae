<?php
/**
 * Register CP: Customers → Customer management (top-level panel).
 * https://www.epartscart.com/epc-customer-mgmt-cp-setup.php?token=epartscart-deploy-2026
 */
error_reporting(E_ALL);
ini_set('display_errors', 1);
header('Content-Type: application/json; charset=utf-8');

require_once __DIR__ . '/epc_deploy_auth.php';
epc_deploy_require_token();

if (!defined('_ASTEXE_')) {
	define('_ASTEXE_', 1);
}
require_once __DIR__ . '/config.php';
require_once __DIR__ . '/content/shop/finance/epc_einvoice_schema.php';
require_once __DIR__ . '/epc_cp_mainstream_menu.php';

$cfg = new DP_Config();
$pdo = new PDO('mysql:host=' . $cfg->host . ';dbname=' . $cfg->db . ';charset=utf8', $cfg->user, $cfg->password);
$pdo->setAttribute(PDO::ATTR_ERRMODE, PDO::ERRMODE_EXCEPTION);

function epc_cm_lang($pdo, $key, $en, $ru)
{
	$pdo->prepare('INSERT IGNORE INTO `lang_text_strings` (`str_key`, `description`, `same`, `is_error`, `is_custom`, `used_found`) VALUES (?, ?, NULL, 0, 1, 1)')->execute(array($key, $en));
	$pdo->prepare('INSERT INTO `lang_text_strings_translation` (`str_key`, `lang_code`, `value`) VALUES (?, ?, ?) ON DUPLICATE KEY UPDATE `value` = VALUES(`value`)')->execute(array($key, 'en', $en));
	$pdo->prepare('INSERT INTO `lang_text_strings_translation` (`str_key`, `lang_code`, `value`) VALUES (?, ?, ?) ON DUPLICATE KEY UPDATE `value` = VALUES(`value`)')->execute(array($key, 'ru', $ru));
}

epc_cm_lang($pdo, 'epc_customer_mgmt_cp', 'Customer management', 'Управление клиентами');
epc_cm_lang($pdo, 'epc_cp_group_customers', 'Customers', 'Клиенты');

epc_einvoice_ensure_schema($pdo);

function epc_cm_register_content(PDO $pdo, $parentUrl, $url, $alias, $valueKey, $phpPath, $title, $order = 87)
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

$hubId = epc_cm_register_content(
	$pdo,
	'shop',
	'shop/customer_mgmt',
	'customer_mgmt_hub',
	'epc_cp_group_customers',
	'/<backend_dir>/content/shop/customer_mgmt/customer_mgmt_hub_page.php',
	'Customers',
	86
);

$contentId = epc_cm_register_content(
	$pdo,
	'shop/customer_mgmt',
	'shop/customer_mgmt/customer_mgmt',
	'customer_mgmt',
	'epc_customer_mgmt_cp',
	'/<backend_dir>/content/shop/customer_mgmt/customer_mgmt_main_page.php',
	'Customer management',
	87
);

$pdo->prepare('UPDATE `content` SET `published_flag` = 0 WHERE `url` = ? AND `is_frontend` = 0')
	->execute(array('users/customer_mgmt'));

$legacy = $pdo->prepare('SELECT `id` FROM `content` WHERE `url` = ? AND `is_frontend` = 0 LIMIT 1');
$legacy->execute(array('users/customer_mgmt'));
$legacyId = (int)$legacy->fetchColumn();
if ($legacyId > 0) {
	$pdo->prepare(
		'UPDATE `content` SET `published_flag` = 1, `content` = ?, `title_tag` = ?, `description` = ? WHERE `id` = ?'
	)->execute(array(
		'/<backend_dir>/content/users/customer_mgmt_page.php',
		'Customer management (redirect)',
		'Legacy redirect to Customers panel',
		$legacyId,
	));
}

$menu = epc_cp_customer_mgmt_menu_apply($pdo);
$base = rtrim($cfg->domain_path, '/');

echo json_encode(array(
	'status' => true,
	'message' => 'Customer management registered as top-level Customers panel',
	'hub_content_id' => $hubId,
	'content_id' => $contentId,
	'menu_group_id' => $menu['customers_group'],
	'menu_item_id' => $menu['customer_mgmt_item'],
	'urls' => array(
		'cp_hub' => $base . '/' . $cfg->backend_dir . '/shop/customer_mgmt/customer_mgmt',
		'legacy_redirect' => $base . '/' . $cfg->backend_dir . '/users/customer_mgmt',
		'setup_lockdown_safe' => $base . '/customer-mgmt-setup.php?token=REDACTED',
	),
), JSON_PRETTY_PRINT | JSON_UNESCAPED_UNICODE);
