/**
 * CP Logistics Carriers — region filter, seed, toggle.
 */
(function () {
	'use strict';

	function cfg() {
		return window.EPC_LC || {};
	}

	function endpoint() {
		var c = cfg();
		return c.url || c.ajaxUrl || '';
	}

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
		var c = cfg();
		if (c.csrf) fd.append('csrf_guard_key', c.csrf);
		if (extra) {
			Object.keys(extra).forEach(function (k) {
				fd.append(k, extra[k]);
			});
		}
		var url = endpoint();
		if (!url) {
			return Promise.reject(new Error('Missing carriers AJAX URL'));
		}
		return fetch(url, { method: 'POST', body: fd, credentials: 'same-origin' })
			.then(function (r) {
				return r.text().then(function (text) {
					try {
						return JSON.parse(text);
					} catch (e) {
						throw new Error('Bad response (' + r.status + ')');
					}
				});
			});
	}

	function applyRegionFilter(region) {
		document.querySelectorAll('.epc-lc-partner[data-region]').forEach(function (card) {
			card.style.display = (region === 'all' || card.getAttribute('data-region') === region) ? '' : 'none';
		});
		document.querySelectorAll('.epc-lc-filter').forEach(function (btn) {
			btn.classList.toggle('is-active', btn.getAttribute('data-region') === region);
		});
	}

	function on(el, ev, fn) {
		if (!el) return;
		el.addEventListener(ev, fn);
	}

	function bind() {
		var root = document.querySelector('.epc-lc');
		if (!root || root.getAttribute('data-epc-lc-bound') === '1') return;
		root.setAttribute('data-epc-lc-bound', '1');

		document.querySelectorAll('.epc-lc-filter').forEach(function (btn) {
			on(btn, 'click', function () {
				applyRegionFilter(btn.getAttribute('data-region') || 'all');
			});
		});

		on(document.getElementById('epc_lc_seed_carriers'), 'click', function () {
			var seedBtn = document.getElementById('epc_lc_seed_carriers');
			seedBtn.disabled = true;
			post('seed_carriers').then(function (j) {
				msg(!!j.status, j.message || '');
				if (j.status) setTimeout(function () { location.reload(); }, 700);
				else seedBtn.disabled = false;
			}).catch(function (err) {
				msg(false, (err && err.message) || 'Network error');
				seedBtn.disabled = false;
			});
		});

		on(document.getElementById('epc_lc_seed_sample'), 'click', function () {
			var sampleBtn = document.getElementById('epc_lc_seed_sample');
			sampleBtn.disabled = true;
			post('seed_sample').then(function (j) {
				msg(!!j.status, j.message || '');
				if (j.status) setTimeout(function () { location.reload(); }, 700);
				else sampleBtn.disabled = false;
			}).catch(function (err) {
				msg(false, (err && err.message) || 'Network error');
				sampleBtn.disabled = false;
			});
		});

		document.querySelectorAll('[data-toggle-carrier]').forEach(function (btn) {
			on(btn, 'click', function () {
				var code = btn.getAttribute('data-toggle-carrier') || '';
				if (!code) return;
				btn.disabled = true;
				post('toggle_carrier', { carrier_code: code }).then(function (j) {
					msg(!!j.status, j.message || '');
					if (j.status) setTimeout(function () { location.reload(); }, 500);
					else btn.disabled = false;
				}).catch(function (err) {
					msg(false, (err && err.message) || 'Network error');
					btn.disabled = false;
				});
			});
		});
	}

	window.epcLcBind = bind;

	if (document.readyState === 'loading') {
		document.addEventListener('DOMContentLoaded', bind);
	} else {
		bind();
	}
})();
