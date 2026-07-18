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
    if (isset($_REQUEST[$key]) && trim((string) $_REQUEST[$key]) !== '') {
        return trim((string) $_REQUEST[$key]);
    }
    // JS / legacy aliases (camelCase ↔ snake_case)
    static $aliases = [
        'vehicle_id' => ['vehicleid', 'vehicleId', 'vid'],
        'unit_id' => ['unitid', 'unitId'],
        'category_id' => ['categoryid', 'categoryId', 'cid'],
        'group_id' => ['groupid', 'groupId', 'quickgroupid', 'quickGroupId'],
        'q' => ['query', 'name', 'search'],
        'oem' => ['number', 'article'],
        'replacement_types' => ['replacementTypes', 'replacementtypes'],
    ];
    if (isset($aliases[$key])) {
        foreach ($aliases[$key] as $alt) {
            if (isset($_REQUEST[$alt]) && trim((string) $_REQUEST[$alt]) !== '') {
                return trim((string) $_REQUEST[$alt]);
            }
        }
    }
    return $default;
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
    // Avoid JSON_SORT_KEYS — missing on some PHP builds (fatal: Undefined constant).
    if (!is_array($params)) {
        $params = array();
    }
    ksort($params);
    $key = $action . ':' . json_encode($params, JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES);
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

function epc_lax_soap_endpoints($oem = true)
{
    if ($oem) {
        return [
            [
                'uri' => 'http://WebCatalog.Kito.ec',
                'location' => 'https://ws.laximo.net/ec.Kito.WebCatalog/services/Catalog.CatalogHttpSoap11Endpoint/',
                'soap_version' => SOAP_1_1,
            ],
            [
                'uri' => 'http://WebCatalog.Kito.ec',
                'location' => 'https://ws.laximo.net/ec.Kito.WebCatalog/services/Catalog.CatalogHttpSoap12Endpoint/',
                'soap_version' => SOAP_1_2,
            ],
        ];
    }
    // Laximo.DOC / Aftermarket lives on aws.laximo.net (Soap12 preferred).
    return [
        [
            'uri' => 'http://Aftermarket.Kito.ec',
            'location' => 'https://aws.laximo.net/ec.Kito.Aftermarket/services/Catalog.CatalogHttpSoap12Endpoint/',
            'soap_version' => SOAP_1_2,
        ],
        [
            'uri' => 'http://Aftermarket.Kito.ec',
            'location' => 'https://aws.laximo.net/ec.Kito.Aftermarket/services/Catalog.CatalogHttpSoap11Endpoint/',
            'soap_version' => SOAP_1_1,
        ],
        [
            'uri' => 'http://Aftermarket.Kito.ec',
            'location' => 'https://ws.laximo.net/ec.Kito.Aftermarket/services/Catalog.CatalogHttpSoap11Endpoint/',
            'soap_version' => SOAP_1_1,
        ],
    ];
}

function epc_lax_soap_normalize_result($result)
{
    if (is_object($result)) {
        if (method_exists($result, '__toString')) {
            $result = (string) $result;
        } else {
            $result = isset($result->return)
                ? $result->return
                : (isset($result->QueryDataLoginResult) ? $result->QueryDataLoginResult : false);
        }
    }
    if (!is_string($result) || $result === '') {
        return false;
    }
    // HTML 404 / gateway pages are not usable SOAP payloads.
    $trim = ltrim($result);
    if ($trim !== '' && $trim[0] !== '<' && stripos($trim, '&lt;') !== 0) {
        if (stripos($trim, 'Not Found') !== false || stripos($trim, '<html') !== false) {
            return false;
        }
    }
    if (strpos($result, '&lt;') !== false && strpos($result, '<response') === false && strpos($result, '<List') === false && strpos($result, '<Find') === false) {
        $result = html_entity_decode($result, ENT_QUOTES | ENT_XML1, 'UTF-8');
    }
    return $result;
}

