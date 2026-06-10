<?php
/**
 * One-shot: register /users/login frontend route on tenant DBs.
 * GET/POST ?token=epartscart-deploy-2026
 */
if (!defined('_ASTEXE_')) {
	define('_ASTEXE_', 1);
}

header('Content-Type: text/plain; charset=utf-8');

$token = (string) ($_GET['token'] ?? $_POST['token'] ?? '');
if ($token !== 'epartscart-deploy-2026') {
	http_response_code(403);
	echo "Forbidden\n";
	exit;
}

require_once $_SERVER['DOCUMENT_ROOT'] . '/config.php';
$DP_Config = new DP_Config();
$epcTenantDbFile = $_SERVER['DOCUMENT_ROOT'] . '/config.tenant-db.php';
if (is_file($epcTenantDbFile)) {
	$epc_tenant_db = null;
	require $epcTenantDbFile;
	if (isset($epc_tenant_db) && is_array($epc_tenant_db)) {
		foreach (array('db', 'user', 'password') as $epcTk) {
			if (!empty($epc_tenant_db[$epcTk]) && property_exists($DP_Config, $epcTk)) {
				$DP_Config->$epcTk = $epc_tenant_db[$epcTk];
			}
		}
	}
}
$epcTenantHostDbFile = $_SERVER['DOCUMENT_ROOT'] . '/config.tenant-host-db.php';
if (is_file($epcTenantHostDbFile)) {
	$epc_tenant_host_db = null;
	require $epcTenantHostDbFile;
	$host = strtolower((string)($_SERVER['HTTP_HOST'] ?? ''));
	if (strpos($host, ':') !== false) {
		$host = explode(':', $host, 2)[0];
	}
	if (isset($epc_tenant_host_db) && is_array($epc_tenant_host_db) && isset($epc_tenant_host_db[$host])) {
		foreach (array('db', 'user', 'password') as $epcTk) {
			if (!empty($epc_tenant_host_db[$host][$epcTk]) && property_exists($DP_Config, $epcTk)) {
				$DP_Config->$epcTk = $epc_tenant_host_db[$host][$epcTk];
			}
		}
	}
}

try {
	$pdo = new PDO(
		'mysql:host=' . $DP_Config->host . ';dbname=' . $DP_Config->db . ';charset=utf8',
		$DP_Config->user,
		$DP_Config->password,
		array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION)
	);
} catch (Throwable $e) {
	echo 'DB connect failed: ' . $e->getMessage() . "\n";
	exit(1);
}

$parent = $pdo->prepare('SELECT `id`, `level` FROM `content` WHERE `url` = ? AND `is_frontend` = 1 LIMIT 1');
$parent->execute(array('users'));
$parentRow = $parent->fetch(PDO::FETCH_ASSOC);
if (!$parentRow) {
	echo "Parent content users not found\n";
	exit(1);
}
$parentId = (int) $parentRow['id'];
$level = (int) $parentRow['level'] + 1;
$now = time();
$url = 'users/login';
$phpPath = '/content/users/loginform.php';

$regRow = $pdo->prepare('SELECT * FROM `content` WHERE `url` = ? AND `is_frontend` = 1 LIMIT 1');
$regRow->execute(array('users/registration'));
$registration = $regRow->fetch(PDO::FETCH_ASSOC);
$alias = $registration ? (string)$registration['alias'] : 'login';
$valueKey = $registration ? (string)$registration['value'] : 'Login';
$descKey = $registration ? (string)$registration['description'] : 'Login';
$modules = $registration ? (string)$registration['modules_array'] : '[1,22,32,34]';
$order = $registration ? ((int)$registration['order'] + 1) : 63;

$existing = $pdo->prepare('SELECT `id` FROM `content` WHERE `url` = ? AND `is_frontend` = 1 LIMIT 1');
$existing->execute(array($url));
$contentId = (int) $existing->fetchColumn();

if ($contentId > 0) {
	$pdo->prepare(
		'UPDATE `content` SET `published_flag` = 1, `content_type` = \'php\', `content` = ?, `title_tag` = ?, `parent` = ?, `level` = ?, `alias` = ?, `value` = ?, `description` = ?, `modules_array` = ?, `time_edited` = ? WHERE `id` = ?'
	)->execute(array($phpPath, 'Login', $parentId, $level, 'login', $valueKey, $descKey, $modules, $now, $contentId));
	echo "Updated content id={$contentId} url={$url}\n";
} else {
	$pdo->prepare(
		'INSERT INTO `content`
		(`count`, `url`, `level`, `alias`, `value`, `parent`, `description`, `is_frontend`, `content_type`, `content`,
		 `title_tag`, `description_tag`, `keywords_tag`, `author_tag`, `main_flag`, `modules_array`, `css_js`, `robots_tag`,
		 `system_flag`, `published_flag`, `open`, `time_created`, `time_edited`, `order`)
		 VALUES (0, ?, ?, ?, ?, ?, ?, 1, \'php\', ?, ?, \'0\', \'0\', \'0\', 0, ?, \'\', \'\', 0, 1, 0, ?, ?, ?)'
	)->execute(array($url, $level, 'login', $valueKey, $parentId, $descKey, $phpPath, 'Login', $modules, $now, $now, $order));
	$contentId = (int) $pdo->lastInsertId();
	echo "Created content id={$contentId} url={$url}\n";
}

$pdo->prepare('DELETE FROM `content_access` WHERE `content_id` = ?')->execute(array($contentId));
if ($registration) {
	$regId = (int)$registration['id'];
	$clone = $pdo->prepare('INSERT IGNORE INTO `content_access` (`content_id`, `group_id`) SELECT ?, `group_id` FROM `content_access` WHERE `content_id` = ?');
	$clone->execute(array($contentId, $regId));
	echo "Cloned content_access from registration id={$regId}\n";
}
$allGroups = $pdo->query('SELECT `id` FROM `groups`');
while ($g = $allGroups->fetch(PDO::FETCH_ASSOC)) {
	$pdo->prepare('INSERT IGNORE INTO `content_access` (`content_id`, `group_id`) VALUES (?, ?)')->execute(array($contentId, (int)$g['id']));
}
echo "OK users/login route ready\n";
echo "db=" . $DP_Config->db . "\n";
$chk = $pdo->prepare('SELECT `id`,`url`,`published_flag`,`is_frontend`,`content` FROM `content` WHERE `url` = ? LIMIT 1');
$chk->execute(array($url));
print_r($chk->fetch(PDO::FETCH_ASSOC));
$acc = $pdo->prepare('SELECT COUNT(*) FROM `content_access` WHERE `content_id` = ?');
$acc->execute(array($contentId));
echo "access_rows=" . $acc->fetchColumn() . "\n";
echo "loginform_exists=" . (is_file($_SERVER['DOCUMENT_ROOT'] . '/content/users/loginform.php') ? 'yes' : 'no') . "\n";
