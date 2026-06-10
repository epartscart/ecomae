<?php
/**
 * Virgin Megastore–style demo catalogue for electronics retail tenants.
 * Static sample data — safe on shared tenant DB (no shop_catalogue inserts).
 * Product photos: Unsplash CDN (electronics lifestyle & product shots).
 */
defined('_ASTEXE_') or die('No access');

/**
 * Curated Unsplash photo IDs — phones, laptops, gaming, audio, tablets, wearables.
 *
 * @return array<string, array{id: string, alt: string}>
 */
function epc_electronics_retail_image_catalog(): array
{
	return array(
		'gaming_setup' => array('id' => 'photo-1606144042614-baa0c4c784de', 'alt' => 'Gaming setup with console and controller'),
		'tablet_lifestyle' => array('id' => 'photo-1544244015-0df4b3ffc6b0', 'alt' => 'Tablet on desk with stylus'),
		'smartphone' => array('id' => 'photo-1592899677977-9c10ca588bbd', 'alt' => 'Modern smartphone on table'),
		'laptop' => array('id' => 'photo-1496181133206-80ce9b88a853', 'alt' => 'Laptop open on workspace'),
		'ps5_controller' => array('id' => 'photo-1606813907291-d86efa9b94db', 'alt' => 'PlayStation controller close-up'),
		'airpods' => array('id' => 'photo-1606220945770-b5b6c2c55bf1', 'alt' => 'Wireless earbuds in charging case'),
		'phone_galaxy' => array('id' => 'photo-1610945265914-64e4d1c2d6f8', 'alt' => 'Premium smartphone rear camera'),
		'headphones' => array('id' => 'photo-1505740420928-5e560c06d30e', 'alt' => 'Over-ear wireless headphones'),
		'drone' => array('id' => 'photo-1473968512648-048fe7799034', 'alt' => 'Compact camera drone in flight'),
		'mouse' => array('id' => 'photo-1527864550417-7fd91fc51a46', 'alt' => 'Wireless computer mouse'),
		'handheld_console' => array('id' => 'photo-1622298434697-9c925e1b2a8e', 'alt' => 'Handheld gaming PC'),
		'gaming_mouse' => array('id' => 'photo-1615663488932-11e9e9a9c9c0', 'alt' => 'RGB gaming mouse'),
		'nintendo_switch' => array('id' => 'photo-1578662996442-48f60103fc96', 'alt' => 'Portable gaming console'),
		'gaming_chair' => array('id' => 'photo-1598550476439-6847785f0346', 'alt' => 'Ergonomic gaming chair'),
		'kindle' => array('id' => 'photo-1592496005850-d68b5c05098b', 'alt' => 'E-reader with book on screen'),
		'tablet_android' => array('id' => 'photo-1631549916768-411f38d667e8', 'alt' => 'Android tablet display'),
		'tablet_pen' => array('id' => 'photo-1585790050230-5d9d060c9dba', 'alt' => 'Tablet with stylus for notes'),
		'speakers' => array('id' => 'photo-1608043152269-423dbba4e7e1', 'alt' => 'Bluetooth speaker on shelf'),
		'smartwatch' => array('id' => 'photo-1576243008065-f55d50f3e6d3', 'alt' => 'Fitness smartwatch on wrist'),
		'smart_home' => array('id' => 'photo-1558089687-59e8b7a1c7a1', 'alt' => 'Smart home speaker and devices'),
		'streaming' => array('id' => 'photo-1611162617474-5b21e939e113', 'alt' => 'Streaming entertainment on TV'),
		'travel_tech' => array('id' => 'photo-1553062407-98eeb64c6a62', 'alt' => 'Travel backpack with tech gear'),
		'tv_living' => array('id' => 'photo-1593359677879-a4bb92f829d1', 'alt' => 'Living room with large TV'),
		'audio_gear' => array('id' => 'photo-1484704849709-f142a44e4a1d', 'alt' => 'Home audio speakers and amplifier'),
		'macbook' => array('id' => 'photo-1517336714731-489689fd1ca8', 'alt' => 'MacBook on minimal desk'),
		'camera' => array('id' => 'photo-1526170375885-4d8ecf77b99f', 'alt' => 'Compact mirrorless camera'),
		'keyboard_rgb' => array('id' => 'photo-1587829741301-dc798b03b5a8', 'alt' => 'Mechanical RGB keyboard'),
	);
}

