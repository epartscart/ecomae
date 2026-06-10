<?php
/**
 * Diagnose CP shop/finance/erp route, file, access, PHP render.
 * GET: token=epartscart-deploy-2026
 */
header('Content-Type: application/json; charset=utf-8');

if (($_GET['token'] ?? '') !== 'epartscart-deploy-2026') {
	http_response_code(403);
	exit(json_encode(array('status' => false, 'message' => 'Forbidden')));
}

require_once __DIR__ . '/config.php';
require_once __DIR__ . '/content/general_pages/epc_portal.php';
$DP_Config = new DP_Config;
epc_portal_apply_config($DP_Config);
$db = new PDO(
	'mysql:host=' . $DP_Config->host . ';dbname=' . $DP_Config->db,
	$DP_Config->user,
	$DP_Config->password,
	array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION)
);
$db->query('SET NAMES utf8');

$url = 'shop/finance/erp';
$guideUrl = 'shop/finance/erp/guide';
$csGuideUrl = 'shop/finance/erp/custom-shipping-guide';
$uaeTaxUrl = 'shop/finance/erp/uae-tax-compliance';
$backend = $DP_Config->backend_dir;
$root = $_SERVER['DOCUMENT_ROOT'];

$out = array('status' => true, 'backend' => $backend, 'checks' => array());

foreach (array(
	'erp' => $url,
	'guide' => $guideUrl,
	'custom_shipping_guide' => $csGuideUrl,
	'uae_tax_compliance' => $uaeTaxUrl,
) as $key => $contentUrl) {
	$st = $db->prepare('SELECT * FROM `content` WHERE `url` = ? AND `is_frontend` = 0 LIMIT 1');
	$st->execute(array($contentUrl));
	$row = $st->fetch(PDO::FETCH_ASSOC);
	$check = array('content_url' => $contentUrl, 'found' => (bool)$row);
	if ($row) {
		$check['content_id'] = (int)$row['id'];
		$check['published'] = (int)$row['published_flag'];
		$check['php_content_path'] = $row['content'];
		$check['parent'] = (int)$row['parent'];
		$check['level'] = (int)$row['level'];
		$check['alias'] = (string)$row['alias'];
		$disk = str_replace('<backend_dir>', $backend, $root . $row['content']);
		$check['disk_path'] = $disk;
		$check['file_exists'] = is_file($disk);
		$check['file_readable'] = is_readable($disk);
		$acc = $db->prepare('SELECT `group_id` FROM `content_access` WHERE `content_id` = ?');
		$acc->execute(array((int)$row['id']));
		$check['access_groups'] = $acc->fetchAll(PDO::FETCH_COLUMN);
		if ($check['file_exists']) {
			$code = 0;
			exec('php -l ' . escapeshellarg($disk) . ' 2>&1', $lint, $code);
			$check['php_lint'] = ($code === 0) ? 'ok' : implode(' ', $lint);
		}
	}
	$out['checks'][$key] = $check;
}

$menu = $db->prepare('SELECT * FROM `control_items` WHERE `url` LIKE ? LIMIT 1');
$menu->execute(array('%/shop/finance/erp%'));
$menuRow = $menu->fetch(PDO::FETCH_ASSOC);
$out['menu'] = $menuRow ? array(
	'id' => (int)$menuRow['id'],
	'url' => $menuRow['url'],
	'caption' => $menuRow['caption'],
	'items_group' => (int)$menuRow['items_group'],
) : null;

// Render test (wrapper include chain)
$mainDisk = $root . '/' . $backend . '/content/shop/finance/erp/erp_main_page.php';
$bodyDisk = $root . '/' . $backend . '/content/shop/finance/erp/erp_main.php';
$out['render'] = array(
	'wrapper' => is_file($mainDisk),
	'body' => is_file($bodyDisk),
	'helpers' => is_file($root . '/content/shop/finance/epc_erp_helpers.php'),
	'gl' => is_file($root . '/content/shop/finance/epc_erp_gl.php'),
);

if (is_file($mainDisk)) {
	define('_ASTEXE_', 1);
	$GLOBALS['DP_Config'] = $DP_Config;
	$GLOBALS['db_link'] = $db;
	$db_link = $db;
	$user_session = array('user_id' => 1, 'csrf_guard_key' => 'diag');
	ob_start();
	$renderErr = null;
	try {
		include $mainDisk;
	} catch (Throwable $e) {
		$renderErr = $e->getMessage() . ' @ ' . $e->getFile() . ':' . $e->getLine();
	}
	$html = ob_get_clean();
	$out['render']['include_ok'] = ($renderErr === null);
	$out['render']['error'] = $renderErr;
	$out['render']['html_bytes'] = strlen($html);
	$out['render']['preview'] = substr(strip_tags($html), 0, 300);
}

$uaeDisk = $root . '/' . $backend . '/content/shop/finance/erp/erp_uae_tax_compliance_page.php';
$out['uae_tax_render'] = array('wrapper' => is_file($uaeDisk));
if (is_file($uaeDisk)) {
	define('_ASTEXE_', 1);
	$GLOBALS['DP_Config'] = $DP_Config;
	$GLOBALS['db_link'] = $db;
	$db_link = $db;
	$user_session = array('user_id' => 1, 'csrf_guard_key' => 'diag');
	$_GET['epc_erp_shell'] = '1';
	$GLOBALS['epc_erp_shell_mode'] = true;
	ob_start();
	$uaeErr = null;
	try {
		include $uaeDisk;
	} catch (Throwable $e) {
		$uaeErr = $e->getMessage() . ' @ ' . $e->getFile() . ':' . $e->getLine();
	}
	$uaeHtml = ob_get_clean();
	$out['uae_tax_render']['include_ok'] = ($uaeErr === null);
	$out['uae_tax_render']['error'] = $uaeErr;
	$out['uae_tax_render']['html_bytes'] = strlen($uaeHtml);
	$out['uae_tax_render']['has_title'] = (stripos($uaeHtml, 'UAE Tax Compliance') !== false);
	$out['uae_tax_render']['has_guide'] = (stripos($uaeHtml, 'epc-uae-tax-guide') !== false);
	$out['uae_tax_render']['preview'] = substr(strip_tags($uaeHtml), 0, 300);
}

$out['cp_url'] = 'https://www.epartscart.com/' . $backend . '/shop/finance/erp';
$out['uae_tax_guide_url'] = 'https://www.epartscart.com/' . $backend . '/shop/finance/erp/uae-tax-compliance?epc_erp_shell=1';
$out['uae_tax_tab_url'] = 'https://www.epartscart.com/' . $backend . '/shop/finance/erp?area=finance&tab=tax_compliance&epc_erp_shell=1';
$out['note'] = 'Unauthenticated browser visits show CP login form — that is normal. Log in first, then open ERP.';

echo json_encode($out, JSON_PRETTY_PRINT | JSON_UNESCAPED_UNICODE);
