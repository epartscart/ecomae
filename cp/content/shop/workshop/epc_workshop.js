/**
 * Garage desk JS — window.EPC_WORKSHOP = { ajaxUrl, csrf, statuses, money }
 */
(function () {
	'use strict';

	function cfg() {
		return window.EPC_WORKSHOP || {};
	}

	function money(n) {
		var v = Number(n || 0);
		return (cfg().currency || 'AED') + ' ' + v.toFixed(2);
	}

	function showMsg(text, ok) {
		var el = document.getElementById('epc-ws-msg');
		if (!el) return;
		el.className = 'alert epc-ws-msg is-show ' + (ok ? 'alert-success' : 'alert-danger');
		el.textContent = text || '';
		if (text) {
			setTimeout(function () { el.classList.remove('is-show'); }, 4000);
		}
	}

	function post(action, data, cb) {
		var c = cfg();
		var body = Object.assign({ action: action, csrf_guard_key: c.csrf || '' }, data || {});
		if (!window.jQuery) {
			showMsg('jQuery required', false);
			return;
		}
		jQuery.ajax({
			type: 'POST',
			url: c.ajaxUrl,
			data: body,
			dataType: 'json',
			success: function (res) {
				if (!res || !res.status) {
					showMsg((res && res.message) || 'Request failed', false);
					if (cb) cb(null, res);
					return;
				}
				if (res.message) showMsg(res.message, true);
				if (cb) cb(res);
			},
			error: function () {
				showMsg('Network error', false);
				if (cb) cb(null);
			}
		});
	}

	function openDetail(jobId) {
		post('get_job', { job_id: jobId }, function (res) {
			if (!res || !res.job) return;
			var h = res.job.header;
			var lines = res.job.lines || [];
			var box = document.getElementById('epc-ws-detail');
			var body = document.getElementById('epc-ws-detail-body');
			if (!box || !body) return;
			var statuses = cfg().statuses || {};
			var statusOpts = Object.keys(statuses).map(function (k) {
				return '<option value="' + k + '"' + (h.status === k ? ' selected' : '') + '>' + statuses[k] + '</option>';
			}).join('');
			var bayOpts = (cfg().bays || []).map(function (b) {
				return '<option value="' + b.id + '"' + (String(h.bay_id) === String(b.id) ? ' selected' : '') + '>' + b.code + ' — ' + b.name + '</option>';
			}).join('');
			var techOpts = (cfg().techs || []).map(function (t) {
				return '<option value="' + t.id + '"' + (String(h.tech_id) === String(t.id) ? ' selected' : '') + '>' + t.name + '</option>';
			}).join('');
			var lineHtml = lines.map(function (ln) {
				return '<tr><td>' + (ln.line_type || '') + '</td><td>' + (ln.description || '') + '</td><td>' + ln.qty + '</td><td>' + money(ln.unit_price) + '</td></tr>';
			}).join('') || '<tr><td colspan="4">No lines yet</td></tr>';

			body.innerHTML =
				'<div class="epc-ws-detail__head">' +
				'<div><strong>' + (h.job_no || '') + '</strong><div class="text-muted" style="font-size:12px">' +
				(h.plate || '') + ' · ' + (h.make || '') + ' ' + (h.model || '') + ' · ' + (h.customer_name || '') +
				'</div></div>' +
				'<button type="button" class="btn btn-default btn-sm" id="epc-ws-detail-close">Close</button></div>' +
				'<p style="font-size:13px;margin:0 0 10px"><strong>Complaint:</strong> ' + (h.complaint || '—') + '</p>' +
				'<div class="row">' +
				'<div class="col-sm-4"><label>Status</label><select class="form-control input-sm" id="epc-ws-status">' + statusOpts + '</select></div>' +
				'<div class="col-sm-4"><label>Bay</label><select class="form-control input-sm" id="epc-ws-bay"><option value="0">—</option>' + bayOpts + '</select></div>' +
				'<div class="col-sm-4"><label>Technician</label><select class="form-control input-sm" id="epc-ws-tech"><option value="0">—</option>' + techOpts + '</select></div>' +
				'</div>' +
				'<div style="margin:10px 0">' +
				'<button type="button" class="btn btn-primary btn-sm" id="epc-ws-save-meta">Save status / assignment</button> ' +
				'<strong style="margin-left:10px">' + money(h.grand_total) + '</strong>' +
				'</div>' +
				'<table class="table table-condensed epc-ws-table"><thead><tr><th>Type</th><th>Description</th><th>Qty</th><th>Price</th></tr></thead><tbody>' + lineHtml + '</tbody></table>' +
				'<div class="row" style="margin-top:10px">' +
				'<div class="col-sm-5"><input class="form-control input-sm" id="epc-ws-line-desc" placeholder="Add part or labour description"></div>' +
				'<div class="col-sm-2"><select class="form-control input-sm" id="epc-ws-line-type"><option value="part">Part</option><option value="labour">Labour</option></select></div>' +
				'<div class="col-sm-2"><input class="form-control input-sm" id="epc-ws-line-qty" value="1" placeholder="Qty/hrs"></div>' +
				'<div class="col-sm-2"><input class="form-control input-sm" id="epc-ws-line-price" value="0" placeholder="Price"></div>' +
				'<div class="col-sm-1"><button type="button" class="btn btn-default btn-sm btn-block" id="epc-ws-add-line">+</button></div>' +
				'</div>';

			box.classList.add('is-open');
			box.setAttribute('data-job-id', String(jobId));

			document.getElementById('epc-ws-detail-close').onclick = function () {
				box.classList.remove('is-open');
			};
			document.getElementById('epc-ws-save-meta').onclick = function () {
				post('set_status', { job_id: jobId, status: document.getElementById('epc-ws-status').value }, function () {
					post('assign', {
						job_id: jobId,
						bay_id: document.getElementById('epc-ws-bay').value,
						tech_id: document.getElementById('epc-ws-tech').value
					}, function () {
						setTimeout(function () { location.reload(); }, 500);
					});
				});
			};
			document.getElementById('epc-ws-add-line').onclick = function () {
				post('add_line', {
					job_id: jobId,
					line_type: document.getElementById('epc-ws-line-type').value,
					description: document.getElementById('epc-ws-line-desc').value,
					qty: document.getElementById('epc-ws-line-qty').value,
					unit_price: document.getElementById('epc-ws-line-price').value
				}, function () {
					openDetail(jobId);
					setTimeout(function () { location.reload(); }, 800);
				});
			};
		});
	}

	function bind() {
		var root = document.getElementById('epc-ws-root');
		if (!root) return;

		root.addEventListener('click', function (ev) {
			var t = ev.target.closest('[data-job-id]');
			if (t && t.getAttribute('data-open-job') === '1') {
				ev.preventDefault();
				openDetail(parseInt(t.getAttribute('data-job-id'), 10));
			}
		});

		var seedBtn = document.getElementById('epc-ws-seed');
		if (seedBtn) {
			seedBtn.addEventListener('click', function () {
				post('seed_demo', {}, function (res) {
					if (res) setTimeout(function () { location.reload(); }, 600);
				});
			});
		}

		var form = document.getElementById('epc-ws-checkin-form');
		if (form) {
			form.addEventListener('submit', function (ev) {
				ev.preventDefault();
				var data = {};
				var fd = new FormData(form);
				fd.forEach(function (v, k) { data[k] = v; });
				post('create_job', data, function (res) {
					if (res) setTimeout(function () { location.href = (cfg().boardUrl || location.pathname); }, 700);
				});
			});
		}

		var detail = document.getElementById('epc-ws-detail');
		if (detail) {
			detail.addEventListener('click', function (ev) {
				if (ev.target === detail) detail.classList.remove('is-open');
			});
		}
	}

	if (document.readyState === 'loading') {
		document.addEventListener('DOMContentLoaded', bind);
	} else {
		bind();
	}

	window.epcWsOpenJob = openDetail;
})();
