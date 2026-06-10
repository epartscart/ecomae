<?php
/**
 * One-off probe: ERP URL prefix for demo ERP-only (delete after verify).
 * GET ?token=…&site_key=demo_260602_eo
 */
declare(strict_types=1);

$token = isset($_GET['token']) ? (string) $_GET['token'] : '';
if ($token !== 'epartscart-deploy-2026') {
	http_response_code(403);
	exit('forbidden');
}

require_once __DIR__ . '/config.php';
$DP_Config = new DP_Config();

require_once __DIR__ . '/content/general_pages/epc_portal.php';
require_once __DIR__ . '/content/general_pages/epc_portal_db.php';
epc_portal_apply_config($DP_Config);
require_once __DIR__ . '/content/general_pages/epc_portal_demo.php';

$siteKey = preg_replace('/[^a-z0-9_]/', '', strtolower((string) ($_GET['site_key'] ?? 'demo_260602_eo')));
$row = epc_portal_demo_load_live_row($siteKey);
if ($row === null) {
	exit("no row for {$siteKey}\n");
}

$GLOBALS['epc_demo_cp_context'] = true;
$GLOBALS['epc_demo_cp_site_key'] = $siteKey;
$GLOBALS['epc_demo_cp_tenant_row'] = $row;
epc_portal_demo_apply_cp_config($DP_Config, $row, $siteKey);

require_once __DIR__ . '/content/shop/finance/epc_erp_access.php';
require_once __DIR__ . '/content/shop/finance/epc_erp_cp_shell.php';

$urls = epc_erp_configure_portal_urls('cp');
header('Content-Type: text/plain; charset=utf-8');
echo "site_key={$siteKey}\n";
echo "erp_only=" . (epc_portal_demo_cp_is_erp_only() ? '1' : '0') . "\n";
echo "erpUrl=" . ($urls['erpUrl'] ?? '') . "\n";
echo "erpAjaxUrl=" . ($urls['erpAjaxUrl'] ?? '') . "\n";
echo "shell_query=" . epc_erp_shell_url_query() . "\n";
$bad = (strpos($urls['erpUrl'], '/cp/demo/' . $siteKey . '/') === 0) ? 'ok' : 'FAIL';
echo "prefix_check={$bad}\n";

echo 'login_email=' . trim((string) ($row['demo_contact_email'] ?? '')) . "\n";
echo 'login_pass=' . trim((string) ($row['operator_temp_password'] ?? '')) . "\n";

if (!empty($_GET['reset_test_pass']) && (string) $_GET['reset_test_pass'] === '1') {
	$email = trim((string) ($row['demo_contact_email'] ?? ''));
	$pass = 'EpcDemoTest99!';
	$tenantPdo = epc_portal_demo_tenant_pdo($row);
	if ($email !== '' && $tenantPdo instanceof PDO) {
		epc_portal_demo_create_cp_user($tenantPdo, $email, $pass, 'ERP probe');
		echo "password_reset_to={$pass}\n";
	}
}
