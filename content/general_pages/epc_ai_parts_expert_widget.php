<?php
/**
 * AI Parts Expert — floating footer part-number search (Epart catalog + cross-refs + warehouse).
 */
defined('_ASTEXE_') or die('No access');

require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/docpart/epc_ai_parts_expert.php';

if (!isset($DP_Config) || !epc_ai_expert_enabled($DP_Config)) {
	return;
}

$epc_ai_csrf = epc_ai_expert_csrf_token($DP_Config);
?>
<div id="epc-ai-expert-widget" class="epc-ai-expert-widget" aria-live="polite">
	<button type="button" class="epc-ai-expert-widget__launcher" id="epc-ai-expert-launcher" aria-expanded="false" aria-controls="epc-ai-expert-panel" title="AI Parts Expert — part number lookup">
		<span class="epc-ai-expert-widget__launcher-icon"><i class="fa fa-search" aria-hidden="true"></i></span>
		<span class="epc-ai-expert-widget__launcher-label">Part lookup</span>
		<span class="epc-ai-expert-widget__live">Live</span>
	</button>
	<div class="epc-ai-expert-widget__panel epc-ai-expert-widget__panel--hidden" id="epc-ai-expert-panel" role="dialog" aria-labelledby="epc-ai-expert-panel-title">
		<div class="epc-ai-expert-widget__head">
			<div class="epc-ai-expert-widget__head-main">
				<div class="epc-ai-expert-widget__avatar"><i class="fa fa-magic" aria-hidden="true"></i></div>
				<div>
					<div class="epc-ai-expert-widget__title" id="epc-ai-expert-panel-title">AI Parts Expert</div>
					<div class="epc-ai-expert-widget__subtitle">Part number · vehicle fitment · warehouse stock</div>
				</div>
			</div>
			<button type="button" class="epc-ai-expert-widget__close" id="epc-ai-expert-close" aria-label="Close panel">&times;</button>
		</div>
		<div class="epc-ai-expert-widget__body">
			<form class="epc-ai-expert-widget__form" id="epc-ai-expert-form" autocomplete="off">
				<label class="sr-only" for="epc-ai-expert-article">Part number</label>
				<input type="text" class="epc-ai-expert-widget__input" id="epc-ai-expert-article" name="article"
					placeholder="Part number — e.g. C110J, DT068" maxlength="64" />
				<label class="sr-only" for="epc-ai-expert-brand">Brand (optional)</label>
				<input type="text" class="epc-ai-expert-widget__input epc-ai-expert-widget__input--brand" id="epc-ai-expert-brand" name="brand"
					placeholder="Brand (optional)" maxlength="48" />
				<button type="submit" class="epc-ai-expert-widget__submit" id="epc-ai-expert-submit">
					<i class="fa fa-search" aria-hidden="true"></i> Search
				</button>
			</form>
			<p class="epc-ai-expert-widget__hint" id="epc-ai-expert-hint" aria-live="polite">
				<span class="epc-ai-expert-widget__pulse" aria-hidden="true"></span>
				Vehicle fitment · cross-references · UAE warehouse
			</p>
			<div class="epc-ai-expert-widget__results epc-ai-expert-widget__results--hidden" id="epc-ai-expert-results" aria-live="polite"></div>
		</div>
	</div>
</div>

