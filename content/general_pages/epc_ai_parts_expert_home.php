<?php
/**
 * Homepage hero — AI Parts Expert part-number search (Epart catalog + cross-refs + warehouse).
 */
defined('_ASTEXE_') or die('No access');

require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/docpart/epc_ai_parts_expert.php';

if (!isset($DP_Config) || !epc_ai_expert_enabled($DP_Config)) {
	return;
}

$epc_ai_csrf = epc_ai_expert_csrf_token($DP_Config);
$lang = '/en';
if (!empty($GLOBALS['multilang_params']['lang_href'])) {
	$lang = (string)$GLOBALS['multilang_params']['lang_href'];
}
?>
<div class="epc-ai-expert-home" id="epc-ai-expert-home" aria-labelledby="epc-ai-expert-title">
		<div class="epc-ai-expert-home__card">
			<div class="epc-ai-expert-home__head">
				<span class="epc-ai-expert-home__badge"><i class="fa fa-magic" aria-hidden="true"></i> AI Parts Expert</span>
				<h3 class="epc-ai-expert-home__title" id="epc-ai-expert-title">Look up any part number</h3>
				<p class="epc-ai-expert-home__lead">Instant fitment from Epart catalog, cross-references from the supplier network, and live UAE warehouse stock.</p>
			</div>
			<form class="epc-ai-expert-home__form" id="epc-ai-expert-form" autocomplete="off">
				<label class="sr-only" for="epc-ai-expert-article">Part number</label>
				<input type="text" class="epc-ai-expert-home__input" id="epc-ai-expert-article" name="article"
					placeholder="Enter part number — e.g. C110J, DT068, 90915-YZZD2" maxlength="64" />
				<label class="sr-only" for="epc-ai-expert-brand">Brand (optional)</label>
				<input type="text" class="epc-ai-expert-home__input epc-ai-expert-home__input--brand" id="epc-ai-expert-brand" name="brand"
					placeholder="Brand (optional)" maxlength="48" />
				<button type="submit" class="epc-ai-expert-home__submit" id="epc-ai-expert-submit">
					<i class="fa fa-search" aria-hidden="true"></i> Search
				</button>
			</form>
			<p class="epc-ai-expert-home__hint" id="epc-ai-expert-hint" aria-live="polite">
				<span class="epc-ai-expert-home__pulse" aria-hidden="true"></span>
				Checking vehicle fitment · cross-references · warehouse price list
			</p>
			<div class="epc-ai-expert-home__results epc-ai-expert-home__results--hidden" id="epc-ai-expert-results" aria-live="polite"></div>
		</div>
