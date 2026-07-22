<?php
/**
 * ECOM Visual Page Editor — shared helpers (layouts, blocks, brand, render).
 */
if (!defined('_ASTEXE_')) {
	define('_ASTEXE_', 1);
}

require_once __DIR__ . '/epc_portal.php';
require_once __DIR__ . '/epc_portal_db.php';
require_once __DIR__ . '/epc_super_cp_platform.php';

function epc_vpe_h($v): string
{
	return htmlspecialchars((string) $v, ENT_QUOTES, 'UTF-8');
}

function epc_vpe_block_library(): array
{
	return array(
		'hero' => array(
			'label' => 'Hero',
			'icon' => 'fa-star',
			'hint' => 'Large headline with call-to-action',
			'defaults' => array(
				'headline' => 'Welcome to our store',
				'subheadline' => 'Quality parts and fast delivery',
				'cta_text' => 'Shop now',
				'cta_url' => '/en/shop',
			),
			'fields' => array(
				'headline' => array('label' => 'Headline', 'type' => 'text'),
				'subheadline' => array('label' => 'Subheadline', 'type' => 'text'),
				'cta_text' => array('label' => 'Button label', 'type' => 'text'),
				'cta_url' => array('label' => 'Button URL', 'type' => 'url'),
			),
		),
		'text' => array(
			'label' => 'Text',
			'icon' => 'fa-align-left',
			'hint' => 'Paragraph or announcement',
			'defaults' => array(
				'body' => 'Add your message here.',
			),
			'fields' => array(
				'body' => array('label' => 'Body text', 'type' => 'textarea'),
			),
		),
		'cta' => array(
			'label' => 'CTA button',
			'icon' => 'fa-hand-pointer-o',
			'hint' => 'Standalone action button',
			'defaults' => array(
				'text' => 'Contact us',
				'url' => '/en/kontakty',
				'style' => 'primary',
			),
			'fields' => array(
				'text' => array('label' => 'Button label', 'type' => 'text'),
				'url' => array('label' => 'Link URL', 'type' => 'url'),
				'style' => array('label' => 'Style', 'type' => 'select', 'options' => array('primary' => 'Primary', 'secondary' => 'Secondary')),
			),
		),
		'image' => array(
			'label' => 'Image',
			'icon' => 'fa-picture-o',
			'hint' => 'Banner or promotional image',
			'defaults' => array(
				'url' => '/content/files/images/ecomae-platform/assets/electronicae.png',
				'alt' => 'Banner image',
				'caption' => '',
			),
			'fields' => array(
				'url' => array('label' => 'Image URL', 'type' => 'url'),
				'alt' => array('label' => 'Alt text', 'type' => 'text'),
				'caption' => array('label' => 'Caption', 'type' => 'text'),
			),
		),
		'two_col' => array(
			'label' => 'Two columns',
			'icon' => 'fa-columns',
			'hint' => 'Side-by-side content',
			'defaults' => array(
				'left' => 'Left column content',
				'right' => 'Right column content',
			),
			'fields' => array(
				'left' => array('label' => 'Left column', 'type' => 'textarea'),
				'right' => array('label' => 'Right column', 'type' => 'textarea'),
			),
		),
		'spacer' => array(
			'label' => 'Spacer',
			'icon' => 'fa-arrows-v',
			'hint' => 'Vertical spacing',
			'defaults' => array(
				'height' => 32,
			),
			'fields' => array(
				'height' => array('label' => 'Height (px)', 'type' => 'number'),
			),
		),
	);
}

/**
 * Frontend levels operators can switch between in the editor.
 *
 * @return array<string, array<string,mixed>>
 */
