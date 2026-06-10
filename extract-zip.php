<?php
declare(strict_types=1);
require_once __DIR__ . '/epc_deploy_auth.php';
epc_deploy_require_token();
header('Content-Type: text/plain; charset=utf-8');
$zip = '/tmp/docpart-epartscart-site.zip';
if (!is_file($zip)) {
    exit('Zip missing at ' . $zip);
}
$target = __DIR__;
if (!is_dir($target)) {
    mkdir($target, 0755, true);
}
$cmd = 'unzip -o ' . escapeshellarg($zip) . ' -d ' . escapeshellarg($target) . ' 2>&1';
if (!function_exists('exec')) {
    exit('exec disabled');
}
exec($cmd, $out, $code);
echo implode("\n", $out);
echo "\nexit=$code\n";
if ($code === 0) {
    @unlink($zip);
    echo "Done. index.php exists: " . (is_file($target . '/index.php') ? 'yes' : 'no');
}
