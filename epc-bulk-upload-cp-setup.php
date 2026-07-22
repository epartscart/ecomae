<?php
/**
 * Register CP Bulk Upload Control hub.
 *
 * Run:
 *   https://www.epartscart.com/epc-bulk-upload-cp-setup.php?token=epartscart-deploy-2026
 *
 * Opens:
 *   https://www.epartscart.com/cp/shop/bulk_upload
 */
error_reporting(E_ALL);
ini_set('display_errors', 0);
header('Content-Type: application/json; charset=utf-8');

require_once __DIR__ . '/epc_deploy_auth.php';
epc_deploy_require_token();

define('_ASTEXE_', 1);
require_once __DIR__ . '/config.php';

$cfg = new DP_Config();
$epcTenantHostDbFile = __DIR__ . '/config.tenant-host-db.php';
if (is_file($epcTenantHostDbFile)) {
	$epc_tenant_host_db = null;
	require $epcTenantHostDbFile;
	$host = strtolower((string) ($_SERVER['HTTP_HOST'] ?? 'www.epartscart.com'));
	if (strpos($host, ':') !== false) {
		$host = explode(':', $host, 2)[0];
	}
	if (isset($epc_tenant_host_db) && is_array($epc_tenant_host_db) && isset($epc_tenant_host_db[$host])) {
		foreach (array('db', 'user', 'password') as $epcTk) {
			if (!empty($epc_tenant_host_db[$host][$epcTk]) && property_exists($cfg, $epcTk)) {
				$cfg->$epcTk = $epc_tenant_host_db[$host][$epcTk];
			}
		}
	}
}

$pdo = new PDO('mysql:host=' . $cfg->host . ';dbname=' . $cfg->db . ';charset=utf8mb4', $cfg->user, $cfg->password);
$pdo->setAttribute(PDO::ATTR_ERRMODE, PDO::ERRMODE_EXCEPTION);

require_once __DIR__ . '/content/shop/bulk_upload/epc_bulk_helpers.php';
epc_bulk_ensure_history_schema($pdo);

function epc_bu_cp_lang(PDO $pdo, $key, $en, $ru)
{
	$pdo->prepare('INSERT IGNORE INTO `lang_text_strings` (`str_key`, `description`, `same`, `is_error`, `is_custom`, `used_found`) VALUES (?, ?, NULL, 0, 1, 1)')->execute(array($key, $en));
	$ins = $pdo->prepare('INSERT INTO `lang_text_strings_translation` (`str_key`, `lang_code`, `value`) VALUES (?, ?, ?) ON DUPLICATE KEY UPDATE `value` = VALUES(`value`)');
	$ins->execute(array($key, 'en', $en));
	$ins->execute(array($key, 'ru', $ru));
}

epc_bu_cp_lang($pdo, 'epc_bulk_upload_cp', 'Bulk Upload', 'Массовая загрузка');

$mmHelpers = __DIR__ . '/epc_cp_mainstream_menu.php';
if (is_file($mmHelpers)) {
	require_once $mmHelpers;
}

$parent = $pdo->prepare('SELECT `id`, `level` FROM `content` WHERE `url` = ? AND `is_frontend` = 0 LIMIT 1');
$parent->execute(array('shop'));
$parentRow = $parent->fetch(PDO::FETCH_ASSOC);
if (!$parentRow) {
	exit(json_encode(array('status' => false, 'message' => 'Parent content shop not found')));
}
$parentId = (int) $parentRow['id'];
$level = (int) $parentRow['level'] + 1;

$url = 'shop/bulk_upload';
$phpPath = '/<backend_dir>/content/shop/bulk_upload/bulk_upload_hub.php';
$now = time();

$existing = $pdo->prepare('SELECT `id` FROM `content` WHERE `url` = ? AND `is_frontend` = 0 LIMIT 1');
$existing->execute(array($url));
$contentId = (int) $existing->fetchColumn();

if ($contentId > 0) {
	$pdo->prepare(
		'UPDATE `content` SET `published_flag` = 1, `content_type` = \'php\', `is_frontend` = 0, `system_flag` = 0,
		 `content` = ?, `title_tag` = ?, `parent` = ?, `level` = ?, `alias` = \'bulk_upload_hub\', `url` = ?, `value` = ?, `description` = ?, `time_edited` = ?
		 WHERE `id` = ?'
	)->execute(array(
		$phpPath,
		'Bulk Upload Control — ePartsCart',
		$parentId,
		$level,
		$url,
		'epc_bulk_upload_cp',
		'Review storefront bulk uploads; process for customers; cart / quote / ERP',
		$now,
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
		'bulk_upload_hub',
		'epc_bulk_upload_cp',
		$parentId,
		'Review storefront bulk uploads; process for customers; cart / quote / ERP',
		$phpPath,
		'Bulk Upload Control — ePartsCart',
		$now,
		$now,
	));
	$contentId = (int) $pdo->lastInsertId();
}

