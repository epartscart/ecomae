<?php
/**
 * Multi-provider social login (Skywork-style) — shared backend.
 *
 * Providers: google, microsoft, facebook, github, apple.
 *
 * Credentials are read from (in order of precedence, non-empty wins):
 *   1. DB table `epc_oauth_config` on the platform database (editable in Super CP)
 *   2. config.epc-oauth.php file at DOCUMENT_ROOT (legacy Google + manual overrides)
 *
 * Account linking: verified email is matched against the tenant `users` table via
 * the existing provisioning helpers; a row is recorded in per-tenant
 * `epc_oauth_identity` for audit + fast re-login.
 *
 * Reuses epc_auth_common.php / epc_auth_social.php for context, sessions and
 * Google id_token verification, so storefront + CP session creation is shared.
 */
if (!defined('_ASTEXE_')) {
	define('_ASTEXE_', 1);
}

require_once __DIR__ . '/epc_auth_common.php';
require_once __DIR__ . '/epc_auth_social.php';

/**
 * Central callback URL — single clean path for all providers (Apple-friendly,
 * provider is carried in the signed state). No query string so it matches the
 * strictest provider redirect-URI rules.
 */
function epc_oauth_callback_url(): string
{
	return 'https://www.ecomae.com/api/epc_oauth_callback.php';
}

/**
 * Static provider metadata (endpoints, scopes, branding).
 *
 * @return array<string,array<string,mixed>>
 */
function epc_oauth_provider_defs(): array
{
	return array(
		'google' => array(
			'id'            => 'google',
			'label'         => 'Google',
			'authorize_url' => 'https://accounts.google.com/o/oauth2/v2/auth',
			'token_url'     => 'https://oauth2.googleapis.com/token',
			'scope'         => 'openid email profile',
			'color'         => '#ffffff',
			'text_color'    => '#3c4043',
			'border'        => '#dadce0',
		),
		'microsoft' => array(
			'id'            => 'microsoft',
			'label'         => 'Microsoft',
			'authorize_url' => 'https://login.microsoftonline.com/{tenant}/oauth2/v2.0/authorize',
			'token_url'     => 'https://login.microsoftonline.com/{tenant}/oauth2/v2.0/token',
			'scope'         => 'openid email profile',
			'color'         => '#ffffff',
			'text_color'    => '#3c4043',
			'border'        => '#dadce0',
		),
		'facebook' => array(
			'id'            => 'facebook',
			'label'         => 'Facebook',
			'authorize_url' => 'https://www.facebook.com/v19.0/dialog/oauth',
			'token_url'     => 'https://graph.facebook.com/v19.0/oauth/access_token',
			'userinfo_url'  => 'https://graph.facebook.com/v19.0/me?fields=id,name,email',
			'scope'         => 'email public_profile',
			'color'         => '#ffffff',
			'text_color'    => '#3c4043',
			'border'        => '#dadce0',
		),
		'github' => array(
			'id'            => 'github',
			'label'         => 'GitHub',
			'authorize_url' => 'https://github.com/login/oauth/authorize',
			'token_url'     => 'https://github.com/login/oauth/access_token',
			'userinfo_url'  => 'https://api.github.com/user',
			'emails_url'    => 'https://api.github.com/user/emails',
			'scope'         => 'read:user user:email',
			'color'         => '#ffffff',
			'text_color'    => '#3c4043',
			'border'        => '#dadce0',
		),
		'apple' => array(
			'id'            => 'apple',
			'label'         => 'Apple',
			'authorize_url' => 'https://appleid.apple.com/auth/authorize',
			'token_url'     => 'https://appleid.apple.com/auth/token',
			'scope'         => 'name email',
			'response_mode' => 'form_post',
			'color'         => '#ffffff',
			'text_color'    => '#3c4043',
			'border'        => '#dadce0',
		),
	);
}

/** @return string[] */
function epc_oauth_provider_ids(): array
{
	return array_keys(epc_oauth_provider_defs());
}

function epc_oauth_is_known_provider(string $provider): bool
{
	return in_array($provider, epc_oauth_provider_ids(), true);
}

