<?php
/**
 * Fix taxofinca 404/403: CLP repoint orphan site + nginx alias on epartscart.
 * https://www.epartscart.com/epc-taxofinca-nginx-fix.php?token=...&clp_pass=...
 */
declare(strict_types=1);
require_once __DIR__ . '/epc_deploy_auth.php';
epc_deploy_require_token();
header('Content-Type: text/plain; charset=utf-8');
set_time_limit(180);

$clpPass = trim((string) ($_GET['clp_pass'] ?? ''));
$hostname = 'www.taxofinca.com';
$bare = 'taxofinca.com';
$platformSite = 'www.epartscart.com';
$sharedRoot = '/home/epartscart/htdocs/www.epartscart.com';
$aliases = array($hostname, $bare);

if ($clpPass !== '') {
	require_once __DIR__ . '/content/general_pages/epc_cloudpanel_helpers.php';
	$cookie = '';
	if (empty(epc_clp_web_login('admin', $clpPass, $cookie)['ok'])) {
		echo "CloudPanel login failed\n";
	} else {
		echo "CloudPanel OK\n";
		$dash = epc_clp_web_request(epc_clp_panel_url() . '/', array(), $cookie);
		foreach (array_unique($aliases) as $orphan) {
			if (!epc_clp_web_site_listed($dash, $orphan)) {
				echo "No separate site {$orphan}\n";
				continue;
			}
			echo "Orphan site {$orphan} exists — delete or repoint\n";
			$del = epc_clp_web_delete_site($cookie, $orphan);
			echo '  ' . implode(' | ', $del['log']) . "\n";
			$dash2 = epc_clp_web_request(epc_clp_panel_url() . '/', array(), $cookie);
			if (epc_clp_web_site_listed($dash2, $orphan)) {
				$rep = epc_clp_web_set_site_docroot($cookie, $orphan, $sharedRoot);
				echo '  repoint: ' . implode(' | ', $rep['log']) . "\n";
			}
		}
		$vh = epc_clp_vhost_configure_tenant_direct_php($cookie, $platformSite, $aliases, $platformSite);
		echo implode("\n", $vh['log']) . "\n";
		@exec('chmod -R o+rX ' . escapeshellarg($sharedRoot) . ' 2>&1');
	}
}

echo "\n--- nginx file patch (sudo) ---\n";
$patched = 0;
$grepCmd = 'sudo -n grep -l taxofinca /etc/nginx/sites-enabled/ 2>/dev/null';
exec($grepCmd, $confFiles, $grepCode);
if (empty($confFiles)) {
	exec('sudo -n ls /etc/nginx/sites-enabled/ 2>/dev/null', $allFiles);
	foreach ($allFiles as $file) {
		if (stripos($file, 'epartscart') !== false) {
			$confFiles[] = '/etc/nginx/sites-enabled/' . $file;
		}
	}
}
foreach ($confFiles as $full) {
	$full = trim($full);
	if ($full === '' || !is_readable($full)) {
		continue;
	}
	$content = (string) file_get_contents($full);
	$orig = $content;
	$hasAlias = false;
	foreach ($aliases as $alias) {
		if (stripos($content, $alias) !== false) {
			$hasAlias = true;
			break;
		}
	}
	if (!$hasAlias && stripos($content, 'www.epartscart.com') !== false && preg_match('/^\s*server_name\s+([^;]+);/m', $content, $m)) {
		if (stripos($m[1], 'www.epartscart.com') !== false) {
			$newNames = trim($m[1]) . ' ' . implode(' ', $aliases);
			$content = preg_replace('/^\s*server_name\s+[^;]+;/m', '    server_name ' . $newNames . ';', $content, 1);
		}
	}
	// Drop taxofinca from redirect-only blocks (server_name without epartscart).
	if (preg_match_all('/^\s*server_name\s+([^;]+);/m', $content, $blocks, PREG_OFFSET_CAPTURE)) {
		foreach ($blocks[1] as $i => $cap) {
			$names = $cap[0];
			if (stripos($names, 'www.epartscart.com') !== false) {
				continue;
			}
			$line = $blocks[0][$i][0];
			$newLine = $line;
			foreach ($aliases as $alias) {
				if (stripos($names, $alias) !== false) {
					$parts = array_values(array_filter(preg_split('/\s+/', trim($names)), function ($n) use ($alias) {
						return strcasecmp($n, $alias) !== 0;
					}));
					$newLine = '  server_name ' . implode(' ', $parts) . ';';
				}
			}
			if ($newLine !== $line) {
				$content = str_replace($line, $newLine, $content);
			}
		}
	}
	if ($content !== $orig) {
		if (@file_put_contents($full, $content)) {
			echo "patched {$full}\n";
			$patched++;
		} else {
			echo "FAIL write {$full} (need root)\n";
		}
	}
}

exec('sudo -n nginx -t 2>&1', $testOut, $testCode);
echo "nginx -t code={$testCode}\n" . implode("\n", $testOut) . "\n";
if ($testCode === 0 && $patched > 0) {
	exec('sudo -n systemctl reload nginx 2>&1', $reloadOut, $reloadCode);
	echo "reload code={$reloadCode}\n";
}

$ctx = stream_context_create(array(
	'http' => array('timeout' => 20, 'header' => "Host: {$hostname}\r\n", 'ignore_errors' => true),
	'ssl' => array('verify_peer' => false, 'verify_peer_name' => false),
));
$body = @file_get_contents('http://127.0.0.1/', false, $ctx);
echo "\norigin Host {$hostname}: ";
if ($body === false) {
	echo "failed\n";
} else {
	$code = 0;
	if (isset($http_response_header[0]) && preg_match('/\s(\d{3})\s/', $http_response_header[0], $m)) {
		$code = (int) $m[1];
	}
	$hint = stripos($body, '403 Forbidden') !== false ? 'nginx403' : (stripos($body, '404') !== false ? 'nginx404' : 'ok');
	echo "HTTP {$code} {$hint} bytes=" . strlen($body) . "\n";
}

$pub = @file_get_contents('https://' . $hostname . '/', false, stream_context_create(array(
	'ssl' => array('verify_peer' => false, 'verify_peer_name' => false),
	'http' => array('timeout' => 20, 'ignore_errors' => true),
)));
echo "public https://{$hostname}/: ";
if ($pub === false) {
	echo "failed\n";
} else {
	$code = 0;
	if (isset($http_response_header[0]) && preg_match('/\s(\d{3})\s/', $http_response_header[0], $m)) {
		$code = (int) $m[1];
	}
	echo "HTTP {$code} len=" . strlen($pub) . "\n";
}
