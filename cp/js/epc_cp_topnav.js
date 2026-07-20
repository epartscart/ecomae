/**
 * CP top mega-menu — open/close + fixed panel placement (mirrors ERP topnav).
 */
(function () {
	'use strict';

	function bindCpTopNav() {
		var root = document.getElementById('epc_cp_topnav');
		if (!root || root._epcCpTopnavBound) {
			return;
		}
		root._epcCpTopnavBound = true;
		var items = root.querySelectorAll('.epc-cp-topnav-item');
		var hoverTimer = null;
		var canHover = false;
		try {
			canHover = window.matchMedia && window.matchMedia('(hover: hover) and (pointer: fine)').matches;
		} catch (e) {}

		function closeAll(except) {
			items.forEach(function (item) {
				if (except && item === except) {
					return;
				}
				item.classList.remove('is-open');
				var btn = item.querySelector('[data-topnav-toggle]');
				var panel = item.querySelector('[data-topnav-panel]');
				if (btn) {
					btn.setAttribute('aria-expanded', 'false');
				}
				if (panel) {
					panel.hidden = true;
				}
			});
			document.body.classList.remove('epc-cp-topnav-open');
		}

		function placePanel(item, panel) {
			if (!panel || !root) {
				return;
			}
			var rootRect = root.getBoundingClientRect();
			var btn = item.querySelector('.epc-cp-topnav-btn');
			var btnRect = btn ? btn.getBoundingClientRect() : rootRect;
			var top = Math.round(rootRect.bottom);
			panel.style.top = top + 'px';
			panel.style.maxHeight = Math.max(180, Math.floor(window.innerHeight - top - 12)) + 'px';
			panel.style.left = '0px';
			panel.style.right = 'auto';
			panel.hidden = false;
			var panelWidth = panel.offsetWidth || 720;
			var left = Math.round(btnRect.left);
			var maxLeft = Math.max(8, window.innerWidth - panelWidth - 8);
			if (left > maxLeft) {
				left = maxLeft;
			}
			if (left < 8) {
				left = 8;
			}
			panel.style.left = left + 'px';
		}

		function openItem(item) {
			if (!item) {
				return;
			}
			closeAll(item);
			item.classList.add('is-open');
			var btn = item.querySelector('[data-topnav-toggle]');
			var panel = item.querySelector('[data-topnav-panel]');
			if (btn) {
				btn.setAttribute('aria-expanded', 'true');
			}
			if (panel) {
				placePanel(item, panel);
			}
			document.body.classList.add('epc-cp-topnav-open');
		}

		items.forEach(function (item) {
			var btn = item.querySelector('[data-topnav-toggle]');
			if (!btn) {
				return;
			}
			btn.addEventListener('click', function (e) {
				e.preventDefault();
				e.stopPropagation();
				if (item.classList.contains('is-open')) {
					closeAll();
				} else {
					openItem(item);
				}
			});
			if (canHover) {
				item.addEventListener('mouseenter', function () {
					if (hoverTimer) {
						clearTimeout(hoverTimer);
						hoverTimer = null;
					}
					openItem(item);
				});
				item.addEventListener('mouseleave', function () {
					hoverTimer = setTimeout(function () {
						if (!item.matches(':hover')) {
							closeAll();
						}
					}, 160);
				});
			}
		});

		document.addEventListener('click', function (e) {
			if (!root.contains(e.target)) {
				closeAll();
			}
		});
		document.addEventListener('keydown', function (e) {
			if (e.key === 'Escape') {
				closeAll();
			}
		});
		window.addEventListener('resize', function () {
			var open = root.querySelector('.epc-cp-topnav-item.is-open');
			if (!open) {
				return;
			}
			var panel = open.querySelector('[data-topnav-panel]');
			if (panel && !panel.hidden) {
				placePanel(open, panel);
			}
		});
	}

	function boot() {
		bindCpTopNav();
	}

	if (document.readyState === 'loading') {
		document.addEventListener('DOMContentLoaded', boot);
	} else {
		boot();
	}
})();
