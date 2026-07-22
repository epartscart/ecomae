<?php
/**
 * SKU Media & Specifications — unlimited photos + multi-type specs for any SKU.
 *
 * Identity: catalogue product_id and/or brand + article (docpart SKUs).
 * Spec "types" are named groups (Technical, Dimensions, Packaging, …) each with
 * unlimited rows of typed values (text, number, bool, list, rich).
 */
defined('_ASTEXE_') or die('No access');

if (!function_exists('epc_sku_media_ensure_schema')) {
	/**
	 * @return void
	 */
	function epc_sku_media_ensure_schema(PDO $db): void
	{
		static $done = false;
		if ($done) {
			return;
		}
		// CLI/unit tests may use SQLite with pre-created tables.
		try {
			$driver = (string) $db->getAttribute(PDO::ATTR_DRIVER_NAME);
		} catch (Throwable $e) {
			$driver = 'mysql';
		}
		if ($driver === 'sqlite') {
			$done = true;
			return;
		}
		$db->exec(
			'CREATE TABLE IF NOT EXISTS `epc_sku_profiles` (
				`id` INT UNSIGNED NOT NULL AUTO_INCREMENT,
				`product_id` INT UNSIGNED NULL DEFAULT NULL,
				`brand` VARCHAR(120) NOT NULL DEFAULT \'\',
				`article` VARCHAR(120) NOT NULL DEFAULT \'\',
				`article_key` VARCHAR(120) NOT NULL DEFAULT \'\',
				`title` VARCHAR(255) NOT NULL DEFAULT \'\',
				`subtitle` VARCHAR(255) NOT NULL DEFAULT \'\',
				`status` VARCHAR(24) NOT NULL DEFAULT \'active\',
				`created_at` INT UNSIGNED NOT NULL DEFAULT 0,
				`updated_at` INT UNSIGNED NOT NULL DEFAULT 0,
				PRIMARY KEY (`id`),
				KEY `product_id` (`product_id`),
				KEY `article_key` (`article_key`),
				KEY `brand_article` (`brand`, `article_key`),
				KEY `status` (`status`)
			) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4'
		);
		$db->exec(
			'CREATE TABLE IF NOT EXISTS `epc_sku_photos` (
				`id` INT UNSIGNED NOT NULL AUTO_INCREMENT,
				`profile_id` INT UNSIGNED NOT NULL,
				`file_name` VARCHAR(255) NOT NULL DEFAULT \'\',
				`alt` VARCHAR(255) NOT NULL DEFAULT \'\',
				`caption` VARCHAR(255) NOT NULL DEFAULT \'\',
				`photo_type` VARCHAR(48) NOT NULL DEFAULT \'product\',
				`sort_order` INT NOT NULL DEFAULT 0,
				`is_primary` TINYINT(1) NOT NULL DEFAULT 0,
				`created_at` INT UNSIGNED NOT NULL DEFAULT 0,
				PRIMARY KEY (`id`),
				KEY `profile_id` (`profile_id`),
				KEY `sort_order` (`profile_id`, `sort_order`)
			) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4'
		);
		$db->exec(
			'CREATE TABLE IF NOT EXISTS `epc_sku_spec_groups` (
				`id` INT UNSIGNED NOT NULL AUTO_INCREMENT,
				`profile_id` INT UNSIGNED NOT NULL,
				`name` VARCHAR(120) NOT NULL DEFAULT \'\',
				`code` VARCHAR(64) NOT NULL DEFAULT \'\',
				`icon` VARCHAR(48) NOT NULL DEFAULT \'fa-list\',
				`sort_order` INT NOT NULL DEFAULT 0,
				`created_at` INT UNSIGNED NOT NULL DEFAULT 0,
				PRIMARY KEY (`id`),
				KEY `profile_id` (`profile_id`),
				KEY `sort_order` (`profile_id`, `sort_order`)
			) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4'
		);
		$db->exec(
			'CREATE TABLE IF NOT EXISTS `epc_sku_spec_rows` (
				`id` INT UNSIGNED NOT NULL AUTO_INCREMENT,
				`group_id` INT UNSIGNED NOT NULL,
				`profile_id` INT UNSIGNED NOT NULL,
				`label` VARCHAR(190) NOT NULL DEFAULT \'\',
				`value` TEXT NULL,
				`value_type` VARCHAR(24) NOT NULL DEFAULT \'text\',
				`unit` VARCHAR(48) NOT NULL DEFAULT \'\',
				`sort_order` INT NOT NULL DEFAULT 0,
				`created_at` INT UNSIGNED NOT NULL DEFAULT 0,
				PRIMARY KEY (`id`),
				KEY `group_id` (`group_id`),
				KEY `profile_id` (`profile_id`),
				KEY `sort_order` (`group_id`, `sort_order`)
			) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4'
		);
		$done = true;
	}
}

if (!function_exists('epc_sku_media_normalize_article')) {
	function epc_sku_media_normalize_article(string $article): string
	{
		$article = strtoupper(trim($article));
		$article = preg_replace('/[^A-Z0-9]+/', '', $article) ?? '';
		return (string) $article;
	}
}

if (!function_exists('epc_sku_media_normalize_brand')) {
	function epc_sku_media_normalize_brand(string $brand): string
	{
		$brand = trim(preg_replace('/\s+/', ' ', $brand) ?? '');
		if ($brand === '') {
			return '';
		}
		return function_exists('mb_strtoupper')
			? mb_strtoupper($brand, 'UTF-8')
			: strtoupper($brand);
	}
}

if (!function_exists('epc_sku_media_photo_types')) {
	/**
	 * @return array<string,string>
	 */
	function epc_sku_media_photo_types(): array
	{
		return array(
			'product' => 'Product',
			'packaging' => 'Packaging',
			'detail' => 'Detail / close-up',
			'diagram' => 'Diagram / drawing',
			'install' => 'Installation',
			'datasheet' => 'Datasheet shot',
			'other' => 'Other',
		);
	}
}

if (!function_exists('epc_sku_media_value_types')) {
	/**
	 * @return array<string,string>
	 */
	function epc_sku_media_value_types(): array
	{
		return array(
			'text' => 'Text',
			'number' => 'Number',
			'bool' => 'Yes / No',
			'list' => 'List (comma-separated)',
			'rich' => 'Rich text',
		);
	}
}

if (!function_exists('epc_sku_media_default_spec_types')) {
	/**
	 * Preset specification group types operators can add in one click.
	 *
	 * @return array<int,array{name:string,code:string,icon:string}>
	 */
	function epc_sku_media_default_spec_types(): array
	{
		return array(
			array('name' => 'Technical', 'code' => 'technical', 'icon' => 'fa-cogs'),
			array('name' => 'Dimensions', 'code' => 'dimensions', 'icon' => 'fa-arrows-alt'),
			array('name' => 'Materials', 'code' => 'materials', 'icon' => 'fa-cube'),
			array('name' => 'Electrical', 'code' => 'electrical', 'icon' => 'fa-bolt'),
			array('name' => 'Compatibility', 'code' => 'compatibility', 'icon' => 'fa-car'),
			array('name' => 'Packaging', 'code' => 'packaging', 'icon' => 'fa-archive'),
			array('name' => 'Performance', 'code' => 'performance', 'icon' => 'fa-tachometer'),
			array('name' => 'Safety', 'code' => 'safety', 'icon' => 'fa-shield'),
			array('name' => 'Custom', 'code' => 'custom', 'icon' => 'fa-list'),
		);
	}
}

