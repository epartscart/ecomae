<?php
/**
 * CHPU: /parts/{MANUFACTURER} — in-stock part numbers + fitment + price search.
 */
if (!defined('_ASTEXE_')) {
	define('_ASTEXE_', 1);
}

$mfr = '';
if (isset($DP_Content->service_data['manufacturer'])) {
	$mfr = trim($DP_Content->service_data['manufacturer']);
}

$lang_href = (isset($multilang_params['lang_href']) && $multilang_params['lang_href'] !== '')
	? rtrim($multilang_params['lang_href'], '/')
	: '/en';
$slash = $DP_Config->chpu_search_config['slash_code'];
$parts_url = $DP_Config->chpu_search_config['level_1']['url'];
$mfr_url = rawurlencode(str_replace('/', $slash, $mfr));
$parts_index_url = $lang_href . '/' . $parts_url;

if ($mfr === '') {
	echo '<div class="alert alert-warning">Manufacturer not specified.</div>';
	return;
}

require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/docpart/epc_stock_brands_helpers.php';

$rows = epc_stock_brand_parts_for_manufacturer($db_link, $mfr, 5000);
$display_brand = $mfr;
if (count($rows) > 0 && !empty($rows[0]['manufacturer'])) {
	$display_brand = $rows[0]['manufacturer'];
}
$n = count($rows);
$rows_json = json_encode($rows, JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES);
if ($rows_json === false) {
	$rows_json = '[]';
}
?>
<div class="epc-brand-browse col-lg-12" id="epc-brand-browse"
	data-lang-href="<?php echo htmlspecialchars($lang_href, ENT_QUOTES, 'UTF-8'); ?>"
	data-parts-url="<?php echo htmlspecialchars($parts_url, ENT_QUOTES, 'UTF-8'); ?>"
	data-slash="<?php echo htmlspecialchars($slash, ENT_QUOTES, 'UTF-8'); ?>"
	data-brand="<?php echo htmlspecialchars($display_brand, ENT_QUOTES, 'UTF-8'); ?>"
	data-brand-url="<?php echo htmlspecialchars($mfr_url, ENT_QUOTES, 'UTF-8'); ?>">
	<div class="epc-brand-browse__hero">
		<div class="epc-brand-browse__hero-main">
			<a class="epc-brand-browse__back" href="<?php echo htmlspecialchars($parts_index_url, ENT_QUOTES, 'UTF-8'); ?>"><i class="fa fa-arrow-left"></i> All brands in stock</a>
			<span class="epc-brand-browse__eyebrow"><i class="fa fa-check-circle"></i> Brand catalog</span>
			<h2 class="epc-brand-browse__title"><?php echo htmlspecialchars($display_brand, ENT_QUOTES, 'UTF-8'); ?></h2>
			<p class="epc-brand-browse__lead">Part numbers in stock for this brand. Use <strong>Fitment check</strong> for vehicle compatibility, or <strong>Search price and availability</strong> for warehouse pricing.</p>
		</div>
		<div class="epc-brand-browse__stat">
			<strong id="epc-brand-browse-count"><?php echo (int) $n; ?></strong>
			<span>Part numbers in stock</span>
		</div>
	</div>

	<form class="epc-brand-browse__search" id="epc-brand-browse-search-form" role="search">
		<label class="epc-brand-browse__search-label" for="epc-brand-browse-article">Search <?php echo htmlspecialchars($display_brand, ENT_QUOTES, 'UTF-8'); ?> by part number</label>
		<div class="epc-brand-browse__search-row">
			<input type="search" class="form-control" id="epc-brand-browse-article" name="article" placeholder="Enter part number" autocomplete="off" />
			<button type="submit" class="btn btn-primary epc-brand-browse__search-btn"><i class="fa fa-search"></i> Search price and availability</button>
		</div>
	</form>

	<div class="epc-brand-browse__filter">
		<input type="search" class="form-control" id="epc-brand-browse-filter" placeholder="Filter listed part numbers…" autocomplete="off" />
	</div>

	<div class="epc-brand-browse__summary" id="epc-brand-browse-summary"></div>
	<div class="epc-brand-browse__pager" id="epc-brand-browse-pager" style="display:none;"></div>
	<div class="epc-brand-browse__list-wrap">
		<div class="epc-brand-browse__list-head" aria-hidden="false">
			<span class="epc-brand-browse__col-h epc-brand-browse__col-h--article"><?php echo htmlspecialchars(translate_str_by_id(4176), ENT_QUOTES, 'UTF-8'); ?></span>
			<span class="epc-brand-browse__col-h epc-brand-browse__col-h--desc"><?php echo htmlspecialchars(translate_str_by_id(2073), ENT_QUOTES, 'UTF-8'); ?></span>
			<span class="epc-brand-browse__col-h epc-brand-browse__col-h--actions">Actions</span>
		</div>
		<div class="epc-brand-browse__list" id="epc-brand-browse-list" role="list"></div>
	</div>
	<div class="epc-brand-browse__empty" id="epc-brand-browse-empty" style="display:none;">No matching part numbers in the current list.</div>
</div>

<?php
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/docpart/epc_fitment_widget.php';
epc_fitment_widget_render();
?>

