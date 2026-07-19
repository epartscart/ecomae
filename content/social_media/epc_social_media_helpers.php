<?php
/**
 * Social Media Marketing hub — schema, encrypted credentials, pack, AI advisor.
 */
if (!defined('_ASTEXE_')) {
	define('_ASTEXE_', 1);
}

$epcSocialDocRoot = rtrim((string) ($_SERVER['DOCUMENT_ROOT'] ?? ''), '/\\');
if ($epcSocialDocRoot === '') {
	$epcSocialDocRoot = dirname(__DIR__, 2);
}
require_once $epcSocialDocRoot . '/content/general_pages/epc_portal.php';
require_once $epcSocialDocRoot . '/content/general_pages/epc_integrations_helpers.php';

function epc_social_h($v): string
{
	return htmlspecialchars((string) $v, ENT_QUOTES, 'UTF-8');
}

function epc_social_backend(): string
{
	global $DP_Config;
	return trim((string) ($GLOBALS['DP_Config']->backend_dir ?? 'cp'), '/');
}

function epc_social_platforms(): array
{
	return array(
		'instagram' => array('label' => 'Instagram', 'icon' => 'fa-instagram', 'color' => '#e1306c'),
		'tiktok' => array('label' => 'TikTok', 'icon' => 'fa-music', 'color' => '#010101'),
		'facebook' => array('label' => 'Facebook', 'icon' => 'fa-facebook', 'color' => '#1877f2'),
		'linkedin' => array('label' => 'LinkedIn', 'icon' => 'fa-linkedin', 'color' => '#0a66c2'),
		'x' => array('label' => 'X / Twitter', 'icon' => 'fa-twitter', 'color' => '#1d9bf0'),
	);
}

function epc_social_crypto_key(string $siteKey): string
{
	$seed = function_exists('epc_deploy_token') ? (string) epc_deploy_token() : 'epc-platform-social';
	return hash('sha256', 'epc_social_v1|' . $seed . '|' . preg_replace('/[^a-z0-9_]/', '', strtolower($siteKey)), true);
}

function epc_social_encrypt(string $plain, string $siteKey): string
{
	if ($plain === '') {
		return '';
	}
	$key = epc_social_crypto_key($siteKey);
	$iv = random_bytes(16);
	$cipher = openssl_encrypt($plain, 'AES-256-CBC', $key, OPENSSL_RAW_DATA, $iv);
	if ($cipher === false) {
		return '';
	}
	return base64_encode($iv . $cipher);
}

function epc_social_decrypt(string $encoded, string $siteKey): string
{
	if ($encoded === '') {
		return '';
	}
	$raw = base64_decode($encoded, true);
	if ($raw === false || strlen($raw) < 17) {
		return '';
	}
	$key = epc_social_crypto_key($siteKey);
	$iv = substr($raw, 0, 16);
	$cipher = substr($raw, 16);
	$plain = openssl_decrypt($cipher, 'AES-256-CBC', $key, OPENSSL_RAW_DATA, $iv);
	return is_string($plain) ? $plain : '';
}

