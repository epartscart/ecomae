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
	var industrySelect = document.getElementById('epc_ps_industry');

	function parseJsonResponse(r) {
		return r.text().then(function (text) {
			try { return JSON.parse(text); }
			catch (e) { throw new Error(text ? text.substring(0, 200) : ('HTTP ' + r.status)); }
		});
	}

	function loadGroupItems(groupId, container) {
		if (menuItemsCache[groupId]) {
			renderGroupItems(groupId, container, menuItemsCache[groupId]);
			return;
		}
		var fd = new FormData();
		fd.append('action', 'menu_items');
		fd.append('group_id', groupId);
		fetch(ajaxUrl, { method: 'POST', body: fd, credentials: 'same-origin' })
			.then(parseJsonResponse)
			.then(function (data) {
				if (!data.status) return;
				menuItemsCache[groupId] = data.items || [];
				renderGroupItems(groupId, container, menuItemsCache[groupId]);
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
			var checked = hiddenItems.indexOf(it.id) === -1 ? ' checked' : '';
			html += '<label style="display:block;font-weight:normal;font-size:12px;"><input type="checkbox" class="epc-cp-item-toggle" data-item-id="' + it.id + '" data-group-id="' + groupId + '"' + checked + '> ' + it.label + '</label>';
		}
		container.innerHTML = html;
	}

	document.querySelectorAll('.epc-cp-group-items').forEach(function (box) {
		var gid = parseInt(box.getAttribute('data-group-id'), 10);
		if (gid > 0) loadGroupItems(gid, box);
	});

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
			html += '<label class="epc-portal-settings__style' + (sel ? ' is-selected' : '') + '" data-template-id="' + tid + '">';
			html += '<input type="radio" name="theme_template_pick" value="' + tid + '"' + (sel ? ' checked' : '') + ' />';
			html += '<span class="epc-portal-settings__style-swatches" aria-hidden="true">';
			html += '<i style="background:' + (t.primary || '#2563eb') + '"></i>';
			html += '<i style="background:' + (t.accent || '#38bdf8') + '"></i>';
			html += '<i style="background:linear-gradient(135deg,' + (t.sidebar_from || '#0f172a') + ',' + (t.sidebar_to || '#1e293b') + ')"></i>';
			html += '<i class="epc-portal-settings__style-hero" style="background:linear-gradient(145deg,' + (t.hero_from || '#0b1220') + ',' + (t.hero_to || '#1e3a5f') + ')"></i>';
			html += '</span><span class="epc-portal-settings__style-text"><strong>' + (tpl.label || tid) + '</strong><small>' + (tpl.desc || '') + '</small></span></label>';
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

	function applyErpPreset(presetId) {
		if (!presetId || !erpModulePresets[presetId] || !erpModulePresets[presetId].modules) return;
		var mods = erpModulePresets[presetId].modules;
		form.querySelectorAll('.epc-erp-mod-cb').forEach(function (cb) {
			cb.checked = mods.indexOf(cb.value) !== -1;
		});
	}

	if (industrySelect) {
		renderStyleTemplates(industrySelect.value, cfg.activeThemeTemplate || 'classic');
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
