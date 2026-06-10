(function () {
	'use strict';

	var cfg = window.EPC_APAI_PRODUCT_LINES || {};
	if (!cfg.active) {
		return;
	}

	function qs(sel, root) {
		return (root || document).querySelector(sel);
	}

	function qsa(sel, root) {
		return Array.prototype.slice.call((root || document).querySelectorAll(sel));
	}

	function cpAbsUrl(path) {
		if (!path) {
			return '';
		}
		if (/^https?:\/\//i.test(path)) {
			return path;
		}
		var base = (cfg.backend ? '/' + String(cfg.backend).replace(/^\/+|\/+$/g, '') : '');
		return path.charAt(0) === '/' ? path : base + '/' + path;
	}

	function initTreeCollapse() {
		var tree = qs('#epc-pl-tax-tree');
		var btn = qs('#epc-pl-tree-collapse-btn');
		if (!tree || !btn) {
			return;
		}
		btn.addEventListener('click', function () {
			var collapsed = tree.classList.toggle('epc-pl-tree--collapsed');
			btn.setAttribute('aria-expanded', collapsed ? 'false' : 'true');
			btn.textContent = collapsed ? 'Expand tree' : 'Collapse tree';
		});
	}

	function initDiscoverLoading() {
		qsa('.epc-pl-inline-form').forEach(function (form) {
			form.addEventListener('submit', function () {
				var btn = form.querySelector('button[type="submit"]');
				if (btn && !btn.disabled) {
					btn.disabled = true;
					btn.dataset.origHtml = btn.innerHTML;
					btn.innerHTML = '<i class="fa fa-spinner fa-spin"></i> Discovering…';
				}
			});
		});
	}

	function initCardHighlight() {
		qsa('.epc-pl-ranked-card').forEach(function (card) {
			card.addEventListener('mouseenter', function () {
				var tid = card.getAttribute('data-taxonomy-id');
				if (!tid) {
					return;
				}
				qsa('.epc-tax-tree__list li').forEach(function (li) {
					li.classList.remove('epc-pl-tree-highlight');
				});
			});
		});
	}

	function initLoadPrices() {
		if (!cfg.ajaxUrl) {
			return;
		}
		qsa('.epc-pl-load-prices').forEach(function (btn) {
			btn.addEventListener('click', function () {
				var tid = btn.getAttribute('data-taxonomy-id');
				if (!tid || btn.disabled) {
					return;
				}
				btn.disabled = true;
				btn.textContent = 'Loading…';
				var fd = new FormData();
				fd.append('action', 'product_line_prices');
				fd.append('site_key', cfg.siteKey || '');
				fd.append('taxonomy_id', tid);
				fetch(cpAbsUrl(cfg.ajaxUrl), {
					method: 'POST',
					body: fd,
					credentials: 'same-origin',
					headers: { Accept: 'application/json' }
				})
					.then(function (r) {
						return r.json();
					})
					.then(function (data) {
						if (!data || !data.ok) {
							throw new Error('Price fetch failed');
						}
						var wrap = btn.closest('.epc-pl-market-range');
						if (wrap) {
							wrap.querySelector('strong').textContent = data.label || '—';
						}
					})
					.catch(function () {
						btn.disabled = false;
						btn.textContent = 'Retry prices';
					});
			});
		});
	}

	function initLoadMore() {
		var moreBtn = qs('#epc-pl-load-more');
		var grid = qs('#epc-pl-ranked-grid');
		if (!moreBtn || !grid || !cfg.ajaxUrl) {
			return;
		}
		moreBtn.addEventListener('click', function () {
			var nextPage = parseInt(moreBtn.getAttribute('data-next-page') || '2', 10);
			if (moreBtn.disabled) {
				return;
			}
			moreBtn.disabled = true;
			moreBtn.innerHTML = '<i class="fa fa-spinner fa-spin"></i> Loading…';
			var fd = new FormData();
			fd.append('action', 'load_tab_html');
			fd.append('site_key', cfg.siteKey || '');
			fd.append('tab', 'product_lines');
			fd.append('pl_page', String(nextPage));
			fetch(cpAbsUrl(cfg.ajaxUrl), {
				method: 'POST',
				body: fd,
				credentials: 'same-origin',
				headers: { Accept: 'application/json' }
			})
				.then(function (r) {
					return r.text().then(function (text) {
						var data = JSON.parse(text);
						if (!data || !data.ok || !data.html) {
							throw new Error(data && data.error ? data.error : 'Load failed');
						}
						var tmp = document.createElement('div');
						tmp.innerHTML = data.html;
						var newGrid = qs('#epc-pl-ranked-grid', tmp);
						if (!newGrid) {
							throw new Error('No cards in response');
						}
						qsa('.epc-pl-ranked-card', newGrid).forEach(function (card) {
							grid.appendChild(card);
						});
						var newMore = qs('#epc-pl-load-more', tmp);
						if (newMore) {
							moreBtn.setAttribute('data-next-page', newMore.getAttribute('data-next-page') || String(nextPage + 1));
							moreBtn.innerHTML = newMore.innerHTML;
							moreBtn.disabled = false;
						} else {
							moreBtn.parentNode.removeChild(moreBtn);
						}
						grid.setAttribute('data-pl-page', String(nextPage));
						initDiscoverLoading();
						initLoadPrices();
						initCardHighlight();
					});
				})
				.catch(function () {
					moreBtn.disabled = false;
					moreBtn.innerHTML = '<i class="fa fa-chevron-down"></i> Retry load more';
				});
		});
	}

	function initLoadTaxTree() {
		var loadBtn = qs('#epc-pl-load-tax-tree');
		var treeHost = qs('#epc-pl-tax-tree');
		if (!loadBtn || !treeHost || !cfg.ajaxUrl) {
			return;
		}
		loadBtn.addEventListener('click', function () {
			if (loadBtn.disabled) {
				return;
			}
			loadBtn.disabled = true;
			loadBtn.innerHTML = '<i class="fa fa-spinner fa-spin"></i> Loading taxonomy…';
			var fd = new FormData();
			fd.append('action', 'product_lines_tax_tree');
			fd.append('site_key', cfg.siteKey || '');
			fetch(cpAbsUrl(cfg.ajaxUrl), {
				method: 'POST',
				body: fd,
				credentials: 'same-origin',
				headers: { Accept: 'application/json' }
			})
				.then(function (r) {
					return r.json();
				})
				.then(function (data) {
					if (!data || !data.ok || !data.html) {
						throw new Error('Tree load failed');
					}
					treeHost.innerHTML = data.html;
					var heading = treeHost.closest('.hpanel');
					if (heading) {
						var h4 = heading.querySelector('.panel-heading h4');
						if (h4 && !qs('#epc-pl-tree-collapse-btn', heading)) {
							var collapseBtn = document.createElement('button');
							collapseBtn.type = 'button';
							collapseBtn.className = 'btn btn-default btn-xs pull-right';
							collapseBtn.id = 'epc-pl-tree-collapse-btn';
							collapseBtn.setAttribute('aria-expanded', 'true');
							collapseBtn.textContent = 'Collapse tree';
							h4.appendChild(collapseBtn);
						}
					}
					initTreeCollapse();
					initDiscoverLoading();
				})
				.catch(function () {
					loadBtn.disabled = false;
					loadBtn.innerHTML = '<i class="fa fa-sitemap"></i> Retry taxonomy tree';
				});
		});
	}

	initTreeCollapse();
	initDiscoverLoading();
	initCardHighlight();
	initLoadPrices();
	initLoadMore();
	initLoadTaxTree();
})();
