<?php
/**
 * Newsletter subscription AJAX endpoint.
 * Saves email to epc_newsletter_subscribers table (auto-created if missing).
 */
header('Content-Type: application/json; charset=utf-8');

define('_ASTEXE_', 1);
require_once $_SERVER['DOCUMENT_ROOT'] . '/config.php';

$email = isset($_POST['email']) ? trim((string) $_POST['email']) : '';
if ($email === '' || !filter_var($email, FILTER_VALIDATE_EMAIL)) {
	http_response_code(400);
	echo json_encode(array('ok' => false, 'error' => 'Invalid email'));
	exit;
}

try {
	if (!isset($db_link) || !($db_link instanceof PDO)) {
		require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_portal_tenant.php';
		$db_link = epc_portal_tenant_pdo();
	}
	if (!($db_link instanceof PDO)) {
		throw new \RuntimeException('Database unavailable');
	}

	$db_link->exec("CREATE TABLE IF NOT EXISTS `epc_newsletter_subscribers` (
		`id` INT UNSIGNED NOT NULL AUTO_INCREMENT PRIMARY KEY,
		`email` VARCHAR(255) NOT NULL,
		`subscribed_at` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
		`status` ENUM('active','unsubscribed') NOT NULL DEFAULT 'active',
		`ip_address` VARCHAR(45) DEFAULT NULL,
		`source` VARCHAR(50) DEFAULT 'storefront',
		UNIQUE KEY `uk_email` (`email`)
	) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4");

	$stmt = $db_link->prepare("INSERT INTO `epc_newsletter_subscribers` (`email`, `ip_address`, `source`)
		VALUES (?, ?, 'storefront')
		ON DUPLICATE KEY UPDATE `status` = 'active', `subscribed_at` = NOW()");
	$ip = isset($_SERVER['REMOTE_ADDR']) ? substr((string) $_SERVER['REMOTE_ADDR'], 0, 45) : null;
	$stmt->execute(array($email, $ip));

	echo json_encode(array('ok' => true));
} catch (\Throwable $e) {
	http_response_code(500);
	echo json_encode(array('ok' => false, 'error' => 'Server error'));
}
