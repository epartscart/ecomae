<?php
ini_set('display_errors', 'stderr');
$root = rtrim($argv[1] ?? '/workspace-wt/small-done', '/');
$out = rtrim($argv[2] ?? '/tmp/plan_q1d_assets', '/');
@mkdir($out, 0777, true);
if (!defined('_ASTEXE_')) {
	define('_ASTEXE_', 1);
}
require $root . '/content/general_pages/epc_jewellery_retail_kiyasha_data.php';
file_put_contents($out . '/jrk.json', json_encode(array(
	'images' => epc_jewellery_retail_kiyasha_image_catalog(),
	'promo' => epc_jewellery_retail_kiyasha_promo_strip(),
	'trust' => epc_jewellery_retail_kiyasha_trust_badges(),
	'departments' => epc_jewellery_retail_kiyasha_departments(),
	'tabs' => epc_jewellery_retail_kiyasha_collection_tabs(),
	'chips' => epc_jewellery_retail_kiyasha_category_chips(),
	'brands' => epc_jewellery_retail_kiyasha_brand_filters(),
	'hero' => epc_jewellery_retail_kiyasha_hero_slides(),
	'tiles' => epc_jewellery_retail_kiyasha_category_tiles(),
	'sections' => epc_jewellery_retail_kiyasha_product_sections(),
	'utility' => epc_jewellery_retail_kiyasha_utility_links(),
	'mega' => epc_jewellery_retail_kiyasha_mega_nav(),
	'social' => epc_jewellery_retail_kiyasha_social_links(),
	'payments' => epc_jewellery_retail_kiyasha_payment_methods(),
	'eyebrow' => epc_jrk_pro_hero_eyebrow(),
	'title' => epc_jrk_pro_hero_title(),
	'copy' => epc_jrk_pro_hero_copy(),
	'actions' => epc_jrk_pro_hero_actions('en'),
	'stats' => epc_jrk_pro_hero_stats(),
), JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES));
require $root . '/content/general_pages/epc_fashion_retail_namshi_data.php';
file_put_contents($out . '/frn.json', json_encode(array(
	'images' => epc_fashion_retail_namshi_image_catalog(),
	'promo' => epc_fashion_retail_namshi_promo_strip(),
	'trust' => epc_fashion_retail_namshi_trust_badges(),
	'departments' => epc_fashion_retail_namshi_departments(),
	'tabs' => epc_fashion_retail_namshi_beauty_tabs(),
	'chips' => epc_fashion_retail_namshi_category_chips(),
	'brands' => epc_fashion_retail_namshi_brand_filters(),
	'hero' => epc_fashion_retail_namshi_hero_slides(),
	'tiles' => epc_fashion_retail_namshi_category_tiles(),
	'sections' => epc_fashion_retail_namshi_product_sections(),
	'utility' => epc_fashion_retail_namshi_utility_links(),
	'mega' => epc_fashion_retail_namshi_mega_nav(),
	'social' => epc_fashion_retail_namshi_social_links(),
	'payments' => epc_fashion_retail_namshi_payment_methods(),
	'eyebrow' => epc_frn_pro_hero_eyebrow(),
	'title' => epc_frn_pro_hero_title(),
	'copy' => epc_frn_pro_hero_copy(),
	'actions' => epc_frn_pro_hero_actions('en'),
	'stats' => epc_frn_pro_hero_stats(),
), JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES));
require $root . '/content/general_pages/epc_electronics_retail_data.php';
file_put_contents($out . '/er.json', json_encode(array(
	'images' => epc_electronics_retail_image_catalog(),
	'promo' => epc_electronics_retail_promo_strip(),
	'trust' => epc_electronics_retail_trust_badges(),
	'hero' => epc_electronics_retail_hero_slides(),
	'tiles' => epc_electronics_retail_category_tiles(),
	'deals' => epc_electronics_retail_deal_sections(),
	'brands' => epc_electronics_retail_brands(),
	'featured' => epc_electronics_retail_featured_categories(),
	'utility' => epc_electronics_retail_utility_links(),
	'mega' => epc_electronics_retail_mega_nav(),
	'social' => epc_electronics_retail_social_links(),
	'payments' => epc_electronics_retail_payment_methods(),
	'eyebrow' => epc_er_pro_hero_eyebrow(),
	'title' => epc_er_pro_hero_title(),
	'copy' => epc_er_pro_hero_copy(),
	'actions' => epc_er_pro_hero_actions('en'),
	'stats' => epc_er_pro_hero_stats(),
), JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES));
echo "ok\n";