function epc_vpe_frontend_levels(): array
{
	return array(
		'homepage' => array(
			'label' => 'Homepage',
			'icon' => 'fa-home',
			'hint' => 'Main storefront landing',
			'page_key' => 'homepage',
			'placement' => 'homepage',
			'mode' => 'layout',
			'preview_path' => '/en/',
		),
		'product_list' => array(
			'label' => 'Category / list',
			'icon' => 'fa-th-list',
			'hint' => 'Product listing banner',
			'page_key' => 'product_list',
			'placement' => 'product_list',
			'mode' => 'layout',
			'preview_path' => '/en/shop',
		),
		'footer' => array(
			'label' => 'Footer',
			'icon' => 'fa-window-minimize',
			'hint' => 'Storefront footer strip',
			'page_key' => 'footer',
			'placement' => 'footer',
			'mode' => 'layout',
			'preview_path' => '/en/',
		),
		'checkout' => array(
			'label' => 'Checkout',
			'icon' => 'fa-shopping-cart',
			'hint' => 'Cart / checkout sidebar',
			'page_key' => 'checkout',
			'placement' => 'checkout',
			'mode' => 'layout',
			'preview_path' => '/en/shop/cart',
		),
		'login' => array(
			'label' => 'Login',
			'icon' => 'fa-sign-in',
			'hint' => 'Login / register page',
			'page_key' => 'login',
			'placement' => 'login',
			'mode' => 'layout',
			'preview_path' => '/en/users/login',
		),
		'brand' => array(
			'label' => 'Brand global',
			'icon' => 'fa-paint-brush',
			'hint' => 'Colours, logo, tagline',
			'page_key' => 'brand',
			'placement' => '',
			'mode' => 'brand_only',
			'preview_path' => '/en/',
		),
	);
}

function epc_vpe_normalize_page_key(string $pageKey): string
{
	$pageKey = preg_replace('/[^a-z0-9_-]/', '', strtolower(trim($pageKey)));
	$levels = epc_vpe_frontend_levels();
	if ($pageKey !== '' && isset($levels[$pageKey])) {
		return (string) $levels[$pageKey]['page_key'];
	}
	if ($pageKey === '') {
		return 'homepage';
	}
	foreach ($levels as $level) {
		if ((string) ($level['page_key'] ?? '') === $pageKey) {
			return $pageKey;
		}
	}
	return 'homepage';
}

function epc_vpe_level_meta(string $pageKey): array
{
	$pageKey = epc_vpe_normalize_page_key($pageKey);
	$levels = epc_vpe_frontend_levels();
	foreach ($levels as $id => $level) {
		if ((string) ($level['page_key'] ?? '') === $pageKey || $id === $pageKey) {
			return array_merge($level, array('id' => $id));
		}
	}
	return array_merge($levels['homepage'], array('id' => 'homepage'));
}

function epc_vpe_ensure_schema(PDO $pdo): void
{
	epc_scp_platform_ensure_schema($pdo);
	$pdo->exec(
		'CREATE TABLE IF NOT EXISTS `epc_page_builder_layouts` (
			`id` INT UNSIGNED NOT NULL AUTO_INCREMENT PRIMARY KEY,
			`site_key` VARCHAR(64) NOT NULL DEFAULT \'\',
			`page_key` VARCHAR(64) NOT NULL DEFAULT \'homepage\',
			`layout_json` MEDIUMTEXT NULL,
			`brand_json` TEXT NULL,
			`is_published` TINYINT(1) NOT NULL DEFAULT 0,
			`updated_at` INT NOT NULL DEFAULT 0,
			`published_at` INT NOT NULL DEFAULT 0,
			UNIQUE KEY `site_page` (`site_key`, `page_key`),
			KEY `published` (`is_published`, `site_key`)
		) ENGINE=InnoDB DEFAULT CHARSET=utf8'
	);
}

function epc_vpe_default_brand(): array
{
	return array(
		'primary' => '#2563eb',
		'accent' => '#0ea5e9',
		'background' => '#f8fafc',
		'logo_url' => '',
		'tagline' => '',
		'footer_text' => '',
		'hero_headline' => '',
		'hero_subheadline' => '',
	);
}

function epc_vpe_normalize_site_key(string $siteKey): string
{
	$siteKey = preg_replace('/[^a-z0-9_]/', '', strtolower(trim($siteKey)));
	return $siteKey === 'ecomae' ? 'platform' : $siteKey;
}

