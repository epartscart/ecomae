<?php
/**
 * Remap industry hub data into a client-ready sub-industry site profile.
 *
 * Keeps the premium 3D storefront template, but swaps hero media, copy,
 * categories, products, and gallery to the active sub-industry pack.
 *
 * Expects (from _base_template.php): $industryData, $activeSub, $name, …
 * Sets: $isSubIndustrySite, $subParentName, $subParentHubUrl, $subSiblings,
 * and reassigns $industryData + derived display vars.
 */
defined('_ASTEXE_') or die('No access');

$isSubIndustrySite = true;
$subLabel = (string) ($activeSub['label'] ?? '');
$subSlug = (string) ($activeSub['slug'] ?? '');
$subParentName = (string) $name;
$subParentHubUrl = rtrim((string) $seoBase, '/') . '/';
$subParentIcon = (string) $icon;

$subProductsMap = $industryData['sub_industry_products'] ?? array();
$subPack = (isset($subProductsMap[$subLabel]) && is_array($subProductsMap[$subLabel]))
	? $subProductsMap[$subLabel]
	: array();

$subDesc = trim((string) ($subPack['desc'] ?? ''));
if ($subDesc === '') {
	$subDesc = $subLabel . ' businesses run storefront, control panel, and ERP on ecomae — '
		. 'configured for this ' . $subParentName . ' vertical with dedicated categories and workflows.';
}

$subCats = array();
if (!empty($subPack['categories']) && is_array($subPack['categories'])) {
	foreach ($subPack['categories'] as $c) {
		$c = trim((string) $c);
		if ($c !== '') {
			$subCats[] = $c;
		}
	}
}
if ($subCats === array()) {
	$subCats = array('Core Products', 'Premium Services', 'Packages', 'Accessories', 'Support', 'Analytics');
}

$rawProds = array();
if (!empty($subPack['products']) && is_array($subPack['products'])) {
	$rawProds = $subPack['products'];
}

$hubHero = (string) ($industryData['hero_photo'] ?? '');
$subPhoto = trim((string) ($subPack['photo'] ?? ''));
if ($subPhoto === '' || $subPhoto === $hubHero) {
	foreach ($rawProds as $rp) {
		if (!empty($rp['image']) && (string) $rp['image'] !== $hubHero) {
			$subPhoto = (string) $rp['image'];
			break;
		}
	}
}
// Prefer large hero crop for Unsplash URLs
if ($subPhoto !== '' && strpos($subPhoto, 'images.unsplash.com') !== false) {
	if (preg_match('/([?&])w=\d+/', $subPhoto)) {
		$subPhoto = preg_replace('/([?&])w=\d+/', '$1w=1600', $subPhoto) ?: $subPhoto;
	} else {
		$subPhoto .= (strpos($subPhoto, '?') !== false ? '&' : '?') . 'w=1600&q=80';
	}
	$subPhoto = preg_replace('/([?&])q=\d+/', '$1q=80', $subPhoto) ?: $subPhoto;
}
if ($subPhoto === '') {
	$subPhoto = $hubHero;
}

// Build a fuller product grid from pack products + categories (client-demo ready)
$prodIcons = array('fa-diamond', 'fa-cubes', 'fa-tags', 'fa-wrench', 'fa-certificate', 'fa-star', 'fa-shopping-bag', 'fa-cogs');
$samplePrices = array('AED 450', 'AED 1,200', 'AED 2,800', 'AED 75', 'AED 3,500', 'AED 950', 'AED 5,000', 'AED 320');
$builtProducts = array();
foreach ($rawProds as $i => $rp) {
	$builtProducts[] = array(
		'name' => (string) ($rp['name'] ?? ($subLabel . ' Offering')),
		'price' => (string) ($rp['price'] ?? $samplePrices[$i % count($samplePrices)]),
		'icon' => $prodIcons[$i % count($prodIcons)],
		'category' => (string) ($rp['category'] ?? ($subCats[$i % count($subCats)] ?? 'Featured')),
		'image' => (string) ($rp['image'] ?? $subPhoto),
	);
}
// Ensure at least one product card per category
$imgPool = array();
foreach ($builtProducts as $bp) {
	if (!empty($bp['image'])) {
		$imgPool[] = $bp['image'];
	}
}
if ($imgPool === array() && $subPhoto !== '') {
	$imgPool[] = $subPhoto;
}
foreach ($subCats as $ci => $cat) {
	$hasCat = false;
	foreach ($builtProducts as $bp) {
		if (strcasecmp((string) ($bp['category'] ?? ''), $cat) === 0) {
			$hasCat = true;
			break;
		}
	}
	if ($hasCat) {
		continue;
	}
	$builtProducts[] = array(
		'name' => $cat . ' — ' . $subLabel,
		'price' => $samplePrices[$ci % count($samplePrices)],
		'icon' => $prodIcons[$ci % count($prodIcons)],
		'category' => $cat,
		'image' => $imgPool[$ci % max(1, count($imgPool))] ?? $subPhoto,
	);
}
// Cap catalog size for page weight
if (count($builtProducts) > 12) {
	$builtProducts = array_slice($builtProducts, 0, 12);
}

