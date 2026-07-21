<?php
if (!isset($_GET['token']) || $_GET['token'] !== 'epartscart-deploy-2026') {
    exit('Forbidden');
}
header('Content-Type: text/plain; charset=utf-8');

try {
    require_once __DIR__ . '/config.php';
    $cfg = new DP_Config();
    $pdo = new PDO(
        'mysql:host=' . $cfg->host . ';dbname=' . $cfg->db . ';charset=utf8',
        $cfg->user,
        $cfg->password,
        array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION, PDO::ATTR_DEFAULT_FETCH_MODE => PDO::FETCH_ASSOC)
    );
} catch (Exception $e) {
    echo "DB connection error: " . $e->getMessage() . "\n";
    exit;
}

function epc_pm_scalar($pdo, $sql, $args = array())
{
    $stmt = $pdo->prepare($sql);
    $stmt->execute($args);
    return $stmt->fetchColumn();
}

function epc_pm_lang($pdo, $key, $en, $ru = null)
{
    $ru = $ru === null ? $en : $ru;
    $pdo->prepare("INSERT IGNORE INTO `lang_text_strings` (`str_key`, `description`, `same`, `is_error`, `is_custom`, `used_found`) VALUES (?, ?, NULL, 0, 1, 1);")->execute(array($key, $en));
    $pdo->prepare("INSERT INTO `lang_text_strings_translation` (`str_key`, `lang_code`, `value`) VALUES (?, 'en', ?) ON DUPLICATE KEY UPDATE `value` = VALUES(`value`);")->execute(array($key, $en));
    $pdo->prepare("INSERT INTO `lang_text_strings_translation` (`str_key`, `lang_code`, `value`) VALUES (?, 'ru', ?) ON DUPLICATE KEY UPDATE `value` = VALUES(`value`);")->execute(array($key, $ru));
}

