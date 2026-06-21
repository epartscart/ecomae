(function () {
	'use strict';

	function importsCfg() {
		return window.EPC_APAI_IMPORTS || {};
	}

	function shouldInitImports() {
		var cfg = importsCfg();
		return cfg.active && cfg.ajaxUrl && document.getElementById('epc-imports-list');
	}

	function qs(sel) {
		return document.querySelector(sel);
	}

	function qsa(sel) {
		return Array.prototype.slice.call(document.querySelectorAll(sel));
	}

	function esc(s) {
		var d = document.createElement('div');
		d.textContent = s == null ? '' : String(s);
		return d.innerHTML;
	}

	function priceBadge(item) {
		if (!item.price_changed) {
			return '';
		}
		var pct = typeof item.price_diff_pct === 'number' ? item.price_diff_pct : 0;
		if (pct !== 0) {
			var arrow = pct < 0 ? '↓' : '↑';
			var label = 'Price ' + arrow + ' ' + Math.abs(pct).toFixed(1).replace(/\.0$/, '') + '%';
			var cls = pct < 0 ? 'epc-disc-price-badge--down' : 'epc-disc-price-badge--up';
			return '<span class="epc-disc-price-badge ' + cls + '">' + esc(label) + '</span>';
		}
		return '<span class="epc-disc-price-badge epc-disc-price-badge--changed">Price changed</span>';
	}

	function postAction(action, extra) {
		var cfg = importsCfg();
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
			.then(function (r) { return r.json(); });
	}

	function updateCountBadges(counts) {
		if (!counts) return;
		Object.keys(counts).forEach(function (k) {
			var el = document.querySelector('[data-imports-count="' + k + '"]');
			if (el) {
				el.textContent = String(counts[k]);
			}
		});
	}

	function partIdentityHtml(item) {
		if (!item.brand && !item.article_number && !item.needs_part_number) {
			return '';
		}
		if (item.brand || item.article_number) {
			return '<div class="epc-disc-part-identity">' +
				'<span class="epc-disc-part-identity__brand label label-primary">' + esc(item.brand || 'OEM') + '</span> ' +
				'<span class="epc-disc-part-identity__article">' + esc(item.article_number || '') + '</span></div>';
		}
		if (item.needs_part_number) {
			return '<div class="epc-disc-part-identity"><span class="label label-warning">Part number required</span></div>';
		}
		return '';
	}

	function renderItemCard(item, filter, siteKey) {
		var img = (item.images && item.images[0]) ? String(item.images[0]) : '';
		var imgHtml = img
			? '<div class="epc-disc-card__img" style="background-image:url(' + esc(img) + ')"></div>'
			: '<div class="epc-disc-card__img epc-disc-card__img--empty"><i class="fa fa-image"></i></div>';
		var checkHtml = filter === 'new'
			? '<label class="epc-disc-card__check-wrap"><input type="checkbox" class="epc-imports-card__check" value="' + item.id + '" /></label>'
			: '';
		var skuHtml = item.sku ? '<span class="label label-default">SKU ' + esc(item.sku) + '</span>' : '';
		var catHtml = item.catalogue_price > 0
			? '<p class="text-muted" style="font-size:11px;margin:0 0 6px">Catalogue: ' + parseFloat(item.catalogue_price).toFixed(2) + ' ' + esc(item.currency || '') + '</p>'
			: '';
		var actions = '';
		if (filter === 'new') {
			actions = '<form method="post" style="display:inline">' +
				'<input type="hidden" name="epc_ape_action" value="approve_discovery" />' +
				'<input type="hidden" name="site_key" value="' + esc(siteKey) + '" />' +
				'<input type="hidden" name="queue_id" value="' + item.id + '" />' +
				'<button type="submit" class="btn btn-success btn-sm"><i class="fa fa-plus"></i> Add to catalogue</button></form> ' +
				'<form method="post" style="display:inline">' +
				'<input type="hidden" name="epc_ape_action" value="reject_discovery" />' +
				'<input type="hidden" name="site_key" value="' + esc(siteKey) + '" />' +
				'<input type="hidden" name="return_tab" value="imports" />' +
				'<input type="hidden" name="queue_id" value="' + item.id + '" />' +
				'<button type="submit" class="btn btn-default btn-sm">Dismiss</button></form>';
		} else if (item.status === 'imported' && item.product_id > 0) {
			actions = '<a class="btn btn-default btn-sm" href="/cp/shop/catalogue/product?product_id=' + item.product_id + '">Catalogue #' + item.product_id + '</a>';
		}
		if (item.source_url) {
			actions += ' <a class="btn btn-link btn-sm" href="' + esc(item.source_url) + '" target="_blank" rel="noopener">View source</a>';
		}
		return '<div class="epc-disc-card" data-queue-id="' + item.id + '">' + checkHtml + imgHtml +
			'<div class="epc-disc-card__body">' + partIdentityHtml(item) + '<h5>' + esc(item.title) + '</h5>' +
			'<p class="epc-disc-card__meta"><span class="label label-info">' + esc(item.source_domain) + '</span> ' +
			(item.status === 'imported' ? '<span class="label label-success">Imported</span> ' : '') +
			skuHtml + ' ' + priceBadge(item) + '</p>' + catHtml +
			'<div class="epc-disc-card__prices"><div><small>Source price</small><strong>' +
			parseFloat(item.suggested_price || 0).toFixed(2) + '</strong> ' + esc(item.currency || '') +
			'</div><div><small>Sell @ margin</small><strong>' + parseFloat(item.sell_price || 0).toFixed(2) + '</strong></div></div>' +
			'<div class="epc-disc-card__actions">' + actions + '</div></div></div>';
	}

	function renderDupGroup(group, siteKey) {
		var items = group.items || [];
		var cards = items.map(function (item) {
			return renderItemCard(item, 'duplicates', siteKey)
				.replace('epc-disc-card"', 'epc-disc-card epc-imports-dup-card"')
				.replace('</div></div></div>', '<div class="epc-disc-card__actions">' +
					'<button type="button" class="btn btn-success btn-sm epc-imports-keep-btn" data-keep-id="' + item.id + '"><i class="fa fa-check"></i> Keep this one</button>' +
					(item.source_url ? ' <a class="btn btn-link btn-sm" href="' + esc(item.source_url) + '" target="_blank">View source</a>' : '') +
					'</div></div></div>');
		}).join('');
		var head = items[0] || {};
		var headLabel = head.brand_article_key
			? esc(head.brand || 'OEM') + ' · ' + esc(head.article_number || head.brand_article_key)
			: esc(head.title || '');
		return '<div class="epc-imports-dup-group" data-dup-key="' + esc(group.dup_key || '') + '">' +
			'<div class="epc-imports-dup-group__head"><span class="label label-warning">Duplicate ×' + (group.count || items.length) + '</span> ' +
			'<small class="text-muted">' + headLabel + '</small></div>' +
			'<div class="epc-disc-grid">' + cards + '</div></div>';
	}

	function renderList(data) {
		var filter = data.filter || 'new';
		var cfg = importsCfg();
		if (filter === 'duplicates') {
			var groups = data.groups || [];
			if (!groups.length) {
				return '<p class="text-muted epc-imports-empty">No duplicate import candidates.</p>';
			}
			return groups.map(function (g) { return renderDupGroup(g, cfg.siteKey); }).join('');
		}
		var items = data.items || [];
		if (!items.length) {
			return '<p class="text-muted epc-imports-empty">' +
				(filter === 'price_changes' ? 'No price changes — source prices match your catalogue.' : 'No new import candidates.') +
				'</p>';
		}
		var bulk = filter === 'new'
			? '<div class="epc-disc-bulk-bar epc-imports-bulk-bar" style="margin-bottom:12px">' +
				'<label><input type="checkbox" id="epc-imports-select-all" /> Select all</label> ' +
				'<button type="button" class="btn btn-success btn-sm" id="epc-imports-bulk-approve" disabled><i class="fa fa-plus"></i> Add selected (0)</button></div>'
			: '';
		return bulk + '<div class="epc-disc-grid" id="epc-imports-grid">' +
			items.map(function (item) { return renderItemCard(item, filter, cfg.siteKey); }).join('') + '</div>';
	}

	function loadFilter(filter, pushState) {
		var cfg = importsCfg();
		var listEl = document.getElementById('epc-imports-list');
		if (!listEl) return;
		listEl.innerHTML = '<p class="text-muted"><i class="fa fa-spinner fa-spin"></i> Loading…</p>';
		qsa('.epc-imports-subtabs__pill').forEach(function (pill) {
			pill.classList.toggle('epc-imports-subtabs__pill--active', pill.getAttribute('data-imports-filter') === filter);
		});
		postAction('list_my_imports', { filter: filter }).then(function (data) {
			if (!data.ok) {
				listEl.innerHTML = '<p class="text-danger">' + esc(data.message || 'Load failed') + '</p>';
				return;
			}
			listEl.setAttribute('data-filter', filter);
			listEl.innerHTML = renderList(data);
			updateCountBadges(data.counts);
			bindBulkUi();
			bindKeepButtons();
			if (pushState && cfg.pageBase) {
				var url = cfg.pageBase + '?site_key=' + encodeURIComponent(cfg.siteKey || '') + '&tab=imports&imports_filter=' + encodeURIComponent(filter);
				history.replaceState({ importsFilter: filter }, '', url);
			}
		}).catch(function (err) {
			listEl.innerHTML = '<p class="text-danger">' + esc(err.message || 'Load failed') + '</p>';
		});
	}

	function selectedIds() {
		return qsa('.epc-imports-card__check:checked').map(function (cb) {
			return parseInt(cb.value, 10);
		}).filter(function (id) { return id > 0; });
	}

	function bindBulkUi() {
		var bulkBtn = document.getElementById('epc-imports-bulk-approve');
		var selAll = document.getElementById('epc-imports-select-all');
		function refresh() {
			var ids = selectedIds();
			if (bulkBtn) {
				bulkBtn.disabled = ids.length === 0;
				bulkBtn.innerHTML = '<i class="fa fa-plus"></i> Add selected (' + ids.length + ')';
			}
			if (selAll) {
				var boxes = qsa('.epc-imports-card__check');
				selAll.checked = boxes.length > 0 && ids.length === boxes.length;
				selAll.indeterminate = ids.length > 0 && ids.length < boxes.length;
			}
		}
		qsa('.epc-imports-card__check').forEach(function (cb) {
			cb.addEventListener('change', refresh);
		});
		if (selAll) {
			selAll.addEventListener('change', function () {
				qsa('.epc-imports-card__check').forEach(function (cb) {
					cb.checked = selAll.checked;
				});
				refresh();
			});
		}
		if (bulkBtn) {
			bulkBtn.addEventListener('click', function () {
				var ids = selectedIds();
				if (!ids.length) return;
				bulkBtn.disabled = true;
				postAction('bulk_approve', { queue_ids: ids }).then(function (data) {
					alert(data.message || (data.ok ? 'Imported' : 'Failed'));
					loadFilter('new', false);
				}).catch(function (err) {
					alert(err.message || 'Bulk import failed');
				}).finally(function () {
					bulkBtn.disabled = false;
				});
			});
		}
		refresh();
	}

	function bindKeepButtons() {
		qsa('.epc-imports-keep-btn').forEach(function (btn) {
			btn.addEventListener('click', function () {
				var keepId = parseInt(btn.getAttribute('data-keep-id'), 10);
				if (!keepId) return;
				if (!window.confirm('Keep this variant and import it? Other duplicates in this group will be dismissed.')) {
					return;
				}
				btn.disabled = true;
				postAction('dismiss_duplicate', { keep_id: keepId, approve_keep: 1 }).then(function (data) {
					alert(data.message || (data.ok ? 'Done' : 'Failed'));
					updateCountBadges(data.counts);
					loadFilter('duplicates', false);
				}).catch(function (err) {
					alert(err.message || 'Failed');
				}).finally(function () {
					btn.disabled = false;
				});
			});
		});
	}

	function init() {
		if (!shouldInitImports()) return;
		qsa('.epc-imports-subtabs__pill[data-imports-filter]').forEach(function (pill) {
			pill.addEventListener('click', function (e) {
				e.preventDefault();
				loadFilter(pill.getAttribute('data-imports-filter'), true);
			});
		});
		bindBulkUi();
		bindKeepButtons();
	}

	if (document.readyState === 'loading') {
		document.addEventListener('DOMContentLoaded', init);
	} else {
		init();
	}
})();
