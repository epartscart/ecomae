#!/usr/bin/env php
<?php
/**
 * Create epc_api_clients once in the ASP.NET TenantRegistry database.
 * Prefers PHP DP_Config credentials → TenantRegistry DB name.
 * Requires ECOMAE_CONFIRM_CREATE_API_CLIENTS_TABLE=YES.
 * Never prints secrets.
 */
declare(strict_types=1);

if (PHP_SAPI !== 'cli') {
	fwrite(STDERR, "CLI only\n");
	exit(1);
}

if (getenv('ECOMAE_CONFIRM_CREATE_API_CLIENTS_TABLE') !== 'YES') {
	fwrite(STDERR, "Refusing: set ECOMAE_CONFIRM_CREATE_API_CLIENTS_TABLE=YES\n");
	exit(2);
}

$envFile = getenv('ECOMAE_ASPNET_ENV_FILE') ?: '/etc/ecomae-aspnet/platform.env';
if (!is_file($envFile)) {
	fwrite(STDERR, "Missing env file: {$envFile}\n");
	exit(1);
}

// Reuse issuer helpers by loading function definitions only via include of a bootstrap.
// Inline minimal parse + connect to avoid running the full issuer.
require_once __DIR__ . '/_smoke_db_bootstrap.php';

$env = smoke_parse_env_file($envFile);
[$pdo, $source] = smoke_open_pdo($env);
fwrite(STDERR, "DB source: {$source}\n");

try {
	$pdo->query('SELECT 1 FROM `epc_api_clients` LIMIT 1');
	fwrite(STDERR, "epc_api_clients already present\n");
	exit(0);
} catch (Throwable $e) {
	fwrite(STDERR, "Creating epc_api_clients (" . $e->getMessage() . ")\n");
}

$sql = <<<'SQL'
CREATE TABLE IF NOT EXISTS `epc_api_clients` (
  `id` INT UNSIGNED NOT NULL AUTO_INCREMENT PRIMARY KEY,
  `client_key_hash` CHAR(64) NOT NULL,
  `client_key_prefix` VARCHAR(32) NOT NULL DEFAULT '',
  `product` ENUM('catalog','price_pro','both') NOT NULL DEFAULT 'catalog',
  `label` VARCHAR(120) NOT NULL DEFAULT '',
  `contact_email` VARCHAR(190) NOT NULL DEFAULT '',
  `active` TINYINT(1) NOT NULL DEFAULT 1,
  `daily_limit` INT NOT NULL DEFAULT 1000,
  `calls_today` INT NOT NULL DEFAULT 0,
  `calls_reset_date` DATE NULL,
  `allowed_actions_json` TEXT NOT NULL,
  `time_created` INT NOT NULL DEFAULT 0,
  `time_updated` INT NOT NULL DEFAULT 0,
  UNIQUE KEY `client_key_hash` (`client_key_hash`),
  KEY `product_active` (`product`, `active`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8
SQL;

try {
	$pdo->exec($sql);
	fwrite(STDERR, "OK created epc_api_clients\n");
	exit(0);
} catch (Throwable $e) {
	fwrite(STDERR, "CREATE failed: " . $e->getMessage() . "\n");
	fwrite(STDERR, "Try as MySQL root:\n");
	fwrite(STDERR, "  mysql -e \"USE <TenantRegistry_db>;\" < DDL from scripts/php/ensure_epc_api_clients_table.php\n");
	exit(1);
}
