<?php
/**
 * Register CP URL shop/channels/guide (repair helper).
 * GET: token=epartscart-deploy-2026, key=tech_key
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

$parentUrl = 'shop/channels/channels';
$guideUrl = 'shop/channels/guide';
$phpPath = '/<backend_dir>/content/shop/channels/channels_guide_page.php';
$now = time();

$parent = $db->prepare('SELECT `id`, `level` FROM `content` WHERE `url` = ? AND `is_frontend` = 0 LIMIT 1');
$parent->execute(array($parentUrl));
$parentRow = $parent->fetch(PDO::FETCH_ASSOC);
if (!$parentRow) {
	exit(json_encode(array('status' => false, 'message' => 'Parent shop/channels/channels not found — run epc-channels-setup.php first')));
}

$existing = $db->prepare('SELECT `id` FROM `content` WHERE `url` = ? AND `is_frontend` = 0 LIMIT 1');
$existing->execute(array($guideUrl));
$contentId = (int)$existing->fetchColumn();

if ($contentId > 0) {
	$db->prepare(
		'UPDATE `content` SET `published_flag` = 1, `content_type` = \'php\', `is_frontend` = 0,
		 `content` = ?, `title_tag` = ?, `parent` = ?, `level` = ?, `alias` = \'guide\', `url` = ?, `value` = ?
		 WHERE `id` = ?'
	)->execute(array($phpPath, 'Channels guide — eParts Cart', (int)$parentRow['id'], (int)$parentRow['level'] + 1, $guideUrl, 'epc_channels_guide_cp', $contentId));
} else {
	$db->prepare(
		'INSERT INTO `content`
		(`count`, `url`, `level`, `alias`, `value`, `parent`, `description`, `is_frontend`, `content_type`, `content`,
		 `title_tag`, `description_tag`, `keywords_tag`, `author_tag`, `main_flag`, `modules_array`, `css_js`, `robots_tag`,
		 `system_flag`, `published_flag`, `open`, `time_created`, `time_edited`, `order`)
		 VALUES (0, ?, ?, \'guide\', \'epc_channels_guide_cp\', ?, ?, 0, \'php\', ?, ?, \'0\', \'0\', \'0\', 0, \'[]\', \'\', \'\', 0, 1, 0, ?, ?, 1)'
	)->execute(array(
		$guideUrl,
		(int)$parentRow['level'] + 1,
		(int)$parentRow['id'],
		'Channels & Logistics step-by-step guide',
		$phpPath,
		'Channels guide — eParts Cart',
		$now,
		$now,
	));
	$contentId = (int)$db->lastInsertId();
}

echo json_encode(array(
	'status' => true,
	'message' => 'Channels guide registered',
	'content_id' => $contentId,
	'cp_url' => '/' . $DP_Config->backend_dir . '/' . $guideUrl,
), JSON_PRETTY_PRINT | JSON_UNESCAPED_UNICODE);
