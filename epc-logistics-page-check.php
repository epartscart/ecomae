<?php
/**
 * Diagnose CP logistics routes (hub, carriers, guide).
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

function epc_log_pc_render($disk, $DP_Config, $db)
{
	if (!is_file($disk)) {
		return array('include_ok' => false, 'error' => 'file missing', 'html_bytes' => 0, 'preview' => '');
	}
	define('_ASTEXE_', 1);
	$GLOBALS['DP_Config'] = $DP_Config;
	$GLOBALS['db_link'] = $db;
	$db_link = $db;
	$user_session = array('user_id' => 1, 'csrf_guard_key' => 'diag');
	ob_start();
	$renderErr = null;
	try {
		include $disk;
	} catch (Throwable $e) {
		$renderErr = $e->getMessage() . ' @ ' . $e->getFile() . ':' . $e->getLine();
	}
	$html = ob_get_clean();
	return array(
		'include_ok' => ($renderErr === null),
		'error' => $renderErr,
		'html_bytes' => strlen($html),
		'preview' => substr(strip_tags($html), 0, 300),
	);
}

$backend = $DP_Config->backend_dir;
$root = $_SERVER['DOCUMENT_ROOT'];

$urls = array(
	'hub' => 'shop/logistics',
	'carriers' => 'shop/logistics/carriers',
	'guide' => 'shop/logistics/guide',
);

$out = array('status' => true, 'backend' => $backend, 'checks' => array());

foreach ($urls as $key => $contentUrl) {
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

$dup = $db->query(
	"SELECT `id`, `url`, `published_flag`, `content` FROM `content`
	 WHERE `url` LIKE '%logistics%guide%' AND `is_frontend` = 0 ORDER BY `id`"
)->fetchAll(PDO::FETCH_ASSOC);
$out['guide_duplicates'] = $dup;

$out['render'] = array(
	'carriers' => epc_log_pc_render(
		$root . '/' . $backend . '/content/shop/logistics/logistics_carriers_page.php',
		$DP_Config,
		$db
	),
	'guide' => epc_log_pc_render(
		$root . '/' . $backend . '/content/shop/logistics/logistics_guide_page.php',
		$DP_Config,
		$db
	),
);

$out['menu_items'] = $db->query(
	"SELECT `id`, `caption`, `url`, `items_group`, `show_anyway` FROM `control_items`
	 WHERE `url` LIKE '%/shop/logistics/%' OR `caption` LIKE 'epc_logistics_%'
	 ORDER BY `items_group`, `order`, `id`"
)->fetchAll(PDO::FETCH_ASSOC);

$out['urls'] = array(
	'hub' => 'https://www.epartscart.com/' . $backend . '/shop/logistics',
	'carriers' => 'https://www.epartscart.com/' . $backend . '/shop/logistics/carriers',
	'guide' => 'https://www.epartscart.com/' . $backend . '/shop/logistics/guide',
);
$out['note'] = 'Unauthenticated browser visits show CP login — log in first at /cp/, then open Logistics guide.';

echo json_encode($out, JSON_PRETTY_PRINT | JSON_UNESCAPED_UNICODE);
