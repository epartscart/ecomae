<?php
/**
 * Verify storefront storage toggle (schema, CP rows, optional live storefront probe).
 * GET /epc-storage-toggle-verify.php?token=…&site_key=epartscart
 * Optional: &part=GUT21&storage_name=S-UAE (read-only — never mutates toggle state)
 */
declare(strict_types=1);

require_once __DIR__ . '/epc_deploy_auth.php';
epc_deploy_require_token();
header('Content-Type: application/json; charset=utf-8');

define('_ASTEXE_', 1);
require_once __DIR__ . '/config.php';
require_once __DIR__ . '/content/general_pages/epc_portal.php';
require_once __DIR__ . '/content/general_pages/epc_portal_db.php';
require_once __DIR__ . '/content/general_pages/epc_portal_tenant.php';
require_once __DIR__ . '/content/shop/price_engine/epc_auto_price_cp_install.php';
require_once __DIR__ . '/content/shop/docpart/epc_storefront_storage_flags.php';
require_once __DIR__ . '/content/shop/docpart/docpart_article_match.php';

$cfg = new DP_Config();
epc_portal_apply_config($cfg);

$siteKey = preg_replace('/[^a-z0-9_]/', '', strtolower(trim((string) ($_GET['site_key'] ?? 'epartscart'))));
$part = strtoupper(preg_replace('/[^A-Za-z0-9]/', '', (string) ($_GET['part'] ?? 'GUT21')));
$brand = trim((string) ($_GET['brand'] ?? 'GMB'));
$storageNeedle = trim((string) ($_GET['storage_name'] ?? 'S-UAE'));
// Read-only probe — legacy toggle_test/restore params are ignored (no DB writes).
$legacyToggleTest = !empty($_GET['toggle_test']) && (string) $_GET['toggle_test'] === '1';

$tenantHostMap = array(
	'epartscart' => 'www.epartscart.com',
	'electronicae' => 'www.electronicae.com',
	'taxofinca' => 'www.taxofinca.com',
	'stylenlook' => 'www.stylenlook.com',
	'thejewellerytrend' => 'www.thejewellerytrend.com',
);

$platformPdo = epc_portal_platform_pdo();
if (!$platformPdo instanceof PDO) {
	http_response_code(500);
	exit(json_encode(array('ok' => false, 'error' => 'platform_unavailable')));
}
epc_portal_db_ensure($platformPdo);

$row = null;
foreach (epc_portal_list_tenants($platformPdo) as $t) {
	if ((string) ($t['site_key'] ?? '') === $siteKey) {
		$row = $t;
		break;
	}
}
if (!$row) {
	http_response_code(404);
	exit(json_encode(array('ok' => false, 'error' => 'tenant_not_found', 'site_key' => $siteKey)));
}

$pdo = epc_auto_price_setup_connect(array(
	'db' => (string) ($row['db_name'] ?? ''),
	'user' => (string) ($row['db_user'] ?? ''),
	'pass' => (string) ($row['db_password'] ?? ''),
), $cfg);

if (!$pdo instanceof PDO) {
	http_response_code(500);
	exit(json_encode(array('ok' => false, 'error' => 'db_connect_failed')));
}

epc_ssf_ensure_schema($pdo);

$schema = array(
	'storage_column' => false,
	'price_column' => false,
	'audit_table' => false,
);
try {
	$schema['storage_column'] = (int) $pdo->query(
		"SELECT COUNT(*) FROM information_schema.COLUMNS WHERE TABLE_SCHEMA = DATABASE()
		 AND TABLE_NAME = 'shop_storages' AND COLUMN_NAME = 'storefront_temp_disabled'"
	)->fetchColumn() > 0;
	$schema['price_column'] = (int) $pdo->query(
		"SELECT COUNT(*) FROM information_schema.COLUMNS WHERE TABLE_SCHEMA = DATABASE()
		 AND TABLE_NAME = 'shop_docpart_prices' AND COLUMN_NAME = 'storefront_temp_disabled'"
	)->fetchColumn() > 0;
	$schema['audit_table'] = (int) $pdo->query(
		"SELECT COUNT(*) FROM information_schema.TABLES WHERE TABLE_SCHEMA = DATABASE()
		 AND TABLE_NAME = 'epc_storefront_storage_toggle_audit'"
	)->fetchColumn() > 0;
} catch (Throwable $e) {
	$schema['error'] = $e->getMessage();
}

