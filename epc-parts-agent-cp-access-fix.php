<?php
/**
 * Fix CP access for shop/parts_agent_chats (AI agent chat review AJAX).
 * Run: https://www.epartscart.com/epc-parts-agent-cp-access-fix.php?token=epartscart-deploy-2026
 */
error_reporting(E_ALL);
ini_set('display_errors', 1);
header('Content-Type: text/plain; charset=utf-8');

if (!isset($_GET['token']) || $_GET['token'] !== 'epartscart-deploy-2026') {
	exit("Forbidden\n");
}

define('_ASTEXE_', 1);
require_once __DIR__ . '/config.php';

$cfg = new DP_Config();
$pdo = new PDO('mysql:host=' . $cfg->host . ';dbname=' . $cfg->db . ';charset=utf8', $cfg->user, $cfg->password);
$pdo->setAttribute(PDO::ATTR_ERRMODE, PDO::ERRMODE_EXCEPTION);

$url = 'shop/parts_agent_chats';
$st = $pdo->prepare('SELECT `id` FROM `content` WHERE `url` = ? AND `is_frontend` = 0 LIMIT 1');
$st->execute(array($url));
$contentId = (int)$st->fetchColumn();
if ($contentId <= 0) {
	exit("Content page {$url} not found. Run epc-parts-agent-cp-setup.php first.\n");
}

function epc_pa_fix_backend_group_ids(PDO $pdo)
{
	$ids = array();
	$st = $pdo->query('SELECT `id` FROM `groups` WHERE `for_backend` = 1 LIMIT 1');
	$root = $st->fetch(PDO::FETCH_ASSOC);
	if (!$root) {
		return array(1, 3);
	}
	$collect = function ($parentId) use ($pdo, &$collect, &$ids) {
		$ids[(int)$parentId] = true;
		$ch = $pdo->prepare('SELECT `id`, `count` FROM `groups` WHERE `parent` = ?');
		$ch->execute(array($parentId));
		while ($row = $ch->fetch(PDO::FETCH_ASSOC)) {
			$ids[(int)$row['id']] = true;
			if ((int)$row['count'] > 0) {
				$collect((int)$row['id']);
			}
		}
	};
	$collect((int)$root['id']);
	return array_keys($ids);
}

$refGroups = array();
foreach (array('shop/crosses', 'shop/demand_countries', 'shop/orders/orders', 'shop') as $refUrl) {
	$ref = $pdo->prepare(
		'SELECT DISTINCT ca.`group_id` FROM `content_access` ca
		 INNER JOIN `content` c ON c.`id` = ca.`content_id`
		 WHERE c.`url` = ? AND c.`is_frontend` = 0'
	);
	$ref->execute(array($refUrl));
	while ($gid = $ref->fetchColumn()) {
		$refGroups[(int)$gid] = true;
	}
}
foreach (epc_pa_fix_backend_group_ids($pdo) as $gid) {
	$refGroups[(int)$gid] = true;
}

$pdo->prepare('DELETE FROM `content_access` WHERE `content_id` = ?')->execute(array($contentId));
$ins = $pdo->prepare('INSERT IGNORE INTO `content_access` (`content_id`, `group_id`) VALUES (?, ?)');
foreach (array_keys($refGroups) as $gid) {
	$ins->execute(array($contentId, (int)$gid));
}

$check = $pdo->prepare('SELECT `group_id` FROM `content_access` WHERE `content_id` = ? ORDER BY `group_id`');
$check->execute(array($contentId));
$granted = $check->fetchAll(PDO::FETCH_COLUMN);

echo "AI agent chats CP access fixed.\n";
echo "content_id: {$contentId}\n";
echo "groups granted: " . implode(', ', $granted) . "\n";
echo "total groups: " . count($granted) . "\n";
echo "Reload CP page: /" . $cfg->backend_dir . "/{$url}\n";
