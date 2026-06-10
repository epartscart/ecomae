<?php
/**
 * Bootstrap /cp/control/portal/epc_cp_auth_settings with admin session — surface eval/runtime errors.
 * GET https://www.ecomae.com/epc-probe-auth-bootstrap.php?token=epartscart-deploy-2026
 */
declare(strict_types=1);

require_once __DIR__ . '/epc_deploy_auth.php';
epc_deploy_require_token();
error_reporting(E_ALL);
ini_set('display_errors', '1');
header('Content-Type: text/plain; charset=utf-8');

function epc_probe_parse_cookie_jar(string $path): array
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

$cookieFile = sys_get_temp_dir() . '/epc_auth_boot_' . getmypid() . '.txt';
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
curl_close($ch);

foreach (epc_probe_parse_cookie_jar($cookieFile) as $k => $v) {
	$_COOKIE[$k] = $v;
}
@unlink($cookieFile);

$_SERVER['HTTP_HOST'] = 'www.ecomae.com';
$_SERVER['SERVER_NAME'] = 'www.ecomae.com';
$_SERVER['HTTPS'] = 'on';
$_SERVER['REQUEST_METHOD'] = 'GET';
$_SERVER['REQUEST_URI'] = '/cp/control/portal/epc_cp_auth_settings';
$_SERVER['DOCUMENT_ROOT'] = __DIR__;

echo "=== Bootstrap cp/index for auth settings ===\n";
echo 'admin_session=' . (isset($_COOKIE['admin_session']) ? 'set' : 'missing') . "\n";

register_shutdown_function(function () {
	$e = error_get_last();
	if ($e) {
		echo 'shutdown: [' . $e['type'] . '] ' . $e['message'] . ' @ ' . $e['file'] . ':' . $e['line'] . "\n";
	}
});

ob_start();
try {
	require __DIR__ . '/cp/index.php';
	$html = ob_get_clean();
	echo 'bytes=' . strlen($html) . "\n";
	echo 'modern_auth=' . (stripos($html, 'Modern CP authentication') !== false ? 'yes' : 'no') . "\n";
	echo 'footer=' . (stripos($html, '<footer class="footer">') !== false ? 'yes' : 'no') . "\n";
	if (stripos($html, 'Modern CP authentication') === false) {
		$pos = stripos($html, 'epc-cp-main-pane');
		echo 'main_snippet=' . substr(preg_replace('/\s+/', ' ', $pos !== false ? substr($html, $pos, 1200) : $html), 0, 600) . "\n";
	}
} catch (Throwable $e) {
	ob_end_clean();
	echo 'throw: ' . $e->getMessage() . "\n" . $e->getFile() . ':' . $e->getLine() . "\n";
}

echo "Done.\n";