function epc_social_ensure_schema(PDO $pdo): void
{
	$pdo->exec(
		'CREATE TABLE IF NOT EXISTS `epc_social_accounts` (
			`id` INT UNSIGNED NOT NULL AUTO_INCREMENT,
			`site_key` VARCHAR(64) NOT NULL,
			`platform` VARCHAR(32) NOT NULL,
			`account_label` VARCHAR(128) NOT NULL DEFAULT \'\',
			`username` VARCHAR(128) NOT NULL DEFAULT \'\',
			`encrypted_credentials` TEXT NULL,
			`status` VARCHAR(32) NOT NULL DEFAULT \'pending\',
			`last_test_at` INT NOT NULL DEFAULT 0,
			`last_test_ok` TINYINT(1) NOT NULL DEFAULT 0,
			`meta_json` TEXT NULL,
			`created_at` INT NOT NULL DEFAULT 0,
			`updated_at` INT NOT NULL DEFAULT 0,
			PRIMARY KEY (`id`),
			UNIQUE KEY `site_platform` (`site_key`, `platform`),
			KEY `site_key` (`site_key`)
		) ENGINE=InnoDB DEFAULT CHARSET=utf8'
	);
	$pdo->exec(
		'CREATE TABLE IF NOT EXISTS `epc_social_post_drafts` (
			`id` INT UNSIGNED NOT NULL AUTO_INCREMENT,
			`site_key` VARCHAR(64) NOT NULL,
			`platform` VARCHAR(32) NOT NULL DEFAULT \'\',
			`title` VARCHAR(255) NOT NULL DEFAULT \'\',
			`caption` TEXT NULL,
			`hashtags` TEXT NULL,
			`media_url` VARCHAR(512) NOT NULL DEFAULT \'\',
			`status` VARCHAR(32) NOT NULL DEFAULT \'draft\',
			`scheduled_at` INT NOT NULL DEFAULT 0,
			`external_post_id` VARCHAR(128) NOT NULL DEFAULT \'\',
			`published_at` INT NOT NULL DEFAULT 0,
			`last_error` TEXT NULL,
			`created_at` INT NOT NULL DEFAULT 0,
			`updated_at` INT NOT NULL DEFAULT 0,
			PRIMARY KEY (`id`),
			KEY `site_key` (`site_key`),
			KEY `status` (`status`)
		) ENGINE=InnoDB DEFAULT CHARSET=utf8'
	);
	epc_social_schema_add_column_if_missing($pdo, 'epc_social_post_drafts', 'external_post_id', "VARCHAR(128) NOT NULL DEFAULT ''");
	epc_social_schema_add_column_if_missing($pdo, 'epc_social_post_drafts', 'published_at', 'INT NOT NULL DEFAULT 0');
	epc_social_schema_add_column_if_missing($pdo, 'epc_social_post_drafts', 'last_error', 'TEXT NULL');
}

function epc_social_schema_add_column_if_missing(PDO $pdo, string $table, string $column, string $definition): void
{
	static $checked = array();
	$key = $table . '.' . $column;
	if (isset($checked[$key])) {
		return;
	}
	$checked[$key] = true;
	try {
		$st = $pdo->prepare(
			'SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS
			 WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = ? AND COLUMN_NAME = ?'
		);
		$st->execute(array($table, $column));
		if ((int) $st->fetchColumn() > 0) {
			return;
		}
		$pdo->exec('ALTER TABLE `' . str_replace('`', '', $table) . '` ADD COLUMN `' . str_replace('`', '', $column) . '` ' . $definition);
	} catch (Throwable $e) {
	}
}

function epc_social_resolve_site_key(?PDO $platformPdo = null): string
{
	if (function_exists('epc_portal_is_super_cp_host') && epc_portal_is_super_cp_host()) {
		$fromGet = preg_replace('/[^a-z0-9_]/', '', strtolower(trim((string) ($_GET['site_key'] ?? ''))));
		return $fromGet !== '' ? $fromGet : 'platform';
	}
	return epc_integrations_site_key($platformPdo);
}

