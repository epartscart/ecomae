(function () {
	'use strict';

	var root = document.getElementById('epc-vpe-app');
	if (!root || root.getAttribute('data-vpe-ready') === '1') {
		return;
	}
	root.setAttribute('data-vpe-ready', '1');

	var cfg = window.EPC_VPE || {};
	var ajaxUrl = cfg.ajaxUrl || '';
	var blockLibrary = cfg.blockLibrary || {};
	var levels = cfg.levels || {};
	var blocks = Array.isArray(cfg.blocks) ? cfg.blocks.slice() : [];
	var brand = Object.assign({}, cfg.brand || {});
	var siteKey = cfg.siteKey || 'platform';
	var pageKey = cfg.pageKey || root.getAttribute('data-page-key') || 'homepage';
	var levelId = cfg.levelId || root.getAttribute('data-level-id') || 'homepage';
	var mode = cfg.mode || root.getAttribute('data-mode') || 'layout';
	var dirty = false;
	var selectedIndex = 0;
	var dragIndex = null;

	var previewFrame = document.getElementById('epc-vpe-preview-frame');
	var previewWrap = document.getElementById('epc-vpe-preview-wrap');
	var blocksList = document.getElementById('epc-vpe-blocks');
	var statusEl = document.getElementById('epc-vpe-status');
	var levelLabelEl = document.getElementById('epc-vpe-level-label');
	var levelPill = document.getElementById('epc-vpe-level-pill');
	var blockCountEl = document.getElementById('epc-vpe-block-count');
	var inspectorTitle = document.getElementById('epc-vpe-inspector-title');
	var brandSection = document.getElementById('epc-vpe-brand-section');
	var blocksSection = document.getElementById('epc-vpe-blocks-section');
	var brandOnlyNote = document.getElementById('epc-vpe-brand-only-note');
	var openStorefront = document.getElementById('epc-vpe-open-storefront');

	function esc(s) {
		return String(s || '').replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/"/g, '&quot;');
	}

	function markDirty() {
		dirty = true;
	}

	function setStatus(msg, ok) {
		if (!statusEl) {
			return;
		}
		statusEl.textContent = msg;
		statusEl.className = 'epc-vpe-status' + (ok === true ? ' is-ok' : ok === false ? ' is-err' : '');
	}

	function currentLevelMeta() {
		return levels[levelId] || levels[pageKey] || { label: pageKey, icon: 'fa-file-o', mode: mode, page_key: pageKey };
	}

	function readBrand() {
		['primary', 'accent', 'background', 'logo_url', 'tagline', 'footer_text', 'hero_headline', 'hero_subheadline'].forEach(function (k) {
			var el = document.getElementById('epc-vpe-brand-' + k);
			if (el) {
				brand[k] = el.value;
			}
		});
	}

	function writeBrand() {
		['primary', 'accent', 'background', 'logo_url', 'tagline', 'footer_text', 'hero_headline', 'hero_subheadline'].forEach(function (k) {
			var el = document.getElementById('epc-vpe-brand-' + k);
			if (el && brand[k] !== undefined) {
				el.value = brand[k];
			}
		});
	}

	function fieldLabel(type, key) {
		var meta = blockLibrary[type] || {};
		var fields = meta.fields || {};
		if (fields[key] && fields[key].label) {
			return fields[key].label;
		}
		return key.replace(/_/g, ' ').replace(/\b\w/g, function (c) { return c.toUpperCase(); });
	}

	function fieldType(type, key, val) {
		var meta = blockLibrary[type] || {};
		var fields = meta.fields || {};
		if (fields[key] && fields[key].type) {
			return fields[key].type;
		}
		if (key === 'body' || key === 'left' || key === 'right' || String(val).length > 80) {
			return 'textarea';
		}
		if (key === 'height') {
			return 'number';
		}
		return 'text';
	}

	function propFields(block, index) {
		var type = block.type;
		var p = block.props || {};
		var meta = blockLibrary[type] || {};
		var fields = meta.fields || {};
		var keys = Object.keys(fields).length ? Object.keys(fields) : Object.keys(p);
		var html = '';
		keys.forEach(function (key) {
			var val = p[key] !== undefined ? p[key] : ((fields[key] && meta.defaults) ? meta.defaults[key] : '');
			var id = 'blk_' + index + '_' + key;
			var fType = fieldType(type, key, val);
			var label = fieldLabel(type, key);
			html += '<div class="form-group"><label for="' + id + '">' + esc(label) + '</label>';
			if (fType === 'textarea') {
				html += '<textarea class="form-control input-sm" id="' + id + '" data-blk="' + index + '" data-prop="' + esc(key) + '" rows="3">' + esc(val) + '</textarea>';
			} else if (fType === 'select' && fields[key] && fields[key].options) {
				html += '<select class="form-control input-sm" id="' + id + '" data-blk="' + index + '" data-prop="' + esc(key) + '">';
				Object.keys(fields[key].options).forEach(function (opt) {
					html += '<option value="' + esc(opt) + '"' + (String(val) === String(opt) ? ' selected' : '') + '>' + esc(fields[key].options[opt]) + '</option>';
				});
				html += '</select>';
			} else {
				html += '<input class="form-control input-sm" id="' + id + '" type="' + (fType === 'number' ? 'number' : 'text') + '" data-blk="' + index + '" data-prop="' + esc(key) + '" value="' + esc(val) + '" />';
			}
			html += '</div>';
		});
		return html;
	}

	function updateChrome() {
		var meta = currentLevelMeta();
		var brandOnly = (mode === 'brand_only');
		if (levelLabelEl) {
			levelLabelEl.textContent = meta.label || pageKey;
		}
		if (levelPill) {
			var icon = levelPill.querySelector('i');
			if (icon) {
				icon.className = 'fa ' + (meta.icon || 'fa-file-o');
			}
		}
		if (inspectorTitle) {
			inspectorTitle.querySelector('span').textContent = brandOnly ? 'Brand settings' : ((meta.label || 'Level') + ' blocks');
		}
		if (blockCountEl) {
			blockCountEl.textContent = brandOnly ? 'Brand only' : (blocks.length + (blocks.length === 1 ? ' block' : ' blocks'));
		}
		if (blocksSection) {
			blocksSection.hidden = brandOnly;
		}
		if (brandOnlyNote) {
			brandOnlyNote.hidden = !brandOnly;
		}
		if (brandSection) {
			brandSection.classList.toggle('is-collapsible', !brandOnly);
			var body = document.getElementById('epc-vpe-brand-body');
			var toggle = document.getElementById('epc-vpe-brand-toggle');
			if (brandOnly) {
				if (toggle) toggle.style.display = 'none';
				if (body) body.hidden = false;
				brandSection.removeAttribute('data-collapsed');
			} else {
				if (toggle) toggle.style.display = '';
				if (body) body.hidden = brandSection.getAttribute('data-collapsed') === '1';
			}
		}
		document.querySelectorAll('.epc-vpe-level').forEach(function (btn) {
			btn.classList.toggle('is-active', btn.getAttribute('data-page-key') === pageKey || btn.getAttribute('data-level') === levelId);
		});
		root.setAttribute('data-page-key', pageKey);
		root.setAttribute('data-level-id', levelId);
		root.setAttribute('data-mode', mode);
		try {
			var url = new URL(window.location.href);
			url.searchParams.set('site_key', siteKey);
			url.searchParams.set('page_key', pageKey);
			window.history.replaceState({}, '', url.toString());
		} catch (e) { /* ignore */ }
	}

	function renderBlocks() {
		if (!blocksList) {
			return;
		}
		if (mode === 'brand_only') {
			blocksList.innerHTML = '';
			updateChrome();
			return;
		}
		if (blocks.length === 0) {
			blocksList.innerHTML = '<li class="epc-vpe-empty epc-vpe-empty--compact"><div class="epc-vpe-empty__title">No blocks yet</div><p>Add a block below for this frontend level.</p></li>';
			updateChrome();
			return;
		}
		if (selectedIndex >= blocks.length) {
			selectedIndex = blocks.length - 1;
		}
		var html = '';
		blocks.forEach(function (block, i) {
			var meta = blockLibrary[block.type] || { label: block.type, icon: 'fa-square-o' };
			var open = i === selectedIndex;
			html += '<li class="epc-vpe-block-item' + (open ? ' is-open' : '') + '" draggable="true" data-index="' + i + '">';
			html += '<div class="epc-vpe-block-item__head" data-select="' + i + '">';
			html += '<i class="fa fa-bars" title="Drag to reorder"></i>';
			html += '<i class="fa ' + esc(meta.icon) + '"></i>';
			html += '<strong>' + esc(meta.label) + '</strong>';
			html += '<span class="epc-vpe-block-item__idx">#' + (i + 1) + '</span>';
			html += '<button type="button" class="btn btn-xs btn-default epc-vpe-dup" data-index="' + i + '" title="Duplicate"><i class="fa fa-copy"></i></button>';
			html += '<button type="button" class="btn btn-xs btn-danger epc-vpe-remove" data-index="' + i + '" title="Remove"><i class="fa fa-trash"></i></button>';
			html += '</div>';
			if (open) {
				html += '<div class="epc-vpe-block-item__body">' + propFields(block, i) + '</div>';
			}
			html += '</li>';
		});
		blocksList.innerHTML = html;

		blocksList.querySelectorAll('.epc-vpe-block-item').forEach(function (li) {
			li.addEventListener('dragstart', function (e) {
				dragIndex = parseInt(li.getAttribute('data-index'), 10);
				li.classList.add('is-dragging');
				e.dataTransfer.effectAllowed = 'move';
			});
			li.addEventListener('dragend', function () {
				li.classList.remove('is-dragging');
				dragIndex = null;
			});
			li.addEventListener('dragover', function (e) { e.preventDefault(); });
			li.addEventListener('drop', function (e) {
				e.preventDefault();
				var to = parseInt(li.getAttribute('data-index'), 10);
				if (dragIndex === null || dragIndex === to) {
					return;
				}
				var moved = blocks.splice(dragIndex, 1)[0];
				blocks.splice(to, 0, moved);
				selectedIndex = to;
				markDirty();
				renderBlocks();
			});
		});

		blocksList.querySelectorAll('[data-select]').forEach(function (head) {
			head.addEventListener('click', function (e) {
				if (e.target.closest('.epc-vpe-remove, .epc-vpe-dup, .fa-bars')) {
					return;
				}
				selectedIndex = parseInt(head.getAttribute('data-select'), 10);
				renderBlocks();
			});
		});

		blocksList.querySelectorAll('.epc-vpe-remove').forEach(function (btn) {
			btn.addEventListener('click', function (e) {
				e.stopPropagation();
				var idx = parseInt(btn.getAttribute('data-index'), 10);
				blocks.splice(idx, 1);
				selectedIndex = Math.max(0, idx - 1);
				markDirty();
				renderBlocks();
			});
		});

		blocksList.querySelectorAll('.epc-vpe-dup').forEach(function (btn) {
			btn.addEventListener('click', function (e) {
				e.stopPropagation();
				var idx = parseInt(btn.getAttribute('data-index'), 10);
				var copy = JSON.parse(JSON.stringify(blocks[idx]));
				copy.id = 'blk_' + Math.random().toString(36).slice(2, 10);
				blocks.splice(idx + 1, 0, copy);
				selectedIndex = idx + 1;
				markDirty();
				renderBlocks();
			});
		});

		blocksList.querySelectorAll('[data-blk]').forEach(function (el) {
			el.addEventListener('input', function () {
				var idx = parseInt(el.getAttribute('data-blk'), 10);
				var prop = el.getAttribute('data-prop');
				if (blocks[idx] && blocks[idx].props) {
					blocks[idx].props[prop] = el.value;
					markDirty();
				}
			});
		});

		updateChrome();
	}

	function setPreviewUrl(url) {
		if (!previewFrame) {
			return;
		}
		var base = url || previewFrame.getAttribute('data-base') || '';
		previewFrame.setAttribute('data-base', base);
		previewFrame.src = base + (base.indexOf('?') >= 0 ? '&' : '?') + '_=' + Date.now();
		if (openStorefront) {
			openStorefront.href = base;
		}
	}

	function reloadPreview() {
		if (!previewFrame) {
			return;
		}
		var base = previewFrame.getAttribute('data-base') || previewFrame.src.split('?')[0];
		setPreviewUrl(base);
	}

	function applyLayoutPayload(layout, previewUrl) {
		blocks = Array.isArray(layout.blocks) ? layout.blocks.slice() : [];
		brand = Object.assign({}, layout.brand || {});
		pageKey = layout.page_key || pageKey;
		levelId = layout.level_id || pageKey;
		mode = layout.mode || 'layout';
		selectedIndex = 0;
		dirty = false;
		writeBrand();
		setStatus(layout.is_published ? 'Published' : 'Draft', null);
		if (previewUrl) {
			setPreviewUrl(previewUrl);
		}
		renderBlocks();
		updateChrome();
	}

	function post(action, publish) {
		readBrand();
		setStatus('Saving…', null);
		var body = new FormData();
		body.append('action', action);
		body.append('site_key', siteKey);
		body.append('page_key', pageKey);
		body.append('blocks_json', JSON.stringify(mode === 'brand_only' ? [] : blocks));
		body.append('brand_json', JSON.stringify(brand));
		if (publish) {
			body.append('publish', '1');
		}
		return fetch(ajaxUrl, { method: 'POST', body: body, credentials: 'same-origin' })
			.then(function (r) { return r.json(); })
			.then(function (data) {
				if (!data.status) {
					throw new Error(data.message || 'Save failed');
				}
				dirty = false;
				setStatus(data.message || 'Saved', true);
				if (data.layout) {
					applyLayoutPayload(data.layout, data.preview_url);
				} else {
					reloadPreview();
				}
				return data;
			})
			.catch(function (err) {
				setStatus(err.message || 'Error', false);
			});
	}

	function loadLevel(nextPageKey, nextLevelId) {
		if (nextPageKey === pageKey) {
			return;
		}
		if (dirty && !window.confirm('You have unsaved changes on this level. Switch anyway and discard them?')) {
			return;
		}
		setStatus('Loading level…', null);
		var qs = 'action=load_layout&site_key=' + encodeURIComponent(siteKey) + '&page_key=' + encodeURIComponent(nextPageKey);
		fetch(ajaxUrl + (ajaxUrl.indexOf('?') >= 0 ? '&' : '?') + qs, { credentials: 'same-origin' })
			.then(function (r) { return r.json(); })
			.then(function (data) {
				if (!data.status || !data.layout) {
					throw new Error((data && data.message) || 'Could not load level');
				}
				if (data.levels) {
					levels = data.levels;
				}
				levelId = nextLevelId || data.layout.level_id || nextPageKey;
				applyLayoutPayload(data.layout, data.preview_url);
				setStatus(data.layout.is_published ? 'Published' : 'Draft', true);
			})
			.catch(function (err) {
				setStatus(err.message || 'Load failed', false);
			});
	}

	document.querySelectorAll('.epc-vpe-add').forEach(function (btn) {
		btn.addEventListener('click', function () {
			var type = btn.getAttribute('data-type');
			var lib = blockLibrary[type];
			if (!lib || mode === 'brand_only') {
				return;
			}
			blocks.push({
				id: 'blk_' + Math.random().toString(36).slice(2, 10),
				type: type,
				props: JSON.parse(JSON.stringify(lib.defaults || {}))
			});
			selectedIndex = blocks.length - 1;
			markDirty();
			renderBlocks();
		});
	});

	document.querySelectorAll('.epc-vpe-level').forEach(function (btn) {
		btn.addEventListener('click', function () {
			loadLevel(btn.getAttribute('data-page-key'), btn.getAttribute('data-level'));
		});
	});

	var brandToggle = document.getElementById('epc-vpe-brand-toggle');
	if (brandToggle) {
		brandToggle.addEventListener('click', function () {
			if (!brandSection || mode === 'brand_only') {
				return;
			}
			var collapsed = brandSection.getAttribute('data-collapsed') === '1';
			brandSection.setAttribute('data-collapsed', collapsed ? '0' : '1');
			var body = document.getElementById('epc-vpe-brand-body');
			if (body) {
				body.hidden = !collapsed;
			}
		});
	}

	['primary', 'accent', 'background', 'logo_url', 'tagline', 'footer_text', 'hero_headline', 'hero_subheadline'].forEach(function (k) {
		var el = document.getElementById('epc-vpe-brand-' + k);
		if (el) {
			el.addEventListener('input', markDirty);
		}
	});

	var tenantSel = document.getElementById('epc-vpe-tenant');
	if (tenantSel) {
		tenantSel.addEventListener('change', function () {
			if (dirty && !window.confirm('Switch tenant and discard unsaved changes?')) {
				tenantSel.value = siteKey;
				return;
			}
			var url = tenantSel.options[tenantSel.selectedIndex].getAttribute('data-url') || '';
			window.location.href = (cfg.pageUrl || '') + '?site_key=' + encodeURIComponent(tenantSel.value)
				+ '&page_key=' + encodeURIComponent(pageKey)
				+ (url ? '&preview=' + encodeURIComponent(url) : '');
		});
	}

	var saveBtn = document.getElementById('epc-vpe-save');
	var publishBtn = document.getElementById('epc-vpe-publish');
	var refreshBtn = document.getElementById('epc-vpe-refresh-preview');
	if (saveBtn) {
		saveBtn.addEventListener('click', function () { post('save_layout', false); });
	}
	if (publishBtn) {
		publishBtn.addEventListener('click', function () { post('save_layout', true); });
	}
	if (refreshBtn) {
		refreshBtn.addEventListener('click', reloadPreview);
	}

	document.querySelectorAll('#epc-vpe-device button').forEach(function (btn) {
		btn.addEventListener('click', function () {
			document.querySelectorAll('#epc-vpe-device button').forEach(function (b) { b.classList.remove('is-active'); });
			btn.classList.add('is-active');
			if (previewWrap) {
				previewWrap.className = 'epc-vpe-preview-frame-wrap is-' + (btn.getAttribute('data-device') || 'desktop');
			}
		});
	});

	if (cfg.previewUrl) {
		previewFrame && previewFrame.setAttribute('data-base', cfg.previewUrl);
	}

	writeBrand();
	renderBlocks();
	updateChrome();
})();
