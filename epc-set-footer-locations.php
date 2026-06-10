<?php
if (!isset($_GET['token']) || $_GET['token'] !== 'epartscart-deploy-2026') {
    exit('Forbidden');
}
header('Content-Type: text/plain; charset=utf-8');

$configPath = __DIR__ . '/config.php';
$value = 'UAE - Dubai Head Office\nAddress: Dubai, United Arab Emirates\nContact person: Sales Manager\nPhone: +971-567607011\nEmail: partsdoc2025@gmail.com\nMap: https://...\n\nOman - Muscat Location\nAddress: Muscat, Oman\nContact person: Branch Coordinator\nPhone: +968-XXXXXXX\nEmail: oman@example.com\n\nSaudi Arabia - Riyadh Location\nAddress: Riyadh, Saudi Arabia\nContact person: KSA Sales\nPhone: +966-XXXXXXX';

if (!is_file($configPath)) {
    echo "config.php not found\n";
    exit;
}

$config = file_get_contents($configPath);
if ($config === false) {
    echo "Unable to read config.php\n";
    exit;
}

$line = "\tpublic \$epc_global_locations_countries = '" . $value . "';/*Footer global countries and locations*/";
if (strpos($config, 'public $epc_global_locations_countries') !== false) {
    $config = preg_replace('/\tpublic \$epc_global_locations_countries[^\n]*\n/', $line . "\n", $config, 1);
    echo "Updated epc_global_locations_countries\n";
} else {
    $config = preg_replace('/(\tpublic \$epc_global_locations_summary[^\n]*\n)/', '$1' . $line . "\n", $config, 1);
    echo "Added epc_global_locations_countries\n";
}

file_put_contents($configPath, $config);
@unlink(__FILE__);
echo "Done\n";
?>
