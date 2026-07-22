(function () {
	'use strict';
	var cfg = window.EPC_PS || {};
	var form = document.getElementById('epc-portal-settings-form');
	if (!form) return;

	var msg = document.getElementById('epc-portal-settings-msg');
	var ajaxUrl = cfg.ajaxUrl || '';
	var menuItemsCache = {};
	var hiddenGroups = cfg.hiddenGroups || [];
	var hiddenItems = cfg.hiddenItems || [];
	var erpModulePresets = cfg.erpModulePresets || {};
	var industryDefaults = cfg.industryDefaults || {};
	var industryErpDefaults = cfg.industryErpDefaults || {};
	var styleTemplatesAll = cfg.styleTemplatesAll || {};
	var styleTemplateInput = document.getElementById('epc_ps_theme_template');
	var styleTemplatesBox = document.getElementById('epc-style-templates');
	var storefrontLayouts = cfg.storefrontLayouts || {};
	var layoutInput = document.getElementById('epc_ps_storefront_layout');
	var layoutsBox = document.getElementById('epc-storefront-layouts');
	var industrySelect = document.getElementById('epc_ps_industry');

	function esc(s) {
		return String(s == null ? '' : s)
			.replace(/&/g, '&amp;')
			.replace(/</g, '&lt;')
			.replace(/>/g, '&gt;')
			.replace(/"/g, '&quot;')
			.replace(/'/g, '&#39;');
	}

	function parseJsonResponse(r) {
		return r.text().then(function (text) {
			try { return JSON.parse(text); }
			catch (e) { throw new Error(text ? text.substring(0, 200) : ('HTTP ' + r.status)); }
		});
	}

	function shortUrl(url) {
		url = String(url || '');
		url = url.replace(/^\/?<backend>\//, '').replace(/^\/?(cp|backend)\//, '');
		return url.length > 42 ? url.slice(0, 40) + '…' : url;
	}

	function loadGroupItems(groupId, container) {
		if (menuItemsCache[groupId]) {
			renderGroupItems(groupId, container, menuItemsCache[groupId]);
			return;
		}
		container.innerHTML = '<small class="text-muted">Loading…</small>';
		var fd = new FormData();
		fd.append('action', 'menu_items');
		fd.append('group_id', groupId);
		fetch(ajaxUrl, { method: 'POST', body: fd, credentials: 'same-origin' })
			.then(parseJsonResponse)
			.then(function (data) {
				if (!data.status) {
					container.innerHTML = '<small class="text-muted">Could not load links</small>';
					return;
				}
				menuItemsCache[groupId] = data.items || [];
				renderGroupItems(groupId, container, menuItemsCache[groupId]);
				applySidebarSearch();
			})
			.catch(function () {
				container.innerHTML = '<small class="text-muted">Could not load links</small>';
			});
	}

	function renderGroupItems(groupId, container, items) {
		if (!items.length) {
			container.innerHTML = '<small class="text-muted">No items</small>';
			return;
		}
		var html = '';
		for (var i = 0; i < items.length; i++) {
			var it = items[i];
			var checked = hiddenItems.indexOf(it.id) === -1 && hiddenItems.indexOf(String(it.id)) === -1 ? ' checked' : '';
			var label = it.label || ('Item ' + it.id);
			var hay = (label + ' ' + (it.url || '')).toLowerCase();
			html += '<label data-item-search="' + esc(hay) + '">';
			html += '<input type="checkbox" class="epc-cp-item-toggle" data-item-id="' + esc(it.id) + '" data-group-id="' + esc(groupId) + '"' + checked + '> ';
			html += '<span>' + esc(label) + '</span>';
			if (it.url) {
				html += '<span class="epc-inds-item-url" title="' + esc(it.url) + '">' + esc(shortUrl(it.url)) + '</span>';
			}
			html += '</label>';
		}
		container.innerHTML = html;
	}

	/* Accordion: open first two groups by default; lazy-load items when opened */
	document.querySelectorAll('[data-inds-acc]').forEach(function (acc, idx) {
		var body = acc.querySelector('[data-inds-acc-body]');
		var toggle = acc.querySelector('[data-inds-acc-toggle]');
		var gid = body ? parseInt(body.getAttribute('data-group-id'), 10) : 0;
		var loaded = false;
		function openAcc() {
			acc.classList.add('is-open');
			if (!loaded && gid > 0 && body) {
				loaded = true;
				loadGroupItems(gid, body);
			}
		}
		if (toggle) {
			toggle.addEventListener('click', function (e) {
				if (e.target && e.target.classList && e.target.classList.contains('epc-cp-group-toggle')) {
					return;
				}
				if (acc.classList.contains('is-open')) {
					acc.classList.remove('is-open');
				} else {
					openAcc();
				}
			});
		}
		if (idx < 2) {
			openAcc();
		}
	});

	var searchInput = document.querySelector('[data-inds-sidebar-search]');
	function applySidebarSearch() {
		var q = (searchInput && searchInput.value ? searchInput.value : '').toLowerCase().trim();
		document.querySelectorAll('[data-inds-acc]').forEach(function (acc) {
			var groupHay = (acc.getAttribute('data-search') || '').toLowerCase();
			var labels = acc.querySelectorAll('[data-item-search]');
			var anyItem = false;
			if (!labels.length) {
				var showGroup = !q || groupHay.indexOf(q) !== -1;
				acc.classList.toggle('is-hidden', !showGroup);
				return;
			}
			labels.forEach(function (lab) {
				var hay = (lab.getAttribute('data-item-search') || '').toLowerCase();
				var show = !q || hay.indexOf(q) !== -1 || groupHay.indexOf(q) !== -1;
				lab.classList.toggle('is-hidden', !show);
				if (show) anyItem = true;
			});
			var showAcc = !q || anyItem || groupHay.indexOf(q) !== -1;
			acc.classList.toggle('is-hidden', !showAcc);
			if (q && showAcc) {
				acc.classList.add('is-open');
			}
		});
	}
	if (searchInput) {
		searchInput.addEventListener('input', applySidebarSearch);
	}
	var expandBtn = document.querySelector('[data-inds-expand-all]');
	var collapseBtn = document.querySelector('[data-inds-collapse-all]');
	if (expandBtn) {
		expandBtn.addEventListener('click', function () {
			document.querySelectorAll('[data-inds-acc]').forEach(function (acc) {
				if (acc.classList.contains('is-hidden')) return;
				acc.classList.add('is-open');
				var body = acc.querySelector('[data-inds-acc-body]');
				var gid = body ? parseInt(body.getAttribute('data-group-id'), 10) : 0;
				if (body && gid > 0 && !menuItemsCache[gid]) {
					loadGroupItems(gid, body);
				}
			});
		});
	}
	if (collapseBtn) {
		collapseBtn.addEventListener('click', function () {
			document.querySelectorAll('[data-inds-acc]').forEach(function (acc) {
				acc.classList.remove('is-open');
			});
		});
	}

	/* Section nav active state */
	var navLinks = document.querySelectorAll('[data-inds-nav]');
	if (navLinks.length && 'IntersectionObserver' in window) {
		var sections = [];
		navLinks.forEach(function (a) {
			var id = (a.getAttribute('href') || '').replace(/^#/, '');
			var el = id ? document.getElementById(id) : null;
			if (el) sections.push({ link: a, el: el });
		});
		var io = new IntersectionObserver(function (entries) {
			entries.forEach(function (entry) {
				if (!entry.isIntersecting) return;
				navLinks.forEach(function (a) { a.classList.remove('is-active'); });
				sections.forEach(function (s) {
					if (s.el === entry.target) s.link.classList.add('is-active');
				});
			});
		}, { rootMargin: '-20% 0px -60% 0px', threshold: 0.01 });
		sections.forEach(function (s) { io.observe(s.el); });
	}

	document.querySelectorAll('.epc-erp-mod-preset').forEach(function (btn) {
		btn.addEventListener('click', function () {
			var pid = btn.getAttribute('data-preset');
			if (!erpModulePresets[pid] || !erpModulePresets[pid].modules) return;
			var mods = erpModulePresets[pid].modules;
			form.querySelectorAll('.epc-erp-mod-cb').forEach(function (cb) {
				cb.checked = mods.indexOf(cb.value) !== -1;
			});
		});
	});

	function renderStyleTemplates(industryCode, selectedId) {
		if (!styleTemplatesBox || !styleTemplatesAll[industryCode]) return;
		var list = styleTemplatesAll[industryCode];
		var html = '';
		var firstId = null;
		for (var tid in list) {
			if (!list.hasOwnProperty(tid)) continue;
			if (firstId === null) firstId = tid;
			var tpl = list[tid];
			var t = tpl.theme || {};
			var sel = (tid === selectedId);
			html += '<label class="epc-portal-settings__style' + (sel ? ' is-selected' : '') + '" data-template-id="' + esc(tid) + '">';
			html += '<input type="radio" name="theme_template_pick" value="' + esc(tid) + '"' + (sel ? ' checked' : '') + ' />';
			html += '<span class="epc-portal-settings__style-swatches" aria-hidden="true">';
			html += '<i style="background:' + esc(t.primary || '#2563eb') + '"></i>';
			html += '<i style="background:' + esc(t.accent || '#38bdf8') + '"></i>';
			html += '<i style="background:linear-gradient(135deg,' + esc(t.sidebar_from || '#0f172a') + ',' + esc(t.sidebar_to || '#1e293b') + ')"></i>';
			html += '<i class="epc-portal-settings__style-hero" style="background:linear-gradient(145deg,' + esc(t.hero_from || '#0b1220') + ',' + esc(t.hero_to || '#1e3a5f') + ')"></i>';
			html += '</span><span class="epc-portal-settings__style-text"><strong>' + esc(tpl.label || tid) + '</strong><small>' + esc(tpl.desc || '') + '</small></span></label>';
		}
		styleTemplatesBox.innerHTML = html;
		var useId = (selectedId && list[selectedId]) ? selectedId : firstId;
		if (styleTemplateInput) styleTemplateInput.value = useId || 'classic';
		styleTemplatesBox.querySelectorAll('.epc-portal-settings__style').forEach(function (lbl) {
			lbl.addEventListener('click', function () {
				var id = lbl.getAttribute('data-template-id');
				if (styleTemplateInput) styleTemplateInput.value = id;
				styleTemplatesBox.querySelectorAll('.epc-portal-settings__style').forEach(function (x) { x.classList.remove('is-selected'); });
				lbl.classList.add('is-selected');
			});
		});
	}

	function renderStorefrontLayouts(industryCode, selectedId) {
		if (!layoutsBox || !storefrontLayouts[industryCode]) {
			if (layoutsBox) layoutsBox.innerHTML = '<p class="text-muted" style="margin:0">No layout templates for this industry yet — package default applies.</p>';
			return;
		}
		var list = storefrontLayouts[industryCode];
		var html = '';
		var firstId = null;
		var icons = {hero_carousel:'\uf1de',category_grid:'\uf009',product_showcase:'\uf00a',brand_focused:'\uf02a',editorial:'\uf1ea',collection_grid:'\uf009',minimal_boutique:'\uf10c',trend_feed:'\uf1e0',luxury_showcase:'\uf219',collection_gallery:'\uf03e',catalog_filter:'\uf0b0',editorial_luxury:'\uf1ea',professional_services:'\uf0b1',calculator_led:'\uf1ec',corporate_clean:'\uf19c'};
		for (var i = 0; i < list.length; i++) {
			var lay = list[i];
			if (firstId === null) firstId = lay.id;
			var sel = (lay.id === selectedId);
			var dflt = lay['default'] ? ' <small style="color:#16a34a">(default)</small>' : '';
			var icon = icons[lay.id] || '\uf009';
			html += '<label class="epc-portal-settings__style' + (sel ? ' is-selected' : '') + '" data-layout-id="' + esc(lay.id) + '">';
			html += '<input type="radio" name="storefront_layout_pick" value="' + esc(lay.id) + '"' + (sel ? ' checked' : '') + ' />';
			html += '<span class="epc-portal-settings__style-swatches" aria-hidden="true">';
			html += '<i title="Layout" style="background:#475569;font-style:normal;display:flex;align-items:center;justify-content:center;font-size:16px;color:#fff;width:32px;height:32px;border-radius:6px;">' + icon + '</i>';
			html += '</span><span class="epc-portal-settings__style-text"><strong>' + esc(lay.label || lay.id) + dflt + '</strong><small>' + esc(lay.desc || '') + '</small></span></label>';
		}
		layoutsBox.innerHTML = html;
		var useId = selectedId;
		var found = false;
		for (var j = 0; j < list.length; j++) { if (list[j].id === useId) { found = true; break; } }
		if (!found) useId = firstId;
		if (layoutInput) layoutInput.value = useId || '';
		layoutsBox.querySelectorAll('.epc-portal-settings__style').forEach(function (lbl) {
			lbl.addEventListener('click', function () {
				var id = lbl.getAttribute('data-layout-id');
				if (layoutInput) layoutInput.value = id;
				layoutsBox.querySelectorAll('.epc-portal-settings__style').forEach(function (x) { x.classList.remove('is-selected'); });
				lbl.classList.add('is-selected');
			});
		});
	}

	function applyErpPreset(presetId) {
		if (!presetId || !erpModulePresets[presetId] || !erpModulePresets[presetId].modules) return;
		var mods = erpModulePresets[presetId].modules;
		form.querySelectorAll('.epc-erp-mod-cb').forEach(function (cb) {
			cb.checked = mods.indexOf(cb.value) !== -1;
		});
	}

	if (industrySelect) {
		renderStyleTemplates(industrySelect.value, cfg.activeThemeTemplate || 'classic');
		renderStorefrontLayouts(industrySelect.value, cfg.activeStorefrontLayout || '');
		industrySelect.addEventListener('change', function () {
			var code = this.value;
			if (industryDefaults[code]) {
				var boxes = form.querySelectorAll('input[name="enabled_packs[]"]');
				for (var i = 0; i < boxes.length; i++) {
					if (boxes[i].disabled) continue;
					boxes[i].checked = industryDefaults[code].indexOf(boxes[i].value) !== -1;
				}
			}
			if (industryErpDefaults[code]) {
				applyErpPreset(industryErpDefaults[code]);
			}
			renderStyleTemplates(code, 'classic');
			renderStorefrontLayouts(code, '');
		});
	} else if (layoutsBox) {
		layoutsBox.querySelectorAll('.epc-portal-settings__style').forEach(function (lbl) {
			lbl.addEventListener('click', function () {
				var id = lbl.getAttribute('data-layout-id');
				if (layoutInput) layoutInput.value = id;
				layoutsBox.querySelectorAll('.epc-portal-settings__style').forEach(function (x) { x.classList.remove('is-selected'); });
				lbl.classList.add('is-selected');
			});
		});
	}

	/* Style template click binding for server-rendered cards */
	if (styleTemplatesBox) {
		styleTemplatesBox.querySelectorAll('.epc-portal-settings__style').forEach(function (lbl) {
			lbl.addEventListener('click', function () {
				var id = lbl.getAttribute('data-template-id');
				if (styleTemplateInput) styleTemplateInput.value = id;
				styleTemplatesBox.querySelectorAll('.epc-portal-settings__style').forEach(function (x) { x.classList.remove('is-selected'); });
				lbl.classList.add('is-selected');
			});
		});
	}
	if (layoutsBox) {
		layoutsBox.querySelectorAll('.epc-portal-settings__style').forEach(function (lbl) {
			lbl.addEventListener('click', function () {
				var id = lbl.getAttribute('data-layout-id');
				if (layoutInput) layoutInput.value = id;
				layoutsBox.querySelectorAll('.epc-portal-settings__style').forEach(function (x) { x.classList.remove('is-selected'); });
				lbl.classList.add('is-selected');
			});
		});
	}

	form.addEventListener('submit', function (e) {
		e.preventDefault();
		if (msg) msg.textContent = 'Saving…';
		var fd = new FormData(form);
		fd.append('action', 'save_settings');
		var hg = [], hi = [];
		form.querySelectorAll('.epc-cp-group-toggle').forEach(function (cb) {
			if (!cb.checked) hg.push(cb.value);
		});
		form.querySelectorAll('.epc-cp-item-toggle').forEach(function (cb) {
			if (!cb.checked) hi.push(cb.getAttribute('data-item-id'));
		});
		hg.forEach(function (v) { fd.append('hidden_groups[]', v); });
		hi.forEach(function (v) { fd.append('hidden_items[]', v); });
		fetch(ajaxUrl, { method: 'POST', body: fd, credentials: 'same-origin' })
			.then(parseJsonResponse)
			.then(function (data) {
				if (!msg) return;
				if (data.status) {
					msg.textContent = data.message || 'Saved. Reloading CP…';
					setTimeout(function () { location.reload(); }, 800);
				} else {
					msg.textContent = data.message || 'Save failed';
				}
			})
			.catch(function (err) {
				if (msg) msg.textContent = err.message || 'Network error';
			});
	});

	var seedBtn = document.getElementById('epc-seed-data-btn');
	if (seedBtn) {
		seedBtn.addEventListener('click', function () {
			if (msg) msg.textContent = 'Seeding demo products…';
			seedBtn.disabled = true;
			var fd = new FormData();
			fd.append('action', 'seed_storefront_data');
			fetch(ajaxUrl, { method: 'POST', body: fd, credentials: 'same-origin' })
				.then(parseJsonResponse)
				.then(function (data) {
					seedBtn.disabled = false;
					if (msg) msg.textContent = data.message || (data.status ? 'Done' : 'Failed');
				})
				.catch(function (err) {
					seedBtn.disabled = false;
					if (msg) msg.textContent = 'Seed failed: ' + (err.message || 'Network error');
				});
		});
	}

	if (cfg.showDeploy) {
		document.querySelectorAll('.epc-deploy-site-btn').forEach(function (btn) {
			btn.addEventListener('click', function () {
				var key = btn.getAttribute('data-site-key');
				var log = document.getElementById('epc-deploy-log');
				if (!log) return;
				log.style.display = 'block';
				log.textContent = 'Deploying to ' + key + '…';
				btn.disabled = true;
				var fd = new FormData();
				fd.append('action', 'deploy_site');
				fd.append('site_key', key);
				fetch(ajaxUrl, { method: 'POST', body: fd, credentials: 'same-origin' })
					.then(parseJsonResponse)
					.then(function (data) {
						btn.disabled = false;
						log.textContent = (data.message || '') + (data.log ? '\n' + data.log : '');
						if (data.status) location.reload();
					})
					.catch(function () {
						btn.disabled = false;
						log.textContent = 'Deploy request failed';
					});
			});
		});
	}
})();
