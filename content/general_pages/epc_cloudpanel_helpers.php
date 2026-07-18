<?php
/**
 * CloudPanel CLI + localhost panel helpers.
 */
declare(strict_types=1);

function epc_clp_run_cmd(string $fullCmd): array
{
	$out = array();
	$code = 1;
	if (!function_exists('exec')) {
		return array('code' => $code, 'output' => 'exec disabled', 'cmd' => $fullCmd);
	}
	exec($fullCmd . ' 2>&1', $out, $code);
	return array('code' => $code, 'output' => implode("\n", $out), 'cmd' => $fullCmd);
}

function epc_clp_bin(): string
{
	static $cached = null;
	if ($cached !== null) {
		return $cached;
	}
	// Hostinger site users get NOPASSWD on clpctlWrapper (export/import/permissions).
	// Plain /usr/bin/clpctl is the same limited CLI without sudo.
	$candidates = array(
		'sudo -n /usr/bin/clpctlWrapper',
		'sudo -n /usr/bin/clpctl',
		'/usr/bin/clpctl',
		'/usr/local/bin/clpctl',
		'clpctl',
	);
	foreach ($candidates as $bin) {
		$r = epc_clp_run_cmd($bin . ' --version');
		if ($r['code'] === 0 || stripos($r['output'], 'CloudPanel') !== false) {
			$cached = $bin;
			return $cached;
		}
		// Wrapper prints the command list on bare invoke (exit 0).
		$r2 = epc_clp_run_cmd($bin);
		if ($r2['code'] === 0 && (stripos($r2['output'], 'CloudPanel') !== false || stripos($r2['output'], 'db:export') !== false)) {
			$cached = $bin;
			return $cached;
		}
	}
	$cached = 'clpctl';
	return $cached;
}

function epc_clp_run(string $subcmd): array
{
	return epc_clp_run_cmd(epc_clp_bin() . ' ' . $subcmd);
}

function epc_clp_available(): bool
{
	$r = epc_clp_run('--version');
	if ($r['code'] === 0) {
		return true;
	}
	$r2 = epc_clp_run('app:list');
	return $r2['code'] === 0;
}

function epc_clp_panel_url(): string
{
	return 'https://127.0.0.1:8443';
}

function epc_clp_web_request(string $url, array $opts, string &$cookieJar): string
{
	$method = isset($opts['method']) ? $opts['method'] : 'GET';
	$body = isset($opts['body']) ? $opts['body'] : '';
	$timeout = isset($opts['timeout']) ? (int) $opts['timeout'] : 60;
	$headers = $cookieJar !== '' ? ("Cookie: {$cookieJar}\r\n") : '';
	if ($body !== '') {
		$headers .= "Content-Type: application/x-www-form-urlencoded\r\n";
	}
	$ctx = stream_context_create(array(
		'http' => array(
			'method' => $method,
			'header' => $headers,
			'content' => $body,
			'timeout' => $timeout,
			'ignore_errors' => true,
			'follow_location' => 0,
		),
		'ssl' => array('verify_peer' => false, 'verify_peer_name' => false),
	));
	$resp = @file_get_contents($url, false, $ctx);
	$GLOBALS['epc_clp_last_http_headers'] = isset($http_response_header) ? $http_response_header : array();
	if (isset($http_response_header) && is_array($http_response_header)) {
		foreach ($http_response_header as $line) {
			if (stripos($line, 'Set-Cookie:') !== 0) {
				continue;
			}
			$part = trim(substr($line, 11));
			$semi = strpos($part, ';');
			if ($semi !== false) {
				$part = substr($part, 0, $semi);
			}
			$name = strtok($part, '=');
			$cookieJar = trim(preg_replace('/(?:^|;\s*)' . preg_quote($name, '/') . '=[^;]*/', '', $cookieJar));
			$cookieJar = trim($cookieJar . '; ' . $part, '; ');
		}
	}
	return $resp === false ? '' : $resp;
}

function epc_clp_web_login(string $user, string $pass, string &$cookie, bool $debug = false): array
{
	$panel = epc_clp_panel_url();
	$cookie = '';
	$detail = array();
	$html = epc_clp_web_request($panel . '/login', array(), $cookie);
	$detail['login_page'] = $html !== '' ? 'OK' : 'EMPTY';
	if ($html === '' || !preg_match('/name="_csrf_token" value="([^"]+)"/', $html, $m)) {
		$detail['csrf'] = 'MISSING';
		return array('ok' => false, 'detail' => $detail);
	}
	$detail['csrf'] = 'OK';
	$after = epc_clp_web_request($panel . '/login', array(
		'method' => 'POST',
		'body' => http_build_query(array(
			'userName' => $user,
			'password' => $pass,
			'_csrf_token' => $m[1],
			'submit' => 'Log In',
			'locale' => 'en',
		)),
	), $cookie);
	$postHeaders = isset($GLOBALS['epc_clp_last_http_headers']) ? $GLOBALS['epc_clp_last_http_headers'] : array();
	$detail['post_status'] = isset($postHeaders[0]) ? $postHeaders[0] : '';
	$detail['post_location'] = '';
	foreach ($postHeaders as $h) {
		if (stripos($h, 'Location:') === 0) {
			$detail['post_location'] = trim(substr($h, 9));
		}
	}
	$detail['cookie'] = $cookie !== '' ? 'SET' : 'EMPTY';
	$detail['still_login_form'] = (stripos($after, 'btn-login') !== false && stripos($after, 'Log In') !== false) ? 'yes' : 'no';
	$dash = epc_clp_web_request($panel . '/dashboard', array(), $cookie);
	$detail['dashboard'] = $dash !== '' ? 'OK' : 'EMPTY';
	$detail['dashboard_is_login'] = (stripos($dash, 'btn-login') !== false) ? 'yes' : 'no';
	$invalid = stripos($after, 'Invalid credentials') !== false;
	$detail['invalid_credentials'] = $invalid ? 'yes' : 'no';
	$detail['has_2fa'] = (stripos($after, 'two-factor') !== false || stripos($after, '2fa') !== false) ? 'yes' : 'no';
	$ok = !$invalid && $detail['dashboard_is_login'] === 'no' && (
		stripos($dash, 'logout') !== false
		|| preg_match('#/site/[a-z0-9._-]+#i', $dash)
		|| stripos($dash, 'Add Site') !== false
		|| stripos($detail['post_location'], 'dashboard') !== false
	);
	if (!$ok && preg_match('/alert[^>]*>([^<]+)/i', $after, $err)) {
		$detail['error'] = trim($err[1]);
	}
	if ($debug && !$ok) {
		$detail['after_snippet'] = substr(strip_tags($after), 0, 200);
	}
	return array('ok' => $ok, 'detail' => $detail);
}

function epc_clp_web_sites(string &$cookie): array
{
	$panel = epc_clp_panel_url();
	$html = epc_clp_web_request($panel . '/dashboard', array(), $cookie);
	if ($html === '') {
		return array();
	}
	preg_match_all('#/site/([a-zA-Z0-9._-]+)#', $html, $m);
	return array_values(array_unique($m[1]));
}

function epc_clp_site_exists(string $domain): bool
{
	$r = epc_clp_run('site:list');
	if ($r['code'] === 0 && stripos($r['output'], $domain) !== false) {
		return true;
	}
	if (is_dir('/home/ecomae/htdocs/' . $domain)) {
		return true;
	}
	return false;
}

function epc_clp_provision_php_site(array $opts): array
{
	$domain = (string) ($opts['domain'] ?? '');
	$user = (string) ($opts['site_user'] ?? '');
	$pass = (string) ($opts['site_user_password'] ?? '');
	$php = (string) ($opts['php_version'] ?? '8.3');
	$log = array();

	if ($domain === '' || $user === '' || $pass === '') {
		return array('ok' => false, 'log' => array('Missing domain, site_user, or password'));
	}

	if (!epc_clp_available()) {
		return array('ok' => false, 'log' => array('clpctl not available — create site manually in CloudPanel UI'));
	}

	if (epc_clp_site_exists($domain)) {
		$log[] = "Site already exists: {$domain}";
	} else {
		$args = '--domainName=' . escapeshellarg($domain)
			. ' --phpVersion=' . escapeshellarg($php)
			. ' --vhostTemplate=' . escapeshellarg('Generic')
			. ' --siteUser=' . escapeshellarg($user)
			. ' --siteUserPassword=' . escapeshellarg($pass);
		$r = epc_clp_run('site:add:php ' . $args);
		$log[] = $r['cmd'];
		$log[] = $r['output'];
		if ($r['code'] !== 0) {
			return array('ok' => false, 'log' => $log);
		}
	}

	$ssl = epc_clp_run('lets-encrypt:install:certificate --domainName=' . escapeshellarg($domain));
	$log[] = 'SSL cmd: ' . $ssl['cmd'];
	$log[] = 'SSL: ' . $ssl['output'];

	return array('ok' => true, 'log' => $log);
}

function epc_clp_provision_database(array $opts): array
{
	$domain = (string) ($opts['domain'] ?? '');
	$db = (string) ($opts['database_name'] ?? '');
	$user = (string) ($opts['database_user'] ?? '');
	$pass = (string) ($opts['database_password'] ?? '');
	$log = array();

	if (!epc_clp_available()) {
		return array('ok' => false, 'log' => array('clpctl not available'));
	}

	$args = '--domainName=' . escapeshellarg($domain)
		. ' --databaseName=' . escapeshellarg($db)
		. ' --databaseUserName=' . escapeshellarg($user)
		. ' --databaseUserPassword=' . escapeshellarg($pass);
	$r = epc_clp_run('db:add ' . $args);
	$log[] = $r['cmd'];
	$log[] = $r['output'];
	return array('ok' => $r['code'] === 0, 'log' => $log);
}

function epc_clp_guess_docroot(string $siteUser, string $domain): string
{
	$candidates = array(
		"/home/{$siteUser}/htdocs/{$domain}",
		"/home/{$siteUser}/htdocs/{$domain}/public",
		"/home/{$siteUser}/htdocs/www.{$domain}",
	);
	foreach ($candidates as $path) {
		if (is_dir($path)) {
			return $path;
		}
	}
	return "/home/{$siteUser}/htdocs/{$domain}";
}

function epc_clp_diagnostics(): array
{
	$out = array(
		'clp_bin' => epc_clp_bin(),
		'clp_available' => epc_clp_available(),
	);
	$ver = epc_clp_run('--version');
	$out['version'] = $ver['output'];
	$list = epc_clp_run('site:list');
	$out['site_list'] = $list['output'];
	$apps = epc_clp_run('app:list');
	$out['app_list'] = substr($apps['output'], 0, 500);
	return $out;
}

