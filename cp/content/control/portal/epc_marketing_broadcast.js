/**
 * Marketing Broadcast — audience, templates, live preview, form sync.
 */
(function () {
	'use strict';

	var cfg = window.EPC_MB || {};
	var ajaxUrl = cfg.ajaxUrl || '';

	function qs(sel, root) {
		return (root || document).querySelector(sel);
	}
	function qsa(sel, root) {
		return Array.prototype.slice.call((root || document).querySelectorAll(sel));
	}

	function audienceMode(form) {
		var checked = qs('input.epc-mb-audience-mode:checked', form);
		if (checked) {
			return checked.value;
		}
		var sel = qs('select.epc-mb-audience-mode', form);
		return sel ? sel.value : 'all';
	}

	function syncAudienceMeta(form) {
		var hidden = qs('input[name="audience_meta"]', form);
		if (!hidden) return;
		var mode = audienceMode(form);
		if (mode === 'group') {
			var g = qs('.epc-mb-group-select select', form);
			hidden.value = g ? g.value : '';
		} else if (mode === 'manual') {
			var m = qs('.epc-mb-manual-input textarea', form);
			hidden.value = m ? m.value : '';
		} else {
			hidden.value = '';
		}
	}

	function toggleAudienceFields(form) {
		var mode = audienceMode(form);
		qsa('.epc-mb-mode', form).forEach(function (lab) {
			var inp = qs('input', lab);
			lab.classList.toggle('is-active', !!(inp && inp.checked));
		});
		var groupBox = qs('.epc-mb-group-select', form);
		var manualBox = qs('.epc-mb-manual-input', form);
		if (groupBox) groupBox.style.display = mode === 'group' ? '' : 'none';
		if (manualBox) manualBox.style.display = mode === 'manual' ? '' : 'none';
		syncAudienceMeta(form);
	}

	function bumpCount(el) {
		if (!el) return;
		var wrap = el.closest('.epc-mb-count');
		if (!wrap) return;
		wrap.classList.remove('is-pop');
		void wrap.offsetWidth;
		wrap.classList.add('is-pop');
	}

	function updateRecipientCount(form) {
		var channel = form.id === 'epc-mb-wa-form' ? 'whatsapp' : 'email';
		var countEl = document.getElementById(channel === 'whatsapp' ? 'epc-mb-wa-count' : 'epc-mb-email-count');
		if (!countEl || !ajaxUrl) return;
		syncAudienceMeta(form);
		var hidden = qs('input[name="audience_meta"]', form);
		var params = new URLSearchParams({
			action: 'count_recipients',
			channel: channel,
			audience_mode: audienceMode(form),
			audience_meta: hidden ? hidden.value : ''
		});
		fetch(ajaxUrl + '?' + params.toString(), { credentials: 'same-origin' })
			.then(function (r) { return r.json(); })
			.then(function (data) {
				if (data && data.ok) {
					countEl.textContent = data.count + ' recipient(s)';
					bumpCount(countEl);
				} else {
					countEl.textContent = '—';
				}
			})
			.catch(function () { countEl.textContent = '—'; });
	}

	function sampleize(text) {
		return String(text || '')
			.replace(/\{\{customer_name\}\}/g, 'Customer')
			.replace(/\{\{shop_name\}\}/g, (cfg.shopName || 'Your shop'))
			.replace(/\{\{shop_url\}\}/g, (cfg.shopUrl || '#'));
	}

	function updateEmailPreview(form) {
		var frame = document.getElementById('epc-mb-email-preview-frame');
		var html = qs('.epc-mb-html-body', form);
		if (!frame || !html) return;
		var doc = sampleize(html.value || '<p style="font-family:sans-serif;color:#64748b;padding:24px">Choose a template or paste HTML to preview.</p>');
		try {
			frame.srcdoc = doc;
		} catch (e) {
			frame.src = 'about:blank';
		}
	}

	function updateWaPreview(form) {
		var bubble = document.getElementById('epc-mb-wa-preview-bubble');
		var body = qs('.epc-mb-wa-body', form);
		if (!bubble || !body) return;
		var text = body.value.trim();
		bubble.textContent = text ? sampleize(text) : 'Select a template or type a message…';
	}

	function setTemplateKey(form, channel, key, forceFill) {
		var hidden = document.getElementById(channel === 'whatsapp' ? 'epc-mb-wa-template-key' : 'epc-mb-email-template-key');
		var sel = qs('.epc-mb-template-select', form);
		if (hidden) hidden.value = key;
		if (sel) sel.value = key;
		qsa('.epc-mb-tpl[data-channel="' + channel + '"]').forEach(function (btn) {
			btn.classList.toggle('is-active', btn.getAttribute('data-template') === key);
		});
		loadTemplate(form, channel, !!forceFill);
	}

	function loadTemplate(form, channel, force) {
		var sel = qs('.epc-mb-template-select', form);
		var hidden = document.getElementById(channel === 'whatsapp' ? 'epc-mb-wa-template-key' : 'epc-mb-email-template-key');
		var key = (sel && sel.value) || (hidden && hidden.value) || '';
		if (!key || !ajaxUrl) return;
		var params = new URLSearchParams({
			action: 'template_preview',
			channel: channel,
			template_key: key
		});
		fetch(ajaxUrl + '?' + params.toString(), { credentials: 'same-origin' })
			.then(function (r) { return r.json(); })
			.then(function (data) {
				if (!data || !data.ok) return;
				if (channel === 'whatsapp') {
					var waBody = qs('.epc-mb-wa-body', form);
					if (waBody && (force || !waBody.value.trim())) waBody.value = data.body_text || '';
					updateWaPreview(form);
				} else {
					var subj = qs('input[name="subject"]', form);
					var prev = qs('input[name="preview"]', form);
					var html = qs('.epc-mb-html-body', form);
					if (subj && (force || !subj.value.trim())) subj.value = data.subject || '';
					if (prev && (force || !prev.value.trim())) prev.value = data.preview || '';
					if (html && (force || !html.value.trim())) html.value = data.body_html || '';
					updateEmailPreview(form);
				}
			});
	}

	function bindForm(formId, channel) {
		var form = document.getElementById(formId);
		if (!form) return;
		toggleAudienceFields(form);
		updateRecipientCount(form);
		loadTemplate(form, channel, false);
		if (channel === 'email') updateEmailPreview(form);
		if (channel === 'whatsapp') updateWaPreview(form);

		form.addEventListener('change', function (e) {
			if (e.target.classList.contains('epc-mb-audience-mode') ||
				e.target.closest('.epc-mb-group-select') ||
				e.target.closest('.epc-mb-manual-input')) {
				toggleAudienceFields(form);
				updateRecipientCount(form);
			}
			if (e.target.classList.contains('epc-mb-template-select')) {
				setTemplateKey(form, channel, e.target.value, false);
			}
		});
		form.addEventListener('input', function (e) {
			if (e.target.closest('.epc-mb-manual-input')) {
				syncAudienceMeta(form);
				updateRecipientCount(form);
			}
			if (e.target.classList.contains('epc-mb-html-body') || e.target.name === 'subject' || e.target.name === 'preview') {
				updateEmailPreview(form);
			}
			if (e.target.classList.contains('epc-mb-wa-body')) {
				updateWaPreview(form);
			}
		});
		form.addEventListener('submit', function (e) {
			syncAudienceMeta(form);
			var btn = qs('button[type="submit"]', form);
			var msg = btn && btn.getAttribute('data-confirm');
			if (msg && !window.confirm(msg)) {
				e.preventDefault();
			}
		});

		qsa('.epc-mb-tpl[data-channel="' + channel + '"]').forEach(function (btn) {
			btn.addEventListener('click', function () {
				setTemplateKey(form, channel, btn.getAttribute('data-template'), false);
			});
		});

		var reloadId = channel === 'whatsapp' ? 'epc-mb-wa-reload-tpl' : 'epc-mb-email-reload-tpl';
		var reloadBtn = document.getElementById(reloadId);
		if (reloadBtn) {
			reloadBtn.addEventListener('click', function () {
				loadTemplate(form, channel, true);
			});
		}
	}

	document.addEventListener('DOMContentLoaded', function () {
		bindForm('epc-mb-email-form', 'email');
		bindForm('epc-mb-wa-form', 'whatsapp');
	});
})();
