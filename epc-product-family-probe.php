<?php
/**
 * Product Family health check (deploy / support).
 * https://www.epartscart.com/epc-product-family-probe.php?token=epartscart-deploy-2026
 */
header('Content-Type: text/plain; charset=utf-8');
if (($_GET['token'] ?? '') !== 'epartscart-deploy-2026') {
	exit("Forbidden\n");
}

define('_ASTEXE_', 1);
require_once __DIR__ . '/config.php';
require_once __DIR__ . '/content/shop/docpart/epc_product_family.php';

$cfg = new DP_Config();
$db = new PDO('mysql:host=' . $cfg->host . ';dbname=' . $cfg->db . ';charset=utf8', $cfg->user, $cfg->password);
$db->setAttribute(PDO::ATTR_ERRMODE, PDO::ERRMODE_EXCEPTION);

$checks = array(
	'epc_product_family.php' => is_readable(__DIR__ . '/content/shop/docpart/epc_product_family.php'),
	'ajax_epc_product_family.php' => is_readable(__DIR__ . '/content/shop/docpart/ajax_epc_product_family.php'),
	'epc_demand_country_iso.php' => is_readable(__DIR__ . '/content/shop/docpart/epc_demand_country_iso.php'),
	'product_family_catalog.php' => is_readable(__DIR__ . '/content/product_family_catalog.php'),
);

foreach ($checks as $name => $ok) {
	echo ($ok ? 'OK' : 'MISSING') . "  {$name}\n";
}

try {
	$lines = epc_pf_fetch_catalog_lines($db, 200);
	echo 'stock_lines_sample: ' . count($lines) . "\n";
	$catalog = epc_pf_build_catalog_from_lines($lines, $cfg, $db, 10);
	echo 'product_families: ' . count($catalog['products']) . "\n";
	echo 'parts_in_sample: ' . (int)($catalog['summary']['parts_count'] ?? 0) . "\n";
	echo "BUILD OK\n";
} catch (Throwable $e) {
	echo "BUILD FAIL: " . $e->getMessage() . "\n";
	echo $e->getFile() . ':' . $e->getLine() . "\n";
}
