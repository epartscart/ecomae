<?php
/**
 * Register WhatsApp CP guide, ensure epc_whatsapp_number in config.
 * Run: https://www.epartscart.com/epc-whatsapp-setup.php?token=epartscart-deploy-2026
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

$cfg = new DP_Config();
$pdo = new PDO(
	'mysql:host=' . $cfg->host . ';dbname=' . $cfg->db . ';charset=utf8',
	$cfg->user,
	$cfg->password,
	array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION)
);

$configPath = __DIR__ . '/config.php';
$config = file_get_contents($configPath);
$configChanged = false;
if (strpos($config, '$epc_whatsapp_number') === false) {
	$config = preg_replace(
		"/\n}\s*\?>\s*$/",
		"\tpublic \$epc_whatsapp_number = '+971-567607011';/*Frontend WhatsApp number*/\n}\n?>",
		$config
	);
	file_put_contents($configPath, $config);
	$configChanged = true;
}

function epc_wa_register_content(PDO $pdo, $parentUrl, $url, $alias, $valueKey, $phpPath, $title, $order = 82)
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
		)->execute(array(
			$url,
			$level,
			$alias,
			$valueKey,
			$parentId,
			'WhatsApp sharing — staff and customer workflows',
			$phpPath,
			$title,
			$now,
			$now,
			$order,
		));
		$contentId = (int)$pdo->lastInsertId();
	}

	$ref = $pdo->prepare('SELECT `id` FROM `content` WHERE `url` = ? AND `is_frontend` = 0 LIMIT 1');
	$refIds = array();
	foreach (array('shop/orders/orders', 'shop/orders/guide', 'shop') as $refUrl) {
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

try {
	$contentId = epc_wa_register_content(
		$pdo,
		'shop/orders/orders',
		'shop/orders/whatsapp-guide',
		'whatsapp-guide',
		'epc_whatsapp_guide_cp',
		'/<backend_dir>/content/shop/order_process/whatsapp_guide_page.php',
		'WhatsApp sharing guide',
		82
	);
} catch (Exception $e) {
	exit(json_encode(array('status' => false, 'message' => $e->getMessage())));
}

$menuItem = null;
// Prefer Logistics top menu (same group as OMS · Orders); fall back to Shop group.
$menuHelper = __DIR__ . '/epc_cp_mainstream_menu.php';
if (is_file($menuHelper)) {
	require_once $menuHelper;
	if (function_exists('epc_cp_mm_lang')) {
		epc_cp_mm_lang($pdo, 'epc_whatsapp_guide', 'WhatsApp guide', 'Гид WhatsApp');
		epc_cp_mm_lang($pdo, 'epc_whatsapp_guide_cp', 'WhatsApp guide', 'Гид WhatsApp');
	}
	if (function_exists('epc_cp_mainstream_menu_apply')) {
		$mm = epc_cp_mainstream_menu_apply($pdo);
		if (!empty($mm['items']['whatsapp_guide'])) {
			$menuItem = array(
				'id' => (int) $mm['items']['whatsapp_guide'],
				'url' => '/<backend>/shop/orders/whatsapp-guide',
				'items_group' => (int) ($mm['logistics_group'] ?? 0),
			);
		}
	}
}
if ($menuItem === null) {
	$ordersGroup = (int)$pdo->query(
		"SELECT `id` FROM `control_items` WHERE `url` LIKE '%/shop/orders/orders%' OR `url` LIKE '%shop/orders/orders%' LIMIT 1"
	)->fetchColumn();
	if ($ordersGroup > 0) {
		$row = $pdo->prepare('SELECT `items_group` FROM `control_items` WHERE `id` = ? LIMIT 1');
		$row->execute(array($ordersGroup));
		$itemsGroup = (int)$row->fetchColumn();
		if ($itemsGroup > 0) {
			$menuUrl = '/<backend>/shop/orders/whatsapp-guide';
			$existingMenu = $pdo->prepare('SELECT `id` FROM `control_items` WHERE `url` LIKE ? LIMIT 1');
			$existingMenu->execute(array('%whatsapp-guide%'));
			$menuId = (int)$existingMenu->fetchColumn();
			if ($menuId > 0) {
				$pdo->prepare(
					'UPDATE `control_items` SET `caption` = ?, `url` = ?, `items_group` = ?, `show_anyway` = 1,
					 `fontawesome_class` = ?, `background_color` = ? WHERE `id` = ?'
				)->execute(array('epc_whatsapp_guide', $menuUrl, $itemsGroup, 'fab fa-whatsapp', '#25D366', $menuId));
			} else {
				$maxOrder = (int)$pdo->query(
					'SELECT COALESCE(MAX(`order`), 0) FROM `control_items` WHERE `items_group` = ' . (int)$itemsGroup
				)->fetchColumn();
				$pdo->prepare(
					'INSERT INTO `control_items` (`items_group`, `caption`, `url`, `img`, `order`, `background_color`, `fontawesome_class`, `target`, `show_anyway`)
					 VALUES (?, ?, ?, \'\', ?, ?, ?, \'\', 1)'
				)->execute(array($itemsGroup, 'epc_whatsapp_guide', $menuUrl, $maxOrder + 2, '#25D366', 'fab fa-whatsapp'));
				$menuId = (int)$pdo->lastInsertId();
			}
			$menuItem = array('id' => $menuId, 'url' => $menuUrl, 'items_group' => $itemsGroup);
		}
	}
}

$base = rtrim($cfg->domain_path, '/');
echo json_encode(array(
	'status' => true,
	'message' => 'WhatsApp guide registered',
	'content_id' => $contentId,
	'config_epc_whatsapp_number_added' => $configChanged,
	'menu' => $menuItem,
	'urls' => array(
		'cp_guide' => $base . '/' . $cfg->backend_dir . '/shop/orders/whatsapp-guide',
		'contact_setup' => $base . '/epc-contact-settings-setup.php?token=epartscart-deploy-2026',
	),
	'hint' => 'Log in to CP first at /cp/ — direct URL without session shows login form.',
), JSON_PRETTY_PRINT | JSON_UNESCAPED_UNICODE);
