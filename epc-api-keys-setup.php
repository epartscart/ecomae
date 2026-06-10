<?php
/**
 * Create epc_api_keys table + read-only demo keys for epartscart and asap.
 * Run once: https://www.ecomae.com/epc-api-keys-setup.php?token=epartscart-deploy-2026
 * Keys printed here ONLY — never embed in marketing HTML.
 */
declare(strict_types=1);

header('Content-Type: text/plain; charset=utf-8');
require_once __DIR__ . '/epc_deploy_auth.php';
if (($_GET['token'] ?? '') !== epc_deploy_token()) {
	exit("Forbidden\n");
}

define('_ASTEXE_', 1);
require_once __DIR__ . '/config.php';
require_once __DIR__ . '/content/general_pages/epc_portal.php';
require_once __DIR__ . '/content/general_pages/epc_portal_db.php';
require_once __DIR__ . '/content/general_pages/epc_api_v1.php';

$cfg = new DP_Config();
epc_portal_apply_config($cfg);

$pdo = epc_portal_platform_pdo();
if (!$pdo instanceof PDO) {
	try {
		$pdo = new PDO(
			'mysql:host=' . $cfg->host . ';dbname=' . $cfg->db . ';charset=utf8',
			$cfg->user,
			$cfg->password,
			array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION)
		);
	} catch (Exception $e) {
		exit('DB connect failed: ' . $e->getMessage() . "\n");
	}
}

epc_api_v1_ensure_keys_table($pdo);

function epc_aks_make_key(string $tenant): string
{
	return 'epc_' . $tenant . '_read_' . bin2hex(random_bytes(8));
}

function epc_aks_upsert_key(PDO $pdo, string $tenant, string $label, array $scopes): string
{
	$tenant = preg_replace('/[^a-z0-9_]/', '', strtolower($tenant));
	$plain = epc_aks_make_key($tenant);
	$hash = hash('sha256', $plain);
	$prefix = substr($plain, 0, 16);
	$now = time();
	$scopesJson = json_encode($scopes, JSON_UNESCAPED_UNICODE);

	$existing = $pdo->prepare('SELECT `id` FROM `epc_api_keys` WHERE `tenant_site_key` = ? AND `label` = ? LIMIT 1');
	$existing->execute(array($tenant, $label));
	$id = (int) $existing->fetchColumn();

	if ($id > 0) {
		$pdo->prepare(
			'UPDATE `epc_api_keys` SET `key_hash` = ?, `key_prefix` = ?, `scopes_json` = ?, `active` = 1, `created_at` = ? WHERE `id` = ?'
		)->execute(array($hash, $prefix, $scopesJson, $now, $id));
	} else {
		$pdo->prepare(
			'INSERT INTO `epc_api_keys` (`tenant_site_key`, `key_hash`, `key_prefix`, `label`, `scopes_json`, `active`, `created_at`)
			 VALUES (?, ?, ?, ?, ?, 1, ?)'
		)->execute(array($tenant, $hash, $prefix, $label, $scopesJson, $now));
	}

	return $plain;
}

$readScopes = array('read:tenant', 'read:orders', 'read:products', 'read:erp');

$keys = array(
	'epartscart' => epc_aks_upsert_key($pdo, 'epartscart', 'Phase 1 read-only (epartscart)', $readScopes),
	'asap' => epc_aks_upsert_key($pdo, 'asap', 'Phase 1 read-only (asap ERP)', $readScopes),
);

echo "db: " . $cfg->db . "\n";
echo "table: epc_api_keys\n\n";
echo "=== DEMO API KEYS (store securely — NOT for marketing) ===\n\n";
foreach ($keys as $tenant => $plain) {
	echo $tenant . ":\n  " . $plain . "\n\n";
}
echo "Scopes: " . implode(', ', $readScopes) . "\n";
echo "Test:\n";
echo "  curl -s -H \"X-API-Key: YOUR_KEY\" https://www.ecomae.com/epc-api/v1/tenant/info\n";
echo "Done.\n";
