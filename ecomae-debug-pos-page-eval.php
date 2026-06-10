<?php
declare(strict_types=1);
require_once __DIR__ . '/epc_deploy_auth.php';
epc_deploy_require_token();
error_reporting(E_ALL);
ini_set('display_errors', '1');
header('Content-Type: text/plain; charset=utf-8');

define('_ASTEXE_', 1);
require_once __DIR__ . '/config.php';
$DP_Config = new DP_Config();
$GLOBALS['DP_Config'] = $DP_Config;
require_once __DIR__ . '/content/general_pages/epc_portal.php';
epc_portal_apply_config($DP_Config);

$pdo = new PDO(
	'mysql:host=' . $DP_Config->host . ';dbname=' . $DP_Config->db . ';charset=utf8',
	$DP_Config->user,
	$DP_Config->password,
	array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION)
);
$db_link = $pdo;
$GLOBALS['db_link'] = $pdo;

require_once __DIR__ . '/content/users/dp_user.php';
$st = $pdo->prepare('SELECT `user_id` FROM `users` WHERE `email` = ? LIMIT 1');
$st->execute(array('taxofin2025@gmail.com'));
$userId = (int) $st->fetchColumn();
$session = bin2hex(random_bytes(16));
$csrf = bin2hex(random_bytes(16));
$now = time();
$pdo->prepare(
	'INSERT INTO `sessions` (`session`, `user_id`, `time`, `data`, `type`, `contact_type`, `csrf_guard_key`) VALUES (?,?,?,?,?,?,?)'
)->execute(array($session, $userId, $now, '', 1, 'email', $csrf));
$_COOKIE['admin_session'] = $session;
$_COOKIE['admin_u_id'] = (string) $userId;
$user_session = DP_User::getAdminSession();

echo "uid={$userId} session=" . (is_array($user_session) ? 'yes' : 'no') . "\n";

$page = __DIR__ . '/cp/content/shop/pos/epc_pos_terminal_page.php';
$raw = (string) file_get_contents($page);
$template = '<div class="row">' . $raw . '</div>';

register_shutdown_function(function () {
	$e = error_get_last();
	if ($e && in_array($e['type'], array(E_ERROR, E_PARSE, E_CORE_ERROR, E_COMPILE_ERROR), true)) {
		echo 'shutdown_fatal: ' . $e['message'] . ' @ ' . $e['file'] . ':' . $e['line'] . "\n";
	}
});

ob_start();
try {
	eval('?>' . $template . '<?php ');
	$out = ob_get_clean();
	echo 'eval_bytes=' . strlen($out) . ' pos_wrap=' . (stripos($out, 'epc-pos-wrap') !== false ? 'yes' : 'no') . "\n";
	if (stripos($out, 'Fatal') !== false) {
		echo substr(strip_tags($out), 0, 1500) . "\n";
	}
} catch (Throwable $e) {
	ob_end_clean();
	echo 'throw: ' . $e->getMessage() . "\n";
}