/** Platform PDO (credentials store lives here). */
function epc_oauth_platform_pdo(): ?PDO
{
	$portalFile = $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_portal.php';
	if (is_file($portalFile)) {
		require_once $portalFile;
		if (function_exists('epc_portal_platform_pdo')) {
			$pdo = epc_portal_platform_pdo();
			if ($pdo instanceof PDO) {
				return $pdo;
			}
		}
	}
	try {
		$cfg = epc_auth_bootstrap_config();
		return new PDO(
			'mysql:host=' . $cfg->host . ';dbname=' . $cfg->db . ';charset=utf8',
			$cfg->user,
			$cfg->password,
			array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION)
		);
	} catch (Throwable $e) {
		return null;
	}
}

/** Provider credential store (platform-wide). */
function epc_oauth_ensure_config_schema(PDO $pdo): void
{
	$pdo->exec(
		'CREATE TABLE IF NOT EXISTS `epc_oauth_config` (
			`provider` VARCHAR(32) NOT NULL PRIMARY KEY,
			`client_id` VARCHAR(255) NOT NULL DEFAULT \'\',
			`client_secret` TEXT NULL,
			`extra_json` TEXT NULL,
			`enabled` TINYINT(1) NOT NULL DEFAULT 1,
			`updated_at` INT NOT NULL DEFAULT 0
		) ENGINE=InnoDB DEFAULT CHARSET=utf8'
	);
}

/** Per-tenant identity link table (user_id references the tenant `users` table). */
function epc_oauth_ensure_identity_schema(PDO $pdo): void
{
	$pdo->exec(
		'CREATE TABLE IF NOT EXISTS `epc_oauth_identity` (
			`id` INT UNSIGNED NOT NULL AUTO_INCREMENT PRIMARY KEY,
			`provider` VARCHAR(32) NOT NULL,
			`provider_sub` VARCHAR(191) NOT NULL,
			`email` VARCHAR(190) NOT NULL DEFAULT \'\',
			`user_id` INT NOT NULL DEFAULT 0,
			`auth_mode` VARCHAR(16) NOT NULL DEFAULT \'storefront\',
			`tenant_key` VARCHAR(64) NOT NULL DEFAULT \'\',
			`created_at` INT NOT NULL DEFAULT 0,
			`last_login_at` INT NOT NULL DEFAULT 0,
			UNIQUE KEY `provider_sub` (`provider`, `provider_sub`),
			INDEX `email` (`email`),
			INDEX `user_id` (`user_id`)
		) ENGINE=InnoDB DEFAULT CHARSET=utf8'
	);
}

/**
 * Load credentials for one provider, merged file + DB (DB non-empty wins,
 * except Google where the legacy file remains source of truth for back-compat).
 *
 * @return array{client_id:string,client_secret:string,extra:array<string,mixed>,enabled:bool}
 */
function epc_oauth_provider_credentials(string $provider): array
{
	$out = array('client_id' => '', 'client_secret' => '', 'extra' => array(), 'enabled' => true);

	// File config (legacy google + manual overrides).
	$fileCfg = epc_auth_oauth_config();
	if (isset($fileCfg[$provider]) && is_array($fileCfg[$provider])) {
		$out['client_id']     = trim((string) ($fileCfg[$provider]['client_id'] ?? ''));
		$out['client_secret'] = trim((string) ($fileCfg[$provider]['client_secret'] ?? ''));
		foreach ($fileCfg[$provider] as $k => $v) {
			if (!in_array($k, array('client_id', 'client_secret'), true)) {
				$out['extra'][$k] = $v;
			}
		}
	}

	// DB overlay.
	$pdo = epc_oauth_platform_pdo();
	if ($pdo instanceof PDO) {
		try {
			epc_oauth_ensure_config_schema($pdo);
			$st = $pdo->prepare('SELECT * FROM `epc_oauth_config` WHERE `provider` = ? LIMIT 1');
			$st->execute(array($provider));
			$row = $st->fetch(PDO::FETCH_ASSOC);
			if ($row) {
				$dbId     = trim((string) ($row['client_id'] ?? ''));
				$dbSecret = trim((string) ($row['client_secret'] ?? ''));
				$dbExtra  = json_decode((string) ($row['extra_json'] ?? ''), true);
				if ($dbId !== '') {
					$out['client_id'] = $dbId;
				}
				if ($dbSecret !== '') {
					$out['client_secret'] = $dbSecret;
				}
				if (is_array($dbExtra)) {
					$out['extra'] = array_merge($out['extra'], $dbExtra);
				}
				$out['enabled'] = (int) ($row['enabled'] ?? 1) === 1;
			}
		} catch (Throwable $e) {
			// fall back to file config silently
		}
	}

	return $out;
}

