(function () {
	var cfg = window.EPC_OI || {};
	var backend = cfg.backend || 'cp';
	var baseItems = cfg.urls && cfg.urls.items ? cfg.urls.items : '/' + backend + '/shop/orders/items';

	function getCookie(name) {
		var matches = document.cookie.match(new RegExp(
			'(?:^|; )' + name.replace(/([.$?*|{}()[\]\\/+^])/g, '\\$1') + '=([^;]*)'
		));
		return matches ? decodeURIComponent(matches[1]) : undefined;
	}

	function setLongCookie(name, value) {
		var date = new Date(new Date().getTime() + 15552000 * 1000);
		document.cookie = name + '=' + value + '; path=/; expires=' + date.toUTCString();
	}

	window.getCookie = getCookie;

	window.goToPage = function (needPage) {
		setLongCookie('orders_items_need_page', String(needPage));
		location = baseItems;
	};

	window.sortOrdersItems = function (field) {
		var ascDesc = 'asc';
		var current = getCookie('orders_items_sort');
		if (current !== undefined) {
			current = JSON.parse(current);
			if (current.field === field) {
				ascDesc = current.asc_desc === 'asc' ? 'desc' : 'asc';
			}
		}
		setLongCookie('orders_items_sort', JSON.stringify({ field: field, asc_desc: ascDesc }));
		goToPage(0);
	};

	window.changeColorFilter = function (filter) {
		if (window.jQuery) {
			jQuery('.ms-choice', jQuery(filter)).css('background-color', '#b9fcab');
		}
	};

	function readFilterCookie() {
		var raw = getCookie('orders_items_filter');
		if (raw === undefined) {
			return null;
		}
		try {
			return JSON.parse(raw);
		} catch (e) {
			return null;
		}
	}

	function initMultipleSelect(id, divId, cookieField, emptyValue) {
		if (!window.jQuery || !jQuery.fn.multipleSelect) {
			return;
		}
		var placeholder = (cfg.msg && cfg.msg.selectPlaceholder) ? cfg.msg.selectPlaceholder : '';
		jQuery('#' + id).multipleSelect({ placeholder: placeholder, width: '100%' });
		var filter = readFilterCookie();
		if (filter && filter[cookieField] !== undefined && filter[cookieField] !== emptyValue) {
			jQuery('#' + id).multipleSelect('setSelects', filter[cookieField]);
			if (divId) {
				changeColorFilter('#' + divId);
			}
		}
	}

	function pad2(n) {
		return (n < 10 ? '0' : '') + n;
	}

	function formatPickerTime(currentTime) {
		if (!currentTime) {
			return '';
		}
		var dateOb = currentTime instanceof Date ? currentTime : new Date(currentTime);
		if (isNaN(dateOb.getTime())) {
			return '';
		}
		return pad2(dateOb.getDate()) + '.' + pad2(dateOb.getMonth() + 1) + '.' + dateOb.getFullYear() + ' ' +
			pad2(dateOb.getHours()) + ':' + pad2(dateOb.getMinutes());
	}

	function parseShowDate(value) {
		var v = String(value || '').trim();
		if (!v) {
			return null;
		}
		var m = v.match(/^(\d{1,2})\.(\d{1,2})\.(\d{4})(?:\s+(\d{1,2}):(\d{1,2}))?$/);
		if (!m) {
			return null;
		}
		var d = new Date(
			parseInt(m[3], 10),
			parseInt(m[2], 10) - 1,
			parseInt(m[1], 10),
			parseInt(m[4] || '0', 10),
			parseInt(m[5] || '0', 10),
			0
		);
		return isNaN(d.getTime()) ? null : d;
	}

	function syncDateHidden(showId, hiddenId) {
		var showEl = document.getElementById(showId);
		var hidEl = document.getElementById(hiddenId);
		if (!showEl || !hidEl) {
			return;
		}
		var parsed = parseShowDate(showEl.value);
		if (!parsed) {
			if (!String(showEl.value || '').trim()) {
				hidEl.value = '';
			}
			return;
		}
		hidEl.value = String(Math.floor(parsed.getTime() / 1000));
		var field = showEl.closest('.epc-orders-filter-field');
		if (field) {
			field.classList.add('is-active');
		}
	}

	function initDatePicker(hiddenId, showId, defaultTime) {
		var showEl = document.getElementById(showId);
		var hidEl = document.getElementById(hiddenId);
		if (!showEl || !hidEl) {
			return;
		}
		if (!String(showEl.value || '').trim()) {
			var unix = parseInt(hidEl.value || (hiddenId === 'time_from' ? cfg.timeFrom : cfg.timeTo) || '0', 10);
			if (unix > 0) {
				showEl.value = formatPickerTime(new Date(unix * 1000));
				var field = showEl.closest('.epc-orders-filter-field');
				if (field) {
					field.classList.add('is-active');
				}
			}
		}
		if (!window.jQuery || !jQuery.fn.datetimepicker) {
			showEl.addEventListener('change', function () {
				syncDateHidden(showId, hiddenId);
			});
			return;
		}
		var syncFromPicker = function (currentTime) {
			if (!currentTime) {
				hidEl.value = '';
				showEl.value = '';
				return;
			}
			showEl.value = formatPickerTime(currentTime);
			hidEl.value = String(Math.floor(currentTime.getTime() / 1000));
			var field = showEl.closest('.epc-orders-filter-field');
			if (field) {
				field.classList.add('is-active');
			}
		};
		jQuery('#' + showId).datetimepicker({
			lang: cfg.lang || 'en',
			closeOnDateSelect: false,
			closeOnTimeSelect: true,
			dayOfWeekStart: 1,
			format: 'd.m.Y H:i',
			defaultTime: defaultTime || '00:00',
			onChangeDateTime: syncFromPicker,
			onClose: syncFromPicker
		});
		showEl.addEventListener('change', function () {
			syncDateHidden(showId, hiddenId);
		});
	}

	window.filterOrdersItems = function () {
		syncDateHidden('time_from_show', 'time_from');
		syncDateHidden('time_to_show', 'time_to');
		var f = {
			time_from: document.getElementById('time_from').value,
			time_to: document.getElementById('time_to').value,
			order_id: document.getElementById('order_id').value,
			customer: document.getElementById('customer').value,
			customer_id: document.getElementById('customer_id').value,
			product_name: encodeURIComponent(document.getElementById('product_name').value),
			article: encodeURIComponent(document.getElementById('article').value),
			manufacturer: encodeURIComponent(document.getElementById('manufacturer').value),
			phone: encodeURIComponent(document.getElementById('phone').value)
		};
		f.order_status = jQuery('#order_status').multipleSelect('getSelects', 'value').length === 0
			? 0 : jQuery('#order_status').multipleSelect('getSelects', 'value');
		f.paid = jQuery('#paid').multipleSelect('getSelects', 'value').length === 0
			? -1 : jQuery('#paid').multipleSelect('getSelects', 'value');
		f.order_item_status = jQuery('#order_item_status').multipleSelect('getSelects', 'value').length === 0
			? 0 : jQuery('#order_item_status').multipleSelect('getSelects', 'value');
		f.office_id = jQuery('#office_id').multipleSelect('getSelects', 'value').length === 0
			? 0 : jQuery('#office_id').multipleSelect('getSelects', 'value');
		f.viewed = jQuery('#viewed').multipleSelect('getSelects', 'value').length === 0
			? -1 : jQuery('#viewed').multipleSelect('getSelects', 'value');
		f.storage_id = jQuery('#storage_id').multipleSelect('getSelects', 'value').length === 0
			? -1 : jQuery('#storage_id').multipleSelect('getSelects', 'value');
		setLongCookie('orders_items_filter', JSON.stringify(f));
		goToPage(0);
	};

	window.unsetFilterOrdersItems = function () {
		var f = {
			time_from: '', time_to: '', order_id: '', order_status: 0, paid: -1,
			customer: '', customer_id: '', order_item_status: 0, office_id: 0,
			product_name: '', article: '', manufacturer: '', viewed: -1,
			storage_id: -1, phone: ''
		};
		setLongCookie('orders_items_filter', JSON.stringify(f));
		goToPage(0);
	};

	window.itemsInProcess = function () {
		var f = {
			time_from: '', time_to: '', order_id: '', order_status: 0, paid: -1,
			customer: '', customer_id: '', order_item_status: cfg.inProcessStatuses || [],
			office_id: 0, product_name: '', article: '', manufacturer: '',
			viewed: -1, storage_id: -1, phone: ''
		};
		setLongCookie('orders_items_filter', JSON.stringify(f));
		goToPage(0);
	};

	window.closeCustomerModalInfo = function (userId) {
		var ev = window.event;
		if (!ev || !ev.srcElement) {
			return;
		}
		if (ev.srcElement.id === 'customer-modal-info-' + userId || ev.srcElement.id === 'close-customer-modal-info-' + userId) {
			var el = document.querySelector('#customer-modal-info-' + userId);
			if (el) {
				el.classList.remove('customer-modal-info-wrapper-show');
			}
		}
	};

	window.showCustomerModalInfo = function (customerId) {
		jQuery.ajax({
			type: 'POST',
			async: false,
			url: cfg.urls.userModal,
			dataType: 'json',
			data: 'customer_id=' + customerId + '&csrf_guard_key=' + encodeURIComponent(cfg.csrf || ''),
			success: function (answer) {
				var el = document.querySelector('#customer-modal-info-' + customerId);
				if (!el) {
					return;
				}
				if (answer.status === true) {
					el.classList.add('customer-modal-info-wrapper-show');
					el.innerHTML = answer.modal;
				} else {
					alert(cfg.msg.userModalFail || 'Error');
				}
			}
		});
	};

	window.setOrderViewed = function () {
		var checked = getCheckedElements();
		if (!checked.length) {
			alert(cfg.msg.selectItemsViewed || 'Select items');
			return;
		}
		var orders = [];
		for (var i = 0; i < checked.length; i++) {
			orders.push(orders_items_to_orders_map[checked[i]]);
		}
		jQuery.ajax({
			type: 'POST',
			async: true,
			url: cfg.urls.setViewed,
			dataType: 'json',
			data: 'request_object=' + encodeURIComponent(JSON.stringify({
				orders: orders,
				viewed_flag: document.getElementById('setOrderViewed').value,
				user_id: cfg.managerId
			})) + '&csrf_guard_key=' + encodeURIComponent(cfg.csrf || ''),
			success: function (answer) {
				if (answer.status === true) {
					location = baseItems;
				} else {
					alert(cfg.msg.setViewedFail || 'Error');
				}
			}
		});
	};

	window.setOrderItemsStatus = function () {
		var ordersItems = getCheckedElements();
		if (!ordersItems.length) {
			alert(cfg.msg.selectItemsStatus || 'Select items');
			return;
		}
		var needStatus = document.getElementById('setOrderItemsStatusSelect').value;
		jQuery.ajax({
			type: 'GET',
			async: false,
			url: cfg.urls.setItemStatus,
			dataType: 'json',
			data: 'initiator=1&orders_items=' + encodeURIComponent(JSON.stringify(ordersItems)) +
				'&status=' + encodeURIComponent(needStatus) +
				'&csrf_guard_key=' + encodeURIComponent(cfg.csrf || ''),
			success: function (answer) {
				if (answer.status === true) {
					location = baseItems + '?success_message=' + encodeURIComponent(cfg.msg.statusOk || 'OK');
				} else if (answer.message) {
					alert(answer.message);
				} else {
					alert(cfg.msg.setStatusFail || 'Error');
				}
			}
		});
	};

	window.on_check_uncheck_all = function () {
		var state = document.getElementById('check_uncheck_all').checked;
		for (var i = 0; i < elements_array.length; i++) {
			document.getElementById(elements_array[i]).checked = state;
		}
	};

	window.on_one_check_changed = function () {
		for (var i = 0; i < elements_array.length; i++) {
			if (!document.getElementById(elements_array[i]).checked) {
				document.getElementById('check_uncheck_all').checked = false;
				return;
			}
		}
	};

	window.getCheckedElements = function () {
		var checked = [];
		for (var i = 0; i < elements_array.length; i++) {
			if (document.getElementById(elements_array[i]).checked) {
				checked.push(elements_id_array[i]);
			}
		}
		return checked;
	};

	function loadBootData() {
		var bootEl = document.getElementById('epc-oi-boot');
		if (!bootEl || !bootEl.textContent) {
			window.elements_array = [];
			window.elements_id_array = [];
			window.orders_items_to_orders_map = {};
			window.orders_items_ids_to_orders_items_objects = {};
			return;
		}
		try {
			var boot = JSON.parse(bootEl.textContent);
			window.elements_array = boot.elements_array || [];
			window.elements_id_array = boot.elements_id_array || [];
			window.orders_items_to_orders_map = boot.orders_items_to_orders_map || {};
			window.orders_items_ids_to_orders_items_objects = boot.orders_items_ids_to_orders_items_objects || {};
		} catch (e) {
			window.elements_array = [];
			window.elements_id_array = [];
			window.orders_items_to_orders_map = {};
			window.orders_items_ids_to_orders_items_objects = {};
		}
	}

	function initPage() {
		loadBootData();
		initDatePicker('time_from', 'time_from_show', '00:00');
		initDatePicker('time_to', 'time_to_show', '23:59');
		initMultipleSelect('paid', 'paid_div', 'paid', -1);
		initMultipleSelect('order_status', 'order_status_div', 'order_status', 0);
		initMultipleSelect('order_item_status', 'order_item_status_div', 'order_item_status', 0);
		initMultipleSelect('office_id', 'office_id_div', 'office_id', 0);
		initMultipleSelect('viewed', 'viewed_div', 'viewed', -1);
		initMultipleSelect('storage_id', 'storage_id_div', 'storage_id', -1);

		jQuery('.filter_panel .form-control').keyup(function (event) {
			if (event.keyCode === 13) {
				filterOrdersItems();
			}
		});

		if (jQuery.fn.footable) {
			jQuery('#orders_items_table').footable();
		}
		var sorter = document.getElementById((cfg.sortField || 'id') + '_sorter');
		if (sorter) {
			sorter.innerHTML += '<img src="/content/files/images/sort_' + (cfg.sortDir || 'desc') + '.png" style="width:15px" />';
		}
	}

	if (document.readyState === 'loading') {
		document.addEventListener('DOMContentLoaded', initPage);
	} else {
		initPage();
	}
})();
