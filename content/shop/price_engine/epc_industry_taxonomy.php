<?php
/**
 * EPC Auto Price AI — multi-industry taxonomy profiles (auto_parts, electronics, fashion, jewellery, tax_advisory, general_retail).
 */
defined('_ASTEXE_') or die('No access');

function epc_apai_industry_profiles(): array
{
	return array(
		'auto_parts' => array(
			'label' => 'Auto parts & accessories',
			'profile' => 'warehouse_supplier',
			'search_hint' => 'Brand + article e.g. Toyota 1310154101',
			'example_tenants' => array('epartscart'),
		),
		'electronics' => array(
			'label' => 'Consumer electronics',
			'profile' => 'marketplace_arbitrage',
			'search_hint' => 'electronics UAE',
			'example_tenants' => array('electronicae'),
		),
		'fashion' => array(
			'label' => 'Fashion & apparel',
			'profile' => 'marketplace_arbitrage',
			'search_hint' => 'fashion UAE',
			'example_tenants' => array('stylenlook'),
		),
		'jewellery' => array(
			'label' => 'Jewellery & watches',
			'profile' => 'marketplace_arbitrage',
			'search_hint' => 'jewellery UAE',
			'example_tenants' => array('thejewellerytrend'),
		),
		'tax_advisory' => array(
			'label' => 'Tax advisory & professional services',
			'profile' => 'professional_services',
			'search_hint' => 'VAT filing corporate tax audit UAE',
			'example_tenants' => array('taxofinca'),
		),
		'general_retail' => array(
			'label' => 'General retail',
			'profile' => 'marketplace_arbitrage',
			'search_hint' => 'home office health UAE',
			'example_tenants' => array(),
		),
	);
}

/** Map tenant registry industry_code → Auto Price AI industry_key. */
function epc_apai_industry_from_code(string $industryCode): string
{
	$code = preg_replace('/[^a-z0-9_]/', '', strtolower(trim($industryCode)));
	$map = array(
		'auto_parts' => 'auto_parts',
		'electronics' => 'electronics',
		'fashion' => 'fashion',
		'jewellery' => 'jewellery',
		'tax_advisory' => 'tax_advisory',
		'consultancy' => 'tax_advisory',
		'professional_services' => 'tax_advisory',
		'general_retail' => 'general_retail',
		'erp_standalone' => 'general_retail',
	);
	return $map[$code] ?? 'general_retail';
}

function epc_apai_tenant_industry_map(): array
{
	return array(
		'epartscart' => 'auto_parts',
		'electronicae' => 'electronics',
		'stylenlook' => 'fashion',
		'thejewellerytrend' => 'jewellery',
		'taxofinca' => 'tax_advisory',
	);
}

function epc_apai_resolve_industry(PDO $pdo, string $siteKey): string
{
	$siteKey = preg_replace('/[^a-z0-9_]/', '', strtolower(trim($siteKey)));
	if ($siteKey === '' || $siteKey === 'platform') {
		return 'general_retail';
	}
	$map = epc_apai_tenant_industry_map();
	if (isset($map[$siteKey])) {
		return $map[$siteKey];
	}
	if (function_exists('epc_ape_tenant_config_get')) {
		$cfg = epc_ape_tenant_config_get($pdo, $siteKey);
		$cfgIndustry = preg_replace('/[^a-z0-9_]/', '', strtolower((string) (($cfg['config'] ?? array())['industry_key'] ?? '')));
		if ($cfgIndustry !== '') {
			return epc_apai_industry_from_code($cfgIndustry);
		}
	}
	if (function_exists('epc_portal_tenant_get')) {
		require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_portal_tenant_intro.php';
		$row = epc_portal_tenant_get($pdo, $siteKey);
		if ($row && !empty($row['industry_code'])) {
			return epc_apai_industry_from_code((string) $row['industry_code']);
		}
	}
	return 'general_retail';
}