/**
 * A provider is "configured" when it has the credentials it needs to run a
 * real OAuth exchange (so we can hide unconfigured providers on the frontend).
 */
function epc_oauth_is_configured(string $provider): bool
{
	if (!epc_oauth_is_known_provider($provider)) {
		return false;
	}
	$c = epc_oauth_provider_credentials($provider);
	if (empty($c['enabled'])) {
		return false;
	}
	if ($c['client_id'] === '') {
		return false;
	}
	if ($provider === 'apple') {
		// Apple uses a signed JWT client secret built from a .p8 key.
		$haveSecret = trim((string) ($c['client_secret'] ?? '')) !== '';
		$haveKey = trim((string) ($c['extra']['private_key'] ?? '')) !== ''
			&& trim((string) ($c['extra']['team_id'] ?? '')) !== ''
			&& trim((string) ($c['extra']['key_id'] ?? '')) !== '';
		return $haveSecret || $haveKey;
	}
	return trim((string) ($c['client_secret'] ?? '')) !== '';
}

/** @return string[] list of configured provider ids (for the frontend buttons). */
function epc_oauth_enabled_providers(): array
{
	$ids = array();
	foreach (epc_oauth_provider_ids() as $id) {
		if (epc_oauth_is_configured($id)) {
			$ids[] = $id;
		}
	}
	return $ids;
}

/* ───────────────────────── State (CSRF-safe, signed) ───────────────────────── */

function epc_oauth_state_pack(string $provider, array $context, string $nonce): string
{
	$payload = array(
		'pv' => $provider,
		'n'  => $nonce,
		'tk' => (string) ($context['tenant_key'] ?? ''),
		'k'  => (string) ($context['kind'] ?? ''),
		'rh' => (string) ($context['return_host'] ?? ''),
		'rp' => (string) ($context['return_path'] ?? '/'),
		'am' => epc_auth_normalize_mode((string) ($context['auth_mode'] ?? 'cp')),
		'lp' => (string) ($context['lang_prefix'] ?? ''),
		'ru' => (string) ($context['return_url'] ?? ''),
		'tm' => !empty($context['terms_accepted']) ? 1 : 0,
		't'  => time(),
	);
	$json = json_encode($payload);
	$p = rtrim(strtr(base64_encode((string) $json), '+/', '-_'), '=');
	$sig = hash_hmac('sha256', $p, epc_auth_signing_secret());
	return $p . '.' . $sig;
}

function epc_oauth_state_unpack(string $state): ?array
{
	$parts = explode('.', $state, 2);
	if (count($parts) !== 2) {
		return null;
	}
	$expected = hash_hmac('sha256', $parts[0], epc_auth_signing_secret());
	if (!hash_equals($expected, $parts[1])) {
		return null;
	}
	$json = base64_decode(strtr($parts[0], '-_', '+/') . str_repeat('=', (4 - strlen($parts[0]) % 4) % 4));
	$data = json_decode((string) $json, true);
	if (!is_array($data) || empty($data['n']) || empty($data['t']) || empty($data['pv'])) {
		return null;
	}
	if (time() - (int) $data['t'] > 900) {
		return null;
	}
	if (!epc_oauth_is_known_provider((string) $data['pv'])) {
		return null;
	}
	return $data;
}

/* ───────────────────────── Authorization URL ───────────────────────── */

/**
 * Build the provider authorize URL for the given login context.
 * Returns '' when the provider is not configured.
 */
