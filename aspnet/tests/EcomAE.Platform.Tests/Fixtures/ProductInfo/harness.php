<?php
// PHP 8.3 runtime goldens for the non-front/catalogue branch of the authoritative
// printProduct_Info.php. Every case owns and drops an ecomae_cpw_* schema.
$root = rtrim($argv[1] ?? '/workspace', '/');
$spec = json_decode(file_get_contents(__DIR__ . '/cases.json'), true);
$password = getenv('ECOMAE_LOCAL_MARIADB_E2E_DSN');
ini_set('display_errors', 'stderr');

function translate_str_by_id($key) { return '{' . $key . '}'; }

$admin = new PDO('mysql:host=127.0.0.1;port=3306;dbname=mysql', 'ecomae', $password, array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION));
$results = array();
foreach ($spec['cases'] as $case) {
    $name = 'ecomae_cpw_' . substr(md5(uniqid('', true)), 0, 12);
    $admin->exec('CREATE DATABASE `' . $name . '`');
    try {
        $db_link = new PDO('mysql:host=127.0.0.1;port=3306;dbname=' . $name . ';charset=utf8mb4', 'ecomae', $password, array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION));
        foreach (array_merge($spec['schema'], $spec['base'], $case['setup']) as $sql) {
            $db_link->exec($sql);
        }
        $DP_Config = (object)array(
            'shop_currency'=>'784', 'currency_show_mode'=>'sign_before',
            'domain_path'=>'https://shop.example/', 'backend_dir'=>'cp',
            'price_rounding'=>'0', 'product_url'=>'alias',
            'chpu_search_config'=>array('chpu_search_on'=>false)
        );
        $_SERVER['DOCUMENT_ROOT'] = $root;
        $_SERVER['REQUEST_URI'] = '/en/filters/part-seven';
        $_REQUEST = array('product_id'=>7);
        $_COOKIE = array();
        $product_id = 7;
        $isFrontMode = false;
        if (!defined('_ASTEXE_')) define('_ASTEXE_', true);
        ob_start();
        include $root . '/content/shop/catalogue/printProduct_Info.php';
        $html = ob_get_clean();
        $results[] = array('name'=>$case['name'], 'html'=>$html);
    } finally {
        $admin->exec('DROP DATABASE IF EXISTS `' . $name . '`');
    }
}

echo json_encode(array(
    'php'=>PHP_VERSION,
    'source'=>'content/shop/catalogue/printProduct_Info.php',
    'results'=>$results,
    'unsupported_property_types'=>$spec['unsupported_property_types']
), JSON_PRETTY_PRINT | JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES), "\n";
