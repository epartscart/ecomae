<?php
/**
 * Fashion/beauty mega-menu panel (replaces legacy Docpart dp_menu on Namshi storefront).
 */
defined('_ASTEXE_') or die('No access');

require_once __DIR__ . '/epc_fashion_retail_namshi_data.php';
require_once __DIR__ . '/epc_portal_industry_catalog.php';

global $multilang_params;
$lang = isset($multilang_params['lang_href']) ? $multilang_params['lang_href'] : '';

$items = epc_portal_industry_catalog_categories('fashion');
foreach (epc_fashion_retail_namshi_category_chips() as $chip) {
	$items[] = array(
		'label' => $chip['label'],
		'href' => $lang . $chip['href'],
		'image' => epc_fashion_retail_namshi_img('beauty_flatlay', 80, 80),
		'alt' => $chip['label'],
	);
}
?>
<ul class="epc-frn-mega-menu__grid">
	<?php foreach ($items as $item) { ?>
	<li>
		<a href="<?php echo htmlspecialchars($item['href'], ENT_QUOTES, 'UTF-8'); ?>">
			<img src="<?php echo htmlspecialchars($item['image'], ENT_QUOTES, 'UTF-8'); ?>" alt="<?php echo htmlspecialchars($item['alt'] ?? $item['label'], ENT_QUOTES, 'UTF-8'); ?>" width="48" height="48" loading="lazy" decoding="async" />
			<span><?php echo htmlspecialchars($item['label'], ENT_QUOTES, 'UTF-8'); ?></span>
		</a>
	</li>
	<?php } ?>
</ul>
