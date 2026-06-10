<?php
/**
 * Social Media Hub — CP render error debugger.
 * GET ?token=…&host=www.epartscart.com
 */
declare(strict_types=1);

require_once __DIR__ . '/epc_deploy_auth.php';
epc_deploy_require_token();
header('Content-Type: application/json; charset=utf-8');

$host = trim((string) ($_GET['host'] ?? 'www.epartscart.com'));
$_SERVER['HTTP_HOST'] = $host;
$_SERVER['SERVER_NAME'] = $host;

define('_ASTEXE_', 1);

$out = array('host' => $host, 'steps' => array());

try {
	require_once __DIR__ . '/config.php';
	require_once __DIR__ . '/content/general_pages/epc_portal.php';
	require_once __DIR__ . '/content/general_pages/epc_portal_db.php';
	require_once __DIR__ . '/content/general_pages/epc_portal_tenant.php';

	$cfg = new DP_Config();
	epc_portal_apply_config($cfg);
	$out['db'] = (string) $cfg->db;
	$backend = trim((string) ($cfg->backend_dir ?? 'cp'), '/');
	$out['backend'] = $backend;

	$dbHost = trim((string) $cfg->host);
	if ($dbHost === '' || strtolower($dbHost) === 'localhost') {
		$dbHost = '127.0.0.1';
	}
	$db_link = new PDO(
		'mysql:host=' . $dbHost . ';dbname=' . $cfg->db . ';charset=utf8',
		$cfg->user,
		$cfg->password,
		array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION)
	);
	$GLOBALS['db_link'] = $db_link;
	$GLOBALS['DP_Config'] = $cfg;
	$out['steps'][] = 'pdo_ok';

	$hubFile = $_SERVER['DOCUMENT_ROOT'] . '/' . $backend . '/content/control/portal/epc_social_media_hub.php';
	$out['hub_file'] = $hubFile;
	$out['hub_bytes'] = is_file($hubFile) ? filesize($hubFile) : 0;

	$_GET['tab'] = 'guide';
	require_once $_SERVER['DOCUMENT_ROOT'] . '/content/users/dp_user.php';
	$out['steps'][] = 'dp_user_ok';
	$out['is_admin'] = DP_User::isAdmin();

	ob_start();
	$error = null;
	try {
		require $hubFile;
	} catch (Throwable $e) {
		$error = $e->getMessage() . ' @ ' . $e->getFile() . ':' . $e->getLine();
	}
	$html = (string) ob_get_clean();
	$out['render_error'] = $error;
	$out['html_bytes'] = strlen($html);
	$out['has_guide'] = stripos($html, 'epc-social-guide-step') !== false;
	$out['preview'] = substr(strip_tags($html), 0, 200);
	$out['ok'] = $error === null && $out['html_bytes'] > 200;
} catch (Throwable $e) {
	$out['ok'] = false;
	$out['fatal'] = $e->getMessage() . ' @ ' . $e->getFile() . ':' . $e->getLine();
}

echo json_encode($out, JSON_UNESCAPED_UNICODE | JSON_PRETTY_PRINT | JSON_INVALID_UTF8_SUBSTITUTE);
