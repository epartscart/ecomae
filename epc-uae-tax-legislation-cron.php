<?php
/**
 * Daily UAE FTA legislation fetch (legislation.aspx) + auto summary/chart refresh.
 * Cron: curl -s "https://www.ecomae.com/epc-uae-tax-legislation-cron.php?token=epartscart-deploy-2026"
 * Force bypass 24h cache: &force=1
 */
declare(strict_types=1);

require_once __DIR__ . '/epc_deploy_auth.php';
epc_deploy_require_token();

define('_ASTEXE_', 1);
require_once __DIR__ . '/config.php';
require_once __DIR__ . '/content/shop/finance/epc_uae_tax_compliance.php';

$format = strtolower(trim((string)($_GET['format'] ?? 'json')));
$force = !empty($_GET['force']);

$cfg = new DP_Config();
$pdo = new PDO(
	'mysql:host=' . $cfg->host . ';dbname=' . $cfg->db . ';charset=utf8',
	$cfg->user,
	$cfg->password
);
$pdo->setAttribute(PDO::ATTR_ERRMODE, PDO::ERRMODE_EXCEPTION);

$result = epc_uae_fta_cron_fetch_legislation($pdo, $force);

if ($format === 'text') {
	header('Content-Type: text/plain; charset=utf-8');
	echo ($result['message'] ?? 'Done') . "\n";
	echo 'OK: ' . (($result['ok'] ?? false) ? 'yes' : 'no') . "\n";
	echo 'Items: ' . (int)($result['legislation_count'] ?? 0) . "\n";
	echo 'New: ' . (int)($result['new_count'] ?? 0) . "\n";
	echo 'Changed: ' . (int)($result['changed_count'] ?? 0) . "\n";
	echo 'Summaries regenerated: ' . (int)($result['summaries_regenerated'] ?? 0) . "\n";
	if (!empty($result['time_fetched_label'])) {
		echo 'Fetched: ' . $result['time_fetched_label'] . "\n";
	}
	if (!empty($result['errors'])) {
		echo 'Errors: ' . implode('; ', (array)$result['errors']) . "\n";
	}
	exit;
}

header('Content-Type: application/json; charset=utf-8');
echo json_encode($result, JSON_UNESCAPED_UNICODE | JSON_PRETTY_PRINT);
