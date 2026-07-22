/**
 * Registration fields CP helpers (filter + category chips).
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
					tree.filter(function () { return true; });
					if (typeof tree.openAll === 'function') {
						tree.openAll();
					}
					return;
				}
				var needle = String(q).toLowerCase();
				tree.filter(function (obj) {
					var hay = ((obj.value || '') + ' ' + (obj.name || '') + ' ' + (obj.field_category || '') + ' ' + (obj.compliance_tag || '')).toLowerCase();
					return hay.indexOf(needle) >= 0;
				});
			}, 120);
		});
	}

	function bindCategoryChips(chipSelector, getTree) {
		var chips = document.querySelectorAll(chipSelector);
		if (!chips.length || typeof getTree !== 'function') {
			return;
		}
		chips.forEach(function (chip) {
			chip.addEventListener('click', function () {
				chips.forEach(function (c) { c.classList.remove('is-active'); });
				chip.classList.add('is-active');
				var cat = chip.getAttribute('data-category') || '';
				var tree = getTree();
				if (!tree || typeof tree.filter !== 'function') {
					return;
				}
				if (!cat || cat === 'all') {
					tree.filter(function () { return true; });
					if (typeof tree.openAll === 'function') {
						tree.openAll();
					}
					return;
				}
				tree.filter(function (obj) {
					return String(obj.field_category || 'general') === cat;
				});
			});
		});
	}

	window.epcRfBindTreeFilter = bindTreeFilter;
	window.epcRfBindCategoryChips = bindCategoryChips;
})(window, document);
