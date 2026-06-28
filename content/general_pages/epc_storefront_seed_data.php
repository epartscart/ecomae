<?php
/**
 * Industry-specific seed data for tenant storefronts.
 *
 * Creates categories hierarchy + products with images for each industry.
 * Safe to call multiple times (idempotent — uses NOT EXISTS / ON DUPLICATE KEY).
 *
 * Usage (from CP or setup script):
 *   require_once __DIR__ . '/epc_storefront_seed_data.php';
 *   $report = epc_storefront_seed_all($pdo, 'electronics', 'electronicae');
 */
defined('_ASTEXE_') or die('No access');

function epc_storefront_seed_all(PDO $pdo, string $industry, string $siteKey): array
{
	$report = array('industry' => $industry, 'site_key' => $siteKey, 'categories' => 0, 'products' => 0, 'errors' => array());

	$catTree = epc_storefront_seed_category_tree($industry);
	$products = epc_storefront_seed_product_catalog($industry);

	$catIdMap = array();
	foreach ($catTree as $cat) {
		$parentId = 0;
		if (!empty($cat['parent_alias']) && isset($catIdMap[$cat['parent_alias']])) {
			$parentId = $catIdMap[$cat['parent_alias']];
		}
		try {
			$catId = epc_storefront_seed_upsert_category($pdo, $cat, $parentId);
			$catIdMap[$cat['alias']] = $catId;
			$report['categories']++;
		} catch (Throwable $e) {
			$report['errors'][] = 'cat:' . $cat['alias'] . ':' . $e->getMessage();
		}
	}

	$now = time();
	foreach ($products as $i => $prod) {
		try {
			$catId = 0;
			if (!empty($prod['category_alias']) && isset($catIdMap[$prod['category_alias']])) {
				$catId = $catIdMap[$prod['category_alias']];
			}
			epc_storefront_seed_upsert_product($pdo, $prod, $now + $i, $catId);
			$report['products']++;
		} catch (Throwable $e) {
			$report['errors'][] = 'prod:' . $prod['alias'] . ':' . $e->getMessage();
		}
	}
	return $report;
}

function epc_storefront_seed_upsert_category(PDO $pdo, array $cat, int $parentId): int
{
	$alias = substr($cat['alias'], 0, 255);
	$name = trim((string) ($cat['name'] ?? $alias));
	$url = !empty($cat['url']) ? substr($cat['url'], 0, 255) : $alias;
	$level = (int) ($cat['level'] ?? ($parentId > 0 ? 2 : 1));
	$order = (int) ($cat['order'] ?? 10);

	$chk = $pdo->prepare('SELECT `id` FROM `shop_catalogue_categories` WHERE `alias` = ? LIMIT 1');
	$chk->execute(array($alias));
	$id = (int) $chk->fetchColumn();
	if ($id > 0) {
		$pdo->prepare(
			'UPDATE `shop_catalogue_categories` SET `value`=?, `url`=?, `level`=?, `order`=?, `published_flag`=1, `parent`=? WHERE `id`=?'
		)->execute(array($name, $url, $level, $order, $parentId, $id));
		return $id;
	}
	$pdo->prepare(
		'INSERT INTO `shop_catalogue_categories`
		 (`alias`, `url`, `count`, `level`, `value`, `parent`, `title_tag`, `description_tag`, `keywords_tag`, `order`, `published_flag`)
		 VALUES (?, ?, 0, ?, ?, ?, ?, ?, ?, ?, 1)'
	)->execute(array($alias, $url, $level, $name, $parentId, $name, $name . ' — shop online', $name, $order));
	return (int) $pdo->lastInsertId();
}

function epc_storefront_seed_upsert_product(PDO $pdo, array $prod, int $timeCreated, int $catId): void
{
	$caption = $prod['name'];
	$alias = $prod['alias'];
	$price = (float) $prod['price'];
	$image = !empty($prod['image']) ? (string) $prod['image'] : '';

	$pdo->prepare(
		'INSERT INTO `shop_catalogue_products` (`caption`, `alias`, `published_flag`, `price`, `time_created`)
		 SELECT ?, ?, 1, ?, ? FROM DUAL
		 WHERE NOT EXISTS (SELECT 1 FROM `shop_catalogue_products` WHERE `alias` = ? LIMIT 1)'
	)->execute(array($caption, $alias, $price, $timeCreated, $alias));

	if ($catId > 0) {
		try {
			$prodStmt = $pdo->prepare('SELECT `id` FROM `shop_catalogue_products` WHERE `alias` = ? LIMIT 1');
			$prodStmt->execute(array($alias));
			$prodId = (int) $prodStmt->fetchColumn();
			if ($prodId > 0) {
				$pdo->prepare(
					'INSERT INTO `shop_catalogue_product_categories` (`product_id`, `category_id`)
					 SELECT ?, ? FROM DUAL
					 WHERE NOT EXISTS (SELECT 1 FROM `shop_catalogue_product_categories` WHERE `product_id`=? AND `category_id`=? LIMIT 1)'
				)->execute(array($prodId, $catId, $prodId, $catId));
			}
		} catch (Throwable $e) {
			// product_categories table may not exist
		}
	}

	if ($image !== '') {
		try {
			$prodStmt = $pdo->prepare('SELECT `id` FROM `shop_catalogue_products` WHERE `alias` = ? LIMIT 1');
			$prodStmt->execute(array($alias));
			$prodId = (int) $prodStmt->fetchColumn();
			if ($prodId > 0) {
				$pdo->prepare(
					'INSERT INTO `shop_catalogue_product_images` (`product_id`, `image_url`, `sort_order`)
					 SELECT ?, ?, 0 FROM DUAL
					 WHERE NOT EXISTS (SELECT 1 FROM `shop_catalogue_product_images` WHERE `product_id`=? AND `image_url`=? LIMIT 1)'
				)->execute(array($prodId, $image, $prodId, $image));
			}
		} catch (Throwable $e) {
			// product_images table may not exist
		}
	}
}

/* ── CATEGORY TREES ─────────────────────────────────────────────────── */

function epc_storefront_seed_category_tree(string $industry): array
{
	switch ($industry) {
		case 'electronics': return epc_storefront_seed_categories_electronics();
		case 'fashion':     return epc_storefront_seed_categories_fashion();
		case 'tax_advisory':
		case 'consultancy': return epc_storefront_seed_categories_consulting();
		case 'jewellery':   return epc_storefront_seed_categories_jewellery();
		default:            return array();
	}
}

