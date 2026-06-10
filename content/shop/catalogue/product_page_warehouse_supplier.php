<?php
/**
 * ePartsCart warehouse_supplier — minimal auto-parts product page (brand + article + CTA only).
 */
defined('_ASTEXE_') or die('No access');
?>

<div class="product_page epc-warehouse-product">

<?php
global $db_link, $DP_Config, $product_id, $multilang_params;

require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/price_engine/epc_auto_price_storefront.php';

$pdo = ($db_link instanceof PDO) ? $db_link : null;
$langHref = (isset($multilang_params['lang_href']) && $multilang_params['lang_href'] !== '')
	? rtrim((string) $multilang_params['lang_href'], '/')
	: rtrim((string) (function_exists('epc_apai_storefront_lang_prefix') ? epc_apai_storefront_lang_prefix() : '/en'), '/');

$identity = ($pdo instanceof PDO)
	? epc_apai_product_part_identity($pdo, (int) $product_id)
	: array('brand' => '', 'article' => '', 'caption' => '', 'brand_display' => '', 'article_display' => '');

$brandDisplay = (string) ($identity['brand_display'] ?? '');
$articleDisplay = (string) ($identity['article_display'] ?? '');
$caption = (string) ($identity['caption'] ?? '');
$partsHref = epc_apai_warehouse_parts_search_href(
	$DP_Config,
	(string) ($identity['brand'] ?? ''),
	(string) ($identity['article'] ?? ''),
	$langHref
);
?>

<div class="col-lg-12">
	<div class="epc-warehouse-product__card">
		<span class="epc-warehouse-product__eyebrow"><i class="fa fa-cogs" aria-hidden="true"></i> Auto spare part</span>
		<h1 class="epc-warehouse-product__title">
			<?php if ($brandDisplay !== '') { ?>
			<span class="label label-primary epc-warehouse-product__brand"><?php echo htmlspecialchars($brandDisplay, ENT_QUOTES, 'UTF-8'); ?></span>
			<?php } ?>
			<?php if ($articleDisplay !== '') { ?>
			<strong class="epc-warehouse-product__article"><?php echo htmlspecialchars($articleDisplay, ENT_QUOTES, 'UTF-8'); ?></strong>
			<?php } ?>
		</h1>
		<?php if ($caption !== '') { ?>
		<p class="epc-warehouse-product__caption"><?php echo htmlspecialchars($caption, ENT_QUOTES, 'UTF-8'); ?></p>
		<?php } ?>
		<?php if ($partsHref !== '') { ?>
		<a class="btn btn-primary btn-lg epc-warehouse-product__cta" href="<?php echo htmlspecialchars($partsHref, ENT_QUOTES, 'UTF-8'); ?>">
			<i class="fa fa-search" aria-hidden="true"></i> Search price and availability
		</a>
		<?php } else { ?>
		<p class="text-muted epc-warehouse-product__missing">Could not resolve brand and part number for warehouse search.</p>
		<?php } ?>
	</div>
</div>

<style>
.epc-warehouse-product { padding: 8px 0 32px; }
.epc-warehouse-product__card {
	max-width: 640px;
	margin: 24px auto;
	padding: 32px 28px;
	text-align: center;
	background: #fff;
	border: 1px solid #e5e7eb;
	border-radius: 12px;
	box-shadow: 0 8px 24px rgba(15, 23, 42, 0.06);
}
.epc-warehouse-product__eyebrow {
	display: inline-block;
	margin-bottom: 12px;
	font-size: 13px;
	color: #64748b;
	text-transform: uppercase;
	letter-spacing: 0.06em;
}
.epc-warehouse-product__title {
	margin: 0 0 12px;
	font-size: 28px;
	line-height: 1.3;
}
.epc-warehouse-product__brand {
	display: inline-block;
	margin-right: 8px;
	font-size: 14px;
	vertical-align: middle;
}
.epc-warehouse-product__article {
	font-size: 28px;
	letter-spacing: 0.04em;
}
.epc-warehouse-product__caption {
	margin: 0 0 24px;
	color: #64748b;
	font-size: 15px;
}
.epc-warehouse-product__cta {
	min-width: 280px;
	padding: 14px 28px;
	font-size: 16px;
	font-weight: 600;
	border-radius: 8px;
}
.epc-warehouse-product__missing { margin: 16px 0 0; }
</style>

</div><!-- .product_page -->
