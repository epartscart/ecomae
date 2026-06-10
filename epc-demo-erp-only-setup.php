<?php
/**
 * Repair / seed ERP-only demo sandboxes (settings, ERP CP content, packs sync).
 *
 * Run: https://www.ecomae.com/epc-demo-erp-only-setup.php?token=epartscart-deploy-2026&apply=1&site_key=demo_260602_eo
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
require_once __DIR__ . '/content/general_pages/epc_portal_db.php';
require_once __DIR__ . '/content/general_pages/epc_portal_tenant.php';
require_once __DIR__ . '/content/general_pages/epc_portal_demo.php';

$apply = !empty($_GET['apply']) && (string) $_GET['apply'] === '1';
$siteKey = preg_replace('/[^a-z0-9_]/', '', strtolower((string) ($_GET['site_key'] ?? '')));
$allDemos = !empty($_GET['all_erp_only']) && (string) $_GET['all_erp_only'] === '1';

$cfg = new DP_Config();
epc_portal_apply_config($cfg);
$pdo = epc_portal_platform_pdo();
if (!$pdo instanceof PDO) {
	exit("Platform DB connect failed\n");
}
epc_portal_demo_ensure_schema($pdo);

$keys = array();
if ($allDemos) {
	$rows = $pdo->query('SELECT * FROM `epc_portal_tenants` WHERE `is_demo` = 1 AND `status` = \'live\' ORDER BY `site_key`')->fetchAll(PDO::FETCH_ASSOC);
	foreach ($rows as $row) {
		if (epc_portal_demo_row_is_erp_only($row)) {
			$keys[] = (string) $row['site_key'];
		}
	}
} elseif ($siteKey !== '') {
	$keys[] = $siteKey;
} else {
	exit("Usage: ?token=…&apply=1&site_key=demo_260602_eo  OR  &all_erp_only=1\n");
}

$presets = epc_portal_demo_industry_presets();
$preset = $presets['erp_only'] ?? null;
if ($preset === null) {
	exit("erp_only preset missing\n");
}

echo 'mode=' . ($apply ? 'apply' : 'dry-run') . "\n";
echo 'targets=' . implode(', ', $keys) . "\n\n";

foreach ($keys as $key) {
	echo "=== {$key} ===\n";
	$row = epc_portal_demo_load_live_row($key, $pdo);
	if ($row === null) {
		$st = $pdo->prepare('SELECT * FROM `epc_portal_tenants` WHERE `site_key` = ? LIMIT 1');
		$st->execute(array($key));
		$row = $st->fetch(PDO::FETCH_ASSOC) ?: null;
	}
	if ($row === null) {
		echo "  SKIP: tenant not found\n\n";
		continue;
	}
	if (!epc_portal_demo_row_is_erp_only($row)) {
		echo "  SKIP: not an ERP-only demo\n\n";
		continue;
	}
	$email = trim((string) ($row['demo_contact_email'] ?? ''));
	$trade = trim((string) ($row['trade_name'] ?? $key));
	$phone = trim((string) ($row['demo_contact_phone'] ?? ''));
	echo '  db=' . ($row['db_name'] ?? '') . ' trade=' . $trade . "\n";
	echo '  cp=' . epc_portal_demo_cp_login_url($key) . "\n";
	echo '  erp_shell=' . epc_portal_demo_erp_shell_url($key) . "\n";

	$tenantPdo = epc_portal_demo_tenant_pdo($row);
	if (!$tenantPdo instanceof PDO) {
		echo "  ERROR: tenant DB connect failed\n\n";
		continue;
	}

	$moduleCount = (int) $tenantPdo->query('SELECT COUNT(*) FROM `modules`')->fetchColumn();
	$contentCount = (int) $tenantPdo->query('SELECT COUNT(*) FROM `content` WHERE `is_frontend` = 0')->fetchColumn();
	echo "  modules={$moduleCount} backend_content={$contentCount}\n";

	if (!$apply) {
		echo "  (dry-run — add apply=1 to execute)\n\n";
		continue;
	}

	$repair = epc_portal_demo_repair_storefront_schema($row);
	echo '  schema_repair: ' . ($repair['message'] ?? json_encode($repair)) . "\n";

	$seed = epc_portal_demo_seed_erp_cp_content($tenantPdo);
	echo '  erp_content_seed: ' . json_encode($seed) . "\n";

	$push = epc_portal_demo_push_tenant_settings_erp_only($pdo, $key, $preset, $trade, $email !== '' ? $email : 'demo@ecomae.com', $phone);
	echo '  settings: ' . ($push['message'] ?? json_encode($push)) . "\n";

	$intro = array(
		'demo_erp_only' => 1,
		'commerce_enabled' => 0,
		'storefront_package' => 'none',
		'access_mode' => 'erp_only',
	);
	$pdo->prepare('UPDATE `epc_portal_tenants` SET `industry_code` = ?, `intro_json` = ? WHERE `site_key` = ?')
		->execute(array('erp_standalone', json_encode($intro), $key));
	echo "  registry intro_json updated\n\n";
}

echo "Done.\n";
