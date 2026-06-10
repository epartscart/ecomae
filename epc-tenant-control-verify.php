<?php
/**
 * Verify Tenant control center password UI + AJAX (deploy token).
 * GET https://www.ecomae.com/epc-tenant-control-verify.php?token=epartscart-deploy-2026&site_key=asapcustom
 */
declare(strict_types=1);

require_once __DIR__ . '/epc_deploy_auth.php';
epc_deploy_require_token();
header('Content-Type: application/json; charset=utf-8');

define('_ASTEXE_', 1);
require_once __DIR__ . '/config.php';
require_once __DIR__ . '/content/general_pages/epc_portal.php';
require_once __DIR__ . '/content/general_pages/epc_portal_tenant_control.php';

$siteKey = preg_replace('/[^a-z0-9_]/', '', strtolower(trim((string) ($_GET['site_key'] ?? 'asapcustom'))));
$out = array('ok' => true, 'site_key' => $siteKey, 'checks' => array());

$backend = 'cp';
$cfg = new DP_Config();
epc_portal_apply_config($cfg);
$backend = (string) ($cfg->backend_dir ?? 'cp');

$tccPhp = __DIR__ . '/' . $backend . '/content/control/portal/epc_tenant_control_center.php';
$ajaxPhp = __DIR__ . '/' . $backend . '/content/control/portal/ajax_portal.php';
$out['checks']['tcc_php'] = is_file($tccPhp);
$out['checks']['ajax_php'] = is_file($ajaxPhp);

if (is_file($tccPhp)) {
	$src = (string) file_get_contents($tccPhp);
	$out['checks']['uses_data_ajax_url'] = stripos($src, 'data-ajax-url') !== false;
	$out['checks']['no_php_in_script_block'] = !preg_match('/<script[^>]*>[\s\S]*?<\?php/i', $src);
}

$pdo = epc_portal_platform_pdo();
if (!$pdo instanceof PDO) {
	$out['ok'] = false;
	$out['checks']['platform_pdo'] = false;
	echo json_encode($out, JSON_PRETTY_PRINT | JSON_UNESCAPED_UNICODE);
	exit;
}
$out['checks']['platform_pdo'] = true;

$row = epc_portal_tenant_control_get_row($pdo, $siteKey);
$out['checks']['registry_row'] = is_array($row);
if (is_array($row)) {
	$connect = epc_portal_tenant_control_tenant_pdo_connect($row);
	$out['checks']['tenant_db_ok'] = $connect['pdo'] instanceof PDO;
	if ($connect['pdo'] instanceof PDO) {
		if (!empty($_GET['reset'])) {
			$reset = epc_portal_tenant_control_reset_cp_password($pdo, $siteKey);
			$out['checks']['reset_cp_password'] = !empty($reset['ok']);
			$out['checks']['reset_password_len'] = strlen((string) ($reset['password'] ?? ''));
			$out['reset'] = array(
				'email' => (string) ($reset['email'] ?? ''),
				'message' => (string) ($reset['message'] ?? ''),
			);
		} else {
			$load = epc_portal_tenant_control_demo_access_load($pdo, $siteKey);
			$out['checks']['demo_access_load'] = !empty($load['ok']);
			$out['checks']['has_stored_password'] = !empty($load['has_stored_password']);
		}
	} else {
		$out['checks']['tenant_db_error'] = (string) ($connect['error'] ?? '');
		$out['ok'] = false;
	}
} else {
	$out['ok'] = false;
}

$cookieFile = sys_get_temp_dir() . '/epc_tcc_verify_' . getmypid() . '.txt';
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

$ch = curl_init('https://www.ecomae.com/cp/control/portal/epc_tenant_control_center');
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
@unlink($cookieFile);

$out['checks']['page_http'] = $code;
$out['checks']['page_has_demo_panel'] = stripos($html, 'Demo access control') !== false;
$out['checks']['page_data_ajax_url'] = preg_match('/id="epc-tenant-control-center"[^>]*data-ajax-url="/', $html) === 1
	|| preg_match('/data-ajax-url="[^"]+"[^>]*id="epc-tenant-control-center"/', $html) === 1;
$out['checks']['page_php_leak_in_script'] = preg_match('/var ajaxUrl = <\?php/i', $html) === 1
	|| stripos($html, '<?php echo json_encode($ajaxUrl') !== false;
$out['checks']['page_script_has_post_fn'] = stripos($html, 'tenant_demo_access_save') !== false;

if ($out['checks']['page_php_leak_in_script']) {
	$out['ok'] = false;
}
if (!$out['checks']['page_data_ajax_url']) {
	$out['ok'] = false;
}

echo json_encode($out, JSON_PRETTY_PRINT | JSON_UNESCAPED_UNICODE);
