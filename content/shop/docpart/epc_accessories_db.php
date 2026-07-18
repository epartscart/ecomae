<?php
/**
 * Accessories marketplace DB — PakWheels-style categories + listings filled over time.
 */
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_accessories_taxonomy.php';

if (!function_exists('epc_acc_taxonomy_json_path')) {
	function epc_acc_taxonomy_json_path(): string
	{
		return $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_pakwheels_accessories_taxonomy.json';
	}
}

if (!function_exists('epc_acc_load_taxonomy_json')) {
	function epc_acc_load_taxonomy_json(): array
	{
		$path = epc_acc_taxonomy_json_path();
		if (!is_readable($path)) {
			return array('categories' => array(), 'makes' => array(), 'cities' => array(), 'filters' => array());
		}
		$data = json_decode((string) file_get_contents($path), true);
		return is_array($data) ? $data : array('categories' => array(), 'makes' => array(), 'cities' => array(), 'filters' => array());
	}
}

if (!function_exists('epc_acc_ensure_schema')) {
	function epc_acc_ensure_schema(PDO $db): void
	{
		$db->exec(
			"CREATE TABLE IF NOT EXISTS `epc_acc_categories` (
				`id` INT UNSIGNED NOT NULL AUTO_INCREMENT,
				`parent_id` INT UNSIGNED NOT NULL DEFAULT 0,
				`slug` VARCHAR(120) NOT NULL,
				`label` VARCHAR(190) NOT NULL,
				`pw_id` INT UNSIGNED NOT NULL DEFAULT 0,
				`sort_order` INT NOT NULL DEFAULT 0,
				`active` TINYINT(1) NOT NULL DEFAULT 1,
				PRIMARY KEY (`id`),
				UNIQUE KEY `slug_parent` (`slug`, `parent_id`),
				KEY `parent_id` (`parent_id`),
				KEY `active` (`active`)
			) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;"
		);
		$db->exec(
			"CREATE TABLE IF NOT EXISTS `epc_acc_listings` (
				`id` INT UNSIGNED NOT NULL AUTO_INCREMENT,
				`category_id` INT UNSIGNED NOT NULL DEFAULT 0,
				`subcategory_id` INT UNSIGNED NOT NULL DEFAULT 0,
				`title` VARCHAR(255) NOT NULL,
				`description` TEXT NULL,
				`make` VARCHAR(120) NOT NULL DEFAULT '',
				`model` VARCHAR(120) NOT NULL DEFAULT '',
				`city` VARCHAR(120) NOT NULL DEFAULT '',
				`condition_type` VARCHAR(32) NOT NULL DEFAULT 'new',
				`price` DECIMAL(12,2) NOT NULL DEFAULT 0,
				`currency` VARCHAR(8) NOT NULL DEFAULT 'PKR',
				`image_url` VARCHAR(500) NOT NULL DEFAULT '',
				`external_url` VARCHAR(500) NOT NULL DEFAULT '',
				`stock_qty` INT NOT NULL DEFAULT 0,
				`status` VARCHAR(32) NOT NULL DEFAULT 'published',
				`created_at` INT UNSIGNED NOT NULL DEFAULT 0,
				`updated_at` INT UNSIGNED NOT NULL DEFAULT 0,
				PRIMARY KEY (`id`),
				KEY `category_id` (`category_id`),
				KEY `subcategory_id` (`subcategory_id`),
				KEY `make` (`make`),
				KEY `city` (`city`),
				KEY `status_price` (`status`, `price`),
				KEY `updated_at` (`updated_at`)
			) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;"
		);
	}
}