function epc_oauth_build_auth_url(string $provider, array $context): string
{
	if (!epc_oauth_is_configured($provider)) {
		return '';
	}
	$defs = epc_oauth_provider_defs();
	$def = $defs[$provider];
	$c = epc_oauth_provider_credentials($provider);
	$nonce = bin2hex(random_bytes(16));
	$state = epc_oauth_state_pack($provider, $context, $nonce);
	$redirect = epc_oauth_callback_url();

	$authorize = (string) $def['authorize_url'];
	if ($provider === 'microsoft') {
		$tenant = trim((string) ($c['extra']['tenant'] ?? 'common'));
		if ($tenant === '') {
			$tenant = 'common';
		}
		$authorize = str_replace('{tenant}', rawurlencode($tenant), $authorize);
	}

	$params = array(
		'client_id'     => $c['client_id'],
		'redirect_uri'  => $redirect,
		'response_type' => 'code',
		'scope'         => (string) $def['scope'],
		'state'         => $state,
	);

	switch ($provider) {
		case 'google':
			$params['nonce'] = $nonce;
			$params['prompt'] = 'select_account';
			$params['access_type'] = 'online';
			break;
		case 'microsoft':
			$params['nonce'] = $nonce;
			$params['response_mode'] = 'query';
			$params['prompt'] = 'select_account';
			break;
		case 'apple':
			$params['response_mode'] = 'form_post';
			break;
		case 'github':
			$params['allow_signup'] = 'true';
			break;
	}

	return $authorize . '?' . http_build_query($params);
}

/* ───────────────────────── HTTP helpers ───────────────────────── */

/** @return array{code:int,body:string,error:string} */
function epc_oauth_http(string $method, string $url, $body = null, array $headers = array()): array
{
	$ch = curl_init($url);
	$opts = array(
		CURLOPT_RETURNTRANSFER => true,
		CURLOPT_TIMEOUT => 30,
		CURLOPT_FOLLOWLOCATION => false,
		CURLOPT_USERAGENT => 'epartscart-oauth/1.0',
	);
	if (strtoupper($method) === 'POST') {
		$opts[CURLOPT_POST] = true;
		$opts[CURLOPT_POSTFIELDS] = is_array($body) ? http_build_query($body) : (string) $body;
	}
	if ($headers !== array()) {
		$opts[CURLOPT_HTTPHEADER] = $headers;
	}
	curl_setopt_array($ch, $opts);
	$raw = curl_exec($ch);
	$err = curl_error($ch);
	$code = (int) curl_getinfo($ch, CURLINFO_HTTP_CODE);
	curl_close($ch);
	return array('code' => $code, 'body' => $raw === false ? '' : (string) $raw, 'error' => $err);
}

function epc_oauth_decode_jwt_payload(string $jwt): ?array
{
	$parts = explode('.', $jwt);
	if (count($parts) < 2) {
		return null;
	}
	$json = base64_decode(strtr($parts[1], '-_', '+/') . str_repeat('=', (4 - strlen($parts[1]) % 4) % 4));
	$data = json_decode((string) $json, true);
	return is_array($data) ? $data : null;
}

/* ───────────────────────── Apple client secret (ES256 JWT) ───────────────────────── */

function epc_oauth_apple_client_secret(array $c): string
{
	// If an explicit secret JWT was provided, use it.
	$explicit = trim((string) ($c['client_secret'] ?? ''));
	if ($explicit !== '' && substr_count($explicit, '.') === 2) {
		return $explicit;
	}
	$teamId  = trim((string) ($c['extra']['team_id'] ?? ''));
	$keyId   = trim((string) ($c['extra']['key_id'] ?? ''));
	$privKey = trim((string) ($c['extra']['private_key'] ?? ''));
	$clientId = trim((string) ($c['client_id'] ?? '')); // Apple "Services ID"
	if ($teamId === '' || $keyId === '' || $privKey === '' || $clientId === '') {
		return '';
	}
	if (!function_exists('openssl_sign')) {
		return '';
	}
	$header = array('alg' => 'ES256', 'kid' => $keyId, 'typ' => 'JWT');
	$now = time();
	$claims = array(
		'iss' => $teamId,
		'iat' => $now,
		'exp' => $now + 3600,
		'aud' => 'https://appleid.apple.com',
		'sub' => $clientId,
	);
	$enc = function ($arr) {
		return rtrim(strtr(base64_encode((string) json_encode($arr)), '+/', '-_'), '=');
	};
	$signingInput = $enc($header) . '.' . $enc($claims);
	$pkey = openssl_pkey_get_private($privKey);
	if ($pkey === false) {
		return '';
	}
	$der = '';
	if (!openssl_sign($signingInput, $der, $pkey, OPENSSL_ALGO_SHA256)) {
		return '';
	}
	// Convert DER ECDSA signature to raw R||S (64 bytes) required by JWS ES256.
	$sig = epc_oauth_der_to_raw_ecdsa($der, 64);
	if ($sig === '') {
		return '';
	}
	$sigB64 = rtrim(strtr(base64_encode($sig), '+/', '-_'), '=');
	return $signingInput . '.' . $sigB64;
}

