(function () {
	var form = document.getElementById('epc-pos-settings-form');
	if (!form) return;
	var ajaxUrl = form.getAttribute('data-ajax-url') || '';
	if (!ajaxUrl) return;
	form.addEventListener('submit', function (e) {
		e.preventDefault();
		var fd = new FormData(form);
		fd.append('action', 'save_settings');
		if (!fd.get('pos_enabled')) fd.append('pos_enabled', '0');
		fetch(ajaxUrl, { method: 'POST', body: fd, credentials: 'same-origin' })
			.then(function (r) { return r.json(); })
			.then(function (j) {
				var msg = document.getElementById('epc-pos-settings-msg');
				if (msg) msg.textContent = j.status ? 'Saved' : (j.message || 'Error');
			})
			.catch(function () {
				var msg = document.getElementById('epc-pos-settings-msg');
				if (msg) msg.textContent = 'Save failed';
			});
	});
})();
