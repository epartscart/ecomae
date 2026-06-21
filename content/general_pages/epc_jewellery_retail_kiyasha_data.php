<?php
/**
 * Kiyasha-style jewellery demo catalogue for retail tenants.
 * Product photos: Unsplash CDN (luxury gold & diamond jewellery).
 */
defined('_ASTEXE_') or die('No access');

/** @return array<string, array{id: string, alt: string}> */
function epc_jewellery_retail_kiyasha_image_catalog(): array
{
	return array(
		'gold_rings' => array('id' => 'photo-1605100804763-247f67b3557e', 'alt' => 'Gold diamond engagement rings'),
		'necklace' => array('id' => 'photo-1599643478518-a784e69ba83f', 'alt' => 'Gold pendant necklace on velvet'),
		'earrings' => array('id' => 'photo-1535632066927-ab7c9a509e3f', 'alt' => 'Pearl and gold drop earrings'),
		'bracelet' => array('id' => 'photo-1611591437281-460bfac7a2c3', 'alt' => 'Gold chain bracelet'),
		'wedding_set' => array('id' => 'photo-1515562141207-7a88fb7ce338', 'alt' => 'Bridal wedding jewellery set'),
		'luxury_flatlay' => array('id' => 'photo-1515562141203-7a88fb0841ec', 'alt' => 'Luxury gold jewellery flat lay'),
		'diamond_ring' => array('id' => 'photo-1603561591411-07134e8257ea', 'alt' => 'Solitaire diamond ring'),
		'gold_bangle' => array('id' => 'photo-1573408301185-9146fe634ad0', 'alt' => 'Stacked gold bangles'),
		'pendant' => array('id' => 'photo-1617032217908-3a471e6b5489', 'alt' => 'Diamond pendant on chain'),
		'hoops' => array('id' => 'photo-1596944924616-7b3842611a43', 'alt' => 'Gold hoop earrings'),
		'watch_jewellery' => array('id' => 'photo-1523170335258-f5ed11844a49', 'alt' => 'Luxury watch and jewellery'),
		'gift_box' => array('id' => 'photo-1602173574767-37ac01994b2a', 'alt' => 'Jewellery gift box with ribbon'),
		'rose_gold' => array('id' => 'photo-1611591437281-460bfac7a2c3', 'alt' => 'Rose gold jewellery collection'),
		'layered_neck' => array('id' => 'photo-1506630448389-4e683c67ddb0', 'alt' => 'Layered gold necklaces'),
		'anklet' => array('id' => 'photo-1611085583191-d3f62f1e1f0a', 'alt' => 'Delicate gold anklet'),
		'men_ring' => array('id' => 'photo-1605100804763-247f67b3557e', 'alt' => 'Men\'s signet gold ring'),
	);
}

function epc_jewellery_retail_kiyasha_img(string $keyOrPath, int $w = 400, int $h = 0): string
{
	static $catalog = null;
	if ($catalog === null) {
		$catalog = epc_jewellery_retail_kiyasha_image_catalog();
	}
	if (isset($catalog[$keyOrPath])) {
		return '/content/files/images/storefronts/jewellery/' . $keyOrPath . '.jpg';
	}
	$path = ltrim((string) $keyOrPath, '/');
	$url = 'https://images.unsplash.com/' . $path . '?auto=format&fit=crop&w=' . (int) $w . '&q=82';
	if ($h > 0) {
		$url .= '&h=' . (int) $h;
	}
	return $url;
}

function epc_jewellery_retail_kiyasha_img_alt(string $key): string
{
	$catalog = epc_jewellery_retail_kiyasha_image_catalog();
	return isset($catalog[$key]['alt']) ? (string) $catalog[$key]['alt'] : '';
}

function epc_jewellery_retail_kiyasha_promo_strip(): array
{
	return array(
		array('label' => 'Free insured delivery on orders over AED 500', 'href' => '/o-dostavke'),
		array('label' => 'Certified 18K & 22K gold — UAE hallmark', 'href' => '/shop/search?q=gold'),
		array('label' => 'Bridal edit — complimentary ring sizing', 'href' => '/shop/search?q=bridal'),
		array('label' => 'Easy returns within 14 days', 'href' => '/ob-oplate'),
	);
}

