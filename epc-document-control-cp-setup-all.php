<?php
/**
 * Document Control — register routes + menu on platform DB and every live tenant DB.
 *
 * Dry-run:  https://www.ecomae.com/epc-document-control-cp-setup-all.php?token=epartscart-deploy-2026
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
require_once __DIR__ . '/content/shop/document_control/epc_document_control_cp_install.php';

$apply = !empty($_GET['apply']) && (string) $_GET['apply'] === '1';
$onlyDb = preg_replace('/[^a-z0-9_]/', '', strtolower((string) ($_GET['db'] ?? '')));

$cfg = new DP_Config();
$backend = trim((string) $cfg->backend_dir, '/');
if ($backend === '') {
	$backend = 'cp';
}
$docRoot = $_SERVER['DOCUMENT_ROOT'];

function epc_dc_setup_connect(array $cred, DP_Config $cfg): ?PDO
{
	$db = trim((string) ($cred['db'] ?? ''));
	if ($db === '') {
		return null;
	}
	$user = trim((string) ($cred['user'] ?? ''));
	if ($user === '') {
		$user = (string) $cfg->user;
	}
	$pass = (string) ($cred['pass'] ?? '');
	if ($pass === '') {
		$pass = (string) $cfg->password;
	}
	try {
		$pdo = new PDO(
			'mysql:host=' . $cfg->host . ';dbname=' . $db . ';charset=utf8',
			$user,
			$pass,
			array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION)
		);
		return $pdo;
	} catch (Throwable $e) {
		return null;
	}
}

$targets = array();
$targets[] = array(
	'label' => 'current_config',
	'host' => function_exists('epc_portal_host') ? epc_portal_host() : (string) ($_SERVER['HTTP_HOST'] ?? ''),
	'cred' => array('db' => $cfg->db, 'user' => $cfg->user, 'pass' => $cfg->password),
);

if (function_exists('epc_portal_platform_pdo')) {
	$platformPdo = epc_portal_platform_pdo();
} else {
	$platformPdo = null;
	try {
		epc_portal_apply_config($cfg);
		$platformPdo = new PDO('mysql:host=' . $cfg->host . ';dbname=' . $cfg->db . ';charset=utf8', $cfg->user, $cfg->password, array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION));
	} catch (Throwable $e) {
		$platformPdo = null;
	}
}

if ($platformPdo instanceof PDO && function_exists('epc_portal_list_tenants')) {
	epc_portal_db_ensure($platformPdo);
	foreach (epc_portal_list_tenants($platformPdo) as $row) {
		if ((string) ($row['status'] ?? '') !== 'live') {
			continue;
		}
		$dbName = preg_replace('/[^a-z0-9_]/', '', strtolower((string) ($row['db_name'] ?? '')));
		if ($dbName === '') {
			continue;
		}
		$targets[] = array(
			'label' => (string) ($row['site_key'] ?? $dbName),
			'host' => (string) ($row['hostname'] ?? ''),
			'cred' => array(
				'db' => $dbName,
				'user' => (string) ($row['db_user'] ?? ''),
				'pass' => (string) ($row['db_password'] ?? ''),
			),
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

echo 'mode=' . ($apply ? 'apply' : 'dry-run') . "\n";
echo 'targets=' . count($unique) . "\n\n";

foreach ($unique as $t) {
	$label = (string) $t['label'];
	$db = (string) $t['cred']['db'];
	$host = (string) $t['host'];
	echo "=== {$label} (db={$db}) ===\n";
	$pdo = epc_dc_setup_connect($t['cred'], $cfg);
	if (!$pdo instanceof PDO) {
		echo "  ERROR: cannot connect\n\n";
		continue;
	}
	$probe = epc_document_control_cp_probe($pdo, $docRoot, $backend);
	$main = $probe['content']['shop/document_control/document_control'] ?? null;
	echo '  content_main=' . ($main ? ('id=' . $main['id'] . ' published=' . $main['published_flag']) : 'MISSING') . "\n";
	echo '  menu_items=' . count($probe['menu']) . ' files_main=' . (!empty($probe['files']['main']) ? 'yes' : 'no') . "\n";
	if ($host !== '') {
		echo '  url=' . epc_document_control_cp_public_url($host, $backend) . "\n";
	}
	if (!$apply) {
		echo "  (dry-run)\n\n";
		continue;
	}
	try {
		$result = epc_document_control_cp_install($pdo, $backend);
		echo '  OK content_id=' . $result['content_id'] . ' menu_item=' . $result['menu']['document_control_item'] . "\n\n";
	} catch (Throwable $e) {
		echo '  FAIL: ' . $e->getMessage() . "\n\n";
	}
}

echo "Done.\n";