function epc_clp_web_create_php_site(string &$cookie, array $opts): array
{
	$domain = (string) ($opts['domain'] ?? '');
	$user = (string) ($opts['site_user'] ?? '');
	$pass = (string) ($opts['site_user_password'] ?? '');
	$php = (string) ($opts['php_version'] ?? '8.3');
	$panel = epc_clp_panel_url();
	$log = array();

	if ($domain === '' || $user === '' || $pass === '') {
		return array('ok' => false, 'log' => array('missing fields'));
	}

	$dash = epc_clp_web_request($panel . '/', array(), $cookie);
	if (stripos($dash, '/site/' . $domain) !== false) {
		$log[] = "Site already listed: {$domain}";
		return array('ok' => true, 'log' => $log);
	}

	$form = epc_clp_web_request($panel . '/site/new/php', array(), $cookie);
	if (!preg_match('/name="site_new_php\[_token\]" value="([^"]+)"/', $form, $m)) {
		return array('ok' => false, 'log' => array('CSRF token not found on /site/new/php'));
	}
	$body = http_build_query(array(
		'site_new_php' => array(
			'application' => 'Generic',
			'domainName' => $domain,
			'phpVersion' => $php,
			'siteUser' => $user,
			'siteUserPassword' => $pass,
			'submit' => 'Create',
			'_token' => $m[1],
		),
	));
	$resp = epc_clp_web_request($panel . '/site/new/php', array(
		'method' => 'POST',
		'body' => $body,
	), $cookie);
	$headers = isset($GLOBALS['epc_clp_last_http_headers']) ? $GLOBALS['epc_clp_last_http_headers'] : array();
	$log[] = isset($headers[0]) ? $headers[0] : 'POST done';
	if (stripos($resp, 'already exists') !== false) {
		$dashCheck = epc_clp_web_request($panel . '/', array(), $cookie);
		if (epc_clp_web_site_listed($dashCheck, $domain)) {
			$log[] = 'Site already exists';
			return array('ok' => true, 'log' => $log);
		}
		$log[] = 'Create error in response: ' . substr(strip_tags($resp), 0, 200);
		return array('ok' => false, 'log' => $log);
	}
	if (stripos($resp, 'error') !== false && stripos($resp, 'alert-danger') !== false) {
		$log[] = substr(strip_tags($resp), 0, 400);
		return array('ok' => false, 'log' => $log);
	}
	$dash2 = epc_clp_web_request($panel . '/', array(), $cookie);
	$ok = stripos($dash2, '/site/' . $domain) !== false;
	if (!$ok && isset($headers[0]) && stripos($headers[0], '302') !== false) {
		$ok = stripos($dash2, '/site/' . $domain) !== false;
	}
	$log[] = $ok ? 'Created OK' : substr(strip_tags($resp), 0, 300);
	return array('ok' => $ok, 'log' => $log);
}

function epc_clp_web_delete_site(string &$cookie, string $domain): array
{
	$panel = epc_clp_panel_url();
	$sitePath = '/site/' . rawurlencode($domain);
	$html = epc_clp_web_request($panel . $sitePath . '/settings', array(), $cookie);
	$log = array('settings len=' . strlen($html));
	$token = '';
	if (preg_match('/name="site_delete\[_token\]" value="([^"]+)"/', $html, $m)) {
		$token = $m[1];
	} elseif (preg_match('/id="site_delete__token"[^>]*value="([^"]+)"/', $html, $m)) {
		$token = $m[1];
	}
	if ($token === '') {
		$html = epc_clp_web_request($panel . $sitePath . '/delete', array(), $cookie);
		$log[] = 'delete page len=' . strlen($html);
		if (preg_match('/name="site_delete\[_token\]" value="([^"]+)"/', $html, $m)) {
			$token = $m[1];
		}
	}
	if ($token === '') {
		return array('ok' => false, 'log' => $log);
	}
	$body = http_build_query(array(
		'site_delete' => array(
			'domainName' => $domain,
			'_token' => $token,
			'submit' => 'Delete Site',
		),
	));
	epc_clp_web_request($panel . $sitePath . '/settings', array('method' => 'POST', 'body' => $body), $cookie);
	$log[] = 'DELETE POST ' . $sitePath . '/settings';
	$dash = epc_clp_web_request($panel . '/dashboard', array(), $cookie);
	$gone = !epc_clp_web_site_listed($dash, $domain);
	$log[] = $gone ? 'site removed from dashboard' : 'site still listed';
	return array('ok' => $gone, 'log' => $log);
}

/** Activate pending certificates from CloudPanel /certificates page (LE create is two-step). */
function epc_clp_web_activate_certificates(string &$cookie, string $domain): array
{
	$panel = epc_clp_panel_url();
	$sitePath = '/site/' . rawurlencode($domain);
	$html = epc_clp_web_request($panel . $sitePath . '/certificates', array(), $cookie);
	$log = array('certificates len=' . strlen($html));
	if ($html === '') {
		return array('ok' => false, 'log' => $log);
	}
	if (!preg_match_all('#' . preg_quote($sitePath, '#') . '/certificate/install\?uid=([a-f0-9]+)#', $html, $m)) {
		$log[] = 'no pending install links';
		return array('ok' => false, 'log' => $log);
	}
	$uids = array_values(array_unique($m[1]));
	if ($uids !== array()) {
		$uids = array(array_pop($uids));
	}
	$installed = 0;
	foreach ($uids as $uid) {
		$installUrl = $panel . $sitePath . '/certificate/install?uid=' . $uid;
		$resp = epc_clp_web_request($installUrl, array('method' => 'GET', 'timeout' => 120), $cookie);
		$headers = isset($GLOBALS['epc_clp_last_http_headers']) ? $GLOBALS['epc_clp_last_http_headers'] : array();
		$status = isset($headers[0]) ? $headers[0] : '';
		$log[] = 'install uid=' . substr($uid, 0, 12) . ' status=' . $status . ' len=' . strlen($resp);
		if (stripos($status, '302') !== false) {
			$installed++;
		}
		foreach ($headers as $h) {
			if (stripos($h, 'Location:') === 0 && stripos($h, 'certificates') !== false) {
				$installed++;
			}
		}
		if (preg_match('/name="([^"]*\[_token\])" value="([^"]+)"/', $resp, $tm)) {
			$prefix = strtok(str_replace(']', '', $tm[1]), '[');
			$body = http_build_query(array(
				$prefix => array(
					'_token' => $tm[2],
					'submit' => 'Install',
				),
			));
			if (preg_match('/action="([^"]+)"/', $resp, $act)) {
				$postUrl = $act[1];
				if (strpos($postUrl, 'http') !== 0) {
					$postUrl = $panel . $postUrl;
				}
			} else {
				$postUrl = $installUrl;
			}
			epc_clp_web_request($postUrl, array('method' => 'POST', 'body' => $body, 'timeout' => 120), $cookie);
			$log[] = 'install POST uid=' . substr($uid, 0, 12);
		}
	}
	$log[] = 'installed_count=' . $installed;
	return array('ok' => $installed > 0 || epc_clp_ssl_certificate_paths($domain) !== null, 'log' => $log);
}

function epc_clp_web_install_ssl(string &$cookie, string $domain, array $extraDomains = array()): array
{
	$panel = epc_clp_panel_url();
	$sitePath = '/site/' . rawurlencode($domain);
	$log = array();
	if (epc_clp_ssl_certificate_paths($domain) !== null) {
		return array('ok' => true, 'log' => array('cert already present'));
	}
	$activateFirst = epc_clp_web_activate_certificates($cookie, $domain);
	foreach ($activateFirst['log'] as $al) {
		$log[] = 'preflight activate: ' . $al;
	}
	for ($wait = 0; $wait < 4; $wait++) {
		if (epc_clp_ssl_certificate_paths($domain) !== null) {
			$log[] = 'cert active after preflight';
			return array('ok' => true, 'log' => $log);
		}
		sleep(1);
	}
	$paths = array(
		$sitePath . '/lets-encrypt-certificate/new',
		$sitePath . '/certificates',
		$sitePath . '/ssl',
		$sitePath . '/certificate',
	);
	$html = '';
	$usedPath = '';
	foreach ($paths as $p) {
		$html = epc_clp_web_request($panel . $p, array(), $cookie);
		if ($html !== '' && strlen($html) > 500 && stripos($html, '404') === false) {
			$usedPath = $p;
			break;
		}
	}
	$log[] = 'fetched ssl page len=' . strlen($html) . ($usedPath !== '' ? ' path=' . $usedPath : '');

	if (preg_match('/name="([^"]*\[_token\])" value="([^"]+)"/', $html, $m)) {
		$tokenName = $m[1];
		$parts = explode('[', str_replace(']', '', $tokenName));
		$prefix = $parts[0] !== '' ? $parts[0] : 'site_lets_encrypt_certificate';
		$formDomains = array();
		if (preg_match_all('/name="domains\[\]"[^>]*value="([^"]+)"/', $html, $dm)) {
			foreach ($dm[1] as $d) {
				$d = trim((string) $d);
				if ($d !== '') {
					$formDomains[] = $d;
				}
			}
		}
		$domains = array_values(array_unique(array_filter(array_merge(
			$formDomains,
			array($domain),
			$extraDomains
		))));
		$body = http_build_query(array(
			$prefix => array(
				'_token' => $m[2],
				'submit' => 'Create and Install',
			),
		));
		foreach ($domains as $d) {
			$body .= '&' . rawurlencode('domains[]') . '=' . rawurlencode($d);
		}
		if (preg_match('/action="([^"]+)"/', $html, $act)) {
			$action = $act[1];
		} else {
			$action = $sitePath . '/lets-encrypt-certificate/new';
		}
		if (strpos($action, 'http') !== 0) {
			$action = $panel . $action;
		}
		$resp = epc_clp_web_request($action, array(
			'method' => 'POST',
			'body' => $body,
			'timeout' => 120,
		), $cookie);
		$log[] = 'SSL POST sent to ' . $action . ' domains=' . implode(',', $domains);
		$postHeaders = isset($GLOBALS['epc_clp_last_http_headers']) ? $GLOBALS['epc_clp_last_http_headers'] : array();
		if (isset($postHeaders[0])) {
			$log[] = 'SSL POST status: ' . $postHeaders[0];
		}
		foreach ($postHeaders as $h) {
			if (stripos($h, 'Location:') === 0) {
				$log[] = 'SSL redirect: ' . trim(substr($h, 9));
			}
		}
		if (stripos($resp, 'alert-danger') !== false) {
			if (preg_match('/alert-danger[^>]*>([^<]+)/i', $resp, $err)) {
				$log[] = 'SSL error: ' . trim($err[1]);
			} else {
				$log[] = 'SSL response hint: ' . substr(strip_tags($resp), 0, 220);
			}
		} elseif (stripos($resp, 'alert-success') !== false || stripos($resp, 'certificate') !== false) {
			$log[] = 'SSL response looks OK len=' . strlen($resp);
		}
		for ($wait = 0; $wait < 8; $wait++) {
			sleep(2);
			if (epc_clp_ssl_certificate_paths($domain) !== null) {
				$log[] = 'cert verified on disk';
				return array('ok' => true, 'log' => $log);
			}
		}
		$activate = epc_clp_web_activate_certificates($cookie, $domain);
		foreach ($activate['log'] as $al) {
			$log[] = 'activate: ' . $al;
		}
		for ($wait = 0; $wait < 8; $wait++) {
			sleep(2);
			if (epc_clp_ssl_certificate_paths($domain) !== null) {
				$log[] = 'cert verified after activate';
				return array('ok' => true, 'log' => $log);
			}
		}
		$log[] = 'cert not on disk yet after POST';
		return array('ok' => false, 'log' => $log);
	}

	$cli = epc_clp_run('lets-encrypt:install:certificate --domainName=' . escapeshellarg($domain));
	$log[] = 'CLI fallback: ' . substr($cli['output'], 0, 400);
	if ($cli['code'] === 0) {
		return array('ok' => true, 'log' => $log);
	}

	$log[] = 'SSL manual step may be needed';
	return array('ok' => false, 'log' => $log);
}

