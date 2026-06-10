<?php
/**
 * Discover taxofinca docroot on VPS (diagnostic).
 * https://www.epartscart.com/epc-taxofinca-find-root.php?token=epartscart-deploy-2026
 */
declare(strict_types=1);
require_once __DIR__ . '/epc_deploy_auth.php';
epc_deploy_require_token();
header('Content-Type: text/plain; charset=utf-8');

if (!function_exists('exec')) {
	exit("exec disabled\n");
}

foreach (array('127.0.0.1', '31.97.216.247') as $ip) {
	$ctx = stream_context_create(array('http' => array('timeout' => 15, 'header' => "Host: www.taxofinca.com\r\n")));
	$body = @file_get_contents('http://' . $ip . '/', false, $ctx);
	echo "probe {$ip}: ";
	if ($body === false) {
		echo "failed\n";
	} else {
		echo 'bytes=' . strlen($body) . ' wp=' . (strpos($body, 'wp-content') !== false ? 'yes' : 'no') . "\n";
	}
}
$body = @file_get_contents('https://www.taxofinca.com/', false, stream_context_create(array(
	'http' => array('timeout' => 20),
	'ssl' => array('verify_peer' => false, 'verify_peer_name' => false),
)));
echo 'probe https://www.taxofinca.com/: ';
echo ($body === false) ? "failed\n" : ('bytes=' . strlen($body) . ' wp=' . (strpos($body, 'wp-content') !== false ? 'yes' : 'no') . "\n");
echo "\n";
echo "find wp-config under epartscart:\n";
exec('find /home/epartscart -maxdepth 8 -name wp-config.php 2>/dev/null', $epWp);
echo implode("\n", $epWp) . "\n\n";

echo "grep taxofinca under epartscart htdocs:\n";
exec('grep -r "taxofinca" /home/epartscart/htdocs 2>/dev/null | head -15', $grep);
echo implode("\n", $grep) . "\n\n";

exec('sudo -u taxofinca ls -la /home/taxofinca/htdocs/ 2>/dev/null', $ls);
echo "taxofinca htdocs:\n" . implode("\n", $ls) . "\n\n";

echo "nginx taxofinca:\n";
exec('grep -r "taxofinca" /etc/nginx/ 2>/dev/null | head -40', $ng);
echo implode("\n", $ng) . "\n\n";

echo "clpctl site:list:\n";
exec('clpctl site:list 2>&1', $clp);
echo implode("\n", $clp) . "\n\n";

foreach (array('taxofinca', 'www.taxofinca.com', 'taxofinca.com') as $site) {
	echo "clpctl site:list:show {$site}:\n";
	exec('clpctl site:list:show ' . escapeshellarg($site) . ' 2>&1', $show);
	echo implode("\n", $show) . "\n\n";
}

echo "clpctl user:list:show taxofinca:\n";
exec('clpctl user:list:show taxofinca 2>&1', $userShow);
echo implode("\n", $userShow) . "\n\n";

echo "clpctl app:list:\n";
exec('clpctl app:list 2>&1', $apps);
echo implode("\n", $apps) . "\n\n";

echo "try readable nginx paths:\n";
exec('grep -r "taxofinca" /home/clp 2>/dev/null | head -20', $clpNg);
echo implode("\n", $clpNg) . "\n";