<script>
window.epcBrandBrowseRows = <?php echo $rows_json; ?>;
(function () {
	'use strict';
	var root = document.getElementById('epc-brand-browse');
	if (!root) {
		return;
	}
	var langHref = root.getAttribute('data-lang-href') || '/en';
	var partsUrl = root.getAttribute('data-parts-url') || 'parts';
	var slashCode = root.getAttribute('data-slash') || '%2F';
	var brand = root.getAttribute('data-brand') || '';
	var brandUrl = root.getAttribute('data-brand-url') || '';
	var rows = Array.isArray(window.epcBrandBrowseRows) ? window.epcBrandBrowseRows.slice(0) : [];
	var listEl = document.getElementById('epc-brand-browse-list');
	var summary = document.getElementById('epc-brand-browse-summary');
	var emptyBox = document.getElementById('epc-brand-browse-empty');
	var filterInput = document.getElementById('epc-brand-browse-filter');
	var searchForm = document.getElementById('epc-brand-browse-search-form');
	var articleInput = document.getElementById('epc-brand-browse-article');
	var pagerBox = document.getElementById('epc-brand-browse-pager');
	var pageSize = 100;
	var currentPage = 1;

	function esc(value) {
		return String(value == null ? '' : value).replace(/[&<>"']/g, function (ch) {
			return ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#039;' })[ch];
		});
	}

	function normalizeArticle(value) {
		return String(value || '').replace(/[\s\-_`\/'"\\.,#]/g, '').toUpperCase();
	}

	function brandArticleUrl(article) {
		var articleNorm = normalizeArticle(article);
		if (!articleNorm) {
			return langHref + '/' + partsUrl + '/' + brandUrl;
		}
		return langHref + '/' + partsUrl + '/' + brandUrl + '/' + encodeURIComponent(articleNorm);
	}

	function filteredRows() {
		var term = filterInput ? filterInput.value.trim().toLowerCase() : '';
		if (!term) {
			return rows;
		}
		return rows.filter(function (row) {
			var article = String(row.article_show || row.article || '').toLowerCase();
			var name = String(row.name || '').toLowerCase();
			return article.indexOf(term) !== -1 || name.indexOf(term) !== -1;
		});
	}

	function openFitment(article, btn) {
		if (typeof window.epcOpenFitmentCheck !== 'function') {
			return;
		}
		window.epcOpenFitmentCheck(article, brand, btn || null);
	}

	function renderPager(totalPages) {
		if (!pagerBox) {
			return;
		}
		if (totalPages <= 1) {
			pagerBox.style.display = 'none';
			pagerBox.innerHTML = '';
			return;
		}
		pagerBox.style.display = 'flex';
		var html = '<button type="button" class="btn btn-default btn-sm" data-page="prev"' + (currentPage <= 1 ? ' disabled' : '') + '>Previous</button>';
		html += '<span class="epc-brand-browse__pager-label">Page <strong>' + currentPage + '</strong> of <strong>' + totalPages + '</strong></span>';
		html += '<button type="button" class="btn btn-default btn-sm" data-page="next"' + (currentPage >= totalPages ? ' disabled' : '') + '>Next</button>';
		pagerBox.innerHTML = html;
		Array.prototype.forEach.call(pagerBox.querySelectorAll('button[data-page]'), function (btn) {
			btn.onclick = function () {
				var dir = btn.getAttribute('data-page');
				if (dir === 'prev' && currentPage > 1) {
					currentPage--;
				} else if (dir === 'next' && currentPage < totalPages) {
					currentPage++;
				}
				render();
			};
		});
	}

	function render() {
		var shown = filteredRows();
		var totalPages = Math.max(1, Math.ceil(shown.length / pageSize));
		if (currentPage > totalPages) {
			currentPage = totalPages;
		}
		if (currentPage < 1) {
			currentPage = 1;
		}
		var start = (currentPage - 1) * pageSize;
		var pageRows = shown.slice(start, start + pageSize);

		if (summary) {
			summary.innerHTML = 'Showing <strong>' + (shown.length ? (start + 1) + '–' + Math.min(start + pageRows.length, shown.length) : '0') + '</strong> of <strong>' + shown.length + '</strong> part numbers for <strong>' + esc(brand) + '</strong>.';
		}
		renderPager(totalPages);

		if (!listEl) {
			return;
		}
		if (!pageRows.length) {
			listEl.innerHTML = '';
			if (emptyBox) {
				emptyBox.style.display = rows.length ? 'block' : 'none';
			}
			return;
		}
		if (emptyBox) {
			emptyBox.style.display = 'none';
		}
		listEl.innerHTML = pageRows.map(function (row) {
			var article = row.article_show || row.article || '';
			var url = brandArticleUrl(article);
			var articleEsc = esc(article);
			var name = esc(row.name || '');
			return '<div class="epc-brand-browse__row" role="listitem">'
				+ '<div class="epc-brand-browse__cell-article"><strong class="epc-brand-browse__article">' + articleEsc + '</strong></div>'
				+ '<div class="epc-brand-browse__cell-desc" title="' + name + '">' + (name || '&mdash;') + '</div>'
				+ '<div class="epc-brand-browse__actions">'
				+ '<button type="button" class="btn btn-sm btn-default epc-fitment-check-btn epc-brand-browse__fitment-btn" data-article="' + articleEsc + '"><i class="fa fa-car"></i> Fitment</button>'
				+ '<a class="btn btn-sm btn-primary epc-brand-browse__price-btn" href="' + esc(url) + '" title="Search price and availability">Price &amp; stock</a>'
				+ '</div></div>';
		}).join('');

		Array.prototype.forEach.call(listEl.querySelectorAll('.epc-brand-browse__fitment-btn'), function (btn) {
			btn.onclick = function () {
				openFitment(btn.getAttribute('data-article') || '', btn);
			};
		});
	}

	if (filterInput) {
		filterInput.oninput = function () {
			currentPage = 1;
			render();
		};
	}
	if (searchForm) {
		searchForm.onsubmit = function (event) {
			if (event && event.preventDefault) {
				event.preventDefault();
			}
			var article = articleInput ? articleInput.value : '';
			var url = brandArticleUrl(article);
			if (url) {
				window.location.href = url;
			}
			return false;
		};
	}
	render();
})();
</script>