function epc_clp_web_add_database(string &$cookie, string $domain, string $dbName, string $dbUser, string $dbPass): array
{
	$panel = epc_clp_panel_url();
	$domain = trim($domain) !== '' ? $domain : 'www.ecomae.com';
	$sitePath = '/site/' . rawurlencode($domain);
	$paths = array(
		$sitePath . '/database/new',
		$sitePath . '/databases/new',
		$sitePath . '/database',
	);
	$html = '';
	$postPath = $sitePath . '/database/new';
	foreach ($paths as $p) {
		$html = epc_clp_web_request($panel . $p, array(), $cookie);
		if ($html !== '' && stripos($html, '404') === false && strlen($html) > 500) {
			$postPath = $p;
			break;
		}
	}
	$log = array('db form len=' . strlen($html), 'post_path=' . $postPath);

	$token = '';
	$formPrefix = 'site_database';
	if (preg_match('/name="((?:site_database|database)\[_token\])" value="([^"]+)"/', $html, $m)) {
		$formPrefix = strpos($m[1], 'database[') === 0 ? 'database' : 'site_database';
		$token = $m[2];
	} elseif (preg_match('/name="([^"]*\[_token\])" value="([^"]+)"/', $html, $m)) {
		$token = $m[2];
		if (preg_match('/^([^\[]+)\[/', $m[1], $pm)) {
			$formPrefix = $pm[1];
		}
	}
	if ($token === '') {
		$log[] = 'DB form not found — create manually in CloudPanel';
		return array('ok' => false, 'log' => $log);
	}

	// CLP 2.x/6.x field names vary slightly; send the common set.
	$fields = array(
		$formPrefix => array(
			'name' => $dbName,
			'userName' => $dbUser,
			'userPassword' => $dbPass,
			'password' => $dbPass,
			'submit' => 'Create',
			'_token' => $token,
		),
	);
	$resp = epc_clp_web_request($panel . $postPath, array(
		'method' => 'POST',
		'body' => http_build_query($fields),
	), $cookie);
	$hdrs = isset($GLOBALS['epc_clp_last_http_headers']) && is_array($GLOBALS['epc_clp_last_http_headers'])
		? $GLOBALS['epc_clp_last_http_headers']
		: array();
	$location = '';
	$status = '';
	foreach ($hdrs as $h) {
		if (preg_match('#^HTTP/#i', $h)) {
			$status = $h;
		}
		if (stripos($h, 'Location:') === 0) {
			$location = trim(substr($h, 9));
		}
	}
	$log[] = 'DB create POST sent status=' . $status . ' location=' . $location;
	$ok = $location !== ''
		|| stripos($resp, 'Redirecting') !== false
		|| stripos($resp, '/databases') !== false
		|| stripos($status, '302') !== false
		|| stripos($status, '303') !== false;
	if (stripos($resp, 'Error Occurred') !== false || stripos($resp, 'already exists') !== false) {
		// "already exists" may still be usable — caller verifies with PDO.
		$log[] = 'response_flag=' . (stripos($resp, 'already exists') !== false ? 'already_exists' : 'error');
	}
	return array('ok' => $ok, 'log' => $log, 'response' => $resp, 'location' => $location);
}

/** Read nginx vhost template from CloudPanel site editor (2.5+ uses #editor div). */
function epc_clp_vhost_fetch(string &$cookie, string $domain): array
{
	$panel = epc_clp_panel_url();
	$html = epc_clp_web_request($panel . '/site/' . rawurlencode($domain) . '/vhost', array(), $cookie);
	$token = '';
	$vhost = '';
	if (preg_match('/name="token" value="([^"]+)"/', $html, $tm)) {
		$token = $tm[1];
	}
	if (preg_match('/<div id="editor">([\s\S]*?)<\/div>\s*<textarea/s', $html, $em)) {
		$vhost = html_entity_decode($em[1], ENT_QUOTES | ENT_HTML5, 'UTF-8');
	} elseif (preg_match('/<textarea[^>]*(?:name|id)="vhost-template"[^>]*>([\s\S]*?)<\/textarea>/i', $html, $vm)) {
		$vhost = html_entity_decode($vm[1], ENT_QUOTES | ENT_HTML5, 'UTF-8');
	} elseif (preg_match('/ace_editor[^>]*data-value="([^"]+)"/', $html, $am)) {
		$vhost = html_entity_decode($am[1], ENT_QUOTES | ENT_HTML5, 'UTF-8');
	}
	return array('html' => $html, 'token' => $token, 'vhost' => $vhost);
}

function epc_clp_vhost_save(string &$cookie, string $domain, string $vhost, string $token): bool
{
	if ($vhost === '' || $token === '') {
		return false;
	}
	$panel = epc_clp_panel_url();
	epc_clp_web_request($panel . '/site/' . rawurlencode($domain) . '/vhost', array(
		'method' => 'POST',
		'body' => http_build_query(array(
			'vhost-update' => '1',
			'vhost-template' => $vhost,
			'token' => $token,
		)),
	), $cookie);
	return true;
}

/** All Model C tenant hostnames (www + apex). */
function epc_clp_model_c_tenant_hostnames(): array
{
	$out = array();
	foreach (epc_clp_nginx_tenant_basename_slugs() as $slug) {
		$out[] = 'www.' . $slug . '.com';
		$out[] = $slug . '.com';
	}
	return array_values(array_unique($out));
}

/**
 * Pre-apply safety: quarantine orphan tenant configs + dedupe duplicate ecomae enabled files.
 *
 * @return array{ok: bool, log: list<string>, disabled: list<string>}
 */
function epc_clp_nginx_apply_safety(string $platformSite = 'www.ecomae.com'): array
{
	$log = array('=== nginx apply safety ===');
	$disabled = array();
	$hosts = epc_clp_model_c_tenant_hostnames();
	$found = epc_clp_nginx_find_configs_for_hosts($hosts, $platformSite);
	if ($found !== array()) {
		$log[] = 'orphan configs detected: ' . count($found);
	}
	$q = epc_clp_nginx_quarantine_orphan_configs($hosts, $platformSite);
	foreach ($q['log'] as $line) {
		$log[] = $line;
	}
	$disabled = array_merge($disabled, $q['disabled']);
	$nt = epc_clp_run_cmd('nginx -t 2>&1');
	$log[] = 'nginx -t: ' . substr(trim((string) $nt['output']), 0, 200);
	return array(
		'ok' => $nt['code'] === 0 || stripos((string) $nt['output'], 'successful') !== false,
		'log' => $log,
		'disabled' => array_values(array_unique($disabled)),
	);
}

/**
 * CloudPanel vhost Save + optional orphan quarantine (always before Model C apply).
 *
 * @return array{ok: bool, log: list<string>}
 */
function epc_clp_vhost_save_and_apply(
	string &$cookie,
	string $domain,
	string $vhost,
	string $token,
	bool $quarantineOrphans = true
): array {
	$log = array();
	if ($quarantineOrphans && stripos($domain, 'ecomae.com') !== false) {
		$safety = epc_clp_nginx_apply_safety($domain);
		foreach ($safety['log'] as $line) {
			$log[] = $line;
		}
		if ($safety['disabled'] !== array()) {
			$log[] = 'quarantined ' . count($safety['disabled']) . ' orphan config(s)';
		}
	}
	if (!epc_clp_vhost_save($cookie, $domain, $vhost, $token)) {
		$log[] = 'vhost save POST failed';
		return array('ok' => false, 'log' => $log);
	}
	$log[] = 'CloudPanel vhost Save OK for ' . $domain;
	return array('ok' => true, 'log' => $log);
}

function epc_clp_web_site_listed(string $dashboardHtml, string $domain): bool
{
	return stripos($dashboardHtml, '/site/' . $domain) !== false
		|| stripos($dashboardHtml, '>' . $domain . '<') !== false;
}

/** Point a CloudPanel site docroot at shared portal code (Model C fallback when delete fails). */
function epc_clp_web_set_site_docroot(string &$cookie, string $domain, string $targetRoot): array
{
	$panel = epc_clp_panel_url();
	$log = array();
	$settingsHtml = epc_clp_web_request($panel . '/site/' . rawurlencode($domain) . '/settings', array(), $cookie);
	if (!preg_match('/name="site_domain_settings\[_token\]" value="([^"]+)"/', $settingsHtml, $tm)) {
		return array('ok' => false, 'log' => array('settings token missing len=' . strlen($settingsHtml)));
	}
	epc_clp_web_request($panel . '/site/' . rawurlencode($domain) . '/settings', array(
		'method' => 'POST',
		'body' => http_build_query(array(
			'site_domain_settings' => array(
				'rootDirectory' => $targetRoot,
				'_token' => $tm[1],
				'submit' => '',
			),
		)),
	), $cookie);
	$log[] = 'settings rootDirectory=' . $targetRoot;

	$vf = epc_clp_vhost_fetch($cookie, $domain);
	if ($vf['vhost'] !== '' && $vf['token'] !== '') {
		$vhost = preg_replace('/\broot\s+[^;]+;/', 'root ' . $targetRoot . ';', $vf['vhost']);
		epc_clp_vhost_save($cookie, $domain, $vhost, $vf['token']);
		$log[] = 'vhost root patched';
	}
	$perm = epc_clp_run('system:permissions:reset --directories=755 --files=644 --path=' . escapeshellarg($targetRoot));
	$log[] = 'permissions code=' . $perm['code'];
	return array('ok' => true, 'log' => $log);
}

/**
 * Add hostnames only to the server block that already contains $anchorSite (e.g. www.epartscart.com).
 */
function epc_clp_vhost_add_aliases_on_site(string &$cookie, string $platformSite, array $aliasHosts, string $anchorSite = ''): array
{
	$anchorSite = $anchorSite !== '' ? $anchorSite : $platformSite;
	$vf = epc_clp_vhost_fetch($cookie, $platformSite);
	$vhost = $vf['vhost'];
	$log = array('vhost_len=' . strlen($vhost));
	if ($vhost === '' || $vf['token'] === '') {
		return array('ok' => false, 'log' => array_merge($log, array('Could not read vhost for ' . $platformSite)));
	}
	foreach ($aliasHosts as $aliasHost) {
		$vhost = preg_replace_callback(
			'/^\s*server_name\s+([^;]+);/m',
			function ($m) use ($aliasHost, $anchorSite) {
				if (stripos($m[1], $anchorSite) === false) {
					return $m[0];
				}
				if (stripos($m[1], $aliasHost) !== false) {
					return $m[0];
				}
				return preg_replace('/;\s*$/', ' ' . $aliasHost . ';', $m[0]);
			},
			$vhost
		);
	}
	// Remove aliases from redirect-only blocks (no anchor).
	foreach ($aliasHosts as $aliasHost) {
		$vhost = preg_replace_callback(
			'/^\s*server_name\s+([^;]+);/m',
			function ($m) use ($aliasHost, $anchorSite) {
				if (stripos($m[1], $anchorSite) !== false || stripos($m[1], $aliasHost) === false) {
					return $m[0];
				}
				$names = array_values(array_filter(preg_split('/\s+/', trim($m[1])), function ($n) use ($aliasHost) {
					return strcasecmp($n, $aliasHost) !== 0;
				}));
				return '  server_name ' . implode(' ', $names) . ';';
			},
			$vhost
		);
	}
	$save = epc_clp_vhost_save_and_apply($cookie, $platformSite, $vhost, $vf['token']);
	if (!$save['ok']) {
		return array('ok' => false, 'log' => array_merge($log, $save['log'], array('vhost save failed')));
	}
	return array('ok' => true, 'log' => array_merge($log, $save['log'], array('Saved aliases on ' . $platformSite)));
}

/**
 * Strip tenant hostnames from mistaken epartscart redirect / varnish server_name lines.
 */
