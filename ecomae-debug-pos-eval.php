<?php
declare(strict_types=1);
require_once __DIR__ . '/epc_deploy_auth.php';
epc_deploy_require_token();
error_reporting(E_ALL);
ini_set('display_errors', '1');
header('Content-Type: text/plain; charset=utf-8');

$_SERVER['HTTP_HOST'] = 'www.ecomae.com';
$_SERVER['SERVER_NAME'] = 'www.ecomae.com';
$_SERVER['REQUEST_URI'] = '/cp/shop/pos/terminal';
$_SERVER['DOCUMENT_ROOT'] = __DIR__;
$_SERVER['REQUEST_METHOD'] = 'GET';
$_SERVER['HTTPS'] = 'on';
chdir(__DIR__);

define('_ASTEXE_', 1);
require_once __DIR__ . '/config.php';
$DP_Config = new DP_Config();
require_once __DIR__ . '/content/general_pages/epc_portal.php';
epc_portal_apply_config($DP_Config);

$isFrontMode = 0;
require_once __DIR__ . '/core/dp_helper.php';
require_once __DIR__ . '/core/dp_content.php';
require_once __DIR__ . '/core/dp_module.php';
require_once __DIR__ . '/core/dp_template.php';
require_once __DIR__ . '/core/dp_core.php';

// Programmatic login as platform operator
require_once __DIR__ . '/content/users/dp_user.php';
$pdo = $db_link;
$email = 'taxofin2025@gmail.com';
$st = $pdo->prepare('SELECT `user_id` FROM `users` WHERE `email` = ? LIMIT 1');
$st->execute(array($email));
$userId = (int) $st->fetchColumn();
if ($userId <= 0) {
	exit("user not found\n");
}
$session = bin2hex(random_bytes(16));
$now = time();
$pdo->prepare('INSERT INTO `sessions` (`session`, `type`, `user_id`, `time_created`, `time_last_activity`) VALUES (?, 1, ?, ?, ?)')->execute(array($session, $userId, $now, $now));
$_COOKIE['admin_session'] = $session;
$_COOKIE['admin_u_id'] = (string) $userId;

require_once __DIR__ . '/cp/epc_cp_auth_gate.php';
epc_cp_auth_gate_run();

echo "=== POS eval (logged in uid={$userId}) ===\n";

$core = file_get_contents(__DIR__ . '/core/dp_core.php');
$core = preg_replace('/^<\?php\s*/', '', $core, 1);
$core = preg_replace('/eval\(" \?\>" \. \$DP_Template->html \. "<\?php "\);\s*\$db_link = NULL;\s*\?\>\s*$/s', '', $core);

try {
	eval($core);
} catch (Throwable $e) {
	echo 'EVAL FAIL: ' . $e->getMessage() . "\n" . $e->getTraceAsString() . "\n";
	exit;
}

echo 'content_url=' . ($DP_Content->url ?? '') . "\n";
echo 'bytes=' . strlen($DP_Template->html) . "\n";
foreach (array('epc-pos-wrap', 'epc-pos-app', 'Fatal error', 'login_form', 'POS Terminal', 'Database connection failed') as $m) {
	echo $m . '=' . (stripos($DP_Template->html, $m) !== false ? 'yes' : 'no') . "\n";
}
if (stripos($DP_Template->html, 'Fatal error') !== false || stripos($DP_Template->html, 'Uncaught') !== false) {
	echo "\n" . substr(strip_tags($DP_Template->html), 0, 2000) . "\n";
}
