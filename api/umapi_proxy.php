<?php
header('Content-Type: application/json; charset=utf-8');
header('X-Content-Type-Options: nosniff');

function epc_json_response($payload, $status = 200, $cacheSeconds = 0)
{
    http_response_code((int)$status);
    if ((int)$cacheSeconds > 0) {
        header('Cache-Control: public, max-age=' . (int)$cacheSeconds . ', stale-while-revalidate=86400');
    } else {
        header('Cache-Control: no-cache, must-revalidate');
    }
    echo json_encode($payload, JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES);
    exit;
}

function epc_request_value($key, $default = '')
{
    return isset($_REQUEST[$key]) ? trim((string)$_REQUEST[$key]) : $default;
}

function epc_clean_code($value, $default)
{
    $value = strtoupper(preg_replace('/[^A-Z0-9]/', '', (string)$value));
    return $value !== '' ? $value : $default;
}

function epc_vehicle_type()
{
    $type = epc_request_value('vehicle_type');
    if ($type !== '') {
        if ($type === 'Engine') {
            return 'Engine';
        }
        if ($type === 'CV' || $type === 'Bus' || $type === 'E-Bus' || $type === 'Tractor') {
            return 'CV';
        }
        if ($type === 'Motorcycle' || $type === 'E-Motorcycle') {
            return 'Motorcycle';
        }
        return 'PC';
    }

    $section = strtolower(epc_request_value('section', 'passenger'));
    if ($section === 'commercial') {
        return 'CV';
    }
    if ($section === 'motorbike') {
        return 'Motorcycle';
    }
    return 'PC';
}

function epc_vehicle_type_list()
{
    $type = epc_vehicle_type();
    if ($type === 'CV') {
        return array('CV', 'Bus', 'E-Bus', 'Tractor');
    }
    if ($type === 'Motorcycle') {
        return array('Motorcycle', 'E-Motorcycle');
    }
    return array('PC', 'E-PC', 'LCV', 'E-LCV');
}

function epc_section()
{
    $section = strtolower(epc_request_value('section', 'passenger'));
    return in_array($section, array('passenger', 'commercial', 'motorbike'), true) ? $section : 'passenger';
}

function epc_config_key()
{
    $key = '';
    $configPath = dirname(__DIR__) . '/config.php';
    if (is_file($configPath)) {
        require_once $configPath;
        if (class_exists('DP_Config')) {
            $cfg = new DP_Config();
            if (!empty($cfg->umapi_api_key)) {
                $key = (string)$cfg->umapi_api_key;
            } elseif (!empty($cfg->umapi_api_url)) {
                $key = (string)$cfg->umapi_api_url;
            }
        }
    }

    if (strpos($key, '/') !== false) {
        $parts = explode('/', rtrim($key, '/'));
        $key = end($parts);
    }

    $key = trim($key);
    if ($key === '') {
        $key = '2da16082-e7bc-4bd9-bee2-62b38c79ad8b';
    }
    return $key;
}

function epc_config()
{
    static $cfg = null;
    if ($cfg !== null) {
        return $cfg;
    }
    $configPath = dirname(__DIR__) . '/config.php';
    if (is_file($configPath)) {
        require_once $configPath;
        if (class_exists('DP_Config')) {
            $cfg = new DP_Config();
            return $cfg;
        }
    }
    return null;
}

function epc_db()
{
    static $db = false;
    if ($db !== false) {
        return $db;
    }
    $cfg = epc_config();
    if (!$cfg || empty($cfg->host) || empty($cfg->db)) {
        $db = null;
        return null;
    }
    try {
        $db = new PDO(
            'mysql:host=' . $cfg->host . ';dbname=' . $cfg->db . ';charset=utf8',
            (string)$cfg->user,
            (string)$cfg->password,
            array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION, PDO::ATTR_DEFAULT_FETCH_MODE => PDO::FETCH_ASSOC)
        );
    } catch (Exception $e) {
        $db = null;
    }
    return $db;
}

