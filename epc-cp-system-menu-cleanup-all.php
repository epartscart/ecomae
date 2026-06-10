<?php
/**
 * Remove legacy SYSTEM sidebar items: About program, Docpart changes, Updates.
 * Super CP (ecomae) + all tenant CP DBs (docpart, etc.).
 *
 * Dry-run: https://www.ecomae.com/epc-cp-system-menu-cleanup-all.php?token=epartscart-deploy-2026
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
require_once __DIR__ . '/content/shop/pos/epc_pos_cp_install.php';
require_once __DIR__ . '/epc_cp_mainstream_menu.php';

$apply = !empty($_GET['apply']) && (string) $_GET['apply'] === '1';
$onlyDb = preg_replace('/[^a-z0-9_]/', '', strtolower((string) ($_GET['db'] ?? '')));

$cfg = new DP_Config();
epc_portal_apply_config($cfg);

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
		$cred = epc_portal_tenant_setup_credentials($row);
		if ($cred['db'] === '') {
			continue;
		}
		$targets[] = array(
			'label' => (string) ($row['site_key'] ?? $cred['db']),
			'host' => (string) ($row['hostname'] ?? ''),
			'cred' => array(
				'db' => $cred['db'],
				'user' => $cred['user'],
				'pass' => $cred['pass'],
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

echo "=== EPC SYSTEM menu cleanup (About program, Docpart changes, Updates) ===\n";
echo 'mode=' . ($apply ? 'apply' : 'dry-run') . "\n";
echo 'targets=' . count($unique) . "\n\n";

foreach ($unique as $t) {
	$label = (string) $t['label'];
	$db = (string) $t['cred']['db'];
	echo "=== {$label} (db={$db}) ===\n";

	$pdo = epc_pos_setup_connect($t['cred'], $cfg);
	if (!$pdo instanceof PDO) {
		echo "  ERROR: cannot connect\n\n";
		continue;
	}

	$preview = epc_cp_system_menu_cleanup_preview($pdo);
	if (!$apply) {
		echo '  would_remove=' . count($preview) . "\n";
		foreach ($preview as $row) {
			echo '    id=' . (int) $row['id'] . ' label=' . ($row['label'] !== '' ? $row['label'] : $row['caption']) . ' url=' . $row['url'] . "\n";
		}
		echo "\n";
		continue;
	}

	$result = epc_cp_system_menu_cleanup($pdo);
	echo '  removed=' . (int) $result['removed'] . "\n";
	foreach ($result['items'] as $row) {
		echo '    id=' . (int) $row['id'] . ' label=' . ($row['label'] !== '' ? $row['label'] : $row['caption']) . ' url=' . $row['url'] . "\n";
	}
	echo "\n";
}

echo "Done.\n";

/**
 * @return array<int, array{id:int,url:string,caption:string,label:string}>
 */
function epc_cp_system_menu_cleanup_preview(PDO $pdo): array
{
	$preview = array();
	$st = $pdo->query('SELECT `id`, `url`, `caption` FROM `control_items` ORDER BY `id` ASC');
	while ($row = $st->fetch(PDO::FETCH_ASSOC)) {
		$url = (string) ($row['url'] ?? '');
		$caption = (string) ($row['caption'] ?? '');
		$label = epc_cp_system_menu_item_label($pdo, $caption);
		if (!epc_cp_system_menu_item_hidden($url, $label)) {
			continue;
		}
		$preview[] = array(
			'id' => (int) $row['id'],
			'url' => $url,
			'caption' => $caption,
			'label' => $label,
		);
	}
	return $preview;
}
