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

	// Accessories marketplace — published listings (+ category hubs when present).
	$accDb = __DIR__ . '/content/shop/docpart/epc_accessories_db.php';
	if (is_file($accDb)) {
		require_once $accDb;
		if (function_exists('epc_acc_ensure_schema')) {
			epc_acc_ensure_schema($pdo);
		}
		$langHref = '/' . $lang;
		$base = rtrim(epc_sitemap_base_url($cfg), '/');
		try {
			$catRows = $pdo->query(
				"SELECT `slug` FROM `epc_acc_categories`
				 WHERE `active` = 1 AND `parent_id` = 0 AND TRIM(IFNULL(`slug`, '')) != ''
				 ORDER BY `sort_order` ASC, `id` ASC
				 LIMIT 200"
			);
			if ($catRows) {
				while ($cat = $catRows->fetch(PDO::FETCH_ASSOC)) {
					$slug = trim((string) ($cat['slug'] ?? ''));
					if ($slug === '') {
						continue;
					}
					$loc = htmlspecialchars(
						$base . $langHref . '/accessories-spare-parts?' . http_build_query(array('category' => $slug)),
						ENT_XML1,
						'UTF-8'
					);
					epc_sitemap_add_entry($entries, $seen, $loc, $lastmod, 'weekly', '0.55');
				}
			}
			$listRows = $pdo->query(
				"SELECT l.`id`, l.`updated_at`, l.`created_at`,
					c.`slug` AS category_slug, s.`slug` AS subcategory_slug
				 FROM `epc_acc_listings` l
				 LEFT JOIN `epc_acc_categories` c ON c.id = l.category_id
				 LEFT JOIN `epc_acc_categories` s ON s.id = l.subcategory_id
				 WHERE l.`status` = 'published'
				 ORDER BY l.`updated_at` DESC, l.`id` DESC
				 LIMIT 5000"
			);
			if ($listRows) {
				while ($row = $listRows->fetch(PDO::FETCH_ASSOC)) {
					$rel = function_exists('epc_acc_storefront_url')
						? epc_acc_storefront_url($row, $langHref)
						: ($langHref . '/accessories-spare-parts?id=' . (int) $row['id']);
					$loc = htmlspecialchars($base . $rel, ENT_XML1, 'UTF-8');
					$ts = (int) ($row['updated_at'] ?? 0);
					if ($ts <= 0) {
						$ts = (int) ($row['created_at'] ?? 0);
					}
					$mod = $ts > 0 ? date('Y-m-d', $ts) : $lastmod;
					epc_sitemap_add_entry($entries, $seen, $loc, $mod, 'weekly', '0.6');
				}
			}
		} catch (Exception $accEx) {
			// Table may be absent on non-warehouse tenants.
		}
	}
} catch (Exception $e) {
}

epc_sitemap_emit_urlset($entries);
