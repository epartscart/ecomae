<?php
/**
 * Warm UMAPI + Crossbase offline caches (run while APIs are online, or via cron).
 * https://www.epartscart.com/epc-offline-resilience-warm.php?token=epartscart-deploy-2026&key=TECH_KEY
 * Optional: &cross_limit=80&umapi=1&crossbase=1&vin=1&vin_limit=150
 */
header('Content-Type: text/plain; charset=utf-8');
@set_time_limit(600);
@ini_set('memory_limit', '512M');

if (($_GET['token'] ?? '') !== 'epartscart-deploy-2026') {
	exit("Forbidden\n");
}

require_once __DIR__ . '/config.php';
$DP_Config = new DP_Config;
if ((string)($_GET['key'] ?? '') !== $DP_Config->tech_key) {
	exit("Invalid key\n");
}

define('_ASTEXE_', 1);
require_once __DIR__ . '/content/shop/docpart/epc_crossbase_cache.php';

$host = isset($_SERVER['HTTP_HOST']) ? $_SERVER['HTTP_HOST'] : 'www.epartscart.com';
$base = 'https://' . $host;
$doUmapi = !isset($_GET['umapi']) || (string)$_GET['umapi'] !== '0';
$doCross = !isset($_GET['crossbase']) || (string)$_GET['crossbase'] !== '0';
$doVin = !isset($_GET['vin']) || (string)$_GET['vin'] !== '0';
$crossLimit = max(10, min(300, (int)($_GET['cross_limit'] ?? 60)));
$vinLimit = max(5, min(500, (int)($_GET['vin_limit'] ?? 150)));
$forceVin = !empty($_GET['force_vin']);

function epc_warm_normalize_vin($value)
{
	return strtoupper(preg_replace('/[^A-Z0-9]/', '', (string)$value));
}

function epc_warm_vin_valid($vin)
{
	$len = strlen($vin);
	return ($len >= 11 && $len <= 17);
}

function epc_warm_fetch($url, $timeout = 45)
{
	if (!function_exists('curl_init')) {
		return '';
	}
	$ch = curl_init($url);
	curl_setopt_array($ch, array(
		CURLOPT_RETURNTRANSFER => true,
		CURLOPT_FOLLOWLOCATION => true,
		CURLOPT_CONNECTTIMEOUT => 10,
		CURLOPT_TIMEOUT => $timeout,
		CURLOPT_SSL_VERIFYPEER => false,
		CURLOPT_USERAGENT => 'ePartsCart offline warm',
	));
	$body = curl_exec($ch);
	curl_close($ch);
	return is_string($body) ? $body : '';
}

echo "Offline resilience warm — {$host}\n\n";

if ($doUmapi) {
	echo "Epart catalog warm:\n";
	$statusJson = epc_warm_fetch($base . '/api/umapi_proxy.php?action=status', 20);
	$statusData = json_decode($statusJson, true);
	$usage = is_array($statusData) && isset($statusData['usage']) ? $statusData['usage'] : array();
	$todayLive = (int)($usage['today_live'] ?? 0);
	$dailyLimit = (int)($usage['daily_limit'] ?? 1000);
	if ($todayLive >= $dailyLimit) {
		echo "  SKIP: daily live limit reached ({$todayLive}/{$dailyLimit}). Warm when quota resets.\n\n";
		$doUmapi = false;
	} elseif ($todayLive > 0) {
		echo "  Usage today: {$todayLive}/{$dailyLimit} live calls\n";
	}
}

if ($doUmapi) {
	$src = '&source=warm_script';
	$sections = array(
		array('section' => 'passenger', 'vehicle_type' => 'PC'),
		array('section' => 'commercial', 'vehicle_type' => 'CV'),
		array('section' => 'motorbike', 'vehicle_type' => 'Motorcycle'),
	);
	foreach ($sections as $spec) {
		$url = $base . '/api/umapi_proxy.php?action=manufacturers&section=' . rawurlencode($spec['section'])
			. '&vehicle_type=' . rawurlencode($spec['vehicle_type']) . '&language=en&region=WWW' . $src;
		$json = epc_warm_fetch($url, 60);
		$data = json_decode($json, true);
		$rows = is_array($data) && isset($data['data']) && is_array($data['data']) ? count($data['data']) : 0;
		$source = is_array($data) && isset($data['source']) ? $data['source'] : 'live';
		echo '  manufacturers/' . $spec['section'] . ': ' . $rows . ' rows (' . $source . ")\n";
	}
	$url = $base . '/api/umapi_proxy.php?action=suppliers&refresh=1&language=en&region=WWW&limit=500&offset=0' . $src;
	$json = epc_warm_fetch($url, 60);
	$data = json_decode($json, true);
	$rows = is_array($data) && isset($data['data']) && is_array($data['data']) ? count($data['data']) : 0;
	echo '  suppliers: ' . $rows . " rows\n";
	$status = epc_warm_fetch($base . '/api/umapi_proxy.php?action=status', 20);
	echo '  status saved: ' . (strlen($status) > 10 ? 'yes' : 'no') . "\n\n";
}

