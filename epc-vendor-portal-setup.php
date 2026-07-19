<?php
/**
 * Register frontend Vendor Portal (multi-vendor self-serve, no CP login).
 *
 * Run:
 *   https://www.epartscart.com/epc-vendor-portal-setup.php?token=...
 *
 * URLs:
 *   /en/vendor
 *   /en/vendor/register
 *   /en/vendor/upload
 */
error_reporting(E_ALL);
ini_set('display_errors', 0);
header('Content-Type: text/plain; charset=utf-8');

require_once __DIR__ . '/epc_deploy_auth.php';
epc_deploy_require_token();

define('_ASTEXE_', 1);
require_once __DIR__ . '/config.php';
require_once __DIR__ . '/content/shop/vendor/epc_vendor_schema.php';
require_once __DIR__ . '/content/shop/vendor/epc_vendor_access.php';

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

function epc_vp_lang(PDO $pdo, $key, $en, $ru)
{
	$pdo->prepare('INSERT IGNORE INTO `lang_text_strings` (`str_key`, `description`, `same`, `is_error`, `is_custom`, `used_found`) VALUES (?, ?, NULL, 0, 1, 1)')->execute(array($key, $en));
	$ins = $pdo->prepare('INSERT INTO `lang_text_strings_translation` (`str_key`, `lang_code`, `value`) VALUES (?, ?, ?) ON DUPLICATE KEY UPDATE `value` = VALUES(`value`)');
	$ins->execute(array($key, 'en', $en));
	$ins->execute(array($key, 'ru', $ru));
}

epc_vp_lang($pdo, 'EPC_VENDOR', 'Vendors', 'Поставщики');
epc_vp_lang($pdo, 'EPC_VENDOR_PORTAL', 'Vendor Portal', 'Кабинет поставщика');
epc_vp_lang($pdo, 'EPC_VENDOR_PORTAL_DESC', 'Register as a vendor and upload price lists from the storefront', 'Регистрация поставщика и загрузка прайсов с витрины');
epc_vp_lang($pdo, 'EPC_VENDOR_REGISTER', 'Vendor registration', 'Регистрация поставщика');
epc_vp_lang($pdo, 'EPC_VENDOR_UPLOAD', 'Vendor price upload', 'Загрузка прайса поставщика');
epc_vp_lang($pdo, 'epc_menu_vendor_portal', 'Sell with us', 'Стать продавцом');
epc_vp_lang($pdo, 'epc_vendor_approvals_cp', 'Vendor approvals', 'Одобрение поставщиков');

epc_vendor_ensure_schema($pdo);
$groupId = epc_vendor_ensure_group($pdo);

