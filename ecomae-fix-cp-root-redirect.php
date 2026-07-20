<?php
/**
 * Patch live nginx vhost: /cp and /cp/ → https://$host/cp/control (no HTTP hop).
 * GET: token=…&clp_pass=…
 */
declare(strict_types=1);
require_once __DIR__ . '/epc_deploy_auth.php';
epc_deploy_require_token();
header('Content-Type: text/plain; charset=utf-8');
require_once __DIR__ . '/content/general_pages/epc_cloudpanel_helpers.php';

$clpPass = trim((string) ($_GET['clp_pass'] ?? $_POST['clp_pass'] ?? ''));
$platformSite = 'www.ecomae.com';
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

$want = <<<'NGINX'
  location = /cp {
    return 301 https://$host/cp/control;
  }
  location = /cp/ {
    return 301 https://$host/cp/control;
  }
NGINX;

$changed = false;
// Replace legacy "return 301 /cp/;" blocks for exact /cp
$vhost2 = preg_replace(
	'/location\s*=\s*\/cp\s*\{[^}]*return\s+301\s+\/cp\/\s*;[^}]*\}/i',
	"location = /cp {\n    return 301 https://\$host/cp/control;\n  }",
	$vhost,
	-1,
	$n1
);
if ($n1 > 0) {
	$vhost = $vhost2;
	$changed = true;
	echo "patched location = /cp ({$n1})\n";
}

if (!preg_match('/location\s*=\s*\/cp\/\s*\{/i', $vhost)) {
	// Insert /cp/ exact redirect before location /cp/
	$vhost2 = preg_replace(
		'/(location\s+\/cp\/\s*\{)/i',
		"location = /cp/ {\n    return 301 https://\$host/cp/control;\n  }\n  $1",
		$vhost,
		1,
		$n2
	);
	if ($n2 > 0) {
		$vhost = $vhost2;
		$changed = true;
		echo "added location = /cp/\n";
	}
} else {
	$vhost2 = preg_replace(
		'/location\s*=\s*\/cp\/\s*\{[^}]*\}/i',
		"location = /cp/ {\n    return 301 https://\$host/cp/control;\n  }",
		$vhost,
		-1,
		$n3
	);
	if ($n3 > 0) {
		$vhost = $vhost2;
		$changed = true;
		echo "patched location = /cp/ ({$n3})\n";
	}
}

if (!$changed && strpos($vhost, 'https://$host/cp/control') !== false) {
	exit("already points to /cp/control\n");
}
if (!$changed) {
	// Ensure location = /cp exists before location /cp/
	if (stripos($vhost, 'location /cp/') !== false && !preg_match('/location\s*=\s*\/cp\s*\{/i', $vhost)) {
		$vhost = preg_replace(
			'/(location\s+\/cp\/\s*\{)/i',
			$want . "\n  $1",
			$vhost,
			1,
			$n4
		);
		if (!empty($n4)) {
			$changed = true;
			echo "inserted /cp and /cp/ redirects\n";
		}
	}
}
if (!$changed) {
	exit("no change applied — inspect vhost manually\n");
}

epc_clp_web_request($panel . '/site/' . rawurlencode($platformSite) . '/vhost', array(
	'method' => 'POST',
	'body' => http_build_query(array(
		'vhost-update' => '1',
		'vhost-template' => $vhost,
		'token' => $vt[1],
	)),
), $cookie);
echo "vhost updated — /cp and /cp/ now redirect to https://\$host/cp/control\n";
exit;