if (!function_exists('epc_sku_media_images_dir')) {
	function epc_sku_media_images_dir(): string
	{
		return '/content/files/images/sku_media/';
	}
}

if (!function_exists('epc_sku_media_images_fs')) {
	function epc_sku_media_images_fs(): string
	{
		$root = isset($_SERVER['DOCUMENT_ROOT']) ? (string) $_SERVER['DOCUMENT_ROOT'] : '';
		$dir = rtrim($root, '/\\') . epc_sku_media_images_dir();
		if (!is_dir($dir)) {
			@mkdir($dir, 0755, true);
		}
		return $dir;
	}
}

if (!function_exists('epc_sku_media_photo_url')) {
	function epc_sku_media_photo_url(string $fileName): string
	{
		$fileName = basename(str_replace('\\', '/', $fileName));
		if ($fileName === '' || $fileName === '.' || $fileName === '..') {
			return '';
		}
		return epc_sku_media_images_dir() . rawurlencode($fileName);
	}
}

if (!function_exists('epc_sku_media_storefront_part_url')) {
	/**
	 * Public storefront URL for a brand/article (part search CHPU).
	 */
	function epc_sku_media_storefront_part_url(string $brand, string $article, string $langHref = '/en'): string
	{
		$brand = trim($brand);
		$article = trim($article);
		if ($article === '') {
			return '';
		}
		$docRoot = rtrim((string) ($_SERVER['DOCUMENT_ROOT'] ?? ''), '/');
		$match = $docRoot . '/content/shop/docpart/docpart_article_match.php';
		if (is_file($match)) {
			require_once $match;
		}
		global $DP_Config;
		if (function_exists('epc_chpu_build_part_url') && is_object($DP_Config)) {
			$url = epc_chpu_build_part_url($DP_Config, $langHref, $brand, $article);
			if (is_string($url) && $url !== '') {
				return $url;
			}
		}
		$langHref = rtrim($langHref, '/');
		if ($langHref === '') {
			$langHref = '/en';
		}
		$artKey = function_exists('epc_sku_media_normalize_article')
			? epc_sku_media_normalize_article($article)
			: strtoupper(preg_replace('/[^A-Za-z0-9]+/', '', $article) ?? '');
		if ($artKey === '') {
			return '';
		}
		if ($brand === '') {
			return $langHref . '/parts/brands/' . rawurlencode($artKey);
		}
		return $langHref . '/parts/' . rawurlencode(strtoupper($brand)) . '/' . rawurlencode($artKey);
	}
}

if (!function_exists('epc_sku_media_attach_local_photo')) {
	/**
	 * Attach an existing local image file to a profile (seed / import — not HTTP upload).
	 *
	 * @param array<string,mixed> $meta
	 * @return array{ok:bool,id?:int,error?:string,file_name?:string,url?:string}
	 */
	function epc_sku_media_attach_local_photo(PDO $db, int $profileId, string $sourcePath, array $meta = array()): array
	{
		epc_sku_media_ensure_schema($db);
		if ($profileId <= 0) {
			return array('ok' => false, 'error' => 'Missing profile');
		}
		if ($sourcePath === '' || !is_file($sourcePath) || !is_readable($sourcePath)) {
			return array('ok' => false, 'error' => 'Source image missing');
		}
		$ext = strtolower(pathinfo($sourcePath, PATHINFO_EXTENSION));
		$allowed = array('jpg' => true, 'jpeg' => true, 'png' => true, 'gif' => true, 'webp' => true);
		if (!isset($allowed[$ext])) {
			return array('ok' => false, 'error' => 'Unsupported image type');
		}
		$dir = epc_sku_media_images_fs();
		$saved = 'sku_' . $profileId . '_' . time() . '_' . bin2hex(random_bytes(4)) . '.' . ($ext === 'jpeg' ? 'jpg' : $ext);
		$dest = $dir . $saved;
		if (!@copy($sourcePath, $dest)) {
			return array('ok' => false, 'error' => 'Could not copy image');
		}
		@chmod($dest, 0644);

		$sort = (int) ($meta['sort_order'] ?? 0);
		if ($sort <= 0) {
			$mx = $db->prepare('SELECT COALESCE(MAX(`sort_order`),0) FROM `epc_sku_photos` WHERE `profile_id` = ?');
			$mx->execute(array($profileId));
			$sort = ((int) $mx->fetchColumn()) + 10;
		}
		$isPrimary = !empty($meta['is_primary']) ? 1 : 0;
		if ($isPrimary) {
			$db->prepare('UPDATE `epc_sku_photos` SET `is_primary` = 0 WHERE `profile_id` = ?')->execute(array($profileId));
		} else {
			$cnt = $db->prepare('SELECT COUNT(*) FROM `epc_sku_photos` WHERE `profile_id` = ?');
			$cnt->execute(array($profileId));
			if ((int) $cnt->fetchColumn() === 0) {
				$isPrimary = 1;
			}
		}
		$photoType = trim((string) ($meta['photo_type'] ?? 'product'));
		$types = epc_sku_media_photo_types();
		if (!isset($types[$photoType])) {
			$photoType = 'product';
		}
		$db->prepare(
			'INSERT INTO `epc_sku_photos`
				(`profile_id`, `file_name`, `alt`, `caption`, `photo_type`, `sort_order`, `is_primary`, `created_at`)
			 VALUES (?, ?, ?, ?, ?, ?, ?, ?)'
		)->execute(array(
			$profileId,
			$saved,
			trim((string) ($meta['alt'] ?? '')),
			trim((string) ($meta['caption'] ?? '')),
			$photoType,
			$sort,
			$isPrimary,
			time(),
		));
		$id = (int) $db->lastInsertId();
		$db->prepare('UPDATE `epc_sku_profiles` SET `updated_at` = ? WHERE `id` = ?')->execute(array(time(), $profileId));
		return array(
			'ok' => true,
			'id' => $id,
			'file_name' => $saved,
			'url' => epc_sku_media_photo_url($saved),
		);
	}
}