$cpRows = epc_ssf_cp_list_rows($pdo);
$priceLists = array();
foreach ($cpRows as $r) {
	if (($r['entity_type'] ?? '') === 'storage' || ($r['entity_type'] ?? '') === 'price_list') {
		$priceLists[] = array(
			'entity_type' => $r['entity_type'],
			'id' => (int) ($r['entity_id'] ?? 0),
			'name' => (string) ($r['name'] ?? ''),
			'short_name' => (string) ($r['short_name'] ?? ''),
			'type' => (string) ($r['type_label'] ?? ''),
			'storefront_disabled' => !empty($r['storefront_disabled']),
		);
	}
}

function epc_ssf_verify_storefront_warehouses(PDO $pdo, string $part): array
{
	$art = docpart_normalize_article_for_price($part);
	$artExpr = docpart_sql_article_normalized_expr('d.`article`');
	$stmt = $pdo->prepare(
		"SELECT DISTINCT COALESCE(NULLIF(TRIM(s.`short_name`), ''), s.`name`, p.`name`) AS `warehouse_label`,
		        s.`id` AS `storage_id`, p.`id` AS `price_id`,
		        IFNULL(s.`storefront_temp_disabled`, 0) AS `storage_disabled`,
		        IFNULL(p.`storefront_temp_disabled`, 0) AS `price_disabled`
		 FROM `shop_docpart_prices_data` d
		 INNER JOIN `shop_docpart_prices` p ON p.`id` = d.`price_id`
		 LEFT JOIN `shop_storages` s ON s.`connection_options` LIKE CONCAT('%\"price_id\":', p.`id`, '%')
		 WHERE {$artExpr} = ?
		   AND IFNULL(d.`price`, 0) > 0
		 ORDER BY `warehouse_label`"
	);
	$stmt->execute(array($art));
	return $stmt->fetchAll(PDO::FETCH_ASSOC) ?: array();
}

function epc_ssf_verify_fetch_url(string $url): array
{
	if (function_exists('curl_init')) {
		$ch = curl_init($url);
		curl_setopt_array($ch, array(
			CURLOPT_RETURNTRANSFER => true,
			CURLOPT_FOLLOWLOCATION => true,
			CURLOPT_MAXREDIRS => 5,
			CURLOPT_TIMEOUT => 30,
			CURLOPT_SSL_VERIFYPEER => false,
			CURLOPT_SSL_VERIFYHOST => 0,
			CURLOPT_USERAGENT => 'EPC-Storage-Toggle-Verify/1.1',
		));
		$body = curl_exec($ch);
		$status = (int) curl_getinfo($ch, CURLINFO_HTTP_CODE);
		curl_close($ch);
		return array('status' => $status, 'body' => is_string($body) ? $body : '', 'url' => $url);
	}
	$ctx = stream_context_create(array(
		'http' => array('timeout' => 25, 'ignore_errors' => true, 'header' => "User-Agent: EPC-Storage-Toggle-Verify/1.1\r\n"),
		'ssl' => array('verify_peer' => false, 'verify_peer_name' => false),
	));
	$body = @file_get_contents($url, false, $ctx);
	$headers = isset($http_response_header) && is_array($http_response_header) ? $http_response_header : array();
	$status = 0;
	if (!empty($headers[0]) && preg_match('/\s(\d{3})\s/', $headers[0], $m)) {
		$status = (int) $m[1];
	}
	return array('status' => $status, 'body' => is_string($body) ? $body : '', 'url' => $url);
}

function epc_ssf_verify_filtered_warehouses(PDO $pdo, string $part): array
{
	$rows = epc_ssf_verify_storefront_warehouses($pdo, $part);
	$disabledPrices = epc_ssf_disabled_price_ids($pdo);
	$disabledStorages = epc_ssf_disabled_storage_ids($pdo);
	$out = array();
	foreach ($rows as $row) {
		$priceId = (int) ($row['price_id'] ?? 0);
		$storageId = (int) ($row['storage_id'] ?? 0);
		if ($priceId > 0 && isset($disabledPrices[$priceId])) {
			continue;
		}
		if ($storageId > 0 && isset($disabledStorages[$storageId])) {
			continue;
		}
		if ($priceId > 0 && epc_ssf_storage_disabled_by_price($pdo, $priceId)) {
			continue;
		}
		$out[] = $row;
	}
	return $out;
}

function epc_ssf_verify_page_storage_ids(string $html, int $targetStorageId): array
{
	$found = array();
	if ($targetStorageId <= 0 || $html === '') {
		return $found;
	}
	if (preg_match('/var epc_initial_price_bunch = (\{.*?\});/s', $html, $m)) {
		$json = json_decode($m[1], true);
		if (is_array($json) && !empty($json['Products']) && is_array($json['Products'])) {
			foreach ($json['Products'] as $prod) {
				if ((int) ($prod['storage_id'] ?? 0) === $targetStorageId) {
					$found[(int) $targetStorageId] = true;
					break;
				}
			}
		}
	}
	if (preg_match('/"storage_id"\s*:\s*' . $targetStorageId . '\b/', $html)) {
		$found[$targetStorageId] = true;
	}
	return array_keys($found);
}

