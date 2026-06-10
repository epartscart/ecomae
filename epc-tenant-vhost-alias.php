<?php
/**
 * Add tenant hostname as nginx alias on an existing live site (Model C).
 * https://www.epartscart.com/epc-tenant-vhost-alias.php?token=...&clp_pass=...&hostname=www.taxofinca.com&platform_site=www.epartscart.com
 */
declare(strict_types=1);

require_once __DIR__ . '/epc_deploy_auth.php';
epc_deploy_require_token();
header('Content-Type: text/plain; charset=utf-8');
set_time_limit(120);

require_once __DIR__ . '/content/general_pages/epc_cloudpanel_helpers.php';

$clpPass = trim((string) ($_GET['clp_pass'] ?? ''));
$hostname = strtolower(trim((string) ($_GET['hostname'] ?? 'www.taxofinca.com')));
$platformSite = strtolower(trim((string) ($_GET['platform_site'] ?? 'www.epartscart.com')));

if ($clpPass === '' || $hostname === '') {
	exit("clp_pass and hostname required\n");
}

$docroots = array(
	'www.epartscart.com' => '/home/epartscart/htdocs/www.epartscart.com',
	'www.ecomae.com' => '/home/ecomae/htdocs/www.ecomae.com',
);
$docroot = isset($docroots[$platformSite]) ? $docroots[$platformSite] : '/home/epartscart/htdocs/www.epartscart.com';

echo "=== vhost alias {$hostname} on {$platformSite} ===\n";

$cookie = '';
if (empty(epc_clp_web_login('admin', $clpPass, $cookie)['ok'])) {
	exit("CloudPanel login failed\n");
}
echo "CloudPanel login OK\n";

$panel = epc_clp_panel_url();
$vhHtml = epc_clp_web_request($panel . '/site/' . rawurlencode($platformSite) . '/vhost', array(), $cookie);
echo "vhost page len=" . strlen($vhHtml) . "\n";

$vhToken = '';
$vhost = '';
if (preg_match('/name="token" value="([^"]+)"/', $vhHtml, $vt)) {
	$vhToken = $vt[1];
}
if (preg_match('/<div id="editor">([\s\S]*?)<\/div>\s*<textarea/s', $vhHtml, $em)) {
	$vhost = html_entity_decode($em[1], ENT_QUOTES | ENT_HTML5, 'UTF-8');
} elseif (preg_match('/<textarea[^>]*(?:name|id)="vhost-template"[^>]*>([\s\S]*?)<\/textarea>/i', $vhHtml, $vm)) {
	$vhost = html_entity_decode($vm[1], ENT_QUOTES | ENT_HTML5, 'UTF-8');
}

$bare = preg_replace('/^www\./', '', $hostname);
$aliasHosts = array_values(array_unique(array_filter(array($hostname, $bare))));
$anchorWww = $platformSite;

// Remove tenant hostnames from bare-domain redirect blocks (they must not 301 to another tenant).
foreach ($aliasHosts as $aliasHost) {
	$vhost = preg_replace_callback(
		'/^\s*server_name\s+([^;]+);/m',
		function ($m) use ($aliasHost, $anchorWww) {
			$line = $m[0];
			if (stripos($line, $anchorWww) !== false) {
				return $line;
			}
			if (stripos($line, $aliasHost) === false) {
				return $line;
			}
			$names = preg_split('/\s+/', trim($m[1]));
			$names = array_values(array_filter($names, function ($n) use ($aliasHost) {
				return strcasecmp($n, $aliasHost) !== 0;
			}));
			echo "Removed {$aliasHost} from redirect block: " . trim($m[1]) . "\n";
			return '  server_name ' . implode(' ', $names) . ';';
		},
		$vhost
	);
}

$added = array();
foreach ($aliasHosts as $aliasHost) {
	if ($aliasHost === '' || stripos($vhost, $aliasHost) !== false) {
		continue;
	}
	$vhost = preg_replace_callback(
		'/^\s*server_name\s+([^;]+);/m',
		function ($m) use ($aliasHost, $anchorWww, &$added) {
			if (stripos($m[1], $anchorWww) === false) {
				return $m[0];
			}
			if (stripos($m[1], $aliasHost) !== false) {
				return $m[0];
			}
			$added[] = $aliasHost;
			echo "Added {$aliasHost} to primary block with {$anchorWww}\n";
			return preg_replace('/;\s*$/', ' ' . $aliasHost . ';', $m[0]);
		},
		$vhost
	);
}

if ($vhost !== '' && $vhToken !== '' && $added !== array()) {
	epc_clp_web_request($panel . '/site/' . rawurlencode($platformSite) . '/vhost', array(
		'method' => 'POST',
		'body' => http_build_query(array(
			'vhost-update' => '1',
			'vhost-template' => $vhost,
			'token' => $vhToken,
		)),
	), $cookie);
	echo "Saved vhost on {$platformSite}\n";
} elseif ($vhost !== '' && stripos($vhost, $hostname) !== false) {
	echo "Alias already in vhost template\n";
} else {
	echo "WARN: vhost update failed token=" . ($vhToken !== '' ? 'yes' : 'no') . ' vhost_len=' . strlen($vhost) . "\n";
	if (strlen($vhHtml) < 500) {
		echo substr($vhHtml, 0, 400) . "\n";
	}
}

foreach (array_unique(array($hostname, $bare)) as $orphan) {
	$del = epc_clp_web_delete_site($cookie, $orphan);
	echo "Remove orphan {$orphan}: " . implode(' ', array_slice($del['log'], 0, 2)) . "\n";
}

if (is_dir($docroot)) {
	exec('chmod -R o+rX ' . escapeshellarg($docroot) . ' 2>&1', $chmodOut, $chmodCode);
	echo "chmod code={$chmodCode}\n";
}

$ssl = epc_clp_web_install_ssl($cookie, $platformSite);
echo 'SSL: ' . implode(' | ', array_slice($ssl['log'], 0, 2)) . "\n";

function probe_host(string $url, string $host): string
{
	$ctx = stream_context_create(array(
		'http' => array('timeout' => 20, 'ignore_errors' => true, 'header' => "Host: {$host}\r\n"),
		'ssl' => array('verify_peer' => false, 'verify_peer_name' => false),
	));
	$body = @file_get_contents($url, false, $ctx);
	$code = 0;
	if (isset($http_response_header[0]) && preg_match('/\s(\d{3})\s/', $http_response_header[0], $m)) {
		$code = (int) $m[1];
	}
	$flag = ($body !== false && stripos((string) $body, 'Temporarily offline') !== false) ? ' [OFFLINE]' : '';
	return "HTTP {$code}{$flag}";
}

echo "\n=== Probes ===\n";
echo "origin: " . probe_host('http://127.0.0.1/', $hostname) . "\n";
echo "https://{$hostname}/: " . probe_host('https://' . $hostname . '/', '') . "\n";
echo "https://{$platformSite}/: " . probe_host('https://' . $platformSite . '/', '') . "\n";
