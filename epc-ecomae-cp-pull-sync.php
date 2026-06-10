<?php
/**
 * Pull-sync portal files from www.ecomae.com docroot (run on cp.ecomae.com).
 */
declare(strict_types=1);

require_once __DIR__ . '/epc_deploy_auth.php';
epc_deploy_require_token();
header('Content-Type: text/plain; charset=utf-8');
set_time_limit(0);

$dest = __DIR__;
$srcCandidates = array(
	'/home/ecomae/htdocs/www.ecomae.com',
	'/home/ecomae/htdocs/ecomae.com',
);
$src = '';
foreach ($srcCandidates as $path) {
	if (is_dir($path) && is_file($path . '/index.php')) {
		$src = $path;
		break;
	}
}
if ($src === '') {
	exit("www source docroot not found\n");
}

echo "src={$src}\ndest={$dest}\n";
$cmd = 'cp -a ' . escapeshellarg($src . '/.') . ' ' . escapeshellarg($dest . '/') . ' 2>&1';
exec($cmd, $out, $code);
echo implode("\n", array_slice($out, 0, 15)) . "\nexit={$code}\n";

$dbPass = '';
$wwwCfg = $src . '/config.local.php';
if (is_file($wwwCfg)) {
	$epc_config_local = null;
	require $wwwCfg;
	if (isset($epc_config_local['password'])) {
		$dbPass = (string) $epc_config_local['password'];
	}
}
if ($dbPass === '' && isset($_GET['db_password'])) {
	$dbPass = trim((string) $_GET['db_password']);
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
	'epc_contact_phone' => '+971-567607011',
	'epc_head_office_email' => 'hello@ecomae.com',
	'epc_head_office_address' => 'Dubai, United Arab Emirates',
);
PHP;
$cpCfg = $dbPass !== '' ? str_replace('DBPASS', addslashes($dbPass), $cpCfg) : str_replace("'DBPASS'", "''", $cpCfg);
file_put_contents($dest . '/config.local.php', $cpCfg);
echo "Wrote config.local.php\n";

foreach (array('index.php', 'cp/index.php', 'core/dp_core.php') as $rel) {
	echo "{$rel}=" . (is_file($dest . '/' . $rel) ? 'yes' : 'no') . "\n";
}
echo "Done.\n";
