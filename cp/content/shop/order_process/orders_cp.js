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

	/**
	 * Open legacy print_docs with a fresh admin CSRF key from EPC_ORDERS.
	 * OMS AJAX pane used to emit empty csrf_guard_key → "Error! CSRF 3".
	 */
	window.epcOdOpenLegacyPrint = function (el) {
		if (!el) {
			return true;
		}
		var docName = el.getAttribute('data-doc-name') || '';
		var orderId = el.getAttribute('data-order-id') || '';
		var itemsRaw = el.getAttribute('data-order-items') || '[]';
		var csrf = (cfg && cfg.csrf) ? String(cfg.csrf) : '';
		if (!csrf) {
			csrf = getCookie('csrf_guard_key') || '';
		}
		var base = (urls && urls.legacyPrintBase) ? String(urls.legacyPrintBase) : '/content/shop/print_docs/service/print.php';
		if (!docName || !orderId) {
			return true;
		}
		if (!csrf) {
			alert('Print session expired. Refresh the Control Panel page and try again.');
			return false;
		}
		var href = base
			+ '?order_id=' + encodeURIComponent(orderId)
			+ '&csrf_admin=1'
			+ '&csrf_guard_key=' + encodeURIComponent(csrf)
			+ '&order_items=' + encodeURIComponent(itemsRaw)
			+ '&doc_name=' + encodeURIComponent(docName);
		window.open(href, '_blank');
		return false;
	};

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

		// Hydrate visible field from unix / config if SSR left it empty.
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
			// Fallback: keep typed dd.mm.yyyy values syncable on Apply.
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
		syncDateHidden('time_from_show', 'time_from');
		syncDateHidden('time_to_show', 'time_to');
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
		var tab = getCookie('orders_tab') || cfg.defaultTab || 'open';
		setLongCookie('orders_tab', tab);
		setLongCookie('orders_filter', JSON.stringify(f));
		goToPage(0);
	};

	window.unsetFilterOrders = function () {
		ordersAllTab();
	};

	window.ordersInProcess = function () {
		ordersOpenTab();
	};

	window.epcFilterByStatus = function (statusId) {
		var f = {
			time_from: '', time_to: '', order_id: '',
			status: [String(statusId)],
			paid: -1, customer: '', customer_id: '', viewed: -1,
			paid_type: -1, office: 0, phone: '', article: ''
		};
		setLongCookie('orders_tab', 'open');
		setLongCookie('orders_filter', JSON.stringify(f));
		goToPage(0);
	};

	function epcBlankFilter(statusVal) {
		return {
			time_from: '', time_to: '', order_id: '',
			status: statusVal,
			paid: -1, customer: '', customer_id: '', viewed: -1,
			paid_type: -1, office: 0, phone: '', article: ''
		};
	}

	window.ordersOpenTab = function () {
		setLongCookie('orders_tab', 'open');
		setLongCookie('orders_filter', JSON.stringify(epcBlankFilter(cfg.openStatuses || cfg.inProcessStatuses || [])));
		goToPage(0);
	};

	window.ordersCompletedTab = function () {
		setLongCookie('orders_tab', 'completed');
		setLongCookie('orders_filter', JSON.stringify(epcBlankFilter(cfg.completedStatuses || [])));
		goToPage(0);
	};

	window.ordersAllTab = function () {
		setLongCookie('orders_tab', 'all');
		setLongCookie('orders_filter', JSON.stringify(epcBlankFilter(0)));
		goToPage(0);
	};

	window.epcToggleOrdersFilter = function () {
		var panel = document.querySelector('.epc-orders-filter-panel');
		if (!panel) {
			return;
		}
		panel.classList.toggle('is-collapsed');
		var icon = panel.querySelector('.panel-tools .showhide i');
		if (icon) {
			icon.className = panel.classList.contains('is-collapsed') ? 'fa fa-chevron-down' : 'fa fa-chevron-up';
		}
		var label = panel.querySelector('.epc-filter-toggle');
		if (label) {
			label.textContent = panel.classList.contains('is-collapsed')
				? 'Advanced · click to expand'
				: 'Advanced · click to collapse';
		}
	};

	var epcOrdersSelectedId = cfg.selectedOrderId || 0;
	var epcOrdersAjaxUrl = urls.ajaxDetail || ((cfg.backend ? '/' + cfg.backend : '/cp') + '/content/shop/order_process/ajax_epc_orders_detail_pane.php');
	if (!urls.ajaxDetail && !(cfg && cfg.csrf)) {
		// Config boot failed — still allow OMS detail fetch with a sane default path.
		epcOrdersAjaxUrl = '/cp/content/shop/order_process/ajax_epc_orders_detail_pane.php';
	}
	var epcOrdersFullBase = urls.orderFullBase || '/cp/shop/orders/order?order_id=';

	function epcMarkWorkspaceActive(orderId) {
		var ws = document.querySelector('.epc-scp-orders-workspace');
		if (ws) {
			ws.classList.toggle('is-oms-active', !!orderId);
		}
		jQuery('.epc-scp-orders-row').removeClass('is-selected');
		if (orderId) {
			jQuery('.epc-scp-orders-row[data-order-id="' + orderId + '"]').addClass('is-selected');
		}
	}

	function epcBindOmsTabs(root) {
		if (!root || root.getAttribute('data-tabs-bound') === '1') {
			return;
		}
		root.setAttribute('data-tabs-bound', '1');
		var tabs = root.querySelectorAll('[data-epc-od-tab]');
		var panels = root.querySelectorAll('[data-epc-od-panel]');
		Array.prototype.forEach.call(tabs, function (btn) {
			btn.addEventListener('click', function () {
				var id = btn.getAttribute('data-epc-od-tab');
				Array.prototype.forEach.call(tabs, function (b) {
					b.classList.toggle('is-active', b === btn);
				});
				Array.prototype.forEach.call(panels, function (p) {
					p.classList.toggle('is-active', p.getAttribute('data-epc-od-panel') === id);
				});
			});
		});
	}

	function epcInitOmsPane(orderId) {
		epcMarkWorkspaceActive(orderId);
		var root = document.querySelector('.epc-od--oms[data-order-id="' + orderId + '"]')
			|| document.querySelector('.epc-od--oms');
		epcBindOmsTabs(root);
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
	}

	window.epcOmsGotoTab = function (tabId) {
		var btn = document.querySelector('.epc-od--oms [data-epc-od-tab="' + tabId + '"]');
		if (btn) {
			btn.click();
		}
	};

	window.epcOmsPayOrder = function (orderId) {
		var input = document.getElementById('epc_od_pay_value');
		var root = document.querySelector('.epc-od--oms[data-order-id="' + orderId + '"]');
		var payValue = input ? parseFloat(input.value) : NaN;
		var maxLeft = root ? parseFloat(root.getAttribute('data-paid-left') || '0') : 0;
		if (!payValue || isNaN(payValue) || payValue <= 0) {
			epcOdToast(msg.payEmpty || 'Enter a payment amount', false);
			return;
		}
		if (payValue > maxLeft + 0.001) {
			epcOdToast(msg.payTooMuch || 'Amount exceeds balance due', false);
			return;
		}
		var directPay = 1;
		var src = document.querySelector('input[name="epc_od_pay_source"]:checked');
		if (src) {
			directPay = parseInt(src.value, 10);
		}
		if (directPay === 0) {
			var bal = root ? parseFloat(root.getAttribute('data-customer-balance') || '0') : 0;
			if (payValue > bal && !window.confirm(msg.payBalanceWarn || 'Customer balance is lower than this amount. Continue?')) {
				return;
			}
		}
		jQuery.ajax({
			type: 'GET',
			url: urls.payForOrder || '/content/shop/protocol/pay_for_order.php',
			dataType: 'json',
			data: {
				order_id: orderId,
				pay_sum: payValue.toFixed(2),
				direct_pay: directPay,
				initiator: 1,
				csrf_guard_key: cfg.csrf || ''
			},
			success: function (answer) {
				if (answer && answer.status === true) {
					epcOdToast(msg.payOk || 'Payment recorded', true);
					epcLoadOrderDetail(orderId);
				} else {
					epcOdToast((answer && answer.message) || msg.payFail || 'Payment failed', false);
				}
			},
			error: function () {
				epcOdToast(msg.payFail || 'Payment failed', false);
			}
		});
	};

	window.epcOmsRefundOrder = function (orderId, directRefund) {
		if (!window.confirm(msg.refundConfirm || 'Refund this order payment?')) {
			return;
		}
		jQuery.ajax({
			type: 'GET',
			url: urls.payRefund || '',
			dataType: 'json',
			data: {
				order_id: orderId,
				direct_refund: directRefund ? 1 : 0,
				csrf_guard_key: cfg.csrf || ''
			},
			success: function (answer) {
				if (answer && answer.status === true) {
					epcOdToast(msg.refundOk || 'Refund completed', true);
					epcLoadOrderDetail(orderId);
				} else {
					epcOdToast((answer && answer.message) || msg.refundFail || 'Refund failed', false);
				}
			},
			error: function () {
				epcOdToast(msg.refundFail || 'Refund failed', false);
			}
		});
	};

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
		epcMarkWorkspaceActive(orderId);
		var applyDetailHtml = function (html) {
			pane.innerHTML = html || '';
			pane.classList.remove('is-loading');
			if (html && html.indexOf('epc-od') >= 0) {
				epcInitOmsPane(orderId);
			}
		};
		jQuery.ajax({
			type: 'GET',
			url: epcOrdersAjaxUrl,
			data: { order_id: orderId },
			dataType: 'html',
			success: function (html) {
				applyDetailHtml(html);
			},
			error: function (xhr) {
				var html = (xhr && xhr.responseText) ? xhr.responseText : '';
				if (html && html.indexOf('epc-od') >= 0) {
					applyDetailHtml(html);
					return;
				}
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

	function epcOmsPost(data, okMsg, failMsg) {
		data = data || {};
		data.csrf_guard_key = cfg.csrf || '';
		jQuery.ajax({
			type: 'POST',
			url: urls.omsAjax || '',
			dataType: 'json',
			data: data,
			success: function (answer) {
				if (answer && answer.status === true) {
					epcOdToast(okMsg || 'OK', true);
					if (data.order_id) {
						epcLoadOrderDetail(data.order_id);
					}
				} else {
					epcOdToast((answer && answer.message) || failMsg || 'Error', false);
				}
			},
			error: function () {
				epcOdToast(failMsg || 'Error', false);
			}
		});
	}

	window.epcOmsSaveItem = function (orderId, itemId) {
		var card = document.querySelector('.epc-od__line[data-item-id="' + itemId + '"], .epc-od__item-card[data-item-id="' + itemId + '"]');
		if (!card) {
			return;
		}
		epcOmsPost({
			action: 'update_item',
			order_id: orderId,
			item_id: itemId,
			price: (card.querySelector('[data-field="price"]') || {}).value,
			count_need: (card.querySelector('[data-field="count_need"]') || {}).value,
			t2_price_purchase: (card.querySelector('[data-field="t2_price_purchase"]') || {}).value,
			t2_storage_id: (card.querySelector('[data-field="t2_storage_id"]') || {}).value,
			t2_name: (card.querySelector('[data-field="t2_name"]') || {}).value
		}, msg.itemSaved || 'Item updated', msg.itemFail || 'Could not update item');
	};

	window.epcOmsSetItemStatus = function (orderId, itemId) {
		var card = document.querySelector('.epc-od__line[data-item-id="' + itemId + '"], .epc-od__item-card[data-item-id="' + itemId + '"]');
		if (!card) {
			return;
		}
		var status = (card.querySelector('[data-field="item_status"]') || {}).value;
		epcOmsPost({
			action: 'set_item_status',
			order_id: orderId,
			item_id: itemId,
			status: status
		}, msg.setStatusOk || 'Status updated', msg.setStatusFail || 'Error');
	};

	window.epcOmsMessageItem = function (orderId, itemId, article, price) {
		var hid = document.getElementById('epc_od_msg_item_id');
		var hint = document.getElementById('epc_od_msg_item_hint');
		var ta = document.getElementById('epc_od_msg_text');
		var tabBtn = document.querySelector('.epc-od--oms [data-epc-od-tab="messages"]');
		if (hid) {
			hid.value = String(itemId || 0);
		}
		if (hint) {
			hint.style.display = '';
			hint.textContent = 'Item context: #' + itemId + (article ? ' ' + article : '') + (price ? ' @ ' + price : '');
		}
		if (ta && !String(ta.value || '').trim()) {
			ta.value = 'Price update for item ' + (article || ('#' + itemId)) + ': new price is ' + (price || '') + '. Please confirm.';
		}
		if (tabBtn) {
			tabBtn.click();
		}
		if (ta) {
			ta.focus();
		}
	};

	window.epcOmsClearItemMsg = function () {
		var hid = document.getElementById('epc_od_msg_item_id');
		var hint = document.getElementById('epc_od_msg_item_hint');
		if (hid) {
			hid.value = '0';
		}
		if (hint) {
			hint.style.display = 'none';
			hint.textContent = '';
		}
	};

	window.epcOmsSendMessage = function (orderId) {
		var ta = document.getElementById('epc_od_msg_text');
		var hid = document.getElementById('epc_od_msg_item_id');
		var text = ta ? String(ta.value || '').trim() : '';
		if (!text) {
			epcOdToast(msg.msgEmpty || 'Enter a message first', false);
			return;
		}
		epcOmsPost({
			action: 'send_message',
			order_id: orderId,
			item_id: hid ? (hid.value || 0) : 0,
			text: text
		}, msg.msgSent || 'Message sent to customer', msg.msgFail || 'Could not send message');
	};

	window.epcSelectOrder = function (orderId, ev) {
		if (ev && (ev.target.tagName === 'INPUT' || ev.target.closest('input[type=checkbox]') ||
			ev.target.closest('.customer-modal-info-wrapper') || ev.target.closest('a.dropdown-toggle') ||
			ev.target.closest('a.btn') || ev.target.closest('button') || ev.target.closest('select') ||
			ev.target.closest('textarea'))) {
			return;
		}
		if (ev && (ev.metaKey || ev.ctrlKey || ev.shiftKey)) {
			window.location = epcOrdersFullBase + orderId;
			return;
		}
		epcLoadOrderDetail(orderId);
	};

	function epcOdToast(text, ok) {
		var el = document.getElementById('epc_od_toast');
		if (!el) {
			return;
		}
		el.textContent = text || '';
		el.className = 'epc-od__toast ' + (ok ? 'is-ok' : 'is-err');
		if (text) {
			window.setTimeout(function () {
				if (el.textContent === text) {
					el.className = 'epc-od__toast';
					el.textContent = '';
				}
			}, 3500);
		}
	}

	window.epcApplyOrderStatus = function (orderId) {
		var sel = document.getElementById('epc_od_status');
		if (!sel || !orderId) {
			return;
		}
		var needStatus = String(sel.value);
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
			url: urls.setOrderStatus || '',
			dataType: 'json',
			data: 'initiator=1&orders=' + JSON.stringify([orderId]) + '&status=' + encodeURIComponent(needStatus) +
				'&csrf_guard_key=' + encodeURIComponent(cfg.csrf || ''),
			success: function (answer) {
				if (answer && answer.status === true) {
					epcOdToast(msg.setStatusOk || 'Status updated', true);
					epcLoadOrderDetail(orderId);
					var row = document.querySelector('.epc-scp-orders-row[data-order-id="' + orderId + '"]');
					if (row) {
						row.classList.remove('not_viewed');
					}
				} else if (answer && answer.message) {
					epcOdToast((msg.setStatusFail || 'Error') + '. ' + answer.message, false);
				} else {
					epcOdToast(msg.setStatusFail || 'Error', false);
				}
			},
			error: function () {
				epcOdToast(msg.setStatusFail || 'Error', false);
			}
		});
	};

	window.epcAddOrderComment = function (orderId) {
		var ta = document.getElementById('epc_od_comment');
		if (!ta || !orderId) {
			return;
		}
		var text = String(ta.value || '').trim();
		if (!text) {
			epcOdToast(msg.commentEmpty || 'Enter a note first', false);
			return;
		}
		jQuery.ajax({
			type: 'GET',
			url: urls.addComment || '',
			dataType: 'json',
			data: {
				order_id: orderId,
				text: text,
				csrf_guard_key: cfg.csrf || ''
			},
			success: function (answer) {
				if (answer && answer.status === true) {
					ta.value = '';
					epcOdToast(msg.commentOk || 'Note saved', true);
					epcLoadOrderDetail(orderId);
				} else {
					epcOdToast((answer && answer.message) || msg.commentFail || 'Could not save note', false);
				}
			},
			error: function () {
				epcOdToast(msg.commentFail || 'Could not save note', false);
			}
		});
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
		if (cfg.rewriteFilter && cfg.defaultFilter) {
			setLongCookie('orders_tab', cfg.defaultTab || 'open');
			setLongCookie('orders_filter', JSON.stringify(cfg.defaultFilter));
		}
		initDatePicker('time_from', 'time_from_show', '00:00');
		initDatePicker('time_to', 'time_to_show', '23:59');
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

		if (!getCookie('orders_tab')) {
			setLongCookie('orders_tab', cfg.defaultTab || 'open');
		}

		if (cfg.autoRunInProcess) {
			ordersInProcess();
		} else {
			var pane = document.getElementById('epc_orders_detail_pane');
			var ssrOd = pane ? pane.querySelector('.epc-od[data-order-id]') : null;
			var ssrId = ssrOd ? parseInt(ssrOd.getAttribute('data-order-id') || '0', 10) : 0;
			if (ssrId > 0) {
				epcOrdersSelectedId = ssrId;
				epcInitOmsPane(ssrId);
			} else if (epcOrdersSelectedId > 0) {
				epcLoadOrderDetail(epcOrdersSelectedId);
			} else if (cfg.autoOpenFirstOrder && cfg.firstOrderId > 0) {
				epcLoadOrderDetail(cfg.firstOrderId);
			} else {
				// Boot payload may carry first visible order when config omitted it
				var bootEl = document.getElementById('epc-orders-boot');
				if (bootEl && bootEl.textContent && cfg.autoOpenFirstOrder) {
					try {
						var boot = JSON.parse(bootEl.textContent);
						var fid = parseInt(boot.firstOrderId || 0, 10);
						if (fid > 0) {
							epcLoadOrderDetail(fid);
						}
					} catch (e2) {
					}
				}
			}
		}
	}

	if (document.readyState === 'loading') {
		document.addEventListener('DOMContentLoaded', initPage);
	} else {
		initPage();
	}
})();
