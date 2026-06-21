(function () {
	'use strict';

	var cfg = window.EPC_DOCUMENT_CONTROL || {};
	var ajaxUrl = cfg.ajaxUrl || '';
	var csrf = cfg.csrf || '';
	if (!ajaxUrl) {
		return;
	}

	var msg = document.getElementById('epc_dc_msg');

	function showMsg(ok, text) {
		if (!msg) {
			return;
		}
		msg.style.display = 'block';
		msg.className = 'alert epc-dc-msg ' + (ok ? 'alert-success' : 'alert-danger');
		msg.textContent = text;
	}

	function postForm(form, extra) {
		var fd = new FormData(form);
		if (extra) {
			Object.keys(extra).forEach(function (k) {
				fd.append(k, extra[k]);
			});
		}
		return fetch(ajaxUrl, { method: 'POST', body: fd, credentials: 'same-origin' }).then(function (r) {
			return r.json();
		});
	}

	var cf = document.getElementById('epc_dc_company_form');
	if (cf) {
		cf.addEventListener('submit', function (e) {
			e.preventDefault();
			postForm(cf).then(function (j) {
				showMsg(j.status, j.message);
			});
		});
	}

	var tf = document.getElementById('epc_dc_tpl_form');
	if (tf) {
		tf.addEventListener('submit', function (e) {
			e.preventDefault();
			postForm(tf).then(function (j) {
				showMsg(j.status, j.message);
			});
		});
	}

	var af = document.getElementById('epc_dc_att_form');
	if (af) {
		af.addEventListener('submit', function (e) {
			e.preventDefault();
			postForm(af).then(function (j) {
				showMsg(j.status, j.message);
				if (j.status) {
					setTimeout(function () {
						location.reload();
					}, 800);
				}
			});
		});
	}

	var syncBtn = document.getElementById('epc_dc_sync_seller');
	if (syncBtn) {
		syncBtn.addEventListener('click', function () {
			var fd = new FormData();
			fd.append('action', 'sync_einvoice_seller');
			fd.append('csrf_guard_key', csrf);
			fetch(ajaxUrl, { method: 'POST', body: fd, credentials: 'same-origin' }).then(function (r) {
				return r.json();
			}).then(function (j) {
				showMsg(j.status, j.message);
				if (j.status) {
					setTimeout(function () {
						location.reload();
					}, 600);
				}
			});
		});
	}

	var logoBtn = document.getElementById('epc_dc_upload_logo');
	if (logoBtn) {
		logoBtn.addEventListener('click', function () {
			var fi = document.getElementById('epc_dc_logo_file');
			if (!fi || !fi.files.length) {
				showMsg(false, 'Select a logo file');
				return;
			}
			var fd = new FormData();
			fd.append('action', 'upload_logo');
			fd.append('csrf_guard_key', csrf);
			fd.append('logo', fi.files[0]);
			fetch(ajaxUrl, { method: 'POST', body: fd, credentials: 'same-origin' }).then(function (r) {
				return r.json();
			}).then(function (j) {
				showMsg(j.status, j.message);
				if (j.status) {
					setTimeout(function () {
						location.reload();
					}, 600);
				}
			});
		});
	}

	document.querySelectorAll('.epc-dc-del-att').forEach(function (btn) {
		btn.addEventListener('click', function () {
			if (!confirm('Delete this attachment?')) {
				return;
			}
			var fd = new FormData();
			fd.append('action', 'delete_attachment');
			fd.append('csrf_guard_key', csrf);
			fd.append('id', btn.getAttribute('data-id'));
			fetch(ajaxUrl, { method: 'POST', body: fd, credentials: 'same-origin' }).then(function (r) {
				return r.json();
			}).then(function (j) {
				showMsg(j.status, j.message);
				if (j.status) {
					btn.closest('tr').remove();
				}
			});
		});
	});
})();