function epc_social_brand_context(string $siteKey, ?PDO $platformPdo = null): array
{
	$isPlatform = ($siteKey === '' || $siteKey === 'platform');
	$brandName = 'ECOM AE';
	$handle = 'ecomae.official';
	$website = 'https://www.ecomae.com';
	$domain = 'ecomae.com';
	$industry = 'platform';
	$country = 'UAE';
	$market = 'GCC';

	if (!$isPlatform && $platformPdo instanceof PDO && function_exists('epc_portal_list_tenants')) {
		foreach (epc_portal_list_tenants($platformPdo) as $row) {
			if ((string) ($row['site_key'] ?? '') === $siteKey) {
				$brandName = trim((string) ($row['trade_name'] ?? $row['hub_name'] ?? $siteKey));
				$handle = preg_replace('/[^a-z0-9._]/', '', strtolower($brandName));
				if ($handle === '') {
					$handle = $siteKey;
				}
				$hostname = trim((string) ($row['hostname'] ?? ''));
				if ($hostname !== '') {
					$website = 'https://' . preg_replace('/^www\./', 'www.', $hostname);
					if (strpos($website, 'www.') === false) {
						$website = 'https://www.' . preg_replace('/^www\./', '', $hostname);
					}
					$domain = preg_replace('/^www\./', '', $hostname);
				}
				$industry = (string) ($row['industry_code'] ?? 'auto_parts');
				break;
			}
		}
	} elseif (!$isPlatform) {
		if (function_exists('epc_brand_trade_name')) {
			require_once __DIR__ . '/../general_pages/epc_branding.php';
			$brandName = epc_brand_trade_name();
		}
		if (function_exists('epc_portal_cp_active_industry')) {
			$industry = epc_portal_cp_active_industry();
		}
		global $DP_Config;
		$store = trim((string) ($GLOBALS['DP_Config']->domain_path ?? ''));
		if ($store !== '') {
			$website = rtrim($store, '/');
			$parsed = parse_url($website);
			$domain = (string) ($parsed['host'] ?? $domain);
		}
		$handle = preg_replace('/[^a-z0-9._]/', '', strtolower($brandName));
	}

	$countryDocRoot = rtrim((string) ($_SERVER['DOCUMENT_ROOT'] ?? ''), '/\\');
	if ($countryDocRoot === '') {
		$countryDocRoot = dirname(__DIR__, 2);
	}
	$countryFile = $countryDocRoot . '/content/shop/tenant_hub/epc_tenant_country_profile.php';
	if (is_readable($countryFile)) {
		require_once $countryFile;
		if (function_exists('epc_tenant_country_market_label')) {
			global $db_link;
			$pdo = isset($db_link) && $db_link instanceof PDO ? $db_link : $platformPdo;
			if ($pdo instanceof PDO) {
				$market = epc_tenant_country_market_label($pdo);
				if (stripos($market, 'Pakistan') !== false) {
					$country = 'Pakistan';
				} elseif (stripos($market, 'UAE') !== false || stripos($market, 'Emirates') !== false) {
					$country = 'UAE';
				}
			}
		}
	}

	return array(
		'site_key' => $siteKey,
		'brand_name' => $brandName,
		'handle' => $handle,
		'website' => $website,
		'domain' => $domain,
		'industry' => $industry,
		'country' => $country,
		'market' => $market,
		'is_platform' => $isPlatform,
	);
}

function epc_social_adapt_text(string $text, array $brand): string
{
	$replacements = array(
		'ECOM AE' => (string) $brand['brand_name'],
		'ecomae.official' => (string) $brand['handle'],
		'ecomae.com' => (string) $brand['domain'],
		'https://www.ecomae.com' => (string) $brand['website'],
		'www.ecomae.com' => (string) $brand['domain'],
		'#ECOMAE' => '#' . strtoupper(preg_replace('/[^A-Z0-9]/', '', strtoupper((string) $brand['brand_name']))),
	);
	return str_replace(array_keys($replacements), array_values($replacements), $text);
}

/** @return array<int, array<string, mixed>> */
function epc_social_list_accounts(PDO $pdo, string $siteKey): array
{
	epc_social_ensure_schema($pdo);
	$st = $pdo->prepare(
		'SELECT `id`, `site_key`, `platform`, `account_label`, `username`, `status`, `last_test_at`, `last_test_ok`, `meta_json`, `updated_at`
		 FROM `epc_social_accounts` WHERE `site_key` = ? ORDER BY `platform`'
	);
	$st->execute(array($siteKey));
	$rows = array();
	while ($row = $st->fetch(PDO::FETCH_ASSOC)) {
		$row['has_credentials'] = true;
		$row['password_hint'] = '••••••••';
		unset($row['encrypted_credentials']);
		$rows[] = $row;
	}
	return $rows;
}

