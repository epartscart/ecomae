<?php
// Runs PHP content/shop/order_process/get_customer_offices.php for every scenario and cookie.
// Usage: ECOMAE_LOCAL_MARIADB_E2E_DSN=... php harness.php /workspace > golden.json
$root = rtrim($argv[1] ?? '/workspace', '/');
$password = getenv('ECOMAE_LOCAL_MARIADB_E2E_DSN');
$spec = json_decode(file_get_contents(__DIR__ . '/scenarios.json'), true);
$admin = new PDO('mysql:host=127.0.0.1;port=3306;dbname=mysql', 'ecomae', $password, array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION));
$results = array();
foreach ($spec['scenarios'] as $scenario) {
	$name = 'ecomae_cpw_' . substr(md5(uniqid('', true)), 0, 12);
	$admin->exec('CREATE DATABASE `' . $name . '`');
	try {
		$db_link = new PDO('mysql:host=127.0.0.1;port=3306;dbname=' . $name . ';charset=utf8', 'ecomae', $password, array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION));
		foreach (array_merge($spec['schema'], $scenario['setup']) as $sql) {
			$db_link->exec($sql);
		}
		foreach ($scenario['cookies'] as $cookie) {
			$_COOKIE = array();
			if ($cookie !== null) {
				$_COOKIE['my_city'] = $cookie;
			}
			$customer_offices = null;
			include $root . '/content/shop/order_process/get_customer_offices.php';
			$results[] = array('scenario' => $scenario['name'], 'cookie' => $cookie, 'offices' => array_map('intval', $customer_offices));
		}
	} finally {
		$admin->exec('DROP DATABASE `' . $name . '`');
	}
}
echo json_encode(array('php' => PHP_VERSION, 'results' => $results), JSON_PRETTY_PRINT | JSON_UNESCAPED_UNICODE), "\n";
