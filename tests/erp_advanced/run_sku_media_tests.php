<?php
/**
 * Offline tests for SKU photos + multi-type specifications.
 *
 *   php tests/erp_advanced/run_sku_media_tests.php
 */
declare(strict_types=1);

define('_ASTEXE_', 1);
$root = dirname(__DIR__, 2);
$_SERVER['DOCUMENT_ROOT'] = $root;

require_once $root . '/content/shop/catalogue/epc_sku_media.php';

$pass = 0;
$fail = 0;
function check(string $label, bool $ok): void
{
	global $pass, $fail;
	if ($ok) {
		$pass++;
		echo "  PASS  $label\n";
	} else {
		$fail++;
		echo "  FAIL  $label\n";
	}
}

echo "== SKU media & specs ==\n";

check('ensure_schema exists', function_exists('epc_sku_media_ensure_schema'));
check('upsert exists', function_exists('epc_sku_media_upsert_profile'));
check('photo types include product+diagram', isset(epc_sku_media_photo_types()['product']) && isset(epc_sku_media_photo_types()['diagram']));
check('value types include text+number+bool+list+rich', count(epc_sku_media_value_types()) >= 5);
check('default spec types >= 5', count(epc_sku_media_default_spec_types()) >= 5);

check('normalize article strips junk', epc_sku_media_normalize_article('0 986-494/053') === '0986494053');
check('normalize brand trims spaces', epc_sku_media_normalize_brand('  Bosch  ') === 'Bosch');

check('format bool yes', epc_sku_media_format_value('1', 'bool') === 'Yes');
check('format bool no', epc_sku_media_format_value('0', 'bool') === 'No');
check('format number with unit', epc_sku_media_format_value('12.5', 'number', 'mm') === '12.5 mm');
check('format list', epc_sku_media_format_value('A, B, C', 'list') === 'A, B, C');

