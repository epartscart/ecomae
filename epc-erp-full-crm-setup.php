<?php
/**
 * Full CRM schema (phases 1–7) + hide standalone /shop/crm routes.
 * https://www.ecomae.com/epc-erp-full-crm-setup.php?token=epartscart-deploy-2026
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
require_once __DIR__ . '/content/general_pages/epc_portal.php';

$cfg = new DP_Config();
if (function_exists('epc_portal_apply_config')) {
	epc_portal_apply_config($cfg);
}
$pdo = new PDO('mysql:host=' . $cfg->host . ';dbname=' . $cfg->db . ';charset=utf8', $cfg->user, $cfg->password);
$pdo->setAttribute(PDO::ATTR_ERRMODE, PDO::ERRMODE_EXCEPTION);

epc_crm_ensure_schema($pdo);
epc_portal_db_ensure($pdo);

$pdo->exec(
	"UPDATE `content` SET `published_flag` = 0
	 WHERE `is_frontend` = 0 AND (`url` = 'shop/crm' OR `url` LIKE 'shop/crm/%')"
);
$pdo->exec("DELETE FROM `control_items` WHERE `url` LIKE '%/shop/crm/%' OR `caption` IN ('epc_crm_cp', 'epc_cp_group_crm')");

$host = isset($_SERVER['HTTP_HOST']) ? strtolower((string)$_SERVER['HTTP_HOST']) : '';
$settings = epc_portal_load_site_settings_for_host($pdo, $host ?: 'www.epartscart.com');
$packs = isset($settings['enabled_packs']) && is_array($settings['enabled_packs']) ? $settings['enabled_packs'] : array();
$packs = array_values(array_unique(array_diff($packs, array('crm'))));
foreach (array('erp', 'professional') as $p) {
	if (!in_array($p, $packs, true)) {
		$packs[] = $p;
	}
}
$settings['enabled_packs'] = $packs;
epc_portal_save_site_settings($pdo, $settings);

$tables = array(
	'epc_crm_leads', 'epc_crm_opportunities', 'epc_crm_activities', 'epc_crm_quotes', 'epc_crm_quote_lines',
	'epc_crm_tickets', 'epc_crm_ticket_messages', 'epc_crm_projects', 'epc_crm_project_tasks',
	'epc_crm_contracts', 'epc_crm_expenses',
);
$counts = array();
foreach ($tables as $t) {
	try {
		$counts[$t] = (int)$pdo->query('SELECT COUNT(*) FROM `' . $t . '`')->fetchColumn();
	} catch (Exception $e) {
		$counts[$t] = -1;
	}
}

echo "epc-erp-full-crm-setup OK\n";
echo "host: {$host}\n";
echo "db: " . $cfg->db . "\n";
echo "packs: " . implode(', ', $packs) . "\n";
echo "rows: " . json_encode($counts) . "\n";
echo "CRM UI: /" . $cfg->backend_dir . "/shop/finance/erp?tab=crm&crm_tab=pipeline\n";
