<?php
// PHP 8.3 goldens for plan Q1-foam (accessories marketplace DB). Taxonomy parent stays injected.
ini_set('display_errors', 'stderr');
date_default_timezone_set('UTC');
$root = rtrim($argv[1] ?? '/workspace', '/');
$spec = json_decode(file_get_contents(__DIR__ . '/cases.json'), true);

function foam_dsn(): array
{
	$pass = getenv('ECOMAE_LOCAL_MARIADB_E2E_DSN') ?: '';
	if ($pass === '') {
		throw new RuntimeException('missing ECOMAE_LOCAL_MARIADB_E2E_DSN');
	}
	return array('ecomae', $pass, '127.0.0.1', '3306');
}

function foam_tax(): array
{
	return array(
		'categories' => array(
			array(
				'slug' => 'brakes',
				'label' => 'Brakes',
				'pw_id' => 1,
				'children' => array(
					array('slug' => 'pads', 'label' => 'Brake Pads', 'pw_id' => 11),
				),
			),
			array(
				'slug' => 'filters',
				'label' => 'Filters',
				'pw_id' => 2,
				'children' => array(
					array('slug' => 'oil', 'label' => 'Oil Filters', 'pw_id' => 21),
				),
			),
		),
		'makes' => array('Toyota', 'Nissan'),
		'cities' => array('Dubai', 'Abu Dhabi', 'Sharjah'),
		'filters' => array(),
	);
}

function foam_patch(string $src, string $dest): void
{
	$code = file_get_contents($src);
	$code = str_replace(
		"require_once \$_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_accessories_taxonomy.php';",
		'// leftover taxonomy injected',
		$code
	);
	file_put_contents($dest, $code);
}

function foam_stubs(): void
{
	if (!function_exists('epc_acc_taxonomy_json_path')) {
		function epc_acc_taxonomy_json_path(): string
		{
			return $GLOBALS['FOAM_TAX_PATH'];
		}
	}
	if (!function_exists('epc_acc_load_taxonomy_json')) {
		function epc_acc_load_taxonomy_json(): array
		{
			return foam_tax();
		}
	}
}

function foam_include(): void
{
	foam_stubs();
	include_once $GLOBALS['FOAM_PAGE'];
}

function foam_run_names()
{
	$_SERVER['DOCUMENT_ROOT'] = '/shop/acme';
	$GLOBALS['FOAM_TAX_PATH'] = '/tmp/foam5/tax.json';
	foam_include();
	list($user, $pass, $host, $port) = foam_dsn();
	$dummy = new PDO('mysql:host=' . $host . ';port=' . $port . ';charset=utf8mb4', $user, $pass);
	$emptyMany = epc_acc_photos_add_many_from_files($dummy, 0, array());
	return array(
		epc_acc_slugify('Brake Pads'),
		epc_acc_slugify('  '),
		epc_acc_slugify('Oil/Filter #2'),
		epc_acc_storefront_url(0),
		epc_acc_storefront_url(12, '/en'),
		epc_acc_storefront_url(array('id' => 7, 'category' => 'brakes', 'subcategory' => 'pads'), '/ar'),
		epc_acc_storefront_url(array('id' => 0), ''),
		epc_acc_is_outbound_external_url('') ? 1 : 0,
		epc_acc_is_outbound_external_url('/en/accessories-spare-parts?category=brakes') ? 1 : 0,
		epc_acc_is_outbound_external_url('/en/accessories-spare-parts?id=9') ? 1 : 0,
		epc_acc_is_outbound_external_url('https://pakwheels.com/x') ? 1 : 0,
		epc_acc_is_outbound_external_url('/en/parts/oc47') ? 1 : 0,
		epc_acc_photo_public_url(''),
		epc_acc_photo_public_url('../acc_1.jpg'),
		epc_acc_uae_cities(),
		epc_acc_legacy_pk_cities()[0],
		count(epc_acc_legacy_pk_cities()),
		substr(epc_acc_taxonomy_json_path(), -10),
		$emptyMany,
	);
}

