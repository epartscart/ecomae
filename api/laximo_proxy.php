<?php
/**
 * Laximo Catalog Proxy API — Syncron-style (local DB cache)
 *
 * Provides a JSON API for Laximo.CAT (OEM catalogs) and Laximo.DOC (aftermarket)
 * with local MySQL caching so the storefront works even when the Laximo API is down.
 *
 * Actions:
 *   catalogs        — list all vehicle brands/catalogs
 *   find_vehicle    — VIN/frame search
 *   wizard          — wizard-based vehicle selection (manufacturer params)
 *   wizard_next     — next wizard step
 *   vehicle_info    — vehicle details
 *   categories      — parts categories for a vehicle
 *   units           — units in a category
 *   unit_details    — parts list in a unit
 *   quick_groups    — quick groups (unified structure)
 *   quick_details   — parts in a quick group
 *   part_search     — text-based part search (full-text)
 *   part_refs       — cross-catalog part references
 *   applicability   — OEM part applicability across vehicles
 *   aftermarket     — aftermarket cross-references (DOC)
 *   sync_status     — check sync/connection status
 */

header('Content-Type: application/json; charset=utf-8');
header('X-Content-Type-Options: nosniff');

// Catch fatal errors and output them as JSON
register_shutdown_function(function () {
    $err = error_get_last();
    if ($err && in_array($err['type'], [E_ERROR, E_PARSE, E_CORE_ERROR, E_COMPILE_ERROR])) {
        if (!headers_sent()) {
            header('Content-Type: application/json; charset=utf-8');
            http_response_code(500);
        }
        echo json_encode(['status' => false, 'fatal' => true, 'error' => $err['message'], 'file' => basename($err['file']), 'line' => $err['line']]);
    }
});
set_error_handler(function ($errno, $errstr, $errfile, $errline) {
    throw new ErrorException($errstr, 0, $errno, $errfile, $errline);
}, E_ERROR | E_WARNING | E_PARSE);

function epc_lax_json($data, $status = 200, $cache = 0)
{
    http_response_code($status);
    if ($cache > 0) {
        header('Cache-Control: public, max-age=' . $cache . ', stale-while-revalidate=86400');
    } else {
        header('Cache-Control: no-cache, must-revalidate');
    }
    echo json_encode($data, JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES);
    exit;
}

function epc_lax_param($key, $default = '')
{
    return isset($_REQUEST[$key]) ? trim((string) $_REQUEST[$key]) : $default;
}

function epc_lax_config()
{
    static $cfg = null;
    if ($cfg !== null) {
        return $cfg;
    }
    $path = dirname(__DIR__) . '/config.php';
    if (is_file($path)) {
        require_once $path;
        if (class_exists('DP_Config')) {
            $cfg = new \DP_Config();
            return $cfg;
        }
    }
    return null;
}

function epc_lax_db()
{
    static $db = false;
    if ($db !== false) {
        return $db;
    }
    $cfg = epc_lax_config();
    if (!$cfg || empty($cfg->host) || empty($cfg->db)) {
        $db = null;
        return null;
    }
    try {
        $db = new PDO(
            'mysql:host=' . $cfg->host . ';dbname=' . $cfg->db . ';charset=utf8mb4',
            (string) $cfg->user,
            (string) $cfg->password,
            [PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION, PDO::ATTR_DEFAULT_FETCH_MODE => PDO::FETCH_ASSOC]
        );
    } catch (Exception $e) {
        $db = null;
    }
    return $db;
}

