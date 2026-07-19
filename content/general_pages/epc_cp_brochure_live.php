<?php
/**
 * Live CP brochure inventory — merges curated dump + capabilities + ERP nav.
 * New ERP tabs / capabilities appear automatically on next brochure render.
 *
 * Item shape:
 *   name, does, url, scope, icon?, id?, image?, category?
 */
defined('_ASTEXE_') or die('No access');

/**
 * Area → Font Awesome icon + optional marketing screen.
 *
 * @return array<string, array{icon:string,image:string,blurb:string}>
 */
/**
 * Topic-related presentation photo for one process.
 * Inventory → warehouse/parts photos; currency → banknotes; etc.
 *
 * @param array{name?:string,icon?:string,image?:string,url?:string,does?:string} $item
 */
function epc_cp_brochure_item_image(array $item, string $area): string
{
	$topicFile = __DIR__ . '/epc_cp_brochure_topic_photos.php';
	if (is_file($topicFile)) {
		require_once $topicFile;
	}
	if (function_exists('epc_cp_brochure_item_topic_photo')) {
		$meta = epc_cp_brochure_item_topic_photo($item, $area);
		return (string) ($meta['photo'] ?? '');
	}
	$name = trim((string) ($item['name'] ?? 'Process'));
	$icon = trim((string) ($item['icon'] ?? 'fa-cube'));
	return '/content/general_pages/epc_brochure_process_photo.php?' . http_build_query(array(
		't' => $name,
		'a' => $area,
		'i' => $icon !== '' ? $icon : 'fa-cube',
	));
}

/**
 * @param array{name?:string,does?:string,url?:string,image?:string} $item
 * @return array{topic:string,label:string,photo:string}
 */
function epc_cp_brochure_item_photo_meta(array $item, string $area): array
{
	$topicFile = __DIR__ . '/epc_cp_brochure_topic_photos.php';
	if (is_file($topicFile)) {
		require_once $topicFile;
	}
	if (function_exists('epc_cp_brochure_item_topic_photo')) {
		return epc_cp_brochure_item_topic_photo($item, $area);
	}
	return array(
		'topic' => 'default',
		'label' => 'Operations',
		'photo' => epc_cp_brochure_item_image($item, $area),
	);
}

/**
 * Pool of real marketing screens for area banners / mosaic tiles.
 *
 * @return list<string>
 */
function epc_cp_brochure_screen_pool(): array
{
	$base = '/content/general_pages/marketing_screens/';
	return array(
		$base . 'pf_workforce.png',
		$base . 'pf_orgmap.png',
		$base . 'pf_tracker.png',
		$base . 'pf_location.png',
		$base . 'og_cover.png',
		$base . 'epartscart-brochure-cover.jpg',
		$base . 'ecomae-brochure-cover.jpg',
	);
}

