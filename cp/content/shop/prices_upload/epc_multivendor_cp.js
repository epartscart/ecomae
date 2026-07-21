(function () {
	'use strict';

	var DEFAULT_AJAX = '/cp/content/shop/prices_upload/ajax_epc_multivendor_ingest.php';
	var DEFAULT_SAMPLE = '/cp/content/shop/prices_upload/epc_multivendor_sample_file.php';

	function readRootData() {
		var root = document.getElementById('epcMultivendorRoot');
		if (!root || !root.getAttribute) {
			return null;
		}
		return {
			ajaxUrl: root.getAttribute('data-ajax-url') || '',
			sampleUrl: root.getAttribute('data-sample-url') || '',
			csrfKey: root.getAttribute('data-csrf-key') || '',
			backend: root.getAttribute('data-backend') || '',
			pricesUrl: root.getAttribute('data-prices-url') || '',
			storagesUrl: root.getAttribute('data-storages-url') || ''
		};
	}

	function readBootJson() {
		var el = document.getElementById('epc-multivendor-boot');
		if (!el || !el.textContent) {
			return null;
		}
		try {
			return JSON.parse(el.textContent);
		} catch (e) {
			return null;
		}
	}

	function ensureCfg() {
		var boot = readBootJson();
		var rootData = readRootData();
		var current = window.EPC_MULTIVENDOR_CP;
		if (!current || typeof current !== 'object') {
			current = {};
		}
		if (boot && typeof boot === 'object') {
			current = Object.assign({}, boot, current);
		}
		if (rootData && typeof rootData === 'object') {
			Object.keys(rootData).forEach(function (k) {
				if ((!current[k] || current[k] === '') && rootData[k]) {
					current[k] = rootData[k];
				}
			});
		}
		if (!current.ajaxUrl) {
			current.ajaxUrl = DEFAULT_AJAX;
		}
		if (!current.sampleUrl) {
			current.sampleUrl = DEFAULT_SAMPLE;
		}
		if (!current.backend) {
			current.backend = 'cp';
		}
		if (!current.pricesUrl) {
			current.pricesUrl = '/' + current.backend + '/shop/prices';
		}
		if (!current.storagesUrl) {
			current.storagesUrl = '/' + current.backend + '/shop/logistics/storages';
		}
		if (!current.csrfKey) {
			var csrfInput = document.querySelector('#epcMultivendorIngestForm input[name="csrf_guard_key"]');
			if (csrfInput && csrfInput.value) {
				current.csrfKey = csrfInput.value;
			}
		}
		window.EPC_MULTIVENDOR_CP = current;
		return current;
	}

	function cfg() {
		return ensureCfg();
	}

	function ajaxUrl() {
		var c = cfg();
		return c.ajaxUrl || DEFAULT_AJAX;
	}

	function csrfKey() {
		var c = cfg();
		if (c.csrfKey) {
			return c.csrfKey;
		}
		var csrfInput = document.querySelector('#epcMultivendorIngestForm input[name="csrf_guard_key"]');
		return csrfInput && csrfInput.value ? csrfInput.value : '';
	}

	function sampleUrl() {
		var c = cfg();
		if (c.sampleUrl) {
			return c.sampleUrl;
		}
		var base = ajaxUrl();
		if (!base) {
			return DEFAULT_SAMPLE;
		}
		return base.replace(/ajax_epc_multivendor_ingest\.php.*/i, 'epc_multivendor_sample_file.php');
	}

	function pricesUrl() {
		return cfg().pricesUrl || '/cp/shop/prices';
	}

	function storagesUrl() {
		return cfg().storagesUrl || '/cp/shop/logistics/storages';
	}

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

	function triggerDownload(url, filename) {
		var a = document.createElement('a');
		a.href = url;
		if (filename) {
			a.download = filename;
		}
		a.rel = 'noopener';
		a.style.display = 'none';
		document.body.appendChild(a);
		a.click();
		setTimeout(function () {
			a.remove();
		}, 500);
	}

	function renderVendors(vendors) {
		if (!vendors || !vendors.length) {
			return '';
		}
		var html = '<div class="epc-multivendor-result-lists"><table class="table table-condensed table-striped"><thead><tr>' +
			'<th>Short (customer)</th><th>Full (backend)</th><th>Type</th><th>Status</th><th>Rows</th><th>List</th><th>Warehouse</th>' +
			'</tr></thead><tbody>';
		vendors.forEach(function (item) {
			var ok = !!item.status;
			html += '<tr class="' + (ok ? 'is-ok' : 'is-fail') + '">' +
				'<td><strong>' + escapeHtml(item.vendor_short || '') + '</strong></td>' +
				'<td>' + escapeHtml(item.vendor_full || '') + '</td>' +
				'<td>' + escapeHtml(item.data_type || '') + '</td>' +
				'<td>' + (ok ? '<span class="label label-success">OK</span>' : '<span class="label label-danger">FAIL</span>') + '</td>' +
				'<td>' + formatNum(item.records_handled || 0) + ' → DB ' + formatNum(item.records_in_db || 0) + '</td>' +
				'<td>#' + escapeHtml(item.price_id || '') + ' ' + escapeHtml(item.price_name || '') + '</td>' +
				'<td>#' + escapeHtml(item.storage_id || '') + '</td>' +
				'</tr>';
			if (item.message && !ok) {
				html += '<tr><td colspan="7"><small class="text-danger">' + escapeHtml(item.message) + '</small></td></tr>';
			}
		});
		html += '</tbody></table>' +
			'<p><a href="' + escapeHtml(pricesUrl()) + '">Open price lists</a> · ' +
			'<a href="' + escapeHtml(storagesUrl()) + '">Open warehouses</a></p></div>';
		return html;
	}

	function upload() {
		ensureCfg();
		var fileInput = $('epcMultivendorFile');
		var btn = $('epcMultivendorUploadBtn');
		var url = ajaxUrl();
		if (!url) {
			setResult('<div class="alert alert-danger">Upload is not configured (reload the page after login).</div>');
			return;
		}
		if (!fileInput || !fileInput.files || !fileInput.files.length) {
			setResult('<div class="alert alert-warning">Choose an Excel/CSV file first.</div>');
			return;
		}
		var key = csrfKey();
		if (!key) {
			setResult('<div class="alert alert-danger">Session CSRF missing — reload the page after login, then try again.</div>');
			return;
		}
		var fd = new FormData();
		fd.append('csrf_guard_key', key);
		fd.append('action', 'upload');
		fd.append('price_file', fileInput.files[0]);
		var typeSel = $('epcMultivendorDataType');
		if (typeSel && typeSel.value) {
			fd.append('data_type', typeSel.value);
		}
		if (btn) {
			btn.disabled = true;
			btn.innerHTML = '<i class="fas fa-spinner fa-spin"></i> Importing vendors…';
		}
		setResult('<div class="alert alert-info">Uploading and creating warehouses. Large files with many vendors can take a few minutes…</div>');

		fetch(url, {
			method: 'POST',
			body: fd,
			credentials: 'same-origin',
			headers: { 'Accept': 'application/json' }
		}).then(function (r) {
			return r.text().then(function (text) {
				var ct = (r.headers.get('content-type') || '').toLowerCase();
				if (!text || text.charAt(0) === '<' || ct.indexOf('json') === -1) {
					throw new Error('Server returned HTML instead of JSON (HTTP ' + r.status + '). Reload CP and try again.');
				}
				try {
					return JSON.parse(text);
				} catch (e) {
					throw new Error('Invalid JSON from upload endpoint');
				}
			});
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

	function downloadSample(ev) {
		if (ev && ev.preventDefault) {
			ev.preventDefault();
		}
		ensureCfg();
		var direct = sampleUrl();
		if (direct) {
			triggerDownload(direct, 'epc-multivendor-sample.csv');
			return;
		}
		setResult('<div class="alert alert-danger">Sample download is not configured (reload the page).</div>');
	}

	function init() {
		ensureCfg();
		var uploadBtn = $('epcMultivendorUploadBtn');
		var sampleBtn = $('epcMultivendorSampleBtn');
		if (uploadBtn) {
			uploadBtn.addEventListener('click', upload);
		}
		if (sampleBtn) {
			if (!sampleBtn.getAttribute('href')) {
				sampleBtn.setAttribute('href', sampleUrl());
				sampleBtn.setAttribute('download', 'epc-multivendor-sample.csv');
			}
			sampleBtn.addEventListener('click', downloadSample);
		}
	}

	if (document.readyState === 'loading') {
		document.addEventListener('DOMContentLoaded', init);
	} else {
		init();
	}
})();
