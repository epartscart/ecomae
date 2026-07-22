<?php
/**
 * Seed demo SKU media (photo + specifications) for a live warehouse article.
 * Shows on storefront part search: /en/parts/{BRAND}/{ARTICLE}
 *
 * Run: /epc-sku-media-seed-demo.php?token=...&apply=1
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
require_once __DIR__ . '/content/shop/catalogue/epc_sku_media.php';
require_once __DIR__ . '/content/shop/docpart/docpart_article_match.php';

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
$GLOBALS['db_link'] = $pdo;

epc_sku_media_ensure_schema($pdo);
echo "schema=ok\n";
echo 'apply=' . ($apply ? '1' : '0') . "\n";

/**
 * Pick a demo article that exists in warehouse stock.
 *
 * @return array{brand:string,article:string,name:string}
 */
function epc_sku_media_seed_pick_article(PDO $db): array
{
	$candidates = array(
		array('AISIN', 'CMT033'),
		array('AISIN', 'DT068'),
		array('TOYOTA', '3141060432'),
	);
	foreach ($candidates as $pair) {
		try {
			$st = $db->prepare(
				'SELECT `manufacturer`, `article_show`, `article`, `name`
				 FROM `shop_docpart_prices_data`
				 WHERE UPPER(`manufacturer`) = ? AND (`article_search` = ? OR `article` = ? OR `article_show` = ?)
				 LIMIT 1'
			);
			$key = preg_replace('/[^A-Za-z0-9]+/', '', strtoupper($pair[1])) ?? '';
			$st->execute(array(strtoupper($pair[0]), $key, $key, $pair[1]));
			$row = $st->fetch(PDO::FETCH_ASSOC);
			if (is_array($row)) {
				$art = trim((string) ($row['article_show'] !== '' ? $row['article_show'] : $row['article']));
				return array(
					'brand' => strtoupper(trim((string) $row['manufacturer'])),
					'article' => $art !== '' ? $art : $pair[1],
					'name' => trim((string) ($row['name'] ?? '')),
				);
			}
		} catch (Throwable $e) {
			// try next
		}
	}
	// Absolute fallback — still create a profile for demo.
	return array(
		'brand' => 'AISIN',
		'article' => 'CMT033',
		'name' => 'Clutch master cylinder (demo)',
	);
}

/**
 * Create a simple product-card PNG for the demo.
 */
function epc_sku_media_seed_make_demo_png(string $dest, string $brand, string $article, string $title): bool
{
	if (!function_exists('imagecreatetruecolor')) {
		return false;
	}
	$w = 960;
	$h = 720;
	$im = imagecreatetruecolor($w, $h);
	if (!$im) {
		return false;
	}
	$bg1 = imagecolorallocate($im, 15, 118, 110);
	$bg2 = imagecolorallocate($im, 15, 23, 42);
	$white = imagecolorallocate($im, 248, 250, 252);
	$muted = imagecolorallocate($im, 204, 251, 241);
	$accent = imagecolorallocate($im, 45, 212, 191);
	// Gradient-ish bands
	imagefilledrectangle($im, 0, 0, $w, $h, $bg2);
	imagefilledrectangle($im, 0, 0, $w, (int) ($h * 0.62), $bg1);
	imagefilledrectangle($im, 48, 48, $w - 48, $h - 48, $bg2);
	imagefilledrectangle($im, 56, 56, $w - 56, (int) ($h * 0.58), $bg1);

	$brand = strtoupper($brand);
	$article = strtoupper($article);
	imagestring($im, 5, 80, 90, 'eParts Cart · SKU MEDIA DEMO', $muted);
	imagestring($im, 5, 80, 150, $brand, $white);
	imagestring($im, 5, 80, 190, $article, $accent);
	$line = substr($title !== '' ? $title : ($brand . ' ' . $article), 0, 48);
	imagestring($im, 4, 80, 250, $line, $white);
	imagestring($im, 3, 80, 320, 'Photo + specifications sample for storefront', $muted);
	imagestring($im, 3, 80, 350, 'Visible on part search after login (prices gated)', $muted);
	// Simple "part" icon box
	imagefilledrectangle($im, $w - 280, 120, $w - 100, 300, $accent);
	imagefilledrectangle($im, $w - 260, 140, $w - 120, 280, $bg2);
	imagestring($im, 5, $w - 230, 190, 'OEM', $accent);

	$ok = imagepng($im, $dest);
	imagedestroy($im);
	return (bool) $ok;
}

