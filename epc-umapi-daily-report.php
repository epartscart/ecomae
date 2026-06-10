<?php
/**
 * Epart catalog daily utilization report (1000 live calls/day limit).
 * https://www.epartscart.com/epc-umapi-daily-report.php?token=epartscart-deploy-2026&key=TECH_KEY
 * Optional: &days=7&format=json&live_only=1
 */
header('Content-Type: text/plain; charset=utf-8');

if (($_GET['token'] ?? '') !== 'epartscart-deploy-2026') {
	exit("Forbidden\n");
}

require_once __DIR__ . '/config.php';
$DP_Config = new DP_Config;
if ((string)($_GET['key'] ?? '') !== $DP_Config->tech_key) {
	exit("Invalid key\n");
}

define('EPC_UMAPI_LIB_ONLY', true);
require_once __DIR__ . '/api/umapi_proxy.php';

$days = max(1, min(30, (int)($_GET['days'] ?? 7)));
$liveOnly = !empty($_GET['live_only']);
$report = epc_umapi_usage_report($days);
$recent = epc_umapi_recent_events(100, $liveOnly);

if (($_GET['format'] ?? '') === 'json') {
	header('Content-Type: application/json; charset=utf-8');
	echo json_encode(array(
		'usage' => $report['usage'],
		'recent_today' => $recent,
	), JSON_PRETTY_PRINT | JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES);
	exit;
}

echo $report['text'];
echo "\nWhere the Epart catalog API is utilized (via /api/umapi_proxy.php):\n";
echo "  warm_script     — epc-offline-resilience-warm.php (manufacturers, suppliers, VIN)\n";
echo "  catalog_ui      — Epart catalog / vehicle catalog pages\n";
echo "  part_search     — part search, fitment widget\n";
echo "  demand_intel    — demand intelligence pages\n";
echo "  parts_agent     — AI parts agent\n";
echo "  cp              — Control panel configuration status\n";
echo "  probe           — offline-resilience / performance probes\n";
echo "  frontend        — other site requests\n";
echo "\nRecent events today" . ($liveOnly ? ' (live only)' : '') . ":\n";
if (!$recent) {
	echo "  (none logged yet — logging starts after this deploy)\n";
} else {
	foreach ($recent as $row) {
		$flags = array();
		if (!empty($row['is_live'])) {
			$flags[] = 'live';
		}
		if (!empty($row['from_cache'])) {
			$flags[] = 'cache';
		}
		if (!empty($row['quota_blocked'])) {
			$flags[] = 'blocked';
		}
		echo '  ' . $row['time'] . ' | ' . $row['action'] . ' | ' . $row['source']
			. ' | HTTP ' . $row['http_status'] . ' | ' . implode(',', $flags);
		if ($row['path'] !== '') {
			echo ' | ' . $row['path'];
		}
		if ($row['message'] !== '') {
			echo ' | ' . $row['message'];
		}
		echo "\n";
	}
}
