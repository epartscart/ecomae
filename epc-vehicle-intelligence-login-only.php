<?php
/**
 * Restrict Vehicle intelligence page to logged-in customers (content_access).
 * Run once: https://www.epartscart.com/epc-vehicle-intelligence-login-only.php
 */
error_reporting(E_ALL);
ini_set('display_errors', 0);
header('Content-Type: text/plain; charset=utf-8');

require_once __DIR__ . '/epc_deploy_auth.php';
epc_deploy_require_token();

define('_ASTEXE_', 1);
require_once __DIR__ . '/config.php';

$cfg = new DP_Config();
$db = new PDO('mysql:host=' . $cfg->host . ';dbname=' . $cfg->db . ';charset=utf8', $cfg->user, $cfg->password);
$db->setAttribute(PDO::ATTR_ERRMODE, PDO::ERRMODE_EXCEPTION);

$stmt = $db->prepare('SELECT `id` FROM `content` WHERE `is_frontend` = 1 AND `url` = ? LIMIT 1');
$stmt->execute(array('demand-intelligence'));
$contentId = (int)$stmt->fetchColumn();
if ($contentId <= 0) {
	echo "Content demand-intelligence not found. Run epc-demand-intelligence-setup.php first.\n";
	exit(1);
}

$groupIds = $db->query('SELECT `id` FROM `groups` WHERE `id` > 0 ORDER BY `id`')->fetchAll(PDO::FETCH_COLUMN);
if (!$groupIds) {
	$groupIds = array(1);
}

$db->prepare('DELETE FROM `content_access` WHERE `content_id` = ?')->execute(array($contentId));
$ins = $db->prepare('INSERT INTO `content_access` (`content_id`, `group_id`) VALUES (?, ?)');
foreach ($groupIds as $gid) {
	$ins->execute(array($contentId, (int)$gid));
}

echo "Vehicle intelligence content_id={$contentId}\n";
echo "content_access set for groups: " . implode(', ', $groupIds) . "\n";
echo "Guests (not logged in) can no longer open this page via CMS access rules.\n";
