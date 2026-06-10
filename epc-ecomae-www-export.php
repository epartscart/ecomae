<?php
declare(strict_types=1);
require_once __DIR__ . '/epc_deploy_auth.php';
epc_deploy_require_token();
header('Content-Type: text/plain; charset=utf-8');
set_time_limit(0);

$src = __DIR__;
$tar = '/tmp/ecomae-www-export.tar.gz';
@unlink($tar);
$cmd = 'tar -czf ' . escapeshellarg($tar) . ' --ignore-failed-read -C ' . escapeshellarg($src) . ' . 2>&1';
exec($cmd, $out, $code);
echo implode("\n", array_slice($out, 0, 8)) . "\nexit={$code}\n";
echo 'tar=' . (is_file($tar) ? filesize($tar) : 0) . "\n";
@chmod($tar, 0644);
