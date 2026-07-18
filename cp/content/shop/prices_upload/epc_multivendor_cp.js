(function () {
	'use strict';

	var cfg = window.EPC_MULTIVENDOR_CP || {};
	var ajaxUrl = cfg.ajaxUrl || '';
	var csrf = cfg.csrfKey || '';
	var pricesUrl = cfg.pricesUrl || '/cp/shop/prices';
	var storagesUrl = cfg.storagesUrl || '/cp/shop/logistics/storages';

	function $(id) {
		return document.getElementById(id);
	}

	function escapeHtml(str) {
		return String(str == null ? '' : str)
			.replace(/&/g, '&amp;')
			.replace(/</g, '&lt;')
			.replace(/>/g, '&gt;')
			.replace(/"/g, '&quot;');
	}

	function formatNum(n) {
		n = parseInt(n, 10) || 0;
		try {
			return n.toLocaleString();
		} catch (e) {
			return String(n);
		}
	}

	function setResult(html) {
		var box = $('epcMultivendorResult');
		if (box) {
			box.innerHTML = html;
		}
	}

	function renderVendors(vendors) {
		if (!vendors || !vendors.length) {
			return '';
		}
		var html = '<div class="epc-multivendor-result-lists"><table class="table table-condensed table-striped"><thead><tr>' +
			'<th>Short (customer)</th><th>Full (backend)</th><th>Status</th><th>Rows</th><th>List</th><th>Warehouse</th>' +
			'</tr></thead><tbody>';
		vendors.forEach(function (item) {
			var ok = !!item.status;
			html += '<tr class="' + (ok ? 'is-ok' : 'is-fail') + '">' +
				'<td><strong>' + escapeHtml(item.vendor_short || '') + '</strong></td>' +
				'<td>' + escapeHtml(item.vendor_full || '') + '</td>' +
				'<td>' + (ok ? '<span class="label label-success">OK</span>' : '<span class="label label-danger">FAIL</span>') + '</td>' +
				'<td>' + formatNum(item.records_handled || 0) + ' → DB ' + formatNum(item.records_in_db || 0) + '</td>' +
				'<td>#' + escapeHtml(item.price_id || '') + ' ' + escapeHtml(item.price_name || '') + '</td>' +
				'<td>#' + escapeHtml(item.storage_id || '') + '</td>' +
				'</tr>';
			if (item.message && !ok) {
				html += '<tr><td colspan="6"><small class="text-danger">' + escapeHtml(item.message) + '</small></td></tr>';
			}
		});
		html += '</tbody></table>' +
			'<p><a href="' + escapeHtml(pricesUrl) + '">Open price lists</a> · ' +
			'<a href="' + escapeHtml(storagesUrl) + '">Open warehouses</a></p></div>';
		return html;
	}

	function upload() {
		var fileInput = $('epcMultivendorFile');
		var btn = $('epcMultivendorUploadBtn');
		if (!fileInput || !fileInput.files || !fileInput.files.length) {
			setResult('<div class="alert alert-warning">Choose an Excel/CSV file first.</div>');
			return;
		}
		var fd = new FormData();
		fd.append('csrf_guard_key', csrf);
		fd.append('action', 'upload');
		fd.append('price_file', fileInput.files[0]);
		if (btn) {
			btn.disabled = true;
			btn.innerHTML = '<i class="fas fa-spinner fa-spin"></i> Importing vendors…';
		}
		setResult('<div class="alert alert-info">Uploading and creating warehouses. Large files with many vendors can take a few minutes…</div>');

		fetch(ajaxUrl, {
			method: 'POST',
			body: fd,
			credentials: 'same-origin'
		}).then(function (r) {
			return r.json();
		}).then(function (data) {
			if (!data || typeof data !== 'object') {
				setResult('<div class="alert alert-danger">Unexpected server response.</div>');
				return;
			}
			var type = data.status ? 'success' : 'danger';
			var summary = '<div class="alert alert-' + type + '"><strong>' + escapeHtml(data.message || '') + '</strong>' +
				'<br>Vendors OK: ' + formatNum(data.vendors_ok || 0) +
				' / ' + formatNum(data.vendors_total || 0) +
				' · Rows imported: ' + formatNum(data.rows_imported || 0) +
				' · Warehouses linked: ' + formatNum(data.warehouses_linked || 0) +
				'</div>';
			setResult(summary + renderVendors(data.vendors || []));
		}).catch(function (err) {
			setResult('<div class="alert alert-danger">Upload failed: ' + escapeHtml(err && err.message ? err.message : err) + '</div>');
		}).finally(function () {
			if (btn) {
				btn.disabled = false;
				btn.innerHTML = '<i class="fas fa-cloud-upload-alt"></i> Upload &amp; create warehouses';
			}
		});
	}

	function downloadSample() {
		var fd = new FormData();
		fd.append('csrf_guard_key', csrf);
		fd.append('action', 'sample');
		fetch(ajaxUrl, {
			method: 'POST',
			body: fd,
			credentials: 'same-origin'
		}).then(function (r) {
			return r.json();
		}).then(function (data) {
			if (!data || !data.status || !data.csv) {
				setResult('<div class="alert alert-danger">Could not build sample CSV.</div>');
				return;
			}
			var blob = new Blob([data.csv], { type: 'text/csv;charset=utf-8' });
			var a = document.createElement('a');
			a.href = URL.createObjectURL(blob);
			a.download = data.filename || 'epc-multivendor-sample.csv';
			document.body.appendChild(a);
			a.click();
			setTimeout(function () {
				URL.revokeObjectURL(a.href);
				a.remove();
			}, 500);
		}).catch(function (err) {
			setResult('<div class="alert alert-danger">Sample download failed: ' + escapeHtml(err && err.message ? err.message : err) + '</div>');
		});
	}

	function init() {
		var uploadBtn = $('epcMultivendorUploadBtn');
		var sampleBtn = $('epcMultivendorSampleBtn');
		if (uploadBtn) {
			uploadBtn.addEventListener('click', upload);
		}
		if (sampleBtn) {
			sampleBtn.addEventListener('click', downloadSample);
		}
	}

	if (document.readyState === 'loading') {
		document.addEventListener('DOMContentLoaded', init);
	} else {
		init();
	}
})();
