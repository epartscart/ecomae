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
				`year` VARCHAR(16) NOT NULL DEFAULT '',
				`city` VARCHAR(120) NOT NULL DEFAULT '',
				`condition_type` VARCHAR(32) NOT NULL DEFAULT 'new',
				`price` DECIMAL(12,2) NOT NULL DEFAULT 0,
				`compare_price` DECIMAL(12,2) NOT NULL DEFAULT 0,
				`currency` VARCHAR(8) NOT NULL DEFAULT 'AED',
				`image_url` VARCHAR(500) NOT NULL DEFAULT '',
				`external_url` VARCHAR(500) NOT NULL DEFAULT '',
				`photo_count` INT NOT NULL DEFAULT 1,
				`featured` TINYINT(1) NOT NULL DEFAULT 0,
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
				KEY `featured` (`featured`),
				KEY `updated_at` (`updated_at`)
			) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;"
		);
		// Upgrade older installs.
		$cols = array();
		try {
			foreach ($db->query('SHOW COLUMNS FROM `epc_acc_listings`') as $col) {
				$cols[strtolower((string) $col['Field'])] = true;
			}
		} catch (Exception $e) {
			$cols = array();
		}
		$alters = array(
			'year' => "ADD COLUMN `year` VARCHAR(16) NOT NULL DEFAULT '' AFTER `model`",
			'compare_price' => "ADD COLUMN `compare_price` DECIMAL(12,2) NOT NULL DEFAULT 0 AFTER `price`",
			'photo_count' => "ADD COLUMN `photo_count` INT NOT NULL DEFAULT 1 AFTER `external_url`",
			'featured' => "ADD COLUMN `featured` TINYINT(1) NOT NULL DEFAULT 0 AFTER `photo_count`",
		);
		foreach ($alters as $name => $ddl) {
			if (empty($cols[$name])) {
				try {
					$db->exec('ALTER TABLE `epc_acc_listings` ' . $ddl);
				} catch (Exception $e) {
					// ignore
				}
			}
		}
		// Prefer AED as the column default for new rows (UAE marketplace).
		try {
			$db->exec("ALTER TABLE `epc_acc_listings` MODIFY COLUMN `currency` VARCHAR(8) NOT NULL DEFAULT 'AED'");
		} catch (Exception $e) {
			// ignore
		}

		$db->exec(
			"CREATE TABLE IF NOT EXISTS `epc_acc_terms` (
				`id` INT UNSIGNED NOT NULL AUTO_INCREMENT,
				`term_type` VARCHAR(32) NOT NULL,
				`parent_id` INT UNSIGNED NOT NULL DEFAULT 0,
				`value` VARCHAR(190) NOT NULL,
				`label` VARCHAR(190) NOT NULL,
				`sort_order` INT NOT NULL DEFAULT 0,
				`active` TINYINT(1) NOT NULL DEFAULT 1,
				PRIMARY KEY (`id`),
				UNIQUE KEY `type_value_parent` (`term_type`, `value`, `parent_id`),
				KEY `term_type_active` (`term_type`, `active`),
				KEY `parent_id` (`parent_id`)
			) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;"
		);

		$db->exec(
			"CREATE TABLE IF NOT EXISTS `epc_acc_photos` (
				`id` INT UNSIGNED NOT NULL AUTO_INCREMENT,
				`listing_id` INT UNSIGNED NOT NULL,
				`file_name` VARCHAR(255) NOT NULL,
				`sort_order` INT NOT NULL DEFAULT 0,
				`is_primary` TINYINT(1) NOT NULL DEFAULT 0,
				`created_at` INT UNSIGNED NOT NULL DEFAULT 0,
				PRIMARY KEY (`id`),
				KEY `listing_id` (`listing_id`),
				KEY `listing_primary` (`listing_id`, `is_primary`)
			) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;"
		);
	}
}

if (!function_exists('epc_acc_photos_fs_dir')) {
	function epc_acc_photos_fs_dir(): string
	{
		$root = rtrim((string) ($_SERVER['DOCUMENT_ROOT'] ?? ''), '/\\');
		$dir = $root . '/content/files/images/accessories/';
		if (!is_dir($dir)) {
			@mkdir($dir, 0755, true);
		}
		return $dir;
	}
}

if (!function_exists('epc_acc_photo_public_url')) {
	function epc_acc_photo_public_url(string $fileName): string
	{
		$fileName = basename(trim($fileName));
		if ($fileName === '') {
			return '';
		}
		return '/content/files/images/accessories/' . rawurlencode($fileName);
	}
}

if (!function_exists('epc_acc_storefront_url')) {
	/**
	 * Deep-link to a listing on the public accessories marketplace.
	 *
	 * @param array<string,mixed>|int $listingOrId
	 */
	function epc_acc_storefront_url($listingOrId, string $langHref = '/en'): string
	{
		$langHref = rtrim($langHref !== '' ? $langHref : '/en', '/');
		$id = 0;
		$cat = '';
		$sub = '';
		if (is_array($listingOrId)) {
			$id = (int) ($listingOrId['id'] ?? 0);
			$cat = trim((string) ($listingOrId['category_slug'] ?? $listingOrId['category'] ?? ''));
			$sub = trim((string) ($listingOrId['subcategory_slug'] ?? $listingOrId['subcategory'] ?? ''));
		} else {
			$id = (int) $listingOrId;
		}
		$qs = array();
		if ($id > 0) {
			$qs['id'] = (string) $id;
		}
		if ($cat !== '') {
			$qs['category'] = $cat;
		}
		if ($sub !== '') {
			$qs['subcategory'] = $sub;
		}
		$path = $langHref . '/accessories-spare-parts';
		if ($qs === array()) {
			return $path;
		}
		return $path . '?' . http_build_query($qs);
	}
}

if (!function_exists('epc_acc_is_outbound_external_url')) {
	/**
	 * True only for a real outbound link. Category browse URLs (no id=) are not outbound.
	 */
	function epc_acc_is_outbound_external_url(string $url): bool
	{
		$url = trim($url);
		if ($url === '') {
			return false;
		}
		// Same-site accessories browse / category filters must not replace the detail page.
		if (preg_match('#(^|/)accessories(-spare-parts)?([/?#]|$)#i', $url)) {
			if (!preg_match('#[?&]id=\d+#', $url)) {
				return false;
			}
			// Same-site detail deep-link — prefer our canonical detail_url instead.
			return false;
		}
		if (preg_match('#^https?://#i', $url)) {
			return true;
		}
		// Relative paths that are not accessories browse (e.g. /en/parts/…)
		if (isset($url[0]) && $url[0] === '/') {
			return true;
		}
		return false;
	}
}