function epc_social_save_account(PDO $pdo, string $siteKey, array $data): array
{
	epc_social_ensure_schema($pdo);
	$platform = preg_replace('/[^a-z0-9_]/', '', strtolower((string) ($data['platform'] ?? '')));
	if ($platform === '' || !isset(epc_social_platforms()[$platform])) {
		return array('ok' => false, 'message' => 'Invalid platform.');
	}
	$username = trim((string) ($data['username'] ?? ''));
	$label = trim((string) ($data['account_label'] ?? ''));
	$accessToken = trim((string) ($data['access_token'] ?? ''));
	$apiKey = trim((string) ($data['api_key'] ?? ''));
	$apiSecret = trim((string) ($data['api_secret'] ?? ''));
	$pageId = trim((string) ($data['page_id'] ?? ''));
	$igUserId = trim((string) ($data['ig_user_id'] ?? ''));
	$openId = trim((string) ($data['open_id'] ?? ''));
	$privacy = strtoupper(trim((string) ($data['privacy_level'] ?? 'SELF_ONLY')));
	if (!in_array($privacy, array('PUBLIC_TO_EVERYONE', 'MUTUAL_FOLLOW_FRIENDS', 'FOLLOWER_OF_CREATOR', 'SELF_ONLY'), true)) {
		$privacy = 'SELF_ONLY';
	}

	if ($platform === 'instagram' && $igUserId === '' && $pageId !== '') {
		$igUserId = $pageId;
	}
	if ($platform === 'tiktok' && $openId === '' && $pageId !== '') {
		$openId = $pageId;
	}

	$credPayload = array(
		'access_token' => $accessToken,
		'api_key' => $apiKey,
		'api_secret' => $apiSecret,
		'page_id' => $pageId,
		'ig_user_id' => $igUserId,
		'open_id' => $openId,
		'privacy_level' => $privacy,
		'updated' => time(),
	);
	$encrypted = epc_social_encrypt(json_encode($credPayload), $siteKey);
	$now = time();

	$existing = $pdo->prepare('SELECT `id`, `encrypted_credentials` FROM `epc_social_accounts` WHERE `site_key` = ? AND `platform` = ? LIMIT 1');
	$existing->execute(array($siteKey, $platform));
	$row = $existing->fetch(PDO::FETCH_ASSOC);

	if ($row) {
		$old = json_decode(epc_social_decrypt((string) ($row['encrypted_credentials'] ?? ''), $siteKey), true);
		if (is_array($old)) {
			if ($accessToken === '') {
				$credPayload['access_token'] = (string) ($old['access_token'] ?? '');
			}
			if ($apiKey === '') {
				$credPayload['api_key'] = (string) ($old['api_key'] ?? '');
			}
			if ($apiSecret === '') {
				$credPayload['api_secret'] = (string) ($old['api_secret'] ?? '');
			}
			if ($pageId === '') {
				$credPayload['page_id'] = (string) ($old['page_id'] ?? '');
			}
			if ($igUserId === '') {
				$credPayload['ig_user_id'] = (string) ($old['ig_user_id'] ?? '');
			}
			if ($openId === '') {
				$credPayload['open_id'] = (string) ($old['open_id'] ?? '');
			}
			if (trim((string) ($data['privacy_level'] ?? '')) === '' && !empty($old['privacy_level'])) {
				$credPayload['privacy_level'] = (string) $old['privacy_level'];
			}
			$encrypted = epc_social_encrypt(json_encode($credPayload), $siteKey);
		}
	}

	if ($row) {
		$pdo->prepare(
			'UPDATE `epc_social_accounts` SET `account_label` = ?, `username` = ?, `encrypted_credentials` = ?, `status` = ?, `updated_at` = ? WHERE `id` = ?'
		)->execute(array($label, $username, $encrypted, 'connected', $now, (int) $row['id']));
	} else {
		$pdo->prepare(
			'INSERT INTO `epc_social_accounts` (`site_key`, `platform`, `account_label`, `username`, `encrypted_credentials`, `status`, `created_at`, `updated_at`)
			 VALUES (?, ?, ?, ?, ?, ?, ?, ?)'
		)->execute(array($siteKey, $platform, $label, $username, $encrypted, 'connected', $now, $now));
	}
	return array('ok' => true, 'message' => ucfirst($platform) . ' account saved securely.');
}

