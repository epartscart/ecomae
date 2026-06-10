<?php
/**
 * POS terminal authenticated eval probe — GET ?token=epartscart-deploy-2026
 */
declare(strict_types=1);
require_once __DIR__ . '/epc_deploy_auth.php';
epc_deploy_require_token();
error_reporting(E_ALL);
ini_set('display_errors', '1');
header('Content-Type: text/plain; charset=utf-8');

function epc_pos_probe_parse_cookie_jar(string $path): array
{
	$cookies = array();
	if (!is_file($path)) {
		return $cookies;
	}
	foreach (file($path, FILE_IGNORE_NEW_LINES) as $line) {
		if (strpos($line, '#HttpOnly_') === 0) {
			$line = substr($line, 10);
		}
		if ($line === '' || $line[0] === '#') {
			continue;
		}
		$parts = explode("\t", $line);
		if (count($parts) >= 7) {
			$cookies[$parts[5]] = $parts[6];
		}
	}
	return $cookies;
}

$_SERVER['HTTP_HOST'] = 'www.ecomae.com';
$_SERVER['SERVER_NAME'] = 'www.ecomae.com';
$_SERVER['HTTPS'] = 'on';
$_SERVER['REQUEST_URI'] = '/cp/shop/pos/terminal';
$_SERVER['REQUEST_METHOD'] = 'GET';
$_SERVER['DOCUMENT_ROOT'] = __DIR__;
chdir(__DIR__);

echo "=== POS auth eval probe ===\n";

register_shutdown_function(function () {
	$e = error_get_last();
	if ($e && in_array($e['type'], array(E_ERROR, E_PARSE, E_CORE_ERROR, E_COMPILE_ERROR), true)) {
		echo 'shutdown_fatal: ' . $e['message'] . ' in ' . $e['file'] . ':' . $e['line'] . "\n";
	}
});

define('_ASTEXE_', 1);
require_once __DIR__ . '/config.php';
$DP_Config = new DP_Config();
$GLOBALS['DP_Config'] = $DP_Config;
require_once __DIR__ . '/content/general_pages/epc_portal.php';
epc_portal_apply_config($DP_Config);

try {
	$pdo = new PDO(
		'mysql:host=' . $DP_Config->host . ';dbname=' . $DP_Config->db . ';charset=utf8',
		$DP_Config->user,
		$DP_Config->password,
		array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION)
	);
} catch (Throwable $e) {
	exit('db_connect_fail: ' . $e->getMessage() . "\n");
}
$db_link = $pdo;
$GLOBALS['db_link'] = $pdo;

require_once __DIR__ . '/content/users/dp_user.php';
$email = 'taxofin2025@gmail.com';
$st = $pdo->prepare('SELECT `user_id` FROM `users` WHERE `email` = ? LIMIT 1');
$st->execute(array($email));
$userId = (int) $st->fetchColumn();
if ($userId <= 0) {
	exit("user_not_found\n");
}
$session = bin2hex(random_bytes(16));
$now = time();
$csrf = bin2hex(random_bytes(16));
$pdo->prepare(
	'INSERT INTO `sessions` (`session`, `user_id`, `time`, `data`, `type`, `contact_type`, `csrf_guard_key`) VALUES (?,?,?,?,?,?,?)'
)->execute(array($session, $userId, $now, '', 1, 'email', $csrf));
$_COOKIE['admin_session'] = $session;
$_COOKIE['admin_u_id'] = (string) $userId;
echo 'programmatic_login uid=' . $userId . ' isAdmin=' . (DP_User::isAdmin() ? 'yes' : 'no') . "\n";

$isFrontMode = 0;
require_once __DIR__ . '/core/dp_helper.php';
require_once __DIR__ . '/core/dp_content.php';
require_once __DIR__ . '/core/dp_module.php';
require_once __DIR__ . '/core/dp_template.php';

echo "booting dp_core...\n";
ob_start();
try {
	require __DIR__ . '/core/dp_core.php';
	$html = ob_get_clean();
} catch (Throwable $e) {
	$html = ob_get_clean();
	echo 'dp_core_throw: ' . $e->getMessage() . "\n" . $e->getTraceAsString() . "\n";
	exit;
}

echo 'content_url=' . ($DP_Content->url ?? '') . "\n";
echo 'bytes=' . strlen($html) . "\n";
foreach (array('epc-pos-wrap', 'epc-pos-app', 'POS Terminal', 'Fatal error', 'Uncaught', 'login_form', 'Database connection failed') as $m) {
	echo $m . '=' . (stripos($html, $m) !== false ? 'yes' : 'no') . "\n";
}
if (stripos($html, 'Fatal error') !== false || stripos($html, 'Uncaught') !== false) {
	echo "\n" . substr(strip_tags($html), 0, 2500) . "\n";
} elseif (strlen($html) < 500) {
	echo "\nhtml:\n" . $html . "\n";
}

echo "\nDone.\n";
