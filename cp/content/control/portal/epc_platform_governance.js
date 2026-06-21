(function () {
	'use strict';
	var cfg = window.EPC_PG || {};
	if (!cfg.ajaxUrl) return;

	document.querySelectorAll('.epc-pg-tab').forEach(function (btn) {
		btn.addEventListener('click', function () {
			var tab = btn.getAttribute('data-tab');
			var rulesTab = document.getElementById('epc_pg_tab_rules');
			var protocolTab = document.getElementById('epc_pg_tab_protocol');
			if (rulesTab) rulesTab.style.display = tab === 'rules' ? '' : 'none';
			if (protocolTab) protocolTab.style.display = tab === 'protocol' ? '' : 'none';
			document.querySelectorAll('.epc-pg-tab').forEach(function (b) {
				b.classList.toggle('btn-primary', b === btn);
				b.classList.toggle('btn-default', b !== btn);
			});
		});
	});

	function saveRule(row) {
		var key = row.getAttribute('data-rule-key');
		var fd = new FormData();
		fd.append('action', 'save_rule');
		fd.append('rule_key', key);
		fd.append('active', row.querySelector('.epc-pg-active').checked ? '1' : '0');
		fd.append('enforcement', row.querySelector('.epc-pg-enforcement').value);
		fetch(cfg.ajaxUrl, { method: 'POST', body: fd, credentials: 'same-origin' })
			.then(function (r) { return r.json(); })
			.catch(function () {});
	}

	document.querySelectorAll('#epc_pg_tab_rules tbody tr').forEach(function (row) {
		row.querySelector('.epc-pg-active').addEventListener('change', function () { saveRule(row); });
		row.querySelector('.epc-pg-enforcement').addEventListener('change', function () { saveRule(row); });
	});

	var runBtn = document.getElementById('epc_pg_run_health');
	if (runBtn && cfg.healthApi) {
		runBtn.addEventListener('click', function () {
			var loading = document.getElementById('epc_pg_health_loading');
			var box = document.getElementById('epc_pg_health_results');
			if (loading) loading.style.display = 'inline';
			if (box) box.innerHTML = '';
			fetch(cfg.healthApi, { credentials: 'omit' })
				.then(function (r) { return r.json(); })
				.then(function (data) {
					if (!box) return;
					(data.surfaces || []).forEach(function (s) {
						var card = document.createElement('div');
						card.className = 'epc-pg__health-card ' + (s.ok ? 'ok' : 'fail');
						card.innerHTML = '<strong>' + (s.label || s.surface) + '</strong><br>HTTP ' + (s.http || '—') + ' · ' + (s.ms || '—') + 'ms' +
							(s.note ? '<br><small>' + s.note + '</small>' : '');
						box.appendChild(card);
					});
					var summary = document.createElement('div');
					summary.className = 'epc-pg__health-card';
					summary.innerHTML = '<strong>Overall</strong><br>' + (data.overall_ok ? 'PASS' : 'REVIEW') +
						' · ' + (data.failure_count || 0) + ' failures · ' + (data.rules_in_db || 0) + ' rules';
					box.insertBefore(summary, box.firstChild);
				})
				.catch(function (err) {
					if (box) box.innerHTML = '<div class="alert alert-danger">' + err + '</div>';
				})
				.finally(function () { if (loading) loading.style.display = 'none'; });
		});
	}
})();
