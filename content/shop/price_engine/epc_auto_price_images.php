<?php
/**
 * Auto Price AI — download discovery product photos into tenant-scoped local storage.
 * MVP: og:image + gallery URLs from queue; respect size/type limits; fallback to external URL.
 */
defined('_ASTEXE_') or die('No access');

const EPC_APE_IMAGE_USER_AGENT = 'ECOM-AutoPrice-Crawler/1.0 (+https://www.ecomae.com; product-discovery)';
const EPC_APE_IMAGE_MAX_BYTES = 5242880;
const EPC_APE_IMAGE_MAX_COUNT = 5;

/**
 * Web URL for a shop_products_images.file_name value (legacy flat name or auto_price/ path).
 */
function epc_product_image_url(string $fileName): string
{
	global $db_link;
	if (is_file($_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_epartscart_storefront.php')) {
		require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_epartscart_storefront.php';
		if (isset($db_link) && $db_link instanceof PDO && function_exists('epc_epartscart_use_neutral_product_image') && epc_epartscart_use_neutral_product_image($db_link)) {
			return epc_epartscart_catalog_placeholder_url($db_link);
		}
	}
	$fileName = trim($fileName);
	if ($fileName === '') {
		return '/content/files/images/no_image.png';
	}
	if (preg_match('#^https?://#i', $fileName)) {
		return $fileName;
	}
	if (strpos($fileName, '/content/files/images/') === 0) {
		return $fileName;
	}
	if (strpos($fileName, 'auto_price/') === 0) {
		return '/content/files/images/' . $fileName;
	}
	return '/content/files/images/products_images/' . $fileName;
}

/**
 * Absolute filesystem path for a catalogue image file_name.
 */
function epc_product_image_fs_path(string $fileName): string
{
	$url = epc_product_image_url($fileName);
	if (preg_match('#^https?://#i', $url)) {
		return '';
	}
	$docRoot = rtrim((string) ($_SERVER['DOCUMENT_ROOT'] ?? ''), '/\\');
	return $docRoot . $url;
}

/**
 * @return array{ok:bool,local_paths:array,file_names:array,imported:int,failed:int,warnings:array,fallback_urls:array}
 */
function epc_auto_price_import_images(PDO $pdo, string $siteKey, int $productId, array $imageUrls, array $options = array()): array
{
	$siteKey = preg_replace('/[^a-z0-9_]/', '', strtolower(trim($siteKey)));
	$productId = max(0, $productId);
	$replaceExisting = !empty($options['replace_existing']);
	$maxCount = max(1, min(EPC_APE_IMAGE_MAX_COUNT, (int) ($options['max_count'] ?? EPC_APE_IMAGE_MAX_COUNT)));

	$result = array(
		'ok' => false,
		'local_paths' => array(),
		'file_names' => array(),
		'imported' => 0,
		'failed' => 0,
		'warnings' => array(),
		'fallback_urls' => array(),
	);

	if ($productId <= 0 || $siteKey === '') {
		$result['warnings'][] = 'Invalid product or site key';
		return $result;
	}

	$urls = epc_ape_image_normalize_urls($imageUrls, $maxCount);
	if (!$urls) {
		$result['warnings'][] = 'No valid image URLs to download';
		return $result;
	}

	$docRoot = rtrim((string) ($_SERVER['DOCUMENT_ROOT'] ?? ''), '/\\');
	$relDir = 'auto_price/' . $siteKey . '/' . $productId;
	$absDir = $docRoot . '/content/files/images/' . $relDir;
	if (!is_dir($absDir) && !@mkdir($absDir, 0755, true) && !is_dir($absDir)) {
		$result['warnings'][] = 'Could not create image directory';
		foreach ($urls as $url) {
			$result['fallback_urls'][] = $url;
			$result['failed']++;
		}
		return $result;
	}

	if ($replaceExisting) {
		epc_ape_image_clear_product_images($pdo, $productId, $siteKey);
	}

	$index = 0;
	foreach ($urls as $url) {
		$index++;
		$dl = epc_ape_image_download($url);
		if (empty($dl['ok'])) {
			$result['failed']++;
			$result['fallback_urls'][] = $url;
			$result['warnings'][] = 'Download failed: ' . $url . ' — ' . ($dl['message'] ?? 'unknown');
			continue;
		}

		$baseName = sprintf('%03d_%d.%s', $index, time(), $dl['ext']);
		$absPath = $absDir . '/' . $baseName;
		if (@file_put_contents($absPath, $dl['data']) === false) {
			$result['failed']++;
			$result['fallback_urls'][] = $url;
			$result['warnings'][] = 'Could not save: ' . $baseName;
			continue;
		}

		$webPath = '/content/files/images/' . $relDir . '/' . $baseName;
		$fileName = $relDir . '/' . $baseName;

		epc_ape_image_make_thumb($absPath, $absDir . '/thumb_' . $baseName);

		try {
			$pdo->prepare('INSERT INTO `shop_products_images` (`product_id`, `file_name`) VALUES (?, ?)')
				->execute(array($productId, $fileName));
		} catch (Throwable $e) {
			@unlink($absPath);
			$result['failed']++;
			$result['fallback_urls'][] = $url;
			$result['warnings'][] = 'DB insert failed for ' . $baseName;
			continue;
		}

		$result['local_paths'][] = $webPath;
		$result['file_names'][] = $fileName;
		$result['imported']++;
	}

	$result['ok'] = $result['imported'] > 0;
	return $result;
}

/**
 * @return array<int,string>
 */
function epc_ape_image_normalize_urls(array $imageUrls, int $maxCount = EPC_APE_IMAGE_MAX_COUNT): array
{
	$out = array();
	$seen = array();
	foreach ($imageUrls as $raw) {
		$url = trim((string) $raw);
		if ($url === '' || isset($seen[$url])) {
			continue;
		}
		if (preg_match('#^data:#i', $url)) {
			continue;
		}
		if (!preg_match('#^https?://#i', $url)) {
			continue;
		}
		$seen[$url] = true;
		$out[] = $url;
		if (count($out) >= $maxCount) {
			break;
		}
	}
	return $out;
}

/**
 * @return array{ok:bool,data:string,ext:string,message:string}
 */
function epc_ape_image_download(string $url): array
{
	$url = trim($url);
	if ($url === '' || preg_match('#^data:#i', $url) || !preg_match('#^https?://#i', $url)) {
		return array('ok' => false, 'data' => '', 'ext' => '', 'message' => 'Invalid URL');
	}

	$data = '';
	$contentType = '';
	if (function_exists('curl_init')) {
		$ch = curl_init($url);
		curl_setopt_array($ch, array(
			CURLOPT_RETURNTRANSFER => true,
			CURLOPT_FOLLOWLOCATION => true,
			CURLOPT_MAXREDIRS => 5,
			CURLOPT_TIMEOUT => 25,
			CURLOPT_CONNECTTIMEOUT => 10,
			CURLOPT_USERAGENT => EPC_APE_IMAGE_USER_AGENT,
			CURLOPT_SSL_VERIFYPEER => true,
			CURLOPT_ENCODING => '',
		));
		$data = (string) curl_exec($ch);
		$contentType = (string) curl_getinfo($ch, CURLINFO_CONTENT_TYPE);
		curl_close($ch);
	}
	if ($data === '') {
		$ctx = stream_context_create(array(
			'http' => array(
				'timeout' => 20,
				'user_agent' => EPC_APE_IMAGE_USER_AGENT,
				'follow_location' => 1,
			),
			'ssl' => array('verify_peer' => true, 'verify_peer_name' => true),
		));
		$data = @file_get_contents($url, false, $ctx) ?: '';
	}

	if ($data === '' || strlen($data) > EPC_APE_IMAGE_MAX_BYTES) {
		return array('ok' => false, 'data' => '', 'ext' => '', 'message' => $data === '' ? 'Empty response' : 'File exceeds 5MB limit');
	}

	$detected = 0;
	$tmp = @tempnam(sys_get_temp_dir(), 'epcimg');
	if ($tmp && @file_put_contents($tmp, $data) !== false) {
		$detected = @exif_imagetype($tmp) ?: 0;
		@unlink($tmp);
	}
	$allowed = array(IMAGETYPE_JPEG, IMAGETYPE_PNG, IMAGETYPE_WEBP);
	if (!in_array($detected, $allowed, true)) {
		$detected = epc_ape_image_guess_type($data, $contentType, $url);
	}
	if (!in_array($detected, $allowed, true)) {
		return array('ok' => false, 'data' => '', 'ext' => '', 'message' => 'Unsupported image type (jpeg/png/webp only)');
	}

	$extMap = array(IMAGETYPE_JPEG => 'jpg', IMAGETYPE_PNG => 'png', IMAGETYPE_WEBP => 'webp');
	return array('ok' => true, 'data' => $data, 'ext' => $extMap[$detected], 'message' => 'OK');
}

function epc_ape_image_guess_type(string $data, string $contentType, string $url): int
{
	$ct = strtolower($contentType);
	if (strpos($ct, 'jpeg') !== false || strpos($ct, 'jpg') !== false) {
		return IMAGETYPE_JPEG;
	}
	if (strpos($ct, 'png') !== false) {
		return IMAGETYPE_PNG;
	}
	if (strpos($ct, 'webp') !== false) {
		return IMAGETYPE_WEBP;
	}
	$path = strtolower(parse_url($url, PHP_URL_PATH) ?: '');
	if (preg_match('/\.(jpe?g)$/', $path)) {
		return IMAGETYPE_JPEG;
	}
	if (preg_match('/\.png$/', $path)) {
		return IMAGETYPE_PNG;
	}
	if (preg_match('/\.webp$/', $path)) {
		return IMAGETYPE_WEBP;
	}
	if (strncmp($data, "\xFF\xD8\xFF", 3) === 0) {
		return IMAGETYPE_JPEG;
	}
	if (strncmp($data, "\x89PNG\r\n\x1a\n", 8) === 0) {
		return IMAGETYPE_PNG;
	}
	if (strncmp($data, 'RIFF', 4) === 0 && substr($data, 8, 4) === 'WEBP') {
		return IMAGETYPE_WEBP;
	}
	return 0;
}

function epc_ape_image_make_thumb(string $sourcePath, string $thumbPath, int $maxW = 320, int $maxH = 320): bool
{
	if (!is_file($sourcePath) || !function_exists('imagecreatetruecolor')) {
		return false;
	}
	$info = @getimagesize($sourcePath);
	if (!$info) {
		return false;
	}
	$srcW = (int) $info[0];
	$srcH = (int) $info[1];
	$type = (int) ($info[2] ?? 0);
	if ($srcW <= 0 || $srcH <= 0) {
		return false;
	}

	$scale = min($maxW / $srcW, $maxH / $srcH, 1);
	$dstW = max(1, (int) round($srcW * $scale));
	$dstH = max(1, (int) round($srcH * $scale));

	switch ($type) {
		case IMAGETYPE_JPEG:
			$src = @imagecreatefromjpeg($sourcePath);
			break;
		case IMAGETYPE_PNG:
			$src = @imagecreatefrompng($sourcePath);
			break;
		case IMAGETYPE_WEBP:
			$src = function_exists('imagecreatefromwebp') ? @imagecreatefromwebp($sourcePath) : false;
			break;
		default:
			return false;
	}
	if (!$src) {
		return false;
	}

	$dst = imagecreatetruecolor($dstW, $dstH);
	if ($type === IMAGETYPE_PNG || $type === IMAGETYPE_WEBP) {
		imagealphablending($dst, false);
		imagesavealpha($dst, true);
	}
	imagecopyresampled($dst, $src, 0, 0, 0, 0, $dstW, $dstH, $srcW, $srcH);
	$ok = false;
	if ($type === IMAGETYPE_JPEG) {
		$ok = @imagejpeg($dst, $thumbPath, 82);
	} elseif ($type === IMAGETYPE_PNG) {
		$ok = @imagepng($dst, $thumbPath, 6);
	} elseif ($type === IMAGETYPE_WEBP && function_exists('imagewebp')) {
		$ok = @imagewebp($dst, $thumbPath, 82);
	}
	imagedestroy($src);
	imagedestroy($dst);
	return (bool) $ok;
}

function epc_ape_image_clear_product_images(PDO $pdo, int $productId, string $siteKey): void
{
	$siteKey = preg_replace('/[^a-z0-9_]/', '', strtolower(trim($siteKey)));
	$prefix = 'auto_price/' . $siteKey . '/' . $productId . '/';
	$stmt = $pdo->prepare('SELECT `id`, `file_name` FROM `shop_products_images` WHERE `product_id` = ?');
	$stmt->execute(array($productId));
	foreach ($stmt->fetchAll(PDO::FETCH_ASSOC) ?: array() as $row) {
		$fileName = (string) ($row['file_name'] ?? '');
		if (strpos($fileName, $prefix) !== 0 && strpos($fileName, 'auto_price/' . $siteKey . '/') !== 0) {
			continue;
		}
		$fs = epc_product_image_fs_path($fileName);
		if ($fs !== '' && is_file($fs)) {
			@unlink($fs);
		}
		$thumb = dirname($fs) . '/thumb_' . basename($fs);
		if (is_file($thumb)) {
			@unlink($thumb);
		}
		$pdo->prepare('DELETE FROM `shop_products_images` WHERE `id` = ?')->execute(array((int) $row['id']));
	}
}

/**
 * Preview URL for discovery card — local path if imported, else external og:image.
 */
function epc_disc_queue_preview_image(array $queueRow): string
{
	$local = json_decode((string) ($queueRow['local_image_paths'] ?? ''), true);
	if (is_array($local) && !empty($local[0])) {
		return (string) $local[0];
	}
	$images = $queueRow['images'] ?? null;
	if (is_array($images) && !empty($images[0])) {
		return (string) $images[0];
	}
	$decoded = json_decode((string) ($queueRow['image_urls'] ?? ''), true);
	if (is_array($decoded) && !empty($decoded[0])) {
		return (string) $decoded[0];
	}
	return '';
}