function epc_storefront_seed_categories_electronics(): array
{
	return array(
		array('alias' => 'elc-smartphones',  'name' => 'Smartphones & Mobiles', 'url' => 'smartphones', 'level' => 1, 'order' => 10),
		array('alias' => 'elc-iphones',      'name' => 'iPhones',             'url' => 'smartphones/iphones',    'level' => 2, 'order' => 10, 'parent_alias' => 'elc-smartphones'),
		array('alias' => 'elc-samsung-phone', 'name' => 'Samsung Galaxy',     'url' => 'smartphones/samsung',    'level' => 2, 'order' => 20, 'parent_alias' => 'elc-smartphones'),
		array('alias' => 'elc-android-other', 'name' => 'Android Phones',     'url' => 'smartphones/android',    'level' => 2, 'order' => 30, 'parent_alias' => 'elc-smartphones'),
		array('alias' => 'elc-laptops',       'name' => 'Laptops & Computers','url' => 'laptops',               'level' => 1, 'order' => 20),
		array('alias' => 'elc-macbooks',      'name' => 'MacBooks',           'url' => 'laptops/macbooks',       'level' => 2, 'order' => 10, 'parent_alias' => 'elc-laptops'),
		array('alias' => 'elc-win-laptops',   'name' => 'Windows Laptops',    'url' => 'laptops/windows',        'level' => 2, 'order' => 20, 'parent_alias' => 'elc-laptops'),
		array('alias' => 'elc-gaming-laptop', 'name' => 'Gaming Laptops',     'url' => 'laptops/gaming',         'level' => 2, 'order' => 30, 'parent_alias' => 'elc-laptops'),
		array('alias' => 'elc-tablets',       'name' => 'Tablets & E-Readers','url' => 'tablets',               'level' => 1, 'order' => 30),
		array('alias' => 'elc-ipads',         'name' => 'iPads',              'url' => 'tablets/ipads',           'level' => 2, 'order' => 10, 'parent_alias' => 'elc-tablets'),
		array('alias' => 'elc-android-tab',   'name' => 'Android Tablets',    'url' => 'tablets/android',         'level' => 2, 'order' => 20, 'parent_alias' => 'elc-tablets'),
		array('alias' => 'elc-gaming',        'name' => 'Gaming',             'url' => 'gaming',                'level' => 1, 'order' => 40),
		array('alias' => 'elc-consoles',      'name' => 'Consoles',           'url' => 'gaming/consoles',         'level' => 2, 'order' => 10, 'parent_alias' => 'elc-gaming'),
		array('alias' => 'elc-accessories-g', 'name' => 'Gaming Accessories', 'url' => 'gaming/accessories',     'level' => 2, 'order' => 20, 'parent_alias' => 'elc-gaming'),
		array('alias' => 'elc-audio',         'name' => 'Audio & Headphones', 'url' => 'audio',                 'level' => 1, 'order' => 50),
		array('alias' => 'elc-headphones',    'name' => 'Headphones',         'url' => 'audio/headphones',        'level' => 2, 'order' => 10, 'parent_alias' => 'elc-audio'),
		array('alias' => 'elc-speakers',      'name' => 'Speakers',           'url' => 'audio/speakers',          'level' => 2, 'order' => 20, 'parent_alias' => 'elc-audio'),
		array('alias' => 'elc-wearables',     'name' => 'Wearables',          'url' => 'wearables',             'level' => 1, 'order' => 60),
		array('alias' => 'elc-smart-home',    'name' => 'Smart Home',         'url' => 'smart-home',            'level' => 1, 'order' => 70),
		array('alias' => 'elc-tv-cinema',     'name' => 'TV & Home Cinema',   'url' => 'tv-cinema',             'level' => 1, 'order' => 80),
		array('alias' => 'elc-cameras',       'name' => 'Cameras & Drones',   'url' => 'cameras',               'level' => 1, 'order' => 90),
	);
}

function epc_storefront_seed_categories_fashion(): array
{
	return array(
		array('alias' => 'fsn-women',         'name' => "Women's Fashion",     'url' => 'women',                'level' => 1, 'order' => 10),
		array('alias' => 'fsn-women-dresses', 'name' => 'Dresses',            'url' => 'women/dresses',          'level' => 2, 'order' => 10, 'parent_alias' => 'fsn-women'),
		array('alias' => 'fsn-women-tops',    'name' => 'Tops & Blouses',     'url' => 'women/tops',             'level' => 2, 'order' => 20, 'parent_alias' => 'fsn-women'),
		array('alias' => 'fsn-women-abayas',  'name' => 'Abayas & Modest',    'url' => 'women/abayas',           'level' => 2, 'order' => 30, 'parent_alias' => 'fsn-women'),
		array('alias' => 'fsn-women-shoes',   'name' => 'Shoes & Sandals',    'url' => 'women/shoes',            'level' => 2, 'order' => 40, 'parent_alias' => 'fsn-women'),
		array('alias' => 'fsn-men',           'name' => "Men's Fashion",       'url' => 'men',                  'level' => 1, 'order' => 20),
		array('alias' => 'fsn-men-shirts',    'name' => 'Shirts & Polos',     'url' => 'men/shirts',             'level' => 2, 'order' => 10, 'parent_alias' => 'fsn-men'),
		array('alias' => 'fsn-men-pants',     'name' => 'Trousers & Chinos',  'url' => 'men/trousers',           'level' => 2, 'order' => 20, 'parent_alias' => 'fsn-men'),
		array('alias' => 'fsn-men-thobes',    'name' => 'Thobes & Kandoras',  'url' => 'men/thobes',             'level' => 2, 'order' => 30, 'parent_alias' => 'fsn-men'),
		array('alias' => 'fsn-men-shoes',     'name' => 'Sneakers & Shoes',   'url' => 'men/shoes',              'level' => 2, 'order' => 40, 'parent_alias' => 'fsn-men'),
		array('alias' => 'fsn-beauty',        'name' => 'Beauty & Fragrance', 'url' => 'beauty',               'level' => 1, 'order' => 30),
		array('alias' => 'fsn-perfume',       'name' => 'Perfumes',           'url' => 'beauty/perfumes',         'level' => 2, 'order' => 10, 'parent_alias' => 'fsn-beauty'),
		array('alias' => 'fsn-skincare',      'name' => 'Skincare',           'url' => 'beauty/skincare',         'level' => 2, 'order' => 20, 'parent_alias' => 'fsn-beauty'),
		array('alias' => 'fsn-makeup',        'name' => 'Makeup',             'url' => 'beauty/makeup',           'level' => 2, 'order' => 30, 'parent_alias' => 'fsn-beauty'),
		array('alias' => 'fsn-kids',          'name' => 'Kids',               'url' => 'kids',                 'level' => 1, 'order' => 40),
		array('alias' => 'fsn-accessories',   'name' => 'Accessories',        'url' => 'accessories',           'level' => 1, 'order' => 50),
		array('alias' => 'fsn-bags',          'name' => 'Bags & Wallets',     'url' => 'accessories/bags',        'level' => 2, 'order' => 10, 'parent_alias' => 'fsn-accessories'),
		array('alias' => 'fsn-jewellery',     'name' => 'Fashion Jewellery',  'url' => 'accessories/jewellery',   'level' => 2, 'order' => 20, 'parent_alias' => 'fsn-accessories'),
		array('alias' => 'fsn-sunglasses',    'name' => 'Sunglasses',         'url' => 'accessories/sunglasses',  'level' => 2, 'order' => 30, 'parent_alias' => 'fsn-accessories'),
		array('alias' => 'fsn-sports',        'name' => 'Sports & Activewear','url' => 'sports',               'level' => 1, 'order' => 60),
		array('alias' => 'fsn-home',          'name' => 'Home & Lifestyle',   'url' => 'home-lifestyle',        'level' => 1, 'order' => 70),
	);
}

