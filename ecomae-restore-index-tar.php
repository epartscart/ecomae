<?php
declare(strict_types=1);
require_once __DIR__ . '/epc_deploy_auth.php';
epc_deploy_require_token();
header('Content-Type: text/plain; charset=utf-8');

$tar = '/tmp/ecomae-www-export.tar.gz';
if (!is_file($tar)) {
	$tar = '/tmp/ecomae-full-site.tar.gz';
}
if (!is_file($tar)) {
	exit("missing tar\n");
}
$members = array(
	'./index.php',
	'www.epartscart.com/index.php',
	'index.php',
);
$body = '';
foreach ($members as $member) {
	$tmp = '/tmp/ecomae-restore-index.php';
	@unlink($tmp);
	exec('tar -xOzf ' . escapeshellarg($tar) . ' ' . escapeshellarg($member) . ' > ' . escapeshellarg($tmp) . ' 2>&1', $o, $c);
	if ($c === 0 && is_file($tmp) && filesize($tmp) > strlen($body)) {
		$body = (string) file_get_contents($tmp);
		echo "member {$member} size=" . strlen($body) . "\n";
	}
}
if ($body === '' || strlen($body) < 5000) {
	exit("no suitable index in tar (best=" . strlen($body) . ")\n");
}
foreach (array(
	__DIR__ . '/index.php',
	'/home/ecomae/htdocs/www.ecomae.com/index.php',
	'/home/ecomaecp/htdocs/cp.ecomae.com/index.php',
) as $dest) {
	if (file_put_contents($dest, $body) !== false) {
		echo "restored {$dest} bytes=" . strlen($body) . "\n";
	}
}
echo "done\n";
