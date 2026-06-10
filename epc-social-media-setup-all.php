<?php
/**
 * Social Media Hub — schema, CP routes, menu on platform + live tenants.
 *
 * Dry-run:  https://www.ecomae.com/epc-social-media-setup-all.php?token=epartscart-deploy-2026
 * Apply:    …&apply=1
 * One DB:   …&apply=1&db=docpart
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
require_once __DIR__ . '/content/general_pages/epc_portal_tenant.php';
require_once __DIR__ . '/content/general_pages/epc_integrations_helpers.php';
require_once __DIR__ . '/content/social_media/epc_social_media_helpers.php';
require_once __DIR__ . '/epc_cp_mainstream_menu.php';

$apply = !empty($_GET['apply']) && (string) $_GET['apply'] === '1';
$onlyDb = preg_replace('/[^a-z0-9_]/', '', strtolower((string) ($_GET['db'] ?? '')));

$cfg = new DP_Config();
epc_portal_apply_config($cfg);
$backend = trim((string) $cfg->backend_dir, '/');
if ($backend === '') {
	$backend = 'cp';
}

$pages = array(
	array('slug' => 'epc_social_media_hub', 'key' => 'epc_social_media_hub_cp', 'en' => 'Social media hub', 'ru' => 'Соцсети — хаб', 'order' => 12),
);

$targets = array();
$targets[] = array(
	'label' => 'current_config',
	'host' => function_exists('epc_portal_host') ? epc_portal_host() : (string) ($_SERVER['HTTP_HOST'] ?? ''),
	'cred' => array('db' => $cfg->db, 'user' => $cfg->user, 'pass' => $cfg->password),
);

$platformPdo = epc_portal_platform_pdo();
if ($platformPdo instanceof PDO && function_exists('epc_portal_list_tenants')) {
	epc_portal_db_ensure($platformPdo);
	foreach (epc_portal_list_tenants($platformPdo) as $row) {
		if ((string) ($row['status'] ?? '') !== 'live') {
			continue;
		}
		$cred = function_exists('epc_portal_tenant_setup_credentials')
			? epc_portal_tenant_setup_credentials($row)
			: array('db' => (string) ($row['db_name'] ?? ''), 'user' => (string) ($row['db_user'] ?? ''), 'pass' => (string) ($row['db_password'] ?? ''));
		if ($cred['db'] === '') {
			continue;
		}
		$targets[] = array(
			'label' => (string) ($row['site_key'] ?? $cred['db']),
			'host' => (string) ($row['hostname'] ?? ''),
			'cred' => array('db' => $cred['db'], 'user' => $cred['user'], 'pass' => $cred['pass']),
		);
	}
}

$seenDb = array();
$unique = array();
foreach ($targets as $t) {
	$db = (string) ($t['cred']['db'] ?? '');
	if ($db === '' || isset($seenDb[$db])) {
		continue;
	}
	if ($onlyDb !== '' && $db !== $onlyDb) {
		continue;
	}
	$seenDb[$db] = true;
	$unique[] = $t;
}

function epc_social_setup_connect(array $cred, DP_Config $cfg): ?PDO
{
	$host = trim((string) $cfg->host);
	if ($host === '' || strtolower($host) === 'localhost') {
		$host = '127.0.0.1';
	}
	try {
		return new PDO(
			'mysql:host=' . $host . ';dbname=' . $cred['db'] . ';charset=utf8',
			$cred['user'] !== '' ? $cred['user'] : $cfg->user,
			$cred['pass'] !== '' ? $cred['pass'] : $cfg->password,
			array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION)
		);
	} catch (Throwable $e) {
		return null;
	}
}

echo "=== ECOM AE Social Media Setup All ===\n";
echo 'apply=' . ($apply ? 'yes' : 'no') . "\n";
echo 'targets=' . count($unique) . "\n\n";

foreach ($unique as $target) {
	$db = (string) ($target['cred']['db'] ?? '');
	echo "--- {$target['label']} ({$db}) ---\n";
	$pdo = epc_social_setup_connect($target['cred'], $cfg);
	if (!$pdo instanceof PDO) {
		echo "  CONNECT FAILED\n\n";
		continue;
	}
	epc_social_ensure_schema($pdo);
	echo "  schema: epc_social_accounts + epc_social_post_drafts OK\n";

	foreach ($pages as $page) {
		$php = __DIR__ . '/cp/content/control/portal/' . $page['slug'] . '.php';
		echo '  page ' . $page['slug'] . ': ' . (is_file($php) ? 'on disk' : 'MISSING') . "\n";
		if ($apply) {
			$cid = epc_integrations_register_cp_content($pdo, $page['slug'], $page['key'], $page['en'], $page['ru'], $page['slug'] . '.php', (int) $page['order']);
			echo '    content_id=' . $cid . "\n";
		}
	}

	if ($apply) {
		$menu = epc_cp_social_media_menu_apply($pdo);
		echo '  menu portal_group=' . (int) ($menu['portal_group'] ?? 0) . ' social_item=' . (int) ($menu['social_hub_item'] ?? 0) . "\n";
	}
	echo "\n";
}

if ($apply) {
	echo "Verify Super CP:\n";
	echo "  https://www.ecomae.com/cp/control/portal/epc_social_media_hub\n";
	echo "  https://www.ecomae.com/cp/shop/tenant_hub/tenant_hub?tab=social\n";
	echo "  Guide: https://www.ecomae.com/cp/control/portal/epc_social_media_hub?tab=guide\n";
	echo "Verify Tenant CP:\n";
	echo "  https://www.epartscart.com/cp/control/portal/epc_social_media_hub\n";
	echo "  Guide: https://www.epartscart.com/cp/control/portal/epc_social_media_hub?tab=guide\n";
	echo "Probe: https://www.ecomae.com/epc-social-media-verify.php?token=epartscart-deploy-2026\n";
}

echo "\nDone.\n";
