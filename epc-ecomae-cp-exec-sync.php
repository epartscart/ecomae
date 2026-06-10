<?php
declare(strict_types=1);
require_once __DIR__ . '/epc_deploy_auth.php';
epc_deploy_require_token();
header('Content-Type: text/plain; charset=utf-8');

$src = '/home/ecomae/htdocs/www.ecomae.com';
$dest = '/home/ecomaecp/htdocs/cp.ecomae.com';
echo "src exists=" . (is_dir($src) ? 'yes' : 'no') . "\n";
echo "dest exists=" . (is_dir($dest) ? 'yes' : 'no') . "\n";
$cmd = 'cp -a ' . escapeshellarg($src . '/.') . ' ' . escapeshellarg($dest . '/') . ' 2>&1';
exec($cmd, $out, $code);
echo implode("\n", array_slice($out, 0, 12)) . "\nexit={$code}\n";
echo "dest index=" . (is_file($dest . '/index.php') ? 'yes' : 'no') . "\n";