if (!function_exists('epc_sku_media_find_profile')) {
	/**
	 * @return array<string,mixed>|null
	 */
	function epc_sku_media_find_profile(PDO $db, int $profileId = 0, int $productId = 0, string $brand = '', string $article = ''): ?array
	{
		epc_sku_media_ensure_schema($db);
		if ($profileId > 0) {
			$st = $db->prepare('SELECT * FROM `epc_sku_profiles` WHERE `id` = ? LIMIT 1');
			$st->execute(array($profileId));
			$row = $st->fetch(PDO::FETCH_ASSOC);
			return is_array($row) ? $row : null;
		}
		if ($productId > 0) {
			$st = $db->prepare('SELECT * FROM `epc_sku_profiles` WHERE `product_id` = ? ORDER BY `id` DESC LIMIT 1');
			$st->execute(array($productId));
			$row = $st->fetch(PDO::FETCH_ASSOC);
			if (is_array($row)) {
				return $row;
			}
		}
		$brand = epc_sku_media_normalize_brand($brand);
		$key = epc_sku_media_normalize_article($article);
		if ($brand !== '' && $key !== '') {
			$st = $db->prepare(
				'SELECT * FROM `epc_sku_profiles`
				 WHERE UPPER(`brand`) = ? AND `article_key` = ?
				 ORDER BY `id` DESC LIMIT 1'
			);
			$st->execute(array($brand, $key));
			$row = $st->fetch(PDO::FETCH_ASSOC);
			return is_array($row) ? $row : null;
		}
		if ($key !== '') {
			$st = $db->prepare('SELECT * FROM `epc_sku_profiles` WHERE `article_key` = ? ORDER BY `id` DESC LIMIT 1');
			$st->execute(array($key));
			$row = $st->fetch(PDO::FETCH_ASSOC);
			return is_array($row) ? $row : null;
		}
		return null;
	}
}

if (!function_exists('epc_sku_media_upsert_profile')) {
	/**
	 * @param array<string,mixed> $data
	 * @return array<string,mixed>
	 */
	function epc_sku_media_upsert_profile(PDO $db, array $data): array
	{
		epc_sku_media_ensure_schema($db);
		$id = (int) ($data['id'] ?? 0);
		$productId = (int) ($data['product_id'] ?? 0);
		$brand = epc_sku_media_normalize_brand((string) ($data['brand'] ?? ''));
		$article = trim((string) ($data['article'] ?? ''));
		$key = epc_sku_media_normalize_article($article !== '' ? $article : (string) ($data['article_key'] ?? ''));
		$title = trim((string) ($data['title'] ?? ''));
		$subtitle = trim((string) ($data['subtitle'] ?? ''));
		$status = trim((string) ($data['status'] ?? 'active'));
		if ($status === '') {
			$status = 'active';
		}
		$now = time();

		if ($id <= 0) {
			$existing = epc_sku_media_find_profile($db, 0, $productId, $brand, $article !== '' ? $article : $key);
			if (is_array($existing)) {
				$id = (int) $existing['id'];
			}
		}

		if ($id > 0) {
			$db->prepare(
				'UPDATE `epc_sku_profiles` SET
					`product_id` = ?, `brand` = ?, `article` = ?, `article_key` = ?,
					`title` = ?, `subtitle` = ?, `status` = ?, `updated_at` = ?
				 WHERE `id` = ?'
			)->execute(array(
				$productId > 0 ? $productId : null,
				$brand,
				$article !== '' ? $article : $key,
				$key,
				$title,
				$subtitle,
				$status,
				$now,
				$id,
			));
		} else {
			$db->prepare(
				'INSERT INTO `epc_sku_profiles`
					(`product_id`, `brand`, `article`, `article_key`, `title`, `subtitle`, `status`, `created_at`, `updated_at`)
				 VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?)'
			)->execute(array(
				$productId > 0 ? $productId : null,
				$brand,
				$article !== '' ? $article : $key,
				$key,
				$title,
				$subtitle,
				$status,
				$now,
				$now,
			));
			$id = (int) $db->lastInsertId();
		}
		$row = epc_sku_media_find_profile($db, $id);
		return is_array($row) ? $row : array('id' => $id);
	}
}

if (!function_exists('epc_sku_media_list_profiles')) {
	/**
	 * @return array<int,array<string,mixed>>
	 */
	function epc_sku_media_list_profiles(PDO $db, string $q = '', int $limit = 100, int $offset = 0): array
	{
		epc_sku_media_ensure_schema($db);
		$limit = max(1, min(500, $limit));
		$offset = max(0, $offset);
		$q = trim($q);
		if ($q !== '') {
			$like = '%' . $q . '%';
			$key = epc_sku_media_normalize_article($q);
			$st = $db->prepare(
				'SELECT p.*,
					(SELECT COUNT(*) FROM `epc_sku_photos` ph WHERE ph.`profile_id` = p.`id`) AS photo_count,
					(SELECT COUNT(*) FROM `epc_sku_spec_groups` g WHERE g.`profile_id` = p.`id`) AS group_count,
					(SELECT COUNT(*) FROM `epc_sku_spec_rows` r WHERE r.`profile_id` = p.`id`) AS spec_count
				 FROM `epc_sku_profiles` p
				 WHERE p.`brand` LIKE ? OR p.`article` LIKE ? OR p.`article_key` LIKE ? OR p.`title` LIKE ? OR p.`article_key` = ?
				 ORDER BY p.`updated_at` DESC
				 LIMIT ' . (int) $limit . ' OFFSET ' . (int) $offset
			);
			$st->execute(array($like, $like, $like, $like, $key));
		} else {
			$st = $db->query(
				'SELECT p.*,
					(SELECT COUNT(*) FROM `epc_sku_photos` ph WHERE ph.`profile_id` = p.`id`) AS photo_count,
					(SELECT COUNT(*) FROM `epc_sku_spec_groups` g WHERE g.`profile_id` = p.`id`) AS group_count,
					(SELECT COUNT(*) FROM `epc_sku_spec_rows` r WHERE r.`profile_id` = p.`id`) AS spec_count
				 FROM `epc_sku_profiles` p
				 ORDER BY p.`updated_at` DESC
				 LIMIT ' . (int) $limit . ' OFFSET ' . (int) $offset
			);
		}
		$rows = $st ? $st->fetchAll(PDO::FETCH_ASSOC) : array();
		return is_array($rows) ? $rows : array();
	}
}

if (!function_exists('epc_sku_media_price_storage_map')) {
	/**
	 * Map price_list id → warehouse short labels (from shop_storages.connection_options.price_id).
	 *
	 * @return array<int,array{storage_id:int,short_name:string,name:string}>
	 */
	function epc_sku_media_price_storage_map(PDO $db): array
	{
		static $cache = null;
		if (is_array($cache)) {
			return $cache;
		}
		$cache = array();
		try {
			$st = $db->query('SELECT `id`, `name`, `short_name`, `connection_options` FROM `shop_storages`');
			$rows = $st ? $st->fetchAll(PDO::FETCH_ASSOC) : array();
		} catch (Throwable $e) {
			return $cache;
		}
		foreach ($rows as $row) {
			$opts = json_decode((string) ($row['connection_options'] ?? ''), true);
			if (!is_array($opts) || empty($opts['price_id'])) {
				continue;
			}
			$priceId = (int) $opts['price_id'];
			if ($priceId <= 0) {
				continue;
			}
			$cache[$priceId] = array(
				'storage_id' => (int) $row['id'],
				'short_name' => (string) ($row['short_name'] !== '' ? $row['short_name'] : $row['name']),
				'name' => (string) ($row['name'] ?? ''),
			);
		}
		return $cache;
	}
}

