<?php
/**
 * One-time: relabel historical verify-probe audit rows (probes no longer write toggles).
 * GET /epc-storage-toggle-audit-annotate.php?token=…&site_key=epartscart&apply=1
 */
declare(strict_types=1);

require_once __DIR__ . '/epc_deploy_auth.php';
epc_deploy_require_token();
header('Content-Type: application/json; charset=utf-8');

define('_ASTEXE_', 1);
require_once __DIR__ . '/config.php';
require_once __DIR__ . '/content/general_pages/epc_portal.php';
require_once __DIR__ . '/content/general_pages/epc_portal_db.php';
require_once __DIR__ . '/content/general_pages/epc_portal_tenant.php';
require_once __DIR__ . '/content/shop/price_engine/epc_auto_price_cp_install.php';

$apply = !empty($_GET['apply']) && (string) $_GET['apply'] === '1';
$siteKey = preg_replace('/[^a-z0-9_]/', '', strtolower(trim((string) ($_GET['site_key'] ?? 'epartscart'))));

$cfg = new DP_Config();
epc_portal_apply_config($cfg);

$platformPdo = epc_portal_platform_pdo();
if (!$platformPdo instanceof PDO) {
	http_response_code(500);
	exit(json_encode(array('ok' => false, 'error' => 'platform_unavailable')));
}
epc_portal_db_ensure($platformPdo);

$row = null;
foreach (epc_portal_list_tenants($platformPdo) as $t) {
	if ((string) ($t['site_key'] ?? '') === $siteKey) {
		$row = $t;
		break;
	}
}
if (!$row) {
	http_response_code(404);
	exit(json_encode(array('ok' => false, 'error' => 'tenant_not_found', 'site_key' => $siteKey)));
}

$pdo = epc_auto_price_setup_connect(array(
	'db' => (string) ($row['db_name'] ?? ''),
	'user' => (string) ($row['db_user'] ?? ''),
	'pass' => (string) ($row['db_password'] ?? ''),
), $cfg);

if (!$pdo instanceof PDO) {
	http_response_code(500);
	exit(json_encode(array('ok' => false, 'error' => 'db_connect_failed')));
}

$probeLabels = array('verify-probe', 'verify-probe-restore');
$placeholders = implode(',', array_fill(0, count($probeLabels), '?'));
$countStmt = $pdo->prepare(
	"SELECT COUNT(*) FROM `epc_storefront_storage_toggle_audit`
	 WHERE `user_label` IN ({$placeholders})
	   AND `user_label` NOT LIKE '[historical probe%'"
);
$countStmt->execute($probeLabels);
$pending = (int) $countStmt->fetchColumn();

$updated = 0;
if ($apply && $pending > 0) {
	$upd = $pdo->prepare(
		"UPDATE `epc_storefront_storage_toggle_audit`
		 SET `user_label` = CONCAT('[historical probe — no longer writes] ', `user_label`)
		 WHERE `user_label` IN ({$placeholders})
		   AND `user_label` NOT LIKE '[historical probe%'"
	);
	$upd->execute($probeLabels);
	$updated = $upd->rowCount();
}

echo json_encode(array(
	'ok' => true,
	'site_key' => $siteKey,
	'apply' => $apply,
	'probe_labels' => $probeLabels,
	'pending_rows' => $pending,
	'updated_rows' => $updated,
	'note' => 'Verify probes are read-only; this script only relabels old audit rows.',
), JSON_UNESCAPED_UNICODE | JSON_PRETTY_PRINT);
