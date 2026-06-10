<?php
/**
 * Electronicae homepage — product line grid (Auto Price AI taxonomy + real images).
 */
defined('_ASTEXE_') or die('No access');

require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_electronicae_storefront.php';

global $db_link;
$pdo = ($db_link instanceof PDO) ? $db_link : null;
if (!$pdo instanceof PDO) {
	return;
}

$lang = isset($multilang_params['lang_href']) && $multilang_params['lang_href'] !== '' ? $multilang_params['lang_href'] : '/en';
$siteKey = epc_electronicae_site_key($pdo);
$tiles = epc_electronicae_product_line_tiles($pdo, $siteKey, 12);
if (!$tiles) {
	return;
}

function epc_el_pl_href($lang, $path)
{
	return htmlspecialchars(epc_electronicae_href($path, $lang), ENT_QUOTES, 'UTF-8');
}
?>
<section class="epc-el-product-lines epc-er-section epc-er-reveal" aria-labelledby="epc_el_pl_title">
	<div class="container">
		<div class="epc-er-section__head">
			<div>
				<h2 class="epc-er-section__title" id="epc_el_pl_title">Shop by product line</h2>
				<p class="epc-er-section__lead">Cell phones, laptops, TV, gaming, smart home &amp; more — verified listings with real product photos. All prices in AED.</p>
			</div>
			<a class="epc-er-section__link" href="<?php echo epc_el_pl_href($lang, epc_electronicae_all_lines_href($pdo, $siteKey)); ?>">View all lines</a>
		</div>
		<div class="epc-el-pl-grid">
			<?php foreach ($tiles as $tile) {
				$name = (string) ($tile['name'] ?? '');
				$href = (string) ($tile['href'] ?? '/');
				$image = (string) ($tile['image'] ?? '');
				$icon = (string) ($tile['icon'] ?? 'fa-microchip');
				$accent = (string) ($tile['accent'] ?? '#e10a0a');
				$count = (int) ($tile['product_count'] ?? 0);
				$trend = (string) ($tile['trend'] ?? '');
			?>
			<a class="epc-el-pl-card" href="<?php echo epc_el_pl_href($lang, $href); ?>" style="--epc-el-accent: <?php echo htmlspecialchars($accent, ENT_QUOTES, 'UTF-8'); ?>">
				<span class="epc-el-pl-card__media">
					<?php if ($image !== '') { ?>
					<img src="<?php echo htmlspecialchars($image, ENT_QUOTES, 'UTF-8'); ?>" alt="<?php echo htmlspecialchars($name, ENT_QUOTES, 'UTF-8'); ?>" loading="lazy" decoding="async" width="280" height="280"/>
					<?php } else { ?>
					<span class="epc-el-pl-card__icon" aria-hidden="true"><i class="fa <?php echo htmlspecialchars($icon, ENT_QUOTES, 'UTF-8'); ?>"></i></span>
					<?php } ?>
				</span>
				<span class="epc-el-pl-card__body">
					<span class="epc-el-pl-card__name"><?php echo htmlspecialchars($name, ENT_QUOTES, 'UTF-8'); ?></span>
					<span class="epc-el-pl-card__meta">
						<?php if ($count > 0) { ?>
						<span class="epc-el-pl-card__count"><?php echo (int) $count; ?> <?php echo $count === 1 ? 'product' : 'products'; ?></span>
						<?php } else { ?>
						<span class="epc-el-pl-card__soon">Coming soon</span>
						<?php } ?>
						<?php if ($trend === 'hot') { ?><span class="epc-el-pl-card__hot">Hot</span><?php } ?>
					</span>
				</span>
			</a>
			<?php } ?>
		</div>
	</div>
</section>