function epc_social_test_account(PDO $pdo, string $siteKey, string $platform): array
{
	$publishFile = __DIR__ . '/epc_social_publish.php';
	if (is_file($publishFile)) {
		require_once $publishFile;
		if (function_exists('epc_social_test_account_live')) {
			return epc_social_test_account_live($pdo, $siteKey, $platform);
		}
	}
	epc_social_ensure_schema($pdo);
	$platform = preg_replace('/[^a-z0-9_]/', '', strtolower($platform));
	$st = $pdo->prepare('SELECT `encrypted_credentials`, `username` FROM `epc_social_accounts` WHERE `site_key` = ? AND `platform` = ? LIMIT 1');
	$st->execute(array($siteKey, $platform));
	$row = $st->fetch(PDO::FETCH_ASSOC);
	if (!$row) {
		return array('ok' => false, 'message' => 'No account configured for ' . $platform . '.');
	}
	$cred = json_decode(epc_social_decrypt((string) ($row['encrypted_credentials'] ?? ''), $siteKey), true);
	$hasToken = is_array($cred) && ((string) ($cred['access_token'] ?? '') !== '' || (string) ($cred['api_key'] ?? '') !== '');
	$ok = $hasToken && trim((string) ($row['username'] ?? '')) !== '';
	$now = time();
	$pdo->prepare('UPDATE `epc_social_accounts` SET `last_test_at` = ?, `last_test_ok` = ?, `status` = ? WHERE `site_key` = ? AND `platform` = ?')
		->execute(array($now, $ok ? 1 : 0, $ok ? 'verified' : 'pending', $siteKey, $platform));
	if (!$ok) {
		return array('ok' => false, 'message' => 'Credentials incomplete. Add username and access token.');
	}
	return array('ok' => true, 'message' => 'Credential vault OK (live API module unavailable).');
}

function epc_social_delete_account(PDO $pdo, string $siteKey, string $platform): array
{
	$platform = preg_replace('/[^a-z0-9_]/', '', strtolower($platform));
	$pdo->prepare('DELETE FROM `epc_social_accounts` WHERE `site_key` = ? AND `platform` = ?')->execute(array($siteKey, $platform));
	return array('ok' => true, 'message' => 'Account removed.');
}

/** @return array<int, array<string, mixed>> */
function epc_social_list_drafts(PDO $pdo, string $siteKey, int $limit = 20): array
{
	epc_social_ensure_schema($pdo);
	$st = $pdo->prepare(
		'SELECT `id`, `platform`, `title`, `caption`, `hashtags`, `media_url`, `status`, `scheduled_at`,
		 `external_post_id`, `published_at`, `last_error`, `updated_at`
		 FROM `epc_social_post_drafts` WHERE `site_key` = ? ORDER BY `updated_at` DESC LIMIT ' . (int) $limit
	);
	$st->execute(array($siteKey));
	return $st->fetchAll(PDO::FETCH_ASSOC) ?: array();
}

function epc_social_save_draft(PDO $pdo, string $siteKey, array $data): array
{
	epc_social_ensure_schema($pdo);
	$now = time();
	$id = (int) ($data['id'] ?? 0);
	$platform = preg_replace('/[^a-z0-9_]/', '', strtolower((string) ($data['platform'] ?? '')));
	$title = trim((string) ($data['title'] ?? 'Untitled draft'));
	$caption = trim((string) ($data['caption'] ?? ''));
	$hashtags = trim((string) ($data['hashtags'] ?? ''));
	$mediaUrl = trim((string) ($data['media_url'] ?? ''));

	if ($id > 0) {
		$pdo->prepare(
			'UPDATE `epc_social_post_drafts` SET `platform` = ?, `title` = ?, `caption` = ?, `hashtags` = ?, `media_url` = ?, `updated_at` = ? WHERE `id` = ? AND `site_key` = ?'
		)->execute(array($platform, $title, $caption, $hashtags, $mediaUrl, $now, $id, $siteKey));
	} else {
		$pdo->prepare(
			'INSERT INTO `epc_social_post_drafts` (`site_key`, `platform`, `title`, `caption`, `hashtags`, `media_url`, `status`, `created_at`, `updated_at`)
			 VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?)'
		)->execute(array($siteKey, $platform, $title, $caption, $hashtags, $mediaUrl, 'draft', $now, $now));
	}
	return array('ok' => true, 'message' => 'Draft saved.');
}