function foam_run_terms()
{
	foam_include();
	list($user, $pass, $host, $port) = foam_dsn();
	$schema = 'ecomae_cpw_foam_' . substr(md5(uniqid('', true)), 0, 8);
	$admin = new PDO('mysql:host=' . $host . ';port=' . $port . ';charset=utf8mb4', $user, $pass);
	$admin->setAttribute(PDO::ATTR_ERRMODE, PDO::ERRMODE_EXCEPTION);
	$admin->exec('CREATE DATABASE `' . $schema . '` CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci');
	try {
		$db = new PDO('mysql:host=' . $host . ';port=' . $port . ';dbname=' . $schema . ';charset=utf8mb4', $user, $pass);
		$db->setAttribute(PDO::ATTR_ERRMODE, PDO::ERRMODE_EXCEPTION);
		epc_acc_ensure_schema($db);
		$seed = epc_acc_seed_terms_from_json($db);
		$makes = epc_acc_term_labels($db, 'make');
		$cities = epc_acc_term_labels($db, 'city');
		$conds = epc_acc_get_terms($db, 'condition');
		$newId = epc_acc_save_term($db, 'make', 'Honda');
		$off = epc_acc_set_term_active($db, $newId, false) ? 1 : 0;
		$activeMakes = epc_acc_term_labels($db, 'make');
		$del = epc_acc_delete_term($db, $newId) ? 1 : 0;
		$db->prepare('INSERT INTO `epc_acc_listings` (`title`,`city`,`currency`,`status`,`created_at`,`updated_at`) VALUES (?,?,?,?,1,1)')
			->execute(array('Pad | Karachi', 'Karachi', 'PKR', 'published'));
		$mig = epc_acc_migrate_uae_locale($db);
		$row = $db->query('SELECT `title`, `city`, `currency` FROM `epc_acc_listings` LIMIT 1')->fetch(PDO::FETCH_ASSOC);
		return array(
			$seed['makes'],
			$seed['cities'],
			$seed['conditions'],
			$seed['years'],
			$makes,
			$cities,
			$conds[0]['value'] ?? '',
			$conds[0]['label'] ?? '',
			$newId > 0 ? 1 : 0,
			$off,
			$activeMakes,
			$del,
			$mig['cities_active'],
			$mig['listings_city'] > 0 ? 1 : 0,
			$mig['listings_currency'] > 0 ? 1 : 0,
			$mig['titles'] > 0 ? 1 : 0,
			$row['city'],
			$row['currency'],
			strpos((string) $row['title'], 'Dubai') !== false ? 1 : 0,
		);
	} finally {
		$admin->exec('DROP DATABASE IF EXISTS `' . $schema . '`');
	}
}

