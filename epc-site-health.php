<?php
/**
 * Quick live site health check.
 * https://www.epartscart.com/epc-site-health.php?token=epartscart-deploy-2026
 */
header('Content-Type: text/plain; charset=utf-8');
if (($_GET['token'] ?? '') !== 'epartscart-deploy-2026') {
	exit("Forbidden\n");
}

define('_ASTEXE_', 1);
require_once __DIR__ . '/config.php';

$checks = array(
	'config.php' => is_readable(__DIR__ . '/config.php'),
	'epc_demand_intelligence.php' => is_readable(__DIR__ . '/content/shop/docpart/epc_demand_intelligence.php'),
	'epc_demand_country_iso.php' => is_readable(__DIR__ . '/content/shop/docpart/epc_demand_country_iso.php'),
	'epc_product_family.php' => is_readable(__DIR__ . '/content/shop/docpart/epc_product_family.php'),
	'ajax_epc_product_family.php' => is_readable(__DIR__ . '/content/shop/docpart/ajax_epc_product_family.php'),
	'epc_admin_notifications.php' => is_readable(__DIR__ . '/content/shop/usefull/epc_admin_notifications.php'),
);

foreach ($checks as $name => $ok) {
	echo ($ok ? 'OK' : 'MISSING') . "  {$name}\n";
}

try {
	$cfg = new DP_Config();
	$db = new PDO('mysql:host=' . $cfg->host . ';dbname=' . $cfg->db . ';charset=utf8', $cfg->user, $cfg->password);
	$db->setAttribute(PDO::ATTR_ERRMODE, PDO::ERRMODE_EXCEPTION);
	$n = (int)$db->query('SELECT COUNT(*) FROM `shop_docpart_prices_data` WHERE IFNULL(`exist`,0) > 0 AND IFNULL(`price`,0) > 0 LIMIT 1')->fetchColumn();
	echo "db_stock_rows_sample: {$n}\n";
} catch (Throwable $e) {
	echo 'db: FAIL ' . $e->getMessage() . "\n";
}

if (is_readable(__DIR__ . '/content/shop/docpart/epc_product_family.php')) {
	require_once __DIR__ . '/content/shop/docpart/epc_product_family.php';
	try {
		$db = new PDO('mysql:host=' . $cfg->host . ';dbname=' . $cfg->db . ';charset=utf8', $cfg->user, $cfg->password);
		$lines = epc_pf_fetch_catalog_lines($db, 50);
		$catalog = epc_pf_build_catalog_from_lines($lines, $cfg, $db, 5);
		echo 'product_family_build: OK families=' . count($catalog['products']) . "\n";
	} catch (Throwable $e) {
		echo 'product_family_build: FAIL ' . $e->getMessage() . "\n";
	}
}

echo "Done\n";
