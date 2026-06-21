(function () {
	'use strict';

	var cfg = window.EPC_PRICES_UPLOAD_HISTORY || {};
	var ajaxUrl = cfg.ajaxUrl || '';
	var csrfKey = cfg.csrfKey || '';

	function historyBodyEl() {
		return document.getElementById('epc_price_upload_history_modal_body');
	}

	function historyTitleEl() {
		return document.getElementById('epc_price_upload_history_modal_title');
	}

	function historyModalEl() {
		return document.getElementById('epc_price_upload_history_modal');
	}

	function epcLoadPriceUploadHistory(priceId, targetEl) {
		var body = targetEl || historyBodyEl();
		if (!body || !ajaxUrl) {
			return;
		}
		body.innerHTML = '<div class="text-center"><i class="fas fa-spinner fa-pulse"></i></div>';
		jQuery.ajax({
			type: 'POST',
			url: ajaxUrl,
			dataType: 'json',
			data: {
				action: 'list',
				price_id: priceId || 0,
				limit: 200,
				csrf_guard_key: csrfKey
			},
			success: function (answer) {
				if (answer && answer.status && answer.html) {
					body.innerHTML = answer.html;
				} else {
					body.innerHTML = '<p class="text-danger">Could not load history.</p>';
				}
			},
			error: function () {
				body.innerHTML = '<p class="text-danger">Request failed.</p>';
			}
		});
	}

	window.epcShowAllPriceUploadHistory = function () {
		var title = historyTitleEl();
		var modal = historyModalEl();
		if (!title || !modal) {
			return;
		}
		title.innerHTML = 'Update file history — all lists';
		jQuery(modal).modal('show');
		epcLoadPriceUploadHistory(0);
	};

	window.epcShowPriceUploadHistory = function (priceId, priceName) {
		var title = historyTitleEl();
		var modal = historyModalEl();
		if (!title || !modal) {
			return;
		}
		title.innerHTML = 'Upload history — ' + priceName + ' (#' + priceId + ')';
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
