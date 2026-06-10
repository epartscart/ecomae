<?php
/**
 * CHPU root: /parts — brands with in-stock articles from UAE price lists.
 */
if (!defined('_ASTEXE_')) {
	define('_ASTEXE_', 1);
}

require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/docpart/epc_stock_brands_helpers.php';

$lang_href = (isset($multilang_params['lang_href']) && $multilang_params['lang_href'] !== '')
	? rtrim($multilang_params['lang_href'], '/')
	: '/en';
$slash = $DP_Config->chpu_search_config['slash_code'];
$parts_url = $DP_Config->chpu_search_config['level_1']['url'];
$catalog_url = $lang_href . '/umapi_catalog';

$price_ids = epc_stock_brand_price_ids($db_link);
$brands = epc_stock_brands_with_counts($db_link, $price_ids);
if (count($brands) === 0) {
	$price_ids = epc_stock_brand_price_ids_with_stock($db_link);
	$brands = epc_stock_brands_with_counts($db_link, $price_ids);
}
$brands = epc_stock_brands_tag_part_types($db_link, $brands, $catalog_url);

$total_brands = count($brands);
$total_articles = 0;
$genuine_brands = 0;
$aftermarket_brands = 0;
foreach ($brands as $b) {
	$total_articles += (int) $b['parts_count'];
	if (isset($b['part_type']) && $b['part_type'] === 'genuine') {
		$genuine_brands++;
	} else {
		$aftermarket_brands++;
	}
}

$brands_json = json_encode($brands, JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES);
if ($brands_json === false) {
	$brands_json = '[]';
}
?>
<div class="epc-parts-stock-brands col-lg-12" id="epc-parts-stock-brands"
	data-lang-href="<?php echo htmlspecialchars($lang_href, ENT_QUOTES, 'UTF-8'); ?>"
	data-parts-url="<?php echo htmlspecialchars($parts_url, ENT_QUOTES, 'UTF-8'); ?>"
	data-slash="<?php echo htmlspecialchars($slash, ENT_QUOTES, 'UTF-8'); ?>"
	data-catalog-url="<?php echo htmlspecialchars($catalog_url, ENT_QUOTES, 'UTF-8'); ?>">
	<div class="epc-parts-stock-brands__hero">
		<div class="epc-parts-stock-brands__hero-main">
			<span class="epc-parts-stock-brands__eyebrow"><i class="fa fa-check-circle" aria-hidden="true"></i> Live warehouse stock</span>
			<h2 class="epc-parts-stock-brands__title"><?php echo htmlspecialchars(translate_str_by_id(4887), ENT_QUOTES, 'UTF-8'); ?></h2>
			<p class="epc-parts-stock-brands__lead">Browse in-stock brands in two groups: <strong>Genuine (OE)</strong> and <strong>Aftermarket</strong>. Open a brand to see part numbers, fitment, and prices.</p>
		</div>
		<div class="epc-parts-stock-brands__stats">
			<div class="epc-parts-stock-brands__stat">
				<strong id="epc-parts-stock-stat-brands"><?php echo (int) $total_brands; ?></strong>
				<span>Brands in stock</span>
			</div>
			<div class="epc-parts-stock-brands__stat">
				<strong id="epc-parts-stock-stat-articles"><?php echo (int) $total_articles; ?></strong>
				<span>Part numbers</span>
			</div>
		</div>
	</div>

	<div class="epc-parts-stock-brands__toolbar">
		<input type="search" class="form-control epc-parts-stock-brands__search" id="epc-parts-stock-search" placeholder="Search brand name…" autocomplete="off" />
		<a class="btn btn-default" href="<?php echo htmlspecialchars($lang_href, ENT_QUOTES, 'UTF-8'); ?>/shop/part_search"><?php echo htmlspecialchars(translate_str_by_id(2763), ENT_QUOTES, 'UTF-8'); ?> by article</a>
	</div>

	<div class="epc-parts-stock-brands__letters" id="epc-parts-stock-letters" role="toolbar" aria-label="Filter by letter"></div>
	<div class="epc-parts-stock-brands__summary" id="epc-parts-stock-summary"></div>

	<section class="epc-parts-stock-section epc-parts-stock-section--genuine" aria-labelledby="epc-parts-stock-heading-genuine">
		<header class="epc-parts-stock-section__head" id="epc-parts-stock-heading-genuine">
			<span class="epc-part-type-badge epc-part-type-badge--genuine">OE</span>
			<h3 class="epc-parts-stock-section__title">Genuine (OE) <span class="epc-parts-stock-section__count" id="epc-parts-stock-genuine-count">(<?php echo (int) $genuine_brands; ?>)</span></h3>
		</header>
		<div class="epc-parts-stock-brands__grid" id="epc-parts-stock-grid-genuine"></div>
	</section>

	<section class="epc-parts-stock-section epc-parts-stock-section--aftermarket" aria-labelledby="epc-parts-stock-heading-aftermarket">
		<header class="epc-parts-stock-section__head" id="epc-parts-stock-heading-aftermarket">
			<span class="epc-part-type-badge epc-part-type-badge--am">AM</span>
			<h3 class="epc-parts-stock-section__title">Aftermarket <span class="epc-parts-stock-section__count" id="epc-parts-stock-am-count">(<?php echo (int) $aftermarket_brands; ?>)</span></h3>
		</header>
		<div class="epc-parts-stock-brands__grid" id="epc-parts-stock-grid-aftermarket"></div>
	</section>