function epc_oauth_der_to_raw_ecdsa(string $der, int $partLen): string
{
	$offset = 0;
	if (!isset($der[$offset]) || ord($der[$offset++]) !== 0x30) {
		return '';
	}
	// total length (skip, may be long form)
	$lenByte = ord($der[$offset++]);
	if ($lenByte & 0x80) {
		$offset += ($lenByte & 0x7f);
	}
	$readInt = function () use ($der, &$offset) {
		if (!isset($der[$offset]) || ord($der[$offset++]) !== 0x02) {
			return null;
		}
		$len = ord($der[$offset++]);
		$val = substr($der, $offset, $len);
		$offset += $len;
		return ltrim($val, "\x00");
	};
	$r = $readInt();
	$s = $readInt();
	if ($r === null || $s === null) {
		return '';
	}
	$half = $partLen / 2;
	$r = str_pad($r, (int) $half, "\x00", STR_PAD_LEFT);
	$s = str_pad($s, (int) $half, "\x00", STR_PAD_LEFT);
	return $r . $s;
}

/* ───────────────────────── Code → token → profile ───────────────────────── */

/**
 * Exchange an authorization code for a normalized profile.
 *
 * @return array{ok:bool,message?:string,email?:string,name?:string,sub?:string,email_verified?:bool}
 */
function epc_oauth_exchange_code(string $provider, string $code): array
{
	if (!epc_oauth_is_configured($provider)) {
		return array('ok' => false, 'message' => ucfirst($provider) . ' sign-in is not configured');
	}
	if ($code === '') {
		return array('ok' => false, 'message' => 'Missing authorization code');
	}
	$defs = epc_oauth_provider_defs();
	$def = $defs[$provider];
	$c = epc_oauth_provider_credentials($provider);
	$redirect = epc_oauth_callback_url();

	$clientSecret = (string) ($c['client_secret'] ?? '');
	$tokenUrl = (string) $def['token_url'];
	if ($provider === 'microsoft') {
		$tenant = trim((string) ($c['extra']['tenant'] ?? 'common')) ?: 'common';
		$tokenUrl = str_replace('{tenant}', rawurlencode($tenant), $tokenUrl);
	}
	if ($provider === 'apple') {
		$clientSecret = epc_oauth_apple_client_secret($c);
		if ($clientSecret === '') {
			return array('ok' => false, 'message' => 'Apple sign-in key is incomplete (need Services ID, Team ID, Key ID and .p8 key)');
		}
	}

	$fields = array(
		'code'          => $code,
		'client_id'     => $c['client_id'],
		'client_secret' => $clientSecret,
		'redirect_uri'  => $redirect,
		'grant_type'    => 'authorization_code',
	);
	$headers = array('Content-Type: application/x-www-form-urlencoded');
	if ($provider === 'github') {
		$headers[] = 'Accept: application/json';
	}

	$res = epc_oauth_http('POST', $tokenUrl, $fields, $headers);
	if ($res['code'] < 200 || $res['code'] >= 300 || $res['body'] === '') {
		return array('ok' => false, 'message' => 'Token exchange failed with ' . ucfirst($provider));
	}
	$token = json_decode($res['body'], true);
	if (!is_array($token)) {
		// Facebook legacy returns urlencoded; parse defensively.
		parse_str($res['body'], $token);
	}
	if (!is_array($token) || (empty($token['access_token']) && empty($token['id_token']))) {
		return array('ok' => false, 'message' => 'Invalid token response from ' . ucfirst($provider));
	}

	switch ($provider) {
		case 'google':
			return epc_oauth_profile_from_google($token, $c['client_id']);
		case 'microsoft':
			return epc_oauth_profile_from_microsoft($token, $c);
		case 'apple':
			return epc_oauth_profile_from_apple($token, $c['client_id']);
		case 'facebook':
			return epc_oauth_profile_from_facebook((string) ($token['access_token'] ?? ''), $def);
		case 'github':
			return epc_oauth_profile_from_github((string) ($token['access_token'] ?? ''), $def);
	}
	return array('ok' => false, 'message' => 'Unsupported provider');
}