function epc_storefront_seed_categories_consulting(): array
{
	return array(
		array('alias' => 'cns-vat',           'name' => 'VAT Services',        'url' => 'services/vat',          'level' => 1, 'order' => 10),
		array('alias' => 'cns-vat-reg',       'name' => 'VAT Registration',    'url' => 'services/vat/registration', 'level' => 2, 'order' => 10, 'parent_alias' => 'cns-vat'),
		array('alias' => 'cns-vat-filing',    'name' => 'VAT Return Filing',   'url' => 'services/vat/filing',       'level' => 2, 'order' => 20, 'parent_alias' => 'cns-vat'),
		array('alias' => 'cns-vat-audit',     'name' => 'VAT Health Check',    'url' => 'services/vat/health-check', 'level' => 2, 'order' => 30, 'parent_alias' => 'cns-vat'),
		array('alias' => 'cns-ct',            'name' => 'Corporate Tax',       'url' => 'services/corporate-tax',   'level' => 1, 'order' => 20),
		array('alias' => 'cns-ct-reg',        'name' => 'CT Registration',     'url' => 'services/corporate-tax/registration', 'level' => 2, 'order' => 10, 'parent_alias' => 'cns-ct'),
		array('alias' => 'cns-ct-filing',     'name' => 'CT Return Filing',    'url' => 'services/corporate-tax/filing',       'level' => 2, 'order' => 20, 'parent_alias' => 'cns-ct'),
		array('alias' => 'cns-audit',         'name' => 'Audit & Assurance',   'url' => 'services/audit',           'level' => 1, 'order' => 30),
		array('alias' => 'cns-audit-ext',     'name' => 'External Audit',      'url' => 'services/audit/external',   'level' => 2, 'order' => 10, 'parent_alias' => 'cns-audit'),
		array('alias' => 'cns-audit-int',     'name' => 'Internal Audit',      'url' => 'services/audit/internal',   'level' => 2, 'order' => 20, 'parent_alias' => 'cns-audit'),
		array('alias' => 'cns-bookkeeping',   'name' => 'Bookkeeping',         'url' => 'services/bookkeeping',     'level' => 1, 'order' => 40),
		array('alias' => 'cns-compliance',    'name' => 'Compliance & AML',    'url' => 'services/compliance',      'level' => 1, 'order' => 50),
		array('alias' => 'cns-aml',           'name' => 'AML Compliance',      'url' => 'services/compliance/aml',   'level' => 2, 'order' => 10, 'parent_alias' => 'cns-compliance'),
		array('alias' => 'cns-esrub',         'name' => 'ESR & UBO Filing',    'url' => 'services/compliance/esr',   'level' => 2, 'order' => 20, 'parent_alias' => 'cns-compliance'),
		array('alias' => 'cns-advisory',      'name' => 'Business Advisory',   'url' => 'services/advisory',        'level' => 1, 'order' => 60),
		array('alias' => 'cns-setup',         'name' => 'Company Formation',   'url' => 'services/company-setup',   'level' => 1, 'order' => 70),
	);
}

function epc_storefront_seed_categories_jewellery(): array
{
	return array(
		array('alias' => 'jwl-gold',          'name' => 'Gold Jewellery',      'url' => 'gold',                 'level' => 1, 'order' => 10),
		array('alias' => 'jwl-gold-necklace', 'name' => 'Gold Necklaces',     'url' => 'gold/necklaces',         'level' => 2, 'order' => 10, 'parent_alias' => 'jwl-gold'),
		array('alias' => 'jwl-gold-bangles',  'name' => 'Gold Bangles',       'url' => 'gold/bangles',           'level' => 2, 'order' => 20, 'parent_alias' => 'jwl-gold'),
		array('alias' => 'jwl-gold-rings',    'name' => 'Gold Rings',         'url' => 'gold/rings',             'level' => 2, 'order' => 30, 'parent_alias' => 'jwl-gold'),
		array('alias' => 'jwl-gold-earrings', 'name' => 'Gold Earrings',     'url' => 'gold/earrings',           'level' => 2, 'order' => 40, 'parent_alias' => 'jwl-gold'),
		array('alias' => 'jwl-diamond',       'name' => 'Diamond Jewellery',  'url' => 'diamonds',               'level' => 1, 'order' => 20),
		array('alias' => 'jwl-dia-rings',     'name' => 'Diamond Rings',      'url' => 'diamonds/rings',          'level' => 2, 'order' => 10, 'parent_alias' => 'jwl-diamond'),
		array('alias' => 'jwl-dia-necklace',  'name' => 'Diamond Necklaces',  'url' => 'diamonds/necklaces',      'level' => 2, 'order' => 20, 'parent_alias' => 'jwl-diamond'),
		array('alias' => 'jwl-dia-earrings',  'name' => 'Diamond Earrings',   'url' => 'diamonds/earrings',       'level' => 2, 'order' => 30, 'parent_alias' => 'jwl-diamond'),
		array('alias' => 'jwl-bridal',        'name' => 'Bridal Collection',  'url' => 'bridal',                 'level' => 1, 'order' => 30),
		array('alias' => 'jwl-bridal-sets',   'name' => 'Bridal Sets',        'url' => 'bridal/sets',             'level' => 2, 'order' => 10, 'parent_alias' => 'jwl-bridal'),
		array('alias' => 'jwl-bridal-rings',  'name' => 'Engagement Rings',   'url' => 'bridal/engagement',       'level' => 2, 'order' => 20, 'parent_alias' => 'jwl-bridal'),
		array('alias' => 'jwl-everyday',      'name' => 'Everyday Jewellery', 'url' => 'everyday',               'level' => 1, 'order' => 40),
		array('alias' => 'jwl-chains',        'name' => 'Chains',             'url' => 'everyday/chains',         'level' => 2, 'order' => 10, 'parent_alias' => 'jwl-everyday'),
		array('alias' => 'jwl-pendants',      'name' => 'Pendants',           'url' => 'everyday/pendants',       'level' => 2, 'order' => 20, 'parent_alias' => 'jwl-everyday'),
		array('alias' => 'jwl-watches',       'name' => 'Watches',            'url' => 'watches',                'level' => 1, 'order' => 50),
		array('alias' => 'jwl-silver',        'name' => 'Silver Jewellery',   'url' => 'silver',                 'level' => 1, 'order' => 60),
		array('alias' => 'jwl-pearls',        'name' => 'Pearls',             'url' => 'pearls',                 'level' => 1, 'order' => 70),
	);
}