function epc_lax_ensure_tables()
{
    $db = epc_lax_db();
    if (!$db) {
        return false;
    }
    $db->exec("CREATE TABLE IF NOT EXISTS `epc_laximo_catalogs` (
        `code` varchar(50) NOT NULL,
        `brand` varchar(255) NOT NULL,
        `name` varchar(255) NOT NULL,
        `icon` varchar(255) NULL,
        `icon_url` varchar(500) NULL,
        `vin_example` varchar(50) NULL,
        `support_vin` tinyint NOT NULL DEFAULT 0,
        `support_wizard` tinyint NOT NULL DEFAULT 0,
        `support_quickgroups` tinyint NOT NULL DEFAULT 0,
        `support_applicability` tinyint NOT NULL DEFAULT 0,
        `support_fulltext` tinyint NOT NULL DEFAULT 0,
        `features_json` text NULL,
        `raw_xml` mediumtext NULL,
        `updated_at` int NOT NULL DEFAULT 0,
        PRIMARY KEY (`code`),
        KEY `brand` (`brand`)
    ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;");

    $db->exec("CREATE TABLE IF NOT EXISTS `epc_laximo_cache` (
        `cache_key` varchar(190) NOT NULL,
        `action` varchar(50) NOT NULL,
        `locale` varchar(10) NOT NULL DEFAULT 'en_US',
        `request_params` text NULL,
        `response_json` mediumtext NOT NULL,
        `response_xml` mediumtext NULL,
        `rows_count` int NOT NULL DEFAULT 0,
        `http_status` int NOT NULL DEFAULT 200,
        `last_sync` int NOT NULL DEFAULT 0,
        PRIMARY KEY (`cache_key`),
        KEY `action` (`action`),
        KEY `last_sync` (`last_sync`)
    ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;");

    $db->exec("CREATE TABLE IF NOT EXISTS `epc_laximo_sync_status` (
        `id` tinyint NOT NULL,
        `service` varchar(10) NOT NULL DEFAULT 'cat',
        `connected` tinyint NOT NULL DEFAULT 0,
        `status_code` int NOT NULL DEFAULT 0,
        `message` varchar(255) NULL,
        `last_checked` int NOT NULL DEFAULT 0,
        `last_success` int NOT NULL DEFAULT 0,
        `last_error` int NOT NULL DEFAULT 0,
        PRIMARY KEY (`id`)
    ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;");

    return true;
}

function epc_lax_cache_get($key, $maxAge = 86400)
{
    $db = epc_lax_db();
    if (!$db) {
        return null;
    }
    try {
        $stmt = $db->prepare("SELECT `response_json`, `last_sync` FROM `epc_laximo_cache` WHERE `cache_key` = ? AND `last_sync` > ? LIMIT 1");
        $stmt->execute([$key, time() - $maxAge]);
        $row = $stmt->fetch();
        if ($row) {
            return json_decode($row['response_json'], true);
        }
    } catch (Exception $e) {
    }
    return null;
}

function epc_lax_cache_set($key, $action, $params, $data, $xml = '')
{
    $db = epc_lax_db();
    if (!$db) {
        return;
    }
    $json = json_encode($data, JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES);
    $count = is_array($data) ? count($data) : (isset($data['items']) ? count($data['items']) : 0);
    try {
        $stmt = $db->prepare("REPLACE INTO `epc_laximo_cache`
            (`cache_key`, `action`, `locale`, `request_params`, `response_json`, `response_xml`, `rows_count`, `http_status`, `last_sync`)
            VALUES (?, ?, ?, ?, ?, ?, ?, 200, ?)");
        $stmt->execute([
            $key, $action, epc_lax_param('locale', 'en_US'),
            json_encode($params, JSON_UNESCAPED_UNICODE), $json, $xml, $count, time()
        ]);
    } catch (Exception $e) {
    }
}

function epc_lax_cache_key($action, $params)
{
    $key = $action . ':' . json_encode($params, JSON_SORT_KEYS);
    return substr(md5($key), 0, 32) . '_' . substr($action, 0, 30);
}

// --- SOAP helpers ---

function epc_lax_oem_credentials()
{
    $cfg = epc_lax_config();
    $login = (!empty($cfg->laximo_cat_login)) ? trim((string) $cfg->laximo_cat_login) : 'au308248';
    $key = (!empty($cfg->laximo_cat_key)) ? trim((string) $cfg->laximo_cat_key) : '5HcskWnQ8FPhy4LNS';
    return ['login' => $login, 'key' => $key];
}

function epc_lax_am_credentials()
{
    $cfg = epc_lax_config();
    $login = (!empty($cfg->laximo_doc_login)) ? trim((string) $cfg->laximo_doc_login) : 'au216116';
    $key = (!empty($cfg->laximo_doc_key)) ? trim((string) $cfg->laximo_doc_key) : 'Y34TRgYaUNV42rd';
    return ['login' => $login, 'key' => $key];
}

function epc_lax_soap_call($request, $oem = true)
{
    $creds = $oem ? epc_lax_oem_credentials() : epc_lax_am_credentials();
    $hash = md5($request . $creds['key']);

    $options = [
        'compression' => SOAP_COMPRESSION_ACCEPT | SOAP_COMPRESSION_GZIP,
    ];

    if ($oem) {
        $options['uri'] = 'http://WebCatalog.Kito.ec';
        $options['location'] = 'http://ws.laximo.net/ec.Kito.WebCatalog/services/Catalog.CatalogHttpSoap11Endpoint/';
    } else {
        $options['uri'] = 'http://Aftermarket.Kito.ec';
        $options['location'] = 'http://ws.laximo.net/ec.Kito.Aftermarket/services/Catalog.CatalogHttpSoap11Endpoint/';
    }

    try {
        $client = new SoapClient(null, $options);
        $result = $client->QueryDataLogin($request, $creds['login'], $hash);
        // SoapClient may return a string (XML) or an object; ensure string
        if (is_object($result)) {
            if (method_exists($result, '__toString')) {
                $result = (string) $result;
            } else {
                // Try to extract from common SOAP response wrappers
                $result = isset($result->return) ? $result->return : (isset($result->QueryDataLoginResult) ? $result->QueryDataLoginResult : json_encode($result));
            }
        }
        return $result;
    } catch (\Throwable $e) {
        return false;
    }
}

function epc_lax_xml_to_array($xmlString)
{
    if (!$xmlString) {
        return [];
    }
    $xml = @simplexml_load_string($xmlString);
    if (!$xml) {
        return [];
    }
    return json_decode(json_encode($xml), true);
}

function epc_lax_parse_catalogs($xmlString)
{
    $xml = @simplexml_load_string('<root>' . $xmlString . '</root>');
    if (!$xml) {
        $xml = @simplexml_load_string($xmlString);
    }
    if (!$xml) {
        return [];
    }

    $catalogs = [];
    $rows = isset($xml->ListCatalogs) ? $xml->ListCatalogs->children() : (isset($xml->row) ? [$xml] : $xml->children());

    if (isset($xml->ListCatalogs)) {
        $rows = $xml->ListCatalogs->children();
    } else {
        $rows = $xml->children();
    }

    foreach ($rows as $row) {
        $attrs = [];
        foreach ($row->attributes() as $k => $v) {
            $attrs[$k] = (string) $v;
        }

        $features = [];
        if (isset($row->features)) {
            foreach ($row->features->children() as $feat) {
                $f = [];
                foreach ($feat->attributes() as $k => $v) {
                    $f[$k] = (string) $v;
                }
                $features[] = $f;
            }
        }
        $attrs['features'] = $features;
        $catalogs[] = $attrs;
    }
    return $catalogs;
}

function epc_lax_parse_vehicles($xmlString)
{
    $xml = @simplexml_load_string('<root>' . $xmlString . '</root>');
    if (!$xml) {
        $xml = @simplexml_load_string($xmlString);
    }
    if (!$xml) {
        return [];
    }

    $vehicles = [];
    $findNode = null;

    foreach (['FindVehicleByVIN', 'FindVehicle', 'FindVehicleByFrameNo', 'FindVehicleByWizard2', 'FindVehicleCustom'] as $tag) {
        if (isset($xml->$tag)) {
            $findNode = $xml->$tag;
            break;
        }
    }
    if (!$findNode) {
        $findNode = $xml;
    }

    foreach ($findNode->children() as $row) {
        $v = [];
        foreach ($row->attributes() as $k => $val) {
            $v[$k] = (string) $val;
        }
        if (isset($row->attribute)) {
            $attrs = [];
            foreach ($row->attribute as $attr) {
                $a = [];
                foreach ($attr->attributes() as $k => $val) {
                    $a[$k] = (string) $val;
                }
                $attrs[] = $a;
            }
            $v['attributes'] = $attrs;
        }
        $vehicles[] = $v;
    }
    return $vehicles;
}

function epc_lax_parse_categories($xmlString)
{
    $xml = @simplexml_load_string('<root>' . $xmlString . '</root>');
    if (!$xml) {
        $xml = @simplexml_load_string($xmlString);
    }
    if (!$xml) {
        return [];
    }

    $node = isset($xml->ListCategories) ? $xml->ListCategories : $xml;
    $categories = [];
    foreach ($node->children() as $row) {
        $c = [];
        foreach ($row->attributes() as $k => $v) {
            $c[$k] = (string) $v;
        }
        if ($row->children()->count() > 0) {
            $c['children'] = [];
            foreach ($row->children() as $child) {
                $cc = [];
                foreach ($child->attributes() as $k => $v) {
                    $cc[$k] = (string) $v;
                }
                $c['children'][] = $cc;
            }
        }
        $categories[] = $c;
    }
    return $categories;
}

function epc_lax_parse_units($xmlString)
{
    $xml = @simplexml_load_string('<root>' . $xmlString . '</root>');
    if (!$xml) {
        $xml = @simplexml_load_string($xmlString);
    }
    if (!$xml) {
        return [];
    }

    $node = isset($xml->ListUnits) ? $xml->ListUnits : $xml;
    $units = [];
    foreach ($node->children() as $row) {
        $u = [];
        foreach ($row->attributes() as $k => $v) {
            $u[$k] = (string) $v;
        }
        $units[] = $u;
    }
    return $units;
}

function epc_lax_parse_details($xmlString)
{
    $xml = @simplexml_load_string('<root>' . $xmlString . '</root>');
    if (!$xml) {
        $xml = @simplexml_load_string($xmlString);
    }
    if (!$xml) {
        return [];
    }

    $node = isset($xml->ListDetailByUnit) ? $xml->ListDetailByUnit : $xml;
    $details = [];
    foreach ($node->children() as $row) {
        $d = [];
        foreach ($row->attributes() as $k => $v) {
            $d[$k] = (string) $v;
        }
        $details[] = $d;
    }
    return $details;
}

function epc_lax_parse_quick_groups($xmlString)
{
    $xml = @simplexml_load_string('<root>' . $xmlString . '</root>');
    if (!$xml) {
        $xml = @simplexml_load_string($xmlString);
    }
    if (!$xml) {
        return [];
    }

    $node = isset($xml->ListQuickGroup) ? $xml->ListQuickGroup : $xml;
    $groups = [];
    foreach ($node->children() as $row) {
        $g = [];
        foreach ($row->attributes() as $k => $v) {
            $g[$k] = (string) $v;
        }
        if ($row->children()->count() > 0) {
            $g['children'] = [];
            foreach ($row->children() as $child) {
                $cg = [];
                foreach ($child->attributes() as $k => $v) {
                    $cg[$k] = (string) $v;
                }
                $g['children'][] = $cg;
            }
        }
        $groups[] = $g;
    }
    return $groups;
}

function epc_lax_parse_wizard($xmlString)
{
    $xml = @simplexml_load_string('<root>' . $xmlString . '</root>');
    if (!$xml) {
        $xml = @simplexml_load_string($xmlString);
    }
    if (!$xml) {
        return [];
    }

    $node = isset($xml->GetWizard2) ? $xml->GetWizard2 : $xml;
    $steps = [];
    foreach ($node->children() as $row) {
        $s = [];
        foreach ($row->attributes() as $k => $v) {
            $s[$k] = (string) $v;
        }
        if ($row->children()->count() > 0) {
            $s['options'] = [];
            foreach ($row->children() as $opt) {
                $o = [];
                foreach ($opt->attributes() as $k => $v) {
                    $o[$k] = (string) $v;
                }
                $s['options'][] = $o;
            }
        }
        $steps[] = $s;
    }
    return $steps;
}

// --- Sync catalogs to local DB ---

function epc_lax_sync_catalogs()
{
    $db = epc_lax_db();
    if (!$db) {
        return false;
    }

    $locale = epc_lax_param('locale', 'en_US');
    $request = 'ListCatalogs:Locale=' . $locale;
    $xml = epc_lax_soap_call($request, true);
    if (!$xml) {
        return false;
    }

    $catalogs = epc_lax_parse_catalogs($xml);
    if (empty($catalogs)) {
        return false;
    }

    $stmt = $db->prepare("REPLACE INTO `epc_laximo_catalogs`
        (`code`, `brand`, `name`, `icon`, `icon_url`, `vin_example`,
         `support_vin`, `support_wizard`, `support_quickgroups`, `support_applicability`, `support_fulltext`,
         `features_json`, `raw_xml`, `updated_at`)
        VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)");

    foreach ($catalogs as $cat) {
        $code = isset($cat['code']) ? $cat['code'] : '';
        $brand = isset($cat['brand']) ? $cat['brand'] : '';
        $name = isset($cat['name']) ? $cat['name'] : $brand;
        $icon = isset($cat['icon']) ? $cat['icon'] : '';
        $iconUrl = $icon ? 'https://cdn.laximo.net/images/catalogs/' . $icon : '';
        $vinExample = isset($cat['vinexample']) ? $cat['vinexample'] : '';
        $supportVin = !empty($cat['supportvinsearch']) && $cat['supportvinsearch'] === 'true' ? 1 : 0;
        $supportWizard = !empty($cat['supportparameteridentification2']) && $cat['supportparameteridentification2'] === 'true' ? 1 : 0;
        $supportQG = !empty($cat['supportquickgroups']) && $cat['supportquickgroups'] === 'true' ? 1 : 0;
        $supportAppl = !empty($cat['supportdetailapplicability']) && $cat['supportdetailapplicability'] === 'true' ? 1 : 0;
        $supportFT = 0;
        $features = isset($cat['features']) ? $cat['features'] : [];
        foreach ($features as $f) {
            if (isset($f['name']) && $f['name'] === 'fulltextsearch') {
                $supportFT = 1;
            }
        }

        $stmt->execute([
            $code, $brand, $name, $icon, $iconUrl, $vinExample,
            $supportVin, $supportWizard, $supportQG, $supportAppl, $supportFT,
            json_encode($features, JSON_UNESCAPED_UNICODE), '', time()
        ]);
    }

    $cacheKey = epc_lax_cache_key('catalogs', ['locale' => $locale]);
    epc_lax_cache_set($cacheKey, 'catalogs', ['locale' => $locale], $catalogs, $xml);

    return count($catalogs);
}

// --- Action handlers ---

function epc_lax_action_catalogs()
{
    $locale = epc_lax_param('locale', 'en_US');
    $cacheKey = epc_lax_cache_key('catalogs', ['locale' => $locale]);

    $cached = epc_lax_cache_get($cacheKey, 86400);
    if ($cached) {
        epc_lax_json(['status' => true, 'source' => 'cache', 'catalogs' => $cached], 200, 3600);
    }

    $db = epc_lax_db();
    if ($db) {
        try {
            $stmt = $db->query("SELECT * FROM `epc_laximo_catalogs` WHERE `updated_at` > " . (time() - 86400) . " ORDER BY `brand` ASC");
            $rows = $stmt->fetchAll();
            if (!empty($rows)) {
                epc_lax_cache_set($cacheKey, 'catalogs', ['locale' => $locale], $rows);
                epc_lax_json(['status' => true, 'source' => 'db', 'catalogs' => $rows], 200, 3600);
            }
        } catch (\Throwable $e) {
            // Table might not exist yet — continue to sync
        }
    }

    try {
        $count = epc_lax_sync_catalogs();
        if ($count) {
            $cached = epc_lax_cache_get($cacheKey, 86400);
            epc_lax_json(['status' => true, 'source' => 'live', 'synced' => $count, 'catalogs' => $cached ?: []], 200, 3600);
        }
    } catch (\Throwable $e) {
        // Sync failed — report the error for debugging
        epc_lax_json(['status' => false, 'error' => 'Sync failed: ' . $e->getMessage(), 'trace' => basename($e->getFile()) . ':' . $e->getLine()], 500);
    }

    if ($db) {
        try {
            $stmt = $db->query("SELECT * FROM `epc_laximo_catalogs` ORDER BY `brand` ASC");
            $rows = $stmt->fetchAll();
            if (!empty($rows)) {
                epc_lax_json(['status' => true, 'source' => 'db_stale', 'catalogs' => $rows], 200, 300);
            }
        } catch (\Throwable $e) {
            // Table might not exist
        }
    }

    epc_lax_json(['status' => false, 'error' => 'Laximo API credentials not configured or service unavailable. Configure via CP > Settings > Laximo.'], 503);
}

function epc_lax_action_find_vehicle()
{
    $vin = strtoupper(trim(epc_lax_param('vin')));
    $catalog = epc_lax_param('catalog');
    $locale = epc_lax_param('locale', 'en_US');

    if (strlen($vin) < 5) {
        epc_lax_json(['status' => false, 'error' => 'VIN too short'], 400);
    }

    $params = ['vin' => $vin, 'catalog' => $catalog, 'locale' => $locale];
    $cacheKey = epc_lax_cache_key('find_vehicle', $params);

    $cached = epc_lax_cache_get($cacheKey, 3600);
    if ($cached) {
        epc_lax_json(['status' => true, 'source' => 'cache', 'vehicles' => $cached]);
    }

    $request = 'FindVehicleByVIN:Locale=' . $locale . '|VIN=' . $vin . '|Localized=true';
    if ($catalog) {
        $request .= '|Catalog=' . $catalog;
    }
    $xml = epc_lax_soap_call($request, true);
    if ($xml === false) {
        $cached = epc_lax_cache_get($cacheKey, 604800);
        if ($cached) {
            epc_lax_json(['status' => true, 'source' => 'cache_stale', 'vehicles' => $cached]);
        }
        epc_lax_json(['status' => false, 'error' => 'Laximo API unavailable'], 503);
    }

    $vehicles = epc_lax_parse_vehicles($xml);
    epc_lax_cache_set($cacheKey, 'find_vehicle', $params, $vehicles, $xml);
    epc_lax_json(['status' => true, 'source' => 'live', 'vehicles' => $vehicles]);
}

function epc_lax_action_wizard()
{
    $catalog = epc_lax_param('catalog');
    $ssd = epc_lax_param('ssd', '');
    $locale = epc_lax_param('locale', 'en_US');

    if (!$catalog) {
        epc_lax_json(['status' => false, 'error' => 'catalog required'], 400);
    }

    $params = ['catalog' => $catalog, 'ssd' => $ssd, 'locale' => $locale];
    $cacheKey = epc_lax_cache_key('wizard', $params);

    $cached = epc_lax_cache_get($cacheKey, 3600);
    if ($cached) {
        epc_lax_json(['status' => true, 'source' => 'cache', 'steps' => $cached]);
    }

    $request = 'GetWizard2:Locale=' . $locale . '|Catalog=' . $catalog . '|ssd=' . $ssd;
    $xml = epc_lax_soap_call($request, true);
    if ($xml === false) {
        $cached = epc_lax_cache_get($cacheKey, 604800);
        if ($cached) {
            epc_lax_json(['status' => true, 'source' => 'cache_stale', 'steps' => $cached]);
        }
        epc_lax_json(['status' => false, 'error' => 'Laximo API unavailable'], 503);
    }

    $steps = epc_lax_parse_wizard($xml);
    epc_lax_cache_set($cacheKey, 'wizard', $params, $steps, $xml);
    epc_lax_json(['status' => true, 'source' => 'live', 'steps' => $steps]);
}

function epc_lax_action_wizard_next()
{
    $catalog = epc_lax_param('catalog');
    $ssd = epc_lax_param('ssd', '');
    $locale = epc_lax_param('locale', 'en_US');

    if (!$catalog || !$ssd) {
        epc_lax_json(['status' => false, 'error' => 'catalog and ssd required'], 400);
    }

    $params = ['catalog' => $catalog, 'ssd' => $ssd, 'locale' => $locale];
    $cacheKey = epc_lax_cache_key('wizard_next', $params);

    $cached = epc_lax_cache_get($cacheKey, 3600);
    if ($cached) {
        epc_lax_json(['status' => true, 'source' => 'cache', 'data' => $cached]);
    }

    $request = 'FindVehicleByWizard2:Locale=' . $locale . '|Catalog=' . $catalog . '|ssd=' . $ssd . '|Localized=true';
    $xml = epc_lax_soap_call($request, true);
    if ($xml === false) {
        epc_lax_json(['status' => false, 'error' => 'Laximo API unavailable'], 503);
    }

    $vehicles = epc_lax_parse_vehicles($xml);
    if (!empty($vehicles)) {
        epc_lax_cache_set($cacheKey, 'wizard_next', $params, ['type' => 'vehicles', 'items' => $vehicles], $xml);
        epc_lax_json(['status' => true, 'source' => 'live', 'data' => ['type' => 'vehicles', 'items' => $vehicles]]);
    }

    $steps = epc_lax_parse_wizard($xml);
    epc_lax_cache_set($cacheKey, 'wizard_next', $params, ['type' => 'wizard', 'items' => $steps], $xml);
    epc_lax_json(['status' => true, 'source' => 'live', 'data' => ['type' => 'wizard', 'items' => $steps]]);
}

function epc_lax_action_vehicle_info()
{
    $catalog = epc_lax_param('catalog');
    $vehicleId = epc_lax_param('vehicle_id');
    $ssd = epc_lax_param('ssd', '');
    $locale = epc_lax_param('locale', 'en_US');

    if (!$catalog || !$vehicleId) {
        epc_lax_json(['status' => false, 'error' => 'catalog and vehicle_id required'], 400);
    }

    $params = ['catalog' => $catalog, 'vehicle_id' => $vehicleId, 'ssd' => $ssd, 'locale' => $locale];
    $cacheKey = epc_lax_cache_key('vehicle_info', $params);

    $cached = epc_lax_cache_get($cacheKey, 86400);
    if ($cached) {
        epc_lax_json(['status' => true, 'source' => 'cache', 'vehicle' => $cached]);
    }

    $request = 'GetVehicleInfo:Locale=' . $locale . '|Catalog=' . $catalog . '|VehicleId=' . $vehicleId . '|ssd=' . $ssd . '|Localized=true';
    $xml = epc_lax_soap_call($request, true);
    if ($xml === false) {
        $cached = epc_lax_cache_get($cacheKey, 604800);
        if ($cached) {
            epc_lax_json(['status' => true, 'source' => 'cache_stale', 'vehicle' => $cached]);
        }
        epc_lax_json(['status' => false, 'error' => 'Laximo API unavailable'], 503);
    }

    $data = epc_lax_xml_to_array($xml);
    epc_lax_cache_set($cacheKey, 'vehicle_info', $params, $data, $xml);
    epc_lax_json(['status' => true, 'source' => 'live', 'vehicle' => $data]);
}

function epc_lax_action_categories()
{
    $catalog = epc_lax_param('catalog');
    $vehicleId = epc_lax_param('vehicle_id');
    $categoryId = epc_lax_param('category_id', '-1');
    $ssd = epc_lax_param('ssd', '');
    $locale = epc_lax_param('locale', 'en_US');

    if (!$catalog || !$vehicleId) {
        epc_lax_json(['status' => false, 'error' => 'catalog and vehicle_id required'], 400);
    }

    $params = ['catalog' => $catalog, 'vehicle_id' => $vehicleId, 'category_id' => $categoryId, 'ssd' => $ssd, 'locale' => $locale];
    $cacheKey = epc_lax_cache_key('categories', $params);

    $cached = epc_lax_cache_get($cacheKey, 86400);
    if ($cached) {
        epc_lax_json(['status' => true, 'source' => 'cache', 'categories' => $cached]);
    }

    $request = 'ListCategories:Locale=' . $locale . '|Catalog=' . $catalog . '|VehicleId=' . $vehicleId . '|CategoryId=' . $categoryId . '|ssd=' . $ssd;
    $xml = epc_lax_soap_call($request, true);
    if ($xml === false) {
        $cached = epc_lax_cache_get($cacheKey, 604800);
        if ($cached) {
            epc_lax_json(['status' => true, 'source' => 'cache_stale', 'categories' => $cached]);
        }
        epc_lax_json(['status' => false, 'error' => 'Laximo API unavailable'], 503);
    }

    $categories = epc_lax_parse_categories($xml);
    epc_lax_cache_set($cacheKey, 'categories', $params, $categories, $xml);
    epc_lax_json(['status' => true, 'source' => 'live', 'categories' => $categories]);
}

function epc_lax_action_units()
{
    $catalog = epc_lax_param('catalog');
    $vehicleId = epc_lax_param('vehicle_id');
    $categoryId = epc_lax_param('category_id');
    $ssd = epc_lax_param('ssd', '');
    $locale = epc_lax_param('locale', 'en_US');

    if (!$catalog || !$vehicleId || !$categoryId) {
        epc_lax_json(['status' => false, 'error' => 'catalog, vehicle_id and category_id required'], 400);
    }

    $params = ['catalog' => $catalog, 'vehicle_id' => $vehicleId, 'category_id' => $categoryId, 'ssd' => $ssd, 'locale' => $locale];
    $cacheKey = epc_lax_cache_key('units', $params);

    $cached = epc_lax_cache_get($cacheKey, 86400);
    if ($cached) {
        epc_lax_json(['status' => true, 'source' => 'cache', 'units' => $cached]);
    }

    $request = 'ListUnits:Locale=' . $locale . '|Catalog=' . $catalog . '|VehicleId=' . $vehicleId . '|CategoryId=' . $categoryId . '|ssd=' . $ssd . '|Localized=true';
    $xml = epc_lax_soap_call($request, true);
    if ($xml === false) {
        $cached = epc_lax_cache_get($cacheKey, 604800);
        if ($cached) {
            epc_lax_json(['status' => true, 'source' => 'cache_stale', 'units' => $cached]);
        }
        epc_lax_json(['status' => false, 'error' => 'Laximo API unavailable'], 503);
    }

    $units = epc_lax_parse_units($xml);
    epc_lax_cache_set($cacheKey, 'units', $params, $units, $xml);
    epc_lax_json(['status' => true, 'source' => 'live', 'units' => $units]);
}

function epc_lax_action_unit_details()
{
    $catalog = epc_lax_param('catalog');
    $unitId = epc_lax_param('unit_id');
    $ssd = epc_lax_param('ssd', '');
    $locale = epc_lax_param('locale', 'en_US');

    if (!$catalog || !$unitId) {
        epc_lax_json(['status' => false, 'error' => 'catalog and unit_id required'], 400);
    }

    $params = ['catalog' => $catalog, 'unit_id' => $unitId, 'ssd' => $ssd, 'locale' => $locale];
    $cacheKey = epc_lax_cache_key('unit_details', $params);

    $cached = epc_lax_cache_get($cacheKey, 86400);
    if ($cached) {
        epc_lax_json(['status' => true, 'source' => 'cache', 'details' => $cached]);
    }

    $request = 'ListDetailByUnit:Locale=' . $locale . '|Catalog=' . $catalog . '|UnitId=' . $unitId . '|ssd=' . $ssd . '|Localized=true|WithLinks=true';
    $xml = epc_lax_soap_call($request, true);
    if ($xml === false) {
        $cached = epc_lax_cache_get($cacheKey, 604800);
        if ($cached) {
            epc_lax_json(['status' => true, 'source' => 'cache_stale', 'details' => $cached]);
        }
        epc_lax_json(['status' => false, 'error' => 'Laximo API unavailable'], 503);
    }

    $details = epc_lax_parse_details($xml);
    epc_lax_cache_set($cacheKey, 'unit_details', $params, $details, $xml);
    epc_lax_json(['status' => true, 'source' => 'live', 'details' => $details]);
}

function epc_lax_action_quick_groups()
{
    $catalog = epc_lax_param('catalog');
    $vehicleId = epc_lax_param('vehicle_id');
    $ssd = epc_lax_param('ssd', '');
    $locale = epc_lax_param('locale', 'en_US');

    if (!$catalog || !$vehicleId) {
        epc_lax_json(['status' => false, 'error' => 'catalog and vehicle_id required'], 400);
    }

    $params = ['catalog' => $catalog, 'vehicle_id' => $vehicleId, 'ssd' => $ssd, 'locale' => $locale];
    $cacheKey = epc_lax_cache_key('quick_groups', $params);

    $cached = epc_lax_cache_get($cacheKey, 86400);
    if ($cached) {
        epc_lax_json(['status' => true, 'source' => 'cache', 'groups' => $cached]);
    }

    $request = 'ListQuickGroup:Locale=' . $locale . '|Catalog=' . $catalog . '|VehicleId=' . $vehicleId . '|ssd=' . $ssd;
    $xml = epc_lax_soap_call($request, true);
    if ($xml === false) {
        $cached = epc_lax_cache_get($cacheKey, 604800);
        if ($cached) {
            epc_lax_json(['status' => true, 'source' => 'cache_stale', 'groups' => $cached]);
        }
        epc_lax_json(['status' => false, 'error' => 'Laximo API unavailable'], 503);
    }

    $groups = epc_lax_parse_quick_groups($xml);
    epc_lax_cache_set($cacheKey, 'quick_groups', $params, $groups, $xml);
    epc_lax_json(['status' => true, 'source' => 'live', 'groups' => $groups]);
}

function epc_lax_action_quick_details()
{
    $catalog = epc_lax_param('catalog');
    $vehicleId = epc_lax_param('vehicle_id');
    $groupId = epc_lax_param('group_id');
    $ssd = epc_lax_param('ssd', '');
    $locale = epc_lax_param('locale', 'en_US');
    $all = epc_lax_param('all', '0');

    if (!$catalog || !$vehicleId || !$groupId) {
        epc_lax_json(['status' => false, 'error' => 'catalog, vehicle_id and group_id required'], 400);
    }

    $params = ['catalog' => $catalog, 'vehicle_id' => $vehicleId, 'group_id' => $groupId, 'ssd' => $ssd, 'locale' => $locale, 'all' => $all];
    $cacheKey = epc_lax_cache_key('quick_details', $params);

    $cached = epc_lax_cache_get($cacheKey, 86400);
    if ($cached) {
        epc_lax_json(['status' => true, 'source' => 'cache', 'details' => $cached]);
    }

    $request = 'ListQuickDetail:Locale=' . $locale . '|Catalog=' . $catalog . '|VehicleId=' . $vehicleId . '|QuickGroupId=' . $groupId . '|ssd=' . $ssd . '|Localized=true';
    if ($all === '1') {
        $request .= '|All=1';
    }
    $xml = epc_lax_soap_call($request, true);
    if ($xml === false) {
        epc_lax_json(['status' => false, 'error' => 'Laximo API unavailable'], 503);
    }

    $details = epc_lax_parse_details($xml);
    epc_lax_cache_set($cacheKey, 'quick_details', $params, $details, $xml);
    epc_lax_json(['status' => true, 'source' => 'live', 'details' => $details]);
}

function epc_lax_action_part_search()
{
    $catalog = epc_lax_param('catalog');
    $vehicleId = epc_lax_param('vehicle_id');
    $query = trim(epc_lax_param('q'));
    $ssd = epc_lax_param('ssd', '');
    $locale = epc_lax_param('locale', 'en_US');

    if (!$query) {
        epc_lax_json(['status' => false, 'error' => 'query (q) required'], 400);
    }

    $params = ['catalog' => $catalog, 'vehicle_id' => $vehicleId, 'q' => $query, 'ssd' => $ssd, 'locale' => $locale];
    $cacheKey = epc_lax_cache_key('part_search', $params);

    $cached = epc_lax_cache_get($cacheKey, 3600);
    if ($cached) {
        epc_lax_json(['status' => true, 'source' => 'cache', 'results' => $cached]);
    }

    if ($catalog && $vehicleId) {
        $request = 'FindPartReferencesByName:Locale=' . $locale . '|Catalog=' . $catalog . '|VehicleId=' . $vehicleId . '|Name=' . urlencode($query) . '|ssd=' . $ssd;
    } else {
        $request = 'FINDPARTREFERENCES:Locale=' . $locale . '|OEM=' . urlencode($query);
    }

    $xml = epc_lax_soap_call($request, true);
    if ($xml === false) {
        epc_lax_json(['status' => false, 'error' => 'Laximo API unavailable'], 503);
    }

    $data = epc_lax_xml_to_array($xml);
    epc_lax_cache_set($cacheKey, 'part_search', $params, $data, $xml);
    epc_lax_json(['status' => true, 'source' => 'live', 'results' => $data]);
}

function epc_lax_action_part_refs()
{
    $oem = strtoupper(trim(epc_lax_param('oem')));
    $locale = epc_lax_param('locale', 'en_US');

    if (!$oem) {
        epc_lax_json(['status' => false, 'error' => 'oem required'], 400);
    }

    $params = ['oem' => $oem, 'locale' => $locale];
    $cacheKey = epc_lax_cache_key('part_refs', $params);

    $cached = epc_lax_cache_get($cacheKey, 86400);
    if ($cached) {
        epc_lax_json(['status' => true, 'source' => 'cache', 'references' => $cached]);
    }

    $request = 'FINDPARTREFERENCES:Locale=' . $locale . '|OEM=' . $oem;
    $xml = epc_lax_soap_call($request, true);
    if ($xml === false) {
        epc_lax_json(['status' => false, 'error' => 'Laximo API unavailable'], 503);
    }

    $data = epc_lax_xml_to_array($xml);
    epc_lax_cache_set($cacheKey, 'part_refs', $params, $data, $xml);
    epc_lax_json(['status' => true, 'source' => 'live', 'references' => $data]);
}

function epc_lax_action_applicability()
{
    $oem = strtoupper(trim(epc_lax_param('oem')));
    $catalog = epc_lax_param('catalog');
    $ssd = epc_lax_param('ssd', '');
    $locale = epc_lax_param('locale', 'en_US');

    if (!$oem) {
        epc_lax_json(['status' => false, 'error' => 'oem required'], 400);
    }

    $params = ['oem' => $oem, 'catalog' => $catalog, 'ssd' => $ssd, 'locale' => $locale];
    $cacheKey = epc_lax_cache_key('applicability', $params);

    $cached = epc_lax_cache_get($cacheKey, 86400);
    if ($cached) {
        epc_lax_json(['status' => true, 'source' => 'cache', 'applicability' => $cached]);
    }

    $request = 'GetOEMPartApplicability:Locale=' . $locale . '|OEM=' . $oem . '|Catalog=' . $catalog . '|ssd=' . $ssd;
    $xml = epc_lax_soap_call($request, true);
    if ($xml === false) {
        epc_lax_json(['status' => false, 'error' => 'Laximo API unavailable'], 503);
    }

    $data = epc_lax_xml_to_array($xml);
    epc_lax_cache_set($cacheKey, 'applicability', $params, $data, $xml);
    epc_lax_json(['status' => true, 'source' => 'live', 'applicability' => $data]);
}

function epc_lax_action_aftermarket()
{
    $oem = strtoupper(trim(epc_lax_param('oem')));
    $brand = epc_lax_param('brand');
    $locale = epc_lax_param('locale', 'en_US');
    $replacementTypes = epc_lax_param('replacement_types', 'default');

    if (!$oem) {
        epc_lax_json(['status' => false, 'error' => 'oem required'], 400);
    }

    $params = ['oem' => $oem, 'brand' => $brand, 'locale' => $locale, 'replacement_types' => $replacementTypes];
    $cacheKey = epc_lax_cache_key('aftermarket', $params);

    $cached = epc_lax_cache_get($cacheKey, 86400);
    if ($cached) {
        epc_lax_json(['status' => true, 'source' => 'cache', 'aftermarket' => $cached]);
    }

    $request = 'FindOEM:Locale=' . $locale . '|OEM=' . $oem . '|ReplacementTypes=' . $replacementTypes . '|Options=';
    if ($brand) {
        $request .= '|Brand=' . $brand;
    }
    $xml = epc_lax_soap_call($request, false);
    if ($xml === false) {
        $cached = epc_lax_cache_get($cacheKey, 604800);
        if ($cached) {
            epc_lax_json(['status' => true, 'source' => 'cache_stale', 'aftermarket' => $cached]);
        }
        epc_lax_json(['status' => false, 'error' => 'Laximo DOC API unavailable'], 503);
    }

    $data = epc_lax_xml_to_array($xml);
    epc_lax_cache_set($cacheKey, 'aftermarket', $params, $data, $xml);
    epc_lax_json(['status' => true, 'source' => 'live', 'aftermarket' => $data]);
}

function epc_lax_action_sync_status()
{
    $db = epc_lax_db();
    $catCreds = epc_lax_oem_credentials();
    $docCreds = epc_lax_am_credentials();

    $result = [
        'cat' => ['connected' => false, 'last_sync' => 0, 'catalogs_count' => 0, 'login' => $catCreds['login']],
        'doc' => ['connected' => false, 'last_sync' => 0, 'login' => $docCreds['login']],
        'cache_rows' => 0,
        'offline_ready' => false,
        'last_checked' => time(),
    ];

    // Test CAT connection
    $catXml = epc_lax_soap_call('ListCatalogs:Locale=en_US', true);
    if ($catXml !== false) {
        $result['cat']['connected'] = true;
    }

    // Test DOC connection
    $docXml = epc_lax_soap_call('FindOEM:Locale=en_US|OEM=90915YZZD4|ReplacementTypes=default|Options=', false);
    if ($docXml !== false) {
        $result['doc']['connected'] = true;
    }

    if ($db) {
        try {
            $stmt = $db->query("SELECT COUNT(*) as cnt, MAX(updated_at) as last_sync FROM `epc_laximo_catalogs`");
            $row = $stmt->fetch();
            if ($row) {
                $result['cat']['catalogs_count'] = (int) $row['cnt'];
                $result['cat']['last_sync'] = (int) $row['last_sync'];
            }
        } catch (Exception $e) {
        }
        try {
            $stmt = $db->query("SELECT COUNT(*) as cnt FROM `epc_laximo_cache`");
            $row = $stmt->fetch();
            $result['cache_rows'] = $row ? (int) $row['cnt'] : 0;
        } catch (Exception $e) {
        }
        $result['offline_ready'] = ($result['cat']['catalogs_count'] > 0);
    }

    $result['connected'] = $result['cat']['connected'];
    $result['message'] = $result['cat']['connected']
        ? 'Laximo.CAT connected' . ($result['doc']['connected'] ? ', Laximo.DOC connected' : ', Laximo.DOC unavailable')
        : 'Laximo.CAT not connected — using cached data';

    epc_lax_json(['status' => true, 'success' => true, 'services' => $result, 'connected' => $result['connected'], 'message' => $result['message'], 'last_checked' => $result['last_checked'], 'cache_rows' => $result['cache_rows'], 'offline_ready' => $result['offline_ready'], 'cat_login' => $catCreds['login'], 'doc_login' => $docCreds['login']]);
}

function epc_lax_action_sync()
{
    $count = epc_lax_sync_catalogs();
    if ($count === false) {
        epc_lax_json(['status' => false, 'error' => 'Sync failed'], 503);
    }
    epc_lax_json(['status' => true, 'synced_catalogs' => $count]);
}

// --- Main router ---

try {
    epc_lax_ensure_tables();
} catch (\Throwable $e) {
    // Tables may already exist or user lacks CREATE permission — continue
}

$action = epc_lax_param('action', 'catalogs');

switch ($action) {
    case 'catalogs':
        epc_lax_action_catalogs();
        break;
    case 'find_vehicle':
        epc_lax_action_find_vehicle();
        break;
    case 'wizard':
        epc_lax_action_wizard();
        break;
    case 'wizard_next':
        epc_lax_action_wizard_next();
        break;
    case 'vehicle_info':
        epc_lax_action_vehicle_info();
        break;
    case 'categories':
        epc_lax_action_categories();
        break;
    case 'units':
        epc_lax_action_units();
        break;
    case 'unit_details':
        epc_lax_action_unit_details();
        break;
    case 'quick_groups':
        epc_lax_action_quick_groups();
        break;
    case 'quick_details':
        epc_lax_action_quick_details();
        break;
    case 'part_search':
        epc_lax_action_part_search();
        break;
    case 'part_refs':
        epc_lax_action_part_refs();
        break;
    case 'applicability':
        epc_lax_action_applicability();
        break;
    case 'aftermarket':
        epc_lax_action_aftermarket();
        break;
    case 'sync_status':
    case 'status':
        epc_lax_action_sync_status();
        break;
    case 'sync':
        epc_lax_action_sync();
        break;
    default:
        epc_lax_json(['status' => false, 'error' => 'Unknown action: ' . $action], 400);
}
