<?php
declare(strict_types=1);

require_once __DIR__ . '/epc_sitemap_lib.php';

$cfg = new DP_Config();
$lang = epc_sitemap_lang();
$lastmod = date('Y-m-d');
$entries = array();
$seen = array();

header('Content-Type: application/xml; charset=utf-8');

$pdo = epc_sitemap_pdo($cfg);
if ($pdo === null) {
	epc_sitemap_emit_urlset(array());
	exit;
}

try {
	$stmt = $pdo->query(
		'SELECT `url`, `main_flag`
		FROM `content`
		WHERE `is_frontend` = 1
		AND `published_flag` = 1
		AND (`robots_tag` IS NULL OR `robots_tag` = \'\' OR `robots_tag` NOT LIKE \'%noindex%\')
		AND `url` NOT IN (\'users\', \'shop\', \'cp\')
		AND `url` NOT LIKE \'shop/part_search%\'
		ORDER BY `main_flag` DESC, `url` ASC'
	);
	while ($row = $stmt->fetch(PDO::FETCH_ASSOC)) {
		$url = trim((string) $row['url']);
		if ($url === '') {
			continue;
		}
		$priority = ((int) $row['main_flag'] === 1) ? '1.0' : '0.5';
		$changefreq = ((int) $row['main_flag'] === 1) ? 'daily' : 'monthly';
		epc_sitemap_add_entry(
			$entries,
			$seen,
			epc_sitemap_page_url($cfg, $lang, $url),
			$lastmod,
			$changefreq,
			$priority
		);
	}

	$cat_stmt = $pdo->query(
		'SELECT `url`, `published_flag`
		FROM `shop_catalogue_categories`
		WHERE `published_flag` = 1
		AND TRIM(IFNULL(`url`, \'\')) != \'\'
		ORDER BY `level` ASC, `order` ASC
		LIMIT 5000'
	);
	while ($cat = $cat_stmt->fetch(PDO::FETCH_ASSOC)) {
		epc_sitemap_add_entry(
			$entries,
			$seen,
			epc_sitemap_page_url($cfg, $lang, $cat['url']),
			$lastmod,
			'weekly',
			'0.6'
		);
	}

	$prod_stmt = $pdo->query(
		'SELECT c.`url` AS category_url, p.`alias`
		FROM `shop_catalogue_products` AS p
		INNER JOIN `shop_catalogue_categories` AS c ON c.`id` = p.`category_id`
		WHERE p.`published_flag` = 1
		AND c.`published_flag` = 1
		AND TRIM(IFNULL(p.`alias`, \'\')) != \'\'
		ORDER BY c.`url` ASC, p.`alias` ASC
		LIMIT 5000'
	);
	while ($prod = $prod_stmt->fetch(PDO::FETCH_ASSOC)) {
		$path = trim((string) $prod['category_url'], '/') . '/' . trim((string) $prod['alias'], '/');
		epc_sitemap_add_entry(
			$entries,
			$seen,
			epc_sitemap_page_url($cfg, $lang, $path),
			$lastmod,
			'weekly',
			'0.5'
		);
	}
} catch (Exception $e) {
}

epc_sitemap_emit_urlset($entries);
