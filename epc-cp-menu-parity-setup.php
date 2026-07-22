<?php
/**
 * Apply full CP menu parity (Customers & accounts + all registered packs)
 * on platform + commerce tenants.
 *
 * Dry-run:  …/epc-cp-menu-parity-setup.php?token=…
 * Apply:    …/epc-cp-menu-parity-setup.php?token=…&apply=1
 * One DB:   …&site_key=ecomae|epartscart|…
 *
 * New modules: register their epc_cp_*_menu_apply in epc_cp_menu_parity_registry().
 */
declare(strict_types=1);

require_once __DIR__ . '/epc_deploy_auth.php';
epc_deploy_require_token();
header('Content-Type: application/json; charset=utf-8');
set_time_limit(240);

$apply = !empty($_GET['apply']);
$onlySite = preg_replace('/[^a-z0-9_]/', '', strtolower(trim((string) ($_GET['site_key'] ?? ''))));

define('_ASTEXE_', 1);
require_once __DIR__ . '/config.php';
require_once __DIR__ . '/content/general_pages/epc_portal.php';
require_once __DIR__ . '/content/general_pages/epc_portal_tenant.php';
require_once __DIR__ . '/content/general_pages/epc_cp_common_parity.php';
require_once __DIR__ . '/epc_cp_mainstream_menu.php';
if (is_file(__DIR__ . '/content/general_pages/epc_perf_cache.php')) {
	require_once __DIR__ . '/content/general_pages/epc_perf_cache.php';
}

function epc_menu_parity_probe(PDO $pdo): array
{
	$need = array(
		'/<backend>/shop/customer_mgmt/customer_mgmt',
		'/<backend>/users/usermanager',
		'/<backend>/users/usergroups',
		'/<backend>/users/customer_approvals',
		'/<backend>/users/polya-registracii',
	);
	$found = array();
	$st = $pdo->prepare(
		'SELECT i.`url`, g.`caption` AS group_caption
		 FROM `control_items` i
		 LEFT JOIN `control_groups` g ON g.`id` = i.`items_group`
		 WHERE i.`url` = ? LIMIT 1'
	);
	foreach ($need as $url) {
		$st->execute(array($url));
		$row = $st->fetch(PDO::FETCH_ASSOC) ?: null;
		$found[$url] = $row
			? array('ok' => true, 'group' => (string) ($row['group_caption'] ?? ''))
			: array('ok' => false, 'group' => '');
	}
	$customersOk = true;
	foreach ($found as $meta) {
		if (empty($meta['ok']) || ($meta['group'] !== '' && $meta['group'] !== 'epc_cp_group_customers')) {
			$customersOk = false;
			break;
		}
	}
	return array(
		'items' => $found,
		'customers_group_ok' => $customersOk,
	);
}

$report = array(
	'ok' => true,
	'apply' => $apply,
	'registry' => array_keys(epc_cp_menu_parity_registry()),
	'targets' => array(),
);

foreach (epc_cp_common_parity_targets() as $siteKey => $meta) {
	if ($onlySite !== '' && $onlySite !== $siteKey) {
		continue;
	}
	$host = (string) $meta['host'];
	$row = array(
		'site_key' => $siteKey,
		'host' => $host,
		'role' => $meta['role'],
		'label' => $meta['label'],
		'ok' => false,
	);
	try {
		$_SERVER['HTTP_HOST'] = $host;
		$_SERVER['SERVER_NAME'] = $host;
		unset($GLOBALS['epc_portal_config_applied'], $GLOBALS['epc_db_link']);

		$cfg = new DP_Config();
		if (function_exists('epc_portal_apply_config')) {
			epc_portal_apply_config($cfg);
		}
		$pdo = new PDO(
			'mysql:host=' . $cfg->host . ';dbname=' . $cfg->db . ';charset=utf8mb4',
			$cfg->user,
			$cfg->password,
			array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION)
		);
		$row['db'] = $cfg->db;
		$row['before'] = epc_menu_parity_probe($pdo);
		if ($apply) {
			$row['result'] = epc_cp_menu_parity_apply($pdo);
			$row['after'] = epc_menu_parity_probe($pdo);
			$row['ok'] = !empty($row['result']['ok']) && !empty($row['after']['customers_group_ok']);
		} else {
			$row['action'] = 'would_apply';
			$row['ok'] = true;
		}
	} catch (Throwable $e) {
		$row['error'] = $e->getMessage();
		$row['ok'] = false;
		$report['ok'] = false;
	}
	$report['targets'][] = $row;
	if (empty($row['ok'])) {
		$report['ok'] = false;
	}
}

echo json_encode($report, JSON_PRETTY_PRINT | JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES);
