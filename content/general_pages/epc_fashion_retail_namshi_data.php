<?php
/**
 * Namshi-style fashion & beauty demo catalogue for retail tenants.
 * Static sample data — safe on shared tenant DB (no shop_catalogue inserts).
 * Product photos: Unsplash CDN (fashion & beauty lifestyle shots).
 */
defined('_ASTEXE_') or die('No access');

/** @return array<string, array{id: string, alt: string}> */
function epc_fashion_retail_namshi_image_catalog(): array
{
	return array(
		'beauty_flatlay' => array('id' => 'photo-1596462502278-4c4d5f9e4c8e', 'alt' => 'Beauty products flat lay with lipstick and skincare'),
		'skincare' => array('id' => 'photo-1570175171301-c871272f48b3', 'alt' => 'Skincare serum and moisturiser bottles'),
		'perfume' => array('id' => 'photo-1541643600914-78b084683601', 'alt' => 'Luxury fragrance bottle on marble'),
		'makeup' => array('id' => 'photo-1522335789203-aabd1fc54bc9', 'alt' => 'Makeup palette and brushes'),
		'dress' => array('id' => 'photo-1595777457582-71d0a6f8b3c8', 'alt' => 'Elegant summer dress on hanger'),
		'streetwear' => array('id' => 'photo-1483985988359-9b0a2f4f0a3e', 'alt' => 'Fashion model in contemporary streetwear'),
		'shoes' => array('id' => 'photo-1543163521-0582f0cfc129', 'alt' => 'Designer sneakers on white background'),
		'handbag' => array('id' => 'photo-1584917865446-14fe4163304e', 'alt' => 'Leather handbag fashion accessory'),
		'jewellery' => array('id' => 'photo-1515562141203-7a88fb0841ec', 'alt' => 'Gold jewellery and rings'),
		'haircare' => array('id' => 'photo-1527797110545-9663a9a4a0a5', 'alt' => 'Hair styling products and tools'),
		'sunglasses' => array('id' => 'photo-1572635196233-4a8726f5f511', 'alt' => 'Designer sunglasses fashion accessory'),
		'activewear' => array('id' => 'photo-1518310383802-640c23307963', 'alt' => 'Activewear leggings and sports top'),
		'watch' => array('id' => 'photo-1523275335684-37898b6baf30', 'alt' => 'Minimalist wristwatch on wrist'),
		'lipstick' => array('id' => 'photo-1586495777744-4413f210a962', 'alt' => 'Red lipstick beauty product'),
		'foundation' => array('id' => 'photo-1631214524020-7e8db0e6a0a5', 'alt' => 'Foundation and concealer makeup'),
		'serum' => array('id' => 'photo-162091656639246-218f362a90af', 'alt' => 'Facial serum dropper bottle'),
		'fragrance_set' => array('id' => 'photo-1592945403249-a6b6cbb9f239', 'alt' => 'Perfume gift set collection'),
		'blazer' => array('id' => 'photo-1591047139829-d91aecb6c2db', 'alt' => 'Tailored blazer fashion outfit'),
		'sandals' => array('id' => 'photo-1543163521-0582f0cfc129', 'alt' => 'Summer sandals footwear'),
		'beauty_model' => array('id' => 'photo-1522337360788-8a4872da2b7c', 'alt' => 'Beauty portrait with glowing skin'),
	);
}

