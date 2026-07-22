<?php
/**
 * Clear bogus accessories external_url values (category browse links) and
 * sync listing.image_url from gallery photos so detail pages show a cover.
 *
 * Run: /epc-acc-fix-detail-links.php?token=...&apply=1
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
require_once __DIR__ . '/content/shop/docpart/epc_accessories_db.php';

$apply = !empty($_GET['apply']) || !empty($_POST['apply']);

$cfg = new DP_Config();
$GLOBALS['DP_Config'] = $cfg;
$epcTenantHostDbFile = __DIR__ . '/config.tenant-host-db.php';
if (is_file($epcTenantHostDbFile)) {
	$epc_tenant_host_db = null;
	require $epcTenantHostDbFile;
	$host = strtolower((string) ($_SERVER['HTTP_HOST'] ?? ''));
	if (strpos($host, ':') !== false) {
		$host = explode(':', $host, 2)[0];
	}
	if (isset($epc_tenant_host_db) && is_array($epc_tenant_host_db) && isset($epc_tenant_host_db[$host])) {
		foreach (array('db', 'user', 'password', 'host') as $epcTk) {
			if (!empty($epc_tenant_host_db[$host][$epcTk]) && property_exists($cfg, $epcTk)) {
				$cfg->$epcTk = $epc_tenant_host_db[$host][$epcTk];
			}
		}
	}
}
if (is_file(__DIR__ . '/content/general_pages/epc_portal.php')) {
	require_once __DIR__ . '/content/general_pages/epc_portal.php';
	if (function_exists('epc_portal_apply_config')) {
		epc_portal_apply_config($cfg);
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

epc_acc_ensure_schema($pdo);
echo "schema=ok\n";
echo 'apply=' . ($apply ? '1' : '0') . "\n";

$bad = $pdo->query(
	"SELECT COUNT(*) FROM `epc_acc_listings`
	 WHERE `external_url` <> ''
	   AND `external_url` LIKE '%accessories%'
	   AND `external_url` NOT REGEXP '[?&]id=[0-9]+'"
)->fetchColumn();
echo "bad_external_url_count={$bad}\n";

$emptyCover = $pdo->query(
	"SELECT COUNT(*) FROM `epc_acc_listings` l
	 WHERE (l.`image_url` = '' OR l.`image_url` IS NULL)
	   AND EXISTS (SELECT 1 FROM `epc_acc_photos` p WHERE p.`listing_id` = l.`id`)"
)->fetchColumn();
echo "listings_missing_cover_with_photos={$emptyCover}\n";

if (!$apply) {
	echo "Dry run — add &apply=1 to clear category external_url values and sync covers.\n";
	exit;
}

$cleared = $pdo->exec(
	"UPDATE `epc_acc_listings`
	 SET `external_url` = '', `updated_at` = UNIX_TIMESTAMP()
	 WHERE `external_url` <> ''
	   AND `external_url` LIKE '%accessories%'
	   AND `external_url` NOT REGEXP '[?&]id=[0-9]+'"
);
echo "cleared_external_url={$cleared}\n";

$ids = $pdo->query(
	"SELECT l.`id` FROM `epc_acc_listings` l
	 WHERE (l.`image_url` = '' OR l.`image_url` IS NULL)
	   AND EXISTS (SELECT 1 FROM `epc_acc_photos` p WHERE p.`listing_id` = l.`id`)"
)->fetchAll(PDO::FETCH_COLUMN);
$synced = 0;
foreach ($ids as $id) {
	epc_acc_photos_sync_listing($pdo, (int) $id);
	$synced++;
}
echo "synced_covers={$synced}\n";

$sample = $pdo->query(
	"SELECT `id`, `title`, `image_url`, `external_url`, `photo_count`
	 FROM `epc_acc_listings`
	 WHERE `id` IN (646, 647, 648)
	 ORDER BY `id`"
)->fetchAll(PDO::FETCH_ASSOC);
foreach ($sample as $row) {
	echo 'sample id=' . $row['id']
		. ' ext=' . json_encode((string) $row['external_url'])
		. ' img=' . json_encode((string) $row['image_url'])
		. ' photos=' . (int) $row['photo_count']
		. "\n";
}
echo "Done.\n";
