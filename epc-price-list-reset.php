<?php
/**
 * Reset price lists on docpart (epartscart commerce tenants) — keep only listed articles.
 * GET/POST: token=epartscart-deploy-2026&apply=1&keep_articles=c110J,DT068&host=www.epartscart.com
 */
declare(strict_types=1);

require_once __DIR__ . '/epc_deploy_auth.php';
header('Content-Type: application/json; charset=utf-8');

$token = (string) ($_GET['token'] ?? $_POST['token'] ?? '');
if ($token === '' || !hash_equals(epc_deploy_token(), $token)) {
	http_response_code(403);
	exit(json_encode(array('ok' => false, 'message' => 'Forbidden')));
}

define('_ASTEXE_', 1);
require_once __DIR__ . '/config.php';
require_once __DIR__ . '/content/general_pages/epc_portal.php';
require_once __DIR__ . '/content/shop/docpart/docpart_price_upload_history.php';

$apply = !empty($_GET['apply']) || !empty($_POST['apply']);
$host = trim((string) ($_GET['host'] ?? $_POST['host'] ?? 'www.epartscart.com'));
$keepRaw = trim((string) ($_GET['keep_articles'] ?? $_POST['keep_articles'] ?? 'c110J,DT068'));
$keepArticles = array_values(array_unique(array_filter(array_map(
	static function ($a) {
		return strtoupper(trim($a));
	},
	preg_split('/[\s,;]+/', $keepRaw) ?: array()
))));

if (count($keepArticles) === 0) {
	exit(json_encode(array('ok' => false, 'message' => 'keep_articles required')));
}

$DP_Config = new DP_Config();
if ($host !== '') {
	$_SERVER['HTTP_HOST'] = $host;
}
epc_portal_apply_config($DP_Config);

$targetDb = 'docpart';
if ((string) $DP_Config->db !== $targetDb) {
	$DP_Config->db = $targetDb;
	require_once __DIR__ . '/content/general_pages/epc_portal_tenant.php';
	$creds = epc_portal_resolve_tenant_db_credentials();
	if (!empty($creds['db'])) {
		$DP_Config->db = (string) $creds['db'];
	}
	if (!empty($creds['user'])) {
		$DP_Config->user = (string) $creds['user'];
	}
	if (isset($creds['password'])) {
		$DP_Config->password = (string) $creds['password'];
	}
}

$result = array(
	'ok' => true,
	'apply' => $apply,
	'host' => $host,
	'db' => $DP_Config->db,
	'keep_articles' => $keepArticles,
	'before' => array(),
	'after' => array(),
	'deleted' => array(),
	'remaining_price_lists' => array(),
	'warehouse_rows' => array(),
);

try {
	$pdo = new PDO(
		'mysql:host=' . $DP_Config->host . ';dbname=' . $DP_Config->db . ';charset=utf8',
		$DP_Config->user,
		$DP_Config->password,
		array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION)
	);
	$pdo->query('SET NAMES utf8');
} catch (Throwable $e) {
	http_response_code(500);
	exit(json_encode(array('ok' => false, 'message' => 'DB connect failed: ' . $e->getMessage())));
}

$tables = array(
	'shop_docpart_prices',
	'shop_docpart_prices_data',
	'epc_price_upload_history',
	'shop_docpart_pyprices_crontab',
	'shop_docpart_pyprices_crontab_prices',
);
foreach ($tables as $tbl) {
	try {
		$result['before'][$tbl] = (int) $pdo->query('SELECT COUNT(*) FROM `' . str_replace('`', '', $tbl) . '`')->fetchColumn();
	} catch (Throwable $e) {
		$result['before'][$tbl] = null;
	}
}

$inList = implode(',', array_map(array($pdo, 'quote'), $keepArticles));

try {
	$st = $pdo->query(
		"SELECT `article`, `storage`, COUNT(*) AS `rows`, GROUP_CONCAT(DISTINCT `price_id`) AS `price_ids`
		 FROM `shop_docpart_prices_data`
		 WHERE UPPER(`article`) IN ({$inList})
		 GROUP BY `article`, `storage`
		 ORDER BY `article`, `storage`"
	);
	$result['warehouse_rows'] = $st ? $st->fetchAll(PDO::FETCH_ASSOC) : array();
} catch (Throwable $e) {
	$result['warehouse_rows_error'] = $e->getMessage();
}

