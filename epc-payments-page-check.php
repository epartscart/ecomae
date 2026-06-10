<?php
/**
 * Diagnose CP shop/payments/payments route.
 * GET: token=epartscart-deploy-2026
 */
header('Content-Type: application/json; charset=utf-8');

if (($_GET['token'] ?? '') !== 'epartscart-deploy-2026') {
	http_response_code(403);
	exit(json_encode(array('status' => false, 'message' => 'Forbidden')));
}

require_once __DIR__ . '/config.php';
$DP_Config = new DP_Config;
$db = new PDO(
	'mysql:host=' . $DP_Config->host . ';dbname=' . $DP_Config->db,
	$DP_Config->user,
	$DP_Config->password,
	array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION)
);
$db->query('SET NAMES utf8');

$url = 'shop/payments/payments';
$guideUrl = 'shop/payments/payments/guide';
$backend = $DP_Config->backend_dir;
$root = $_SERVER['DOCUMENT_ROOT'];

$out = array('status' => true, 'backend' => $backend, 'checks' => array());

foreach (array('payments' => $url, 'guide' => $guideUrl, 'guide_legacy' => 'shop/payments/guide') as $key => $contentUrl) {
	$st = $db->prepare('SELECT * FROM `content` WHERE `url` = ? AND `is_frontend` = 0 LIMIT 1');
	$st->execute(array($contentUrl));
	$row = $st->fetch(PDO::FETCH_ASSOC);
	$check = array('content_url' => $contentUrl, 'found' => (bool)$row);
	if ($row) {
		$check['content_id'] = (int)$row['id'];
		$check['published'] = (int)$row['published_flag'];
		$check['php_content_path'] = $row['content'];
		$disk = str_replace('<backend_dir>', $backend, $root . $row['content']);
		$check['disk_path'] = $disk;
		$check['file_exists'] = is_file($disk);
		$acc = $db->prepare('SELECT `group_id` FROM `content_access` WHERE `content_id` = ?');
		$acc->execute(array((int)$row['id']));
		$check['access_groups'] = $acc->fetchAll(PDO::FETCH_COLUMN);
	}
	$out['checks'][$key] = $check;
}

$out['render'] = array(
	'wrapper' => is_file($root . '/' . $backend . '/content/shop/payments/payments_main_page.php'),
	'body' => is_file($root . '/' . $backend . '/content/shop/payments/payments_main.php'),
	'helpers' => is_file($root . '/content/shop/payments/epc_payment_helpers.php'),
);

$mainDisk = $root . '/' . $backend . '/content/shop/payments/payments_main_page.php';
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

$menu = $db->prepare('SELECT * FROM `control_items` WHERE `url` LIKE ? LIMIT 1');
$menu->execute(array('%/shop/payments/payments%'));
$menuRow = $menu->fetch(PDO::FETCH_ASSOC);
$out['menu'] = $menuRow ? array(
	'id' => (int)$menuRow['id'],
	'url' => $menuRow['url'],
	'caption' => $menuRow['caption'],
	'items_group' => (int)$menuRow['items_group'],
) : null;

$out['cp_url'] = 'https://www.epartscart.com/' . $backend . '/shop/payments/payments';
$out['note'] = 'Unauthenticated browser visits show CP login — log in first, then open Payments.';

echo json_encode($out, JSON_PRETTY_PRINT | JSON_UNESCAPED_UNICODE);
