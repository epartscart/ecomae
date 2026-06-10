<?php
/**
 * Discover CloudPanel add-site routes after web login (diagnostic).
 */
declare(strict_types=1);
require_once __DIR__ . '/epc_deploy_auth.php';
epc_deploy_require_token();
header('Content-Type: text/plain; charset=utf-8');

require_once __DIR__ . '/content/general_pages/epc_cloudpanel_helpers.php';

$pass = trim((string) ($_GET['clp_pass'] ?? ''));
$user = trim((string) ($_GET['clp_user'] ?? 'admin'));
$cookie = '';
$r = epc_clp_web_login($user, $pass, $cookie, true);
echo 'login: ' . (!empty($r['ok']) ? 'OK' : 'FAIL') . "\n\n";

$panel = epc_clp_panel_url();
$html = epc_clp_web_request($panel . '/site/new/php', array(), $cookie);
echo "=== /site/new/php ===\n";
if (preg_match('/site_new_php\[application\][^>]*>(.*?)<\/select/is', $html, $sel)) {
	echo "application options snippet:\n" . substr(strip_tags($sel[1]), 0, 500) . "\n";
}
if (preg_match('/name="site_new_php\[_token\]" value="([^"]+)"/', $html, $tk)) {
	echo "token: " . substr($tk[1], 0, 40) . "...\n";
}
if (preg_match_all('/name="site_new_php\[phpVersion\]"[^>]*value="([^"]+)"/', $html, $pv)) {
	echo "php versions: " . implode(', ', array_unique($pv[1])) . "\n";
}
if (preg_match_all('/<option value="([^"]+)"[^>]*>([^<]+)</', $html, $opts, PREG_SET_ORDER)) {
	foreach (array_slice($opts, 0, 15) as $o) {
		if (stripos($o[2], 'generic') !== false || stripos($o[1], 'generic') !== false) {
			echo "opt: {$o[1]} => {$o[2]}\n";
		}
	}
}

foreach (array('www.ecomae.com', 'cp.ecomae.com') as $dom) {
	$certForm = epc_clp_web_request($panel . '/site/' . $dom . '/certificates', array(), $cookie);
	echo "\n=== {$dom} certificates len=" . strlen($certForm) . " ===\n";
	if (preg_match('/<form[^>]+action="([^"]+)"/', $certForm, $fa)) {
		echo "form action: {$fa[1]}\n";
	}
	preg_match_all('/name="([^"]+)"/', $certForm, $fn);
	if (!empty($fn[1])) {
		echo implode(', ', array_unique($fn[1])) . "\n";
	}
}
