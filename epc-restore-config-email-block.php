<?php
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
$esc = static fn(string $v): string => str_replace(['\\', "'"], ['\\\\', "\\'"], $v);

$newBlock = "\t//Настройки почты для загрузки прайс-листов\n"
    . "\tpublic \$prices_email_server = 'imap.gmail.com';/*Почтовый сервер*/\n"
    . "\tpublic \$prices_email_encryption = 'ssl';/*Шифрование*/\n"
    . "\tpublic \$prices_email_port = '993';/*Порт*/\n"
    . "\tpublic \$prices_email_username = '" . $esc($email) . "';/*Пользователь*/\n"
    . "\tpublic \$prices_email_password = '" . $esc($password) . "';/*Пароль*/\n";

$pattern = '/\t\/\/Настройки почты для загрузки прайс-листов\r?\n\tpublic \$prices_email_server = [^;]+;\/\*Почтовый сервер\*\/\r?\n\tpublic \$prices_email_encryption = [^;]+;\/\*Шифрование\*\/\r?\n\tpublic \$prices_email_port = [^;]+;\/\*Порт\*\/\r?\n\tpublic \$prices_email_username = [^;]+;\/\*Пользователь\*\/\r?\n\tpublic \$prices_email_password = [^;]+;\/\*Пароль\*\//';

$newContent = preg_replace($pattern, $newBlock, $content, 1, $count);
if ($newContent === null || $count < 1) {
    exit(json_encode(['status' => false, 'message' => 'Could not locate email settings block in config.php', 'hint' => 'pattern failed']));
}

if (file_put_contents($configPath, $newContent) === false) {
    exit(json_encode(['status' => false, 'message' => 'Cannot write config.php']));
}

// syntax check
$output = [];
$code = 0;
exec('php -l ' . escapeshellarg($configPath) . ' 2>&1', $output, $code);

echo json_encode([
    'status' => $code === 0,
    'syntax_check' => implode("\n", $output),
    'block_replaced' => $count,
], JSON_UNESCAPED_UNICODE | JSON_PRETTY_PRINT);