function epc_vpe_target_options(PDO $platformPdo): array
{
	$out = array(
		array(
			'site_key' => 'platform',
			'label' => 'ECOM AE marketing (ecomae.com)',
			'preview_url' => 'https://www.ecomae.com/en/',
			'scope' => 'platform',
		),
		array(
			'site_key' => 'epartscart',
			'label' => 'EpartsCart storefront',
			'preview_url' => 'https://www.epartscart.com/en/',
			'scope' => 'tenant',
		),
	);
	foreach (epc_scp_tenant_options($platformPdo) as $t) {
		$key = (string) ($t['site_key'] ?? '');
		if ($key === '' || $key === 'epartscart') {
			continue;
		}
		$preview = (string) (($t['urls']['storefront'] ?? '') ?: '');
		if ($preview === '') {
			$host = trim((string) ($t['hostname'] ?? ''));
			if ($host !== '') {
				$preview = 'https://' . preg_replace('/^www\./', '', $host) . '/en/';
			}
		}
		$out[] = array(
			'site_key' => $key,
			'label' => (string) ($t['label'] ?? $key),
			'preview_url' => $preview,
			'scope' => 'tenant',
		);
	}
	return $out;
}

function epc_vpe_resolve_preview_url(PDO $platformPdo, string $siteKey, string $pageKey = 'homepage'): string
{
	$siteKey = epc_vpe_normalize_site_key($siteKey);
	$pageKey = epc_vpe_normalize_page_key($pageKey);
	$base = 'https://www.ecomae.com/en/';
	foreach (epc_vpe_target_options($platformPdo) as $opt) {
		if ($opt['site_key'] === $siteKey) {
			$base = (string) $opt['preview_url'];
			break;
		}
	}
	$level = epc_vpe_level_meta($pageKey);
	$path = (string) ($level['preview_path'] ?? '/en/');
	$parts = parse_url($base);
	if (!is_array($parts) || empty($parts['scheme']) || empty($parts['host'])) {
		return 'https://www.ecomae.com' . $path;
	}
	return $parts['scheme'] . '://' . $parts['host'] . $path;
}

function epc_vpe_layout_load(PDO $pdo, string $siteKey, string $pageKey = 'homepage'): array
{
	epc_vpe_ensure_schema($pdo);
	$siteKey = epc_vpe_normalize_site_key($siteKey);
	$pageKey = epc_vpe_normalize_page_key($pageKey);
	$st = $pdo->prepare('SELECT * FROM `epc_page_builder_layouts` WHERE `site_key` = ? AND `page_key` = ? LIMIT 1');
	$st->execute(array($siteKey, $pageKey));
	$row = $st->fetch(PDO::FETCH_ASSOC);
	$brand = epc_vpe_default_brand();
	$blocks = array();
	$published = false;
	if ($row) {
		$decoded = json_decode((string) ($row['layout_json'] ?? ''), true);
		if (is_array($decoded)) {
			$blocks = $decoded;
		}
		$bDecoded = json_decode((string) ($row['brand_json'] ?? ''), true);
		if (is_array($bDecoded)) {
			$brand = array_merge($brand, $bDecoded);
		}
		$published = !empty($row['is_published']);
	}
	// Brand level (and empty brand on other levels) falls back to homepage/brand globals.
	if ($pageKey !== 'homepage' || empty($row)) {
		$homeBrand = array();
		try {
			$hst = $pdo->prepare('SELECT `brand_json` FROM `epc_page_builder_layouts` WHERE `site_key` = ? AND `page_key` IN (\'homepage\', \'brand\') ORDER BY FIELD(`page_key`, \'brand\', \'homepage\') LIMIT 1');
			$hst->execute(array($siteKey));
			$hb = json_decode((string) ($hst->fetchColumn() ?: ''), true);
			if (is_array($hb)) {
				$homeBrand = $hb;
			}
		} catch (Throwable $e) {
			$homeBrand = array();
		}
		if ($homeBrand) {
			foreach ($homeBrand as $bk => $bv) {
				if ((!isset($brand[$bk]) || $brand[$bk] === '' || $brand[$bk] === null) && $bv !== '' && $bv !== null) {
					$brand[$bk] = $bv;
				}
			}
		}
	}
	$level = epc_vpe_level_meta($pageKey);
	return array(
		'site_key' => $siteKey,
		'page_key' => $pageKey,
		'level_id' => (string) ($level['id'] ?? $pageKey),
		'mode' => (string) ($level['mode'] ?? 'layout'),
		'blocks' => $blocks,
		'brand' => $brand,
		'is_published' => $published,
		'updated_at' => (int) ($row['updated_at'] ?? 0),
	);
}