function epc_social_trending_formats(): array
{
	$week = (int) date('W');
	$formats = array(
		array('name' => 'Before / After workflow', 'platforms' => 'TikTok, Reels', 'tip' => 'Show manual process vs your platform in 15 seconds.'),
		array('name' => 'POV operator day', 'platforms' => 'TikTok, Stories', 'tip' => 'Screen-record CP dashboard handling orders + ERP sync.'),
		array('name' => 'Carousel feature list', 'platforms' => 'Instagram, LinkedIn', 'tip' => '5 slides: pain → solution → proof → CTA → link in bio.'),
		array('name' => 'Compliance alert', 'platforms' => 'LinkedIn, Facebook', 'tip' => 'UAE VAT / e-invoice or Pakistan FBR angle — timely authority content.'),
		array('name' => 'Customer quote stitch', 'platforms' => 'TikTok, X', 'tip' => 'Reply to common WhatsApp-quote pain with your B2B portal demo.'),
	);
	return array_slice($formats, $week % 2, 4) + array_slice($formats, 0, 4 - min(4, count($formats) - ($week % 2)));
}

function epc_social_industry_hooks(string $industry, array $brand): array
{
	$hooks = array(
		'auto_parts' => array(
			'VIN search demo in 10 seconds',
			'OEM vs aftermarket — educate buyers',
			'B2B trade account self-service',
			'Stop WhatsApp quoting — show CP order flow',
		),
		'electronics' => array(
			'Spec comparison carousel',
			'RMA / warranty workflow reel',
			'Multi-warehouse stock accuracy',
			'Bundle deals for GCC retailers',
		),
		'fashion' => array(
			'Variant SKU lookbook Reel',
			'Size guide pinned post',
			'Ramadan / Eid collection drop',
			'Influencer unboxing with shop link',
		),
		'jewellery' => array(
			'Gallery product cards showcase',
			'Gold rate + making charge transparency',
			'Appointment booking CTA',
			'Gift season carousel',
		),
		'medical' => array(
			'Compliance-first supply chain post',
			'Batch traceability explainer',
			'B2B clinic ordering portal',
			'Cold chain / expiry alerts',
		),
		'platform' => array(
			'Multi-tenant Super CP demo',
			'Go live in 24 hours story',
			'UAE e-invoice compliance built-in',
			'Industry template showcase',
		),
	);
	$list = $hooks[$industry] ?? $hooks['platform'];
	$out = array();
	foreach ($list as $hook) {
		$out[] = epc_social_adapt_text($hook, $brand);
	}
	return $out;
}

function epc_social_generate_caption(array $brand, string $platform, string $productLine = ''): array
{
	$platform = strtolower($platform);
	$name = (string) $brand['brand_name'];
	$site = (string) $brand['website'];
	$industry = (string) $brand['industry'];
	$country = (string) $brand['country'];
	$productLine = trim($productLine);

	$lines = array();
	if ($platform === 'tiktok') {
		$lines[] = 'POV: You run ' . $country . ' ' . str_replace('_', ' ', $industry) . ' without 5 apps.';
		$lines[] = $productLine !== '' ? 'Today: ' . $productLine : 'One platform. Orders → ERP → VAT. Automatically.';
		$lines[] = 'Link in bio → ' . (string) $brand['domain'];
	} elseif ($platform === 'instagram') {
		$lines[] = $name . ' — built for ' . $country . ' traders.';
		$lines[] = $productLine !== '' ? $productLine . ' ✨' : 'Storefront + ERP + CRM in one stack.';
		$lines[] = 'DM us or tap link in bio for a demo.';
	} elseif ($platform === 'linkedin') {
		$lines[] = 'Most ' . str_replace('_', ' ', $industry) . ' businesses in ' . $country . ' still reconcile orders manually.';
		$lines[] = $name . ' connects commerce, inventory, and finance in one database.';
		$lines[] = 'Explore: ' . $site;
	} else {
		$lines[] = $name . ' — ' . str_replace('_', ' ', $industry) . ' on one cloud.';
		$lines[] = $productLine !== '' ? $productLine : 'Go live faster. Operate smarter.';
		$lines[] = $site;
	}

	$caption = implode("\n\n", $lines);
	$tags = epc_social_hashtags_for_industry($industry, $country);
	return array(
		'caption' => $caption,
		'hashtags' => implode(' ', $tags),
		'platform' => $platform,
	);
}

