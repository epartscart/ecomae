<?php
/**
 * Fix APAI-synced shop_catalogue_categories missing display names (question-mark icons in catalog menu).
 * Run: https://www.epartscart.com/epc-epartscart-category-fix.php?token=epartscart-deploy-2026
 */
error_reporting(E_ALL);
ini_set('display_errors', 0);
header('Content-Type: text/plain; charset=utf-8');

require_once __DIR__ . '/epc_deploy_auth.php';
epc_deploy_require_token();

define('_ASTEXE_', 1);
require_once __DIR__ . '/config.php';

$cfg = new DP_Config();
$epcTenantHostDbFile = __DIR__ . '/config.tenant-host-db.php';
if (is_file($epcTenantHostDbFile)) {
	$epc_tenant_host_db = null;
	require $epcTenantHostDbFile;
	$host = strtolower((string) ($_SERVER['HTTP_HOST'] ?? 'www.epartscart.com'));
	if (strpos($host, ':') !== false) {
		$host = explode(':', $host, 2)[0];
	}
	if (isset($epc_tenant_host_db) && is_array($epc_tenant_host_db) && isset($epc_tenant_host_db[$host])) {
		foreach (array('db', 'user', 'password') as $epcTk) {
			if (!empty($epc_tenant_host_db[$host][$epcTk]) && property_exists($cfg, $epcTk)) {
				$cfg->$epcTk = $epc_tenant_host_db[$host][$epcTk];
			}
		}
	}
}

$pdo = new PDO('mysql:host=' . $cfg->host . ';dbname=' . $cfg->db . ';charset=utf8', $cfg->user, $cfg->password);
$pdo->setAttribute(PDO::ATTR_ERRMODE, PDO::ERRMODE_EXCEPTION);

require_once __DIR__ . '/content/general_pages/epc_perf_cache.php';
epc_perf_cache_bust_prefix('epc_cat_tree_json:');

$fixed = 0;
$hidden = 0;

$stmt = $pdo->query(
	'SELECT c.`id`, c.`alias`, c.`value`, c.`published_flag`, n.`name_en`
	 FROM `shop_catalogue_categories` c
	 LEFT JOIN `epc_taxonomy_category_map` m ON m.`category_id` = c.`id`
	 LEFT JOIN `epc_product_taxonomy_nodes` n ON n.`id` = m.`taxonomy_node_id`
	 WHERE c.`alias` LIKE \'apai-%\' OR c.`alias` LIKE \'apai_%\''
);
$upd = $pdo->prepare('UPDATE `shop_catalogue_categories` SET `value` = ?, `title_tag` = ?, `description_tag` = ?, `keywords_tag` = ?, `published_flag` = ? WHERE `id` = ?');

while ($row = $stmt->fetch(PDO::FETCH_ASSOC)) {
	$id = (int) ($row['id'] ?? 0);
	$current = trim((string) ($row['value'] ?? ''));
	$nameEn = trim((string) ($row['name_en'] ?? ''));
	$alias = (string) ($row['alias'] ?? '');
	$slug = preg_replace('/^apai-[a-z0-9_]+-/', '', $alias);
	$derived = ucwords(str_replace('-', ' ', $slug));
	$newName = $nameEn !== '' ? $nameEn : ($current !== '' && !preg_match('/^\?+$/u', $current) && !ctype_digit($current) ? $current : $derived);
	$newName = trim($newName);
	if ($newName === '' || preg_match('/^\?+$/u', $newName)) {
		$upd->execute(array('', '', '', '', 0, $id));
		$hidden++;
		continue;
	}
	if ($newName === $current) {
		continue;
	}
	$upd->execute(array($newName, $newName, $newName, $newName, 1, $id));
	$fixed++;
}

if (is_file(__DIR__ . '/content/shop/price_engine/epc_auto_price_categories.php')) {
	require_once __DIR__ . '/content/shop/price_engine/epc_auto_price_categories.php';
	epc_apai_refresh_category_counts($pdo);
}

echo "fixed_names={$fixed}\n";
echo "hidden_empty={$hidden}\n";
echo "cache_bust=epc_cat_tree_json\n";
echo "db=" . $cfg->db . "\n";
echo "OK\n";
