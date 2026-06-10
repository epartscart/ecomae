(function () {
	'use strict';

	var toastEl = null;
	var bulkBtn = null;
	var selectedCount = 0;
	var advisoryCache = {};
	var initialized = false;
	var crawlInFlight = false;
	var crawlPollTimer = null;

	function discoverCfg() {
		return window.EPC_APAI_DISCOVER || {};
	}

	function shouldInitDiscover() {
		var cfg = discoverCfg();
		if (!cfg.ajaxUrl) {
			return false;
		}
		if (cfg.active) {
			return true;
		}
		return !!document.querySelector('.epc-disc-card__check, #epc-disc-bulk-bar');
	}

	function qs(sel) {
		return document.querySelector(sel);
	}

	function qsa(sel) {
		return Array.prototype.slice.call(document.querySelectorAll(sel));
	}

	function showToast(msg, type) {
		if (!toastEl) {
			toastEl = document.getElementById('epc-apai-disc-toast');
		}
		if (!toastEl) {
			toastEl = document.createElement('div');
			toastEl.id = 'epc-apai-disc-toast';
			toastEl.className = 'alert alert-info';
			toastEl.style.cssText = 'position:fixed;bottom:20px;right:20px;z-index:9999;min-width:240px;max-width:420px;box-shadow:0 4px 12px rgba(0,0,0,.15)';
			document.body.appendChild(toastEl);
		}
		toastEl.className = 'alert alert-' + (type || 'info');
		toastEl.textContent = msg;
		toastEl.style.display = 'block';
		clearTimeout(showToast._t);
		showToast._t = setTimeout(function () {
			toastEl.style.display = 'none';
		}, 5000);
	}

	function setLoading(btn, on, label) {
		if (!btn) return;
		if (on) {
			if (btn.disabled && btn.dataset.loading === '1') {
				return;
			}
			btn.dataset.origHtml = btn.innerHTML;
			btn.dataset.loading = '1';
			btn.disabled = true;
			btn.innerHTML = '<i class="fa fa-spinner fa-spin"></i> ' + (label || 'Working…');
		} else {
			btn.disabled = false;
			btn.dataset.loading = '0';
			if (btn.dataset.origHtml) {
				btn.innerHTML = btn.dataset.origHtml;
			}
		}
	}

	function loadDiscoverCounts() {
		var taxSel = qs('#epc-disc-taxonomy-filter') || qs('select[name="taxonomy_id"]');
		var payload = discoverFilterPayload();
		if (taxSel && taxSel.value) {
			payload.taxonomy_id = taxSel.value;
		}
		postAction('discover_counts', payload)
			.then(function (data) {
				if (!data || !data.counts) return;
				Object.keys(data.counts).forEach(function (vk) {
					var badge = qs('[data-disc-count="' + vk + '"]');
					if (badge) {
						badge.textContent = String(data.counts[vk]);
					}
				});
			})
			.catch(function () {});
	}

	function ensureProgressBar(containerId, label) {
		var progress = qs('#' + containerId);
		if (!progress) {
			var wrap = qs('.epc-disc-toolbar') || qs('#epc-apai-tab-body') || document.body;
			progress = document.createElement('div');
			progress.id = containerId;
			progress.className = 'epc-apai-job-progress alert alert-info';
			progress.innerHTML =
				'<div class="epc-apai-job-progress__head"><i class="fa fa-spinner fa-spin"></i> <span class="epc-apai-job-progress__label">' +
				(label || 'Working…') + '</span> <span class="epc-apai-job-progress__elapsed text-muted"></span></div>' +
				'<div class="epc-apai-job-progress__track"><div class="epc-apai-job-progress__bar" style="width:0%"></div></div>' +
				'<div class="epc-apai-job-progress__msg text-muted" style="font-size:12px;margin-top:6px"></div>';
			if (wrap && wrap.parentNode) {
				wrap.parentNode.insertBefore(progress, wrap);
			} else {
				document.body.appendChild(progress);
			}
		}
		progress.style.display = 'block';
		return progress;
	}

	function updateProgressBar(containerId, data) {
		var progress = qs('#' + containerId);
		if (!progress) {
			return;
		}
		var pct = Math.max(0, Math.min(100, parseInt(data.progress_pct, 10) || 0));
		var bar = qs('.epc-apai-job-progress__bar', progress);
		if (bar) {
			bar.style.width = pct + '%';
		}
		var msg = qs('.epc-apai-job-progress__msg', progress);
		if (msg) {
			msg.textContent = data.progress_msg || data.message || data.status || '';
		}
		var elapsed = qs('.epc-apai-job-progress__elapsed', progress);
		if (elapsed && data.elapsed_sec) {
			elapsed.textContent = '(' + data.elapsed_sec + 's)';
		}
	}

	function pollApaiJob(jobId, containerId, onDone, runTick) {
		if (crawlPollTimer) {
			clearInterval(crawlPollTimer);
		}
		var startedAt = Date.now();
		ensureProgressBar(containerId || 'epc-disc-crawl-progress', 'Background job');
		crawlPollTimer = setInterval(function () {
			var payload = { job_id: jobId };
			if (runTick) {
				payload.run_tick = '1';
			}
			postAction('job_status', payload)
				.then(function (data) {
					if (!data.elapsed_sec) {
						data.elapsed_sec = Math.round((Date.now() - startedAt) / 1000);
					}
					updateProgressBar(containerId || 'epc-disc-crawl-progress', data);
					if (data.status === 'done' || data.status === 'failed' || data.status === 'idle') {
						clearInterval(crawlPollTimer);
						crawlPollTimer = null;
						crawlInFlight = false;
						var type = data.status === 'done' ? 'success' : (data.status === 'failed' ? 'danger' : 'info');
						showToast(data.message || data.progress_msg || data.status, type);
						if (typeof onDone === 'function') {
							onDone(data);
						} else if (data.status === 'done') {
							setTimeout(function () { window.location.reload(); }, 1200);
						}
					}
				})
				.catch(function () {});
		}, 2000);
	}

	function pollCrawlStatus(jobId) {
		pollApaiJob(jobId, 'epc-disc-crawl-progress');
	}

	function selectedIds() {
		return qsa('.epc-disc-card__check:checked').map(function (cb) {
			return parseInt(cb.value, 10);
		}).filter(function (id) { return id > 0; });
	}

	function updateBulkUi() {
		selectedCount = selectedIds().length;
		if (bulkBtn) {
			bulkBtn.disabled = selectedCount === 0;
			bulkBtn.innerHTML = '<i class="fa fa-plus"></i> Add selected to catalogue (' + selectedCount + ')';
		}
		var selAll = qs('#epc-disc-select-all');
		var cards = qsa('.epc-disc-card__check');
		if (selAll && cards.length) {
			selAll.checked = selectedCount === cards.length;
			selAll.indeterminate = selectedCount > 0 && selectedCount < cards.length;
		}
	}

	function timeAgo(ts) {
		if (!ts || ts <= 0) return '';
		var sec = Math.max(0, Math.floor(Date.now() / 1000) - ts);
		if (sec < 60) return 'just now';
		if (sec < 3600) return Math.floor(sec / 60) + 'm ago';
		if (sec < 86400) return Math.floor(sec / 3600) + 'h ago';
		return Math.floor(sec / 86400) + 'd ago';
	}

	function discoverFilterPayload() {
		var viewInput = qs('input[name="view"]');
		var sortSel = qs('#epc-disc-sort');
		var activeSubtab = qs('.epc-disc-subtabs .epc-imports-subtabs__pill--active');
		var view = 'all_suggestions';
		if (viewInput && viewInput.value) {
			view = viewInput.value;
		} else if (activeSubtab && activeSubtab.getAttribute('data-disc-view')) {
			view = activeSubtab.getAttribute('data-disc-view');
		}
		if (view === 'new') {
			view = 'all_suggestions';
		}
		return {
			view: view,
			sort: sortSel ? sortSel.value : 'newest'
		};
	}

	function updateCardFromFetchItem(item) {
		var card = document.querySelector('.epc-disc-card[data-queue-id="' + item.id + '"]');
		if (!card) return;
		var ts = item.last_crawl_at || item.last_fetched || 0;
		var tsEl = card.querySelector('.epc-disc-card__fetched');
		if (tsEl && ts) {
			tsEl.dataset.ts = String(ts);
			tsEl.style.display = '';
			tsEl.innerHTML = '<i class="fa fa-clock-o"></i> Updated ' + timeAgo(ts);
			card.dataset.lastUpdated = String(ts);
		}
		var priceEl = card.querySelector('[data-field="source_price"]');
		if (priceEl && item.price) {
			priceEl.textContent = parseFloat(item.price).toFixed(2);
		}
		if (item.source_price_range && item.source_price_range.min) {
			var range = item.source_price_range;
			var rangeEl = card.querySelector('[data-field="source_price_range"]');
			if (!rangeEl) {
				var pricesBlock = card.querySelector('.epc-disc-card__prices');
				if (pricesBlock) {
					pricesBlock.outerHTML = '<div class="epc-disc-price-range epc-disc-price-range--good" data-field="source_price_range"></div>';
					rangeEl = card.querySelector('[data-field="source_price_range"]');
				}
			}
			if (rangeEl) {
				var marginPct = parseFloat(range.margin_pct) || 0;
				var minP = parseFloat(range.buy_min || range.min) || 0;
				var mpP = parseFloat(range.target_sell_price || range.marketplace_price) || 0;
				var cls = 'epc-disc-price-range--good';
				if (mpP > 0 && Math.abs(minP - mpP) < 0.01) {
					cls = 'epc-disc-price-range--flat';
				} else if (marginPct < 10) {
					cls = 'epc-disc-price-range--thin';
				}
				rangeEl.className = 'epc-disc-price-range ' + cls;
				var minEl = rangeEl.querySelector('[data-field="range_min"]');
				var maxEl = rangeEl.querySelector('[data-field="range_max"]');
				var mpEl = rangeEl.querySelector('[data-field="range_marketplace"]');
				var marginEl = rangeEl.querySelector('[data-field="range_margin"]');
				if (minEl) minEl.textContent = minP.toFixed(2);
				if (maxEl && mpP > 0) maxEl.textContent = mpP.toFixed(2);
				if (mpEl && mpP > 0) mpEl.textContent = mpP.toFixed(2);
				if (marginEl) {
					marginEl.textContent = (parseFloat(range.margin_abs) || 0).toFixed(2) + ' ' + (range.currency || 'AED') + ' (' + marginPct.toFixed(1) + '%)';
				}
				var advice = item.pricing_advice || range.pricing_advice;
				if (advice && advice.badge) {
					var advEl = rangeEl.querySelector('[data-field="pricing_advice"]');
					if (!advEl) {
						advEl = document.createElement('div');
						advEl.setAttribute('data-field', 'pricing_advice');
						rangeEl.insertBefore(advEl, rangeEl.firstChild);
					}
					var advCls = 'epc-disc-advice--info';
					if (advice.level === 'success') advCls = 'epc-disc-advice--success';
					else if (advice.level === 'warning') advCls = 'epc-disc-advice--warning';
					else if (advice.level === 'danger') advCls = 'epc-disc-advice--danger';
					advEl.className = 'epc-disc-advice ' + advCls;
					advEl.innerHTML = '<strong>' + advice.badge + '</strong>' + (advice.message ? ' — ' + advice.message : '');
				}
			}
		}
		if (item.catalogue_price) {
			var catEl = card.querySelector('[data-field="catalogue_price"]');
			if (catEl) {
				catEl.textContent = 'Catalogue: ' + parseFloat(item.catalogue_price).toFixed(2);
			}
		}
		var badge = card.querySelector('[data-field="price_badge"]');
		if (item.price_changed && typeof item.price_diff_pct === 'number') {
			var pct = item.price_diff_pct;
			var arrow = pct < 0 ? '↓' : '↑';
			var label = 'Price ' + arrow + ' ' + Math.abs(pct).toFixed(1).replace(/\.0$/, '') + '%';
			if (!badge) {
				var meta = card.querySelector('.epc-disc-card__meta');
				if (meta) {
					badge = document.createElement('span');
					badge.setAttribute('data-field', 'price_badge');
					meta.appendChild(badge);
				}
			}
			if (badge) {
				badge.className = 'epc-disc-price-badge ' + (pct < 0 ? 'epc-disc-price-badge--down' : 'epc-disc-price-badge--up');
				badge.textContent = label;
			}
			card.dataset.priceDiff = String(pct);
		}
	}

	function refreshRelativeTimestamps() {
		qsa('.epc-disc-card__fetched[data-ts]').forEach(function (el) {
			var ts = parseInt(el.dataset.ts, 10);
			if (ts > 0) {
				el.innerHTML = '<i class="fa fa-clock-o"></i> Updated ' + timeAgo(ts);
			}
		});
	}

	function postAction(action, extra) {
		var cfg = discoverCfg();
		var fd = new FormData();
		fd.append('action', action);
		fd.append('site_key', cfg.siteKey || '');
		if (extra) {
			Object.keys(extra).forEach(function (k) {
				var v = extra[k];
				if (Array.isArray(v)) {
					v.forEach(function (item) { fd.append(k + '[]', item); });
				} else if (v !== undefined && v !== null) {
					fd.append(k, v);
				}
			});
		}
		return fetch(cfg.ajaxUrl, { method: 'POST', body: fd, credentials: 'same-origin' })
			.then(function (r) {
				return r.text().then(function (text) {
					var data = null;
					if (text) {
						try {
							data = JSON.parse(text);
						} catch (parseErr) {
							var snippet = text.replace(/\s+/g, ' ').trim().slice(0, 120);
							throw new Error(r.status + ' ' + (snippet || 'Empty server response'));
						}
					}
					if (!data) {
						throw new Error(r.status + ' Empty server response');
					}
					if (!r.ok && data.message) {
						throw new Error(data.message);
					}
					if (!r.ok) {
						throw new Error('Request failed (' + r.status + ')');
					}
					return data;
				});
			});
	}

	function confidenceClass(conf) {
		if (conf === 'high') return 'label-success';
		if (conf === 'medium') return 'label-warning';
		return 'label-default';
	}

	function renderAdvisory(queueId, data) {
		advisoryCache[queueId] = data;
		var wrap = document.querySelector('.epc-disc-card__category[data-queue-id="' + queueId + '"] .epc-disc-cat-advisory');
		if (!wrap) return;
		wrap.dataset.loading = '0';
		if (!data || !data.ok) {
			wrap.innerHTML = '<span class="text-muted">Category suggestion unavailable</span>';
			return;
		}
		var conf = (data.confidence || 'medium');
		var confLabel = conf.charAt(0).toUpperCase() + conf.slice(1);
		var html = '<strong>' + (data.category_name || 'Unknown') + '</strong> ';
		html += '<span class="label ' + confidenceClass(conf) + '">' + confLabel + ' confidence</span>';
		if (data.will_create) {
			html += ' <span class="label label-info epc-disc-cat-new-badge">New category will be created</span>';
		}
		if (data.taxonomy_slug) {
			html += '<br><small class="text-muted">' + data.taxonomy_slug + '</small>';
		}
		wrap.innerHTML = html;

		var sel = document.querySelector('.epc-disc-cat-select[data-queue-id="' + queueId + '"]');
		if (sel && data.category_id > 0) {
			var opt = sel.querySelector('option[value="' + data.category_id + '"]');
			if (opt) {
				sel.dataset.advisedId = String(data.category_id);
			}
		}
	}

	function loadAdvisory(queueId) {
		return postAction('advise_category', { queue_id: queueId })
			.then(function (data) {
				renderAdvisory(queueId, data);
				return data;
			})
			.catch(function () {
				renderAdvisory(queueId, { ok: false });
			});
	}

	function categorySelectionForCard(queueId) {
		var sel = document.querySelector('.epc-disc-cat-select[data-queue-id="' + queueId + '"]');
		if (!sel) {
			return { category_mode: 'auto', category_id: 0 };
		}
		var val = sel.value;
		if (val === 'auto') {
			return { category_mode: 'auto', category_id: 0 };
		}
		if (val === 'create_new') {
			return { category_mode: 'create_from_name', category_id: 0 };
		}
		return { category_mode: 'override', category_id: parseInt(val, 10) || 0 };
	}

	function syncApproveFormHidden(card) {
		var queueId = parseInt(card.getAttribute('data-queue-id'), 10);
		var sel = card.querySelector('.epc-disc-cat-select');
		var form = card.querySelector('.epc-disc-approve-form');
		if (!sel || !form) return;
		var pick = categorySelectionForCard(queueId);
		var modeInput = form.querySelector('.epc-disc-cat-mode');
		var idInput = form.querySelector('.epc-disc-cat-id');
		if (modeInput) modeInput.value = pick.category_mode;
		if (idInput) idInput.value = String(pick.category_id || 0);
	}

	function bindCategoryControls() {
		qsa('.epc-disc-cat-select').forEach(function (sel) {
			sel.addEventListener('change', function () {
				var card = sel.closest('.epc-disc-card');
				if (card) syncApproveFormHidden(card);
			});
		});
		qsa('.epc-disc-approve-form').forEach(function (form) {
			form.addEventListener('submit', function () {
				var card = form.closest('.epc-disc-card');
				if (card) syncApproveFormHidden(card);
			});
		});
	}

	function loadAllAdvisories() {
		var cards = qsa('.epc-disc-card[data-queue-id]');
		cards.forEach(function (card) {
			var qid = parseInt(card.getAttribute('data-queue-id'), 10);
			if (qid > 0) {
				loadAdvisory(qid);
			}
		});
	}

	function bindCheckboxes() {
		qsa('.epc-disc-card__check').forEach(function (cb) {
			cb.addEventListener('change', updateBulkUi);
		});
		updateBulkUi();
	}

	function bindToolbarActions() {
		function runDiscoverSearch(btn, searchMode) {
			var keyword = (qs('#epc-disc-search-input') || {}).value || '';
			var taxSel = qs('#epc-disc-taxonomy-filter') || qs('select[name="taxonomy_id"]');
			var taxonomyId = taxSel ? taxSel.value : '0';
			setLoading(btn, true, searchMode === 'fast' ? 'Fast search…' : 'Searching…');
			var filters = discoverFilterPayload();
			postAction('discover_search', Object.assign({
				keyword: keyword,
				taxonomy_id: taxonomyId,
				search_mode: searchMode || 'full'
			}, filters))
				.then(function (data) {
					var msg = data.message || 'Search complete';
					if (data.search_message) {
						msg += ' — ' + data.search_message;
					}
					if (data.source_domains && data.source_domains.length) {
						msg += ' [' + data.source_domains.join(', ') + ']';
					}
					showToast(msg, data.ok ? 'success' : 'warning');
					if (data.ok && (data.added || 0) > 0) {
						setTimeout(function () { window.location.reload(); }, 1200);
					}
				})
				.catch(function (err) {
					showToast('Search failed: ' + err, 'danger');
				})
				.finally(function () {
					setLoading(btn, false);
				});
		}

		var searchBtn = qs('#epc-disc-search-btn');
		if (searchBtn) {
			searchBtn.addEventListener('click', function () {
				runDiscoverSearch(searchBtn, 'full');
			});
		}
		var fastSearchBtn = qs('#epc-disc-fast-search-btn');
		if (fastSearchBtn) {
			fastSearchBtn.addEventListener('click', function () {
				runDiscoverSearch(fastSearchBtn, 'fast');
			});
		}

		var fetchBtn = qs('#epc-disc-fetch-btn');
		if (fetchBtn) {
			fetchBtn.addEventListener('click', function () {
				var ids = selectedIds();
				setLoading(fetchBtn, true, 'Fetching…');
				var payload = {};
				if (ids.length) {
					payload.queue_ids = ids;
				}
				var taxSel = qs('#epc-disc-taxonomy-filter') || qs('select[name="taxonomy_id"]');
				if (taxSel && taxSel.value && taxSel.value !== '0') {
					payload.taxonomy_id = taxSel.value;
				}
				postAction('fetch_prices', payload)
					.then(function (data) {
						showToast(data.message || 'Fetch complete', data.ok ? 'success' : 'warning');
						if (data.ok && data.items && data.items.length) {
							data.items.forEach(updateCardFromFetchItem);
							if (!ids.length) {
								setTimeout(function () { window.location.reload(); }, 1500);
							}
						}
					})
					.catch(function (err) {
						showToast('Fetch failed: ' + err, 'danger');
					})
					.finally(function () {
						setLoading(fetchBtn, false);
					});
			});
		}

		function runCrawl(btn) {
			if (!btn || crawlInFlight) return;
			var mode = btn.getAttribute('data-crawl-mode') || 'quick';
			crawlInFlight = true;
			setLoading(btn, true, mode === 'full' ? 'Queuing…' : 'Quick crawl…');
			qsa('#epc-disc-crawl-btn, #epc-disc-crawl-full-btn, #epc-disc-crawl-empty-btn, #epc-disc-crawl-banner-btn').forEach(function (b) {
				if (b !== btn) b.disabled = true;
			});
			var taxSel = qs('#epc-disc-taxonomy-filter') || qs('select[name="taxonomy_id"]');
			var jobType = mode === 'full' ? 'crawl_full' : 'crawl_quick';
			showToast((mode === 'full' ? 'Full' : 'Quick') + ' crawl starting…', 'info');
			postAction('start_job', {
				type: jobType,
				taxonomy_id: taxSel ? taxSel.value : '0'
			})
				.then(function (data) {
					if (data.job_id) {
						pollApaiJob(data.job_id, 'epc-disc-crawl-progress');
						return;
					}
					throw new Error(data.message || 'Failed to start crawl');
				})
				.catch(function (err) {
					showToast('Crawl failed: ' + err, 'danger');
					crawlInFlight = false;
					setLoading(btn, false);
					qsa('#epc-disc-crawl-btn, #epc-disc-crawl-full-btn, #epc-disc-crawl-empty-btn, #epc-disc-crawl-banner-btn').forEach(function (b) {
						b.disabled = false;
					});
				});
		}

		qsa('#epc-disc-crawl-btn, #epc-disc-crawl-full-btn, #epc-disc-crawl-empty-btn, #epc-disc-crawl-banner-btn').forEach(function (crawlBtn) {
			crawlBtn.addEventListener('click', function () {
				runCrawl(crawlBtn);
			});
		});

		bulkBtn = qs('#epc-disc-bulk-approve');
		if (bulkBtn) {
			bulkBtn.addEventListener('click', function () {
				var ids = selectedIds();
				if (!ids.length) {
					showToast('Select at least one product', 'warning');
					return;
				}
				if (!window.confirm('Import ' + ids.length + ' selected product(s) to your catalogue?')) {
					return;
				}
				setLoading(bulkBtn, true, 'Importing…');

				var overrides = {};
				ids.forEach(function (qid) {
					overrides[qid] = categorySelectionForCard(qid);
				});

				postAction('bulk_approve', {
					queue_ids: ids,
					category_overrides: JSON.stringify(overrides)
				})
					.then(function (data) {
						showToast(data.message || 'Bulk import complete', data.ok ? 'success' : 'warning');
						if (data.ok && (data.imported || 0) > 0) {
							setTimeout(function () {
								var url = new URL(window.location.href);
								url.searchParams.set('tab', 'discover');
								url.searchParams.set('view', 'market_confirmed');
								window.location.href = url.toString();
							}, 1500);
						}
					})
					.catch(function (err) {
						showToast('Bulk import failed: ' + err, 'danger');
					})
					.finally(function () {
						setLoading(bulkBtn, false);
					});
			});
		}

		var clearBtn = qs('#epc-disc-clear-sel');
		if (clearBtn) {
			clearBtn.addEventListener('click', function (e) {
				e.preventDefault();
				qsa('.epc-disc-card__check').forEach(function (cb) { cb.checked = false; });
				var selAllBtn = qs('#epc-disc-select-all');
				if (selAllBtn) {
					selAllBtn.checked = false;
					selAllBtn.indeterminate = false;
				}
				updateBulkUi();
			});
		}
	}

	var selectAllDelegationBound = false;

	function bindSelectAllDelegation() {
		if (selectAllDelegationBound) {
			return;
		}
		selectAllDelegationBound = true;
		document.addEventListener('change', function (e) {
			var target = e.target;
			if (!target || target.id !== 'epc-disc-select-all') {
				return;
			}
			var on = !!target.checked;
			qsa('.epc-disc-card__check').forEach(function (cb) { cb.checked = on; });
			updateBulkUi();
		});
	}

	function initDiscoverTab() {
		if (initialized || !shouldInitDiscover()) {
			return;
		}
		initialized = true;
		bindSelectAllDelegation();
		bindToolbarActions();
		bindCheckboxes();
		bindCategoryControls();
		loadAllAdvisories();
		refreshRelativeTimestamps();
		setInterval(refreshRelativeTimestamps, 60000);
		loadDiscoverCounts();
		if (qs('#epc-disc-crawl-progress')) {
			pollCrawlStatus(0);
		}
	}

	function boot() {
		initDiscoverTab();
	}

	if (document.readyState === 'loading') {
		document.addEventListener('DOMContentLoaded', boot);
	} else {
		boot();
	}

	document.addEventListener('epc-apai-tab-loaded', function () {
		initialized = false;
		initDiscoverTab();
	});
})();
