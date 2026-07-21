/**
 * Init elFinder after footer jQuery reload (head-loaded plugin is wiped by duplicate jQuery).
 */
(function (window, document) {
	'use strict';

	function ready(fn) {
		if (document.readyState === 'loading') {
			document.addEventListener('DOMContentLoaded', fn);
		} else {
			fn();
		}
	}

	function showError(msg) {
		var box = document.getElementById('elfinder');
		if (!box) {
			return;
		}
		box.classList.remove('epc-fm-loading');
		box.innerHTML = '<div class="epc-filemanager__error">' + String(msg) + '</div>';
	}

	function loadScript(src) {
		return new Promise(function (resolve, reject) {
			if (!src) {
				resolve();
				return;
			}
			var existing = document.querySelector('script[src="' + src + '"]');
			if (existing && existing.getAttribute('data-epc-fm-loaded') === '1') {
				resolve();
				return;
			}
			var s = document.createElement('script');
			s.src = src;
			s.async = false;
			s.onload = function () {
				s.setAttribute('data-epc-fm-loaded', '1');
				resolve();
			};
			s.onerror = function () {
				reject(new Error('Failed to load ' + src));
			};
			document.head.appendChild(s);
		});
	}

	function initElfinder() {
		var $ = window.jQuery;
		var cfg = window.EPC_FILEMANAGER || {};
		var mount = document.getElementById('elfinder');
		if (!mount) {
			return;
		}
		if (!$ || typeof $.fn !== 'object') {
			showError('jQuery is required for the file manager.');
			return;
		}
		if (typeof $.fn.elfinder !== 'function') {
			showError('File manager library failed to load. Refresh the page and try again.');
			return;
		}

		var connectorUrl = cfg.connectorUrl || mount.getAttribute('data-connector') || '';
		var csrf = cfg.csrf || mount.getAttribute('data-csrf') || '';
		if (!connectorUrl) {
			showError('File manager is not configured.');
			return;
		}

		mount.classList.remove('epc-fm-loading');
		mount.innerHTML = '';

		try {
			$('#elfinder').elfinder({
				url: connectorUrl,
				lang: cfg.lang || 'en',
				height: cfg.height || 560,
				resizable: true,
				customData: {
					csrf_guard_key: csrf
				}
			});
		} catch (err) {
			showError((err && err.message) ? err.message : 'Unable to start the file manager.');
		}
	}

	ready(function () {
		var $ = window.jQuery;
		var cfg = window.EPC_FILEMANAGER || {};
		var mount = document.getElementById('elfinder');
		if (mount) {
			mount.classList.add('epc-fm-loading');
			if (!mount.innerHTML.trim()) {
				mount.textContent = 'Loading file manager…';
			}
		}

		var chain = Promise.resolve();
		// Always (re)bind the plugin to the current jQuery instance.
		if (!$ || typeof $.fn.elfinder !== 'function') {
			var elfSrc = '/cp/lib/elfinder/js/elfinder.min.js';
			if (cfg.connectorUrl && cfg.connectorUrl.indexOf('/lib/elfinder/') !== -1) {
				elfSrc = cfg.connectorUrl.replace(/\/php\/connector\.php.*$/, '/js/elfinder.min.js');
			}
			chain = chain.then(function () {
				return loadScript(elfSrc);
			});
		}
		if (cfg.langUrl && cfg.lang && cfg.lang !== 'en') {
			chain = chain.then(function () {
				return loadScript(cfg.langUrl);
			});
		}
		chain.then(initElfinder).catch(function (err) {
			showError((err && err.message) ? err.message : 'File manager failed to start.');
		});
	});
})(window, document);