if (!function_exists('epc_sku_media_search_library')) {
	/**
	 * Unified CP library: existing media profiles + supplier warehouse brands/articles
	 * (+ catalogue products with optional product_id link).
	 *
	 * @return array<int,array<string,mixed>>
	 */
	function epc_sku_media_search_library(PDO $db, string $q = '', int $limit = 120): array
	{
		epc_sku_media_ensure_schema($db);
		$limit = max(1, min(200, $limit));
		$q = trim($q);
		$out = array();
		$seen = array(); // brand|article_key
		$priceMap = epc_sku_media_price_storage_map($db);

		$profiles = epc_sku_media_list_profiles($db, $q, $limit, 0);
		foreach ($profiles as $p) {
			$brand = (string) ($p['brand'] ?? '');
			$article = (string) ($p['article'] ?? '');
			$key = epc_sku_media_normalize_article($article !== '' ? $article : (string) ($p['article_key'] ?? ''));
			$sig = epc_sku_media_normalize_brand($brand) . '|' . $key;
			if ($sig !== '|' && isset($seen[$sig])) {
				continue;
			}
			if ($sig !== '|') {
				$seen[$sig] = true;
			}
			$articleShow = $article !== '' ? $article : $key;
			$out[] = array(
				'id' => (int) ($p['id'] ?? 0),
				'source' => 'profile',
				'brand' => $brand,
				'article' => $articleShow,
				'article_show' => $articleShow,
				'title' => (string) ($p['title'] ?? ''),
				'warehouse' => '',
				'product_id' => (int) ($p['product_id'] ?? 0),
				'photo_count' => (int) ($p['photo_count'] ?? 0),
				'spec_count' => (int) ($p['spec_count'] ?? 0),
				'group_count' => (int) ($p['group_count'] ?? 0),
				'has_profile' => true,
				'status' => (string) ($p['status'] ?? 'active'),
				'storefront_url' => epc_sku_media_storefront_part_url($brand, $articleShow),
			);
		}

		$supplierLimit = max(20, $limit - count($out));
		try {
			if ($q !== '') {
				$like = '%' . $q . '%';
				$key = epc_sku_media_normalize_article($q);
				$st = $db->prepare(
					'SELECT d.`manufacturer`,
						MAX(d.`article`) AS `article`,
						MAX(d.`article_show`) AS `article_show`,
						d.`article_search`,
						MAX(d.`name`) AS `name`,
						MAX(d.`price_id`) AS `price_id`,
						COUNT(*) AS `offer_count`
					 FROM `shop_docpart_prices_data` d
					 WHERE d.`manufacturer` LIKE ?
						OR d.`article` LIKE ?
						OR d.`article_show` LIKE ?
						OR d.`article_search` LIKE ?
						OR d.`name` LIKE ?
						OR d.`article_search` = ?
					 GROUP BY d.`manufacturer`, d.`article_search`
					 ORDER BY MAX(d.`id`) DESC
					 LIMIT ' . (int) $supplierLimit
				);
				$st->execute(array($like, $like, $like, $like, $like, $key));
			} else {
				$st = $db->query(
					'SELECT d.`manufacturer`,
						MAX(d.`article`) AS `article`,
						MAX(d.`article_show`) AS `article_show`,
						d.`article_search`,
						MAX(d.`name`) AS `name`,
						MAX(d.`price_id`) AS `price_id`,
						COUNT(*) AS `offer_count`
					 FROM `shop_docpart_prices_data` d
					 WHERE d.`manufacturer` <> \'\' AND d.`article_search` <> \'\'
					 GROUP BY d.`manufacturer`, d.`article_search`
					 ORDER BY MAX(d.`id`) DESC
					 LIMIT ' . (int) $supplierLimit
				);
			}
			$supplierRows = $st ? $st->fetchAll(PDO::FETCH_ASSOC) : array();
		} catch (Throwable $e) {
			$supplierRows = array();
		}

		foreach ($supplierRows as $row) {
			$brand = epc_sku_media_normalize_brand((string) ($row['manufacturer'] ?? ''));
			$articleShow = trim((string) ($row['article_show'] ?? ''));
			if ($articleShow === '') {
				$articleShow = trim((string) ($row['article'] ?? ''));
			}
			$key = epc_sku_media_normalize_article(
				$articleShow !== '' ? $articleShow : (string) ($row['article_search'] ?? '')
			);
			if ($brand === '' || $key === '') {
				continue;
			}
			$sig = $brand . '|' . $key;
			if (isset($seen[$sig])) {
				// Enrich existing profile row with warehouse label if empty.
				foreach ($out as &$existing) {
					if (($existing['brand'] ?? '') === $brand
						&& epc_sku_media_normalize_article((string) ($existing['article'] ?? '')) === $key
						&& (string) ($existing['warehouse'] ?? '') === ''
					) {
						$priceId = (int) ($row['price_id'] ?? 0);
						if (isset($priceMap[$priceId])) {
							$existing['warehouse'] = $priceMap[$priceId]['short_name'];
						}
					}
				}
				unset($existing);
				continue;
			}
			$seen[$sig] = true;
			$priceId = (int) ($row['price_id'] ?? 0);
			$wh = isset($priceMap[$priceId]) ? $priceMap[$priceId]['short_name'] : '';
			$profile = epc_sku_media_find_profile($db, 0, 0, $brand, $key);
			$hasProfile = is_array($profile);
			$artOut = $articleShow !== '' ? $articleShow : $key;
			$out[] = array(
				'id' => $hasProfile ? (int) $profile['id'] : 0,
				'source' => 'supplier',
				'brand' => $brand,
				'article' => $artOut,
				'article_show' => $artOut,
				'title' => (string) ($row['name'] ?? ''),
				'warehouse' => $wh,
				'product_id' => 0,
				'photo_count' => 0,
				'spec_count' => 0,
				'group_count' => 0,
				'has_profile' => $hasProfile,
				'status' => $hasProfile ? (string) ($profile['status'] ?? 'active') : 'new',
				'offer_count' => (int) ($row['offer_count'] ?? 1),
				'storefront_url' => epc_sku_media_storefront_part_url($brand, $artOut),
			);
		}

		// Catalogue products (by caption) — optional product_id link for media.
		$catBudget = max(0, $limit - count($out));
		if ($catBudget > 0) {
			try {
				if ($q !== '') {
					$like = '%' . $q . '%';
					$st = $db->prepare(
						'SELECT `id`, `caption`, `alias` FROM `shop_catalogue_products`
						 WHERE `caption` LIKE ? OR `alias` LIKE ?
						 ORDER BY `id` DESC LIMIT ' . (int) $catBudget
					);
					$st->execute(array($like, $like));
				} else {
					$st = $db->query(
						'SELECT `id`, `caption`, `alias` FROM `shop_catalogue_products`
						 ORDER BY `id` DESC LIMIT ' . (int) min(15, $catBudget)
					);
				}
				$catRows = $st ? $st->fetchAll(PDO::FETCH_ASSOC) : array();
			} catch (Throwable $e) {
				$catRows = array();
			}
			foreach ($catRows as $c) {
				$pid = (int) ($c['id'] ?? 0);
				if ($pid <= 0) {
					continue;
				}
				$profile = epc_sku_media_find_profile($db, 0, $pid, '', '');
				$out[] = array(
					'id' => is_array($profile) ? (int) $profile['id'] : 0,
					'source' => 'catalogue',
					'brand' => is_array($profile) ? (string) ($profile['brand'] ?? '') : '',
					'article' => is_array($profile) ? (string) ($profile['article'] ?? '') : '',
					'article_show' => is_array($profile) ? (string) ($profile['article'] ?? '') : '',
					'title' => (string) ($c['caption'] ?? ''),
					'warehouse' => '',
					'product_id' => $pid,
					'photo_count' => 0,
					'spec_count' => 0,
					'group_count' => 0,
					'has_profile' => is_array($profile),
					'status' => is_array($profile) ? (string) ($profile['status'] ?? 'active') : 'new',
				);
			}
		}

		return array_slice($out, 0, $limit);
	}
}

