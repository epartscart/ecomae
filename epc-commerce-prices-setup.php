<?php
/**
 * Register CP route shop/prices/commerce for commerce S/P/L uploads.
 * https://www.epartscart.com/epc-commerce-prices-setup.php?token=epartscart-deploy-2026&key=TECH_KEY&apply=1
 */
declare(strict_types=1);

header('Content-Type: application/json; charset=utf-8');

if (($_GET['token'] ?? '') !== 'epartscart-deploy-2026') {
	http_response_code(403);
	exit(json_encode(array('ok' => false, 'error' => 'Forbidden')));
}

if (!defined('_ASTEXE_')) {
	define('_ASTEXE_', 1);
}
require_once __DIR__ . '/config.php';
$cfg = new DP_Config();
if ((string) ($_GET['key'] ?? '') !== $cfg->tech_key) {
	http_response_code(403);
	exit(json_encode(array('ok' => false, 'error' => 'Invalid key')));
}

require_once __DIR__ . '/content/general_pages/epc_portal.php';
if (function_exists('epc_portal_apply_config')) {
	epc_portal_apply_config($cfg);
}

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

function epc_commerce_setup_ensure_content(PDO $pdo, string $parentUrl, string $url, string $alias, string $phpPath, string $title): int
{
	$parent = $pdo->prepare('SELECT `id`, `level` FROM `content` WHERE `url` = ? AND `is_frontend` = 0 LIMIT 1');
	$parent->execute(array($parentUrl));
	$parentRow = $parent->fetch(PDO::FETCH_ASSOC);
	if (!$parentRow) {
		return 0;
	}
	$parentId = (int) $parentRow['id'];
	$level = (int) $parentRow['level'] + 1;
	$now = time();
	$existing = $pdo->prepare('SELECT `id` FROM `content` WHERE `url` = ? AND `is_frontend` = 0 LIMIT 1');
	$existing->execute(array($url));
	$contentId = (int) $existing->fetchColumn();
	if ($contentId > 0) {
		$pdo->prepare(
			'UPDATE `content` SET `published_flag` = 1, `content_type` = \'php\', `content` = ?, `title_tag` = ?, `parent` = ?, `level` = ?, `alias` = ?, `time_edited` = ? WHERE `id` = ?'
		)->execute(array($phpPath, $title, $parentId, $level, $alias, $now, $contentId));
	} else {
		$pdo->prepare(
			'INSERT INTO `content`
			(`count`, `url`, `level`, `alias`, `value`, `parent`, `description`, `is_frontend`, `content_type`, `content`,
			 `title_tag`, `description_tag`, `keywords_tag`, `author_tag`, `main_flag`, `modules_array`, `css_js`, `robots_tag`,
			 `system_flag`, `published_flag`, `open`, `time_created`, `time_edited`, `order`)
			 VALUES (0, ?, ?, ?, ?, ?, ?, 0, \'php\', ?, ?, \'0\', \'0\', \'0\', 0, \'[]\', \'\', \'\', 0, 1, 0, ?, ?, 80)'
		)->execute(array($url, $level, $alias, $title, $parentId, $title, $phpPath, $title, $now, $now));
		$contentId = (int) $pdo->lastInsertId();
	}
	if ($contentId > 0) {
		$ref = $pdo->prepare('SELECT `id` FROM `content` WHERE `url` = ? AND `is_frontend` = 0 LIMIT 1');
		$ref->execute(array('shop/prices'));
		$refId = (int) $ref->fetchColumn();
		$groups = array(1 => true, 3 => true);
		if ($refId > 0) {
			$gq = $pdo->prepare('SELECT DISTINCT `group_id` FROM `content_access` WHERE `content_id` = ?');
			$gq->execute(array($refId));
			while ($g = $gq->fetchColumn()) {
				$groups[(int) $g] = true;
			}
		}
		$pdo->prepare('DELETE FROM `content_access` WHERE `content_id` = ?')->execute(array($contentId));
		$ins = $pdo->prepare('INSERT IGNORE INTO `content_access` (`content_id`, `group_id`) VALUES (?, ?)');
		foreach (array_keys($groups) as $gid) {
			$ins->execute(array($contentId, (int) $gid));
		}
	}
	return $contentId;
}

$phpPath = '/<backend_dir>/content/shop/prices_upload/commerce_data_page.php';
$fileOk = is_file(__DIR__ . '/cp/content/shop/prices_upload/commerce_data_page.php');
$report['checks']['file'] = $fileOk;

if ($apply) {
	$id = epc_commerce_setup_ensure_content(
		$pdo,
		'shop/prices',
		'shop/prices/commerce',
		'commerce',
		$phpPath,
		'Commerce data upload — sales / purchase / inventory'
	);
	$report['changes'][] = 'content shop/prices/commerce id=' . $id;
	if ($id <= 0) {
		$report['ok'] = false;
	}
}

$st = $pdo->prepare('SELECT `id`, `published_flag`, `content` FROM `content` WHERE `url` = ? AND `is_frontend` = 0 LIMIT 1');
$st->execute(array('shop/prices/commerce'));
$row = $st->fetch(PDO::FETCH_ASSOC);
$report['checks']['route'] = array(
	'ok' => $row && (int) $row['published_flag'] === 1,
	'id' => $row ? (int) $row['id'] : 0,
	'content' => $row ? (string) $row['content'] : null,
);
$report['cp_url'] = 'https://www.epartscart.com/cp/shop/prices/commerce';
$report['api_url'] = 'https://www.epartscart.com/epc-upload-commerce-prices.php';
$report['hint'] = $apply ? 'Route ready. Open CP → Price lists → Commerce data.' : 'Dry run — add apply=1 to register route.';

echo json_encode($report, JSON_UNESCAPED_UNICODE | JSON_PRETTY_PRINT);
