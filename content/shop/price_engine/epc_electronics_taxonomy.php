<?php
/**
 * EPC Electronics Taxonomy — Amazon node 172282 inspired hierarchy (3–4 levels).
 */
defined('_ASTEXE_') or die('No access');

function epc_tax_ensure_schema(PDO $pdo): void
{
	$pdo->exec(
		'CREATE TABLE IF NOT EXISTS `epc_product_taxonomy_nodes` (
			`id` INT UNSIGNED NOT NULL AUTO_INCREMENT PRIMARY KEY,
			`industry_key` VARCHAR(32) NOT NULL DEFAULT \'electronics\',
			`parent_id` INT UNSIGNED NOT NULL DEFAULT 0,
			`slug` VARCHAR(80) NOT NULL,
			`name_en` VARCHAR(160) NOT NULL,
			`amazon_node_ref` VARCHAR(32) NOT NULL DEFAULT \'\',
			`catalogue_category_id` INT UNSIGNED NOT NULL DEFAULT 0,
			`sort` INT NOT NULL DEFAULT 100,
			`level` TINYINT UNSIGNED NOT NULL DEFAULT 1,
			`active` TINYINT(1) NOT NULL DEFAULT 1,
			UNIQUE KEY `industry_slug` (`industry_key`, `slug`),
			KEY `parent_sort` (`parent_id`, `sort`),
			KEY `industry_parent` (`industry_key`, `parent_id`, `sort`),
			KEY `level` (`level`)
		) ENGINE=InnoDB DEFAULT CHARSET=utf8'
	);
	if (is_file(__DIR__ . '/epc_industry_taxonomy.php')) {
		require_once __DIR__ . '/epc_industry_taxonomy.php';
		epc_apai_taxonomy_migrate_schema($pdo);
	}
}

/**
 * @return array<int,array{slug:string,name:string,amazon?:string,sort?:int,children?:array}>
 */