function epc_apai_taxonomy_migrate_schema(PDO $pdo): void
{
	$cols = $pdo->query("SHOW COLUMNS FROM `epc_product_taxonomy_nodes` LIKE 'industry_key'")->fetch(PDO::FETCH_ASSOC);
	if (!$cols) {
		$pdo->exec(
			"ALTER TABLE `epc_product_taxonomy_nodes`
			 ADD COLUMN `industry_key` VARCHAR(32) NOT NULL DEFAULT 'electronics' AFTER `id`,
			 ADD KEY `industry_parent` (`industry_key`, `parent_id`, `sort`)"
		);
		$pdo->exec("UPDATE `epc_product_taxonomy_nodes` SET `industry_key` = 'electronics' WHERE `industry_key` = '' OR `industry_key` IS NULL");
	}
	try {
		$pdo->exec('ALTER TABLE `epc_product_taxonomy_nodes` DROP INDEX `slug`');
	} catch (Throwable $e) {
	}
	try {
		$pdo->exec('ALTER TABLE `epc_product_taxonomy_nodes` ADD UNIQUE KEY `industry_slug` (`industry_key`, `slug`)');
	} catch (Throwable $e) {
	}
	$autoCol = $pdo->query("SHOW COLUMNS FROM `epc_product_taxonomy_nodes` LIKE 'auto_created'")->fetch(PDO::FETCH_ASSOC);
	if (!$autoCol) {
		try {
			$pdo->exec(
				"ALTER TABLE `epc_product_taxonomy_nodes`
				 ADD COLUMN `auto_created` TINYINT(1) NOT NULL DEFAULT 0 AFTER `active`"
			);
		} catch (Throwable $e) {
		}
	}
}

function epc_apai_taxonomy_trees(): array
{
	if (is_file(__DIR__ . '/epc_electronics_taxonomy.php')) {
		require_once __DIR__ . '/epc_electronics_taxonomy.php';
	}
	if (is_file(__DIR__ . '/epc_auto_parts_taxonomy.php')) {
		require_once __DIR__ . '/epc_auto_parts_taxonomy.php';
	}
	if (is_file(__DIR__ . '/epc_fashion_taxonomy.php')) {
		require_once __DIR__ . '/epc_fashion_taxonomy.php';
	}
	if (is_file(__DIR__ . '/epc_retail_taxonomy.php')) {
		require_once __DIR__ . '/epc_retail_taxonomy.php';
	}
	if (is_file(__DIR__ . '/epc_tax_advisory_taxonomy.php')) {
		require_once __DIR__ . '/epc_tax_advisory_taxonomy.php';
	}

	$electronics = function_exists('epc_tax_seed_tree') ? epc_tax_seed_tree() : array();
	$autoParts = function_exists('epc_auto_tax_seed_tree') ? epc_auto_tax_seed_tree() : array();
	$fashion = function_exists('epc_fashion_tax_seed_tree') ? epc_fashion_tax_seed_tree() : array();
	$generalRetail = function_exists('epc_retail_tax_seed_tree') ? epc_retail_tax_seed_tree() : array();
	$taxAdvisory = function_exists('epc_tax_advisory_seed_tree') ? epc_tax_advisory_seed_tree() : array();

	return array(
		'auto_parts' => $autoParts,
		'electronics' => $electronics,
		'fashion' => $fashion,
		'jewellery' => array(
			array('slug' => 'jewellery-rings', 'name' => 'Rings', 'sort' => 10, 'children' => array(
				array('slug' => 'jewellery-rings-gold', 'name' => 'Gold rings', 'children' => array(
					array('slug' => 'jewellery-rings-gold-22k', 'name' => '22K gold'),
					array('slug' => 'jewellery-rings-gold-18k', 'name' => '18K gold'),
				)),
				array('slug' => 'jewellery-rings-diamond', 'name' => 'Diamond rings'),
				array('slug' => 'jewellery-rings-silver', 'name' => 'Silver rings'),
			)),
			array('slug' => 'jewellery-necklaces', 'name' => 'Necklaces & chains', 'sort' => 20, 'children' => array(
				array('slug' => 'jewellery-necklaces-pendant', 'name' => 'Pendants'),
				array('slug' => 'jewellery-necklaces-pearl', 'name' => 'Pearl necklaces'),
			)),
			array('slug' => 'jewellery-watches', 'name' => 'Watches', 'sort' => 30, 'children' => array(
				array('slug' => 'jewellery-watches-luxury', 'name' => 'Luxury watches'),
				array('slug' => 'jewellery-watches-smart', 'name' => 'Smart watches'),
				array('slug' => 'jewellery-watches-sports', 'name' => 'Sports watches'),
			)),
			array('slug' => 'jewellery-earrings', 'name' => 'Earrings & bracelets', 'sort' => 40, 'children' => array(
				array('slug' => 'jewellery-earrings-studs', 'name' => 'Stud earrings'),
				array('slug' => 'jewellery-bracelets', 'name' => 'Bracelets & bangles'),
			)),
		),
		'general_retail' => $generalRetail,
		'tax_advisory' => $taxAdvisory,
	);
}