function epc_ssf_verify_page_has_warehouse(string $html, string $needle): bool
{
	if ($needle === '') {
		return false;
	}
	return stripos($html, $needle) !== false;
}

function epc_ssf_verify_agent_chat(string $tenantHost, string $message): array
{
	if ($tenantHost === '' || trim($message) === '') {
		return array('ok' => false, 'message' => 'missing host or message');
	}
	$url = 'https://' . $tenantHost . '/api/epc_parts_agent.php';
	$ctx = stream_context_create(array(
		'http' => array(
			'method' => 'POST',
			'header' => "Content-Type: application/x-www-form-urlencoded\r\nUser-Agent: EPC-Storage-Toggle-Verify/1.0\r\n",
			'content' => http_build_query(array('action' => 'chat', 'message' => $message)),
			'timeout' => 35,
			'ignore_errors' => true,
		),
		'ssl' => array('verify_peer' => false, 'verify_peer_name' => false),
	));
	$body = @file_get_contents($url, false, $ctx);
	$parsed = is_string($body) ? json_decode($body, true) : null;
	if (!is_array($parsed) || empty($parsed['ok'])) {
		return array(
			'ok' => false,
			'message' => is_array($parsed) ? (string) ($parsed['message'] ?? 'agent_error') : 'invalid_json',
			'raw' => is_string($body) ? substr($body, 0, 400) : '',
		);
	}
	$replyText = (string) ($parsed['reply']['text'] ?? '');
	return array('ok' => true, 'reply_text' => $replyText, 'reply' => $parsed['reply'] ?? array());
}

function epc_ssf_verify_agent_reply_safe(string $text, string $hiddenWarehouse): array
{
	$issues = array();
	if ($hiddenWarehouse !== '' && stripos($text, $hiddenWarehouse) !== false) {
		$issues[] = 'mentions_hidden_warehouse';
	}
	foreach (array(
		'toggle', 'turned off', 'temporarily disabled', 'admin control', 'price list was',
		'hidden warehouse', 'storefront_temp_disabled', 'operator disabled',
	) as $phrase) {
		if (stripos($text, $phrase) !== false) {
			$issues[] = 'disclosure:' . $phrase;
		}
	}
	return array('ok' => $issues === array(), 'issues' => $issues);
}

$beforeWarehouses = epc_ssf_verify_storefront_warehouses($pdo, $part);
$targetStorage = null;
foreach ($cpRows as $r) {
	$name = (string) ($r['name'] ?? '');
	$short = (string) ($r['short_name'] ?? '');
	if (stripos($name, $storageNeedle) !== false || stripos($short, $storageNeedle) !== false) {
		$targetStorage = $r;
		break;
	}
}

$readOnlyCheck = null;
$storefrontProbe = null;

$tenantHost = (string) ($tenantHostMap[$siteKey] ?? ($row['hostname'] ?? ''));
$cpUrl = $tenantHost !== '' ? 'https://' . $tenantHost . '/' . trim((string) $cfg->backend_dir, '/') . '/shop/prices' : '';
$storefrontBrandSeg = rawurlencode($brand);
$storefrontPartSeg = rawurlencode($part);
$storefrontUrl = $tenantHost !== ''
	? 'https://' . $tenantHost . '/en/parts/' . $storefrontBrandSeg . '/' . $storefrontPartSeg
	: '';

if ($storefrontUrl !== '') {
	$probe = epc_ssf_verify_fetch_url($storefrontUrl);
	$storefrontProbe = array(
		'url' => $storefrontUrl,
		'http_status' => $probe['status'],
		'warehouse_rows_in_db' => $beforeWarehouses,
		'page_contains_target' => epc_ssf_verify_page_has_warehouse($probe['body'], $storageNeedle),
	);
}