function epc_clp_vhost_scrub_tenant_misroutes(string $vhost, array $tenantHosts): string
{
	$tenantHosts = array_values(array_unique(array_filter(array_map('trim', $tenantHosts))));
	if ($tenantHosts === array()) {
		return $vhost;
	}
	$vhost = preg_replace_callback(
		'/(\s*server_name\s+)([^;]+)(;\s*\n\s*return\s+301\s+https:\/\/www\.epartscart\.com)/i',
		function ($m) use ($tenantHosts) {
			$names = array_values(array_filter(preg_split('/\s+/', trim($m[2])), function ($n) use ($tenantHosts) {
				foreach ($tenantHosts as $t) {
					if (strcasecmp($n, $t) === 0) {
						return false;
					}
				}
				return $n !== '';
			}));
			if ($names === array()) {
				$names = array('epartscart.com');
			}
			return $m[1] . implode(' ', $names) . $m[3];
		},
		$vhost
	);
	$vhost = preg_replace_callback(
		'/^\s*server_name\s+([^;]+);/m',
		function ($m) use ($tenantHosts) {
			if (stripos($m[1], 'epartscart.com') === false) {
				return $m[0];
			}
			$names = array_values(array_filter(preg_split('/\s+/', trim($m[1])), function ($n) use ($tenantHosts) {
				foreach ($tenantHosts as $t) {
					if (strcasecmp($n, $t) === 0) {
						return false;
					}
				}
				return $n !== '';
			}));
			return '  server_name ' . implode(' ', $names) . ';';
		},
		$vhost
	);
	return $vhost;
}

/**
 * Remove ssl_reject_handshake from server blocks that serve tenant hostnames.
 * Keeps non-tenant blocks (e.g. platform/Super CP) untouched.
 */
function epc_clp_vhost_strip_ssl_reject_for_hosts(string $vhost, array $tenantHosts, int &$removedCount = 0): string
{
	$removedCount = 0;
	$tenantHosts = array_values(array_unique(array_filter(array_map('strtolower', array_map('trim', $tenantHosts)))));
	if ($tenantHosts === array()) {
		return $vhost;
	}

	$len = strlen($vhost);
	$cursor = 0;
	$out = '';
	while ($cursor < $len) {
		$serverPos = strpos($vhost, 'server', $cursor);
		if ($serverPos === false) {
			$out .= substr($vhost, $cursor);
			break;
		}
		$braceOpen = strpos($vhost, '{', $serverPos);
		if ($braceOpen === false) {
			$out .= substr($vhost, $cursor);
			break;
		}
		$between = substr($vhost, $serverPos, $braceOpen - $serverPos);
		if (!preg_match('/^\s*server\b/i', $between)) {
			$out .= substr($vhost, $cursor, $braceOpen - $cursor + 1);
			$cursor = $braceOpen + 1;
			continue;
		}

		$depth = 1;
		$i = $braceOpen + 1;
		for (; $i < $len; $i++) {
			$ch = $vhost[$i];
			if ($ch === '{') {
				$depth++;
			} elseif ($ch === '}') {
				$depth--;
				if ($depth === 0) {
					break;
				}
			}
		}
		if ($depth !== 0) {
			$out .= substr($vhost, $cursor);
			break;
		}

		$blockStart = $serverPos;
		$blockEnd = $i + 1;
		$block = substr($vhost, $blockStart, $blockEnd - $blockStart);
		$out .= substr($vhost, $cursor, $blockStart - $cursor);

		$hasTenantHost = false;
		if (preg_match_all('/^\s*server_name\s+([^;]+);/mi', $block, $snMatches) && !empty($snMatches[1])) {
			foreach ($snMatches[1] as $snLine) {
				$names = preg_split('/\s+/', trim((string) $snLine));
				foreach ($names as $name) {
					if ($name !== '' && in_array(strtolower($name), $tenantHosts, true)) {
						$hasTenantHost = true;
						break 2;
					}
				}
			}
		}

		if ($hasTenantHost) {
			$localRemoved = 0;
			$patched = preg_replace('/^\s*ssl_reject_handshake\s+on;\s*$/mi', '', $block, -1, $localRemoved);
			if (is_string($patched)) {
				$removedCount += $localRemoved;
				$block = $patched;
			}
		}

		$out .= $block;
		$cursor = $blockEnd;
	}

	return $out;
}

/**
 * Remove standalone tenant server blocks that reject/proxy away from Model C
 * (e.g. return 444 or proxy_pass 127.0.0.1:3000 remnants).
 */
function epc_clp_vhost_strip_tenant_standalone_blocks(
	string $vhost,
	array $tenantHosts,
	int &$removed444 = 0,
	int &$removed3000 = 0
): string {
	$removed444 = 0;
	$removed3000 = 0;
	$tenantHosts = array_values(array_unique(array_filter(array_map('strtolower', array_map('trim', $tenantHosts)))));
	if ($tenantHosts === array()) {
		return $vhost;
	}

	$len = strlen($vhost);
	$cursor = 0;
	$out = '';
	while ($cursor < $len) {
		$serverPos = strpos($vhost, 'server', $cursor);
		if ($serverPos === false) {
			$out .= substr($vhost, $cursor);
			break;
		}
		$braceOpen = strpos($vhost, '{', $serverPos);
		if ($braceOpen === false) {
			$out .= substr($vhost, $cursor);
			break;
		}
		$between = substr($vhost, $serverPos, $braceOpen - $serverPos);
		if (!preg_match('/^\s*server\b/i', $between)) {
			$out .= substr($vhost, $cursor, $braceOpen - $cursor + 1);
			$cursor = $braceOpen + 1;
			continue;
		}

		$depth = 1;
		$i = $braceOpen + 1;
		for (; $i < $len; $i++) {
			$ch = $vhost[$i];
			if ($ch === '{') {
				$depth++;
			} elseif ($ch === '}') {
				$depth--;
				if ($depth === 0) {
					break;
				}
			}
		}
		if ($depth !== 0) {
			$out .= substr($vhost, $cursor);
			break;
		}

		$blockStart = $serverPos;
		$blockEnd = $i + 1;
		$block = substr($vhost, $blockStart, $blockEnd - $blockStart);
		$out .= substr($vhost, $cursor, $blockStart - $cursor);

		$hasTenantHost = false;
		if (preg_match_all('/^\s*server_name\s+([^;]+);/mi', $block, $snMatches) && !empty($snMatches[1])) {
			foreach ($snMatches[1] as $snLine) {
				$names = preg_split('/\s+/', trim((string) $snLine));
				foreach ($names as $name) {
					if ($name !== '' && in_array(strtolower($name), $tenantHosts, true)) {
						$hasTenantHost = true;
						break 2;
					}
				}
			}
		}

		if ($hasTenantHost) {
			$hasReturn444 = (bool) preg_match('/\breturn\s+444\s*;/i', $block);
			$hasProxy3000 = (bool) preg_match('/\bproxy_pass\s+https?:\/\/(?:127\.0\.0\.1|localhost):3000\b/i', $block);
			if ($hasReturn444 || $hasProxy3000) {
				if ($hasReturn444) {
					$removed444++;
				}
				if ($hasProxy3000) {
					$removed3000++;
				}
				$cursor = $blockEnd;
				continue;
			}
		}

		$out .= $block;
		$cursor = $blockEnd;
	}

	return $out;
}

/**
 * Remove EPC tenant nginx snippets (CloudPanel may truncate them and break nginx).
 */
function epc_clp_vhost_strip_tenant_markers(string $vhost): string
{
	$vhost = preg_replace('/# EPC_TENANT_DIRECT_START[\s\S]*?# EPC_TENANT_DIRECT_END\s*/', '', $vhost);
	$vhost = preg_replace('/# EPC_TENANT_APEX_REDIRECT_START[\s\S]*?# EPC_TENANT_APEX_REDIRECT_END\s*/', '', $vhost);
	return $vhost;
}

/** Hostnames that belong on the platform vhost only (not tenant storefronts). */
function epc_clp_vhost_model_c_platform_hosts(): array
{
	return array('www.ecomae.com', 'ecomae.com', 'cp.ecomae.com');
}

/**
 * @return array{0: string, 1: list<string>} vhost and log lines
 */
function epc_clp_vhost_strip_hosts_from_server_names(string $vhost, array $hostsToStrip, bool $preserveEpcMarkers = true): array
{
	$strip = array();
	foreach ($hostsToStrip as $h) {
		$h = strtolower(trim((string) $h));
		if ($h !== '') {
			$strip[$h] = true;
		}
	}
	$log = array();
	if ($strip === array()) {
		return array($vhost, $log);
	}

	$scrubChunk = function (string $chunk) use ($strip, &$log): string {
		return (string) preg_replace_callback(
			'/^\s*server_name\s+([^;]+);/m',
			function ($m) use ($strip, &$log) {
				$names = preg_split('/\s+/', trim($m[1]));
				$removed = array();
				$kept = array();
				foreach ($names as $n) {
					$nl = strtolower($n);
					if ($n !== '' && isset($strip[$nl])) {
						$removed[] = $n;
					} elseif ($n !== '') {
						$kept[] = $n;
					}
				}
				if ($removed === array()) {
					return $m[0];
				}
				$log[] = 'stripped from server_name: ' . implode(', ', $removed);
				if ($kept === array()) {
					return $m[0];
				}
				return '  server_name ' . implode(' ', $kept) . ';';
			},
			$chunk
		);
	};

	if (!$preserveEpcMarkers) {
		return array($scrubChunk($vhost), $log);
	}

	$pattern = '/(# EPC_TENANT_(?:DIRECT|APEX_REDIRECT)_START[\s\S]*?# EPC_TENANT_(?:DIRECT|APEX_REDIRECT)_END\s*)/';
	$parts = preg_split($pattern, $vhost, -1, PREG_SPLIT_DELIM_CAPTURE);
	if (!is_array($parts) || count($parts) <= 1) {
		return array($scrubChunk($vhost), $log);
	}
	$out = '';
	foreach ($parts as $part) {
		if ($part !== '' && preg_match('/^# EPC_TENANT_(?:DIRECT|APEX_REDIRECT)_START/', $part)) {
			$out .= $part;
			continue;
		}
		$out .= $scrubChunk($part);
	}
	return array($out, $log);
}

/** List server_name lines for diagnostics. */
function epc_clp_vhost_audit_server_names(string $vhost): array
{
	$lines = array();
	if (!preg_match_all('/^\s*server_name\s+([^;]+);/m', $vhost, $m)) {
		return $lines;
	}
	foreach ($m[1] as $idx => $names) {
		$lines[] = ($idx + 1) . ': ' . trim((string) $names);
	}
	return $lines;
}

/** CloudPanel standard LE paths (www-data often cannot stat these files). */
function epc_clp_ssl_standard_paths(string $domain): array
{
	return array(
		'crt' => '/etc/nginx/ssl-certificates/' . $domain . '.crt',
		'key' => '/etc/nginx/ssl-certificates/' . $domain . '.key',
	);
}

/** CloudPanel Let's Encrypt material on disk (per domain). */
function epc_clp_ssl_certificate_paths(string $domain, bool $assumeStandard = false): ?array
{
	$domain = trim($domain);
	if ($domain === '') {
		return null;
	}
	$std = epc_clp_ssl_standard_paths($domain);
	$crt = $std['crt'];
	$key = $std['key'];
	if (is_file($crt) && is_file($key)) {
		return $std;
	}
	$r = epc_clp_run_cmd('test -r ' . escapeshellarg($crt) . ' -a -r ' . escapeshellarg($key) . ' && echo OK');
	if (strpos((string) $r['output'], 'OK') !== false) {
		return $std;
	}
	$subj = epc_clp_run_cmd('openssl x509 -in ' . escapeshellarg($crt) . ' -noout -subject 2>/dev/null');
	if ($subj['code'] === 0 && stripos((string) $subj['output'], $domain) !== false) {
		return $std;
	}
	if ($assumeStandard) {
		return $std;
	}
	return null;
}

/**
 * Issue / renew LE cert for one hostname (CLI first).
 *
 * @return array{ok: bool, log: list<string>}
 */
