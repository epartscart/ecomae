<?php
declare(strict_types=1);
require_once __DIR__ . '/epc_deploy_auth.php';
epc_deploy_require_token();
header('Content-Type: text/plain; charset=utf-8');

require_once __DIR__ . '/content/general_pages/epc_portal_tenant.php';
$pdo = epc_portal_platform_pdo();
if (!$pdo) {
	exit("no platform pdo\n");
}

$urls = array(
	'/cp/shop/tenant_hub/tenant_hub',
	'cp/shop/tenant_hub/tenant_hub',
	'/shop/tenant_hub/tenant_hub',
);
foreach ($urls as $u) {
	$st = $pdo->prepare('SELECT id, url, title, template, content_type FROM content WHERE url = ? OR url = ? LIMIT 3');
	$st->execute(array($u, ltrim($u, '/')));
	$rows = $st->fetchAll(PDO::FETCH_ASSOC);
	echo "url={$u} matches=" . count($rows) . "\n";
	foreach ($rows as $r) {
		echo '  id=' . $r['id'] . ' url=' . $r['url'] . ' template=' . $r['template'] . "\n";
	}
}

$st2 = $pdo->query("SELECT id, url, title, template FROM content WHERE url LIKE '%tenant_hub%' ORDER BY id");
foreach ($st2->fetchAll(PDO::FETCH_ASSOC) as $r) {
	echo 'row id=' . $r['id'] . ' url=' . $r['url'] . ' template=' . $r['template'] . "\n";
}

$st3 = $pdo->query('SELECT id, url FROM control_items WHERE url LIKE "%tenant_hub%"');
foreach ($st3->fetchAll(PDO::FETCH_ASSOC) as $r) {
	echo 'menu id=' . $r['id'] . ' url=' . $r['url'] . "\n";
}