if ($targetStorage) {
	$entityType = (string) ($targetStorage['entity_type'] ?? 'storage');
	$entityId = (int) ($targetStorage['entity_id'] ?? 0);
	$targetStorageId = ($entityType === 'storage') ? $entityId : 0;
	$isDisabled = !empty($targetStorage['storefront_disabled']);
	$allWarehouses = epc_ssf_verify_storefront_warehouses($pdo, $part);
	$filteredWarehouses = epc_ssf_verify_filtered_warehouses($pdo, $part);
	$allLabels = array_column($allWarehouses, 'warehouse_label');
	$filteredLabels = array_column($filteredWarehouses, 'warehouse_label');

	$targetInAll = false;
	$targetInFiltered = false;
	foreach ($allWarehouses as $wh) {
		$label = (string) ($wh['warehouse_label'] ?? '');
		if ($label !== '' && stripos($label, $storageNeedle) !== false) {
			$targetInAll = true;
			break;
		}
	}
	foreach ($filteredWarehouses as $wh) {
		$label = (string) ($wh['warehouse_label'] ?? '');
		if ($label !== '' && stripos($label, $storageNeedle) !== false) {
			$targetInFiltered = true;
			break;
		}
	}

	$storefrontPage = $storefrontUrl !== '' ? epc_ssf_verify_fetch_url($storefrontUrl) : array('status' => 0, 'body' => '');
	$pageStorageIds = epc_ssf_verify_page_storage_ids((string) ($storefrontPage['body'] ?? ''), $targetStorageId);
	$pageHasTarget = epc_ssf_verify_page_has_warehouse((string) ($storefrontPage['body'] ?? ''), $storageNeedle);

	$dbFilterOk = $isDisabled ? !$targetInFiltered : ($targetInAll ? $targetInFiltered : true);
	$pageOk = $isDisabled
		? (!$pageHasTarget && ($targetStorageId <= 0 || !in_array($targetStorageId, $pageStorageIds, true)))
		: ($targetInAll ? $pageHasTarget : true);

	$agentBrand = trim((string) ($_GET['brand'] ?? 'GMB'));
	$agentMessage = trim($agentBrand . ' ' . $part);
	$agentReply = epc_ssf_verify_agent_chat($tenantHost, $agentMessage);
	$agentSafety = array('ok' => false, 'issues' => array('agent_unreachable'));
	if (!empty($agentReply['ok'])) {
		$hiddenNeedle = $isDisabled ? $storageNeedle : '';
		$agentSafety = epc_ssf_verify_agent_reply_safe((string) ($agentReply['reply_text'] ?? ''), $hiddenNeedle);
	}

	$readOnlyCheck = array(
		'read_only' => true,
		'legacy_toggle_test_ignored' => $legacyToggleTest,
		'target' => array(
			'entity_type' => $entityType,
			'entity_id' => $entityId,
			'storage_id' => $targetStorageId,
			'name' => (string) ($targetStorage['name'] ?? ''),
			'short_name' => (string) ($targetStorage['short_name'] ?? ''),
			'storefront_disabled' => $isDisabled,
		),
		'db_all_warehouses' => $allLabels,
		'db_filtered_warehouses' => $filteredLabels,
		'target_in_all_db' => $targetInAll,
		'target_in_filtered_db' => $targetInFiltered,
		'db_filter_matches_state' => $dbFilterOk,
		'storefront_page' => array(
			'http_status' => $storefrontPage['status'] ?? 0,
			'page_contains_target' => $pageHasTarget,
			'target_storage_id_in_bunch' => $pageStorageIds,
			'page_matches_state' => $pageOk,
		),
		'agent_probe' => array(
			'message' => $agentMessage,
			'ok' => !empty($agentReply['ok']),
			'reply_excerpt' => !empty($agentReply['reply_text'])
				? mb_substr((string) $agentReply['reply_text'], 0, 220, 'UTF-8') : '',
			'disclosure_issues' => $agentSafety['issues'],
			'agent_safe' => $agentSafety['ok'],
		),
		'ok' => $dbFilterOk && $pageOk,
	);
}

$schemaOk = !empty($schema['storage_column']) && !empty($schema['price_column']) && !empty($schema['audit_table']);

echo json_encode(array(
	'ok' => $schemaOk,
	'site_key' => $siteKey,
	'db' => (string) ($row['db_name'] ?? ''),
	'cp_url' => $cpUrl,
	'schema' => $schema,
	'price_lists_and_warehouses' => $priceLists,
	'count' => count($priceLists),
	'part_probe' => array(
		'part' => $part,
		'storefront_url' => $storefrontUrl,
		'warehouses_in_db' => $beforeWarehouses,
	),
	'storefront_probe' => $storefrontProbe,
	'target_storage' => $targetStorage,
	'read_only_check' => $readOnlyCheck,
	'toggle_test' => null,
	'note' => 'Probes are read-only; toggle_test no longer mutates DB state.',
	'ui' => array(
		'prices_cp' => $cpUrl,
		'storefront_toggle_panel' => 'Shop → Prices (top card: Storefront availability)',
	),
), JSON_UNESCAPED_UNICODE | JSON_PRETTY_PRINT);
