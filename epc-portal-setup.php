<?php
/**
 * Portal + Taxofinca install setup.
 * https://www.epartscart.com/epc-portal-setup.php?token=...
 * https://www.taxofinca.com/epc-portal-setup.php?token=...
 */
require_once __DIR__ . '/epc_deploy_auth.php';
epc_deploy_require_token(false, 'secret');
header('Content-Type: text/plain; charset=utf-8');
header('Cache-Control: no-store');

require_once __DIR__ . '/config.php';
require_once __DIR__ . '/content/general_pages/epc_portal.php';

$cfg = new DP_Config();
epc_portal_apply_config($cfg);

$host = epc_portal_host();
$site = epc_portal_site_profile();
$industry = epc_portal_industry();

echo "EPC Multi-Industry Portal Setup\n";
echo "Host: {$host}\n";
echo "Industry: " . $industry['name'] . " ({$industry['code']})\n";
echo "System: " . (isset($site['system_name']) ? $site['system_name'] : '') . "\n";
echo "Domain: {$cfg->domain_path}\n";
echo "Database: {$cfg->db}\n\n";

try {
	$pdo = new PDO(
		'mysql:host=' . $cfg->host . ';dbname=' . $cfg->db,
		$cfg->user,
		$cfg->password,
		array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION)
	);
	$pdo->query('SET NAMES utf8');
	echo "DB connection: OK\n";
} catch (Exception $e) {
	echo "DB connection: FAILED — " . $e->getMessage() . "\n";
	if ($host === 'www.taxofinca.com' || $host === 'taxofinca.com') {
		echo "\nCreate database user in CloudPanel and copy config.local.taxofinca.php to config.local.php with the DB password.\n";
	}
	exit(1);
}

$pdo->exec(
	'CREATE TABLE IF NOT EXISTS `epc_portal_industry` (
		`code` VARCHAR(32) NOT NULL PRIMARY KEY,
		`name` VARCHAR(120) NOT NULL,
		`theme_json` TEXT NULL,
		`active` TINYINT(1) NOT NULL DEFAULT 1,
		`sort_order` INT NOT NULL DEFAULT 0,
		`updated_at` INT NOT NULL DEFAULT 0
	) ENGINE=InnoDB DEFAULT CHARSET=utf8'
);

$now = time();
$sort = 0;
foreach (epc_portal_industries() as $row) {
	$sort += 10;
	$stmt = $pdo->prepare(
		'INSERT INTO `epc_portal_industry` (`code`, `name`, `theme_json`, `active`, `sort_order`, `updated_at`)
		VALUES (?, ?, ?, 1, ?, ?)
		ON DUPLICATE KEY UPDATE `name` = VALUES(`name`), `theme_json` = VALUES(`theme_json`), `updated_at` = VALUES(`updated_at`)'
	);
	$stmt->execute(array(
		$row['code'],
		$row['name'],
		json_encode(isset($row['theme']) ? $row['theme'] : array()),
		$sort,
		$now,
	));
}

echo "Industries table: " . count(epc_portal_industries()) . " rows synced\n";
echo "CP industry filter: use dropdown in top navigation bar\n";
echo "Done.\n";
