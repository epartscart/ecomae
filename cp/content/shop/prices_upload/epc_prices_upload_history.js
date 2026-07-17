(function () {
	'use strict';

	var cfg = window.EPC_PRICES_UPLOAD_HISTORY || {};
	var ajaxUrl = cfg.ajaxUrl || '';
	var csrfKey = cfg.csrfKey || '';
	var activeXhr = null;
	var lastPriceId = 0;

	function historyBodyEl() {
		return document.getElementById('epc_price_upload_history_modal_body');
	}

	function historyTitleEl() {
		return document.getElementById('epc_price_upload_history_modal_title');
	}

	function historySubEl() {
		return document.getElementById('epc_price_upload_history_modal_sub');
	}

	function historyModalEl() {
		return document.getElementById('epc_price_upload_history_modal');
	}

	function escapeHtml(str) {
		return String(str || '').replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;').replace(/"/g, '&quot;');
	}

	function loadingHtml() {
		return '<div class="epc-hist-loading"><div class="epc-hist-spinner" aria-hidden="true"></div>Loading upload history…</div>';
	}

	function bindHistoryFilters(root) {
		if (!root) {
			return;
		}
		var input = root.querySelector('.epc-hist-search');
		var rows = root.querySelectorAll('.epc-hist-table tbody tr[data-hist-filter]');
		var countEl = root.querySelector('.epc-hist-count');
		if (!input || !rows.length) {
			return;
		}
		var total = rows.length;
		function applyFilter() {
			var q = (input.value || '').toLowerCase().trim();
			var shown = 0;
			for (var i = 0; i < rows.length; i++) {
				var hay = rows[i].getAttribute('data-hist-filter') || '';
				var ok = !q || hay.indexOf(q) !== -1;
				rows[i].style.display = ok ? '' : 'none';
				if (ok) {
					shown++;
				}
			}
			if (countEl) {
				countEl.textContent = q ? (shown + ' / ' + total + ' shown') : (total + ' record' + (total === 1 ? '' : 's'));
			}
		}
		input.addEventListener('input', applyFilter);
		applyFilter();
	}

	function openPypricesHistory(priceId) {
		var fn = window['show_update_history_' + priceId];
		if (typeof fn === 'function') {
			var modal = historyModalEl();
			if (modal && window.jQuery) {
				jQuery(modal).modal('hide');
			}
			fn();
			return;
		}
		alert('Task update history is available from the Actions column (clock / history icon) for this price list.');
	}

	window.epcOpenPypricesUpdateHistory = openPypricesHistory;

	function epcLoadPriceUploadHistory(priceId, targetEl) {
		var body = targetEl || historyBodyEl();
		if (!body) {
			return;
		}
		if (!ajaxUrl) {
			body.innerHTML = '<div class="epc-hist-empty"><div class="epc-hist-empty__icon"><i class="fas fa-exclamation-triangle"></i></div><h5>History unavailable</h5><p>Upload history script did not load. Refresh the page and try again.</p></div>';
			return;
		}
		if (activeXhr && typeof activeXhr.abort === 'function') {
			try {
				activeXhr.abort();
			} catch (e) { /* ignore */ }
		}
		lastPriceId = priceId || 0;
		body.innerHTML = loadingHtml();
		activeXhr = jQuery.ajax({
			type: 'POST',
			url: ajaxUrl,
			dataType: 'json',
			timeout: 20000,
			data: {
				action: 'list',
				price_id: priceId || 0,
				limit: 80,
				csrf_guard_key: csrfKey
			},
			success: function (answer) {
				if (answer && answer.status && answer.html) {
					body.innerHTML = answer.html;
					bindHistoryFilters(body);
					var openBtn = body.querySelector('[data-epc-open-pyprices-history]');
					if (openBtn) {
						openBtn.addEventListener('click', function (ev) {
							ev.preventDefault();
							openPypricesHistory(lastPriceId);
						});
					}
				} else {
					var msg = (answer && (answer.message || answer.error)) ? (answer.message || answer.error) : 'Could not load history.';
					body.innerHTML = '<div class="epc-hist-empty"><div class="epc-hist-empty__icon"><i class="fas fa-exclamation-circle"></i></div><h5>Could not load history</h5><p class="text-danger">' + escapeHtml(msg) + '</p></div>';
				}
			},
			error: function (xhr, textStatus) {
				if (textStatus === 'abort') {
					return;
				}
				var detail = '';
				if (xhr && xhr.responseText) {
					try {
						var parsed = JSON.parse(xhr.responseText);
						detail = parsed.message || parsed.error || '';
					} catch (e) {
						detail = xhr.status ? ('HTTP ' + xhr.status) : '';
					}
				}
				if (textStatus === 'timeout') {
					detail = 'Request timed out';
				}
				body.innerHTML = '<div class="epc-hist-empty"><div class="epc-hist-empty__icon"><i class="fas fa-unlink"></i></div><h5>History request failed</h5><p>' + escapeHtml(detail || 'Please try again.') + '</p></div>';
			},
			complete: function () {
				activeXhr = null;
			}
		});
	}

	window.epcShowAllPriceUploadHistory = function () {
		var title = historyTitleEl();
		var sub = historySubEl();
		var modal = historyModalEl();
		if (!title || !modal) {
			return;
		}
		title.textContent = 'Upload history — all price lists';
		if (sub) {
			sub.textContent = 'Recent archived uploads across every list. Use search to filter quickly.';
		}
		jQuery(modal).modal('show');
		epcLoadPriceUploadHistory(0);
	};

	window.epcShowPriceUploadHistory = function (priceId, priceName) {
		var title = historyTitleEl();
		var sub = historySubEl();
		var modal = historyModalEl();
		if (!title || !modal) {
			return;
		}
		title.textContent = 'Upload history — ' + priceName + ' (#' + priceId + ')';
		if (sub) {
			sub.textContent = 'Archived source files, import stats, and quick downloads.';
		}
		jQuery(modal).modal('show');
		epcLoadPriceUploadHistory(priceId);
	};

	window.epcPriceEditLoadUploadHistory = function (priceId) {
		var panel = document.getElementById('epc_price_edit_upload_history_body');
		if (!panel) {
			return;
		}
		if (!priceId) {
			priceId = parseInt(panel.getAttribute('data-price-id') || '0', 10);
		}
		epcLoadPriceUploadHistory(priceId, panel);
	};

	jQuery(function () {
		var panel = document.getElementById('epc_price_edit_upload_history_body');
		if (panel && panel.getAttribute('data-price-id')) {
			window.epcPriceEditLoadUploadHistory();
		}
	});
})();