function epc_vpe_new_block(string $type): array
{
	$lib = epc_vpe_block_library();
	if (!isset($lib[$type])) {
		$type = 'text';
	}
	return array(
		'id' => 'blk_' . substr(md5(uniqid((string) mt_rand(), true)), 0, 10),
		'type' => $type,
		'props' => $lib[$type]['defaults'],
	);
}

function epc_vpe_block_to_html(array $block): string
{
	$type = (string) ($block['type'] ?? 'text');
	$p = is_array($block['props'] ?? null) ? $block['props'] : array();
	switch ($type) {
		case 'hero':
			$h = epc_vpe_h($p['headline'] ?? '');
			$s = epc_vpe_h($p['subheadline'] ?? '');
			$ct = epc_vpe_h($p['cta_text'] ?? '');
			$cu = epc_vpe_h($p['cta_url'] ?? '#');
			return '<section class="epc-vpe-block epc-vpe-block--hero"><div class="epc-vpe-hero"><h2>' . $h . '</h2><p>' . $s . '</p>'
				. ($ct !== '' ? '<a class="epc-vpe-btn epc-vpe-btn--primary" href="' . $cu . '">' . $ct . '</a>' : '')
				. '</div></section>';
		case 'text':
			return '<section class="epc-vpe-block epc-vpe-block--text"><div class="epc-vpe-text">' . nl2br(epc_vpe_h($p['body'] ?? '')) . '</div></section>';
		case 'cta':
			$t = epc_vpe_h($p['text'] ?? 'Learn more');
			$u = epc_vpe_h($p['url'] ?? '#');
			$style = ($p['style'] ?? 'primary') === 'secondary' ? 'secondary' : 'primary';
			return '<section class="epc-vpe-block epc-vpe-block--cta"><a class="epc-vpe-btn epc-vpe-btn--' . $style . '" href="' . $u . '">' . $t . '</a></section>';
		case 'image':
			$url = epc_vpe_h($p['url'] ?? '');
			$alt = epc_vpe_h($p['alt'] ?? '');
			$cap = epc_vpe_h($p['caption'] ?? '');
			$html = '<section class="epc-vpe-block epc-vpe-block--image"><figure><img src="' . $url . '" alt="' . $alt . '" loading="lazy" />';
			if ($cap !== '') {
				$html .= '<figcaption>' . $cap . '</figcaption>';
			}
			$html .= '</figure></section>';
			return $html;
		case 'two_col':
			return '<section class="epc-vpe-block epc-vpe-block--two-col"><div class="epc-vpe-two-col"><div>' . nl2br(epc_vpe_h($p['left'] ?? '')) . '</div><div>' . nl2br(epc_vpe_h($p['right'] ?? '')) . '</div></div></section>';
		case 'spacer':
			$h = max(8, min(200, (int) ($p['height'] ?? 32)));
			return '<div class="epc-vpe-block epc-vpe-block--spacer" style="height:' . $h . 'px" aria-hidden="true"></div>';
		default:
			return '';
	}
}