function epc_electronics_retail_img(string $keyOrPath, int $w = 400, int $h = 0): string
{
	static $catalog = null;
	if ($catalog === null) {
		$catalog = epc_electronics_retail_image_catalog();
	}
	$path = $keyOrPath;
	if (isset($catalog[$keyOrPath])) {
		$path = $catalog[$keyOrPath]['id'];
	}
	$path = ltrim((string) $path, '/');
	$url = 'https://images.unsplash.com/' . $path . '?auto=format&fit=crop&w=' . (int) $w . '&q=82';
	if ($h > 0) {
		$url .= '&h=' . (int) $h;
	}
	return $url;
}

function epc_electronics_retail_img_alt(string $key): string
{
	$catalog = epc_electronics_retail_image_catalog();
	return isset($catalog[$key]['alt']) ? (string) $catalog[$key]['alt'] : '';
}

function epc_electronics_retail_promo_strip()
{
	return array(
		array('label' => 'Free UAE delivery on orders over AED 200', 'href' => '/o-dostavke'),
		array('label' => 'Official UAE warranty on tech', 'href' => '/kontakty'),
		array('label' => 'Gaming deals — save up to 25%', 'href' => '/shop/search?q=gaming'),
		array('label' => 'Latest smartphones', 'href' => '/shop/search?q=mobile'),
	);
}

function epc_electronics_retail_trust_badges(): array
{
	return array(
		array('icon' => 'fa-truck', 'title' => 'Free UAE delivery', 'text' => 'On orders over AED 200'),
		array('icon' => 'fa-shield', 'title' => 'Official warranty', 'text' => 'UAE retailer guarantee'),
		array('icon' => 'fa-refresh', 'title' => 'Easy returns', 'text' => '14-day exchange policy'),
		array('icon' => 'fa-credit-card', 'title' => 'Secure checkout', 'text' => 'Visa, Apple Pay & Tabby'),
	);
}

function epc_electronics_retail_hero_slides()
{
	return array(
		array(
			'title' => 'Upgrade Your Game Set Up',
			'sub' => 'PlayStation, handheld PCs, gaming chairs & accessories',
			'cta' => 'Shop Gaming',
			'href' => '/shop/search?q=gaming',
			'image' => epc_electronics_retail_img('gaming_setup', 1400, 900),
			'alt' => epc_electronics_retail_img_alt('gaming_setup'),
			'tone' => 'dark',
		),
		array(
			'title' => 'Tablet Deals in Demand',
			'sub' => 'Samsung Galaxy Tab, Lenovo, Kindle — save up to 25%',
			'cta' => 'Shop Tablets',
			'href' => '/shop/search?q=tablet',
			'image' => epc_electronics_retail_img('tablet_lifestyle', 1400, 900),
			'alt' => epc_electronics_retail_img_alt('tablet_lifestyle'),
			'tone' => 'light',
		),
		array(
			'title' => 'Most Trending Tech',
			'sub' => 'Flagship phones, audio, wearables & smart home',
			'cta' => 'Explore Tech',
			'href' => '/shop/search?q=tech',
			'image' => epc_electronics_retail_img('smartphone', 1400, 900),
			'alt' => epc_electronics_retail_img_alt('smartphone'),
			'tone' => 'dark',
		),
		array(
			'title' => 'Laptops for Work & Play',
			'sub' => 'Lenovo, ASUS & MacBook — UAE warranty, prices in AED',
			'cta' => 'Shop Laptops',
			'href' => '/shop/search?q=laptop',
			'image' => epc_electronics_retail_img('laptop', 1400, 900),
			'alt' => epc_electronics_retail_img_alt('laptop'),
			'tone' => 'light',
		),
	);
}

function epc_electronics_retail_category_tiles()
{
	$tiles = array(
		array('name' => 'Laptops & PCs', 'icon' => 'fa-laptop', 'href' => '/shop/search?q=laptop', 'key' => 'laptop'),
		array('name' => 'Speakers & Audio', 'icon' => 'fa-volume-up', 'href' => '/shop/search?q=speakers', 'key' => 'speakers'),
		array('name' => 'Mobiles', 'icon' => 'fa-mobile', 'href' => '/shop/search?q=mobile', 'key' => 'smartphone'),
		array('name' => 'Smart Home', 'icon' => 'fa-home', 'href' => '/shop/search?q=smart+home', 'key' => 'smart_home'),
		array('name' => 'Wearables', 'icon' => 'fa-heartbeat', 'href' => '/shop/search?q=tracker', 'key' => 'smartwatch'),
		array('name' => 'Gaming', 'icon' => 'fa-gamepad', 'href' => '/shop/search?q=gaming', 'key' => 'gaming_setup'),
		array('name' => 'Streaming', 'icon' => 'fa-play-circle', 'href' => '/shop/search?q=streaming', 'key' => 'streaming'),
		array('name' => 'Travel Tech', 'icon' => 'fa-suitcase', 'href' => '/shop/search?q=travel', 'key' => 'travel_tech'),
		array('name' => 'Headphones', 'icon' => 'fa-headphones', 'href' => '/shop/search?q=headphones', 'key' => 'headphones'),
	);
	$out = array();
	foreach ($tiles as $tile) {
		$key = $tile['key'];
		unset($tile['key']);
		$tile['image'] = epc_electronics_retail_img($key, 400, 400);
		$tile['alt'] = epc_electronics_retail_img_alt($key);
		$out[] = $tile;
	}
	return $out;
}