function epc_oauth_profile_from_google(array $token, string $clientId): array
{
	if (empty($token['id_token'])) {
		return array('ok' => false, 'message' => 'Google did not return an id_token');
	}
	$p = epc_auth_google_verify_id_token((string) $token['id_token'], $clientId);
	if (empty($p['ok'])) {
		return $p;
	}
	return array(
		'ok' => true,
		'email' => (string) ($p['email'] ?? ''),
		'name' => (string) ($p['name'] ?? ''),
		'sub' => (string) ($p['sub'] ?? ''),
		'email_verified' => true,
	);
}

function epc_oauth_profile_from_microsoft(array $token, array $c): array
{
	$claims = !empty($token['id_token']) ? epc_oauth_decode_jwt_payload((string) $token['id_token']) : null;
	$email = '';
	$name = '';
	$sub = '';
	if (is_array($claims)) {
		$aud = (string) ($claims['aud'] ?? '');
		if ($aud !== '' && $aud !== (string) $c['client_id']) {
			return array('ok' => false, 'message' => 'Microsoft id_token audience mismatch');
		}
		if (!empty($claims['exp']) && (int) $claims['exp'] < time()) {
			return array('ok' => false, 'message' => 'Microsoft id_token expired');
		}
		$email = strtolower(trim((string) ($claims['email'] ?? $claims['preferred_username'] ?? '')));
		$name = trim((string) ($claims['name'] ?? ''));
		$sub = (string) ($claims['sub'] ?? $claims['oid'] ?? '');
	}
	if ($email === '' && !empty($token['access_token'])) {
		$res = epc_oauth_http('GET', 'https://graph.microsoft.com/v1.0/me', null, array(
			'Authorization: Bearer ' . $token['access_token'],
			'Accept: application/json',
		));
		$me = json_decode($res['body'], true);
		if (is_array($me)) {
			$email = strtolower(trim((string) ($me['mail'] ?? $me['userPrincipalName'] ?? '')));
			$name = $name !== '' ? $name : trim((string) ($me['displayName'] ?? ''));
			$sub = $sub !== '' ? $sub : (string) ($me['id'] ?? '');
		}
	}
	if ($email === '' || !filter_var($email, FILTER_VALIDATE_EMAIL)) {
		return array('ok' => false, 'message' => 'Microsoft account has no usable email address');
	}
	return array('ok' => true, 'email' => $email, 'name' => $name, 'sub' => $sub, 'email_verified' => true);
}

function epc_oauth_profile_from_apple(array $token, string $clientId): array
{
	if (empty($token['id_token'])) {
		return array('ok' => false, 'message' => 'Apple did not return an id_token');
	}
	$claims = epc_oauth_decode_jwt_payload((string) $token['id_token']);
	if (!is_array($claims)) {
		return array('ok' => false, 'message' => 'Invalid Apple id_token');
	}
	$aud = (string) ($claims['aud'] ?? '');
	if ($aud !== '' && $aud !== $clientId) {
		return array('ok' => false, 'message' => 'Apple id_token audience mismatch');
	}
	if (!empty($claims['exp']) && (int) $claims['exp'] < time()) {
		return array('ok' => false, 'message' => 'Apple id_token expired');
	}
	if ((string) ($claims['iss'] ?? '') !== 'https://appleid.apple.com') {
		return array('ok' => false, 'message' => 'Apple id_token issuer invalid');
	}
	$email = strtolower(trim((string) ($claims['email'] ?? '')));
	$verified = $claims['email_verified'] ?? false;
	$verified = ($verified === true || $verified === 'true' || $verified === 1 || $verified === '1');
	if ($email === '') {
		return array('ok' => false, 'message' => 'Apple did not share an email (enable email scope / private relay)');
	}
	return array('ok' => true, 'email' => $email, 'name' => '', 'sub' => (string) ($claims['sub'] ?? ''), 'email_verified' => $verified);
}

