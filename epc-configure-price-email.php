<?php
/**
 * One-time: configure IMAP mailbox for price uploads + set a price list to E-mail mode.
 * POST: token, key, email, password, price_id (optional, default FJ-UAE id 6)
 */
declare(strict_types=1);

header('Content-Type: application/json; charset=utf-8');

$deployToken = 'epartscart-deploy-2026';
if (($_POST['token'] ?? $_GET['token'] ?? '') !== $deployToken) {
    http_response_code(403);
    exit(json_encode(['status' => false, 'message' => 'Forbidden']));
}

require_once __DIR__ . '/config.php';
$DP_Config = new DP_Config;

$requestKey = (string)($_POST['key'] ?? $_GET['key'] ?? '');
if ($requestKey !== $DP_Config->tech_key) {
    http_response_code(403);
    exit(json_encode(['status' => false, 'message' => 'Invalid tech_key']));
}

$email = trim((string)($_POST['email'] ?? $_GET['email'] ?? 'epartscart@gmail.com'));
$password = (string)($_POST['password'] ?? $_GET['password'] ?? '');
$priceId = (int)($_POST['price_id'] ?? $_GET['price_id'] ?? 6);

if ($email === '' || $password === '') {
    exit(json_encode(['status' => false, 'message' => 'email and password required']));
}

$configPath = __DIR__ . '/config.php';
if (!is_writable($configPath)) {
    exit(json_encode(['status' => false, 'message' => 'config.php not writable']));
}

$content = file_get_contents($configPath);
if ($content === false) {
    exit(json_encode(['status' => false, 'message' => 'Cannot read config.php']));
}

$esc = static function (string $v): string {
    return str_replace(['\\', "'"], ['\\\\', "\\'"], $v);
};

$replacements = [
    'prices_email_server' => 'imap.gmail.com',
    'prices_email_encryption' => 'ssl',
    'prices_email_port' => '993',
    'prices_email_username' => $email,
    'prices_email_password' => $password,
];
$replaceCounts = [];
foreach ($replacements as $key => $value) {
    $pattern = '/([\\t ]*public \\$' . preg_quote($key, '/') . " = ')[^']*(';)/";
    $new = preg_replace_callback(
        $pattern,
        static function (array $m) use ($value, $esc): string {
            return $m[1] . $esc((string)$value) . $m[2];
        },
        $content,
        1,
        $count
    );
    if ($new !== null && $count > 0) {
        $content = $new;
        $replaceCounts[$key] = $count;
    } else {
        $replaceCounts[$key] = 0;
    }
}

if (file_put_contents($configPath, $content) === false) {
    exit(json_encode(['status' => false, 'message' => 'Cannot write config.php']));
}

try {
    $db = new PDO(
        'mysql:host=' . $DP_Config->host . ';dbname=' . $DP_Config->db,
        $DP_Config->user,
        $DP_Config->password,
        [PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION]
    );
    $db->query('SET NAMES utf8');
} catch (Throwable $e) {
    exit(json_encode(['status' => false, 'message' => 'DB error', 'config_written' => true]));
}

$template = $db->query('SELECT * FROM `shop_docpart_prices` WHERE `id` = 3 LIMIT 1')->fetch(PDO::FETCH_ASSOC);
if (!$template) {
    $template = $db->query('SELECT * FROM `shop_docpart_prices` WHERE `records_count` > 0 LIMIT 1')->fetch(PDO::FETCH_ASSOC);
}

$priceRow = $db->prepare('SELECT * FROM `shop_docpart_prices` WHERE `id` = ? LIMIT 1');
$priceRow->execute([$priceId]);
$price = $priceRow->fetch(PDO::FETCH_ASSOC);
if (!$price) {
    exit(json_encode(['status' => false, 'message' => 'Price list not found', 'price_id' => $priceId]));
}

$cols = $template ?: $price;
$db->prepare(
    'UPDATE `shop_docpart_prices` SET
        `load_mode` = 3,
        `sender_email` = ?,
        `file_name_substring` = ?,
        `strings_to_left` = ?,
        `manufacturer_col` = ?,
        `article_col` = ?,
        `name_col` = ?,
        `exist_col` = ?,
        `price_col` = ?,
        `time_to_exe_col` = ?,
        `storage_col` = ?,
        `min_order_col` = ?,
        `encoding` = ?,
        `separator` = ?,
        `clean_before` = 1
     WHERE `id` = ? LIMIT 1'
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

$passwordOk = (bool)preg_match(
    '/\$prices_email_password = \'' . preg_quote($esc($password), '/') . '\'/',
    $content
);

echo json_encode([
    'status' => true,
    'message' => 'IMAP configured and price list set to E-mail mode',
    'config_replace_counts' => $replaceCounts,
    'password_verified' => $passwordOk,
    'password_length_expected' => strlen($password),
    'imap' => [
        'server' => 'imap.gmail.com',
        'port' => '993',
        'encryption' => 'ssl',
        'username' => $email,
    ],
    'price_list' => [
        'id' => $priceId,
        'name' => (string)$price['name'],
        'load_mode' => 3,
        'sender_email' => $email,
        'file_name_substring' => (string)$price['name'],
    ],
], JSON_UNESCAPED_UNICODE | JSON_PRETTY_PRINT);
