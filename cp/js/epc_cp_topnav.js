/**
 * CP top mega-menu — fast click-to-toggle on the whole tab (label + icon + caret).
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

		function toggleItem(item) {
			if (item.classList.contains('is-open')) {
				closeAll();
			} else {
				openItem(item);
			}
		}

		items.forEach(function (item) {
			var btn = item.querySelector('[data-topnav-toggle]');
			if (!btn) {
				return;
			}
			var skippedClick = false;

			function onToggle(e) {
				if (e) {
					e.preventDefault();
					e.stopPropagation();
				}
				if (hoverTimer) {
					clearTimeout(hoverTimer);
					hoverTimer = null;
				}
				toggleItem(item);
			}

			// pointerdown = instant open/close on text, icon, or caret (whole button)
			btn.addEventListener(
				'pointerdown',
				function (e) {
					if (e.button != null && e.button !== 0) {
						return;
					}
					skippedClick = true;
					onToggle(e);
				},
				{ passive: false }
			);
			// Keyboard / assistive click (skip duplicate after pointerdown)
			btn.addEventListener('click', function (e) {
				e.preventDefault();
				e.stopPropagation();
				if (skippedClick) {
					skippedClick = false;
					return;
				}
				onToggle(e);
			});

			// Close when leaving the tab+panel (no hover-open — that races with click)
			item.addEventListener('mouseleave', function () {
				hoverTimer = setTimeout(function () {
					if (!item.matches(':hover') && item.classList.contains('is-open')) {
						closeAll();
					}
				}, 120);
			});
			item.addEventListener('mouseenter', function () {
				if (hoverTimer) {
					clearTimeout(hoverTimer);
					hoverTimer = null;
				}
			});
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
