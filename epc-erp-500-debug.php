<?php
/**
 * Reproduce CP ERP route with live admin session; capture PHP fatal errors.
 * GET: token=epartscart-deploy-2026
 * Optional: route=shop/finance/erp, user_id=5
 */
header('Content-Type: text/plain; charset=utf-8');
if (($_GET['token'] ?? '') !== 'epartscart-deploy-2026') {
	exit('Forbidden');
}

require_once __DIR__ . '/config.php';
$DP_Config = new DP_Config;

$userId = isset($_GET['user_id']) ? (int)$_GET['user_id'] : 5;
$db = new PDO(
	'mysql:host=' . $DP_Config->host . ';dbname=' . $DP_Config->db,
	$DP_Config->user,
	$DP_Config->password,
	array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION)
);
$st = $db->prepare('SELECT `session`, `user_id`, `2fa_session` FROM `sessions` WHERE `user_id` = ? AND `type` = 1 ORDER BY `last_activiti_time` DESC LIMIT 1');
$st->execute(array($userId));
$row = $st->fetch(PDO::FETCH_ASSOC);
if (!$row) {
	exit("No session for user_id={$userId}\n");
}

$_COOKIE['admin_session'] = $row['session'];
$_COOKIE['admin_u_id'] = (string)$row['user_id'];
if (!empty($row['2fa_session'])) {
	$_COOKIE['2fa'] = $row['2fa_session'];
}

$route = isset($_GET['route']) ? (string)$_GET['route'] : 'shop/finance/erp';
$_SERVER['REQUEST_URI'] = '/' . $DP_Config->backend_dir . '/' . $route;
$_GET = array();

register_shutdown_function(function () {
	$e = error_get_last();
	if ($e && in_array($e['type'], array(E_ERROR, E_PARSE, E_CORE_ERROR, E_COMPILE_ERROR), true)) {
		echo "\nSHUTDOWN FATAL: {$e['message']} in {$e['file']}:{$e['line']}\n";
	}
});

$stProbe = $db->prepare('SELECT `id`, `url`, `content` FROM `content` WHERE `url` = ? AND `published_flag` = 1 AND `is_frontend` = 0');
$stProbe->execute(array($route));
$probeRow = $stProbe->fetch(PDO::FETCH_ASSOC);
echo "probe_route={$route} content_id=" . ($probeRow['id'] ?? '') . " path=" . ($probeRow['content'] ?? '') . "\n";
echo "session_user={$row['user_id']}\n";

chdir($_SERVER['DOCUMENT_ROOT'] . '/' . $DP_Config->backend_dir);

ob_start();
try {
	include 'index.php';
} catch (Throwable $t) {
	ob_end_clean();
	exit("Throwable: " . $t->getMessage() . "\n" . $t->getFile() . ':' . $t->getLine() . "\n" . $t->getTraceAsString());
}
$html = ob_get_clean();
echo 'bytes=' . strlen($html) . "\n";
echo 'http_response_code=' . http_response_code() . "\n";
if (strlen($html) < 800) {
	echo $html;
} else {
	echo substr(strip_tags($html), 0, 600) . "\n...\n";
	echo (stripos($html, 'epc-erp-kpi') !== false ? "HAS erp content\n" : "NO erp marker\n");
	echo (stripos($html, 'Log in form') !== false ? "HAS login form\n" : "");
	echo (stripos($html, '4014') !== false || stripos($html, 'privileges') !== false ? "HAS access denied\n" : "");
}
