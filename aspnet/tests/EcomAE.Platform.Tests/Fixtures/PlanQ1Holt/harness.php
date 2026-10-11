<?php
// PHP 8.3 goldens for plan Q1-holt (ePartsCart accessories page).
// Real page file is included for chrome. JS helpers are twinned here.
// GET never mints a session.
ini_set('display_errors', 'stderr');
date_default_timezone_set('UTC');
$root = rtrim($argv[1] ?? '/workspace', '/');
$spec = json_decode(file_get_contents(__DIR__ . '/cases.json'), true);

function holt_boot(string $root, string $lang = '/en'): void
{
	if (!defined('_ASTEXE_')) {
		define('_ASTEXE_', 1);
	}
	$_SERVER['DOCUMENT_ROOT'] = $root;
	$_SERVER['REQUEST_METHOD'] = 'GET';
	$_SERVER['HTTP_HOST'] = 'www.epartscart.com';
	$GLOBALS['multilang_params'] = array('lang_href' => $lang);
}

function holt_render(string $lang = '/en'): string
{
	$multilang_params = array('lang_href' => $lang);
	ob_start();
	require $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_epartscart_accessories.php';
	return (string) ob_get_clean();
}

function holt_page_flags(string $html): array
{
	return array(
		'root' => (strpos($html, 'id="epc-accessories"') !== false) ? 1 : 0,
		'api' => (strpos($html, '/content/shop/docpart/ajax_epc_accessories_search.php') !== false) ? 1 : 0,
		'hero' => (strpos($html, 'Car spare parts and accessories') !== false) ? 1 : 0,
		'search' => (strpos($html, 'id="epc-acc-search-form"') !== false) ? 1 : 0,
		'notify' => (strpos($html, 'id="epc-acc-notify-dialog"') !== false) ? 1 : 0,
		'saved' => (strpos($html, 'id="epc-acc-saved-bar"') !== false) ? 1 : 0,
		'version' => (strpos($html, '20260721accDetail2') !== false) ? 1 : 0,
		'css' => (strpos($html, '/content/general_pages/epc_accessories.css?v=') !== false) ? 1 : 0,
		'session' => (strpos($html, 'PHPSESSID') !== false) ? 1 : 0,
		'lang_en' => (strpos($html, 'data-lang="/en"') !== false) ? 1 : 0,
		'lang_ar' => (strpos($html, 'data-lang="/ar"') !== false) ? 1 : 0,
		'prefix' => substr($html, 0, 80),
	);
}

function holt_text($v): string
{
	return (string) ($v === null ? '' : $v);
}

function holt_esc($v): string
{
	return strtr(holt_text($v), array('&' => '&amp;', '<' => '&lt;', '>' => '&gt;', '"' => '&quot;'));
}

function holt_truthy($v): bool
{
	return !empty($v) || $v === '0' || $v === 0 || $v === 0.0 || $v === '0.0';
}

function holt_js_truthy($v): bool
{
	if ($v === null || $v === false || $v === '') {
		return false;
	}
	if ($v === true) {
		return true;
	}
	if (is_int($v) || is_float($v)) {
		return $v != 0;
	}
	return $v !== '';
}

function holt_number($v): float
{
	if ($v === null || $v === false || $v === '') {
		return 0.0;
	}
	if ($v === true) {
		return 1.0;
	}
	if (is_int($v) || is_float($v)) {
		return (float) $v;
	}
	$s = trim(holt_text($v));
	if ($s === '') {
		return 0.0;
	}
	return is_numeric($s) ? (float) $s : NAN;
}

function holt_money($amount, $currency = 'AED'): string
{
	$n = holt_number($amount);
	if (!is_finite($n) || $n <= 0) {
		return 'Price on request';
	}
	$cur = holt_text($currency);
	if ($cur === '') {
		$cur = 'AED';
	}
	return $cur . ' ' . number_format((float) round($n), 0, '.', ',');
}

function holt_page_base(string $lang): string
{
	$href = $lang === '' ? '/en' : rtrim($lang, '/');
	if ($href === '') {
		$href = '/en';
	}
	return $href . '/accessories-spare-parts';
}

