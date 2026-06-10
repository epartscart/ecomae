<?php
/**
 * Register CP URL shop/orders/guide (one-time deploy helper).
 * GET/POST: token=epartscart-deploy-2026, key=tech_key
 */
declare(strict_types=1);
header('Content-Type: application/json; charset=utf-8');

$deployToken = 'epartscart-deploy-2026';
if (($_POST['token'] ?? $_GET['token'] ?? '') !== $deployToken) {
	http_response_code(403);
	exit(json_encode(array('status' => false, 'message' => 'Forbidden')));
}

require_once __DIR__ . '/config.php';
$DP_Config = new DP_Config;
if ((string)($_POST['key'] ?? $_GET['key'] ?? '') !== $DP_Config->tech_key) {
	http_response_code(403);
	exit(json_encode(array('status' => false, 'message' => 'Invalid tech_key')));
}

try {
	$db = new PDO(
		'mysql:host=' . $DP_Config->host . ';dbname=' . $DP_Config->db,
		$DP_Config->user,
		$DP_Config->password,
		array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION)
	);
} catch (PDOException $e) {
	exit(json_encode(array('status' => false, 'message' => 'DB connect failed')));
}
$db->query('SET NAMES utf8');

$parentUrl = 'shop/orders/orders';
$guideUrl = 'shop/orders/guide';
$phpPath = '/<backend_dir>/content/shop/order_process/order_fulfilment_guide_page.php';
$now = time();

$existing = $db->prepare('SELECT `id` FROM `content` WHERE `url` = ? AND `is_frontend` = 0 LIMIT 1');
$existing->execute(array($guideUrl));
$existingId = (int)$existing->fetchColumn();

if ($existingId > 0) {
	$ps = $db->prepare('SELECT `id` FROM `content` WHERE `url` = ? AND `is_frontend` = 0 LIMIT 1');
	$ps->execute(array($parentUrl));
	$parentIdFix = (int)$ps->fetchColumn();
	$db->prepare(
		'UPDATE `content` SET `published_flag` = 1, `content_type` = \'php\', `is_frontend` = 0,
		 `content` = ?, `title_tag` = ?, `parent` = ?, `level` = 3, `alias` = \'guide\', `url` = ?
		 WHERE `id` = ?'
	)->execute(array($phpPath, 'Order fulfilment guide — eParts Cart', $parentIdFix, $guideUrl, $existingId));
} else {
	$existingId = 0;
}

if ($existingId > 0) {
	$ps2 = $db->prepare('SELECT `id` FROM `content` WHERE `url` = ? AND `is_frontend` = 0 LIMIT 1');
	$ps2->execute(array($parentUrl));
	$parentIdFix = (int)$ps2->fetchColumn();
	if ($parentIdFix > 0) {
		$db->prepare('DELETE FROM `content_access` WHERE `content_id` = ?')->execute(array($existingId));
		$groups = $db->prepare('SELECT DISTINCT `group_id` FROM `content_access` WHERE `content_id` = ?');
		$groups->execute(array($parentIdFix));
		$insAcc = $db->prepare('INSERT INTO `content_access` (`content_id`, `group_id`) VALUES (?, ?)');
		while ($g = $groups->fetch(PDO::FETCH_ASSOC)) {
			try {
				$insAcc->execute(array($existingId, (int)$g['group_id']));
			} catch (Throwable $e) {
			}
		}
		if ((int)$db->query('SELECT COUNT(*) FROM `content_access` WHERE `content_id` = ' . (int)$existingId)->fetchColumn() === 0) {
			$db->prepare('INSERT INTO `content_access` (`content_id`, `group_id`) VALUES (?, 1)')->execute(array($existingId));
		}
	}
	echo json_encode(array(
		'status' => true,
		'message' => 'Order fulfilment guide page repaired',
		'content_id' => $existingId,
		'cp_url' => '/' . $DP_Config->backend_dir . '/' . $guideUrl,
		'php_path' => $phpPath,
	), JSON_UNESCAPED_UNICODE | JSON_PRETTY_PRINT);
	exit;
}

$parent = $db->prepare('SELECT `id`, `level` FROM `content` WHERE `url` = ? AND `is_frontend` = 0 LIMIT 1');
$parent->execute(array($parentUrl));
$parentRow = $parent->fetch(PDO::FETCH_ASSOC);
if (!$parentRow) {
	exit(json_encode(array('status' => false, 'message' => 'Parent content shop/orders/orders not found')));
}
$parentId = (int)$parentRow['id'];
$level = (int)$parentRow['level'] + 1;

$ins = $db->prepare(
	'INSERT INTO `content`
	(`count`, `url`, `level`, `alias`, `value`, `parent`, `description`, `is_frontend`, `content_type`, `content`,
	 `title_tag`, `description_tag`, `keywords_tag`, `author_tag`, `main_flag`, `modules_array`, `css_js`, `robots_tag`,
	 `system_flag`, `published_flag`, `open`, `time_created`, `time_edited`, `order`)
	 VALUES (0, ?, ?, ?, ?, ?, ?, 0, \'php\', ?, ?, \'0\', \'0\', \'0\', 0, \'[]\', \'\', \'\', 0, 1, 0, ?, ?, 80)'
);
$ins->execute(array(
	$guideUrl,
	$level,
	'guide',
	'Order fulfilment guide',
	$parentId,
	'Checkout to supplier LPO and staff processing',
	$phpPath,
	'Order fulfilment guide — eParts Cart',
	$now,
	$now,
));

$contentId = (int)$db->lastInsertId();
if ($contentId > 0) {
	try {
		$db->prepare('INSERT INTO `content_access` (`content_id`, `group_id`) VALUES (?, 1)')->execute(array($contentId));
	} catch (Throwable $e) {
	}
	$groups = $db->prepare('SELECT DISTINCT `group_id` FROM `content_access` WHERE `content_id` = ?');
	$groups->execute(array($parentId));
	$insAcc = $db->prepare('INSERT INTO `content_access` (`content_id`, `group_id`) VALUES (?, ?)');
	while ($g = $groups->fetch(PDO::FETCH_ASSOC)) {
		try {
			$insAcc->execute(array($contentId, (int)$g['group_id']));
		} catch (Throwable $e) {
		}
	}
}

echo json_encode(array(
	'status' => $contentId > 0,
	'message' => $contentId > 0 ? 'Order fulfilment guide registered' : 'Insert failed',
	'content_id' => $contentId,
	'cp_url' => '/' . $DP_Config->backend_dir . '/' . $guideUrl,
), JSON_UNESCAPED_UNICODE | JSON_PRETTY_PRINT);