function epc_electronics_retail_deal_sections()
{
	$sections = array(
		array(
			'title' => 'Tablet Deals in Demand',
			'products' => array(
				array('brand' => 'SAMSUNG', 'name' => 'Galaxy Tab S10 FE 5G 12GB/256GB — Silver', 'price' => 2004, 'was' => 2499, 'key' => 'tablet_lifestyle'),
				array('brand' => 'AMAZON', 'name' => 'Kindle Colorsoft 16GB E-Reader — Black', 'price' => 999, 'was' => 1149, 'key' => 'kindle'),
				array('brand' => 'LENOVO', 'name' => 'Idea Tab Pro 8GB/256GB 12.7" + Tab Pen Plus', 'price' => 1799, 'was' => 1999, 'key' => 'tablet_pen'),
				array('brand' => 'SAMSUNG', 'name' => 'Galaxy Tab A11 LTE 8.7" 4GB/64GB — Grey', 'price' => 561, 'was' => 699, 'key' => 'tablet_android'),
				array('brand' => 'AMAZON', 'name' => 'Kindle Scribe 64GB With Premium Pen — Jade', 'price' => 2199, 'was' => 2399, 'key' => 'kindle'),
				array('brand' => 'LENOVO', 'name' => 'Tab Plus 256GB JBL Hi-Fi Speakers 11.5" 2K', 'price' => 1149, 'was' => 1299, 'key' => 'tablet_lifestyle'),
			),
		),
		array(
			'title' => 'Most Trending Deals',
			'products' => array(
				array('brand' => 'SONY', 'name' => 'PlayStation 5 Pro Digital Standalone', 'price' => 3499, 'was' => 0, 'key' => 'ps5_controller', 'is_new' => true),
				array('brand' => 'APPLE', 'name' => 'AirPods Pro (2nd Gen) with USB-C Case', 'price' => 899, 'was' => 999, 'key' => 'airpods'),
				array('brand' => 'SAMSUNG', 'name' => 'Galaxy S25 Ultra 512GB — Titanium Grey', 'price' => 4299, 'was' => 4699, 'key' => 'phone_galaxy'),
				array('brand' => 'BOSE', 'name' => 'QuietComfort Ultra Headphones — Black', 'price' => 1499, 'was' => 1699, 'key' => 'headphones'),
				array('brand' => 'DJI', 'name' => 'Mini 4 Pro Fly More Combo', 'price' => 3999, 'was' => 4299, 'key' => 'drone'),
				array('brand' => 'LOGITECH', 'name' => 'MX Master 3S Wireless Mouse — Graphite', 'price' => 399, 'was' => 449, 'key' => 'mouse'),
			),
		),
		array(
			'title' => 'Upgrade Your Game Set Up',
			'products' => array(
				array('brand' => 'ASUS ROG', 'name' => 'Xbox Ally X Handheld PC 24GB/1TB', 'price' => 4199, 'was' => 4499, 'key' => 'handheld_console'),
				array('brand' => 'RAZER', 'name' => 'Viper V4 Pro Wireless Esports Mouse', 'price' => 669, 'was' => 729, 'key' => 'gaming_mouse'),
				array('brand' => 'NINTENDO', 'name' => 'Switch 2 Joy-Con Bundle + Mario Kart World', 'price' => 2099, 'was' => 0, 'key' => 'nintendo_switch'),
				array('brand' => 'SECRETLAB', 'name' => 'Titan Evo Softweave Gaming Chair — Black', 'price' => 2899, 'was' => 3199, 'key' => 'gaming_chair'),
				array('brand' => 'SONY', 'name' => 'DualSense Wireless Controller — Limited', 'price' => 379, 'was' => 399, 'key' => 'ps5_controller'),
				array('brand' => 'LENOVO', 'name' => 'Legion Go S Handheld PC 32GB/1TB', 'price' => 2799, 'was' => 2999, 'key' => 'handheld_console'),
			),
		),
	);
	foreach ($sections as &$section) {
		$n = 2001;
		foreach ($section['products'] as &$product) {
			if (empty($product['sku'])) {
				$product['sku'] = 'ELC-' . $n++;
			}
		}
		unset($product);
	}
	unset($section);
	return $sections;
}