function holt_relative($ts, int $now): string
{
	$t = holt_number($ts);
	if (!is_finite($t) || $t == 0) {
		return '';
	}
	$unix = (int) $t;
	$diff = max(0, $now - $unix);
	if ($diff < 3600) {
		return max(1, intdiv($diff, 60)) . ' min ago';
	}
	if ($diff < 86400) {
		return intdiv($diff, 3600) . ' hours ago';
	}
	if ($diff < 604800) {
		return intdiv($diff, 86400) . ' days ago';
	}
	return gmdate('n/j/Y', $unix);
}

function holt_listing_href($item, string $pageBase): string
{
	if (!is_array($item) || !holt_js_truthy($item['id'] ?? null)) {
		return $pageBase;
	}
	if (holt_js_truthy($item['detail_url'] ?? null)) {
		return (string) $item['detail_url'];
	}
	$url = holt_text($item['url'] ?? '');
	if (strpos($url, 'id=') !== false) {
		return $url;
	}
	$href = $pageBase . (strpos($pageBase, '?') !== false ? '&' : '?') . 'id=' . rawurlencode(holt_text($item['id']));
	if (holt_js_truthy($item['category'] ?? null)) {
		$href .= '&category=' . rawurlencode(holt_text($item['category']));
	}
	if (holt_js_truthy($item['subcategory'] ?? null)) {
		$href .= '&subcategory=' . rawurlencode(holt_text($item['subcategory']));
	}
	return $href;
}

function holt_listing_photos($item): array
{
	$photos = array();
	if (is_array($item) && isset($item['photos']) && is_array($item['photos'])) {
		foreach ($item['photos'] as $p) {
			$u = is_string($p) ? $p : (is_array($p) ? holt_text($p['url'] ?? '') : '');
			if ($u !== '') {
				$photos[] = $u;
			}
		}
	}
	if (!$photos && is_array($item) && holt_js_truthy($item['image_url'] ?? null)) {
		$photos[] = holt_text($item['image_url']);
	}
	return $photos;
}

function holt_browse_back($item, string $pageBase): string
{
	$href = $pageBase;
	$qs = array();
	if (is_array($item) && holt_js_truthy($item['category'] ?? null)) {
		$qs[] = 'category=' . rawurlencode(holt_text($item['category']));
	}
	if (is_array($item) && holt_js_truthy($item['subcategory'] ?? null)) {
		$qs[] = 'subcategory=' . rawurlencode(holt_text($item['subcategory']));
	}
	if (!$qs) {
		return $href;
	}
	return $href . (strpos($href, '?') !== false ? '&' : '?') . implode('&', $qs);
}

function holt_sync_url(string $path, array $state): string
{
	$keys = array('q','category','subcategory','make','model','city','condition','price_min','price_max','id','sort','page');
	$parts = array();
	foreach ($keys as $k) {
		$v = holt_text($state[$k] ?? '');
		if ($v !== '' && !($k === 'page' && $v === '1') && !($k === 'sort' && $v === 'updated-desc')) {
			$parts[] = rawurlencode($k) . '=' . rawurlencode($v);
		}
	}
	return $parts ? ($path . '?' . implode('&', $parts)) : $path;
}

function holt_read_url(string $url): array
{
	$state = array(
		'q' => '', 'category' => '', 'subcategory' => '', 'make' => '', 'model' => '',
		'city' => '', 'condition' => '', 'price_min' => '', 'price_max' => '', 'id' => '',
		'sort' => 'updated-desc', 'page' => 1, 'view' => 'grid', 'year' => '', 'showSavedOnly' => false,
	);
	$q = strpos($url, '?');
	if ($q === false) {
		return $state;
	}
	parse_str(substr($url, $q + 1), $qs);
	foreach ($qs as $k => $v) {
		if (array_key_exists($k, $state)) {
			$state[$k] = $v;
		}
	}
	return $state;
}

