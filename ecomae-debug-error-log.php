<?php
declare(strict_types=1);
require_once __DIR__ . '/epc_deploy_auth.php';
epc_deploy_require_token();
header('Content-Type: text/plain; charset=utf-8');

$paths = array(
	'/home/ecomae/logs/php/error.log',
	'/var/log/php-fpm/www-error.log',
	'/var/log/nginx/error.log',
	'/home/ecomae/htdocs/www.ecomae.com/php_errors.log',
);
foreach ($paths as $p) {
	echo "=== $p ===\n";
	if (!is_readable($p)) {
		echo "not readable\n\n";
		continue;
	}
	$lines = @file($p, FILE_IGNORE_NEW_LINES);
	if (!is_array($lines)) {
		echo "read fail\n\n";
		continue;
	}
	$tail = array_slice($lines, -30);
	foreach ($tail as $line) {
		if (stripos($line, 'pos') !== false || stripos($line, 'Fatal') !== false || stripos($line, 'Jun') !== false) {
			echo $line . "\n";
		}
	}
	echo "\n";
}
