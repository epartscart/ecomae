/**
 * Quote request: amend / alternative offer modal (window 2).
 * Dropdowns from cross / article / OEM + supplier warehouse for order process.
 * Loaded in CP footer AFTER jquery is reloaded by desktop.php.
 */
(function () {
	'use strict';

	var optionsCache = {};
	var currentOptions = null;

	function jq() {
		return window.jQuery || window.$;
	}

	function cfg() {
		return window.epcQuoteAltConfig || {};
	}

	function rowByLineId(id) {
		return document.querySelector('#epc-quote-lines-table tr[data-line-id="' + String(id) + '"]');
	}

	function qs(row, sel) {
		return row ? row.querySelector(sel) : null;
	}

	function setVisible(el, on) {
		if (!el) return;
		el.style.display = on ? '' : 'none';
	}

	function money(n) {
		var v = parseFloat(n);
		if (isNaN(v)) return '';
		return v.toFixed(2);
	}

	function pairKey(brand, article) {
		var b = String(brand || '').toUpperCase().replace(/[#`"'\\\r\n\t]/g, '').trim();
		var a = String(article || '').toUpperCase().replace(/[\s\-_/`'"\\.,#\r\n\t]/g, '');
		if (!a) return '';
		return b + '|' + a;
	}

	function refreshSummary(row) {
		if (!row) return;
		var flag = qs(row, '.epc-alt-flag');
		var on = flag && String(flag.value) === '1';
		var mfr = (qs(row, '.epc-alt-mfr') || {}).value || '';
		var art = (qs(row, '.epc-alt-art') || {}).value || '';
		var qty = (qs(row, '.epc-alt-qty') || {}).value || '1';
		var price = (qs(row, '.epc-alt-price') || {}).value || '';
		var wh = (qs(row, '.epc-alt-storage-label') || {}).value || '';
		var text = [mfr, art, '× ' + qty, '@ ' + price, wh ? '· ' + wh : ''].join(' ').replace(/\s+/g, ' ').trim();
		var summaryText = qs(row, '.epc-alt-summary-text');
		if (summaryText) summaryText.textContent = text;
		setVisible(qs(row, '.epc-alt-summary'), on);
		setVisible(qs(row, '.epc-clear-alt'), on);
		var openBtn = qs(row, '.epc-open-alt-modal');
		if (openBtn) openBtn.textContent = on ? 'Edit alternative' : 'Amend / alternative';
		var mainPrice = qs(row, '.epc-quote-main-price');
		if (mainPrice) {
			mainPrice.readOnly = !!on;
			if (on) mainPrice.value = price;
		}
	}

	function showModal(show) {
		var $ = jq();
		if ($ && $('#epcAltOfferModal').modal) {
			$('#epcAltOfferModal').modal(show ? 'show' : 'hide');
			return;
		}
		var modal = document.getElementById('epcAltOfferModal');
		if (!modal) return;
		if (show) {
			modal.classList.add('in');
			modal.style.display = 'block';
			modal.setAttribute('aria-hidden', 'false');
		} else {
			modal.classList.remove('in');
			modal.style.display = 'none';
			modal.setAttribute('aria-hidden', 'true');
		}
	}

	function setStatus(msg, isError) {
		var el = document.getElementById('epcAltOptionsStatus');
		if (!el) return;
		if (!msg) {
			el.style.display = 'none';
			el.textContent = '';
			el.className = 'text-muted';
			return;
		}
		el.style.display = '';
		el.textContent = msg;
		el.className = isError ? 'text-danger' : 'text-muted';
	}

	function setManualMode(on) {
		setVisible(document.getElementById('epcAltManualFields'), !!on);
		var brand = document.getElementById('epcAltBrand');
		var article = document.getElementById('epcAltArticle');
		if (!on) {
			return;
		}
		if (brand) brand.focus();
	}

	function findAltByKey(key) {
		if (!currentOptions || !currentOptions.alternatives) return null;
		for (var i = 0; i < currentOptions.alternatives.length; i++) {
			if (currentOptions.alternatives[i].key === key) return currentOptions.alternatives[i];
		}
		return null;
	}

	function fillPartSelect(selectedKey) {
		var sel = document.getElementById('epcAltPartSelect');
		if (!sel) return;
		sel.innerHTML = '';
		var opt0 = document.createElement('option');
		opt0.value = '';
		opt0.textContent = '— Choose alternative —';
		sel.appendChild(opt0);

		var list = (currentOptions && currentOptions.alternatives) || [];
		for (var i = 0; i < list.length; i++) {
			var a = list[i];
			var o = document.createElement('option');
			o.value = a.key;
			var bits = [a.brand, a.article_show || a.article];
			if (a.name) bits.push('— ' + a.name);
			var tag = [];
			if (a.in_stock) tag.push('in stock');
			if (a.source) {
				var src = String(a.source);
				if (/oem/i.test(src)) tag.push('OEM');
				else if (/cross/i.test(src)) tag.push('cross');
				else if (/requested/i.test(src)) tag.push('requested');
			}
			if (tag.length) bits.push('(' + tag.join(', ') + ')');
			o.textContent = bits.join(' ');
			sel.appendChild(o);
		}

		var manual = document.createElement('option');
		manual.value = '__manual__';
		manual.textContent = '— Enter manually —';
		sel.appendChild(manual);

		if (selectedKey && selectedKey !== '__manual__') {
			sel.value = selectedKey;
			if (sel.value !== selectedKey) {
				// Saved alt not in catalog list — switch to manual with values already filled.
				sel.value = '__manual__';
			}
		}
	}

	function warehouseOptionLabel(wh, preferStock) {
		var label = wh.label || wh.warehouse || ('Warehouse #' + wh.storage_id);
		var bits = [label];
		if (preferStock && wh.price != null && wh.price !== '' && !isNaN(parseFloat(wh.price)) && parseFloat(wh.price) > 0) {
			bits.push('@ ' + money(wh.price));
		}
		if (preferStock && wh.qty != null && wh.qty !== '') {
			bits.push('qty ' + wh.qty);
		}
		if (preferStock && wh.delivery != null && wh.delivery !== '') {
			bits.push(wh.delivery + 'd');
		}
		return bits.join(' · ');
	}

	function fillWarehouseSelect(alt, selectedStorageId) {
		var sel = document.getElementById('epcAltWarehouseSelect');
		if (!sel) return;
		sel.innerHTML = '';
		var opt0 = document.createElement('option');
		opt0.value = '';
		opt0.textContent = '— Choose supplier warehouse —';
		sel.appendChild(opt0);

		var seen = {};
		var stockWh = (alt && alt.warehouses) || [];
		if (stockWh.length) {
			var grp = document.createElement('optgroup');
			grp.label = 'With price / stock for this part';
			for (var i = 0; i < stockWh.length; i++) {
				var wh = stockWh[i];
				var sid = String(wh.storage_id || '');
				if (!sid || seen[sid]) continue;
				seen[sid] = true;
				var o = document.createElement('option');
				o.value = sid;
				o.textContent = warehouseOptionLabel(wh, true);
				o.setAttribute('data-price', wh.price != null ? String(wh.price) : '');
				o.setAttribute('data-delivery', wh.delivery != null ? String(wh.delivery) : '');
				o.setAttribute('data-label', wh.warehouse || wh.label || '');
				grp.appendChild(o);
			}
			if (grp.children.length) sel.appendChild(grp);
		}

		var all = (currentOptions && currentOptions.warehouses_all) || [];
		if (all.length) {
			var grp2 = document.createElement('optgroup');
			grp2.label = 'All warehouses';
			for (var j = 0; j < all.length; j++) {
				var w = all[j];
				var id = String(w.storage_id || '');
				if (!id || seen[id]) continue;
				seen[id] = true;
				var o2 = document.createElement('option');
				o2.value = id;
				o2.textContent = warehouseOptionLabel(w, false);
				o2.setAttribute('data-price', '');
				o2.setAttribute('data-delivery', '');
				o2.setAttribute('data-label', w.warehouse || w.label || '');
				grp2.appendChild(o2);
			}
			if (grp2.children.length) sel.appendChild(grp2);
		}

		if (selectedStorageId) {
			sel.value = String(selectedStorageId);
		} else if (stockWh.length === 1) {
			sel.value = String(stockWh[0].storage_id);
			onWarehouseChange();
		}
	}

	function onPartChange() {
		var sel = document.getElementById('epcAltPartSelect');
		var key = sel ? sel.value : '';
		if (key === '__manual__') {
			setManualMode(true);
			fillWarehouseSelect(null, (document.getElementById('epcAltWarehouseSelect') || {}).value || '');
			return;
		}
		setManualMode(false);
		var alt = findAltByKey(key);
		if (!alt) {
			fillWarehouseSelect(null, '');
			return;
		}
		document.getElementById('epcAltBrand').value = alt.brand || '';
		document.getElementById('epcAltArticle').value = alt.article_show || alt.article || '';
		if (alt.name) {
			document.getElementById('epcAltName').value = alt.name;
		}
		var prevWh = (document.getElementById('epcAltWarehouseSelect') || {}).value || '';
		fillWarehouseSelect(alt, prevWh);
		if (!prevWh && alt.warehouses && alt.warehouses.length === 1) {
			// already handled in fillWarehouseSelect
		} else if (prevWh) {
			onWarehouseChange();
		} else if (alt.warehouses && alt.warehouses.length) {
			// Prefer first in-stock warehouse with price
			for (var i = 0; i < alt.warehouses.length; i++) {
				if (alt.warehouses[i].price > 0) {
					document.getElementById('epcAltWarehouseSelect').value = String(alt.warehouses[i].storage_id);
					onWarehouseChange();
					break;
				}
			}
		}
	}

	function onWarehouseChange() {
		var sel = document.getElementById('epcAltWarehouseSelect');
		if (!sel || !sel.value) return;
		var opt = sel.options[sel.selectedIndex];
		if (!opt) return;
		var price = opt.getAttribute('data-price') || '';
		var delivery = opt.getAttribute('data-delivery') || '';
		var priceEl = document.getElementById('epcAltPrice');
		if (priceEl && price && (!priceEl.value || parseFloat(priceEl.value) <= 0)) {
			priceEl.value = money(price);
		} else if (priceEl && price) {
			// Always sync when choosing a stocked warehouse option with price
			if (opt.parentNode && opt.parentNode.label && /stock|price/i.test(opt.parentNode.label)) {
				priceEl.value = money(price);
			}
		}
		if (delivery !== '' && !isNaN(parseInt(delivery, 10))) {
			var lineId = document.getElementById('epcAltLineId').value;
			var row = rowByLineId(lineId);
			var lead = qs(row, '.epc-quote-lead');
			if (lead && String(lead.value || '').trim() === '') {
				lead.value = String(parseInt(delivery, 10));
			}
		}
	}

	function loadOptions(quoteId, lineId, done) {
		var c = cfg();
		var cacheKey = String(quoteId) + ':' + String(lineId);
		if (optionsCache[cacheKey]) {
			currentOptions = optionsCache[cacheKey];
			done(null, currentOptions);
			return;
		}
		setStatus('Loading cross / OEM alternatives…');
		var url = c.ajaxUrl || '';
		if (!url) {
			done('Options URL not configured');
			return;
		}
		var body =
			'quote_id=' + encodeURIComponent(quoteId) +
			'&line_id=' + encodeURIComponent(lineId) +
			'&csrf_guard_key=' + encodeURIComponent(c.csrf || '');

		var $ = jq();
		if ($ && $.ajax) {
			$.ajax({
				url: url,
				type: 'POST',
				dataType: 'json',
				data: body,
				timeout: 120000
			}).done(function (data) {
				if (!data || !data.status) {
					done((data && data.message) || 'Could not load alternatives');
					return;
				}
				optionsCache[cacheKey] = data;
				currentOptions = data;
				done(null, data);
			}).fail(function () {
				done('Could not load alternatives (network)');
			});
			return;
		}

		fetch(url, {
			method: 'POST',
			headers: { 'Content-Type': 'application/x-www-form-urlencoded; charset=UTF-8' },
			body: body,
			credentials: 'same-origin'
		}).then(function (r) { return r.json(); }).then(function (data) {
			if (!data || !data.status) {
				done((data && data.message) || 'Could not load alternatives');
				return;
			}
			optionsCache[cacheKey] = data;
			currentOptions = data;
			done(null, data);
		}).catch(function () {
			done('Could not load alternatives (network)');
		});
	}

	function openFromButton(btn) {
		var id = btn.getAttribute('data-line-id');
		var quoteId = btn.getAttribute('data-quote-id') || (cfg().quoteId || '');
		var row = rowByLineId(id);
		document.getElementById('epcAltLineId').value = id || '';
		document.getElementById('epcAltQuoteId').value = quoteId || '';
		document.getElementById('epcAltReqLabel').textContent = btn.getAttribute('data-req-label') || '';
		document.getElementById('epcAltReqQty').textContent = btn.getAttribute('data-req-qty') || '';
		document.getElementById('epcAltBrand').value = (qs(row, '.epc-alt-mfr') || {}).value || '';
		document.getElementById('epcAltArticle').value = (qs(row, '.epc-alt-art') || {}).value || '';
		document.getElementById('epcAltName').value = (qs(row, '.epc-alt-name') || {}).value || '';
		document.getElementById('epcAltQty').value = (qs(row, '.epc-alt-qty') || {}).value || btn.getAttribute('data-req-qty') || '1';
		document.getElementById('epcAltPrice').value = (qs(row, '.epc-alt-price') || {}).value || (qs(row, '.epc-quote-main-price') || {}).value || '';

		var savedBrand = (qs(row, '.epc-alt-mfr') || {}).value || '';
		var savedArt = (qs(row, '.epc-alt-art') || {}).value || '';
		var savedStorage = (qs(row, '.epc-alt-storage-id') || {}).value || '';
		var preferKey = pairKey(savedBrand, savedArt);

		var partSel = document.getElementById('epcAltPartSelect');
		if (partSel) {
			partSel.innerHTML = '<option value="">Loading…</option>';
		}
		fillWarehouseSelect(null, savedStorage);
		setManualMode(false);
		showModal(true);

		loadOptions(quoteId, id, function (err, data) {
			if (err) {
				setStatus(err + ' — you can enter brand/article manually.', true);
				fillPartSelect('__manual__');
				setManualMode(true);
				fillWarehouseSelect(null, savedStorage);
				return;
			}
			var n = (data.alternatives || []).length;
			var stockN = 0;
			for (var i = 0; i < (data.alternatives || []).length; i++) {
				if (data.alternatives[i].in_stock) stockN++;
			}
			setStatus(n + ' alternative(s) from cross/OEM' + (stockN ? ' · ' + stockN + ' with warehouse stock' : '') + '.');
			fillPartSelect(preferKey || '');
			if (preferKey && findAltByKey(preferKey)) {
				onPartChange();
				if (savedStorage) {
					document.getElementById('epcAltWarehouseSelect').value = String(savedStorage);
				}
			} else if (preferKey) {
				document.getElementById('epcAltPartSelect').value = '__manual__';
				setManualMode(true);
				fillWarehouseSelect(null, savedStorage);
			} else {
				// Default: first in-stock alternative, else leave blank for staff choice
				var firstStock = null;
				for (var j = 0; j < (data.alternatives || []).length; j++) {
					if (data.alternatives[j].in_stock) {
						firstStock = data.alternatives[j];
						break;
					}
				}
				if (firstStock) {
					document.getElementById('epcAltPartSelect').value = firstStock.key;
					onPartChange();
				}
			}
		});
	}

	function applyAlternative() {
		var id = document.getElementById('epcAltLineId').value;
		var partSel = document.getElementById('epcAltPartSelect');
		var partKey = partSel ? partSel.value : '';
		if (!partKey) {
			alert('Choose an alternative part from the list (or Enter manually).');
			return;
		}
		if (partKey !== '__manual__') {
			var alt = findAltByKey(partKey);
			if (alt) {
				document.getElementById('epcAltBrand').value = alt.brand || '';
				document.getElementById('epcAltArticle').value = alt.article_show || alt.article || '';
				if (alt.name && !String(document.getElementById('epcAltName').value || '').trim()) {
					document.getElementById('epcAltName').value = alt.name;
				}
			}
		}
		var brand = String(document.getElementById('epcAltBrand').value || '').trim();
		var article = String(document.getElementById('epcAltArticle').value || '').trim();
		var name = String(document.getElementById('epcAltName').value || '').trim();
		var qty = parseInt(document.getElementById('epcAltQty').value, 10) || 0;
		var price = String(document.getElementById('epcAltPrice').value || '').trim().replace(',', '.');
		var whSel = document.getElementById('epcAltWarehouseSelect');
		var storageId = whSel ? String(whSel.value || '').trim() : '';
		var whLabel = '';
		if (whSel && whSel.selectedIndex >= 0) {
			var opt = whSel.options[whSel.selectedIndex];
			whLabel = (opt && opt.getAttribute('data-label')) || (opt ? opt.textContent : '') || '';
			// Strip price/qty suffix from optgroup label text for summary
			if (whLabel.indexOf(' · ') !== -1 && opt && opt.getAttribute('data-label')) {
				whLabel = opt.getAttribute('data-label');
			}
		}
		if (!brand || !article) {
			alert('Brand and article are required for an alternative.');
			return;
		}
		if (!storageId || parseInt(storageId, 10) <= 0) {
			alert('Choose the supplier warehouse for the order process.');
			return;
		}
		if (qty < 1) {
			alert('Qty must be at least 1.');
			return;
		}
		if (!price || isNaN(parseFloat(price)) || parseFloat(price) <= 0) {
			alert('Enter a positive alternative price.');
			return;
		}
		var row = rowByLineId(id);
		if (!row) {
			alert('Could not find quote line #' + id);
			return;
		}
		qs(row, '.epc-alt-flag').value = '1';
		qs(row, '.epc-alt-mfr').value = brand;
		qs(row, '.epc-alt-art').value = article;
		qs(row, '.epc-alt-name').value = name;
		qs(row, '.epc-alt-qty').value = String(qty);
		qs(row, '.epc-alt-price').value = price;
		var sidEl = qs(row, '.epc-alt-storage-id');
		if (sidEl) sidEl.value = String(parseInt(storageId, 10));
		var labEl = qs(row, '.epc-alt-storage-label');
		if (labEl) labEl.value = whLabel;
		onWarehouseChange();
		refreshSummary(row);
		showModal(false);
	}

	function clearAlternative(row) {
		if (!row) return;
		qs(row, '.epc-alt-flag').value = '0';
		['.epc-alt-mfr', '.epc-alt-art', '.epc-alt-name', '.epc-alt-qty', '.epc-alt-price', '.epc-alt-storage-id', '.epc-alt-storage-label'].forEach(function (sel) {
			var el = qs(row, sel);
			if (el) el.value = '';
		});
		refreshSummary(row);
	}

	document.addEventListener('click', function (ev) {
		var t = ev.target;
		if (!t || !t.closest) return;
		var openBtn = t.closest('.epc-open-alt-modal');
		if (openBtn) {
			ev.preventDefault();
			openFromButton(openBtn);
			return;
		}
		var clearBtn = t.closest('.epc-clear-alt');
		if (clearBtn) {
			ev.preventDefault();
			clearAlternative(clearBtn.closest('tr'));
			return;
		}
		if (t.id === 'epcAltApplyBtn' || (t.closest && t.closest('#epcAltApplyBtn'))) {
			ev.preventDefault();
			applyAlternative();
		}
	});

	document.addEventListener('change', function (ev) {
		var t = ev.target;
		if (!t) return;
		if (t.id === 'epcAltPartSelect') {
			onPartChange();
		} else if (t.id === 'epcAltWarehouseSelect') {
			onWarehouseChange();
		}
	});

	// Initial paint for any pre-saved alternatives
	document.querySelectorAll('#epc-quote-lines-table tr[data-line-id]').forEach(refreshSummary);
})();
