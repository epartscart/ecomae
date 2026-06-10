<?php
/**
 * Tenant/industry catalog grid for the home-page "Catalog of goods" block.
 * Legacy Docpart shop_catalogue_categories remain for auto_parts / automotive_spareparts_pro.
 */
defined('_ASTEXE_') or die('No access');

/**
 * @return string|null Profile key: electronics, consulting, fashion, jewellery; null = legacy DB catalog.
 */
function epc_portal_industry_catalog_profile()
{
	if (!function_exists('epc_portal_site_profile')) {
		return null;
	}
	$site = epc_portal_site_profile();
	$industry = isset($site['industry']) ? (string) $site['industry'] : 'auto_parts';
	$package = function_exists('epc_portal_active_storefront_package')
		? (string) epc_portal_active_storefront_package()
		: '';

	if ($package === 'automotive_spareparts_pro') {
		return null;
	}
	if ($industry === 'auto_parts' && $package === '') {
		return null;
	}
	if ($package === 'electronics_retail_virgin' || $industry === 'electronics') {
		return 'electronics';
	}
	if ($package === 'consulting_primeinvest' || in_array($industry, array('tax_advisory', 'consultancy'), true)) {
		return 'consulting';
	}
	if ($industry === 'fashion') {
		return 'fashion';
	}
	if ($industry === 'jewellery') {
		return 'jewellery';
	}
	return null;
}

function epc_portal_industry_catalog_section_title($profile)
{
	switch ($profile) {
		case 'electronics':
			return 'Shop by category';
		case 'consulting':
			return 'Our services';
		case 'fashion':
			return 'Shop by collection';
		case 'jewellery':
			return 'Our collections';
		default:
			return function_exists('translate_str_by_id') ? translate_str_by_id(3994) : 'Catalog of goods';
	}
}

function epc_portal_industry_catalog_unsplash($photoId, $w = 400, $h = 400)
{
	return 'https://images.unsplash.com/' . $photoId . '?auto=format&fit=crop&w=' . (int) $w . '&h=' . (int) $h . '&q=80';
}

/**
 * @return array<int, array{label: string, href: string, image: string, alt: string}>
 */
