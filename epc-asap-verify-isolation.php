<?php
declare(strict_types=1);
require_once __DIR__ . '/epc_deploy_auth.php';
epc_deploy_require_token();
header('Content-Type: text/plain; charset=utf-8');

$cfgFile = '/home/ecomae/htdocs/www.ecomae.com/config.local.php';
$epc_config_local = null;
include $cfgFile;
$platDb = (string) ($epc_config_local['db'] ?? 'ecomae');
$platUser = (string) ($epc_config_local['user'] ?? 'ecomae');
$platPass = (string) ($epc_config_local['password'] ?? '');

$platformPdo = new PDO('mysql:host=127.0.0.1;dbname=' . $platDb . ';charset=utf8', $platUser, $platPass);
$st = $platformPdo->prepare('SELECT site_key, db_name, trade_name, erp_only_shared FROM epc_portal_tenants WHERE site_key IN (?, ?)');
$st->execute(array('asap', 'epartscart'));
echo "=== Tenant registry ===\n";
while ($row = $st->fetch(PDO::FETCH_ASSOC)) {
	echo json_encode($row, JSON_UNESCAPED_UNICODE) . "\n";
}

$st = $platformPdo->prepare('SELECT db_name, db_user, db_password FROM epc_portal_tenants WHERE site_key = ? LIMIT 1');
$st->execute(array('asap'));
$asap = $st->fetch(PDO::FETCH_ASSOC);
if (!$asap) {
	exit("ASAP not found\n");
}
$tp = new PDO(
	'mysql:host=127.0.0.1;dbname=' . $asap['db_name'] . ';charset=utf8',
	$asap['db_user'],
	$asap['db_password']
);
echo "\n=== ASAP DB stats (db=" . $asap['db_name'] . ") ===\n";
foreach (array('shop_orders', 'epc_crm_opportunities', 'users') as $tbl) {
	try {
		$n = (int) $tp->query('SELECT COUNT(*) FROM `' . $tbl . '`')->fetchColumn();
		echo "{$tbl}={$n}\n";
	} catch (Exception $e) {
		echo "{$tbl}=ERR\n";
	}
}
foreach (array(
	'epc_erp_gl_journals' => 'gl_journals',
	'epc_erp_cash_movements' => 'cash_movements',
	'epc_erp_supplier_accounting' => 'supplier_acct_rows',
) as $tbl => $label) {
	try {
		$n = (int) $tp->query('SELECT COUNT(*) FROM `' . $tbl . '`')->fetchColumn();
		echo "{$label}={$n}\n";
	} catch (Exception $e) {
		echo "{$label}=ERR\n";
	}
}
try {
	$cash = (float) $tp->query(
		'SELECT IFNULL(SUM(`amount`),0) FROM `epc_erp_cash_movements`'
	)->fetchColumn();
	echo 'cash_movements_sum=' . $cash . "\n";
} catch (Exception $e) {
	echo "cash_movements_sum=ERR\n";
}
$ust = $tp->prepare('SELECT user_id, email FROM users WHERE user_id = 19 LIMIT 1');
$ust->execute();
echo 'user19=' . json_encode($ust->fetch(PDO::FETCH_ASSOC) ?: 'NOT_FOUND') . "\n";
$st = $tp->query("SELECT system_name, access_mode FROM epc_portal_site_settings LIMIT 1");
$settings = $st ? $st->fetch(PDO::FETCH_ASSOC) : null;
echo 'site_settings=' . json_encode($settings) . "\n";

$st = $platformPdo->prepare('SELECT db_name FROM epc_portal_tenants WHERE site_key = ? LIMIT 1');
$st->execute(array('epartscart'));
echo "\nepartscart registry db=" . $st->fetchColumn() . " (expect docpart)\n";
echo "Done.\n";