function epc_jewellery_retail_kiyasha_trust_badges(): array
{
	return array(
		array('icon' => 'fa-shield', 'title' => 'Certified gold', 'text' => 'Hallmarked 18K & 22K pieces'),
		array('icon' => 'fa-truck', 'title' => 'Insured UAE delivery', 'text' => 'Secure packaging & tracking'),
		array('icon' => 'fa-diamond', 'title' => 'GIA-ready diamonds', 'text' => 'Solitaires & bridal sets'),
		array('icon' => 'fa-gift', 'title' => 'Luxury gifting', 'text' => 'Ribbon box on every order'),
	);
}

function epc_jewellery_retail_kiyasha_departments(): array
{
	return array(
		array('label' => 'Fine Jewellery', 'href' => '/shop/search?q=jewellery', 'active' => true),
		array('label' => 'Gold', 'href' => '/shop/search?q=gold', 'active' => false),
		array('label' => 'Diamonds', 'href' => '/shop/search?q=diamond', 'active' => false),
		array('label' => 'Bridal', 'href' => '/shop/search?q=bridal', 'active' => false),
		array('label' => 'Gifts', 'href' => '/shop/search?q=gifts', 'active' => false),
		array('label' => 'Sale', 'href' => '/shop/search?q=sale', 'active' => false),
	);
}

function epc_jewellery_retail_kiyasha_collection_tabs(): array
{
	return array(
		array('label' => 'New Arrivals', 'href' => '/shop/search?q=new', 'active' => true),
		array('label' => 'Rings', 'href' => '/shop/search?q=rings', 'active' => false),
		array('label' => 'Necklaces', 'href' => '/shop/search?q=necklaces', 'active' => false),
		array('label' => 'Earrings', 'href' => '/shop/search?q=earrings', 'active' => false),
		array('label' => 'Bracelets', 'href' => '/shop/search?q=bracelets', 'active' => false),
		array('label' => 'Wedding', 'href' => '/shop/search?q=wedding', 'active' => false),
	);
}

function epc_jewellery_retail_kiyasha_category_chips(): array
{
	return array(
		array('label' => 'New In', 'href' => '/shop/search?q=new', 'icon' => 'fa-star'),
		array('label' => 'Sale', 'href' => '/shop/search?q=sale', 'icon' => 'fa-percent', 'highlight' => true),
		array('label' => 'Rings', 'href' => '/shop/search?q=rings', 'icon' => ''),
		array('label' => 'Necklaces', 'href' => '/shop/search?q=necklaces', 'icon' => ''),
		array('label' => 'Earrings', 'href' => '/shop/search?q=earrings', 'icon' => ''),
		array('label' => 'Gold', 'href' => '/shop/search?q=gold', 'icon' => ''),
		array('label' => 'Diamonds', 'href' => '/shop/search?q=diamond', 'icon' => ''),
		array('label' => 'Bridal', 'href' => '/shop/search?q=bridal', 'icon' => ''),
		array('label' => 'Gift Sets', 'href' => '/shop/search?q=gift', 'icon' => 'fa-gift'),
	);
}

function epc_jewellery_retail_kiyasha_brand_filters(): array
{
	return array('Cartier', 'Tiffany & Co.', 'Bvlgari', 'Pandora', 'Damiani', 'Chopard', 'Van Cleef', 'Graff', 'Messika', 'David Morris', 'Chaumet', 'Boucheron');
}

