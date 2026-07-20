/**
 * ERP multi-user concurrency client — presence, edit locks, idempotency keys.
 * Safe under many simultaneous users on the same tenant.
 */
(function () {
	'use strict';
	if (window.__epcErpConcurrencyBound) {
		return;
	}
	window.__epcErpConcurrencyBound = true;

	var cfg = window.EPC_ERP_CONCURRENCY || {};
	var ajaxUrl = cfg.ajaxUrl || '';
	var csrf = cfg.csrf || '';
	if (!ajaxUrl) {
		return;
	}

	function uuid() {
		if (window.crypto && crypto.randomUUID) {
			return crypto.randomUUID();
		}
		return 'idem-' + Date.now().toString(36) + '-' + Math.random().toString(36).slice(2, 10);
	}

	function post(action, data) {
		var fd = new FormData();
		fd.append('action', action);
		fd.append('csrf_guard_key', csrf);
		Object.keys(data || {}).forEach(function (k) {
			if (data[k] !== undefined && data[k] !== null) {
				fd.append(k, data[k]);
			}
		});
		return fetch(ajaxUrl, { method: 'POST', body: fd, credentials: 'same-origin' })
			.then(function (r) { return r.json(); });
	}

	var state = {
		lockToken: '',
		entityType: '',
		entityId: '',
		rowVersion: 0,
		heartbeatTimer: null,
		presenceTimer: null
	};

	function showBanner(msg, isWarn) {
		var el = document.getElementById('epc_erp_concurrency_banner');
		if (!el) {
			el = document.createElement('div');
			el.id = 'epc_erp_concurrency_banner';
			el.setAttribute('role', 'status');
			el.style.cssText = 'position:sticky;top:0;z-index:1100;padding:8px 14px;font-size:12.5px;font-weight:700;display:none;';
			var host = document.querySelector('.epc-erp-shell, .epc-erp-section, .epc-erp-workspace, body');
			if (host) {
				host.insertBefore(el, host.firstChild);
			}
		}
		el.style.display = msg ? 'block' : 'none';
		el.style.background = isWarn ? '#fef3c7' : '#ecfdf5';
		el.style.color = isWarn ? '#92400e' : '#065f46';
		el.style.borderBottom = isWarn ? '1px solid #fcd34d' : '1px solid #86efac';
		el.textContent = msg || '';
	}

	function updatePresenceStrip(active, selfId) {
		var strip = document.getElementById('epc_erp_presence_strip');
		if (!strip) {
			strip = document.createElement('div');
			strip.id = 'epc_erp_presence_strip';
			strip.style.cssText = 'display:flex;flex-wrap:wrap;gap:6px;align-items:center;padding:6px 12px;font-size:11.5px;color:#525252;background:#fafafa;border-bottom:1px solid #e5e5e5;';
			var host = document.querySelector('.epc-erp-topbar, .epc-erp-shell, .epc-erp-workspace, body');
			if (host) {
				host.insertBefore(strip, host.firstChild);
			}
		}
		var others = (active || []).filter(function (u) { return parseInt(u.user_id, 10) !== parseInt(selfId, 10); });
		if (!others.length) {
			strip.innerHTML = '<span><i class="fa fa-users"></i> Only you in ERP</span>';
			return;
		}
		var labels = others.slice(0, 8).map(function (u) {
			var tab = u.tab ? (' · ' + u.tab) : '';
			return '<span style="padding:2px 8px;border-radius:999px;background:#fff;border:1px solid #e5e5e5;">'
				+ (u.user_label || ('#' + u.user_id)) + tab + '</span>';
		}).join('');
		strip.innerHTML = '<span><i class="fa fa-users"></i> ' + others.length + ' other user(s) online:</span> ' + labels;
	}

	function presenceBeat() {
		var params = new URLSearchParams(window.location.search);
		post('presence_heartbeat', {
			tab: params.get('tab') || cfg.tab || '',
			area: params.get('area') || cfg.area || '',
			entity_type: state.entityType,
			entity_id: state.entityId
		}).then(function (j) {
			if (j && j.active) {
				updatePresenceStrip(j.active, j.self_user_id);
			}
		}).catch(function () {});
	}

	function lockHeartbeat() {
		if (!state.lockToken || !state.entityType || !state.entityId) {
			return;
		}
		post('edit_lock_heartbeat', {
			entity_type: state.entityType,
			entity_id: state.entityId,
			lock_token: state.lockToken,
			ttl: 120
		}).then(function (j) {
			if (!j || !j.status) {
				showBanner(j && j.message ? j.message : 'Edit lock lost — reload before saving', true);
				state.lockToken = '';
			}
		}).catch(function () {});
	}

	window.EpcErpConcurrency = {
		/** Attach lock + version to a form (invoice, PO, etc.). */
		bindEntity: function (opts) {
			opts = opts || {};
			state.entityType = String(opts.entityType || '');
			state.entityId = String(opts.entityId || '');
			state.rowVersion = parseInt(opts.rowVersion || 0, 10) || 0;
			if (!state.entityType || !state.entityId) {
				return Promise.resolve(null);
			}
			return post('edit_lock_acquire', {
				entity_type: state.entityType,
				entity_id: state.entityId,
				ttl: 120,
				force: opts.force ? 1 : 0
			}).then(function (j) {
				if (!j || !j.status) {
					showBanner((j && j.message) || 'Record locked by another user', true);
					return j;
				}
				state.lockToken = (j.lock && j.lock.lock_token) || '';
				showBanner('Editing locked for you — other users cannot overwrite this record', false);
				if (state.heartbeatTimer) {
					clearInterval(state.heartbeatTimer);
				}
				state.heartbeatTimer = setInterval(lockHeartbeat, 45000);
				return j;
			});
		},
		release: function () {
			if (!state.lockToken) {
				return Promise.resolve();
			}
			var t = state.lockToken;
			var et = state.entityType;
			var eid = state.entityId;
			state.lockToken = '';
			if (state.heartbeatTimer) {
				clearInterval(state.heartbeatTimer);
				state.heartbeatTimer = null;
			}
			return post('edit_lock_release', {
				entity_type: et,
				entity_id: eid,
				lock_token: t
			}).then(function () {
				showBanner('', false);
			});
		},
		/** Ensure FormData / forms get concurrency fields before POST. */
		augmentFormData: function (fd) {
			if (!(fd instanceof FormData)) {
				return fd;
			}
			if (!fd.has('idempotency_key')) {
				fd.append('idempotency_key', uuid());
			}
			if (state.lockToken) {
				fd.append('edit_lock_token', state.lockToken);
			}
			if (state.rowVersion > 0 && !fd.has('expected_version')) {
				fd.append('expected_version', String(state.rowVersion));
			}
			return fd;
		},
		newIdempotencyKey: uuid,
		getLockToken: function () { return state.lockToken; },
		getRowVersion: function () { return state.rowVersion; },
		setRowVersion: function (v) { state.rowVersion = parseInt(v, 10) || 0; }
	};

	// Auto-augment fetch POSTs to ERP ajax with idempotency when missing.
	var origFetch = window.fetch;
	window.fetch = function (input, init) {
		try {
			var url = typeof input === 'string' ? input : (input && input.url) || '';
			if (url && ajaxUrl && url.indexOf(ajaxUrl) !== -1 && init && init.method && String(init.method).toUpperCase() === 'POST') {
				if (init.body instanceof FormData) {
					window.EpcErpConcurrency.augmentFormData(init.body);
				}
			}
		} catch (e) { /* ignore */ }
		return origFetch.apply(this, arguments);
	};

	// Auto-bind when page declares data-erp-entity / data-erp-entity-id
	document.addEventListener('DOMContentLoaded', function () {
		var root = document.querySelector('[data-erp-entity][data-erp-entity-id]');
		if (root) {
			window.EpcErpConcurrency.bindEntity({
				entityType: root.getAttribute('data-erp-entity'),
				entityId: root.getAttribute('data-erp-entity-id'),
				rowVersion: root.getAttribute('data-erp-row-version') || 0
			});
		}
		presenceBeat();
		state.presenceTimer = setInterval(presenceBeat, 30000);
	});

	window.addEventListener('beforeunload', function () {
		if (!state.lockToken || !navigator.sendBeacon) {
			return;
		}
		try {
			var fd = new FormData();
			fd.append('action', 'edit_lock_release');
			fd.append('csrf_guard_key', csrf);
			fd.append('entity_type', state.entityType);
			fd.append('entity_id', state.entityId);
			fd.append('lock_token', state.lockToken);
			navigator.sendBeacon(ajaxUrl, fd);
		} catch (e) { /* ignore */ }
	});
})();
