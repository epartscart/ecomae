/**
 * CP Logistics Carriers — region filter, seed, toggle.
 */
(function () {
	'use strict';
	var cfg = window.EPC_LC || {};
	var url = cfg.url || '';
	var csrf = cfg.csrf || '';

	function msg(ok, text) {
		var el = document.getElementById('epc_lc_msg');
		if (!el) return;
		el.className = 'alert alert-' + (ok ? 'success' : 'danger') + ' epc-lc-msg';
		el.textContent = text || '';
		el.style.display = text ? 'block' : 'none';
	}

	function post(action, extra) {
		var fd = new FormData();
		fd.append('action', action);
		if (csrf) fd.append('csrf_guard_key', csrf);
		if (extra) {
			Object.keys(extra).forEach(function (k) {
				fd.append(k, extra[k]);
			});
		}
		var endpoint = url || (cfg.pageUrl || '');
		return fetch(endpoint, { method: 'POST', body: fd, credentials: 'same-origin' })
			.then(function (r) {
				return r.text().then(function (text) {
					try {
						return JSON.parse(text);
					} catch (e) {
						throw new Error(r.status + ' non-JSON response');
					}
				});
			});
	}

	function applyRegionFilter(region) {
		var cards = document.querySelectorAll('.epc-lc-partner[data-region]');
		cards.forEach(function (card) {
			var show = region === 'all' || card.getAttribute('data-region') === region;
			card.style.display = show ? '' : 'none';
		});
		document.querySelectorAll('.epc-lc-filter').forEach(function (btn) {
			btn.classList.toggle('is-active', btn.getAttribute('data-region') === region);
		});
	}

	document.addEventListener('DOMContentLoaded', function () {
		document.querySelectorAll('.epc-lc-filter').forEach(function (btn) {
			btn.addEventListener('click', function () {
				applyRegionFilter(btn.getAttribute('data-region') || 'all');
			});
		});

		var seedBtn = document.getElementById('epc_lc_seed_carriers');
		if (seedBtn) {
			seedBtn.addEventListener('click', function () {
				seedBtn.disabled = true;
				post('seed_carriers').then(function (j) {
					msg(!!j.status, j.message || '');
					if (j.status) setTimeout(function () { location.reload(); }, 700);
					else seedBtn.disabled = false;
				}).catch(function () {
					msg(false, 'Network error');
					seedBtn.disabled = false;
				});
			});
		}

		var sampleBtn = document.getElementById('epc_lc_seed_sample');
		if (sampleBtn) {
			sampleBtn.addEventListener('click', function () {
				sampleBtn.disabled = true;
				post('seed_sample').then(function (j) {
					msg(!!j.status, j.message || '');
					if (j.status) setTimeout(function () { location.reload(); }, 700);
					else sampleBtn.disabled = false;
				}).catch(function () {
					msg(false, 'Network error');
					sampleBtn.disabled = false;
				});
			});
		}

		document.querySelectorAll('[data-toggle-carrier]').forEach(function (btn) {
			btn.addEventListener('click', function () {
				var code = btn.getAttribute('data-toggle-carrier') || '';
				if (!code) return;
				btn.disabled = true;
				post('toggle_carrier', { carrier_code: code }).then(function (j) {
					msg(!!j.status, j.message || '');
					if (j.status) setTimeout(function () { location.reload(); }, 500);
					else btn.disabled = false;
				}).catch(function (err) {
					msg(false, (err && err.message) ? err.message : 'Network error');
					btn.disabled = false;
				});
			});
		});
	});
})();