$pick = epc_sku_media_seed_pick_article($pdo);
$brand = $pick['brand'];
$article = $pick['article'];
$name = $pick['name'] !== '' ? $pick['name'] : ($brand . ' ' . $article);
echo "picked={$brand}|{$article}\n";
echo 'name=' . $name . "\n";

$storeUrl = epc_sku_media_storefront_part_url($brand, $article, '/en');
echo 'storefront_url=' . $storeUrl . "\n";

if (!$apply) {
	echo "Dry run — add &apply=1 to write demo photo + specs.\n";
	exit;
}

$profile = epc_sku_media_upsert_profile($pdo, array(
	'brand' => $brand,
	'article' => $article,
	'title' => $name,
	'subtitle' => 'Demo SKU media — photo & detailed specifications',
	'status' => 'active',
));
$profileId = (int) ($profile['id'] ?? 0);
echo 'profile_id=' . $profileId . "\n";
if ($profileId <= 0) {
	exit("Failed to upsert profile\n");
}

// Clear previous demo photos/specs for a clean example (keep profile).
try {
	$oldPhotos = $pdo->prepare('SELECT `file_name` FROM `epc_sku_photos` WHERE `profile_id` = ?');
	$oldPhotos->execute(array($profileId));
	while ($ph = $oldPhotos->fetch(PDO::FETCH_ASSOC)) {
		$fn = basename((string) ($ph['file_name'] ?? ''));
		if ($fn !== '' && strpos($fn, 'sku_' . $profileId . '_') === 0) {
			@unlink(epc_sku_media_images_fs() . $fn);
		}
	}
	$pdo->prepare('DELETE FROM `epc_sku_photos` WHERE `profile_id` = ?')->execute(array($profileId));
	$pdo->prepare('DELETE FROM `epc_sku_spec_rows` WHERE `profile_id` = ?')->execute(array($profileId));
	$pdo->prepare('DELETE FROM `epc_sku_spec_groups` WHERE `profile_id` = ?')->execute(array($profileId));
} catch (Throwable $e) {
	echo "cleanup_warn=" . $e->getMessage() . "\n";
}

$tmpPng = rtrim(sys_get_temp_dir(), DIRECTORY_SEPARATOR) . DIRECTORY_SEPARATOR . 'epc_sku_demo_' . $profileId . '.png';
if (!epc_sku_media_seed_make_demo_png($tmpPng, $brand, $article, $name)) {
	exit("Could not generate demo PNG (GD missing?)\n");
}
$photo = epc_sku_media_attach_local_photo($pdo, $profileId, $tmpPng, array(
	'is_primary' => 1,
	'photo_type' => 'product',
	'alt' => $brand . ' ' . $article,
	'caption' => 'Primary product photo (demo)',
));
@unlink($tmpPng);
echo 'photo=' . json_encode($photo, JSON_UNESCAPED_SLASHES) . "\n";

