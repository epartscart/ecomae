<?php
declare(strict_types=1);
require_once __DIR__ . '/epc_deploy_auth.php';
epc_deploy_require_token();
header('Content-Type: text/plain; charset=utf-8');
set_time_limit(0);

$tar = '/tmp/ecomae-www-export.tar.gz';
$dest = __DIR__;
if (!is_file($tar)) {
	exit("missing {$tar} — run epc-ecomae-www-export.php on www first\n");
}
@mkdir('/tmp/ecomae-cp-import', 0755, true);
exec('rm -rf /tmp/ecomae-cp-import/* 2>&1');
exec('tar -xzf ' . escapeshellarg($tar) . ' -C /tmp/ecomae-cp-import 2>&1', $o, $c);
echo implode("\n", array_slice($o, 0, 8)) . "\ntar exit={$c}\n";
exec('cp -a /tmp/ecomae-cp-import/. ' . escapeshellarg($dest . '/') . ' 2>&1', $o2, $c2);
echo implode("\n", array_slice($o2, 0, 8)) . "\ncp exit={$c2}\n";

$dbPass = trim((string) ($_GET['db_password'] ?? ''));
if ($dbPass === '' && is_file($dest . '/config.local.php')) {
	$epc_config_local = null;
	require $dest . '/config.local.php';
	if (isset($epc_config_local['password'])) {
		$dbPass = (string) $epc_config_local['password'];
	}
}
$cpCfg = <<<'PHP'
<?php
$epc_config_local = array(
	'password' => 'DBPASS',
	'db' => 'ecomae',
	'user' => 'ecomae',
	'domain_path' => 'https://cp.ecomae.com/',
	'from_name' => 'ecomae Platform',
	'from_email' => 'hello@ecomae.com',
);
PHP;
$cpCfg = $dbPass !== '' ? str_replace('DBPASS', addslashes($dbPass), $cpCfg) : str_replace("'DBPASS'", "''", $cpCfg);
file_put_contents($dest . '/config.local.php', $cpCfg);
echo "config.local.php written\n";
foreach (array('index.php', 'cp/index.php', 'core/dp_core.php') as $rel) {
	echo "{$rel}=" . (is_file($dest . '/' . $rel) ? 'yes' : 'no') . "\n";
}
