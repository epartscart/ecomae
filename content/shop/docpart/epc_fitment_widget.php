<?php
/**
 * Shared UMAPI fitment panel (used on part search and /parts/{BRAND} browse).
 */
if (!defined('_ASTEXE_')) {
	define('_ASTEXE_', 1);
}

function epc_fitment_widget_render()
{
	static $rendered = false;
	if ($rendered) {
		return;
	}
	$rendered = true;
	?>
<div class="epc-fitment-panel" id="epc-fitment-panel" aria-live="polite">
	<div class="epc-fitment-panel__head">
		<div>
			<div class="epc-fitment-panel__title">Part Fitment</div>
			<div class="epc-fitment-panel__hint">Choose the matching brand/number — photo, specifications and vehicle fitment load automatically.</div>
		</div>
		<button type="button" class="epc-fitment-panel__close" id="epc-fitment-close" aria-label="Close fitment check">&times;</button>
	</div>
	<div class="epc-fitment-panel__body">
		<div id="epc-fitment-brands" class="epc-fitment-message">Loading matching brands from Epart catalog…</div>
		<div class="epc-fitment-type-tabs" id="epc-fitment-types" style="display:none;">
			<button type="button" data-section="PC" class="active">Passenger</button>
			<button type="button" data-section="CV">Commercial</button>
			<button type="button" data-section="Motorcycle">Motorbike</button>
			<button type="button" data-section="ALL">All vehicles</button>
		</div>
		<div class="epc-fitment-widget-shell" id="epc-fitment-widget-shell" style="display:none;">
			<div id="epc-fitment-part" class="epc-fitment-part" style="display:none;" aria-live="polite"></div>
			<div id="applicability_widget" class="epc-fitment-message">Select a brand/part box to load fitment.</div>
		</div>
	</div>
</div>
<script>
(function(){
	if (window.epcFitmentWidgetReady) { return; }
	window.epcFitmentWidgetReady = true;
	var button = document.getElementById('epc-fitment-check-btn');
	var panel = document.getElementById('epc-fitment-panel');
	var close = document.getElementById('epc-fitment-close');
	var brandsBox = document.getElementById('epc-fitment-brands');
	var typesBox = document.getElementById('epc-fitment-types');
	var widgetShell = document.getElementById('epc-fitment-widget-shell');
	var selectedArticle = '';
	var selectedBrand = '';
	var selectedSection = 'PC';
	var selectedFitment = null;
	var partBox = document.getElementById('epc-fitment-part');
	var brandsLoaded = false;
	var brandsLoadedArticle = '';
	var pendingPreferredBrand = '';
	if (!panel || !brandsBox) { return; }
	function esc(value) {
		return String(value == null ? '' : value).replace(/[&<>"']/g, function (ch) {
			return ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#039;' })[ch];
		});
	}
	function compact(value) {
		return String(value || '').replace(/[^A-Za-z0-9]/g, '').toUpperCase();
	}
	function defaultArticle() {
		if (selectedArticle) { return selectedArticle; }
		if (button) { return button.getAttribute('data-article') || ''; }
		return '';
	}
	function fitmentArticle(row) {
		return row.DISPLAY_NR || row.SEARCH_NUMBER || row.ARTICLE || defaultArticle();
	}
	function fitmentDisplay(row) {
		return row.DISPLAY_NR || row.SEARCH_NUMBER || defaultArticle();
	}
	function brandsEquivalent(left, right) {
		return compact(left) !== '' && compact(left) === compact(right);
	}
	function setMessage(message) {
		brandsBox.className = 'epc-fitment-message';
		brandsBox.innerHTML = message;
	}
	function api(action, params) {
		var query = Object.keys(params || {}).map(function (key) {
			return encodeURIComponent(key) + '=' + encodeURIComponent(params[key]);
		}).join('&');
		var url = '/api/umapi_proxy.php?action=' + encodeURIComponent(action) + (query ? '&' + query : '') + '&language=en&vehicle_type=PC';
		return fetch(url, { cache: 'no-store', credentials: 'same-origin' })
			.then(function (response) {
				return response.json().catch(function () { return {}; }).then(function (data) {
					if (response.ok) { return data; }
					if (data && (Array.isArray(data.data) || data.PC || data.CV || data.Motorcycle)) { return data; }
					var err = new Error((data && data.message) ? String(data.message) : ('HTTP ' + response.status));
					err.status = response.status;
					err.data = data;
					return Promise.reject(err);
				});
			});
	}
	function fitmentErrorMessage(err, fallback) {
		if (err && err.data && err.data.message) { return String(err.data.message); }
		if (err && err.message) { return String(err.message); }
		return fallback || 'Fitment lookup is temporarily unavailable.';
	}
	function loadEpartscrossFitmentFallback(article, widget) {
		if (!widget || !article) { return; }
		widget.className = 'epc-fitment-message';
		widget.innerHTML = '<div class="epc-fitment-message">Loading vehicle applicability from epartscross...</div>';
		var oldScript = document.getElementById('epc-fitment-epartscross-script');
		if (oldScript && oldScript.parentNode) { oldScript.parentNode.removeChild(oldScript); }
		var script = document.createElement('script');
		script.id = 'epc-fitment-epartscross-script';
		script.type = 'text/javascript';
		script.async = true;
		script.onerror = function () {
			widget.innerHTML = '<div class="epc-fitment-message">Vehicle fitment is temporarily unavailable. Update the Epart catalog API key in Control Panel or try again later.</div>';
		};
		var lang = (document.documentElement.getAttribute('lang') || 'en').toLowerCase();
		if (lang !== 'ru') { lang = 'en'; }
		script.src = '/api/epartscross_fitment.js.php?n=' + encodeURIComponent(article) + '&lang=' + encodeURIComponent(lang) + '&_=' + Date.now();
		document.body.appendChild(script);
	}
	function rowsFromPayload(data) {
		return Array.isArray(data) ? data : (data && Array.isArray(data.data) ? data.data : []);
	}
	function yearRange(row) {
		var from = row.CI_FROM || '';
		var to = row.CI_TO || '';
		if (from && to) { return from + ' - ' + to; }
		if (from) { return from + ' - now'; }
		return to || '';
	}
	function fitmentPowerText(row) {
		var kw = row.POWER_KW || row.POWER_KW_START || '';
		var ps = row.POWER_PS || row.POWER_PS_START || '';
		if (kw && ps) { return kw + ' kW / ' + ps + ' PS'; }
		if (kw) { return String(kw) + ' kW'; }
		if (ps) { return String(ps) + ' PS'; }
		return '';
	}
	function fitmentEngineText(row) {
		return [row.CAPACITY_TECH || row.CAPACITY_LT || '', row.FUEL_TYPE || '', row.BODY_TYPE || row.PLATFORM_TYPE || ''].filter(Boolean).join(' / ');
	}
	function fitmentModificationText(row) {
		return row.PASSENGER_CAR || row.COMMERCIAL_VEHICLE || row.MOTORBIKE || '';
	}
	function csvValue(value) {
		var text = String(value == null ? '' : value);
		if (/[",\r\n]/.test(text)) {
			return '"' + text.replace(/"/g, '""') + '"';
		}
		return text;
	}
	function downloadFitmentExcel(rows) {
		if (!rows || !rows.length) { return; }
		var sectionLabel = selectedSection === 'ALL' ? 'All vehicles' : selectedSection;
		var sheetRows = [
			['Part brand', selectedBrand || ''],
			['Part number', selectedArticle || defaultArticle() || ''],
			['Vehicle type', sectionLabel],
			['Exported', new Date().toISOString().slice(0, 19).replace('T', ' ')],
			[],
			['Make', 'Model', 'Modification', 'Year', 'Power', 'Engine / fuel']
		];
		rows.forEach(function (row) {
			sheetRows.push([
				row.MANUFACTURER || '',
				row.MODEL_SERIES || '',
				fitmentModificationText(row),
				yearRange(row),
				fitmentPowerText(row),
				fitmentEngineText(row)
			]);
		});
		var csv = sheetRows.map(function (line) {
			return line.map(csvValue).join(',');
		}).join('\r\n');
		var blob = new Blob(['\ufeff' + csv], { type: 'text/csv;charset=utf-8;' });
		var link = document.createElement('a');
		var safeBrand = String(selectedBrand || 'brand').replace(/[^\w\-]+/g, '_');
		var safeArticle = String(selectedArticle || defaultArticle() || 'part').replace(/[^\w\-]+/g, '_');
		link.href = URL.createObjectURL(blob);
		link.download = safeBrand + '-' + safeArticle + '-fitment-' + String(selectedSection || 'PC').toLowerCase() + '.csv';
		document.body.appendChild(link);
		link.click();
		window.setTimeout(function () {
			URL.revokeObjectURL(link.href);
			if (link.parentNode) { link.parentNode.removeChild(link); }
		}, 100);
	}
	function criteriaList(detail) {
		var rows = [];
		['CRITERIAS', 'LA_CRITERIAS'].forEach(function (key) {
			var list = detail && detail[key];
			if (Array.isArray(list)) { list.forEach(function (item) { rows.push(item); }); }
		});
		return rows;
	}
	function criteriaFind(detail, patterns) {
		var list = criteriaList(detail);
		for (var i = 0; i < list.length; i++) {
			var label = String(list[i].CRI_DES || list[i].CRI_SHORT_DES || '').toLowerCase();
			for (var j = 0; j < patterns.length; j++) {
				if (label.indexOf(patterns[j]) !== -1) {
					var value = list[i].VALUE || list[i].DES || '';
					if (value !== '' && value !== null) {
						return String(value) + (list[i].CRI_UNIT_DES ? ' ' + list[i].CRI_UNIT_DES : '');
					}
				}
			}
		}
		return '';
	}
	function articleImageUrl(detail) {
		if (!detail || !detail.MEDIA_FILE || !detail.SUP_ID) { return ''; }
		return 'https://image.umapi.ru/IMAGE/' + encodeURIComponent(detail.SUP_ID) + '/' + encodeURIComponent(detail.MEDIA_FILE);
	}
	var brandImageCache = {};
	function brandImageKey(brand, article) {
		return compact(brand) + '|' + compact(article);
	}
	function ensureImageLightbox() {
		if (document.getElementById('epc-image-lightbox')) { return; }
		var el = document.createElement('div');
		el.id = 'epc-image-lightbox';
		el.className = 'epc-image-lightbox';
		el.innerHTML = '<div class="epc-image-lightbox__backdrop"></div><div class="epc-image-lightbox__panel"><button type="button" class="epc-image-lightbox__close" aria-label="Close photo">&times;</button><img src="" alt=""></div>';
		document.body.appendChild(el);
		el.querySelector('.epc-image-lightbox__backdrop').onclick = function () { epcCloseImageLightbox(); };
		el.querySelector('.epc-image-lightbox__close').onclick = function () { epcCloseImageLightbox(); };
	}
	function epcCloseImageLightbox() {
		var el = document.getElementById('epc-image-lightbox');
		if (el) { el.classList.remove('active'); }
	}
	function epcOpenImageLightbox(url, alt) {
		if (!url) { return; }
		ensureImageLightbox();
		var el = document.getElementById('epc-image-lightbox');
		var img = el.querySelector('img');
		img.src = url;
		img.alt = alt || '';
		el.classList.add('active');
	}
	window.epcOpenImageLightbox = epcOpenImageLightbox;
	function bindPartImageClick(container, url, alt) {
		if (!container || !url) { return; }
		var media = container.querySelector('.epc-fitment-part-card__media');
		if (!media) { return; }
		media.classList.add('epc-fitment-part-card__media--clickable');
		media.setAttribute('role', 'button');
		media.setAttribute('tabindex', '0');
		media.setAttribute('aria-label', 'View larger photo');
		media.onclick = function (event) {
			event.preventDefault();
			event.stopPropagation();
			epcOpenImageLightbox(url, alt);
		};
		media.onkeydown = function (event) {
			if (event.key === 'Enter' || event.key === ' ') {
				event.preventDefault();
				epcOpenImageLightbox(url, alt);
			}
		};
	}
	function brandCardThumbHtml(brand, article, existingUrl) {
		if (existingUrl) {
			return '<span class="epc-fitment-brand-card__thumb epc-fitment-brand-card__thumb--loaded"><img src="' + esc(existingUrl) + '" alt="" loading="lazy"></span>';
		}
		return '<span class="epc-fitment-brand-card__thumb epc-fitment-brand-card__thumb--empty"><i class="fa fa-image"></i></span>';
	}
	function updateBrandCardThumb(key, url, brand, article) {
		if (!brandsBox || !key || !url) { return; }
		var card = brandsBox.querySelector('[data-fitment-key="' + key + '"]');
		if (!card) { return; }
		var b = brand || card.getAttribute('data-fitment-brand') || '';
		var a = article || card.getAttribute('data-fitment-article') || '';
		var thumb = card.querySelector('.epc-fitment-brand-card__thumb');
		if (!thumb) { return; }
		var span = document.createElement('span');
		span.className = 'epc-fitment-brand-card__thumb epc-fitment-brand-card__thumb--loaded';
		span.innerHTML = '<img src="' + esc(url) + '" alt="" loading="lazy">';
		span.style.cursor = 'zoom-in';
		span.onclick = function (e) {
			e.preventDefault();
			e.stopPropagation();
			epcOpenImageLightbox(url, b + ' ' + a);
		};
		if (thumb.parentNode) {
			thumb.parentNode.replaceChild(span, thumb);
		}
	}
	function fetchBrandImage(brand, article) {
		var key = brandImageKey(brand, article);
		if (Object.prototype.hasOwnProperty.call(brandImageCache, key)) {
			var cached = brandImageCache[key];
			return cached && typeof cached.then === 'function' ? cached : Promise.resolve(cached);
		}
		brandImageCache[key] = fetch('/content/shop/catalogue/ajax_epc_sku_media_public.php?action=lookup&brand=' + encodeURIComponent(brand || '') + '&article=' + encodeURIComponent(article || ''), {
			credentials: 'same-origin'
		}).then(function (r) { return r.json(); }).then(function (data) {
			if (data && data.ok && data.url) { return String(data.url); }
			return '';
		}).catch(function () { return ''; }).then(function (cpUrl) {
			if (cpUrl) { return cpUrl; }
			return api('analogs', { article: article, brand: brand, limit: 12, offset: 0 })
				.then(function (data) {
					var rows = rowsFromPayload(data);
					var target = rows.filter(function (row) {
						return compact(row.BRAND || row.SUP_BRAND) === compact(brand) && compact(row.ARTICLE_NR || row.ARTICLE || row.ART_ARTICLE_NR) === compact(article);
					})[0] || rows.filter(function (row) {
						return compact(row.BRAND || row.SUP_BRAND) === compact(brand);
					})[0] || rows[0];
					if (!target || !target.ART_ID) { return ''; }
					return api('article', { id: target.ART_ID }).then(articleImageUrl);
				});
		}).then(function (url) {
			brandImageCache[key] = url || '';
			return url || '';
		}).catch(function () {
			brandImageCache[key] = '';
			return '';
		});
		return brandImageCache[key];
	}
	window.epcFetchBrandPartImage = fetchBrandImage;
	function detailFacts(detail) {
		var facts = [];
		if (detail.PACK_UNIT) { facts.push({ label: 'Pack unit', value: detail.PACK_UNIT }); }
		if (detail.QUANTITY_PER_UNIT) { facts.push({ label: 'Qty / unit', value: detail.QUANTITY_PER_UNIT }); }
		if (detail.MATERIAL_MARK) { facts.push({ label: 'Material', value: detail.MATERIAL_MARK }); }
		if (detail.STATUS_DES) { facts.push({ label: 'Status', value: detail.STATUS_DES }); }
		if (Array.isArray(detail.EAN_CODES) && detail.EAN_CODES.length) {
			facts.push({ label: 'EAN', value: detail.EAN_CODES.map(function (code) {
				return typeof code === 'string' ? code : (code.EAN || code.CODE || '');
			}).filter(Boolean).join(', ') });
		}
		if (Array.isArray(detail.INFO)) {
			detail.INFO.forEach(function (item, index) {
				var text = item.TEXT || item.DES || '';
				if (text) { facts.push({ label: 'Info ' + (index + 1), value: text }); }
			});
		}
		return facts;
	}
	function renderPartDetail(detail, brand, article) {
		if (!partBox) { return; }
		if (!detail) {
			partBox.style.display = 'none';
			partBox.innerHTML = '';
			return;
		}
		var name = detail.COMPLETE_DES || detail.DES || detail.ART_PRODUCT_NAME || 'Part';
		var img = articleImageUrl(detail);
		var weight = criteriaFind(detail, ['weight', 'net weight', 'gross weight', 'weight [']);
		var country = criteriaFind(detail, ['country', 'country of origin', 'origin']) || detail.COUNTRY || detail.COUNTRY_OF_ORIGIN || '';
		var specs = criteriaList(detail).filter(function (item) {
			var label = String(item.CRI_DES || item.CRI_SHORT_DES || '').toLowerCase();
			return label.indexOf('weight') === -1 && label.indexOf('country') === -1 && label.indexOf('origin') === -1;
		});
		var facts = detailFacts(detail);
		var specHtml = specs.slice(0, 8).map(function (item) {
			var label = item.CRI_SHORT_DES || item.CRI_DES || 'Spec';
			var value = item.VALUE || item.DES || '';
			var unit = item.CRI_UNIT_DES || '';
			return '<span class="epc-fitment-spec-chip" title="' + esc(label) + '"><b>' + esc(label) + '</b> ' + esc(value + (unit ? ' ' + unit : '')) + '</span>';
		}).join('');
		var factsHtml = facts.slice(0, 4).map(function (item) {
			return '<span class="epc-fitment-spec-chip epc-fitment-spec-chip--muted"><b>' + esc(item.label) + '</b> ' + esc(item.value) + '</span>';
		}).join('');
		partBox.className = 'epc-fitment-part';
		partBox.style.display = 'block';
		partBox.innerHTML = '<div class="epc-fitment-part-card">'
			+ '<div class="epc-fitment-part-card__media">'
			+ (img ? '<img src="' + esc(img) + '" alt="" loading="lazy" onerror="this.style.display=\'none\';this.nextElementSibling.style.display=\'flex\';">' : '')
			+ '<div class="epc-fitment-part-card__placeholder"' + (img ? ' style="display:none;"' : '') + '><i class="fa fa-image"></i></div>'
			+ '</div><div class="epc-fitment-part-card__main">'
			+ '<p class="epc-fitment-part-card__brand">' + esc(brand) + ' · <span>' + esc(article) + '</span></p>'
			+ '<h4 class="epc-fitment-part-card__name">' + esc(name) + '</h4>'
			+ '<dl class="epc-fitment-part-card__facts"><div><dt>Weight</dt><dd>' + esc(weight || '—') + '</dd></div>'
			+ '<div><dt>Country</dt><dd>' + esc(country || '—') + '</dd></div></dl></div>'
			+ '<div class="epc-fitment-part-card__specs"><div class="epc-fitment-part-card__specs-title">Specifications &amp; details</div>'
			+ '<div class="epc-fitment-part-card__chips">' + (specHtml || '') + (factsHtml || '')
			+ (!specHtml && !factsHtml ? '<span class="epc-fitment-spec-chip epc-fitment-spec-chip--muted">No extra specifications available for this part.</span>' : '')
			+ '</div></div></div>';
		bindPartImageClick(partBox, img, brand + ' ' + article);
		if (img) {
			brandImageCache[brandImageKey(brand, article)] = img;
			updateBrandCardThumb(brandImageKey(brand, article), img, brand, article);
		}
	}
	function sectionRows(fitment, section) {
		if (!fitment) { return []; }
		if (section === 'ALL') {
			return [].concat(fitment.PC || [], fitment.CV || [], fitment.Motorcycle || []);
		}
		return fitment[section] || [];
	}
	function renderFitment(fitment) {
		var widget = document.getElementById('applicability_widget');
		if (!widget) { return; }
		selectedFitment = fitment || {};
		var rows = sectionRows(selectedFitment, selectedSection);
		var total = (selectedFitment.PC || []).length + (selectedFitment.CV || []).length + (selectedFitment.Motorcycle || []).length;
		if (!total) {
			widget.className = 'epc-fitment-message';
			widget.innerHTML = '<div class="epc-fitment-message">No vehicle fitment was found in Epart catalog for this part.</div>';
			return;
		}
		if (!rows.length) {
			widget.className = 'epc-fitment-message';
			widget.innerHTML = '<div class="epc-fitment-message">No rows in this vehicle type. Choose another tab or All vehicles.</div>';
			return;
		}
		var html = '<table class="table table-condensed table-striped epc-umapi-table"><thead><tr><th>Make</th><th>Model</th><th>Modification</th><th>Year</th><th>Power</th><th>Engine / fuel</th></tr></thead><tbody>';
		rows.forEach(function (row) {
			html += '<tr><td>' + esc(row.MANUFACTURER || '') + '</td><td>' + esc(row.MODEL_SERIES || '') + '</td><td>' + esc(fitmentModificationText(row)) + '</td><td>' + esc(yearRange(row)) + '</td><td>' + esc(fitmentPowerText(row)) + '</td><td>' + esc(fitmentEngineText(row)) + '</td></tr>';
		});
		html += '</tbody></table>';
		var sectionLabel = selectedSection === 'ALL' ? 'All vehicles' : selectedSection;
		widget.className = 'epc-fitment-widget-table-host';
		widget.innerHTML = '<div class="epc-fitment-results-toolbar">'
			+ '<span class="epc-fitment-results-toolbar__count"><strong>' + rows.length + '</strong> vehicle' + (rows.length === 1 ? '' : 's') + ' <span class="epc-fitment-results-toolbar__meta">(' + esc(sectionLabel) + ')</span></span>'
			+ '<button type="button" class="btn btn-xs btn-default epc-fitment-download-btn" id="epc-fitment-download-btn" title="Download fitment list for Excel">'
			+ '<i class="fa fa-file-excel-o" aria-hidden="true"></i> Download Excel</button>'
			+ '</div>'
			+ '<div class="epc-fitment-table-scroll">' + html + '</div>';
		var downloadBtn = document.getElementById('epc-fitment-download-btn');
		if (downloadBtn) {
			downloadBtn.onclick = function () { downloadFitmentExcel(rows); };
		}
	}
	function resolveAndLoadFitment(article, brand) {
		var widget = document.getElementById('applicability_widget');
		if (!widget || !article || !brand) { return; }
		widgetShell.style.display = 'block';
		if (typesBox) { typesBox.style.display = 'flex'; }
		widget.innerHTML = '<div class="epc-fitment-message">Looking up part details in Epart catalog for ' + esc(brand) + ' ' + esc(article) + '...</div>';
		api('analogs', { article: article, brand: brand, limit: 30, offset: 0, source: 'fitment' })
			.then(function (data) {
				var rows = rowsFromPayload(data);
				var target = rows.filter(function (row) {
					return compact(row.BRAND || row.SUP_BRAND) === compact(brand) && compact(row.ARTICLE_NR || row.ARTICLE || row.ART_ARTICLE_NR) === compact(article);
				})[0] || rows.filter(function (row) {
					return compact(row.BRAND || row.SUP_BRAND) === compact(brand);
				})[0] || rows[0];
				if (!target || !target.ART_ID) { throw new Error('Article ID not found'); }
				var artId = target.ART_ID;
				var displayBrand = target.BRAND || brand;
				var displayArticle = target.ARTICLE_NR || target.ART_ARTICLE_NR || article;
				widget.innerHTML = '<div class="epc-fitment-message">Loading vehicle fitment list...</div>';
				if (partBox) {
					partBox.style.display = 'block';
					partBox.innerHTML = '<div class="epc-fitment-message">Loading part photo and specifications...</div>';
				}
				return Promise.all([api('article', { id: artId, source: 'fitment' }), api('article_links', { id: artId, source: 'fitment' })]).then(function (results) {
					renderPartDetail(results[0], displayBrand, displayArticle);
					return results[1];
				});
			})
			.then(renderFitment)
			.catch(function (err) {
				renderPartDetail(null, selectedBrand, selectedArticle);
				loadEpartscrossFitmentFallback(article, widget);
			});
	}
	function renderBrands(rows, preferredBrand) {
		if (!rows.length) {
			setMessage('No matching brand was found in Epart catalog for this part number.');
			return;
		}
		var preferred = preferredBrand || pendingPreferredBrand || '';
		pendingPreferredBrand = '';
		brandsBox.className = 'epc-fitment-brand-grid';
		brandsBox.innerHTML = rows.map(function (row, index) {
			var b = row.BRAND || row.SUP_BRAND || row.MANUFACTURER || 'Brand';
			var number = fitmentDisplay(row);
			var title = row.TITLE || row.DES || 'Click to view fitment';
			var isActive = preferred ? brandsEquivalent(b, preferred) : (index === 0);
			var key = brandImageKey(b, fitmentArticle(row));
			var cachedImg = brandImageCache[key];
			var thumbUrl = typeof cachedImg === 'string' && cachedImg ? cachedImg : '';
			return '<button type="button" class="epc-fitment-brand-card' + (isActive ? ' active' : '') + '" data-fitment-key="' + esc(key) + '" data-fitment-brand="' + esc(b) + '" data-fitment-article="' + esc(fitmentArticle(row)) + '">'
				+ brandCardThumbHtml(b, fitmentArticle(row), thumbUrl)
				+ '<span class="epc-fitment-brand-card__text"><strong>' + esc(b) + '</strong><span>' + esc(number) + '</span><small>' + esc(title) + '</small></span></button>';
		}).join('');
		Array.prototype.forEach.call(brandsBox.querySelectorAll('.epc-fitment-brand-card'), function (card) {
			card.onclick = function () {
				Array.prototype.forEach.call(brandsBox.querySelectorAll('.epc-fitment-brand-card'), function (item) { item.classList.remove('active'); });
				card.classList.add('active');
				selectedArticle = card.getAttribute('data-fitment-article') || selectedArticle;
				selectedBrand = card.getAttribute('data-fitment-brand') || selectedBrand;
				resolveAndLoadFitment(selectedArticle, selectedBrand);
			};
		});
		var target = null;
		if (preferred) {
			Array.prototype.forEach.call(brandsBox.querySelectorAll('.epc-fitment-brand-card'), function (card) {
				if (!target && brandsEquivalent(card.getAttribute('data-fitment-brand') || '', preferred)) { target = card; }
			});
		}
		if (!target) { target = brandsBox.querySelector('.epc-fitment-brand-card'); }
		if (target) {
			Array.prototype.forEach.call(brandsBox.querySelectorAll('.epc-fitment-brand-card'), function (item) { item.classList.remove('active'); });
			target.classList.add('active');
			selectedArticle = target.getAttribute('data-fitment-article') || selectedArticle;
			selectedBrand = target.getAttribute('data-fitment-brand') || selectedBrand;
			resolveAndLoadFitment(selectedArticle, selectedBrand);
		}
	}
	function resetFitmentWidget() {
		selectedFitment = null;
		if (widgetShell) { widgetShell.style.display = 'none'; }
		if (typesBox) { typesBox.style.display = 'none'; }
		var widget = document.getElementById('applicability_widget');
		if (widget) { widget.innerHTML = '<div class="epc-fitment-message">Select a brand/part box to load fitment.</div>'; }
		if (partBox) { partBox.style.display = 'none'; partBox.innerHTML = ''; }
	}
	function loadBrandsForArticle(article, preferredBrand) {
		article = String(article || '').trim();
		if (article === '') { return; }
		if (brandsLoadedArticle !== article) {
			brandsLoaded = false;
			resetFitmentWidget();
		}
		brandsLoaded = true;
		brandsLoadedArticle = article;
		selectedArticle = article;
		pendingPreferredBrand = String(preferredBrand || '').trim();
		if (button) { button.setAttribute('data-article', article); }
		setMessage('Loading matching brand and part number boxes from Epart catalog...');
		widgetShell.style.display = 'none';
		if (typesBox) { typesBox.style.display = 'none'; }
		api('brands', { article: article, source: 'fitment' })
			.then(function (data) {
				renderBrands(rowsFromPayload(data), pendingPreferredBrand);
			})
			.catch(function (err) {
				brandsLoaded = false;
				brandsLoadedArticle = '';
				setMessage(fitmentErrorMessage(err, 'Fitment brand lookup is temporarily unavailable.'));
			});
	}
	function ensureFitmentPanelPortal() {
		if (panel.parentNode !== document.body) { document.body.appendChild(panel); }
	}
	function resetFitmentPanelStyles() {
		panel.classList.remove('epc-fitment-panel--anchored', 'epc-fitment-panel--centered');
		panel.style.top = panel.style.left = panel.style.width = panel.style.height = panel.style.maxHeight = panel.style.transform = '';
	}
	function positionFitmentPanel(anchorEl) {
		resetFitmentPanelStyles();
		if (!anchorEl || typeof anchorEl.getBoundingClientRect !== 'function') {
			panel.classList.add('epc-fitment-panel--centered');
			return;
		}
		panel.classList.add('epc-fitment-panel--anchored');
		var rect = anchorEl.getBoundingClientRect();
		var panelW = Math.min(920, Math.max(320, window.innerWidth - 24));
		var panelH = Math.min(Math.max(360, window.innerHeight * 0.72), window.innerHeight - 24);
		var gap = 10;
		var top = rect.bottom + gap;
		if (top + panelH > window.innerHeight - 12) { top = Math.max(12, rect.top - panelH - gap); }
		if (top < 12) { top = 12; panelH = Math.min(panelH, window.innerHeight - top - 12); }
		var left = Math.max(12, Math.min(rect.left + (rect.width / 2) - (panelW / 2), window.innerWidth - panelW - 12));
		panel.style.width = panelW + 'px';
		panel.style.height = panelH + 'px';
		panel.style.maxHeight = panelH + 'px';
		panel.style.top = top + 'px';
		panel.style.left = left + 'px';
	}
	function openFitmentPanel(article, preferredBrand, anchorEl) {
		ensureFitmentPanelPortal();
		positionFitmentPanel(anchorEl);
		panel.classList.add('active');
		document.body.style.overflow = 'hidden';
		loadBrandsForArticle(article, preferredBrand);
	}
	window.epcOpenFitmentCheck = openFitmentPanel;
	if (typesBox) {
		Array.prototype.forEach.call(typesBox.querySelectorAll('button[data-section]'), function (typeButton) {
			typeButton.onclick = function () {
				Array.prototype.forEach.call(typesBox.querySelectorAll('button[data-section]'), function (item) { item.classList.remove('active'); });
				typeButton.classList.add('active');
				selectedSection = typeButton.getAttribute('data-section') || 'PC';
				renderFitment(selectedFitment);
			};
		});
	}
	if (button) {
		button.onclick = function () {
			if (panel.classList.contains('active')) {
				panel.classList.remove('active');
				document.body.style.overflow = '';
			} else {
				openFitmentPanel(button.getAttribute('data-article') || '', '', null);
			}
		};
	}
	if (close) {
		close.onclick = function () {
			panel.classList.remove('active');
			document.body.style.overflow = '';
			resetFitmentPanelStyles();
		};
	}
	document.addEventListener('keydown', function (event) {
		if (event.key === 'Escape' && panel.classList.contains('active')) {
			panel.classList.remove('active');
			document.body.style.overflow = '';
			resetFitmentPanelStyles();
		}
		if (event.key === 'Escape') {
			epcCloseImageLightbox();
		}
	});
})();
</script>
	<?php
}