function epc_vp_register_content(PDO $pdo, $parentId, $url, $alias, $valueKey, $phpPath, $descKey, $order = 50)
{
	$now = time();
	$level = 1;
	if ($parentId > 0) {
		$pl = $pdo->prepare('SELECT `level` FROM `content` WHERE `id` = ? LIMIT 1');
		$pl->execute(array($parentId));
		$lv = (int) $pl->fetchColumn();
		if ($lv > 0) {
			$level = $lv + 1;
		}
	}
	$existing = $pdo->prepare('SELECT `id` FROM `content` WHERE `url` = ? AND `is_frontend` = 1 LIMIT 1');
	$existing->execute(array($url));
	$contentId = (int) $existing->fetchColumn();
	if ($contentId > 0) {
		$pdo->prepare(
			'UPDATE `content` SET `published_flag` = 1, `content_type` = \'php\', `is_frontend` = 1, `system_flag` = 0,
			 `content` = ?, `title_tag` = ?, `description_tag` = ?, `parent` = ?, `level` = ?, `alias` = ?, `value` = ?, `description` = ?, `time_edited` = ?, `robots_tag` = \'\'
			 WHERE `id` = ?'
		)->execute(array($phpPath, $valueKey, $descKey, $parentId, $level, $alias, $valueKey, $descKey, $now, $contentId));
	} else {
		$pdo->prepare(
			'INSERT INTO `content`
			(`count`, `url`, `level`, `alias`, `value`, `parent`, `description`, `is_frontend`, `content_type`, `content`,
			 `title_tag`, `description_tag`, `keywords_tag`, `author_tag`, `main_flag`, `modules_array`, `css_js`, `robots_tag`,
			 `system_flag`, `published_flag`, `open`, `time_created`, `time_edited`, `order`)
			 VALUES (0, ?, ?, ?, ?, ?, ?, 1, \'php\', ?, ?, ?, \'0\', \'0\', 0, \'[1,22,32,34]\', \'\', \'\', 0, 1, 0, ?, ?, ?)'
		)->execute(array(
			$url, $level, $alias, $valueKey, $parentId, $descKey, $phpPath, $valueKey, $descKey, $now, $now, $order,
		));
		$contentId = (int) $pdo->lastInsertId();
	}
	// Guests must reach login/register — access enforced in PHP.
	$pdo->prepare('DELETE FROM `content_access` WHERE `content_id` = ?')->execute(array($contentId));
	return $contentId;
}

$shopParent = (int) $pdo->query("SELECT `id` FROM `content` WHERE `url` = 'shop' AND `is_frontend` = 1 LIMIT 1")->fetchColumn();
// Prefer top-level /vendor (cleaner) under root parent 0 if shop missing.
$rootParent = $shopParent > 0 ? $shopParent : 0;

$mainId = epc_vp_register_content(
	$pdo,
	0,
	'vendor',
	'vendor_portal',
	'EPC_VENDOR_PORTAL',
	'/content/shop/vendor/vendor_portal.php',
	'EPC_VENDOR_PORTAL_DESC',
	70
);
$regId = epc_vp_register_content(
	$pdo,
	$mainId,
	'vendor/register',
	'vendor_register',
	'EPC_VENDOR_REGISTER',
	'/content/shop/vendor/vendor_register.php',
	'EPC_VENDOR_PORTAL_DESC',
	1
);
$uploadId = epc_vp_register_content(
	$pdo,
	$mainId,
	'vendor/upload',
	'vendor_upload',
	'EPC_VENDOR_UPLOAD',
	'/content/shop/vendor/vendor_upload.php',
	'EPC_VENDOR_PORTAL_DESC',
	2
);

// Optional frontend menu link (menu id 15 = top nav when present).
$menuUpdated = array();
$stmt = $pdo->query('SELECT `id`, `structure` FROM `menu` WHERE `is_frontend` = 1 ORDER BY `id`');
while ($row = $stmt->fetch(PDO::FETCH_ASSOC)) {
	$menuId = (int) $row['id'];
	if ($menuId !== 15 && count($menuUpdated) > 0) {
		continue;
	}
	$structure = json_decode((string) $row['structure'], true);
	if (!is_array($structure)) {
		$structure = array();
	}
	$has = false;
	foreach ($structure as $item) {
		if ((isset($item['content_id']) && (int) $item['content_id'] === $mainId)
			|| (isset($item['a_innerhtml']) && $item['a_innerhtml'] === 'epc_menu_vendor_portal')) {
			$has = true;
			break;
		}
	}
	if ($has) {
		continue;
	}
	$item = array(
		'value' => 'epc_menu_vendor_portal',
		'class_li' => '', 'class_ul' => '', 'class_a' => '',
		'id_li' => '', 'id_ul' => '', 'id_a' => '',
		'a_innerhtml_mode' => 'auto',
		'a_innerhtml' => 'epc_menu_vendor_portal',
		'link_mode' => 'content',
		'content_id' => $mainId,
		'href' => '',
		'target' => '', 'onclick' => '',
		'img_src' => '',
		'$count' => 0,
		'$level' => 1,
		'$parent' => 0,
		'id' => time() + 91,
	);
	if (count($structure) > 2) {
		array_splice($structure, 2, 0, array($item));
	} else {
		$structure[] = $item;
	}
	$pdo->prepare('UPDATE `menu` SET `structure` = ? WHERE `id` = ?')->execute(array(
		json_encode($structure, JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES),
		$menuId,
	));
	$menuUpdated[] = $menuId;
}

// Optional CP approvals page under users.
$cpParent = $pdo->prepare('SELECT `id`, `level` FROM `content` WHERE `url` = ? AND `is_frontend` = 0 LIMIT 1');
$cpParent->execute(array('users'));
$cpParentRow = $cpParent->fetch(PDO::FETCH_ASSOC);
$cpContentId = 0;
$cpControlId = 0;
if ($cpParentRow) {
	$cpUrl = 'users/vendor_approvals';
	$cpPhp = '/<backend_dir>/content/users/epc_vendor_approvals.php';
	$now = time();
	$ex = $pdo->prepare('SELECT `id` FROM `content` WHERE `url` = ? AND `is_frontend` = 0 LIMIT 1');
	$ex->execute(array($cpUrl));
	$cpContentId = (int) $ex->fetchColumn();
	$parentId = (int) $cpParentRow['id'];
	$level = (int) $cpParentRow['level'] + 1;
	if ($cpContentId > 0) {
		$pdo->prepare(
			'UPDATE `content` SET `published_flag`=1, `content_type`=\'php\', `content`=?, `title_tag`=?, `parent`=?, `level`=?, `alias`=\'vendor_approvals\', `value`=?, `time_edited`=? WHERE `id`=?'
		)->execute(array($cpPhp, 'Vendor approvals — ePartsCart', $parentId, $level, 'epc_vendor_approvals_cp', $now, $cpContentId));
	} else {
		$pdo->prepare(
			'INSERT INTO `content`
			(`count`,`url`,`level`,`alias`,`value`,`parent`,`description`,`is_frontend`,`content_type`,`content`,
			 `title_tag`,`description_tag`,`keywords_tag`,`author_tag`,`main_flag`,`modules_array`,`css_js`,`robots_tag`,
			 `system_flag`,`published_flag`,`open`,`time_created`,`time_edited`,`order`)
			 VALUES (0,?,?,?,?,?,?,0,\'php\',?,?,\'0\',\'0\',\'0\',0,\'[]\',\'\',\'\',0,1,0,?,?,90)'
		)->execute(array(
			$cpUrl, $level, 'vendor_approvals', 'epc_vendor_approvals_cp', $parentId,
			'Approve or suspend frontend vendor accounts', $cpPhp, 'Vendor approvals — ePartsCart', $now, $now,
		));
		$cpContentId = (int) $pdo->lastInsertId();
	}
	$ref = $pdo->prepare('SELECT `id` FROM `content` WHERE `url` = ? AND `is_frontend` = 0 LIMIT 1');
	$ref->execute(array('users/users'));
	$refId = (int) $ref->fetchColumn();
	if ($refId < 1) {
		$ref->execute(array('users'));
		$refId = (int) $ref->fetchColumn();
	}
	if ($refId > 0 && $cpContentId > 0) {
		$pdo->prepare('DELETE FROM `content_access` WHERE `content_id` = ?')->execute(array($cpContentId));
		$groups = $pdo->prepare('SELECT DISTINCT `group_id` FROM `content_access` WHERE `content_id` = ?');
		$groups->execute(array($refId));
		$ins = $pdo->prepare('INSERT INTO `content_access` (`content_id`, `group_id`) VALUES (?, ?)');
		while ($g = $groups->fetch(PDO::FETCH_ASSOC)) {
			try { $ins->execute(array($cpContentId, (int) $g['group_id'])); } catch (Exception $e) {}
		}
	}
	$controlUrl = '/<backend>/users/vendor_approvals';
	$existingControl = $pdo->prepare('SELECT `id` FROM `control_items` WHERE `url` = ? OR `url` LIKE ? LIMIT 1');
	$existingControl->execute(array($controlUrl, '%/users/vendor_approvals'));
	$cpControlId = (int) $existingControl->fetchColumn();
	$itemsGroup = 3;
	$menuOrder = 40;
	$uRef = $pdo->query("SELECT `items_group`, `order` FROM `control_items` WHERE `url` LIKE '%/users/%' ORDER BY `order` ASC LIMIT 1")->fetch(PDO::FETCH_ASSOC);
	if ($uRef) {
		$itemsGroup = (int) $uRef['items_group'] ?: 3;
		$menuOrder = (int) $uRef['order'] + 5;
	}
	if ($cpControlId > 0) {
		$pdo->prepare('UPDATE `control_items` SET `items_group`=?, `caption`=?, `url`=?, `order`=?, `fontawesome_class`=?, `background_color`=? WHERE `id`=?')
			->execute(array($itemsGroup, 'epc_vendor_approvals_cp', $controlUrl, $menuOrder, 'fas fa-store', '#dc2626', $cpControlId));
	} else {
		$pdo->prepare(
			'INSERT INTO `control_items` (`items_group`,`caption`,`url`,`img`,`order`,`background_color`,`fontawesome_class`,`target`,`show_anyway`)
			 VALUES (?,?,?,?,?,?,?,?,0)'
		)->execute(array($itemsGroup, 'epc_vendor_approvals_cp', $controlUrl, '', $menuOrder, '#dc2626', 'fas fa-store', ''));
		$cpControlId = (int) $pdo->lastInsertId();
	}
}

$vendors = (int) $pdo->query('SELECT COUNT(*) FROM `epc_vendor_accounts`')->fetchColumn();
$base = rtrim((string) $cfg->domain_path, '/');
if ($base === '') {
	$base = 'https://www.epartscart.com';
}

echo "OK vendor portal registered\n";
echo "group_id={$groupId} key=EPC_VENDOR\n";
echo "content vendor={$mainId} register={$regId} upload={$uploadId}\n";
echo "menus=" . implode(',', $menuUpdated) . "\n";
echo "cp_approvals content_id={$cpContentId} control_id={$cpControlId}\n";
echo "vendor_accounts={$vendors}\n";
echo "URLs:\n";
echo "  {$base}/en/vendor\n";
echo "  {$base}/en/vendor/register\n";
echo "  {$base}/en/vendor/upload\n";
echo "  {$base}/cp/users/vendor_approvals\n";
