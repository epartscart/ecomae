<?php
// PHP 8.3 goldens for plan Q1-hull (CP social login). Leftover auth-common / OTP / buttons stay stubbed.
ini_set('display_errors', 'stderr');
date_default_timezone_set('UTC');
$root = rtrim($argv[1] ?? '/workspace', '/');
$spec = json_decode(file_get_contents(__DIR__ . '/cases.json'), true);

function hull_now(): int
{
	return (int) ($GLOBALS['HULL_NOW'] ?? 1760000000);
}

function hull_nonce(): string
{
	return (string) ($GLOBALS['HULL_NONCE'] ?? str_repeat('a', 32));
}

function hull_http_post(string $url, string $body): array
{
	$fn = $GLOBALS['HULL_HTTP'] ?? null;
	if (is_callable($fn)) {
		return $fn($url, $body);
	}
	return array(false, '', 'no-http');
}

function hull_jwt(array $payload): string
{
	$h = rtrim(strtr(base64_encode('{"alg":"none"}'), '+/', '-_'), '=');
	$p = rtrim(strtr(base64_encode(json_encode($payload)), '+/', '-_'), '=');
	return $h . '.' . $p . '.sig';
}

function hull_ctx($tk = 'acme', $mode = 'cp'): array
{
	return array(
		'tenant_key' => $tk,
		'kind' => 'mixed',
		'return_host' => 'shop.acme.test',
		'return_path' => '/cp/',
		'auth_mode' => $mode,
		'lang_prefix' => 'en',
	);
}

function hull_run_providers(): array
{
	$GLOBALS['HULL_OAUTH'] = array('google' => array('client_id' => '', 'client_secret' => '', 'redirect_uri' => ''));
	$empty = epc_auth_social_providers();
	$cbEmpty = epc_auth_oauth_central_callback_url();
	$GLOBALS['HULL_OAUTH'] = array('google' => array(
		'client_id' => 'id.apps.googleusercontent.com',
		'client_secret' => '',
		'redirect_uri' => '',
	));
	$idOnly = epc_auth_social_providers();
	$GLOBALS['HULL_OAUTH'] = array('google' => array(
		'client_id' => 'id.apps.googleusercontent.com',
		'client_secret' => 'secret',
		'redirect_uri' => 'https://www.ecomae.com/custom-callback.php',
	));
	$full = epc_auth_social_providers();
	$cb = epc_auth_oauth_central_callback_url();
	return array($empty, $cbEmpty, $idOnly, $full, $cb);
}

function hull_run_state(): array
{
	$ok = epc_auth_oauth_state_pack(hull_ctx(), hull_nonce());
	$un = epc_auth_oauth_state_unpack($ok);
	$bad = epc_auth_oauth_state_unpack($ok . 'x');
	$parts = explode('.', $ok, 2);
	$cut = epc_auth_oauth_state_unpack($parts[0]);
	$GLOBALS['HULL_NOW'] = 1760000000 + 901;
	$expired = epc_auth_oauth_state_unpack($ok);
	$GLOBALS['HULL_NOW'] = 1760000000;
	$other = epc_auth_oauth_state_pack(hull_ctx('beta', 'storefront'), hull_nonce());
	$unOther = epc_auth_oauth_state_unpack($other);
	return array($ok, $un, $bad, $cut, $expired, $other, $unOther);
}