if (!function_exists('epc_sku_media_ensure_from_identity')) {
	/**
	 * Create or return a profile for brand+article (supplier / manual) or product_id.
	 *
	 * @return array<string,mixed>
	 */
	function epc_sku_media_ensure_from_identity(
		PDO $db,
		string $brand,
		string $article,
		string $title = '',
		int $productId = 0
	): array {
		$brand = epc_sku_media_normalize_brand($brand);
		$article = trim($article);
		$key = epc_sku_media_normalize_article($article);
		$existing = epc_sku_media_find_profile($db, 0, $productId, $brand, $article !== '' ? $article : $key);
		if (is_array($existing)) {
			return $existing;
		}
		if ($title === '' && $brand !== '' && $key !== '') {
			$title = $brand . ' ' . ($article !== '' ? $article : $key);
		}
		return epc_sku_media_upsert_profile($db, array(
			'product_id' => $productId,
			'brand' => $brand,
			'article' => $article !== '' ? $article : $key,
			'title' => $title,
			'status' => 'active',
		));
	}
}

if (!function_exists('epc_sku_media_public_lookup')) {
	/**
	 * Storefront-safe lookup: active profile photos + specs for brand/article/product.
	 *
	 * @return array{ok:bool,url:string,photos:array,specs:array,profile:array|null}
	 */
	function epc_sku_media_public_lookup(PDO $db, string $brand = '', string $article = '', int $productId = 0): array
	{
		$empty = array('ok' => true, 'url' => '', 'photos' => array(), 'specs' => array(), 'profile' => null);
		$profile = epc_sku_media_resolve_for_product($db, $productId, $brand, $article);
		if (!is_array($profile)) {
			return $empty;
		}
		$status = (string) ($profile['status'] ?? 'active');
		if ($status === 'hidden' || $status === 'draft') {
			return $empty;
		}
		$payload = epc_sku_media_full_payload($db, (int) $profile['id']);
		if (!is_array($payload)) {
			return $empty;
		}
		$photos = array();
		$primaryUrl = '';
		foreach ($payload['photos'] ?? array() as $ph) {
			$url = (string) ($ph['url'] ?? '');
			if ($url === '') {
				continue;
			}
			$photos[] = array(
				'url' => $url,
				'alt' => (string) ($ph['alt'] ?? ''),
				'caption' => (string) ($ph['caption'] ?? ''),
				'photo_type' => (string) ($ph['photo_type'] ?? 'product'),
				'is_primary' => !empty($ph['is_primary']),
			);
			if ($primaryUrl === '' || !empty($ph['is_primary'])) {
				$primaryUrl = $url;
			}
		}
		$specs = array();
		foreach ($payload['spec_groups'] ?? array() as $g) {
			$rows = array();
			foreach ($g['rows'] ?? array() as $row) {
				$rows[] = array(
					'label' => (string) ($row['label'] ?? ''),
					'value' => (string) ($row['display'] ?? $row['value'] ?? ''),
					'value_type' => (string) ($row['value_type'] ?? 'text'),
				);
			}
			if (!$rows) {
				continue;
			}
			$specs[] = array(
				'name' => (string) ($g['name'] ?? 'Specifications'),
				'icon' => (string) ($g['icon'] ?? 'fa-list'),
				'rows' => $rows,
			);
		}
		return array(
			'ok' => true,
			'url' => $primaryUrl,
			'photos' => $photos,
			'specs' => $specs,
			'profile' => array(
				'id' => (int) $profile['id'],
				'brand' => (string) ($profile['brand'] ?? ''),
				'article' => (string) ($profile['article'] ?? ''),
				'title' => (string) ($profile['title'] ?? ''),
			),
		);
	}
}

if (!function_exists('epc_sku_media_photos')) {
	/**
	 * @return array<int,array<string,mixed>>
	 */
	function epc_sku_media_photos(PDO $db, int $profileId): array
	{
		if ($profileId <= 0) {
			return array();
		}
		epc_sku_media_ensure_schema($db);
		$st = $db->prepare(
			'SELECT * FROM `epc_sku_photos` WHERE `profile_id` = ? ORDER BY `is_primary` DESC, `sort_order` ASC, `id` ASC'
		);
		$st->execute(array($profileId));
		$rows = $st->fetchAll(PDO::FETCH_ASSOC);
		return is_array($rows) ? $rows : array();
	}
}

