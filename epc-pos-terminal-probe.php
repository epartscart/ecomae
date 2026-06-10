<?php
/**
 * Diagnose POS terminal 500 — GET ?token=epartscart-deploy-2026
 */
declare(strict_types=1);
header('Content-Type: text/plain; charset=utf-8');
require_once __DIR__ . '/epc_deploy_auth.php';
if (($_GET['token'] ?? '') !== epc_deploy_token()) {
	exit("Forbidden\n");
}

define('_ASTEXE_', 1);
require_once __DIR__ . '/config.php';
require_once __DIR__ . '/content/general_pages/epc_portal.php';
require_once __DIR__ . '/content/general_pages/epc_portal_tenant.php';
require_once __DIR__ . '/content/shop/pos/epc_pos_helpers.php';

$cfg = new DP_Config();
epc_portal_apply_config($cfg);
$GLOBALS['DP_Config'] = $cfg;

echo "=== POS terminal probe ===\n";
echo 'host=' . ($_SERVER['HTTP_HOST'] ?? '') . "\n";
echo 'config_db=' . $cfg->db . ' user=' . $cfg->user . "\n";

$errors = array();
set_error_handler(function ($no, $str, $file, $line) use (&$errors) {
	$errors[] = "$str @ $file:$line";
	return true;
});

try {
	$pdo = new PDO(
		'mysql:host=' . $cfg->host . ';dbname=' . $cfg->db . ';charset=utf8',
		$cfg->user,
		$cfg->password,
		array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION)
	);
	echo "config_pdo=OK\n";
	epc_pos_ensure_schema($pdo);
	echo "schema=OK\n";
	$settings = epc_pos_get_settings($pdo);
	echo 'pos_enabled=' . (int) ($settings['pos_enabled'] ?? 0) . "\n";
	$stats = epc_pos_dashboard_stats($pdo);
	echo 'today_sales=' . (int) ($stats['today_sales'] ?? 0) . "\n";
	$walkin = epc_pos_ensure_walkin_user($pdo);
	echo 'walkin_user=' . $walkin . "\n";
	$taxCtx = epc_tax_toolkit_resolve($pdo, $walkin);
	echo 'tax_label=' . ($taxCtx['tax_label'] ?? '?') . ' rate=' . ($taxCtx['tax_rate'] ?? '?') . "\n";
} catch (Throwable $e) {
	echo 'FAIL config_db: ' . $e->getMessage() . "\n";
	echo $e->getTraceAsString() . "\n";
}

if (function_exists('epc_portal_tenant_setup_credentials') && function_exists('epc_portal_platform_pdo')) {
	$platformPdo = epc_portal_platform_pdo();
	if ($platformPdo instanceof PDO) {
		foreach (epc_portal_list_tenants($platformPdo) as $row) {
			if ((string) ($row['site_key'] ?? '') !== 'epartscart') {
				continue;
			}
			$cred = epc_portal_tenant_setup_credentials($row);
			echo "\n--- epartscart registry ---\n";
			echo 'registry_db=' . ($cred['db'] ?? '') . ' source=' . ($cred['source'] ?? '') . "\n";
			try {
				$tp = new PDO(
					'mysql:host=' . $cfg->host . ';dbname=' . $cred['db'] . ';charset=utf8',
					$cred['user'] ?: $cfg->user,
					$cred['pass'] ?: $cfg->password,
					array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION)
				);
				epc_pos_ensure_schema($tp);
				$s = epc_pos_get_settings($tp);
				echo 'docpart pos_enabled=' . (int) ($s['pos_enabled'] ?? 0) . "\n";
			} catch (Throwable $e) {
				echo 'docpart FAIL: ' . $e->getMessage() . "\n";
			}
			break;
		}
	}
}

$wrapper = __DIR__ . '/cp/content/shop/pos/epc_pos_terminal_page.php';
$module = __DIR__ . '/cp/content/shop/pos/epc_pos_terminal.php';
echo "\nfiles: page=" . (is_file($wrapper) ? 'yes' : 'no') . ' terminal=' . (is_file($module) ? 'yes' : 'no') . "\n";

// Simulate logged-in CP eval (capture fatal errors)
$cookieFile = sys_get_temp_dir() . '/epc_pos_probe_' . getmypid() . '.txt';
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

$ch = curl_init('https://www.ecomae.com/cp/shop/pos/terminal');
curl_setopt_array($ch, array(
	CURLOPT_RETURNTRANSFER => true,
	CURLOPT_COOKIEJAR => $cookieFile,
	CURLOPT_COOKIEFILE => $cookieFile,
	CURLOPT_SSL_VERIFYPEER => false,
	CURLOPT_TIMEOUT => 90,
));
$html = (string) curl_exec($ch);
$code = (int) curl_getinfo($ch, CURLINFO_HTTP_CODE);
curl_close($ch);
@unlink($cookieFile);

echo "\n--- CP HTTP eval ---\n";
echo 'http=' . $code . ' bytes=' . strlen($html) . "\n";
echo 'has_pos=' . (stripos($html, 'epc-pos-wrap') !== false ? 'yes' : 'no') . "\n";
echo 'has_fatal=' . (stripos($html, 'Fatal error') !== false || stripos($html, 'Uncaught') !== false ? 'yes' : 'no') . "\n";
if ($code >= 400 || stripos($html, 'Fatal error') !== false) {
	echo "snippet:\n" . substr(strip_tags($html), 0, 1200) . "\n";
}

if ($errors) {
	echo "\nPHP notices:\n" . implode("\n", $errors) . "\n";
}

echo "\nDone.\n";