if (!function_exists('epc_acc_photos_list')) {
	/**
	 * @return list<array{id:int,listing_id:int,file_name:string,url:string,sort_order:int,is_primary:bool,created_at:int}>
	 */
	function epc_acc_photos_list(PDO $db, int $listingId): array
	{
		if ($listingId <= 0) {
			return array();
		}
		epc_acc_ensure_schema($db);
		$st = $db->prepare(
			'SELECT `id`, `listing_id`, `file_name`, `sort_order`, `is_primary`, `created_at`
			 FROM `epc_acc_photos`
			 WHERE `listing_id` = ?
			 ORDER BY `is_primary` DESC, `sort_order` ASC, `id` ASC'
		);
		$st->execute(array($listingId));
		$out = array();
		while ($row = $st->fetch(PDO::FETCH_ASSOC)) {
			$file = (string) ($row['file_name'] ?? '');
			$out[] = array(
				'id' => (int) $row['id'],
				'listing_id' => (int) $row['listing_id'],
				'file_name' => $file,
				'url' => epc_acc_photo_public_url($file),
				'sort_order' => (int) ($row['sort_order'] ?? 0),
				'is_primary' => !empty($row['is_primary']),
				'created_at' => (int) ($row['created_at'] ?? 0),
			);
		}
		return $out;
	}
}

if (!function_exists('epc_acc_photos_sync_listing')) {
	/** Keep listing.image_url + photo_count in sync with gallery. */
	function epc_acc_photos_sync_listing(PDO $db, int $listingId): void
	{
		if ($listingId <= 0) {
			return;
		}
		$photos = epc_acc_photos_list($db, $listingId);
		$count = count($photos);
		$primaryUrl = '';
		foreach ($photos as $p) {
			if (!empty($p['is_primary']) && !empty($p['url'])) {
				$primaryUrl = (string) $p['url'];
				break;
			}
		}
		if ($primaryUrl === '' && $photos !== array()) {
			$primaryUrl = (string) ($photos[0]['url'] ?? '');
		}
		try {
			if ($count > 0 && $primaryUrl !== '') {
				$db->prepare(
					'UPDATE `epc_acc_listings` SET `image_url` = ?, `photo_count` = ?, `updated_at` = ? WHERE `id` = ?'
				)->execute(array($primaryUrl, $count, time(), $listingId));
			} else {
				$db->prepare(
					'UPDATE `epc_acc_listings` SET `photo_count` = 1, `updated_at` = ? WHERE `id` = ?'
				)->execute(array(time(), $listingId));
			}
		} catch (Throwable $e) {
		}
	}
}