function epc_jewellery_retail_kiyasha_hero_slides(): array
{
	return array(
		array(
			'title' => 'Timeless Gold Collections',
			'sub' => '18K & 22K rings, necklaces and bracelets — curated for the UAE — prices in AED',
			'cta' => 'Shop Gold',
			'href' => '/shop/search?q=gold',
			'image' => epc_jewellery_retail_kiyasha_img('luxury_flatlay', 1400, 900),
			'alt' => epc_jewellery_retail_kiyasha_img_alt('luxury_flatlay'),
			'tone' => 'light',
		),
		array(
			'title' => 'Diamond Bridal Edit',
			'sub' => 'Solitaire rings, wedding bands & matching sets — insured delivery',
			'cta' => 'Explore Bridal',
			'href' => '/shop/search?q=bridal',
			'image' => epc_jewellery_retail_kiyasha_img('wedding_set', 1400, 900),
			'alt' => epc_jewellery_retail_kiyasha_img_alt('wedding_set'),
			'tone' => 'dark',
		),
		array(
			'title' => 'Everyday Elegance',
			'sub' => 'Earrings, pendants & layering necklaces — gift-ready packaging',
			'cta' => 'Shop Bestsellers',
			'href' => '/shop/search?q=earrings',
			'image' => epc_jewellery_retail_kiyasha_img('necklace', 1400, 900),
			'alt' => epc_jewellery_retail_kiyasha_img_alt('necklace'),
			'tone' => 'light',
		),
	);
}

function epc_jewellery_retail_kiyasha_category_tiles(): array
{
	$tiles = array(
		array('name' => 'Rings', 'href' => '/shop/search?q=rings', 'key' => 'gold_rings'),
		array('name' => 'Necklaces', 'href' => '/shop/search?q=necklaces', 'key' => 'necklace'),
		array('name' => 'Earrings', 'href' => '/shop/search?q=earrings', 'key' => 'earrings'),
		array('name' => 'Bracelets', 'href' => '/shop/search?q=bracelets', 'key' => 'bracelet'),
		array('name' => 'Bridal', 'href' => '/shop/search?q=bridal', 'key' => 'wedding_set'),
		array('name' => 'Diamonds', 'href' => '/shop/search?q=diamond', 'key' => 'diamond_ring'),
		array('name' => 'Gold', 'href' => '/shop/search?q=gold', 'key' => 'gold_bangle'),
		array('name' => 'Gifts', 'href' => '/shop/search?q=gifts', 'key' => 'gift_box'),
	);
	$out = array();
	foreach ($tiles as $tile) {
		$key = $tile['key'];
		unset($tile['key']);
		$tile['image'] = epc_jewellery_retail_kiyasha_img($key, 400, 400);
		$tile['alt'] = epc_jewellery_retail_kiyasha_img_alt($key);
		$out[] = $tile;
	}
	return $out;
}