if (!function_exists('epc_sku_media_add_photo')) {
	/**
	 * @param array<string,mixed> $meta
	 * @return array{ok:bool,id?:int,error?:string,file_name?:string}
	 */
	function epc_sku_media_add_photo(PDO $db, int $profileId, array $file, array $meta = array()): array
	{
		epc_sku_media_ensure_schema($db);
		if ($profileId <= 0) {
			return array('ok' => false, 'error' => 'Missing profile');
		}
		if (empty($file['tmp_name']) || !is_uploaded_file((string) $file['tmp_name'])) {
			return array('ok' => false, 'error' => 'No upload');
		}
		$orig = (string) ($file['name'] ?? 'photo.jpg');
		$ext = strtolower(pathinfo($orig, PATHINFO_EXTENSION));
		$allowed = array('jpg' => true, 'jpeg' => true, 'png' => true, 'gif' => true, 'webp' => true);
		if (!isset($allowed[$ext])) {
			return array('ok' => false, 'error' => 'Unsupported image type');
		}
		if (function_exists('exif_imagetype')) {
			$type = @exif_imagetype((string) $file['tmp_name']);
			$okTypes = array(IMAGETYPE_JPEG, IMAGETYPE_PNG, IMAGETYPE_GIF);
			if (defined('IMAGETYPE_WEBP')) {
				$okTypes[] = IMAGETYPE_WEBP;
			}
			if ($type === false || !in_array($type, $okTypes, true)) {
				return array('ok' => false, 'error' => 'Invalid image file');
			}
		}
		$dir = epc_sku_media_images_fs();
		$saved = 'sku_' . $profileId . '_' . time() . '_' . bin2hex(random_bytes(4)) . '.' . ($ext === 'jpeg' ? 'jpg' : $ext);
		$dest = $dir . $saved;
		if (!@move_uploaded_file((string) $file['tmp_name'], $dest)) {
			if (!@copy((string) $file['tmp_name'], $dest)) {
				return array('ok' => false, 'error' => 'Could not save file');
			}
		}
		@chmod($dest, 0644);

		$sort = (int) ($meta['sort_order'] ?? 0);
		if ($sort <= 0) {
			$mx = $db->prepare('SELECT COALESCE(MAX(`sort_order`),0) FROM `epc_sku_photos` WHERE `profile_id` = ?');
			$mx->execute(array($profileId));
			$sort = ((int) $mx->fetchColumn()) + 10;
		}
		$isPrimary = !empty($meta['is_primary']) ? 1 : 0;
		if ($isPrimary) {
			$db->prepare('UPDATE `epc_sku_photos` SET `is_primary` = 0 WHERE `profile_id` = ?')->execute(array($profileId));
		} else {
			$cnt = $db->prepare('SELECT COUNT(*) FROM `epc_sku_photos` WHERE `profile_id` = ?');
			$cnt->execute(array($profileId));
			if ((int) $cnt->fetchColumn() === 0) {
				$isPrimary = 1;
			}
		}
		$photoType = trim((string) ($meta['photo_type'] ?? 'product'));
		$types = epc_sku_media_photo_types();
		if (!isset($types[$photoType])) {
			$photoType = 'product';
		}
		$db->prepare(
			'INSERT INTO `epc_sku_photos`
				(`profile_id`, `file_name`, `alt`, `caption`, `photo_type`, `sort_order`, `is_primary`, `created_at`)
			 VALUES (?, ?, ?, ?, ?, ?, ?, ?)'
		)->execute(array(
			$profileId,
			$saved,
			trim((string) ($meta['alt'] ?? '')),
			trim((string) ($meta['caption'] ?? '')),
			$photoType,
			$sort,
			$isPrimary,
			time(),
		));
		$id = (int) $db->lastInsertId();

		// Mirror primary/any photo into legacy catalogue gallery when product linked.
		$profile = epc_sku_media_find_profile($db, $profileId);
		$productId = is_array($profile) ? (int) ($profile['product_id'] ?? 0) : 0;
		if ($productId > 0) {
			epc_sku_media_mirror_to_catalogue($db, $productId, $saved);
		}

		$db->prepare('UPDATE `epc_sku_profiles` SET `updated_at` = ? WHERE `id` = ?')->execute(array(time(), $profileId));
		return array('ok' => true, 'id' => $id, 'file_name' => $saved);
	}
}

if (!function_exists('epc_sku_media_mirror_to_catalogue')) {
	function epc_sku_media_mirror_to_catalogue(PDO $db, int $productId, string $fileName): void
	{
		if ($productId <= 0 || $fileName === '') {
			return;
		}
		try {
			$legacyDir = rtrim((string) ($_SERVER['DOCUMENT_ROOT'] ?? ''), '/\\') . '/content/files/images/products_images/';
			$src = epc_sku_media_images_fs() . basename($fileName);
			if (is_file($src) && is_dir(dirname($legacyDir))) {
				if (!is_dir($legacyDir)) {
					@mkdir($legacyDir, 0755, true);
				}
				$legacyName = 'sku_m_' . basename($fileName);
				$dest = $legacyDir . $legacyName;
				if (!is_file($dest)) {
					@copy($src, $dest);
				}
				$chk = $db->prepare('SELECT COUNT(*) FROM `shop_products_images` WHERE `product_id` = ? AND `file_name` = ?');
				$chk->execute(array($productId, $legacyName));
				if ((int) $chk->fetchColumn() === 0) {
					$db->prepare('INSERT INTO `shop_products_images` (`product_id`, `file_name`) VALUES (?, ?)')->execute(array($productId, $legacyName));
				}
			}
		} catch (Throwable $e) {
			// Non-fatal — SKU media remains source of truth.
		}
	}
}

if (!function_exists('epc_sku_media_delete_photo')) {
	function epc_sku_media_delete_photo(PDO $db, int $photoId): bool
	{
		epc_sku_media_ensure_schema($db);
		$st = $db->prepare('SELECT * FROM `epc_sku_photos` WHERE `id` = ? LIMIT 1');
		$st->execute(array($photoId));
		$row = $st->fetch(PDO::FETCH_ASSOC);
		if (!is_array($row)) {
			return false;
		}
		$db->prepare('DELETE FROM `epc_sku_photos` WHERE `id` = ?')->execute(array($photoId));
		$file = basename((string) ($row['file_name'] ?? ''));
		if ($file !== '') {
			$path = epc_sku_media_images_fs() . $file;
			if (is_file($path)) {
				$use = $db->prepare('SELECT COUNT(*) FROM `epc_sku_photos` WHERE `file_name` = ?');
				$use->execute(array($file));
				if ((int) $use->fetchColumn() === 0) {
					@unlink($path);
				}
			}
		}
		$db->prepare('UPDATE `epc_sku_profiles` SET `updated_at` = ? WHERE `id` = ?')->execute(array(time(), (int) $row['profile_id']));
		return true;
	}
}

if (!function_exists('epc_sku_media_update_photo')) {
	/**
	 * @param array<string,mixed> $meta
	 */
	function epc_sku_media_update_photo(PDO $db, int $photoId, array $meta): bool
	{
		epc_sku_media_ensure_schema($db);
		$st = $db->prepare('SELECT * FROM `epc_sku_photos` WHERE `id` = ? LIMIT 1');
		$st->execute(array($photoId));
		$row = $st->fetch(PDO::FETCH_ASSOC);
		if (!is_array($row)) {
			return false;
		}
		$profileId = (int) $row['profile_id'];
		$alt = array_key_exists('alt', $meta) ? trim((string) $meta['alt']) : (string) $row['alt'];
		$caption = array_key_exists('caption', $meta) ? trim((string) $meta['caption']) : (string) $row['caption'];
		$photoType = array_key_exists('photo_type', $meta) ? trim((string) $meta['photo_type']) : (string) $row['photo_type'];
		$types = epc_sku_media_photo_types();
		if (!isset($types[$photoType])) {
			$photoType = (string) $row['photo_type'];
		}
		$sort = array_key_exists('sort_order', $meta) ? (int) $meta['sort_order'] : (int) $row['sort_order'];
		$isPrimary = array_key_exists('is_primary', $meta) ? (!empty($meta['is_primary']) ? 1 : 0) : (int) $row['is_primary'];
		if ($isPrimary) {
			$db->prepare('UPDATE `epc_sku_photos` SET `is_primary` = 0 WHERE `profile_id` = ?')->execute(array($profileId));
		}
		$db->prepare(
			'UPDATE `epc_sku_photos` SET `alt` = ?, `caption` = ?, `photo_type` = ?, `sort_order` = ?, `is_primary` = ? WHERE `id` = ?'
		)->execute(array($alt, $caption, $photoType, $sort, $isPrimary, $photoId));
		$db->prepare('UPDATE `epc_sku_profiles` SET `updated_at` = ? WHERE `id` = ?')->execute(array(time(), $profileId));
		return true;
	}
}