if (!$apply) {
	$result['message'] = 'Dry run — pass apply=1 to execute cleanup';
	echo json_encode($result, JSON_PRETTY_PRINT | JSON_UNESCAPED_UNICODE);
	exit;
}

$pdo->beginTransaction();
try {
	$delData = $pdo->exec(
		"DELETE FROM `shop_docpart_prices_data` WHERE UPPER(`article`) NOT IN ({$inList})"
	);
	$result['deleted']['shop_docpart_prices_data'] = (int) $delData;

	$keepPriceIds = array();
	$st = $pdo->query('SELECT DISTINCT `price_id` FROM `shop_docpart_prices_data`');
	while ($row = $st->fetch(PDO::FETCH_ASSOC)) {
		$keepPriceIds[] = (int) $row['price_id'];
	}
	if (count($keepPriceIds) === 0) {
		$keepPriceIds = array(0);
	}
	$idList = implode(',', array_map('intval', $keepPriceIds));

	$delPrices = $pdo->exec("DELETE FROM `shop_docpart_prices` WHERE `id` NOT IN ({$idList})");
	$result['deleted']['shop_docpart_prices'] = (int) $delPrices;

	try {
		$result['deleted']['shop_docpart_pyprices_crontab_prices'] = (int) $pdo->exec(
			"DELETE FROM `shop_docpart_pyprices_crontab_prices` WHERE `price_id` NOT IN ({$idList})"
		);
	} catch (Throwable $e) {
		$result['deleted']['shop_docpart_pyprices_crontab_prices'] = 0;
	}

	try {
		$result['deleted']['shop_docpart_pyprices_crontab_orphans'] = (int) $pdo->exec(
			'DELETE FROM `shop_docpart_pyprices_crontab`
			 WHERE (SELECT COUNT(*) FROM `shop_docpart_pyprices_crontab_prices` p WHERE p.`crontab_task_id` = `shop_docpart_pyprices_crontab`.`id`) = 0'
		);
		$pdo->exec('UPDATE `shop_docpart_pyprices_crontab` SET `active` = 0');
	} catch (Throwable $e) {
		$result['deleted']['shop_docpart_pyprices_crontab_orphans'] = 0;
	}

	try {
		$result['deleted']['epc_price_upload_history'] = (int) $pdo->exec('DELETE FROM `epc_price_upload_history`');
	} catch (Throwable $e) {
		epc_price_history_ensure_schema($pdo);
		$result['deleted']['epc_price_upload_history'] = (int) $pdo->exec('DELETE FROM `epc_price_upload_history`');
	}

	try {
		$result['deleted']['shop_docpart_prices_cron_executor_launches'] = (int) $pdo->exec(
			'DELETE FROM `shop_docpart_prices_cron_executor_launches`'
		);
	} catch (Throwable $e) {
		$result['deleted']['shop_docpart_prices_cron_executor_launches'] = 0;
	}

	$pdo->commit();
} catch (Throwable $e) {
	$pdo->rollBack();
	http_response_code(500);
	exit(json_encode(array('ok' => false, 'message' => $e->getMessage(), 'partial' => $result), JSON_PRETTY_PRINT | JSON_UNESCAPED_UNICODE));
}

foreach ($tables as $tbl) {
	try {
		$result['after'][$tbl] = (int) $pdo->query('SELECT COUNT(*) FROM `' . str_replace('`', '', $tbl) . '`')->fetchColumn();
	} catch (Throwable $e) {
		$result['after'][$tbl] = null;
	}
}

$lists = $pdo->query(
	'SELECT p.`id`, p.`name`, (SELECT COUNT(*) FROM `shop_docpart_prices_data` d WHERE d.`price_id` = p.`id`) AS `rows`
	 FROM `shop_docpart_prices` p ORDER BY p.`id`'
)->fetchAll(PDO::FETCH_ASSOC);
$result['remaining_price_lists'] = $lists;
$result['message'] = 'Price list reset complete — only ' . implode(', ', $keepArticles) . ' retained';

echo json_encode($result, JSON_PRETTY_PRINT | JSON_UNESCAPED_UNICODE);