function epc_clp_install_le_certificate(string $domain, array $subjectAlternativeNames = array()): array
{
	$domain = trim($domain);
	$log = array();
	if ($domain === '') {
		return array('ok' => false, 'log' => array('empty domain'));
	}
	$paths = epc_clp_ssl_certificate_paths($domain);
	if ($paths !== null) {
		return array('ok' => true, 'log' => array('cert already present: ' . $paths['crt']));
	}
	$san = array_values(array_unique(array_filter(array_map('trim', $subjectAlternativeNames))));
	$san = array_values(array_filter($san, function ($h) use ($domain) {
		return strcasecmp($h, $domain) !== 0;
	}));
	$subcmd = 'lets-encrypt:install:certificate --domainName=' . escapeshellarg($domain);
	if ($san !== array()) {
		$subcmd .= ' --subjectAlternativeName=' . escapeshellarg(implode(',', $san));
	}
	$inner = '/usr/bin/clpctl ' . $subcmd;
	$runAsClp = array(
		'runuser -u clp -- /usr/bin/clpctl ' . $subcmd,
		'su -s /bin/bash -c ' . escapeshellarg($inner) . ' clp',
	);
	foreach ($runAsClp as $cmd) {
		$cli = epc_clp_run_cmd($cmd);
		$log[] = $cmd . ' exit=' . $cli['code'];
		$log[] = substr(trim((string) $cli['output']), 0, 500);
		if ($cli['code'] === 0 && epc_clp_ssl_certificate_paths($domain) !== null) {
			return array('ok' => true, 'log' => $log);
		}
	}
	$cli2 = epc_clp_run($subcmd);
	$log[] = 'CLI (default) exit=' . $cli2['code'];
	$log[] = substr(trim((string) $cli2['output']), 0, 400);
	if (epc_clp_ssl_certificate_paths($domain) !== null) {
		return array('ok' => true, 'log' => $log);
	}
	return array('ok' => false, 'log' => $log);
}

/**
 * In server {} blocks whose server_name includes $hostnames, set explicit ssl_certificate paths.
 *
 * @param list<string> $hostnames
 * @return array{vhost: string, log: list<string>, patched: int}
 */
function epc_clp_vhost_patch_server_ssl_for_hosts(string $vhost, array $hostnames, string $certDomain, bool $assumeStandardCert = false): array
{
	$paths = epc_clp_ssl_certificate_paths($certDomain, $assumeStandardCert);
	$log = array();
	if ($paths === null) {
		return array('vhost' => $vhost, 'log' => array('no cert files for ' . $certDomain), 'patched' => 0);
	}
	$hostnames = array_values(array_unique(array_filter(array_map('trim', $hostnames))));
	$certLine = '  ssl_certificate ' . $paths['crt'] . ';';
	$keyLine = '  ssl_certificate_key ' . $paths['key'] . ';';
	$patched = 0;
	$len = strlen($vhost);
	$out = '';
	$i = 0;
	while ($i < $len) {
		if (!preg_match('/\bserver\s*\{/i', $vhost, $m, PREG_OFFSET_CAPTURE, $i)) {
			$out .= substr($vhost, $i);
			break;
		}
		$start = (int) $m[0][1];
		$out .= substr($vhost, $i, $start - $i);
		$depth = 0;
		$j = $start;
		$end = $len;
		while ($j < $len) {
			$ch = $vhost[$j];
			if ($ch === '{') {
				$depth++;
			} elseif ($ch === '}') {
				$depth--;
				if ($depth === 0) {
					$end = $j + 1;
					break;
				}
			}
			$j++;
		}
		$block = substr($vhost, $start, $end - $start);
		$newBlock = $block;
		if (preg_match('/^\s*server_name\s+([^;]+);/im', $block, $sn)) {
			$names = preg_split('/\s+/', trim((string) $sn[1]));
			$hit = false;
			foreach ($hostnames as $want) {
				foreach ($names as $n) {
					if (strcasecmp($n, $want) === 0) {
						$hit = true;
						break 2;
					}
				}
			}
			if ($hit) {
				$newBlock = (string) preg_replace('/\{\{ssl_certificate_key\}\}\s*/', $keyLine . "\n", $block);
				$newBlock = (string) preg_replace('/\{\{ssl_certificate\}\}\s*/', $certLine . "\n", $newBlock);
				$newBlock = (string) preg_replace('/^\s*ssl_certificate\s+[^;]+;/m', $certLine, $newBlock);
				$newBlock = (string) preg_replace('/^\s*ssl_certificate_key\s+[^;]+;/m', $keyLine, $newBlock);
				if ($newBlock !== $block) {
					$patched++;
					$log[] = 'ssl paths for server_name ' . trim((string) $sn[1]);
				}
			}
		}
		$out .= $newBlock;
		$i = $end;
	}
	return array('vhost' => $out, 'log' => $log, 'patched' => $patched);
}

/**
 * Remove entire server {} blocks that serve any of $hostnames (Model C cleanup).
 *
 * @param list<string> $hostnames
 * @return array{vhost: string, log: list<string>, removed: int}
 */
function epc_clp_vhost_remove_server_blocks_for_hosts(string $vhost, array $hostnames): array
{
	$hostnames = array_values(array_unique(array_filter(array_map('trim', $hostnames))));
	$log = array();
	$removed = 0;
	$len = strlen($vhost);
	$out = '';
	$i = 0;
	while ($i < $len) {
		if (!preg_match('/\bserver\s*\{/i', $vhost, $m, PREG_OFFSET_CAPTURE, $i)) {
			$out .= substr($vhost, $i);
			break;
		}
		$start = (int) $m[0][1];
		$out .= substr($vhost, $i, $start - $i);
		$depth = 0;
		$j = $start;
		$end = $len;
		while ($j < $len) {
			$ch = $vhost[$j];
			if ($ch === '{') {
				$depth++;
			} elseif ($ch === '}') {
				$depth--;
				if ($depth === 0) {
					$end = $j + 1;
					break;
				}
			}
			$j++;
		}
		$block = substr($vhost, $start, $end - $start);
		$drop = false;
		if (preg_match('/^\s*server_name\s+([^;]+);/im', $block, $sn)) {
			$names = preg_split('/\s+/', trim((string) $sn[1]));
			foreach ($hostnames as $want) {
				foreach ($names as $n) {
					if (strcasecmp($n, $want) === 0) {
						$drop = true;
						$removed++;
						$log[] = 'removed server block: ' . trim((string) $sn[1]);
						break 2;
					}
				}
			}
		}
		if (!$drop) {
			$out .= $block;
		}
		$i = $end;
	}
	$out = (string) preg_replace("/\n{3,}/", "\n\n", $out);
	return array('vhost' => $out, 'log' => $log, 'removed' => $removed);
}

/** Reload nginx using the same fallbacks as one-shot repair scripts. */
function epc_clp_nginx_reload(): array
{
	$log = array();
	$cmds = array(
		'systemctl reload nginx 2>/dev/null',
		'systemctl restart nginx 2>/dev/null',
		"runuser -u clp -- /usr/bin/clpctl webserver:reload 2>/dev/null",
		"su -s /bin/bash -c '/usr/bin/clpctl webserver:reload' clp",
		'sudo -n nginx -t 2>&1',
		'nginx -t 2>&1',
		'sudo -n systemctl reload nginx 2>&1',
		'systemctl reload nginx 2>&1',
		'systemctl restart nginx 2>&1',
	);
	foreach ($cmds as $cmd) {
		$r = epc_clp_run_cmd($cmd);
		$log[] = $cmd . ' [exit=' . $r['code'] . '] ' . substr(trim((string) $r['output']), 0, 200);
	}
	$ok = false;
	foreach (array_reverse($log) as $line) {
		if (stripos($line, 'reload nginx') !== false && preg_match('/\[exit=0\]/', $line)) {
			$ok = true;
			break;
		}
		if (stripos($line, 'restart nginx') !== false && preg_match('/\[exit=0\]/', $line)) {
			$ok = true;
			break;
		}
	}
	return array('ok' => $ok, 'log' => $log);
}

/** nginx -t + reload using sudo -S when passwordless sudo is unavailable. */
function epc_clp_nginx_reload_with_pass(string $sudoPass): array
{
	$log = array();
	if ($sudoPass === '') {
		return array('ok' => false, 'log' => array('empty sudo password'));
	}
	$escaped = str_replace("'", "'\\''", $sudoPass);
	foreach (array(
		"echo '{$escaped}' | sudo -S nginx -t 2>&1",
		"echo '{$escaped}' | sudo -S systemctl reload nginx 2>&1",
	) as $cmd) {
		$r = epc_clp_run_cmd($cmd);
		$log[] = substr($cmd, 0, 40) . '... [exit=' . $r['code'] . '] ' . substr(trim((string) $r['output']), 0, 240);
	}
	$ok = false;
	foreach ($log as $line) {
		if (stripos($line, 'reload nginx') !== false && preg_match('/\[exit=0\]/', $line)) {
			$ok = true;
		}
	}
	return array('ok' => $ok, 'log' => $log);
}

/** Model C tenant slugs — orphan per-tenant site configs use these in filenames. */
function epc_clp_nginx_tenant_basename_slugs(): array
{
	return array('thejewellerytrend', 'epartscart', 'taxofinca', 'electronicae', 'stylenlook');
}

/** Canonical enabled platform vhost filename (CloudPanel). */
function epc_clp_nginx_platform_config_basename(string $platformSite = 'www.ecomae.com'): string
{
	$site = trim($platformSite);
	if ($site === '') {
		$site = 'www.ecomae.com';
	}
	return $site . '.conf';
}

function epc_clp_nginx_ensure_sites_disabled_dir(): string
{
	$dir = '/etc/nginx/sites-disabled';
	if (!is_dir($dir)) {
		@mkdir($dir, 0755, true);
	}
	return $dir;
}

/**
 * Move one enabled nginx config into sites-disabled/ (root or www-data via sudo).
 *
 * @return array{ok: bool, dest: string, log: string}
 */
function epc_clp_nginx_move_to_sites_disabled(string $conf): array
{
	$disabledDir = epc_clp_nginx_ensure_sites_disabled_dir();
	$base = basename($conf);
	$stamp = date('YmdHis');
	$dest = rtrim($disabledDir, '/') . '/' . $base . '.epc-quarantine-' . $stamp;
	if (@rename($conf, $dest)) {
		return array('ok' => true, 'dest' => $dest, 'log' => 'quarantined ' . $conf . ' -> ' . $dest);
	}
	$mv = epc_clp_run_cmd('mv -f ' . escapeshellarg($conf) . ' ' . escapeshellarg($dest));
	if ($mv['code'] === 0) {
		return array('ok' => true, 'dest' => $dest, 'log' => 'quarantined (sudo) ' . $conf);
	}
	$dest2 = $conf . '.epc-disabled';
	if (@rename($conf, $dest2)) {
		return array('ok' => true, 'dest' => $dest2, 'log' => 'quarantined (fallback) ' . $dest2);
	}
	return array('ok' => false, 'dest' => '', 'log' => 'FAIL quarantine ' . $conf . ' — ' . substr(trim((string) $mv['output']), 0, 120));
}

/**
 * Find enabled nginx vhost files mentioning tenant hostnames (excluding platform site file).
 *
 * @param list<string> $hosts
 * @return list<string> absolute paths
 */