function epc_tax_seed_tree(): array
{
	return array(
		array('slug' => 'computers', 'name' => 'Computers & Accessories', 'amazon' => '541966', 'sort' => 10, 'children' => array(
			array('slug' => 'computers-laptops', 'name' => 'Laptops', 'amazon' => '13896617011', 'children' => array(
				array('slug' => 'computers-laptops-traditional', 'name' => 'Traditional Laptops'),
				array('slug' => 'computers-laptops-2in1', 'name' => '2-in-1 Laptops'),
				array('slug' => 'computers-laptops-gaming', 'name' => 'Gaming Laptops'),
			)),
			array('slug' => 'computers-desktops', 'name' => 'Desktops', 'amazon' => '565098', 'children' => array(
				array('slug' => 'computers-desktops-towers', 'name' => 'Tower Desktops'),
				array('slug' => 'computers-desktops-allinone', 'name' => 'All-in-One PCs'),
				array('slug' => 'computers-desktops-mini', 'name' => 'Mini PCs'),
			)),
			array('slug' => 'computers-tablets', 'name' => 'Tablets', 'amazon' => '1232597011', 'children' => array(
				array('slug' => 'computers-tablets-android', 'name' => 'Android Tablets'),
				array('slug' => 'computers-tablets-ipad', 'name' => 'iPad & iOS Tablets'),
				array('slug' => 'computers-tablets-accessories', 'name' => 'Tablet Accessories'),
			)),
			array('slug' => 'computers-monitors', 'name' => 'Monitors', 'amazon' => '1292115011'),
			array('slug' => 'computers-accessories', 'name' => 'Computer Accessories', 'amazon' => '172456', 'children' => array(
				array('slug' => 'computers-accessories-keyboards', 'name' => 'Keyboards & Mice'),
				array('slug' => 'computers-accessories-storage', 'name' => 'External Storage'),
				array('slug' => 'computers-accessories-networking', 'name' => 'Networking'),
			)),
		)),
		array('slug' => 'tv-video', 'name' => 'TV, Video & Audio', 'amazon' => '1266092011', 'sort' => 20, 'children' => array(
			array('slug' => 'tv-video-televisions', 'name' => 'Televisions', 'amazon' => '21489946011', 'children' => array(
				array('slug' => 'tv-video-televisions-led', 'name' => 'LED & LCD TVs'),
				array('slug' => 'tv-video-televisions-oled', 'name' => 'OLED TVs'),
				array('slug' => 'tv-video-televisions-qled', 'name' => 'QLED TVs'),
			)),
			array('slug' => 'tv-video-projectors', 'name' => 'Projectors', 'amazon' => '300334'),
			array('slug' => 'tv-video-streaming', 'name' => 'Streaming Devices', 'amazon' => '13447451'),
			array('slug' => 'tv-video-home-theater', 'name' => 'Home Theater Audio', 'amazon' => '667846011', 'children' => array(
				array('slug' => 'tv-video-home-theater-soundbars', 'name' => 'Soundbars'),
				array('slug' => 'tv-video-home-theater-receivers', 'name' => 'AV Receivers'),
			)),
		)),
		array('slug' => 'cell-phones', 'name' => 'Cell Phones & Accessories', 'amazon' => '7072561011', 'sort' => 30, 'children' => array(
			array('slug' => 'cell-phones-unlocked', 'name' => 'Unlocked Cell Phones', 'amazon' => '2407755011', 'children' => array(
				array('slug' => 'cell-phones-unlocked-android', 'name' => 'Android Phones'),
				array('slug' => 'cell-phones-unlocked-iphone', 'name' => 'iPhones'),
				array('slug' => 'cell-phones-unlocked-foldable', 'name' => 'Foldable Phones'),
			)),
			array('slug' => 'cell-phones-cases', 'name' => 'Cases & Covers', 'amazon' => '2407761011'),
			array('slug' => 'cell-phones-chargers', 'name' => 'Chargers & Power', 'amazon' => '2407765011'),
			array('slug' => 'cell-phones-wearables-link', 'name' => 'Smartwatches (Phones)', 'amazon' => '7939901011'),
		)),
		array('slug' => 'camera-photo', 'name' => 'Camera & Photo', 'amazon' => '502394', 'sort' => 40, 'children' => array(
			array('slug' => 'camera-photo-digital', 'name' => 'Digital Cameras', 'amazon' => '281052', 'children' => array(
				array('slug' => 'camera-photo-digital-mirrorless', 'name' => 'Mirrorless Cameras'),
				array('slug' => 'camera-photo-digital-dslr', 'name' => 'DSLR Cameras'),
				array('slug' => 'camera-photo-digital-compact', 'name' => 'Point & Shoot'),
			)),
			array('slug' => 'camera-photo-lenses', 'name' => 'Lenses', 'amazon' => '3347771'),
			array('slug' => 'camera-photo-action', 'name' => 'Action Cameras', 'amazon' => '7161074011'),
			array('slug' => 'camera-photo-drones', 'name' => 'Camera Drones', 'amazon' => '14733106011'),
		)),
		array('slug' => 'headphones', 'name' => 'Headphones & Earbuds', 'amazon' => '172541', 'sort' => 50, 'children' => array(
			array('slug' => 'headphones-over-ear', 'name' => 'Over-Ear Headphones'),
			array('slug' => 'headphones-on-ear', 'name' => 'On-Ear Headphones'),
			array('slug' => 'headphones-earbuds', 'name' => 'Earbuds & In-Ear', 'children' => array(
				array('slug' => 'headphones-earbuds-true-wireless', 'name' => 'True Wireless Earbuds'),
				array('slug' => 'headphones-earbuds-sports', 'name' => 'Sports Earbuds'),
			)),
			array('slug' => 'headphones-noise-cancelling', 'name' => 'Noise Cancelling'),
		)),
		array('slug' => 'wearables', 'name' => 'Wearable Technology', 'amazon' => '10048700011', 'sort' => 60, 'children' => array(
			array('slug' => 'wearables-smartwatches', 'name' => 'Smartwatches', 'amazon' => '7939901011'),
			array('slug' => 'wearables-fitness', 'name' => 'Fitness Trackers', 'amazon' => '10048702011'),
			array('slug' => 'wearables-vr', 'name' => 'VR Headsets', 'amazon' => '14241167011'),
		)),
		array('slug' => 'smart-home', 'name' => 'Smart Home', 'amazon' => '6563140011', 'sort' => 70, 'children' => array(
			array('slug' => 'smart-home-assistants', 'name' => 'Smart Speakers & Displays', 'amazon' => '9818047011'),
			array('slug' => 'smart-home-lighting', 'name' => 'Smart Lighting', 'amazon' => '17386948011'),
			array('slug' => 'smart-home-security', 'name' => 'Smart Security', 'amazon' => '17386950011', 'children' => array(
				array('slug' => 'smart-home-security-cameras', 'name' => 'Security Cameras'),
				array('slug' => 'smart-home-security-doorbells', 'name' => 'Video Doorbells'),
			)),
			array('slug' => 'smart-home-climate', 'name' => 'Smart Thermostats & Climate'),
		)),
		array('slug' => 'gaming', 'name' => 'Video Games & Consoles', 'amazon' => '468642', 'sort' => 80, 'children' => array(
			array('slug' => 'gaming-consoles', 'name' => 'Game Consoles', 'amazon' => '6427814011', 'children' => array(
				array('slug' => 'gaming-consoles-playstation', 'name' => 'PlayStation'),
				array('slug' => 'gaming-consoles-xbox', 'name' => 'Xbox'),
				array('slug' => 'gaming-consoles-nintendo', 'name' => 'Nintendo'),
			)),
			array('slug' => 'gaming-accessories', 'name' => 'Gaming Accessories', 'amazon' => '318493011'),
			array('slug' => 'gaming-pc', 'name' => 'PC Gaming', 'amazon' => '8588809011'),
		)),
		array('slug' => 'car-electronics', 'name' => 'Car Electronics', 'amazon' => '3248684011', 'sort' => 90, 'children' => array(
			array('slug' => 'car-electronics-gps', 'name' => 'GPS & Navigation'),
			array('slug' => 'car-electronics-dash-cam', 'name' => 'Dash Cams'),
			array('slug' => 'car-electronics-car-audio', 'name' => 'Car Audio', 'children' => array(
				array('slug' => 'car-electronics-car-audio-speakers', 'name' => 'Car Speakers'),
				array('slug' => 'car-electronics-car-audio-head-units', 'name' => 'Head Units'),
			)),
		)),
		array('slug' => 'office-electronics', 'name' => 'Office Electronics', 'amazon' => '172574', 'sort' => 100, 'children' => array(
			array('slug' => 'office-electronics-printers', 'name' => 'Printers & Scanners', 'amazon' => '172635'),
			array('slug' => 'office-electronics-shredders', 'name' => 'Shredders'),
			array('slug' => 'office-electronics-telephones', 'name' => 'Office Telephones'),
		)),
		array('slug' => 'portable-audio', 'name' => 'Portable Audio & Video', 'amazon' => '667846011', 'sort' => 110, 'children' => array(
			array('slug' => 'portable-audio-bluetooth', 'name' => 'Bluetooth Speakers'),
			array('slug' => 'portable-audio-mp3', 'name' => 'MP3 & MP4 Players'),
			array('slug' => 'portable-audio-boomboxes', 'name' => 'Boomboxes'),
		)),
		array('slug' => 'pc-components', 'name' => 'PC Components', 'amazon' => '541966', 'sort' => 120, 'children' => array(
			array('slug' => 'pc-components-gpu', 'name' => 'Graphics cards', 'children' => array(
				array('slug' => 'pc-components-gpu-nvidia', 'name' => 'NVIDIA GeForce'),
				array('slug' => 'pc-components-gpu-amd', 'name' => 'AMD Radeon'),
			)),
			array('slug' => 'pc-components-cpu', 'name' => 'Processors (CPU)'),
			array('slug' => 'pc-components-ram', 'name' => 'Memory (RAM)'),
			array('slug' => 'pc-components-storage', 'name' => 'SSD & HDD storage'),
			array('slug' => 'pc-components-motherboard', 'name' => 'Motherboards'),
			array('slug' => 'pc-components-psu', 'name' => 'Power supplies'),
			array('slug' => 'pc-components-cooling', 'name' => 'PC cooling & fans'),
		)),
		array('slug' => 'electronics-accessories', 'name' => 'Electronics accessories', 'sort' => 130, 'children' => array(
			array('slug' => 'electronics-accessories-cables', 'name' => 'Cables & adapters'),
			array('slug' => 'electronics-accessories-power', 'name' => 'Power banks & chargers'),
			array('slug' => 'electronics-accessories-cases', 'name' => 'Cases & screen protectors'),
		)),
	);
}