</div>
<script>
(function () {
	var API = '/api/epc_ai_parts_expert.php';
	var csrf = <?php echo json_encode($epc_ai_csrf, JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES); ?>;
	var form = document.getElementById('epc-ai-expert-form');
	var articleEl = document.getElementById('epc-ai-expert-article');
	var brandEl = document.getElementById('epc-ai-expert-brand');
	var resultsEl = document.getElementById('epc-ai-expert-results');
	var hintEl = document.getElementById('epc-ai-expert-hint');
	var submitBtn = document.getElementById('epc-ai-expert-submit');
	if (!form || !articleEl || !resultsEl) { return; }

	function esc(s) {
		return String(s == null ? '' : s).replace(/[&<>"']/g, function (ch) {
			return ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#039;' })[ch];
		});
	}
	function setHint(text, loading) {
		if (!hintEl) { return; }
		hintEl.innerHTML = (loading ? '<span class="epc-ai-expert-home__pulse" aria-hidden="true"></span> ' : '') + esc(text);
	}
	function stockRows(stock) {
		if (!stock || !stock.length) {
			return '<p class="epc-ai-expert-home__empty">No matching rows in the uploaded price list for this number.</p>';
		}
		var html = '<div class="epc-ai-expert-home__table-wrap"><table class="table table-condensed epc-ai-expert-home__table"><thead><tr>'
			+ '<th>Brand</th><th>Article</th><th>Name</th><th>Warehouse</th><th>Qty</th><th>Price</th><th></th></tr></thead><tbody>';
		stock.forEach(function (row) {
			var link = row.url ? '<a class="btn btn-xs btn-primary" href="' + esc(row.url) + '">Part page</a>' : '';
			html += '<tr><td>' + esc(row.brand) + '</td><td><strong>' + esc(row.article) + '</strong></td><td>' + esc(row.name) + '</td>'
				+ '<td>' + esc(row.warehouse) + '</td><td>' + esc(row.qty) + '</td><td>' + esc(row.price) + '</td><td>' + link + '</td></tr>';
		});
		return html + '</tbody></table></div>';
	}
	function crossRows(refs) {
		if (!refs || !refs.length) {
			return '<p class="epc-ai-expert-home__empty">No cross-references returned for this number.</p>';
		}
		var html = '<ul class="epc-ai-expert-home__cross-list">';
		refs.slice(0, 24).forEach(function (ref) {
			var label = (ref.brand ? ref.brand + ' ' : '') + ref.article;
			if (ref.url) {
				html += '<li><a href="' + esc(ref.url) + '">' + esc(label) + '</a></li>';
			} else {
				html += '<li>' + esc(label) + '</li>';
			}
		});
		if (refs.length > 24) {
			html += '<li class="epc-ai-expert-home__more">+' + (refs.length - 24) + ' more in catalog</li>';
		}
		return html + '</ul>';
	}
	function fitmentBlock(data, partUrl) {
		var fit = data.fitment || {};
		var rows = fit.rows || fit.sample || [];
		var head = '';
		if (fit.part_name) {
			head += '<p class="epc-ai-expert-home__part-name"><strong>' + esc(fit.part_name) + '</strong>';
			if (fit.product_group) { head += ' <span class="text-muted">· ' + esc(fit.product_group) + '</span>'; }
			head += '</p>';
		}
		if (data.brand) {
			head += '<p class="epc-ai-expert-home__fit-brand">Fitment for <strong>' + esc(data.brand) + '</strong> · ' + esc(data.article) + '</p>';
		}
		if (partUrl) {
			head += '<p><a class="btn btn-sm btn-default" href="' + esc(partUrl) + '"><i class="fa fa-external-link"></i> Open full part page</a></p>';
		}
		if (!rows.length) {
			return head + '<p class="epc-ai-expert-home__empty">No vehicle fitment found in Epart catalog for this number.</p>';
		}
		var count = fit.vehicle_count || rows.length;
		var html = head + '<p class="epc-ai-expert-home__fit-count">' + esc(String(count)) + ' vehicle' + (count === 1 ? '' : 's') + ' (showing ' + rows.length + ')</p>';
		html += '<div class="epc-ai-expert-home__table-wrap"><table class="table table-condensed table-striped epc-ai-expert-home__table"><thead><tr>'
			+ '<th>Make</th><th>Model</th><th>Modification</th><th>Years</th><th>Power</th><th>Engine</th></tr></thead><tbody>';
		rows.forEach(function (row) {
			html += '<tr><td>' + esc(row.make) + '</td><td>' + esc(row.model) + '</td><td>' + esc(row.modification) + '</td>'
				+ '<td>' + esc(row.years) + '</td><td>' + esc(row.power) + '</td><td>' + esc(row.engine) + '</td></tr>';
		});
		return html + '</tbody></table></div>';
	}
	function renderResult(data) {
		var msgs = (data.messages || []).map(function (m) {
			return '<div class="alert alert-warning epc-ai-expert-home__alert">' + esc(m) + '</div>';
		}).join('');
		var brands = '';
		if (data.umapi_brands && data.umapi_brands.length > 1) {
			brands = '<div class="epc-ai-expert-home__brands"><span class="text-muted">Epart catalog brands:</span> ';
			brands += data.umapi_brands.slice(0, 8).map(function (b) {
				return '<button type="button" class="btn btn-xs btn-default epc-ai-expert-brand-pick" data-brand="' + esc(b.brand) + '">' + esc(b.brand) + '</button>';
			}).join(' ');
			brands += '</div>';
		}
		resultsEl.innerHTML = msgs
			+ '<div class="epc-ai-expert-home__sections">'
			+ '<section class="epc-ai-expert-home__section"><h4><i class="fa fa-cubes"></i> Warehouse stock</h4>' + stockRows(data.local_stock) + '</section>'
			+ '<section class="epc-ai-expert-home__section"><h4><i class="fa fa-exchange"></i> Cross-references</h4>' + crossRows(data.cross_refs) + '</section>'
			+ '<section class="epc-ai-expert-home__section"><h4><i class="fa fa-car"></i> Vehicle fitment (Epart catalog)</h4>' + brands + fitmentBlock(data, data.part_url) + '</section>'
			+ '</div>';
		resultsEl.classList.remove('epc-ai-expert-home__results--hidden');
		Array.prototype.forEach.call(resultsEl.querySelectorAll('.epc-ai-expert-brand-pick'), function (btn) {
			btn.onclick = function () {
				brandEl.value = btn.getAttribute('data-brand') || '';
				form.dispatchEvent(new Event('submit', { cancelable: true, bubbles: true }));
			};
		});
	}

	form.addEventListener('submit', function (event) {
		event.preventDefault();
		var article = (articleEl.value || '').trim();
		if (article.length < 3) {
			setHint('Enter at least 3 characters for the part number.', false);
			return;
		}
		submitBtn.disabled = true;
		setHint('Searching Epart catalog, cross-references, and warehouse…', true);
		resultsEl.classList.add('epc-ai-expert-home__results--hidden');
		var body = new URLSearchParams();
		body.set('action', 'search');
		body.set('article', article);
		body.set('brand', (brandEl.value || '').trim());
		body.set('csrf', csrf);
		fetch(API, { method: 'POST', headers: { 'Content-Type': 'application/x-www-form-urlencoded' }, body: body.toString(), cache: 'no-store' })
			.then(function (r) { return r.json().then(function (j) { return { status: r.status, json: j }; }); })
			.then(function (pack) {
				submitBtn.disabled = false;
				var data = pack.json || {};
				if (!data.ok) {
					if (pack.status === 403 && data.message) {
						return fetch(API + '?action=bootstrap', { cache: 'no-store' })
							.then(function (r2) { return r2.json(); })
							.then(function (b) {
								if (b && b.csrf) { csrf = b.csrf; }
								setHint(data.message || 'Search failed.', false);
							});
					}
					setHint(data.message || 'Search failed.', false);
					return;
				}
				setHint('Results for ' + article + (data.brand ? ' · ' + data.brand : ''), false);
				renderResult(data);
			})
			.catch(function () {
				submitBtn.disabled = false;
				setHint('Network error — please try again.', false);
			});
	});
})();
</script>