function epc_vpe_sync_info_blocks(PDO $pdo, string $siteKey, array $blocks, bool $publish, string $pageKey = 'homepage'): void
{
	$level = epc_vpe_level_meta($pageKey);
	$placement = (string) ($level['placement'] ?? '');
	if ($placement === '' || (string) ($level['mode'] ?? '') === 'brand_only') {
		return;
	}
	if (!isset(epc_scp_info_placements()[$placement])) {
		$placement = 'homepage';
	}
	$scope = ($siteKey === 'platform') ? 'platform' : 'tenant';
	$tenantKey = ($scope === 'tenant') ? $siteKey : '';
	$existing = epc_scp_info_blocks_list($pdo, $placement);
	$prefix = 'vpe_' . $siteKey . '_' . $placement . '_';
	// Legacy homepage prefix (pre-levels) — clean when publishing homepage.
	$legacyPrefix = ($placement === 'homepage') ? ('vpe_' . $siteKey . '_') : '';
	foreach ($existing as $b) {
		$key = (string) ($b['block_key'] ?? '');
		$match = (strpos($key, $prefix) === 0);
		if (!$match && $legacyPrefix !== '' && strpos($key, $legacyPrefix) === 0 && strpos($key, 'vpe_' . $siteKey . '_homepage_') !== 0) {
			// Old keys: vpe_{site}_{type}_{idx} without placement segment.
			$rest = substr($key, strlen($legacyPrefix));
			$match = (bool) preg_match('/^[a-z0-9]+_\d+$/', $rest);
		}
		if ($match) {
			epc_scp_info_block_delete($pdo, (int) $b['id']);
		}
	}
	$order = 10;
	foreach ($blocks as $idx => $block) {
		if (!is_array($block)) {
			continue;
		}
		$type = preg_replace('/[^a-z0-9_]/', '', (string) ($block['type'] ?? 'text'));
		$blockKey = $prefix . $type . '_' . (int) $idx;
		epc_scp_info_block_save($pdo, array(
			'block_key' => $blockKey,
			'title' => ucfirst($type) . ' · ' . (string) ($level['label'] ?? $placement) . ' ' . ((int) $idx + 1),
			'scope' => $scope,
			'site_key' => $tenantKey,
			'placement' => $placement,
			'content_html' => epc_vpe_block_to_html($block),
			'locale' => 'en',
			'active' => $publish ? 1 : 0,
			'sort_order' => $order,
		));
		$order += 10;
	}
}

function epc_vpe_tenant_row_by_key(PDO $platformPdo, string $siteKey): ?array
{
	if ($siteKey === '' || $siteKey === 'platform') {
		return null;
	}
	require_once __DIR__ . '/epc_portal_tenant_control.php';
	foreach (epc_portal_tenant_control_list_all($platformPdo) as $row) {
		if (epc_vpe_normalize_site_key((string) ($row['site_key'] ?? '')) === $siteKey) {
			return $row;
		}
	}
	return null;
}

function epc_vpe_settings_pdo(PDO $platformPdo, string $siteKey): array
{
	$siteKey = epc_vpe_normalize_site_key($siteKey);
	if ($siteKey === 'platform') {
		return array('pdo' => $platformPdo, 'host' => 'www.ecomae.com');
	}
	if ($siteKey === 'epartscart') {
		return array('pdo' => $platformPdo, 'host' => 'www.epartscart.com');
	}
	$row = epc_vpe_tenant_row_by_key($platformPdo, $siteKey);
	if ($row === null) {
		return array('pdo' => $platformPdo, 'host' => 'www.ecomae.com');
	}
	$tenantPdo = epc_portal_tenant_control_tenant_pdo($row);
	if ($tenantPdo instanceof PDO) {
		$host = epc_portal_tenant_control_commerce_host((string) ($row['hostname'] ?? ''));
		if ($host === '') {
			$host = preg_replace('/^www\./', '', strtolower((string) ($row['hostname'] ?? 'www.ecomae.com')));
		}
		return array('pdo' => $tenantPdo, 'host' => $host);
	}
	return array('pdo' => $platformPdo, 'host' => 'www.ecomae.com');
}