function epc_tax_insert_node(PDO $pdo, array $node, int $parentId, int $level, int &$count): int
{
	$slug = (string) ($node['slug'] ?? '');
	$name = (string) ($node['name'] ?? $slug);
	$amazon = (string) ($node['amazon'] ?? '');
	$sort = (int) ($node['sort'] ?? (100 + $count));

	$industryKey = 'electronics';
	$chk = $pdo->prepare('SELECT `id` FROM `epc_product_taxonomy_nodes` WHERE `industry_key` = ? AND `slug` = ? LIMIT 1');
	$chk->execute(array($industryKey, $slug));
	$id = (int) $chk->fetchColumn();

	if ($id <= 0) {
		$pdo->prepare(
			'INSERT INTO `epc_product_taxonomy_nodes` (`industry_key`, `parent_id`, `slug`, `name_en`, `amazon_node_ref`, `sort`, `level`, `active`)
			 VALUES (?, ?, ?, ?, ?, ?, ?, 1)'
		)->execute(array($industryKey, $parentId, $slug, $name, $amazon, $sort, $level));
		$id = (int) $pdo->lastInsertId();
	} else {
		$pdo->prepare(
			'UPDATE `epc_product_taxonomy_nodes` SET `parent_id`=?, `name_en`=?, `amazon_node_ref`=?, `sort`=?, `level`=?, `industry_key`=? WHERE `id`=?'
		)->execute(array($parentId, $name, $amazon, $sort, $level, $industryKey, $id));
	}
	$count++;

	$children = $node['children'] ?? array();
	if (is_array($children)) {
		$ci = 0;
		foreach ($children as $child) {
			if (!is_array($child)) {
				continue;
			}
			if (!isset($child['sort'])) {
				$child['sort'] = 10 + ($ci * 10);
			}
			epc_tax_insert_node($pdo, $child, $id, $level + 1, $count);
			$ci++;
		}
	}
	return $id;
}

