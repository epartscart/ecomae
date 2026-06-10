<?php
/**
 * Home-page industry catalog grid (included from desktop.php).
 */
defined('_ASTEXE_') or die('No access');

require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_portal_industry_catalog.php';

$epc_industry_catalog_profile = epc_portal_industry_catalog_profile();
if ($epc_industry_catalog_profile === null) {
	return;
}

$epc_industry_catalog_title = epc_portal_industry_catalog_section_title($epc_industry_catalog_profile);
$epc_industry_catalog_items = epc_portal_industry_catalog_categories($epc_industry_catalog_profile);
if (count($epc_industry_catalog_items) === 0) {
	return;
}
?>
<div class="col-lg-12 epc-goods-catalog epc-goods-catalog--<?php echo htmlspecialchars($epc_industry_catalog_profile, ENT_QUOTES, 'UTF-8'); ?>">
	<h2 class="section-title"><?php echo htmlspecialchars($epc_industry_catalog_title, ENT_QUOTES, 'UTF-8'); ?></h2>
	<?php epc_portal_industry_catalog_render($epc_industry_catalog_profile); ?>
</div>
