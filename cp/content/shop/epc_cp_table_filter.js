/**
 * Lightweight CP table filter — show/hide rows by text match.
 * Usage: epcCpBindTableFilter({ inputId, tableId, countId? })
 */
(function (w) {
	'use strict';
	function normalize(s) {
		return String(s || '').toLowerCase().replace(/\s+/g, ' ').trim();
	}
	function bind(opts) {
		opts = opts || {};
		var input = document.getElementById(opts.inputId || '');
		var table = document.getElementById(opts.tableId || '');
		if (!input || !table) {
			return;
		}
		var tbody = table.tBodies && table.tBodies[0] ? table.tBodies[0] : table.querySelector('tbody');
		if (!tbody) {
			return;
		}
		var countEl = opts.countId ? document.getElementById(opts.countId) : null;
		var apply = function () {
			var q = normalize(input.value);
			var rows = tbody.querySelectorAll('tr');
			var shown = 0;
			var total = 0;
			Array.prototype.forEach.call(rows, function (tr) {
				total++;
				var hay = normalize(tr.getAttribute('data-epc-filter') || tr.textContent || '');
				var ok = !q || hay.indexOf(q) !== -1;
				tr.style.display = ok ? '' : 'none';
				if (ok) {
					shown++;
				}
			});
			if (countEl) {
				countEl.textContent = shown + ' / ' + total;
			}
		};
		input.addEventListener('input', apply);
		input.addEventListener('keyup', apply);
		apply();
	}
	w.epcCpBindTableFilter = bind;
	if (document.readyState === 'loading') {
		document.addEventListener('DOMContentLoaded', function () {
			if (w.EPC_CP_TABLE_FILTERS && w.EPC_CP_TABLE_FILTERS.length) {
				w.EPC_CP_TABLE_FILTERS.forEach(bind);
			}
		});
	} else if (w.EPC_CP_TABLE_FILTERS && w.EPC_CP_TABLE_FILTERS.length) {
		w.EPC_CP_TABLE_FILTERS.forEach(bind);
	}
})(window);