if ($doVin) {
	echo "Epart catalog VIN decode warm (limit {$vinLimit}):\n";
	$statusJson = epc_warm_fetch($base . '/api/umapi_proxy.php?action=status', 20);
	$statusData = json_decode($statusJson, true);
	$usage = is_array($statusData) && isset($statusData['usage']) ? $statusData['usage'] : array();
	$todayLive = (int)($usage['today_live'] ?? 0);
	$dailyLimit = (int)($usage['daily_limit'] ?? 1000);
	if ($todayLive >= $dailyLimit) {
		echo "  SKIP: daily live limit reached ({$todayLive}/{$dailyLimit}).\n\n";
		$doVin = false;
	}
}

if ($doVin) {
	$vins = array('WBAXG1103CDW29096', 'JHMCM56557C404453', 'WAUZZZ4G6DN123456', 'JTDBR32E030123456');
	$db = null;
	try {
		$db = new PDO('mysql:host=' . $DP_Config->host . ';dbname=' . $DP_Config->db . ';charset=utf8', $DP_Config->user, $DP_Config->password);
		$db->setAttribute(PDO::ATTR_ERRMODE, PDO::ERRMODE_EXCEPTION);
		epc_warm_fetch($base . '/api/umapi_proxy.php?action=status', 20);
		$gq = $db->query(
			"SELECT DISTINCT UPPER(TRIM(`vin`)) AS `vin` FROM `shop_docpart_garage`
			 WHERE TRIM(IFNULL(`vin`, '')) != '' AND CHAR_LENGTH(TRIM(`vin`)) BETWEEN 11 AND 17
			 ORDER BY `id` DESC LIMIT 300"
		);
		while ($row = $gq->fetch(PDO::FETCH_ASSOC)) {
			$vins[] = (string)$row['vin'];
		}
		$fq = $db->query(
			"SELECT DISTINCT UPPER(TRIM(`frame`)) AS `vin` FROM `shop_docpart_garage`
			 WHERE TRIM(IFNULL(`frame`, '')) != '' AND CHAR_LENGTH(TRIM(`frame`)) BETWEEN 11 AND 17
			 ORDER BY `id` DESC LIMIT 200"
		);
		while ($row = $fq->fetch(PDO::FETCH_ASSOC)) {
			$vins[] = (string)$row['vin'];
		}
	} catch (Throwable $e) {
		echo '  VIN source DB skipped: ' . $e->getMessage() . "\n";
	}
	$seen = array();
	$queue = array();
	foreach ($vins as $rawVin) {
		$vin = epc_warm_normalize_vin($rawVin);
		if (!epc_warm_vin_valid($vin) || isset($seen[$vin])) {
			continue;
		}
		$seen[$vin] = true;
		$queue[] = $vin;
	}
	$queue = array_slice($queue, 0, $vinLimit);
	$fetched = 0;
	$skipped = 0;
	$failed = 0;
	$quotaHit = false;
	foreach ($queue as $i => $vin) {
		if ($quotaHit) {
			break;
		}
		if ($db && !$forceVin) {
			try {
				$cq = $db->prepare('SELECT `vehicle_count`, `updated_at` FROM `epc_umapi_vin_cache` WHERE `vin` = ? AND `language` = ? AND `region` = ? LIMIT 1');
				$cq->execute(array($vin, 'en', 'WWW'));
				$cached = $cq->fetch(PDO::FETCH_ASSOC);
				if ($cached && (int)$cached['vehicle_count'] > 0 && (time() - (int)$cached['updated_at']) < 30 * 86400) {
					$skipped++;
					continue;
				}
			} catch (Throwable $e) {
			}
		}
		$url = $base . '/api/umapi_proxy.php?action=vin&refresh=1&vin=' . rawurlencode($vin) . '&language=en&region=WWW&source=warm_script';
		$json = epc_warm_fetch($url, 45);
		$data = json_decode($json, true);
		if (!is_array($data)) {
			$failed++;
			continue;
		}
		if ((int)($data['statusCode'] ?? 0) === 402 || stripos((string)($data['message'] ?? ''), 'payment required') !== false) {
			echo "  quota exceeded — stopping VIN warm at {$vin}\n";
			$quotaHit = true;
			break;
		}
		$vehicles = 0;
		if (isset($data['data']['matchingVehicles']) && is_array($data['data']['matchingVehicles'])) {
			$vehicles = count($data['data']['matchingVehicles']);
		} elseif (isset($data['matchingVehicles']) && is_array($data['matchingVehicles'])) {
			$vehicles = count($data['matchingVehicles']);
		}
		if ($vehicles > 0) {
			$fetched++;
			echo '  saved ' . $vin . ' (' . $vehicles . " vehicle(s))\n";
		} else {
			$failed++;
		}
		if (($i + 1) % 25 === 0) {
			echo '  progress ' . ($i + 1) . '/' . count($queue) . "\n";
		}
		usleep(150000);
	}
	$savedTotal = 0;
	if ($db) {
		try {
			$savedTotal = (int)$db->query('SELECT COUNT(*) FROM `epc_umapi_vin_cache` WHERE `vehicle_count` > 0')->fetchColumn();
		} catch (Throwable $e) {
		}
	}
	echo "  newly fetched: {$fetched}, skipped fresh cache: {$skipped}, no match/fail: {$failed}\n";
	echo "  total VINs saved: {$savedTotal}\n\n";
}