function epc_cp_brochure_area_visuals(): array
{
	$topicFile = __DIR__ . '/epc_cp_brochure_topic_photos.php';
	if (is_file($topicFile)) {
		require_once $topicFile;
	}
	$pick = static function (string $topic) use ($topicFile): string {
		if (!function_exists('epc_cp_brochure_topic_catalog')) {
			return '/content/general_pages/marketing_screens/og_cover.png';
		}
		$cat = epc_cp_brochure_topic_catalog();
		$photos = $cat[$topic]['photos'] ?? ($cat['default']['photos'] ?? array());
		return (string) ($photos[0] ?? '/content/general_pages/marketing_screens/og_cover.png');
	};
	return array(
		'Super CP / Platform' => array('icon' => 'fa-cloud', 'image' => $pick('platform'), 'blurb' => 'Host every tenant from one operator console.'),
		'Super CP / Operator' => array('icon' => 'fa-user-secret', 'image' => $pick('platform'), 'blurb' => 'Cross-tenant tools for platform operators.'),
		'Super CP / BOC' => array('icon' => 'fa-shield', 'image' => $pick('platform'), 'blurb' => 'Business operations control and audit.'),
		'Portal' => array('icon' => 'fa-sliders', 'image' => $pick('settings'), 'blurb' => 'Industry packs, branding, and site settings.'),
		'Integrations' => array('icon' => 'fa-plug', 'image' => $pick('platform'), 'blurb' => 'APIs, webhooks, and partner connections.'),
		'Shop / OMS' => array('icon' => 'fa-shopping-cart', 'image' => $pick('orders'), 'blurb' => 'Orders, fulfilment, and daily desk work.'),
		'Prices & Catalogue' => array('icon' => 'fa-tags', 'image' => $pick('inventory'), 'blurb' => 'Price lists, warehouses, and catalogue truth.'),
		'Payments' => array('icon' => 'fa-credit-card', 'image' => $pick('money'), 'blurb' => 'Gateways, currency, and reconciliation.'),
		'Logistics' => array('icon' => 'fa-truck', 'image' => $pick('logistics'), 'blurb' => 'Carriers, branches, and shipping methods.'),
		'Channels / Marketplace' => array('icon' => 'fa-share-alt', 'image' => $pick('orders'), 'blurb' => 'Sell beyond your own storefront.'),
		'Procurement' => array('icon' => 'fa-clipboard', 'image' => $pick('procurement'), 'blurb' => 'Suppliers, POs, and three-way match.'),
		'ERP / Finance' => array('icon' => 'fa-university', 'image' => $pick('money'), 'blurb' => 'GL, AR/AP, treasury, and close.'),
		'ERP / Modules' => array('icon' => 'fa-th-large', 'image' => $pick('erp'), 'blurb' => 'Every ERP area and tab — auto-synced from live nav.'),
		'ERP / Tax & VAT' => array('icon' => 'fa-percent', 'image' => $pick('money'), 'blurb' => 'VAT, tax toolkit, and jurisdiction rules.'),
		'ERP / External Reporting' => array('icon' => 'fa-file-text-o', 'image' => $pick('documents'), 'blurb' => 'Statutory returns and export packs.'),
		'Documents' => array('icon' => 'fa-folder-open', 'image' => $pick('documents'), 'blurb' => 'Print packs, PDFs, and document control.'),
		'Customers / CRM' => array('icon' => 'fa-users', 'image' => $pick('customers'), 'blurb' => 'Customers, profiles, and CRM pipelines.'),
		'AI' => array('icon' => 'fa-magic', 'image' => $pick('ai'), 'blurb' => 'Parts Expert chat and agent review.'),
		'Marketing' => array('icon' => 'fa-bullhorn', 'image' => $pick('marketing'), 'blurb' => 'Broadcast, social hub, and campaigns.'),
		'Content / CMS' => array('icon' => 'fa-pencil', 'image' => $pick('content'), 'blurb' => 'Pages, blocks, and storefront content.'),
		'System & Admin' => array('icon' => 'fa-cogs', 'image' => $pick('settings'), 'blurb' => 'Users, roles, and platform admin.'),
		'Industry Templates' => array('icon' => 'fa-industry', 'image' => $pick('platform'), 'blurb' => 'Vertical packs ready to deploy.'),
	);
}

/**
 * @return array<int, array{id:string,category:string,title:string,summary:string,icon:string}>
 */
function epc_cp_brochure_capabilities_catalog(): array
{
	static $caps = null;
	if ($caps !== null) {
		return $caps;
	}
	$path = __DIR__ . '/epc_ecomae_platform_capabilities_catalog.php';
	$caps = is_file($path) ? require $path : array();
	if (!is_array($caps)) {
		$caps = array();
	}
	return $caps;
}

function epc_cp_brochure_norm_key(string $s): string
{
	$s = strtolower(trim($s));
	$s = preg_replace('/[^a-z0-9]+/', ' ', $s) ?? $s;
	return trim(preg_replace('/\s+/', ' ', $s) ?? $s);
}

/**
 * Map capability category → inventory area when creating new rows.
 */
function epc_cp_brochure_cap_category_to_area(string $cat): string
{
	$map = array(
		'Platform & Super CP' => 'Super CP / Platform',
		'Commerce — Pricing & catalog' => 'Prices & Catalogue',
		'Commerce — Orders & fulfilment' => 'Shop / OMS',
		'Payments' => 'Payments',
		'Logistics & shipping' => 'Logistics',
		'Marketplace channels' => 'Channels / Marketplace',
		'Procurement & suppliers' => 'Procurement',
		'Finance & ERP' => 'ERP / Finance',
		'UAE e-invoicing & VAT' => 'ERP / Tax & VAT',
		'External Reporting & statutory returns' => 'ERP / External Reporting',
		'Document control' => 'Documents',
		'CRM & customer management' => 'Customers / CRM',
		'AI & automation' => 'AI',
		'Marketing & growth' => 'Marketing',
		'System & admin' => 'System & Admin',
		'Industry templates' => 'Industry Templates',
	);
	return $map[$cat] ?? 'Portal';
}

