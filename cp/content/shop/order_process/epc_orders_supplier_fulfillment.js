/**
 * OMS per-supplier fulfillment pipeline (keyed by order #).
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

	function post(url, data) {
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

	function render(el, data, cfg) {
		if (!el) {
			return;
		}
		var suppliers = (data && data.suppliers) || [];
		var html = '<div class="epc-sf-panel">';
		html += '<div class="epc-sf-meta">Order <strong>#' + esc(cfg.orderId) + '</strong> · '
			+ esc(String(suppliers.length)) + ' supplier(s) · rollup: '
			+ esc((data && data.rollup) || 'none') + '</div>';
		if (!suppliers.length) {
			html += '<p class="text-muted">No supplier groups on this order yet. Assign warehouses on Items, then refresh.</p>';
			html += '<button type="button" class="btn btn-default btn-xs epc-sf-refresh">Refresh</button>';
			html += '</div>';
			el.innerHTML = html;
			bind(el, cfg);
			return;
		}
		suppliers.forEach(function (s) {
			html += '<article class="epc-sf-card" data-supplier-key="' + esc(s.supplier_key) + '">';
			html += '<div class="epc-sf-card__head">';
			html += '<h5><i class="fa fa-truck"></i> ' + esc(s.supplier_name || s.supplier_key)
				+ ' <span class="epc-scp-badge epc-scp-badge--normal">' + esc(s.stage_label || s.stage) + '</span></h5>';
			html += '<div class="epc-sf-meta">' + esc(String(s.lines || 0)) + ' line(s)'
				+ ' · sell ' + esc(Number(s.sell_sum || 0).toFixed(2))
				+ ' · buy ' + esc(Number(s.purchase_sum || 0).toFixed(2))
				+ ' · margin <strong class="' + ((Number(s.margin || 0) >= 0) ? 'is-ok' : 'is-bad') + '">'
				+ esc(Number(s.margin || 0).toFixed(2)) + '</strong></div>';
			html += '</div>';
			html += '<div class="epc-sf-pipe">';
			(s.pipeline || []).forEach(function (step) {
				var cls = 'epc-sf-step';
				if (step.current) {
					cls += ' is-current';
				} else if (step.done) {
					cls += ' is-done';
				}
				html += '<span class="' + cls + '" title="' + esc(step.label) + '">' + esc(step.label) + '</span>';
			});
			html += '</div>';
			html += '<div class="epc-sf-actions">';
			html += '<select class="form-control input-sm epc-sf-stage" style="max-width:220px;">';
			var stages = (data && data.stages) || {};
			Object.keys(stages).forEach(function (k) {
				html += '<option value="' + esc(k) + '"' + (k === s.stage ? ' selected' : '') + '>'
					+ esc(stages[k]) + '</option>';
			});
			html += '</select>';
			html += '<button type="button" class="btn btn-default btn-xs epc-sf-set">Set status</button> ';
			if (s.next_stage) {
				html += '<button type="button" class="btn btn-primary btn-xs epc-sf-advance">Advance → '
					+ esc(s.next_label || s.next_stage) + '</button>';
			} else {
				html += '<span class="text-success small"><i class="fa fa-check"></i> Complete</span>';
			}
			html += '</div>';
			html += '</article>';
		});
		html += '<button type="button" class="btn btn-default btn-xs epc-sf-refresh"><i class="fa fa-refresh"></i> Refresh</button>';
		html += '<div class="epc-sf-msg small text-muted" style="margin-top:6px;"></div>';
		html += '</div>';
		el.innerHTML = html;
		bind(el, cfg);
	}

	function bind(el, cfg) {
		el.onclick = function (ev) {
			var t = ev.target;
			if (!t || !t.classList) {
				return;
			}
			var msg = qs('.epc-sf-msg', el);
			if (t.classList.contains('epc-sf-refresh')) {
				ev.preventDefault();
				load(cfg);
				return;
			}
			var card = t.closest ? t.closest('.epc-sf-card') : null;
			if (!card) {
				return;
			}
			var key = card.getAttribute('data-supplier-key') || '';
			var payload = {
				order_id: cfg.orderId,
				supplier_key: key,
				csrf_guard_key: cfg.csrf || ''
			};
			if (t.classList.contains('epc-sf-advance')) {
				ev.preventDefault();
				payload.action = 'supplier_fulfillment_advance';
			} else if (t.classList.contains('epc-sf-set')) {
				ev.preventDefault();
				var sel = qs('.epc-sf-stage', card);
				payload.action = 'supplier_fulfillment_set_stage';
				payload.stage = sel ? sel.value : '';
			} else {
				return;
			}
			if (msg) {
				msg.textContent = 'Working…';
			}
			post(cfg.ajaxUrl, payload)
				.then(function (res) {
					if (!res || !res.status) {
						throw new Error((res && res.message) || 'Failed');
					}
					render(el, res.fulfillment || {}, cfg);
					if (msg) {
						msg.textContent = res.message || 'Updated';
					}
				})
				.catch(function (err) {
					if (msg) {
						msg.textContent = err.message || 'Error';
					}
				});
		};
	}

	function load(cfg) {
		var el = typeof cfg.mount === 'string' ? qs(cfg.mount) : cfg.mount;
		if (!el) {
			return;
		}
		el.innerHTML = '<div class="text-muted small">Loading supplier fulfillment…</div>';
		post(cfg.ajaxUrl, {
			action: 'supplier_fulfillment_status',
			order_id: cfg.orderId,
			csrf_guard_key: cfg.csrf || ''
		})
			.then(function (res) {
				if (!res || !res.status) {
					el.innerHTML = '<div class="text-danger small">' + esc((res && res.message) || 'Could not load fulfillment') + '</div>';
					return;
				}
				render(el, res.fulfillment || {}, cfg);
			})
			.catch(function (err) {
				el.innerHTML = '<div class="text-danger small">' + esc(err.message || 'Error') + '</div>';
			});
	}

	window.epcSupplierFulfillment = {
		load: load,
		render: render
	};
})(window, document);
