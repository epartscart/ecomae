<?php
/**
 * Storefront — EParts product deep-link landing.
 * Route: /en/eparts-product
 */
defined('_ASTEXE_') or die('No access');

require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_eparts_product_route.php';

$langHref = (isset($multilang_params['lang_href']) && $multilang_params['lang_href'] !== '')
	? rtrim((string) $multilang_params['lang_href'], '/')
	: '/en';

$article = trim((string) ($_GET['article'] ?? $_GET['oem'] ?? ''));
$brand = trim((string) ($_GET['brand'] ?? $_GET['manufacturer'] ?? ''));
$category = trim((string) ($_GET['category'] ?? ''));

// Prefer catalogue part search when an article is present.
if ($article !== '') {
	$q = array('article' => $article);
	if ($brand !== '') {
		$q['brend'] = $brand;
	}
	$target = $langHref . '/shop/part_search?' . http_build_query($q, '', '&', PHP_QUERY_RFC3986);
	echo '<script>location = ' . json_encode($target) . ';</script>';
	echo '<noscript><meta http-equiv="refresh" content="0;url=' . htmlspecialchars($target, ENT_QUOTES, 'UTF-8') . '"></noscript>';
	echo '<p><a href="' . htmlspecialchars($target, ENT_QUOTES, 'UTF-8') . '">Continue to part search</a></p>';
} else {
	$cataUrl = $langHref . '/eparts-cata';
	if ($category !== '') {
		$cataUrl .= '?category=' . rawurlencode($category);
	}
	$modUrl = $langHref . '/eparts-mod';
	?>
<div class="container" style="padding:28px 16px;max-width:720px;">
	<h1 style="margin:0 0 8px;font-size:28px;">EParts product</h1>
	<p style="color:#64748b;margin:0 0 18px;">Open the vehicle catalog to find OE / aftermarket parts, or browse by category.</p>
	<p style="display:flex;gap:10px;flex-wrap:wrap;">
		<a class="btn btn-primary" href="<?php echo htmlspecialchars($cataUrl, ENT_QUOTES, 'UTF-8'); ?>">EParts CATA</a>
		<a class="btn btn-default" href="<?php echo htmlspecialchars($modUrl, ENT_QUOTES, 'UTF-8'); ?>">Select a vehicle</a>
		<a class="btn btn-default" href="<?php echo htmlspecialchars($langHref . '/shop/part_search', ENT_QUOTES, 'UTF-8'); ?>">Part search</a>
	</p>
</div>
	<?php
}
?>