function holt_facet_list(array $rows, string $valueKey, string $labelKey, $active, string $allLabel = 'All'): string
{
	$html = '<li><button type="button" class="' . (!holt_js_truthy($active) ? 'is-active' : '') . '" data-value="">' . holt_esc($allLabel === '' ? 'All' : $allLabel) . '</button></li>';
	foreach ($rows as $row) {
		$value = $row[$valueKey] ?? '';
		$label = holt_js_truthy($row[$labelKey] ?? null) ? $row[$labelKey] : $value;
		$hasCount = array_key_exists('count', $row) && $row['count'] !== null;
		$cls = holt_text($value) === holt_text($active) ? ' is-active' : '';
		$html .= '<li><button type="button" class="' . $cls . '" data-value="' . holt_esc($value) . '"><span>' . holt_esc($label) . '</span>'
			. ($hasCount ? '<span class="count">' . holt_esc($row['count']) . '</span>' : '') . '</button></li>';
	}
	return $html;
}

function holt_fill_makes(array $makes, $cur): string
{
	$cur = holt_text($cur);
	$opts = '<option value="">Any make</option>';
	foreach ($makes as $m) {
		$name = is_string($m) ? $m : holt_text($m['make'] ?? '');
		if ($name === '') {
			continue;
		}
		$opts .= '<option value="' . holt_esc($name) . '"' . ($name === $cur ? ' selected' : '') . '>' . holt_esc($name) . '</option>';
	}
	return $opts;
}

function holt_pager(int $page, int $pages, bool $showSavedOnly = false): string
{
	if ($pages <= 1 || $showSavedOnly) {
		return '';
	}
	$html = '';
	$start = max(1, $page - 2);
	$end = min($pages, $page + 2);
	if ($page > 1) {
		$html .= '<button type="button" data-page="' . ($page - 1) . '">Prev</button>';
	}
	for ($p = $start; $p <= $end; $p++) {
		$html .= '<button type="button" class="' . ($p === $page ? 'is-active' : '') . '" data-page="' . $p . '">' . $p . '</button>';
	}
	if ($page < $pages) {
		$html .= '<button type="button" data-page="' . ($page + 1) . '">Next</button>';
	}
	return $html;
}

function holt_load(array $state, string $api): string
{
	$keys = array('q','category','subcategory','make','model','city','condition','price_min','price_max','id','sort','page');
	$parts = array();
	foreach ($keys as $k) {
		if (isset($state[$k]) && $state[$k] !== '' && $state[$k] !== null) {
			$parts[] = rawurlencode($k) . '=' . rawurlencode(holt_text($state[$k]));
		}
	}
	$parts[] = 'per_page=24';
	return $api . '?' . implode('&', $parts);
}

function holt_count_label(array $data, array $state): string
{
	$total = holt_number($data['total'] ?? 0);
	$label = ($total != 0 && is_finite($total))
		? ('<strong>' . holt_esc($data['from'] ?? '') . ' – ' . holt_esc($data['to'] ?? '') . '</strong> of <strong>' . holt_esc($data['total']) . '</strong> results')
		: '<strong>0</strong> results — categories ready, ads will be added category by category';
	if (holt_js_truthy($state['showSavedOnly'] ?? false)) {
		$label = 'Showing <strong>saved</strong> ads from this search';
	}
	if (holt_js_truthy($state['id'] ?? '') && holt_number($data['total'] ?? 0) == 1) {
		$label = 'Showing listing <strong>#' . holt_esc($state['id']) . '</strong>';
	} elseif (holt_js_truthy($state['id'] ?? '') && !holt_js_truthy($data['total'] ?? 0)) {
		$label = 'Listing <strong>#' . holt_esc($state['id']) . '</strong> not found or not published';
	}
	return $label;
}

function holt_save_notify(string $email, array $state, int $nowMs): array
{
	return array(
		'email' => $email,
		'created_at' => $nowMs,
		'filters' => array(
			'q' => $state['q'] ?? '',
			'category' => $state['category'] ?? '',
			'subcategory' => $state['subcategory'] ?? '',
			'make' => $state['make'] ?? '',
			'model' => $state['model'] ?? '',
			'city' => $state['city'] ?? '',
			'condition' => $state['condition'] ?? '',
			'price_min' => $state['price_min'] ?? '',
			'price_max' => $state['price_max'] ?? '',
		),
	);
}

function holt_set_view(string $mode): array
{
	return array(
		'view' => $mode,
		'resultsList' => $mode === 'list' ? 1 : 0,
		'gridActive' => $mode === 'grid' ? 1 : 0,
		'listActive' => $mode === 'list' ? 1 : 0,
	);
}