function epc_clp_nginx_find_configs_for_hosts(array $hosts, string $platformSite = 'www.ecomae.com'): array
{
	$hosts = array_values(array_unique(array_filter(array_map('strtolower', array_map('trim', $hosts)))));
	if ($hosts === array()) {
		return array();
	}
	$platformBase = strtolower(epc_clp_nginx_platform_config_basename($platformSite));
	$tenantSlugs = epc_clp_nginx_tenant_basename_slugs();
	$found = array();
	$dirs = array('/etc/nginx/sites-enabled', '/etc/nginx/conf.d');
	foreach ($dirs as $dir) {
		if (!is_dir($dir)) {
			continue;
		}
		foreach (glob($dir . '/*') ?: array() as $conf) {
			if (!is_file($conf)) {
				continue;
			}
			if (preg_match('/\.(bak|disabled|epc-disabled)(\.|$)/', $conf)) {
				continue;
			}
			$base = strtolower(basename($conf));
			if ($base === $platformBase) {
				continue;
			}
			if (strpos($base, 'ecomae') !== false && strpos($base, (string) preg_replace('/^www\./', '', $platformSite)) !== false) {
				continue;
			}
			$matched = false;
			foreach ($tenantSlugs as $slug) {
				if (strpos($base, $slug) !== false) {
					$found[] = $conf;
					$matched = true;
					break;
				}
			}
			if ($matched) {
				continue;
			}
			$text = @file_get_contents($conf);
			if ($text === false) {
				continue;
			}
			$lower = strtolower($text);
			if (stripos($text, 'managed by certbot') !== false) {
				foreach ($hosts as $host) {
					if ($host !== '' && strpos($lower, strtolower($host)) !== false) {
						$found[] = $conf;
						$matched = true;
						break;
					}
				}
			}
			if ($matched) {
				continue;
			}
			foreach ($hosts as $host) {
				if ($host !== '' && strpos($lower, strtolower($host)) !== false) {
					$found[] = $conf;
					break;
				}
			}
		}
	}
	sort($found);
	return array_values(array_unique($found));
}

/**
 * Disable duplicate enabled platform vhost files (keep canonical www.ecomae.com.conf only).
 *
 * @return array{disabled: list<string>, kept: string, log: list<string>}
 */
function epc_clp_nginx_dedupe_platform_enabled_configs(string $platformSite = 'www.ecomae.com'): array
{
	$log = array();
	$disabled = array();
	$canonical = '/etc/nginx/sites-enabled/' . epc_clp_nginx_platform_config_basename($platformSite);
	$enabledDir = '/etc/nginx/sites-enabled';
	$candidates = array();
	if (is_dir($enabledDir)) {
		foreach (glob($enabledDir . '/*') ?: array() as $conf) {
			if (!is_file($conf) || preg_match('/\.(bak|disabled|epc-disabled)(\.|$)/', $conf)) {
				continue;
			}
			$text = @file_get_contents($conf);
			if ($text === false) {
				continue;
			}
			if (preg_match('/^\s*server_name\s+[^;]*\b(?:www\.)?ecomae\.com\b/im', $text)
				|| stripos($text, 'server_name www.ecomae.com') !== false) {
				$candidates[] = $conf;
			}
		}
	}
	$candidates = array_values(array_unique($candidates));
	if ($candidates === array()) {
		return array('disabled' => array(), 'kept' => $canonical, 'log' => array('no duplicate ecomae configs found'));
	}
	$kept = is_file($canonical) ? $canonical : $candidates[0];
	if (!is_file($kept)) {
		$kept = $candidates[0];
	}
	$log[] = 'keeping ' . $kept;
	foreach ($candidates as $conf) {
		if (realpath($conf) === realpath($kept)) {
			continue;
		}
		$mv = epc_clp_nginx_move_to_sites_disabled($conf);
		$log[] = $mv['log'];
		if ($mv['ok']) {
			$disabled[] = $conf;
		}
	}
	return array('disabled' => $disabled, 'kept' => $kept, 'log' => $log);
}

/**
 * Protected byte ranges inside Model C platform vhost (EPC tenant marker blocks).
 *
 * @return list<array{0: int, 1: int}>
 */
function epc_clp_vhost_protected_regions(string $vhost): array
{
	$regions = array();
	foreach (array(
		array('# EPC_TENANT_DIRECT_START', '# EPC_TENANT_DIRECT_END'),
		array('# EPC_TENANT_APEX_REDIRECT_START', '# EPC_TENANT_APEX_REDIRECT_END'),
	) as $pair) {
		$start = strpos($vhost, $pair[0]);
		$end = strpos($vhost, $pair[1]);
		if ($start !== false && $end !== false && $end > $start) {
			$regions[] = array($start, $end + strlen($pair[1]));
		}
	}
	return $regions;
}

function epc_clp_vhost_position_in_regions(int $pos, array $regions): bool
{
	foreach ($regions as $region) {
		if ($pos >= $region[0] && $pos <= $region[1]) {
			return true;
		}
	}
	return false;
}

/**
 * Remove orphan tenant server {} blocks (Certbot, old standalone) outside EPC Model C markers.
 *
 * @param list<string> $tenantHosts
 * @return array{vhost: string, log: list<string>, removed: int}
 */
function epc_clp_vhost_strip_orphan_tenant_server_blocks(string $vhost, array $tenantHosts): array
{
	$tenantHosts = array_values(array_unique(array_filter(array_map('strtolower', array_map('trim', $tenantHosts)))));
	$log = array();
	$removed = 0;
	if ($tenantHosts === array()) {
		return array('vhost' => $vhost, 'log' => $log, 'removed' => 0);
	}

	$protected = epc_clp_vhost_protected_regions($vhost);
	$len = strlen($vhost);
	$cursor = 0;
	$out = '';
	while ($cursor < $len) {
		$serverPos = strpos($vhost, 'server', $cursor);
		if ($serverPos === false) {
			$out .= substr($vhost, $cursor);
			break;
		}
		$braceOpen = strpos($vhost, '{', $serverPos);
		if ($braceOpen === false) {
			$out .= substr($vhost, $cursor);
			break;
		}
		$between = substr($vhost, $serverPos, $braceOpen - $serverPos);
		if (!preg_match('/^\s*server\b/i', $between)) {
			$out .= substr($vhost, $cursor, $braceOpen - $cursor + 1);
			$cursor = $braceOpen + 1;
			continue;
		}

		$depth = 1;
		$i = $braceOpen + 1;
		for (; $i < $len; $i++) {
			$ch = $vhost[$i];
			if ($ch === '{') {
				$depth++;
			} elseif ($ch === '}') {
				$depth--;
				if ($depth === 0) {
					break;
				}
			}
		}
		if ($depth !== 0) {
			$out .= substr($vhost, $cursor);
			break;
		}

		$blockStart = $serverPos;
		$blockEnd = $i + 1;
		$block = substr($vhost, $blockStart, $blockEnd - $blockStart);
		$out .= substr($vhost, $cursor, $blockStart - $cursor);

		$hasTenantHost = false;
		$tenantLabel = '';
		if (preg_match_all('/^\s*server_name\s+([^;]+);/mi', $block, $snMatches) && !empty($snMatches[1])) {
			foreach ($snMatches[1] as $snLine) {
				$names = preg_split('/\s+/', trim((string) preg_replace('/#.*$/', '', $snLine)));
				foreach ($names as $name) {
					$nl = strtolower(trim($name));
					if ($nl !== '' && in_array($nl, $tenantHosts, true)) {
						$hasTenantHost = true;
						$tenantLabel = $nl;
						break 2;
					}
				}
			}
		}

		$isCertbot = (bool) preg_match('/managed by certbot/i', $block);
		$inProtected = epc_clp_vhost_position_in_regions($blockStart, $protected);
		if ($hasTenantHost && ($isCertbot || !$inProtected)) {
			$removed++;
			$reason = $isCertbot ? 'certbot' : 'orphan';
			$log[] = 'removed ' . $reason . ' tenant server block: ' . $tenantLabel;
			$cursor = $blockEnd;
			continue;
		}

		$out .= $block;
		$cursor = $blockEnd;
	}

	$out = (string) preg_replace("/\n{3,}/", "\n\n", $out);
	return array('vhost' => $out, 'log' => $log, 'removed' => $removed);
}

/**
 * Disable orphan per-tenant CloudPanel nginx files (Model C serves tenants on www.ecomae.com vhost).
 *
 * @param list<string> $hosts
 * @return array{disabled: list<string>, log: list<string>}
 */
function epc_clp_nginx_quarantine_orphan_configs(array $hosts, string $platformSite = 'www.ecomae.com'): array
{
	$log = array();
	$disabled = array();
	epc_clp_nginx_ensure_sites_disabled_dir();
	foreach (epc_clp_nginx_find_configs_for_hosts($hosts, $platformSite) as $conf) {
		$mv = epc_clp_nginx_move_to_sites_disabled($conf);
		$log[] = $mv['log'];
		if ($mv['ok']) {
			$disabled[] = $conf;
			continue;
		}
		$del = epc_clp_run_cmd('rm -f ' . escapeshellarg($conf));
		if ($del['code'] === 0) {
			$log[] = 'removed ' . $conf;
			$disabled[] = $conf;
		}
	}
	$dedupe = epc_clp_nginx_dedupe_platform_enabled_configs($platformSite);
	foreach ($dedupe['log'] as $line) {
		$log[] = 'dedupe: ' . $line;
	}
	foreach ($dedupe['disabled'] as $conf) {
		$disabled[] = $conf;
	}
	return array('disabled' => array_values(array_unique($disabled)), 'log' => $log);
}

/**
 * Issue LE cert for each tenant www host and patch Model C vhost ssl_certificate paths.
 *
 * @param list<array{www: string, bare: string}> $tenantRows
 */
function epc_clp_vhost_install_per_tenant_ssl(
	string &$cookie,
	string $platformSite,
	string $platformDocroot,
	array $tenantRows
): array {
	$log = array();
	$vf = epc_clp_vhost_fetch($cookie, $platformSite);
	if ($vf['vhost'] === '' || $vf['token'] === '') {
		return array('ok' => false, 'log' => array('vhost fetch failed'), 'vhost' => '');
	}
	$vhost = $vf['vhost'];
	$totalPatched = 0;
	foreach ($tenantRows as $row) {
		$www = trim((string) ($row['www'] ?? ''));
		$bare = trim((string) ($row['bare'] ?? ''));
		if ($www === '') {
			continue;
		}
		$hosts = array_values(array_unique(array_filter(array($www, $bare))));
		$le = epc_clp_install_le_certificate($www);
		$log[] = $www . ' LE: ' . implode(' | ', array_slice($le['log'], 0, 2));
		if (!$le['ok']) {
			$sslWeb = epc_clp_web_install_ssl($cookie, $www, $bare !== '' ? array($bare) : array());
			$log[] = $www . ' web SSL: ' . implode(' | ', array_slice($sslWeb['log'], 0, 2));
		}
		$patch = epc_clp_vhost_patch_server_ssl_for_hosts($vhost, $hosts, $www);
		$vhost = $patch['vhost'];
		$totalPatched += (int) $patch['patched'];
		foreach ($patch['log'] as $pl) {
			$log[] = '  ' . $pl;
		}
	}
	$vhost = epc_clp_vhost_patch_tenant_direct_root($vhost, $platformDocroot);
	$save = epc_clp_vhost_save_and_apply($cookie, $platformSite, $vhost, $vf['token']);
	foreach ($save['log'] as $sl) {
		$log[] = $sl;
	}
	if (!$save['ok']) {
		$log[] = 'vhost save failed';
		return array('ok' => false, 'log' => $log, 'vhost' => $vhost);
	}
	$log[] = 'ssl_certificate patches total=' . $totalPatched;
	return array('ok' => true, 'log' => $log, 'vhost' => $vhost);
}

/**
 * Nginx server {} for one tenant (Model C direct PHP). Wrapped by EPC_TENANT_DIRECT_* markers elsewhere.
 */
