<?php
/**
 * Backfill UAE FTA legislation ERP summaries (all rows in epc_uae_tax_legislation_items).
 * Run: https://www.ecomae.com/epc-uae-tax-legislation-backfill.php?token=epartscart-deploy-2026
 * Optional: &fetch_pdf=1 to curl each PDF for excerpt (slower).
 */
declare(strict_types=1);

require_once __DIR__ . '/epc_deploy_auth.php';
epc_deploy_require_token();

error_reporting(E_ALL);
ini_set('display_errors', 1);
header('Content-Type: text/plain; charset=utf-8');

define('_ASTEXE_', 1);
require_once __DIR__ . '/config.php';
require_once __DIR__ . '/content/shop/finance/epc_uae_tax_compliance.php';

$cfg = new DP_Config();
$pdo = new PDO('mysql:host=' . $cfg->host . ';dbname=' . $cfg->db . ';charset=utf8', $cfg->user, $cfg->password);
$pdo->setAttribute(PDO::ATTR_ERRMODE, PDO::ERRMODE_EXCEPTION);

$fetchPdfs = !empty($_GET['fetch_pdf']);
$result = epc_uae_tax_legislation_backfill_summaries($pdo, $fetchPdfs);

echo $result['message'] . "\n\n";
echo 'Updated: ' . (int)$result['updated'] . "\n";
echo 'DB synced: ' . (int)$result['synced'] . "\n";
echo 'PDF excerpts: ' . (int)$result['pdf_excerpts'] . ' (fetch_pdf=' . ($fetchPdfs ? '1' : '0') . ")\n\n";

echo "=== VAT overall card ===\n";
$vat = $result['vat_overall'] ?? array();
echo (string)($vat['title'] ?? 'VAT') . "\n";
foreach ((array)($vat['bullets'] ?? array()) as $b) {
	echo ' • ' . $b . "\n";
}
echo "\n=== Sample item summaries (first 3) ===\n";
foreach ((array)($result['samples'] ?? array()) as $i => $s) {
	echo ($i + 1) . '. ' . ($s['title'] ?? '') . "\n";
	echo '   Summary: ' . substr((string)($s['erp_summary'] ?? ''), 0, 320);
	if (strlen((string)($s['erp_summary'] ?? '')) > 320) {
		echo '...';
	}
	echo "\n";
	foreach ((array)($s['compliance_actions'] ?? array()) as $act) {
		echo '   - ' . $act . "\n";
	}
	echo "\n";
}