if (!function_exists('epc_acc_photos_add')) {
	/**
	 * @param array<string,mixed> $file $_FILES element
	 * @return array{ok:bool,id?:int,url?:string,error?:string,photo?:array<string,mixed>}
	 */
	function epc_acc_photos_add(PDO $db, int $listingId, array $file, bool $asPrimary = false): array
	{
		epc_acc_ensure_schema($db);
		if ($listingId <= 0) {
			return array('ok' => false, 'error' => 'Save the listing first, then upload photos.');
		}
		$exists = $db->prepare('SELECT `id` FROM `epc_acc_listings` WHERE `id` = ? LIMIT 1');
		$exists->execute(array($listingId));
		if (!(int) $exists->fetchColumn()) {
			return array('ok' => false, 'error' => 'Listing not found');
		}
		if (empty($file['tmp_name']) || !is_uploaded_file((string) $file['tmp_name'])) {
			return array('ok' => false, 'error' => 'No upload');
		}
		if ((int) ($file['error'] ?? UPLOAD_ERR_OK) !== UPLOAD_ERR_OK) {
			return array('ok' => false, 'error' => 'Upload error');
		}
		$size = (int) ($file['size'] ?? 0);
		if ($size <= 0 || $size > 8 * 1024 * 1024) {
			return array('ok' => false, 'error' => 'Image must be under 8 MB');
		}
		$orig = (string) ($file['name'] ?? 'photo.jpg');
		$ext = strtolower(pathinfo($orig, PATHINFO_EXTENSION));
		$allowed = array('jpg' => true, 'jpeg' => true, 'png' => true, 'gif' => true, 'webp' => true);
		if (!isset($allowed[$ext])) {
			return array('ok' => false, 'error' => 'Use JPG, PNG, GIF or WEBP');
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
		$dir = epc_acc_photos_fs_dir();
		if (!is_dir($dir) || !is_writable($dir)) {
			return array('ok' => false, 'error' => 'Photo folder is not writable');
		}
		$saved = 'acc_' . $listingId . '_' . time() . '_' . bin2hex(random_bytes(4)) . '.' . ($ext === 'jpeg' ? 'jpg' : $ext);
		$dest = $dir . $saved;
		if (!@move_uploaded_file((string) $file['tmp_name'], $dest) && !@copy((string) $file['tmp_name'], $dest)) {
			return array('ok' => false, 'error' => 'Could not save file');
		}
		@chmod($dest, 0644);

		$mx = $db->prepare('SELECT COALESCE(MAX(`sort_order`),0) FROM `epc_acc_photos` WHERE `listing_id` = ?');
		$mx->execute(array($listingId));
		$sort = ((int) $mx->fetchColumn()) + 10;
		$cnt = $db->prepare('SELECT COUNT(*) FROM `epc_acc_photos` WHERE `listing_id` = ?');
		$cnt->execute(array($listingId));
		$isPrimary = ($asPrimary || (int) $cnt->fetchColumn() === 0) ? 1 : 0;
		if ($isPrimary) {
			$db->prepare('UPDATE `epc_acc_photos` SET `is_primary` = 0 WHERE `listing_id` = ?')->execute(array($listingId));
		}
		$db->prepare(
			'INSERT INTO `epc_acc_photos` (`listing_id`, `file_name`, `sort_order`, `is_primary`, `created_at`)
			 VALUES (?, ?, ?, ?, ?)'
		)->execute(array($listingId, $saved, $sort, $isPrimary, time()));
		$id = (int) $db->lastInsertId();
		epc_acc_photos_sync_listing($db, $listingId);
		$photos = epc_acc_photos_list($db, $listingId);
		$photo = null;
		foreach ($photos as $p) {
			if ((int) $p['id'] === $id) {
				$photo = $p;
				break;
			}
		}
		return array(
			'ok' => true,
			'id' => $id,
			'url' => epc_acc_photo_public_url($saved),
			'photo' => $photo,
			'photos' => $photos,
		);
	}
}

if (!function_exists('epc_acc_photos_delete')) {
	/**
	 * @return array{ok:bool,error?:string,photos?:list<array<string,mixed>>}
	 */
	function epc_acc_photos_delete(PDO $db, int $listingId, int $photoId): array
	{
		epc_acc_ensure_schema($db);
		if ($listingId <= 0 || $photoId <= 0) {
			return array('ok' => false, 'error' => 'Invalid photo');
		}
		$st = $db->prepare('SELECT `id`, `file_name`, `is_primary` FROM `epc_acc_photos` WHERE `id` = ? AND `listing_id` = ? LIMIT 1');
		$st->execute(array($photoId, $listingId));
		$row = $st->fetch(PDO::FETCH_ASSOC);
		if (!$row) {
			return array('ok' => false, 'error' => 'Photo not found');
		}
		$db->prepare('DELETE FROM `epc_acc_photos` WHERE `id` = ?')->execute(array($photoId));
		$file = basename((string) ($row['file_name'] ?? ''));
		if ($file !== '') {
			$path = epc_acc_photos_fs_dir() . $file;
			if (is_file($path)) {
				@unlink($path);
			}
		}
		if (!empty($row['is_primary'])) {
			$next = $db->prepare(
				'SELECT `id` FROM `epc_acc_photos` WHERE `listing_id` = ? ORDER BY `sort_order` ASC, `id` ASC LIMIT 1'
			);
			$next->execute(array($listingId));
			$nextId = (int) $next->fetchColumn();
			if ($nextId > 0) {
				$db->prepare('UPDATE `epc_acc_photos` SET `is_primary` = 1 WHERE `id` = ?')->execute(array($nextId));
			}
		}
		epc_acc_photos_sync_listing($db, $listingId);
		return array('ok' => true, 'photos' => epc_acc_photos_list($db, $listingId));
	}
}

if (!function_exists('epc_acc_photos_set_primary')) {
	/**
	 * @return array{ok:bool,error?:string,photos?:list<array<string,mixed>>}
	 */
	function epc_acc_photos_set_primary(PDO $db, int $listingId, int $photoId): array
	{
		epc_acc_ensure_schema($db);
		$st = $db->prepare('SELECT `id` FROM `epc_acc_photos` WHERE `id` = ? AND `listing_id` = ? LIMIT 1');
		$st->execute(array($photoId, $listingId));
		if (!(int) $st->fetchColumn()) {
			return array('ok' => false, 'error' => 'Photo not found');
		}
		$db->prepare('UPDATE `epc_acc_photos` SET `is_primary` = 0 WHERE `listing_id` = ?')->execute(array($listingId));
		$db->prepare('UPDATE `epc_acc_photos` SET `is_primary` = 1 WHERE `id` = ?')->execute(array($photoId));
		epc_acc_photos_sync_listing($db, $listingId);
		return array('ok' => true, 'photos' => epc_acc_photos_list($db, $listingId));
	}
}

if (!function_exists('epc_acc_photos_add_many_from_files')) {
	/**
	 * Process $_FILES['photos'] style multi-upload after listing save.
	 *
	 * @param array<string,mixed> $filesField
	 * @return array{ok:int,failed:int,errors:list<string>}
	 */
	function epc_acc_photos_add_many_from_files(PDO $db, int $listingId, array $filesField): array
	{
		$ok = 0;
		$failed = 0;
		$errors = array();
		if ($listingId <= 0 || empty($filesField['name'])) {
			return array('ok' => 0, 'failed' => 0, 'errors' => array());
		}
		$names = $filesField['name'];
		if (!is_array($names)) {
			$one = array(
				'name' => $filesField['name'] ?? '',
				'type' => $filesField['type'] ?? '',
				'tmp_name' => $filesField['tmp_name'] ?? '',
				'error' => $filesField['error'] ?? UPLOAD_ERR_NO_FILE,
				'size' => $filesField['size'] ?? 0,
			);
			$res = epc_acc_photos_add($db, $listingId, $one, false);
			if (!empty($res['ok'])) {
				$ok++;
			} else {
				$failed++;
				$errors[] = (string) ($res['error'] ?? 'Upload failed');
			}
			return array('ok' => $ok, 'failed' => $failed, 'errors' => $errors);
		}
		$count = count($names);
		for ($i = 0; $i < $count; $i++) {
			$one = array(
				'name' => $filesField['name'][$i] ?? '',
				'type' => $filesField['type'][$i] ?? '',
				'tmp_name' => $filesField['tmp_name'][$i] ?? '',
				'error' => $filesField['error'][$i] ?? UPLOAD_ERR_NO_FILE,
				'size' => $filesField['size'][$i] ?? 0,
			);
			if ((int) $one['error'] === UPLOAD_ERR_NO_FILE || (string) $one['tmp_name'] === '') {
				continue;
			}
			$res = epc_acc_photos_add($db, $listingId, $one, false);
			if (!empty($res['ok'])) {
				$ok++;
			} else {
				$failed++;
				$errors[] = (string) ($res['error'] ?? 'Upload failed');
			}
		}
		return array('ok' => $ok, 'failed' => $failed, 'errors' => $errors);
	}
}

if (!function_exists('epc_acc_slugify')) {
	function epc_acc_slugify(string $label): string
	{
		$s = strtolower(trim($label));
		$s = preg_replace('/[^a-z0-9]+/', '-', $s);
		$s = trim((string) $s, '-');
		return $s !== '' ? $s : 'item';
	}
}

if (!function_exists('epc_acc_uae_cities')) {
	/**
	 * Canonical UAE city / emirate list for accessories marketplace filters.
	 *
	 * @return list<string>
	 */
	function epc_acc_uae_cities(): array
	{
		$tax = epc_acc_load_taxonomy_json();
		$fromJson = array();
		foreach ((isset($tax['cities']) && is_array($tax['cities']) ? $tax['cities'] : array()) as $city) {
			$city = trim((string) $city);
			if ($city !== '') {
				$fromJson[] = $city;
			}
		}
		if ($fromJson) {
			return array_values(array_unique($fromJson));
		}
		return array(
			'Dubai',
			'Abu Dhabi',
			'Sharjah',
			'Ajman',
			'Ras Al Khaimah',
			'Fujairah',
			'Umm Al Quwain',
			'Al Ain',
		);
	}
}

if (!function_exists('epc_acc_legacy_pk_cities')) {
	/**
	 * Former Pakistan city list (replaced by UAE cities).
	 *
	 * @return list<string>
	 */
	function epc_acc_legacy_pk_cities(): array
	{
		return array(
			'Karachi',
			'Lahore',
			'Okara',
			'Islamabad',
			'Sialkot',
			'Mirpur Khas',
			'Rawalpindi',
			'Peshawar',
			'Faisalabad',
			'Gujranwala',
			'Multan',
			'Quetta',
			'Hyderabad',
			'Bahawalpur',
		);
	}
}

if (!function_exists('epc_acc_migrate_uae_locale')) {
	/**
	 * Switch accessories marketplace locale to UAE cities + AED currency.
	 * Idempotent: safe to call on every CP page load.
	 *
	 * @return array{cities_active:int, cities_deactivated:int, listings_city:int, listings_currency:int, titles:int}
	 */
	function epc_acc_migrate_uae_locale(PDO $db): array
	{
		static $done = null;
		if (is_array($done)) {
			return $done;
		}
		epc_acc_ensure_schema($db);
		$result = array(
			'cities_active' => 0,
			'cities_deactivated' => 0,
			'listings_city' => 0,
			'listings_currency' => 0,
			'titles' => 0,
		);

		$uae = epc_acc_uae_cities();
		$pk = epc_acc_legacy_pk_cities();
		$cityMap = array(
			'Karachi' => 'Dubai',
			'Lahore' => 'Abu Dhabi',
			'Islamabad' => 'Abu Dhabi',
			'Rawalpindi' => 'Al Ain',
			'Peshawar' => 'Sharjah',
			'Faisalabad' => 'Ajman',
			'Gujranwala' => 'Ajman',
			'Multan' => 'Ras Al Khaimah',
			'Quetta' => 'Fujairah',
			'Hyderabad' => 'Sharjah',
			'Bahawalpur' => 'Umm Al Quwain',
			'Sialkot' => 'Ras Al Khaimah',
			'Mirpur Khas' => 'Fujairah',
			'Okara' => 'Al Ain',
		);

		$upsert = $db->prepare(
			'INSERT INTO `epc_acc_terms` (`term_type`, `parent_id`, `value`, `label`, `sort_order`, `active`)
			VALUES (\'city\', 0, ?, ?, ?, 1)
			ON DUPLICATE KEY UPDATE `label` = VALUES(`label`), `sort_order` = VALUES(`sort_order`), `active` = 1'
		);
		$i = 0;
		foreach ($uae as $city) {
			$i++;
			$upsert->execute(array($city, $city, $i));
			$result['cities_active']++;
		}

		// Deactivate any city term that is not in the UAE list (covers legacy PK cities).
		$activeCities = $db->query("SELECT `id`, `value` FROM `epc_acc_terms` WHERE `term_type` = 'city' AND `active` = 1")->fetchAll(PDO::FETCH_ASSOC);
		$uaeLookup = array_fill_keys($uae, true);
		$deactivate = $db->prepare('UPDATE `epc_acc_terms` SET `active` = 0 WHERE `id` = ?');
		foreach ($activeCities as $row) {
			$val = trim((string) ($row['value'] ?? ''));
			if ($val !== '' && empty($uaeLookup[$val])) {
				$deactivate->execute(array((int) $row['id']));
				$result['cities_deactivated']++;
			}
		}
		// Also deactivate known PK values even if already inactive (no-op) — ensure value match case.
		$deactByValue = $db->prepare("UPDATE `epc_acc_terms` SET `active` = 0 WHERE `term_type` = 'city' AND `value` = ?");
		foreach ($pk as $oldCity) {
			$deactByValue->execute(array($oldCity));
		}

		$updCity = $db->prepare('UPDATE `epc_acc_listings` SET `city` = ?, `updated_at` = ? WHERE `city` = ?');
		$now = time();
		foreach ($cityMap as $from => $to) {
			$updCity->execute(array($to, $now, $from));
			$result['listings_city'] += $updCity->rowCount();
		}
		// Any remaining unknown non-UAE city → Dubai.
		$st = $db->query('SELECT DISTINCT `city` FROM `epc_acc_listings` WHERE TRIM(`city`) <> \'\'');
		while ($row = $st->fetch(PDO::FETCH_ASSOC)) {
			$city = trim((string) ($row['city'] ?? ''));
			if ($city === '' || isset($uaeLookup[$city])) {
				continue;
			}
			$updCity->execute(array('Dubai', $now, $city));
			$result['listings_city'] += $updCity->rowCount();
		}

		// Swap Pakistan city names embedded in listing titles.
		foreach ($cityMap as $from => $to) {
			$like = '%| ' . $from . '%';
			$sel = $db->prepare('SELECT `id`, `title` FROM `epc_acc_listings` WHERE `title` LIKE ?');
			$sel->execute(array($like));
			$fixTitle = $db->prepare('UPDATE `epc_acc_listings` SET `title` = ?, `updated_at` = ? WHERE `id` = ?');
			while ($row = $sel->fetch(PDO::FETCH_ASSOC)) {
				$title = str_replace('| ' . $from, '| ' . $to, (string) $row['title']);
				$title = str_replace($from, $to, $title);
				if ($title !== (string) $row['title']) {
					$fixTitle->execute(array($title, $now, (int) $row['id']));
					$result['titles']++;
				}
			}
		}

		$cur = $db->prepare("UPDATE `epc_acc_listings` SET `currency` = 'AED', `updated_at` = ? WHERE `currency` <> 'AED' OR `currency` = '' OR `currency` IS NULL");
		$cur->execute(array($now));
		$result['listings_currency'] = $cur->rowCount();

		$done = $result;
		return $result;
	}
}

if (!function_exists('epc_acc_seed_terms_from_json')) {
	/**
	 * @return array{makes:int, cities:int, conditions:int, years:int}
	 */
	function epc_acc_seed_terms_from_json(PDO $db): array
	{
		epc_acc_ensure_schema($db);
		$tax = epc_acc_load_taxonomy_json();
		$upsert = $db->prepare(
			'INSERT INTO `epc_acc_terms` (`term_type`, `parent_id`, `value`, `label`, `sort_order`, `active`)
			VALUES (?, 0, ?, ?, ?, 1)
			ON DUPLICATE KEY UPDATE `label` = VALUES(`label`), `sort_order` = VALUES(`sort_order`)'
		);
		$counts = array('makes' => 0, 'cities' => 0, 'conditions' => 0, 'years' => 0);
		$i = 0;
		foreach ((isset($tax['makes']) && is_array($tax['makes']) ? $tax['makes'] : array()) as $make) {
			$make = trim((string) $make);
			if ($make === '') {
				continue;
			}
			$i++;
			$upsert->execute(array('make', $make, $make, $i));
			$counts['makes']++;
		}
		$i = 0;
		foreach ((isset($tax['cities']) && is_array($tax['cities']) ? $tax['cities'] : array()) as $city) {
			$city = trim((string) $city);
			if ($city === '') {
				continue;
			}
			$i++;
			$upsert->execute(array('city', $city, $city, $i));
			$counts['cities']++;
		}
		$i = 0;
		foreach (array('New' => 'new', 'Used' => 'used') as $label => $value) {
			$i++;
			$upsert->execute(array('condition', $value, $label, $i));
			$counts['conditions']++;
		}
		$yearNow = (int) date('Y');
		$i = 0;
		for ($y = $yearNow; $y >= $yearNow - 30; $y--) {
			$i++;
			$upsert->execute(array('year', (string) $y, (string) $y, $i));
			$counts['years']++;
		}
		return $counts;
	}
}

if (!function_exists('epc_acc_get_terms')) {
	/**
	 * @return list<array{id:int, term_type:string, parent_id:int, value:string, label:string, sort_order:int, active:int}>
	 */
	function epc_acc_get_terms(PDO $db, string $type, bool $includeInactive = false, int $parentId = -1): array
	{
		epc_acc_ensure_schema($db);
		$type = preg_replace('/[^a-z_]/', '', strtolower($type));
		$sql = 'SELECT `id`, `term_type`, `parent_id`, `value`, `label`, `sort_order`, `active`
			FROM `epc_acc_terms` WHERE `term_type` = ?';
		$bind = array($type);
		if (!$includeInactive) {
			$sql .= ' AND `active` = 1';
		}
		if ($parentId >= 0) {
			$sql .= ' AND `parent_id` = ?';
			$bind[] = $parentId;
		}
		$sql .= ' ORDER BY `sort_order` ASC, `label` ASC';
		$st = $db->prepare($sql);
		$st->execute($bind);
		$rows = $st->fetchAll(PDO::FETCH_ASSOC);
		if (!$rows && in_array($type, array('make', 'city', 'condition', 'year'), true)) {
			epc_acc_seed_terms_from_json($db);
			$st->execute($bind);
			$rows = $st->fetchAll(PDO::FETCH_ASSOC);
		}
		$out = array();
		foreach ($rows as $row) {
			$out[] = array(
				'id' => (int) $row['id'],
				'term_type' => (string) $row['term_type'],
				'parent_id' => (int) $row['parent_id'],
				'value' => (string) $row['value'],
				'label' => (string) $row['label'],
				'sort_order' => (int) $row['sort_order'],
				'active' => (int) $row['active'],
			);
		}
		return $out;
	}
}

if (!function_exists('epc_acc_term_labels')) {
	/** @return list<string> */
	function epc_acc_term_labels(PDO $db, string $type): array
	{
		$labels = array();
		foreach (epc_acc_get_terms($db, $type, false) as $term) {
			$labels[] = $term['label'];
		}
		return $labels;
	}
}

if (!function_exists('epc_acc_save_term')) {
	function epc_acc_save_term(PDO $db, string $type, string $label, int $id = 0, int $parentId = 0, int $sortOrder = 0): int
	{
		epc_acc_ensure_schema($db);
		$type = preg_replace('/[^a-z_]/', '', strtolower($type));
		$label = trim($label);
		if ($label === '' || $type === '') {
			return 0;
		}
		$value = ($type === 'condition') ? epc_acc_slugify($label) : $label;
		if ($type === 'condition' && !in_array($value, array('new', 'used', 'refurbished'), true)) {
			$value = epc_acc_slugify($label);
		}
		if ($sortOrder <= 0) {
			$mx = $db->prepare('SELECT COALESCE(MAX(`sort_order`), 0) + 1 FROM `epc_acc_terms` WHERE `term_type` = ? AND `parent_id` = ?');
			$mx->execute(array($type, $parentId));
			$sortOrder = (int) $mx->fetchColumn();
		}
		if ($id > 0) {
			$st = $db->prepare(
				'UPDATE `epc_acc_terms` SET `label` = ?, `value` = ?, `parent_id` = ?, `sort_order` = ?, `active` = 1 WHERE `id` = ? AND `term_type` = ?'
			);
			$st->execute(array($label, $value, $parentId, $sortOrder, $id, $type));
			return $id;
		}
		$st = $db->prepare(
			'INSERT INTO `epc_acc_terms` (`term_type`, `parent_id`, `value`, `label`, `sort_order`, `active`)
			VALUES (?, ?, ?, ?, ?, 1)
			ON DUPLICATE KEY UPDATE `label` = VALUES(`label`), `sort_order` = VALUES(`sort_order`), `active` = 1, `id` = LAST_INSERT_ID(`id`)'
		);
		$st->execute(array($type, $parentId, $value, $label, $sortOrder));
		return (int) $db->lastInsertId();
	}
}

if (!function_exists('epc_acc_set_term_active')) {
	function epc_acc_set_term_active(PDO $db, int $id, bool $active): bool
	{
		epc_acc_ensure_schema($db);
		$st = $db->prepare('UPDATE `epc_acc_terms` SET `active` = ? WHERE `id` = ?');
		$st->execute(array($active ? 1 : 0, $id));
		return $st->rowCount() > 0;
	}
}

if (!function_exists('epc_acc_delete_term')) {
	function epc_acc_delete_term(PDO $db, int $id): bool
	{
		epc_acc_ensure_schema($db);
		$db->prepare('DELETE FROM `epc_acc_terms` WHERE `parent_id` = ?')->execute(array($id));
		$st = $db->prepare('DELETE FROM `epc_acc_terms` WHERE `id` = ?');
		$st->execute(array($id));
		return $st->rowCount() > 0;
	}
}

if (!function_exists('epc_acc_admin_category_tree')) {
	/** Full category tree including inactive (for CP management). */
	function epc_acc_admin_category_tree(PDO $db): array
	{
		epc_acc_ensure_schema($db);
		$rows = $db->query(
			'SELECT `id`, `parent_id`, `slug`, `label`, `pw_id`, `sort_order`, `active`
			FROM `epc_acc_categories` ORDER BY `parent_id` ASC, `sort_order` ASC, `label` ASC'
		)->fetchAll(PDO::FETCH_ASSOC);
		if (!$rows) {
			epc_acc_seed_categories_from_json($db, false);
			$rows = $db->query(
				'SELECT `id`, `parent_id`, `slug`, `label`, `pw_id`, `sort_order`, `active`
				FROM `epc_acc_categories` ORDER BY `parent_id` ASC, `sort_order` ASC, `label` ASC'
			)->fetchAll(PDO::FETCH_ASSOC);
		}
		$parents = array();
		$children = array();
		foreach ($rows as $row) {
			if ((int) $row['parent_id'] === 0) {
				$parents[(int) $row['id']] = array(
					'id' => (int) $row['id'],
					'slug' => $row['slug'],
					'label' => $row['label'],
					'pw_id' => (int) $row['pw_id'],
					'sort_order' => (int) $row['sort_order'],
					'active' => (int) $row['active'],
					'children' => array(),
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
				'sort_order' => (int) $row['sort_order'],
				'active' => (int) $row['active'],
				'parent_id' => $pid,
			);
		}
		return array_values($parents);
	}
}

if (!function_exists('epc_acc_save_category')) {
	function epc_acc_save_category(PDO $db, string $label, int $parentId = 0, int $id = 0, int $sortOrder = 0): int
	{
		epc_acc_ensure_schema($db);
		$label = trim($label);
		if ($label === '') {
			return 0;
		}
		$slug = epc_acc_slugify($label);
		if ($sortOrder <= 0) {
			$mx = $db->prepare('SELECT COALESCE(MAX(`sort_order`), 0) + 1 FROM `epc_acc_categories` WHERE `parent_id` = ?');
			$mx->execute(array($parentId));
			$sortOrder = (int) $mx->fetchColumn();
		}
		if ($id > 0) {
			$st = $db->prepare(
				'UPDATE `epc_acc_categories` SET `label` = ?, `slug` = ?, `parent_id` = ?, `sort_order` = ?, `active` = 1 WHERE `id` = ?'
			);
			$st->execute(array($label, $slug, $parentId, $sortOrder, $id));
			return $id;
		}
		// Unique slug under parent — append suffix if needed.
		$base = $slug;
		$n = 1;
		while (true) {
			$chk = $db->prepare('SELECT `id` FROM `epc_acc_categories` WHERE `parent_id` = ? AND `slug` = ? LIMIT 1');
			$chk->execute(array($parentId, $slug));
			$existing = (int) $chk->fetchColumn();
			if ($existing < 1) {
				break;
			}
			$n++;
			$slug = $base . '-' . $n;
		}
		$st = $db->prepare(
			'INSERT INTO `epc_acc_categories` (`parent_id`, `slug`, `label`, `pw_id`, `sort_order`, `active`)
			VALUES (?, ?, ?, 0, ?, 1)'
		);
		$st->execute(array($parentId, $slug, $label, $sortOrder));
		return (int) $db->lastInsertId();
	}
}

if (!function_exists('epc_acc_set_category_active')) {
	function epc_acc_set_category_active(PDO $db, int $id, bool $active): bool
	{
		epc_acc_ensure_schema($db);
		$st = $db->prepare('UPDATE `epc_acc_categories` SET `active` = ? WHERE `id` = ?');
		$st->execute(array($active ? 1 : 0, $id));
		if (!$active) {
			$db->prepare('UPDATE `epc_acc_categories` SET `active` = 0 WHERE `parent_id` = ?')->execute(array($id));
		}
		return true;
	}
}

if (!function_exists('epc_acc_delete_category')) {
	/**
	 * @return array{ok:bool, message:string}
	 */
	function epc_acc_delete_category(PDO $db, int $id): array
	{
		epc_acc_ensure_schema($db);
		if ($id < 1) {
			return array('ok' => false, 'message' => 'Invalid category');
		}
		$used = $db->prepare(
			'SELECT COUNT(*) FROM `epc_acc_listings` WHERE `category_id` = ? OR `subcategory_id` = ?'
		);
		$used->execute(array($id, $id));
		if ((int) $used->fetchColumn() > 0) {
			return array('ok' => false, 'message' => 'Category has listings — deactivate instead of delete');
		}
		$childUsed = $db->prepare(
			'SELECT COUNT(*) FROM `epc_acc_listings` l
			 INNER JOIN `epc_acc_categories` c ON c.id = l.subcategory_id
			 WHERE c.parent_id = ?'
		);
		$childUsed->execute(array($id));
		if ((int) $childUsed->fetchColumn() > 0) {
			return array('ok' => false, 'message' => 'Sub-categories have listings — deactivate instead');
		}
		$db->prepare('DELETE FROM `epc_acc_categories` WHERE `parent_id` = ?')->execute(array($id));
		$db->prepare('DELETE FROM `epc_acc_categories` WHERE `id` = ?')->execute(array($id));
		return array('ok' => true, 'message' => 'Category deleted');
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
			(`category_id`, `subcategory_id`, `title`, `description`, `make`, `model`, `year`, `city`, `condition_type`,
			 `price`, `compare_price`, `currency`, `image_url`, `external_url`, `photo_count`, `featured`,
			 `stock_qty`, `status`, `created_at`, `updated_at`)
			VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)'
		);
		$stmt->execute(array(
			(int) ($data['category_id'] ?? 0),
			(int) ($data['subcategory_id'] ?? 0),
			trim((string) ($data['title'] ?? '')),
			trim((string) ($data['description'] ?? '')),
			trim((string) ($data['make'] ?? '')),
			trim((string) ($data['model'] ?? '')),
			trim((string) ($data['year'] ?? '')),
			trim((string) ($data['city'] ?? '')),
			trim((string) ($data['condition_type'] ?? 'new')),
			(float) ($data['price'] ?? 0),
			(float) ($data['compare_price'] ?? 0),
			trim((string) ($data['currency'] ?? 'AED')) ?: 'AED',
			trim((string) ($data['image_url'] ?? '')),
			trim((string) ($data['external_url'] ?? '')),
			max(1, (int) ($data['photo_count'] ?? 1)),
			!empty($data['featured']) ? 1 : 0,
			(int) ($data['stock_qty'] ?? 0),
			trim((string) ($data['status'] ?? 'published')) ?: 'published',
			$now,
			$now,
		));
		return (int) $db->lastInsertId();
	}
}

if (!function_exists('epc_acc_get_listing')) {
	/**
	 * @return array<string, mixed>|null
	 */
	function epc_acc_get_listing(PDO $db, int $id): ?array
	{
		epc_acc_ensure_schema($db);
		$stmt = $db->prepare(
			'SELECT l.*, c.slug AS category_slug, c.label AS category_label,
				s.slug AS subcategory_slug, s.label AS subcategory_label
			FROM `epc_acc_listings` l
			LEFT JOIN `epc_acc_categories` c ON c.id = l.category_id
			LEFT JOIN `epc_acc_categories` s ON s.id = l.subcategory_id
			WHERE l.id = ? LIMIT 1'
		);
		$stmt->execute(array($id));
		$row = $stmt->fetch(PDO::FETCH_ASSOC);
		return $row ? $row : null;
	}
}

if (!function_exists('epc_acc_update_listing')) {
	/**
	 * @param array<string, mixed> $data
	 */
	function epc_acc_update_listing(PDO $db, int $id, array $data): bool
	{
		epc_acc_ensure_schema($db);
		$stmt = $db->prepare(
			'UPDATE `epc_acc_listings` SET
				`category_id` = ?, `subcategory_id` = ?, `title` = ?, `description` = ?,
				`make` = ?, `model` = ?, `year` = ?, `city` = ?, `condition_type` = ?,
				`price` = ?, `compare_price` = ?, `currency` = ?, `image_url` = ?, `external_url` = ?,
				`photo_count` = ?, `featured` = ?, `stock_qty` = ?, `status` = ?, `updated_at` = ?
			WHERE `id` = ?'
		);
		return $stmt->execute(array(
			(int) ($data['category_id'] ?? 0),
			(int) ($data['subcategory_id'] ?? 0),
			trim((string) ($data['title'] ?? '')),
			trim((string) ($data['description'] ?? '')),
			trim((string) ($data['make'] ?? '')),
			trim((string) ($data['model'] ?? '')),
			trim((string) ($data['year'] ?? '')),
			trim((string) ($data['city'] ?? '')),
			trim((string) ($data['condition_type'] ?? 'new')),
			(float) ($data['price'] ?? 0),
			(float) ($data['compare_price'] ?? 0),
			trim((string) ($data['currency'] ?? 'AED')) ?: 'AED',
			trim((string) ($data['image_url'] ?? '')),
			trim((string) ($data['external_url'] ?? '')),
			max(1, (int) ($data['photo_count'] ?? 1)),
			!empty($data['featured']) ? 1 : 0,
			(int) ($data['stock_qty'] ?? 0),
			trim((string) ($data['status'] ?? 'published')) ?: 'published',
			time(),
			$id,
		));
	}
}

if (!function_exists('epc_acc_set_listing_status')) {
	function epc_acc_set_listing_status(PDO $db, int $id, string $status): bool
	{
		epc_acc_ensure_schema($db);
		$status = trim($status);
		if ($status === '') {
			$status = 'draft';
		}
		$stmt = $db->prepare('UPDATE `epc_acc_listings` SET `status` = ?, `updated_at` = ? WHERE `id` = ?');
		return $stmt->execute(array($status, time(), $id));
	}
}

if (!function_exists('epc_acc_delete_listing')) {
	function epc_acc_delete_listing(PDO $db, int $id): bool
	{
		epc_acc_ensure_schema($db);
		if ($id > 0) {
			try {
				$photos = epc_acc_photos_list($db, $id);
				foreach ($photos as $p) {
					$file = basename((string) ($p['file_name'] ?? ''));
					if ($file !== '') {
						$path = epc_acc_photos_fs_dir() . $file;
						if (is_file($path)) {
							@unlink($path);
						}
					}
				}
				$db->prepare('DELETE FROM `epc_acc_photos` WHERE `listing_id` = ?')->execute(array($id));
			} catch (Throwable $e) {
			}
		}
		$stmt = $db->prepare('DELETE FROM `epc_acc_listings` WHERE `id` = ?');
		return $stmt->execute(array($id));
	}
}

if (!function_exists('epc_acc_admin_search')) {
	/**
	 * Admin list — includes drafts/unpublished.
	 *
	 * @param array<string, mixed> $filters
	 * @return array<string, mixed>
	 */
	function epc_acc_admin_search(PDO $db, array $filters = array()): array
	{
		epc_acc_ensure_schema($db);
		$tree = epc_acc_get_category_tree($db);
		$q = trim((string) ($filters['q'] ?? ''));
		$category = trim((string) ($filters['category'] ?? ''));
		$subcategory = trim((string) ($filters['subcategory'] ?? ''));
		$status = trim((string) ($filters['status'] ?? ''));
		$make = trim((string) ($filters['make'] ?? ''));
		$page = max(1, (int) ($filters['page'] ?? 1));
		$perPage = max(10, min(100, (int) ($filters['per_page'] ?? 50)));

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

		$where = array('1=1');
		$bind = array();
		if ($categoryId > 0) {
			$where[] = 'l.`category_id` = ?';
			$bind[] = $categoryId;
		}
		if ($subcategoryId > 0) {
			$where[] = 'l.`subcategory_id` = ?';
			$bind[] = $subcategoryId;
		}
		if ($status !== '') {
			$where[] = 'l.`status` = ?';
			$bind[] = $status;
		}
		if ($make !== '') {
			$where[] = 'l.`make` = ?';
			$bind[] = $make;
		}
		if ($q !== '') {
			$where[] = '(l.`title` LIKE ? OR l.`make` LIKE ? OR l.`model` LIKE ? OR l.`city` LIKE ? OR l.`id` = ?)';
			$like = '%' . $q . '%';
			array_push($bind, $like, $like, $like, $like, (int) $q);
		}
		$whereSql = implode(' AND ', $where);

		$countStmt = $db->prepare('SELECT COUNT(*) FROM `epc_acc_listings` l WHERE ' . $whereSql);
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
			ORDER BY l.`updated_at` DESC, l.`id` DESC
			LIMIT ' . (int) $perPage . ' OFFSET ' . (int) $offset
		);
		$listStmt->execute($bind);
		$items = $listStmt->fetchAll(PDO::FETCH_ASSOC);

		$statusCounts = array();
		try {
			foreach ($db->query('SELECT `status`, COUNT(*) AS cnt FROM `epc_acc_listings` GROUP BY `status`') as $row) {
				$statusCounts[(string) $row['status']] = (int) $row['cnt'];
			}
		} catch (Exception $e) {
			$statusCounts = array();
		}

		return array(
			'total' => $total,
			'page' => $page,
			'pages' => $pages,
			'per_page' => $perPage,
			'items' => $items,
			'tree' => $tree,
			'status_counts' => $statusCounts,
		);
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
		$listingId = (int) ($filters['id'] ?? $filters['listing_id'] ?? 0);
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

		$where = array("l.`status` = 'published'");
		$bind = array();
		// Deep-link: focus one listing (CP "View on storefront").
		if ($listingId > 0) {
			$where[] = 'l.`id` = ?';
			$bind[] = $listingId;
		}
		if ($listingId < 1) {
			if ($categoryId > 0) {
				$where[] = 'l.`category_id` = ?';
				$bind[] = $categoryId;
			}
			if ($subcategoryId > 0) {
				$where[] = 'l.`subcategory_id` = ?';
				$bind[] = $subcategoryId;
			}
			if ($make !== '') {
				$where[] = 'l.`make` = ?';
				$bind[] = $make;
			}
			if ($model !== '') {
				$where[] = 'l.`model` LIKE ?';
				$bind[] = '%' . $model . '%';
			}
			if ($city !== '') {
				$where[] = 'l.`city` = ?';
				$bind[] = $city;
			}
			if ($condition !== '') {
				$where[] = 'l.`condition_type` = ?';
				$bind[] = strtolower($condition);
			}
			if ($priceMin > 0) {
				$where[] = 'l.`price` >= ?';
				$bind[] = $priceMin;
			}
			if ($priceMax > 0) {
				$where[] = 'l.`price` <= ?';
				$bind[] = $priceMax;
			}
			if ($q !== '') {
				$where[] = '(l.`title` LIKE ? OR l.`description` LIKE ? OR l.`make` LIKE ? OR l.`model` LIKE ?)';
				$like = '%' . $q . '%';
				array_push($bind, $like, $like, $like, $like);
			}
		}
		$whereSql = implode(' AND ', $where);

		switch ($sort) {
			case 'price-asc':
				$orderSql = 'l.`featured` DESC, l.`price` ASC, l.`updated_at` DESC';
				break;
			case 'price-desc':
				$orderSql = 'l.`featured` DESC, l.`price` DESC, l.`updated_at` DESC';
				break;
			case 'updated-asc':
				$orderSql = 'l.`featured` DESC, l.`updated_at` ASC';
				break;
			case 'top-sales':
				$orderSql = 'l.`featured` DESC, l.`stock_qty` DESC, l.`updated_at` DESC';
				break;
			case 'updated-desc':
			default:
				$orderSql = 'l.`featured` DESC, l.`updated_at` DESC, l.`id` DESC';
				break;
		}

		$countStmt = $db->prepare('SELECT COUNT(*) FROM `epc_acc_listings` l WHERE ' . $whereSql);
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
			$id = (int) $row['id'];
			$photos = epc_acc_photos_list($db, $id);
			$photoUrls = array();
			foreach ($photos as $ph) {
				$url = trim((string) ($ph['url'] ?? ''));
				if ($url !== '') {
					$photoUrls[] = array(
						'id' => (int) ($ph['id'] ?? 0),
						'url' => $url,
						'is_primary' => !empty($ph['is_primary']),
					);
				}
			}
			$imageUrl = trim((string) ($row['image_url'] ?? ''));
			if ($imageUrl === '' && $photoUrls !== array()) {
				$imageUrl = (string) $photoUrls[0]['url'];
				foreach ($photoUrls as $pu) {
					if (!empty($pu['is_primary'])) {
						$imageUrl = (string) $pu['url'];
						break;
					}
				}
			}
			$photoCount = max(count($photoUrls), (int) ($row['photo_count'] ?? 0), $imageUrl !== '' ? 1 : 0);
			$detailUrl = epc_acc_storefront_url(array(
				'id' => $id,
				'category' => (string) ($row['category_slug'] ?? ''),
				'subcategory' => (string) ($row['subcategory_slug'] ?? ''),
			));
			$rawExternal = trim((string) ($row['external_url'] ?? ''));
			$outbound = epc_acc_is_outbound_external_url($rawExternal) ? $rawExternal : '';
			$items[] = array(
				'id' => $id,
				'title' => $row['title'],
				'description' => $row['description'],
				'make' => $row['make'],
				'model' => $row['model'],
				'year' => isset($row['year']) ? (string) $row['year'] : '',
				'city' => $row['city'],
				'condition' => $row['condition_type'],
				'price' => (float) $row['price'],
				'compare_price' => isset($row['compare_price']) ? (float) $row['compare_price'] : 0,
				'currency' => $row['currency'],
				'image_url' => $imageUrl,
				'photos' => $photoUrls,
				'external_url' => $outbound,
				'detail_url' => $detailUrl,
				'url' => $detailUrl,
				'photo_count' => max(1, $photoCount),
				'featured' => !empty($row['featured']),
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

		$makesTax = epc_acc_term_labels($db, 'make');
		if (!$makesTax && isset($tax['makes']) && is_array($tax['makes'])) {
			$makesTax = $tax['makes'];
		}
		$citiesTax = epc_acc_term_labels($db, 'city');
		if (!$citiesTax && isset($tax['cities']) && is_array($tax['cities'])) {
			$citiesTax = $tax['cities'];
		}
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
		$facetConditions = array();
		foreach (epc_acc_get_terms($db, 'condition', false) as $cond) {
			$facetConditions[] = array('value' => $cond['value'], 'label' => $cond['label']);
		}
		if (!$facetConditions) {
			$facetConditions = array(
				array('value' => 'new', 'label' => 'New'),
				array('value' => 'used', 'label' => 'Used'),
			);
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
				'conditions' => $facetConditions,
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