function epc_electronics_retail_resolve_product_images(array $product): array
{
	$key = isset($product['key']) ? (string) $product['key'] : '';
	if ($key !== '' && !isset($product['image'])) {
		$product['image'] = epc_electronics_retail_img($key, 480, 480);
	}
	if ($key !== '' && empty($product['alt'])) {
		$product['alt'] = epc_electronics_retail_img_alt($key);
		if ($product['alt'] === '') {
			$product['alt'] = $product['name'];
		}
	}
	return $product;
}

function epc_electronics_retail_brands()
{
	return array('Samsung', 'Apple', 'Sony', 'Lenovo', 'Amazon', 'Bose', 'Logitech', 'Nintendo', 'DJI', 'ASUS', 'Razer', 'JBL');
}

function epc_electronics_retail_featured_categories()
{
	$items = array(
		array('name' => 'Gaming', 'href' => '/shop/search?q=gaming', 'key' => 'gaming_setup'),
		array('name' => 'Phones', 'href' => '/shop/search?q=mobile', 'key' => 'smartphone'),
		array('name' => 'Audio', 'href' => '/shop/search?q=audio', 'key' => 'audio_gear'),
		array('name' => 'Laptops', 'href' => '/shop/search?q=laptop', 'key' => 'macbook'),
		array('name' => 'Tablets', 'href' => '/shop/search?q=tablet', 'key' => 'tablet_lifestyle'),
		array('name' => 'Smart Home', 'href' => '/shop/search?q=smart+home', 'key' => 'smart_home'),
		array('name' => 'TV & Cinema', 'href' => '/shop/search?q=tv', 'key' => 'tv_living'),
		array('name' => 'Cameras', 'href' => '/shop/search?q=camera', 'key' => 'camera'),
	);
	$out = array();
	foreach ($items as $item) {
		$key = $item['key'];
		unset($item['key']);
		$item['image'] = epc_electronics_retail_img($key, 640, 420);
		$item['alt'] = epc_electronics_retail_img_alt($key);
		$out[] = $item;
	}
	return $out;
}

function epc_electronics_retail_format_aed($amount)
{
	$amount = (float) $amount;
	if ($amount <= 0) {
		return '';
	}
	return 'AED ' . number_format($amount, 0, '.', ',');
}

/** Top utility bar — Virgin Megastore AE pattern */
function epc_electronics_retail_utility_links()
{
	return array(
		array('label' => 'Store Locator', 'href' => '/kontakty', 'icon' => 'fa-map-marker'),
		array('label' => 'Help', 'href' => '/kontakty', 'icon' => 'fa-question-circle'),
		array('label' => 'Track Order', 'href' => '/shop/orders', 'icon' => 'fa-truck'),
	);
}

/** Primary mega navigation */
function epc_electronics_retail_mega_nav()
{
	return array(
		array('label' => 'Gaming', 'href' => '/shop/search?q=gaming'),
		array('label' => 'Tech', 'href' => '/shop/search?q=tech'),
		array('label' => 'Mobiles', 'href' => '/shop/search?q=mobile'),
		array('label' => 'Laptops', 'href' => '/shop/search?q=laptop'),
		array('label' => 'Tablets', 'href' => '/shop/search?q=tablet'),
		array('label' => 'Audio', 'href' => '/shop/search?q=audio'),
		array('label' => 'Wearables', 'href' => '/shop/search?q=wearables'),
		array('label' => 'Smart Home', 'href' => '/shop/search?q=smart+home'),
		array('label' => 'Toys', 'href' => '/shop/search?q=toys'),
		array('label' => 'Books', 'href' => '/shop/search?q=books'),
		array('label' => 'Music', 'href' => '/shop/search?q=music'),
		array('label' => 'Fashion', 'href' => '/shop/search?q=fashion'),
		array('label' => 'Deals', 'href' => '/shop/search?q=deals', 'highlight' => true),
	);
}

