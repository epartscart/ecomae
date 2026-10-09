<?php
// Executes the authoritative storefront orders_background.php against isolated MariaDB schemas.
// Usage: ECOMAE_LOCAL_MARIADB_E2E_DSN=... php harness.php /workspace > golden.json
ini_set('display_errors', 'stderr');
$root = rtrim($argv[1] ?? '/workspace', '/');
$spec = json_decode(file_get_contents(__DIR__ . '/cases.json'), true);
$password = getenv('ECOMAE_LOCAL_MARIADB_E2E_DSN');
$admin = new PDO(
    'mysql:host=127.0.0.1;port=3306;dbname=mysql',
    'ecomae',
    $password,
    array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION)
);

function named_rows($rows)
{
    $result = array();
    foreach ($rows as $id => $row) {
        $fields = array();
        foreach ($row as $key => $value) {
            if (is_string($key)) {
                $fields[$key] = $value;
            }
        }
        $result[(string)$id] = $fields;
    }
    return $result;
}

function counts($db)
{
    $result = array();
    foreach (array('shop_orders_statuses_ref', 'shop_orders_items_statuses_ref', 'shop_offices') as $table) {
        $result[$table] = (int)$db->query('SELECT COUNT(*) FROM `' . $table . '`')->fetchColumn();
    }
    return $result;
}

$results = array();
foreach ($spec['cases'] as $case) {
    $name = 'ecomae_cpw_' . substr(md5(uniqid('', true)), 0, 12);
    $admin->exec('CREATE DATABASE `' . $name . '`');
    try {
        $db_link = new PDO(
            'mysql:host=127.0.0.1;port=3306;dbname=' . $name . ';charset=utf8',
            'ecomae',
            $password,
            array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION)
        );
        foreach (array_merge($spec['schema'], $case['setup']) as $sql) {
            $db_link->exec($sql);
        }
        $before = counts($db_link);
        include $root . '/content/shop/order_process/orders_background.php';
        $after = counts($db_link);
        $results[] = array(
            'name' => $case['name'],
            'orders_statuses' => named_rows($orders_statuses),
            'orders_items_statuses' => named_rows($orders_items_statuses),
            'orders_items_statuses_not_count' => array_values($orders_items_statuses_not_count),
            'offices_list' => named_rows($offices_list),
            'counts_before' => $before,
            'counts_after' => $after
        );
    } finally {
        $admin->exec('DROP DATABASE IF EXISTS `' . $name . '`');
    }
}

echo json_encode(
    array(
        'php' => PHP_VERSION,
        'source' => 'content/shop/order_process/orders_background.php',
        'results' => $results
    ),
    JSON_PRETTY_PRINT | JSON_UNESCAPED_UNICODE
), "\n";