$gallery = array();
if ($subPhoto !== '') {
	$gallery[] = $subPhoto;
}
foreach ($builtProducts as $bp) {
	if (!empty($bp['image']) && !in_array($bp['image'], $gallery, true)) {
		$gallery[] = $bp['image'];
	}
	if (count($gallery) >= 6) {
		break;
	}
}

$subSiblings = array();
foreach (array_values(is_array($subIndustries) ? $subIndustries : array()) as $siLabel) {
	$siSlug = function_exists('epc_industry_seo_sub_slug')
		? epc_industry_seo_sub_slug((string) $siLabel)
		: '';
	if ($siSlug === '' || $siSlug === $subSlug) {
		continue;
	}
	$sibPack = $subProductsMap[$siLabel] ?? array();
	$subSiblings[] = array(
		'label' => (string) $siLabel,
		'slug' => $siSlug,
		'url' => rtrim((string) $seoBase, '/') . '/' . $siSlug,
		'photo' => (string) ($sibPack['photo'] ?? ''),
		'desc' => (string) ($sibPack['desc'] ?? ''),
		'categories' => isset($sibPack['categories']) && is_array($sibPack['categories']) ? $sibPack['categories'] : array(),
		'products' => isset($sibPack['products']) && is_array($sibPack['products']) ? $sibPack['products'] : array(),
	);
	if (count($subSiblings) >= 12) {
		break;
	}
}

$catPreview = implode(', ', array_slice($subCats, 0, 6));

// Rewrite industry payload for the premium template
$industryData['parent_industry'] = $subParentName;
$industryData['name'] = $subLabel;
$industryData['tagline'] = 'Client-ready ' . $subLabel . ' storefront · CP · ERP';
$industryData['description'] = $subDesc . ' Categories: ' . $catPreview . '.';
$industryData['hero_photo'] = $subPhoto;
$industryData['gallery_photos'] = $gallery;
$industryData['sample_products'] = $builtProducts;
$industryData['product_label'] = $subLabel . ' Products & Services';
$industryData['about_text'] = $subDesc;
$industryData['about_highlights'] = array(
	'Dedicated storefront for ' . $subLabel . ' (not the generic ' . $subParentName . ' hub)',
	count($subCats) . ' product & service categories: ' . $catPreview,
	'Premium 3D animated hero with vertical-specific photography',
	'Control Panel + ERP demo ready for client onboarding',
	'Related verticals under ' . $subParentName . ' for cross-sell',
);
$industryData['about_photo'] = $subPhoto;
$industryData['stats'] = array(
	array('value' => (string) count($subCats), 'label' => 'Categories'),
	array('value' => (string) count($builtProducts), 'label' => 'Demo SKUs'),
	array('value' => '24h', 'label' => 'Go-live target'),
	array('value' => '1', 'label' => 'Unified BOS stack'),
);
$industryData['cta_cp_text'] = 'Open ' . $subLabel . ' CP';
$industryData['cta_erp_text'] = 'Launch ' . $subLabel . ' ERP';
$industryData['nav_items'] = array('Products', 'Categories', 'Features', 'Related');

// Rebuild display vars used by the template
$name = $subLabel;
$tagline = $industryData['tagline'];
$desc = $industryData['description'];
$heroPhoto = $subPhoto;
$products = $builtProducts;
$galleryPhotos = $gallery;
$stats = $industryData['stats'];
$ctaCp = $industryData['cta_cp_text'];
$ctaErp = $industryData['cta_erp_text'];
$navItems = $industryData['nav_items'];

// Categories section: single focused group (this vertical only)
$subIndustries = array($subLabel);
$industryData['sub_industry_products'] = array(
	$subLabel => array(
		'photo' => $subPhoto,
		'desc' => $subDesc,
		'categories' => $subCats,
		'products' => $rawProds,
	),
);

$seoTitle = $subLabel . ' — Storefront, CP & ERP | ecomae';
$seoDesc = $subLabel . ' on ' . $seoHost . ': ' . $catPreview
	. '. Premium animated storefront with categories, products, Control Panel and ERP — ready for client go-live.';
$seoPageName = $subLabel . ' — ecomae ' . $subParentName;
$seoKeywords = strtolower($subLabel . ', ' . $subParentName . ', ' . $catPreview . ', ERP, storefront, control panel, ecomae');
