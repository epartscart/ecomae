/**
 * CP Bulk Upload hub — review inbox, process for customer, cart / quote / ERP.
 */
(function () {
	'use strict';

	function cfg() {
		return window.EPC_BU || {};
	}

	function endpoint() {
		return cfg().ajaxUrl || '';
	}

	function msg(ok, text) {
		var el = document.getElementById('epc_bu_msg');
		if (!el) return;
		el.className = 'alert alert-' + (ok ? 'success' : 'danger') + ' epc-bu-msg';
		el.textContent = text || '';
		el.style.display = text ? 'block' : 'none';
	}

	function post(action, extra, file) {
		var fd = new FormData();
		fd.append('action', action);
		var c = cfg();
		if (c.csrf) fd.append('csrf_guard_key', c.csrf);
		if (extra) {
			Object.keys(extra).forEach(function (k) {
				if (extra[k] !== undefined && extra[k] !== null) fd.append(k, extra[k]);
			});
		}
		if (file) fd.append('bulk_file', file);
		var url = endpoint();
		if (!url) return Promise.reject(new Error('Missing AJAX URL'));
		return fetch(url, {
			method: 'POST',
			body: fd,
			credentials: 'same-origin',
			headers: { Accept: 'application/json', 'X-Requested-With': 'XMLHttpRequest' }
		}).then(function (r) {
			return r.text().then(function (t) {
				var j = null;
				try { j = t ? JSON.parse(t) : null; } catch (e) { j = null; }
				if (!j) throw new Error('Bad response (' + r.status + ')');
				return j;
			});
		});
	}

	var state = {
		customerId: 0,
		customerLabel: '',
		groupId: 0,
		uploadId: 0,
		rows: []
	};

	function esc(s) {
		return String(s == null ? '' : s)
			.replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;')
			.replace(/"/g, '&quot;');
	}

	function money(n) {
		var x = Number(n || 0);
		return x.toFixed(2);
	}

	function setTab(name) {
		document.querySelectorAll('.epc-bu-tab').forEach(function (b) {
			b.classList.toggle('is-active', b.getAttribute('data-tab') === name);
		});
		document.querySelectorAll('.epc-bu-panel').forEach(function (p) {
			p.classList.toggle('is-on', p.getAttribute('data-panel') === name);
		});
	}

	function renderInbox(rows) {
		var body = document.getElementById('epc_bu_inbox_body');
		if (!body) return;
		if (!rows || !rows.length) {
			body.innerHTML = '<div class="epc-bu-empty">No bulk uploads yet. Customers use /en/shop/bulk-upload, or process a file here.</div>';
			return;
		}
		var html = '<table class="epc-bu-table"><thead><tr>' +
			'<th>ID</th><th>Customer</th><th>File</th><th>Source</th><th>Stats</th><th>Reviewed</th><th></th>' +
			'</tr></thead><tbody>';
		rows.forEach(function (r) {
			var reviewed = r.cp_reviewed_at
				? '<span class="epc-bu-badge epc-bu-badge--ok">Reviewed</span>'
				: '<span class="epc-bu-badge epc-bu-badge--warn">Needs review</span>';
			var src = (r.source || 'storefront') === 'cp'
				? '<span class="epc-bu-badge epc-bu-badge--blue">CP</span>'
				: '<span class="epc-bu-badge">Storefront</span>';
			html += '<tr>' +
				'<td>#' + esc(r.id) + '</td>' +
				'<td>' + esc(r.customer_label || ('#' + r.user_id)) + '</td>' +
				'<td><strong>' + esc(r.file_name) + '</strong><div class="text-muted" style="font-size:11px">' + esc(r.created_at) + '</div></td>' +
				'<td>' + src + '</td>' +
				'<td>' + esc(r.available_count) + '/' + esc(r.uploaded_count) + ' avail · ' + esc(r.notfound_count) + ' miss</td>' +
				'<td>' + reviewed +
					(r.shop_quote_id > 0 ? '<div><span class="epc-bu-badge epc-bu-badge--ok">Q#' + esc(r.shop_quote_id) + '</span></div>' : '') +
					(r.crm_quote_id > 0 ? '<div><span class="epc-bu-badge epc-bu-badge--blue">ERP#' + esc(r.crm_quote_id) + '</span></div>' : '') +
					(r.cart_added_count > 0 ? '<div><span class="epc-bu-badge">Cart +' + esc(r.cart_added_count) + '</span></div>' : '') +
				'</td>' +
				'<td><button type="button" class="btn btn-xs btn-primary" data-open-upload="' + esc(r.id) + '">Open</button></td>' +
				'</tr>';
		});
		html += '</tbody></table>';
		body.innerHTML = html;
	}

	function selectedIndexes() {
		var out = [];
		document.querySelectorAll('#epc_bu_detail_body input[data-row-idx]:checked').forEach(function (cb) {
			out.push(parseInt(cb.getAttribute('data-row-idx'), 10));
		});
		return out;
	}

	function renderDetail(upload) {
		state.uploadId = parseInt(upload.id, 10) || 0;
		state.rows = upload.rows || [];
		if (!state.customerId && upload.user_id) {
			state.customerId = parseInt(upload.user_id, 10) || 0;
			state.customerLabel = upload.customer_label || ('#' + state.customerId);
			var sel = document.getElementById('epc_bu_selected_customer');
			if (sel) sel.textContent = 'Customer: ' + state.customerLabel;
		}
		var meta = document.getElementById('epc_bu_detail_meta');
		if (meta) {
			meta.innerHTML =
				'<span class="epc-bu-badge epc-bu-badge--blue">Upload #' + esc(upload.id) + '</span>' +
				'<span class="epc-bu-badge">' + esc(upload.customer_label || '') + '</span>' +
				'<span class="epc-bu-badge">' + esc(upload.file_name || '') + '</span>' +
				'<span class="epc-bu-badge">' + esc(upload.available_count) + '/' + esc(upload.uploaded_count) + ' available</span>';
		}
		var body = document.getElementById('epc_bu_detail_body');
		if (!body) return;
		if (!state.rows.length) {
			body.innerHTML = '<div class="epc-bu-empty">No result rows in this upload.</div>';
			return;
		}
		var html = '<table class="epc-bu-table"><thead><tr>' +
			'<th><input type="checkbox" id="epc_bu_check_all" checked></th>' +
			'<th>Requested</th><th>Match</th><th>Price</th><th>Stock</th><th>Status</th>' +
			'</tr></thead><tbody>';
		state.rows.forEach(function (row, idx) {
			var opt = null;
			if (row.exact && row.exact.selected) opt = row.exact;
			else if (row.cross && row.cross.selected) opt = row.cross;
			else if (row.exact) opt = row.exact;
			else if (row.cross) opt = row.cross;
			var can = !!(opt && opt.product_object);
			var req = (row.input && row.input.brand ? row.input.brand + ' ' : '') + (row.input ? row.input.article : '');
			var qty = row.input ? row.input.qty : 1;
			html += '<tr class="' + (can ? '' : 'text-muted') + '">' +
				'<td><input type="checkbox" data-row-idx="' + idx + '"' + (can ? ' checked' : ' disabled') + '></td>' +
				'<td><strong>' + esc(req) + '</strong> ×' + esc(qty) + '</td>' +
				'<td>' + (opt ? (esc(opt.manufacturer) + ' ' + esc(opt.article_show || opt.article) +
					' <span class="epc-bu-badge">' + esc(opt.match_label || opt.match_type || '') + '</span>') : '—') + '</td>' +
				'<td>' + (opt ? money(opt.price) : '—') + '</td>' +
				'<td>' + (opt ? esc(opt.exist) : '—') + '</td>' +
				'<td>' + esc(row.status_label || '') + '</td>' +
				'</tr>';
		});
		html += '</tbody></table>';
		body.innerHTML = html;
		setTab('detail');
	}

	function loadInbox() {
		var unreviewed = document.getElementById('epc_bu_filter_unreviewed');
		var source = document.getElementById('epc_bu_filter_source');
		return post('list_history', {
			unreviewed: unreviewed && unreviewed.checked ? '1' : '',
			source: source ? source.value : '',
			q: (document.getElementById('epc_bu_filter_q') || {}).value || ''
		}).then(function (j) {
			if (!j.status) throw new Error(j.message || 'List failed');
			renderInbox(j.rows || []);
		}).catch(function (err) {
			msg(false, err.message || 'Could not load inbox');
		});
	}

	function openUpload(id) {
		post('get_upload', { upload_id: id }).then(function (j) {
			if (!j.status) throw new Error(j.message || 'Load failed');
			renderDetail(j.upload);
			msg(true, 'Opened upload #' + id);
		}).catch(function (err) {
			msg(false, err.message || 'Open failed');
		});
	}

	function searchCustomers() {
		var q = (document.getElementById('epc_bu_customer_q') || {}).value || '';
		var box = document.getElementById('epc_bu_customer_pick');
		if (!box) return;
		post('search_customers', { q: q }).then(function (j) {
			if (!j.status) throw new Error(j.message || 'Search failed');
			var list = j.customers || [];
			if (!list.length) {
				box.style.display = 'block';
				box.innerHTML = '<button type="button" disabled>No customers found</button>';
				return;
			}
			box.style.display = 'block';
			box.innerHTML = list.map(function (c) {
				return '<button type="button" data-pick-customer="' + esc(c.user_id) + '" data-label="' + esc(c.label || c.email) + '" data-group="' + esc(c.group_id || 0) + '">' +
					esc(c.label || c.email) + '</button>';
			}).join('');
		}).catch(function (err) {
			msg(false, err.message || 'Customer search failed');
		});
	}

	function runAction(action) {
		if (!state.uploadId) {
			msg(false, 'Open an upload first');
			return;
		}
		var indexes = selectedIndexes();
		if (!indexes.length) {
			msg(false, 'Select at least one available line');
			return;
		}
		var payload = {
			upload_id: state.uploadId,
			indexes: JSON.stringify(indexes),
			customer_user_id: state.customerId || ''
		};
		post(action, payload).then(function (j) {
			msg(!!j.status, j.message || '');
			if (j.status) {
				loadInbox();
				if (j.quote_url) {
					setTimeout(function () { window.open(j.quote_url, '_blank'); }, 400);
				} else if (j.crm_url) {
					setTimeout(function () { window.open(j.crm_url, '_blank'); }, 400);
				}
			}
		}).catch(function (err) {
			msg(false, err.message || 'Action failed');
		});
	}

	function bind() {
		var root = document.querySelector('.epc-bu');
		if (!root || root.getAttribute('data-bound') === '1') return;
		root.setAttribute('data-bound', '1');

		document.querySelectorAll('.epc-bu-tab').forEach(function (btn) {
			btn.addEventListener('click', function () {
				setTab(btn.getAttribute('data-tab') || 'inbox');
			});
		});

		document.getElementById('epc_bu_refresh_inbox') && document.getElementById('epc_bu_refresh_inbox').addEventListener('click', loadInbox);
		['epc_bu_filter_unreviewed', 'epc_bu_filter_source'].forEach(function (id) {
			var el = document.getElementById(id);
			if (el) el.addEventListener('change', loadInbox);
		});
		var fq = document.getElementById('epc_bu_filter_q');
		if (fq) {
			fq.addEventListener('keydown', function (e) {
				if (e.key === 'Enter') { e.preventDefault(); loadInbox(); }
			});
		}

		document.addEventListener('click', function (e) {
			var openBtn = e.target.closest('[data-open-upload]');
			if (openBtn) {
				openUpload(openBtn.getAttribute('data-open-upload'));
				return;
			}
			var pick = e.target.closest('[data-pick-customer]');
			if (pick) {
				state.customerId = parseInt(pick.getAttribute('data-pick-customer'), 10) || 0;
				state.customerLabel = pick.getAttribute('data-label') || '';
				state.groupId = parseInt(pick.getAttribute('data-group'), 10) || 0;
				var sel = document.getElementById('epc_bu_selected_customer');
				if (sel) sel.textContent = 'Selected: ' + state.customerLabel + (state.groupId ? ' (group ' + state.groupId + ')' : '');
				var box = document.getElementById('epc_bu_customer_pick');
				if (box) box.style.display = 'none';
				var groupSel = document.getElementById('epc_bu_group_id');
				if (groupSel && state.groupId) groupSel.value = String(state.groupId);
			}
		});

		document.getElementById('epc_bu_customer_search') && document.getElementById('epc_bu_customer_search').addEventListener('click', searchCustomers);
		var cq = document.getElementById('epc_bu_customer_q');
		if (cq) {
			cq.addEventListener('keydown', function (e) {
				if (e.key === 'Enter') { e.preventDefault(); searchCustomers(); }
			});
		}

		document.getElementById('epc_bu_process') && document.getElementById('epc_bu_process').addEventListener('click', function () {
			if (!state.customerId) { msg(false, 'Select a customer first'); return; }
			var fileInput = document.getElementById('epc_bu_file');
			if (!fileInput || !fileInput.files || !fileInput.files[0]) { msg(false, 'Choose an Excel/CSV file'); return; }
			var groupId = (document.getElementById('epc_bu_group_id') || {}).value || state.groupId || '';
			var priority = (document.getElementById('epc_bu_priority') || {}).value || 'price';
			var btn = document.getElementById('epc_bu_process');
			btn.disabled = true;
			post('process_upload', {
				customer_user_id: state.customerId,
				group_id: groupId,
				priority: priority
			}, fileInput.files[0]).then(function (j) {
				btn.disabled = false;
				if (!j.status) throw new Error(j.message || 'Process failed');
				msg(true, 'Processed upload #' + j.upload_id + ' for ' + (j.customer_label || ''));
				state.uploadId = j.upload_id;
				state.rows = j.rows || [];
				renderDetail({
					id: j.upload_id,
					user_id: state.customerId,
					customer_label: j.customer_label || state.customerLabel,
					file_name: fileInput.files[0].name,
					available_count: j.summary ? j.summary.available : 0,
					uploaded_count: j.summary ? j.summary.uploaded : 0,
					rows: j.rows || []
				});
				loadInbox();
			}).catch(function (err) {
				btn.disabled = false;
				msg(false, err.message || 'Process failed');
			});
		});

		document.addEventListener('change', function (e) {
			if (e.target && e.target.id === 'epc_bu_check_all') {
				var on = e.target.checked;
				document.querySelectorAll('#epc_bu_detail_body input[data-row-idx]:not(:disabled)').forEach(function (cb) {
					cb.checked = on;
				});
			}
		});

		document.getElementById('epc_bu_add_cart') && document.getElementById('epc_bu_add_cart').addEventListener('click', function () {
			runAction('add_to_cart');
		});
		document.getElementById('epc_bu_shop_quote') && document.getElementById('epc_bu_shop_quote').addEventListener('click', function () {
			runAction('create_shop_quote');
		});
		document.getElementById('epc_bu_crm_quote') && document.getElementById('epc_bu_crm_quote').addEventListener('click', function () {
			runAction('create_crm_quote');
		});
		document.getElementById('epc_bu_mark_reviewed') && document.getElementById('epc_bu_mark_reviewed').addEventListener('click', function () {
			if (!state.uploadId) return;
			post('mark_reviewed', { upload_id: state.uploadId, notes: 'Reviewed in CP hub' }).then(function (j) {
				msg(!!j.status, j.message || '');
				if (j.status) loadInbox();
			}).catch(function (err) { msg(false, err.message || 'Failed'); });
		});

		loadInbox();
	}

	if (document.readyState === 'loading') {
		document.addEventListener('DOMContentLoaded', bind);
	} else {
		bind();
	}
})();
