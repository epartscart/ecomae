<?php
/**
 * Token-authed smoke test — discovery source auth columns + save/list strip password.
 * GET /epc-apai-verify-source-auth.php?token=…&site_key=epartscart
 */
declare(strict_types=1);

require_once __DIR__ . '/epc_deploy_auth.php';
epc_deploy_require_token();
header('Content-Type: application/json; charset=utf-8');

define('_ASTEXE_', 1);
require_once __DIR__ . '/config.php';
require_once __DIR__ . '/content/shop/price_engine/epc_auto_price_engine.php';

$cfg = new DP_Config();
$siteKey = preg_replace('/[^a-z0-9_]/', '', strtolower(trim((string) ($_GET['site_key'] ?? 'epartscart'))));

try {
	$pdo = new PDO(
		'mysql:host=' . $cfg->host . ';dbname=' . $cfg->db . ';charset=utf8',
		(string) $cfg->user,
		(string) $cfg->password,
		array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION)
	);
	epc_disc_ensure_schema($pdo);

	$cols = array();
	foreach (array('auth_type', 'auth_username', 'auth_password') as $col) {
		$chk = $pdo->query("SHOW COLUMNS FROM `epc_discovery_sources` LIKE " . $pdo->quote($col))->fetch(PDO::FETCH_ASSOC);
		$cols[$col] = !empty($chk);
	}

	$testDomain = 'auth-test-' . substr(md5((string) time()), 0, 8) . '.example';
	$id = epc_disc_source_save($pdo, $siteKey, array(
		'domain' => $testDomain,
		'label' => 'Auth smoke test',
		'source_type' => 'custom_website',
		'created_by_tenant' => 1,
		'enabled' => 0,
		'requires_login' => 1,
		'auth_type' => 'form_login',
		'auth_username' => 'smoke_user',
		'auth_password' => 'smoke_pass_123',
		'login_url' => 'https://example.com/login',
	));

	$row = epc_disc_source_get($pdo, $id, $siteKey);
	$fmt = $row ? epc_disc_source_format_row($row) : array();
	$storedPwd = (string) ($row['auth_password'] ?? '');
	$decoded = epc_disc_auth_password_decode($storedPwd);

	epc_disc_source_delete($pdo, $id, $siteKey);

	echo json_encode(array(
		'ok' => !in_array(false, $cols, true),
		'site_key' => $siteKey,
		'columns' => $cols,
		'saved_id' => $id,
		'login_configured' => !empty($fmt['login_configured']),
		'password_stripped_in_format' => !array_key_exists('auth_password', $fmt),
		'password_encoded_at_rest' => strpos($storedPwd, 'b64:') === 0,
		'password_roundtrip_ok' => $decoded === 'smoke_pass_123',
		'auth_type' => $fmt['auth_type'] ?? '',
	), JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES);
} catch (Throwable $e) {
	http_response_code(500);
	echo json_encode(array('ok' => false, 'error' => $e->getMessage()));
}
