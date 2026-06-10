<?php
/**
 * Probe Super CP Modern auth settings page (500 / missing deps / content row).
 * GET https://www.ecomae.com/epc-probe-cp-auth-settings.php?token=epartscart-deploy-2026
 */
declare(strict_types=1);

require_once __DIR__ . '/epc_deploy_auth.php';
epc_deploy_require_token();
error_reporting(E_ALL);
ini_set('display_errors', '1');
header('Content-Type: text/plain; charset=utf-8');

define('_ASTEXE_', 1);
require_once __DIR__ . '/config.php';
$DP_Config = new DP_Config();
require_once __DIR__ . '/content/general_pages/epc_portal.php';
epc_portal_apply_config($DP_Config);

$backend = (string) ($DP_Config->backend_dir ?? 'cp');
$authPhp = __DIR__ . '/' . $backend . '/content/control/portal/epc_cp_auth_settings.php';
$guardPhp = __DIR__ . '/' . $backend . '/content/control/epc_cp_page_guard.php';

echo "=== Probe Modern auth settings ===\n";
echo 'auth_php: ' . (is_file($authPhp) ? 'yes' : 'NO') . " {$authPhp}\n";
if (is_file($authPhp)) {
	$tail = substr((string) file_get_contents($authPhp), -80);
	echo 'auth_php_tail: ' . str_replace(array("\r", "\n"), array('', '\\n'), $tail) . "\n";
}
echo 'guard_php: ' . (is_file($guardPhp) ? 'yes' : 'NO') . "\n";

$deps = array(
	'epc_portal.php' => __DIR__ . '/content/general_pages/epc_portal.php',
	'epc_auth_common.php' => __DIR__ . '/content/general_pages/epc_auth_common.php',
	'epc_auth_social.php' => __DIR__ . '/content/general_pages/epc_auth_social.php',
	'epc_auth_email_otp.php' => __DIR__ . '/content/general_pages/epc_auth_email_otp.php',
	'epc_auth_smtp.php' => __DIR__ . '/content/general_pages/epc_auth_smtp.php',
);
foreach ($deps as $name => $path) {
	echo "dep {$name}: " . (is_file($path) ? 'yes' : 'MISSING') . "\n";
}

try {
	$pdo = new PDO(
		'mysql:host=' . $DP_Config->host . ';dbname=' . $DP_Config->db . ';charset=utf8',
		$DP_Config->user,
		$DP_Config->password,
		array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION)
	);
} catch (Exception $e) {
	exit('DB: ' . $e->getMessage() . "\n");
}

$contentUrl = 'control/portal/epc_cp_auth_settings';
$st = $pdo->prepare('SELECT `id`, `url`, `published_flag`, `content_type`, `content` FROM `content` WHERE `url` = ? AND `is_frontend` = 0 LIMIT 1');
$st->execute(array($contentUrl));
$row = $st->fetch(PDO::FETCH_ASSOC);
echo 'content_row: ' . ($row ? json_encode($row) : 'MISSING') . "\n";

if ($row) {
	$cid = (int) $row['id'];
	$acc = $pdo->prepare('SELECT `group_id` FROM `content_access` WHERE `content_id` = ?');
	$acc->execute(array($cid));
	$groups = $acc->fetchAll(PDO::FETCH_COLUMN);
	echo 'content_access groups for auth: ' . json_encode(array_map('intval', $groups)) . "\n";
	$ref = $pdo->prepare('SELECT `id` FROM `content` WHERE `url` = ? AND `is_frontend` = 0 LIMIT 1');
	$ref->execute(array('control/portal/epc_tenant_control_center'));
	$refId = (int) $ref->fetchColumn();
	if ($refId > 0) {
		$acc->execute(array($refId));
		$tccGroups = $acc->fetchAll(PDO::FETCH_COLUMN);
		echo 'content_access groups for tenant_control_center: ' . json_encode(array_map('intval', $tccGroups)) . "\n";
	}
}

$prevUri = $_SERVER['REQUEST_URI'] ?? '';
$_SERVER['REQUEST_URI'] = '/' . $backend . '/control/portal/epc_cp_auth_settings';
$_SERVER['REQUEST_METHOD'] = 'GET';
$isSuper = function_exists('epc_portal_is_super_cp_host') && epc_portal_is_super_cp_host();
$_SERVER['REQUEST_URI'] = $prevUri;
echo 'is_super_cp_host: ' . ($isSuper ? 'yes' : 'no') . "\n";

