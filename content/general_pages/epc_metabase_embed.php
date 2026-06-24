<?php
/**
 * P1 #19 — Metabase Embed POC
 *
 * Generates signed JWT tokens for embedding Metabase dashboards in CP/ERP.
 * Each tenant gets scoped access to their own data via Metabase row-level security.
 *
 * Setup: Configure Metabase instance URL and secret key in platform settings.
 * Then map tenant site_keys to Metabase dashboard IDs.
 */
declare(strict_types=1);
if (!defined('_ASTEXE_')) { define('_ASTEXE_', 1); }

define('EPC_METABASE_VERSION', '1.0.0');

/* ─── Schema ─── */

function epc_metabase_ensure_schema(PDO $pdo): void
{
	static $done = false;
	if ($done) return;
	$done = true;

	$pdo->exec("
		CREATE TABLE IF NOT EXISTS `epc_metabase_config` (
			`id`              INT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
			`site_key`        VARCHAR(64)    NOT NULL DEFAULT '__platform__',
			`metabase_url`    VARCHAR(256)   NOT NULL DEFAULT '',
			`secret_key`      VARCHAR(256)   NOT NULL DEFAULT '',
			`active`          TINYINT(1)     NOT NULL DEFAULT 0,
			`created_at`      DATETIME       NOT NULL DEFAULT CURRENT_TIMESTAMP,
			`updated_at`      DATETIME       NULL ON UPDATE CURRENT_TIMESTAMP,
			UNIQUE KEY `site` (`site_key`)
		) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4
	");

	$pdo->exec("
		CREATE TABLE IF NOT EXISTS `epc_metabase_dashboards` (
			`id`              INT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
			`site_key`        VARCHAR(64)    NOT NULL,
			`dashboard_id`    INT UNSIGNED   NOT NULL,
			`dashboard_name`  VARCHAR(128)   NOT NULL DEFAULT '',
			`category`        VARCHAR(32)    NOT NULL DEFAULT 'finance',
			`active`          TINYINT(1)     NOT NULL DEFAULT 1,
			`created_at`      DATETIME       NOT NULL DEFAULT CURRENT_TIMESTAMP,
			INDEX `idx_site` (`site_key`)
		) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4
	");
}

/* ─── JWT Token Generation ─── */

function epc_metabase_jwt_encode(array $payload, string $secret): string
{
	$header = base64_encode(json_encode(array('typ' => 'JWT', 'alg' => 'HS256')));
	$body = base64_encode(json_encode($payload));
	$signature = base64_encode(hash_hmac('sha256', $header . '.' . $body, $secret, true));

	$header = rtrim(strtr($header, '+/', '-_'), '=');
	$body = rtrim(strtr($body, '+/', '-_'), '=');
	$signature = rtrim(strtr($signature, '+/', '-_'), '=');

	return $header . '.' . $body . '.' . $signature;
}

/**
 * Generate a signed embed URL for a Metabase dashboard.
 */
function epc_metabase_embed_url(PDO $pdo, int $dashboardId, string $siteKey, array $params = array()): array
{
	epc_metabase_ensure_schema($pdo);

	$st = $pdo->prepare("SELECT * FROM `epc_metabase_config` WHERE `site_key` = ? AND `active` = 1 LIMIT 1");
	$st->execute(array($siteKey));
	$config = $st->fetch(PDO::FETCH_ASSOC);

	if (!$config) {
		$st2 = $pdo->prepare("SELECT * FROM `epc_metabase_config` WHERE `site_key` = '__platform__' AND `active` = 1 LIMIT 1");
		$st2->execute();
		$config = $st2->fetch(PDO::FETCH_ASSOC);
	}

	if (!$config || empty($config['metabase_url']) || empty($config['secret_key'])) {
		return array('ok' => false, 'error' => 'Metabase not configured. Set URL and secret key in platform settings.');
	}

	$payload = array(
		'resource' => array('dashboard' => $dashboardId),
		'params' => array_merge(array('site_key' => $siteKey), $params),
		'exp' => time() + 600,
	);

	$token = epc_metabase_jwt_encode($payload, $config['secret_key']);
	$url = rtrim($config['metabase_url'], '/') . '/embed/dashboard/' . $token . '#bordered=true&titled=true';

	return array('ok' => true, 'url' => $url, 'expires_in' => 600);
}

/* ─── Dashboard Management ─── */

function epc_metabase_dashboards_list(PDO $pdo, string $siteKey): array
{
	epc_metabase_ensure_schema($pdo);
	$st = $pdo->prepare("SELECT * FROM `epc_metabase_dashboards` WHERE `site_key` = ? AND `active` = 1 ORDER BY `category`, `dashboard_name`");
	$st->execute(array($siteKey));
	return $st->fetchAll(PDO::FETCH_ASSOC) ?: array();
}

function epc_metabase_register_dashboard(PDO $pdo, string $siteKey, int $dashboardId, string $name, string $category = 'finance'): array
{
	epc_metabase_ensure_schema($pdo);
	$pdo->prepare("INSERT INTO `epc_metabase_dashboards` (`site_key`,`dashboard_id`,`dashboard_name`,`category`) VALUES (?,?,?,?)
		ON DUPLICATE KEY UPDATE `dashboard_name`=VALUES(`dashboard_name`), `category`=VALUES(`category`)")
		->execute(array($siteKey, $dashboardId, $name, $category));
	return array('ok' => true);
}

/* ─── Config Management ─── */

function epc_metabase_configure(PDO $pdo, string $siteKey, string $url, string $secret): array
{
	epc_metabase_ensure_schema($pdo);
	$pdo->prepare("INSERT INTO `epc_metabase_config` (`site_key`,`metabase_url`,`secret_key`,`active`) VALUES (?,?,?,1)
		ON DUPLICATE KEY UPDATE `metabase_url`=VALUES(`metabase_url`), `secret_key`=VALUES(`secret_key`), `active`=1")
		->execute(array($siteKey, $url, $secret));
	return array('ok' => true, 'message' => 'Metabase configured for ' . $siteKey);
}

function epc_metabase_fleet_stats(PDO $pdo): array
{
	epc_metabase_ensure_schema($pdo);
	$configs = $pdo->query("SELECT `site_key`, `metabase_url`, `active` FROM `epc_metabase_config`")->fetchAll(PDO::FETCH_ASSOC) ?: array();
	$dashboards = $pdo->query("SELECT `site_key`, COUNT(*) AS `count` FROM `epc_metabase_dashboards` WHERE `active`=1 GROUP BY `site_key`")->fetchAll(PDO::FETCH_KEY_PAIR) ?: array();
	return array('configs' => $configs, 'dashboards_per_tenant' => $dashboards);
}