function epc_tax_seed(PDO $pdo): array
{
	epc_tax_ensure_schema($pdo);
	$count = 0;
	foreach (epc_tax_seed_tree() as $root) {
		epc_tax_insert_node($pdo, $root, 0, 1, $count);
	}
	epc_tax_link_catalogue_categories($pdo);
	return array('nodes' => $count);
}

function epc_tax_link_catalogue_categories(PDO $pdo): int
{
	$linked = 0;
	try {
		$cats = $pdo->query('SELECT `id`, `value`, `caption` FROM `shop_catalogue_categories` WHERE `published_flag` = 1')->fetchAll(PDO::FETCH_ASSOC);
	} catch (Throwable $e) {
		return 0;
	}
	if (!$cats) {
		return 0;
	}
	$nodes = $pdo->query('SELECT `id`, `slug`, `name_en` FROM `epc_product_taxonomy_nodes` WHERE `catalogue_category_id` = 0')->fetchAll(PDO::FETCH_ASSOC);
	foreach ($nodes as $node) {
		$slug = (string) ($node['slug'] ?? '');
		$name = strtolower((string) ($node['name_en'] ?? ''));
		foreach ($cats as $cat) {
			$catVal = strtolower((string) ($cat['value'] ?? ''));
			$catCap = strtolower((string) ($cat['caption'] ?? ''));
			if ($catVal === $slug || strpos($catCap, substr($name, 0, 8)) !== false || strpos($name, substr($catCap, 0, 8)) !== false) {
				$pdo->prepare('UPDATE `epc_product_taxonomy_nodes` SET `catalogue_category_id` = ? WHERE `id` = ?')->execute(array((int) $cat['id'], (int) $node['id']));
				$linked++;
				break;
			}
		}
	}
	return $linked;
}