echo "\n--- syntax check ---\n";
foreach (array($authPhp, $guardPhp) as $f) {
	if (!is_file($f)) {
		continue;
	}
	$out = array();
	$code = 0;
	exec('php -l ' . escapeshellarg($f) . ' 2>&1', $out, $code);
	echo basename($f) . ': ' . implode(' ', $out) . "\n";
}

function epc_probe_parse_cookie_jar(string $path): array
{
	$cookies = array();
	if (!is_file($path)) {
		return $cookies;
	}
	foreach (file($path, FILE_IGNORE_NEW_LINES) as $line) {
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

echo "\n--- HTTP logged-in curl ---\n";
$cookieFile = sys_get_temp_dir() . '/epc_auth_probe_' . getmypid() . '.txt';
@unlink($cookieFile);
$loginBody = http_build_query(array(
	'authentication' => 'authentication',
	'auth_contact_select' => 'email',
	'auth_contact' => 'taxofin2025@gmail.com',
	'password' => '12345678',
));
$ch = curl_init('https://www.ecomae.com/cp/');
curl_setopt_array($ch, array(
	CURLOPT_POST => true,
	CURLOPT_POSTFIELDS => $loginBody,
	CURLOPT_RETURNTRANSFER => true,
	CURLOPT_COOKIEJAR => $cookieFile,
	CURLOPT_COOKIEFILE => $cookieFile,
	CURLOPT_SSL_VERIFYPEER => false,
	CURLOPT_TIMEOUT => 60,
));
curl_exec($ch);
$loginCode = (int) curl_getinfo($ch, CURLINFO_HTTP_CODE);
curl_close($ch);
echo "login_http={$loginCode}\n";

echo "\n--- eval smoke (logged-in cookies, template wrap) ---\n";
foreach (epc_probe_parse_cookie_jar($cookieFile) as $k => $v) {
	$_COOKIE[$k] = $v;
}
$db_link = $pdo;
$GLOBALS['DP_Config'] = $DP_Config;
$raw = is_file($authPhp) ? (string) file_get_contents($authPhp) : '';
$template = '<div class="row"><docpart type="main" name="main" /></div>';
$template = str_replace('<docpart type="main" name="main" />', $raw, $template);
register_shutdown_function(function () {
	$e = error_get_last();
	if ($e && in_array($e['type'], array(E_ERROR, E_PARSE, E_CORE_ERROR, E_COMPILE_ERROR), true)) {
		echo 'shutdown_fatal: ' . $e['message'] . ' in ' . $e['file'] . ':' . $e['line'] . "\n";
	}
});
ob_start();
try {
	eval(' ?>' . $template . '<?php ');
	$evalHtml = ob_get_clean();
	echo 'eval_ok bytes=' . strlen($evalHtml) . ' modern=' . (stripos($evalHtml, 'Modern CP authentication') !== false ? 'yes' : 'no') . "\n";
	if (strlen($evalHtml) < 800) {
		echo "eval_snippet:\n" . $evalHtml . "\n";
	}
} catch (Throwable $e) {
	ob_end_clean();
	echo 'eval_error: ' . $e->getMessage() . "\n";
	echo $e->getFile() . ':' . $e->getLine() . "\n";
}

$urls = array(
	'auth_settings' => 'https://www.ecomae.com/cp/control/portal/epc_cp_auth_settings',
	'tenant_control' => 'https://www.ecomae.com/cp/control/portal/epc_tenant_control_center',
);
foreach ($urls as $label => $url) {
	$ch = curl_init($url);
	curl_setopt_array($ch, array(
		CURLOPT_RETURNTRANSFER => true,
		CURLOPT_FOLLOWLOCATION => true,
		CURLOPT_COOKIEJAR => $cookieFile,
		CURLOPT_COOKIEFILE => $cookieFile,
		CURLOPT_SSL_VERIFYPEER => false,
		CURLOPT_TIMEOUT => 90,
	));
	$html = (string) curl_exec($ch);
	$code = (int) curl_getinfo($ch, CURLINFO_HTTP_CODE);
	curl_close($ch);
	echo "{$label}: http={$code} bytes=" . strlen($html);
	echo ' modern_auth=' . (stripos($html, 'Modern CP authentication') !== false ? 'yes' : 'no');
	echo ' tenant_cc=' . (stripos($html, 'Tenant control center') !== false ? 'yes' : 'no');
	echo ' login_form=' . (stripos($html, 'login_form') !== false ? 'yes' : 'no');
	if ($code >= 500 || strlen($html) < 200) {
		echo ' body=' . substr(preg_replace('/\s+/', ' ', $html), 0, 300);
	}
	echo "\n";
}
@unlink($cookieFile);

echo "\nDone.\n";