function holt_run_empty(): array
{
	$now = 1700000000;
	$saved = array();
	$toggleOn = (function () use (&$saved) {
		$id = '42';
		$i = array_search($id, $saved, true);
		if ($i !== false) {
			array_splice($saved, $i, 1);
		} else {
			$saved[] = $id;
		}
		return in_array($id, $saved, true) ? 1 : 0;
	})();
	$bar = array('count' => count($saved), 'hidden' => (count($saved) < 1 && true) ? 1 : 0);
	$toggleOff = (function () use (&$saved) {
		$id = '42';
		$i = array_search($id, $saved, true);
		if ($i !== false) {
			array_splice($saved, $i, 1);
		} else {
			$saved[] = $id;
		}
		return in_array($id, $saved, true) ? 1 : 0;
	})();
	$barAfter = array('count' => count($saved), 'hidden' => (count($saved) < 1) ? 1 : 0);
	$item = array('id' => '99', 'category' => 'brakes', 'subcategory' => 'pads');
	$photos = holt_listing_photos(array(
		'photos' => array(' /a.jpg ', array('url' => 'https://cdn/b.jpg'), array('url' => '')),
		'image_url' => 'https://cdn/fallback.jpg',
	));
	$fallback = holt_listing_photos(array('image_url' => 'https://cdn/only.jpg'));
	$state = array('q' => 'oil', 'category' => 'car-care', 'subcategory' => '', 'make' => 'Toyota', 'model' => '', 'city' => '', 'condition' => '', 'price_min' => '', 'price_max' => '', 'id' => '', 'sort' => 'updated-desc', 'page' => 2);
	$read = holt_read_url('/en/accessories-spare-parts?q=oil&category=car-care&make=Toyota&page=2');
	$rows = array(
		array('slug' => 'brakes', 'label' => 'Brakes', 'count' => 4),
		array('slug' => 'interior', 'label' => 'Interior', 'count' => 2),
	);
	return array(
		holt_text(null),
		holt_text(0),
		holt_esc('A & B <x> "q" \'z\''),
		holt_money(0, 'AED'),
		holt_money(-5, 'AED'),
		holt_money(1234, ''),
		holt_money('abc', 'AED'),
		holt_page_base('/en'),
		holt_page_base('/ar/'),
		holt_relative(0, $now),
		holt_relative($now - 30, $now),
		holt_relative($now - 120, $now),
		holt_relative($now - 7200, $now),
		holt_relative($now - 172800, $now),
		holt_relative($now - 700000, $now),
		holt_listing_href(null, holt_page_base('/en')),
		holt_listing_href(array('id' => ''), holt_page_base('/en')),
		holt_listing_href(array('id' => '7', 'detail_url' => '/en/accessories-spare-parts?id=7'), holt_page_base('/en')),
		holt_listing_href(array('id' => '7', 'url' => '/old?id=7'), holt_page_base('/en')),
		holt_listing_href($item, holt_page_base('/en')),
		count($photos),
		$photos[1] ?? '',
		count($fallback),
		$fallback[0] ?? '',
		holt_browse_back(array(), holt_page_base('/en')),
		holt_browse_back($item, holt_page_base('/en')),
		$toggleOn,
		$toggleOff,
		$barAfter['count'],
		$barAfter['hidden'],
		holt_sync_url('/en/accessories-spare-parts', $state),
		holt_text($read['q'] ?? ''),
		holt_text($read['make'] ?? ''),
		holt_text($read['page'] ?? ''),
		holt_facet_list($rows, 'slug', 'label', '', 'All categories'),
		holt_facet_list($rows, 'slug', 'label', 'brakes', 'All categories'),
		holt_fill_makes(array(array('make' => 'Toyota'), array('make' => 'BMW'), ''), 'Toyota'),
		holt_pager(1, 1),
		holt_pager(3, 8),
		holt_pager(3, 8, true),
		holt_load($state, '/content/shop/docpart/ajax_epc_accessories_search.php'),
		holt_count_label(array('from' => 1, 'to' => 24, 'total' => 40), array()),
		holt_count_label(array('total' => 0), array()),
		holt_count_label(array('total' => 1), array('id' => '99', 'showSavedOnly' => false)),
		holt_set_view('grid'),
		holt_set_view('list'),
		holt_save_notify('yawer@ecomae.com', array('q' => 'oil', 'category' => 'car-care', 'subcategory' => '', 'make' => 'Toyota', 'model' => '', 'city' => 'Dubai', 'condition' => 'new', 'price_min' => '10', 'price_max' => '90'), 1700000000000),
	);
}

