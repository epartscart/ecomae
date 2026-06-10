<?php
/**
 * One-time SEO indexing setup: noindex on legacy part search and submit sitemap hint.
 * Run once after deploy: https://www.epartscart.com/epc-seo-indexing-setup.php
 */
header('Content-Type: text/plain; charset=utf-8');

require_once __DIR__ . '/config.php';

$cfg = new DP_Config();
$steps = array();

try {
	$pdo = new PDO(
		'mysql:host=' . $cfg->host . ';dbname=' . $cfg->db,
		$cfg->user,
		$cfg->password,
		array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION)
	);
	$pdo->query('SET NAMES utf8');

	$stmt = $pdo->prepare(
		'UPDATE `content`
		SET `robots_tag` = ?
		WHERE `url` = ?
		AND `is_frontend` = 1
		AND (`robots_tag` IS NULL OR `robots_tag` = \'\')'
	);
	$stmt->execute(array('noindex, follow', 'shop/part_search'));
	$steps[] = 'shop/part_search robots_tag: updated ' . $stmt->rowCount() . ' row(s)';

	require_once __DIR__ . '/content/general_pages/epc_seo_indexing.php';
	$priceClause = epc_seo_sitemap_price_clause($pdo);
	$count_stmt = $pdo->query(
		'SELECT COUNT(DISTINCT TRIM(`manufacturer`), COALESCE(NULLIF(TRIM(`article_show`), \'\'), TRIM(`article`))) AS c
		FROM `shop_docpart_prices_data`
		WHERE IFNULL(`exist`, 0) > 0' . $priceClause
	);
	$in_stock = (int) $count_stmt->fetchColumn();
	$steps[] = 'In-stock brand+article URLs eligible for sitemap: ' . $in_stock;
} catch (Exception $e) {
	echo "ERROR: " . $e->getMessage() . "\n";
	exit(1);
}

echo "Eparts SEO indexing setup OK\n\n";
echo implode("\n", $steps) . "\n\n";
echo "Next steps in Google Search Console:\n";
echo "1. Submit sitemap: https://www.epartscart.com/sitemap-index.php\n";
echo "2. Open Page indexing report and click Validate fix on affected issues after 1-2 weeks.\n";
