<?php
/**
 * Repair/set prices_email_* in config.php without loading DP_Config (if config is broken).
 */
declare(strict_types=1);
header('Content-Type: application/json; charset=utf-8');

$deployToken = 'epartscart-deploy-2026';
if (($_POST['token'] ?? '') !== $deployToken) {
    http_response_code(403);
    exit(json_encode(['status' => false, 'message' => 'Forbidden']));
}

$configPath = __DIR__ . '/config.php';
$content = file_get_contents($configPath);
if ($content === false) {
    exit(json_encode(['status' => false, 'message' => 'Cannot read config.php']));
}

if (!preg_match("/public \\\$tech_key = '([^']*)';/", $content, $m) || $m[1] !== (string)($_POST['key'] ?? '')) {
    http_response_code(403);
    exit(json_encode(['status' => false, 'message' => 'Invalid tech_key']));
}

$email = trim((string)($_POST['email'] ?? 'epartscart@gmail.com'));
$password = (string)($_POST['password'] ?? '');
if ($password === '') {
    exit(json_encode(['status' => false, 'message' => 'password required']));
}

$esc = static fn(string $v): string => str_replace(['\\', "'"], ['\\\\', "\\'"], $v);

$map = [
    'prices_email_server' => 'imap.gmail.com',
    'prices_email_encryption' => 'ssl',
    'prices_email_port' => '993',
    'prices_email_username' => $email,
    'prices_email_password' => $password,
];

$counts = [];
foreach ($map as $key => $value) {
    $pattern = '/([\\t ]*public \\$' . preg_quote($key, '/') . " = ')[^']*(';)/";
    $content = preg_replace_callback(
        $pattern,
        static fn(array $mm) => $mm[1] . $esc((string)$value) . $mm[2],
        $content,
        1,
        $count
    );
    $counts[$key] = $count;
}

if (file_put_contents($configPath, $content) === false) {
    exit(json_encode(['status' => false, 'message' => 'Cannot write config.php']));
}

$ok = (bool)preg_match('/\$prices_email_password = \'' . preg_quote($esc($password), '/') . '\'/', $content);

echo json_encode([
    'status' => true,
    'config_replace_counts' => $counts,
    'password_verified' => $ok,
    'password_length_expected' => strlen($password),
], JSON_UNESCAPED_UNICODE | JSON_PRETTY_PRINT);
