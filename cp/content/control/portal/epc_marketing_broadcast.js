/**
 * Marketing Broadcast — audience preview, template fill, form meta sync.
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

	function syncAudienceMeta(form) {
		var modeSel = qs('.epc-mb-audience-mode', form);
		var hidden = qs('input[name="audience_meta"]', form);
		if (!modeSel || !hidden) return;
		var mode = modeSel.value;
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
		var modeSel = qs('.epc-mb-audience-mode', form);
		if (!modeSel) return;
		var mode = modeSel.value;
		var groupBox = qs('.epc-mb-group-select', form);
		var manualBox = qs('.epc-mb-manual-input', form);
		if (groupBox) groupBox.style.display = mode === 'group' ? '' : 'none';
		if (manualBox) manualBox.style.display = mode === 'manual' ? '' : 'none';
		syncAudienceMeta(form);
	}

	function updateRecipientCount(form) {
		var channel = form.id === 'epc-mb-wa-form' ? 'whatsapp' : 'email';
		var countEl = document.getElementById(channel === 'whatsapp' ? 'epc-mb-wa-count' : 'epc-mb-email-count');
		if (!countEl || !ajaxUrl) return;
		syncAudienceMeta(form);
		var modeSel = qs('.epc-mb-audience-mode', form);
		var hidden = qs('input[name="audience_meta"]', form);
		var params = new URLSearchParams({
			action: 'count_recipients',
			channel: channel,
			audience_mode: modeSel ? modeSel.value : 'all',
			audience_meta: hidden ? hidden.value : ''
		});
		fetch(ajaxUrl + '?' + params.toString(), { credentials: 'same-origin' })
			.then(function (r) { return r.json(); })
			.then(function (data) {
				if (data && data.ok) {
					countEl.textContent = data.count + ' recipient(s)';
				} else {
					countEl.textContent = '—';
				}
			})
			.catch(function () { countEl.textContent = '—'; });
	}

	function loadTemplate(form, channel) {
		var sel = qs('.epc-mb-template-select', form);
		if (!sel || !ajaxUrl) return;
		var params = new URLSearchParams({
			action: 'template_preview',
			channel: channel,
			template_key: sel.value
		});
		fetch(ajaxUrl + '?' + params.toString(), { credentials: 'same-origin' })
			.then(function (r) { return r.json(); })
			.then(function (data) {
				if (!data || !data.ok) return;
				if (channel === 'whatsapp') {
					var waBody = qs('.epc-mb-wa-body', form);
					if (waBody && !waBody.value.trim()) waBody.value = data.body_text || '';
				} else {
					var subj = qs('input[name="subject"]', form);
					var prev = qs('input[name="preview"]', form);
					var html = qs('.epc-mb-html-body', form);
					if (subj && !subj.value.trim()) subj.value = data.subject || '';
					if (prev && !prev.value.trim()) prev.value = data.preview || '';
					if (html && !html.value.trim()) html.value = data.body_html || '';
				}
			});
	}

	function bindForm(formId, channel) {
		var form = document.getElementById(formId);
		if (!form) return;
		toggleAudienceFields(form);
		updateRecipientCount(form);
		loadTemplate(form, channel);

		form.addEventListener('change', function (e) {
			if (e.target.classList.contains('epc-mb-audience-mode') ||
				e.target.closest('.epc-mb-group-select') ||
				e.target.closest('.epc-mb-manual-input')) {
				toggleAudienceFields(form);
				updateRecipientCount(form);
			}
			if (e.target.classList.contains('epc-mb-template-select')) {
				loadTemplate(form, channel);
			}
		});
		form.addEventListener('input', function (e) {
			if (e.target.closest('.epc-mb-manual-input')) {
				syncAudienceMeta(form);
				updateRecipientCount(form);
			}
		});
		form.addEventListener('submit', function () {
			syncAudienceMeta(form);
		});
	}

	document.addEventListener('DOMContentLoaded', function () {
		bindForm('epc-mb-email-form', 'email');
		bindForm('epc-mb-wa-form', 'whatsapp');
	});
})();
