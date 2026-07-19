/**
 * ERP order fulfillment panel — SO/PO/invoice/bill map + VAT/courier notes.
 */
(function (window, document) {
	'use strict';

	function qs(sel, root) {
		return (root || document).querySelector(sel);
	}

	function esc(s) {
		return String(s == null ? '' : s)
			.replace(/&/g, '&amp;')
			.replace(/</g, '&lt;')
			.replace(/>/g, '&gt;')
			.replace(/"/g, '&quot;');
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
		return '<span class="epc-scp-badge epc-scp-badge--' + (tone || 'normal') + '">' + esc(text) + '</span>';
	}

	function normalizeStatus(res) {
		if (!res) {
			return null;
		}
		if (res.fulfillment_status && (res.sales_order || res.purchase_orders || res.shop_order_id)) {
			return res;
		}
		if (res.status && typeof res.status === 'object' && (res.status.fulfillment_status || res.status.sales_order)) {
			return res.status;
		}
		if (res.sales_order || res.purchase_orders) {
			return res;
		}
		return res.status && typeof res.status === 'object' ? res.status : res;
	}

	function renderPanel(el, status) {
		if (!el || !status) {
			return;
		}
		var so = status.sales_order || null;
		var pos = status.purchase_orders || [];
		var bills = status.purchase_invoices || [];
		var inv = status.sales_invoice || null;
		var map = status.document_map || null;
		var vat = (map && map.vat) || status.vat || null;
		var courier = (map && map.courier) || status.courier || null;

		var html = '<div class="epc-of-panel">';
		html += '<h4 class="epc-of-panel__title"><i class="fa fa-random"></i> ERP fulfillment &amp; document map</h4>';
		html += '<div class="epc-of-panel__row">Status: ' + badge(status.fulfillment_status || 'none', status.fulfillment_status === 'fulfilled' ? 'tenant' : 'high') + '</div>';

		html += '<ol class="epc-of-panel__chain">';
		html += '<li><strong>Order</strong> #' + esc(status.shop_order_id || '') + '</li>';
		if (vat) {
			html += '<li><strong>VAT</strong> ' + esc(vat.label || vat.type || '') +
				(vat.zero_rated ? ' · zero-rated export' : ' · UAE standard') + '</li>';
		} else {
			html += '<li><strong>VAT</strong> — see OMS header (UAE vs outside UAE)</li>';
		}
		if (so) {
			html += '<li><strong>Sales order</strong> ' + esc(so.so_no || ('#' + so.id)) + ' ' + badge(so.status || 'open', 'normal') + '</li>';
		} else {
			html += '<li><strong>Sales order</strong> <span class="text-muted">not linked</span></li>';
		}
		html += '<li><strong>Supplier POs</strong> ' + esc(String(pos.length)) + '</li>';
		html += '<li><strong>AP bills</strong> ' + esc(String(bills.length)) + '</li>';
		if (inv) {
			html += '<li><strong>AR tax invoice</strong> ' + esc(inv.invoice_number || ('#' + inv.id)) +
				' ' + badge(inv.validation_ok ? 'validated' : 'draft', inv.validation_ok ? 'tenant' : 'high') + '</li>';
		} else {
			html += '<li><strong>AR tax invoice</strong> <span class="text-muted">not posted</span></li>';
		}
		if (courier && Number(courier.line_net || 0) > 0) {
			html += '<li><strong>Courier (customer pays)</strong> ' +
				esc(Number(courier.line_net).toFixed(2)) + ' + VAT ' +
				esc(Number(courier.vat_amount || 0).toFixed(2)) + ' = ' +
				esc(Number(courier.gross || 0).toFixed(2)) + ' AED' +
				(Number(courier.vat_amount || 0) <= 0 ? ' (no VAT outside UAE)' : ' (VAT = income)') +
				'</li>';
		} else {
			html += '<li><strong>Courier</strong> <span class="text-muted">none set</span></li>';
		}
		html += '</ol>';

		if (pos.length) {
			html += '<ul class="epc-of-panel__po-list">';
			pos.forEach(function (po) {
				html += '<li>PO ' + esc(po.po_no || po.id) + ' — ' + esc(po.supplier_name || 'Supplier') +
					' ' + badge(po.status || 'draft', po.status === 'received' ? 'tenant' : 'normal') + '</li>';
			});
			html += '</ul>';
		}
		if (bills.length) {
			html += '<ul class="epc-of-panel__po-list">';
			bills.forEach(function (b) {
				html += '<li>Bill ' + esc(b.invoice_number || b.id) +
					' · ' + esc(Number(b.total_amount || 0).toFixed(2)) + ' AED ' +
					badge(b.status || 'open', 'normal') + '</li>';
			});
			html += '</ul>';
		}
		if (vat && vat.documentation) {
			html += '<div class="epc-of-panel__docs small"><i class="fa fa-file-text-o"></i> ' + esc(vat.documentation) + '</div>';
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
					var st = normalizeStatus(res);
					if (!st || (!st.fulfillment_status && !st.sales_order && !st.shop_order_id)) {
						return postJson(cfg.ajaxUrl, { action: 'order_fulfillment_status', order_id: cfg.orderId })
							.then(function (r2) {
								return normalizeStatus(r2) || r2;
							});
					}
					return st;
				})
				.then(function (st) {
					renderPanel(el, st || { fulfillment_status: 'none', purchase_orders: [], accounting: {} });
					if (msg) {
						msg.textContent = 'Done';
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
				var st = normalizeStatus(res) || { fulfillment_status: 'none', purchase_orders: [], accounting: {} };
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
