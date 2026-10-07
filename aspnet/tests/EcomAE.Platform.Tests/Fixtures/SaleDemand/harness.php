<?php
// Runs PHP epc_erp_inventory_record_sale_demand against a throwaway database.
// Usage: php harness.php <repo_root> <dsn> <user> <password> schema|run <out_dir>
declare(strict_types=1);
date_default_timezone_set('UTC');

[$self, $root, $dsn, $dbUser, $dbPassword, $mode] = $argv;
$out = $argv[6] ?? '';
$_SERVER['DOCUMENT_ROOT'] = $root;

final class DP_User
{
	public static function getAdminId(): int
	{
		return 9;
	}

	public static function getUserId(): int
	{
		return 0;
	}
}

define('_ASTEXE_', 1);
$db = new PDO($dsn, $dbUser, $dbPassword, array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION));
$db->query('SET NAMES utf8;');
require_once $root . '/content/shop/finance/epc_erp_inventory.php';

if ($mode === 'schema') {
	epc_erp_inventory_ensure_schema($db);
	exit(0);
}

$results = array();
$results[] = 'doc7=' . epc_erp_inventory_record_sale_demand($db, 7, 40, array(array('item_name' => 'Wiper', 'quantity' => 9)), 1767225600);
$results[] = 'doc7_again=' . epc_erp_inventory_record_sale_demand($db, 7, 40, array(), 1767225600);
$results[] = 'doc8=' . epc_erp_inventory_record_sale_demand($db, 8, 0, array(
	array('item_name' => 'Brake pad', 'quantity' => 2),
	array('item_name' => ' Wiper ', 'quantity' => 1.5),
	array('item_name' => 'Unknown', 'quantity' => 1),
	array('item_name' => 'Spark plug', 'quantity' => 1),
	array('item_name' => 'Wiper', 'quantity' => 0),
	array('item_name' => 'Brake pad', 'quantity' => 1),
), 1767312000);
$results[] = 'doc9=' . epc_erp_inventory_record_sale_demand($db, 9, 41, array(array('item_name' => 'Wiper', 'quantity' => 2)), 1767398400);
$results[] = 'doc10=' . epc_erp_inventory_record_sale_demand($db, 10, 0, array(array('item_name' => 'Unknown', 'quantity' => 2)), 1767398400);
$results[] = 'doc0=' . epc_erp_inventory_record_sale_demand($db, 0, 40, array(), 1767398400);
file_put_contents($out . '/results.txt', implode("\n", $results) . "\n");

$dump = static function (string $sql) use ($db): string {
	$lines = array();
	foreach ($db->query($sql)->fetchAll(PDO::FETCH_NUM) as $row) {
		$lines[] = implode("\t", array_map(static fn ($v) => $v === null ? 'NULL' : (string) $v, $row));
	}
	return implode("\n", $lines) . "\n";
};
file_put_contents($out . '/movements.tsv', $dump('SELECT `movement_type`,`warehouse_id`,`item_id`,`qty`,`unit_cost`,`total_cost`,`order_id`,`reference`,`note`,`movement_date`,`admin_id`,`active` FROM `epc_erp_inv_movements` ORDER BY `id`'));
file_put_contents($out . '/stock.tsv', $dump('SELECT `warehouse_id`,`item_id`,`qty_on_hand`,`avg_unit_cost`,IFNULL(`batch_no`,\'\') FROM `epc_erp_inv_stock` ORDER BY `id`'));
