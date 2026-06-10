<?php
if (!isset($_GET['token']) || $_GET['token'] !== 'epartscart-deploy-2026') {
    exit('Forbidden');
}
header('Content-Type: text/plain; charset=utf-8');

require_once __DIR__ . '/config.php';
$cfg = new DP_Config();
$pdo = new PDO('mysql:host='.$cfg->host.';dbname='.$cfg->db.';charset=utf8', $cfg->user, $cfg->password, array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION));

function epc_bulk_scalar($pdo, $sql, $args = array()) {
    $stmt = $pdo->prepare($sql);
    $stmt->execute($args);
    return $stmt->fetchColumn();
}

function epc_bulk_lang($pdo, $key, $value) {
    $pdo->prepare("INSERT IGNORE INTO `lang_text_strings` (`str_key`, `description`, `same`, `is_error`, `is_custom`, `used_found`) VALUES (?, ?, NULL, 0, 1, 1);")->execute(array($key, $value));
    $pdo->prepare("INSERT INTO `lang_text_strings_translation` (`str_key`, `lang_code`, `value`) VALUES (?, 'en', ?) ON DUPLICATE KEY UPDATE `value` = VALUES(`value`);")->execute(array($key, $value));
    $pdo->prepare("INSERT INTO `lang_text_strings_translation` (`str_key`, `lang_code`, `value`) VALUES (?, 'ru', ?) ON DUPLICATE KEY UPDATE `value` = VALUES(`value`);")->execute(array($key, $value));
}

epc_bulk_lang($pdo, 'EPC_BULK_UPLOAD', 'Bulk spare parts upload');
epc_bulk_lang($pdo, 'EPC_BULK_UPLOAD_DESC', 'Upload Excel/CSV list, compare prices and add selected spare parts to cart');

$shopId = (int)epc_bulk_scalar($pdo, "SELECT `id` FROM `content` WHERE `is_frontend` = 1 AND `url` = 'shop' LIMIT 1;");
if ($shopId <= 0) {
    $shopId = 0;
}
$now = time();
$routeId = (int)epc_bulk_scalar($pdo, "SELECT `id` FROM `content` WHERE `is_frontend` = 1 AND `url` = 'shop/bulk-upload' LIMIT 1;");
if ($routeId <= 0) {
    $pdo->prepare("INSERT INTO `content` (`count`, `url`, `level`, `alias`, `value`, `parent`, `description`, `is_frontend`, `content_type`, `content`, `title_tag`, `description_tag`, `keywords_tag`, `author_tag`, `main_flag`, `modules_array`, `css_js`, `robots_tag`, `system_flag`, `published_flag`, `open`, `time_created`, `time_edited`, `order`) VALUES (0, 'shop/bulk-upload', 2, 'bulk-upload', 'EPC_BULK_UPLOAD', ?, 'EPC_BULK_UPLOAD_DESC', 1, 'php', '/content/shop/bulk_upload/bulk_upload.php', 'EPC_BULK_UPLOAD', 'EPC_BULK_UPLOAD_DESC', '0', '0', 0, '[]', '', 'noindex, follow', 1, 1, 0, ?, ?, 90);")->execute(array($shopId, $now, $now));
    $routeId = (int)$pdo->lastInsertId();
} else {
    $pdo->prepare("UPDATE `content` SET `content_type` = 'php', `content` = '/content/shop/bulk_upload/bulk_upload.php', `published_flag` = 1 WHERE `id` = ?;")->execute(array($routeId));
}

$groups = $pdo->query("SELECT `id` FROM `groups` WHERE `for_registrated` = 1 OR `for_backend` = 1;")->fetchAll(PDO::FETCH_COLUMN);
if (!$groups) {
    $groups = array(1, 2, 3);
}
foreach ($groups as $groupId) {
    $pdo->prepare("INSERT IGNORE INTO `content_access` (`content_id`, `group_id`) VALUES (?, ?);")->execute(array($routeId, (int)$groupId));
}

echo "Bulk upload route ready: /shop/bulk-upload (content id {$routeId})\n";
echo "Done\n";
@unlink(__FILE__);
?>
