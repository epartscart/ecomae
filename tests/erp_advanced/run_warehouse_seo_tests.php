<?php
/**
 * epartscart warehouse stock SEO — part number / article / cross indexing.
 *
 *   php tests/erp_advanced/run_warehouse_seo_tests.php
 */

declare(strict_types=1);

if (PHP_SAPI !== 'cli') {
	http_response_code(403);
	exit('CLI only');
}

define('_ASTEXE_', 1);
$root = dirname(__DIR__, 2);
$_SERVER['DOCUMENT_ROOT'] = $root;

require_once $root . '/content/general_pages/epc_seo_indexing.php';
require_once $root . '/content/shop/docpart/docpart_article_match.php';

$pass = 0;
$fail = 0;
function check(string $label, bool $cond): void
{
	global $pass, $fail;
	if ($cond) {
		$pass++;
		echo "  PASS  $label\n";
	} else {
		$fail++;
		echo "  FAIL  $label\n";
	}
}

echo "== Titles expose part numbers ==\n";
$t = epc_seo_format_part_title('BOSCH', 'OC47', 'eParts Cart');
check('title has Part number', stripos($t, 'Part number OC47') !== false);
check('title has brand+article', strpos($t, 'BOSCH OC47') === 0);

$d = epc_seo_format_part_description(array(
	'manufacturer' => 'BOSCH',
	'article' => 'OC47',
	'article_show' => 'OC47',
	'name' => 'Oil filter',
	'exist' => 3,
));
check('description has Part number / article', stripos($d, 'Part number / article: OC47') !== false);
check('description has Brand', stripos($d, 'Brand: BOSCH') !== false);

echo "\n== Product schema ==\n";
$cfg = new stdClass();
$cfg->domain_path = 'https://www.epartscart.com/';
$cfg->chpu_search_config = array(
	'slash_code' => '---',
	'level_1' => array('url' => 'parts'),
);
$row = array(
	'manufacturer' => 'BOSCH',
	'article' => 'OC-47',
	'article_show' => 'OC-47',
	'name' => 'Oil filter',
	'exist' => 2,
	'price' => 15.5,
);
$cross = array(
	array('brand' => 'MANN', 'article' => 'W71275'),
);
$schema = epc_seo_build_product_schema_array($row, $cfg, '/en', true, 'AED', $cross);
check('schema type is Product only', ($schema['@type'] ?? '') === 'Product');
check('sku normalized', ($schema['sku'] ?? '') === 'OC47');
check('mpn normalized', ($schema['mpn'] ?? '') === 'OC47');
check('url uses normalized article', strpos((string) ($schema['url'] ?? ''), '/parts/BOSCH/OC47') !== false);
check('additionalProperty has Part number', !empty($schema['additionalProperty']));
$propNames = array();
foreach ($schema['additionalProperty'] as $p) {
	$propNames[] = (string) ($p['name'] ?? '');
}
check('has Part number property', in_array('Part number', $propNames, true));
check('has Cross reference property', in_array('Cross reference / OE', $propNames, true));
check('isRelatedTo present', !empty($schema['isRelatedTo']));

echo "\n== Sitemap sharding ==\n";
$prodSrc = (string) file_get_contents($root . '/sitemap-products.php');
$idxSrc = (string) file_get_contents($root . '/sitemap-index.php');
$robots = (string) file_get_contents($root . '/robots.txt');
check('products sitemap supports shard', strpos($prodSrc, "\$_GET['shard']") !== false || strpos($prodSrc, '$_GET["shard"]') !== false || strpos($prodSrc, "['shard']") !== false);
check('products use GROUP BY manufacturer/article', strpos($prodSrc, 'GROUP BY TRIM(d.`manufacturer`)') !== false);
check('products normalize article for URL', strpos($prodSrc, 'docpart_normalize_article_for_price') !== false);
check('index emits product shards', strpos($idxSrc, 'sitemap-products.php?shard=') !== false);
check('robots lists sitemap-products', stripos($robots, 'Sitemap: /sitemap-products.php') !== false);
check('robots lists sitemap-index', stripos($robots, 'Sitemap: /sitemap-index.php') !== false);

echo "\n== Cross refs crawlable in part page ==\n";
$partSrc = (string) file_get_contents($root . '/content/shop/docpart/part_search_page.php');
check('server-rendered cross nav', strpos($partSrc, 'epc-seo-cross-refs') !== false);
check('cross nav mentions OE numbers', stripos($partSrc, 'OE numbers') !== false);
check('schema receives cross rows', strpos($partSrc, 'epc_cross_fallback_rows') !== false);

echo "\n----------------------------\n";
echo "Passed: $pass  Failed: $fail\n";
exit($fail > 0 ? 1 : 0);
