<?php
/**
 * Pinpoint eval / runtime failure for Marketing Broadcast CP route.
 * GET ?token=…&host=www.epartscart.com&admin=1
 */
declare(strict_types=1);

require_once __DIR__ . '/epc_deploy_auth.php';
epc_deploy_require_token();
header('Content-Type: application/json; charset=utf-8');
set_time_limit(60);

define('_ASTEXE_', 1);
require_once __DIR__ . '/config.php';
require_once __DIR__ . '/content/general_pages/epc_portal.php';
require_once __DIR__ . '/content/general_pages/epc_portal_db.php';
require_once __DIR__ . '/content/general_pages/epc_cp_script_relocate.php';

$host = trim((string) ($_GET['host'] ?? 'www.epartscart.com'));
$asAdmin = !empty($_GET['admin']);
$_SERVER['HTTP_HOST'] = $host;
$_SERVER['SERVER_NAME'] = $host;

$cfg = new DP_Config();
epc_portal_apply_config($cfg);

$overrideFile = __DIR__ . '/config.tenant-host-db.php';
if (is_file($overrideFile)) {
	require $overrideFile;
	$hk = strtolower($host);
	if (!empty($epc_tenant_host_db[$hk]) && is_array($epc_tenant_host_db[$hk])) {
		foreach (array('db', 'user', 'password') as $tk) {
			if (!empty($epc_tenant_host_db[$hk][$tk])) {
				$cfg->$tk = $epc_tenant_host_db[$hk][$tk];
			}
		}
	}
}

$dbHost = trim((string) $cfg->host);
if ($dbHost === '' || strtolower($dbHost) === 'localhost') {
	$dbHost = '127.0.0.1';
}
$pdo = new PDO(
	'mysql:host=' . $dbHost . ';dbname=' . $cfg->db . ';charset=utf8',
	$cfg->user,
	$cfg->password,
	array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION)
);

$adminCookie = '';
if ($asAdmin) {
	$st = $pdo->prepare(
		'SELECT s.`session`, s.`user_id` FROM `sessions` s
		 WHERE s.`type` = 1 ORDER BY s.`last_activiti_time` DESC LIMIT 1'
	);
	$st->execute();
	$row = $st->fetch(PDO::FETCH_ASSOC);
	if ($row && !empty($row['session'])) {
		$_COOKIE['admin_session'] = (string) $row['session'];
		$_COOKIE['admin_u_id'] = (string) (int) ($row['user_id'] ?? 0);
		$adminCookie = 'set';
	}
}

$route = 'control/portal/epc_marketing_broadcast';
$st = $pdo->prepare('SELECT * FROM `content` WHERE `url` = ? AND `is_frontend` = 0 LIMIT 1');
$st->execute(array($route));
$contentRow = $st->fetch(PDO::FETCH_ASSOC);

$mainPhp = '';
if ($contentRow && ($contentRow['content_type'] ?? '') === 'php') {
	$phpPath = str_replace('<backend_dir>', $cfg->backend_dir, $_SERVER['DOCUMENT_ROOT'] . $contentRow['content']);
	$mainPhp = is_file($phpPath) ? (string) file_get_contents($phpPath) : '';
}
$mainPhp = epc_cp_prepare_cp_page_content($mainPhp);

$tplPath = $_SERVER['DOCUMENT_ROOT'] . '/' . $cfg->backend_dir . '/templates/bootstrap_admin/desktop.php';
$tpl = is_file($tplPath) ? (string) file_get_contents($tplPath) : '';
$merged = str_replace('<docpart type="main" name="main" />', $mainPhp, $tpl);
$merged = str_replace('<backend_dir>', $cfg->backend_dir, $merged);
$evalCode = ' ?>' . $merged . '<?php ';

$parseOk = true;
$parseErr = '';
try {
	token_get_all($evalCode, TOKEN_PARSE);
} catch (ParseError $e) {
	$parseOk = false;
	$parseErr = $e->getMessage() . ' @ line ' . $e->getLine();
}

$runOk = true;
$runErr = '';
$runBytes = 0;
$hasHub = false;
$hasFatal = false;
if ($parseOk) {
	$GLOBALS['DP_Config'] = $cfg;
	$GLOBALS['db_link'] = $pdo;
	$_GET['tab'] = 'email';
	ob_start();
	set_error_handler(static function ($errno, $errstr) {
		throw new RuntimeException($errstr, $errno);
	});
	try {
		eval($evalCode);
	} catch (Throwable $e) {
		$runOk = false;
		$runErr = $e->getMessage();
	}
	restore_error_handler();
	$body = (string) ob_get_clean();
	$runBytes = strlen($body);
	$hasHub = stripos($body, 'epc-mb-hub') !== false;
	$hasFatal = stripos($body, 'Fatal error') !== false || stripos($body, 'Parse error') !== false;
}

$directOk = true;
$directErr = '';
$directBytes = 0;
$directHasHub = false;
if ($asAdmin && $adminCookie === 'set') {
	ob_start();
	try {
		global $DP_Config, $db_link;
		$DP_Config = $cfg;
		$db_link = $pdo;
		$_GET['tab'] = 'email';
		$phpPath = str_replace('<backend_dir>', $cfg->backend_dir, $_SERVER['DOCUMENT_ROOT'] . ($contentRow['content'] ?? ''));
		require $phpPath;
		$directBody = (string) ob_get_clean();
		$directBytes = strlen($directBody);
		$directHasHub = stripos($directBody, 'epc-mb-hub') !== false;
	} catch (Throwable $e) {
		ob_end_clean();
		$directOk = false;
		$directErr = $e->getMessage();
	}
}

echo json_encode(array(
	'host' => $host,
	'db' => $cfg->db,
	'admin_simulated' => $adminCookie === 'set',
	'main_php_bytes' => strlen($mainPhp),
	'main_has_gt_question' => preg_match('/\?>/', $mainPhp) === 1,
	'main_has_script' => stripos($mainPhp, '<script') !== false,
	'parse_ok' => $parseOk,
	'parse_error' => $parseErr,
	'eval_run_ok' => $runOk,
	'eval_run_error' => $runErr,
	'eval_html_bytes' => $runBytes,
	'eval_has_hub' => $hasHub,
	'eval_has_fatal' => $hasFatal,
	'direct_require_ok' => $directOk,
	'direct_require_error' => $directErr,
	'direct_bytes' => $directBytes,
	'direct_has_hub' => $directHasHub,
), JSON_UNESCAPED_UNICODE | JSON_PRETTY_PRINT);