function epc_apai_insert_tax_node(PDO $pdo, string $industryKey, array $node, int $parentId, int $level, int &$count): int
{
	$slug = (string) ($node['slug'] ?? '');
	$name = (string) ($node['name'] ?? $slug);
	$amazon = (string) ($node['amazon'] ?? '');
	$sort = (int) ($node['sort'] ?? (100 + $count));

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

	foreach ((array) ($node['children'] ?? array()) as $ci => $child) {
		if (!is_array($child)) {
			continue;
		}
		if (!isset($child['sort'])) {
			$child['sort'] = 10 + ($ci * 10);
		}
		epc_apai_insert_tax_node($pdo, $industryKey, $child, $id, $level + 1, $count);
	}
	return $id;
}

function epc_apai_seed_all_taxonomies(PDO $pdo): array
{
	if (is_file(__DIR__ . '/epc_electronics_taxonomy.php')) {
		require_once __DIR__ . '/epc_electronics_taxonomy.php';
		epc_tax_ensure_schema($pdo);
	}
	epc_apai_taxonomy_migrate_schema($pdo);

	$totals = array();
	foreach (epc_apai_taxonomy_trees() as $industryKey => $tree) {
		$count = 0;
		foreach ($tree as $root) {
			epc_apai_insert_tax_node($pdo, $industryKey, $root, 0, 1, $count);
		}
		$totals[$industryKey] = $count;
	}
	if (function_exists('epc_tax_link_catalogue_categories')) {
		epc_tax_link_catalogue_categories($pdo);
	}
	return array('by_industry' => $totals, 'nodes' => array_sum($totals));
}

function epc_apai_tax_count(PDO $pdo, string $industryKey = ''): int
{
	if ($industryKey === '') {
		return (int) $pdo->query('SELECT COUNT(*) FROM `epc_product_taxonomy_nodes`')->fetchColumn();
	}
	$industryKey = preg_replace('/[^a-z0-9_]/', '', strtolower($industryKey));
	$stmt = $pdo->prepare('SELECT COUNT(*) FROM `epc_product_taxonomy_nodes` WHERE `industry_key` = ?');
	$stmt->execute(array($industryKey));
	return (int) $stmt->fetchColumn();
}

function epc_apai_tax_list_tree(PDO $pdo, string $industryKey, int $parentId = 0, int $depth = 0): array
{
	$industryKey = preg_replace('/[^a-z0-9_]/', '', strtolower($industryKey));
	$stmt = $pdo->prepare('SELECT * FROM `epc_product_taxonomy_nodes` WHERE `industry_key` = ? AND `parent_id` = ? AND `active` = 1 ORDER BY `sort`, `name_en`');
	$stmt->execute(array($industryKey, $parentId));
	$tree = array();
	foreach ($stmt->fetchAll(PDO::FETCH_ASSOC) ?: array() as $row) {
		$row['depth'] = $depth;
		$row['children'] = epc_apai_tax_list_tree($pdo, $industryKey, (int) $row['id'], $depth + 1);
		$tree[] = $row;
	}
	return $tree;
}

function epc_apai_tax_by_slug(PDO $pdo, string $industryKey, string $slug): ?array
{
	$industryKey = preg_replace('/[^a-z0-9_]/', '', strtolower($industryKey));
	$slug = preg_replace('/[^a-z0-9\-]/', '', strtolower(trim($slug)));
	$stmt = $pdo->prepare('SELECT * FROM `epc_product_taxonomy_nodes` WHERE `industry_key` = ? AND `slug` = ? LIMIT 1');
	$stmt->execute(array($industryKey, $slug));
	$row = $stmt->fetch(PDO::FETCH_ASSOC);
	return $row ?: null;
}