function epc_social_hashtags_for_industry(string $industry, string $country): array
{
	$base = array('#Ecommerce', '#B2B', '#DigitalTransformation');
	if ($country === 'Pakistan') {
		$base = array_merge($base, array('#PakistanBusiness', '#Karachi', '#Lahore', '#SME'));
	} else {
		$base = array_merge($base, array('#UAEBusiness', '#Dubai', '#GCC', '#SME'));
	}
	$extra = array(
		'auto_parts' => array('#AutoParts', '#SpareParts', '#VIN'),
		'electronics' => array('#Electronics', '#TechRetail', '#ConsumerTech'),
		'fashion' => array('#FashionRetail', '#ModestFashion', '#StyleUAE'),
		'jewellery' => array('#Jewellery', '#Gold', '#LuxuryRetail'),
		'medical' => array('#Healthcare', '#MedicalSupplies', '#Pharma'),
		'platform' => array('#ECOMAE', '#CloudERP', '#SaaS'),
	);
	return array_merge($base, $extra[$industry] ?? $extra['platform']);
}

function epc_social_hub_url(string $tab = 'pack', ?string $siteKey = null): string
{
	$backend = epc_social_backend();
	$base = '/' . $backend . '/control/portal/epc_social_media_hub';
	$params = array();
	if ($tab !== '' && $tab !== 'pack') {
		$params['tab'] = $tab;
	}
	if ($siteKey !== null && $siteKey !== '' && $siteKey !== 'platform') {
		$params['site_key'] = $siteKey;
	}
	return $params ? $base . '?' . http_build_query($params) : $base;
}

function epc_social_tenant_hub_url(string $tab = 'social'): string
{
	$backend = epc_social_backend();
	return '/' . $backend . '/shop/tenant_hub/tenant_hub?tab=' . rawurlencode($tab);
}

function epc_social_pdo(?PDO $fallback = null): ?PDO
{
	if (function_exists('epc_portal_platform_pdo')) {
		$p = epc_portal_platform_pdo();
		if ($p instanceof PDO) {
			return $p;
		}
	}
	global $db_link;
	if ($db_link instanceof PDO) {
		return $db_link;
	}
	return $fallback;
}

function epc_social_csrf_token(): string
{
	if (!isset($_SESSION)) {
		@session_start();
	}
	if (empty($_SESSION['csrf_token'])) {
		$_SESSION['csrf_token'] = bin2hex(random_bytes(16));
	}
	return (string) $_SESSION['csrf_token'];
}

function epc_social_verify_csrf(): bool
{
	$token = (string) ($_POST['csrf_token'] ?? $_SERVER['HTTP_X_CSRF_TOKEN'] ?? '');
	if ($token === '') {
		return false;
	}
	if (!isset($_SESSION)) {
		@session_start();
	}
	return isset($_SESSION['csrf_token']) && hash_equals((string) $_SESSION['csrf_token'], $token);
}

function epc_cp_social_media_menu_apply(PDO $pdo): array
{
	require_once dirname(__DIR__, 2) . '/epc_cp_mainstream_menu.php';
	$portalMenu = epc_cp_portal_menu_apply($pdo);
	return array(
		'portal_group' => (int) ($portalMenu['portal_group'] ?? 0),
		'social_hub_item' => (int) ($portalMenu['items']['social_media_hub'] ?? 0),
	);
}