/** Footer column groups */
function epc_electronics_retail_footer_columns()
{
	require_once __DIR__ . '/epc_electronics_retail_helpers.php';
	$store = epc_electronics_retail_store_name();
	return array(
		array(
			'title' => 'Customer Service',
			'links' => array(
				array('label' => 'Contact Us', 'href' => '/kontakty'),
				array('label' => 'FAQs', 'href' => '/kontakty'),
				array('label' => 'Delivery Information', 'href' => '/o-dostavke'),
				array('label' => 'Returns & Refunds', 'href' => '/ob-oplate'),
				array('label' => 'Track Your Order', 'href' => '/shop/orders'),
			),
		),
		array(
			'title' => 'About ' . $store,
			'links' => array(
				array('label' => 'About Us', 'href' => '/kontakty'),
				array('label' => 'Careers', 'href' => '/kontakty'),
				array('label' => 'Store Locator', 'href' => '/kontakty'),
				array('label' => 'Gift Cards', 'href' => '/shop/search?q=gift+card'),
			),
		),
		array(
			'title' => 'Shop',
			'links' => array(
				array('label' => 'Gaming', 'href' => '/shop/search?q=gaming'),
				array('label' => 'Tech & Gadgets', 'href' => '/shop/search?q=tech'),
				array('label' => 'Laptops & Tablets', 'href' => '/shop/search?q=laptop'),
				array('label' => 'Mobiles', 'href' => '/shop/search?q=mobile'),
				array('label' => 'Books & Music', 'href' => '/shop/search?q=books'),
				array('label' => 'Online Exclusives', 'href' => '/shop/search?q=online'),
			),
		),
		array(
			'title' => 'My Account',
			'links' => array(
				array('label' => 'Sign In', 'href' => '/users/register'),
				array('label' => 'My Orders', 'href' => '/shop/orders'),
				array('label' => 'Wishlist', 'href' => '/shop/zakladki'),
				array('label' => 'Loyalty Program', 'href' => '/kontakty'),
			),
		),
	);
}

function epc_electronics_retail_social_links()
{
	return array(
		array('label' => 'Facebook', 'href' => 'https://www.facebook.com/', 'icon' => 'fa-facebook'),
		array('label' => 'Instagram', 'href' => 'https://www.instagram.com/', 'icon' => 'fa-instagram'),
		array('label' => 'Twitter', 'href' => 'https://twitter.com/', 'icon' => 'fa-twitter'),
		array('label' => 'YouTube', 'href' => 'https://www.youtube.com/', 'icon' => 'fa-youtube-play'),
	);
}

function epc_electronics_retail_payment_methods()
{
	return array('visa', 'mastercard', 'paypal', 'applepay', 'tabby', 'tamara');
}

/** Product card badge: sale | new | empty */
function epc_electronics_retail_product_badge(array $product)
{
	if (!empty($product['badge'])) {
		return (string) $product['badge'];
	}
	$was = isset($product['was']) ? (float) $product['was'] : 0;
	$price = isset($product['price']) ? (float) $product['price'] : 0;
	if ($was > 0 && $price > 0 && $was > $price) {
		return 'sale';
	}
	if (!empty($product['is_new'])) {
		return 'new';
	}
	return '';
}

function epc_er_pro_hero_eyebrow(): string
{
	return 'Tech, gaming & lifestyle retail';
}

function epc_er_pro_hero_title(): string
{
	return 'Upgrade your setup with flagship tech — scanned, verified, ready to ship.';
}

function epc_er_pro_hero_copy(): string
{
	return 'Phones, gaming, audio, tablets and smart home — official UAE warranty with prices in AED.';
}

/** @return array<int, array{label: string, href: string, icon: string, primary?: bool}> */
function epc_er_pro_hero_actions(string $lang): array
{
	return array(
		array('label' => 'Shop gaming', 'href' => '/shop/search?q=gaming', 'icon' => 'fa-gamepad', 'primary' => true),
		array('label' => 'Latest phones', 'href' => '/shop/search?q=mobile', 'icon' => 'fa-mobile'),
		array('label' => 'Audio deals', 'href' => '/shop/search?q=audio', 'icon' => 'fa-headphones'),
	);
}

/** @return array<int, array{value: string, label: string}> */
function epc_er_pro_hero_stats(): array
{
	return array(
		array('value' => 'UAE', 'label' => 'official warranty'),
		array('value' => 'Free', 'label' => 'delivery 200+'),
		array('value' => 'AED', 'label' => 'clear pricing'),
	);
}
