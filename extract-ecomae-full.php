<?php
require_once __DIR__ . '/epc_deploy_auth.php';
epc_deploy_require_token();
header('Content-Type: text/plain; charset=utf-8');
set_time_limit(0);
$tar = '/tmp/docpart-epartscart-site.zip';
$dest = __DIR__;
if (!is_file($tar)) { exit("archive missing at {$tar}\n"); }
@mkdir('/tmp/ecomae-extract', 0755, true);
exec('rm -rf /tmp/ecomae-extract/* 2>&1');
exec('tar -xzf ' . escapeshellarg($tar) . ' -C /tmp/ecomae-extract 2>&1', $o, $c);
echo implode("\n", $o) . "\ntar exit={$c}\n";
$src = '/tmp/ecomae-extract/www.epartscart.com';
if (!is_dir($src)) {
	foreach (glob('/tmp/ecomae-extract/*') ?: array() as $d) {
		if (is_dir($d)) { $src = $d; break; }
	}
}
if (!is_dir($src)) { exit("extract dir not found\n"); }
exec('cp -a ' . escapeshellarg($src . '/.') . ' ' . escapeshellarg($dest) . '/ 2>&1', $o2, $c2);
echo implode("\n", array_slice($o2, 0, 8)) . "\ncp exit={$c2}\n";
@unlink($tar);
echo "Done. index=" . (is_file($dest . '/index.php') ? 'yes' : 'no') . " cp=" . (is_file($dest . '/cp/index.php') ? 'yes' : 'no') . "\n";