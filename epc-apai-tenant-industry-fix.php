<?php
/**
 * Auto Price AI — fix per-tenant industry config + reject cross-industry discovery queue rows.
 *
 * Dry-run: …/epc-apai-tenant-industry-fix.php?token=…
 * Apply:   …&apply=1&purge_queue=1
 */
declare(strict_types=1);

header('Content-Type: text/plain; charset=utf-8');
require_once __DIR__ . '/epc_deploy_auth.php';
if (($_GET['token'] ?? '') !== epc_deploy_token()) {
	exit("Forbidden\n");
}

define('_ASTEXE_', 1);
require_once __DIR__ . '/config.php';
require_once __DIR__ . '/content/shop/price_engine/epc_auto_price_engine.php';
require_once __DIR__ . '/content/shop/price_engine/epc_industry_taxonomy.php';

$apply = !empty($_GET['apply']) && (string) $_GET['apply'] === '1';
$purgeQueue = !empty($_GET['purge_queue']) && (string) $_GET['purge_queue'] === '1';
$onlySite = preg_replace('/[^a-z0-9_]/', '', strtolower(trim((string) ($_GET['site_key'] ?? ''))));

$presets = array(
	'epartscart' => array('industry_key' => 'auto_parts', 'profile' => 'warehouse_supplier'),
	'electronicae' => array('industry_key' => 'electronics', 'profile' => 'marketplace_arbitrage'),
	'stylenlook' => array('industry_key' => 'fashion', 'profile' => 'marketplace_arbitrage'),
	'thejewellerytrend' => array('industry_key' => 'jewellery', 'profile' => 'marketplace_arbitrage'),
	'taxofinca' => array('industry_key' => 'tax_advisory', 'profile' => 'professional_services'),
);

global $db_link;
$pdo = ($db_link instanceof PDO) ? $db_link : null;
if (!$pdo instanceof PDO) {
	$cfg = new DP_Config();
	$pdo = new PDO(
		'mysql:host=' . $cfg->host . ';dbname=' . $cfg->db . ';charset=utf8',
		$cfg->user,
		$cfg->password,
		array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION)
	);
}

epc_ape_ensure_schema($pdo);
$now = time();

echo "=== APAI tenant industry fix ===\n";
echo 'apply=' . ($apply ? 'yes' : 'no') . ' purge_queue=' . ($purgeQueue ? 'yes' : 'no') . "\n\n";

try {
foreach ($presets as $siteKey => $preset) {
	if ($onlySite !== '' && $siteKey !== $onlySite) {
		continue;
	}
	$industryKey = (string) $preset['industry_key'];
	$profile = (string) $preset['profile'];
	$resolved = epc_apai_resolve_industry($pdo, $siteKey);
	echo "--- {$siteKey} ---\n";
	echo "resolved_industry={$resolved} (expect {$industryKey})\n";

	$cfg = epc_ape_tenant_config_get($pdo, $siteKey);
	$config = (array) ($cfg['config'] ?? array());
	$config['industry_key'] = $industryKey;
	echo "profile: " . (string) ($cfg['profile'] ?? '') . " -> {$profile}\n";
	$cfgArr = (array) ($cfg['config'] ?? array());
	echo 'config_json industry_key: ' . (string) ($cfgArr['industry_key'] ?? '') . " -> {$industryKey}\n";

	if ($apply) {
		epc_ape_tenant_config_save($pdo, $siteKey, $profile, (string) ($cfg['currency'] ?? 'AED'));
		$pdo->prepare(
			'UPDATE `epc_auto_price_tenant_config` SET `config_json` = ?, `updated_at` = ? WHERE `site_key` = ?'
		)->execute(array(json_encode($config, JSON_UNESCAPED_UNICODE), $now, $siteKey));
	}

	$taxStmt = $pdo->prepare(
		'SELECT COUNT(*) FROM `epc_product_taxonomy_nodes` WHERE `active` = 1 AND `industry_key` = ?'
	);
	$taxStmt->execute(array($industryKey));
	$taxCount = (int) $taxStmt->fetchColumn();
	echo "taxonomy_nodes({$industryKey})={$taxCount}\n";

	if ($purgeQueue) {
		$badStmt = $pdo->prepare(
			'SELECT q.`id`, n.`industry_key`, n.`name_en`
			 FROM `epc_product_discovery_queue` q
			 INNER JOIN `epc_product_taxonomy_nodes` n ON n.`id` = q.`taxonomy_node_id`
			 WHERE q.`site_key` = ? AND q.`status` = \'suggested\' AND n.`industry_key` <> ?'
			 LIMIT 500'
		);
		$badStmt->execute(array($siteKey, $industryKey));
		$badRows = $badStmt->fetchAll(PDO::FETCH_ASSOC) ?: array();
		echo 'wrong_industry_queue_rows=' . count($badRows) . "\n";
		if ($apply && $badRows) {
			$rej = $pdo->prepare(
				'UPDATE `epc_product_discovery_queue` SET `status` = \'rejected\', `updated_at` = ? WHERE `id` = ? AND `site_key` = ?'
			);
			foreach ($badRows as $br) {
				$rej->execute(array($now, (int) $br['id'], $siteKey));
			}
		}
	}

	try {
		if (is_file(__DIR__ . '/content/shop/price_engine/epc_apai_product_line_rankings.php')) {
			require_once __DIR__ . '/content/shop/price_engine/epc_apai_product_line_rankings.php';
			$rank = epc_apai_product_line_rankings($pdo, $siteKey, $industryKey);
			$sample = array();
			foreach (array_slice((array) ($rank['top'] ?? array()), 0, 3) as $line) {
				$sample[] = (string) ($line['name_en'] ?? $line['slug'] ?? '');
			}
			echo 'top_lines: ' . implode(', ', $sample) . "\n";
		}
	} catch (Throwable $rankErr) {
		echo 'top_lines: (skipped) ' . $rankErr->getMessage() . "\n";
	}
	echo "\n";
}
} catch (Throwable $e) {
	echo 'ERROR: ' . $e->getMessage() . "\n";
	echo $e->getFile() . ':' . $e->getLine() . "\n";
}

echo "Done. Re-seed optional: /epc-auto-price-setup-all.php?token=…&apply=1&seed=1&db=docpart\n";
