<?php
/**
 * Full sync epartscart docroot → ecomae (server-side tar if permitted).
 */
declare(strict_types=1);
require_once __DIR__ . '/epc_deploy_auth.php';
epc_deploy_require_token();
header('Content-Type: text/plain; charset=utf-8');
set_time_limit(0);

$src = '/home/epartscart/htdocs/www.epartscart.com';
$dest = '/home/ecomae/htdocs/www.ecomae.com';
$tar = '/tmp/ecomae-full-sync.tar.gz';

echo "src exists: " . (is_dir($src) ? 'yes' : 'no') . "\n";
echo "dest exists: " . (is_dir($dest) ? 'yes' : 'no') . "\n";

if (!is_dir($src) || !is_dir($dest)) {
	exit("paths missing\n");
}

$cmd = 'rsync -a --delete ' . escapeshellarg($src . '/') . ' ' . escapeshellarg($dest . '/') . ' 2>&1';
exec($cmd, $out, $code);
echo "rsync: " . implode("\n", array_slice($out, 0, 15)) . "\nexit={$code}\n";

if ($code !== 0) {
	$cmd2 = 'cp -a ' . escapeshellarg($src . '/.') . ' ' . escapeshellarg($dest . '/') . ' 2>&1';
	exec($cmd2, $out2, $code2);
	echo "cp: " . implode("\n", array_slice($out2, 0, 10)) . "\nexit={$code2}\n";
}

$dbPass = trim((string) ($_GET['db_password'] ?? ''));
if ($dbPass !== '') {
	$cfg = <<<'PHP'
<?php
$epc_config_local = array(
	'password' => 'DBPASS',
	'db' => 'ecomae',
	'user' => 'ecomae',
	'domain_path' => 'https://www.ecomae.com/',
	'from_name' => 'ecomae',
	'from_email' => 'hello@ecomae.com',
	'epc_contact_phone' => '+971-567607011',
	'epc_head_office_email' => 'hello@ecomae.com',
	'epc_head_office_address' => 'Dubai, United Arab Emirates',
);
PHP;
	file_put_contents($dest . '/config.local.php', str_replace('DBPASS', addslashes($dbPass), $cfg));
	echo "Wrote config.local.php\n";
}

echo "index.php: " . (is_file($dest . '/index.php') ? 'yes' : 'no') . "\n";
echo "cp/index.php: " . (is_file($dest . '/cp/index.php') ? 'yes' : 'no') . "\n";
echo "Done.\n";