if (!function_exists('epc_sku_media_spec_bundle')) {
	/**
	 * Groups + rows for a profile.
	 *
	 * @return array{groups:array<int,array<string,mixed>>,rows:array<int,array<string,mixed>>}
	 */
	function epc_sku_media_spec_bundle(PDO $db, int $profileId): array
	{
		epc_sku_media_ensure_schema($db);
		if ($profileId <= 0) {
			return array('groups' => array(), 'rows' => array());
		}
		$g = $db->prepare('SELECT * FROM `epc_sku_spec_groups` WHERE `profile_id` = ? ORDER BY `sort_order` ASC, `id` ASC');
		$g->execute(array($profileId));
		$groups = $g->fetchAll(PDO::FETCH_ASSOC) ?: array();
		$r = $db->prepare('SELECT * FROM `epc_sku_spec_rows` WHERE `profile_id` = ? ORDER BY `sort_order` ASC, `id` ASC');
		$r->execute(array($profileId));
		$rows = $r->fetchAll(PDO::FETCH_ASSOC) ?: array();
		return array('groups' => $groups, 'rows' => $rows);
	}
}

if (!function_exists('epc_sku_media_add_spec_group')) {
	/**
	 * @param array<string,mixed> $data
	 * @return array{ok:bool,id?:int,error?:string}
	 */
	function epc_sku_media_add_spec_group(PDO $db, int $profileId, array $data): array
	{
		epc_sku_media_ensure_schema($db);
		if ($profileId <= 0) {
			return array('ok' => false, 'error' => 'Missing profile');
		}
		$name = trim((string) ($data['name'] ?? ''));
		if ($name === '') {
			return array('ok' => false, 'error' => 'Group name required');
		}
		$code = trim((string) ($data['code'] ?? ''));
		if ($code === '') {
			$code = strtolower(preg_replace('/[^a-zA-Z0-9]+/', '_', $name) ?? 'custom');
		}
		$icon = trim((string) ($data['icon'] ?? 'fa-list'));
		if ($icon === '') {
			$icon = 'fa-list';
		}
		$sort = (int) ($data['sort_order'] ?? 0);
		if ($sort <= 0) {
			$mx = $db->prepare('SELECT COALESCE(MAX(`sort_order`),0) FROM `epc_sku_spec_groups` WHERE `profile_id` = ?');
			$mx->execute(array($profileId));
			$sort = ((int) $mx->fetchColumn()) + 10;
		}
		$db->prepare(
			'INSERT INTO `epc_sku_spec_groups` (`profile_id`, `name`, `code`, `icon`, `sort_order`, `created_at`) VALUES (?, ?, ?, ?, ?, ?)'
		)->execute(array($profileId, $name, $code, $icon, $sort, time()));
		$id = (int) $db->lastInsertId();
		$db->prepare('UPDATE `epc_sku_profiles` SET `updated_at` = ? WHERE `id` = ?')->execute(array(time(), $profileId));
		return array('ok' => true, 'id' => $id);
	}
}

if (!function_exists('epc_sku_media_delete_spec_group')) {
	function epc_sku_media_delete_spec_group(PDO $db, int $groupId): bool
	{
		epc_sku_media_ensure_schema($db);
		$st = $db->prepare('SELECT `profile_id` FROM `epc_sku_spec_groups` WHERE `id` = ? LIMIT 1');
		$st->execute(array($groupId));
		$profileId = (int) $st->fetchColumn();
		if ($profileId <= 0) {
			return false;
		}
		$db->prepare('DELETE FROM `epc_sku_spec_rows` WHERE `group_id` = ?')->execute(array($groupId));
		$db->prepare('DELETE FROM `epc_sku_spec_groups` WHERE `id` = ?')->execute(array($groupId));
		$db->prepare('UPDATE `epc_sku_profiles` SET `updated_at` = ? WHERE `id` = ?')->execute(array(time(), $profileId));
		return true;
	}
}

if (!function_exists('epc_sku_media_add_spec_row')) {
	/**
	 * @param array<string,mixed> $data
	 * @return array{ok:bool,id?:int,error?:string}
	 */
	function epc_sku_media_add_spec_row(PDO $db, int $groupId, array $data): array
	{
		epc_sku_media_ensure_schema($db);
		$st = $db->prepare('SELECT `id`, `profile_id` FROM `epc_sku_spec_groups` WHERE `id` = ? LIMIT 1');
		$st->execute(array($groupId));
		$group = $st->fetch(PDO::FETCH_ASSOC);
		if (!is_array($group)) {
			return array('ok' => false, 'error' => 'Group not found');
		}
		$profileId = (int) $group['profile_id'];
		$label = trim((string) ($data['label'] ?? ''));
		if ($label === '') {
			return array('ok' => false, 'error' => 'Label required');
		}
		$valueType = trim((string) ($data['value_type'] ?? 'text'));
		$types = epc_sku_media_value_types();
		if (!isset($types[$valueType])) {
			$valueType = 'text';
		}
		$value = (string) ($data['value'] ?? '');
		if ($valueType === 'bool') {
			$value = (!empty($data['value']) && $value !== '0' && strtolower($value) !== 'no' && strtolower($value) !== 'false') ? '1' : '0';
		}
		$unit = trim((string) ($data['unit'] ?? ''));
		$sort = (int) ($data['sort_order'] ?? 0);
		if ($sort <= 0) {
			$mx = $db->prepare('SELECT COALESCE(MAX(`sort_order`),0) FROM `epc_sku_spec_rows` WHERE `group_id` = ?');
			$mx->execute(array($groupId));
			$sort = ((int) $mx->fetchColumn()) + 10;
		}
		$db->prepare(
			'INSERT INTO `epc_sku_spec_rows`
				(`group_id`, `profile_id`, `label`, `value`, `value_type`, `unit`, `sort_order`, `created_at`)
			 VALUES (?, ?, ?, ?, ?, ?, ?, ?)'
		)->execute(array($groupId, $profileId, $label, $value, $valueType, $unit, $sort, time()));
		$id = (int) $db->lastInsertId();
		$db->prepare('UPDATE `epc_sku_profiles` SET `updated_at` = ? WHERE `id` = ?')->execute(array(time(), $profileId));
		return array('ok' => true, 'id' => $id);
	}
}

