<?php
/**
 * Delete expired demo tenants + optional 1-day reminder emails.
 * Cron: curl -s "https://www.ecomae.com/epc-demo-expire-cron.php?token=epartscart-deploy-2026"
 */
declare(strict_types=1);

require_once __DIR__ . '/epc_deploy_auth.php';
epc_deploy_require_token();

define('_ASTEXE_', 1);
require_once __DIR__ . '/content/general_pages/epc_portal_demo.php';

$format = strtolower(trim((string) ($_GET['format'] ?? 'json')));
$sendReminders = !isset($_GET['remind']) || (string) $_GET['remind'] !== '0';

$pdo = epc_portal_demo_platform_pdo();
if (!$pdo instanceof PDO) {
	if ($format === 'text') {
		header('Content-Type: text/plain; charset=utf-8');
		exit("Platform DB unavailable\n");
	}
	header('Content-Type: application/json; charset=utf-8');
	http_response_code(500);
	exit(json_encode(array('ok' => false, 'message' => 'Platform DB unavailable')));
}

$report = epc_portal_demo_expire_cron($pdo, $sendReminders);
$out = array(
	'ok' => true,
	'timestamp' => time(),
	'active_demos' => epc_portal_demo_count_active($pdo),
	'max_demos' => epc_portal_demo_max_active(),
	'deleted' => $report['deleted'],
	'reminded' => $report['reminded'],
	'errors' => $report['errors'],
);

if ($format === 'text') {
	header('Content-Type: text/plain; charset=utf-8');
	echo "EPC demo expire cron\n";
	echo 'time=' . date('c') . "\n";
	echo 'active=' . $out['active_demos'] . '/' . $out['max_demos'] . "\n";
	echo 'deleted=' . implode(',', $out['deleted']) . "\n";
	echo 'reminded=' . implode(',', $out['reminded']) . "\n";
	if ($out['errors'] !== array()) {
		echo 'errors=' . implode('; ', $out['errors']) . "\n";
	}
	echo "done\n";
	exit;
}

header('Content-Type: application/json; charset=utf-8');
echo json_encode($out, JSON_UNESCAPED_UNICODE | JSON_PRETTY_PRINT);
