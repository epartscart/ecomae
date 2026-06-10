<?php
declare(strict_types=1);
require_once __DIR__ . '/epc_deploy_auth.php';
epc_deploy_require_token();
header('Content-Type: text/plain; charset=utf-8');

$paths = array(
	'/home/ecomaecp/htdocs/cp.ecomae.com',
	'/home/ecomae/htdocs/cp.ecomae.com',
);
foreach ($paths as $path) {
	echo "{$path}\n";
	echo '  is_dir=' . (is_dir($path) ? 'yes' : 'no') . "\n";
	echo '  writable=' . (is_writable(dirname($path)) ? 'parent-yes' : 'parent-no') . "\n";
	$test = @file_put_contents($path . '/.write-test', 'ok');
	echo '  write_test=' . ($test === false ? 'fail' : 'ok') . "\n";
	if ($test !== false) {
		@unlink($path . '/.write-test');
	}
}

foreach (glob('/etc/nginx/sites-enabled/*') ?: array() as $conf) {
	$text = @file_get_contents($conf);
	if ($text === false || stripos($text, 'ecomae') === false) {
		continue;
	}
	echo "\n=== {$conf} ===\n";
	foreach (preg_split('/\r?\n/', $text) as $line) {
		if (preg_match('/server_name|root\s|listen\s/', $line)) {
			echo trim($line) . "\n";
		}
	}
}
