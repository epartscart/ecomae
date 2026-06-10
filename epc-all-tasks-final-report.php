<?php
/**
 * Final task matrix — runs session probes and returns PASS/FAIL/BLOCKED per task.
 * GET ?token=...
 */
declare(strict_types=1);

require_once __DIR__ . '/epc_deploy_auth.php';
epc_deploy_require_token();
header('Content-Type: application/json; charset=utf-8');
set_time_limit(300);

$token = epc_deploy_token();
$platform = 'https://www.ecomae.com';
$epHost = 'https://www.epartscart.com';

function epc_atfr_fetch(string $url, int $timeout = 120): array
{
	$ch = curl_init($url);
	curl_setopt_array($ch, array(
		CURLOPT_RETURNTRANSFER => true,
		CURLOPT_FOLLOWLOCATION => true,
		CURLOPT_SSL_VERIFYPEER => false,
		CURLOPT_TIMEOUT => $timeout,
	));
	$body = (string) curl_exec($ch);
	$http = (int) curl_getinfo($ch, CURLINFO_HTTP_CODE);
	curl_close($ch);
	$json = json_decode($body, true);
	return array('http' => $http, 'body' => $body, 'json' => is_array($json) ? $json : null);
}

function epc_atfr_status(array $r, string $mode = 'ok'): string
{
	if (($r['http'] ?? 0) === 0 || ($r['http'] ?? 0) >= 502) {
		return 'BLOCKED';
	}
	$j = $r['json'] ?? null;
	if ($mode === 'session') {
		return !empty($j['ok']) ? 'PASS' : 'FAIL';
	}
	if ($mode === 'audit') {
		if (!is_array($j)) {
			return 'FAIL';
		}
		$fail = (int) ($j['summary']['fail'] ?? $j['fail_count'] ?? -1);
		if ($fail === 0 || (!empty($j['ok']) && $fail < 0)) {
			return 'PASS';
		}
		return !empty($j['ok']) ? 'PASS' : 'FAIL';
	}
	if ($mode === 'orders_all') {
		if (!is_array($j) || empty($j['tenants'])) {
			return 'FAIL';
		}
		foreach ($j['tenants'] as $row) {
			if (empty($row['db_ok'])) {
				return 'FAIL';
			}
		}
		return !empty($j['ok']) ? 'PASS' : 'FAIL';
	}
	if ($mode === 'order_detail') {
		if (!is_array($j)) {
			return 'FAIL';
		}
		foreach ($j['orders'] ?? array() as $ord) {
			if (empty($ord['pass'])) {
				return 'FAIL';
			}
		}
		return !empty($j['ok']) ? 'PASS' : 'FAIL';
	}
	return !empty($j['ok']) ? 'PASS' : 'FAIL';
}

$t = urlencode($token);
$tasks = array();

$probes = array(
	array(
		'task' => 'cp_full_audit',
		'url' => $platform . '/epc-cp-full-audit.php?token=' . $t,
		'mode' => 'audit',
	),
	array(
		'task' => 'session_complete_verify',
		'url' => $platform . '/epc-session-complete-verify.php?token=' . $t,
		'mode' => 'session',
	),
	array(
		'task' => 'seo_google_readiness_epartscart',
		'url' => $epHost . '/epc-seo-google-readiness.php?token=' . $t . '&host=www.epartscart.com',
		'mode' => 'ok',
	),
	array(
		'task' => 'seo_google_readiness_ecomae',
		'url' => $platform . '/epc-seo-google-readiness.php?token=' . $t . '&host=www.ecomae.com',
		'mode' => 'ok',
	),
	array(
		'task' => 'seo_regional_verify_gcc_pk',
		'url' => $epHost . '/epc-seo-regional-verify.php?token=' . $t . '&host=www.epartscart.com',
		'mode' => 'ok',
	),
	array(
		'task' => 'erp_shell_verify',
		'url' => $platform . '/epc-erp-shell-verify.php?token=' . $t,
		'mode' => 'ok',
	),
	array(
		'task' => 'cp_orders_verify_all',
		'url' => $platform . '/epc-cp-orders-verify.php?token=' . $t . '&site_key=all',
		'mode' => 'orders_all',
	),
	array(
		'task' => 'cp_prices_verify',
		'url' => $platform . '/epc-cp-prices-verify.php?token=' . $t,
		'mode' => 'ok',
	),
	array(
		'task' => 'storage_toggle_verify_readonly',
		'url' => $epHost . '/epc-storage-toggle-verify.php?token=' . $t . '&site_key=epartscart&brand=GMB&part=GUT21&storage_name=S-UAE',
		'mode' => 'ok',
	),
	array(
		'task' => 'seo_index_verify_epartscart',
		'url' => $epHost . '/epc-seo-index-verify.php?token=' . $t . '&host=www.epartscart.com',
		'mode' => 'ok',
	),
	array(
		'task' => 'seo_index_verify_ecomae',
		'url' => $platform . '/epc-seo-index-verify.php?token=' . $t . '&host=www.ecomae.com',
		'mode' => 'ok',
	),
	array(
		'task' => 'storefront_price_guest_verify',
		'url' => $platform . '/epc-storefront-price-guest-verify.php?token=' . $t,
		'mode' => 'ok',
	),
	array(
		'task' => 'cp_order_detail_verify_16_18',
		'url' => $platform . '/epc-cp-order-detail-verify.php?token=' . $t . '&site_key=epartscart&order_id=16,18',
		'mode' => 'order_detail',
	),
);

$allPass = true;
foreach ($probes as $p) {
	$r = epc_atfr_fetch($p['url']);
	$status = epc_atfr_status($r, $p['mode']);
	if ($status !== 'PASS') {
		$allPass = false;
	}
	$issues = null;
	if (is_array($r['json'])) {
		$issues = $r['json']['issues'] ?? $r['json']['summary'] ?? null;
	}
	$tasks[] = array(
		'task' => $p['task'],
		'status' => $status,
		'url' => $p['url'],
		'http' => $r['http'],
		'probe_ok' => $r['json']['ok'] ?? null,
		'issues' => $issues,
	);
}

echo json_encode(array(
	'ok' => $allPass,
	'generated_at' => gmdate('c'),
	'tasks' => $tasks,
), JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES | JSON_PRETTY_PRINT);