function epc_oauth_profile_from_facebook(string $accessToken, array $def): array
{
	if ($accessToken === '') {
		return array('ok' => false, 'message' => 'Facebook did not return an access token');
	}
	$url = (string) $def['userinfo_url'] . '&access_token=' . rawurlencode($accessToken);
	$res = epc_oauth_http('GET', $url, null, array('Accept: application/json'));
	$me = json_decode($res['body'], true);
	if (!is_array($me)) {
		return array('ok' => false, 'message' => 'Could not read Facebook profile');
	}
	$email = strtolower(trim((string) ($me['email'] ?? '')));
	if ($email === '' || !filter_var($email, FILTER_VALIDATE_EMAIL)) {
		return array('ok' => false, 'message' => 'Facebook account has no shared email — use another method');
	}
	return array(
		'ok' => true,
		'email' => $email,
		'name' => trim((string) ($me['name'] ?? '')),
		'sub' => (string) ($me['id'] ?? ''),
		'email_verified' => true,
	);
}

function epc_oauth_profile_from_github(string $accessToken, array $def): array
{
	if ($accessToken === '') {
		return array('ok' => false, 'message' => 'GitHub did not return an access token');
	}
	$auth = array('Authorization: Bearer ' . $accessToken, 'Accept: application/json', 'User-Agent: epartscart-oauth');
	$res = epc_oauth_http('GET', (string) $def['userinfo_url'], null, $auth);
	$user = json_decode($res['body'], true);
	if (!is_array($user)) {
		return array('ok' => false, 'message' => 'Could not read GitHub profile');
	}
	$sub = (string) ($user['id'] ?? '');
	$name = trim((string) ($user['name'] ?? $user['login'] ?? ''));
	$email = strtolower(trim((string) ($user['email'] ?? '')));

	// Pick the primary verified email.
	$emailsRes = epc_oauth_http('GET', (string) $def['emails_url'], null, $auth);
	$emails = json_decode($emailsRes['body'], true);
	if (is_array($emails)) {
		$primary = '';
		$anyVerified = '';
		foreach ($emails as $row) {
			if (!is_array($row)) {
				continue;
			}
			$addr = strtolower(trim((string) ($row['email'] ?? '')));
			$isVerified = !empty($row['verified']);
			if ($addr === '' || !$isVerified) {
				continue;
			}
			if ($anyVerified === '') {
				$anyVerified = $addr;
			}
			if (!empty($row['primary'])) {
				$primary = $addr;
			}
		}
		$email = $primary !== '' ? $primary : ($anyVerified !== '' ? $anyVerified : $email);
		if ($primary === '' && $anyVerified === '') {
			return array('ok' => false, 'message' => 'No verified email on your GitHub account');
		}
	}
	if ($email === '' || !filter_var($email, FILTER_VALIDATE_EMAIL)) {
		return array('ok' => false, 'message' => 'GitHub account has no verified email');
	}
	return array('ok' => true, 'email' => $email, 'name' => $name, 'sub' => $sub, 'email_verified' => true);
}

/* ───────────────────────── Account linking + login ───────────────────────── */

function epc_oauth_record_identity(array $context, string $provider, array $profile, int $userId): void
{
	$pdo = $context['pdo'] ?? null;
	if (!$pdo instanceof PDO || $userId <= 0) {
		return;
	}
	try {
		epc_oauth_ensure_identity_schema($pdo);
		$now = time();
		$pdo->prepare(
			'INSERT INTO `epc_oauth_identity`
				(`provider`, `provider_sub`, `email`, `user_id`, `auth_mode`, `tenant_key`, `created_at`, `last_login_at`)
			 VALUES (?, ?, ?, ?, ?, ?, ?, ?)
			 ON DUPLICATE KEY UPDATE `email` = VALUES(`email`), `user_id` = VALUES(`user_id`),
				`auth_mode` = VALUES(`auth_mode`), `tenant_key` = VALUES(`tenant_key`), `last_login_at` = VALUES(`last_login_at`)'
		)->execute(array(
			$provider,
			(string) ($profile['sub'] ?? ''),
			(string) ($profile['email'] ?? ''),
			$userId,
			epc_auth_normalize_mode((string) ($context['auth_mode'] ?? 'cp')),
			(string) ($context['tenant_key'] ?? ''),
			$now,
			$now,
		));
	} catch (Throwable $e) {
		// non-fatal: identity audit only
	}
}

/**
 * Resolve context from state, link/create the account by verified email, and
 * start the storefront or CP session.
 *
 * @return array{ok:bool,message?:string,redirect?:string}
 */
