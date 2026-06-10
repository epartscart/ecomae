<?php
declare(strict_types=1);
require_once __DIR__ . '/epc_deploy_auth.php';
epc_deploy_require_token();
header('Content-Type: text/plain; charset=utf-8');
require_once __DIR__ . '/content/general_pages/epc_cloudpanel_helpers.php';

$clpPass = trim((string) ($_GET['clp_pass'] ?? ''));
$platformSite = 'www.ecomae.com';
$aliases = array('cp.ecomae.com', 'www.epartscart.com', 'epartscart.com');
if ($clpPass === '') {
	exit("clp_pass required\n");
}
$cookie = '';
if (empty(epc_clp_web_login('admin', $clpPass, $cookie)['ok'])) {
	exit("CloudPanel login failed\n");
}
$panel = epc_clp_panel_url();
$vhHtml = epc_clp_web_request($panel . '/site/' . rawurlencode($platformSite) . '/vhost', array(), $cookie);
if (!preg_match('/name="token" value="([^"]+)"/', $vhHtml, $vt)) {
	exit("vhost token missing\n");
}
$vhost = '';
if (preg_match('/<div id="editor">([\s\S]*?)<\/div>\s*<textarea/s', $vhHtml, $em)) {
	$vhost = html_entity_decode($em[1], ENT_QUOTES | ENT_HTML5, 'UTF-8');
} elseif (preg_match('/<textarea[^>]*name="vhost-template"[^>]*>([\s\S]*?)<\/textarea>/', $vhHtml, $tm)) {
	$vhost = html_entity_decode($tm[1], ENT_QUOTES | ENT_HTML5, 'UTF-8');
}
if ($vhost === '') {
	exit("vhost editor missing\n");
}
$changed = false;

// Hostnames that must NOT sit on the bare-domain redirect vhost (they 301 to www.ecomae.com).
$stripFromRedirect = array('cp.ecomae.com', 'www.epartscart.com', 'epartscart.com');
$vhost = preg_replace_callback('/^\s*server_name\s+([^;]+);/m', function ($m) use ($stripFromRedirect, &$changed) {
	$names = preg_split('/\s+/', trim($m[1]));
	if (!in_array('ecomae.com', $names, true) || in_array('www.ecomae.com', $names, true)) {
		return $m[0];
	}
	$filtered = array_values(array_filter($names, function ($name) use ($stripFromRedirect) {
		return !in_array($name, $stripFromRedirect, true);
	}));
	if ($filtered === $names) {
		return $m[0];
	}
	$changed = true;
	echo 'redirect block cleaned: ' . implode(' ', $filtered) . "\n";
	return '  server_name ' . implode(' ', $filtered) . ';';
}, $vhost);

$aliasGroups = array(
	'www.ecomae.com' => array('cp.ecomae.com', 'www.epartscart.com', 'epartscart.com'),
);
foreach ($aliasGroups as $anchor => $hosts) {
	$vhost = preg_replace_callback('/^\s*server_name\s+([^;]+);/m', function ($m) use ($anchor, $hosts, &$changed) {
		if (stripos($m[1], $anchor) === false) {
			return $m[0];
		}
		$line = $m[0];
		foreach ($hosts as $alias) {
			if (stripos($line, $alias) !== false) {
				continue;
			}
			$line = preg_replace('/;\s*$/', ' ' . $alias . ';', $line);
			$changed = true;
			echo "added {$alias} to block with {$anchor}\n";
		}
		return $line;
	}, $vhost);
}

if (stripos($vhost, 'location /cp/') === false) {
	$cpBlock = <<<'NGINX'

  location = /cp {
    return 301 /cp/;
  }

  location /cp/ {
    try_files $uri $uri/ /cp/index.php?$args;
  }

NGINX;
	$vhost = preg_replace('/(\s+try_files \$uri \$uri\/ \/index\.php\?\$args;)/', $cpBlock . '$1', $vhost, -1, $cpCount);
	if (!empty($cpCount)) {
		$changed = true;
		echo "cp location added to {$cpCount} block(s)\n";
	}
}

// Ensure cp.ecomae.com is on the primary www block (required for Super CP hostname).
$vhost = preg_replace_callback('/^\s*server_name\s+([^;]+);/m', function ($m) use (&$changed) {
	if (stripos($m[1], 'www.ecomae.com') === false) {
		return $m[0];
	}
	if (stripos($m[1], 'cp.ecomae.com') !== false) {
		return $m[0];
	}
	$changed = true;
	echo "adding cp.ecomae.com to www server_name\n";
	return preg_replace('/;\s*$/', ' cp.ecomae.com;', $m[0]);
}, $vhost);

if (!$changed) {
	echo "no vhost text changes (aliases/cp block may already be ok)\n";
} else {
	epc_clp_web_request($panel . '/site/' . rawurlencode($platformSite) . '/vhost', array(
		'method' => 'POST',
		'body' => http_build_query(array(
			'vhost-update' => '1',
			'vhost-template' => $vhost,
			'token' => $vt[1],
		)),
	), $cookie);
	echo "vhost saved\n";
}

foreach (array('www.epartscart.com', 'cp.ecomae.com') as $drop) {
	$check = epc_clp_web_request($panel . '/site/' . rawurlencode($drop) . '/settings', array(), $cookie);
	if (strlen($check) < 500 || stripos($check, '404') !== false) {
		echo "skip delete {$drop} (absent)\n";
		continue;
	}
	$del = epc_clp_web_delete_site($cookie, $drop);
	echo "delete {$drop}: " . implode(' ', $del['log']) . "\n";
}

$ssl = epc_clp_web_install_ssl($cookie, $platformSite);
echo 'ssl www: ' . implode(' | ', array_slice($ssl['log'], 0, 2)) . "\n";

$perm = epc_clp_run('system:permissions:reset --directories=755 --files=644 --path=/home/ecomae/htdocs/www.ecomae.com');
echo 'perm: ' . substr($perm['output'], 0, 120) . "\n";