if (!function_exists('epc_acc_seed_categories_from_json')) {
	/**
	 * @return array{parents:int, children:int}
	 */
	function epc_acc_seed_categories_from_json(PDO $db, bool $reset = false): array
	{
		epc_acc_ensure_schema($db);
		if ($reset) {
			$db->exec('DELETE FROM `epc_acc_listings`');
			$db->exec('DELETE FROM `epc_acc_categories`');
		}
		$tax = epc_acc_load_taxonomy_json();
		$parents = isset($tax['categories']) && is_array($tax['categories']) ? $tax['categories'] : array();
		$upsert = $db->prepare(
			'INSERT INTO `epc_acc_categories` (`parent_id`, `slug`, `label`, `pw_id`, `sort_order`, `active`)
			VALUES (?, ?, ?, ?, ?, 1)
			ON DUPLICATE KEY UPDATE `label` = VALUES(`label`), `pw_id` = VALUES(`pw_id`), `sort_order` = VALUES(`sort_order`), `active` = 1'
		);
		$parentCount = 0;
		$childCount = 0;
		$order = 0;
		foreach ($parents as $parent) {
			$order++;
			$pslug = trim((string) ($parent['slug'] ?? ''));
			$plabel = trim((string) ($parent['label'] ?? $pslug));
			if ($pslug === '') {
				continue;
			}
			$upsert->execute(array(0, $pslug, $plabel, (int) ($parent['pw_id'] ?? 0), $order));
			$parentCount++;
			$pidStmt = $db->prepare('SELECT `id` FROM `epc_acc_categories` WHERE `parent_id` = 0 AND `slug` = ? LIMIT 1');
			$pidStmt->execute(array($pslug));
			$parentId = (int) $pidStmt->fetchColumn();
			$children = isset($parent['children']) && is_array($parent['children']) ? $parent['children'] : array();
			$cOrder = 0;
			foreach ($children as $child) {
				$cOrder++;
				$cslug = trim((string) ($child['slug'] ?? ''));
				$clabel = trim((string) ($child['label'] ?? $cslug));
				if ($cslug === '' || $parentId < 1) {
					continue;
				}
				$upsert->execute(array($parentId, $cslug, $clabel, (int) ($child['pw_id'] ?? 0), $cOrder));
				$childCount++;
			}
		}
		return array('parents' => $parentCount, 'children' => $childCount);
	}
}

if (!function_exists('epc_acc_get_category_tree')) {
	function epc_acc_get_category_tree(PDO $db): array
	{
		epc_acc_ensure_schema($db);
		$rows = $db->query(
			'SELECT `id`, `parent_id`, `slug`, `label`, `pw_id`, `sort_order`
			FROM `epc_acc_categories` WHERE `active` = 1 ORDER BY `parent_id` ASC, `sort_order` ASC, `label` ASC'
		)->fetchAll(PDO::FETCH_ASSOC);
		if (!$rows) {
			epc_acc_seed_categories_from_json($db, false);
			$rows = $db->query(
				'SELECT `id`, `parent_id`, `slug`, `label`, `pw_id`, `sort_order`
				FROM `epc_acc_categories` WHERE `active` = 1 ORDER BY `parent_id` ASC, `sort_order` ASC, `label` ASC'
			)->fetchAll(PDO::FETCH_ASSOC);
		}
		$parents = array();
		$children = array();
		foreach ($rows as $row) {
			$pid = (int) $row['parent_id'];
			if ($pid === 0) {
				$parents[(int) $row['id']] = array(
					'id' => (int) $row['id'],
					'slug' => $row['slug'],
					'label' => $row['label'],
					'pw_id' => (int) $row['pw_id'],
					'children' => array(),
					'count' => 0,
				);
			} else {
				$children[] = $row;
			}
		}
		foreach ($children as $row) {
			$pid = (int) $row['parent_id'];
			if (!isset($parents[$pid])) {
				continue;
			}
			$parents[$pid]['children'][] = array(
				'id' => (int) $row['id'],
				'slug' => $row['slug'],
				'label' => $row['label'],
				'pw_id' => (int) $row['pw_id'],
				'count' => 0,
			);
		}
		return array_values($parents);
	}
}

if (!function_exists('epc_acc_add_listing')) {
	/**
	 * @param array<string, mixed> $data
	 */
	function epc_acc_add_listing(PDO $db, array $data): int
	{
		epc_acc_ensure_schema($db);
		$now = time();
		$stmt = $db->prepare(
			'INSERT INTO `epc_acc_listings`
			(`category_id`, `subcategory_id`, `title`, `description`, `make`, `model`, `city`, `condition_type`,
			 `price`, `currency`, `image_url`, `external_url`, `stock_qty`, `status`, `created_at`, `updated_at`)
			VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)'
		);
		$stmt->execute(array(
			(int) ($data['category_id'] ?? 0),
			(int) ($data['subcategory_id'] ?? 0),
			trim((string) ($data['title'] ?? '')),
			trim((string) ($data['description'] ?? '')),
			trim((string) ($data['make'] ?? '')),
			trim((string) ($data['model'] ?? '')),
			trim((string) ($data['city'] ?? '')),
			trim((string) ($data['condition_type'] ?? 'new')),
			(float) ($data['price'] ?? 0),
			trim((string) ($data['currency'] ?? 'PKR')) ?: 'PKR',
			trim((string) ($data['image_url'] ?? '')),
			trim((string) ($data['external_url'] ?? '')),
			(int) ($data['stock_qty'] ?? 0),
			trim((string) ($data['status'] ?? 'published')) ?: 'published',
			$now,
			$now,
		));
		return (int) $db->lastInsertId();
	}
}

