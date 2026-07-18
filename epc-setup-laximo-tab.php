<?php
/**
 * Setup Laximo Catalog tab in storefront search tabs
 * Run once per tenant to enable the Laximo OEM catalog tab.
 *
 * Usage: php epc-setup-laximo-tab.php
 * Or:    curl "https://www.epartscart.com/epc-setup-laximo-tab.php?token=epartscart-deploy-2026"
 */
header('Content-Type: text/plain; charset=utf-8');

// Auth check
if (php_sapi_name() !== 'cli') {
    require_once __DIR__ . '/epc_deploy_auth.php';
    epc_deploy_require_token();
}

require_once __DIR__ . '/config.php';
$cfg = new DP_Config();

try {
    $db = new PDO(
        'mysql:host=' . $cfg->host . ';dbname=' . $cfg->db . ';charset=utf8mb4',
        $cfg->user,
        $cfg->password,
        [PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION]
    );
} catch (Exception $e) {
    exit("DB connection failed: " . $e->getMessage() . "\n");
}

// Check if laximo_catalog tab already exists
$stmt = $db->prepare("SELECT * FROM `shop_docpart_search_tabs` WHERE `name` = ?");
$stmt->execute(['laximo_catalog']);
$existing = $stmt->fetch(PDO::FETCH_ASSOC);

if ($existing) {
    echo "Laximo catalog tab already exists (id=" . $existing['id'] . ", enabled=" . $existing['enabled'] . ")\n";
    if (!$existing['enabled']) {
        $db->exec("UPDATE `shop_docpart_search_tabs` SET `enabled` = 1 WHERE `name` = 'laximo_catalog'");
        echo "Enabled the tab.\n";
    }
} else {
    // Find max order
    $maxOrder = (int) $db->query("SELECT MAX(`order`) FROM `shop_docpart_search_tabs`")->fetchColumn();
    $newOrder = $maxOrder + 1;

    // Insert the tab (caption uses translation ID — we'll create one or use a temp value)
    $stmt = $db->prepare("INSERT INTO `shop_docpart_search_tabs` (`name`, `caption`, `enabled`, `order`) VALUES (?, ?, ?, ?)");
    $stmt->execute(['laximo_catalog', 'OEM Catalog', 1, $newOrder]);
    $tabId = $db->lastInsertId();
    echo "Created Laximo catalog tab (id=$tabId, order=$newOrder)\n";
}

// Ensure Laximo tables exist
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
echo "Table epc_laximo_catalogs: OK\n";

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
echo "Table epc_laximo_cache: OK\n";

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
echo "Table epc_laximo_sync_status: OK\n";

// Create CMS page entry for /katalog-laximo (so the URL resolves in the storefront router)
$cmsCheck = $db->prepare("SELECT `id` FROM `content` WHERE `url` = ? AND `is_frontend` = 1");
$cmsCheck->execute(['katalog-laximo']);
$cmsRow = $cmsCheck->fetch(PDO::FETCH_ASSOC);
if ($cmsRow) {
    echo "CMS page 'katalog-laximo' already exists (id=" . $cmsRow['id'] . ")\n";
} else {
    $cmsInsert = $db->prepare("INSERT INTO `content` (`url`, `content_type`, `content`, `value`, `title_tag`, `description_tag`, `keywords_tag`, `author_tag`, `robots_tag`, `main_flag`, `published_flag`, `is_frontend`, `modules_array`, `css_js`) VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)");
    $cmsInsert->execute([
        'katalog-laximo',
        'php',
        '/content/laximo/index.php',
        'OEM Parts Catalog',
        'OEM Parts Catalog',
        'Search original OEM parts by vehicle brand, VIN, or part name. Cross-references with aftermarket analogs.',
        'OEM parts, vehicle catalog, VIN search, original parts',
        '',
        '',
        0,
        1,
        1,
        '[]',
        ''
    ]);
    echo "Created CMS page 'katalog-laximo' (id=" . $db->lastInsertId() . ")\n";
}

// Add Laximo config properties to DP_Config if not present
$config_path = __DIR__ . '/config.php';
$config_content = file_get_contents($config_path);
$added = [];

$props = [
    'laximo_cat_login' => 'au308248',
    'laximo_cat_key' => '5HcskWnQ8FPhy4LNS',
    'laximo_doc_login' => 'au216116',
    'laximo_doc_key' => 'Y34TRgYaUNV42rd',
];

foreach ($props as $prop_name => $prop_value) {
    if (strpos($config_content, '$' . $prop_name) === false) {
        // Add property to DP_Config class
        if (preg_match('/(class\s+DP_Config[^{]*\{)/', $config_content, $m, PREG_OFFSET_CAPTURE)) {
            $pos = (int) $m[1][1] + strlen($m[1][0]);
            $insert = "\n\tpublic \$" . $prop_name . " = '" . addslashes($prop_value) . "';";
            $config_content = substr($config_content, 0, $pos) . $insert . substr($config_content, $pos);
            $added[] = $prop_name;
        }
    }
}

if (!empty($added)) {
    file_put_contents($config_path, $config_content);
    echo "Added config properties: " . implode(', ', $added) . "\n";
} else {
    echo "Config properties already present.\n";
}

echo "\nDone! Laximo catalog integration is ready.\n";
echo "Storefront: /katalog-laximo or via the search tabs on homepage.\n";
echo "CP status: visible under Settings > Config (below Epart catalog status).\n";
echo "API: /api/laximo_proxy.php?action=status (check connection)\n";
echo "Sync: /api/laximo_proxy.php?action=sync (manual sync)\n";