/* ── PRODUCT CATALOGS ───────────────────────────────────────────────── */

function epc_storefront_seed_product_catalog(string $industry): array
{
	switch ($industry) {
		case 'electronics': return epc_storefront_seed_products_electronics();
		case 'fashion':     return epc_storefront_seed_products_fashion();
		case 'tax_advisory':
		case 'consultancy': return epc_storefront_seed_products_consulting();
		case 'jewellery':   return epc_storefront_seed_products_jewellery();
		default:            return array();
	}
}

function epc_storefront_seed_products_electronics(): array
{
	return array(
		// Smartphones
		array('name' => 'iPhone 16 Pro Max 256GB — Natural Titanium',    'alias' => 'ELC-IP16PM-256',   'price' => 5299, 'category_alias' => 'elc-iphones',      'image' => 'https://images.unsplash.com/photo-1695048133142-1a20484d2569?auto=format&fit=crop&w=480'),
		array('name' => 'iPhone 16 128GB — Ultramarine',                 'alias' => 'ELC-IP16-128',     'price' => 3499, 'category_alias' => 'elc-iphones',      'image' => 'https://images.unsplash.com/photo-1695048133142-1a20484d2569?auto=format&fit=crop&w=480'),
		array('name' => 'Samsung Galaxy S25 Ultra 512GB — Titanium Grey','alias' => 'ELC-GS25U-512',    'price' => 4699, 'category_alias' => 'elc-samsung-phone', 'image' => 'https://images.unsplash.com/photo-1610945265064-0e34e5519bbf?auto=format&fit=crop&w=480'),
		array('name' => 'Samsung Galaxy A55 128GB — Ice Blue',           'alias' => 'ELC-GA55-128',     'price' => 1299, 'category_alias' => 'elc-samsung-phone', 'image' => 'https://images.unsplash.com/photo-1610945265064-0e34e5519bbf?auto=format&fit=crop&w=480'),
		array('name' => 'Google Pixel 9 Pro 256GB — Porcelain',          'alias' => 'ELC-PX9P-256',     'price' => 3899, 'category_alias' => 'elc-android-other', 'image' => 'https://images.unsplash.com/photo-1598327105666-5b89351aff97?auto=format&fit=crop&w=480'),
		// Laptops
		array('name' => 'MacBook Air M3 15" 16GB/512GB — Midnight',      'alias' => 'ELC-MBA-M3-15',    'price' => 5999, 'category_alias' => 'elc-macbooks',     'image' => 'https://images.unsplash.com/photo-1517336714731-489689fd1ca8?auto=format&fit=crop&w=480'),
		array('name' => 'MacBook Pro M4 Pro 14" 24GB/1TB',               'alias' => 'ELC-MBP-M4P-14',   'price' => 9499, 'category_alias' => 'elc-macbooks',     'image' => 'https://images.unsplash.com/photo-1517336714731-489689fd1ca8?auto=format&fit=crop&w=480'),
		array('name' => 'Dell XPS 15 i7 32GB/1TB — Platinum',            'alias' => 'ELC-DXPS15-32',    'price' => 6499, 'category_alias' => 'elc-win-laptops',  'image' => 'https://images.unsplash.com/photo-1496181133206-80ce9b88a853?auto=format&fit=crop&w=480'),
		array('name' => 'ASUS ROG Strix G16 RTX 4070 i9 32GB',           'alias' => 'ELC-ROG-G16',      'price' => 7999, 'category_alias' => 'elc-gaming-laptop','image' => 'https://images.unsplash.com/photo-1593642632559-0c6d3fc62b89?auto=format&fit=crop&w=480'),
		// Tablets
		array('name' => 'iPad Pro M4 13" 256GB Wi-Fi — Space Black',     'alias' => 'ELC-IPADP-M4',     'price' => 5499, 'category_alias' => 'elc-ipads',        'image' => 'https://images.unsplash.com/photo-1544244015-0df4b3ffc6b0?auto=format&fit=crop&w=480'),
		array('name' => 'Samsung Galaxy Tab S10 FE 5G 12GB/256GB',       'alias' => 'ELC-TABS10FE',     'price' => 2004, 'category_alias' => 'elc-android-tab',  'image' => 'https://images.unsplash.com/photo-1585790050230-5dd28404ccb9?auto=format&fit=crop&w=480'),
		// Gaming
		array('name' => 'PlayStation 5 Pro Digital Edition',              'alias' => 'ELC-PS5PRO-D',     'price' => 3499, 'category_alias' => 'elc-consoles',     'image' => 'https://images.unsplash.com/photo-1606144042614-b2417e99c4e3?auto=format&fit=crop&w=480'),
		array('name' => 'Nintendo Switch 2 Joy-Con Bundle',              'alias' => 'ELC-NSW2-JC',      'price' => 2099, 'category_alias' => 'elc-consoles',     'image' => 'https://images.unsplash.com/photo-1578303512597-81e6cc155b3e?auto=format&fit=crop&w=480'),
		array('name' => 'Razer Viper V4 Pro Wireless Esports Mouse',     'alias' => 'ELC-RZ-VPR4',      'price' => 669,  'category_alias' => 'elc-accessories-g','image' => 'https://images.unsplash.com/photo-1527814050087-3793815479db?auto=format&fit=crop&w=480'),
		// Audio
		array('name' => 'Apple AirPods Pro 2nd Gen USB-C',               'alias' => 'ELC-APP2-USC',     'price' => 899,  'category_alias' => 'elc-headphones',  'image' => 'https://images.unsplash.com/photo-1606220588913-b3aacb4d2f46?auto=format&fit=crop&w=480'),
		array('name' => 'Bose QuietComfort Ultra Headphones — Black',    'alias' => 'ELC-BOSE-QCU',     'price' => 1499, 'category_alias' => 'elc-headphones',  'image' => 'https://images.unsplash.com/photo-1546435770-a3e426bf472b?auto=format&fit=crop&w=480'),
		array('name' => 'JBL Charge 5 Bluetooth Speaker — Squad',        'alias' => 'ELC-JBL-CH5',      'price' => 599,  'category_alias' => 'elc-speakers',    'image' => 'https://images.unsplash.com/photo-1608043152269-423dbba4e7e1?auto=format&fit=crop&w=480'),
		array('name' => 'Sony WH-1000XM5 Wireless — Silver',            'alias' => 'ELC-SONY-XM5',     'price' => 1299, 'category_alias' => 'elc-headphones',  'image' => 'https://images.unsplash.com/photo-1618366712010-f4ae9c647dcb?auto=format&fit=crop&w=480'),
		// Wearables
		array('name' => 'Apple Watch Ultra 2 49mm Titanium',             'alias' => 'ELC-AWU2-49',      'price' => 3699, 'category_alias' => 'elc-wearables',   'image' => 'https://images.unsplash.com/photo-1434493789847-2f02dc6ca35d?auto=format&fit=crop&w=480'),
		array('name' => 'Samsung Galaxy Watch 7 44mm — Green',           'alias' => 'ELC-GW7-44',       'price' => 1199, 'category_alias' => 'elc-wearables',   'image' => 'https://images.unsplash.com/photo-1434493789847-2f02dc6ca35d?auto=format&fit=crop&w=480'),
		// Smart Home
		array('name' => 'Amazon Echo Show 10 3rd Gen',                   'alias' => 'ELC-ECHO10-3',     'price' => 999,  'category_alias' => 'elc-smart-home',  'image' => 'https://images.unsplash.com/photo-1558618666-fcd25c85f82e?auto=format&fit=crop&w=480'),
		array('name' => 'Google Nest Hub Max 10" Smart Display',         'alias' => 'ELC-NEST-HUB',     'price' => 899,  'category_alias' => 'elc-smart-home',  'image' => 'https://images.unsplash.com/photo-1558618666-fcd25c85f82e?auto=format&fit=crop&w=480'),
		// TV
		array('name' => 'Samsung 65" Neo QLED 4K Smart TV',              'alias' => 'ELC-SAM-65QLED',   'price' => 5999, 'category_alias' => 'elc-tv-cinema',   'image' => 'https://images.unsplash.com/photo-1593359677879-a4bb92f829d1?auto=format&fit=crop&w=480'),
		array('name' => 'LG OLED C4 55" 4K Dolby Vision',                'alias' => 'ELC-LG-OLED55',    'price' => 4799, 'category_alias' => 'elc-tv-cinema',   'image' => 'https://images.unsplash.com/photo-1593359677879-a4bb92f829d1?auto=format&fit=crop&w=480'),
		// Cameras
		array('name' => 'DJI Mini 4 Pro Fly More Combo',                 'alias' => 'ELC-DJI-M4P',      'price' => 3999, 'category_alias' => 'elc-cameras',     'image' => 'https://images.unsplash.com/photo-1473968512647-3e447244af8f?auto=format&fit=crop&w=480'),
		array('name' => 'Sony Alpha A7 IV Full Frame Mirrorless',        'alias' => 'ELC-SONY-A7IV',    'price' => 8999, 'category_alias' => 'elc-cameras',     'image' => 'https://images.unsplash.com/photo-1516035069371-29a1b244cc32?auto=format&fit=crop&w=480'),
	);
}