$pdo->exec("CREATE TABLE IF NOT EXISTS `epc_price_profiles` (
    `id` int(11) NOT NULL AUTO_INCREMENT,
    `code` varchar(64) NOT NULL,
    `group_id` int(11) NOT NULL,
    `created_at` int(11) NOT NULL DEFAULT 0,
    PRIMARY KEY (`id`),
    UNIQUE KEY `x_code` (`code`),
    UNIQUE KEY `x_group_id` (`group_id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8;");

$pdo->exec("CREATE TABLE IF NOT EXISTS `epc_price_profile_brand_rules` (
    `id` int(11) NOT NULL AUTO_INCREMENT,
    `group_id` int(11) NOT NULL,
    `manufacturer` varchar(255) NOT NULL,
    `margin_percent` decimal(10,2) NOT NULL DEFAULT 0.00,
    `visible` tinyint(1) NOT NULL DEFAULT 1,
    `updated_at` int(11) NOT NULL DEFAULT 0,
    PRIMARY KEY (`id`),
    UNIQUE KEY `x_group_brand` (`group_id`, `manufacturer`),
    KEY `x_manufacturer` (`manufacturer`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8;");

$pdo->exec("CREATE TABLE IF NOT EXISTS `epc_price_settings` (
    `setting_key` varchar(128) NOT NULL,
    `setting_value` text,
    PRIMARY KEY (`setting_key`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8;");

$pdo->exec("CREATE TABLE IF NOT EXISTS `epc_price_storage_rules` (
    `id` int(11) NOT NULL AUTO_INCREMENT,
    `storage_id` int(11) NOT NULL,
    `margin_percent` decimal(10,2) NOT NULL DEFAULT 0.00,
    `visible` tinyint(1) NOT NULL DEFAULT 1,
    `updated_at` int(11) NOT NULL DEFAULT 0,
    PRIMARY KEY (`id`),
    UNIQUE KEY `x_storage` (`storage_id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8;");

$pdo->exec("CREATE TABLE IF NOT EXISTS `epc_price_storage_brand_rules` (
    `id` int(11) NOT NULL AUTO_INCREMENT,
    `storage_id` int(11) NOT NULL,
    `manufacturer` varchar(255) NOT NULL,
    `margin_percent` decimal(10,2) NOT NULL DEFAULT 0.00,
    `visible` tinyint(1) NOT NULL DEFAULT 1,
    `updated_at` int(11) NOT NULL DEFAULT 0,
    PRIMARY KEY (`id`),
    UNIQUE KEY `x_storage_brand` (`storage_id`, `manufacturer`),
    KEY `x_manufacturer` (`manufacturer`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8;");

$pdo->exec("CREATE TABLE IF NOT EXISTS `epc_price_storage_article_rules` (
    `id` int(11) NOT NULL AUTO_INCREMENT,
    `storage_id` int(11) NOT NULL,
    `manufacturer` varchar(255) NOT NULL,
    `article` varchar(64) NOT NULL,
    `margin_percent` decimal(10,2) NOT NULL DEFAULT 0.00,
    `visible` tinyint(1) NOT NULL DEFAULT 1,
    `updated_at` int(11) NOT NULL DEFAULT 0,
    PRIMARY KEY (`id`),
    UNIQUE KEY `x_storage_brand_article` (`storage_id`, `manufacturer`, `article`),
    KEY `x_article` (`article`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8;");
echo "OK storage pricing tables\n";

$now = time();
$profiles = array(
    'retail' => array('EPC_PROFILE_RETAIL', 'Retail', 'Retail'),
    'wholesale' => array('EPC_PROFILE_WHOLESALE', 'Wholesale', 'Wholesale'),
    'cis' => array('EPC_PROFILE_CIS', 'CIS', 'CIS'),
    'gcc' => array('EPC_PROFILE_GCC', 'GCC', 'GCC'),
);

$maxGroupId = (int)epc_pm_scalar($pdo, "SELECT IFNULL(MAX(`id`), 0) FROM `groups`;");
$order = 10;
foreach ($profiles as $code => $data) {
    list($key, $en, $ru) = $data;
    epc_pm_lang($pdo, $key, $en, $ru);
    $groupId = (int)epc_pm_scalar($pdo, "SELECT `group_id` FROM `epc_price_profiles` WHERE `code` = ? LIMIT 1;", array($code));
    if ($groupId <= 0) {
        $existing = (int)epc_pm_scalar($pdo, "SELECT `id` FROM `groups` WHERE `value` = ? LIMIT 1;", array($key));
        if ($existing > 0) {
            $groupId = $existing;
        } else {
            $maxGroupId++;
            $pdo->prepare("INSERT INTO `groups` (`id`, `value`, `count`, `level`, `parent`, `unblocked`, `for_guests`, `for_registrated`, `for_backend`, `for_percentage`, `description`, `order`) VALUES (?, ?, 0, 2, 1, 1, 0, 0, 0, 0, ?, ?);")->execute(array($maxGroupId, $key, $key, $order));
            $groupId = $maxGroupId;
        }
        $pdo->prepare("INSERT INTO `epc_price_profiles` (`code`, `group_id`, `created_at`) VALUES (?, ?, ?) ON DUPLICATE KEY UPDATE `group_id` = VALUES(`group_id`);")->execute(array($code, $groupId, $now));
    }
    $order++;
}
$pdo->exec("UPDATE `groups` SET `count` = (SELECT COUNT(*) FROM (SELECT `id` FROM `groups` WHERE `parent` = 1) AS x) WHERE `id` = 1;");

$pdo->prepare("INSERT INTO `epc_price_settings` (`setting_key`, `setting_value`) VALUES ('vat_percent', '5.00') ON DUPLICATE KEY UPDATE `setting_value` = `setting_value`;")->execute();

epc_pm_lang($pdo, 'EPC_PRICE_MANAGEMENT', 'Price profiles / brand pricing', 'Price profiles / brand pricing');
epc_pm_lang($pdo, 'EPC_PRICE_MANAGEMENT_DESC', 'Customer profiles, brand margins, visibility and VAT', 'Customer profiles, brand margins, visibility and VAT');

$shopParentId = (int)epc_pm_scalar($pdo, "SELECT `id` FROM `content` WHERE `is_frontend` = 0 AND `url` = 'shop' LIMIT 1;");
$routeId = (int)epc_pm_scalar($pdo, "SELECT `id` FROM `content` WHERE `is_frontend` = 0 AND `url` = 'shop/price-management' LIMIT 1;");
if ($routeId <= 0) {
    $pdo->prepare("INSERT INTO `content` (`count`, `url`, `level`, `alias`, `value`, `parent`, `description`, `is_frontend`, `content_type`, `content`, `title_tag`, `description_tag`, `keywords_tag`, `author_tag`, `main_flag`, `modules_array`, `css_js`, `robots_tag`, `system_flag`, `published_flag`, `open`, `time_created`, `time_edited`, `order`) VALUES (0, 'shop/price-management', 2, 'price-management', 'EPC_PRICE_MANAGEMENT', ?, 'EPC_PRICE_MANAGEMENT_DESC', 0, 'php', '/<backend_dir>/content/shop/pricing/price_management.php', 'EPC_PRICE_MANAGEMENT', 'EPC_PRICE_MANAGEMENT_DESC', '0', '0', 0, '[]', '', '', 1, 1, 0, ?, ?, 47);")->execute(array($shopParentId, $now, $now));
    $routeId = (int)$pdo->lastInsertId();
} else {
    $pdo->prepare("UPDATE `content` SET `content_type` = 'php', `content` = '/<backend_dir>/content/shop/pricing/price_management.php', `published_flag` = 1 WHERE `id` = ?;")->execute(array($routeId));
}
echo "CP route ready: {$routeId}\n";

$itemsGroup = (int)epc_pm_scalar($pdo, "SELECT `items_group` FROM `control_items` WHERE `url` = '/<backend>/shop/prices' LIMIT 1;");
if ($itemsGroup <= 0) {
    $itemsGroup = 4;
}
$controlExists = (int)epc_pm_scalar($pdo, "SELECT `id` FROM `control_items` WHERE `url` = '/<backend>/shop/price-management' LIMIT 1;");
if ($controlExists <= 0) {
    $pdo->prepare("INSERT INTO `control_items` (`items_group`, `caption`, `url`, `img`, `order`, `background_color`, `fontawesome_class`, `target`, `show_anyway`) VALUES (?, 'EPC_PRICE_MANAGEMENT', '/<backend>/shop/price-management', '', 33, '#0ea5e9', 'fas fa-tags', '', 0);")->execute(array($itemsGroup));
}
echo "CP menu ready\n";

$groups = $pdo->query("SELECT DISTINCT `group_id` FROM `content_access` WHERE `content_id` IN (SELECT `id` FROM `content` WHERE `is_frontend` = 0 AND (`url` = 'shop' OR `url` = 'shop/prices' OR `url` = 'shop/orders/orders'));")->fetchAll(PDO::FETCH_COLUMN);
if (!$groups) {
    $groups = array(1);
}
foreach ($groups as $groupId) {
    $pdo->prepare("INSERT IGNORE INTO `content_access` (`content_id`, `group_id`) VALUES (?, ?);")->execute(array($routeId, (int)$groupId));
}
echo "Access ready\n";

echo "Done\n";
@unlink(__FILE__);
?>
