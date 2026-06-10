<?php
/**
 * Register Payment gateways CP hub, seed UAE gateways, top-level menu.
 * Run: https://www.epartscart.com/epc-payments-setup.php?token=epartscart-deploy-2026
 * Reseed dummy keys: &reseed=1
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
require_once __DIR__ . '/content/shop/payments/epc_payment_helpers.php';
require_once __DIR__ . '/epc_cp_mainstream_menu.php';

$cfg = new DP_Config();
$pdo = new PDO('mysql:host=' . $cfg->host . ';dbname=' . $cfg->db . ';charset=utf8', $cfg->user, $cfg->password);
$pdo->setAttribute(PDO::ATTR_ERRMODE, PDO::ERRMODE_EXCEPTION);

function epc_pay_register_content(PDO $pdo, $parentUrl, $url, $alias, $valueKey, $phpPath, $title, $order = 88)
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
			'UPDATE `content` SET `published_flag` = 1, `content_type` = \'php\', `is_frontend` = 0, `system_flag` = 0,
			 `content` = ?, `title_tag` = ?, `parent` = ?, `level` = ?, `alias` = ?, `url` = ?, `value` = ?, `time_edited` = ?
			 WHERE `id` = ?'
		)->execute(array($phpPath, $title, $parentId, $level, $alias, $url, $valueKey, $now, $contentId));
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

	$ref = $pdo->prepare('SELECT `id` FROM `content` WHERE `url` = ? AND `is_frontend` = 0 LIMIT 1');
	$refIds = array();
	foreach (array('shop/orders/orders', 'shop/finance/erp', 'shop/channels/channels', 'shop/finance/account_operations', 'shop') as $refUrl) {
		$ref->execute(array($refUrl));
		$rid = (int)$ref->fetchColumn();
		if ($rid > 0) {
			$refIds[] = $rid;
		}
	}
	if ($contentId > 0 && !empty($refIds)) {
		$pdo->prepare('DELETE FROM `content_access` WHERE `content_id` = ?')->execute(array($contentId));
		$placeholders = implode(',', array_fill(0, count($refIds), '?'));
		$groups = $pdo->prepare(
			'SELECT DISTINCT `group_id` FROM `content_access` WHERE `content_id` IN (' . $placeholders . ')'
		);
		$groups->execute($refIds);
		$ins = $pdo->prepare('INSERT INTO `content_access` (`content_id`, `group_id`) VALUES (?, ?)');
		while ($g = $groups->fetch(PDO::FETCH_ASSOC)) {
			try {
				$ins->execute(array($contentId, (int)$g['group_id']));
			} catch (Exception $e) {
			}
		}
	}
	if ($contentId > 0 && (int)$pdo->query('SELECT COUNT(*) FROM `content_access` WHERE `content_id` = ' . (int)$contentId)->fetchColumn() === 0) {
		$rootG = (int)$pdo->query('SELECT `id` FROM `groups` WHERE `for_backend` = 1 LIMIT 1')->fetchColumn();
		$pdo->prepare('INSERT INTO `content_access` (`content_id`, `group_id`) VALUES (?, ?)')->execute(array($contentId, $rootG > 0 ? $rootG : 1));
	}
	return $contentId;
}

function epc_pay_sync_all_access(PDO $pdo)
{
	$ids = array();
	$st = $pdo->query('SELECT `id` FROM `groups` WHERE `for_backend` = 1 LIMIT 1');
	$root = $st->fetch(PDO::FETCH_ASSOC);
	if (!$root) {
		return array();
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
	$groupIds = array_keys($ids);
	$fixed = array();
	foreach (array('shop/payments/payments', 'shop/payments/payments/guide', 'shop/payments/guide') as $url) {
		$st = $pdo->prepare('SELECT `id` FROM `content` WHERE `url` = ? AND `is_frontend` = 0 LIMIT 1');
		$st->execute(array($url));
		$cid = (int)$st->fetchColumn();
		if ($cid <= 0) {
			continue;
		}
		$pdo->prepare('DELETE FROM `content_access` WHERE `content_id` = ?')->execute(array($cid));
		$ins = $pdo->prepare('INSERT IGNORE INTO `content_access` (`content_id`, `group_id`) VALUES (?, ?)');
		foreach ($groupIds as $gid) {
			$ins->execute(array($cid, (int)$gid));
		}
		$fixed[$url] = $cid;
	}
	$pdo->exec("UPDATE `control_items` SET `show_anyway` = 1 WHERE `url` LIKE '%/shop/payments/%'");
	return $fixed;
}

epc_payment_lang_seed($pdo);
$uaeIds = epc_payment_seed_uae_gateways($pdo);
epc_payment_enable_legacy($pdo);

$active = $pdo->query('SELECT `handler` FROM `shop_payment_systems` WHERE `active` = 1 LIMIT 1')->fetchColumn();
if (!$active) {
	epc_payment_set_active($pdo, 'stripe');
}

if (!empty($_GET['reseed']) && $_GET['reseed'] !== '0') {
	foreach (epc_payment_uae_gateway_defs() as $handler => $def) {
		$pdo->prepare('UPDATE `shop_payment_systems` SET `parameters_values` = ? WHERE `handler` = ?')
			->execute(array(json_encode($def['dummy']), $handler));
	}
	epc_payment_enable_legacy($pdo);
}

$contentId = epc_pay_register_content(
	$pdo,
	'shop',
	'shop/payments/payments',
	'payments',
	'epc_payments_cp',
	'/<backend_dir>/content/shop/payments/payments_main_page.php',
	'Payment gateways',
	88
);

$guideContentId = epc_pay_register_content(
	$pdo,
	'shop/payments/payments',
	'shop/payments/payments/guide',
	'guide',
	'epc_payments_guide_cp',
	'/<backend_dir>/content/shop/payments/payments_guide_page.php',
	'Payment gateways guide',
	1
);

$accessFixed = epc_pay_sync_all_access($pdo);
$menu = epc_cp_mainstream_menu_apply($pdo);
$payMenu = epc_cp_payments_menu_apply($pdo);

// Legacy CIS payment page stays under Shop/Finance — do not merge into Payment gateways group.
$shopGroup = (int)$pdo->query("SELECT `id` FROM `control_groups` WHERE `caption` = '744' LIMIT 1")->fetchColumn();
if ($shopGroup <= 0) {
	$shopGroup = (int)$menu['shop_group'];
}
if ($shopGroup > 0) {
	$pdo->prepare(
		"UPDATE `control_items` SET `items_group` = ?, `caption` = '3320', `url` = '/<backend>/shop/finance/platezhnye-sistemy', `show_anyway` = 0
		 WHERE `url` LIKE '%platezhnye-sistemy%'"
	)->execute(array($shopGroup));
}

$base = rtrim($cfg->domain_path, '/');
echo json_encode(array(
	'status' => true,
	'message' => 'Payment gateways hub registered',
	'content_id' => $contentId,
	'guide_content_id' => $guideContentId,
	'uae_gateways_seeded' => count($uaeIds),
	'access_fixed' => $accessFixed,
	'menu' => $payMenu,
	'report' => epc_payment_demo_report($pdo),
	'urls' => array(
		'cp_payments' => $base . '/' . $cfg->backend_dir . '/shop/payments/payments',
		'cp_guide' => $base . '/' . $cfg->backend_dir . '/shop/payments/payments/guide',
		'demo_json' => $base . '/epc-payments-demo.php?token=epartscart-deploy-2026',
		'setup_reseed' => $base . '/epc-payments-setup.php?token=epartscart-deploy-2026&reseed=1',
		'access_fix' => $base . '/epc-payments-access-fix.php?token=epartscart-deploy-2026',
	),
	'hint' => 'Log in to CP first at /cp/ — direct URL without session shows login form.',
), JSON_PRETTY_PRINT | JSON_UNESCAPED_UNICODE);
