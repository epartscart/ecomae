<?php
declare(strict_types=1);
require_once __DIR__ . '/epc_deploy_auth.php';
epc_deploy_require_token();
header('Content-Type: text/plain; charset=utf-8');

foreach (glob('/home/*', GLOB_ONLYDIR) ?: array() as $home) {
	echo $home . "\n";
	foreach (glob($home . '/htdocs/*', GLOB_ONLYDIR) ?: array() as $doc) {
		echo "  " . basename($doc) . " index=" . (is_file($doc . '/index.php') ? filesize($doc . '/index.php') : 'no') . "\n";
	}
}
