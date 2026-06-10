<?php
/**
 * Register POS CP module on current tenant DB.
 * Run: https://www.epartscart.com/epc-pos-setup.php?token=epartscart-deploy-2026&apply=1
 */
declare(strict_types=1);

header('Content-Type: application/json; charset=utf-8');
require_once __DIR__ . '/epc_deploy_auth.php';
epc_deploy_require_token();

define('_ASTEXE_', 1);
require_once __DIR__ . '/config.php';
require_once __DIR__ . '/content/shop/pos/epc_pos_cp_install.php';

$apply = !empty($_GET['apply']) && (string) $_GET['apply'] === '1';

$cfg = new DP_Config();
try {
	$pdo = new PDO(
		'mysql:host=' . $cfg->host . ';dbname=' . $cfg->db . ';charset=utf8',
		$cfg->user,
		$cfg->password,
		array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION)
	);
} catch (Throwable $e) {
	echo json_encode(array('status' => false, 'message' => 'DB connect failed: ' . $e->getMessage()));
	exit;
}

$backend = trim((string) $cfg->backend_dir, '/');
if ($backend === '') {
	$backend = 'cp';
}

if (!$apply) {
	$route = null;
	try {
		$st = $pdo->prepare(
			'SELECT `id`, `published_flag`, `content` FROM `content` WHERE `url` = ? AND `is_frontend` = 0 LIMIT 1'
		);
		$st->execute(array('shop/pos/terminal'));
		$route = $st->fetch(PDO::FETCH_ASSOC) ?: null;
	} catch (Throwable $e) {
		$route = array('error' => $e->getMessage());
	}
	$phpRel = '/<backend_dir>/content/shop/pos/epc_pos_terminal_page.php';
	$phpPath = str_replace('<backend_dir>', $backend, $_SERVER['DOCUMENT_ROOT'] . $phpRel);
	echo json_encode(array(
		'status' => true,
		'message' => 'Dry run — add &apply=1 to install',
		'db' => $cfg->db,
		'cp_url' => rtrim($cfg->domain_path, '/') . '/' . $backend . '/shop/pos/terminal',
		'route' => array(
			'registered' => is_array($route) && !empty($route['id']),
			'content_id' => (int) (is_array($route) ? ($route['id'] ?? 0) : 0),
			'published' => is_array($route) && !empty($route['published_flag']),
			'php_on_disk' => is_file($phpPath),
		),
	), JSON_PRETTY_PRINT | JSON_UNESCAPED_UNICODE);
	exit;
}

try {
	$result = epc_pos_cp_install($pdo, $backend);
	$base = rtrim($cfg->domain_path, '/');
	echo json_encode(array(
		'status' => true,
		'message' => 'POS module registered',
		'db' => $cfg->db,
		'hub_content_id' => $result['hub_content_id'],
		'content_id' => $result['content_id'],
		'super_content_id' => $result['super_content_id'],
		'walkin_user_id' => $result['walkin_user_id'],
		'menu' => $result['menu'],
		'urls' => array(
			'cp_terminal' => $base . '/' . $backend . '/shop/pos/terminal',
			'super_overview' => $base . '/' . $backend . '/control/portal/epc_pos_tenant_manage',
			'ajax' => $base . '/' . $backend . '/content/shop/pos/ajax_pos_endpoint.php',
		),
	), JSON_PRETTY_PRINT | JSON_UNESCAPED_UNICODE);
} catch (Throwable $e) {
	echo json_encode(array('status' => false, 'message' => $e->getMessage()));
}