function epc_tax_list_flat(PDO $pdo, int $parentId = 0): array
{
	$stmt = $pdo->prepare('SELECT * FROM `epc_product_taxonomy_nodes` WHERE `parent_id` = ? AND `active` = 1 ORDER BY `sort`, `name_en`');
	$stmt->execute(array($parentId));
	return $stmt->fetchAll(PDO::FETCH_ASSOC) ?: array();
}

function epc_tax_list_tree(PDO $pdo, int $parentId = 0, int $depth = 0): array
{
	$tree = array();
	foreach (epc_tax_list_flat($pdo, $parentId) as $row) {
		$row['depth'] = $depth;
		$row['children'] = epc_tax_list_tree($pdo, (int) $row['id'], $depth + 1);
		$tree[] = $row;
	}
	return $tree;
}

function epc_tax_count(PDO $pdo): int
{
	return (int) $pdo->query('SELECT COUNT(*) FROM `epc_product_taxonomy_nodes`')->fetchColumn();
}

function epc_tax_by_slug(PDO $pdo, string $slug, string $industryKey = 'electronics'): ?array
{
	$slug = preg_replace('/[^a-z0-9\-]/', '', strtolower(trim($slug)));
	$industryKey = preg_replace('/[^a-z0-9_]/', '', strtolower($industryKey));
	if ($industryKey === '' || $industryKey === 'electronics') {
		$stmt = $pdo->prepare('SELECT * FROM `epc_product_taxonomy_nodes` WHERE `slug` = ? AND (`industry_key` = ? OR `industry_key` = \'\') LIMIT 1');
		$stmt->execute(array($slug, 'electronics'));
	} else {
		$stmt = $pdo->prepare('SELECT * FROM `epc_product_taxonomy_nodes` WHERE `industry_key` = ? AND `slug` = ? LIMIT 1');
		$stmt->execute(array($industryKey, $slug));
	}
	$row = $stmt->fetch(PDO::FETCH_ASSOC);
	return $row ?: null;
}

function epc_tax_by_id(PDO $pdo, int $id): ?array
{
	if ($id <= 0) {
		return null;
	}
	$stmt = $pdo->prepare('SELECT * FROM `epc_product_taxonomy_nodes` WHERE `id` = ? LIMIT 1');
	$stmt->execute(array($id));
	$row = $stmt->fetch(PDO::FETCH_ASSOC);
	return $row ?: null;
}

function epc_tax_breadcrumb(PDO $pdo, int $nodeId): array
{
	$crumb = array();
	$cur = $nodeId;
	while ($cur > 0) {
		$node = epc_tax_by_id($pdo, $cur);
		if (!$node) {
			break;
		}
		array_unshift($crumb, $node);
		$cur = (int) ($node['parent_id'] ?? 0);
	}
	return $crumb;
}