function epc_storefront_seed_products_fashion(): array
{
	return array(
		// Women
		array('name' => 'Silk Midi Dress — Emerald Green',              'alias' => 'FSN-WD-SILK-EM',   'price' => 449, 'category_alias' => 'fsn-women-dresses', 'image' => 'https://images.unsplash.com/photo-1595777457583-95e059d581b8?auto=format&fit=crop&w=480'),
		array('name' => 'Floral Maxi Dress — Blush Pink',               'alias' => 'FSN-WD-FLR-BP',    'price' => 359, 'category_alias' => 'fsn-women-dresses', 'image' => 'https://images.unsplash.com/photo-1572804013309-59a88b7e92f1?auto=format&fit=crop&w=480'),
		array('name' => 'Linen Wrap Top — Ivory',                       'alias' => 'FSN-WT-LN-IV',     'price' => 189, 'category_alias' => 'fsn-women-tops',    'image' => 'https://images.unsplash.com/photo-1564257631407-4deb1f99d992?auto=format&fit=crop&w=480'),
		array('name' => 'Premium Open Abaya — Black with Gold Trim',    'alias' => 'FSN-WA-BLK-GLD',   'price' => 599, 'category_alias' => 'fsn-women-abayas',  'image' => 'https://images.unsplash.com/photo-1590735213920-68192a487bc2?auto=format&fit=crop&w=480'),
		array('name' => 'Embroidered Abaya — Navy',                     'alias' => 'FSN-WA-EMB-NVY',   'price' => 699, 'category_alias' => 'fsn-women-abayas',  'image' => 'https://images.unsplash.com/photo-1590735213920-68192a487bc2?auto=format&fit=crop&w=480'),
		array('name' => 'Block Heel Sandals — Nude',                    'alias' => 'FSN-WS-HEEL-ND',   'price' => 329, 'category_alias' => 'fsn-women-shoes',   'image' => 'https://images.unsplash.com/photo-1543163521-1bf539c55dd2?auto=format&fit=crop&w=480'),
		// Men
		array('name' => 'Oxford Button-Down Shirt — White',             'alias' => 'FSN-MS-OXF-WHT',   'price' => 199, 'category_alias' => 'fsn-men-shirts',    'image' => 'https://images.unsplash.com/photo-1596755094514-f87e34085b2c?auto=format&fit=crop&w=480'),
		array('name' => 'Slim Fit Polo — Navy',                         'alias' => 'FSN-MS-PLO-NVY',   'price' => 149, 'category_alias' => 'fsn-men-shirts',    'image' => 'https://images.unsplash.com/photo-1625910513413-5fc421e0b6b4?auto=format&fit=crop&w=480'),
		array('name' => 'Tailored Chinos — Olive',                      'alias' => 'FSN-MP-CHN-OLV',   'price' => 229, 'category_alias' => 'fsn-men-pants',     'image' => 'https://images.unsplash.com/photo-1473966968600-fa801b869a1a?auto=format&fit=crop&w=480'),
		array('name' => 'Premium White Thobe — Emirati Style',          'alias' => 'FSN-MT-WHT-EM',    'price' => 399, 'category_alias' => 'fsn-men-thobes',    'image' => 'https://images.unsplash.com/photo-1590735213920-68192a487bc2?auto=format&fit=crop&w=480'),
		array('name' => 'Retro Running Sneakers — White/Grey',          'alias' => 'FSN-MSH-RUN-WG',   'price' => 499, 'category_alias' => 'fsn-men-shoes',     'image' => 'https://images.unsplash.com/photo-1542291026-7eec264c27ff?auto=format&fit=crop&w=480'),
		array('name' => 'Classic Leather Loafers — Brown',              'alias' => 'FSN-MSH-LOF-BR',   'price' => 449, 'category_alias' => 'fsn-men-shoes',     'image' => 'https://images.unsplash.com/photo-1614252369475-531eba835eb1?auto=format&fit=crop&w=480'),
		// Beauty
		array('name' => 'Oud Rose EDP 100ml — Unisex',                  'alias' => 'FSN-BF-OUD-100',   'price' => 599, 'category_alias' => 'fsn-perfume',       'image' => 'https://images.unsplash.com/photo-1541643600914-78b084683601?auto=format&fit=crop&w=480'),
		array('name' => 'French Vanilla EDP 50ml — Women',              'alias' => 'FSN-BF-VAN-50',    'price' => 349, 'category_alias' => 'fsn-perfume',       'image' => 'https://images.unsplash.com/photo-1541643600914-78b084683601?auto=format&fit=crop&w=480'),
		array('name' => 'Vitamin C Serum 30ml — Radiance Boost',        'alias' => 'FSN-BS-VITC-30',   'price' => 189, 'category_alias' => 'fsn-skincare',      'image' => 'https://images.unsplash.com/photo-1556228578-0d85b1a4d571?auto=format&fit=crop&w=480'),
		array('name' => 'Hydrating Face Cream 50ml — SPF 30',           'alias' => 'FSN-BS-HYD-50',    'price' => 149, 'category_alias' => 'fsn-skincare',      'image' => 'https://images.unsplash.com/photo-1556228578-0d85b1a4d571?auto=format&fit=crop&w=480'),
		array('name' => 'Matte Lipstick Set — 6 Shades',                'alias' => 'FSN-BM-LIP-SET',   'price' => 129, 'category_alias' => 'fsn-makeup',        'image' => 'https://images.unsplash.com/photo-1586495777744-4413f21062fa?auto=format&fit=crop&w=480'),
		// Accessories
		array('name' => 'Leather Tote Bag — Camel',                     'alias' => 'FSN-AB-TOTE-CM',   'price' => 549, 'category_alias' => 'fsn-bags',          'image' => 'https://images.unsplash.com/photo-1548036328-c9fa89d128fa?auto=format&fit=crop&w=480'),
		array('name' => 'Mini Crossbody — Black',                       'alias' => 'FSN-AB-CROSS-BK',  'price' => 299, 'category_alias' => 'fsn-bags',          'image' => 'https://images.unsplash.com/photo-1548036328-c9fa89d128fa?auto=format&fit=crop&w=480'),
		array('name' => 'Gold-Plated Statement Necklace',               'alias' => 'FSN-AJ-NECK-GLD',  'price' => 179, 'category_alias' => 'fsn-jewellery',     'image' => 'https://images.unsplash.com/photo-1515562141589-67f0d727b750?auto=format&fit=crop&w=480'),
		array('name' => 'Oversized Square Sunglasses — Tortoise',       'alias' => 'FSN-ASG-SQ-TRT',   'price' => 249, 'category_alias' => 'fsn-sunglasses',    'image' => 'https://images.unsplash.com/photo-1511499767150-a48a237f0083?auto=format&fit=crop&w=480'),
		array('name' => 'Aviator Polarized Sunglasses — Gold Frame',    'alias' => 'FSN-ASG-AV-GLD',   'price' => 299, 'category_alias' => 'fsn-sunglasses',    'image' => 'https://images.unsplash.com/photo-1511499767150-a48a237f0083?auto=format&fit=crop&w=480'),
		// Sports
		array('name' => 'Performance Running Tights — Black',           'alias' => 'FSN-SP-RUN-BLK',   'price' => 199, 'category_alias' => 'fsn-sports',        'image' => 'https://images.unsplash.com/photo-1571902943202-507ec2618e8f?auto=format&fit=crop&w=480'),
		array('name' => 'Yoga Mat Premium 6mm — Sage Green',            'alias' => 'FSN-SP-YGA-SGE',   'price' => 149, 'category_alias' => 'fsn-sports',        'image' => 'https://images.unsplash.com/photo-1571902943202-507ec2618e8f?auto=format&fit=crop&w=480'),
		// Kids
		array('name' => 'Boys Cotton T-Shirt Pack (3) — Multi',         'alias' => 'FSN-KD-TSHRT-3',   'price' => 99,  'category_alias' => 'fsn-kids',          'image' => 'https://images.unsplash.com/photo-1519238263530-99bdd11df2ea?auto=format&fit=crop&w=480'),
	);
}

