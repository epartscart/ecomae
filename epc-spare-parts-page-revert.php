<?php
/**
 * Revert: remove /spare-parts page + top menu link added by epc-spare-parts-page-setup.php
 * Run: https://www.epartscart.com/epc-spare-parts-page-revert.php?token=epartscart-deploy-2026
 */
error_reporting(E_ALL);
ini_set('display_errors', 0);
header('Content-Type: text/plain; charset=utf-8');

require_once __DIR__ . '/epc_deploy_auth.php';
epc_deploy_require_token();

define('_ASTEXE_', 1);
require_once __DIR__ . '/config.php';

$cfg = new DP_Config();
$epcTenantHostDbFile = __DIR__ . '/config.tenant-host-db.php';
if (is_file($epcTenantHostDbFile)) {
	$epc_tenant_host_db = null;
	require $epcTenantHostDbFile;
	$host = strtolower((string) ($_SERVER['HTTP_HOST'] ?? 'www.epartscart.com'));
	if (strpos($host, ':') !== false) {
		$host = explode(':', $host, 2)[0];
	}
	if (isset($epc_tenant_host_db) && is_array($epc_tenant_host_db) && isset($epc_tenant_host_db[$host])) {
		foreach (array('db', 'user', 'password') as $epcTk) {
			if (!empty($epc_tenant_host_db[$host][$epcTk]) && property_exists($cfg, $epcTk)) {
				$cfg->$epcTk = $epc_tenant_host_db[$host][$epcTk];
			}
		}
	}
}

$pdo = new PDO('mysql:host=' . $cfg->host . ';dbname=' . $cfg->db . ';charset=utf8', $cfg->user, $cfg->password);
$pdo->setAttribute(PDO::ATTR_ERRMODE, PDO::ERRMODE_EXCEPTION);

function epc_sp_rev_href_match($item, array $needles)
{
	$href = isset($item['href']) ? (string) $item['href'] : '';
	foreach ($needles as $n) {
		if ($n !== '' && (strpos($href, $n) !== false
			|| (isset($item['a_innerhtml']) && $item['a_innerhtml'] === $n)
			|| (isset($item['value']) && $item['value'] === $n))) {
			return true;
		}
	}
	return false;
}

$contentId = 0;
$stmt = $pdo->prepare('SELECT `id` FROM `content` WHERE `is_frontend` = 1 AND `url` = ? LIMIT 1');
$stmt->execute(array('spare-parts'));
$contentId = (int) $stmt->fetchColumn();

$menuUpdated = array();
$stmt = $pdo->query('SELECT `id`, `structure` FROM `menu` WHERE `is_frontend` = 1 ORDER BY `id`');
while ($row = $stmt->fetch(PDO::FETCH_ASSOC)) {
	$menuId = (int) $row['id'];
	$structure = json_decode($row['structure'], true);
	if (!is_array($structure)) {
		continue;
	}
	$changed = false;
	$newStructure = array();
	foreach ($structure as $item) {
		if (!is_array($item)) {
			$newStructure[] = $item;
			continue;
		}
		if ($contentId > 0 && isset($item['content_id']) && (int) $item['content_id'] === $contentId) {
			$changed = true;
			continue;
		}
		if (epc_sp_rev_href_match($item, array('spare-parts', 'epc_menu_spare_parts', 'Spare Parts'))) {
			$changed = true;
			continue;
		}
		$newStructure[] = $item;
	}
	if ($changed) {
		$pdo->prepare('UPDATE `menu` SET `structure` = ? WHERE `id` = ?')->execute(array(json_encode($newStructure), $menuId));
		$menuUpdated[] = $menuId;
	}
}

if ($contentId > 0) {
	$pdo->prepare('DELETE FROM `content_access` WHERE `content_id` = ?')->execute(array($contentId));
	$pdo->prepare('UPDATE `content` SET `published_flag` = 0 WHERE `id` = ?')->execute(array($contentId));
}

echo "content_id={$contentId} unpublished\n";
echo 'menu_updated=' . ($menuUpdated ? implode(',', $menuUpdated) : 'none') . "\n";
echo "db=" . $cfg->db . "\n";
echo "OK\n";