function epc_jewellery_retail_kiyasha_product_sections(): array
{
	$sections = array(
		array(
			'title' => 'Curated For You — Gold Essentials',
			'sub' => 'Bestselling 18K pieces loved in Dubai & Abu Dhabi',
			'products' => array(
				array('brand' => 'KIYASHA GOLD', 'name' => '18K Yellow Gold Band Ring — Size 52', 'price' => 1290, 'was' => 1490, 'key' => 'gold_rings'),
				array('brand' => 'THE TREND', 'name' => 'Layered Chain Necklace — 45cm', 'price' => 890, 'was' => 0, 'key' => 'layered_neck', 'is_new' => true),
				array('brand' => 'KIYASHA', 'name' => 'Pearl Drop Earrings — 14K Gold', 'price' => 650, 'was' => 799, 'key' => 'earrings'),
				array('brand' => 'LUXE EDIT', 'name' => 'Cuban Link Bracelet — 18K', 'price' => 2100, 'was' => 2450, 'key' => 'bracelet'),
				array('brand' => 'THE TREND', 'name' => 'Stackable Gold Bangles — Set of 3', 'price' => 1750, 'was' => 0, 'key' => 'gold_bangle'),
				array('brand' => 'KIYASHA', 'name' => 'Delicate Anklet — Adjustable', 'price' => 420, 'was' => 499, 'key' => 'anklet'),
			),
		),
		array(
			'title' => 'Diamond & Bridal Highlights',
			'sub' => 'Engagement rings and wedding sets with certificate options',
			'products' => array(
				array('brand' => 'BRIDAL ATELIER', 'name' => 'Solitaire Diamond Ring — 0.50ct G/VVS', 'price' => 8900, 'was' => 9900, 'key' => 'diamond_ring'),
				array('brand' => 'THE TREND', 'name' => 'Bridal Set — Ring & Band 18K', 'price' => 5200, 'was' => 5800, 'key' => 'wedding_set'),
				array('brand' => 'KIYASHA', 'name' => 'Pendant Necklace — 0.25ct Diamond', 'price' => 3200, 'was' => 0, 'key' => 'pendant', 'is_new' => true),
				array('brand' => 'LUXE EDIT', 'name' => 'Eternity Band — 18K White Gold', 'price' => 4100, 'was' => 4600, 'key' => 'gold_rings'),
				array('brand' => 'THE TREND', 'name' => 'Gold Hoop Earrings — Medium', 'price' => 780, 'was' => 920, 'key' => 'hoops'),
				array('brand' => 'KIYASHA GIFT', 'name' => 'Luxury Jewellery Gift Set — Boxed', 'price' => 1190, 'was' => 0, 'key' => 'gift_box', 'is_new' => true),
			),
		),
		array(
			'title' => 'New Arrivals — Sparkle Edit',
			'sub' => 'Fresh drops in rose gold and contemporary silhouettes',
			'products' => array(
				array('brand' => 'ROSE COLLECTION', 'name' => 'Rose Gold Signet Ring', 'price' => 980, 'was' => 0, 'key' => 'rose_gold', 'is_new' => true),
				array('brand' => 'THE TREND', 'name' => 'Men\'s Gold Band — Matte Finish', 'price' => 1450, 'was' => 0, 'key' => 'men_ring', 'is_new' => true),
				array('brand' => 'KIYASHA', 'name' => 'Diamond Stud Earrings — 0.20ct tw', 'price' => 2400, 'was' => 2690, 'key' => 'earrings'),
				array('brand' => 'WATCH & JEWEL', 'name' => 'Bracelet Watch Gift Duo', 'price' => 3600, 'was' => 0, 'key' => 'watch_jewellery', 'is_new' => true),
				array('brand' => 'THE TREND', 'name' => 'Layered Necklace Duo — Gold', 'price' => 1120, 'was' => 1290, 'key' => 'layered_neck'),
				array('brand' => 'KIYASHA', 'name' => 'Heart Pendant — 18K with Chain', 'price' => 890, 'was' => 0, 'key' => 'pendant'),
			),
		),
	);
	foreach ($sections as &$section) {
		$n = 3001;
		foreach ($section['products'] as &$product) {
			if (empty($product['sku'])) {
				$product['sku'] = 'JWL-' . $n++;
			}
		}
		unset($product);
	}
	unset($section);
	return $sections;
}

function epc_jewellery_retail_kiyasha_resolve_product_images(array $product): array
{
	$key = isset($product['key']) ? (string) $product['key'] : '';
	if ($key !== '' && !isset($product['image'])) {
		$product['image'] = epc_jewellery_retail_kiyasha_img($key, 480, 480);
	}
	if ($key !== '' && empty($product['alt'])) {
		$product['alt'] = epc_jewellery_retail_kiyasha_img_alt($key);
		if ($product['alt'] === '') {
			$product['alt'] = $product['name'];
		}
	}
	return $product;
}

function epc_jewellery_retail_kiyasha_format_aed($amount): string
{
	$amount = (float) $amount;
	if ($amount <= 0) {
		return '';
	}
	return 'AED ' . number_format($amount, 0, '.', ',');
}

function epc_jewellery_retail_kiyasha_product_badge(array $product): string
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

function epc_jewellery_retail_kiyasha_utility_links(): array
{
	return array(
		array('label' => 'Help', 'href' => '/kontakty', 'icon' => 'fa-question-circle'),
		array('label' => 'Track Order', 'href' => '/shop/orders', 'icon' => 'fa-truck'),
		array('label' => 'Ring Sizing', 'href' => '/kontakty', 'icon' => 'fa-expand'),
	);
}