function epc_storefront_seed_products_consulting(): array
{
	return array(
		// VAT Services
		array('name' => 'VAT Registration — New Business',              'alias' => 'CNS-VAT-REG-NEW',  'price' => 1500, 'category_alias' => 'cns-vat-reg',      'image' => 'https://images.unsplash.com/photo-1554224155-6726b3ff858f?auto=format&fit=crop&w=480'),
		array('name' => 'VAT Registration — Group Registration',        'alias' => 'CNS-VAT-REG-GRP',  'price' => 3500, 'category_alias' => 'cns-vat-reg',      'image' => 'https://images.unsplash.com/photo-1554224155-6726b3ff858f?auto=format&fit=crop&w=480'),
		array('name' => 'VAT Return Filing — Quarterly',                'alias' => 'CNS-VAT-FIL-Q',    'price' => 1000, 'category_alias' => 'cns-vat-filing',   'image' => 'https://images.unsplash.com/photo-1460925895917-afdab827c52f?auto=format&fit=crop&w=480'),
		array('name' => 'VAT Return Filing — Monthly',                  'alias' => 'CNS-VAT-FIL-M',    'price' => 800,  'category_alias' => 'cns-vat-filing',   'image' => 'https://images.unsplash.com/photo-1460925895917-afdab827c52f?auto=format&fit=crop&w=480'),
		array('name' => 'VAT Health Check & Compliance Review',         'alias' => 'CNS-VAT-HLTH',     'price' => 5000, 'category_alias' => 'cns-vat-audit',    'image' => 'https://images.unsplash.com/photo-1450101499163-c8848c66ca85?auto=format&fit=crop&w=480'),
		// Corporate Tax
		array('name' => 'Corporate Tax Registration — FTA Portal',      'alias' => 'CNS-CT-REG',       'price' => 2000, 'category_alias' => 'cns-ct-reg',       'image' => 'https://images.unsplash.com/photo-1554224155-6726b3ff858f?auto=format&fit=crop&w=480'),
		array('name' => 'Corporate Tax Return Filing — Annual',         'alias' => 'CNS-CT-FIL-A',     'price' => 5000, 'category_alias' => 'cns-ct-filing',    'image' => 'https://images.unsplash.com/photo-1460925895917-afdab827c52f?auto=format&fit=crop&w=480'),
		array('name' => 'Transfer Pricing Documentation',               'alias' => 'CNS-CT-TP-DOC',    'price' => 8000, 'category_alias' => 'cns-ct-filing',    'image' => 'https://images.unsplash.com/photo-1450101499163-c8848c66ca85?auto=format&fit=crop&w=480'),
		// Audit
		array('name' => 'External Audit — SME (up to AED 10M)',         'alias' => 'CNS-AUD-EXT-SM',   'price' => 8000,  'category_alias' => 'cns-audit-ext',   'image' => 'https://images.unsplash.com/photo-1454165804606-c3d57bc86b40?auto=format&fit=crop&w=480'),
		array('name' => 'External Audit — Enterprise (AED 10M+)',       'alias' => 'CNS-AUD-EXT-EN',   'price' => 25000, 'category_alias' => 'cns-audit-ext',   'image' => 'https://images.unsplash.com/photo-1454165804606-c3d57bc86b40?auto=format&fit=crop&w=480'),
		array('name' => 'Internal Audit — Quarterly Review',            'alias' => 'CNS-AUD-INT-Q',    'price' => 6000,  'category_alias' => 'cns-audit-int',   'image' => 'https://images.unsplash.com/photo-1454165804606-c3d57bc86b40?auto=format&fit=crop&w=480'),
		// Bookkeeping
		array('name' => 'Monthly Bookkeeping — Starter (< 100 txn)',    'alias' => 'CNS-BK-STR-M',     'price' => 1500, 'category_alias' => 'cns-bookkeeping',  'image' => 'https://images.unsplash.com/photo-1554224155-6726b3ff858f?auto=format&fit=crop&w=480'),
		array('name' => 'Monthly Bookkeeping — Growth (100-500 txn)',   'alias' => 'CNS-BK-GRW-M',     'price' => 3000, 'category_alias' => 'cns-bookkeeping',  'image' => 'https://images.unsplash.com/photo-1554224155-6726b3ff858f?auto=format&fit=crop&w=480'),
		array('name' => 'Monthly Bookkeeping — Enterprise (500+ txn)',  'alias' => 'CNS-BK-ENT-M',     'price' => 5000, 'category_alias' => 'cns-bookkeeping',  'image' => 'https://images.unsplash.com/photo-1554224155-6726b3ff858f?auto=format&fit=crop&w=480'),
		// Compliance
		array('name' => 'AML/CFT Compliance Setup & Training',          'alias' => 'CNS-AML-SETUP',    'price' => 7500, 'category_alias' => 'cns-aml',          'image' => 'https://images.unsplash.com/photo-1450101499163-c8848c66ca85?auto=format&fit=crop&w=480'),
		array('name' => 'AML Ongoing Monitoring — Annual',              'alias' => 'CNS-AML-MON-A',    'price' => 3000, 'category_alias' => 'cns-aml',          'image' => 'https://images.unsplash.com/photo-1450101499163-c8848c66ca85?auto=format&fit=crop&w=480'),
		array('name' => 'ESR Notification & Report Filing',             'alias' => 'CNS-ESR-FILE',     'price' => 2500, 'category_alias' => 'cns-esrub',        'image' => 'https://images.unsplash.com/photo-1460925895917-afdab827c52f?auto=format&fit=crop&w=480'),
		array('name' => 'UBO Declaration Filing',                       'alias' => 'CNS-UBO-FILE',     'price' => 1500, 'category_alias' => 'cns-esrub',        'image' => 'https://images.unsplash.com/photo-1460925895917-afdab827c52f?auto=format&fit=crop&w=480'),
		// Advisory
		array('name' => 'Business Plan & Financial Projections',        'alias' => 'CNS-ADV-BPLAN',    'price' => 10000,'category_alias' => 'cns-advisory',     'image' => 'https://images.unsplash.com/photo-1454165804606-c3d57bc86b40?auto=format&fit=crop&w=480'),
		array('name' => 'CFO-as-a-Service — Monthly Retainer',          'alias' => 'CNS-ADV-CFO-M',    'price' => 8000, 'category_alias' => 'cns-advisory',     'image' => 'https://images.unsplash.com/photo-1454165804606-c3d57bc86b40?auto=format&fit=crop&w=480'),
		// Company Formation
		array('name' => 'Mainland LLC Formation — Dubai DED',           'alias' => 'CNS-CO-MAIN-DXB',  'price' => 15000,'category_alias' => 'cns-setup',        'image' => 'https://images.unsplash.com/photo-1486406146926-c627a92ad1ab?auto=format&fit=crop&w=480'),
		array('name' => 'Free Zone Company Formation — DMCC',           'alias' => 'CNS-CO-FZ-DMCC',   'price' => 12000,'category_alias' => 'cns-setup',        'image' => 'https://images.unsplash.com/photo-1486406146926-c627a92ad1ab?auto=format&fit=crop&w=480'),
		array('name' => 'Free Zone Company Formation — IFZA',           'alias' => 'CNS-CO-FZ-IFZA',   'price' => 8500, 'category_alias' => 'cns-setup',        'image' => 'https://images.unsplash.com/photo-1486406146926-c627a92ad1ab?auto=format&fit=crop&w=480'),
	);
}