function epc_fashion_retail_namshi_img(string $keyOrPath, int $w = 400, int $h = 0): string
{
	static $catalog = null;
	if ($catalog === null) {
		$catalog = epc_fashion_retail_namshi_image_catalog();
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

function epc_fashion_retail_namshi_img_alt(string $key): string
{
	$catalog = epc_fashion_retail_namshi_image_catalog();
	return isset($catalog[$key]['alt']) ? (string) $catalog[$key]['alt'] : '';
}

function epc_fashion_retail_namshi_promo_strip(): array
{
	return array(
		array('label' => 'Free delivery on orders over AED 150', 'href' => '/o-dostavke'),
		array('label' => 'Beauty essentials in 90 minutes — Dubai', 'href' => '/shop/search?q=beauty'),
		array('label' => 'New season arrivals — up to 40% off', 'href' => '/shop/search?q=sale'),
		array('label' => 'Easy returns within 14 days', 'href' => '/ob-oplate'),
	);
}

function epc_fashion_retail_namshi_trust_badges(): array
{
	return array(
		array('icon' => 'fa-truck', 'title' => 'Fast UAE delivery', 'text' => 'Same-day in select cities'),
		array('icon' => 'fa-refresh', 'title' => 'Easy returns', 'text' => '14-day hassle-free policy'),
		array('icon' => 'fa-tag', 'title' => '800+ brands', 'text' => 'Fashion, beauty & lifestyle'),
		array('icon' => 'fa-lock', 'title' => 'Secure checkout', 'text' => 'Tabby, Tamara & cards'),
	);
}

function epc_fashion_retail_namshi_departments(): array
{
	return array(
		array('label' => 'Women', 'href' => '/shop/search?q=women', 'active' => false),
		array('label' => 'Men', 'href' => '/shop/search?q=men', 'active' => false),
		array('label' => 'Kids', 'href' => '/shop/search?q=kids', 'active' => false),
		array('label' => 'Beauty', 'href' => '/shop/search?q=beauty', 'active' => true),
		array('label' => 'Home', 'href' => '/shop/search?q=home', 'active' => false),
		array('label' => 'Sports', 'href' => '/shop/search?q=sports', 'active' => false),
	);
}

function epc_fashion_retail_namshi_beauty_tabs(): array
{
	return array(
		array('label' => 'For You', 'href' => '/shop/search?q=beauty', 'active' => true),
		array('label' => 'Makeup', 'href' => '/shop/search?q=makeup', 'active' => false),
		array('label' => 'Skincare', 'href' => '/shop/search?q=skincare', 'active' => false),
		array('label' => 'Hair', 'href' => '/shop/search?q=hair', 'active' => false),
		array('label' => 'Fragrance', 'href' => '/shop/search?q=fragrance', 'active' => false),
		array('label' => 'Bath & Body', 'href' => '/shop/search?q=bath', 'active' => false),
	);
}

function epc_fashion_retail_namshi_category_chips(): array
{
	return array(
		array('label' => 'New Arrivals', 'href' => '/shop/search?q=new+arrivals', 'icon' => 'fa-star'),
		array('label' => 'Sale', 'href' => '/shop/search?q=sale', 'icon' => 'fa-percent', 'highlight' => true),
		array('label' => 'Skincare', 'href' => '/shop/search?q=skincare', 'icon' => ''),
		array('label' => 'Makeup', 'href' => '/shop/search?q=makeup', 'icon' => ''),
		array('label' => 'Fragrance', 'href' => '/shop/search?q=perfume', 'icon' => ''),
		array('label' => 'Hair Care', 'href' => '/shop/search?q=hair', 'icon' => ''),
		array('label' => 'K-Beauty', 'href' => '/shop/search?q=k-beauty', 'icon' => ''),
		array('label' => 'Luxury', 'href' => '/shop/search?q=luxury', 'icon' => ''),
		array('label' => 'Gift Sets', 'href' => '/shop/search?q=gift+set', 'icon' => 'fa-gift'),
	);
}

function epc_fashion_retail_namshi_brand_filters(): array
{
	return array('MAC', 'Charlotte Tilbury', 'Kylie Cosmetics', 'NYX', 'Medicube', 'Beauty of Joseon', 'Dior', 'Chanel', 'NARS', 'Huda Beauty', 'Fenty Beauty', 'KIKO MILANO');
}

function epc_fashion_retail_namshi_hero_slides(): array
{
	return array(
		array(
			'title' => 'Beauty Picked For You',
			'sub' => 'Skincare, makeup & fragrance curated for your routine — prices in AED',
			'cta' => 'Shop Beauty',
			'href' => '/shop/search?q=beauty',
			'image' => epc_fashion_retail_namshi_img('beauty_flatlay', 1400, 900),
			'alt' => epc_fashion_retail_namshi_img_alt('beauty_flatlay'),
			'tone' => 'light',
		),
		array(
			'title' => 'New Season Fashion',
			'sub' => 'Dresses, streetwear & accessories from 800+ global brands',
			'cta' => 'Explore Women',
			'href' => '/shop/search?q=women',
			'image' => epc_fashion_retail_namshi_img('streetwear', 1400, 900),
			'alt' => epc_fashion_retail_namshi_img_alt('streetwear'),
			'tone' => 'dark',
		),
		array(
			'title' => 'Luxury Fragrance Edit',
			'sub' => 'Dior, Chanel, Tom Ford & more — exclusive UAE offers',
			'cta' => 'Shop Fragrance',
			'href' => '/shop/search?q=perfume',
			'image' => epc_fashion_retail_namshi_img('perfume', 1400, 900),
			'alt' => epc_fashion_retail_namshi_img_alt('perfume'),
			'tone' => 'light',
		),
	);
}

function epc_fashion_retail_namshi_category_tiles(): array
{
	$tiles = array(
		array('name' => 'Makeup', 'href' => '/shop/search?q=makeup', 'key' => 'makeup'),
		array('name' => 'Skincare', 'href' => '/shop/search?q=skincare', 'key' => 'skincare'),
		array('name' => 'Fragrance', 'href' => '/shop/search?q=perfume', 'key' => 'perfume'),
		array('name' => 'Hair', 'href' => '/shop/search?q=hair', 'key' => 'haircare'),
		array('name' => 'Women', 'href' => '/shop/search?q=women', 'key' => 'dress'),
		array('name' => 'Shoes', 'href' => '/shop/search?q=shoes', 'key' => 'shoes'),
		array('name' => 'Bags', 'href' => '/shop/search?q=bags', 'key' => 'handbag'),
		array('name' => 'Accessories', 'href' => '/shop/search?q=accessories', 'key' => 'sunglasses'),
	);
	$out = array();
	foreach ($tiles as $tile) {
		$key = $tile['key'];
		unset($tile['key']);
		$tile['image'] = epc_fashion_retail_namshi_img($key, 400, 400);
		$tile['alt'] = epc_fashion_retail_namshi_img_alt($key);
		$out[] = $tile;
	}
	return $out;
}

function epc_fashion_retail_namshi_product_sections(): array
{
	$sections = array(
		array(
			'title' => 'For You — Beauty Essentials',
			'sub' => 'Personalised picks based on trending UAE beauty',
			'products' => array(
				array('brand' => 'MEDICUBE', 'name' => 'Zero Pore Pad 2.0 — 70 pads', 'price' => 89, 'was' => 119, 'key' => 'skincare'),
				array('brand' => 'BEAUTY OF JOSEON', 'name' => 'Relief Sun Rice + Probiotics SPF50+', 'price' => 65, 'was' => 0, 'key' => 'serum', 'is_new' => true),
				array('brand' => 'MAC', 'name' => 'Retro Matte Lipstick — Ruby Woo', 'price' => 110, 'was' => 0, 'key' => 'lipstick'),
				array('brand' => 'CHARLOTTE TILBURY', 'name' => 'Flawless Filter Foundation — 4 Fair', 'price' => 195, 'was' => 225, 'key' => 'foundation'),
				array('brand' => 'DIOR', 'name' => 'Miss Dior Eau de Parfum 50ml', 'price' => 520, 'was' => 580, 'key' => 'perfume'),
				array('brand' => 'NYX', 'name' => 'Butter Gloss — Tiramisu', 'price' => 35, 'was' => 45, 'key' => 'makeup'),
			),
		),
		array(
			'title' => 'Trending Fashion Deals',
			'sub' => 'Save on dresses, shoes & accessories',
			'products' => array(
				array('brand' => 'RIVER ISLAND', 'name' => 'Floral Midi Dress — Sage Green', 'price' => 149, 'was' => 249, 'key' => 'dress'),
				array('brand' => 'TOMMY HILFIGER', 'name' => 'Classic Logo Sneakers — White', 'price' => 399, 'was' => 499, 'key' => 'shoes'),
				array('brand' => 'MICHAEL KORS', 'name' => 'Jet Set Crossbody Bag — Black', 'price' => 599, 'was' => 799, 'key' => 'handbag'),
				array('brand' => 'DEFACTO', 'name' => 'Relaxed Linen Blazer — Beige', 'price' => 179, 'was' => 259, 'key' => 'blazer'),
				array('brand' => 'PIERRE CARDIN', 'name' => 'Aviator Sunglasses — Gold', 'price' => 129, 'was' => 169, 'key' => 'sunglasses'),
				array('brand' => 'JOCKEY', 'name' => 'Seamless Active Set — Black', 'price' => 199, 'was' => 279, 'key' => 'activewear'),
			),
		),
		array(
			'title' => 'New Arrivals — Beauty & Glow',
			'sub' => 'Fresh drops from K-Beauty and luxury houses',
			'products' => array(
				array('brand' => 'HUDA BEAUTY', 'name' => 'Easy Bake Loose Powder — Cupcake', 'price' => 165, 'was' => 0, 'key' => 'makeup', 'is_new' => true),
				array('brand' => 'FENTY BEAUTY', 'name' => 'Gloss Bomb Heat — Fenty Glow', 'price' => 95, 'was' => 0, 'key' => 'lipstick', 'is_new' => true),
				array('brand' => 'NARS', 'name' => 'Radiant Creamy Concealer — Vanilla', 'price' => 145, 'was' => 0, 'key' => 'foundation'),
				array('brand' => 'CHANEL', 'name' => 'N°5 Eau de Parfum 35ml', 'price' => 485, 'was' => 0, 'key' => 'fragrance_set', 'is_new' => true),
				array('brand' => 'KIKO MILANO', 'name' => '3D Hydra Lip Gloss — 05', 'price' => 55, 'was' => 69, 'key' => 'lipstick'),
				array('brand' => 'Kylie Cosmetics', 'name' => 'Skin Tint Blurring Elixir — 4N', 'price' => 175, 'was' => 199, 'key' => 'beauty_model'),
			),
		),
	);
	foreach ($sections as &$section) {
		$n = 1001;
		foreach ($section['products'] as &$product) {
			if (empty($product['sku'])) {
				$product['sku'] = 'SLN-' . $n++;
			}
		}
		unset($product);
	}
	unset($section);
	return $sections;
}

function epc_fashion_retail_namshi_resolve_product_images(array $product): array
{
	$key = isset($product['key']) ? (string) $product['key'] : '';
	if ($key !== '' && !isset($product['image'])) {
		$product['image'] = epc_fashion_retail_namshi_img($key, 480, 480);
	}
	if ($key !== '' && empty($product['alt'])) {
		$product['alt'] = epc_fashion_retail_namshi_img_alt($key);
		if ($product['alt'] === '') {
			$product['alt'] = $product['name'];
		}
	}
	return $product;
}

function epc_fashion_retail_namshi_format_aed($amount): string
{
	$amount = (float) $amount;
	if ($amount <= 0) {
		return '';
	}
	return 'AED ' . number_format($amount, 0, '.', ',');
}

function epc_fashion_retail_namshi_product_badge(array $product): string
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

function epc_fashion_retail_namshi_utility_links(): array
{
	return array(
		array('label' => 'Help', 'href' => '/kontakty', 'icon' => 'fa-question-circle'),
		array('label' => 'Track Order', 'href' => '/shop/orders', 'icon' => 'fa-truck'),
		array('label' => 'Returns', 'href' => '/ob-oplate', 'icon' => 'fa-refresh'),
	);
}

function epc_fashion_retail_namshi_mega_nav(): array
{
	return array(
		array('label' => 'New In', 'href' => '/shop/search?q=new'),
		array('label' => 'Women', 'href' => '/shop/search?q=women'),
		array('label' => 'Men', 'href' => '/shop/search?q=men'),
		array('label' => 'Beauty', 'href' => '/shop/search?q=beauty', 'highlight' => true),
		array('label' => 'Shoes', 'href' => '/shop/search?q=shoes'),
		array('label' => 'Bags', 'href' => '/shop/search?q=bags'),
		array('label' => 'Sale', 'href' => '/shop/search?q=sale', 'highlight' => true),
	);
}

function epc_fashion_retail_namshi_footer_columns(): array
{
	require_once __DIR__ . '/epc_fashion_retail_namshi_helpers.php';
	$store = epc_fashion_retail_namshi_store_name();
	return array(
		array(
			'title' => 'Customer Care',
			'links' => array(
				array('label' => 'Contact Us', 'href' => '/kontakty'),
				array('label' => 'FAQs', 'href' => '/kontakty'),
				array('label' => 'Delivery', 'href' => '/o-dostavke'),
				array('label' => 'Returns & Refunds', 'href' => '/ob-oplate'),
				array('label' => 'Track Order', 'href' => '/shop/orders'),
			),
		),
		array(
			'title' => 'Shop',
			'links' => array(
				array('label' => 'Women', 'href' => '/shop/search?q=women'),
				array('label' => 'Men', 'href' => '/shop/search?q=men'),
				array('label' => 'Beauty', 'href' => '/shop/search?q=beauty'),
				array('label' => 'Shoes & Bags', 'href' => '/shop/search?q=shoes'),
				array('label' => 'Sale', 'href' => '/shop/search?q=sale'),
			),
		),
		array(
			'title' => 'About ' . $store,
			'links' => array(
				array('label' => 'About Us', 'href' => '/kontakty'),
				array('label' => 'Careers', 'href' => '/kontakty'),
				array('label' => 'Gift Cards', 'href' => '/shop/search?q=gift'),
				array('label' => 'Brand Directory', 'href' => '/shop/search'),
			),
		),
		array(
			'title' => 'My Account',
			'links' => array(
				array('label' => 'Sign In', 'href' => '/users/register'),
				array('label' => 'Orders', 'href' => '/shop/orders'),
				array('label' => 'Wishlist', 'href' => '/shop/zakladki'),
				array('label' => 'Addresses', 'href' => '/shop/orders'),
			),
		),
	);
}

function epc_fashion_retail_namshi_social_links(): array
{
	return array(
		array('label' => 'Instagram', 'href' => 'https://www.instagram.com/', 'icon' => 'fa-instagram'),
		array('label' => 'Facebook', 'href' => 'https://www.facebook.com/', 'icon' => 'fa-facebook'),
		array('label' => 'TikTok', 'href' => 'https://www.tiktok.com/', 'icon' => 'fa-music'),
		array('label' => 'YouTube', 'href' => 'https://www.youtube.com/', 'icon' => 'fa-youtube-play'),
	);
}

function epc_fashion_retail_namshi_payment_methods(): array
{
	return array('visa', 'mastercard', 'applepay', 'tabby', 'tamara');
}

function epc_frn_pro_hero_eyebrow(): string
{
	return 'Fashion & beauty retail';
}

function epc_frn_pro_hero_title(): string
{
	return 'Curated style with runway energy — beauty that moves with you.';
}

function epc_frn_pro_hero_copy(): string
{
	return 'Discover trending looks, K-beauty essentials, and designer fragrances. Fast UAE delivery with prices in AED.';
}

/** @return array<int, array{label: string, href: string, icon: string, primary?: bool}> */
function epc_frn_pro_hero_actions(string $lang): array
{
	return array(
		array('label' => 'Shop For You', 'href' => '/shop/search', 'icon' => 'fa-heart', 'primary' => true),
		array('label' => 'Beauty edit', 'href' => '/shop/search?q=beauty', 'icon' => 'fa-magic'),
		array('label' => 'New arrivals', 'href' => '/shop/search?q=new+arrivals', 'icon' => 'fa-star'),
	);
}

/** @return array<int, array{value: string, label: string}> */
function epc_frn_pro_hero_stats(): array
{
	return array(
		array('value' => '800+', 'label' => 'brands'),
		array('value' => 'AED', 'label' => 'clear pricing'),
		array('value' => '90 min', 'label' => 'beauty delivery'),
	);
}