function foam_run_listings()
{
	foam_include();
	list($user, $pass, $host, $port) = foam_dsn();
	$schema = 'ecomae_cpw_foam_' . substr(md5(uniqid('', true)), 0, 8);
	$admin = new PDO('mysql:host=' . $host . ';port=' . $port . ';charset=utf8mb4', $user, $pass);
	$admin->setAttribute(PDO::ATTR_ERRMODE, PDO::ERRMODE_EXCEPTION);
	$admin->exec('CREATE DATABASE `' . $schema . '` CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci');
	try {
		$db = new PDO('mysql:host=' . $host . ';port=' . $port . ';dbname=' . $schema . ';charset=utf8mb4', $user, $pass);
		$db->setAttribute(PDO::ATTR_ERRMODE, PDO::ERRMODE_EXCEPTION);
		$seeded = epc_acc_seed_categories_from_json($db, false);
		$tree = epc_acc_get_category_tree($db);
		$adminTree = epc_acc_admin_category_tree($db);
		$extra = epc_acc_save_category($db, 'Extra Cat');
		$dup = epc_acc_save_category($db, 'Extra Cat');
		$off = epc_acc_set_category_active($db, $extra, false);
		$brakesId = (int) $tree[0]['id'];
		$padsId = (int) $tree[0]['children'][0]['id'];
		$lid = epc_acc_add_listing($db, array(
			'category_id' => $brakesId,
			'subcategory_id' => $padsId,
			'title' => 'Pad kit',
			'make' => 'Toyota',
			'model' => 'Corolla',
			'city' => 'Dubai',
			'price' => 12.5,
			'featured' => 1,
			'stock_qty' => 4,
		));
		$got = epc_acc_get_listing($db, $lid);
		epc_acc_update_listing($db, $lid, array(
			'category_id' => $brakesId,
			'subcategory_id' => $padsId,
			'title' => 'Pad kit HD',
			'make' => 'Toyota',
			'model' => 'Corolla',
			'city' => 'Sharjah',
			'price' => 15,
			'status' => 'published',
		));
		epc_acc_set_listing_status($db, $lid, 'draft');
		$search = epc_acc_admin_search($db, array('q' => 'Pad', 'status' => 'draft'));
		$used = epc_acc_delete_category($db, $brakesId);
		epc_acc_set_listing_status($db, $lid, 'published');
		$okDel = epc_acc_delete_listing($db, $lid);
		$free = epc_acc_delete_category($db, $brakesId);
		return array(
			$seeded['parents'],
			$seeded['children'],
			count($tree),
			$tree[0]['slug'],
			$tree[0]['children'][0]['slug'],
			count($adminTree),
			$extra > 0 ? 1 : 0,
			$dup !== $extra ? 1 : 0,
			$off ? 1 : 0,
			$lid > 0 ? 1 : 0,
			(string) ($got['title'] ?? ''),
			(float) ($got['price'] ?? 0),
			$search['total'],
			$search['items'][0]['title'] ?? '',
			$search['items'][0]['city'] ?? '',
			$used['ok'] ? 1 : 0,
			$used['message'],
			$okDel ? 1 : 0,
			$free['ok'] ? 1 : 0,
		);
	} finally {
		$admin->exec('DROP DATABASE IF EXISTS `' . $schema . '`');
	}
}

