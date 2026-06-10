<?php
/**
 * Verify ASAP shared ERP shell can render (tenant PDO, user #19, erp_main include).
 * GET: token=epartscart-deploy-2026
 */
declare(strict_types=1);
require_once __DIR__ . '/epc_deploy_auth.php';
epc_deploy_require_token();
header('Content-Type: application/json; charset=utf-8');

$out = array('status' => true, 'checks' => array());

require_once __DIR__ . '/config.php';
require_once __DIR__ . '/content/general_pages/epc_portal.php';
require_once __DIR__ . '/content/general_pages/epc_portal_shared_erp.php';

$cfgFile = '/home/ecomae/htdocs/www.ecomae.com/config.local.php';
$epc_config_local = null;
include $cfgFile;
$platDb = (string) ($epc_config_local['db'] ?? 'ecomae');
$platUser = (string) ($epc_config_local['user'] ?? 'ecomae');
$platPass = (string) ($epc_config_local['password'] ?? '');
$platformPdo = new PDO('mysql:host=127.0.0.1;dbname=' . $platDb . ';charset=utf8', $platUser, $platPass);

$row = epc_portal_shared_erp_load_by_site_key('asap', $platformPdo);
$out['checks']['registry'] = $row ? array(
	'site_key' => $row['site_key'],
	'db_name' => $row['db_name'],
	'trade_name' => $row['trade_name'],
	'db_user_set' => trim((string) ($row['db_user'] ?? '')) !== '',
	'db_password_set' => trim((string) ($row['db_password'] ?? '')) !== '',
) : null;

$tenantPdo = ($row && is_array($row)) ? epc_portal_shared_erp_tenant_pdo($row) : null;
$out['checks']['tenant_pdo'] = $tenantPdo instanceof PDO;

if ($tenantPdo instanceof PDO) {
	$ust = $tenantPdo->prepare('SELECT user_id, email FROM users WHERE user_id = 19 LIMIT 1');
	$ust->execute();
	$out['checks']['user19'] = $ust->fetch(PDO::FETCH_ASSOC) ?: 'NOT_FOUND';
	$out['checks']['shop_orders'] = (int) $tenantPdo->query('SELECT COUNT(*) FROM shop_orders')->fetchColumn();
}

$_COOKIE['epc_erp_tenant'] = 'asap';
$_COOKIE['admin_u_id'] = '19';
if ($tenantPdo instanceof PDO) {
	$st = $tenantPdo->prepare('SELECT session FROM sessions WHERE user_id = 19 AND type = 1 ORDER BY id DESC LIMIT 1');
	$st->execute();
	$sess = (string) $st->fetchColumn();
	if ($sess !== '') {
		$_COOKIE['admin_session'] = $sess;
	}
}
$out['checks']['session_cookie'] = !empty($_COOKIE['admin_session']);
$out['checks']['shared_session_valid'] = epc_portal_shared_erp_session_valid($row);
$out['checks']['active_tenant'] = epc_portal_shared_erp_active_tenant($platformPdo);

$DP_Config = new DP_Config();
epc_portal_apply_config($DP_Config);
$out['checks']['config_db'] = (string) ($DP_Config->db ?? '');

if ($tenantPdo instanceof PDO) {
	$db_link = $tenantPdo;
	$GLOBALS['DP_Config'] = $DP_Config;
	$GLOBALS['db_link'] = $tenantPdo;
	$GLOBALS['epc_erp_standalone'] = true;
	$_GET['epc_erp_shell'] = '1';
	require_once __DIR__ . '/content/shop/finance/epc_erp_cp_shell.php';
	$mainDisk = __DIR__ . '/cp/content/shop/finance/erp/erp_main_page.php';
	ob_start();
	$err = null;
	try {
		define('_ASTEXE_', 1);
		include $mainDisk;
	} catch (Throwable $e) {
		$err = $e->getMessage();
	}
	$html = ob_get_clean();
	$out['checks']['render_error'] = $err;
	$out['checks']['html_bytes'] = strlen($html);
	$out['checks']['has_sidebar'] = (stripos($html, 'epc-erp-sidebar') !== false);
	$out['checks']['has_dashboard'] = (stripos($html, 'Finance overview') !== false || stripos($html, 'epc-erp-kpi') !== false);
	$out['checks']['isolation_block'] = (stripos($html, 'epc-erp-isolation-block') !== false);
}

echo json_encode($out, JSON_PRETTY_PRINT | JSON_UNESCAPED_UNICODE);
