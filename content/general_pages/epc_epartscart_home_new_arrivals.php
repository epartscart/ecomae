<?php
/**
 * ePartsCart homepage — recently imported Auto Price AI catalogue products.
 */
defined('_ASTEXE_') or die('No access');

require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_electronicae_storefront.php';

global $db_link, $DP_Config;
$pdo = ($db_link instanceof PDO) ? $db_link : null;
if (!$pdo instanceof PDO) {
	return;
}

$lang = isset($multilang_params['lang_href']) && $multilang_params['lang_href'] !== '' ? $multilang_params['lang_href'] : '/en';
$siteKey = 'epartscart';
$productUrlMode = is_object($DP_Config) ? (string) ($DP_Config->product_url ?? 'alias') : 'alias';

$stmt = $pdo->prepare(
	'SELECT scp.`id`, scp.`alias`, scp.`caption`, scc.`url` AS `category_url`, q.`title`,
	        (SELECT MIN(sd.`price`) FROM `shop_storages_data` sd WHERE sd.`product_id` = scp.`id` AND sd.`price` > 0) AS `price`,
	        (SELECT spi.`file_name` FROM `shop_products_images` spi WHERE spi.`product_id` = scp.`id` ORDER BY spi.`id` ASC LIMIT 1) AS `file_name`
	 FROM `epc_product_discovery_queue` q
	 INNER JOIN `shop_catalogue_products` scp ON scp.`id` = q.`product_id` AND scp.`published_flag` = 1
	 LEFT JOIN `shop_catalogue_categories` scc ON scc.`id` = scp.`category_id`
	 WHERE q.`site_key` = ? AND q.`status` = \'imported\'
	 ORDER BY q.`updated_at` DESC
	 LIMIT 8'
);
$stmt->execute(array($siteKey));
$products = array();
while ($row = $stmt->fetch(PDO::FETCH_ASSOC)) {
	$card = epc_electronicae_product_card($pdo, $row, $productUrlMode);
	if ($card['href'] === '' || $card['name'] === '') {
		continue;
	}
	$parsed = function_exists('epc_apai_parse_product_chpu')
		? epc_apai_parse_product_chpu((string) ($row['alias'] ?? ''))
		: array('brand' => '', 'article' => '');
	if (!empty($parsed['brand'])) {
		$card['brand'] = strtoupper((string) $parsed['brand']);
	}
	if (!empty($parsed['article'])) {
		$card['sku'] = (string) $parsed['article'];
	}
	$products[] = $card;
}
if (!$products) {
	return;
}
?>
<section class="epc-ep-new-arrivals epc-asp-home-section" aria-labelledby="epc_ep_na_title">
	<div class="container">
		<h2 class="epc-asp-home-section__title" id="epc_ep_na_title">New arrivals</h2>
		<p class="epc-asp-home-section__lead">Latest spare parts added to our catalogue — brand / part number URLs, market prices where available.</p>
		<div class="epc-ep-na-grid">
			<?php foreach ($products as $p) {
				$href = epc_electronicae_href((string) ($p['href'] ?? '/'), $lang);
				$label = trim((string) ($p['brand'] ?? '') . ($p['sku'] !== '' ? ' · ' . $p['sku'] : ''));
			?>
			<a class="epc-ep-na-card" href="<?php echo htmlspecialchars($href, ENT_QUOTES, 'UTF-8'); ?>">
				<?php if (!empty($p['image'])) { ?>
				<img src="<?php echo htmlspecialchars((string) $p['image'], ENT_QUOTES, 'UTF-8'); ?>" alt="" loading="lazy" decoding="async"/>
				<?php } ?>
				<span class="epc-ep-na-card__body">
					<?php if ($label !== '') { ?><strong class="epc-ep-na-card__id"><?php echo htmlspecialchars($label, ENT_QUOTES, 'UTF-8'); ?></strong><?php } ?>
					<span class="epc-ep-na-card__title"><?php echo htmlspecialchars((string) ($p['name'] ?? ''), ENT_QUOTES, 'UTF-8'); ?></span>
					<?php if ((float) ($p['price'] ?? 0) > 0) { ?>
					<span class="epc-ep-na-card__price"><?php echo number_format((float) $p['price'], 2); ?> AED</span>
					<?php } ?>
				</span>
			</a>
			<?php } ?>
		</div>
	</div>
</section>
<style>
.epc-ep-na-grid{display:grid;grid-template-columns:repeat(auto-fill,minmax(200px,1fr));gap:14px;margin-top:16px}
.epc-ep-na-card{display:flex;flex-direction:column;background:#fff;border:1px solid #e5eaf2;border-radius:14px;overflow:hidden;text-decoration:none;color:inherit}
.epc-ep-na-card img{width:100%;height:140px;object-fit:contain;background:#f8fafc}
.epc-ep-na-card__body{padding:12px 14px}
.epc-ep-na-card__id{display:block;font-size:13px;color:#dc2626;margin-bottom:4px}
.epc-ep-na-card__title{display:block;font-size:13px;color:#334155;line-height:1.35}
.epc-ep-na-card__price{display:block;font-weight:800;margin-top:6px}
</style>
