/**
 * ERP order fulfillment panel — SO/PO linkage, sync, posting hooks.
 */
(function (window, document) {
	'use strict';

	function qs(sel, root) {
		return (root || document).querySelector(sel);
	}

	function postJson(url, data) {
		var body = new URLSearchParams();
		Object.keys(data).forEach(function (k) {
			if (data[k] !== undefined && data[k] !== null) {
				body.append(k, String(data[k]));
			}
		});
		return fetch(url, {
			method: 'POST',
			credentials: 'same-origin',
			headers: { 'Content-Type': 'application/x-www-form-urlencoded; charset=UTF-8' },
			body: body.toString(),
		}).then(function (r) { return r.json(); });
	}

	function badge(text, tone) {
		return '<span class="epc-scp-badge epc-scp-badge--' + (tone || 'normal') + '">' + text + '</span>';
	}

	function renderPanel(el, status) {
		if (!el || !status) {
			return;
		}
		var so = status.sales_order || null;
		var pos = status.purchase_orders || [];
		var html = '<div class="epc-of-panel">';
		html += '<h4 class="epc-of-panel__title"><i class="fa fa-random"></i> ERP fulfillment</h4>';
		html += '<div class="epc-of-panel__row">Status: ' + badge(status.fulfillment_status || 'none', status.fulfillment_status === 'fulfilled' ? 'tenant' : 'high') + '</div>';
		if (so) {
			html += '<div class="epc-of-panel__row">Sales order: <strong>' + (so.so_no || ('#' + so.id)) + '</strong> (' + (so.status || 'open') + ')</div>';
		} else {
			html += '<div class="epc-of-panel__row text-muted">No ERP sales order linked yet.</div>';
		}
		if (pos.length) {
			html += '<ul class="epc-of-panel__po-list">';
			pos.forEach(function (po) {
				html += '<li>PO ' + (po.po_no || po.id) + ' — ' + (po.supplier_name || 'Supplier') +
					' ' + badge(po.status || 'draft', po.status === 'received' ? 'tenant' : 'normal') + '</li>';
			});
			html += '</ul>';
		}
		var acct = status.accounting || {};
		html += '<div class="epc-of-panel__row small text-muted">Cost posted: ' + (acct.cost_posted ? 'yes' : 'no') +
			' · Revenue posted: ' + (acct.revenue_posted ? 'yes' : 'no') + '</div>';
		html += '<div class="epc-of-panel__actions">';
		if (!so) {
			html += '<button type="button" class="btn btn-default btn-xs epc-of-btn-bootstrap">Link ERP</button> ';
		}
		html += '<button type="button" class="btn btn-default btn-xs epc-of-btn-sync">Sync status</button> ';
		html += '<button type="button" class="btn btn-primary btn-xs epc-of-btn-auto">Auto-post</button>';
		html += '</div>';
		html += '<div class="epc-of-panel__msg small text-muted"></div>';
		html += '</div>';
		el.innerHTML = html;
	}

	function bindPanel(el, cfg) {
		if (!el || !cfg || !cfg.orderId) {
			return;
		}
		el.addEventListener('click', function (ev) {
			var t = ev.target;
			if (!t || !t.classList) {
				return;
			}
			var msg = qs('.epc-of-panel__msg', el);
			var action = '';
			var payload = { order_id: cfg.orderId };
			if (t.classList.contains('epc-of-btn-bootstrap')) {
				action = 'order_fulfillment_bootstrap';
			} else if (t.classList.contains('epc-of-btn-sync')) {
				action = 'order_fulfillment_sync';
			} else if (t.classList.contains('epc-of-btn-auto')) {
				action = 'order_fulfillment_auto_post';
			} else {
				return;
			}
			ev.preventDefault();
			if (msg) {
				msg.textContent = 'Working…';
			}
			postJson(cfg.ajaxUrl, { action: action, order_id: cfg.orderId })
				.then(function (res) {
					if (!res || !res.status) {
						throw new Error((res && res.message) || 'Request failed');
					}
					var st = res.status && res.sales_order ? res : (res.status || res);
					if (res.status && res.fulfillment_status) {
						st = res;
					} else if (res.status && res.status.fulfillment_status) {
						st = res.status;
					} else if (res.status && res.status.sales_order) {
						st = res.status;
					} else if (res.sales_order || res.purchase_orders) {
						st = res;
					} else if (res.status && typeof res.status === 'object' && res.status.shop_order_id) {
						st = res.status;
					}
					renderPanel(el, st);
					bindPanel(el, cfg);
					if (msg) {
						msg.textContent = res.message || 'Done';
					}
				})
				.catch(function (err) {
					if (msg) {
						msg.textContent = err.message || 'Error';
					}
				});
		});
	}

	function load(cfg) {
		var el = typeof cfg.mount === 'string' ? qs(cfg.mount) : cfg.mount;
		if (!el) {
			return;
		}
		postJson(cfg.ajaxUrl, { action: 'order_fulfillment_status', order_id: cfg.orderId })
			.then(function (res) {
				if (!res || !res.status) {
					renderPanel(el, { fulfillment_status: 'none', purchase_orders: [], accounting: {} });
					bindPanel(el, cfg);
					return;
				}
				var st = res;
				if (res.sales_order || res.purchase_orders) {
					st = res;
				}
				renderPanel(el, st);
				bindPanel(el, cfg);
			})
			.catch(function () {
				renderPanel(el, { fulfillment_status: 'error', purchase_orders: [], accounting: {} });
				bindPanel(el, cfg);
			});
	}

	window.epcOrdersFulfillment = {
		load: load,
		render: renderPanel,
	};
})(window, document);
