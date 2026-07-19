(function () {
	var root = document.getElementById('epc-vendor-upload');
	if (!root) return;
	var ajax = root.getAttribute('data-ajax') || '';
	var csrf = root.getAttribute('data-csrf') || '';
	var form = document.getElementById('epc-vp-upload-form');
	var fileInput = document.getElementById('epc-vp-file');
	var typeSel = document.getElementById('epc-vp-data-type');
	var submitBtn = document.getElementById('epc-vp-submit');
	var msg = document.getElementById('epc-vp-msg');
	var result = document.getElementById('epc-vp-result');

	function showMsg(text, ok) {
		if (!msg) return;
		msg.hidden = false;
		msg.className = 'epc-vp__alert ' + (ok ? 'epc-vp__alert--ok' : 'epc-vp__alert--err');
		msg.textContent = text;
	}

	function renderResult(data) {
		if (!result) return;
		if (!data) { result.hidden = true; return; }
		var html = '<p><strong>' + escapeHtml(data.message || '') + '</strong></p>';
		var vendors = data.vendors || [];
		if (vendors.length) {
			html += '<table><thead><tr><th>List</th><th>Type</th><th>Rows</th><th>Status</th></tr></thead><tbody>';
			vendors.forEach(function (v) {
				html += '<tr><td>' + escapeHtml(v.price_name || v.vendor_short || '') + '</td>'
					+ '<td>' + escapeHtml(v.data_type || '') + '</td>'
					+ '<td>' + escapeHtml(String(v.records_handled || 0)) + '</td>'
					+ '<td>' + escapeHtml(v.status ? 'ok' : (v.message || 'failed')) + '</td></tr>';
			});
			html += '</tbody></table>';
		}
		result.innerHTML = html;
		result.hidden = false;
	}

	function escapeHtml(s) {
		return String(s == null ? '' : s)
			.replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;').replace(/"/g, '&quot;');
	}

	if (!form) return;
	form.addEventListener('submit', function (e) {
		e.preventDefault();
		if (!fileInput || !fileInput.files || !fileInput.files[0]) {
			showMsg('Choose a CSV or Excel file first.', false);
			return;
		}
		var fd = new FormData();
		fd.append('price_file', fileInput.files[0]);
		fd.append('data_type', typeSel ? typeSel.value : 'inventory');
		if (csrf) fd.append('csrf_guard_key', csrf);
		if (submitBtn) {
			submitBtn.disabled = true;
			submitBtn.textContent = 'Uploading…';
		}
		showMsg('Uploading and importing…', true);
		fetch(ajax, { method: 'POST', body: fd, credentials: 'same-origin', cache: 'no-store' })
			.then(function (r) { return r.json(); })
			.then(function (data) {
				if (!data || !data.status) {
					showMsg((data && data.message) ? data.message : 'Upload failed.', false);
					renderResult(data);
					return;
				}
				showMsg(data.message || 'Upload complete.', true);
				renderResult(data);
				if (fileInput) fileInput.value = '';
			})
			.catch(function () {
				showMsg('Network error during upload.', false);
			})
			.finally(function () {
				if (submitBtn) {
					submitBtn.disabled = false;
					submitBtn.innerHTML = '<i class="fa fa-cloud-upload"></i> Upload &amp; publish';
				}
			});
	});
})();
