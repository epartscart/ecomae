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
		var useVideoBtn = e.target.closest('.epc-social-use-video');
		if (useVideoBtn) {
			var mediaUrl = useVideoBtn.getAttribute('data-url') || '';
			if (!mediaUrl) {
				return;
			}
			// Prefer absolute URL so Meta/TikTok can fetch from public HTTPS.
			if (mediaUrl.charAt(0) === '/') {
				mediaUrl = window.location.origin + mediaUrl;
			}
			var videoUrlInput = document.getElementById('epc_social_video_url');
			var composeMedia = document.getElementById('epc_social_compose_media');
			if (videoUrlInput) {
				videoUrlInput.value = mediaUrl;
			}
			if (composeMedia) {
				composeMedia.value = mediaUrl;
			}
			copyText(mediaUrl, useVideoBtn);
			var origLabel = useVideoBtn.innerHTML;
			useVideoBtn.innerHTML = '<i class="fa fa-check"></i> Media URL set';
			setTimeout(function () { useVideoBtn.innerHTML = origLabel; }, 1800);
			if (!videoUrlInput && !composeMedia) {
				alert('Media URL copied. Open Drafts or TikTok and paste into the media URL field.');
			}
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
			var composeMedia = document.getElementById('epc_social_compose_media');
			if (videoUrl && videoUrl.value) {
				fd.append('media_url', videoUrl.value);
			} else if (composeMedia && composeMedia.value) {
				fd.append('media_url', composeMedia.value);
			}
			fetch(cfg.ajaxUrl, { method: 'POST', body: fd, credentials: 'same-origin' })
				.then(function (r) { return r.json(); })
				.then(function (j) {
					alert(j.message || (j.ok ? 'Draft saved' : 'Error'));
				});
			return;
		}
		var pubBtn = e.target.closest('.epc-social-publish-now');
		if (pubBtn && cfg.ajaxUrl) {
			var platform = pubBtn.getAttribute('data-platform') || 'instagram';
			var mediaEl = document.getElementById('epc_social_video_url') || document.getElementById('epc_social_compose_media');
			var mediaUrl = mediaEl && mediaEl.value ? mediaEl.value.trim() : '';
			if ((platform === 'instagram' || platform === 'tiktok') && !mediaUrl) {
				mediaUrl = window.prompt('Public HTTPS media URL required for ' + platform + ' publish:', '') || '';
			}
			if ((platform === 'instagram' || platform === 'tiktok') && !mediaUrl) {
				alert('Publish cancelled — media URL is required for ' + platform + '.');
				return;
			}
			if (!window.confirm('Publish this post to ' + platform + ' now?')) {
				return;
			}
			pubBtn.disabled = true;
			var pfd = new FormData();
			pfd.append('action', 'publish_now');
			pfd.append('csrf_token', cfg.csrfToken || '');
			pfd.append('platform', platform);
			pfd.append('title', pubBtn.getAttribute('data-title') || 'Published post');
			pfd.append('caption', pubBtn.getAttribute('data-caption') || '');
			pfd.append('media_url', mediaUrl);
			fetch(cfg.ajaxUrl, { method: 'POST', body: pfd, credentials: 'same-origin' })
				.then(function (r) { return r.json(); })
				.then(function (j) {
					alert(j.message || (j.ok ? 'Published' : 'Publish failed'));
					if (j.ok && cfg.hubUrl) {
						window.location.href = cfg.hubUrl;
					} else if (j.ok) {
						window.location.search = 'tab=drafts';
					}
				})
				.catch(function () { alert('Network error'); })
				.finally(function () { pubBtn.disabled = false; });
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