if (!function_exists('epc_acc_marketplace_search')) {
	/**
	 * Search published listings with PakWheels-style filters.
	 *
	 * @param array<string, mixed> $filters
	 * @return array<string, mixed>
	 */
	function epc_acc_marketplace_search(PDO $db, array $filters = array()): array
	{
		epc_acc_ensure_schema($db);
		$tree = epc_acc_get_category_tree($db);
		$tax = epc_acc_load_taxonomy_json();

		$q = trim((string) ($filters['q'] ?? ''));
		$category = trim((string) ($filters['category'] ?? ''));
		$subcategory = trim((string) ($filters['subcategory'] ?? ''));
		$make = trim((string) ($filters['make'] ?? ''));
		$model = trim((string) ($filters['model'] ?? ''));
		$city = trim((string) ($filters['city'] ?? ''));
		$condition = trim((string) ($filters['condition'] ?? ''));
		$priceMin = (float) ($filters['price_min'] ?? 0);
		$priceMax = (float) ($filters['price_max'] ?? 0);
		$sort = (string) ($filters['sort'] ?? 'updated-desc');
		$page = max(1, (int) ($filters['page'] ?? 1));
		$perPage = max(12, min(48, (int) ($filters['per_page'] ?? 24)));

		$categoryId = 0;
		$subcategoryId = 0;
		foreach ($tree as $parent) {
			if ($category !== '' && ($parent['slug'] === $category || (string) $parent['id'] === $category)) {
				$categoryId = (int) $parent['id'];
			}
			foreach ($parent['children'] as $child) {
				if ($subcategory !== '' && ($child['slug'] === $subcategory || (string) $child['id'] === $subcategory)) {
					$subcategoryId = (int) $child['id'];
					if ($categoryId < 1) {
						$categoryId = (int) $parent['id'];
					}
				}
			}
		}

		$where = array("`status` = 'published'");
		$bind = array();
		if ($categoryId > 0) {
			$where[] = '`category_id` = ?';
			$bind[] = $categoryId;
		}
		if ($subcategoryId > 0) {
			$where[] = '`subcategory_id` = ?';
			$bind[] = $subcategoryId;
		}
		if ($make !== '') {
			$where[] = '`make` = ?';
			$bind[] = $make;
		}
		if ($model !== '') {
			$where[] = '`model` LIKE ?';
			$bind[] = '%' . $model . '%';
		}
		if ($city !== '') {
			$where[] = '`city` = ?';
			$bind[] = $city;
		}
		if ($condition !== '') {
			$where[] = '`condition_type` = ?';
			$bind[] = strtolower($condition);
		}
		if ($priceMin > 0) {
			$where[] = '`price` >= ?';
			$bind[] = $priceMin;
		}
		if ($priceMax > 0) {
			$where[] = '`price` <= ?';
			$bind[] = $priceMax;
		}
		if ($q !== '') {
			$where[] = '(`title` LIKE ? OR `description` LIKE ? OR `make` LIKE ? OR `model` LIKE ?)';
			$like = '%' . $q . '%';
			array_push($bind, $like, $like, $like, $like);
		}
		$whereSql = implode(' AND ', $where);

		switch ($sort) {
			case 'price-asc':
				$orderSql = '`price` ASC, `updated_at` DESC';
				break;
			case 'price-desc':
				$orderSql = '`price` DESC, `updated_at` DESC';
				break;
			case 'updated-asc':
				$orderSql = '`updated_at` ASC';
				break;
			case 'top-sales':
				$orderSql = '`stock_qty` DESC, `updated_at` DESC';
				break;
			case 'updated-desc':
			default:
				$orderSql = '`updated_at` DESC, `id` DESC';
				break;
		}

		$countStmt = $db->prepare('SELECT COUNT(*) FROM `epc_acc_listings` WHERE ' . $whereSql);
		$countStmt->execute($bind);
		$total = (int) $countStmt->fetchColumn();
		$pages = max(1, (int) ceil($total / $perPage));
		if ($page > $pages) {
			$page = $pages;
		}
		$offset = ($page - 1) * $perPage;

		$listStmt = $db->prepare(
			'SELECT l.*, c.label AS category_label, c.slug AS category_slug,
				s.label AS subcategory_label, s.slug AS subcategory_slug
			FROM `epc_acc_listings` l
			LEFT JOIN `epc_acc_categories` c ON c.id = l.category_id
			LEFT JOIN `epc_acc_categories` s ON s.id = l.subcategory_id
			WHERE ' . $whereSql . '
			ORDER BY ' . $orderSql . '
			LIMIT ' . (int) $perPage . ' OFFSET ' . (int) $offset
		);
		$listStmt->execute($bind);
		$items = array();
		while ($row = $listStmt->fetch(PDO::FETCH_ASSOC)) {
			$items[] = array(
				'id' => (int) $row['id'],
				'title' => $row['title'],
				'description' => $row['description'],
				'make' => $row['make'],
				'model' => $row['model'],
				'city' => $row['city'],
				'condition' => $row['condition_type'],
				'price' => (float) $row['price'],
				'currency' => $row['currency'],
				'image_url' => $row['image_url'],
				'external_url' => $row['external_url'],
				'stock_qty' => (int) $row['stock_qty'],
				'category' => $row['category_slug'],
				'category_label' => $row['category_label'] ?: '',
				'subcategory' => $row['subcategory_slug'],
				'subcategory_label' => $row['subcategory_label'] ?: '',
				'updated_at' => (int) $row['updated_at'],
			);
		}

		// Facet counts (published only, respecting current filters except the facet itself)
		$facetBase = "SELECT category_id, subcategory_id, make, city, COUNT(*) AS cnt FROM `epc_acc_listings` WHERE `status` = 'published' GROUP BY category_id, subcategory_id, make, city";
		$facetRows = $db->query($facetBase)->fetchAll(PDO::FETCH_ASSOC);
		$catCounts = array();
		$subCounts = array();
		$makeCounts = array();
		$cityCounts = array();
		foreach ($facetRows as $fr) {
			$cid = (int) $fr['category_id'];
			$sid = (int) $fr['subcategory_id'];
			$cnt = (int) $fr['cnt'];
			if ($cid > 0) {
				$catCounts[$cid] = ($catCounts[$cid] ?? 0) + $cnt;
			}
			if ($sid > 0) {
				$subCounts[$sid] = ($subCounts[$sid] ?? 0) + $cnt;
			}
			$m = trim((string) $fr['make']);
			if ($m !== '') {
				$makeCounts[$m] = ($makeCounts[$m] ?? 0) + $cnt;
			}
			$c = trim((string) $fr['city']);
			if ($c !== '') {
				$cityCounts[$c] = ($cityCounts[$c] ?? 0) + $cnt;
			}
		}

		$facetCats = array();
		foreach ($tree as $parent) {
			$subs = array();
			foreach ($parent['children'] as $child) {
				$subs[] = array(
					'id' => $child['id'],
					'slug' => $child['slug'],
					'label' => $child['label'],
					'count' => (int) ($subCounts[$child['id']] ?? 0),
				);
			}
			$facetCats[] = array(
				'id' => $parent['id'],
				'slug' => $parent['slug'],
				'label' => $parent['label'],
				'count' => (int) ($catCounts[$parent['id']] ?? 0),
				'subs' => $subs,
			);
		}

		$makesTax = isset($tax['makes']) && is_array($tax['makes']) ? $tax['makes'] : array();
		$citiesTax = isset($tax['cities']) && is_array($tax['cities']) ? $tax['cities'] : array();
		$facetMakes = array();
		foreach ($makesTax as $mName) {
			$facetMakes[] = array('make' => $mName, 'count' => (int) ($makeCounts[$mName] ?? 0));
		}
		foreach ($makeCounts as $mName => $cnt) {
			if (!in_array($mName, $makesTax, true)) {
				$facetMakes[] = array('make' => $mName, 'count' => $cnt);
			}
		}
		$facetCities = array();
		foreach ($citiesTax as $cName) {
			$facetCities[] = array('city' => $cName, 'count' => (int) ($cityCounts[$cName] ?? 0));
		}
		foreach ($cityCounts as $cName => $cnt) {
			if (!in_array($cName, $citiesTax, true)) {
				$facetCities[] = array('city' => $cName, 'count' => $cnt);
			}
		}

		$from = $total === 0 ? 0 : ($offset + 1);
		$to = min($total, $offset + count($items));

		return array(
			'total' => $total,
			'page' => $page,
			'per_page' => $perPage,
			'pages' => $pages,
			'from' => $from,
			'to' => $to,
			'items' => $items,
			'facets' => array(
				'categories' => $facetCats,
				'makes' => $facetMakes,
				'cities' => $facetCities,
				'conditions' => array(
					array('value' => 'new', 'label' => 'New'),
					array('value' => 'used', 'label' => 'Used'),
				),
			),
			'taxonomy' => $facetCats,
			'makes' => $makesTax,
			'cities' => $citiesTax,
			'sort' => $sort,
			'source' => 'epc_acc_listings',
			'empty_catalog' => ($total === 0),
		);
	}
}
