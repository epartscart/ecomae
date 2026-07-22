/**
 * CP Channels — worldwide marketplace hub (filter, seed, toggle, sync, import).
 */
(function () {
	'use strict';

	function cfg() {
		return window.EPC_CH || {};
	}

	function endpoint() {
		var c = cfg();
		return c.ajaxUrl || c.url || '';
	}

	function msg(ok, text) {
		var el = document.getElementById('epc_ch_msg');
		if (!el) return;
		el.className = 'alert alert-' + (ok ? 'success' : 'danger') + ' epc-ch-msg';
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
			return Promise.reject(new Error('Missing channels AJAX URL'));
		}
		return fetch(url, {
			method: 'POST',
			body: fd,
			credentials: 'same-origin',
			headers: { Accept: 'application/json', 'X-Requested-With': 'XMLHttpRequest' }
		}).then(function (r) {
			return r.text().then(function (text) {
				var j = null;
				try {
					j = text ? JSON.parse(text) : null;
				} catch (e) {
					j = null;
				}
				if (!j) {
					throw new Error('Bad response (' + r.status + ')');
				}
				return j;
			});
		});
	}

	function applyFilter(key) {
		document.querySelectorAll('.epc-ch-partner').forEach(function (card) {
			var region = card.getAttribute('data-region') || '';
			var family = card.getAttribute('data-family') || '';
			var show = key === 'all' || region === key || family === key;
			card.style.display = show ? '' : 'none';
		});
		document.querySelectorAll('.epc-ch-filter').forEach(function (btn) {
			var f = btn.getAttribute('data-filter') || btn.getAttribute('data-region') || '';
			btn.classList.toggle('is-active', f === key);
		});
	}

	function on(el, ev, fn) {
		if (!el) return;
		el.addEventListener(ev, fn);
	}

	function bind() {
		var root = document.querySelector('.epc-ch');
		if (!root || root.getAttribute('data-epc-ch-bound') === '1') return;
		root.setAttribute('data-epc-ch-bound', '1');

		document.querySelectorAll('.epc-ch-filter').forEach(function (btn) {
			on(btn, 'click', function () {
				applyFilter(btn.getAttribute('data-filter') || btn.getAttribute('data-region') || 'all');
			});
		});

		on(document.getElementById('epc_ch_seed_channels'), 'click', function () {
			var seedBtn = document.getElementById('epc_ch_seed_channels');
			seedBtn.disabled = true;
			post('seed_channels').then(function (j) {
				msg(!!j.status, j.message || '');
				if (j.status) setTimeout(function () { location.reload(); }, 700);
				else seedBtn.disabled = false;
			}).catch(function (err) {
				msg(false, (err && err.message) || 'Network error');
				seedBtn.disabled = false;
			});
		});

		on(document.getElementById('epc_ch_seed_sample'), 'click', function () {
			var sampleBtn = document.getElementById('epc_ch_seed_sample');
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

		document.querySelectorAll('[data-toggle-channel]').forEach(function (btn) {
			on(btn, 'click', function () {
				var code = btn.getAttribute('data-toggle-channel') || '';
				if (!code) return;
				btn.disabled = true;
				post('toggle_channel', { channel_code: code }).then(function (j) {
					msg(!!j.status, j.message || '');
					if (j.status) setTimeout(function () { location.reload(); }, 500);
					else btn.disabled = false;
				}).catch(function (err) {
					msg(false, (err && err.message) || 'Network error');
					btn.disabled = false;
				});
			});
		});

		document.querySelectorAll('[data-sync-channel]').forEach(function (btn) {
			on(btn, 'click', function () {
				var code = btn.getAttribute('data-sync-channel') || '';
				if (!code) return;
				var orig = btn.innerHTML;
				btn.disabled = true;
				btn.innerHTML = '<i class="fa fa-spinner fa-spin"></i> Syncing…';
				post('sync_inventory', { channel: code }).then(function (j) {
					msg(!!j.status, j.message || '');
					if (j.status) setTimeout(function () { location.reload(); }, 700);
					else {
						btn.disabled = false;
						btn.innerHTML = orig;
					}
				}).catch(function (err) {
					msg(false, (err && err.message) || 'Network error');
					btn.disabled = false;
					btn.innerHTML = orig;
				});
			});
		});

		document.querySelectorAll('[data-import-order]').forEach(function (btn) {
			on(btn, 'click', function () {
				var id = btn.getAttribute('data-import-order') || '';
				if (!id) return;
				btn.disabled = true;
				post('import_order', { marketplace_order_id: id }).then(function (j) {
					msg(!!j.status, j.message || '');
					if (j.status) setTimeout(function () { location.reload(); }, 700);
					else btn.disabled = false;
				}).catch(function (err) {
					msg(false, (err && err.message) || 'Network error');
					btn.disabled = false;
				});
			});
		});
	}

	window.epcChBind = bind;

	if (document.readyState === 'loading') {
		document.addEventListener('DOMContentLoaded', bind);
	} else {
		bind();
	}
})();
