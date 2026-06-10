<?php
/**
 * Verify standalone ERP portal login CSRF + page load for tenant hosts.
 * GET ?token=epartscart-deploy-2026&site_key=epartscart
 */
declare(strict_types=1);

require_once __DIR__ . '/epc_deploy_auth.php';
epc_deploy_require_token();
header('Content-Type: application/json; charset=utf-8');

$siteKey = preg_replace('/[^a-z0-9_]/', '', strtolower((string) ($_GET['site_key'] ?? 'epartscart')));
if ($siteKey === '') {
	$siteKey = 'epartscart';
}
$hosts = array(
	'epartscart' => 'www.epartscart.com',
	'electronicae' => 'www.electronicae.com',
);
$host = $hosts[$siteKey] ?? ('www.' . $siteKey . '.com');
$erpUrl = 'https://' . $host . '/en/erp';

function epc_elv_curl(string $url, array $opts = array()): array
{
	$cookieJar = isset($opts['cookie_jar']) ? (string) $opts['cookie_jar'] : '';
	$method = strtoupper((string) ($opts['method'] ?? 'GET'));
	$postFields = isset($opts['post_fields']) ? (string) $opts['post_fields'] : '';
	$extraCookies = isset($opts['extra_cookies']) ? (string) $opts['extra_cookies'] : '';
	$referer = isset($opts['referer']) ? (string) $opts['referer'] : '';

	$ch = curl_init($url);
	$headers = array('User-Agent: EPC-ERP-Login-Verify/1.0');
	if ($referer !== '') {
		$headers[] = 'Referer: ' . $referer;
	}
	if ($extraCookies !== '') {
		$headers[] = 'Cookie: ' . $extraCookies;
	}
	curl_setopt_array($ch, array(
		CURLOPT_RETURNTRANSFER => true,
		CURLOPT_FOLLOWLOCATION => false,
		CURLOPT_SSL_VERIFYPEER => false,
		CURLOPT_SSL_VERIFYHOST => 0,
		CURLOPT_TIMEOUT => (int) ($opts['timeout'] ?? 60),
		CURLOPT_HTTPHEADER => $headers,
		CURLOPT_CUSTOMREQUEST => $method,
	));
	if ($cookieJar !== '') {
		curl_setopt($ch, CURLOPT_COOKIEJAR, $cookieJar);
		curl_setopt($ch, CURLOPT_COOKIEFILE, $cookieJar);
	}
	if ($method === 'POST' && $postFields !== '') {
		curl_setopt($ch, CURLOPT_POSTFIELDS, $postFields);
	}
	$body = (string) curl_exec($ch);
	$code = (int) curl_getinfo($ch, CURLINFO_HTTP_CODE);
	$location = (string) curl_getinfo($ch, CURLINFO_REDIRECT_URL);
	curl_close($ch);
	return array('code' => $code, 'body' => $body, 'location' => $location);
}

$stopCsrf = (string) @file_get_contents(__DIR__ . '/content/users/stop_csrf.php');
$access = (string) @file_get_contents(__DIR__ . '/content/shop/finance/epc_erp_access.php');

$checks = array();
$checks['stop_csrf_storefront_guard'] = strpos($stopCsrf, 'epc_csrf_request_is_storefront_user_auth') !== false;
$checks['erp_access_forces_user_csrf'] = strpos($access, '$csrf_check_admin = false') !== false
	&& strpos($access, 'epc_erp_portal_handle_auth_post') !== false;

$tmp = sys_get_temp_dir() . '/epc_elv_' . md5($host . microtime(true)) . '.txt';
$get = epc_elv_curl($erpUrl, array('cookie_jar' => $tmp));
$checks['get_http_200'] = $get['code'] === 200;
$checks['login_form_present'] = stripos($get['body'], 'Sign in') !== false
	&& stripos($get['body'], 'csrf_guard_key') !== false;
$csrf = '';
if (preg_match('/name="csrf_guard_key"\s+value="([^"]+)"/', $get['body'], $m)) {
	$csrf = $m[1];
}
$checks['csrf_token_present'] = $csrf !== '';

$postOk = false;
$postDetail = 'skipped';
if ($csrf !== '') {
	$post = epc_elv_curl($erpUrl, array(
		'cookie_jar' => $tmp,
		'method' => 'POST',
		'referer' => $erpUrl,
		'post_fields' => http_build_query(array(
			'authentication' => 'true',
			'auth_contact' => 'erp-verify@example.com',
			'auth_contact_type' => 'email',
			'password' => 'invalid-probe-password',
			'csrf_guard_key' => $csrf,
		)),
	));
	$postOk = $post['code'] === 302
		&& stripos($post['body'], 'CSRF 4') === false
		&& stripos($post['body'], 'CSRF 3.1') === false;
	$postDetail = 'http=' . $post['code'] . ' location=' . $post['location'];
	if (!$postOk && $post['body'] !== '') {
		$postDetail .= ' body=' . substr(trim($post['body']), 0, 120);
	}
}
$checks['guest_post_not_csrf4'] = $postOk;

@unlink($tmp);
$tmpAdmin = sys_get_temp_dir() . '/epc_elv_admin_' . md5($host . microtime(true)) . '.txt';
$get2 = epc_elv_curl($erpUrl, array('cookie_jar' => $tmpAdmin));
$csrf2 = '';
if (preg_match('/name="csrf_guard_key"\s+value="([^"]+)"/', $get2['body'], $m2)) {
	$csrf2 = $m2[1];
}
$adminPostOk = false;
$adminPostDetail = 'skipped';
if ($csrf2 !== '') {
	$cookieLine = '';
	if (is_file($tmpAdmin)) {
		$lines = file($tmpAdmin, FILE_IGNORE_NEW_LINES | FILE_SKIP_EMPTY_LINES);
		foreach ((array) $lines as $line) {
			if ($line[0] === '#') {
				continue;
			}
			$parts = explode("\t", $line);
			if (count($parts) >= 7) {
				$cookieLine .= $parts[5] . '=' . $parts[6] . '; ';
			}
		}
	}
	$cookieLine .= 'admin_session=probe_admin_session; admin_u_id=1';
	$postAdmin = epc_elv_curl($erpUrl, array(
		'cookie_jar' => $tmpAdmin,
		'method' => 'POST',
		'referer' => $erpUrl,
		'extra_cookies' => trim($cookieLine),
		'post_fields' => http_build_query(array(
			'authentication' => 'true',
			'auth_contact' => 'erp-verify@example.com',
			'auth_contact_type' => 'email',
			'password' => 'invalid-probe-password',
			'csrf_guard_key' => $csrf2,
		)),
	));
	$adminPostOk = stripos($postAdmin['body'], 'CSRF 4') === false
		&& ($postAdmin['code'] === 302 || stripos($postAdmin['body'], 'CSRF 3.1') !== false);
	$adminPostDetail = 'http=' . $postAdmin['code'] . ' body=' . substr(trim($postAdmin['body']), 0, 120);
}
$checks['admin_cookie_post_not_csrf4'] = $adminPostOk;
@unlink($tmpAdmin);

$pass = true;
foreach ($checks as $ok) {
	if (!$ok) {
		$pass = false;
	}
}

echo json_encode(array(
	'ok' => $pass,
	'site_key' => $siteKey,
	'host' => $host,
	'erp_url' => $erpUrl,
	'checks' => $checks,
	'post_probe' => $postDetail,
	'admin_cookie_probe' => $adminPostDetail,
	'generated_at' => gmdate('c'),
), JSON_PRETTY_PRINT | JSON_UNESCAPED_SLASHES);