function hull_run_google(): array
{
	$GLOBALS['HULL_OAUTH'] = array('google' => array('client_id' => '', 'client_secret' => '', 'redirect_uri' => ''));
	$emptyStart = epc_auth_google_start_url(hull_ctx());
	$GLOBALS['HULL_OAUTH'] = array('google' => array(
		'client_id' => 'id.apps.googleusercontent.com',
		'client_secret' => 'secret',
		'redirect_uri' => 'https://www.ecomae.com/epc-auth-google-callback.php',
	));
	$start = epc_auth_google_start_url(hull_ctx());
	$malformed = epc_auth_google_verify_id_token('nope', 'id.apps.googleusercontent.com');
	$aud = epc_auth_google_verify_id_token(hull_jwt(array(
		'aud' => 'other',
		'email' => 'ops@acme.test',
		'email_verified' => true,
		'iss' => 'https://accounts.google.com',
		'exp' => 1760000900,
		'name' => 'Ops',
		'sub' => '1',
	)), 'id.apps.googleusercontent.com');
	$unverified = epc_auth_google_verify_id_token(hull_jwt(array(
		'aud' => 'id.apps.googleusercontent.com',
		'email' => 'ops@acme.test',
		'email_verified' => false,
		'iss' => 'https://accounts.google.com',
		'exp' => 1760000900,
		'name' => 'Ops',
		'sub' => '1',
	)), 'id.apps.googleusercontent.com');
	$zero = epc_auth_google_verify_id_token(hull_jwt(array(
		'aud' => 'id.apps.googleusercontent.com',
		'email' => 'ops@acme.test',
		'email_verified' => '0',
		'iss' => 'https://accounts.google.com',
		'exp' => 1760000900,
		'name' => 'Ops',
		'sub' => '1',
	)), 'id.apps.googleusercontent.com');
	$issuer = epc_auth_google_verify_id_token(hull_jwt(array(
		'aud' => 'id.apps.googleusercontent.com',
		'email' => 'ops@acme.test',
		'email_verified' => true,
		'iss' => 'evil.example',
		'exp' => 1760000900,
		'name' => 'Ops',
		'sub' => '1',
	)), 'id.apps.googleusercontent.com');
	$expired = epc_auth_google_verify_id_token(hull_jwt(array(
		'aud' => 'id.apps.googleusercontent.com',
		'email' => 'ops@acme.test',
		'email_verified' => true,
		'iss' => 'accounts.google.com',
		'exp' => 1759999999,
		'name' => 'Ops',
		'sub' => '1',
	)), 'id.apps.googleusercontent.com');
	$ok = epc_auth_google_verify_id_token(hull_jwt(array(
		'aud' => 'id.apps.googleusercontent.com',
		'email' => ' Ops@Acme.TEST ',
		'email_verified' => 1,
		'iss' => 'https://accounts.google.com',
		'exp' => 1760000900,
		'name' => '  Ops User  ',
		'sub' => 'sub-9',
	)), 'id.apps.googleusercontent.com');
	$GLOBALS['HULL_OAUTH']['google']['client_id'] = '';
	$noCfg = epc_auth_google_exchange_code('abc');
	$GLOBALS['HULL_OAUTH']['google']['client_id'] = 'id.apps.googleusercontent.com';
	$GLOBALS['HULL_HTTP'] = function () {
		return array(false, '', 'timeout');
	};
	$failHttp = epc_auth_google_exchange_code('abc');
	$GLOBALS['HULL_HTTP'] = function () {
		return array(true, '{"access_token":"x"}', '');
	};
	$badJson = epc_auth_google_exchange_code('abc');
	$GLOBALS['HULL_HTTP'] = function () {
		$tok = hull_jwt(array(
			'aud' => 'id.apps.googleusercontent.com',
			'email' => 'ops@acme.test',
			'email_verified' => true,
			'iss' => 'https://accounts.google.com',
			'exp' => 1760000900,
			'name' => 'Ops',
			'sub' => '1',
		));
		return array(true, json_encode(array('id_token' => $tok)), '');
	};
	$exOk = epc_auth_google_exchange_code('abc');
	return array($emptyStart, $start, $malformed, $aud, $unverified, $zero, $issuer, $expired, $ok, $noCfg, $failHttp, $badJson, $exOk);
}