function holt_sample_item(): array
{
	return array(
		'id' => '42',
		'title' => 'Cabin Filter <OEM>',
		'category' => 'filters',
		'subcategory' => 'cabin',
		'category_label' => 'Filters',
		'subcategory_label' => 'Cabin',
		'make' => 'Toyota',
		'model' => 'Corolla',
		'year' => '2018',
		'city' => 'Dubai',
		'condition' => 'new',
		'price' => 120,
		'compare_price' => 180,
		'currency' => 'AED',
		'stock_qty' => 3,
		'updated_at' => 1699999280,
		'description' => 'Fits Corolla 2015-2019 & "Yaris".',
		'external_url' => 'https://seller.example/ad',
		'photos' => array('https://cdn/a.jpg', array('url' => 'https://cdn/b.jpg')),
		'featured' => true,
		'photo_count' => 2,
	);
}

function holt_render_detail(array $item, string $pageBase, array $saved, int $now): string
{
	$photos = holt_listing_photos($item);
	$main = $photos[0] ?? '';
	$savedOn = in_array(holt_text($item['id'] ?? ''), $saved, true);
	$bits = array();
	foreach (array($item['make'] ?? '', $item['model'] ?? '', $item['year'] ?? '') as $b) {
		if (holt_js_truthy($b)) {
			$bits[] = $b;
		}
	}
	$vehicleBits = implode(' · ', $bits);
	$compareHtml = (holt_js_truthy($item['compare_price'] ?? null) && holt_number($item['compare_price']) > holt_number($item['price'] ?? 0))
		? '<span class="epc-acc__compare">' . holt_esc(holt_money($item['compare_price'], $item['currency'] ?? 'AED')) . '</span>'
		: '';
	$thumbs = '';
	if (count($photos) > 1) {
		$thumbs = '<div class="epc-acc__detail-thumbs" role="list">';
		foreach ($photos as $i => $u) {
			$thumbs .= '<button type="button" class="' . ($i === 0 ? 'is-active' : '') . '" data-src="' . holt_esc($u) . '" aria-label="Photo ' . ($i + 1) . '">'
				. '<img src="' . holt_esc($u) . '" alt="" loading="lazy" /></button>';
		}
		$thumbs .= '</div>';
	}
	$desc = trim(holt_text($item['description'] ?? ''));
	$catBits = array();
	foreach (array($item['category_label'] ?? '', $item['subcategory_label'] ?? '') as $b) {
		if (holt_js_truthy($b)) {
			$catBits[] = $b;
		}
	}
	$catJoined = implode(' / ', $catBits);
	return '<div class="epc-acc__detail-bar">'
		. '<a class="epc-acc__detail-back" href="' . holt_esc(holt_browse_back($item, $pageBase)) . '" id="epc-acc-detail-back"><i class="fa fa-arrow-left" aria-hidden="true"></i> Back to listings</a>'
		. '<span class="epc-acc__detail-id">Ad #' . holt_esc($item['id']) . '</span>'
		. '</div>'
		. '<div class="epc-acc__detail-grid">'
		. '<div class="epc-acc__detail-gallery">'
		. '<div class="epc-acc__detail-main">'
		. ($main !== ''
			? '<img id="epc-acc-detail-main-img" src="' . holt_esc($main) . '" alt="' . holt_esc($item['title'] ?? '') . '" />'
			: '<div class="epc-acc__detail-placeholder"><i class="fa fa-image" aria-hidden="true"></i><span>No photo yet</span></div>')
		. '</div>'
		. $thumbs
		. '</div>'
		. '<div class="epc-acc__detail-info">'
		. '<div class="epc-acc__meta">'
		. (holt_js_truthy($item['category_label'] ?? null) ? '<span>' . holt_esc($item['category_label']) . '</span>' : '')
		. (holt_js_truthy($item['subcategory_label'] ?? null) ? '<span>' . holt_esc($item['subcategory_label']) . '</span>' : '')
		. (holt_js_truthy($item['city'] ?? null) ? '<span>' . holt_esc($item['city']) . '</span>' : '')
		. '<span>' . holt_esc(($item['condition'] ?? '') === 'used' ? 'Used' : 'New') . '</span>'
		. '</div>'
		. '<h2 class="epc-acc__detail-title">' . holt_esc(holt_js_truthy($item['title'] ?? null) ? $item['title'] : 'Listing') . '</h2>'
		. ($vehicleBits !== '' ? '<p class="epc-acc__article">' . holt_esc($vehicleBits) . (holt_js_truthy($item['updated_at'] ?? null) ? ' · ' . holt_esc(holt_relative($item['updated_at'], $GLOBALS['holt_now'])) : '') . '</p>' : '')
		. '<div class="epc-acc__price">' . holt_esc(holt_money($item['price'] ?? 0, $item['currency'] ?? 'AED')) . $compareHtml . '</div>'
		. (holt_number($item['stock_qty'] ?? 0) > 0 ? '<div class="epc-acc__stock">' . holt_esc($item['stock_qty']) . ' in stock</div>' : '')
		. '<div class="epc-acc__detail-specs"><table><tbody>'
		. '<tr><th scope="row">Make</th><td>' . holt_esc(holt_js_truthy($item['make'] ?? null) ? $item['make'] : '—') . '</td></tr>'
		. '<tr><th scope="row">Model</th><td>' . holt_esc(holt_js_truthy($item['model'] ?? null) ? $item['model'] : '—') . '</td></tr>'
		. '<tr><th scope="row">Year</th><td>' . holt_esc(holt_js_truthy($item['year'] ?? null) ? $item['year'] : '—') . '</td></tr>'
		. '<tr><th scope="row">City</th><td>' . holt_esc(holt_js_truthy($item['city'] ?? null) ? $item['city'] : '—') . '</td></tr>'
		. '<tr><th scope="row">Condition</th><td>' . holt_esc(($item['condition'] ?? '') === 'used' ? 'Used' : 'New') . '</td></tr>'
		. '<tr><th scope="row">Category</th><td>' . holt_esc($catJoined !== '' ? $catJoined : '—') . '</td></tr>'
		. '</tbody></table></div>'
		. ($desc !== '' ? '<div class="epc-acc__detail-desc"><h3>Details</h3><p>' . holt_esc($desc) . '</p></div>' : '')
		. '<div class="epc-acc__actions epc-acc__detail-actions">'
		. '<button type="button" class="secondary epc-acc__save' . ($savedOn ? ' is-saved' : '') . '" data-id="' . holt_esc($item['id']) . '" id="epc-acc-detail-save">'
		. ($savedOn ? 'Saved' : 'Save Ad') . '</button>'
		. (holt_js_truthy($item['external_url'] ?? null) ? '<a class="primary" href="' . holt_esc($item['external_url']) . '" target="_blank" rel="noopener">Seller link</a>' : '')
		. '</div>'
		. '</div></div>';
}