if ($doCross) {
	echo "Cross-reference HTML warm (limit {$crossLimit}):\n";
	$articles = array('C110J', '90915YZZD4', '15400PLMA02', '2630035504');
	try {
		$db = new PDO('mysql:host=' . $DP_Config->host . ';dbname=' . $DP_Config->db . ';charset=utf8', $DP_Config->user, $DP_Config->password);
		$q = $db->query(
			"SELECT COALESCE(NULLIF(`article_show`, ''), `article`) AS `article`, MAX(IFNULL(`exist`, 0)) AS `mx`
			 FROM `shop_docpart_prices_data`
			 WHERE IFNULL(`exist`,0) > 0 AND IFNULL(`price`,0) > 0 AND TRIM(IFNULL(`article`, '')) != ''
			 GROUP BY COALESCE(NULLIF(`article_show`, ''), `article`)
			 ORDER BY `mx` DESC
			 LIMIT " . (int)$crossLimit
		);
		while ($row = $q->fetch(PDO::FETCH_ASSOC)) {
			$a = trim((string)$row['article']);
			if ($a !== '') {
				$articles[] = $a;
			}
		}
	} catch (Throwable $e) {
		echo '  DB sample list skipped: ' . $e->getMessage() . "\n";
	}
	$articles = array_values(array_unique($articles));
	$articles = array_slice($articles, 0, $crossLimit);
	$ok = 0;
	$stale = 0;
	foreach ($articles as $i => $article) {
		if (epc_crossbase_cache_read($article, 6 * 3600, false) !== '') {
			$stale++;
			continue;
		}
		$html = epc_warm_fetch('https://crossbase.ru/cross/?q=' . rawurlencode($article), 18);
		if ($html !== '' && strlen($html) > 400) {
			epc_crossbase_cache_write($article, $html);
			$ok++;
		}
		if (($i + 1) % 20 === 0) {
			echo '  progress ' . ($i + 1) . '/' . count($articles) . "\n";
		}
	}
	$stats = epc_crossbase_cache_stats();
	echo "  newly cached: {$ok}, already fresh: {$stale}\n";
	echo '  cache files total: ' . $stats['files_total'] . ' (fresh ' . $stats['files_fresh'] . ', stale ' . $stats['files_stale'] . ")\n\n";
}

echo "Done. Schedule this URL daily (cron) while APIs are reachable.\n";
