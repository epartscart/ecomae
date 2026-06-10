(function () {
	'use strict';
	function flash(el, msg, ok) {
		if (!el) return;
		el.style.display = 'block';
		el.className = 'alert alert-' + (ok ? 'success' : 'danger');
		el.textContent = msg;
	}
	function bindAjaxForm(formId, flashId) {
		var form = document.getElementById(formId);
		var flashEl = flashId ? document.getElementById(flashId) : null;
		if (!form) return;
		form.addEventListener('submit', function (e) {
			e.preventDefault();
			var url = form.getAttribute('data-ajax-url');
			if (!url) return;
			var fd = new FormData(form);
			fetch(url, { method: 'POST', body: fd, credentials: 'same-origin' })
				.then(function (r) { return r.json(); })
				.then(function (data) {
					flash(flashEl, data.message || (data.status ? 'Saved' : 'Error'), !!data.status);
				})
				.catch(function () {
					flash(flashEl, 'Request failed', false);
				});
		});
	}
	bindAjaxForm('epc-mobile-form', 'epc-mobile-flash');
	bindAjaxForm('epc-tf-save', 'epc-tf-flash');
	bindAjaxForm('epc-tenant-smtp-form', 'epc-smtp-flash');
	bindAjaxForm('epc-tenant-smtp-test', 'epc-smtp-flash');
})();
