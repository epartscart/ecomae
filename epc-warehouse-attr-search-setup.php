<?php
/**
 * Register storefront route shop/warehouse-search + ensure attr index tables.
 *
 *   https://www.epartscart.com/epc-warehouse-attr-search-setup.php?token=epartscart-deploy-2026&apply=1
 */
declare(strict_types=1);

header('Content-Type: application/json; charset=utf-8');

require_once __DIR__ . '/epc_deploy_auth.php';
epc_deploy_require_token(false);

if (!defined('_ASTEXE_')) {
	define('_ASTEXE_', 1);
}
require_once __DIR__ . '/config.php';
$cfg = new DP_Config();

$apply = !empty($_GET['apply']);
$report = array('ok' => true, 'apply' => $apply, 'changes' => array(), 'checks' => array());

try {
	$pdo = new PDO(
		'mysql:host=' . $cfg->host . ';dbname=' . $cfg->db . ';charset=utf8',
		$cfg->user,
		$cfg->password,
		array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION)
	);
} catch (Throwable $e) {
	exit(json_encode(array('ok' => false, 'error' => 'DB: ' . $e->getMessage())));
}

require_once __DIR__ . '/content/shop/docpart/epc_price_extra_fields.php';
$schemaOk = epc_price_extra_ensure_schema($pdo);
$report['checks']['schema'] = $schemaOk;
$report['checks']['page_file'] = is_file(__DIR__ . '/content/shop/docpart/epc_warehouse_attr_search_page.php');
$report['checks']['helper_file'] = is_file(__DIR__ . '/content/shop/docpart/epc_price_extra_fields.php');

function epc_wh_attr_setup_ensure_content(PDO $pdo, string $url, string $alias, string $phpPath, string $title, string $desc): int
{
	$shop = $pdo->prepare('SELECT `id`, `level` FROM `content` WHERE `url` = ? AND `is_frontend` = 1 LIMIT 1');
	$shop->execute(array('shop'));
	$shopRow = $shop->fetch(PDO::FETCH_ASSOC);
	$parentId = $shopRow ? (int) $shopRow['id'] : 0;
	$level = $shopRow ? ((int) $shopRow['level'] + 1) : 2;
	$now = time();
	$existing = $pdo->prepare('SELECT `id` FROM `content` WHERE `url` = ? AND `is_frontend` = 1 LIMIT 1');
	$existing->execute(array($url));
	$contentId = (int) $existing->fetchColumn();
	if ($contentId > 0) {
		$pdo->prepare(
			'UPDATE `content`
			 SET `published_flag` = 1, `content_type` = \'php\', `content` = ?, `title_tag` = ?,
			     `description_tag` = ?, `parent` = ?, `level` = ?, `alias` = ?, `value` = ?, `time_edited` = ?
			 WHERE `id` = ?'
		)->execute(array($phpPath, $title, $desc, $parentId, $level, $alias, $title, $now, $contentId));
	} else {
		$pdo->prepare(
			'INSERT INTO `content`
			(`count`, `url`, `level`, `alias`, `value`, `parent`, `description`, `is_frontend`, `content_type`, `content`,
			 `title_tag`, `description_tag`, `keywords_tag`, `author_tag`, `main_flag`, `modules_array`, `css_js`, `robots_tag`,
			 `system_flag`, `published_flag`, `open`, `time_created`, `time_edited`, `order`)
			 VALUES (0, ?, ?, ?, ?, ?, ?, 1, \'php\', ?, ?, ?, \'0\', \'0\', 0, \'[]\', \'\', \'noindex, follow\', 1, 1, 0, ?, ?, 92)'
		)->execute(array($url, $level, $alias, $title, $parentId, $desc, $phpPath, $title, $desc, $now, $now));
		$contentId = (int) $pdo->lastInsertId();
	}
	if ($contentId > 0) {
		$groups = array(1 => true, 2 => true, 3 => true);
		try {
			$gq = $pdo->query('SELECT `id` FROM `groups`');
			if ($gq) {
				while ($g = $gq->fetchColumn()) {
					$groups[(int) $g] = true;
				}
			}
		} catch (Throwable $e) {
		}
		$pdo->prepare('DELETE FROM `content_access` WHERE `content_id` = ?')->execute(array($contentId));
		$ins = $pdo->prepare('INSERT IGNORE INTO `content_access` (`content_id`, `group_id`) VALUES (?, ?)');
		foreach (array_keys($groups) as $gid) {
			$ins->execute(array($contentId, (int) $gid));
		}
	}
	return $contentId;
}

if ($apply) {
	if (!$schemaOk) {
		$report['ok'] = false;
		$report['changes'][] = 'schema ensure failed';
	} else {
		$report['changes'][] = 'schema epc_price_data_extras + epc_price_attr_index ready';
	}
	$id = epc_wh_attr_setup_ensure_content(
		$pdo,
		'shop/warehouse-search',
		'warehouse-search',
		'/content/shop/docpart/epc_warehouse_attr_search_page.php',
		'Warehouse product search',
		'Search warehouse products by engine code, size, country, cross reference and other info'
	);
	$report['changes'][] = 'content shop/warehouse-search id=' . $id;
	if ($id <= 0) {
		$report['ok'] = false;
	}
} else {
	$existing = $pdo->prepare('SELECT `id`, `content`, `published_flag` FROM `content` WHERE `url` = ? AND `is_frontend` = 1 LIMIT 1');
	$existing->execute(array('shop/warehouse-search'));
	$row = $existing->fetch(PDO::FETCH_ASSOC);
	$report['checks']['existing_route'] = $row ?: null;
}

echo json_encode($report, JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES | JSON_PRETTY_PRINT);
