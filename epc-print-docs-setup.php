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

function epc_print_doc_scalar($pdo, $sql, $params = array())
{
    $stmt = $pdo->prepare($sql);
    $stmt->execute($params);
    return $stmt->fetchColumn();
}

function epc_print_doc_exec($pdo, $sql, $params = array())
{
    $stmt = $pdo->prepare($sql);
    $stmt->execute($params);
    return $stmt;
}

$pdo->exec("CREATE TABLE IF NOT EXISTS `shop_print_docs` (
    `id` int(11) NOT NULL AUTO_INCREMENT,
    `caption` varchar(255) DEFAULT NULL,
    `description` text,
    `name` varchar(255) DEFAULT NULL,
    `post_params` text,
    `parameters_description` text,
    `parameters_values` text,
    `control_available` tinyint(4) DEFAULT '1' COMMENT 'Element available in control panel',
    PRIMARY KEY (`id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='Print document types';");

$pdo->exec("CREATE TABLE IF NOT EXISTS `shop_print_docs_wholesaler` (
    `id` int(11) NOT NULL AUTO_INCREMENT COMMENT 'ID',
    `doc_name` varchar(255) CHARACTER SET utf8 DEFAULT NULL COMMENT 'Document name from shop_print_docs',
    `office_id` int(11) DEFAULT NULL COMMENT 'Office ID',
    `parameters_values` text CHARACTER SET utf8 COMMENT 'Document settings',
    PRIMARY KEY (`id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='Office document print settings';");
echo "Print document tables ready\n";

$seedPath = __DIR__ . '/epc-print-docs-seed.sql';
if (is_file($seedPath)) {
    $seedSql = file_get_contents($seedPath);
    if ($seedSql !== false && trim($seedSql) !== '') {
        $pdo->exec($seedSql);
        echo "Print document definitions seeded\n";
    }
}

$strings = array(
    667 => array('ru' => 'Модуль печати документов', 'en' => 'Document print module'),
    668 => array('ru' => 'Модуль печати документов', 'en' => 'Document print module'),
    669 => array('ru' => 'Модуль печати документов', 'en' => 'Document print module'),
    670 => array('ru' => 'Модуль печати документов', 'en' => 'Document print module'),
    671 => array('ru' => 'Настройка печати документа', 'en' => 'Document print settings'),
    672 => array('ru' => 'Настройка печати документа', 'en' => 'Document print settings'),
    673 => array('ru' => 'Настройка печати документа', 'en' => 'Document print settings'),
    674 => array('ru' => 'Настройка печати документа', 'en' => 'Document print settings'),
    797 => array('ru' => 'Модуль печати документов', 'en' => 'Document print module'),
    1947 => array('ru' => 'Товарный чек', 'en' => 'Sales receipt'),
    1948 => array('ru' => 'Простой товарный чек с переченем заказываемых товаров, ценой и подписью за получение.', 'en' => 'Simple sales receipt with ordered goods, prices, and receipt signature.'),
    1949 => array('ru' => 'Счет на оплату', 'en' => 'Invoice for payment'),
    1950 => array('ru' => 'Документ, содержащий платежные реквизиты продавца, по которым покупатель осуществляет перевод денежных средств за перечисленные в счете товары.', 'en' => 'Document with seller payment details for the invoiced goods.'),
    1951 => array('ru' => 'Накладная ТОРГ-12', 'en' => 'TORG-12 consignment note'),
    1952 => array('ru' => 'Первичный документ, который применяется для оформления продажи товара сторонней организации или ИП.', 'en' => 'Primary document used for goods sales to a company or individual entrepreneur.'),
    1953 => array('ru' => 'УПД', 'en' => 'Universal transfer document'),
    1954 => array('ru' => 'Универсальный передаточный документ, объединяет накладную на реализацию товара и счет-фактуру.', 'en' => 'Universal transfer document combining goods invoice and VAT invoice.'),
    1955 => array('ru' => 'УПД (с 01.07.2021)', 'en' => 'Universal transfer document from 01.07.2021'),
    1956 => array('ru' => 'Универсальный передаточный документ, объединяет накладную на реализацию товара и счет-фактуру.', 'en' => 'Universal transfer document combining goods invoice and VAT invoice.'),
    3794 => array('ru' => 'Список документов для печати', 'en' => 'Print document list'),
    3795 => array('ru' => 'Здесь перечислены заготовленные разработчиками документы для печати. Нажав на название документа, откроется окно для его настроек', 'en' => 'Prepared print documents are listed here. Click a document name to open its settings.')
);

$stringStmt = $pdo->prepare("INSERT IGNORE INTO `lang_text_strings` (`id`, `str_key`, `description`, `same`, `is_error`, `is_custom`, `used_found`) VALUES (?, ?, ?, NULL, 0, 0, 1);");
$translationStmt = $pdo->prepare("INSERT IGNORE INTO `lang_text_strings_translation` (`str_key`, `lang_code`, `value`) VALUES (?, ?, ?);");
foreach ($strings as $id => $translations) {
    $stringStmt->execute(array($id, (string)$id, $translations['ru']));
    foreach ($translations as $language => $text) {
        $translationStmt->execute(array((string)$id, $language, $text));
    }
}
echo "Language labels ready\n";

epc_print_doc_exec(
    $pdo,
    "INSERT INTO `control_items` (`items_group`, `caption`, `url`, `img`, `order`, `background_color`, `fontawesome_class`, `target`, `show_anyway`)
     SELECT 6, '797', '/<backend>/shop/modul-pechati-dokumentov', '', 1000, '#27ae60', 'fas fa-print', '', 0
     FROM DUAL WHERE NOT EXISTS (SELECT 1 FROM `control_items` WHERE `url` = '/<backend>/shop/modul-pechati-dokumentov' LIMIT 1);"
);
echo "Control panel button ready\n";

$now = time();
$shopParentId = (int)epc_print_doc_scalar($pdo, "SELECT `id` FROM `content` WHERE `is_frontend` = 0 AND `url` = 'shop' LIMIT 1;");
if ($shopParentId <= 0) {
    $shopParentId = 0;
}

$mainId = (int)epc_print_doc_scalar($pdo, "SELECT `id` FROM `content` WHERE `is_frontend` = 0 AND `url` = 'shop/modul-pechati-dokumentov' LIMIT 1;");
if ($mainId <= 0) {
    epc_print_doc_exec($pdo, "INSERT INTO `content` (`count`, `url`, `level`, `alias`, `value`, `parent`, `description`, `is_frontend`, `content_type`, `content`, `title_tag`, `description_tag`, `keywords_tag`, `author_tag`, `main_flag`, `modules_array`, `css_js`, `robots_tag`, `system_flag`, `published_flag`, `open`, `time_created`, `time_edited`, `order`) VALUES (1, 'shop/modul-pechati-dokumentov', 2, 'modul-pechati-dokumentov', '667', ?, '668', 0, 'php', '/<backend_dir>/content/shop/print_docs/print_docs_manager.php', '669', '670', '0', '0', 0, '[]', '', '', 1, 1, 1, ?, ?, 43);", array($shopParentId, $now, $now));
    $mainId = (int)$pdo->lastInsertId();
}

$tuningId = (int)epc_print_doc_scalar($pdo, "SELECT `id` FROM `content` WHERE `is_frontend` = 0 AND `url` = 'shop/modul-pechati-dokumentov/nastrojka-pechati-dokumenta' LIMIT 1;");
if ($tuningId <= 0) {
    epc_print_doc_exec($pdo, "INSERT INTO `content` (`count`, `url`, `level`, `alias`, `value`, `parent`, `description`, `is_frontend`, `content_type`, `content`, `title_tag`, `description_tag`, `keywords_tag`, `author_tag`, `main_flag`, `modules_array`, `css_js`, `robots_tag`, `system_flag`, `published_flag`, `open`, `time_created`, `time_edited`, `order`) VALUES (0, 'shop/modul-pechati-dokumentov/nastrojka-pechati-dokumenta', 3, 'nastrojka-pechati-dokumenta', '671', ?, '672', 0, 'php', '/<backend_dir>/content/shop/print_docs/print_doc_tuning.php', '673', '674', '0', '0', 0, '[]', '<script src=\"/lib/multiple_select/jquery.multiple.select.js\"></script>\n<link href=\"/lib/multiple_select/multiple-select.css\" rel=\"stylesheet\">\n', '', 1, 1, 0, ?, ?, 44);", array($mainId, $now, $now));
    $tuningId = (int)$pdo->lastInsertId();
}
echo "CP content routes ready: {$mainId}, {$tuningId}\n";

$groups = $pdo->query("SELECT DISTINCT `group_id` FROM `content_access` WHERE `content_id` IN (SELECT `id` FROM `content` WHERE `is_frontend` = 0 AND (`url` = 'shop' OR `url` = 'shop/orders/orders' OR `url` = 'control/config'));")->fetchAll(PDO::FETCH_COLUMN);
if (!$groups || count($groups) === 0) {
    $groups = array(1);
}
$accessStmt = $pdo->prepare("INSERT INTO `content_access` (`content_id`, `group_id`) SELECT ?, ? FROM DUAL WHERE NOT EXISTS (SELECT 1 FROM `content_access` WHERE `content_id` = ? AND `group_id` = ? LIMIT 1);");
foreach ($groups as $groupId) {
    $groupId = (int)$groupId;
    $accessStmt->execute(array($mainId, $groupId, $mainId, $groupId));
    $accessStmt->execute(array($tuningId, $groupId, $tuningId, $groupId));
}
echo "Access rights synced\n";

@unlink($seedPath);
@unlink(__FILE__);
echo "Done\n";
?>