function epc_vpe_apply_brand(PDO $platformPdo, string $siteKey, array $brand, bool $publish): void
{
	if (!$publish) {
		return;
	}
	$ctx = epc_vpe_settings_pdo($platformPdo, $siteKey);
	$pdo = $ctx['pdo'];
	$host = (string) $ctx['host'];
	epc_portal_db_ensure($pdo);
	$settings = epc_portal_load_site_settings_for_host($pdo, $host);
	$theme = is_array($settings['theme'] ?? null) ? $settings['theme'] : array();
	if (!empty($brand['primary'])) {
		$theme['primary'] = (string) $brand['primary'];
		$theme['hero_from'] = (string) $brand['primary'];
	}
	if (!empty($brand['accent'])) {
		$theme['accent'] = (string) $brand['accent'];
		$theme['hero_to'] = (string) $brand['accent'];
	}
	if (!empty($brand['background'])) {
		$theme['page_bg'] = (string) $brand['background'];
	}
	$settings['theme'] = $theme;
	if (!empty($brand['tagline'])) {
		$settings['tagline'] = (string) $brand['tagline'];
	}
	if (!empty($brand['hero_headline'])) {
		$settings['system_name'] = (string) $brand['hero_headline'];
	}
	if (!empty($brand['hero_subheadline'])) {
		$settings['hero_subheadline'] = (string) $brand['hero_subheadline'];
		$theme['hero_subheadline'] = (string) $brand['hero_subheadline'];
		$settings['theme'] = $theme;
	}
	$contact = is_array($settings['contact'] ?? null) ? $settings['contact'] : array();
	if (!empty($brand['logo_url'])) {
		$contact['logo_url'] = (string) $brand['logo_url'];
	}
	if (!empty($brand['footer_text'])) {
		$contact['footer_text'] = (string) $brand['footer_text'];
	}
	$contact['visual_editor_version'] = (string) time();
	$settings['contact'] = $contact;
	$settings['host'] = $host;
	epc_portal_save_site_settings($pdo, $settings);
}

function epc_vpe_layout_save(PDO $pdo, string $siteKey, array $blocks, array $brand, bool $publish, string $pageKey = 'homepage'): array
{
	epc_vpe_ensure_schema($pdo);
	$siteKey = epc_vpe_normalize_site_key($siteKey);
	$pageKey = epc_vpe_normalize_page_key($pageKey);
	$level = epc_vpe_level_meta($pageKey);
	$mode = (string) ($level['mode'] ?? 'layout');
	if ($mode === 'brand_only') {
		$blocks = array();
	}
	$now = time();
	$st = $pdo->prepare(
		'INSERT INTO `epc_page_builder_layouts`
		 (`site_key`, `page_key`, `layout_json`, `brand_json`, `is_published`, `updated_at`, `published_at`)
		 VALUES (?, ?, ?, ?, ?, ?, ?)
		 ON DUPLICATE KEY UPDATE
		 `layout_json` = VALUES(`layout_json`),
		 `brand_json` = VALUES(`brand_json`),
		 `is_published` = VALUES(`is_published`),
		 `updated_at` = VALUES(`updated_at`),
		 `published_at` = IF(VALUES(`is_published`) = 1, VALUES(`published_at`), `published_at`)'
	);
	$st->execute(array(
		$siteKey,
		$pageKey,
		json_encode(array_values($blocks), JSON_UNESCAPED_UNICODE),
		json_encode($brand, JSON_UNESCAPED_UNICODE),
		$publish ? 1 : 0,
		$now,
		$publish ? $now : 0,
	));
	// Keep homepage brand_json aligned when editing Brand global.
	if ($mode === 'brand_only' || $pageKey === 'homepage') {
		$mirrorKey = ($pageKey === 'homepage') ? 'brand' : 'homepage';
		$mirror = epc_vpe_layout_load($pdo, $siteKey, $mirrorKey);
		$st->execute(array(
			$siteKey,
			$mirrorKey,
			json_encode(array_values((array) ($mirror['blocks'] ?? array())), JSON_UNESCAPED_UNICODE),
			json_encode($brand, JSON_UNESCAPED_UNICODE),
			$publish ? 1 : (int) !empty($mirror['is_published']),
			$now,
			$publish ? $now : 0,
		));
	}
	epc_vpe_sync_info_blocks($pdo, $siteKey, $blocks, $publish, $pageKey);
	epc_vpe_apply_brand($pdo, $siteKey, $brand, $publish && ($mode === 'brand_only' || $pageKey === 'homepage'));
	epc_vpe_clear_cache($siteKey);
	return array(
		'ok' => true,
		'published' => $publish,
		'updated_at' => $now,
		'page_key' => $pageKey,
		'level_id' => (string) ($level['id'] ?? $pageKey),
	);
}

