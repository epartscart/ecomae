<?php
/**
 * One-shot: CRM schema + portal pack hint + ERP integration check.
 * https://www.epartscart.com/epc-erp-crm-setup.php?token=epartscart-deploy-2026
 */
error_reporting(E_ALL);
ini_set('display_errors', 1);
header('Content-Type: text/plain; charset=utf-8');

require_once __DIR__ . '/epc_deploy_auth.php';
epc_deploy_require_token();

define('_ASTEXE_', 1);
require_once __DIR__ . '/config.php';
require_once __DIR__ . '/content/shop/finance/epc_crm_schema.php';
require_once __DIR__ . '/content/general_pages/epc_portal_db.php';

$cfg = new DP_Config();
$pdo = new PDO('mysql:host=' . $cfg->host . ';dbname=' . $cfg->db . ';charset=utf8', $cfg->user, $cfg->password);
$pdo->setAttribute(PDO::ATTR_ERRMODE, PDO::ERRMODE_EXCEPTION);

epc_crm_ensure_schema($pdo);
epc_portal_db_ensure($pdo);

$host = isset($_SERVER['HTTP_HOST']) ? strtolower((string)$_SERVER['HTTP_HOST']) : 'www.epartscart.com';
$settings = epc_portal_load_site_settings_for_host($pdo, $host);
$packs = isset($settings['enabled_packs']) && is_array($settings['enabled_packs']) ? $settings['enabled_packs'] : array();
$changed = false;
foreach (array('crm', 'professional', 'erp') as $p) {
	if (!in_array($p, $packs, true)) {
		if ($p === 'crm') {
			$packs[] = 'crm';
			$changed = true;
		}
	}
}
if ($changed) {
	$settings['enabled_packs'] = $packs;
	epc_portal_save_site_settings($pdo, $settings);
	echo "Added 'crm' to enabled_packs for {$host}\n";
} else {
	echo "Portal packs OK (crm or professional/erp present): " . implode(', ', $packs) . "\n";
}

$counts = array(
	'leads' => (int)$pdo->query('SELECT COUNT(*) FROM `epc_crm_leads`')->fetchColumn(),
	'opportunities' => (int)$pdo->query('SELECT COUNT(*) FROM `epc_crm_opportunities`')->fetchColumn(),
	'activities' => (int)$pdo->query('SELECT COUNT(*) FROM `epc_crm_activities`')->fetchColumn(),
);
echo "CRM rows: " . json_encode($counts) . "\n";
echo "Run epc-crm-cp-setup.php if CP route /shop/crm/crm is missing.\n";
echo "CP URL: /" . $cfg->backend_dir . "/shop/crm/crm\n";
echo "ERP link: /" . $cfg->backend_dir . "/shop/finance/erp (CRM tab)\n";
