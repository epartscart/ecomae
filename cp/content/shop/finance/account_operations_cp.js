/**
 * CP Account operations — datepickers, customer autocomplete, filter cookies.
 * Loaded after jquery.datetimepicker.js via epc_cp_page_assets.
 * Config is read from #epc-ao-config (DOM), because relocated inline scripts
 * are injected after page footer assets.
 */
(function (window, document, $) {
	'use strict';

	function readConfig() {
		if (window.EPC_ACCOUNT_OPS && typeof window.EPC_ACCOUNT_OPS === 'object') {
			return window.EPC_ACCOUNT_OPS;
		}
		var el = document.getElementById('epc-ao-config');
		if (!el) {
			return null;
		}
		try {
			var raw = (el.textContent || el.value || '').trim();
			if (!raw) {
				return null;
			}
			return JSON.parse(raw);
		} catch (e) {
			return null;
		}
	}

	function pad(n) {
		return (n < 10 ? '0' : '') + n;
	}

	function formatShow(dateObj) {
		if (!(dateObj instanceof Date) || isNaN(dateObj.getTime())) {
			return '';
		}
		return pad(dateObj.getDate()) + '.' + pad(dateObj.getMonth() + 1) + '.' + dateObj.getFullYear()
			+ ' ' + pad(dateObj.getHours()) + ':' + pad(dateObj.getMinutes());
	}

	function markActive(fieldEl, on) {
		if (!fieldEl) {
			return;
		}
		if (on) {
			fieldEl.classList.add('is-active');
		} else {
			fieldEl.classList.remove('is-active');
		}
	}

	function getCookie(name) {
		var matches = document.cookie.match(new RegExp(
			'(?:^|; )' + name.replace(/([.$?*|{}()[\]\\/+^])/g, '\\$1') + '=([^;]*)'
		));
		return matches ? decodeURIComponent(matches[1]) : undefined;
	}

	function setCookie(name, value, expireMs) {
		var date = new Date(new Date().getTime() + expireMs);
		document.cookie = name + '=' + value + '; path=/; expires=' + date.toUTCString();
	}

	function boot() {
		if (!$ || !$.fn) {
			return;
		}
		var cfg = readConfig();
		if (!cfg) {
			return;
		}
		window.EPC_ACCOUNT_OPS = cfg;

		function initDateField(showId, hiddenId, defaultTime) {
			var showEl = document.getElementById(showId);
			var hidEl = document.getElementById(hiddenId);
			if (!showEl || !hidEl || !$.fn.datetimepicker) {
				return;
			}

			var sync = function (currentTime) {
				var field = showEl.closest('.epc-ao-field');
				if (!(currentTime instanceof Date) || isNaN(currentTime.getTime())) {
					showEl.value = '';
					hidEl.value = '';
					markActive(field, false);
					return;
				}
				showEl.value = formatShow(currentTime);
				hidEl.value = String(Math.floor(currentTime.getTime() / 1000));
				markActive(field, true);
			};

			$(showEl).datetimepicker({
				lang: cfg.lang || 'en',
				closeOnDateSelect: false,
				closeOnTimeSelect: true,
				dayOfWeekStart: 1,
				format: 'd.m.Y H:i',
				defaultTime: defaultTime || '00:00',
				onChangeDateTime: sync,
				onClose: sync
			});

			if (hidEl.value && /^\d+$/.test(hidEl.value)) {
				var existing = new Date(parseInt(hidEl.value, 10) * 1000);
				showEl.value = formatShow(existing);
				markActive(showEl.closest('.epc-ao-field'), true);
				try {
					$(showEl).datetimepicker({ value: existing });
				} catch (e) {
					/* ignore */
				}
			}

			showEl.addEventListener('change', function () {
				var v = String(showEl.value || '').trim();
				var field = showEl.closest('.epc-ao-field');
				if (v === '') {
					hidEl.value = '';
					markActive(field, false);
					return;
				}
				var m = v.match(/^(\d{1,2})\.(\d{1,2})\.(\d{4})\s+(\d{1,2}):(\d{1,2})$/);
				if (!m) {
					return;
				}
				var d = new Date(
					parseInt(m[3], 10),
					parseInt(m[2], 10) - 1,
					parseInt(m[1], 10),
					parseInt(m[4], 10),
					parseInt(m[5], 10),
					0
				);
				if (!isNaN(d.getTime())) {
					hidEl.value = String(Math.floor(d.getTime() / 1000));
					markActive(field, true);
				}
			});
		}

		function setCustomer(userId, userInfo) {
			var search = document.getElementById('user_id_search');
			var hidden = document.getElementById('user_id');
			var chip = document.getElementById('user_id_show');
			var chipText = document.getElementById('user_id_show_text');
			var field = search ? search.closest('.epc-ao-field') : null;
			if (!hidden || !chip || !chipText || !search) {
				return;
			}
			userId = userId == null ? '' : String(userId);
			userInfo = userInfo == null ? '' : String(userInfo);
			hidden.value = userId;
			if (userId === '') {
				search.value = '';
				search.style.display = '';
				chip.classList.remove('is-visible');
				chipText.textContent = '';
				markActive(field, false);
				return;
			}
			search.value = '';
			search.style.display = 'none';
			chipText.textContent = (cfg.labels && cfg.labels.selected ? cfg.labels.selected + ': ' : '') + userInfo;
			chip.classList.add('is-visible');
			markActive(field, true);
		}

		function initCustomerAutocomplete() {
			var search = $('#user_id_search');
			if (!search.length || !$.fn.autocomplete) {
				return;
			}
			search.autocomplete({
				minLength: 1,
				source: function (request, response) {
					$.ajax({
						type: 'POST',
						url: cfg.autocompleteUrl,
						dataType: 'text',
						data: {
							input_str: request.term,
							csrf_guard_key: cfg.csrf || ''
						},
						success: function (answer) {
							var answerOb;
							try {
								answerOb = JSON.parse(answer);
							} catch (e) {
								return;
							}
							if (!answerOb || answerOb.status !== true || !answerOb.vars || !answerOb.vars.length) {
								response([]);
								return;
							}
							response($.map(answerOb.vars, function (item) {
								return {
									label: item.user_info,
									value: item.user_info,
									object: item
								};
							}));
						}
					});
				},
				select: function (event, ui) {
					if (ui.item && ui.item.object) {
						setCustomer(String(ui.item.object.user_id), ui.item.object.user_info);
					}
					return false;
				}
			});

			var clearBtn = document.getElementById('user_id_clear');
			if (clearBtn) {
				clearBtn.addEventListener('click', function () {
					setCustomer('', '');
				});
			}

			if (cfg.userId) {
				setCustomer(cfg.userId, cfg.userLabel || ('ID ' + cfg.userId));
			}
		}

		function readFilters() {
			var out = {
				time_from: (document.getElementById('time_from') || {}).value || '',
				time_to: (document.getElementById('time_to') || {}).value || '',
				income: (document.getElementById('income') || {}).value || '-1',
				operation_code: (document.getElementById('operation_code') || {}).value || '-1',
				user_id: (document.getElementById('user_id') || {}).value || '',
				order_id: (document.getElementById('order_id') || {}).value || ''
			};
			if (cfg.wholesaler) {
				out.office_id = (document.getElementById('office_id') || {}).value || '-1';
			}
			return out;
		}

		window.filterOperations = function () {
			setCookie('account_operations_filter', JSON.stringify(readFilters()), 15552000 * 1000);
			location = cfg.pageUrl;
		};

		window.unsetFilterOperations = function () {
			var empty = {
				time_from: '',
				time_to: '',
				income: -1,
				operation_code: -1,
				user_id: '',
				order_id: '',
				office_id: -1
			};
			setCookie('account_operations_filter', JSON.stringify(empty), -15552000 * 1000);
			location = cfg.pageUrl;
		};

		window.handle_user_selected = setCustomer;

		window.sortOperationsItems = function (field) {
			var ascDesc = 'asc';
			var current = getCookie('account_operations_sort');
			if (current !== undefined) {
				try {
					current = JSON.parse(current);
					if (current && current.field === field) {
						ascDesc = current.asc_desc === 'asc' ? 'desc' : 'asc';
					}
				} catch (e) {
					/* ignore */
				}
			}
			setCookie('account_operations_sort', JSON.stringify({ field: field, asc_desc: ascDesc }), 15552000 * 1000);
			location = cfg.pageUrl;
		};

		window.goToPage = function (needPage) {
			setCookie('account_operations_need_page', String(needPage), 15552000 * 1000);
			location = cfg.pageUrl;
		};

		window.getCookie = getCookie;

		window.closeCustomerModalInfo = function (userId) {
			var ev = window.event;
			if (!ev || !ev.target) {
				return;
			}
			var id = ev.target.id || '';
			if (id === 'customer-modal-info-' + userId || id === 'close-customer-modal-info-' + userId) {
				var wrap = document.querySelector('#customer-modal-info-' + userId);
				if (wrap) {
					wrap.classList.remove('customer-modal-info-wrapper-show');
				}
			}
		};

		window.showCustomerModalInfo = function (customerId) {
			var backend = (cfg.pageUrl || '/cp/shop/finance/account_operations').split('/shop/')[0] || '/cp';
			$.ajax({
				type: 'POST',
				async: false,
				url: backend + '/content/users/statistics/frontAjax/ajax_loadUserModal.php',
				dataType: 'json',
				data: {
					customer_id: customerId,
					csrf_guard_key: cfg.csrf || ''
				},
				success: function (answer) {
					if (answer && answer.status === true) {
						var wrap = document.querySelector('#customer-modal-info-' + customerId);
						if (wrap) {
							wrap.classList.add('customer-modal-info-wrapper-show');
							wrap.innerHTML = answer.modal;
						}
					} else {
						alert((cfg.labels && cfg.labels.modalError) ? cfg.labels.modalError : 'Error');
					}
				}
			});
		};

		function markSelectActive(id) {
			var el = document.getElementById(id);
			if (!el) {
				return;
			}
			var field = el.closest('.epc-ao-field');
			var v = el.value;
			markActive(field, v !== '' && v !== '-1');
		}

		function bindActiveMarkers() {
			['income', 'operation_code', 'order_id', 'office_id'].forEach(function (id) {
				var el = document.getElementById(id);
				if (!el) {
					return;
				}
				markSelectActive(id);
				el.addEventListener('change', function () {
					markSelectActive(id);
				});
				el.addEventListener('keyup', function () {
					markSelectActive(id);
				});
			});
		}

		initDateField('time_from_show', 'time_from', '00:00');
		initDateField('time_to_show', 'time_to', '23:59');
		initCustomerAutocomplete();
		bindActiveMarkers();

		$('.epc-ao-filter .form-control').on('keyup', function (event) {
			if (event.keyCode === 13) {
				window.filterOperations();
			}
		});
	}

	function scheduleBoot() {
		// Config may land in DOM after footer assets when scripts are relocated.
		var tries = 0;
		var timer = window.setInterval(function () {
			tries += 1;
			var cfg = readConfig();
			var hasPicker = $ && $.fn && $.fn.datetimepicker;
			var hasField = document.getElementById('time_from_show');
			if (cfg && hasPicker && hasField) {
				window.clearInterval(timer);
				boot();
				return;
			}
			if (tries >= 40) {
				window.clearInterval(timer);
				if (cfg && $) {
					boot();
				}
			}
		}, 50);
	}

	if (document.readyState === 'loading') {
		document.addEventListener('DOMContentLoaded', scheduleBoot);
	} else {
		scheduleBoot();
	}
})(window, document, window.jQuery);
