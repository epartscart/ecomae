<?php
ini_set('display_errors', 'stderr');
$root = rtrim($argv[1] ?? '/workspace-wt/small-done', '/');
$out = rtrim($argv[2] ?? '/tmp/plan_q1m_assets', '/');
@mkdir($out, 0777, true);
require $root . '/content/general_pages/epc_db_migrations.php';
$reg = epc_migrations_registry();
foreach ($reg as &$m) {
	$m['checksum'] = md5($m['up']);
}
unset($m);
file_put_contents($out . '/registry.json', json_encode(array(
	'version' => EPC_MIGRATIONS_VERSION,
	'registry' => $reg,
), JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES));
echo "ok\n";