function epc_jewellery_retail_kiyasha_mega_nav(): array
{
	return array(
		array('label' => 'New In', 'href' => '/shop/search?q=new'),
		array('label' => 'Rings', 'href' => '/shop/search?q=rings'),
		array('label' => 'Necklaces', 'href' => '/shop/search?q=necklaces'),
		array('label' => 'Bridal', 'href' => '/shop/search?q=bridal', 'highlight' => true),
		array('label' => 'Gold', 'href' => '/shop/search?q=gold'),
		array('label' => 'Diamonds', 'href' => '/shop/search?q=diamond'),
		array('label' => 'Sale', 'href' => '/shop/search?q=sale', 'highlight' => true),
	);
}

function epc_jewellery_retail_kiyasha_footer_columns(): array
{
	require_once __DIR__ . '/epc_jewellery_retail_kiyasha_helpers.php';
	$store = epc_jewellery_retail_kiyasha_store_name();
	return array(
		array(
			'title' => 'Customer Care',
			'links' => array(
				array('label' => 'Contact Us', 'href' => '/kontakty'),
				array('label' => 'Ring Sizing Guide', 'href' => '/kontakty'),
				array('label' => 'Delivery & Insurance', 'href' => '/o-dostavke'),
				array('label' => 'Returns', 'href' => '/ob-oplate'),
				array('label' => 'Track Order', 'href' => '/shop/orders'),
			),
		),
		array(
			'title' => 'Collections',
			'links' => array(
				array('label' => 'Rings', 'href' => '/shop/search?q=rings'),
				array('label' => 'Necklaces', 'href' => '/shop/search?q=necklaces'),
				array('label' => 'Earrings', 'href' => '/shop/search?q=earrings'),
				array('label' => 'Bridal', 'href' => '/shop/search?q=bridal'),
				array('label' => 'Sale', 'href' => '/shop/search?q=sale'),
			),
		),
		array(
			'title' => 'About ' . $store,
			'links' => array(
				array('label' => 'Our Story', 'href' => '/kontakty'),
				array('label' => 'Gold Certification', 'href' => '/kontakty'),
				array('label' => 'Gift Cards', 'href' => '/shop/search?q=gift'),
				array('label' => 'Store Locator', 'href' => '/kontakty'),
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

function epc_jewellery_retail_kiyasha_social_links(): array
{
	return array(
		array('label' => 'Instagram', 'href' => 'https://www.instagram.com/', 'icon' => 'fa-instagram'),
		array('label' => 'Facebook', 'href' => 'https://www.facebook.com/', 'icon' => 'fa-facebook'),
		array('label' => 'Pinterest', 'href' => 'https://www.pinterest.com/', 'icon' => 'fa-pinterest'),
		array('label' => 'YouTube', 'href' => 'https://www.youtube.com/', 'icon' => 'fa-youtube-play'),
	);
}

function epc_jewellery_retail_kiyasha_payment_methods(): array
{
	return array('visa', 'mastercard', 'applepay', 'tabby', 'tamara');
}

function epc_jrk_pro_hero_eyebrow(): string
{
	return 'Fine gold & diamond jewellery';
}

function epc_jrk_pro_hero_title(): string
{
	return 'Luxury pieces with a golden glow — bridal, diamonds, and heirloom craft.';
}

function epc_jrk_pro_hero_copy(): string
{
	return 'Certified 18K and 22K gold, solitaires and bridal sets — insured UAE delivery with prices in AED.';
}

/** @return array<int, array{label: string, href: string, icon: string, primary?: bool}> */
function epc_jrk_pro_hero_actions(string $lang): array
{
	return array(
		array('label' => 'Shop gold', 'href' => '/shop/search?q=gold', 'icon' => 'fa-diamond', 'primary' => true),
		array('label' => 'Bridal edit', 'href' => '/shop/search?q=bridal', 'icon' => 'fa-heart'),
		array('label' => 'Gift sets', 'href' => '/shop/search?q=gifts', 'icon' => 'fa-gift'),
	);
}

/** @return array<int, array{value: string, label: string}> */
function epc_jrk_pro_hero_stats(): array
{
	return array(
		array('value' => '18K', 'label' => 'certified gold'),
		array('value' => 'Insured', 'label' => 'UAE delivery'),
		array('value' => 'AED', 'label' => 'clear pricing'),
	);
}