</div>

<script>
window.epcPartsStockBrands = <?php echo $brands_json; ?>;
(function () {
	'use strict';

	var root = document.getElementById('epc-parts-stock-brands');
	if (!root) {
		return;
	}

	var langHref = root.getAttribute('data-lang-href') || '/en';
	var partsUrl = root.getAttribute('data-parts-url') || 'parts';
	var slashCode = root.getAttribute('data-slash') || '%2F';
	var searchInput = document.getElementById('epc-parts-stock-search');
	var lettersBox = document.getElementById('epc-parts-stock-letters');
	var summaryBox = document.getElementById('epc-parts-stock-summary');
	var gridGenuine = document.getElementById('epc-parts-stock-grid-genuine');
	var gridAftermarket = document.getElementById('epc-parts-stock-grid-aftermarket');
	var brands = Array.isArray(window.epcPartsStockBrands) ? window.epcPartsStockBrands.slice(0) : [];
	var activeLetter = 'All';
	var alphabet = ['All', '0-9', 'A', 'B', 'C', 'D', 'E', 'F', 'G', 'H', 'I', 'J', 'K', 'L', 'M', 'N', 'O', 'P', 'Q', 'R', 'S', 'T', 'U', 'V', 'W', 'X', 'Y', 'Z', '#'];

	function esc(value) {
		return String(value == null ? '' : value).replace(/[&<>"']/g, function (ch) {
			return ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#039;' })[ch];
		});
	}

	function brandHref(name) {
		return langHref + '/' + partsUrl + '/' + encodeURIComponent(String(name).replace(/\//g, slashCode));
	}

	function filteredBrands() {
		var term = searchInput ? searchInput.value.trim().toLowerCase() : '';
		return brands.filter(function (item) {
			var letterMatch = activeLetter === 'All' || item.letter === activeLetter;
			var termMatch = !term || String(item.name || '').toLowerCase().indexOf(term) !== -1;
			return letterMatch && termMatch;
		});
	}

	function buildLetters() {
		var available = {};
		brands.forEach(function (item) {
			available[item.letter] = true;
		});
		lettersBox.innerHTML = alphabet.map(function (letter) {
			var disabled = letter !== 'All' && !available[letter] ? ' disabled' : '';
			var active = letter === activeLetter ? ' active' : '';
			return '<button type="button" class="epc-parts-stock-brands__letter' + active + '"' + disabled + ' data-letter="' + esc(letter) + '">' + esc(letter) + '</button>';
		}).join('');
		Array.prototype.forEach.call(lettersBox.querySelectorAll('button[data-letter]'), function (btn) {
			btn.onclick = function () {
				if (btn.disabled) {
					return;
				}
				activeLetter = btn.getAttribute('data-letter') || 'All';
				render();
			};
		});
	}

	function renderCards(grid, items, emptyText) {
		if (!grid) {
			return;
		}
		if (!items.length) {
			grid.innerHTML = '<div class="epc-parts-stock-brands__empty">' + esc(emptyText) + '</div>';
			return;
		}
		grid.innerHTML = items.map(function (item) {
			var initial = esc((item.name || '?').trim().charAt(0) || '?');
			return '<a class="epc-parts-stock-brands__card" href="' + esc(brandHref(item.name)) + '">'
				+ '<span class="epc-parts-stock-brands__card-logo" aria-hidden="true">' + initial + '</span>'
				+ '<span class="epc-parts-stock-brands__card-body">'
				+ '<strong class="epc-parts-stock-brands__card-name">' + esc(item.name) + '</strong>'
				+ '<span class="epc-parts-stock-brands__card-meta">'
				+ '<span class="epc-parts-stock-brands__count">' + esc(item.parts_count) + ' part numbers in stock</span>'
				+ '</span></span>'
				+ '<i class="fa fa-chevron-right epc-parts-stock-brands__card-arrow" aria-hidden="true"></i>'
				+ '</a>';
		}).join('');
	}

	function render() {
		buildLetters();
		var shown = filteredBrands();
		var shownArticles = 0;
		var genuineShown = [];
		var amShown = [];
		shown.forEach(function (item) {
			shownArticles += item.parts_count * 1;
			if (item.part_type === 'genuine') {
				genuineShown.push(item);
			} else {
				amShown.push(item);
			}
		});
		summaryBox.innerHTML = 'Showing <strong>' + shown.length + '</strong> of <strong>' + brands.length + '</strong> brands in stock'
			+ (shown.length ? ' — <strong>' + shownArticles + '</strong> part numbers in this view.' : '.');

		var countGenuine = document.getElementById('epc-parts-stock-genuine-count');
		var countAm = document.getElementById('epc-parts-stock-am-count');
		if (countGenuine) {
			countGenuine.textContent = '(' + genuineShown.length + ')';
		}
		if (countAm) {
			countAm.textContent = '(' + amShown.length + ')';
		}

		if (!brands.length) {
			renderCards(gridGenuine, [], 'No brands with stock were found in your connected price lists.');
			renderCards(gridAftermarket, [], '');
			return;
		}
		renderCards(gridGenuine, genuineShown, 'No genuine (OE) brands match this filter.');
		renderCards(gridAftermarket, amShown, 'No aftermarket brands match this filter.');
	}

	if (searchInput) {
		searchInput.oninput = render;
	}
	render();
})();
</script>
