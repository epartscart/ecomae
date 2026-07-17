(function () {
	'use strict';

	var cfg = window.EPC_COMMERCE_CP || {};
	var ajaxUrl = cfg.ajaxUrl || '';
	var csrf = cfg.csrfKey || '';
	var backend = cfg.backend || 'cp';
	var pricesUrl = cfg.pricesUrl || ('/' + backend + '/shop/prices');

	function $(id) {
		return document.getElementById(id);
	}

	function escapeHtml(str) {
		return String(str == null ? '' : str)
			.replace(/&/g, '&amp;')
			.replace(/</g, '&lt;')
			.replace(/>/g, '&gt;')
			.replace(/"/g, '&quot;');
	}

	function roleClass(role) {
		role = String(role || '').toLowerCase();
		if (role === 'sales') return 'success';
		if (role === 'purchase') return 'warning';
		return 'info';
	}

	function formatUpdated(ts) {
		ts = parseInt(ts, 10) || 0;
		if (ts <= 0) return '—';
		var d = new Date(ts * 1000);
		if (isNaN(d.getTime())) return '—';
		var pad = function (n) { return n < 10 ? '0' + n : String(n); };
		return d.getFullYear() + '-' + pad(d.getMonth() + 1) + '-' + pad(d.getDate()) +
			' ' + pad(d.getHours()) + ':' + pad(d.getMinutes());
	}

	function formatNum(n) {
		n = parseInt(n, 10) || 0;
		try {
			return n.toLocaleString();
		} catch (e) {
			return String(n);
		}
	}

	function alertHtml(type, message) {
		return '<div class="alert alert-' + type + '">' + message + '</div>';
	}

	function renderLists(lists) {
		var html = '<div class="epc-commerce-result-lists"><ul>';
		(lists || []).forEach(function (item) {
			var ok = !!item.status;
			html += '<li class="' + (ok ? 'is-ok' : 'is-fail') + '">' +
				'<strong>' + escapeHtml(item.price_name || '') + '</strong> — ' +
				(ok ? 'OK' : 'FAIL') +
				', imported ' + formatNum(item.records_handled || 0) +
				', in DB ' + formatNum(item.records_in_db || 0) +
				(item.price_id ? ' <span class="epc-commerce-muted">#' + escapeHtml(item.price_id) + '</span>' : '') +
				(item.message ? '<br><small>' + escapeHtml(item.message) + '</small>' : '') +
				'</li>';
		});
		html += '</ul><p><a href="' + escapeHtml(pricesUrl) + '">Open price lists</a></p></div>';
		return html;
	}

	function renderSourceRow(src) {
		var priceId = parseInt(src.price_id, 10) || 0;
		var role = String(src.role || '');
		var hasUrl = !!src.has_url;
		var link = String(src.link || '');
		var rows = (typeof src.records_count !== 'undefined') ? formatNum(src.records_count) : '—';
		var urlCell = hasUrl
			? '<a class="epc-commerce-url" href="' + escapeHtml(link) + '" target="_blank" rel="noopener" title="' + escapeHtml(link) + '">' + escapeHtml(link) + '</a>'
			: '<span class="epc-commerce-muted">File upload only</span>';
		var refreshBtn = hasUrl
			? '<button type="button" class="btn btn-xs btn-primary epc-commerce-refresh-one" data-price-id="' + priceId + '"><i class="fas fa-cloud-download-alt"></i> Refresh</button> '
			: '';
		return '<tr data-price-id="' + priceId + '">' +
			'<td><strong>' + escapeHtml(src.price_name || '') + '</strong><br><small class="epc-commerce-muted">#' + priceId + '</small></td>' +
			'<td><span class="label label-' + roleClass(role) + '">' + escapeHtml(role || '—') + '</span></td>' +
			'<td class="epc-commerce-num">' + escapeHtml(src.margin_percent != null ? src.margin_percent : '0') + '%</td>' +
			'<td class="epc-commerce-num">' + escapeHtml(rows) + '</td>' +
			'<td class="epc-commerce-url-cell">' + urlCell + '</td>' +
			'<td class="epc-commerce-num">' + escapeHtml(formatUpdated(src.last_updated)) + '</td>' +
			'<td class="text-right epc-commerce-row-actions">' +
			refreshBtn +
			'<a class="btn btn-xs btn-default" href="/' + escapeHtml(backend) + '/shop/prices/price?price_id=' + priceId + '">Open</a>' +
			'</td></tr>';
	}

	function bindRefreshButtons(root) {
		(root || document).querySelectorAll('.epc-commerce-refresh-one').forEach(function (el) {
			if (el.getAttribute('data-epc-bound') === '1') return;
			el.setAttribute('data-epc-bound', '1');
			el.addEventListener('click', function () {
				refreshOne(el);
			});
		});
	}

	function applyFilter() {
		var input = $('epcCommerceFilter');
		var body = $('epcCommerceSourcesBody');
		var countEl = $('epcCommerceListCount');
		if (!body) return;
		var q = input ? String(input.value || '').toLowerCase().trim() : '';
		var rows = body.querySelectorAll('tr[data-price-id]');
		var shown = 0;
		rows.forEach(function (tr) {
			var hay = (tr.textContent || '').toLowerCase();
			var ok = !q || hay.indexOf(q) !== -1;
			tr.style.display = ok ? '' : 'none';
			if (ok) shown++;
		});
		if (countEl) {
			var total = rows.length;
			countEl.textContent = q ? (shown + ' / ' + total + ' shown') : (total + ' list' + (total === 1 ? '' : 's'));
		}
	}

	function renderSources(sources) {
		var body = $('epcCommerceSourcesBody');
		if (!body) return;
		sources = sources || [];
		if (!sources.length) {
			body.innerHTML = '<tr class="epc-commerce-empty-row"><td colspan="7" class="text-muted">No commerce lists yet — import a sales / purchase / inventory file above.</td></tr>';
		} else {
			body.innerHTML = sources.map(renderSourceRow).join('');
			bindRefreshButtons(body);
		}
		applyFilter();
	}

	function postAction(fields) {
		var fd = new FormData();
		fd.append('csrf_guard_key', csrf);
		Object.keys(fields).forEach(function (k) {
			fd.append(k, fields[k]);
		});
		return fetch(ajaxUrl, { method: 'POST', body: fd, credentials: 'same-origin' })
			.then(function (r) { return r.json(); });
	}

	function reloadSources(showStatus) {
		var refreshOut = $('epcCommerceRefreshResult');
		if (!ajaxUrl) {
			if (refreshOut) refreshOut.innerHTML = alertHtml('danger', 'Commerce script did not load. Refresh the page.');
			return Promise.resolve();
		}
		if (showStatus && refreshOut) {
			refreshOut.innerHTML = alertHtml('info', '<span class="epc-commerce-spinner"></span> Reloading lists…');
		}
		return postAction({ action: 'list_sources' }).then(function (j) {
			if (!j || !j.status) {
				if (refreshOut) {
					refreshOut.innerHTML = alertHtml('danger', escapeHtml((j && j.message) || 'Could not reload lists'));
				}
				return;
			}
			renderSources(j.sources || []);
			if (showStatus && refreshOut) {
				refreshOut.innerHTML = alertHtml('success', 'Lists updated (' + formatNum(j.count || 0) + ').');
			}
		}).catch(function () {
			if (refreshOut) refreshOut.innerHTML = alertHtml('danger', 'Network error while reloading lists');
		});
	}

	function syncMarginVisibility() {
		var role = ($('epcCommerceRole') || {}).value || 'sales';
		var wrap = $('epcCommerceMarginWrap');
		if (!wrap) return;
		wrap.style.opacity = role === 'sales' ? '0.45' : '1';
		wrap.style.pointerEvents = role === 'sales' ? 'none' : '';
	}

	function refreshOne(el) {
		var refreshOut = $('epcCommerceRefreshResult');
		var id = el.getAttribute('data-price-id');
		el.disabled = true;
		if (refreshOut) {
			refreshOut.innerHTML = alertHtml('info', '<span class="epc-commerce-spinner"></span> Refreshing list #' + escapeHtml(id) + '…');
		}
		postAction({ action: 'refresh_url', price_id: id }).then(function (j) {
			el.disabled = false;
			if (!j || !j.status) {
				if (refreshOut) {
					refreshOut.innerHTML = alertHtml('danger', escapeHtml((j && j.message) || 'Refresh failed'));
				}
				return;
			}
			if (refreshOut) {
				refreshOut.innerHTML = alertHtml('success', escapeHtml(j.message || 'Refreshed') +
					' — rows ' + formatNum(j.source_rows || 0)) + renderLists(j.lists || []);
			}
			return reloadSources(false);
		}).catch(function () {
			el.disabled = false;
			if (refreshOut) refreshOut.innerHTML = alertHtml('danger', 'Network error');
		});
	}

	function init() {
		if (!ajaxUrl) return;

		var btn = $('epcCommerceSubmit');
		var out = $('epcCommerceResult');
		var refreshOut = $('epcCommerceRefreshResult');
		var roleEl = $('epcCommerceRole');
		var filterEl = $('epcCommerceFilter');

		if (roleEl) {
			roleEl.addEventListener('change', syncMarginVisibility);
			syncMarginVisibility();
		}
		if (filterEl) {
			filterEl.addEventListener('input', applyFilter);
		}

		if (btn && out) {
			btn.addEventListener('click', function () {
				var fileInput = $('epcCommerceFile');
				var url = (($('epcCommerceUrl') || {}).value || '').trim();
				var hasFile = fileInput && fileInput.files && fileInput.files.length;
				if (!hasFile && !url) {
					out.innerHTML = alertHtml('warning', 'Choose a file or paste a recurring file URL.');
					return;
				}
				var fd = new FormData();
				fd.append('csrf_guard_key', csrf);
				fd.append('action', 'upload');
				fd.append('role', ($('epcCommerceRole') || {}).value || 'sales');
				fd.append('base_name', ($('epcCommerceBase') || {}).value || 'EPC');
				fd.append('margin_percent', ($('epcCommerceMargin') || {}).value || '0');
				fd.append('source_url', url);
				if (hasFile) {
					fd.append('price_file', fileInput.files[0]);
				}
				btn.disabled = true;
				out.innerHTML = alertHtml('info', '<span class="epc-commerce-spinner"></span> Importing… large files can take a minute.');
				fetch(ajaxUrl, { method: 'POST', body: fd, credentials: 'same-origin' })
					.then(function (r) { return r.json(); })
					.then(function (j) {
						btn.disabled = false;
						if (!j || !j.status) {
							out.innerHTML = alertHtml('danger', escapeHtml((j && j.message) || 'Import failed'));
							return;
						}
						out.innerHTML = alertHtml('success', escapeHtml(j.message || 'OK') +
							' — source rows: ' + formatNum(j.source_rows || 0) +
							(j.ingest_mode ? ' (' + escapeHtml(j.ingest_mode) + ')' : '')) +
							renderLists(j.lists);
						if (fileInput) fileInput.value = '';
						reloadSources(false);
					}).catch(function () {
						btn.disabled = false;
						out.innerHTML = alertHtml('danger', 'Network error');
					});
			});
		}

		var refreshAll = $('epcCommerceRefreshAll');
		if (refreshAll && refreshOut) {
			refreshAll.addEventListener('click', function () {
				refreshAll.disabled = true;
				refreshOut.innerHTML = alertHtml('info', '<span class="epc-commerce-spinner"></span> Refreshing all linked URLs…');
				postAction({ action: 'refresh_all' }).then(function (j) {
					refreshAll.disabled = false;
					if (!j) {
						refreshOut.innerHTML = alertHtml('danger', 'Refresh failed');
						return;
					}
					var cls = j.status ? 'success' : 'warning';
					refreshOut.innerHTML = alertHtml(cls, escapeHtml(j.message || ''));
					return reloadSources(false);
				}).catch(function () {
					refreshAll.disabled = false;
					refreshOut.innerHTML = alertHtml('danger', 'Network error');
				});
			});
		}

		var reloadBtn = $('epcCommerceReloadSources');
		if (reloadBtn) {
			reloadBtn.addEventListener('click', function () {
				reloadSources(true);
			});
		}

		bindRefreshButtons(document);
		applyFilter();
	}

	if (document.readyState === 'loading') {
		document.addEventListener('DOMContentLoaded', init);
	} else {
		init();
	}
})();
