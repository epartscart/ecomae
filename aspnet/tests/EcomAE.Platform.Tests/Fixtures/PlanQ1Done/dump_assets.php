<?php
ini_set('display_errors', 'stderr');
$root = rtrim($argv[1] ?? '/workspace', '/');
$out = rtrim($argv[2] ?? '/tmp/plan_q1d_assets', '/');
@mkdir($out, 0777, true);
if (!defined('_ASTEXE_')) {
	define('_ASTEXE_', 1);
}
require $root . '/content/general_pages/epc_consulting_primeinvest_data.php';
file_put_contents($out . '/cpi.json', json_encode(array(
	'nav' => epc_cpi_nav_links(),
	'contact' => epc_cpi_header_contact(),
	'slides' => epc_cpi_hero_slides(),
	'icons' => epc_cpi_icon_boxes(),
	'credentials' => epc_cpi_credentials(),
	'about_image' => epc_cpi_about_image_url(),
	'services' => epc_cpi_services(),
	'stats' => epc_cpi_stats(),
	'team' => epc_cpi_team(),
	'testimonials' => epc_cpi_testimonials(),
	'partners' => epc_cpi_partners(),
	'steps' => epc_cpi_process_steps(),
	'footer' => epc_cpi_footer_columns(),
	'palette' => epc_cpi_theme_palette(),
	'eyebrow' => epc_cpi_pro_hero_eyebrow(),
	'title' => epc_cpi_pro_hero_title(),
	'copy' => epc_cpi_pro_hero_copy(),
	'actions' => epc_cpi_pro_hero_actions('en'),
	'hero_stats' => epc_cpi_pro_hero_stats(),
	'packages' => epc_cpi_service_packages(),
), JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES));
require $root . '/content/general_pages/epc_ecomae_marketing_content.php';
file_put_contents($out . '/docs.json', json_encode(epc_ecomae_docs_catalog(), JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES));
file_put_contents($out . '/compare.json', json_encode(epc_ecomae_compare_catalog(), JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES));
file_put_contents($out . '/bos.json', json_encode(epc_ecomae_bos_articles_catalog(), JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES));
file_put_contents($out . '/solutions.json', json_encode(epc_ecomae_solutions_catalog(), JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES));
echo "ok\n";
