(function () {
	'use strict';

	var TAB_FETCH_TIMEOUT_MS = 15000;
	var TAB_LONG_WAIT_MS = 8000;
	var TAB_CACHE_TTL_MS = 60000;

	function cfg() {
		return window.EPC_APAI_SHELL || {};
	}

	function qs(sel, root) {
		return (root || document).querySelector(sel);
	}

	function cpBackend() {
		var c = cfg();
		return String(c.backend || 'cp').replace(/^\/+|\/+$/g, '') || 'cp';
	}

	/** Absolute CP URL — never resolve against bootstrap_admin <base href>. */
	function cpAbsUrl(url) {
		if (!url) {
			return url;
		}
		if (/^https?:\/\//i.test(url)) {
			return url;
		}
		if (url.charAt(0) === '/') {
			return url;
		}
		return '/' + cpBackend() + '/' + String(url).replace(/^\//, '');
	}

	function defaultPageBase() {
		return '/' + cpBackend() + '/control/portal/epc_auto_price_engine';
	}

	function syncUrlForTab(tab) {
		var c = cfg();
		var base = cpAbsUrl(c.pageBase || defaultPageBase());
		return base + '?site_key=' + encodeURIComponent(c.siteKey || '') + '&tab=' + encodeURIComponent(tab || c.tab || 'discover') + '&apai_sync=1';
	}

	function parseAjaxError(status, text) {
		var msg = 'HTTP ' + status;
		if (!text) {
			return msg;
		}
		try {
			var j = JSON.parse(text);
			if (j && j.error) {
				return String(j.error);
			}
			if (j && j.message) {
				return String(j.message);
			}
		} catch (e) {
			if (/admin login required/i.test(text)) {
				return 'Admin login required — refresh CP or log in again';
			}
			if (text.length < 400) {
				return text.replace(/\s+/g, ' ').trim();
			}
		}
		return msg;
	}

	function parseTabPayload(text) {
		if (!text) {
			return '';
		}
		try {
			var j = JSON.parse(text);
			if (j && typeof j.ok !== 'undefined') {
				if (!j.ok) {
					throw new Error(j.error || j.message || 'Tab load failed');
				}
				return String(j.html || '');
			}
		} catch (e) {
			if (e.message && e.message !== 'Unexpected token < in JSON at position 0') {
				throw e;
			}
		}
		return text;
	}

	function showTabError(body, tab, errMsg) {
		var syncUrl = syncUrlForTab(tab);
		body.innerHTML = '<div class="alert alert-danger"><strong>Loading error.</strong> ' +
			(errMsg ? ('<span>' + errMsg + '</span> — ') : '') +
			'<a href="javascript:void(0)" data-apai-retry="' + tab + '">Retry</a> or <a href="' + syncUrl + '">open with sync render</a>.</div>';
	}

	function fetchJson(url, opts, retries) {
		retries = typeof retries === 'number' ? retries : 1;
		url = cpAbsUrl(url);
		return fetch(url, opts).catch(function (err) {
			if (retries > 0) {
				return fetchJson(url, opts, retries - 1);
			}
			throw err;
		});
	}

	function fetchWithTimeout(url, opts, timeoutMs) {
		var controller = typeof AbortController !== 'undefined' ? new AbortController() : null;
		var timer = null;
		var fetchOpts = opts || {};
		if (controller) {
			fetchOpts.signal = controller.signal;
		}
		var fetchPromise = fetchJson(url, fetchOpts, 1);
		if (!timeoutMs || timeoutMs <= 0) {
			return fetchPromise;
		}
		var timeoutPromise = new Promise(function (_, reject) {
			timer = window.setTimeout(function () {
				if (controller) {
					try {
						controller.abort();
					} catch (e) {}
				}
				reject(new Error('Request timed out after ' + timeoutMs + 'ms'));
			}, timeoutMs);
		});
		return Promise.race([fetchPromise, timeoutPromise]).finally(function () {
			if (timer) {
				window.clearTimeout(timer);
			}
		});
	}

	var tabLoading = false;
	var tabDebounceTimer = null;
	var tabLoadSeq = 0;
	var tabLongWaitTimer = null;
	var tabHtmlCache = {};

	function tabCacheKey(tab, extraParams) {
		var c = cfg();
		var parts = [c.siteKey || '', tab || 'discover'];
		if (extraParams && extraParams.get) {
			['view', 'taxonomy_id', 'disc_sort', 'imports_filter', 'filter_taxonomy_id'].forEach(function (key) {
				var val = extraParams.get(key);
				if (val) {
					parts.push(key + '=' + val);
				}
			});
		}
		return parts.join('|');
	}

	function readTabCache(tab, extraParams) {
		var key = tabCacheKey(tab, extraParams);
		var hit = tabHtmlCache[key];
		if (hit && (Date.now() - hit.at) < TAB_CACHE_TTL_MS) {
			return hit.html;
		}
		return '';
	}

	function writeTabCache(tab, extraParams, html) {
		if (!html || !html.trim()) {
			return;
		}
		tabHtmlCache[tabCacheKey(tab, extraParams)] = { html: html, at: Date.now() };
	}

	function tabLabelFor(tab) {
		var labels = {
			discover: 'Discover',
			product_lines: 'Product lines',
			compare: 'Compare',
			uae_sources: 'Market sources',
			imports: 'My imports',
			rules: 'Rules',
			guide: 'Guide'
		};
		return labels[tab] || tab;
	}

	function bodyHasTabContent(body) {
		if (!body) {
			return false;
		}
		if (body.getAttribute('data-apai-inlined') === '1') {
			return true;
		}
		if (body.querySelector('.epc-disc-card, .hpanel, .alert-danger, .epc-disc-subtabs, #epc-wh-compare')) {
			return true;
		}
		var loading = body.querySelector('.epc-apai-tab-loading');
		if (loading && body.children.length <= 1) {
			return false;
		}
		return (body.textContent || '').replace(/\s+/g, ' ').trim().length > 24;
	}

	function clearTabLongWaitTimer() {
		if (tabLongWaitTimer) {
			window.clearTimeout(tabLongWaitTimer);
			tabLongWaitTimer = null;
		}
	}

	function showTabSkeleton(body, tab, progressStep) {
		body.setAttribute('data-apai-loading', '1');
		var progress = progressStep ? (' <small class="text-muted">(' + progressStep + ')</small>') : '';
		body.innerHTML = '<div class="epc-apai-tab-loading text-center" style="padding:48px 16px">' +
			'<i class="fa fa-spinner fa-spin fa-2x text-muted"></i>' +
			'<p class="text-muted" style="margin-top:12px" id="epc-apai-tab-loading-msg">Loading ' + tabLabelFor(tab) + '…' + progress + '</p></div>';
	}

	function scheduleLongWaitHint(body, tab) {
		clearTabLongWaitTimer();
		tabLongWaitTimer = window.setTimeout(function () {
			tabLongWaitTimer = null;
			if (!body || body.getAttribute('data-apai-loading') !== '1') {
				return;
			}
			var msg = qs('#epc-apai-tab-loading-msg', body);
			if (!msg || qs('#epc-apai-long-wait', body)) {
				return;
			}
			msg.innerHTML = 'Loading ' + tabLabelFor(tab) + '… Taking long — <a href="' + syncUrlForTab(tab) + '" id="epc-apai-long-wait">open full page</a>';
		}, TAB_LONG_WAIT_MS);
	}

	function setActiveTabPill(tab) {
		var container = qs('.epc-ape-tabs');
		if (!container) {
			return;
		}
		container.querySelectorAll('a[data-apai-tab]').forEach(function (a) {
			var t = a.getAttribute('data-apai-tab') || '';
			var active = t === tab;
			a.classList.toggle('btn-primary', active);
			a.classList.toggle('btn-default', !active);
		});
	}

	function updateUrlTab(tab) {
		var c = cfg();
		var pageBase = cpAbsUrl(c.pageBase || defaultPageBase());
		if (!pageBase || !window.history || !window.history.replaceState) {
			return;
		}
		try {
			var url = new URL(window.location.href);
			if (url.pathname.indexOf('/control/portal/epc_auto_price_engine') === -1) {
				url.pathname = pageBase;
			}
			url.searchParams.set('site_key', c.siteKey || url.searchParams.get('site_key') || '');
			url.searchParams.set('tab', tab);
			url.searchParams.delete('apai_sync');
			url.searchParams.delete('apai_partial');
			window.history.replaceState({ apaiTab: tab }, '', url.pathname + url.search);
		} catch (e) {}
	}

	function loadKpi() {
		var c = cfg();
		if (!c.ajaxUrl || !c.siteKey) {
			return;
		}
		var url = cpAbsUrl(c.ajaxUrl) + '?action=shell_kpi&site_key=' + encodeURIComponent(c.siteKey);
		fetchJson(url, { credentials: 'same-origin', headers: { Accept: 'application/json' } }, 1)
			.then(function (r) {
				return r.text().then(function (text) {
					if (!r.ok) {
						throw new Error(parseAjaxError(r.status, text));
					}
					return JSON.parse(text);
				});
			})
			.then(function (data) {
				if (!data || !data.ok) {
					return;
				}
				Object.keys(data.kpi || {}).forEach(function (key) {
					var el = qs('[data-apai-kpi="' + key + '"]');
					if (el) {
						el.textContent = String(data.kpi[key]);
					}
				});
			})
			.catch(function () {});
	}

	function prewarmTab(tab) {
		var c = cfg();
		if (!c.ajaxUrl || !c.siteKey || tabLoading) {
			return;
		}
		if (readTabCache(tab, null)) {
			return;
		}
		var fd = new FormData();
		fd.append('action', 'load_tab_html');
		fd.append('site_key', c.siteKey || '');
		fd.append('tab', tab);
		fetch(cpAbsUrl(c.ajaxUrl), {
			method: 'POST',
			body: fd,
			credentials: 'same-origin',
			headers: { Accept: 'application/json' }
		}).then(function (r) {
			return r.text().then(function (text) {
				if (!r.ok) {
					return;
				}
				try {
					var html = parseTabPayload(text);
					writeTabCache(tab, null, html);
				} catch (e) {}
			});
		}).catch(function () {});
	}

	function loadTab(tab, extraParams) {
		var c = cfg();
		var body = qs('#epc-apai-tab-body');
		if (!body || !c.ajaxUrl) {
			return Promise.resolve();
		}
		tab = tab || c.tab || 'discover';
		if (tabLoading && body.getAttribute('data-apai-tab') === tab) {
			return Promise.resolve();
		}

		var params = extraParams;
		if (!params) {
			params = new URLSearchParams(window.location.search);
		}

		var cachedHtml = readTabCache(tab, params);
		if (cachedHtml) {
			body.removeAttribute('data-apai-loading');
			body.innerHTML = cachedHtml;
			body.setAttribute('data-apai-tab', tab);
			setActiveTabPill(tab);
			updateUrlTab(tab);
			document.dispatchEvent(new CustomEvent('epc-apai-tab-loaded', { detail: { tab: tab } }));
			return Promise.resolve();
		}

		var seq = ++tabLoadSeq;
		tabLoading = true;
		var progressStep = tab === 'discover' ? '1/2' : '';
		showTabSkeleton(body, tab, progressStep);
		scheduleLongWaitHint(body, tab);

		var fd = new FormData();
		fd.append('action', 'load_tab_html');
		fd.append('site_key', c.siteKey || '');
		fd.append('tab', tab);
		['view', 'taxonomy_id', 'disc_sort', 'imports_filter', 'filter_taxonomy_id'].forEach(function (key) {
			var val = params.get ? params.get(key) : '';
			if (val) {
				fd.append(key, val);
			}
		});

		return fetchWithTimeout(cpAbsUrl(c.ajaxUrl), {
			method: 'POST',
			body: fd,
			credentials: 'same-origin',
			headers: { Accept: 'application/json' }
		}, TAB_FETCH_TIMEOUT_MS)
			.then(function (r) {
				return r.text().then(function (text) {
					if (!r.ok) {
						throw new Error(parseAjaxError(r.status, text));
					}
					return parseTabPayload(text);
				});
			})
			.then(function (html) {
				if (seq !== tabLoadSeq) {
					return;
				}
				body.removeAttribute('data-apai-loading');
				if (!html || html.trim() === '') {
					throw new Error('Empty tab response');
				}
				writeTabCache(tab, params, html);
				body.innerHTML = html;
				body.setAttribute('data-apai-tab', tab);
				setActiveTabPill(tab);
				updateUrlTab(tab);
				document.dispatchEvent(new CustomEvent('epc-apai-tab-loaded', { detail: { tab: tab } }));
			})
			.catch(function (err) {
				if (seq !== tabLoadSeq) {
					return;
				}
				body.removeAttribute('data-apai-loading');
				var errMsg = err && err.message ? err.message : '';
				showTabError(body, tab, errMsg);
			})
			.finally(function () {
				clearTabLongWaitTimer();
				if (seq === tabLoadSeq) {
					tabLoading = false;
				}
			});
	}

	function onTabClick(e) {
		var link = e.target.closest('a[data-apai-tab]');
		if (!link || !link.closest('.epc-ape-tabs')) {
			return;
		}
		var tab = link.getAttribute('data-apai-tab');
		if (!tab) {
			return;
		}
		e.preventDefault();
		if (tabDebounceTimer) {
			window.clearTimeout(tabDebounceTimer);
		}
		tabDebounceTimer = window.setTimeout(function () {
			tabDebounceTimer = null;
			loadTab(tab);
		}, 200);
	}

	function onDocumentClick(e) {
		var retry = e.target.closest('[data-apai-retry]');
		if (retry) {
			e.preventDefault();
			loadTab(retry.getAttribute('data-apai-retry'));
		}
	}

	function boot() {
		var c = cfg();
		if (c.ajaxUrl && String(c.ajaxUrl).charAt(0) !== '/' && !/^https?:\/\//i.test(String(c.ajaxUrl))) {
			c.ajaxUrl = cpAbsUrl(c.ajaxUrl);
		}
		if (!c.pageBase) {
			c.pageBase = defaultPageBase();
		} else {
			c.pageBase = cpAbsUrl(c.pageBase);
		}
		window.EPC_APAI_SHELL = c;

		loadKpi();
		if (!c.active || !qs('#epc-apai-tab-body')) {
			return;
		}
		document.addEventListener('click', onTabClick);
		document.addEventListener('click', onDocumentClick);
		var params = new URLSearchParams(window.location.search);
		var tab = params.get('tab') || c.tab || 'discover';
		var body = qs('#epc-apai-tab-body');

		window.setTimeout(function () {
			prewarmTab('uae_sources');
		}, 400);

		if (c.discoverInlined && tab === 'discover' && bodyHasTabContent(body)) {
			body.setAttribute('data-apai-tab', 'discover');
			setActiveTabPill('discover');
			document.dispatchEvent(new CustomEvent('epc-apai-tab-loaded', { detail: { tab: 'discover' } }));
			return;
		}

		if (bodyHasTabContent(body)) {
			body.setAttribute('data-apai-tab', tab);
			setActiveTabPill(tab);
			document.dispatchEvent(new CustomEvent('epc-apai-tab-loaded', { detail: { tab: tab } }));
			return;
		}

		loadTab(tab, params);
	}

	if (document.readyState === 'loading') {
		document.addEventListener('DOMContentLoaded', boot);
	} else {
		boot();
	}

	function postApaiAction(action, payload) {
		var c = cfg();
		if (!c.ajaxUrl) {
			return Promise.reject(new Error('AJAX URL missing'));
		}
		var fd = new FormData();
		fd.append('action', action);
		fd.append('site_key', c.siteKey || '');
		Object.keys(payload || {}).forEach(function (k) {
			if (payload[k] !== undefined && payload[k] !== null) {
				fd.append(k, payload[k]);
			}
		});
		return fetchWithTimeout(cpAbsUrl(c.ajaxUrl), { method: 'POST', body: fd, credentials: 'same-origin' }, 25000)
			.then(function (r) { return r.text(); })
			.then(function (text) {
				var j = JSON.parse(text);
				if (!j.ok && j.message && j.status !== 'idle') {
					throw new Error(j.message || j.error || 'Request failed');
				}
				return j;
			});
	}

	function bindCompareWarehouseMatch() {
		var btn = qs('#epc-compare-wh-match-btn');
		if (!btn || btn.dataset.bound === '1') {
			return;
		}
		btn.dataset.bound = '1';
		btn.addEventListener('click', function () {
			if (btn.disabled) {
				return;
			}
			btn.disabled = true;
			var prog = qs('#epc-compare-wh-progress');
			if (prog) {
				prog.style.display = 'block';
				prog.innerHTML = '<div class="epc-apai-job-progress__head"><i class="fa fa-spinner fa-spin"></i> Starting warehouse match…</div>' +
					'<div class="epc-apai-job-progress__track"><div class="epc-apai-job-progress__bar" style="width:2%"></div></div>' +
					'<div class="epc-apai-job-progress__msg text-muted" style="font-size:12px;margin-top:6px"></div>';
			}
			var startedAt = Date.now();
			postApaiAction('start_job', { type: 'warehouse_market_match' })
				.then(function (data) {
					var jobId = data.job_id;
					var timer = window.setInterval(function () {
						postApaiAction('job_status', { job_id: jobId, run_tick: '1' })
							.then(function (st) {
								if (prog) {
									var pct = Math.max(0, Math.min(100, parseInt(st.progress_pct, 10) || 0));
									var bar = qs('.epc-apai-job-progress__bar', prog);
									if (bar) {
										bar.style.width = pct + '%';
									}
									var msg = qs('.epc-apai-job-progress__msg', prog);
									if (msg) {
										msg.textContent = (st.progress_msg || st.message || st.status) +
											' (' + (st.elapsed_sec || Math.round((Date.now() - startedAt) / 1000)) + 's)';
									}
								}
								if (st.status === 'done' || st.status === 'failed') {
									window.clearInterval(timer);
									btn.disabled = false;
									if (st.status === 'done') {
										window.setTimeout(function () { window.location.reload(); }, 1200);
									}
								}
							})
							.catch(function () {});
					}, 2000);
				})
				.catch(function (err) {
					btn.disabled = false;
					if (prog) {
						prog.className = 'alert alert-danger';
						prog.textContent = 'Match failed: ' + (err.message || err);
					}
				});
		});
	}

	document.addEventListener('epc-apai-tab-loaded', function (ev) {
		if (ev.detail && ev.detail.tab === 'compare') {
			bindCompareWarehouseMatch();
		}
	});
	bindCompareWarehouseMatch();
})();
