(function () {
	'use strict';

	var root = document.getElementById('epc-vpe-app');
	if (!root) {
		return;
	}

	var cfg = window.EPC_VPE || {};
	var ajaxUrl = cfg.ajaxUrl || '';
	var blockLibrary = cfg.blockLibrary || {};
	var blocks = Array.isArray(cfg.blocks) ? cfg.blocks.slice() : [];
	var brand = cfg.brand || {};
	var siteKey = cfg.siteKey || 'platform';
	var previewFrame = document.getElementById('epc-vpe-preview-frame');
	var blocksList = document.getElementById('epc-vpe-blocks');
	var statusEl = document.getElementById('epc-vpe-status');
	var dragIndex = null;

	function esc(s) {
		return String(s || '').replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/"/g, '&quot;');
	}

	function setStatus(msg, ok) {
		if (!statusEl) {
			return;
		}
		statusEl.textContent = msg;
		statusEl.className = 'epc-vpe-status' + (ok === true ? ' is-ok' : ok === false ? ' is-err' : '');
	}

	function readBrand() {
		['primary', 'accent', 'background', 'logo_url', 'tagline', 'footer_text', 'hero_headline', 'hero_subheadline'].forEach(function (k) {
			var el = document.getElementById('epc-vpe-brand-' + k);
			if (el) {
				brand[k] = el.value;
			}
		});
	}

	function propFields(block, index) {
		var type = block.type;
		var p = block.props || {};
		var html = '';
		Object.keys(p).forEach(function (key) {
			var val = p[key];
			var id = 'blk_' + index + '_' + key;
			if (String(val).length > 80 || key === 'body' || key === 'left' || key === 'right') {
				html += '<div class="form-group"><label>' + esc(key) + '</label><textarea class="form-control input-sm" data-blk="' + index + '" data-prop="' + esc(key) + '" rows="2">' + esc(val) + '</textarea></div>';
			} else {
				html += '<div class="form-group"><label>' + esc(key) + '</label><input class="form-control input-sm" data-blk="' + index + '" data-prop="' + esc(key) + '" value="' + esc(val) + '" /></div>';
			}
		});
		return html;
	}

	function renderBlocks() {
		if (!blocksList) {
			return;
		}
		if (blocks.length === 0) {
			blocksList.innerHTML = '<li class="text-muted" style="padding:12px">No blocks yet — add one below.</li>';
			return;
		}
		var html = '';
		blocks.forEach(function (block, i) {
			var meta = blockLibrary[block.type] || { label: block.type, icon: 'fa-square-o' };
			html += '<li class="epc-vpe-block-item" draggable="true" data-index="' + i + '">';
			html += '<div class="epc-vpe-block-item__head"><i class="fa fa-bars"></i><i class="fa ' + esc(meta.icon) + '"></i><strong>' + esc(meta.label) + '</strong>';
			html += '<button type="button" class="btn btn-xs btn-danger epc-vpe-remove" data-index="' + i + '"><i class="fa fa-trash"></i></button></div>';
			html += '<div class="epc-vpe-block-item__body">' + propFields(block, i) + '</div></li>';
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
			li.addEventListener('dragover', function (e) {
				e.preventDefault();
			});
			li.addEventListener('drop', function (e) {
				e.preventDefault();
				var to = parseInt(li.getAttribute('data-index'), 10);
				if (dragIndex === null || dragIndex === to) {
					return;
				}
				var moved = blocks.splice(dragIndex, 1)[0];
				blocks.splice(to, 0, moved);
				renderBlocks();
			});
		});

		blocksList.querySelectorAll('.epc-vpe-remove').forEach(function (btn) {
			btn.addEventListener('click', function () {
				var idx = parseInt(btn.getAttribute('data-index'), 10);
				blocks.splice(idx, 1);
				renderBlocks();
			});
		});

		blocksList.querySelectorAll('[data-blk]').forEach(function (el) {
			el.addEventListener('input', function () {
				var idx = parseInt(el.getAttribute('data-blk'), 10);
				var prop = el.getAttribute('data-prop');
				if (blocks[idx] && blocks[idx].props) {
					blocks[idx].props[prop] = el.value;
				}
			});
		});
	}

	function reloadPreview() {
		if (!previewFrame) {
			return;
		}
		var base = previewFrame.getAttribute('data-base') || previewFrame.src.split('?')[0];
		previewFrame.src = base + '?_=' + Date.now();
	}

	function post(action, publish) {
		readBrand();
		setStatus('Saving…', null);
		var body = new FormData();
		body.append('action', action);
		body.append('site_key', siteKey);
		body.append('blocks_json', JSON.stringify(blocks));
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
				setStatus(data.message || 'Saved', true);
				reloadPreview();
				return data;
			})
			.catch(function (err) {
				setStatus(err.message || 'Error', false);
			});
	}

	document.querySelectorAll('.epc-vpe-add').forEach(function (btn) {
		btn.addEventListener('click', function () {
			var type = btn.getAttribute('data-type');
			var lib = blockLibrary[type];
			if (!lib) {
				return;
			}
			blocks.push({
				id: 'blk_' + Math.random().toString(36).slice(2, 10),
				type: type,
				props: JSON.parse(JSON.stringify(lib.defaults || {}))
			});
			renderBlocks();
		});
	});

	var tenantSel = document.getElementById('epc-vpe-tenant');
	if (tenantSel) {
		tenantSel.addEventListener('change', function () {
			var url = tenantSel.options[tenantSel.selectedIndex].getAttribute('data-url') || '';
			window.location.href = cfg.pageUrl + '?site_key=' + encodeURIComponent(tenantSel.value) + (url ? '&preview=' + encodeURIComponent(url) : '');
		});
	}

	document.getElementById('epc-vpe-save')?.addEventListener('click', function () {
		post('save_layout', false);
	});
	document.getElementById('epc-vpe-publish')?.addEventListener('click', function () {
		post('save_layout', true);
	});
	document.getElementById('epc-vpe-refresh-preview')?.addEventListener('click', reloadPreview);

	renderBlocks();
})();
