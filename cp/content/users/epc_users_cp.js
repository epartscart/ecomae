/**
 * Shared helpers for Users CP (groups tree + user editor).
 */
(function (window, document) {
	'use strict';

	function bindTreeFilter(inputId, getTree) {
		var input = document.getElementById(inputId);
		if (!input || typeof getTree !== 'function') {
			return;
		}
		var timer = null;
		input.addEventListener('input', function () {
			var q = this.value;
			clearTimeout(timer);
			timer = setTimeout(function () {
				var tree = getTree();
				if (!tree || typeof tree.filter !== 'function') {
					return;
				}
				if (!q) {
					tree.filter('#value#', '');
					if (typeof tree.openAll === 'function') {
						tree.openAll();
					}
					return;
				}
				tree.filter('#value#', q);
			}, 140);
		});
	}

	window.epcUsersBindTreeFilter = bindTreeFilter;

	window.epcUsersEsc = function (v) {
		return String(v == null ? '' : v)
			.replace(/&/g, '&amp;')
			.replace(/</g, '&lt;')
			.replace(/>/g, '&gt;')
			.replace(/"/g, '&quot;');
	};
})(window, document);
