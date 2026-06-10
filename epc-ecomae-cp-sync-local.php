<?php
/**
 * Sync www.ecomae.com docroot → cp.ecomae.com (run on www.ecomae.com as site user).
 * https://www.ecomae.com/epc-ecomae-cp-sync-local.php?token=...
 */
declare(strict_types=1);

require_once __DIR__ . '/epc_deploy_auth.php';
epc_deploy_require_token();
header('Content-Type: text/plain; charset=utf-8');
set_time_limit(0);

$src = __DIR__;
$cpUser = trim((string) ($_GET['cp_user'] ?? 'ecomaecp'));
$candidates = array(
	'/home/' . $cpUser . '/htdocs/cp.ecomae.com',
	'/home/ecomae/htdocs/cp.ecomae.com',
	'/home/ecomaecp/htdocs/cp.ecomae.com',
	dirname($src) . '/cp.ecomae.com',
);
if (is_dir('/home/ecomae')) {
	$scan = @scandir('/home/ecomae');
	if (is_array($scan)) {
		foreach ($scan as $entry) {
			if ($entry === '.' || $entry === '..') {
				continue;
			}
			$path = '/home/ecomae/' . $entry;
			if (is_dir($path) && stripos($entry, 'cp.ecomae') !== false) {
				array_unshift($candidates, $path);
			}
		}
	}
}
if (is_dir('/home/ecomae/htdocs')) {
	$scan = @scandir('/home/ecomae/htdocs');
	if (is_array($scan)) {
		foreach ($scan as $entry) {
			if ($entry === '.' || $entry === '..') {
				continue;
			}
			$path = '/home/ecomae/htdocs/' . $entry;
			if (is_dir($path) && stripos($entry, 'cp.ecomae') !== false) {
				array_unshift($candidates, $path);
			}
		}
	}
}
$dest = '';
foreach ($candidates as $path) {
	if (is_dir($path)) {
		$dest = $path;
		break;
	}
}
if ($dest === '' && !empty($_GET['create_cp_dir'])) {
	$dest = '/home/' . $cpUser . '/htdocs/cp.ecomae.com';
	if (!is_dir(dirname($dest))) {
		$dest = '/home/ecomae/htdocs/cp.ecomae.com';
	}
	@mkdir($dest, 0755, true);
}
if ($dest === '') {
	echo "src={$src}\n/home/ecomae=" . (is_dir('/home/ecomae') ? 'yes' : 'no') . "\n";
	if (is_dir('/home/ecomae/htdocs')) {
		echo "htdocs: " . implode(', ', array_diff(scandir('/home/ecomae/htdocs') ?: array(), array('.', '..'))) . "\n";
	}
	exit("cp docroot not found\n");
}

echo "src={$src}\ndest={$dest}\n";

if (!function_exists('exec')) {
	exit("exec disabled\n");
}

$cmd = 'cp -a ' . escapeshellarg($src . '/.') . ' ' . escapeshellarg($dest . '/') . ' 2>&1';
exec($cmd, $out, $code);
echo "cp: " . implode("\n", array_slice($out, 0, 12)) . "\nexit={$code}\n";

$wwwCfg = is_file($src . '/config.local.php') ? $src . '/config.local.php' : '';
$dbPass = '';
if ($wwwCfg !== '') {
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
if ($dbPass !== '') {
	$cpCfg = str_replace('DBPASS', addslashes($dbPass), $cpCfg);
} else {
	$cpCfg = str_replace("'DBPASS'", "''", $cpCfg);
}
file_put_contents($dest . '/config.local.php', $cpCfg);
echo "Wrote cp config.local.php (domain_path=cp.ecomae.com)\n";

$checks = array('index.php', 'cp/index.php', 'core/dp_core.php', 'config.local.php');
foreach ($checks as $rel) {
	echo "{$rel}=" . (is_file($dest . '/' . $rel) ? 'yes' : 'no') . "\n";
}
echo "Done.\n";
