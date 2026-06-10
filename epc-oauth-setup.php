<?php
/**
 * Multi-provider social login setup (token-gated).
 *
 * Creates the platform credential store `epc_oauth_config`, ensures the
 * per-tenant `epc_oauth_identity` table exists on the platform DB (it is also
 * created lazily on each tenant DB at first link), seeds empty provider rows,
 * and reports the redirect URIs + configuration status.
 *
 * Run: https://www.ecomae.com/epc-oauth-setup.php?token=epartscart-deploy-2026
 */
error_reporting(E_ALL);
ini_set('display_errors', '1');
header('Content-Type: application/json; charset=utf-8');

require_once __DIR__ . '/epc_deploy_auth.php';
epc_deploy_require_token();

define('_ASTEXE_', 1);

$out = array(
	'status' => true,
	'steps' => array(),
	'redirect_uri' => '',
	'providers' => array(),
);

try {
	require_once __DIR__ . '/content/general_pages/epc_oauth_providers.php';

	$pdo = epc_oauth_platform_pdo();
	if (!$pdo instanceof PDO) {
		throw new Exception('Platform database unavailable');
	}

	epc_oauth_ensure_config_schema($pdo);
	$out['steps'][] = 'epc_oauth_config table ensured (platform DB)';

	epc_oauth_ensure_identity_schema($pdo);
	$out['steps'][] = 'epc_oauth_identity table ensured (platform DB; also created lazily per tenant)';

	// Seed empty rows for every known provider so the CP form has something to edit.
	$now = time();
	$seed = $pdo->prepare(
		'INSERT IGNORE INTO `epc_oauth_config` (`provider`, `client_id`, `client_secret`, `extra_json`, `enabled`, `updated_at`)
		 VALUES (?, \'\', \'\', \'{}\', 0, ?)'
	);
	foreach (epc_oauth_provider_ids() as $pid) {
		$seed->execute(array($pid, $now));
	}
	$out['steps'][] = 'Seeded provider rows: ' . implode(', ', epc_oauth_provider_ids());

	$out['redirect_uri'] = epc_oauth_callback_url();
	$defs = epc_oauth_provider_defs();
	foreach ($defs as $pid => $def) {
		$creds = epc_oauth_provider_credentials($pid);
		$configured = epc_oauth_is_configured($pid);
		$out['providers'][$pid] = array(
			'label' => (string) ($def['label'] ?? $pid),
			'configured' => $configured,
			'status' => $configured ? 'live' : ($creds['client_id'] !== '' ? 'incomplete (needs secret/key)' : 'stub (needs client credentials)'),
			'scope' => (string) ($def['scope'] ?? ''),
			'redirect_uri' => epc_oauth_callback_url(),
		);
	}

	$out['cp_settings'] = 'Super CP → Modern auth settings (control/portal/epc_cp_auth_settings) → "Social sign-in providers"';
	$out['enabled_now'] = epc_oauth_enabled_providers();
	$out['message'] = 'OAuth infrastructure ready. Add client credentials in Super CP to activate providers.';
} catch (Throwable $e) {
	http_response_code(500);
	$out['status'] = false;
	$out['message'] = $e->getMessage();
	$out['file'] = $e->getFile() . ':' . $e->getLine();
}

echo json_encode($out, JSON_PRETTY_PRINT | JSON_UNESCAPED_SLASHES);