function epc_portal_industry_catalog_categories($profile)
{
	global $multilang_params;
	$lang = isset($multilang_params['lang_href']) ? $multilang_params['lang_href'] : '';

	switch ($profile) {
		case 'electronics':
			if (is_file(__DIR__ . '/epc_electronics_retail_data.php')) {
				require_once __DIR__ . '/epc_electronics_retail_data.php';
			}
			$items = array(
				array('label' => 'Phones', 'href' => '/shop/search?q=phones', 'key' => 'smartphone'),
				array('label' => 'Laptops', 'href' => '/shop/search?q=laptops', 'key' => 'laptop'),
				array('label' => 'Gaming', 'href' => '/shop/search?q=gaming', 'key' => 'gaming_setup'),
				array('label' => 'Audio', 'href' => '/shop/search?q=audio', 'key' => 'headphones'),
				array('label' => 'TVs', 'href' => '/shop/search?q=tv', 'key' => 'tv_living'),
				array('label' => 'Accessories', 'href' => '/shop/search?q=accessories', 'key' => 'mouse'),
				array('label' => 'Smart Home', 'href' => '/shop/search?q=smart+home', 'key' => 'smart_home'),
				array('label' => 'Tablets', 'href' => '/shop/search?q=tablet', 'key' => 'tablet_lifestyle'),
			);
			$out = array();
			foreach ($items as $item) {
				$key = $item['key'];
				$image = function_exists('epc_electronics_retail_img')
					? epc_electronics_retail_img($key, 400, 400)
					: epc_portal_industry_catalog_unsplash('photo-1592899677977-9c10ca588bbd');
				$alt = function_exists('epc_electronics_retail_img_alt')
					? epc_electronics_retail_img_alt($key)
					: $item['label'];
				$out[] = array(
					'label' => $item['label'],
					'href' => $lang . $item['href'],
					'image' => $image,
					'alt' => $alt,
				);
			}
			return $out;

		case 'consulting':
			return array(
				array(
					'label' => 'Corporate Tax',
					'href' => $lang . '/#epc-cpi-services',
					'image' => epc_portal_industry_catalog_unsplash('photo-1450101499163-c8848c66ca85'),
					'alt' => 'Corporate tax advisory',
				),
				array(
					'label' => 'VAT',
					'href' => $lang . '/#epc-cpi-services',
					'image' => epc_portal_industry_catalog_unsplash('photo-1554224155-6726b3ff858f'),
					'alt' => 'VAT compliance and filing',
				),
				array(
					'label' => 'Accounting',
					'href' => $lang . '/#epc-cpi-services',
					'image' => epc_portal_industry_catalog_unsplash('photo-1551836022-d5d88e9c9639'),
					'alt' => 'Accounting and bookkeeping',
				),
				array(
					'label' => 'Audit',
					'href' => $lang . '/#epc-cpi-services',
					'image' => epc_portal_industry_catalog_unsplash('photo-1507679799987-c73779587ccf'),
					'alt' => 'Audit-ready reporting',
				),
				array(
					'label' => 'ERP',
					'href' => $lang . '/erp',
					'image' => epc_portal_industry_catalog_unsplash('photo-1460925895917-afdab827c52f'),
					'alt' => 'Client ERP portal',
				),
				array(
					'label' => 'Advisory',
					'href' => $lang . '/kontakty',
					'image' => epc_portal_industry_catalog_unsplash('photo-1454165804606-220107c9a589'),
					'alt' => 'Business advisory',
				),
			);

		case 'fashion':
			return array(
				array('label' => 'Women', 'href' => $lang . '/shop/search?q=women', 'image' => epc_portal_industry_catalog_unsplash('photo-1490481651871-ab68de25d43d'), 'alt' => 'Women\'s fashion'),
				array('label' => 'Men', 'href' => $lang . '/shop/search?q=men', 'image' => epc_portal_industry_catalog_unsplash('photo-1617137968427-85924c800a22'), 'alt' => 'Men\'s fashion'),
				array('label' => 'Shoes', 'href' => $lang . '/shop/search?q=shoes', 'image' => epc_portal_industry_catalog_unsplash('photo-1543163521-1bf539c55dd1'), 'alt' => 'Footwear'),
				array('label' => 'Bags', 'href' => $lang . '/shop/search?q=bags', 'image' => epc_portal_industry_catalog_unsplash('photo-1584917865442-de89a762c4a6'), 'alt' => 'Bags and leather goods'),
				array('label' => 'Accessories', 'href' => $lang . '/shop/search?q=accessories', 'image' => epc_portal_industry_catalog_unsplash('photo-1523381210434-271fa8a0a992'), 'alt' => 'Fashion accessories'),
				array('label' => 'New arrivals', 'href' => $lang . '/shop/search?q=new', 'image' => epc_portal_industry_catalog_unsplash('photo-1483985988357-763728e1935b'), 'alt' => 'New arrivals'),
			);

		case 'jewellery':
			return array(
				array('label' => 'Rings', 'href' => $lang . '/shop/search?q=rings', 'image' => epc_portal_industry_catalog_unsplash('photo-1605100804763-247f67b3557e'), 'alt' => 'Rings'),
				array('label' => 'Necklaces', 'href' => $lang . '/shop/search?q=necklaces', 'image' => epc_portal_industry_catalog_unsplash('photo-1599643478518-a784e69ba83f'), 'alt' => 'Necklaces'),
				array('label' => 'Earrings', 'href' => $lang . '/shop/search?q=earrings', 'image' => epc_portal_industry_catalog_unsplash('photo-1535632066927-ab7c9a509e3f'), 'alt' => 'Earrings'),
				array('label' => 'Bracelets', 'href' => $lang . '/shop/search?q=bracelets', 'image' => epc_portal_industry_catalog_unsplash('photo-1611591437281-460bfac7a2c3'), 'alt' => 'Bracelets'),
				array('label' => 'Wedding', 'href' => $lang . '/shop/search?q=wedding', 'image' => epc_portal_industry_catalog_unsplash('photo-1515562141207-7a88fb7ce338'), 'alt' => 'Wedding jewellery'),
				array('label' => 'Gifts', 'href' => $lang . '/shop/search?q=gifts', 'image' => epc_portal_industry_catalog_unsplash('photo-1602173574767-37ac01994b2a'), 'alt' => 'Gift jewellery'),
			);

		default:
			return array();
	}
}

function epc_portal_industry_catalog_render($profile)
{
	$categories = epc_portal_industry_catalog_categories($profile);
	if (count($categories) === 0) {
		return;
	}
	?>
	<div class="row" style="padding: 0px 4px; margin-top:-9px;">
	<?php
	foreach ($categories as $cat) {
		$href = htmlspecialchars($cat['href'], ENT_QUOTES, 'UTF-8');
		$img = htmlspecialchars($cat['image'], ENT_QUOTES, 'UTF-8');
		$alt = htmlspecialchars($cat['alt'], ENT_QUOTES, 'UTF-8');
		$label = htmlspecialchars($cat['label'], ENT_QUOTES, 'UTF-8');
		?>
		<div class="col-xs-6 col-sm-4 col-md-4 col-lg-3 new-cat-block">
			<a href="<?php echo $href; ?>" class="ucats-h-1 new-cat-block-catalog">
				<div class="new-cat-block-catalog-img" style="background:url('<?php echo $img; ?>') no-repeat; background-position: center; background-size: contain;" role="img" aria-label="<?php echo $alt; ?>"></div>
				<div class="new-cat-block-text navbar-inverse"><?php echo $label; ?></div>
			</a>
		</div>
		<?php
	}
	?>
	</div>
	<?php
}