function holt_empty_cards(bool $savedOnly, bool $emptyCatalog): string
{
	return '<div class="epc-acc__empty">'
		. '<strong>' . ($savedOnly ? 'No saved ads yet' : 'No ads in this search yet') . '</strong>'
		. '<p>Browse by category, make, model, and city. Listings will appear here as they are added category by category.</p>'
		. ($emptyCatalog && !$savedOnly ? '<p class="epc-acc__empty-hint">Catalog is ready — start by adding products under Car Care, Interior, Exterior, Brakes, and other categories.</p>' : '')
		. '</div>';
}

function holt_run_page(): array
{
	holt_boot($GLOBALS['holt_root'], '/en');
	$html = holt_render('/en');
	return array(
		holt_page_flags($html),
		(strpos($html, 'Set-Cookie') !== false) ? 1 : 0,
		(strpos($html, 'function esc(s)') !== false) ? 1 : 0,
		(strpos($html, 'function listingHref(item)') !== false) ? 1 : 0,
		(strpos($html, 'function saveNotify(email)') !== false) ? 1 : 0,
		(strpos($html, 'epc_acc_saved_ads') !== false) ? 1 : 0,
	);
}

function holt_run_live(): array
{
	$GLOBALS['holt_now'] = 1700000000;
	$item = holt_sample_item();
	$base = holt_page_base('/en');
	$facets = array(
		'categories' => array(
			array('slug' => 'filters', 'label' => 'Filters', 'count' => 3, 'subs' => array(array('slug' => 'cabin', 'label' => 'Cabin', 'count' => 1))),
			array('slug' => 'brakes', 'label' => 'Brakes', 'count' => 2, 'subs' => array(array('slug' => 'pads', 'label' => 'Pads', 'count' => 2))),
		),
		'makes' => array(array('make' => 'Toyota', 'count' => 4), array('make' => 'BMW', 'count' => 1)),
		'cities' => array(array('city' => 'Dubai', 'count' => 5)),
	);
	$cats = $facets['categories'];
	$browseCats = '';
	foreach ($cats as $c) {
		$browseCats .= '<button type="button" data-cat="' . holt_esc($c['slug']) . '">' . holt_esc($c['label']) . ($c['count'] ? ' (' . $c['count'] . ')' : '') . '</button>';
	}
	$detail = holt_render_detail($item, $base, array(), $GLOBALS['holt_now']);
	$savedDetail = holt_render_detail($item, $base, array('42'), $GLOBALS['holt_now']);
	return array(
		$detail,
		$savedDetail,
		holt_empty_cards(false, true),
		holt_empty_cards(true, true),
		$browseCats,
		holt_listing_href($item, $base),
		holt_browse_back($item, $base),
		holt_money(120, 'AED'),
		holt_esc($item['title']),
	);
}

