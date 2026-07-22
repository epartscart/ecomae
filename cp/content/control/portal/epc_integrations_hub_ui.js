/**
 * Integrations hub — search + category filter (external; CP base-href safe).
 */
(function () {
	'use strict';
	function ready(fn) {
		if (document.readyState === 'loading') {
			document.addEventListener('DOMContentLoaded', fn);
		} else {
			fn();
		}
	}
	ready(function () {
		var root = document.getElementById('epc-inthub-root');
		if (!root) return;
		var search = root.querySelector('[data-inthub-search]');
		var chips = root.querySelectorAll('[data-inthub-chip]');
		var cards = root.querySelectorAll('[data-inthub-card]');
		var sections = root.querySelectorAll('[data-inthub-section]');
		var empty = root.querySelector('[data-inthub-empty]');
		var cat = 'all';

		function apply() {
			var q = (search && search.value ? search.value : '').toLowerCase().trim();
			var visible = 0;
			cards.forEach(function (card) {
				var hay = (card.getAttribute('data-search') || '').toLowerCase();
				var c = card.getAttribute('data-category') || '';
				var okCat = cat === 'all' || c === cat;
				var okQ = !q || hay.indexOf(q) !== -1;
				var show = okCat && okQ;
				card.classList.toggle('is-hidden', !show);
				if (show) visible += 1;
			});
			sections.forEach(function (sec) {
				var any = false;
				sec.querySelectorAll('[data-inthub-card]').forEach(function (card) {
					if (!card.classList.contains('is-hidden')) any = true;
				});
				sec.classList.toggle('is-hidden', !any);
			});
			if (empty) empty.classList.toggle('is-visible', visible === 0);
		}

		chips.forEach(function (chip) {
			chip.addEventListener('click', function () {
				cat = chip.getAttribute('data-inthub-chip') || 'all';
				chips.forEach(function (c) {
					c.classList.toggle('is-active', c === chip);
				});
				apply();
			});
		});
		if (search) {
			search.addEventListener('input', apply);
		}
		apply();
	});
})();