function epc_apai_demo_slugs_for_industry(string $industryKey): array
{
	$map = array(
		'auto_parts' => array(
			'auto-engine-filters-oil', 'auto-brakes-pads', 'auto-electrical-batteries',
			'auto-engine-spark', 'auto-suspension-shocks', 'auto-oem-toyota',
		),
		'electronics' => array(
			'cell-phones', 'headphones', 'computers-laptops', 'tv-video-televisions',
			'gaming-consoles', 'smart-home', 'wearables-smartwatches', 'pc-components-gpu',
		),
		'fashion' => array(
			'fashion-men-shirts', 'fashion-women-dresses', 'fashion-men-footwear',
			'fashion-women-activewear', 'fashion-accessories-bags',
		),
		'jewellery' => array('jewellery-rings-gold-22k', 'jewellery-watches-luxury', 'jewellery-necklaces-pendant'),
		'general_retail' => array(
			'retail-home-appliances-coffee', 'retail-health-vitamins',
			'retail-garden-tools', 'retail-industrial-power-tools',
		),
		'tax_advisory' => array(
			'svc-tax-vat-registration', 'svc-tax-ct-registration', 'svc-accounting-monthly-basic',
			'svc-audit-statutory', 'svc-advisory-company-setup', 'svc-consulting-retainer',
		),
	);
	return $map[$industryKey] ?? array('retail-home-appliances-coffee');
}