// Optional DB round-trip when PDO SQLite is available.
if (in_array('sqlite', PDO::getAvailableDrivers(), true)) {
	$db = new PDO('sqlite::memory:');
	$db->setAttribute(PDO::ATTR_ERRMODE, PDO::ERRMODE_EXCEPTION);
	$db->exec('CREATE TABLE epc_sku_profiles (
		id INTEGER PRIMARY KEY AUTOINCREMENT,
		product_id INTEGER NULL,
		brand TEXT NOT NULL DEFAULT "",
		article TEXT NOT NULL DEFAULT "",
		article_key TEXT NOT NULL DEFAULT "",
		title TEXT NOT NULL DEFAULT "",
		subtitle TEXT NOT NULL DEFAULT "",
		status TEXT NOT NULL DEFAULT "active",
		created_at INTEGER NOT NULL DEFAULT 0,
		updated_at INTEGER NOT NULL DEFAULT 0
	)');
	$db->exec('CREATE TABLE epc_sku_photos (
		id INTEGER PRIMARY KEY AUTOINCREMENT,
		profile_id INTEGER NOT NULL,
		file_name TEXT NOT NULL DEFAULT "",
		alt TEXT NOT NULL DEFAULT "",
		caption TEXT NOT NULL DEFAULT "",
		photo_type TEXT NOT NULL DEFAULT "product",
		sort_order INTEGER NOT NULL DEFAULT 0,
		is_primary INTEGER NOT NULL DEFAULT 0,
		created_at INTEGER NOT NULL DEFAULT 0
	)');
	$db->exec('CREATE TABLE epc_sku_spec_groups (
		id INTEGER PRIMARY KEY AUTOINCREMENT,
		profile_id INTEGER NOT NULL,
		name TEXT NOT NULL DEFAULT "",
		code TEXT NOT NULL DEFAULT "",
		icon TEXT NOT NULL DEFAULT "fa-list",
		sort_order INTEGER NOT NULL DEFAULT 0,
		created_at INTEGER NOT NULL DEFAULT 0
	)');
	$db->exec('CREATE TABLE epc_sku_spec_rows (
		id INTEGER PRIMARY KEY AUTOINCREMENT,
		group_id INTEGER NOT NULL,
		profile_id INTEGER NOT NULL,
		label TEXT NOT NULL DEFAULT "",
		value TEXT NULL,
		value_type TEXT NOT NULL DEFAULT "text",
		unit TEXT NOT NULL DEFAULT "",
		sort_order INTEGER NOT NULL DEFAULT 0,
		created_at INTEGER NOT NULL DEFAULT 0
	)');

	$profile = epc_sku_media_upsert_profile($db, array(
		'brand' => 'Bosch',
		'article' => '0 986 494 053',
		'title' => 'Brake pad set',
		'product_id' => 42,
	));
	check('upsert creates profile', (int) ($profile['id'] ?? 0) > 0);
	$pid = (int) $profile['id'];
	check('article_key normalized', ($profile['article_key'] ?? '') === '0986494053');

	$found = epc_sku_media_find_profile($db, 0, 42, '', '');
	check('find by product_id', is_array($found) && (int) $found['id'] === $pid);
	$found2 = epc_sku_media_find_profile($db, 0, 0, 'Bosch', '0986494053');
	check('find by brand+article', is_array($found2) && (int) $found2['id'] === $pid);

	$g = epc_sku_media_add_spec_group($db, $pid, array('name' => 'Technical', 'code' => 'technical', 'icon' => 'fa-cogs'));
	check('add spec group', !empty($g['ok']) && (int) ($g['id'] ?? 0) > 0);
	$gid = (int) $g['id'];
	$r1 = epc_sku_media_add_spec_row($db, $gid, array('label' => 'Thickness', 'value' => '12.5', 'value_type' => 'number', 'unit' => 'mm'));
	$r2 = epc_sku_media_add_spec_row($db, $gid, array('label' => 'OEM fit', 'value' => '1', 'value_type' => 'bool'));
	check('add unlimited spec rows', !empty($r1['ok']) && !empty($r2['ok']));

	$g2 = epc_sku_media_add_spec_group($db, $pid, array('name' => 'Dimensions', 'code' => 'dimensions'));
	check('multiple specification types', !empty($g2['ok']));
	epc_sku_media_add_spec_row($db, (int) $g2['id'], array('label' => 'Width', 'value' => '100', 'value_type' => 'number', 'unit' => 'mm'));

	$payload = epc_sku_media_full_payload($db, $pid);
	check('full payload has 2 groups', is_array($payload) && count($payload['spec_groups'] ?? []) === 2);

	$db->prepare('INSERT INTO epc_sku_photos (profile_id, file_name, alt, caption, photo_type, sort_order, is_primary, created_at) VALUES (?,?,?,?,?,?,?,?)')
		->execute(array($pid, 'demo.jpg', 'Pad', 'Front view', 'product', 10, 1, time()));
	$db->prepare('INSERT INTO epc_sku_photos (profile_id, file_name, alt, caption, photo_type, sort_order, is_primary, created_at) VALUES (?,?,?,?,?,?,?,?)')
		->execute(array($pid, 'demo2.jpg', 'Box', 'Packaging', 'packaging', 20, 0, time()));
	$photos = epc_sku_media_photos($db, $pid);
	check('unlimited photos listed', count($photos) === 2);
	check('primary photo first', (int) ($photos[0]['is_primary'] ?? 0) === 1);
	$list = epc_sku_media_list_profiles($db, 'Bosch');
	check('list search finds brand', count($list) === 1 && (int) $list[0]['photo_count'] === 2);
	check('delete spec row', epc_sku_media_delete_spec_row($db, (int) $r2['id']));
	check('delete group', epc_sku_media_delete_spec_group($db, (int) $g2['id']));
} else {
	check('sqlite driver optional (skipped CRUD)', true);
	check('CRUD helpers exist', function_exists('epc_sku_media_add_spec_group')
		&& function_exists('epc_sku_media_add_spec_row')
		&& function_exists('epc_sku_media_photos')
		&& function_exists('epc_sku_media_full_payload'));
}
// File / wiring checks
$files = array(
	'content/shop/catalogue/epc_sku_media.php',
	'content/shop/catalogue/epc_sku_media_cp_install.php',
	'content/shop/catalogue/ajax_epc_sku_media.php',
	'content/shop/catalogue/epc_sku_media.css',
	'content/shop/catalogue/epc_sku_media.js',
	'content/shop/catalogue/epc_sku_media_storefront.php',
	'cp/content/shop/catalogue/epc_sku_media_manager.php',
	'epc-sku-media-install.php',
);
foreach ($files as $f) {
	check('file ' . $f, is_file($root . '/' . $f));
}

$manager = (string) file_get_contents($root . '/cp/content/shop/catalogue/epc_sku_media_manager.php');
check('manager has unlimited photos copy', strpos($manager, 'unlimited') !== false);
check('manager loads JS', strpos($manager, 'epc_sku_media.js') !== false);

$productCp = (string) file_get_contents($root . '/cp/content/shop/catalogue/product.php');
check('product editor links to sku_media', strpos($productCp, 'shop/catalogue/sku_media') !== false);

$customer = (string) file_get_contents($root . '/content/shop/catalogue/product_page_for_customer.php');
check('storefront page calls SKU renderer', strpos($customer, 'epc_sku_media_render_storefront') !== false);

$dash = (string) file_get_contents($root . '/cp/content/control/epc_tenant_cp_dashboard.php');
check('tenant dashboard tile present', strpos($dash, 'SKU photos & specs') !== false);

$shortcuts = (string) file_get_contents($root . '/content/shop/finance/epc_erp_shortcut_icons.php');
check('shortcuts catalogue includes sku_media', strpos($shortcuts, "'sku_media'") !== false);

$storefront = (string) file_get_contents($root . '/content/shop/catalogue/epc_sku_media_storefront.php');
check('storefront has gallery + spec groups', strpos($storefront, 'epc-sku-storefront__gallery') !== false
	&& strpos($storefront, 'epc-sku-storefront__group') !== false);

echo "\n----------------------------\n";
echo "Passed: $pass  Failed: $fail\n";
exit($fail > 0 ? 1 : 0);