function foam_run_market()
{
	foam_include();
	list($user, $pass, $host, $port) = foam_dsn();
	$schema = 'ecomae_cpw_foam_' . substr(md5(uniqid('', true)), 0, 8);
	$admin = new PDO('mysql:host=' . $host . ';port=' . $port . ';charset=utf8mb4', $user, $pass);
	$admin->setAttribute(PDO::ATTR_ERRMODE, PDO::ERRMODE_EXCEPTION);
	$admin->exec('CREATE DATABASE `' . $schema . '` CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci');
	try {
		$db = new PDO('mysql:host=' . $host . ';port=' . $port . ';dbname=' . $schema . ';charset=utf8mb4', $user, $pass);
		$db->setAttribute(PDO::ATTR_ERRMODE, PDO::ERRMODE_EXCEPTION);
		epc_acc_seed_categories_from_json($db, false);
		$tree = epc_acc_get_category_tree($db);
		$brakesId = (int) $tree[0]['id'];
		$padsId = (int) $tree[0]['children'][0]['id'];
		$a = epc_acc_add_listing($db, array(
			'category_id' => $brakesId,
			'subcategory_id' => $padsId,
			'title' => 'Pad kit',
			'make' => 'Toyota',
			'city' => 'Dubai',
			'price' => 20,
			'featured' => 1,
			'stock_qty' => 2,
			'external_url' => 'https://example.com/x',
		));
		$b = epc_acc_add_listing($db, array(
			'category_id' => $brakesId,
			'subcategory_id' => $padsId,
			'title' => 'Cheap pad',
			'make' => 'Nissan',
			'city' => 'Sharjah',
			'price' => 8,
			'stock_qty' => 9,
		));
		epc_acc_add_listing($db, array(
			'category_id' => $brakesId,
			'subcategory_id' => $padsId,
			'title' => 'Hidden',
			'make' => 'Toyota',
			'city' => 'Dubai',
			'price' => 99,
			'status' => 'draft',
		));
		$db->prepare('INSERT INTO `epc_acc_photos` (`listing_id`,`file_name`,`sort_order`,`is_primary`,`created_at`) VALUES (?,?,?,?,1)')
			->execute(array($a, 'acc_a.jpg', 10, 0));
		$db->prepare('INSERT INTO `epc_acc_photos` (`listing_id`,`file_name`,`sort_order`,`is_primary`,`created_at`) VALUES (?,?,?,?,1)')
			->execute(array($a, 'acc_b.jpg', 20, 1));
		epc_acc_photos_sync_listing($db, $a);
		$photos = epc_acc_photos_list($db, $a);
		$prim = epc_acc_photos_set_primary($db, $a, (int) $photos[1]['id']);
		$gone = epc_acc_photos_delete($db, $a, (int) $photos[0]['id']);
		$all = epc_acc_marketplace_search($db, array());
		$toy = epc_acc_marketplace_search($db, array('make' => 'Toyota', 'sort' => 'price-asc'));
		$one = epc_acc_marketplace_search($db, array('id' => $b));
		return array(
			$all['total'],
			$all['empty_catalog'] ? 1 : 0,
			$all['source'],
			$all['items'][0]['title'],
			$all['items'][0]['featured'] ? 1 : 0,
			$all['items'][0]['external_url'],
			$all['items'][0]['detail_url'],
			$all['items'][0]['photo_count'],
			count($all['facets']['categories']),
			$all['facets']['categories'][0]['count'],
			$toy['total'],
			$toy['items'][0]['make'],
			$one['total'],
			$one['items'][0]['title'],
			count($photos),
			$photos[0]['is_primary'] ? 1 : 0,
			$prim['ok'] ? 1 : 0,
			$gone['ok'] ? 1 : 0,
			count($gone['photos'] ?? array()),
		);
	} finally {
		$admin->exec('DROP DATABASE IF EXISTS `' . $schema . '`');
	}
}

if (isset($argv[2])) {
	$case = $spec['cases'][(int) $argv[2]];
	$tmp = sys_get_temp_dir() . '/ecomae_foam_' . substr(md5(uniqid('', true)), 0, 8);
	@mkdir($tmp, 0777, true);
	$tax = $tmp . '/tax.json';
	file_put_contents($tax, json_encode(foam_tax()));
	$GLOBALS['FOAM_TAX_PATH'] = $tax;
	foam_patch($root . '/content/shop/docpart/epc_accessories_db.php', $tmp . '/page.php');
	$GLOBALS['FOAM_PAGE'] = $tmp . '/page.php';
	$_SERVER['DOCUMENT_ROOT'] = $root;
	$fn = 'foam_run_' . $case['name'];
	$result = $fn();
	@unlink($tmp . '/page.php');
	@unlink($tax);
	@rmdir($tmp);
	echo json_encode(array('name' => $case['name'], 'output' => '', 'result' => $result), JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES | JSON_PARTIAL_OUTPUT_ON_ERROR);
	exit(0);
}

$results = array();
foreach ($spec['cases'] as $i => $case) {
	$cmd = escapeshellarg(PHP_BINARY) . ' ' . escapeshellarg(__FILE__) . ' ' . escapeshellarg($root) . ' ' . $i;
	$out = shell_exec($cmd . ' 2>/tmp/plan_q1foam_err_' . $i . '.txt');
	$row = json_decode((string) $out, true);
	if (!is_array($row)) {
		fwrite(STDERR, "case {$case['name']} failed:\n" . $out . "\n" . @file_get_contents('/tmp/plan_q1foam_err_' . $i . '.txt') . "\n");
		exit(1);
	}
	$results[] = $row;
}
echo json_encode(array('php' => PHP_VERSION, 'results' => $results), JSON_PRETTY_PRINT | JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES), "\n";
