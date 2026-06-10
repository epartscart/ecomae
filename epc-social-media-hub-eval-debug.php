<?php
/**
 * Pinpoint eval parse failure for social media hub CP route.
 */
declare(strict_types=1);

require_once __DIR__ . '/epc_deploy_auth.php';
epc_deploy_require_token();
header('Content-Type: application/json; charset=utf-8');

define('_ASTEXE_', 1);
require_once __DIR__ . '/config.php';
$cfg = new DP_Config();

$route = 'control/portal/epc_social_media_hub';
$db = new PDO(
	'mysql:host=' . $cfg->host . ';dbname=' . $cfg->db,
	$cfg->user,
	$cfg->password
);
$st = $db->prepare('SELECT * FROM `content` WHERE `url` = ? AND `is_frontend` = 0 LIMIT 1');
$st->execute(array($route));
$row = $st->fetch(PDO::FETCH_ASSOC);

$mainPhp = '';
if ($row && ($row['content_type'] ?? '') === 'php') {
	$phpPath = str_replace('<backend_dir>', $cfg->backend_dir, $_SERVER['DOCUMENT_ROOT'] . $row['content']);
	$mainPhp = is_file($phpPath) ? (string) file_get_contents($phpPath) : '';
}

$relocate = $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_cp_script_relocate.php';
if (is_file($relocate)) {
	require_once $relocate;
	if (function_exists('epc_cp_prepare_cp_page_content')) {
		$mainPhp = epc_cp_prepare_cp_page_content($mainPhp);
	}
}

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
if ($parseOk) {
	$_SERVER['HTTP_HOST'] = 'www.ecomae.com';
	$GLOBALS['DP_Config'] = $cfg;
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
	$runBytes = strlen((string) ob_get_clean());
}

echo json_encode(array(
	'main_php_bytes' => strlen($mainPhp),
	'merged_bytes' => strlen($merged),
	'main_has_script' => stripos($mainPhp, '<script') !== false,
	'main_has_gt_question' => preg_match('/\?>/', $mainPhp) === 1,
	'parse_ok' => $parseOk,
	'parse_error' => $parseErr,
	'run_ok' => $runOk,
	'run_error' => $runErr,
	'run_html_bytes' => $runBytes,
	'main_preview' => substr($mainPhp, 0, 220),
), JSON_UNESCAPED_UNICODE | JSON_PRETTY_PRINT);
