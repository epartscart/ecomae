(function () {
	'use strict';
	var cfg = window.EPC_SOCIAL_HUB || {};

	function copyText(text, btn) {
		if (!text) return;
		var done = function () {
			if (!btn) return;
			var orig = btn.innerHTML;
			btn.innerHTML = '<i class="fa fa-check"></i> Copied';
			btn.classList.add('btn-success');
			setTimeout(function () {
				btn.innerHTML = orig;
				btn.classList.remove('btn-success');
			}, 1800);
		};
		if (navigator.clipboard && navigator.clipboard.writeText) {
			navigator.clipboard.writeText(text).then(done).catch(function () {
				fallbackCopy(text);
				done();
			});
		} else {
			fallbackCopy(text);
			done();
		}
	}

	function fallbackCopy(text) {
		var ta = document.createElement('textarea');
		ta.value = text;
		document.body.appendChild(ta);
		ta.select();
		try { document.execCommand('copy'); } catch (e) {}
		document.body.removeChild(ta);
	}

	document.addEventListener('click', function (e) {
		var copyBtn = e.target.closest('.epc-social-copy');
		if (copyBtn) {
			var cap = copyBtn.getAttribute('data-caption') || '';
			copyText(cap, copyBtn);
			return;
		}
		var draftBtn = e.target.closest('.epc-social-save-draft');
		if (draftBtn && cfg.ajaxUrl) {
			var fd = new FormData();
			fd.append('action', 'save_draft');
			fd.append('csrf_token', cfg.csrfToken || '');
			fd.append('platform', draftBtn.getAttribute('data-platform') || 'tiktok');
			fd.append('title', draftBtn.getAttribute('data-title') || 'Draft');
			fd.append('caption', draftBtn.getAttribute('data-caption') || '');
			var videoUrl = document.getElementById('epc_social_video_url');
			if (videoUrl && videoUrl.value) {
				fd.append('media_url', videoUrl.value);
			}
			fetch(cfg.ajaxUrl, { method: 'POST', body: fd, credentials: 'same-origin' })
				.then(function (r) { return r.json(); })
				.then(function (j) {
					alert(j.message || (j.ok ? 'Draft saved' : 'Error'));
				});
		}
	});

	var genBtn = document.getElementById('epc_social_gen_btn');
	if (genBtn && cfg.ajaxUrl) {
		genBtn.addEventListener('click', function () {
			var platform = (document.getElementById('epc_social_gen_platform') || {}).value || 'instagram';
			var product = (document.getElementById('epc_social_gen_product') || {}).value || '';
			var fd = new FormData();
			fd.append('action', 'generate_caption');
			fd.append('csrf_token', cfg.csrfToken || '');
			fd.append('platform', platform);
			fd.append('product_line', product);
			fetch(cfg.ajaxUrl, { method: 'POST', body: fd, credentials: 'same-origin' })
				.then(function (r) { return r.json(); })
				.then(function (j) {
					if (!j.ok) {
						alert(j.message || 'Generate failed');
						return;
					}
					var box = document.getElementById('epc_social_gen_result');
					var cap = document.getElementById('epc_social_gen_caption');
					var tags = document.getElementById('epc_social_gen_tags');
					var copyB = document.getElementById('epc_social_gen_copy');
					if (box) box.style.display = 'block';
					if (cap) cap.textContent = j.caption || '';
					if (tags) tags.textContent = j.hashtags || '';
					if (copyB) {
						copyB.setAttribute('data-caption', (j.caption || '') + '\n\n' + (j.hashtags || ''));
					}
				});
		});
	}
})();