function epc_oauth_complete_login(array $stateData, array $profile): array
{
	$provider = (string) ($stateData['pv'] ?? '');
	$email = strtolower(trim((string) ($profile['email'] ?? '')));
	if ($email === '' || !filter_var($email, FILTER_VALIDATE_EMAIL)) {
		return array('ok' => false, 'message' => 'No usable email returned by ' . ucfirst($provider));
	}
	if (empty($profile['email_verified'])) {
		return array('ok' => false, 'message' => 'Your ' . ucfirst($provider) . ' email is not verified');
	}

	$authMode = epc_auth_normalize_mode((string) ($stateData['am'] ?? 'cp'));
	$hints = array('tenant_key' => (string) ($stateData['tk'] ?? ''));
	$ctx = epc_auth_resolve_for_mode($authMode, $hints);
	if (empty($ctx['ok'])) {
		if ($authMode === 'cp' && (string) ($stateData['tk'] ?? '') !== '') {
			$ctx = epc_auth_context_from_registry_key((string) $stateData['tk'], (string) ($stateData['k'] ?? ''));
		}
		if (empty($ctx['ok']) && (string) ($stateData['tk'] ?? '') !== '') {
			$ctx = epc_auth_storefront_from_registry_key((string) $stateData['tk'], (string) ($stateData['k'] ?? ''));
		}
	}
	if (empty($ctx['ok'])) {
		return array('ok' => false, 'message' => (string) ($ctx['message'] ?? 'Tenant context lost'));
	}
	$ctx['auth_mode'] = $authMode;
	if (!empty($stateData['rh'])) {
		$ctx['return_host'] = (string) $stateData['rh'];
	}
	if (!empty($stateData['rp'])) {
		$ctx['return_path'] = (string) $stateData['rp'];
	}
	if (!empty($stateData['lp'])) {
		$ctx['lang_prefix'] = (string) $stateData['lp'];
	}

	$name = (string) ($profile['name'] ?? '');

	// Prefer an existing identity link (provider_sub) when present.
	$linkedUserId = 0;
	$pdo = $ctx['pdo'] ?? null;
	if ($pdo instanceof PDO && !empty($profile['sub'])) {
		try {
			epc_oauth_ensure_identity_schema($pdo);
			$st = $pdo->prepare('SELECT `user_id` FROM `epc_oauth_identity` WHERE `provider` = ? AND `provider_sub` = ? LIMIT 1');
			$st->execute(array($provider, (string) $profile['sub']));
			$linkedUserId = (int) $st->fetchColumn();
			if ($linkedUserId > 0) {
				// Confirm the user still exists / is unlocked.
				$chk = $pdo->prepare('SELECT `unlocked` FROM `users` WHERE `user_id` = ? LIMIT 1');
				$chk->execute(array($linkedUserId));
				$row = $chk->fetch(PDO::FETCH_ASSOC);
				if (!$row || (int) ($row['unlocked'] ?? 0) !== 1) {
					$linkedUserId = 0;
				}
			}
		} catch (Throwable $e) {
			$linkedUserId = 0;
		}
	}

	if ($linkedUserId > 0) {
		$userId = $linkedUserId;
	} elseif ($authMode === 'storefront') {
		$userId = epc_auth_find_or_provision_storefront_customer($ctx, $email, $name);
		if ($userId <= 0) {
			return array('ok' => false, 'message' => 'Could not sign in with this ' . ucfirst($provider) . ' account');
		}
	} else {
		$userId = epc_auth_find_or_provision_cp_user($ctx, $email, $name);
		if ($userId <= 0) {
			return array('ok' => false, 'message' => 'No CP access for this ' . ucfirst($provider) . ' account on this workspace');
		}
	}

	epc_oauth_record_identity($ctx, $provider, $profile, $userId);

	$returnUrl = (string) ($stateData['ru'] ?? '');
	$finish = epc_auth_finish_login($ctx, $userId, 'email', $returnUrl);
	if (empty($finish['ok'])) {
		return array('ok' => false, 'message' => (string) ($finish['message'] ?? 'Could not create session'));
	}

	// Remember the last-used Google email for the Skywork-style hint.
	if ($provider === 'google') {
		$secure = epc_auth_require_https();
		@setcookie('epc_oauth_last_google_email', $email, time() + 60 * 60 * 24 * 60, '/', '', $secure, false);
	}

	return array(
		'ok' => true,
		'redirect' => (string) ($finish['redirect'] ?? epc_auth_post_login_redirect($ctx)),
	);
}
