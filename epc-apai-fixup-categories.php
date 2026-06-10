<?php
declare(strict_types=1);
require_once __DIR__ . '/epc_deploy_auth.php';
epc_deploy_require_token();
header('Content-Type: application/json; charset=utf-8');
define('_ASTEXE_', 1);
require_once __DIR__ . '/config.php';
require_once __DIR__ . '/content/general_pages/epc_portal.php';
require_once __DIR__ . '/content/general_pages/epc_portal_db.php';
require_once __DIR__ . '/content/general_pages/epc_portal_tenant.php';
require_once __DIR__ . '/content/shop/price_engine/epc_auto_price_engine.php';
require_once __DIR__ . '/content/shop/price_engine/epc_auto_price_categories.php';

$cfg = new DP_Config();
epc_portal_apply_config($cfg);
$platformPdo = epc_portal_platform_pdo();
$siteKey = 'electronicae';
foreach (epc_portal_list_tenants($platformPdo) as $t) {
	if ((string) ($t['site_key'] ?? '') !== $siteKey) continue;
	$cred = epc_portal_tenant_setup_credentials($t);
	$pdo = new PDO('mysql:host=' . $cfg->host . ';dbname=' . (string) $cred['db'] . ';charset=utf8', (string) ($cred['user'] ?: $cfg->user), (string) ($cred['pass'] ?: $cfg->password), array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION));
	epc_ape_ensure_schema($pdo);
	$fixed = epc_apai_fixup_imported_products($pdo, $siteKey);
	echo json_encode(array('ok' => true, 'fixed' => $fixed), JSON_PRETTY_PRINT);
	exit;
}
echo json_encode(array('ok' => false));