/**
 * @return array<string, array{icon:string,summary:string,id:string,category:string}>
 */
function epc_cp_brochure_capability_index(): array
{
	$idx = array();
	foreach (epc_cp_brochure_capabilities_catalog() as $cap) {
		if (!is_array($cap)) {
			continue;
		}
		$title = (string) ($cap['title'] ?? '');
		if ($title === '') {
			continue;
		}
		$key = epc_cp_brochure_norm_key($title);
		$idx[$key] = array(
			'icon' => (string) ($cap['icon'] ?? 'fa-cube'),
			'summary' => (string) ($cap['summary'] ?? ''),
			'id' => (string) ($cap['id'] ?? ''),
			'category' => (string) ($cap['category'] ?? ''),
		);
	}
	return $idx;
}

/**
 * Live ERP nav → brochure items under "ERP / Modules".
 *
 * @return array<int, array{name:string,does:string,url:string,scope:string,icon:string,id:string}>
 */
function epc_cp_brochure_erp_nav_items(): array
{
	$items = array();
	$navFile = (isset($_SERVER['DOCUMENT_ROOT']) ? rtrim((string) $_SERVER['DOCUMENT_ROOT'], '/') : dirname(__DIR__, 2))
		. '/cp/content/shop/finance/erp/erp_nav_areas.php';
	if (!is_file($navFile)) {
		$navFile = dirname(__DIR__, 2) . '/cp/content/shop/finance/erp/erp_nav_areas.php';
	}
	if (!is_file($navFile)) {
		return $items;
	}
	require_once $navFile;
	if (!function_exists('epc_erp_nav_areas_config')) {
		return $items;
	}
	foreach (epc_erp_nav_areas_config() as $areaKey => $area) {
		if (!is_array($area)) {
			continue;
		}
		$areaLabel = (string) ($area['label'] ?? $areaKey);
		$areaIcon = (string) ($area['icon'] ?? 'fa-cube');
		$areaDesc = (string) ($area['desc'] ?? '');
		$tabs = isset($area['tabs']) && is_array($area['tabs']) ? $area['tabs'] : array();
		if (!$tabs) {
			$items[] = array(
				'name' => $areaLabel,
				'does' => $areaDesc !== '' ? $areaDesc : ('ERP area: ' . $areaLabel),
				'url' => '/cp/shop/finance/erp?area=' . rawurlencode((string) $areaKey),
				'scope' => 'client',
				'icon' => $areaIcon,
				'id' => 'erp-' . preg_replace('/[^a-z0-9_\-]/', '', (string) $areaKey),
			);
			continue;
		}
		foreach ($tabs as $tabKey => $tab) {
			if (!is_array($tab)) {
				continue;
			}
			$tabLabel = (string) ($tab['label'] ?? $tabKey);
			$tabIcon = (string) ($tab['icon'] ?? $areaIcon);
			$items[] = array(
				'name' => $areaLabel . ' · ' . $tabLabel,
				'does' => $areaDesc !== '' ? ($areaDesc . ' — ' . $tabLabel) : ($areaLabel . ' / ' . $tabLabel),
				'url' => '/cp/shop/finance/erp?area=' . rawurlencode((string) $areaKey) . '&tab=' . rawurlencode((string) $tabKey),
				'scope' => 'client',
				'icon' => $tabIcon,
				'id' => 'erp-' . preg_replace('/[^a-z0-9_\-]/', '', (string) $areaKey . '-' . $tabKey),
			);
		}
	}
	return $items;
}

/**
 * Enrich + merge live sources into inventory.
 *
 * @return array{
 *   areas: array<string, array<int, array<string,mixed>>>,
 *   meta: array{generated_at:int,sources:array<int,string>,total:int,area_count:int}
 * }
 */
