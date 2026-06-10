<?php
declare(strict_types=1);

if (($_GET['token'] ?? '') !== 'epartscart-deploy-2026') {
    http_response_code(403);
    exit('Forbidden');
}

header('Content-Type: text/plain; charset=utf-8');

require __DIR__ . '/config.php';

$cfg = new DP_Config();
$db = new mysqli($cfg->host, $cfg->user, $cfg->password, $cfg->db);
if ($db->connect_errno) {
    exit('DB connect failed: ' . $db->connect_error);
}

$email = 'epartscart@gmail.com';
$passwordHash = md5('5wzNcBkJs4aueTm' . $cfg->secret_succession);
$now = (string) time();

$stmt = $db->prepare(
    "INSERT INTO users
        (reg_variant, email, email_confirmed, email_code_attempts, email_code_send_lock_expired,
         phone_confirmed, phone_code_attempts, phone_code_send_lock_expired, password, unlocked,
         time_registered, time_last_visit, admin_created)
     VALUES (1, ?, 1, 0, 0, 0, 0, 0, ?, 1, ?, ?, 1)
     ON DUPLICATE KEY UPDATE
        email_confirmed = 1,
        password = VALUES(password),
        unlocked = 1,
        admin_created = 1"
);
$stmt->bind_param('ssss', $email, $passwordHash, $now, $now);
if (!$stmt->execute()) {
    exit('User upsert failed: ' . $stmt->error);
}

$userId = (int) ($db->insert_id ?: 0);
if ($userId === 0) {
    $lookup = $db->prepare('SELECT user_id FROM users WHERE email = ? LIMIT 1');
    $lookup->bind_param('s', $email);
    $lookup->execute();
    $result = $lookup->get_result();
    $row = $result ? $result->fetch_assoc() : null;
    $userId = (int) ($row['user_id'] ?? 0);
}

if ($userId <= 0) {
    exit('Could not determine user_id');
}

$groupId = 3;
$bind = $db->prepare(
    'INSERT INTO users_groups_bind (user_id, group_id)
     SELECT ?, ?
     WHERE NOT EXISTS (
        SELECT 1 FROM users_groups_bind WHERE user_id = ? AND group_id = ?
     )'
);
$bind->bind_param('iiii', $userId, $groupId, $userId, $groupId);
if (!$bind->execute()) {
    exit('Group bind failed: ' . $bind->error);
}

echo "Demo CP user ready: {$email}, user_id={$userId}, group={$groupId}\n";
