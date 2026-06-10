<?php
if (!isset($_GET['token']) || $_GET['token'] !== 'epartscart-deploy-2026') {
    exit('Forbidden');
}
header('Content-Type: text/plain; charset=utf-8');

$root = __DIR__;
$files = array(
    'epc-integrations-setup.php',
    'epc-integrations-enable.php',
    'epc-shop_payment_systems-seed.sql',
    'epc-sms_api-seed.sql',
    'epc-shop_storages_interfaces_types-seed.sql',
);

foreach ($files as $file) {
    $path = $root . '/' . $file;
    if (is_file($path)) {
        echo (@unlink($path) ? 'Removed ' : 'Failed ') . $file . "\n";
    }
}

function epc_remove_dir($dir)
{
    if (!is_dir($dir)) {
        return;
    }
    $items = scandir($dir);
    if ($items === false) {
        return;
    }
    foreach ($items as $item) {
        if ($item === '.' || $item === '..') {
            continue;
        }
        $path = $dir . '/' . $item;
        if (is_dir($path)) {
            epc_remove_dir($path);
        } else {
            @unlink($path);
        }
    }
    @rmdir($dir);
}

epc_remove_dir($root . '/content/sms/sms');
@unlink(__FILE__);
echo "Cleanup done\n";
?>