$refUrls = array('shop/quote-requests', 'shop/orders/orders', 'shop/prices', 'shop/crosses');
$refId = 0;
foreach ($refUrls as $refUrl) {
	$ref = $pdo->prepare('SELECT `id` FROM `content` WHERE `url` = ? AND `is_frontend` = 0 LIMIT 1');
	$ref->execute(array($refUrl));
	$refId = (int) $ref->fetchColumn();
	if ($refId > 0) {
		break;
	}
}
$aclGroups = 0;
if ($refId > 0 && $contentId > 0) {
	$pdo->prepare('DELETE FROM `content_access` WHERE `content_id` = ?')->execute(array($contentId));
	$groups = $pdo->prepare('SELECT DISTINCT `group_id` FROM `content_access` WHERE `content_id` = ?');
	$groups->execute(array($refId));
	$ins = $pdo->prepare('INSERT INTO `content_access` (`content_id`, `group_id`) VALUES (?, ?)');
	while ($g = $groups->fetch(PDO::FETCH_ASSOC)) {
		try {
			$ins->execute(array($contentId, (int) $g['group_id']));
			$aclGroups++;
		} catch (Exception $e) {
		}
	}
}

$itemsGroup = 6;
$menuOrder = 28;
if (function_exists('epc_cp_mm_find_shop_group')) {
	$shopGroup = epc_cp_mm_find_shop_group($pdo);
	$itemsGroup = (int) ($shopGroup['id'] ?? 6);
}
$quoteRef = $pdo->prepare('SELECT `items_group`, `order` FROM `control_items` WHERE `url` LIKE ? LIMIT 1');
$quoteRef->execute(array('%/shop/quote-requests%'));
$quoteRow = $quoteRef->fetch(PDO::FETCH_ASSOC);
if ($quoteRow) {
	if ((int) $quoteRow['items_group'] > 0) {
		$itemsGroup = (int) $quoteRow['items_group'];
	}
	$menuOrder = (int) $quoteRow['order'] + 1;
}

$controlUrl = '/<backend>/shop/bulk_upload';
$controlId = 0;
if (function_exists('epc_cp_mm_ensure_item')) {
	$controlId = epc_cp_mm_ensure_item(
		$pdo,
		$itemsGroup,
		'epc_bulk_upload_cp',
		$controlUrl,
		$menuOrder,
		'#1d4ed8',
		'fas fa-file-excel',
		0
	);
} else {
	$st = $pdo->prepare('SELECT `id` FROM `control_items` WHERE `items_group` = ? AND `url` = ? LIMIT 1');
	$st->execute(array($itemsGroup, $controlUrl));
	$controlId = (int) $st->fetchColumn();
	if ($controlId > 0) {
		$pdo->prepare('UPDATE `control_items` SET `caption` = ?, `order` = ?, `background_color` = ?, `fontawesome_class` = ? WHERE `id` = ?')
			->execute(array('epc_bulk_upload_cp', $menuOrder, '#1d4ed8', 'fas fa-file-excel', $controlId));
	} else {
		$pdo->prepare(
			'INSERT INTO `control_items` (`items_group`, `caption`, `url`, `img`, `order`, `background_color`, `fontawesome_class`, `target`, `show_anyway`)
			 VALUES (?, ?, ?, \'\', ?, ?, ?, \'\', 0)'
		)->execute(array($itemsGroup, 'epc_bulk_upload_cp', $controlUrl, $menuOrder, '#1d4ed8', 'fas fa-file-excel'));
		$controlId = (int) $pdo->lastInsertId();
	}
}

$base = rtrim((string) $cfg->domain_path, '/');
if ($base === '') {
	$base = 'https://www.epartscart.com';
}

echo json_encode(array(
	'status' => true,
	'message' => 'CP Bulk Upload hub registered',
	'content_id' => $contentId,
	'control_items_id' => $controlId,
	'acl_groups' => $aclGroups,
	'urls' => array(
		'cp' => $base . '/' . $cfg->backend_dir . '/shop/bulk_upload',
		'storefront' => $base . '/en/shop/bulk-upload',
		'quotes' => $base . '/' . $cfg->backend_dir . '/shop/quote-requests',
		'crm_quotes' => $base . '/' . $cfg->backend_dir . '/shop/finance/erp?tab=crm&crm_tab=quotes',
	),
), JSON_PRETTY_PRINT | JSON_UNESCAPED_SLASHES);