function epc_clp_vhost_tenant_direct_server_template(): string
{
	return <<<'NGX'
server {
  listen 80;
  listen [::]:80;
  listen 443 quic;
  listen 443 ssl;
  listen [::]:443 quic;
  listen [::]:443 ssl;
  http2 on;
  http3 off;
  {{ssl_certificate_key}}
  {{ssl_certificate}}
  server_name TENANT_NAMES;
  {{root}}
  {{nginx_access_log}}
  {{nginx_error_log}}
  if ($scheme != https) {
    rewrite ^ https://$host$request_uri permanent;
  }
  location ~ /.well-known {
    auth_basic off;
    allow all;
  }
  {{settings}}
  include /etc/nginx/global_settings;
  index index.php index.html;
  location = /cp {
    return 301 /cp/;
  }
  location /cp/ {
    try_files $uri $uri/ /cp/index.php?$args;
  }
  location = /erp {
    return 301 /erp/;
  }
  location ^~ /erp/ {
    try_files $uri $uri/ /index.php?$args;
  }
  location / {
    try_files $uri $uri/ /index.php?$args;
  }
  error_page 502 503 504 525 = /epc-platform-splash.html;
  location = /epc-platform-splash.html {
    add_header Cache-Control "no-store";
    try_files $uri =404;
  }
  location = /epc-platform-status.json {
    add_header Cache-Control "no-store";
    try_files $uri =404;
  }
  location ~ \.php$ {
    include fastcgi_params;
    fastcgi_intercept_errors on;
    fastcgi_index index.php;
    fastcgi_param SCRIPT_FILENAME $document_root$fastcgi_script_name;
    try_files $uri =404;
    fastcgi_read_timeout 3600;
    fastcgi_send_timeout 3600;
    fastcgi_param HTTPS on;
    fastcgi_param SERVER_PORT 443;
    fastcgi_pass 127.0.0.1:{{php_fpm_port}};
  }
}

NGX;
}

/**
 * Nginx server block template for tenant hosts (Model C, direct PHP) — single tenant, legacy wrapper.
 */
function epc_clp_vhost_tenant_direct_template(): string
{
	return "# EPC_TENANT_DIRECT_START\n"
		. epc_clp_vhost_tenant_direct_server_template()
		. "# EPC_TENANT_DIRECT_END\n";
}

/**
 * Build combined Model C tenant direct + apex redirect snippets (one server {} per tenant).
 *
 * @param list<array{key?: string, hosts: list<string>}> $tenantGroups
 */
function epc_clp_vhost_build_model_c_tenant_snippets(array $tenantGroups): string
{
	$direct = '';
	$apex = '';
	foreach ($tenantGroups as $group) {
		$hosts = array_values(array_unique(array_filter(array_map('trim', $group['hosts'] ?? array()))));
		if ($hosts === array()) {
			continue;
		}
		usort($hosts, function ($a, $b) {
			$aw = stripos($a, 'www.') === 0 ? 0 : 1;
			$bw = stripos($b, 'www.') === 0 ? 0 : 1;
			if ($aw !== $bw) {
				return $aw - $bw;
			}
			return strcasecmp($a, $b);
		});
		$directNames = array();
		foreach ($hosts as $h) {
			if (stripos($h, 'www.') === 0) {
				$directNames[] = $h;
			}
		}
		if ($directNames === array()) {
			$directNames = $hosts;
		}
		$direct .= str_replace('TENANT_NAMES', implode(' ', $directNames), epc_clp_vhost_tenant_direct_server_template());
		$wwwHost = '';
		$bareHost = '';
		foreach ($hosts as $h) {
			if (stripos($h, 'www.') === 0) {
				$wwwHost = $h;
			} elseif (strpos($h, '.') !== false) {
				$bareHost = $h;
			}
		}
		if ($wwwHost !== '' && $bareHost !== '' && strcasecmp($bareHost, $wwwHost) !== 0) {
			$apex .= "# tenant {$bareHost}\nserver {\n  listen 80;\n  listen [::]:80;\n  listen 443 ssl;\n  listen [::]:443 ssl;\n  http2 on;\n  {{ssl_certificate_key}}\n  {{ssl_certificate}}\n  server_name {$bareHost};\n  return 301 https://{$wwwHost}\$request_uri;\n}\n";
		}
	}
	$out = '';
	if ($direct !== '') {
		$out .= "# EPC_TENANT_DIRECT_START\n" . $direct . "# EPC_TENANT_DIRECT_END\n";
	}
	if ($apex !== '') {
		$out .= "# EPC_TENANT_APEX_REDIRECT_START\n" . $apex . "# EPC_TENANT_APEX_REDIRECT_END\n";
	}
	return $out;
}

/**
 * Remove tenant hostnames from the listen 8080 PHP backend (Model C serves tenants on dedicated server {} blocks).
 *
 * @return array{vhost: string, log: list<string>}
 */
function epc_clp_vhost_remove_aliases_from_php_backend(string $vhost, array $aliasHosts, string $anchorSite): array
{
	$log = array();
	$out = preg_replace_callback(
		'/^\s*server_name\s+([^;]+);/m',
		function ($m) use ($aliasHosts, $anchorSite, $vhost, &$log) {
			if (stripos($m[1], $anchorSite) === false) {
				return $m[0];
			}
			$pos = strpos($vhost, $m[0]);
			if ($pos === false) {
				return $m[0];
			}
			$chunk = substr($vhost, max(0, $pos - 500), 500);
			if (stripos($chunk, 'listen 8080') === false) {
				return $m[0];
			}
			$names = preg_split('/\s+/', trim($m[1]));
			$before = count($names);
			$names = array_values(array_filter($names, function ($n) use ($aliasHosts) {
				foreach ($aliasHosts as $alias) {
					if (strcasecmp($n, $alias) === 0) {
						return false;
					}
				}
				return $n !== '';
			}));
			if (count($names) === $before) {
				return $m[0];
			}
			foreach ($aliasHosts as $alias) {
				if (stripos($m[1], $alias) !== false) {
					$log[] = '8080 backend -' . $alias;
				}
			}
			return '  server_name ' . implode(' ', $names) . ';';
		},
		$vhost
	);
	return array('vhost' => is_string($out) ? $out : $vhost, 'log' => $log);
}

/**
 * Model C: dedicated server {} per tenant on the platform vhost; platform server_name stays ecomae-only.
 *
 * @param list<array{key?: string, hosts: list<string>}> $tenantGroups
 */
function epc_clp_vhost_configure_model_c_tenants(string &$cookie, string $platformSite, array $tenantGroups): array
{
	$platformHosts = epc_clp_vhost_model_c_platform_hosts();
	$allTenantHosts = array();
	foreach ($tenantGroups as $group) {
		foreach ($group['hosts'] ?? array() as $h) {
			$h = trim((string) $h);
			if ($h !== '') {
				$allTenantHosts[] = $h;
			}
		}
	}
	$allTenantHosts = array_values(array_unique($allTenantHosts));
	if ($allTenantHosts === array()) {
		return array('ok' => false, 'log' => array('no tenant groups'));
	}

	$vf = epc_clp_vhost_fetch($cookie, $platformSite);
	$vhost = $vf['vhost'];
	$log = array('vhost_len=' . strlen($vhost));
	if ($vhost === '' || $vf['token'] === '') {
		return array('ok' => false, 'log' => array_merge($log, array('Could not read vhost')));
	}

	$vhost = epc_clp_vhost_strip_tenant_markers($vhost);
	$log[] = 'cleared EPC tenant marker blocks';

	$vhost = epc_clp_vhost_scrub_tenant_misroutes($vhost, $allTenantHosts);
	$log[] = 'scrubbed tenant misroutes';

	$stripped = epc_clp_vhost_strip_hosts_from_server_names($vhost, $allTenantHosts, false);
	$vhost = $stripped[0];
	$log = array_merge($log, array_slice($stripped[1], 0, 12));
	if (count($stripped[1]) > 12) {
		$log[] = '... +' . (count($stripped[1]) - 12) . ' more strip lines';
	}

	$varPos = stripos($vhost, 'varnish_proxy_pass');
	if ($varPos !== false) {
		$head = substr($vhost, 0, $varPos);
		$tail = substr($vhost, $varPos);
		$head = preg_replace_callback(
			'/^\s*server_name\s+([^;]+);/m',
			function ($m) use ($allTenantHosts, $platformHosts) {
				$hasPlatform = false;
				foreach ($platformHosts as $ph) {
					if (stripos($m[1], $ph) !== false) {
						$hasPlatform = true;
						break;
					}
				}
				if (!$hasPlatform) {
					return $m[0];
				}
				$names = preg_split('/\s+/', trim($m[1]));
				$names = array_values(array_filter($names, function ($n) use ($allTenantHosts) {
					foreach ($allTenantHosts as $alias) {
						if (strcasecmp($n, $alias) === 0) {
							return false;
						}
					}
					return $n !== '';
				}));
				return '  server_name ' . implode(' ', $names) . ';';
			},
			$head
		);
		$vhost = $head . $tail;
		$log[] = 'removed tenant hosts from varnish/platform front server_name';
	}

	foreach ($allTenantHosts as $aliasHost) {
		$vhost = preg_replace_callback(
			'/^\s*server_name\s+([^;]+);/m',
			function ($m) use ($aliasHost, $platformHosts) {
				$isPlatformLine = false;
				foreach ($platformHosts as $ph) {
					if (stripos($m[1], $ph) !== false) {
						$isPlatformLine = true;
						break;
					}
				}
				if ($isPlatformLine || stripos($m[1], $aliasHost) === false) {
					return $m[0];
				}
				$names = array_values(array_filter(preg_split('/\s+/', trim($m[1])), function ($n) use ($aliasHost) {
					return strcasecmp($n, $aliasHost) !== 0;
				}));
				if ($names === array()) {
					return $m[0];
				}
				return '  server_name ' . implode(' ', $names) . ';';
			},
			$vhost
		);
	}
	$log[] = 'removed tenant hosts from redirect server_name lines';

	$backend = epc_clp_vhost_remove_aliases_from_php_backend($vhost, $allTenantHosts, $platformSite);
	$vhost = $backend['vhost'];
	$log = array_merge($log, $backend['log']);

	$vhost = rtrim($vhost) . "\n\n" . epc_clp_vhost_build_model_c_tenant_snippets($tenantGroups);
	$log[] = 'inserted ' . count($tenantGroups) . ' tenant direct server block(s)';

	$vhost = (string) preg_replace(
		'/(# EPC_TENANT_DIRECT_START[\s\S]*?\n)\s*root\s+[^;]+;/',
		'$1  {{root}}',
		$vhost
	);
	$log[] = 'tenant direct root set to {{root}}';

	$save = epc_clp_vhost_save_and_apply($cookie, $platformSite, $vhost, $vf['token']);
	foreach ($save['log'] as $sl) {
		$log[] = $sl;
	}
	if (!$save['ok']) {
		return array('ok' => false, 'log' => array_merge($log, array('vhost save failed')));
	}
	return array('ok' => true, 'log' => $log);
}

/** Shared portal code roots (Model C); prefer ecomae tree when both exist. */
function epc_portal_shared_docroots(): array
{
	// Tenant direct blocks use the epartscart site user / PHP-FPM pool — docroot must match that tree.
	$roots = array(
		'/home/epartscart/htdocs/www.epartscart.com',
		'/home/ecomae/htdocs/www.ecomae.com',
	);
	$out = array();
	foreach ($roots as $root) {
		if (is_dir($root) && is_file($root . '/index.php')) {
			$out[] = $root;
		}
	}
	return $out;
}

/**
 * Inject failover splash error_page + static locations into server blocks for tenant hosts.
 *
 * @param list<string> $hostnames empty = all EPC_TENANT_DIRECT server blocks only
 * @return array{vhost: string, log: list<string>, patched: int}
 */