function epc_vpe_clear_cache(string $siteKey): void
{
	$dir = $_SERVER['DOCUMENT_ROOT'] . '/content/files/cache';
	if (is_dir($dir)) {
		foreach (glob($dir . '/*') ?: array() as $file) {
			if (is_file($file)) {
				@unlink($file);
			}
		}
	}
	$siteKey = epc_vpe_normalize_site_key($siteKey);
	$flag = $_SERVER['DOCUMENT_ROOT'] . '/content/files/epc_vpe_version_' . $siteKey . '.txt';
	@file_put_contents($flag, (string) time());
}

function epc_vpe_storefront_site_key(): string
{
	if (function_exists('epc_portal_is_super_cp_host') && epc_portal_is_super_cp_host()) {
		return 'platform';
	}
	$site = function_exists('epc_portal_site_profile') ? epc_portal_site_profile() : array();
	$key = preg_replace('/[^a-z0-9_]/', '', strtolower((string) ($site['site_key'] ?? '')));
	if ($key !== '') {
		return $key;
	}
	$host = preg_replace('/^www\./', '', strtolower(epc_portal_host()));
	if ($host === 'ecomae.com') {
		return 'platform';
	}
	if ($host === 'epartscart.com') {
		return 'epartscart';
	}
	return $key;
}

function epc_vpe_render_storefront(PDO $pdo, string $siteKey = ''): string
{
	if ($siteKey === '') {
		$siteKey = epc_vpe_storefront_site_key();
	}
	$siteKey = epc_vpe_normalize_site_key($siteKey);
	$layout = epc_vpe_layout_load($pdo, $siteKey);
	if (empty($layout['is_published']) || count($layout['blocks']) === 0) {
		return '';
	}
	$brand = $layout['brand'];
	$css = '--epc-vpe-primary:' . epc_vpe_h($brand['primary'] ?? '#2563eb') . ';'
		. '--epc-vpe-accent:' . epc_vpe_h($brand['accent'] ?? '#0ea5e9') . ';'
		. '--epc-vpe-bg:' . epc_vpe_h($brand['background'] ?? '#f8fafc') . ';'
		. '--epc-portal-hero-from:' . epc_vpe_h($brand['primary'] ?? '#2563eb') . ';'
		. '--epc-portal-hero-to:' . epc_vpe_h($brand['accent'] ?? '#0ea5e9') . ';';
	$html = '<div class="epc-vpe-storefront" style="' . $css . '">';
	foreach ($layout['blocks'] as $block) {
		if (is_array($block)) {
			$html .= epc_vpe_block_to_html($block);
		}
	}
	$html .= '</div>';
	static $cssDone = false;
	if (!$cssDone) {
		$cssDone = true;
		$ver = (int) ($layout['updated_at'] ?? time());
		$html = '<link rel="stylesheet" href="/content/general_pages/epc_page_builder_render.css?v=' . $ver . '" />' . $html;
	}
	return $html;
}

function epc_vpe_guard_admin(): bool
{
	require_once $_SERVER['DOCUMENT_ROOT'] . '/content/users/dp_user.php';
	if (!DP_User::isAdmin()) {
		global $DP_Config;
		echo '<div class="alert alert-warning">Please <a href="/' . epc_vpe_h((string) ($DP_Config->backend_dir ?? 'cp')) . '/">log in as admin</a>.</div>';
		return false;
	}
	return true;
}

function epc_vpe_allowed_site_keys(PDO $platformPdo): array
{
	if (function_exists('epc_portal_is_super_cp_host') && epc_portal_is_super_cp_host()) {
		$keys = array();
		foreach (epc_vpe_target_options($platformPdo) as $opt) {
			$keys[] = (string) $opt['site_key'];
		}
		return $keys;
	}
	$key = epc_vpe_storefront_site_key();
	return $key !== '' ? array($key) : array('platform');
}