function hull_run_login_html(): array
{
	$lost = epc_auth_google_complete_login(array('am' => 'cp', 'tk' => ''), array('email' => 'a@b.c', 'name' => 'A'));
	$GLOBALS['HULL_RESOLVE'] = array('ok' => false, 'message' => 'no-host');
	$noHost = epc_auth_google_complete_login(array('am' => 'cp', 'tk' => 'gone'), array('email' => 'a@b.c'));
	$GLOBALS['HULL_RESOLVE'] = array('ok' => true, 'tenant_key' => 'acme');
	$GLOBALS['HULL_PROVISION_CP'] = 0;
	$noCp = epc_auth_google_complete_login(array('am' => 'cp', 'tk' => 'acme'), array('email' => 'a@b.c'));
	$GLOBALS['HULL_PROVISION_CP'] = 7;
	$GLOBALS['HULL_FINISH'] = array('ok' => false, 'message' => 'session-denied');
	$noSess = epc_auth_google_complete_login(array('am' => 'cp', 'tk' => 'acme', 'rh' => 'cp.acme.test', 'rp' => '/cp/users', 'lp' => 'en'), array('email' => 'a@b.c', 'name' => 'A'));
	$GLOBALS['HULL_FINISH'] = array('ok' => true, 'redirect' => '/cp/');
	$okCp = epc_auth_google_complete_login(array('am' => 'cp', 'tk' => 'acme', 'rh' => 'cp.acme.test', 'rp' => '/cp/users', 'lp' => 'en'), array('email' => 'a@b.c', 'name' => 'A'));
	$GLOBALS['HULL_RESOLVE'] = array('ok' => true, 'tenant_key' => 'beta');
	$GLOBALS['HULL_PROVISION_SF'] = 0;
	$noSf = epc_auth_google_complete_login(array('am' => 'storefront', 'tk' => 'beta'), array('email' => 'c@d.e'));
	$GLOBALS['HULL_PROVISION_SF'] = 11;
	$okSf = epc_auth_google_complete_login(array('am' => 'STOREFRONT', 'tk' => 'beta', 'rh' => 'shop.beta.test'), array('email' => 'c@d.e', 'name' => 'C'));
	$GLOBALS['HULL_POLICY'] = array('password' => true, 'email_otp' => true, 'google_oauth' => true);
	$GLOBALS['HULL_BUTTONS'] = '<div class="epc-oauth-buttons">G</div>';
	$GLOBALS['HULL_OTP'] = 'OTP';
	$htmlBoth = epc_cp_login_modern_auth_html(array('tenant_key' => 'acme', 'login_label' => "Ops & Co", 'context' => 'cp'));
	$htmlAgain = epc_cp_login_modern_auth_html(array('tenant_key' => 'acme', 'login_label' => 'Ops', 'context' => 'cp'));
	$GLOBALS['HULL_HTML_CALLS'] = 0;
	$GLOBALS['HULL_POLICY'] = array('password' => false, 'email_otp' => true, 'google_oauth' => false);
	$GLOBALS['HULL_BUTTONS'] = '';
	$htmlOtp = epc_cp_login_modern_auth_html(array('tenant_key' => 'beta', 'login_label' => 'Store', 'context' => 'storefront'));
	return array($lost, $noHost, $noCp, $noSess, $okCp, $noSf, $okSf, $htmlBoth, $htmlAgain, $htmlOtp, $GLOBALS['HULL_LAST_CTX'] ?? null);
}