function epc_cp_brochure_build_live_inventory(): array
{
	static $built = null;
	if ($built !== null) {
		return $built;
	}

	$sources = array('curated inventory');
	$path = __DIR__ . '/epc_cp_brochure_inventory.php';
	$raw = is_file($path) ? require $path : array();
	if (!is_array($raw)) {
		$raw = array();
	}

	$capIdx = epc_cp_brochure_capability_index();
	$sources[] = 'capabilities catalog (' . count($capIdx) . ')';
	$areaVisuals = epc_cp_brochure_area_visuals();

	// Enrich curated rows with icons / richer copy from capabilities.
	$out = array();
	$seenUrl = array();
	$seenName = array();
	foreach ($raw as $area => $items) {
		if (!is_array($items)) {
			continue;
		}
		$area = (string) $area;
		$out[$area] = array();
		foreach ($items as $item) {
			if (!is_array($item)) {
				continue;
			}
			$name = trim((string) ($item['name'] ?? ''));
			$does = trim((string) ($item['does'] ?? ''));
			$url = trim((string) ($item['url'] ?? ''));
			$scope = (string) ($item['scope'] ?? 'client');
			$isStub = (strpos($name, 'Epc ') === 0) || ($does === 'Open from left CP menu.');
			if ($isStub) {
				continue;
			}
			$key = epc_cp_brochure_norm_key($name);
			$icon = 'fa-cube';
			$id = '';
			if (isset($capIdx[$key])) {
				$icon = $capIdx[$key]['icon'] !== '' ? $capIdx[$key]['icon'] : $icon;
				$id = $capIdx[$key]['id'];
				if (strlen($capIdx[$key]['summary']) > strlen($does)) {
					$does = $capIdx[$key]['summary'];
				}
			} elseif (isset($areaVisuals[$area]['icon'])) {
				$icon = $areaVisuals[$area]['icon'];
			}
			$row = array(
				'name' => $name,
				'does' => $does,
				'url' => $url,
				'scope' => $scope,
				'icon' => $icon,
				'id' => $id !== '' ? $id : ('fn-' . substr(md5($area . '|' . $name . '|' . $url), 0, 10)),
			);
			if ($url !== '') {
				$seenUrl[$url] = true;
			}
			$seenName[$key] = true;
			$out[$area][] = $row;
		}
	}

	// Auto-append capabilities not already represented by title.
	foreach (epc_cp_brochure_capabilities_catalog() as $cap) {
		if (!is_array($cap)) {
			continue;
		}
		$title = trim((string) ($cap['title'] ?? ''));
		if ($title === '') {
			continue;
		}
		$key = epc_cp_brochure_norm_key($title);
		if (isset($seenName[$key])) {
			continue;
		}
		$area = epc_cp_brochure_cap_category_to_area((string) ($cap['category'] ?? ''));
		if (!isset($out[$area])) {
			$out[$area] = array();
		}
		$out[$area][] = array(
			'name' => $title,
			'does' => (string) ($cap['summary'] ?? ''),
			'url' => '',
			'scope' => (strpos($area, 'Super CP') === 0) ? 'super' : 'both',
			'icon' => (string) ($cap['icon'] ?? 'fa-cube'),
			'id' => (string) ($cap['id'] ?? ''),
		);
		$seenName[$key] = true;
	}
	$sources[] = 'capability append';

	// Replace / merge ERP / Modules from live nav (always current).
	$erpItems = epc_cp_brochure_erp_nav_items();
	if ($erpItems) {
		$out['ERP / Modules'] = $erpItems;
		$sources[] = 'ERP nav live (' . count($erpItems) . ' tabs)';
	}

	// Drop empty areas; sort items by name.
	foreach ($out as $area => $items) {
		if (!$items) {
			unset($out[$area]);
			continue;
		}
		usort($out[$area], static function ($a, $b) {
			return strcasecmp((string) $a['name'], (string) $b['name']);
		});
	}

	$total = 0;
	foreach ($out as $items) {
		$total += count($items);
	}

	$built = array(
		'areas' => $out,
		'meta' => array(
			'generated_at' => time(),
			'sources' => $sources,
			'total' => $total,
			'area_count' => count($out),
		),
	);
	return $built;
}