function epc_apai_ae_sources_for_industry(string $industryKey): array
{
	$base = array(
		'auto_parts' => array(
			array('domain' => 'spare247.com', 'label' => 'Spare247 B2B', 'priority' => 1, 'auth_type' => 'form_login'),
			array('domain' => 'autoparts.ae', 'label' => 'AutoParts.ae UAE', 'priority' => 9),
			array('domain' => 'autodoc.ae', 'label' => 'AutoDoc UAE', 'priority' => 10),
			array('domain' => 'partsouq.com', 'label' => 'Partsouq OEM catalogue', 'priority' => 12),
			array('domain' => 'amayama.com', 'label' => 'Amayama parts', 'priority' => 14),
			array('domain' => 'partslink24.com', 'label' => 'Partslink24 (dealer login)', 'priority' => 16, 'auth_type' => 'form_login'),
			array('domain' => 'rockauto.com', 'label' => 'RockAuto (reference)', 'priority' => 18),
			array('domain' => 'alfuttaimparts.com', 'label' => 'Al-Futtaim Parts', 'priority' => 20),
			array('domain' => 'agmc.ae', 'label' => 'AGMC parts & service', 'priority' => 22),
			array('domain' => 'autoexpressparts.ae', 'label' => 'Auto Express Parts UAE', 'priority' => 24),
			array('domain' => 'carparts.ae', 'label' => 'Car parts UAE', 'priority' => 26),
			array('domain' => 'noon.com', 'label' => 'Noon auto', 'priority' => 30),
			array('domain' => 'amazon.ae', 'label' => 'Amazon.ae auto', 'priority' => 32),
			array('domain' => 'dubizzle.com', 'label' => 'Dubizzle auto parts', 'priority' => 34),
			array('domain' => 'autotrader.ae', 'label' => 'AutoTrader UAE parts', 'priority' => 36),
		),
		'electronics' => array(
			array('domain' => 'sharafdg.com', 'label' => 'Sharaf DG UAE', 'priority' => 10),
			array('domain' => 'jumbo.ae', 'label' => 'Jumbo Electronics', 'priority' => 12),
			array('domain' => 'virginmegastore.ae', 'label' => 'Virgin Megastore UAE', 'priority' => 14),
			array('domain' => 'emaxme.com', 'label' => 'EMAX UAE', 'priority' => 16),
			array('domain' => 'noon.com', 'label' => 'Noon UAE', 'priority' => 18),
			array('domain' => 'amazon.ae', 'label' => 'Amazon.ae', 'priority' => 20),
			array('domain' => 'apple.com', 'label' => 'Apple UAE store', 'priority' => 22),
			array('domain' => 'samsung.com', 'label' => 'Samsung Gulf', 'priority' => 24),
			array('domain' => 'istyle.ae', 'label' => 'iStyle Apple reseller', 'priority' => 26),
			array('domain' => 'carrefouruae.com', 'label' => 'Carrefour UAE', 'priority' => 28),
			array('domain' => 'luluhypermarket.com', 'label' => 'Lulu Hypermarket electronics', 'priority' => 30),
			array('domain' => 'microless.com', 'label' => 'Microless PC components', 'priority' => 32),
			array('domain' => 'gear-up.me', 'label' => 'Gear-Up gaming', 'priority' => 34),
			array('domain' => 'jackys.com', 'label' => 'Jackys Electronics', 'priority' => 36),
			array('domain' => 'ecity.ae', 'label' => 'Ecity UAE', 'priority' => 38),
		),
		'fashion' => array(
			array('domain' => 'namshi.com', 'label' => 'Namshi UAE', 'priority' => 10),
			array('domain' => 'ounass.ae', 'label' => 'Ounass UAE', 'priority' => 12),
			array('domain' => 'noon.com', 'label' => 'Noon fashion', 'priority' => 14),
			array('domain' => '6thstreet.com', 'label' => '6thStreet', 'priority' => 16),
			array('domain' => 'amazon.ae', 'label' => 'Amazon.ae fashion', 'priority' => 18),
			array('domain' => 'maxfashion.com', 'label' => 'Max Fashion', 'priority' => 20),
			array('domain' => 'centrepointstores.com', 'label' => 'Centrepoint', 'priority' => 22),
			array('domain' => 'brandforless.com', 'label' => 'Brand For Less', 'priority' => 24),
			array('domain' => 'bloomingdales.ae', 'label' => 'Bloomingdales UAE', 'priority' => 26),
			array('domain' => 'farfetch.com', 'label' => 'Farfetch UAE', 'priority' => 28),
			array('domain' => 'shein.com', 'label' => 'SHEIN UAE', 'priority' => 30),
			array('domain' => 'hm.com', 'label' => 'H&M UAE', 'priority' => 32),
			array('domain' => 'zara.com', 'label' => 'Zara UAE', 'priority' => 34),
			array('domain' => 'mango.com', 'label' => 'Mango UAE', 'priority' => 36),
			array('domain' => 'sivvi.com', 'label' => 'Sivvi fashion', 'priority' => 38),
		),
		'jewellery' => array(
			array('domain' => 'damasjewellery.com', 'label' => 'Damas Jewellery', 'priority' => 10),
			array('domain' => 'malabargoldanddiamonds.com', 'label' => 'Malabar Gold', 'priority' => 12),
			array('domain' => 'joyalukkas.com', 'label' => 'Joyalukkas UAE', 'priority' => 14),
			array('domain' => 'ounass.ae', 'label' => 'Ounass jewellery', 'priority' => 16),
			array('domain' => 'noon.com', 'label' => 'Noon jewellery', 'priority' => 18),
			array('domain' => 'amazon.ae', 'label' => 'Amazon.ae watches', 'priority' => 20),
			array('domain' => 'tiffany.com', 'label' => 'Tiffany & Co UAE', 'priority' => 22),
			array('domain' => 'cartier.com', 'label' => 'Cartier UAE', 'priority' => 24),
			array('domain' => 'pandora.net', 'label' => 'Pandora UAE', 'priority' => 26),
			array('domain' => 'swarovski.com', 'label' => 'Swarovski UAE', 'priority' => 28),
			array('domain' => 'lifestylestores.com', 'label' => 'Lifestyle watches', 'priority' => 30),
			array('domain' => 'watches.ae', 'label' => 'Watches.ae', 'priority' => 32),
			array('domain' => 'chrono24.ae', 'label' => 'Chrono24 UAE', 'priority' => 34),
			array('domain' => 'gold.ae', 'label' => 'Gold.ae bullion', 'priority' => 36),
			array('domain' => 'carrefouruae.com', 'label' => 'Carrefour jewellery', 'priority' => 38),
		),
		'general_retail' => array(
			array('domain' => 'noon.com', 'label' => 'Noon UAE', 'priority' => 10),
			array('domain' => 'amazon.ae', 'label' => 'Amazon.ae', 'priority' => 12),
			array('domain' => 'carrefouruae.com', 'label' => 'Carrefour UAE', 'priority' => 14),
			array('domain' => 'luluhypermarket.com', 'label' => 'Lulu Hypermarket', 'priority' => 16),
			array('domain' => 'instashop.com', 'label' => 'Instashop', 'priority' => 18),
			array('domain' => 'aceuae.com', 'label' => 'ACE Hardware UAE', 'priority' => 20),
			array('domain' => 'danubehome.com', 'label' => 'Danube Home', 'priority' => 22),
			array('domain' => 'homecentre.com', 'label' => 'Home Centre', 'priority' => 24),
			array('domain' => 'ikea.com', 'label' => 'IKEA UAE', 'priority' => 26),
			array('domain' => 'toolsmart.ae', 'label' => 'Toolmart industrial', 'priority' => 28),
			array('domain' => 'wemart.ae', 'label' => 'Wemart home & garden', 'priority' => 30),
			array('domain' => 'blink.com', 'label' => 'Blink grocery & home', 'priority' => 32),
			array('domain' => 'talabat.com', 'label' => 'Talabat Mart', 'priority' => 34),
			array('domain' => 'unioncoop.ae', 'label' => 'Union Coop', 'priority' => 36),
			array('domain' => 'spinneys.com', 'label' => 'Spinneys UAE', 'priority' => 38),
		),
		'tax_advisory' => array(
			array('domain' => 'tax.gov.ae', 'label' => 'UAE Federal Tax Authority (FTA)', 'priority' => 1),
			array('domain' => 'mof.gov.ae', 'label' => 'Ministry of Finance UAE', 'priority' => 3),
			array('domain' => 'pwc.com', 'label' => 'PwC Middle East (benchmark)', 'priority' => 10),
			array('domain' => 'ey.com', 'label' => 'EY UAE (benchmark)', 'priority' => 12),
			array('domain' => 'kpmg.com', 'label' => 'KPMG Lower Gulf (benchmark)', 'priority' => 14),
			array('domain' => 'deloitte.com', 'label' => 'Deloitte Middle East (benchmark)', 'priority' => 16),
			array('domain' => 'grantthornton.ae', 'label' => 'Grant Thornton UAE', 'priority' => 18),
			array('domain' => 'bdo.ae', 'label' => 'BDO UAE', 'priority' => 20),
			array('domain' => 'crowe.com', 'label' => 'Crowe UAE', 'priority' => 22),
			array('domain' => 'mazars.ae', 'label' => 'Mazars UAE', 'priority' => 24),
			array('domain' => 'rsm.global', 'label' => 'RSM UAE', 'priority' => 26),
			array('domain' => 'emiratesnbd.com', 'label' => 'Emirates NBD business banking', 'priority' => 28),
			array('domain' => 'adcb.com', 'label' => 'ADCB business services', 'priority' => 30),
			array('domain' => 'dmcc.ae', 'label' => 'DMCC free zone (setup reference)', 'priority' => 32),
			array('domain' => 'ifza.com', 'label' => 'IFZA free zone (setup reference)', 'priority' => 34),
		),
	);
	return $base[$industryKey] ?? $base['general_retail'];
}

