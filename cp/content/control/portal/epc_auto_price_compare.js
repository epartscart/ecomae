(function () {
	'use strict';

	var MAX_SELECTED = 10;
	var pollTimer = null;
	var selected = {};
	var listState = { page: 1, total: 0, perPage: 50, priceListId: 0, search: '' };

	function cfg() {
		return window.EPC_APAI_COMPARE || {};
	}

	function shellCfg() {
		return window.EPC_APAI_SHELL || {};
	}

	function shouldInit() {
		var c = cfg();
		if (!c.ajaxUrl) {
			return false;
		}
		if (c.active) {
			return true;
		}
		return !!document.getElementById('epc-wh-compare');
	}

	function qs(sel, root) {
		return (root || document).querySelector(sel);
	}

	function qsa(sel, root) {
		return Array.prototype.slice.call((root || document).querySelectorAll(sel));
	}

	function ajaxUrl() {
		var c = cfg();
		var url = c.ajaxUrl || shellCfg().ajaxUrl || '/cp/control/portal/ajax_auto_price';
		if (url.charAt(0) !== '/' && !/^https?:\/\//i.test(url)) {
			url = '/' + url.replace(/^\/+/, '');
		}
		return url;
	}

	function postAction(action, payload) {
		var fd = new FormData();
		fd.append('action', action);
		fd.append('site_key', cfg().siteKey || shellCfg().siteKey || '');
		Object.keys(payload || {}).forEach(function (key) {
			var val = payload[key];
			if (val === undefined || val === null) {
				return;
			}
			if (Array.isArray(val)) {
				val.forEach(function (item) {
					fd.append(key + '[]', item);
				});
			} else if (typeof val === 'object') {
				fd.append(key, JSON.stringify(val));
			} else {
				fd.append(key, val);
			}
		});
		return fetch(ajaxUrl(), {
			method: 'POST',
			body: fd,
			credentials: 'same-origin',
			headers: { Accept: 'application/json' }
		}).then(function (r) {
			return r.text().then(function (text) {
				var data;
				try {
					data = JSON.parse(text);
				} catch (e) {
					throw new Error(text.slice(0, 180) || 'Invalid response');
				}
				if (!r.ok || data.ok === false) {
					throw new Error(data.message || data.error || 'Request failed');
				}
				return data;
			});
		});
	}

	function fmtMoney(n) {
		var v = parseFloat(n);
		if (!isFinite(v) || v <= 0) {
			return '—';
		}
		return v.toFixed(2);
	}

	function escHtml(s) {
		return String(s || '')
			.replace(/&/g, '&amp;')
			.replace(/</g, '&lt;')
			.replace(/>/g, '&gt;')
			.replace(/"/g, '&quot;');
	}

	function selectedCount() {
		return Object.keys(selected).length;
	}

	function updateSelectedUi() {
		var count = selectedCount();
		var countEl = qs('#epc-wh-selected-count');
		var btn = qs('#epc-wh-compare-btn');
		var chips = qs('#epc-wh-selected-chips');
		if (countEl) {
			countEl.textContent = String(count);
		}
		if (btn) {
			btn.disabled = count <= 0;
		}
		if (!chips) {
			return;
		}
		if (!count) {
			chips.innerHTML = '<span class="text-muted" style="font-size:12px">No items selected.</span>';
			return;
		}
		chips.innerHTML = Object.keys(selected).map(function (key) {
			var row = selected[key];
			return '<span class="label label-default" style="margin:0 6px 6px 0;display:inline-block">' +
				escHtml(row.brand) + ' · ' + escHtml(row.article_show || row.article) +
				' <a href="#" data-wh-remove="' + escHtml(key) + '" style="color:#fff;margin-left:4px">&times;</a></span>';
		}).join('');
	}

	function syncPageChecks() {
		qsa('#epc-wh-list-body .epc-wh-row-check').forEach(function (cb) {
			var key = cb.getAttribute('data-key') || '';
			cb.checked = !!selected[key];
		});
		var pageCb = qs('#epc-wh-check-page');
		if (!pageCb) {
			return;
		}
		var rows = qsa('#epc-wh-list-body .epc-wh-row-check');
		pageCb.checked = rows.length > 0 && rows.every(function (cb) { return cb.checked; });
	}

	function toggleSelect(row, on) {
		var key = row.brand_article_key;
		if (!key) {
			return;
		}
		if (on) {
			if (selected[key]) {
				return;
			}
			if (selectedCount() >= MAX_SELECTED) {
				window.alert('Select at most ' + MAX_SELECTED + ' warehouse items to compare with market.');
				syncPageChecks();
				return;
			}
			selected[key] = row;
		} else {
			delete selected[key];
		}
		updateSelectedUi();
		syncPageChecks();
	}

	function renderList(data) {
		var body = qs('#epc-wh-list-body');
		var summary = qs('#epc-wh-list-summary');
		var meta = qs('#epc-wh-page-meta');
		if (!body) {
			return;
		}
		listState.total = parseInt(data.total, 10) || 0;
		listState.page = parseInt(data.page, 10) || 1;
		listState.perPage = parseInt(data.per_page, 10) || 50;
		if (summary) {
			summary.innerHTML = '<strong>' + listState.total.toLocaleString() + '</strong> warehouse items — select up to ' + MAX_SELECTED + ' to compare with market';
		}
		if (!data.items || !data.items.length) {
			body.innerHTML = '<tr><td colspan="6" class="text-muted text-center">No warehouse rows match this filter.</td></tr>';
		} else {
			body.innerHTML = data.items.map(function (row) {
				var key = row.brand_article_key || '';
				var checked = selected[key] ? ' checked' : '';
				return '<tr>' +
					'<td><input type="checkbox" class="epc-wh-row-check" data-key="' + escHtml(key) + '"' + checked + ' /></td>' +
					'<td>' + escHtml(row.brand) + '</td>' +
					'<td>' + escHtml(row.article_show || row.article) + '</td>' +
					'<td>' + escHtml(row.warehouse || '—') + '</td>' +
					'<td>' + fmtMoney(row.cost) + '</td>' +
					'<td>' + (row.in_catalogue ? '<span class="label label-success">Yes</span>' : '<span class="text-muted">No</span>') + '</td>' +
					'</tr>';
			}).join('');
		}
		if (meta) {
			var pages = Math.max(1, Math.ceil(listState.total / listState.perPage));
			meta.textContent = 'Page ' + listState.page + ' / ' + pages;
		}
		renderPagination();
		qsa('#epc-wh-list-body .epc-wh-row-check').forEach(function (cb) {
			cb.addEventListener('change', function () {
				var key = cb.getAttribute('data-key') || '';
				var row = (data.items || []).find(function (item) { return item.brand_article_key === key; }) || selected[key];
				if (!row) {
					cb.checked = false;
					return;
				}
				toggleSelect(row, cb.checked);
			});
		});
		syncPageChecks();
	}

	function renderPagination() {
		var ul = qs('#epc-wh-pagination');
		if (!ul) {
			return;
		}
		var pages = Math.max(1, Math.ceil(listState.total / listState.perPage));
		if (pages <= 1) {
			ul.innerHTML = '';
			return;
		}
		var html = '';
		var cur = listState.page;
		if (cur > 1) {
			html += '<li><a href="#" data-wh-page="' + (cur - 1) + '">&laquo;</a></li>';
		}
		var start = Math.max(1, cur - 2);
		var end = Math.min(pages, cur + 2);
		for (var p = start; p <= end; p++) {
			html += '<li' + (p === cur ? ' class="active"' : '') + '><a href="#" data-wh-page="' + p + '">' + p + '</a></li>';
		}
		if (cur < pages) {
			html += '<li><a href="#" data-wh-page="' + (cur + 1) + '">&raquo;</a></li>';
		}
		ul.innerHTML = html;
	}

	function loadList(page) {
		listState.page = page || listState.page || 1;
		var body = qs('#epc-wh-list-body');
		if (body) {
			body.innerHTML = '<tr><td colspan="6" class="text-muted text-center"><i class="fa fa-spinner fa-spin"></i> Loading…</td></tr>';
		}
		return postAction('warehouse_list', {
			page: listState.page,
			per_page: cfg().perPage || 50,
			price_list_id: listState.priceListId || 0,
			search: listState.search || ''
		}).then(renderList).catch(function (err) {
			if (body) {
				body.innerHTML = '<tr><td colspan="6" class="text-danger text-center">' + escHtml(err.message || 'Load failed') + '</td></tr>';
			}
		});
	}

	function renderResults(results) {
		var wrap = qs('#epc-wh-results-wrap');
		var body = qs('#epc-wh-results-body');
		if (!wrap || !body) {
			return;
		}
		if (!results || !results.length) {
			wrap.style.display = 'none';
			body.innerHTML = '';
			return;
		}
		wrap.style.display = '';
		body.innerHTML = results.map(function (row) {
			var advice = row.pricing_advice || {};
			var advCls = advice.level === 'danger' ? 'danger' : (advice.level === 'warning' ? 'warning' : (advice.level === 'success' ? 'success' : 'default'));
			var margin = row.margin_abs && parseFloat(row.margin_abs) !== 0
				? fmtMoney(row.margin_abs) + ' (' + fmtMoney(row.margin_pct) + '%)'
				: '—';
			return '<tr>' +
				'<td><strong>' + escHtml(row.brand) + '</strong> · ' + escHtml(row.article_show || row.article) + '</td>' +
				'<td>' + fmtMoney(row.warehouse_cost) + '</td>' +
				'<td class="epc-ape-cell-lowest">' + fmtMoney(row.market_min) + '</td>' +
				'<td class="epc-ape-cell-highest">' + fmtMoney(row.market_max) + '</td>' +
				'<td>' + margin + '</td>' +
				'<td>' + (advice.badge ? '<span class="label label-' + advCls + '">' + escHtml(advice.badge) + '</span>' : '—') + '</td>' +
				'</tr>';
		}).join('');
	}

	function renderPrevious(rows) {
		var body = qs('#epc-wh-previous-body');
		var labels = cfg().badgeLabels || {};
		if (!body) {
			return;
		}
		if (!rows || !rows.length) {
			return;
		}
		body.innerHTML = rows.slice(0, 10).map(function (row) {
			var badge = row.badge || 'no_market_data';
			var badgeCls = badge === 'good_margin' ? 'success' : (badge === 'below_market' ? 'info' : (badge === 'over_market' ? 'danger' : 'default'));
			return '<tr>' +
				'<td><strong>' + escHtml(row.brand) + '</strong> · ' + escHtml(row.article_show || row.article) + '</td>' +
				'<td>' + fmtMoney(row.warehouse_cost) + '</td>' +
				'<td class="epc-ape-cell-lowest">' + fmtMoney(row.market_min) + '</td>' +
				'<td class="epc-ape-cell-highest">' + fmtMoney(row.market_max) + '</td>' +
				'<td>' + fmtMoney(row.margin_abs) + '</td>' +
				'<td><span class="label label-' + badgeCls + '">' + escHtml(labels[badge] || badge) + '</span></td>' +
				'</tr>';
		}).join('');
	}

	function setProgress(done, total, message) {
		var box = qs('#epc-wh-compare-progress');
		var bar = qs('#epc-wh-progress-bar');
		var msg = qs('#epc-wh-progress-msg');
		if (box) {
			box.style.display = '';
		}
		var pct = total > 0 ? Math.round((done / total) * 100) : 0;
		if (bar) {
			bar.style.width = pct + '%';
		}
		if (msg) {
			msg.textContent = message || ('Compared ' + done + ' / ' + total);
		}
	}

	function clearPoll() {
		if (pollTimer) {
			window.clearTimeout(pollTimer);
			pollTimer = null;
		}
	}

	function pollJob(jobId) {
		clearPoll();
		pollTimer = window.setTimeout(function () {
			postAction('job_status', { job_id: jobId, job_type: 'warehouse_compare' })
				.then(function (data) {
					setProgress(parseInt(data.done, 10) || 0, parseInt(data.total, 10) || MAX_SELECTED, data.message || '');
					if (data.results) {
						renderResults(data.results);
					}
					if (data.recent) {
						renderPrevious(data.recent);
					}
					if (data.status === 'done' || data.status === 'idle') {
						clearPoll();
						var btn = qs('#epc-wh-compare-btn');
						if (btn) {
							btn.disabled = selectedCount() <= 0;
							btn.innerHTML = '<i class="fa fa-balance-scale"></i> Compare selected with market';
						}
						if (data.status === 'done') {
							postAction('job_status', { job_type: 'warehouse_compare' }).then(function (idleData) {
								if (idleData.recent) {
									renderPrevious(idleData.recent);
								}
							}).catch(function () {});
						}
						return;
					}
					pollJob(jobId);
				})
				.catch(function (err) {
					clearPoll();
					window.alert(err.message || 'Compare job failed');
					var btn = qs('#epc-wh-compare-btn');
					if (btn) {
						btn.disabled = selectedCount() <= 0;
						btn.innerHTML = '<i class="fa fa-balance-scale"></i> Compare selected with market';
					}
				});
		}, 800);
	}

	function startCompare() {
		var count = selectedCount();
		if (count <= 0) {
			window.alert('Select at least one warehouse item.');
			return;
		}
		if (count > MAX_SELECTED) {
			window.alert('Select at most ' + MAX_SELECTED + ' items to compare with market.');
			return;
		}
		var keys = Object.keys(selected);
		var context = {};
		keys.forEach(function (key) {
			context[key] = selected[key];
		});
		var btn = qs('#epc-wh-compare-btn');
		if (btn) {
			btn.disabled = true;
			btn.innerHTML = '<i class="fa fa-spinner fa-spin"></i> Starting…';
		}
		setProgress(0, keys.length, 'Starting market compare…');
		postAction('warehouse_compare_selected', { keys: keys, context: context })
			.then(function (data) {
				renderResults(data.results || []);
				if (data.status === 'done') {
					setProgress(parseInt(data.done, 10) || keys.length, parseInt(data.total, 10) || keys.length, data.message || 'Done');
					if (btn) {
						btn.disabled = selectedCount() <= 0;
						btn.innerHTML = '<i class="fa fa-balance-scale"></i> Compare selected with market';
					}
					postAction('job_status', { job_type: 'warehouse_compare' }).then(function (idleData) {
						if (idleData.recent) {
							renderPrevious(idleData.recent);
						}
					}).catch(function () {});
					return;
				}
				pollJob(data.job_id);
			})
			.catch(function (err) {
				window.alert(err.message || 'Could not start compare job');
				if (btn) {
					btn.disabled = selectedCount() <= 0;
					btn.innerHTML = '<i class="fa fa-balance-scale"></i> Compare selected with market';
				}
			});
	}

	function bindEvents() {
		var filter = qs('#epc-wh-filter-pl');
		var search = qs('#epc-wh-search');
		var reload = qs('#epc-wh-reload');
		var compareBtn = qs('#epc-wh-compare-btn');
		var pageCb = qs('#epc-wh-check-page');
		var chips = qs('#epc-wh-selected-chips');
		var pagination = qs('#epc-wh-pagination');

		if (filter) {
			filter.addEventListener('change', function () {
				listState.priceListId = parseInt(filter.value, 10) || 0;
				listState.page = 1;
				loadList(1);
			});
		}
		if (search) {
			var searchTimer = null;
			search.addEventListener('input', function () {
				window.clearTimeout(searchTimer);
				searchTimer = window.setTimeout(function () {
					listState.search = search.value.trim();
					listState.page = 1;
					loadList(1);
				}, 350);
			});
		}
		if (reload) {
			reload.addEventListener('click', function () {
				loadList(listState.page);
			});
		}
		if (compareBtn) {
			compareBtn.addEventListener('click', startCompare);
		}
		if (pageCb) {
			pageCb.addEventListener('change', function () {
				qsa('#epc-wh-list-body .epc-wh-row-check').forEach(function (cb) {
					var key = cb.getAttribute('data-key') || '';
					if (!key) {
						return;
					}
					if (pageCb.checked) {
						if (selectedCount() >= MAX_SELECTED && !selected[key]) {
							cb.checked = false;
							return;
						}
					}
					var row = selected[key];
					if (!row && pageCb.checked) {
						var tr = cb.closest('tr');
						var tds = tr ? tr.querySelectorAll('td') : [];
						row = {
							brand_article_key: key,
							brand: tds[1] ? tds[1].textContent.trim() : '',
							article: tds[2] ? tds[2].textContent.trim() : '',
							article_show: tds[2] ? tds[2].textContent.trim() : '',
							cost: tds[4] ? tds[4].textContent.trim() : '',
							warehouse: tds[3] ? tds[3].textContent.trim() : ''
						};
					}
					if (row) {
						toggleSelect(row, pageCb.checked);
					}
				});
			});
		}
		if (chips) {
			chips.addEventListener('click', function (e) {
				var link = e.target.closest('[data-wh-remove]');
				if (!link) {
					return;
				}
				e.preventDefault();
				delete selected[link.getAttribute('data-wh-remove') || ''];
				updateSelectedUi();
				syncPageChecks();
			});
		}
		if (pagination) {
			pagination.addEventListener('click', function (e) {
				var link = e.target.closest('[data-wh-page]');
				if (!link) {
					return;
				}
				e.preventDefault();
				loadList(parseInt(link.getAttribute('data-wh-page'), 10) || 1);
			});
		}
	}

	function init() {
		if (!shouldInit()) {
			return;
		}
		MAX_SELECTED = parseInt(cfg().maxSelected, 10) || 10;
		listState.perPage = parseInt(cfg().perPage, 10) || 50;
		bindEvents();
		updateSelectedUi();
		loadList(1);
	}

	document.addEventListener('epc-apai-tab-loaded', function (ev) {
		if (ev.detail && ev.detail.tab === 'compare') {
			init();
		}
	});

	if (document.getElementById('epc-wh-compare')) {
		init();
	}
})();
