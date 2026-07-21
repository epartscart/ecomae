/**
 * Quote request: amend / alternative offer modal (window 2).
 * Loaded in CP footer AFTER jquery is reloaded by desktop.php.
 */
(function () {
	'use strict';

	function jq() {
		return window.jQuery || window.$;
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

	function refreshSummary(row) {
		if (!row) return;
		var flag = qs(row, '.epc-alt-flag');
		var on = flag && String(flag.value) === '1';
		var mfr = (qs(row, '.epc-alt-mfr') || {}).value || '';
		var art = (qs(row, '.epc-alt-art') || {}).value || '';
		var qty = (qs(row, '.epc-alt-qty') || {}).value || '1';
		var price = (qs(row, '.epc-alt-price') || {}).value || '';
		var text = [mfr, art, '× ' + qty, '@ ' + price].join(' ').replace(/\s+/g, ' ').trim();
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

	function openFromButton(btn) {
		var id = btn.getAttribute('data-line-id');
		var row = rowByLineId(id);
		document.getElementById('epcAltLineId').value = id || '';
		document.getElementById('epcAltReqLabel').textContent = btn.getAttribute('data-req-label') || '';
		document.getElementById('epcAltReqQty').textContent = btn.getAttribute('data-req-qty') || '';
		document.getElementById('epcAltBrand').value = (qs(row, '.epc-alt-mfr') || {}).value || '';
		document.getElementById('epcAltArticle').value = (qs(row, '.epc-alt-art') || {}).value || '';
		document.getElementById('epcAltName').value = (qs(row, '.epc-alt-name') || {}).value || '';
		document.getElementById('epcAltQty').value = (qs(row, '.epc-alt-qty') || {}).value || btn.getAttribute('data-req-qty') || '1';
		document.getElementById('epcAltPrice').value = (qs(row, '.epc-alt-price') || {}).value || (qs(row, '.epc-quote-main-price') || {}).value || '';
		showModal(true);
	}

	function applyAlternative() {
		var id = document.getElementById('epcAltLineId').value;
		var brand = String(document.getElementById('epcAltBrand').value || '').trim();
		var article = String(document.getElementById('epcAltArticle').value || '').trim();
		var name = String(document.getElementById('epcAltName').value || '').trim();
		var qty = parseInt(document.getElementById('epcAltQty').value, 10) || 0;
		var price = String(document.getElementById('epcAltPrice').value || '').trim().replace(',', '.');
		if (!brand || !article) {
			alert('Brand and article are required for an alternative.');
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
		refreshSummary(row);
		showModal(false);
	}

	function clearAlternative(row) {
		if (!row) return;
		qs(row, '.epc-alt-flag').value = '0';
		['.epc-alt-mfr', '.epc-alt-art', '.epc-alt-name', '.epc-alt-qty', '.epc-alt-price'].forEach(function (sel) {
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

	// Initial paint for any pre-saved alternatives
	document.querySelectorAll('#epc-quote-lines-table tr[data-line-id]').forEach(refreshSummary);
})();
