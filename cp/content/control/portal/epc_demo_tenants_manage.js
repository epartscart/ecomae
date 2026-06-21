(function () {
	'use strict';

	function copyText(text, btn) {
		if (!text) {
			return;
		}
		var done = function () {
			if (!btn) {
				return;
			}
			var prev = btn.textContent;
			btn.textContent = 'Copied';
			window.setTimeout(function () {
				btn.textContent = prev;
			}, 1200);
		};
		if (navigator.clipboard && navigator.clipboard.writeText) {
			navigator.clipboard.writeText(text).then(done).catch(function () {
				fallbackCopy(text, done);
			});
			return;
		}
		fallbackCopy(text, done);
	}

	function fallbackCopy(text, done) {
		var ta = document.createElement('textarea');
		ta.value = text;
		document.body.appendChild(ta);
		ta.select();
		try {
			document.execCommand('copy');
			done();
		} catch (e) {}
		document.body.removeChild(ta);
	}

	document.addEventListener('click', function (e) {
		var copyBtn = e.target.closest('.epc-demo-copy');
		if (copyBtn) {
			e.preventDefault();
			copyText(copyBtn.getAttribute('data-copy') || '', copyBtn);
			return;
		}
		var copyAllBtn = e.target.closest('.epc-demo-copy-all');
		if (copyAllBtn) {
			e.preventDefault();
			copyText(copyAllBtn.getAttribute('data-copy') || '', copyAllBtn);
		}
	});
})();