if (!function_exists('epc_sku_media_update_spec_row')) {
	/**
	 * @param array<string,mixed> $data
	 */
	function epc_sku_media_update_spec_row(PDO $db, int $rowId, array $data): bool
	{
		epc_sku_media_ensure_schema($db);
		$st = $db->prepare('SELECT * FROM `epc_sku_spec_rows` WHERE `id` = ? LIMIT 1');
		$st->execute(array($rowId));
		$row = $st->fetch(PDO::FETCH_ASSOC);
		if (!is_array($row)) {
			return false;
		}
		$label = array_key_exists('label', $data) ? trim((string) $data['label']) : (string) $row['label'];
		$valueType = array_key_exists('value_type', $data) ? trim((string) $data['value_type']) : (string) $row['value_type'];
		$types = epc_sku_media_value_types();
		if (!isset($types[$valueType])) {
			$valueType = (string) $row['value_type'];
		}
		$value = array_key_exists('value', $data) ? (string) $data['value'] : (string) $row['value'];
		if ($valueType === 'bool') {
			$value = ($value !== '' && $value !== '0' && strtolower($value) !== 'no' && strtolower($value) !== 'false') ? '1' : '0';
		}
		$unit = array_key_exists('unit', $data) ? trim((string) $data['unit']) : (string) $row['unit'];
		$sort = array_key_exists('sort_order', $data) ? (int) $data['sort_order'] : (int) $row['sort_order'];
		$db->prepare(
			'UPDATE `epc_sku_spec_rows` SET `label` = ?, `value` = ?, `value_type` = ?, `unit` = ?, `sort_order` = ? WHERE `id` = ?'
		)->execute(array($label, $value, $valueType, $unit, $sort, $rowId));
		$db->prepare('UPDATE `epc_sku_profiles` SET `updated_at` = ? WHERE `id` = ?')->execute(array(time(), (int) $row['profile_id']));
		return true;
	}
}

if (!function_exists('epc_sku_media_delete_spec_row')) {
	function epc_sku_media_delete_spec_row(PDO $db, int $rowId): bool
	{
		epc_sku_media_ensure_schema($db);
		$st = $db->prepare('SELECT `profile_id` FROM `epc_sku_spec_rows` WHERE `id` = ? LIMIT 1');
		$st->execute(array($rowId));
		$profileId = (int) $st->fetchColumn();
		if ($profileId <= 0) {
			return false;
		}
		$db->prepare('DELETE FROM `epc_sku_spec_rows` WHERE `id` = ?')->execute(array($rowId));
		$db->prepare('UPDATE `epc_sku_profiles` SET `updated_at` = ? WHERE `id` = ?')->execute(array(time(), $profileId));
		return true;
	}
}

if (!function_exists('epc_sku_media_delete_profile')) {
	function epc_sku_media_delete_profile(PDO $db, int $profileId): bool
	{
		epc_sku_media_ensure_schema($db);
		if ($profileId <= 0) {
			return false;
		}
		$photos = epc_sku_media_photos($db, $profileId);
		$db->prepare('DELETE FROM `epc_sku_spec_rows` WHERE `profile_id` = ?')->execute(array($profileId));
		$db->prepare('DELETE FROM `epc_sku_spec_groups` WHERE `profile_id` = ?')->execute(array($profileId));
		$db->prepare('DELETE FROM `epc_sku_photos` WHERE `profile_id` = ?')->execute(array($profileId));
		$db->prepare('DELETE FROM `epc_sku_profiles` WHERE `id` = ?')->execute(array($profileId));
		foreach ($photos as $ph) {
			$file = basename((string) ($ph['file_name'] ?? ''));
			if ($file === '') {
				continue;
			}
			$path = epc_sku_media_images_fs() . $file;
			if (is_file($path)) {
				@unlink($path);
			}
		}
		return true;
	}
}

if (!function_exists('epc_sku_media_format_value')) {
	function epc_sku_media_format_value(string $value, string $valueType, string $unit = ''): string
	{
		$out = '';
		switch ($valueType) {
			case 'bool':
				$out = ($value === '1' || strtolower($value) === 'yes' || strtolower($value) === 'true') ? 'Yes' : 'No';
				break;
			case 'list':
				$parts = preg_split('/\s*,\s*/', $value) ?: array();
				$parts = array_values(array_filter(array_map('trim', $parts), static function ($p) {
					return $p !== '';
				}));
				$out = implode(', ', $parts);
				break;
			case 'rich':
				$out = $value;
				break;
			case 'number':
			case 'text':
			default:
				$out = $value;
				break;
		}
		if ($unit !== '' && $valueType !== 'bool' && $valueType !== 'rich') {
			$out = trim($out . ' ' . $unit);
		}
		return $out;
	}
}

if (!function_exists('epc_sku_media_resolve_for_product')) {
	/**
	 * Resolve profile for a catalogue product (by product_id, then brand/article props).
	 *
	 * @return array<string,mixed>|null
	 */
	function epc_sku_media_resolve_for_product(PDO $db, int $productId, string $brand = '', string $article = ''): ?array
	{
		if ($productId <= 0 && $brand === '' && $article === '') {
			return null;
		}
		$found = epc_sku_media_find_profile($db, 0, $productId, $brand, $article);
		return $found;
	}
}

if (!function_exists('epc_sku_media_full_payload')) {
	/**
	 * Full editor/storefront payload for a profile.
	 *
	 * @return array<string,mixed>|null
	 */
	function epc_sku_media_full_payload(PDO $db, int $profileId): ?array
	{
		$profile = epc_sku_media_find_profile($db, $profileId);
		if (!is_array($profile)) {
			return null;
		}
		$photos = epc_sku_media_photos($db, $profileId);
		foreach ($photos as &$ph) {
			$ph['url'] = epc_sku_media_photo_url((string) ($ph['file_name'] ?? ''));
		}
		unset($ph);
		$bundle = epc_sku_media_spec_bundle($db, $profileId);
		$grouped = array();
		foreach ($bundle['groups'] as $g) {
			$gid = (int) $g['id'];
			$grouped[$gid] = $g;
			$grouped[$gid]['rows'] = array();
		}
		foreach ($bundle['rows'] as $row) {
			$gid = (int) $row['group_id'];
			if (!isset($grouped[$gid])) {
				continue;
			}
			$row['display'] = epc_sku_media_format_value(
				(string) ($row['value'] ?? ''),
				(string) ($row['value_type'] ?? 'text'),
				(string) ($row['unit'] ?? '')
			);
			$grouped[$gid]['rows'][] = $row;
		}
		$profile['storefront_url'] = epc_sku_media_storefront_part_url(
			(string) ($profile['brand'] ?? ''),
			(string) ($profile['article'] ?? '')
		);
		return array(
			'profile' => $profile,
			'photos' => $photos,
			'spec_groups' => array_values($grouped),
			'photo_types' => epc_sku_media_photo_types(),
			'value_types' => epc_sku_media_value_types(),
			'default_spec_types' => epc_sku_media_default_spec_types(),
			'storefront_url' => (string) $profile['storefront_url'],
		);
	}
}
