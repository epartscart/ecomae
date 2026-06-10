<?php
declare(strict_types=1);
require_once __DIR__ . '/epc_deploy_auth.php';
epc_deploy_require_token();
header('Content-Type: text/plain; charset=utf-8');
error_reporting(E_ALL);
ini_set('display_errors', '1');

require_once __DIR__ . '/content/general_pages/epc_portal_tenant.php';
$pdo = epc_portal_platform_pdo();
if (!$pdo) {
	exit("no platform pdo\n");
}

echo "=== templates ===\n";
foreach ($pdo->query('SELECT id, name, current, is_frontend FROM templates ORDER BY id')->fetchAll(PDO::FETCH_ASSOC) as $r) {
	echo "id={$r['id']} name={$r['name']} current={$r['current']} frontend={$r['is_frontend']}\n";
}

$fix = !empty($_GET['fix']);
if ($fix) {
	$pdo->exec('UPDATE templates SET `current` = 0');
	$st = $pdo->prepare('UPDATE templates SET `current` = 1 WHERE name = ? AND is_frontend = 0 LIMIT 1');
	$st->execute(array('bootstrap_admin'));
	echo "set bootstrap_admin current for backend rows=" . $st->rowCount() . "\n";
	$st2 = $pdo->prepare('UPDATE templates SET `current` = 1 WHERE name = ? AND is_frontend = 1 LIMIT 1');
	$st2->execute(array('expanse'));
	echo "set expanse current for frontend rows=" . $st2->rowCount() . "\n";
}

echo "\n=== tenant_hub content ===\n";
foreach ($pdo->query("SELECT id, url, title, template FROM content WHERE url LIKE '%tenant_hub%'")->fetchAll(PDO::FETCH_ASSOC) as $r) {
	echo "id={$r['id']} url={$r['url']} template={$r['template']}\n";
}

echo "\n=== control_items tenant_hub ===\n";
foreach ($pdo->query("SELECT id, url, items_group FROM control_items WHERE url LIKE '%tenant_hub%'")->fetchAll(PDO::FETCH_ASSOC) as $r) {
	echo "id={$r['id']} url={$r['url']} group={$r['items_group']}\n";
}

if ($fix) {
	echo "\n=== templates after fix ===\n";
	foreach ($pdo->query('SELECT id, name, current, is_frontend FROM templates WHERE current = 1')->fetchAll(PDO::FETCH_ASSOC) as $r) {
		echo "id={$r['id']} name={$r['name']} frontend={$r['is_frontend']}\n";
	}
}