function epc_lax_soap_call($request, $oem = true)
{
    $creds = $oem ? epc_lax_oem_credentials() : epc_lax_am_credentials();
    $hash = md5($request . $creds['key']);
    $stream = stream_context_create([
        'ssl' => [
            'verify_peer' => false,
            'verify_peer_name' => false,
        ],
        'http' => [
            'timeout' => 20,
        ],
    ]);

    foreach (epc_lax_soap_endpoints($oem) as $ep) {
        $options = [
            'compression' => SOAP_COMPRESSION_ACCEPT | SOAP_COMPRESSION_GZIP,
            'uri' => $ep['uri'],
            'location' => $ep['location'],
            'soap_version' => $ep['soap_version'],
            'stream_context' => $stream,
            'connection_timeout' => 20,
            'exceptions' => true,
            'trace' => false,
        ];
        try {
            $client = new SoapClient(null, $options);
            $result = epc_lax_soap_normalize_result($client->QueryDataLogin($request, $creds['login'], $hash));
            if ($result !== false) {
                return $result;
            }
        } catch (\Throwable $e) {
            continue;
        }
    }
    return false;
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

/**
 * Load Laximo QueryDataLogin XML. Responses are usually:
 *   <response><ListCatalogs>...</ListCatalogs></response>
 * SoapClient may also return entity-encoded markup.
 */
function epc_lax_load_xml($xmlString)
{
    if (!$xmlString || !is_string($xmlString)) {
        return null;
    }
    $xmlString = trim($xmlString);
    if ($xmlString === '') {
        return null;
    }
    // Soap/HTTP sometimes leaves the payload HTML-entity encoded.
    if (strpos($xmlString, '&lt;') !== false && strpos($xmlString, '<response') === false && strpos($xmlString, '<List') === false) {
        $xmlString = html_entity_decode($xmlString, ENT_QUOTES | ENT_XML1, 'UTF-8');
    }

    $xml = @simplexml_load_string($xmlString);
    if (!$xml) {
        $xml = @simplexml_load_string('<root>' . $xmlString . '</root>');
    }
    if (!$xml) {
        return null;
    }

    // Unwrap <response> so tag lookups work at the top level.
    if (isset($xml->response) && $xml->response instanceof \SimpleXMLElement) {
        return $xml->response;
    }
    if (strtolower($xml->getName()) === 'response') {
        return $xml;
    }
    if (isset($xml->ListCatalogs) || isset($xml->FindVehicleByVIN) || isset($xml->ListCategories)
        || isset($xml->FindOEM) || isset($xml->ListQuickGroup) || isset($xml->SearchVehicleDetails)
        || isset($xml->GetWizard2) || isset($xml->ListDetailByUnit) || isset($xml->ListQuickDetail)) {
        return $xml;
    }
    // <root><response>...</response></root>
    foreach ($xml->children() as $child) {
        if (strtolower($child->getName()) === 'response') {
            return $child;
        }
    }
    return $xml;
}

function epc_lax_xml_find($xml, $tag)
{
    if (!$xml instanceof \SimpleXMLElement) {
        return null;
    }
    if (isset($xml->$tag)) {
        return $xml->$tag;
    }
    $found = @$xml->xpath('.//' . $tag);
    if (is_array($found) && isset($found[0]) && $found[0] instanceof \SimpleXMLElement) {
        return $found[0];
    }
    return null;
}

/** True when catalog rows look usable (have code or brand). */
function epc_lax_catalogs_usable($catalogs)
{
    if (!is_array($catalogs) || count($catalogs) < 2) {
        // Single junk row like [{"features":[]}] is not usable.
        if (!is_array($catalogs) || count($catalogs) === 0) {
            return false;
        }
    }
    $usable = 0;
    foreach ($catalogs as $cat) {
        if (!is_array($cat)) {
            continue;
        }
        $code = isset($cat['code']) ? trim((string) $cat['code']) : '';
        $brand = isset($cat['brand']) ? trim((string) $cat['brand']) : '';
        $name = isset($cat['name']) ? trim((string) $cat['name']) : '';
        if ($code !== '' || $brand !== '' || $name !== '') {
            $usable++;
        }
    }
    return $usable >= 1 && $usable === count($catalogs);
}

function epc_lax_cache_delete($key)
{
    $db = epc_lax_db();
    if (!$db) {
        return;
    }
    try {
        $stmt = $db->prepare("DELETE FROM `epc_laximo_cache` WHERE `cache_key` = ?");
        $stmt->execute([$key]);
    } catch (\Throwable $e) {
    }
}

function epc_lax_parse_catalogs($xmlString)
{
    $xml = epc_lax_load_xml($xmlString);
    if (!$xml) {
        return [];
    }

    $list = epc_lax_xml_find($xml, 'ListCatalogs');
    if (!$list) {
        // Some payloads are a bare list of <row> nodes.
        $list = $xml;
    }

    $catalogs = [];
    foreach ($list->children() as $row) {
        if (strtolower($row->getName()) !== 'row' && !isset($row['code']) && !isset($row['brand'])) {
            continue;
        }

        $attrs = [];
        foreach ($row->attributes() as $k => $v) {
            $attrs[strtolower((string) $k)] = (string) $v;
        }
        if (empty($attrs['code']) && empty($attrs['brand']) && empty($attrs['name'])) {
            continue;
        }

        $features = [];
        if (isset($row->features)) {
            foreach ($row->features->children() as $feat) {
                $f = [];
                foreach ($feat->attributes() as $k => $v) {
                    $f[strtolower((string) $k)] = (string) $v;
                }
                $features[] = $f;
            }
        }
        $attrs['features'] = $features;
        if (!empty($attrs['icon']) && empty($attrs['icon_url'])) {
            $attrs['icon_url'] = 'https://cdn.laximo.net/images/catalogs/' . $attrs['icon'];
        }
        $catalogs[] = $attrs;
    }
    return $catalogs;
}

function epc_lax_parse_vehicles($xmlString)
{
    $xml = epc_lax_load_xml($xmlString);
    if (!$xml) {
        return [];
    }

    $vehicles = [];
    $findNode = null;

    foreach (['FindVehicleByVIN', 'FindVehicle', 'FindVehicleByFrameNo', 'FindVehicleByWizard2', 'FindVehicleCustom'] as $tag) {
        $findNode = epc_lax_xml_find($xml, $tag);
        if ($findNode) {
            break;
        }
    }
    if (!$findNode) {
        $findNode = $xml;
    }

    foreach ($findNode->children() as $row) {
        $v = [];
        foreach ($row->attributes() as $k => $val) {
            $v[strtolower((string) $k)] = (string) $val;
        }
        if (isset($row->attribute)) {
            $attrs = [];
            foreach ($row->attribute as $attr) {
                $a = [];
                foreach ($attr->attributes() as $k => $val) {
                    $a[strtolower((string) $k)] = (string) $val;
                }
                $attrs[] = $a;
            }
            $v['attributes'] = $attrs;
        }
        if (!empty($v)) {
            $vehicles[] = $v;
        }
    }
    return $vehicles;
}

function epc_lax_parse_categories($xmlString)
{
    $xml = epc_lax_load_xml($xmlString);
    if (!$xml) {
        return [];
    }

    $node = epc_lax_xml_find($xml, 'ListCategories');
    if (!$node) {
        $node = $xml;
    }
    $categories = [];
    foreach ($node->children() as $row) {
        $c = [];
        foreach ($row->attributes() as $k => $v) {
            $c[strtolower((string) $k)] = (string) $v;
        }
        if ($row->children()->count() > 0) {
            $c['children'] = [];
            foreach ($row->children() as $child) {
                $cc = [];
                foreach ($child->attributes() as $k => $v) {
                    $cc[strtolower((string) $k)] = (string) $v;
                }
                $c['children'][] = $cc;
            }
        }
        if (!empty($c)) {
            $categories[] = $c;
        }
    }
    return $categories;
}

function epc_lax_parse_units($xmlString)
{
    $xml = epc_lax_load_xml($xmlString);
    if (!$xml) {
        return [];
    }

    $node = epc_lax_xml_find($xml, 'ListUnits');
    if (!$node) {
        $node = $xml;
    }
    $units = [];
    foreach ($node->children() as $row) {
        $u = [];
        foreach ($row->attributes() as $k => $v) {
            $u[strtolower((string) $k)] = (string) $v;
        }
        if (!empty($u)) {
            $units[] = $u;
        }
    }
    return $units;
}

function epc_lax_attrs_of($row)
{
    $d = [];
    if (!$row instanceof \SimpleXMLElement) {
        return $d;
    }
    foreach ($row->attributes() as $k => $v) {
        $d[strtolower((string) $k)] = (string) $v;
    }
    return $d;
}

function epc_lax_collect_oem_rows($node, &$details)
{
    if (!$node instanceof \SimpleXMLElement) {
        return;
    }
    $attrs = epc_lax_attrs_of($node);
    $tag = strtolower($node->getName());
    $hasOem = !empty($attrs['oem']) || !empty($attrs['number']) || !empty($attrs['code']);
    if ($hasOem && ($tag === 'row' || $tag === 'detail' || isset($attrs['oem']) || isset($attrs['name']))) {
        if (empty($attrs['name']) && trim((string) $node) !== '') {
            $attrs['name'] = trim((string) $node);
        }
        if (!isset($attrs['number']) && !empty($attrs['oem'])) {
            $attrs['number'] = $attrs['oem'];
        }
        $details[] = $attrs;
    }
    foreach ($node->children() as $child) {
        epc_lax_collect_oem_rows($child, $details);
    }
}

function epc_lax_parse_details($xmlString)
{
    $xml = epc_lax_load_xml($xmlString);
    if (!$xml) {
        return [];
    }

    $node = null;
    foreach (['ListDetailByUnit', 'ListQuickDetail', 'SearchVehicleDetails', 'GetOEMPartApplicability'] as $tag) {
        $node = epc_lax_xml_find($xml, $tag);
        if ($node) {
            break;
        }
    }
    if (!$node) {
        $node = $xml;
    }
    $details = [];
    epc_lax_collect_oem_rows($node, $details);
    // De-dupe by oem+name
    $seen = [];
    $out = [];
    foreach ($details as $d) {
        $key = strtolower(($d['oem'] ?? '') . '|' . ($d['name'] ?? ''));
        if ($key === '|' || isset($seen[$key])) {
            continue;
        }
        $seen[$key] = true;
        $out[] = $d;
    }
    return $out;
}

function epc_lax_parse_quick_group_node($row)
{
    $g = epc_lax_attrs_of($row);
    if (empty($g['id']) && !empty($g['quickgroupid'])) {
        $g['id'] = $g['quickgroupid'];
    }
    if (empty($g['categoryid']) && !empty($g['quickgroupid'])) {
        $g['categoryid'] = $g['quickgroupid'];
    }
    $children = [];
    foreach ($row->children() as $child) {
        $children[] = epc_lax_parse_quick_group_node($child);
    }
    if ($children) {
        $g['children'] = $children;
    }
    return $g;
}

function epc_lax_flatten_quick_groups($groups, &$flat)
{
    foreach ($groups as $g) {
        $children = isset($g['children']) && is_array($g['children']) ? $g['children'] : [];
        $id = $g['quickgroupid'] ?? ($g['id'] ?? ($g['categoryid'] ?? ''));
        $explicitLink = isset($g['link']) && ($g['link'] === 'true' || $g['link'] === '1' || $g['link'] === true);
        $isLeaf = empty($children);
        if ($id !== '' && ($explicitLink || $isLeaf)) {
            $flat[] = $g;
        }
        if (!empty($children)) {
            epc_lax_flatten_quick_groups($children, $flat);
        }
    }
}

function epc_lax_parse_quick_groups($xmlString)
{
    $xml = epc_lax_load_xml($xmlString);
    if (!$xml) {
        return [];
    }

    $node = epc_lax_xml_find($xml, 'ListQuickGroup');
    if (!$node) {
        $node = $xml;
    }
    $groups = [];
    foreach ($node->children() as $row) {
        $groups[] = epc_lax_parse_quick_group_node($row);
    }
    $flat = [];
    epc_lax_flatten_quick_groups($groups, $flat);
    // Prefer flat linkable list for storefront; keep tree under 'tree' if needed by callers.
    if (!empty($flat)) {
        return $flat;
    }
    return $groups;
}

function epc_lax_parse_aftermarket($xmlString)
{
    $xml = epc_lax_load_xml($xmlString);
    if (!$xml) {
        return [];
    }
    $node = epc_lax_xml_find($xml, 'FindOEM');
    if (!$node) {
        $node = epc_lax_xml_find($xml, 'FindDetail');
    }
    if (!$node) {
        $node = $xml;
    }

    $items = [];
    $push = function ($attrs, $extra = []) use (&$items) {
        if (empty($attrs['oem']) && empty($attrs['formattedoem']) && empty($attrs['number'])) {
            return;
        }
        $oem = $attrs['oem'] ?? ($attrs['formattedoem'] ?? ($attrs['number'] ?? ''));
        $row = array_merge([
            'brand' => $attrs['manufacturer'] ?? ($attrs['brand'] ?? ''),
            'manufacturer' => $attrs['manufacturer'] ?? ($attrs['brand'] ?? ''),
            'number' => $oem,
            'oem' => $oem,
            'name' => $attrs['name'] ?? '',
            'weight' => $attrs['weight'] ?? '',
            'formattedoem' => $attrs['formattedoem'] ?? $oem,
        ], $extra);
        $items[] = $row;
    };

    foreach ($node->children() as $detail) {
        if (strtolower($detail->getName()) !== 'detail' && empty(epc_lax_attrs_of($detail)['oem'])) {
            // Some payloads wrap differently; still try.
        }
        $attrs = epc_lax_attrs_of($detail);
        $push($attrs, ['is_replacement' => false]);
        if (isset($detail->replacements)) {
            foreach ($detail->replacements->children() as $rep) {
                $repAttrs = epc_lax_attrs_of($rep);
                $detailNode = isset($rep->detail) ? $rep->detail : null;
                $dAttrs = $detailNode ? epc_lax_attrs_of($detailNode) : $repAttrs;
                $push($dAttrs, [
                    'is_replacement' => true,
                    'replacement_type' => $repAttrs['type'] ?? ($repAttrs['replacementtype'] ?? ''),
                    'rate' => $repAttrs['rate'] ?? '',
                ]);
            }
        }
    }

    // Fallback: any oem-bearing nodes
    if (empty($items)) {
        $raw = [];
        epc_lax_collect_oem_rows($node, $raw);
        foreach ($raw as $r) {
            $push($r);
        }
    }
    return $items;
}

function epc_lax_parse_wizard($xmlString)
{
    $xml = epc_lax_load_xml($xmlString);
    if (!$xml) {
        return [];
    }

    $node = epc_lax_xml_find($xml, 'GetWizard2');
    if (!$node) {
        $node = $xml;
    }
    $steps = [];
    foreach ($node->children() as $row) {
        $s = [];
        foreach ($row->attributes() as $k => $v) {
            $s[strtolower((string) $k)] = (string) $v;
        }
        if ($row->children()->count() > 0) {
            $s['options'] = [];
            foreach ($row->children() as $opt) {
                $o = [];
                foreach ($opt->attributes() as $k => $v) {
                    $o[strtolower((string) $k)] = (string) $v;
                }
                $s['options'][] = $o;
            }
        }
        if (!empty($s)) {
            $steps[] = $s;
        }
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
    if (!epc_lax_catalogs_usable($catalogs)) {
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
    $forceRefresh = epc_lax_param('refresh', '') === '1' || epc_lax_param('nocache', '') === '1';

    if ($forceRefresh) {
        epc_lax_cache_delete($cacheKey);
    }

    if (!$forceRefresh) {
        $cached = epc_lax_cache_get($cacheKey, 86400);
        if (epc_lax_catalogs_usable($cached)) {
            epc_lax_json(['status' => true, 'source' => 'cache', 'catalogs' => $cached], 200, 3600);
        }
        if ($cached !== null) {
            // Poisoned cache from older parser bugs (e.g. [{"features":[]}]).
            epc_lax_cache_delete($cacheKey);
        }
    }

    $db = epc_lax_db();
    if ($db && !$forceRefresh) {
        try {
            $stmt = $db->query("SELECT * FROM `epc_laximo_catalogs` WHERE `updated_at` > " . (time() - 86400) . " ORDER BY `brand` ASC");
            $rows = $stmt->fetchAll();
            if (epc_lax_catalogs_usable($rows)) {
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
            if (!epc_lax_catalogs_usable($cached) && $db) {
                $stmt = $db->query("SELECT * FROM `epc_laximo_catalogs` ORDER BY `brand` ASC");
                $cached = $stmt->fetchAll();
            }
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
            if (epc_lax_catalogs_usable($rows)) {
                epc_lax_json(['status' => true, 'source' => 'db_stale', 'catalogs' => $rows], 200, 300);
            }
        } catch (\Throwable $e) {
            // Table might not exist
        }
    }

    epc_lax_json(['status' => false, 'error' => 'OEM catalog credentials not configured or service unavailable. Configure catalog settings in the control panel.'], 503);
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
        epc_lax_json(['status' => false, 'error' => 'Catalog service unavailable'], 503);
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
        epc_lax_json(['status' => true, 'success' => true, 'source' => 'cache', 'steps' => $cached, 'wizard' => ['steps' => $cached]]);
    }

    $request = 'GetWizard2:Locale=' . $locale . '|Catalog=' . $catalog . '|ssd=' . $ssd;
    $xml = epc_lax_soap_call($request, true);
    if ($xml === false) {
        $cached = epc_lax_cache_get($cacheKey, 604800);
        if ($cached) {
            epc_lax_json(['status' => true, 'success' => true, 'source' => 'cache_stale', 'steps' => $cached, 'wizard' => ['steps' => $cached]]);
        }
        epc_lax_json(['status' => false, 'error' => 'Catalog service unavailable'], 503);
    }

    $steps = epc_lax_parse_wizard($xml);
    epc_lax_cache_set($cacheKey, 'wizard', $params, $steps, $xml);
    epc_lax_json(['status' => true, 'success' => true, 'source' => 'live', 'steps' => $steps, 'wizard' => ['steps' => $steps]]);
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
        $payload = is_array($cached) ? $cached : ['type' => 'wizard', 'items' => $cached];
        epc_lax_json([
            'status' => true,
            'success' => true,
            'source' => 'cache',
            'data' => $payload,
            'steps' => ($payload['type'] ?? '') === 'wizard' ? ($payload['items'] ?? []) : [],
            'vehicles' => ($payload['type'] ?? '') === 'vehicles' ? ($payload['items'] ?? []) : [],
        ]);
    }

    // Continue wizard with GetWizard2 (updated SSD), then try vehicle list.
    $wizardXml = epc_lax_soap_call('GetWizard2:Locale=' . $locale . '|Catalog=' . $catalog . '|ssd=' . $ssd, true);
    $steps = $wizardXml !== false ? epc_lax_parse_wizard($wizardXml) : [];

    $vehXml = epc_lax_soap_call('FindVehicleByWizard2:Locale=' . $locale . '|Catalog=' . $catalog . '|ssd=' . $ssd . '|Localized=true', true);
    $vehicles = $vehXml !== false ? epc_lax_parse_vehicles($vehXml) : [];

    if ($wizardXml === false && $vehXml === false) {
        epc_lax_json(['status' => false, 'error' => 'Catalog service unavailable'], 503);
    }

    if (!empty($vehicles)) {
        $payload = ['type' => 'vehicles', 'items' => $vehicles, 'steps' => $steps];
        epc_lax_cache_set($cacheKey, 'wizard_next', $params, $payload, $vehXml ?: $wizardXml);
        epc_lax_json([
            'status' => true,
            'success' => true,
            'source' => 'live',
            'data' => $payload,
            'vehicles' => $vehicles,
            'steps' => $steps,
        ]);
    }

    $payload = ['type' => 'wizard', 'items' => $steps, 'steps' => $steps];
    epc_lax_cache_set($cacheKey, 'wizard_next', $params, $payload, $wizardXml ?: '');
    epc_lax_json([
        'status' => true,
        'success' => true,
        'source' => 'live',
        'data' => $payload,
        'steps' => $steps,
        'wizard' => ['steps' => $steps],
    ]);
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
        epc_lax_json(['status' => false, 'error' => 'Catalog service unavailable'], 503);
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
        epc_lax_json(['status' => false, 'error' => 'Catalog service unavailable'], 503);
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
        epc_lax_json(['status' => false, 'error' => 'Catalog service unavailable'], 503);
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
        epc_lax_json(['status' => false, 'error' => 'Catalog service unavailable'], 503);
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
        epc_lax_json(['status' => true, 'success' => true, 'source' => 'cache', 'groups' => $cached, 'quick_groups' => $cached]);
    }

    $request = 'ListQuickGroup:Locale=' . $locale . '|Catalog=' . $catalog . '|VehicleId=' . $vehicleId . '|ssd=' . $ssd;
    $xml = epc_lax_soap_call($request, true);
    if ($xml === false) {
        $cached = epc_lax_cache_get($cacheKey, 604800);
        if ($cached) {
            epc_lax_json(['status' => true, 'success' => true, 'source' => 'cache_stale', 'groups' => $cached, 'quick_groups' => $cached]);
        }
        epc_lax_json(['status' => false, 'error' => 'Catalog service unavailable'], 503);
    }

    $groups = epc_lax_parse_quick_groups($xml);
    epc_lax_cache_set($cacheKey, 'quick_groups', $params, $groups, $xml);
    epc_lax_json(['status' => true, 'success' => true, 'source' => 'live', 'groups' => $groups, 'quick_groups' => $groups]);
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
        epc_lax_json(['status' => false, 'error' => 'Catalog service unavailable'], 503);
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
        epc_lax_json(['status' => true, 'success' => true, 'source' => 'cache', 'results' => $cached, 'parts' => $cached]);
    }

    // Unified catalog full-text search (requires catalog feature fulltextsearch).
    if ($catalog && $vehicleId !== '') {
        $safeQuery = str_replace(['|', "\n", "\r"], ' ', $query);
        $request = 'SearchVehicleDetails:Query=' . $safeQuery
            . '|Catalog=' . $catalog
            . '|VehicleId=' . $vehicleId
            . '|ssd=' . $ssd
            . '|Locale=' . $locale;
        $xml = epc_lax_soap_call($request, true);
        if ($xml !== false) {
            $data = epc_lax_parse_details($xml);
            epc_lax_cache_set($cacheKey, 'part_search', $params, $data, $xml);
            epc_lax_json(['status' => true, 'success' => true, 'source' => 'live', 'results' => $data, 'parts' => $data]);
        }
    }

    // Fallback: OEM reference lookup when query looks like a part number.
    $oemGuess = strtoupper(preg_replace('/[^A-Za-z0-9]/', '', $query));
    if (strlen($oemGuess) >= 5 && preg_match('/[0-9]/', $oemGuess)) {
        $request = 'FINDPARTREFERENCES:Locale=' . $locale . '|OEM=' . $oemGuess;
        $xml = epc_lax_soap_call($request, true);
        if ($xml !== false) {
            $data = epc_lax_parse_details($xml);
            epc_lax_cache_set($cacheKey, 'part_search', $params, $data, $xml);
            epc_lax_json(['status' => true, 'success' => true, 'source' => 'live', 'results' => $data, 'parts' => $data]);
        }
    }

    epc_lax_json(['status' => false, 'error' => 'Part name search unavailable for this vehicle/catalog, or catalog service error'], 503);
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
        epc_lax_json(['status' => false, 'error' => 'Catalog service unavailable'], 503);
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
        epc_lax_json(['status' => false, 'error' => 'Catalog service unavailable'], 503);
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
        epc_lax_json(['status' => true, 'success' => true, 'source' => 'cache', 'aftermarket' => $cached]);
    }

    $request = 'FindOEM:Locale=' . $locale . '|OEM=' . $oem . '|ReplacementTypes=' . $replacementTypes . '|Options=crosses';
    if ($brand) {
        $request .= '|Brand=' . $brand;
    }
    $xml = epc_lax_soap_call($request, false);
    if ($xml === false) {
        $cached = epc_lax_cache_get($cacheKey, 604800);
        if ($cached) {
            epc_lax_json(['status' => true, 'success' => true, 'source' => 'cache_stale', 'aftermarket' => $cached]);
        }
        epc_lax_json(['status' => false, 'error' => 'Aftermarket catalog service unavailable'], 503);
    }

    $data = epc_lax_parse_aftermarket($xml);
    epc_lax_cache_set($cacheKey, 'aftermarket', $params, $data, $xml);
    epc_lax_json(['status' => true, 'success' => true, 'source' => 'live', 'aftermarket' => $data]);
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

    // Test DOC connection (aws.laximo.net Aftermarket / FindOEM)
    $docXml = epc_lax_soap_call('FindOEM:Locale=en_US|OEM=90915YZZD4|ReplacementTypes=default|Options=crosses', false);
    if ($docXml !== false && is_string($docXml) && (stripos($docXml, 'FindOEM') !== false || stripos($docXml, 'detail') !== false || stripos($docXml, '<response') !== false)) {
        $result['doc']['connected'] = true;
        // Clear stale false negatives from older endpoint bugs
        $parsed = epc_lax_parse_aftermarket($docXml);
        $result['doc']['sample_rows'] = count($parsed);
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
        ? 'OEM catalog connected' . ($result['doc']['connected'] ? ', aftermarket catalog connected' : ', aftermarket catalog unavailable')
        : 'OEM catalog not connected — using cached data';

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
