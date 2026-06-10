<?php
/**
 * ePartsCart homepage — Auto Price AI product line grid (synced taxonomy categories).
 */
defined('_ASTEXE_') or die('No access');

require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_electronicae_storefront.php';

global $db_link;
$pdo = ($db_link instanceof PDO) ? $db_link : null;
if (!$pdo instanceof PDO) {
	return;
}

require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_perf_cache.php';
$siteKey = 'epartscart';
$cacheKey = 'epc_pl_tiles:v1:' . $siteKey . ':8';
$cachedTiles = epc_perf_cache_get($cacheKey);
if (!empty($GLOBALS['epc_home_perf_fast']) && $cachedTiles === null) {
	return;
}

$lang = isset($multilang_params['lang_href']) && $multilang_params['lang_href'] !== '' ? $multilang_params['lang_href'] : '/en';
$tiles = is_array($cachedTiles) ? $cachedTiles : epc_electronicae_product_line_tiles($pdo, $siteKey, 8);
if (!$tiles) {
	return;
}

function epc_ep_pl_href($lang, $path)
{
	return htmlspecialchars(epc_electronicae_href($path, $lang), ENT_QUOTES, 'UTF-8');
}
?>
<section class="epc-ep-product-lines epc-asp-home-section" aria-labelledby="epc_ep_pl_title">
	<div class="container">
		<div class="epc-asp-home-section__head">
			<h2 class="epc-asp-home-section__title" id="epc_ep_pl_title">Shop by product line</h2>
			<p class="epc-asp-home-section__lead">Engine, brakes, body, interior — verified UAE listings imported via Auto Price AI.</p>
			<a class="epc-asp-home-section__link" href="<?php echo epc_ep_pl_href($lang, epc_electronicae_all_lines_href($pdo, $siteKey)); ?>">View all lines</a>
		</div>
		<div class="epc-ep-pl-grid">
			<?php foreach ($tiles as $tile) {
				$name = (string) ($tile['name'] ?? '');
				$href = (string) ($tile['href'] ?? '/');
				$image = (string) ($tile['image'] ?? '');
				$icon = (string) ($tile['icon'] ?? 'fa-cogs');
				$count = (int) ($tile['product_count'] ?? 0);
			?>
			<a class="epc-ep-pl-card" href="<?php echo epc_ep_pl_href($lang, $href); ?>">
				<span class="epc-ep-pl-card__media">
					<?php if ($image !== '') { ?>
					<img src="<?php echo htmlspecialchars($image, ENT_QUOTES, 'UTF-8'); ?>" alt="<?php echo htmlspecialchars($name, ENT_QUOTES, 'UTF-8'); ?>" loading="lazy" decoding="async" width="220" height="220"/>
					<?php } else { ?>
					<span class="epc-ep-pl-card__icon" aria-hidden="true"><i class="fa <?php echo htmlspecialchars($icon, ENT_QUOTES, 'UTF-8'); ?>"></i></span>
					<?php } ?>
				</span>
				<span class="epc-ep-pl-card__body">
					<span class="epc-ep-pl-card__name"><?php echo htmlspecialchars($name, ENT_QUOTES, 'UTF-8'); ?></span>
					<span class="epc-ep-pl-card__meta"><?php echo $count > 0 ? ((int) $count . ' products') : 'New arrivals soon'; ?></span>
				</span>
			</a>
			<?php } ?>
		</div>
	</div>
</section>
<style>
.epc-ep-pl-grid{display:grid;grid-template-columns:repeat(auto-fill,minmax(160px,1fr));gap:14px;margin-top:16px}
.epc-ep-pl-card{display:flex;flex-direction:column;background:#fff;border:1px solid #e5eaf2;border-radius:14px;overflow:hidden;text-decoration:none;color:inherit;box-shadow:0 10px 28px rgba(15,23,42,.08)}
.epc-ep-pl-card__media{display:flex;align-items:center;justify-content:center;min-height:120px;background:#f8fafc}
.epc-ep-pl-card__media img{max-width:100%;max-height:120px;object-fit:contain}
.epc-ep-pl-card__icon{font-size:32px;color:#dc2626}
.epc-ep-pl-card__body{padding:12px 14px}
.epc-ep-pl-card__name{display:block;font-weight:800;font-size:14px}
.epc-ep-pl-card__meta{display:block;font-size:12px;color:#64748b;margin-top:4px}
.epc-asp-home-section{margin:28px 0}
.epc-asp-home-section__head{display:flex;flex-wrap:wrap;align-items:flex-end;justify-content:space-between;gap:10px}
.epc-asp-home-section__title{margin:0;font-size:22px;font-weight:900}
.epc-asp-home-section__lead{margin:6px 0 0;color:#64748b;flex:1 1 100%}
.epc-asp-home-section__link{font-weight:700;color:#dc2626}
</style>
