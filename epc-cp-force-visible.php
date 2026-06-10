<?php
/**
 * Super CP main-pane server probe — confirms live HTML has content markers.
 * GET https://www.ecomae.com/epc-cp-force-visible.php
 * Optional: ?token=epartscart-deploy-2026 for JSON detail
 */
declare(strict_types=1);
define('_ASTEXE_', 1);

$wantJson = isset($_GET['token']);
if ($wantJson) {
	require_once __DIR__ . '/epc_deploy_auth.php';
	if (($_GET['token'] ?? '') !== epc_deploy_token()) {
		http_response_code(403);
		header('Content-Type: application/json; charset=utf-8');
		echo json_encode(array('ok' => false, 'error' => 'forbidden'));
		exit;
	}
	header('Content-Type: application/json; charset=utf-8');
} else {
	header('Content-Type: text/plain; charset=utf-8');
}

$ver = 'unknown';
$shellFile = __DIR__ . '/content/general_pages/epc_cp_professional_shell.php';
if (is_file($shellFile)) {
	require_once $shellFile;
	if (function_exists('epc_cp_shell_css_version')) {
		$ver = epc_cp_shell_css_version();
	}
}

$desktopPath = __DIR__ . '/cp/templates/bootstrap_admin/desktop.php';
$desktopRaw = is_file($desktopPath) ? (string) file_get_contents($desktopPath) : '';
$markers = array(
	'css_version' => $ver,
	'desktop_has_supercpfix1' => stripos($desktopRaw, 'supercpfix1') !== false,
	'desktop_has_nuclear_css' => stripos($desktopRaw, 'epc_cp_nuclear_critical_css') !== false,
	'desktop_has_force_visible' => stripos($desktopRaw, 'epc_cp_force_visible_script') !== false,
	'desktop_has_no_cache_meta' => stripos($desktopRaw, 'Cache-Control') !== false,
	'desktop_bytes' => strlen($desktopRaw),
);

$base = 'https://www.ecomae.com';
$loginUrl = $base . '/cp/';
$probeUrl = $base . '/cp/shop/tenant_hub/tenant_hub?tab=onboard';
$cookieFile = sys_get_temp_dir() . '/epc_cp_force_' . md5((string) getmypid()) . '.txt';
@unlink($cookieFile);

$loginBody = http_build_query(array(
	'authentication' => 'authentication',
	'auth_contact_select' => 'email',
	'auth_contact' => 'taxofin2025@gmail.com',
	'password' => '12345678',
));

$ch = curl_init($loginUrl);
curl_setopt_array($ch, array(
	CURLOPT_POST => true,
	CURLOPT_POSTFIELDS => $loginBody,
	CURLOPT_RETURNTRANSFER => true,
	CURLOPT_FOLLOWLOCATION => false,
	CURLOPT_COOKIEJAR => $cookieFile,
	CURLOPT_COOKIEFILE => $cookieFile,
	CURLOPT_SSL_VERIFYPEER => false,
	CURLOPT_TIMEOUT => 60,
));
curl_exec($ch);
curl_close($ch);

$ch = curl_init($probeUrl);
curl_setopt_array($ch, array(
	CURLOPT_RETURNTRANSFER => true,
	CURLOPT_FOLLOWLOCATION => true,
	CURLOPT_COOKIEJAR => $cookieFile,
	CURLOPT_COOKIEFILE => $cookieFile,
	CURLOPT_SSL_VERIFYPEER => false,
	CURLOPT_TIMEOUT => 90,
));
$html = (string) curl_exec($ch);
$http = (int) curl_getinfo($ch, CURLINFO_HTTP_CODE);
curl_close($ch);
@unlink($cookieFile);

$live = array(
	'http' => $http,
	'bytes' => strlen($html),
	'logged_in' => stripos($html, 'login_form') === false && stripos($html, 'epc-cp-page-header__title') !== false,
	'has_build_comment' => stripos($html, 'epc-cp-build:' . $ver) !== false,
	'has_force_visible_css' => stripos($html, 'epc-cp-body-force-visible') !== false,
	'has_main_pane_class' => stripos($html, 'epc-cp-main-pane') !== false,
	'has_tenant_hub_hero' => stripos($html, 'epc-th-hero') !== false,
	'has_onboard_text' => stripos($html, 'Onboard client') !== false,
	'has_nuclear_js' => stripos($html, 'epc-cp-force-visible-js') !== false,
	'css_version_in_html' => (preg_match('/\?v=([0-9a-z]+)/i', $html, $m) ? $m[1] : null),
);

$ok = $markers['desktop_has_supercpfix1']
	&& !empty($live['logged_in'])
	&& !empty($live['has_main_pane_class'])
	&& (!empty($live['has_tenant_hub_hero']) || !empty($live['has_onboard_text']));

if ($wantJson) {
	echo json_encode(array(
		'ok' => $ok,
		'server' => $markers,
		'live_tenant_hub' => $live,
	), JSON_PRETTY_PRINT | JSON_UNESCAPED_SLASHES);
	exit;
}

echo "EPC Super CP force-visible probe\n";
echo "build: {$ver}\n";
echo 'server_ok: ' . ($markers['desktop_has_supercpfix1'] ? 'YES' : 'NO') . "\n";
echo 'live_logged_in: ' . (!empty($live['logged_in']) ? 'YES' : 'NO') . "\n";
echo 'live_main_pane: ' . (!empty($live['has_main_pane_class']) ? 'YES' : 'NO') . "\n";
echo 'live_tenant_hub_content: ' . ((!empty($live['has_tenant_hub_hero']) || !empty($live['has_onboard_text'])) ? 'YES' : 'NO') . "\n";
echo 'live_build_comment: ' . (!empty($live['has_build_comment']) ? 'YES' : 'NO') . "\n";
echo 'live_css_v: ' . ($live['css_version_in_html'] ?? 'n/a') . "\n";
echo 'overall: ' . ($ok ? 'PASS — server HTML has main content' : 'FAIL — check cache or login') . "\n";
