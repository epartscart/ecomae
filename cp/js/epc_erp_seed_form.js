/**
 * Jewellery Seed Data form — external JS (loaded via <head>).
 * Attaches AJAX submit handler to #jw_seed_form after DOMContentLoaded.
 */
(function () {
	'use strict';

	function init() {
		var form = document.getElementById('jw_seed_form');
		if (!form) return;

		var wrapper = form.closest('[data-seed-endpoint]') || document.querySelector('[data-seed-endpoint]');
		var endpoint = wrapper ? wrapper.getAttribute('data-seed-endpoint') : '';
		var dashUrl  = wrapper ? (wrapper.getAttribute('data-dashboard-url') || '') : '';

		form.addEventListener('submit', function (e) {
			e.preventDefault();
			var btn = form.querySelector('button[type="submit"]');
			if (!btn) return;
			btn.disabled = true;
			btn.innerHTML = '<i class="fa fa-spinner fa-spin"></i> Seeding...';

			var fd = new FormData(form);
			fd.append('action', 'jw_seed_sample_data');

			fetch(endpoint, { method: 'POST', body: fd, credentials: 'same-origin' })
				.then(function (r) { return r.json(); })
				.then(function (j) {
					var out = document.getElementById('jw_seed_result');
					if (out) out.style.display = 'block';
					btn.disabled = false;
					btn.innerHTML = '<i class="fa fa-magic"></i> Seed sample data now';
					if (j.status) {
						var d = j.seeded || {};
						var errHtml = '';
						if (d.errors && d.errors.length > 0) {
							errHtml = '<br><div style="margin-top:6px;color:#c62828;font-size:11px"><strong>Errors:</strong><ul style="margin:2px 0 0 16px">';
							d.errors.forEach(function (e) { errHtml += '<li>' + e + '</li>'; });
							errHtml += '</ul></div>';
						}
						if (out) {
							out.innerHTML = '<div style="background:#e8f5e9;border:1px solid #a5d6a7;padding:10px 14px;border-radius:3px;font-size:12px">'
								+ '<strong><i class="fa fa-check-circle" style="color:#2e7d32"></i> Sample data seeded successfully!</strong><br>'
								+ 'Warehouses: <strong>' + (d.warehouses || 0) + '</strong> | '
								+ 'Items: <strong>' + (d.items || 0) + '</strong> | '
								+ 'Suppliers: <strong>' + (d.suppliers || 0) + '</strong> | '
								+ 'Customers: <strong>' + (d.customers || 0) + '</strong> | '
								+ 'Purchases: <strong>' + (d.purchases || 0) + '</strong> | '
								+ 'Sales: <strong>' + (d.sales || 0) + '</strong> | '
								+ 'Repairs: <strong>' + (d.repairs || 0) + '</strong> | '
								+ 'GL: <strong>' + (d.gl_entries || 0) + '</strong> | '
								+ 'Compliance: <strong>' + (d.compliance || 0) + '</strong>'
								+ errHtml
								+ (dashUrl ? '<br><a href="' + dashUrl + '" style="color:#1565c0;font-weight:600">Go to Dashboard &rarr;</a>' : '')
								+ '</div>';
						}
					} else {
						if (out) {
							out.innerHTML = '<div style="background:#ffebee;border:1px solid #ef9a9a;padding:10px 14px;border-radius:3px;font-size:12px;color:#c62828">'
								+ '<strong><i class="fa fa-exclamation-circle"></i> Error:</strong> ' + (j.message || 'Failed to seed data')
								+ '</div>';
						}
					}
				})
				.catch(function (err) {
					btn.disabled = false;
					btn.innerHTML = '<i class="fa fa-magic"></i> Seed sample data now';
					var out = document.getElementById('jw_seed_result');
					if (out) {
						out.style.display = 'block';
						out.innerHTML = '<div style="background:#ffebee;border:1px solid #ef9a9a;padding:10px 14px;border-radius:3px;font-size:12px;color:#c62828">'
							+ '<strong>Network error:</strong> ' + err.message + '</div>';
					}
				});
		});
	}

	if (document.readyState === 'loading') {
		document.addEventListener('DOMContentLoaded', init);
	} else {
		init();
	}
})();