function epc_storefront_seed_products_jewellery(): array
{
	return array(
		// Gold Necklaces
		array('name' => '22K Gold Chain Necklace 20" — 15g',             'alias' => 'JWL-GN-22K-15G',   'price' => 5250, 'category_alias' => 'jwl-gold-necklace', 'image' => 'https://images.unsplash.com/photo-1599643478518-a784e5dc4c8f?auto=format&fit=crop&w=480'),
		array('name' => '21K Gold Necklace with Pendant — 12g',          'alias' => 'JWL-GN-21K-12G',   'price' => 3960, 'category_alias' => 'jwl-gold-necklace', 'image' => 'https://images.unsplash.com/photo-1599643478518-a784e5dc4c8f?auto=format&fit=crop&w=480'),
		array('name' => '18K Italian Gold Choker — Rose Gold 8g',        'alias' => 'JWL-GN-18K-CHK',   'price' => 2640, 'category_alias' => 'jwl-gold-necklace', 'image' => 'https://images.unsplash.com/photo-1515562141589-67f0d727b750?auto=format&fit=crop&w=480'),
		// Gold Bangles
		array('name' => '22K Gold Bangle Set (4 pcs) — 40g',            'alias' => 'JWL-GB-22K-40G',   'price' => 14000,'category_alias' => 'jwl-gold-bangles',  'image' => 'https://images.unsplash.com/photo-1535632066927-ab7c9ab60908?auto=format&fit=crop&w=480'),
		array('name' => '21K Gold Bangle — Twisted Design 12g',         'alias' => 'JWL-GB-21K-12G',   'price' => 3960, 'category_alias' => 'jwl-gold-bangles',  'image' => 'https://images.unsplash.com/photo-1535632066927-ab7c9ab60908?auto=format&fit=crop&w=480'),
		// Gold Rings
		array('name' => '22K Gold Wedding Band — 6g',                    'alias' => 'JWL-GR-22K-6G',    'price' => 2100, 'category_alias' => 'jwl-gold-rings',    'image' => 'https://images.unsplash.com/photo-1605100804763-247f67b3557e?auto=format&fit=crop&w=480'),
		array('name' => '21K Gold Statement Ring — Filigree 8g',         'alias' => 'JWL-GR-21K-8G',    'price' => 2640, 'category_alias' => 'jwl-gold-rings',    'image' => 'https://images.unsplash.com/photo-1605100804763-247f67b3557e?auto=format&fit=crop&w=480'),
		// Gold Earrings
		array('name' => '22K Gold Jhumka Earrings — 10g',                'alias' => 'JWL-GE-22K-JHM',   'price' => 3500, 'category_alias' => 'jwl-gold-earrings', 'image' => 'https://images.unsplash.com/photo-1535632787350-4e68ef0ac584?auto=format&fit=crop&w=480'),
		// Diamond Rings
		array('name' => 'Solitaire Diamond Ring 1.0ct — Platinum',       'alias' => 'JWL-DR-SOL-100',   'price' => 22000,'category_alias' => 'jwl-dia-rings',     'image' => 'https://images.unsplash.com/photo-1605100804763-247f67b3557e?auto=format&fit=crop&w=480'),
		array('name' => 'Halo Diamond Ring 0.75ct — 18K White Gold',     'alias' => 'JWL-DR-HAL-075',   'price' => 15000,'category_alias' => 'jwl-dia-rings',     'image' => 'https://images.unsplash.com/photo-1605100804763-247f67b3557e?auto=format&fit=crop&w=480'),
		array('name' => 'Three-Stone Diamond Ring 1.5ct Total',          'alias' => 'JWL-DR-3ST-150',   'price' => 28000,'category_alias' => 'jwl-dia-rings',     'image' => 'https://images.unsplash.com/photo-1605100804763-247f67b3557e?auto=format&fit=crop&w=480'),
		// Diamond Necklaces
		array('name' => 'Diamond Tennis Necklace 5.0ct — 18K Gold',      'alias' => 'JWL-DN-TEN-500',   'price' => 45000,'category_alias' => 'jwl-dia-necklace',  'image' => 'https://images.unsplash.com/photo-1515562141589-67f0d727b750?auto=format&fit=crop&w=480'),
		array('name' => 'Diamond Pendant 0.50ct — 18K White Gold',       'alias' => 'JWL-DN-PND-050',   'price' => 7500, 'category_alias' => 'jwl-dia-necklace',  'image' => 'https://images.unsplash.com/photo-1515562141589-67f0d727b750?auto=format&fit=crop&w=480'),
		// Diamond Earrings
		array('name' => 'Diamond Stud Earrings 1.0ct Total — 18K',      'alias' => 'JWL-DE-STD-100',   'price' => 12000,'category_alias' => 'jwl-dia-earrings',  'image' => 'https://images.unsplash.com/photo-1535632787350-4e68ef0ac584?auto=format&fit=crop&w=480'),
		// Bridal
		array('name' => 'Bridal Set — 22K Necklace + Earrings + Ring',   'alias' => 'JWL-BR-SET-22K',   'price' => 35000,'category_alias' => 'jwl-bridal-sets',   'image' => 'https://images.unsplash.com/photo-1599643478518-a784e5dc4c8f?auto=format&fit=crop&w=480'),
		array('name' => 'Diamond Engagement Ring Solitaire 0.80ct',      'alias' => 'JWL-BR-ENG-080',   'price' => 18000,'category_alias' => 'jwl-bridal-rings',  'image' => 'https://images.unsplash.com/photo-1605100804763-247f67b3557e?auto=format&fit=crop&w=480'),
		// Everyday
		array('name' => '18K Gold Rope Chain 18" — 5g',                  'alias' => 'JWL-EV-ROPE-5G',   'price' => 1650, 'category_alias' => 'jwl-chains',        'image' => 'https://images.unsplash.com/photo-1599643478518-a784e5dc4c8f?auto=format&fit=crop&w=480'),
		array('name' => '18K Gold Heart Pendant — 3g',                   'alias' => 'JWL-EV-HEART-3G',  'price' => 990,  'category_alias' => 'jwl-pendants',      'image' => 'https://images.unsplash.com/photo-1515562141589-67f0d727b750?auto=format&fit=crop&w=480'),
		// Watches
		array('name' => 'Swiss Automatic Watch — Rose Gold Dial',        'alias' => 'JWL-WTC-SWISS-RG', 'price' => 8500, 'category_alias' => 'jwl-watches',       'image' => 'https://images.unsplash.com/photo-1524592094714-0f0654e20314?auto=format&fit=crop&w=480'),
		array('name' => 'Diamond-Set Ladies Watch — 18K Gold',           'alias' => 'JWL-WTC-DIA-18K',  'price' => 15000,'category_alias' => 'jwl-watches',       'image' => 'https://images.unsplash.com/photo-1524592094714-0f0654e20314?auto=format&fit=crop&w=480'),
		// Silver
		array('name' => 'Sterling Silver Cuff Bracelet — Hammered',      'alias' => 'JWL-SLV-CUFF-HM',  'price' => 350,  'category_alias' => 'jwl-silver',        'image' => 'https://images.unsplash.com/photo-1535632066927-ab7c9ab60908?auto=format&fit=crop&w=480'),
		array('name' => 'Sterling Silver Pendant — Evil Eye',            'alias' => 'JWL-SLV-EYE-PND',  'price' => 180,  'category_alias' => 'jwl-silver',        'image' => 'https://images.unsplash.com/photo-1515562141589-67f0d727b750?auto=format&fit=crop&w=480'),
		// Pearls
		array('name' => 'South Sea Pearl Necklace 18" — White',          'alias' => 'JWL-PRL-SS-18',    'price' => 12000,'category_alias' => 'jwl-pearls',        'image' => 'https://images.unsplash.com/photo-1515562141589-67f0d727b750?auto=format&fit=crop&w=480'),
		array('name' => 'Freshwater Pearl Stud Earrings — 8mm',          'alias' => 'JWL-PRL-STD-8',    'price' => 450,  'category_alias' => 'jwl-pearls',        'image' => 'https://images.unsplash.com/photo-1535632787350-4e68ef0ac584?auto=format&fit=crop&w=480'),
	);
}