/**
 * Per-country industry source overlays (merged with country base pack).
 *
 * @return array<int,array{domain:string,label:string,priority:int,auth_type?:string}>
 */
function epc_apai_country_industry_sources(string $countryCode, string $industryKey): array
{
	$countryCode = strtoupper(substr(preg_replace('/[^A-Za-z]/', '', $countryCode), 0, 2));
	$industryKey = preg_replace('/[^a-z0-9_]/', '', strtolower(trim($industryKey)));
	$packs = array(
		'PK' => array(
			'auto_parts' => array(
				array('domain' => 'autostore.pk', 'label' => 'AutoStore Pakistan', 'priority' => 8),
				array('domain' => 'pakwheels.com', 'label' => 'PakWheels parts', 'priority' => 10),
				array('domain' => 'autoparts.pk', 'label' => 'AutoParts.pk', 'priority' => 12),
				array('domain' => 'partsouq.com', 'label' => 'Partsouq', 'priority' => 14),
			),
			'electronics' => array(
				array('domain' => 'telemart.pk', 'label' => 'Telemart', 'priority' => 8),
				array('domain' => 'shophive.com', 'label' => 'Shophive', 'priority' => 10),
				array('domain' => 'priceoye.pk', 'label' => 'PriceOye', 'priority' => 12),
			),
			'fashion' => array(
				array('domain' => 'khaadi.com', 'label' => 'Khaadi', 'priority' => 8),
				array('domain' => 'outfitters.com.pk', 'label' => 'Outfitters', 'priority' => 10),
				array('domain' => 'sapphireonline.pk', 'label' => 'Sapphire', 'priority' => 12),
			),
			'general_retail' => array(
				array('domain' => 'metro-online.pk', 'label' => 'Metro Online', 'priority' => 8),
				array('domain' => 'imtiaz.com.pk', 'label' => 'Imtiaz Super Market', 'priority' => 10),
			),
		),
		'IN' => array(
			'auto_parts' => array(
				array('domain' => 'boodmo.com', 'label' => 'Boodmo India', 'priority' => 8),
				array('domain' => 'spares.in', 'label' => 'Spares.in', 'priority' => 10),
				array('domain' => 'carparts.com', 'label' => 'CarParts India', 'priority' => 12),
			),
			'electronics' => array(
				array('domain' => 'reliancedigital.in', 'label' => 'Reliance Digital', 'priority' => 8),
				array('domain' => 'vijaysales.com', 'label' => 'Vijay Sales', 'priority' => 10),
				array('domain' => 'poorvika.com', 'label' => 'Poorvika Mobiles', 'priority' => 12),
			),
			'fashion' => array(
				array('domain' => 'myntra.com', 'label' => 'Myntra', 'priority' => 8),
				array('domain' => 'ajio.com', 'label' => 'AJIO', 'priority' => 10),
				array('domain' => 'nykaa.com', 'label' => 'Nykaa Fashion', 'priority' => 12),
			),
			'general_retail' => array(
				array('domain' => 'bigbasket.com', 'label' => 'BigBasket', 'priority' => 8),
				array('domain' => 'blinkit.com', 'label' => 'Blinkit', 'priority' => 10),
			),
		),
		'SA' => array(
			'auto_parts' => array(
				array('domain' => 'partsouq.com', 'label' => 'Partsouq KSA', 'priority' => 8),
				array('domain' => 'autozone.sa', 'label' => 'AutoZone KSA', 'priority' => 10),
			),
			'electronics' => array(
				array('domain' => 'extra.com', 'label' => 'Extra Stores', 'priority' => 8),
				array('domain' => 'jarir.com', 'label' => 'Jarir Bookstore', 'priority' => 10),
			),
			'fashion' => array(
				array('domain' => 'stylishop.com', 'label' => 'Styli KSA', 'priority' => 8),
				array('domain' => 'shein.com', 'label' => 'SHEIN KSA', 'priority' => 10),
			),
		),
		'OM' => array(
			'auto_parts' => array(
				array('domain' => 'autopartsoman.com', 'label' => 'Auto Parts Oman', 'priority' => 8),
				array('domain' => 'partsouq.com', 'label' => 'Partsouq GCC', 'priority' => 10),
				array('domain' => 'spare247.com', 'label' => 'Spare247 (GCC)', 'priority' => 12),
				array('domain' => 'autoparts.ae', 'label' => 'Autoparts.ae (regional)', 'priority' => 14),
			),
			'electronics' => array(
				array('domain' => 'luluhypermarket.com', 'label' => 'Lulu Hypermarket', 'priority' => 8),
				array('domain' => 'extra.com', 'label' => 'Extra Stores Oman', 'priority' => 10),
				array('domain' => 'omantel.om', 'label' => 'Omantel Shop', 'priority' => 12),
			),
			'fashion' => array(
				array('domain' => 'namshi.com', 'label' => 'Namshi Oman', 'priority' => 8),
				array('domain' => 'shein.com', 'label' => 'SHEIN', 'priority' => 10),
			),
			'general_retail' => array(
				array('domain' => 'carrefouroman.com', 'label' => 'Carrefour Oman', 'priority' => 8),
				array('domain' => 'luluhypermarket.com', 'label' => 'Lulu Hypermarket', 'priority' => 10),
			),
		),
		'GB' => array(
			'auto_parts' => array(
				array('domain' => 'eurocarparts.com', 'label' => 'Euro Car Parts', 'priority' => 8),
				array('domain' => 'gsfautoparts.com', 'label' => 'GSF Auto Parts', 'priority' => 10),
			),
			'electronics' => array(
				array('domain' => 'argos.co.uk', 'label' => 'Argos', 'priority' => 8),
				array('domain' => 'johnlewis.com', 'label' => 'John Lewis', 'priority' => 10),
			),
		),
		'US' => array(
			'auto_parts' => array(
				array('domain' => 'rockauto.com', 'label' => 'RockAuto', 'priority' => 8),
				array('domain' => 'autozone.com', 'label' => 'AutoZone', 'priority' => 10),
				array('domain' => 'oreillyauto.com', 'label' => "O'Reilly Auto", 'priority' => 12),
			),
			'electronics' => array(
				array('domain' => 'newegg.com', 'label' => 'Newegg', 'priority' => 8),
				array('domain' => 'bhphotovideo.com', 'label' => 'B&H Photo', 'priority' => 10),
			),
		),
	);
	if ($countryCode === 'AE') {
		return epc_apai_ae_sources_for_industry($industryKey);
	}
	return (array) ($packs[$countryCode][$industryKey] ?? ($packs[$countryCode]['general_retail'] ?? array()));
}
