<?php
header('Content-Type: application/json; charset=utf-8');
header('X-Content-Type-Options: nosniff');

require_once __DIR__ . '/../content/shop/docpart/epc_crossbase_cache.php';

function epc_crossbase_json($payload, $status = 200)
{
    http_response_code((int)$status);
    echo json_encode($payload, JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES);
    exit;
}

$sample = isset($_GET['sample']) ? trim((string)$_GET['sample']) : 'C110J';
if ($sample === '') {
    $sample = 'C110J';
}

$url = 'https://crossbase.ru/cross/?q=' . rawurlencode($sample);
$started = microtime(true);
$body = '';
$statusCode = 0;
$error = '';
$usedStaleCache = false;

$cachedFresh = epc_crossbase_cache_read($sample, 6 * 3600, false);
$cachedStale = epc_crossbase_cache_read($sample, 0, true);

if (function_exists('curl_init')) {
    $ch = curl_init($url);
    curl_setopt_array($ch, array(
        CURLOPT_RETURNTRANSFER => true,
        CURLOPT_FOLLOWLOCATION => true,
        CURLOPT_CONNECTTIMEOUT => 6,
        CURLOPT_TIMEOUT => 12,
        CURLOPT_SSL_VERIFYPEER => false,
        CURLOPT_USERAGENT => 'ePartsCart CP crossbase status',
    ));
    $body = curl_exec($ch);
    $error = curl_error($ch);
    $statusCode = (int)curl_getinfo($ch, CURLINFO_HTTP_CODE);
    curl_close($ch);
} else {
    $context = stream_context_create(array('http' => array(
        'timeout' => 12,
        'header' => "User-Agent: ePartsCart CP crossbase status\r\n",
        'ignore_errors' => true,
    )));
    $body = @file_get_contents($url, false, $context);
    if (!empty($http_response_header)) {
        foreach ($http_response_header as $header) {
            if (preg_match('/^HTTP\/\S+\s+(\d+)/', $header, $match)) {
                $statusCode = (int)$match[1];
                break;
            }
        }
    }
}

if ((!is_string($body) || $body === '') && $cachedStale !== '') {
    $body = $cachedStale;
    $usedStaleCache = true;
    if ($statusCode <= 0) {
        $statusCode = 200;
    }
} elseif (is_string($body) && strlen($body) > 400) {
    epc_crossbase_cache_write($sample, $body);
}

$elapsedMs = (int)round((microtime(true) - $started) * 1000);
$body = is_string($body) ? $body : '';
$referencesTotal = null;
$rowsParsed = 0;

if ($body !== '') {
    if (preg_match('~существует.*?([0-9]+).*?замен~isu', $body, $match)) {
        $referencesTotal = (int)$match[1];
    }
    if (preg_match_all('~<tr>\s*<td[^>]*>\s*[0-9]+\s*</td>\s*<td[^>]*>\s*<a[^>]*href=["\']/cross/\?q=([^"\']+)["\']~isu', $body, $matches)) {
        $rowsParsed = count($matches[1]);
    }
}

$connected = ($statusCode >= 200 && $statusCode < 400 && $body !== '' && !$usedStaleCache);
$degradedViaCache = ($usedStaleCache && $rowsParsed > 0);
if ($degradedViaCache) {
    $connected = true;
}
$message = $connected
    ? ($degradedViaCache ? 'Connected via saved cross-reference cache (live crossbase.ru slow or unreachable)' : 'Connected')
    : ($usedStaleCache ? 'Using saved cross-reference cache (offline)' : ($error !== '' ? $error : 'Cross-reference lookup did not return usable data'));

$cacheStats = epc_crossbase_cache_stats();
$cpCrossRows = 0;
$localCrossesOn = false;
try {
    require_once __DIR__ . '/../config.php';
    $cfg = new DP_Config();
    $localCrossesOn = !empty($cfg->local_crosses);
    $db = new PDO('mysql:host=' . $cfg->host . ';dbname=' . $cfg->db . ';charset=utf8', $cfg->user, $cfg->password);
    $cpCrossRows = (int)$db->query('SELECT COUNT(*) FROM `shop_docpart_articles_analogs_list`;')->fetchColumn();
} catch (Throwable $e) {
}

$offlineReady = ($cacheStats['files_total'] > 0 || $cpCrossRows > 100);
$actionRequired = array();
if (!$connected && !$offlineReady) {
    $actionRequired[] = 'Run /epc-offline-resilience-warm.php while the cross-reference service is online, and sync CP crosses for popular parts.';
}
if (!$connected && $offlineReady) {
    $actionRequired[] = 'Cross-reference service offline — storefront uses saved HTML cache + local CP crosses when available.';
}
if ($localCrossesOn && $cpCrossRows < 500) {
    $actionRequired[] = 'Import more crosses in CP → Shop → Crosses (sync interchange for top sellers).';
}

epc_crossbase_json(array(
    'connected' => $connected,
    'status_code' => $statusCode,
    'message' => $message,
    'sample' => $sample,
    'references_total' => $referencesTotal,
    'rows_parsed' => $rowsParsed,
    'response_ms' => $elapsedMs,
    'last_checked' => time(),
    'used_stale_cache' => $usedStaleCache,
    'cache' => $cacheStats,
    'cp_cross_rows' => $cpCrossRows,
    'local_crosses_on' => $localCrossesOn,
    'offline_ready' => $offlineReady,
    'action_required' => $actionRequired,
));