function epc_ensure_cache_tables()
{
    $db = epc_db();
    if (!$db) {
        return false;
    }
    try {
        $db->exec("CREATE TABLE IF NOT EXISTS `epc_umapi_cache` (
            `cache_key` varchar(190) NOT NULL,
            `action` varchar(40) NOT NULL,
            `section` varchar(20) NOT NULL,
            `language` varchar(10) NOT NULL,
            `region` varchar(20) NOT NULL,
            `request_json` text NULL,
            `response_json` mediumtext NOT NULL,
            `rows_count` int NOT NULL DEFAULT 0,
            `http_status` int NOT NULL DEFAULT 200,
            `last_sync` int NOT NULL DEFAULT 0,
            PRIMARY KEY (`cache_key`),
            KEY `action_section` (`action`, `section`),
            KEY `last_sync` (`last_sync`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8;");
        $db->exec("CREATE TABLE IF NOT EXISTS `epc_umapi_manufacturers` (
            `section` varchar(20) NOT NULL,
            `mfa_id` int NOT NULL,
            `manufacturer` varchar(255) NOT NULL,
            `manufacturer_ru` varchar(255) NULL,
            `type` varchar(255) NULL,
            `country` varchar(120) NULL,
            `popular` tinyint NOT NULL DEFAULT 0,
            `is_logo` tinyint NOT NULL DEFAULT 0,
            `raw_json` text NULL,
            `updated_at` int NOT NULL DEFAULT 0,
            PRIMARY KEY (`section`, `mfa_id`),
            KEY `manufacturer` (`manufacturer`),
            KEY `popular` (`popular`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8;");
        $db->exec("CREATE TABLE IF NOT EXISTS `epc_umapi_models` (
            `section` varchar(20) NOT NULL,
            `mfa_id` int NOT NULL,
            `ms_id` int NOT NULL,
            `model_series` varchar(255) NOT NULL,
            `year_from` varchar(20) NULL,
            `year_to` varchar(20) NULL,
            `raw_json` text NULL,
            `updated_at` int NOT NULL DEFAULT 0,
            PRIMARY KEY (`section`, `ms_id`),
            KEY `mfa_id` (`mfa_id`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8;");
        $db->exec("CREATE TABLE IF NOT EXISTS `epc_umapi_modifications` (
            `section` varchar(20) NOT NULL,
            `ms_id` int NOT NULL,
            `modification_id` int NOT NULL,
            `title` varchar(255) NOT NULL,
            `year_from` varchar(20) NULL,
            `year_to` varchar(20) NULL,
            `power_kw` varchar(50) NULL,
            `capacity_lt` varchar(50) NULL,
            `fuel_type` varchar(120) NULL,
            `raw_json` text NULL,
            `updated_at` int NOT NULL DEFAULT 0,
            PRIMARY KEY (`section`, `modification_id`),
            KEY `ms_id` (`ms_id`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8;");
        $db->exec("CREATE TABLE IF NOT EXISTS `epc_umapi_brands` (
            `sup_id` int NOT NULL,
            `brand` varchar(255) NOT NULL,
            `full_name` varchar(255) NULL,
            `raw_json` text NULL,
            `updated_at` int NOT NULL DEFAULT 0,
            PRIMARY KEY (`sup_id`),
            KEY `brand` (`brand`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8;");
        $db->exec("CREATE TABLE IF NOT EXISTS `epc_umapi_sync_status` (
            `id` tinyint NOT NULL,
            `connected` tinyint NOT NULL DEFAULT 0,
            `status_code` int NOT NULL DEFAULT 0,
            `message` varchar(255) NULL,
            `last_checked` int NOT NULL DEFAULT 0,
            `last_success` int NOT NULL DEFAULT 0,
            `last_error` int NOT NULL DEFAULT 0,
            `key_hash` varchar(64) NULL,
            PRIMARY KEY (`id`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8;");
        $db->exec("CREATE TABLE IF NOT EXISTS `epc_umapi_vin_cache` (
            `vin` varchar(17) NOT NULL,
            `language` varchar(10) NOT NULL DEFAULT 'en',
            `region` varchar(20) NOT NULL DEFAULT 'WWW',
            `response_json` mediumtext NOT NULL,
            `vehicle_count` int NOT NULL DEFAULT 0,
            `manufacturer` varchar(255) NULL,
            `model_label` varchar(255) NULL,
            `http_status` int NOT NULL DEFAULT 200,
            `updated_at` int NOT NULL DEFAULT 0,
            PRIMARY KEY (`vin`, `language`, `region`),
            KEY `updated_at` (`updated_at`),
            KEY `vehicle_count` (`vehicle_count`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8;");
        $db->exec("CREATE TABLE IF NOT EXISTS `epc_umapi_usage_log` (
            `id` bigint NOT NULL AUTO_INCREMENT,
            `usage_date` date NOT NULL,
            `created_at` int NOT NULL DEFAULT 0,
            `action` varchar(40) NOT NULL,
            `section` varchar(20) NOT NULL DEFAULT '',
            `source` varchar(40) NOT NULL DEFAULT 'unknown',
            `request_path` varchar(255) NOT NULL DEFAULT '',
            `http_status` int NOT NULL DEFAULT 0,
            `from_cache` tinyint NOT NULL DEFAULT 0,
            `quota_blocked` tinyint NOT NULL DEFAULT 0,
            `is_live` tinyint NOT NULL DEFAULT 0,
            `message` varchar(255) NULL,
            `ip` varchar(45) NULL,
            PRIMARY KEY (`id`),
            KEY `usage_date` (`usage_date`),
            KEY `created_at` (`created_at`),
            KEY `action_date` (`action`, `usage_date`),
            KEY `source_date` (`source`, `usage_date`),
            KEY `live_date` (`is_live`, `usage_date`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8;");
        return true;
    } catch (Exception $e) {
        return false;
    }
}

function epc_umapi_daily_limit()
{
    $cfg = epc_config();
    $limit = 1000;
    if ($cfg && isset($cfg->umapi_daily_limit) && (int)$cfg->umapi_daily_limit > 0) {
        $limit = (int)$cfg->umapi_daily_limit;
    }
    return $limit;
}

function epc_umapi_current_action()
{
    $action = strtolower(epc_request_value('action'));
    return $action !== '' ? $action : 'unknown';
}

function epc_umapi_detect_source()
{
    $source = epc_request_value('source');
    if ($source !== '') {
        return substr(preg_replace('/[^a-z0-9_\-]/i', '', $source), 0, 40);
    }

    $ua = isset($_SERVER['HTTP_USER_AGENT']) ? (string)$_SERVER['HTTP_USER_AGENT'] : '';
    if (stripos($ua, 'ePartsCart offline warm') !== false) {
        return 'warm_script';
    }
    if (stripos($ua, 'epc-site-performance-probe') !== false || stripos($ua, 'offline-resilience-probe') !== false) {
        return 'probe';
    }

    $referer = isset($_SERVER['HTTP_REFERER']) ? (string)$_SERVER['HTTP_REFERER'] : '';
    if ($referer !== '') {
        if (stripos($referer, '/cp/') !== false) {
            return 'cp';
        }
        if (stripos($referer, 'umapi_catalog') !== false || stripos($referer, 'vehicle_catalog') !== false) {
            return 'catalog_ui';
        }
        if (stripos($referer, 'part_search') !== false || stripos($referer, 'epc_fitment') !== false) {
            return 'part_search';
        }
        if (stripos($referer, 'demand_intelligence') !== false) {
            return 'demand_intel';
        }
        if (stripos($referer, 'epc_parts_agent') !== false) {
            return 'parts_agent';
        }
    }

    return 'frontend';
}

function epc_umapi_log_access(array $meta)
{
    if (!epc_ensure_cache_tables()) {
        return;
    }
    $db = epc_db();
    if (!$db) {
        return;
    }
    try {
        $ip = isset($_SERVER['REMOTE_ADDR']) ? substr((string)$_SERVER['REMOTE_ADDR'], 0, 45) : '';
        $stmt = $db->prepare(
            "INSERT INTO `epc_umapi_usage_log`
            (`usage_date`, `created_at`, `action`, `section`, `source`, `request_path`, `http_status`, `from_cache`, `quota_blocked`, `is_live`, `message`, `ip`)
            VALUES (CURDATE(), ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?);"
        );
        $stmt->execute(array(
            time(),
            substr((string)($meta['action'] ?? 'unknown'), 0, 40),
            substr((string)($meta['section'] ?? ''), 0, 20),
            substr((string)($meta['source'] ?? 'unknown'), 0, 40),
            substr((string)($meta['request_path'] ?? ''), 0, 255),
            (int)($meta['http_status'] ?? 0),
            !empty($meta['from_cache']) ? 1 : 0,
            !empty($meta['quota_blocked']) ? 1 : 0,
            !empty($meta['is_live']) ? 1 : 0,
            isset($meta['message']) ? substr((string)$meta['message'], 0, 255) : null,
            $ip,
        ));
    } catch (Exception $e) {
    }
}

function epc_umapi_today_live_count()
{
    if (!epc_ensure_cache_tables()) {
        return 0;
    }
    $db = epc_db();
    if (!$db) {
        return 0;
    }
    try {
        return (int)$db->query(
            "SELECT COUNT(*) FROM `epc_umapi_usage_log`
             WHERE `usage_date` = CURDATE() AND `is_live` = 1 AND `quota_blocked` = 0;"
        )->fetchColumn();
    } catch (Exception $e) {
        return 0;
    }
}

function epc_umapi_usage_summary($days = 7)
{
    $summary = array(
        'daily_limit' => epc_umapi_daily_limit(),
        'today_live' => 0,
        'today_cache' => 0,
        'today_blocked' => 0,
        'remaining' => epc_umapi_daily_limit(),
        'pct_used' => 0,
        'quota_exceeded' => false,
        'by_action_today' => array(),
        'by_source_today' => array(),
        'history' => array(),
    );
    if (!epc_ensure_cache_tables()) {
        return $summary;
    }
    $db = epc_db();
    if (!$db) {
        return $summary;
    }
    try {
        $summary['today_live'] = epc_umapi_today_live_count();
        $summary['today_cache'] = (int)$db->query(
            "SELECT COUNT(*) FROM `epc_umapi_usage_log`
             WHERE `usage_date` = CURDATE() AND `from_cache` = 1;"
        )->fetchColumn();
        $summary['today_blocked'] = (int)$db->query(
            "SELECT COUNT(*) FROM `epc_umapi_usage_log`
             WHERE `usage_date` = CURDATE() AND `quota_blocked` = 1;"
        )->fetchColumn();
        $summary['remaining'] = max(0, $summary['daily_limit'] - $summary['today_live']);
        $summary['pct_used'] = $summary['daily_limit'] > 0
            ? round(($summary['today_live'] / $summary['daily_limit']) * 100, 1)
            : 0;
        $summary['quota_exceeded'] = $summary['today_live'] >= $summary['daily_limit'];

        $actionRows = $db->query(
            "SELECT `action`, SUM(`is_live`) AS `live`, SUM(`from_cache`) AS `cache`, SUM(`quota_blocked`) AS `blocked`
             FROM `epc_umapi_usage_log`
             WHERE `usage_date` = CURDATE()
             GROUP BY `action`
             ORDER BY `live` DESC, `action` ASC;"
        )->fetchAll();
        foreach ($actionRows as $row) {
            $summary['by_action_today'][] = array(
                'action' => (string)$row['action'],
                'live' => (int)$row['live'],
                'cache' => (int)$row['cache'],
                'blocked' => (int)$row['blocked'],
            );
        }

        $sourceRows = $db->query(
            "SELECT `source`, SUM(`is_live`) AS `live`, SUM(`from_cache`) AS `cache`, SUM(`quota_blocked`) AS `blocked`
             FROM `epc_umapi_usage_log`
             WHERE `usage_date` = CURDATE()
             GROUP BY `source`
             ORDER BY `live` DESC, `source` ASC;"
        )->fetchAll();
        foreach ($sourceRows as $row) {
            $summary['by_source_today'][] = array(
                'source' => (string)$row['source'],
                'live' => (int)$row['live'],
                'cache' => (int)$row['cache'],
                'blocked' => (int)$row['blocked'],
            );
        }

        $days = max(1, min(30, (int)$days));
        $historyRows = $db->query(
            "SELECT `usage_date`, SUM(`is_live`) AS `live`, SUM(`from_cache`) AS `cache`, SUM(`quota_blocked`) AS `blocked`
             FROM `epc_umapi_usage_log`
             WHERE `usage_date` >= DATE_SUB(CURDATE(), INTERVAL " . ($days - 1) . " DAY)
             GROUP BY `usage_date`
             ORDER BY `usage_date` DESC;"
        )->fetchAll();
        foreach ($historyRows as $row) {
            $summary['history'][] = array(
                'date' => (string)$row['usage_date'],
                'live' => (int)$row['live'],
                'cache' => (int)$row['cache'],
                'blocked' => (int)$row['blocked'],
            );
        }
    } catch (Exception $e) {
    }
    return $summary;
}

function epc_umapi_usage_report($days = 7)
{
    $usage = epc_umapi_usage_summary($days);
    $lines = array();
    $lines[] = 'Epart catalog daily utilization report';
    $lines[] = 'Generated: ' . date('Y-m-d H:i:s T');
    $lines[] = '';
    $lines[] = 'Today (live API calls count toward daily limit):';
    $lines[] = '  Daily limit:    ' . $usage['daily_limit'];
    $lines[] = '  Live calls:     ' . $usage['today_live'];
    $lines[] = '  Cache serves:   ' . $usage['today_cache'];
    $lines[] = '  Quota blocked:  ' . $usage['today_blocked'];
    $lines[] = '  Remaining:      ' . $usage['remaining'];
    $lines[] = '  Used:           ' . $usage['pct_used'] . '%';
    $lines[] = '  Quota exceeded: ' . ($usage['quota_exceeded'] ? 'yes' : 'no');
    $lines[] = '';
    $lines[] = 'By action (today):';
    if (!$usage['by_action_today']) {
        $lines[] = '  (no logged requests yet today)';
    } else {
        foreach ($usage['by_action_today'] as $row) {
            $lines[] = '  ' . $row['action'] . ': live ' . $row['live'] . ', cache ' . $row['cache'] . ', blocked ' . $row['blocked'];
        }
    }
    $lines[] = '';
    $lines[] = 'By source (today):';
    if (!$usage['by_source_today']) {
        $lines[] = '  (no logged requests yet today)';
    } else {
        foreach ($usage['by_source_today'] as $row) {
            $lines[] = '  ' . $row['source'] . ': live ' . $row['live'] . ', cache ' . $row['cache'] . ', blocked ' . $row['blocked'];
        }
    }
    $lines[] = '';
    $lines[] = 'Last ' . max(1, (int)$days) . ' day(s):';
    if (!$usage['history']) {
        $lines[] = '  (no history yet)';
    } else {
        foreach ($usage['history'] as $row) {
            $lines[] = '  ' . $row['date'] . ': live ' . $row['live'] . ', cache ' . $row['cache'] . ', blocked ' . $row['blocked'];
        }
    }
    return array('text' => implode("\n", $lines) . "\n", 'usage' => $usage);
}

function epc_umapi_recent_events($limit = 50, $liveOnly = false)
{
    $rows = array();
    if (!epc_ensure_cache_tables()) {
        return $rows;
    }
    $db = epc_db();
    if (!$db) {
        return $rows;
    }
    $limit = max(1, min(500, (int)$limit));
    $where = "`usage_date` = CURDATE()";
    if ($liveOnly) {
        $where .= " AND `is_live` = 1";
    }
    try {
        $stmt = $db->query(
            "SELECT `created_at`, `action`, `section`, `source`, `request_path`, `http_status`, `from_cache`, `quota_blocked`, `is_live`, `message`
             FROM `epc_umapi_usage_log`
             WHERE {$where}
             ORDER BY `id` DESC
             LIMIT {$limit};"
        );
        while ($row = $stmt->fetch()) {
            $rows[] = array(
                'time' => date('Y-m-d H:i:s', (int)$row['created_at']),
                'action' => (string)$row['action'],
                'section' => (string)$row['section'],
                'source' => (string)$row['source'],
                'path' => (string)$row['request_path'],
                'http_status' => (int)$row['http_status'],
                'from_cache' => (int)$row['from_cache'] === 1,
                'quota_blocked' => (int)$row['quota_blocked'] === 1,
                'is_live' => (int)$row['is_live'] === 1,
                'message' => (string)$row['message'],
            );
        }
    } catch (Exception $e) {
    }
    return $rows;
}

function epc_passthrough_params(array $allow)
{
    $params = array();
    foreach ($allow as $name) {
        $value = epc_request_value($name);
        if ($value !== '') {
            $params[$name] = $value;
        }
    }
    return $params;
}

function epc_build_query(array $params)
{
    $parts = array();
    foreach ($params as $key => $value) {
        if (is_array($value)) {
            foreach ($value as $item) {
                $parts[] = rawurlencode($key) . '=' . rawurlencode((string)$item);
            }
        } else {
            $parts[] = rawurlencode($key) . '=' . rawurlencode((string)$value);
        }
    }
    return implode('&', $parts);
}

function epc_first_non_empty(array $item, array $keys)
{
    foreach ($keys as $key) {
        if (isset($item[$key]) && trim((string)$item[$key]) !== '') {
            return trim((string)$item[$key]);
        }
    }
    return '';
}

function epc_normalize_article_item(array $item)
{
    $article = epc_first_non_empty($item, array(
        'ART_ARTICLE_NR',
        'ARTICLE_NR',
        'ARTICLE',
        'ART_NUMBER',
        'ARTICLE_NUMBER',
        'OEN',
        'OEM',
        'ARL_SEARCH_NUMBER',
        'DISPLAY_ARTICLE',
        'NUMBER',
    ));
    if ($article !== '') {
        $item['ART_ARTICLE_NR'] = $article;
        $item['ARTICLE'] = $article;
    }

    $brand = epc_first_non_empty($item, array('SUP_BRAND', 'BRAND', 'SUPPLIER', 'MANUFACTURER', 'BRAND_NAME'));
    if ($brand !== '') {
        $item['SUP_BRAND'] = $brand;
        $item['BRAND'] = $brand;
    }

    $name = epc_first_non_empty($item, array('ART_PRODUCT_NAME', 'COMPLETE_DES', 'PRODUCT_NAME', 'DES', 'NAME', 'PRODUCT'));
    if ($name !== '') {
        $item['ART_PRODUCT_NAME'] = $name;
        $item['NAME'] = $name;
    }

    return $item;
}

function epc_normalize_article_payload($payload)
{
    if (isset($payload['data']) && is_array($payload['data'])) {
        foreach ($payload['data'] as $index => $item) {
            if (is_array($item)) {
                $payload['data'][$index] = epc_normalize_article_item($item);
            }
        }
        return $payload;
    }

    if (is_array($payload)) {
        foreach ($payload as $index => $item) {
            if (is_array($item)) {
                $payload[$index] = epc_normalize_article_item($item);
            }
        }
    }
    return $payload;
}

function epc_normalize_products_payload($payload)
{
    $isWrapped = isset($payload['data']) && is_array($payload['data']);
    $items = $isWrapped ? $payload['data'] : (is_array($payload) ? $payload : array());
    $seen = array();
    $clean = array();

    foreach ($items as $item) {
        if (!is_array($item)) {
            continue;
        }
        $id = epc_first_non_empty($item, array('PT_ID', 'PT_IDS', 'ID'));
        $label = epc_first_non_empty($item, array('PT_DES', 'DES', 'PRODUCT_GROUP', 'PRODUCT', 'NAME'));
        $key = $id !== '' ? 'id:' . $id : 'label:' . mb_strtoupper($label, 'UTF-8');
        if ($key === 'label:' || isset($seen[$key])) {
            continue;
        }
        $seen[$key] = true;
        if ($label !== '') {
            $item['PT_DES'] = $label;
            $item['DES'] = $label;
        }
        $clean[] = $item;
    }

    if ($isWrapped) {
        $payload['data'] = $clean;
        $payload['rows'] = count($clean);
        return $payload;
    }
    return $clean;
}

function epc_cache_key($action, $section, $language, $region, array $params)
{
    ksort($params);
    return sha1($action . '|' . $section . '|' . $language . '|' . $region . '|' . json_encode($params));
}

function epc_count_rows($payload)
{
    if (is_array($payload)) {
        if (isset($payload['data']) && is_array($payload['data'])) {
            $inner = $payload['data'];
            if (isset($inner['matchingVehicles']) && is_array($inner['matchingVehicles'])) {
                return count($inner['matchingVehicles']);
            }
            return count($payload['data']);
        }
        if (isset($payload['matchingVehicles']) && is_array($payload['matchingVehicles'])) {
            return count($payload['matchingVehicles']);
        }
        return count($payload);
    }
    return 0;
}

function epc_cacheable_action($action)
{
    return in_array($action, array('manufacturers', 'models', 'modifications', 'categories', 'products', 'suppliers', 'article', 'vin', 'engines', 'engine_search', 'brands', 'analogs', 'article_links'), true);
}

function epc_umapi_brands_offline_payload($article, $reason = '')
{
    $article = trim((string)$article);
    if ($article === '') {
        return null;
    }
    $db = epc_db();
    $cfg = epc_config();
    if (!$db || !$cfg) {
        return null;
    }
    $brandsLib = dirname(__DIR__) . '/content/shop/docpart/docpart_epc_article_brands.php';
    if (!is_file($brandsLib)) {
        return null;
    }
    require_once $brandsLib;
    try {
        $collected = epc_collect_article_catalog_brands($db, $cfg, $article);
    } catch (Exception $e) {
        return null;
    }
    if (empty($collected['manufacturers']) || !is_array($collected['manufacturers'])) {
        return null;
    }
    $data = array();
    foreach ($collected['manufacturers'] as $row) {
        if (!is_array($row)) {
            continue;
        }
        $brand = trim((string)($row['manufacturer_show'] ?? ($row['manufacturer'] ?? '')));
        if ($brand === '') {
            continue;
        }
        $name = trim((string)($row['name'] ?? ''));
        $data[] = array(
            'BRAND' => $brand,
            'SUP_BRAND' => $brand,
            'MANUFACTURER' => $brand,
            'DISPLAY_NR' => $article,
            'SEARCH_NUMBER' => $article,
            'ARTICLE' => $article,
            'TITLE' => $name !== '' ? $name : ('Part ' . $article),
            'DES' => $name !== '' ? $name : ('Part ' . $article),
        );
    }
    if (!$data) {
        return null;
    }
    $payload = array(
        'rows' => count($data),
        'data' => $data,
        'source' => 'offline_catalog',
        'stale' => true,
    );
    if ($reason !== '') {
        $payload['offline_message'] = $reason;
    }
    return $payload;
}

function epc_normalize_engine_code($code)
{
    return strtoupper(preg_replace('/[^A-Z0-9]/', '', (string)$code));
}

function epc_engine_code_valid($code)
{
    $code = epc_normalize_engine_code($code);
    if ($code === '' || strlen($code) < 2 || strlen($code) > 12) {
        return false;
    }
    return (bool)preg_match('/^[A-Z0-9]+$/', $code);
}

function epc_engine_matches_code(array $engine, $needle)
{
    $needle = epc_normalize_engine_code($needle);
    if ($needle === '') {
        return false;
    }
    $code = epc_normalize_engine_code($engine['ENGINE_CODE'] ?? ($engine['ENG_CODE'] ?? ''));
    return $code !== '' && $code === $needle;
}

function epc_umapi_raw_get($path, array $params = array())
{
    $key = epc_config_key();
    if ($key === '') {
        return array('ok' => false, 'status' => 500, 'data' => array(), 'message' => 'API key missing');
    }
    $url = 'https://api.umapi.ru' . $path;
    if (!empty($params)) {
        $url .= '?' . epc_build_query($params);
    }
    $headers = array(
        'Accept: application/json',
        'X-App-Key: ' . $key,
    );
    $body = false;
    $status = 0;
    if (function_exists('curl_init')) {
        $ch = curl_init($url);
        curl_setopt_array($ch, array(
            CURLOPT_RETURNTRANSFER => true,
            CURLOPT_CONNECTTIMEOUT => 8,
            CURLOPT_TIMEOUT => 20,
            CURLOPT_HTTPHEADER => $headers,
            CURLOPT_FOLLOWLOCATION => false,
            CURLOPT_SSL_VERIFYPEER => true,
            CURLOPT_SSL_VERIFYHOST => 2,
        ));
        $body = curl_exec($ch);
        $status = (int)curl_getinfo($ch, CURLINFO_HTTP_CODE);
        curl_close($ch);
    }
    if ($body === false || $body === '') {
        return array('ok' => false, 'status' => $status ?: 502, 'data' => array(), 'message' => 'Empty response');
    }
    $decoded = json_decode($body, true);
    if (json_last_error() !== JSON_ERROR_NONE || !is_array($decoded)) {
        return array('ok' => false, 'status' => $status ?: 502, 'data' => array(), 'message' => 'Non-JSON response');
    }
    if ($status < 200 || $status >= 300) {
        $message = isset($decoded['message']) ? (is_array($decoded['message']) ? implode('; ', $decoded['message']) : (string)$decoded['message']) : ('HTTP ' . $status);
        return array('ok' => false, 'status' => $status, 'data' => $decoded, 'message' => $message);
    }
    return array('ok' => true, 'status' => $status, 'data' => $decoded, 'message' => '');
}

function epc_engines_list_items($payload)
{
    if (!is_array($payload)) {
        return array();
    }
    if (isset($payload['data']) && is_array($payload['data'])) {
        return $payload['data'];
    }
    if (isset($payload[0]) && is_array($payload[0])) {
        return $payload;
    }
    return array();
}

function epc_engines_for_manufacturer($base, $mfaId, $section, $language, $region)
{
    $mfaId = (int)$mfaId;
    if ($mfaId <= 0) {
        return array();
    }
    $params = array('MFA_ID' => $mfaId);
    $cached = epc_get_cached_response('engines', $section, $language, $region, $params);
    if ($cached && !empty($cached['payload'])) {
        return epc_engines_list_items($cached['payload']);
    }
    $result = epc_umapi_raw_get($base . '/Engines', $params);
    if (!$result['ok']) {
        return array();
    }
    epc_save_cached_response('engines', $section, $language, $region, $params, $result['data'], (int)$result['status']);
    return epc_engines_list_items($result['data']);
}

function epc_engine_search_priority_brands()
{
    return array(
        'TOYOTA', 'NISSAN', 'HONDA', 'MAZDA', 'MITSUBISHI', 'SUBARU', 'SUZUKI', 'ISUZU',
        'DAIHATSU', 'LEXUS', 'MERCEDES-BENZ', 'BMW', 'AUDI', 'VOLKSWAGEN', 'FORD', 'OPEL',
        'PEUGEOT', 'RENAULT', 'HYUNDAI', 'KIA', 'VOLVO', 'LAND ROVER', 'JAGUAR',
    );
}

function epc_sort_manufacturers_for_engine_search(array $manufacturers, $section)
{
    $priority = array();
    foreach (epc_engine_search_priority_brands() as $index => $name) {
        $priority[strtoupper($name)] = $index;
    }
    $popularKey = $section === 'commercial' ? 'POPULAR_CV' : ($section === 'motorbike' ? 'POPULAR_MTB' : 'POPULAR_PC');
    usort($manufacturers, function ($a, $b) use ($priority, $popularKey) {
        $na = strtoupper((string)($a['MANUFACTURER'] ?? ''));
        $nb = strtoupper((string)($b['MANUFACTURER'] ?? ''));
        $pa = isset($priority[$na]) ? $priority[$na] : 1000;
        $pb = isset($priority[$nb]) ? $priority[$nb] : 1000;
        if ($pa !== $pb) {
            return $pa - $pb;
        }
        $popA = !empty($a[$popularKey]) ? 0 : 1;
        $popB = !empty($b[$popularKey]) ? 0 : 1;
        if ($popA !== $popB) {
            return $popA - $popB;
        }
        return strcasecmp($na, $nb);
    });
    return $manufacturers;
}

function epc_manufacturers_for_engine_search($base, $section, $mfaFilter = 0)
{
    $manufacturers = array();
    if ($mfaFilter > 0) {
        if (epc_ensure_cache_tables()) {
            $db = epc_db();
            if ($db) {
                try {
                    $stmt = $db->prepare("SELECT `raw_json` FROM `epc_umapi_manufacturers` WHERE `section` = ? AND `mfa_id` = ? LIMIT 1;");
                    $stmt->execute(array($section, $mfaFilter));
                    $row = $stmt->fetch();
                    if ($row && !empty($row['raw_json'])) {
                        $item = json_decode($row['raw_json'], true);
                        if (is_array($item)) {
                            $manufacturers[] = $item;
                        }
                    }
                } catch (Exception $e) {}
            }
        }
        if (!$manufacturers) {
            $manufacturers[] = array('MFA_ID' => $mfaFilter, 'MANUFACTURER' => '');
        }
        return $manufacturers;
    }
    $cached = epc_cached_manufacturers_payload($section);
    if ($cached && !empty($cached['data'])) {
        $manufacturers = $cached['data'];
    } else {
        $result = epc_umapi_raw_get($base . '/Manufacturers', array('type' => array('PC', 'E-PC', 'LCV', 'E-LCV'), 'popular' => 'false'));
        if ($result['ok']) {
            $manufacturers = epc_engines_list_items($result['data']);
        }
    }
    return epc_sort_manufacturers_for_engine_search($manufacturers, $section);
}

function epc_engine_search_payload($code, $section, $language, $region)
{
    $needle = epc_normalize_engine_code($code);
    if (!epc_engine_code_valid($needle)) {
        return null;
    }
    $mfaFilter = (int)epc_request_value('MFA_ID', '0');
    $cacheParams = array('code' => $needle, 'MFA_ID' => $mfaFilter);
    if (epc_request_value('refresh') !== '1') {
        $cached = epc_get_cached_response('engine_search', $section, $language, $region, $cacheParams);
        if ($cached && !empty($cached['payload']) && is_array($cached['payload'])) {
            $payload = $cached['payload'];
            $payload['source'] = 'cache';
            return $payload;
        }
    }

    $base = '/v2/autocatalog/' . rawurlencode($language) . '-' . rawurlencode($region);
    $manufacturers = epc_manufacturers_for_engine_search($base, $section, $mfaFilter);
    $matches = array();
    $scanned = 0;
    $maxMatches = 25;
    $maxScan = $mfaFilter > 0 ? 1 : 50;

    foreach ($manufacturers as $mfg) {
        if (count($matches) >= $maxMatches) {
            break;
        }
        if ($scanned >= $maxScan) {
            break;
        }
        $mfaId = (int)($mfg['MFA_ID'] ?? 0);
        if ($mfaId <= 0) {
            continue;
        }
        $scanned++;
        $engines = epc_engines_for_manufacturer($base, $mfaId, $section, $language, $region);
        foreach ($engines as $engine) {
            if (!is_array($engine)) {
                continue;
            }
            if (epc_engine_matches_code($engine, $needle)) {
                $matches[] = $engine;
                if (count($matches) >= $maxMatches) {
                    break 2;
                }
            }
        }
    }

    $payload = array(
        'code' => $needle,
        'rows' => count($matches),
        'data' => $matches,
        'scanned_brands' => $scanned,
        'truncated' => ($mfaFilter <= 0 && $scanned >= $maxScan && count($matches) < $maxMatches),
    );
    epc_save_cached_response('engine_search', $section, $language, $region, $cacheParams, $payload, 200);
    return $payload;
}

function epc_normalize_vin($vin)
{
    return strtoupper(preg_replace('/[^A-Z0-9]/', '', (string)$vin));
}

function epc_vin_decode_data(array $payload)
{
    if (isset($payload['data']) && is_array($payload['data'])) {
        return $payload['data'];
    }
    return $payload;
}

function epc_vin_vehicle_count(array $payload)
{
    $data = epc_vin_decode_data($payload);
    if (!is_array($data) || !isset($data['matchingVehicles'])) {
        return 0;
    }
    $vehicles = $data['matchingVehicles'];
    if (is_array($vehicles)) {
        return count($vehicles);
    }
    if (is_object($vehicles)) {
        return count((array)$vehicles);
    }
    return 0;
}

function epc_vin_response_has_data($payload)
{
    return is_array($payload) && epc_vin_vehicle_count($payload) > 0;
}

function epc_vin_summary_from_payload(array $payload)
{
    $data = epc_vin_decode_data($payload);
    $manufacturer = '';
    $model = '';
    if (isset($data['matchingManufacturers']) && is_array($data['matchingManufacturers'])) {
        $first = reset($data['matchingManufacturers']);
        if (is_array($first)) {
            $manufacturer = (string)($first['manuName'] ?? ($first['MANUFACTURER'] ?? ($first['manufacturer'] ?? '')));
        }
    }
    if (isset($data['matchingModels']) && is_array($data['matchingModels'])) {
        $first = reset($data['matchingModels']);
        if (is_array($first)) {
            $model = (string)($first['modelName'] ?? ($first['MODEL_SERIES'] ?? ($first['model'] ?? '')));
        }
    }
    if ($manufacturer === '' && isset($data['matchingVehicles']) && is_array($data['matchingVehicles'])) {
        $first = reset($data['matchingVehicles']);
        if (is_array($first)) {
            $manufacturer = (string)($first['manuName'] ?? ($first['carName'] ?? ''));
            $model = (string)($first['vehicleTypeDescription'] ?? ($first['modelName'] ?? $model));
        }
    }
    return array($manufacturer, $model);
}

function epc_save_vin_cache($vin, $language, $region, $payload, $status)
{
    if (!epc_ensure_cache_tables() || !is_array($payload)) {
        return;
    }
    $vin = epc_normalize_vin($vin);
    if ($vin === '') {
        return;
    }
    $db = epc_db();
    if (!$db) {
        return;
    }
    list($manufacturer, $model) = epc_vin_summary_from_payload($payload);
    try {
        $stmt = $db->prepare(
            "REPLACE INTO `epc_umapi_vin_cache`
            (`vin`, `language`, `region`, `response_json`, `vehicle_count`, `manufacturer`, `model_label`, `http_status`, `updated_at`)
            VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?);"
        );
        $stmt->execute(array(
            $vin,
            $language,
            $region,
            json_encode($payload, JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES),
            epc_vin_vehicle_count($payload),
            substr($manufacturer, 0, 255),
            substr($model, 0, 255),
            (int)$status,
            time(),
        ));
    } catch (Exception $e) {
    }
}

function epc_cached_vin_payload($vin, $language, $region)
{
    if (!epc_ensure_cache_tables()) {
        return null;
    }
    $vin = epc_normalize_vin($vin);
    if ($vin === '') {
        return null;
    }
    $db = epc_db();
    if (!$db) {
        return null;
    }
    try {
        $stmt = $db->prepare("SELECT `response_json`, `updated_at` FROM `epc_umapi_vin_cache` WHERE `vin` = ? AND `language` = ? AND `region` = ? LIMIT 1;");
        $stmt->execute(array($vin, $language, $region));
        $row = $stmt->fetch();
        if (!$row) {
            return null;
        }
        $payload = json_decode((string)$row['response_json'], true);
        if (!is_array($payload) || !epc_vin_response_has_data($payload)) {
            return null;
        }
        $payload['source'] = 'database';
        $payload['stale'] = true;
        $payload['cached_at'] = (int)$row['updated_at'];
        return $payload;
    } catch (Exception $e) {
        return null;
    }
}

function epc_get_cached_response($action, $section, $language, $region, array $params)
{
    if (!epc_cacheable_action($action) || !epc_ensure_cache_tables()) {
        return null;
    }
    $db = epc_db();
    $key = epc_cache_key($action, $section, $language, $region, $params);
    try {
        $stmt = $db->prepare("SELECT * FROM `epc_umapi_cache` WHERE `cache_key` = ? LIMIT 1;");
        $stmt->execute(array($key));
        $row = $stmt->fetch();
        if (!$row) {
            return null;
        }
        $payload = json_decode($row['response_json'], true);
        if (json_last_error() !== JSON_ERROR_NONE) {
            return null;
        }
        return array('payload' => $payload, 'last_sync' => (int)$row['last_sync'], 'rows_count' => (int)$row['rows_count']);
    } catch (Exception $e) {
        return null;
    }
}

function epc_save_sync_status($connected, $statusCode, $message)
{
    if (!epc_ensure_cache_tables()) {
        return;
    }
    $db = epc_db();
    $now = time();
    $keyHash = hash('sha256', epc_config_key());
    try {
        $stmt = $db->prepare("REPLACE INTO `epc_umapi_sync_status` (`id`, `connected`, `status_code`, `message`, `last_checked`, `last_success`, `last_error`, `key_hash`) VALUES (1, ?, ?, ?, ?, IF(? = 1, ?, COALESCE((SELECT `last_success` FROM (SELECT `last_success` FROM `epc_umapi_sync_status` WHERE `id` = 1) x), 0)), IF(? = 1, COALESCE((SELECT `last_error` FROM (SELECT `last_error` FROM `epc_umapi_sync_status` WHERE `id` = 1) y), 0), ?), ?);");
        $stmt->execute(array($connected ? 1 : 0, (int)$statusCode, substr((string)$message, 0, 255), $now, $connected ? 1 : 0, $now, $connected ? 1 : 0, $now, $keyHash));
    } catch (Exception $e) {}
}

function epc_save_denormalized($action, $section, array $params, $payload, $language = 'en', $region = 'WWW')
{
    if (!is_array($payload) || !epc_ensure_cache_tables()) {
        return;
    }
    $db = epc_db();
    $now = time();
    $items = isset($payload['data']) && is_array($payload['data']) ? $payload['data'] : $payload;
    if (!is_array($items)) {
        return;
    }
    try {
        if ($action === 'manufacturers') {
            $stmt = $db->prepare("REPLACE INTO `epc_umapi_manufacturers` (`section`, `mfa_id`, `manufacturer`, `manufacturer_ru`, `type`, `country`, `popular`, `is_logo`, `raw_json`, `updated_at`) VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?);");
            foreach ($items as $item) {
                if (!is_array($item) || empty($item['MFA_ID'])) {
                    continue;
                }
                $popularKey = $section === 'commercial' ? 'POPULAR_CV' : ($section === 'motorbike' ? 'POPULAR_MTB' : 'POPULAR_PC');
                $stmt->execute(array($section, (int)$item['MFA_ID'], (string)($item['MANUFACTURER'] ?? ''), (string)($item['MANUFACTURER_RU'] ?? ''), (string)($item['TYPE'] ?? ''), (string)($item['COUNTRY'] ?? ''), !empty($item[$popularKey]) ? 1 : 0, !empty($item['IS_LOGO']) ? 1 : 0, json_encode($item, JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES), $now));
            }
        } elseif ($action === 'models') {
            $mfaId = isset($params['MFA_ID']) ? (int)$params['MFA_ID'] : 0;
            $stmt = $db->prepare("REPLACE INTO `epc_umapi_models` (`section`, `mfa_id`, `ms_id`, `model_series`, `year_from`, `year_to`, `raw_json`, `updated_at`) VALUES (?, ?, ?, ?, ?, ?, ?, ?);");
            foreach ($items as $item) {
                if (!is_array($item) || empty($item['MS_ID'])) {
                    continue;
                }
                $stmt->execute(array($section, $mfaId, (int)$item['MS_ID'], (string)($item['MODEL_SERIES'] ?? ''), (string)($item['CI_FROM'] ?? ''), (string)($item['CI_TO'] ?? ''), json_encode($item, JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES), $now));
            }
        } elseif ($action === 'modifications') {
            $msId = isset($params['MS_ID']) ? (int)$params['MS_ID'] : 0;
            $stmt = $db->prepare("REPLACE INTO `epc_umapi_modifications` (`section`, `ms_id`, `modification_id`, `title`, `year_from`, `year_to`, `power_kw`, `capacity_lt`, `fuel_type`, `raw_json`, `updated_at`) VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?);");
            foreach ($items as $item) {
                if (!is_array($item)) {
                    continue;
                }
                $id = (int)($item['PC_ID'] ?? ($item['CV_ID'] ?? ($item['MTB_ID'] ?? ($item['ID'] ?? 0))));
                if ($id <= 0) {
                    continue;
                }
                $title = (string)($item['PASSENGER_CAR'] ?? ($item['COMMERCIAL_VEHICLE'] ?? ($item['MOTORBIKE'] ?? ($item['MODIFICATION'] ?? ($item['DES'] ?? '')))));
                $stmt->execute(array($section, $msId, $id, $title, (string)($item['CI_FROM'] ?? ''), (string)($item['CI_TO'] ?? ''), (string)($item['POWER_KW'] ?? ''), (string)($item['CAPACITY_LT'] ?? ''), (string)($item['FUEL_TYPE'] ?? ''), json_encode($item, JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES), $now));
            }
        } elseif ($action === 'suppliers') {
            $stmt = $db->prepare("REPLACE INTO `epc_umapi_brands` (`sup_id`, `brand`, `full_name`, `raw_json`, `updated_at`) VALUES (?, ?, ?, ?, ?);");
            foreach ($items as $item) {
                if (!is_array($item) || empty($item['SUP_ID'])) {
                    continue;
                }
                $brand = (string)($item['SUP_BRAND'] ?? ($item['BRAND'] ?? ''));
                if ($brand === '') {
                    $brand = (string)($item['SUP_FULL_NAME'] ?? '');
                }
                if ($brand === '') {
                    continue;
                }
                $stmt->execute(array((int)$item['SUP_ID'], $brand, (string)($item['SUP_FULL_NAME'] ?? ''), json_encode($item, JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES), $now));
            }
        } elseif ($action === 'vin') {
            $vin = epc_normalize_vin($params['vin'] ?? '');
            if ($vin !== '') {
                epc_save_vin_cache($vin, $language, $region, $payload, 200);
            }
        }
    } catch (Exception $e) {}
}

function epc_save_cached_response($action, $section, $language, $region, array $params, $payload, $status)
{
    if (!epc_cacheable_action($action) || !epc_ensure_cache_tables()) {
        return;
    }
    $db = epc_db();
    $key = epc_cache_key($action, $section, $language, $region, $params);
    $now = time();
    try {
        $stmt = $db->prepare("REPLACE INTO `epc_umapi_cache` (`cache_key`, `action`, `section`, `language`, `region`, `request_json`, `response_json`, `rows_count`, `http_status`, `last_sync`) VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?);");
        $stmt->execute(array($key, $action, $section, $language, $region, json_encode($params, JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES), json_encode($payload, JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES), epc_count_rows($payload), (int)$status, $now));
        epc_save_denormalized($action, $section, $params, $payload, $language, $region);
    } catch (Exception $e) {}
}

function epc_status_payload()
{
    epc_ensure_cache_tables();
    $db = epc_db();
    $payload = array(
        'connected' => false,
        'message' => 'No Epart catalog check saved yet.',
        'last_checked' => 0,
        'last_success' => 0,
        'counts' => array('manufacturers' => 0, 'models' => 0, 'modifications' => 0, 'brands' => 0),
    );
    if (!$db) {
        $payload['message'] = 'Database connection unavailable.';
        return $payload;
    }
    try {
        $row = $db->query("SELECT * FROM `epc_umapi_sync_status` WHERE `id` = 1;")->fetch();
        if ($row) {
            $payload['connected'] = (int)$row['connected'] === 1;
            $payload['status_code'] = (int)$row['status_code'];
            $payload['message'] = (string)$row['message'];
            $payload['last_checked'] = (int)$row['last_checked'];
            $payload['last_success'] = (int)$row['last_success'];
            $payload['last_error'] = (int)$row['last_error'];
        }
        $payload['counts']['manufacturers'] = (int)$db->query("SELECT COUNT(*) FROM `epc_umapi_manufacturers`;")->fetchColumn();
        $payload['counts']['models'] = (int)$db->query("SELECT COUNT(*) FROM `epc_umapi_models`;")->fetchColumn();
        $payload['counts']['modifications'] = (int)$db->query("SELECT COUNT(*) FROM `epc_umapi_modifications`;")->fetchColumn();
        $payload['counts']['brands'] = (int)$db->query("SELECT COUNT(*) FROM `epc_umapi_brands`;")->fetchColumn();
        $payload['counts']['vins'] = (int)$db->query("SELECT COUNT(*) FROM `epc_umapi_vin_cache` WHERE `vehicle_count` > 0;")->fetchColumn();
        $sections = $db->query("SELECT `section`, COUNT(*) AS `cnt` FROM `epc_umapi_manufacturers` GROUP BY `section`;")->fetchAll();
        $payload['sections'] = array();
        foreach ($sections as $section) {
            $payload['sections'][$section['section']] = (int)$section['cnt'];
        }
        $cacheRows = (int)$db->query("SELECT COUNT(*) FROM `epc_umapi_cache`;")->fetchColumn();
        $payload['cache_rows'] = $cacheRows;
        $payload['offline_ready'] = ($payload['counts']['manufacturers'] >= 20 || $cacheRows >= 5);
        $payload['action_required'] = array();
        if (!$payload['connected'] && !$payload['offline_ready']) {
            $payload['action_required'][] = 'Run /epc-offline-resilience-warm.php while Epart catalog is online to save catalog data.';
        }
        if (!$payload['connected'] && $payload['offline_ready']) {
            $payload['action_required'][] = 'Epart catalog offline — site will use saved catalog. Re-run warm script when the service is back.';
        }
        if ((int)($payload['counts']['vins'] ?? 0) < 5) {
            $payload['action_required'][] = 'Run /epc-offline-resilience-warm.php?vin=1 to save VIN decode data while Epart catalog is available.';
        }
        $payload['usage'] = epc_umapi_usage_summary(7);
        if (!empty($payload['usage']['quota_exceeded'])) {
            $payload['action_required'][] = 'Daily Epart catalog API limit reached (' . (int)$payload['usage']['daily_limit'] . '). Live calls blocked until tomorrow — site uses saved catalog/cache.';
            $payload['action_required'][] = 'View /epc-umapi-daily-report.php?token=epartscart-deploy-2026&key=TECH_KEY for utilization breakdown.';
        }
    } catch (Exception $e) {
        $payload['message'] = $e->getMessage();
    }
    if (function_exists('epc_api_clients_status_overlay')) {
        $payload = epc_api_clients_status_overlay($payload);
    }
    return $payload;
}

function epc_payload_from_raw_rows(array $rows, $source = 'database')
{
    $data = array();
    foreach ($rows as $row) {
        $raw = isset($row['raw_json']) ? (string)$row['raw_json'] : '';
        if ($raw === '') {
            continue;
        }
        $item = json_decode($raw, true);
        if (is_array($item)) {
            $data[] = $item;
        }
    }
    if (!$data) {
        return null;
    }
    return array('rows' => count($data), 'data' => $data, 'source' => $source, 'stale' => true);
}

function epc_cached_manufacturers_payload($section)
{
    if (!epc_ensure_cache_tables()) {
        return null;
    }
    $db = epc_db();
    if (!$db) {
        return null;
    }
    try {
        $stmt = $db->prepare("SELECT `raw_json` FROM `epc_umapi_manufacturers` WHERE `section` = ? ORDER BY `manufacturer` ASC;");
        $stmt->execute(array($section));
        return epc_payload_from_raw_rows($stmt->fetchAll());
    } catch (Exception $e) {
        return null;
    }
}

function epc_cached_models_payload($section, $mfaId)
{
    if ($mfaId <= 0 || !epc_ensure_cache_tables()) {
        return null;
    }
    $db = epc_db();
    if (!$db) {
        return null;
    }
    try {
        $stmt = $db->prepare("SELECT `raw_json` FROM `epc_umapi_models` WHERE `section` = ? AND `mfa_id` = ? ORDER BY `model_series` ASC;");
        $stmt->execute(array($section, $mfaId));
        return epc_payload_from_raw_rows($stmt->fetchAll());
    } catch (Exception $e) {
        return null;
    }
}

function epc_cached_modifications_payload($section, $msId)
{
    if ($msId <= 0 || !epc_ensure_cache_tables()) {
        return null;
    }
    $db = epc_db();
    if (!$db) {
        return null;
    }
    try {
        $stmt = $db->prepare("SELECT `raw_json` FROM `epc_umapi_modifications` WHERE `section` = ? AND `ms_id` = ? ORDER BY `title` ASC;");
        $stmt->execute(array($section, $msId));
        return epc_payload_from_raw_rows($stmt->fetchAll());
    } catch (Exception $e) {
        return null;
    }
}

function epc_normalize_list_payload($payload)
{
    if (!is_array($payload)) {
        return null;
    }
    if (isset($payload['data']) && is_array($payload['data'])) {
        return $payload;
    }
    if (isset($payload[0]) && is_array($payload[0])) {
        $data = array_values($payload);
        return array('rows' => count($data), 'data' => $data);
    }
    return null;
}

function epc_cache_ttl_for_action($cacheAction, $fromDb = false, $stale = false)
{
    if ($cacheAction === 'status') {
        return 60;
    }
    if ($cacheAction === 'vin') {
        return $stale ? 300 : 600;
    }
    if ($cacheAction === 'categories') {
        return $stale ? 1800 : 3600;
    }
    if ($cacheAction === 'engines') {
        return $stale ? 43200 : 86400;
    }
    if ($cacheAction === 'engine_search') {
        return $stale ? 3600 : 21600;
    }
    if (in_array($cacheAction, array('manufacturers', 'models', 'modifications', 'suppliers'), true)) {
        if ($fromDb) {
            return 7200;
        }
        return $stale ? 1800 : 3600;
    }
    if (in_array($cacheAction, array('brands', 'analogs', 'article_links', 'article'), true)) {
        return $stale ? 1800 : 86400;
    }
    return 0;
}

function epc_serve_offline_fallback($cacheAction, $section, $language, $region, array $params, $reason = '')
{
    if ($cacheAction === 'brands') {
        $article = trim((string)($params['article'] ?? ''));
        if ($article !== '') {
            $offlineBrands = epc_umapi_brands_offline_payload($article, $reason);
            if ($offlineBrands && epc_count_rows($offlineBrands) > 0) {
                epc_json_response($offlineBrands, 200, epc_cache_ttl_for_action('brands', true, true));
            }
        }
    }

    if ($cacheAction === 'vin') {
        $vin = epc_normalize_vin($params['vin'] ?? '');
        if ($vin !== '') {
            $vinPayload = epc_cached_vin_payload($vin, $language, $region);
            if ($vinPayload && epc_vin_response_has_data($vinPayload)) {
                if ($reason !== '') {
                    $vinPayload['offline_message'] = $reason;
                }
                epc_json_response($vinPayload, 200, epc_cache_ttl_for_action('vin', true));
            }
        }
    }

    $dbPayload = null;
    switch ($cacheAction) {
        case 'manufacturers':
            $dbPayload = epc_cached_manufacturers_payload($section);
            break;
        case 'models':
            $dbPayload = epc_cached_models_payload($section, (int)($params['MFA_ID'] ?? 0));
            break;
        case 'modifications':
            $dbPayload = epc_cached_modifications_payload($section, (int)($params['MS_ID'] ?? 0));
            break;
        case 'suppliers':
            $dbPayload = epc_cached_brands_payload();
            break;
    }
    if ($dbPayload && epc_count_rows($dbPayload) > 0) {
        if ($reason !== '') {
            $dbPayload['offline_message'] = $reason;
        }
        epc_json_response($dbPayload, 200, epc_cache_ttl_for_action($cacheAction, true));
    }

    if ($cacheAction && epc_cacheable_action($cacheAction)) {
        $cached = epc_get_cached_response($cacheAction, $section, $language, $region, $params);
        if ($cached && !empty($cached['payload'])) {
            if ($cacheAction === 'vin' && epc_vin_response_has_data($cached['payload'])) {
                $payload = $cached['payload'];
                if (is_array($payload)) {
                    $payload['source'] = 'cache';
                    $payload['stale'] = true;
                    if ($reason !== '') {
                        $payload['offline_message'] = $reason;
                    }
                }
                epc_json_response($payload, 200, epc_cache_ttl_for_action('vin', false, true));
            }
            if ($cacheAction === 'categories') {
                $payload = $cached['payload'];
                $body = is_array($payload) && isset($payload['data']) ? $payload['data'] : $payload;
                if (is_array($body) && (isset($body['root']) || isset($body['quic']))) {
                    if (!isset($payload['data'])) {
                        $payload = array('data' => $body);
                    }
                    $payload['source'] = 'cache';
                    $payload['stale'] = true;
                    if ($reason !== '') {
                        $payload['offline_message'] = $reason;
                    }
                    epc_json_response($payload, 200, epc_cache_ttl_for_action('categories', false, true));
                }
            }
            $payload = epc_normalize_list_payload($cached['payload']);
            if ($payload && epc_count_rows($payload) > 0) {
                $payload['source'] = 'cache';
                $payload['stale'] = true;
                if ($reason !== '') {
                    $payload['offline_message'] = $reason;
                }
                epc_json_response($payload, 200, epc_cache_ttl_for_action($cacheAction, false, true));
            }
        }
    }

    return false;
}

function epc_cached_brands_payload()
{
    if (!epc_ensure_cache_tables()) {
        return null;
    }
    $db = epc_db();
    if (!$db) {
        return null;
    }
    try {
        $rows = $db->query("SELECT `sup_id`, `brand`, `full_name` FROM `epc_umapi_brands` ORDER BY `brand` ASC;")->fetchAll();
        $data = array();
        $seen = array();
        foreach ($rows as $row) {
            $brand = trim((string)$row['brand']);
            if ($brand === '') {
                continue;
            }
            $seen[mb_strtoupper($brand, 'UTF-8')] = true;
            $data[] = array(
                'SUP_ID' => (int)$row['sup_id'],
                'SUP_BRAND' => $brand,
                'SUP_FULL_NAME' => (string)$row['full_name'],
            );
        }
        $stockRows = $db->query("SELECT UPPER(TRIM(`manufacturer`)) AS `brand`, COUNT(DISTINCT COALESCE(NULLIF(`article_show`, ''), `article`)) AS `parts_count`
            FROM `shop_docpart_prices_data`
            WHERE TRIM(IFNULL(`manufacturer`, '')) != '' AND TRIM(IFNULL(`article`, '')) != '' AND IFNULL(`price`, 0) > 0 AND IFNULL(`exist`, 0) > 0
            GROUP BY UPPER(TRIM(`manufacturer`))
            ORDER BY `brand` ASC;")->fetchAll();
        foreach ($stockRows as $row) {
            $brand = trim((string)$row['brand']);
            if ($brand === '') {
                continue;
            }
            $key = mb_strtoupper($brand, 'UTF-8');
            if (isset($seen[$key])) {
                continue;
            }
            $seen[$key] = true;
            $data[] = array(
                'SUP_ID' => 0,
                'SUP_BRAND' => $brand,
                'SUP_FULL_NAME' => 'Loaded price-list brand: ' . (int)$row['parts_count'] . ' part numbers',
                'LOCAL_STOCK_COUNT' => (int)$row['parts_count'],
            );
        }
        if (!$data) {
            return null;
        }
        usort($data, function ($a, $b) {
            return strcasecmp($a['SUP_BRAND'], $b['SUP_BRAND']);
        });
        return array('rows' => count($data), 'data' => $data, 'source' => 'database');
    } catch (Exception $e) {
        return null;
    }
}

function epc_brand_parts_payload($brand)
{
    $brand = trim((string)$brand);
    if ($brand === '') {
        epc_json_response(array('message' => 'Brand is required.'), 400);
    }

    $db = epc_db();
    if (!$db) {
        epc_json_response(array('brand' => $brand, 'rows' => 0, 'data' => array(), 'message' => 'Database connection unavailable.'), 200);
    }

    $limit = (int)epc_request_value('limit', '100');
    $offset = (int)epc_request_value('offset', '0');
    if ($limit < 1 || $limit > 500) {
        $limit = 100;
    }
    if ($offset < 0) {
        $offset = 0;
    }

    $brandUpper = mb_strtoupper($brand, 'UTF-8');
    $brandCompact = preg_replace('/[^A-Z0-9А-ЯЁ]+/u', '', $brandUpper);
    try {
        $where = "TRIM(IFNULL(`manufacturer`, '')) != '' AND TRIM(IFNULL(`article`, '')) != '' AND IFNULL(`price`, 0) > 0 AND IFNULL(`exist`, 0) > 0 AND (UPPER(TRIM(`manufacturer`)) = ? OR REPLACE(REPLACE(REPLACE(UPPER(TRIM(`manufacturer`)), ' ', ''), '-', ''), '.', '') = ?)";
        $articleExpression = "COALESCE(NULLIF(`article_show`, ''), `article`)";
        $countStmt = $db->prepare("SELECT COUNT(*) FROM (SELECT $articleExpression AS `article_key` FROM `shop_docpart_prices_data` WHERE $where GROUP BY UPPER(TRIM(`manufacturer`)), $articleExpression) AS brand_items;");
        $countStmt->execute(array($brandUpper, $brandCompact));
        $rowsCount = (int)$countStmt->fetchColumn();

        $stmt = $db->prepare("SELECT
                UPPER(TRIM(`manufacturer`)) AS `manufacturer`,
                $articleExpression AS `article_show`,
                MIN(`article`) AS `article`,
                MIN(`name`) AS `name`,
                SUM(IFNULL(`exist`, 0)) AS `exist`,
                MIN(`price`) AS `price`,
                MIN(`time_to_exe`) AS `time_to_exe`,
                MIN(`storage`) AS `storage`
            FROM `shop_docpart_prices_data`
            WHERE $where
            GROUP BY UPPER(TRIM(`manufacturer`)), $articleExpression
            ORDER BY `article_show` ASC
            LIMIT $limit OFFSET $offset;");
        $stmt->execute(array($brandUpper, $brandCompact));
        $data = $stmt->fetchAll();

        epc_json_response(array(
            'brand' => $brand,
            'rows' => $rowsCount,
            'data' => $data,
            'source' => 'shop_docpart_prices_data',
        ), 200, 600);
    } catch (Exception $e) {
        epc_json_response(array('brand' => $brand, 'rows' => 0, 'data' => array(), 'message' => $e->getMessage()), 200);
    }
}

function epc_call_umapi($path, array $params = array(), $cacheAction = '')
{
    global $language, $region;
    $section = epc_section();
    $action = epc_umapi_current_action();
    if ($action === 'unknown' && $cacheAction !== '') {
        $action = $cacheAction;
    }
    $source = epc_umapi_detect_source();
    $key = epc_config_key();
    if ($key === '') {
        epc_json_response(array('message' => 'Epart catalog API key is not configured.'), 500);
    }

    $dailyLimit = epc_umapi_daily_limit();
    $todayLive = epc_umapi_today_live_count();
    if ($todayLive >= $dailyLimit) {
        epc_umapi_log_access(array(
            'action' => $action,
            'section' => $section,
            'source' => $source,
            'request_path' => $path,
            'http_status' => 402,
            'quota_blocked' => 1,
            'message' => 'Daily limit ' . $dailyLimit . ' reached',
        ));
        epc_save_sync_status(false, 402, 'Payment Required. Exceeded the number of requests per day');
        if ($cacheAction) {
            epc_serve_offline_fallback($cacheAction, $section, $language, $region, $params, 'Daily API limit reached (' . $dailyLimit . '). Using saved catalog.');
        }
        epc_json_response(array(
            'message' => 'Payment Required. Exceeded the number of requests per day',
            'statusCode' => 402,
            'quota' => array(
                'daily_limit' => $dailyLimit,
                'today_live' => $todayLive,
                'remaining' => 0,
            ),
        ), 402);
    }

    $url = 'https://api.umapi.ru' . $path;
    if (!empty($params)) {
        $url .= '?' . epc_build_query($params);
    }

    $headers = array(
        'Accept: application/json',
        'X-App-Key: ' . $key,
    );

    if (function_exists('curl_init')) {
        $ch = curl_init($url);
        curl_setopt_array($ch, array(
            CURLOPT_RETURNTRANSFER => true,
            CURLOPT_CONNECTTIMEOUT => 10,
            CURLOPT_TIMEOUT => 25,
            CURLOPT_HTTPHEADER => $headers,
            CURLOPT_FOLLOWLOCATION => false,
            CURLOPT_SSL_VERIFYPEER => true,
            CURLOPT_SSL_VERIFYHOST => 2,
        ));
        $body = curl_exec($ch);
        $curlError = curl_error($ch);
        $status = (int)curl_getinfo($ch, CURLINFO_HTTP_CODE);
        $contentType = (string)curl_getinfo($ch, CURLINFO_CONTENT_TYPE);
        curl_close($ch);
    } else {
        $context = stream_context_create(array(
            'http' => array(
                'method' => 'GET',
                'header' => implode("\r\n", $headers),
                'timeout' => 25,
                'ignore_errors' => true,
            ),
        ));
        $body = @file_get_contents($url, false, $context);
        $curlError = '';
        $status = 0;
        $contentType = '';
        if (!empty($http_response_header)) {
            foreach ($http_response_header as $header) {
                if (preg_match('/^HTTP\/\S+\s+(\d+)/', $header, $m)) {
                    $status = (int)$m[1];
                } elseif (stripos($header, 'Content-Type:') === 0) {
                    $contentType = trim(substr($header, 13));
                }
            }
        }
    }

    if ($body === false || $body === '') {
        epc_umapi_log_access(array(
            'action' => $action,
            'section' => $section,
            'source' => $source,
            'request_path' => $path,
            'http_status' => $status ?: 502,
            'is_live' => 1,
            'message' => $curlError !== '' ? $curlError : 'Empty response',
        ));
        epc_save_sync_status(false, $status ?: 502, $curlError !== '' ? $curlError : 'Catalog service did not return a response.');
        if ($cacheAction) {
            epc_serve_offline_fallback($cacheAction, $section, $language, $region, $params, $curlError !== '' ? $curlError : 'Catalog service unavailable');
        }
        epc_json_response(array(
            'message' => $curlError !== '' ? $curlError : 'Catalog service did not return a response.',
            'statusCode' => $status ?: 502,
        ), 502);
    }

    $decoded = json_decode($body, true);
    if (json_last_error() !== JSON_ERROR_NONE) {
        epc_umapi_log_access(array(
            'action' => $action,
            'section' => $section,
            'source' => $source,
            'request_path' => $path,
            'http_status' => $status ?: 502,
            'is_live' => 1,
            'message' => 'Non-JSON response',
        ));
        if ($cacheAction) {
            epc_save_sync_status(false, $status ?: 502, 'Non-JSON response');
            epc_serve_offline_fallback($cacheAction, $section, $language, $region, $params, 'Catalog service returned non-JSON');
        }
        epc_json_response(array(
            'message' => 'Catalog service returned a non-JSON response.',
            'statusCode' => $status ?: 502,
            'contentType' => $contentType,
        ), 502);
    }

    $currentAction = strtolower(epc_request_value('action'));
    if (in_array($currentAction, array('articles', 'analogs'), true)) {
        $decoded = epc_normalize_article_payload($decoded);
    } elseif ($currentAction === 'products') {
        $decoded = epc_normalize_products_payload($decoded);
    }

    $apiMessage = '';
    if (is_array($decoded) && isset($decoded['message'])) {
        $apiMessage = is_array($decoded['message']) ? implode('; ', $decoded['message']) : (string)$decoded['message'];
    }
    epc_umapi_log_access(array(
        'action' => $action,
        'section' => $section,
        'source' => $source,
        'request_path' => $path,
        'http_status' => $status,
        'is_live' => 1,
        'message' => $apiMessage !== '' ? $apiMessage : ('HTTP ' . $status),
    ));

    if ($status >= 200 && $status < 300) {
        epc_save_sync_status(true, $status, 'Connected');
        if ($cacheAction) {
            epc_save_cached_response($cacheAction, $section, $language, $region, $params, $decoded, $status);
        }
    } elseif ($cacheAction) {
        epc_save_sync_status(false, $status, is_array($decoded) && isset($decoded['message']) ? (is_array($decoded['message']) ? implode('; ', $decoded['message']) : $decoded['message']) : 'Catalog service error');
        epc_serve_offline_fallback($cacheAction, $section, $language, $region, $params, 'Catalog service error HTTP ' . $status);
    }

    $forwardStatus = ($status >= 400 && $status < 600) ? $status : 200;
    $ttl = ($status >= 200 && $status < 300 && $cacheAction) ? epc_cache_ttl_for_action($cacheAction) : 0;
    epc_json_response($decoded, $forwardStatus, $ttl);
}

if (!defined('EPC_UMAPI_LIB_ONLY') || !EPC_UMAPI_LIB_ONLY) {
if (!defined('_ASTEXE_')) {
    define('_ASTEXE_', 1);
}
$__epcApiClientsLib = dirname(__DIR__) . '/content/general_pages/epc_api_clients.php';
if (is_file($__epcApiClientsLib)) {
    require_once $__epcApiClientsLib;
    if (function_exists('epc_api_client_umapi_gate')) {
        epc_api_client_umapi_gate();
    }
}
$language = strtolower(epc_request_value('language', 'en'));
$language = preg_match('/^[a-z]{2}$/', $language) ? $language : 'en';
$region = epc_clean_code(epc_request_value('region', 'WWW'), 'WWW');
$base = '/v2/autocatalog/' . rawurlencode($language) . '-' . rawurlencode($region);
$action = strtolower(epc_request_value('action'));
$type = epc_vehicle_type();

switch ($action) {
    case 'status':
        epc_json_response(epc_status_payload(), 200, 60);
        break;

    case 'usage_report':
        $days = max(1, min(30, (int)epc_request_value('days', '7')));
        $report = epc_umapi_usage_report($days);
        $report['usage']['recent_today'] = epc_umapi_recent_events(100, false);
        epc_json_response($report['usage'], 200, 30);
        break;

    case 'manufacturers':
        epc_call_umapi($base . '/Manufacturers', array('type' => epc_vehicle_type_list(), 'popular' => 'false'), 'manufacturers');
        break;

    case 'models':
        $params = epc_passthrough_params(array('MFA_ID'));
        $params['type'] = epc_vehicle_type_list();
        epc_call_umapi($base . '/ModelSeries', $params, 'models');
        break;

    case 'modifications':
        if ($type === 'CV') {
            $params = epc_passthrough_params(array('MS_ID'));
            $params['type'] = epc_vehicle_type_list();
            epc_call_umapi($base . '/CommercialVehicles', $params, 'modifications');
        } elseif ($type === 'Motorcycle') {
            $params = epc_passthrough_params(array('MS_ID'));
            $params['type'] = epc_vehicle_type_list();
            epc_call_umapi($base . '/Motorbikes', $params, 'modifications');
        } else {
            $params = epc_passthrough_params(array('MS_ID'));
            $params['type'] = epc_vehicle_type_list();
            epc_call_umapi($base . '/Passangers', $params, 'modifications');
        }
        break;

    case 'categories':
        $params = epc_passthrough_params(array('ID'));
        $params['type'] = $type;
        epc_call_umapi($base . '/Categories', $params, 'categories');
        break;

    case 'products':
        $params = epc_passthrough_params(array('CATEGORY_ID', 'ID'));
        $params['type'] = $type;
        epc_call_umapi($base . '/Products', $params, 'products');
        break;

    case 'articles':
        $params = epc_passthrough_params(array('PT_IDS', 'CATEGORY_ID', 'ID', 'limit', 'offset'));
        $params['type'] = $type;
        epc_call_umapi($base . '/Articles', $params);
        break;

    case 'brands':
        $article = epc_request_value('article');
        if ($article === '') {
            epc_json_response(array('message' => 'Article number is required.'), 400);
        }
        $brandParams = array('article' => $article);
        if (epc_request_value('refresh') !== '1') {
            $cachedBrands = epc_get_cached_response('brands', epc_section(), $language, $region, $brandParams);
            if ($cachedBrands && !empty($cachedBrands['payload']) && epc_count_rows($cachedBrands['payload']) > 0) {
                $payload = $cachedBrands['payload'];
                if (is_array($payload)) {
                    $payload['source'] = 'cache';
                    $payload['stale'] = true;
                }
                epc_umapi_log_access(array(
                    'action' => 'brands',
                    'section' => epc_section(),
                    'source' => epc_umapi_detect_source(),
                    'http_status' => 200,
                    'from_cache' => 1,
                    'message' => 'Saved brand refinement',
                ));
                epc_json_response($payload, 200, epc_cache_ttl_for_action('brands', false, true));
            }
            $offlineBrands = epc_umapi_brands_offline_payload($article);
            if ($offlineBrands && epc_count_rows($offlineBrands) > 0) {
                epc_umapi_log_access(array(
                    'action' => 'brands',
                    'section' => epc_section(),
                    'source' => epc_umapi_detect_source(),
                    'http_status' => 200,
                    'from_cache' => 1,
                    'message' => 'Offline catalog brands',
                ));
                epc_json_response($offlineBrands, 200, epc_cache_ttl_for_action('brands', true, true));
            }
        }
        epc_call_umapi($base . '/BrandRefinement/' . rawurlencode($article), $brandParams, 'brands');
        break;

    case 'analogs':
        $article = epc_request_value('article');
        $brand = epc_request_value('brand');
        if ($article === '' || $brand === '') {
            epc_json_response(array('message' => 'Article and brand are required.'), 400);
        }
        $analogParams = array_merge(
            array('article' => $article, 'brand' => $brand),
            epc_passthrough_params(array('limit', 'offset'))
        );
        if (epc_request_value('refresh') !== '1') {
            $cachedAnalogs = epc_get_cached_response('analogs', epc_section(), $language, $region, $analogParams);
            if ($cachedAnalogs && !empty($cachedAnalogs['payload']) && epc_count_rows($cachedAnalogs['payload']) > 0) {
                $payload = $cachedAnalogs['payload'];
                if (is_array($payload)) {
                    $payload['source'] = 'cache';
                    $payload['stale'] = true;
                }
                epc_umapi_log_access(array(
                    'action' => 'analogs',
                    'section' => epc_section(),
                    'source' => epc_umapi_detect_source(),
                    'http_status' => 200,
                    'from_cache' => 1,
                    'message' => 'Saved analogs',
                ));
                epc_json_response($payload, 200, epc_cache_ttl_for_action('analogs', false, true));
            }
        }
        epc_call_umapi($base . '/Analogs/' . rawurlencode($article) . '/' . rawurlencode($brand), $analogParams, 'analogs');
        break;

    case 'article':
        $id = (int)epc_request_value('id');
        if ($id <= 0) {
            epc_json_response(array('message' => 'Article ID is required.'), 400);
        }
        epc_call_umapi($base . '/Article/' . rawurlencode((string)$id), array(), 'article');
        break;

    case 'article_links':
        $id = (int)epc_request_value('id');
        if ($id <= 0) {
            epc_json_response(array('message' => 'Article ID is required.'), 400);
        }
        $linkParams = array('id' => $id);
        if (epc_request_value('refresh') !== '1') {
            $cachedLinks = epc_get_cached_response('article_links', epc_section(), $language, $region, $linkParams);
            if ($cachedLinks && !empty($cachedLinks['payload'])) {
                $payload = $cachedLinks['payload'];
                $hasFitment = is_array($payload) && (
                    !empty($payload['PC']) || !empty($payload['CV']) || !empty($payload['Motorcycle'])
                    || (isset($payload['data']) && is_array($payload['data']) && (
                        !empty($payload['data']['PC']) || !empty($payload['data']['CV']) || !empty($payload['data']['Motorcycle'])
                    ))
                );
                if ($hasFitment || epc_count_rows($payload) > 0) {
                    if (is_array($payload)) {
                        $payload['source'] = 'cache';
                        $payload['stale'] = true;
                    }
                    epc_umapi_log_access(array(
                        'action' => 'article_links',
                        'section' => epc_section(),
                        'source' => epc_umapi_detect_source(),
                        'http_status' => 200,
                        'from_cache' => 1,
                        'message' => 'Saved article links',
                    ));
                    epc_json_response($payload, 200, epc_cache_ttl_for_action('article_links', false, true));
                }
            }
        }
        epc_call_umapi($base . '/ArticleLinks/' . rawurlencode((string)$id), $linkParams, 'article_links');
        break;

    case 'suppliers':
        if (epc_request_value('refresh') !== '1' && (int)epc_request_value('offset', '0') === 0) {
            $cachedBrands = epc_cached_brands_payload();
            if ($cachedBrands && !empty($cachedBrands['data'])) {
                epc_umapi_log_access(array(
                    'action' => 'suppliers',
                    'section' => epc_section(),
                    'source' => epc_umapi_detect_source(),
                    'http_status' => 200,
                    'from_cache' => 1,
                    'message' => 'Saved brands',
                ));
                epc_json_response($cachedBrands, 200, epc_cache_ttl_for_action('suppliers', true));
            }
        }
        epc_call_umapi($base . '/Suppliers', epc_passthrough_params(array('limit', 'offset')), 'suppliers');
        break;

    case 'brand_parts':
        epc_brand_parts_payload(epc_request_value('brand'));
        break;

    case 'vin':
        $vin = epc_normalize_vin(epc_request_value('vin'));
        if ($vin === '' || strlen($vin) < 11 || strlen($vin) > 17) {
            epc_json_response(array('message' => 'Valid VIN is required (11–17 characters).'), 400);
        }
        if (epc_request_value('refresh') !== '1') {
            $cachedVin = epc_cached_vin_payload($vin, $language, $region);
            if ($cachedVin && epc_vin_response_has_data($cachedVin)) {
                epc_umapi_log_access(array(
                    'action' => 'vin',
                    'section' => epc_section(),
                    'source' => epc_umapi_detect_source(),
                    'request_path' => '/Vin/' . $vin,
                    'http_status' => 200,
                    'from_cache' => 1,
                    'message' => 'Saved VIN',
                ));
                epc_json_response($cachedVin, 200, epc_cache_ttl_for_action('vin', true));
            }
        }
        epc_call_umapi($base . '/Vin/' . rawurlencode($vin), array('vin' => $vin), 'vin');
        break;

    case 'engines':
        $mfaId = (int)epc_request_value('MFA_ID', '0');
        if ($mfaId <= 0) {
            epc_json_response(array('message' => 'Manufacturer ID (MFA_ID) is required.'), 400);
        }
        if (epc_request_value('refresh') !== '1') {
            $cachedEngines = epc_get_cached_response('engines', epc_section(), $language, $region, array('MFA_ID' => $mfaId));
            if ($cachedEngines && !empty($cachedEngines['payload'])) {
                epc_json_response($cachedEngines['payload'], 200, epc_cache_ttl_for_action('engines', true));
            }
        }
        epc_call_umapi($base . '/Engines', array('MFA_ID' => $mfaId), 'engines');
        break;

    case 'engine':
        $engId = (int)epc_request_value('id', '0');
        if ($engId <= 0) {
            $engId = (int)epc_request_value('ENG_ID', '0');
        }
        if ($engId <= 0) {
            epc_json_response(array('message' => 'Engine ID is required.'), 400);
        }
        epc_call_umapi($base . '/Engine/' . rawurlencode((string)$engId));
        break;

    case 'engine_search':
        $code = epc_request_value('code', epc_request_value('engine', ''));
        $payload = epc_engine_search_payload($code, epc_section(), $language, $region);
        if ($payload === null) {
            epc_json_response(array('message' => 'Valid engine code is required (2–12 characters, e.g. 3L, 12R, 5L).'), 400);
        }
        epc_umapi_log_access(array(
            'action' => 'engine_search',
            'section' => epc_section(),
            'source' => epc_umapi_detect_source(),
            'request_path' => '/Engines',
            'http_status' => 200,
            'message' => 'Engine code ' . $payload['code'],
        ));
        epc_json_response($payload, 200, epc_cache_ttl_for_action('engine_search', !empty($payload['source'])));
        break;

    default:
        epc_json_response(array('message' => 'Unknown catalog action.'), 400);
}
}
