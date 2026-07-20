/**
 * ERP multi-user concurrency client — presence, edit locks, idempotency keys.
 * Safe under ~1000 simultaneous users on the same tenant.
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
			.then(function (r) {
				return r.json().then(function (j) {
					if (j && typeof j === 'object') {
						j._http_status = r.status;
					}
					return j;
				});
			});
	}

	var state = {
		lockToken: '',
		entityType: '',
		entityId: '',
		rowVersion: 0,
		canForce: false,
		heartbeatTimer: null,
		presenceTimer: null
	};

	function showBanner(msg, isWarn, extraHtml) {
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
		if (extraHtml) {
			el.innerHTML = (msg || '') + ' ' + extraHtml;
		} else {
			el.textContent = msg || '';
		}
	}

	function updatePresenceStrip(payload) {
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
		var selfId = payload && payload.self_user_id;
		var count = parseInt((payload && payload.count) || 0, 10) || 0;
		var sample = (payload && (payload.sample || payload.active)) || [];
		if (payload && typeof payload.can_force_lock !== 'undefined') {
			state.canForce = !!payload.can_force_lock;
		}
		var othersSample = sample.filter(function (u) {
			return parseInt(u.user_id, 10) !== parseInt(selfId, 10);
		});
		var othersCount = Math.max(0, count - 1);
		if (othersCount <= 0) {
			strip.innerHTML = '<span><i class="fa fa-users"></i> Only you in ERP</span>';
			return;
		}
		var labels = othersSample.slice(0, 8).map(function (u) {
			var tab = u.tab ? (' · ' + u.tab) : '';
			return '<span style="padding:2px 8px;border-radius:999px;background:#fff;border:1px solid #e5e5e5;">'
				+ (u.user_label || ('#' + u.user_id)) + tab + '</span>';
		}).join('');
		var more = othersCount > othersSample.length
			? ' <span style="color:#737373;">+' + (othersCount - Math.min(8, othersSample.length)) + ' more</span>'
			: '';
		strip.innerHTML = '<span><i class="fa fa-users"></i> ' + othersCount + ' other user(s) online:</span> ' + labels + more;
	}

	function presenceBeat() {
		var params = new URLSearchParams(window.location.search);
		post('presence_heartbeat', {
			tab: params.get('tab') || cfg.tab || '',
			area: params.get('area') || cfg.area || '',
			entity_type: state.entityType,
			entity_id: state.entityId
		}).then(function (j) {
			if (j && (j.active || j.sample || typeof j.count !== 'undefined')) {
				updatePresenceStrip(j);
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

	function conflictBanner(j) {
		var msg = (j && j.message) || 'Another user changed this record — reload and try again';
		var forceBtn = '';
		if (state.canForce && state.entityType && state.entityId && j && j.conflict) {
			forceBtn = '<button type="button" id="epc_erp_force_lock_btn" style="margin-left:10px;font-size:11px;font-weight:700;padding:3px 8px;cursor:pointer;">Admin takeover</button>';
		}
		var reloadBtn = '<button type="button" id="epc_erp_reload_btn" style="margin-left:8px;font-size:11px;font-weight:700;padding:3px 8px;cursor:pointer;">Reload</button>';
		showBanner(msg, true, forceBtn + reloadBtn);
		var rb = document.getElementById('epc_erp_reload_btn');
		if (rb) {
			rb.onclick = function () { window.location.reload(); };
		}
		var fb = document.getElementById('epc_erp_force_lock_btn');
		if (fb) {
			fb.onclick = function () {
				window.EpcErpConcurrency.bindEntity({
					entityType: state.entityType,
					entityId: state.entityId,
					rowVersion: state.rowVersion,
					force: true
				});
			};
		}
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
				if (j && typeof j.can_force !== 'undefined') {
					state.canForce = !!j.can_force;
				}
				if (!j || !j.status) {
					conflictBanner(j);
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
		setRowVersion: function (v) { state.rowVersion = parseInt(v, 10) || 0; },
		handleConflictResponse: conflictBanner
	};

	// Auto-augment fetch POSTs to ERP ajax with idempotency when missing;
	// surface HTTP 409 multi-user conflicts in the sticky banner.
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
		return origFetch.apply(this, arguments).then(function (resp) {
			try {
				var url2 = typeof input === 'string' ? input : (input && input.url) || '';
				if (url2 && ajaxUrl && url2.indexOf(ajaxUrl) !== -1 && resp && resp.status === 409) {
					resp.clone().json().then(function (j) {
						if (j && (j.conflict || j.conflict_code)) {
							conflictBanner(j);
						}
					}).catch(function () {});
				}
			} catch (e2) { /* ignore */ }
			return resp;
		});
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
		// Stagger presence across clients to avoid thundering-herd DB writes at 1000 users.
		var staggerMs = 2000 + Math.floor(Math.random() * 8000);
		setTimeout(function () {
			presenceBeat();
			state.presenceTimer = setInterval(presenceBeat, 28000 + Math.floor(Math.random() * 8000));
		}, staggerMs);
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