function hull_write_stub_common($dir)
{
	file_put_contents($dir . '/epc_auth_common.php', '<?php
function epc_auth_oauth_config(): array { return $GLOBALS["HULL_OAUTH"] ?? array("google"=>array("client_id"=>"","client_secret"=>"","redirect_uri"=>"")); }
function epc_auth_signing_secret(): string { return (string) ($GLOBALS["HULL_SECRET"] ?? "secret"); }
function epc_auth_normalize_mode(string $mode): string { return strtolower(trim($mode)) === "storefront" ? "storefront" : "cp"; }
function epc_auth_resolve_for_mode(string $mode, array $hints): array { return $GLOBALS["HULL_RESOLVE"] ?? array("ok"=>false,"message"=>"Tenant context lost"); }
function epc_auth_context_from_registry_key(string $key, string $kind): array { return $GLOBALS["HULL_CTX_REG"] ?? array("ok"=>false); }
function epc_auth_storefront_from_registry_key(string $key, string $kind): array { return $GLOBALS["HULL_SF_REG"] ?? array("ok"=>false); }
function epc_auth_find_or_provision_storefront_customer(array $ctx, string $email, string $name): int { $GLOBALS["HULL_LAST_CTX"]=$ctx; return (int) ($GLOBALS["HULL_PROVISION_SF"] ?? 0); }
function epc_auth_find_or_provision_cp_user(array $ctx, string $email, string $name): int { $GLOBALS["HULL_LAST_CTX"]=$ctx; return (int) ($GLOBALS["HULL_PROVISION_CP"] ?? 0); }
function epc_auth_finish_login(array $ctx, int $userId, string $via = "email"): array { $GLOBALS["HULL_LAST_CTX"]=$ctx; return $GLOBALS["HULL_FINISH"] ?? array("ok"=>false,"message"=>"Could not create session"); }
function epc_auth_post_login_redirect(array $ctx): string { return (string) ($ctx["return_path"] ?? "/cp/"); }
function epc_cp_modern_auth_policy(?PDO $pdo = null): array { return $GLOBALS["HULL_POLICY"] ?? array("password"=>true,"email_otp"=>true,"google_oauth"=>true); }
');
	file_put_contents($dir . '/epc_oauth_buttons.php', '<?php
function epc_oauth_buttons_render(array $cfg): string { return (string) ($GLOBALS["HULL_BUTTONS"] ?? ""); }
');
	file_put_contents($dir . '/epc_otp_modal.php', '<?php
function epc_otp_modal_render(array $cfg): string { return (string) ($GLOBALS["HULL_OTP"] ?? ""); }
');
}

function hull_patch_social(string $src, string $dest): void
{
	$code = file_get_contents($src);
	$code = str_replace('bin2hex(random_bytes(16))', 'hull_nonce()', $code);
	$code = preg_replace('/\btime\(\)/', 'hull_now()', $code);
	$code = str_replace(
		"\tstatic \$callCount = 0;\n\t\$callCount++;\n\tif (\$callCount > 1) {",
		"\t\$GLOBALS['HULL_HTML_CALLS'] = (int) (\$GLOBALS['HULL_HTML_CALLS'] ?? 0) + 1;\n\tif (\$GLOBALS['HULL_HTML_CALLS'] > 1) {",
		$code
	);
	$curl = <<<'PHP'
	$hit = hull_http_post('https://oauth2.googleapis.com/token', $body);
	$raw = $hit[0] ? $hit[1] : false;
	$err = $hit[0] ? '' : (string) $hit[2];
	if ($raw === false) {
PHP;
	$code = preg_replace(
		'/\t\$ch = curl_init\(\'https:\/\/oauth2\.googleapis\.com\/token\'\);.*?if \(\$raw === false\) \{/s',
		$curl,
		$code,
		1,
		$count
	);
	if ($count !== 1) {
		fwrite(STDERR, "failed to patch curl block\n");
		exit(1);
	}
	file_put_contents($dest, $code);
}

if (isset($argv[2])) {
	$case = $spec['cases'][(int) $argv[2]];
	$work = sys_get_temp_dir() . '/ecomae_cpw_q1h_' . substr(md5(uniqid('', true)), 0, 8);
	@mkdir($work . '/content/general_pages', 0777, true);
	hull_write_stub_common($work . '/content/general_pages');
	hull_patch_social($root . '/content/general_pages/epc_auth_social.php', $work . '/content/general_pages/epc_auth_social.php');
	$cleanup = function () use ($work) {
		foreach (glob($work . '/content/general_pages/*') ?: array() as $file) {
			@unlink($file);
		}
		@rmdir($work . '/content/general_pages');
		@rmdir($work . '/content');
		@rmdir($work);
	};
	if (!defined('_ASTEXE_')) {
		define('_ASTEXE_', 1);
	}
	$_SERVER['DOCUMENT_ROOT'] = $work;
	$GLOBALS['HULL_NOW'] = 1760000000;
	$GLOBALS['HULL_NONCE'] = str_repeat('a', 32);
	$GLOBALS['HULL_SECRET'] = 'secret';
	$GLOBALS['HULL_OAUTH'] = array('google' => array(
		'client_id' => '',
		'client_secret' => '',
		'redirect_uri' => 'https://www.ecomae.com/epc-auth-google-callback.php',
	));
	$GLOBALS['HULL_RESOLVE'] = array('ok' => false, 'message' => 'Tenant context lost');
	$GLOBALS['HULL_CTX_REG'] = array('ok' => false);
	$GLOBALS['HULL_SF_REG'] = array('ok' => false);
	$GLOBALS['HULL_PROVISION_CP'] = 0;
	$GLOBALS['HULL_PROVISION_SF'] = 0;
	$GLOBALS['HULL_FINISH'] = array('ok' => false, 'message' => 'Could not create session');
	$GLOBALS['HULL_POLICY'] = array('password' => true, 'email_otp' => true, 'google_oauth' => true);
	$GLOBALS['HULL_BUTTONS'] = '';
	$GLOBALS['HULL_OTP'] = '';
	$GLOBALS['HULL_LAST_CTX'] = null;
	$GLOBALS['HULL_HTTP'] = null;
	require $work . '/content/general_pages/epc_auth_social.php';
	register_shutdown_function($cleanup);
	if (isset($case['eval'])) {
		$result = eval($case['eval']);
		echo json_encode(array('name' => $case['name'], 'output' => '', 'result' => $result), JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES | JSON_PARTIAL_OUTPUT_ON_ERROR);
	}
	exit(0);
}

$results = array();
foreach ($spec['cases'] as $i => $case) {
	$cmd = escapeshellarg(PHP_BINARY) . ' ' . escapeshellarg(__FILE__) . ' ' . escapeshellarg($root) . ' ' . $i;
	$out = shell_exec($cmd . ' 2>/tmp/plan_q1hull_err_' . $i . '.txt');
	$row = json_decode((string) $out, true);
	if (!is_array($row)) {
		fwrite(STDERR, "case {$case['name']} failed:\n" . $out . "\n" . @file_get_contents('/tmp/plan_q1hull_err_' . $i . '.txt') . "\n");
		exit(1);
	}
	$results[] = $row;
}
echo json_encode(array('php' => PHP_VERSION, 'results' => $results), JSON_PRETTY_PRINT | JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES), "\n";
