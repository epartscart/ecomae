(function () {
	var cfg = window.EPC_OC || {};
	var urls = cfg.urls || {};
	var msg = cfg.msg || {};

	function loadBootData() {
		var bootEl = document.getElementById('epc-oc-boot');
		if (!bootEl || !bootEl.textContent) {
			window.elements_array = [];
			window.elements_id_array = [];
			window.orders_items_ids_to_orders_items_objects = {};
			return;
		}
		try {
			var boot = JSON.parse(bootEl.textContent);
			window.elements_array = boot.elements_array || [];
			window.elements_id_array = boot.elements_id_array || [];
			window.orders_items_ids_to_orders_items_objects = boot.orders_items_ids_to_orders_items_objects || {};
		} catch (e) {
			window.elements_array = [];
			window.elements_id_array = [];
			window.orders_items_ids_to_orders_items_objects = {};
		}
	}

	window.deleteOrder = function () {
		if (!confirm(msg.deleteConfirm || 'Delete?')) {
			return;
		}
		jQuery.ajax({
			type: 'POST',
			async: true,
			url: urls.deleteOrders || '',
			dataType: 'json',
			data: 'orders_list=' + JSON.stringify([cfg.orderId]) + '&csrf_guard_key=' + encodeURIComponent(cfg.csrf || ''),
			success: function (answer) {
				if (answer.status === true) {
					location = urls.orders || '/cp/shop/orders/orders';
				} else {
					alert(answer.message || 'Error');
				}
			}
		});
	};

	window.setOrderNoViewed = function () {
		jQuery.ajax({
			type: 'POST',
			async: true,
			url: urls.setViewed || '',
			dataType: 'json',
			data: 'request_object=' + encodeURIComponent(JSON.stringify({
				orders: [cfg.orderId],
				viewed_flag: 0,
				user_id: cfg.managerId
			})) + '&csrf_guard_key=' + encodeURIComponent(cfg.csrf || ''),
			success: function (answer) {
				if (answer.status === true) {
					location = urls.orders || '/cp/shop/orders/orders';
				} else {
					alert(msg.setViewedFail || 'Error');
				}
			}
		});
	};

	window.auth_with_user = function () {
		jQuery.ajax({
			type: 'POST',
			async: false,
			url: urls.authWithUser || '',
			dataType: 'json',
			data: 'user_id=' + cfg.customerId + '&csrf_guard_key=' + encodeURIComponent(cfg.csrf || ''),
			success: function (answer) {
				if (answer.status === true) {
					window.open(cfg.domainPath || '/', '_blank');
				} else {
					alert(msg.authFail || 'Error');
				}
			}
		});
	};

	window.locationOrders = function () {
		var ordersFilter = {
			time_from: '', time_to: '', order_id: '', status: 0, paid: -1,
			viewed: -1, customer: '', customer_id: cfg.customerId, paid_type: -1
		};
		var date = new Date(new Date().getTime() + 15552000 * 1000);
		document.cookie = 'orders_filter=' + JSON.stringify(ordersFilter) + '; path=/; expires=' + date.toUTCString();
		window.open(urls.orders || '/cp/shop/orders/orders', '_blank');
	};

	window.locationBalance = function () {
		var accountFilter = {
			time_from: '', time_to: '', income: -1, operation_code: -1, user_id: cfg.customerId
		};
		var date = new Date(new Date().getTime() + 15552000 * 1000);
		document.cookie = 'account_operations_filter=' + JSON.stringify(accountFilter) + '; path=/; expires=' + date.toUTCString();
		window.open(urls.accountOps || '/cp/shop/finance/account_operations', '_blank');
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

	function initPage() {
		loadBootData();
		if (window.jQuery && jQuery.fn.footable) {
			jQuery(window).load(function () {
				jQuery('#order_items_table').footable();
			});
		}
	}

	if (document.readyState === 'loading') {
		document.addEventListener('DOMContentLoaded', initPage);
	} else {
		initPage();
	}
})();