<style>
.epc-ai-expert-widget {
	--epc-ai-accent: #ff8c1a;
	--epc-ai-accent-dark: #e65c00;
	position: fixed;
	right: 18px;
	bottom: 96px;
	z-index: 10055;
	font-family: inherit;
}
.epc-ai-expert-widget__launcher {
	position: relative;
	display: flex;
	align-items: center;
	gap: 10px;
	border: 2px solid rgba(255, 255, 255, .35);
	border-radius: 999px;
	padding: 11px 18px 11px 12px;
	background: linear-gradient(120deg, #1e293b 0%, #334155 50%, #0f172a 100%);
	background-size: 200% 200%;
	color: #fff;
	box-shadow: 0 8px 28px rgba(15, 23, 42, .45);
	cursor: pointer;
	font-size: 12px;
	font-weight: 800;
	letter-spacing: .04em;
	text-transform: uppercase;
	animation: epcAiExpertFabFloat 3.4s ease-in-out infinite;
}
.epc-ai-expert-widget__launcher:hover {
	transform: translateY(-3px);
	box-shadow: 0 12px 36px rgba(15, 23, 42, .55);
}
.epc-ai-expert-widget--open .epc-ai-expert-widget__launcher {
	animation: none;
	opacity: .92;
}
.epc-ai-expert-widget__launcher-icon {
	display: flex;
	align-items: center;
	justify-content: center;
	width: 34px;
	height: 34px;
	border-radius: 50%;
	background: linear-gradient(135deg, var(--epc-ai-accent), var(--epc-ai-accent-dark));
	font-size: 15px;
}
.epc-ai-expert-widget__live {
	position: absolute;
	top: -6px;
	right: -2px;
	padding: 3px 8px;
	border-radius: 999px;
	background: linear-gradient(135deg, #22c55e, #16a34a);
	color: #fff;
	font-size: 9px;
	font-weight: 800;
	border: 2px solid #fff;
}
@keyframes epcAiExpertFabFloat {
	0%, 100% { transform: translateY(0); }
	50% { transform: translateY(-4px); }
}
.epc-ai-expert-widget__panel {
	position: absolute;
	right: 0;
	bottom: calc(100% + 12px);
	width: min(400px, calc(100vw - 24px));
	max-height: min(560px, calc(100vh - 120px));
	display: flex;
	flex-direction: column;
	overflow: hidden;
	border-radius: 16px;
	border: 1px solid #e2e8f0;
	background: #fff;
	box-shadow: 0 20px 60px rgba(15, 23, 42, .22);
	animation: epcAiExpertSlideUp .25s ease;
}
.epc-ai-expert-widget__panel--hidden { display: none; }
@keyframes epcAiExpertSlideUp {
	from { opacity: 0; transform: translateY(10px); }
	to { opacity: 1; transform: none; }
}
.epc-ai-expert-widget__head {
	display: flex;
	align-items: center;
	justify-content: space-between;
	padding: 14px;
	background: #1a1f2e;
	color: #fff;
}
.epc-ai-expert-widget__head-main { display: flex; align-items: center; gap: 10px; }
.epc-ai-expert-widget__avatar {
	width: 40px;
	height: 40px;
	border-radius: 12px;
	background: linear-gradient(135deg, var(--epc-ai-accent), var(--epc-ai-accent-dark));
	display: flex;
	align-items: center;
	justify-content: center;
	font-size: 18px;
}
.epc-ai-expert-widget__title { font-weight: 700; font-size: 15px; }
.epc-ai-expert-widget__subtitle { font-size: 11px; opacity: .75; margin-top: 2px; }
.epc-ai-expert-widget__close {
	border: none;
	background: rgba(255,255,255,.12);
	color: #fff;
	width: 32px;
	height: 32px;
	border-radius: 8px;
	font-size: 20px;
	cursor: pointer;
}
.epc-ai-expert-widget__body {
	flex: 1;
	overflow-y: auto;
	padding: 14px;
	background: #f8fafc;
}
.epc-ai-expert-widget__form {
	display: grid;
	grid-template-columns: 1fr auto;
	gap: 8px;
	margin-bottom: 10px;
}
.epc-ai-expert-widget__input {
	grid-column: 1 / 2;
	border: 1px solid #cbd5e1;
	border-radius: 10px;
	padding: 10px 12px;
	font-size: 13px;
	outline: none;
	background: #fff;
}
.epc-ai-expert-widget__input:focus {
	border-color: var(--epc-ai-accent);
	box-shadow: 0 0 0 3px rgba(255, 140, 26, .15);
}
.epc-ai-expert-widget__input--brand { grid-column: 1 / 2; }
.epc-ai-expert-widget__submit {
	grid-column: 2 / 3;
	grid-row: 1 / 3;
	align-self: stretch;
	border: none;
	border-radius: 10px;
	padding: 0 16px;
	background: linear-gradient(135deg, var(--epc-ai-accent), var(--epc-ai-accent-dark));
	color: #fff;
	font-weight: 700;
	font-size: 13px;
	cursor: pointer;
}
.epc-ai-expert-widget__submit:disabled { opacity: .55; cursor: wait; }
.epc-ai-expert-widget__hint {
	font-size: 12px;
	color: #64748b;
	margin: 0 0 10px;
	display: flex;
	align-items: center;
	gap: 8px;
}
.epc-ai-expert-widget__pulse {
	width: 8px;
	height: 8px;
	border-radius: 50%;
	background: var(--epc-ai-accent);
	animation: epcAiExpertPulse 1.2s ease-in-out infinite;
}
@keyframes epcAiExpertPulse {
	0%, 100% { opacity: .35; transform: scale(.85); }
	50% { opacity: 1; transform: scale(1); }
}
.epc-ai-expert-widget__results--hidden { display: none; }
.epc-ai-expert-widget__sections { display: flex; flex-direction: column; gap: 12px; }
.epc-ai-expert-widget__section {
	background: #fff;
	border: 1px solid #e2e8f0;
	border-radius: 12px;
	padding: 12px;
}
.epc-ai-expert-widget__section h4 {
	margin: 0 0 8px;
	font-size: 13px;
	color: #0f172a;
}
.epc-ai-expert-widget__table-wrap { overflow-x: auto; }
.epc-ai-expert-widget__table { font-size: 12px; margin: 0; }
.epc-ai-expert-widget__cross-list {
	margin: 0;
	padding-left: 18px;
	font-size: 12px;
	line-height: 1.6;
}
.epc-ai-expert-widget__empty {
	margin: 0;
	font-size: 12px;
	color: #64748b;
}
.epc-ai-expert-widget__alert {
	font-size: 12px;
	padding: 8px 10px;
	margin-bottom: 8px;
}
.epc-ai-expert-widget__brands { margin-bottom: 8px; }
.epc-ai-expert-widget__brands .btn { margin: 2px 4px 2px 0; }
@media (max-width: 479px) {
	.epc-ai-expert-widget { right: 12px; bottom: 88px; }
	.epc-ai-expert-widget__form { grid-template-columns: 1fr; }
	.epc-ai-expert-widget__submit { grid-column: 1; grid-row: auto; padding: 10px; }
	.epc-ai-expert-widget__panel {
		position: fixed;
		left: 0;
		right: 0;
		bottom: 0;
		width: 100%;
		max-height: min(85vh, 560px);
		border-radius: 16px 16px 0 0;
	}
}
@media (prefers-reduced-motion: reduce) {
	.epc-ai-expert-widget__launcher,
	.epc-ai-expert-widget__pulse { animation: none !important; }
}
</style>

<script>
(function () {
	var API = '/api/epc_ai_parts_expert.php';
	var csrf = <?php echo json_encode($epc_ai_csrf, JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES); ?>;
	var root = document.getElementById('epc-ai-expert-widget');
	var launcher = document.getElementById('epc-ai-expert-launcher');
	var panel = document.getElementById('epc-ai-expert-panel');
	var closeBtn = document.getElementById('epc-ai-expert-close');
	var form = document.getElementById('epc-ai-expert-form');
	var articleEl = document.getElementById('epc-ai-expert-article');
	var brandEl = document.getElementById('epc-ai-expert-brand');
	var resultsEl = document.getElementById('epc-ai-expert-results');
	var hintEl = document.getElementById('epc-ai-expert-hint');
	var submitBtn = document.getElementById('epc-ai-expert-submit');
	if (!root || !form || !articleEl || !resultsEl) { return; }

	function esc(s) {
		return String(s == null ? '' : s).replace(/[&<>"']/g, function (ch) {
			return ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#039;' })[ch];
		});
	}
	function setHint(text, loading) {
		if (!hintEl) { return; }
		hintEl.innerHTML = (loading ? '<span class="epc-ai-expert-widget__pulse" aria-hidden="true"></span> ' : '') + esc(text);
	}
	function setOpen(open) {
		if (!panel || !launcher) { return; }
		panel.classList.toggle('epc-ai-expert-widget__panel--hidden', !open);
		root.classList.toggle('epc-ai-expert-widget--open', open);
		launcher.setAttribute('aria-expanded', open ? 'true' : 'false');
		if (open) {
			setTimeout(function () { if (articleEl) { articleEl.focus(); } }, 200);
		}
	}
	function stockRows(stock) {
		if (!stock || !stock.length) {
			return '<p class="epc-ai-expert-widget__empty">No matching rows in the uploaded price list.</p>';
		}
		var html = '<div class="epc-ai-expert-widget__table-wrap"><table class="table table-condensed epc-ai-expert-widget__table"><thead><tr>'
			+ '<th>Brand</th><th>Article</th><th>Name</th><th>Qty</th><th>Price</th><th></th></tr></thead><tbody>';
		stock.forEach(function (row) {
			var link = row.url ? '<a class="btn btn-xs btn-primary" href="' + esc(row.url) + '">Open</a>' : '';
			html += '<tr><td>' + esc(row.brand) + '</td><td><strong>' + esc(row.article) + '</strong></td><td>' + esc(row.name) + '</td>'
				+ '<td>' + esc(row.qty) + '</td><td>' + esc(row.price) + '</td><td>' + link + '</td></tr>';
		});
		return html + '</tbody></table></div>';
	}
	function crossRows(refs) {
		if (!refs || !refs.length) {
			return '<p class="epc-ai-expert-widget__empty">No cross-references for this number.</p>';
		}
		var html = '<ul class="epc-ai-expert-widget__cross-list">';
		refs.slice(0, 20).forEach(function (ref) {
			var label = (ref.brand ? ref.brand + ' ' : '') + ref.article;
			html += ref.url ? '<li><a href="' + esc(ref.url) + '">' + esc(label) + '</a></li>' : '<li>' + esc(label) + '</li>';
		});
		if (refs.length > 20) { html += '<li>+' + (refs.length - 20) + ' more</li>'; }
		return html + '</ul>';
	}
	function fitmentBlock(data, partUrl) {
		var fit = data.fitment || {};
		var rows = fit.rows || fit.sample || [];
		var head = '';
		if (fit.part_name) {
			head += '<p><strong>' + esc(fit.part_name) + '</strong></p>';
		}
		if (data.brand) {
			head += '<p class="epc-ai-expert-widget__empty">Fitment for <strong>' + esc(data.brand) + '</strong></p>';
		}
		if (partUrl) {
			head += '<p><a class="btn btn-xs btn-default" href="' + esc(partUrl) + '">Full part page</a></p>';
		}
		if (!rows.length) {
			return head + '<p class="epc-ai-expert-widget__empty">No vehicle fitment in Epart catalog.</p>';
		}
		var html = head + '<div class="epc-ai-expert-widget__table-wrap"><table class="table table-condensed table-striped epc-ai-expert-widget__table"><thead><tr>'
			+ '<th>Make</th><th>Model</th><th>Years</th><th>Engine</th></tr></thead><tbody>';
		rows.slice(0, 12).forEach(function (row) {
			html += '<tr><td>' + esc(row.make) + '</td><td>' + esc(row.model) + '</td>'
				+ '<td>' + esc(row.years) + '</td><td>' + esc(row.engine) + '</td></tr>';
		});
		return html + '</tbody></table></div>';
	}
	function renderResult(data) {
		var msgs = (data.messages || []).map(function (m) {
			return '<div class="alert alert-warning epc-ai-expert-widget__alert">' + esc(m) + '</div>';
		}).join('');
		var brands = '';
		if (data.umapi_brands && data.umapi_brands.length > 1) {
			brands = '<div class="epc-ai-expert-widget__brands">';
			data.umapi_brands.slice(0, 6).forEach(function (b) {
				brands += '<button type="button" class="btn btn-xs btn-default epc-ai-expert-brand-pick" data-brand="' + esc(b.brand) + '">' + esc(b.brand) + '</button>';
			});
			brands += '</div>';
		}
		resultsEl.innerHTML = msgs
			+ '<div class="epc-ai-expert-widget__sections">'
			+ '<section class="epc-ai-expert-widget__section"><h4><i class="fa fa-cubes"></i> Warehouse stock</h4>' + stockRows(data.local_stock) + '</section>'
			+ '<section class="epc-ai-expert-widget__section"><h4><i class="fa fa-exchange"></i> Cross-references</h4>' + crossRows(data.cross_refs) + '</section>'
			+ '<section class="epc-ai-expert-widget__section"><h4><i class="fa fa-car"></i> Vehicle fitment</h4>' + brands + fitmentBlock(data, data.part_url) + '</section>'
			+ '</div>';
		resultsEl.classList.remove('epc-ai-expert-widget__results--hidden');
		Array.prototype.forEach.call(resultsEl.querySelectorAll('.epc-ai-expert-brand-pick'), function (btn) {
			btn.onclick = function () {
				brandEl.value = btn.getAttribute('data-brand') || '';
				form.dispatchEvent(new Event('submit', { cancelable: true, bubbles: true }));
			};
		});
	}

	if (launcher) {
		launcher.addEventListener('click', function () {
			var open = !panel.classList.contains('epc-ai-expert-widget__panel--hidden');
			setOpen(!open);
		});
	}
	if (closeBtn) { closeBtn.addEventListener('click', function () { setOpen(false); }); }

	form.addEventListener('submit', function (event) {
		event.preventDefault();
		var article = (articleEl.value || '').trim();
		if (article.length < 3) {
			setHint('Enter at least 3 characters for the part number.', false);
			return;
		}
		submitBtn.disabled = true;
		setHint('Searching Epart catalog, cross-references, and warehouse…', true);
		resultsEl.classList.add('epc-ai-expert-widget__results--hidden');
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
