<?php
declare(strict_types=1);
header('Content-Type: application/json; charset=utf-8');

$deployToken = 'epartscart-deploy-2026';
if (($_POST['token'] ?? '') !== $deployToken) {
    http_response_code(403);
    exit(json_encode(['status' => false, 'message' => 'Forbidden']));
}

require_once __DIR__ . '/config.php';
$DP_Config = new DP_Config;
if ((string)($_POST['key'] ?? '') !== $DP_Config->tech_key) {
    http_response_code(403);
    exit(json_encode(['status' => false, 'message' => 'Invalid tech_key']));
}

$email = trim((string)($_POST['email'] ?? 'epartscart@gmail.com'));
$priceId = (int)($_POST['price_id'] ?? 6);

$db = new PDO(
    'mysql:host=' . $DP_Config->host . ';dbname=' . $DP_Config->db,
    $DP_Config->user,
    $DP_Config->password,
    [PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION]
);
$db->query('SET NAMES utf8');

$template = $db->query('SELECT * FROM `shop_docpart_prices` WHERE `id` = 3 LIMIT 1')->fetch(PDO::FETCH_ASSOC);
$price = $db->prepare('SELECT * FROM `shop_docpart_prices` WHERE `id` = ?');
$price->execute([$priceId]);
$price = $price->fetch(PDO::FETCH_ASSOC);
if (!$price) {
    exit(json_encode(['status' => false, 'message' => 'Price not found']));
}
$cols = $template ?: $price;

$db->prepare(
    'UPDATE `shop_docpart_prices` SET `load_mode`=3, `sender_email`=?, `file_name_substring`=?,
     `strings_to_left`=?, `manufacturer_col`=?, `article_col`=?, `name_col`=?, `exist_col`=?, `price_col`=?,
     `time_to_exe_col`=?, `storage_col`=?, `min_order_col`=?, `encoding`=?, `separator`=?, `clean_before`=1
     WHERE `id`=?'
)->execute([
    $email,
    (string)$price['name'],
    (int)($cols['strings_to_left'] ?? 1),
    (int)($cols['manufacturer_col'] ?? 1),
    (int)($cols['article_col'] ?? 2),
    (int)($cols['name_col'] ?? 3),
    (int)($cols['exist_col'] ?? 4),
    (int)($cols['price_col'] ?? 5),
    (int)($cols['time_to_exe_col'] ?? 0),
    (int)($cols['storage_col'] ?? 0),
    (int)($cols['min_order_col'] ?? 0),
    (string)($cols['encoding'] ?? 'utf-8'),
    (string)($cols['separator'] ?? ','),
    $priceId,
]);

echo json_encode(['status' => true, 'price_id' => $priceId, 'name' => $price['name'], 'load_mode' => 3], JSON_PRETTY_PRINT);
