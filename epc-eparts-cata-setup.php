<?php
/**
 * Ensure EParts CATA / Mod CP + storefront content rows and scripts exist.
 *
 * Usage:
 *   /epc-eparts-cata-setup.php?token=...
 */
declare(strict_types=1);

error_reporting(E_ALL);
ini_set('display_errors', '0');
header('Content-Type: text/plain; charset=utf-8');

require_once __DIR__ . '/epc_deploy_auth.php';
epc_deploy_require_token();

if (!defined('_ASTEXE_')) {
	define('_ASTEXE_', 1);
}
require_once __DIR__ . '/config.php';

$cfg = new DP_Config();
$tenantFile = __DIR__ . '/config.tenant-host-db.php';
if (is_file($tenantFile)) {
	$epc_tenant_host_db = null;
	require $tenantFile;
	$host = strtolower((string) ($_SERVER['HTTP_HOST'] ?? ''));
	if (strpos($host, ':') !== false) {
		$host = explode(':', $host, 2)[0];
	}
	if (isset($epc_tenant_host_db) && is_array($epc_tenant_host_db) && isset($epc_tenant_host_db[$host])) {
		foreach (array('db', 'user', 'password', 'host') as $k) {
			if (!empty($epc_tenant_host_db[$host][$k]) && property_exists($cfg, $k)) {
				$cfg->$k = $epc_tenant_host_db[$host][$k];
			}
		}
	}
}

$dbHost = trim((string) ($cfg->host ?? '127.0.0.1'));
if ($dbHost === '' || strtolower($dbHost) === 'localhost') {
	$dbHost = '127.0.0.1';
}
$pdo = new PDO(
	'mysql:host=' . $dbHost . ';dbname=' . $cfg->db . ';charset=utf8mb4',
	(string) $cfg->user,
	(string) $cfg->password,
	array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION)
);

function epc_cata_setup_lang(PDO $pdo, string $key, string $en, string $ru): void
{
	$pdo->prepare('INSERT IGNORE INTO `lang_text_strings` (`str_key`, `description`, `same`, `is_error`, `is_custom`, `used_found`) VALUES (?, ?, NULL, 0, 1, 1)')->execute(array($key, $en));
	$ins = $pdo->prepare('INSERT INTO `lang_text_strings_translation` (`str_key`, `lang_code`, `value`) VALUES (?, ?, ?) ON DUPLICATE KEY UPDATE `value` = VALUES(`value`)');
	$ins->execute(array($key, 'en', $en));
	$ins->execute(array($key, 'ru', $ru));
}

function epc_cata_setup_copy_acl(PDO $pdo, int $fromId, int $toId): int
{
	if ($fromId < 1 || $toId < 1) {
		return 0;
	}
	$pdo->prepare('DELETE FROM `content_access` WHERE `content_id` = ?')->execute(array($toId));
	$st = $pdo->prepare('SELECT DISTINCT `group_id` FROM `content_access` WHERE `content_id` = ?');
	$st->execute(array($fromId));
	$ins = $pdo->prepare('INSERT INTO `content_access` (`content_id`, `group_id`) VALUES (?, ?)');
	$n = 0;
	while ($row = $st->fetch(PDO::FETCH_ASSOC)) {
		$ins->execute(array($toId, (int) $row['group_id']));
		$n++;
	}
	return $n;
}

$files = array(
	'cp/content/shop/eparts-cata/eparts_cata.php',
	'cp/content/shop/eparts-mod/eparts_mod_cp.php',
	'content/eparts_cata.php',
	'content/eparts_mod_catalog.php',
	'content/eparts_product.php',
);
foreach ($files as $rel) {
	$path = __DIR__ . '/' . $rel;
	echo 'file ' . $rel . ' exists=' . (is_file($path) ? '1' : '0') . ' size=' . (is_file($path) ? filesize($path) : 0) . "\n";
}

epc_cata_setup_lang($pdo, 'epc_cp_eparts_cata', 'EParts CATA', 'EParts CATA');
epc_cata_setup_lang($pdo, 'epc_cp_eparts_mod', 'EParts Mod', 'EParts Mod');
epc_cata_setup_lang($pdo, 'epc_eparts_cata_title', 'EParts CATA', 'EParts CATA');
epc_cata_setup_lang($pdo, 'epc_eparts_mod_title', 'EParts Mod', 'EParts Mod');
epc_cata_setup_lang($pdo, 'epc_eparts_product_title', 'EParts product', 'EParts product');