function holt_run_tenant(): array
{
	holt_boot($GLOBALS['holt_root'], '/ar/');
	$ar = holt_render('/ar/');
	holt_boot($GLOBALS['holt_root'], '/en');
	$en = holt_render('/en');
	$item = array('id' => '7', 'category' => 'brakes');
	return array(
		holt_page_flags($ar),
		holt_page_flags($en),
		(strpos($ar, 'data-lang="/ar"') !== false) ? 1 : 0,
		(strpos($en, 'data-lang="/en"') !== false) ? 1 : 0,
		(strpos($ar, 'data-lang="/en"') !== false) ? 1 : 0,
		(strpos($en, 'data-lang="/ar"') !== false) ? 1 : 0,
		holt_page_base('/ar/'),
		holt_page_base('/en'),
		holt_listing_href($item, holt_page_base('/ar/')),
		holt_listing_href($item, holt_page_base('/en')),
		(strpos(holt_listing_href($item, holt_page_base('/ar/')), '/en/') !== false) ? 1 : 0,
		(strpos(holt_listing_href($item, holt_page_base('/en')), '/ar/') !== false) ? 1 : 0,
	);
}

$GLOBALS['holt_root'] = $root;
$GLOBALS['holt_now'] = 1700000000;
if (isset($argv[2])) {
	$case = $spec['cases'][(int) $argv[2]];
	$fn = 'holt_run_' . $case['name'];
	try {
		if ($case['name'] === 'empty') {
			holt_boot($root, '/en');
		}
		$result = $fn();
	} catch (Throwable $e) {
		fwrite(STDERR, $e->getMessage() . "\n" . $e->getTraceAsString() . "\n");
		exit(1);
	}
	echo json_encode(array('name' => $case['name'], 'output' => '', 'result' => $result), JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES | JSON_PARTIAL_OUTPUT_ON_ERROR);
	exit(0);
}

$results = array();
foreach ($spec['cases'] as $i => $case) {
	$cmd = escapeshellarg(PHP_BINARY) . ' ' . escapeshellarg(__FILE__) . ' ' . escapeshellarg($root) . ' ' . $i;
	$out = shell_exec($cmd . ' 2>/tmp/plan_q1holt_err_' . $i . '.txt');
	$row = json_decode((string) $out, true);
	if (!is_array($row)) {
		fwrite(STDERR, "case {$case['name']} failed:\n" . $out . "\n" . @file_get_contents('/tmp/plan_q1holt_err_' . $i . '.txt') . "\n");
		exit(1);
	}
	$results[] = $row;
}
echo json_encode(array('php' => PHP_VERSION, 'results' => $results), JSON_PRETTY_PRINT | JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES), "\n";
