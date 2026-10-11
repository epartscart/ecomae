<?php
// Dump static catalogs for PhpPlanQ1RestJson.cs
define('_ASTEXE_', 1);
$root = rtrim($argv[1] ?? '/workspace', '/');
require $root . '/content/general_pages/epc_storefront_industry_themes.php';
require $root . '/content/general_pages/epc_cp_brochure_topic_photos.php';
require $root . '/content/general_pages/epc_portal_theme_templates.php';
require $root . '/content/general_pages/epc_portal_storefront_packages.php';
$kitKeys = array('healthcare_medical','automotive','food_beverage','jewellery_luxury','construction_realestate','hospitality_travel','electronics_technology','fashion_apparel','beauty_wellness','education_training','energy_utilities','manufacturing_industrial','agriculture_farming');
$alignKeys = array('healthcare_medical','automotive','food_beverage','jewellery_luxury','construction_realestate','hospitality_travel');
$kits = array();
foreach ($kitKeys as $k) { $kits[$k] = epc_erp_industry_kit($k); }
$align = array();
foreach ($alignKeys as $k) { $align[$k] = epc_cp_industry_alignment($k); }
$packages = epc_portal_storefront_package_registry();
foreach ($packages as &$pkg) {
	foreach (array('css', 'home', 'piston', 'hero', 'hero_css', 'header', 'footer', 'preset_file') as $drop) {
		unset($pkg[$drop]);
	}
}
unset($pkg);
echo json_encode(array(
	'themes' => epc_industry_theme_registry(),
	'kits' => $kits,
	'align' => $align,
	'brochure' => epc_cp_brochure_topic_catalog(),
	'palettes' => epc_portal_theme_palette_definitions(),
	'defaults' => epc_portal_default_theme_template_by_industry(),
	'packages' => $packages,
), JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES), "\n";