// Detail / packaging second image
$tmpPng2 = rtrim(sys_get_temp_dir(), DIRECTORY_SEPARATOR) . DIRECTORY_SEPARATOR . 'epc_sku_demo2_' . $profileId . '.png';
if (epc_sku_media_seed_make_demo_png($tmpPng2, $brand, $article . ' DETAIL', 'Close-up / packaging view')) {
	$photo2 = epc_sku_media_attach_local_photo($pdo, $profileId, $tmpPng2, array(
		'is_primary' => 0,
		'photo_type' => 'detail',
		'alt' => $brand . ' ' . $article . ' detail',
		'caption' => 'Detail / close-up (demo)',
	));
	@unlink($tmpPng2);
	echo 'photo2=' . json_encode($photo2, JSON_UNESCAPED_SLASHES) . "\n";
}

$tech = epc_sku_media_add_spec_group($pdo, $profileId, array(
	'name' => 'Technical',
	'code' => 'technical',
	'icon' => 'fa-cogs',
));
$techId = (int) ($tech['id'] ?? 0);
echo 'group_technical=' . $techId . "\n";

$dims = epc_sku_media_add_spec_group($pdo, $profileId, array(
	'name' => 'Dimensions',
	'code' => 'dimensions',
	'icon' => 'fa-arrows-alt',
));
$dimsId = (int) ($dims['id'] ?? 0);
echo 'group_dimensions=' . $dimsId . "\n";

$fit = epc_sku_media_add_spec_group($pdo, $profileId, array(
	'name' => 'Fitment',
	'code' => 'fitment',
	'icon' => 'fa-car',
));
$fitId = (int) ($fit['id'] ?? 0);
echo 'group_fitment=' . $fitId . "\n";

$techRows = array(
	array('label' => 'Brand', 'value' => $brand, 'value_type' => 'text'),
	array('label' => 'Article / OEM', 'value' => $article, 'value_type' => 'text'),
	array('label' => 'Part type', 'value' => 'Clutch master cylinder', 'value_type' => 'text'),
	array('label' => 'Material', 'value' => 'Aluminium body / rubber seals', 'value_type' => 'text'),
	array('label' => 'Warranty', 'value' => '12', 'value_type' => 'number', 'unit' => 'months'),
	array('label' => 'Quality grade', 'value' => 'OEM-equivalent', 'value_type' => 'text'),
);
foreach ($techRows as $row) {
	if ($techId > 0) {
		epc_sku_media_add_spec_row($pdo, $techId, $row);
	}
}

$dimRows = array(
	array('label' => 'Length', 'value' => '185', 'value_type' => 'number', 'unit' => 'mm'),
	array('label' => 'Diameter', 'value' => '22.2', 'value_type' => 'number', 'unit' => 'mm'),
	array('label' => 'Weight', 'value' => '0.42', 'value_type' => 'number', 'unit' => 'kg'),
	array('label' => 'Ports', 'value' => '2', 'value_type' => 'number', 'unit' => 'pcs'),
);
foreach ($dimRows as $row) {
	if ($dimsId > 0) {
		epc_sku_media_add_spec_row($pdo, $dimsId, $row);
	}
}

$fitRows = array(
	array('label' => 'Vehicle example', 'value' => 'Toyota Land Cruiser 1990–1994', 'value_type' => 'text'),
	array('label' => 'OEM cross', 'value' => '31410-60432', 'value_type' => 'text'),
	array('label' => 'Notes', 'value' => 'Demo specification sheet for SKU media — replace with real datasheet values.', 'value_type' => 'rich'),
);
foreach ($fitRows as $row) {
	if ($fitId > 0) {
		epc_sku_media_add_spec_row($pdo, $fitId, $row);
	}
}

$lookup = epc_sku_media_public_lookup($pdo, $brand, $article, 0);
echo 'public_ok=' . (!empty($lookup['ok']) ? '1' : '0') . "\n";
echo 'public_photo=' . (string) ($lookup['url'] ?? '') . "\n";
echo 'public_specs=' . count($lookup['specs'] ?? array()) . "\n";
echo 'cp_url=/cp/shop/catalogue/sku_media?brand=' . rawurlencode($brand) . '&article=' . rawurlencode($article) . "\n";
echo "Done. Open storefront: {$storeUrl}\n";
