(function () {
	var cfg = window.EPC_ORDERS || {};
	var urls = cfg.urls || {};
	var msg = cfg.msg || {};
	var baseOrders = urls.orders || '/cp/shop/orders/orders';

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

	function formatPickerTime(currentTime) {
		var dateOb = new Date(currentTime);
		return dateOb.getDate() + '.' + (dateOb.getMonth() + 1) + '.' + dateOb.getFullYear() + ' ' +
			dateOb.getHours() + ':' + dateOb.getMinutes();
	}

	function initDatePicker(inputId, showId) {
		if (!window.jQuery || !jQuery.fn.datetimepicker) {
			return;
		}
		var opts = {
			lang: cfg.lang || 'en',
			closeOnDateSelect: true,
			closeOnTimeSelect: false,
			dayOfWeekStart: 1,
			format: 'unixtime',
			onClose: function (currentTime) {
				var el = document.getElementById(showId);
				if (el) {
					el.value = formatPickerTime(currentTime);
				}
			}
		};
		if ((inputId === 'time_from' && cfg.timeFrom) || (inputId === 'time_to' && cfg.timeTo)) {
			opts.onGenerate = opts.onClose;
		}
		jQuery('#' + inputId).datetimepicker(opts);
	}

	function msOpts() {
		return {
			placeholder: msg.selectPlaceholder || '',
			width: '100%',
			selectAllText: msg.selectAllText || '',
			allSelected: msg.allSelected || '',
			countSelected: msg.countSelected || ''
		};
	}

	function initMultipleSelect(id, divId, cookieField, emptyValue) {
		if (!window.jQuery || !jQuery.fn.multipleSelect) {
			return;
		}
		jQuery('#' + id).multipleSelect(msOpts());
		var raw = getCookie('orders_filter');
		if (raw === undefined) {
			return;
		}
		try {
			var filter = JSON.parse(raw);
			if (filter && filter[cookieField] !== undefined && filter[cookieField] !== emptyValue) {
				jQuery('#' + id).multipleSelect('setSelects', filter[cookieField]);
				changeColorFilter('#' + divId);
			}
		} catch (e) {
		}
	}

	window.changeColorFilter = function (filter) {
		if (window.jQuery) {
			jQuery('.ms-choice', jQuery(filter)).css('background-color', '#b9fcab');
		}
	};

	window.goToPage = function (needPage) {
		setLongCookie('orders_need_page', String(needPage));
		location = baseOrders;
	};

	window.sortOrders = function (field) {
		var ascDesc = 'asc';
		var current = getCookie('orders_sort');
		if (current !== undefined) {
			current = JSON.parse(current);
			if (current.field === field) {
				ascDesc = current.asc_desc === 'asc' ? 'desc' : 'asc';
			}
		}
		setLongCookie('orders_sort', JSON.stringify({ field: field, asc_desc: ascDesc }));
		goToPage(0);
	};

	window.filterOrders = function () {
		var f = {
			time_from: document.getElementById('time_from').value,
			time_to: document.getElementById('time_to').value,
			order_id: document.getElementById('order_id').value,
			customer: document.getElementById('customer').value,
			customer_id: document.getElementById('customer_id').value,
			phone: encodeURIComponent(document.getElementById('phone').value),
			article: encodeURIComponent(document.getElementById('article').value)
		};
		f.status = jQuery('#status').multipleSelect('getSelects', 'value').length === 0
			? 0 : jQuery('#status').multipleSelect('getSelects', 'value');
		f.paid = jQuery('#paid').multipleSelect('getSelects', 'value').length === 0
			? -1 : jQuery('#paid').multipleSelect('getSelects', 'value');
		f.viewed = jQuery('#viewed').multipleSelect('getSelects', 'value').length === 0
			? -1 : jQuery('#viewed').multipleSelect('getSelects', 'value');
		f.paid_type = jQuery('#paid_type').multipleSelect('getSelects', 'value').length === 0
			? -1 : jQuery('#paid_type').multipleSelect('getSelects', 'value');
		f.office = jQuery('#office').multipleSelect('getSelects', 'value').length === 0
			? 0 : jQuery('#office').multipleSelect('getSelects', 'value');
		setLongCookie('orders_filter', JSON.stringify(f));
		goToPage(0);
	};

	window.unsetFilterOrders = function () {
		var f = {
			time_from: '', time_to: '', order_id: '', status: 0, paid: -1,
			customer: '', customer_id: '', viewed: -1, paid_type: -1,
			office: 0, phone: '', article: ''
		};
		setLongCookie('orders_filter', JSON.stringify(f));
		goToPage(0);
	};

	window.ordersInProcess = function () {
		var f = {
			time_from: '', time_to: '', order_id: '',
			status: cfg.inProcessStatuses || [],
			paid: -1, customer: '', customer_id: '', viewed: -1,
			paid_type: -1, office: 0, phone: '', article: ''
		};
		setLongCookie('orders_filter', JSON.stringify(f));
		goToPage(0);
	};

	window.epcFilterByStatus = function (statusId) {
		var f = {
			time_from: '', time_to: '', order_id: '',
			status: [String(statusId)],
			paid: -1, customer: '', customer_id: '', viewed: -1,
			paid_type: -1, office: 0, phone: '', article: ''
		};
		setLongCookie('orders_filter', JSON.stringify(f));
		goToPage(0);
	};

	var epcOrdersSelectedId = cfg.selectedOrderId || 0;
	var epcOrdersAjaxUrl = urls.ajaxDetail || '';
	var epcOrdersFullBase = urls.orderFullBase || '/cp/shop/orders/order?order_id=';

	window.epcLoadOrderDetail = function (orderId) {
		if (!orderId) {
			return;
		}
		epcOrdersSelectedId = orderId;
		var pane = document.getElementById('epc_orders_detail_pane');
		if (!pane) {
			return;
		}
		pane.classList.add('is-loading');
		jQuery('.epc-scp-orders-row').removeClass('is-selected');
		jQuery('.epc-scp-orders-row[data-order-id="' + orderId + '"]').addClass('is-selected');
		jQuery.ajax({
			type: 'GET',
			url: epcOrdersAjaxUrl,
			data: { order_id: orderId },
			success: function (html) {
				pane.innerHTML = html;
				pane.classList.remove('is-loading');
				if (window.epcOrdersFulfillment && urls.erpAjax) {
					var mount = document.getElementById('epc-order-fulfillment-panel-' + orderId);
					if (mount) {
						window.epcOrdersFulfillment.load({
							mount: mount,
							orderId: orderId,
							ajaxUrl: urls.erpAjax,
						});
					}
				}
			},
			error: function () {
				pane.innerHTML = '<div class="epc-scp-orders-detail__empty"><i class="fa fa-exclamation-triangle"></i><p>Could not load order detail</p></div>';
				pane.classList.remove('is-loading');
			}
		});
		if (window.history && window.history.replaceState) {
			var u = new URL(window.location.href);
			u.searchParams.set('order_id', orderId);
			window.history.replaceState({}, '', u.toString());
		}
	};

	window.epcSelectOrder = function (orderId, ev) {
		if (ev && (ev.target.tagName === 'INPUT' || ev.target.closest('input[type=checkbox]') ||
			ev.target.closest('.customer-modal-info-wrapper') || ev.target.closest('a.dropdown-toggle'))) {
			return;
		}
		if (ev && (ev.metaKey || ev.ctrlKey || ev.shiftKey)) {
			window.location = epcOrdersFullBase + orderId;
			return;
		}
		epcLoadOrderDetail(orderId);
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
			url: urls.userModal || '',
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
					alert(msg.userModalFail || 'Error');
				}
			}
		});
	};

	window.setOrdersStatus = function () {
		var checkedOrders = getCheckedElements();
		if (!checkedOrders.length) {
			alert(msg.selectOrders || 'Select orders');
			return;
		}
		var needStatus = document.getElementById('setOrderStatusSelect').value;
		var finish = cfg.statusesForFinish || [];
		var inverse = cfg.statusesForInverse || [];
		if (finish.indexOf(needStatus) !== -1 && !confirm(msg.finishConfirm || 'Continue?')) {
			return;
		}
		if (inverse.indexOf(needStatus) !== -1 && !confirm(msg.inverseConfirm || 'Continue?')) {
			return;
		}
		jQuery.ajax({
			type: 'GET',
			async: false,
			url: urls.setOrderStatus || '',
			dataType: 'json',
			data: 'initiator=1&orders=' + JSON.stringify(checkedOrders) + '&status=' + needStatus +
				'&csrf_guard_key=' + encodeURIComponent(cfg.csrf || ''),
			success: function (answer) {
				if (answer.status === true) {
					location = baseOrders;
				} else if (answer.message) {
					alert((msg.setStatusFail || 'Error') + '. ' + answer.message);
				} else {
					alert(msg.setStatusFail || 'Error');
				}
			}
		});
	};

	window.setOrderViewed = function () {
		var ordersChecked = getCheckedElements();
		if (!ordersChecked.length) {
			alert(msg.selectOrdersViewed || 'Select orders');
			return;
		}
		jQuery.ajax({
			type: 'POST',
			async: true,
			url: urls.setViewed || '',
			dataType: 'json',
			data: 'request_object=' + encodeURIComponent(JSON.stringify({
				orders: ordersChecked,
				viewed_flag: document.getElementById('setOrderViewed').value,
				user_id: cfg.managerId
			})) + '&csrf_guard_key=' + encodeURIComponent(cfg.csrf || ''),
			success: function (answer) {
				if (answer.status === true) {
					location = baseOrders;
				} else {
					alert(msg.setViewedFail || 'Error');
				}
			}
		});
	};

	window.deleteSelectedeOrders = function () {
		var checkedOrders = getCheckedElements();
		if (!checkedOrders.length) {
			alert(msg.selectOrders || 'Select orders');
			return;
		}
		if (!confirm(msg.deleteConfirm || 'Delete?')) {
			return;
		}
		jQuery.ajax({
			type: 'POST',
			async: true,
			url: urls.deleteOrders || '',
			dataType: 'json',
			data: 'orders_list=' + JSON.stringify(checkedOrders) + '&csrf_guard_key=' + encodeURIComponent(cfg.csrf || ''),
			success: function (answer) {
				if (answer.status === true) {
					location = baseOrders;
				} else {
					alert(answer.message || 'Error');
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
		var bootEl = document.getElementById('epc-orders-boot');
		if (!bootEl || !bootEl.textContent) {
			window.elements_array = [];
			window.elements_id_array = [];
			return;
		}
		try {
			var boot = JSON.parse(bootEl.textContent);
			window.elements_array = boot.elements_array || [];
			window.elements_id_array = boot.elements_id_array || [];
		} catch (e) {
			window.elements_array = [];
			window.elements_id_array = [];
		}
	}

	function initPage() {
		loadBootData();
		initDatePicker('time_from', 'time_from_show');
		initDatePicker('time_to', 'time_to_show');
		initMultipleSelect('paid', 'paid_div', 'paid', -1);
		initMultipleSelect('paid_type', 'paid_type_div', 'paid_type', -1);
		initMultipleSelect('status', 'status_div', 'status', 0);
		initMultipleSelect('viewed', 'viewed_div', 'viewed', -1);
		initMultipleSelect('office', 'office_div', 'office', 0);

		jQuery('.filter_panel .form-control').keyup(function (event) {
			if (event.keyCode === 13) {
				filterOrders();
			}
		});

		if (jQuery.fn.footable) {
			jQuery(window).load(function () {
				jQuery('#orders_table').footable();
				var sorter = document.getElementById((cfg.sortField || 'id') + '_sorter');
				if (sorter) {
					sorter.innerHTML += '<img src="/content/files/images/sort_' + (cfg.sortDir || 'desc') + '.png" style="width:15px" />';
				}
			});
		}

		if (cfg.autoRunInProcess) {
			ordersInProcess();
		} else if (epcOrdersSelectedId > 0) {
			epcLoadOrderDetail(epcOrdersSelectedId);
		}
	}

	if (document.readyState === 'loading') {
		document.addEventListener('DOMContentLoaded', initPage);
	} else {
		initPage();
	}
})();