function epc_clp_vhost_patch_failover_splash(string $vhost, string $docroot, array $hostnames = array()): array
{
	$docroot = rtrim($docroot, '/');
	$log = array();
	$patched = 0;
	if ($docroot === '') {
		return array('vhost' => $vhost, 'log' => array('empty docroot'), 'patched' => 0);
	}
	$splashNeedle = 'error_page 502 503 504 525';
	$hostnames = array_values(array_unique(array_filter(array_map('trim', $hostnames))));
	$len = strlen($vhost);
	$out = '';
	$i = 0;
	while ($i < $len) {
		if (!preg_match('/\bserver\s*\{/i', $vhost, $m, PREG_OFFSET_CAPTURE, $i)) {
			$out .= substr($vhost, $i);
			break;
		}
		$start = (int) $m[0][1];
		$out .= substr($vhost, $i, $start - $i);
		$depth = 0;
		$j = $start;
		$end = $len;
		while ($j < $len) {
			$ch = $vhost[$j];
			if ($ch === '{') {
				$depth++;
			} elseif ($ch === '}') {
				$depth--;
				if ($depth === 0) {
					$end = $j + 1;
					break;
				}
			}
			$j++;
		}
		$block = substr($vhost, $start, $end - $start);
		$newBlock = $block;
		$inTenantMarker = stripos($block, 'EPC_TENANT_DIRECT') !== false
			|| stripos($block, 'www.thejewellerytrend.com') !== false;
		$hit = $hostnames === array();
		if (!$hit && preg_match('/^\s*server_name\s+([^;]+);/im', $block, $sn)) {
			$names = preg_split('/\s+/', trim((string) $sn[1]));
			foreach ($hostnames as $want) {
				foreach ($names as $n) {
					if (strcasecmp($n, $want) === 0) {
						$hit = true;
						break 2;
					}
				}
			}
		}
		if ($hit && ($inTenantMarker || $hostnames !== array()) && stripos($block, $splashNeedle) === false) {
			$inject = "\n  error_page 502 503 504 525 = /epc-platform-splash.html;\n"
				. "  location = /epc-platform-splash.html {\n"
				. '    root ' . $docroot . ";\n"
				. "    add_header Cache-Control \"no-store\";\n"
				. "    try_files \$uri =404;\n"
				. "  }\n"
				. "  location = /epc-platform-status.json {\n"
				. '    root ' . $docroot . ";\n"
				. "    add_header Cache-Control \"no-store\";\n"
				. "    try_files \$uri =404;\n"
				. "  }\n";
			if (preg_match('/^\s*location\s+~\s+\\\.php\$/m', $block, $phpLoc, PREG_OFFSET_CAPTURE)) {
				$pos = (int) $phpLoc[0][1];
				$newBlock = substr($block, 0, $pos) . $inject . substr($block, $pos);
			} else {
				$replaced = preg_replace('/\}\s*$/', $inject . "}\n", $block, 1);
				$newBlock = $replaced !== null ? $replaced : $block;
			}
			if ($newBlock !== $block) {
				$patched++;
				$log[] = 'failover splash for ' . (preg_match('/server_name\s+([^;]+)/i', $block, $sn2) ? trim($sn2[1]) : 'server');
			}
		}
		$out .= $newBlock;
		$i = $end;
	}
	return array('vhost' => $out, 'log' => $log, 'patched' => $patched);
}

function epc_clp_vhost_patch_tenant_direct_root(string $vhost, string $preferredRoot): string
{
	$preferredRoot = rtrim($preferredRoot, '/');
	if ($preferredRoot === '' || !is_dir($preferredRoot)) {
		return $vhost;
	}
	$marker = '# EPC_TENANT_DIRECT_START';
	$endMarker = '# EPC_TENANT_DIRECT_END';
	if (stripos($vhost, $marker) === false) {
		return $vhost;
	}
	$rootLine = '  root ' . $preferredRoot . ';';
	$out = (string) preg_replace_callback(
		'/' . preg_quote($marker, '/') . '[\s\S]*?' . preg_quote($endMarker, '/') . '/',
		function (array $m) use ($rootLine) {
			$block = $m[0];
			$block = (string) preg_replace('/\{\{root\}\}/', $rootLine, $block);
			$block = (string) preg_replace(
				'/^\s*root\s+[^;]+;/m',
				$rootLine,
				$block
			);
			return $block;
		},
		$vhost
	);
	return $out !== '' ? $out : $vhost;
}

/**
 * Add tenant hostnames to the listen 8080 PHP backend server_name (required when Varnish or fallback routes apply).
 */
function epc_clp_vhost_add_aliases_to_php_backend(string $vhost, array $aliasHosts, string $anchorSite): array
{
	$log = array();
	$out = preg_replace_callback(
		'/^\s*server_name\s+([^;]+);/m',
		function ($m) use ($aliasHosts, $anchorSite, $vhost, &$log) {
			if (stripos($m[1], $anchorSite) === false) {
				return $m[0];
			}
			$pos = strpos($vhost, $m[0]);
			if ($pos === false) {
				return $m[0];
			}
			$chunk = substr($vhost, max(0, $pos - 500), 500);
			if (stripos($chunk, 'listen 8080') === false) {
				return $m[0];
			}
			$names = preg_split('/\s+/', trim($m[1]));
			$changed = false;
			foreach ($aliasHosts as $alias) {
				$found = false;
				foreach ($names as $n) {
					if (strcasecmp($n, $alias) === 0) {
						$found = true;
						break;
					}
				}
				if (!$found) {
					$names[] = $alias;
					$log[] = '8080 backend +' . $alias;
					$changed = true;
				}
			}
			if (!$changed) {
				return $m[0];
			}
			return '  server_name ' . implode(' ', $names) . ';';
		},
		$vhost
	);
	return array('vhost' => is_string($out) ? $out : $vhost, 'log' => $log);
}

/**
 * Model C tenant hosts: serve PHP directly (bypass Varnish) on 443/80.
 * Removes aliases from the Varnish front block and adds a dedicated server {}.
 */
function epc_clp_vhost_configure_tenant_direct_php(string &$cookie, string $platformSite, array $aliasHosts, string $anchorSite = ''): array
{
	$anchorSite = $anchorSite !== '' ? $anchorSite : $platformSite;
	$aliasHosts = array_values(array_unique(array_filter(array_map('trim', $aliasHosts))));
	if ($aliasHosts === array()) {
		return array('ok' => false, 'log' => array('no alias hosts'));
	}
	$vf = epc_clp_vhost_fetch($cookie, $platformSite);
	$vhost = $vf['vhost'];
	$log = array('vhost_len=' . strlen($vhost));
	if ($vhost === '' || $vf['token'] === '') {
		return array('ok' => false, 'log' => array_merge($log, array('Could not read vhost')));
	}

	$varPos = stripos($vhost, 'varnish_proxy_pass');
	if ($varPos !== false) {
		$head = substr($vhost, 0, $varPos);
		$tail = substr($vhost, $varPos);
		$head = preg_replace_callback(
			'/^\s*server_name\s+([^;]+);/m',
			function ($m) use ($aliasHosts, $anchorSite) {
				if (stripos($m[1], $anchorSite) === false) {
					return $m[0];
				}
				$names = preg_split('/\s+/', trim($m[1]));
				$names = array_values(array_filter($names, function ($n) use ($aliasHosts) {
					foreach ($aliasHosts as $alias) {
						if (strcasecmp($n, $alias) === 0) {
							return false;
						}
					}
					return true;
				}));
				return '  server_name ' . implode(' ', $names) . ';';
			},
			$head
		);
		$vhost = $head . $tail;
		$log[] = 'removed tenant hosts from varnish server_name';
	}

	// Remove tenant hostnames from redirect-only server_name lines (e.g. return 301 to epartscart).
	foreach ($aliasHosts as $aliasHost) {
		$vhost = preg_replace_callback(
			'/^\s*server_name\s+([^;]+);/m',
			function ($m) use ($aliasHost, $anchorSite) {
				if (stripos($m[1], $anchorSite) !== false || stripos($m[1], $aliasHost) === false) {
					return $m[0];
				}
				$names = array_values(array_filter(preg_split('/\s+/', trim($m[1])), function ($n) use ($aliasHost) {
					return strcasecmp($n, $aliasHost) !== 0;
				}));
				return '  server_name ' . implode(' ', $names) . ';';
			},
			$vhost
		);
	}
	$log[] = 'removed tenant hosts from redirect server_name lines';

	$isEcomaePlatform = stripos($platformSite, 'ecomae.com') !== false;
	if ($isEcomaePlatform) {
		$backend = epc_clp_vhost_remove_aliases_from_php_backend($vhost, $aliasHosts, $anchorSite);
		$log = array_merge($log, $backend['log']);
	} else {
		$backend = epc_clp_vhost_add_aliases_to_php_backend($vhost, $aliasHosts, $anchorSite);
		$log = array_merge($log, $backend['log']);
	}
	$vhost = $backend['vhost'];

	usort($aliasHosts, function ($a, $b) {
		$aw = stripos($a, 'www.') === 0 ? 0 : 1;
		$bw = stripos($b, 'www.') === 0 ? 0 : 1;
		if ($aw !== $bw) {
			return $aw - $bw;
		}
		return strcasecmp($a, $b);
	});
	$namesLine = implode(' ', $aliasHosts);
	$marker = '# EPC_TENANT_DIRECT_START';
	$tenantBlock = "# EPC_TENANT_DIRECT_START\n"
		. str_replace('TENANT_NAMES', $namesLine, epc_clp_vhost_tenant_direct_server_template())
		. "# EPC_TENANT_DIRECT_END\n";
	if (stripos($vhost, $marker) !== false) {
		$vhost = preg_replace(
			'/' . preg_quote($marker, '/') . '[\s\S]*?# EPC_TENANT_DIRECT_END\s*/',
			$tenantBlock,
			$vhost,
			1
		);
		$log[] = 'updated tenant direct block';
	} else {
		$vhost .= "\n" . $tenantBlock;
		$log[] = 'inserted tenant direct block';
	}

	$wwwHost = '';
	$bareHost = '';
	foreach ($aliasHosts as $h) {
		if (stripos($h, 'www.') === 0) {
			$wwwHost = $h;
		} elseif (strpos($h, '.') !== false) {
			$bareHost = $h;
		}
	}
	$apexMarker = '# EPC_TENANT_APEX_REDIRECT_START';
	if ($wwwHost !== '' && $bareHost !== '' && strcasecmp($bareHost, $wwwHost) !== 0) {
		$apexBlock = "# EPC_TENANT_APEX_REDIRECT_START\nserver {\n  listen 80;\n  listen [::]:80;\n  listen 443 ssl;\n  listen [::]:443 ssl;\n  http2 on;\n  {{ssl_certificate_key}}\n  {{ssl_certificate}}\n  server_name {$bareHost};\n  return 301 https://{$wwwHost}\$request_uri;\n}\n# EPC_TENANT_APEX_REDIRECT_END\n";
		if (stripos($vhost, $apexMarker) !== false) {
			$vhost = preg_replace(
				'/' . preg_quote($apexMarker, '/') . '[\s\S]*?# EPC_TENANT_APEX_REDIRECT_END\s*/',
				$apexBlock,
				$vhost,
				1
			);
		} else {
			$vhost .= "\n" . $apexBlock;
		}
		$log[] = 'apex redirect ' . $bareHost . ' -> ' . $wwwHost;
	}

	// Restore CloudPanel {{root}} if a prior deploy replaced it with a path the tenant FPM pool cannot read.
	$vhost = (string) preg_replace(
		'/(# EPC_TENANT_DIRECT_START[\s\S]*?\n)\s*root\s+[^;]+;/',
		'$1  {{root}}',
		$vhost
	);
	$log[] = 'tenant direct root reset to {{root}}';

	$save = epc_clp_vhost_save_and_apply($cookie, $platformSite, $vhost, $vf['token']);
	foreach ($save['log'] as $sl) {
		$log[] = $sl;
	}
	if (!$save['ok']) {
		return array('ok' => false, 'log' => array_merge($log, array('vhost save failed')));
	}
	return array('ok' => true, 'log' => $log);
}