$now = time();
$pages = array(
	array(
		'url' => 'shop/eparts-cata',
		'is_frontend' => 0,
		'content' => '/<backend_dir>/content/shop/eparts-cata/eparts_cata.php',
		'value' => 'epc_cp_eparts_cata',
		'title' => 'epc_cp_eparts_cata',
		'description' => 'EParts CATA — unified catalog sync dashboard',
		'alias' => 'eparts-cata',
	),
	array(
		'url' => 'shop/eparts_cata',
		'is_frontend' => 0,
		'content' => '/<backend_dir>/content/shop/eparts-cata/eparts_cata.php',
		'value' => 'epc_cp_eparts_cata',
		'title' => 'epc_cp_eparts_cata',
		'description' => 'EParts CATA (legacy underscore URL alias)',
		'alias' => 'eparts_cata',
	),
	array(
		'url' => 'shop/eparts-mod',
		'is_frontend' => 0,
		'content' => '/<backend_dir>/content/shop/eparts-mod/eparts_mod_cp.php',
		'value' => 'epc_cp_eparts_mod',
		'title' => 'epc_cp_eparts_mod',
		'description' => 'EParts Mod — storefront presentation settings',
		'alias' => 'eparts-mod',
	),
	array(
		'url' => 'eparts-cata',
		'is_frontend' => 1,
		'content' => '/content/eparts_cata.php',
		'value' => 'epc_eparts_cata_title',
		'title' => 'epc_eparts_cata_title',
		'description' => 'EParts CATA storefront',
		'alias' => 'eparts-cata',
	),
	array(
		'url' => 'eparts-mod',
		'is_frontend' => 1,
		'content' => '/content/eparts_mod_catalog.php',
		'value' => 'epc_eparts_mod_title',
		'title' => 'epc_eparts_mod_title',
		'description' => 'EParts Mod storefront',
		'alias' => 'eparts-mod',
	),
	array(
		'url' => 'eparts-product',
		'is_frontend' => 1,
		'content' => '/content/eparts_product.php',
		'value' => 'epc_eparts_product_title',
		'title' => 'epc_eparts_product_title',
		'description' => 'EParts product deep link',
		'alias' => 'eparts-product',
	),
);

$ref = $pdo->prepare('SELECT `id` FROM `content` WHERE `url` = ? AND `is_frontend` = 0 LIMIT 1');
$ref->execute(array('shop/catalogue/products'));
$refId = (int) $ref->fetchColumn();

foreach ($pages as $page) {
	$st = $pdo->prepare('SELECT `id`, `parent`, `level` FROM `content` WHERE `url` = ? AND `is_frontend` = ? LIMIT 1');
	$st->execute(array($page['url'], (int) $page['is_frontend']));
	$row = $st->fetch(PDO::FETCH_ASSOC);
	if ($row) {
		$id = (int) $row['id'];
		$pdo->prepare(
			'UPDATE `content` SET `published_flag` = 1, `content_type` = \'php\', `content` = ?, `title_tag` = ?, `value` = ?, `alias` = ?, `time_edited` = ? WHERE `id` = ?'
		)->execute(array($page['content'], $page['title'], $page['value'], $page['alias'], $now, $id));
		echo 'updated id=' . $id . ' url=' . $page['url'] . "\n";
	} else {
		$parentId = 0;
		$level = 1;
		if ((int) $page['is_frontend'] === 0) {
			$parent = $pdo->query("SELECT `id`, `level` FROM `content` WHERE `url` = 'shop' AND `is_frontend` = 0 LIMIT 1")->fetch(PDO::FETCH_ASSOC);
			if ($parent) {
				$parentId = (int) $parent['id'];
				$level = (int) $parent['level'] + 1;
			}
		}
		$pdo->prepare(
			'INSERT INTO `content` (`count`, `url`, `level`, `alias`, `value`, `parent`, `description`, `is_frontend`, `content_type`, `content`,
			 `title_tag`, `description_tag`, `keywords_tag`, `author_tag`, `main_flag`, `modules_array`, `css_js`, `robots_tag`,
			 `system_flag`, `published_flag`, `open`, `time_created`, `time_edited`, `order`)
			 VALUES (0, ?, ?, ?, ?, ?, ?, ?, \'php\', ?, ?, \'0\', \'0\', \'0\', 0, \'[]\', \'\', \'\', 0, 1, 0, ?, ?, 40)'
		)->execute(array(
			$page['url'], $level, $page['alias'], $page['value'], $parentId,
			$page['description'], (int) $page['is_frontend'], $page['content'], $page['title'], $now, $now,
		));
		$id = (int) $pdo->lastInsertId();
		echo 'inserted id=' . $id . ' url=' . $page['url'] . "\n";
	}
	if ((int) $page['is_frontend'] === 0 && $refId > 0) {
		$n = epc_cata_setup_copy_acl($pdo, $refId, $id);
		echo 'acl content_id=' . $id . ' groups=' . $n . "\n";
	}
}

echo "OK eparts-cata setup\n";
echo "CP: /cp/shop/eparts-cata\n";
echo "Storefront: /en/eparts-cata\n";
