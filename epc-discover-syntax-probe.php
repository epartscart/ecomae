<?php
declare(strict_types=1);
require_once __DIR__ . '/epc_deploy_auth.php';
epc_deploy_require_token();
header('Content-Type: text/plain; charset=utf-8');
$path = __DIR__ . '/content/shop/price_engine/epc_auto_price_engine.php';
echo 'bytes=' . filesize($path) . "\nmd5=" . md5_file($path) . "\n";
$out = array();
exec('php -l ' . escapeshellarg($path) . ' 2>&1', $out, $code);
echo implode("\n", $out) . "\n";
