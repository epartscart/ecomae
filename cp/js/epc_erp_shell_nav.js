/**
 * ERP CP shell — sidebar accordion + shell navigation guard.
 * Loaded from erp_desktop.php head (never inline in eval'd CP content pane).
 */
(function () {
	'use strict';

	var SHELL_Q = 'epc_erp_shell=1';
	var erpKey = 'epc_erp_menu_groups_open';
	var PLATFORM_ERP_SEG = '/platform-erp/';

	function isPlatformErpPage() {
		try {
			return (location.pathname || '').indexOf(PLATFORM_ERP_SEG) !== -1;
		} catch (e) {
			return false;
		}
	}

	function fixErpHrefRoutePrefix(href) {
		if (!href || !isPlatformErpPage()) {
			return href;
		}
		if (href.indexOf(PLATFORM_ERP_SEG) !== -1) {
			return href;
		}
		try {
			var u = document.createElement('a');
			u.href = href;
			var path = u.pathname || '';
			if (path.indexOf('/shop/finance/erp') === -1) {
				return href;
			}
			var fixed = path.replace(/(\/[^/]+)\/shop\/finance\/erp/, '$1/platform-erp/shop/finance/erp');
			if (fixed === path) {
				return href;
			}
			u.pathname = fixed;
			return u.href;
		} catch (e) {
			return href.replace('/shop/finance/erp', '/platform-erp/shop/finance/erp');
		}
	}

	function readSaved() {
		try {
			return JSON.parse(localStorage.getItem(erpKey) || 'null');
		} catch (e) {
			return null;
		}
	}

	function writeSaved() {
		var open = [];
		document.querySelectorAll('.epc-erp-sidebar-group.is-open').forEach(function (g) {
			var area = g.getAttribute('data-area');
			if (area) {
				open.push(area);
			}
		});
		try {
			localStorage.setItem(erpKey, JSON.stringify(open));
		} catch (e) {}
	}

	function setGroupOpen(grp, open, closeSiblings) {
		if (!grp) {
			return;
		}
		var btn = grp.querySelector('.epc-erp-sidebar-group-hd');
		if (open && closeSiblings !== false) {
			document.querySelectorAll('.epc-erp-sidebar-group.is-open').forEach(function (other) {
				if (other !== grp) {
					setGroupOpen(other, false, false);
				}
			});
		}
		grp.classList.toggle('is-open', open);
		if (btn) {
			btn.setAttribute('aria-expanded', open ? 'true' : 'false');
		}
	}

	window.epcErpMenuSectionsSave = writeSaved;

	window.epcErpMenuSectionsInit = function () {
		var activeGroup = document.querySelector('.epc-erp-sidebar-group.is-active-area');
		document.querySelectorAll('.epc-erp-sidebar-group.is-open').forEach(function (g) {
			g.classList.remove('is-open');
			var h = g.querySelector('.epc-erp-sidebar-group-hd');
			if (h) {
				h.setAttribute('aria-expanded', 'false');
			}
		});
		var saved = readSaved();
		if (saved && saved.length) {
			saved.forEach(function (areaKey) {
				var g = document.querySelector('.epc-erp-sidebar-group[data-area="' + areaKey + '"]');
				if (g) {
					setGroupOpen(g, true, false);
				}
			});
			return;
		}
		if (activeGroup) {
			setGroupOpen(activeGroup, true, false);
		}
	};

	function isErpFinancePath(href) {
		if (!href) {
			return false;
		}
		try {
			var u = document.createElement('a');
			u.href = href;
			return (u.pathname || '').indexOf('/shop/finance/erp') !== -1;
		} catch (e) {
			return href.indexOf('/shop/finance/erp') !== -1;
		}
	}

	function appendShellQuery(href) {
		if (!href || href.indexOf('javascript') === 0 || href.indexOf('#') === 0) {
			return href;
		}
		href = fixErpHrefRoutePrefix(href);
		if (!isErpFinancePath(href)) {
			return href;
		}
		if (href.indexOf('epc_erp_shell=') !== -1) {
			return href;
		}
		return href + (href.indexOf('?') >= 0 ? '&' : '?') + SHELL_Q;
	}

	function isErpShellNavLink(a) {
		if (!a) {
			return false;
		}
		if (a.hasAttribute('data-epc-erp-shell')) {
			return true;
		}
		return !!a.closest('.epc-erp-sidebar-item, .epc-erp-breadcrumb, .epc-erp-content-actions');
	}

	function ensureShellNavLinks() {
		document.querySelectorAll(
			'.epc-erp-sidebar-item a[href], .epc-erp-breadcrumb a[href], .epc-erp-content-actions a[href], a[data-epc-erp-shell]'
		).forEach(function (a) {
			var href = a.getAttribute('href') || '';
			if (!href || href.indexOf('javascript') === 0 || href.indexOf('#') === 0) {
				return;
			}
			if (!isErpFinancePath(href)) {
				return;
			}
			var fixed = appendShellQuery(href);
			if (fixed !== href) {
				a.setAttribute('href', fixed);
			}
			a.setAttribute('data-epc-erp-shell', '1');
		});
	}

	function interceptShellNav() {
		if (document.body && document.body._epcErpShellNavBound) {
			return;
		}
		if (document.body) {
			document.body._epcErpShellNavBound = true;
		}
		document.addEventListener(
			'click',
			function (e) {
				var a = e.target.closest('a[href]');
				if (!a) {
					return;
				}
				if (a.getAttribute('target') === '_blank') {
					return;
				}
				if (!isErpShellNavLink(a)) {
					return;
				}
				var href = a.getAttribute('href') || '';
				if (!isErpFinancePath(href)) {
					return;
				}
				var fixed = appendShellQuery(href);
				e.preventDefault();
				e.stopPropagation();
				if (typeof e.stopImmediatePropagation === 'function') {
					e.stopImmediatePropagation();
				}
				window.location.assign(fixed);
			},
			true
		);
	}

	function setCategoryOpen(cat, open) {
		if (!cat) {
			return;
		}
		var btn = cat.querySelector('.epc-erp-sidebar-category-hd');
		cat.classList.toggle('is-open', open);
		if (btn) {
			btn.setAttribute('aria-expanded', open ? 'true' : 'false');
		}
	}

	function bindAccordion() {
		var sidebar = document.getElementById('epc_erp_sidebar');
		if (!sidebar) {
			return;
		}
		sidebar.style.setProperty('pointer-events', 'auto', 'important');
		ensureShellNavLinks();
		// Bind category-level toggle (outer layer)
		document.querySelectorAll('.epc-erp-sidebar-category-hd').forEach(function (btn) {
			if (btn._epcErpCategoryBound) {
				return;
			}
			btn._epcErpCategoryBound = true;
			btn.addEventListener('click', function (e) {
				e.preventDefault();
				e.stopPropagation();
				var cat = btn.closest('.epc-erp-sidebar-category');
				if (!cat) {
					return;
				}
				var wasOpen = cat.classList.contains('is-open');
				// Close all other categories
				document.querySelectorAll('.epc-erp-sidebar-category.is-open').forEach(function (other) {
					if (other !== cat) {
						setCategoryOpen(other, false);
					}
				});
				setCategoryOpen(cat, !wasOpen);
			});
		});
		// Bind area-level toggle (inner layer) — same as before
		document.querySelectorAll('.epc-erp-sidebar-group-hd').forEach(function (btn) {
			if (btn._epcErpAccordionBound) {
				return;
			}
			btn._epcErpAccordionBound = true;
			btn.addEventListener('click', function (e) {
				e.preventDefault();
				e.stopPropagation();
				var grp = btn.closest('.epc-erp-sidebar-group');
				if (!grp) {
					return;
				}
				var wasOpen = grp.classList.contains('is-open');
				setGroupOpen(grp, !wasOpen, true);
				writeSaved();
			});
		});
		window.epcErpMenuSectionsInit();
	}

	function bindMobileSidebar() {
		var sidebar = document.getElementById('epc_erp_sidebar');
		var backdrop = document.getElementById('epc_erp_sidebar_backdrop');
		var toggleBtn = document.getElementById('epc_erp_sidebar_toggle');
		var closeBtn = document.getElementById('epc_erp_sidebar_close');

		function setSidebarOpen(open) {
			if (!sidebar) {
				return;
			}
			document.body.classList.toggle('epc-erp-sidebar-open', open);
			if (toggleBtn) {
				toggleBtn.setAttribute('aria-expanded', open ? 'true' : 'false');
			}
		}

		if (toggleBtn && !toggleBtn._epcErpToggleBound) {
			toggleBtn._epcErpToggleBound = true;
			toggleBtn.addEventListener('click', function () {
				setSidebarOpen(true);
			});
		}
		if (closeBtn && !closeBtn._epcErpCloseBound) {
			closeBtn._epcErpCloseBound = true;
			closeBtn.addEventListener('click', function () {
				setSidebarOpen(false);
			});
		}
		if (backdrop && !backdrop._epcErpBackdropBound) {
			backdrop._epcErpBackdropBound = true;
			backdrop.addEventListener('click', function () {
				setSidebarOpen(false);
			});
		}
	}

	var NAV_COLLAPSE_KEY = 'epc_erp_nav_collapsed';

	function applyNavCollapsed(collapsed) {
		if (!document.body) {
			return;
		}
		document.body.classList.toggle('epc-erp-nav-collapsed', collapsed);
		var btn = document.getElementById('epc_erp_sidebar_collapse_toggle');
		if (btn) {
			btn.setAttribute('aria-expanded', collapsed ? 'false' : 'true');
			var ic = btn.querySelector('.fa');
			if (ic) {
				ic.classList.toggle('fa-chevron-left', !collapsed);
				ic.classList.toggle('fa-chevron-right', collapsed);
			}
		}
	}

	function bindNavCollapse() {
		var btn = document.getElementById('epc_erp_sidebar_collapse_toggle');
		var hasTopnav = !!document.getElementById('epc_erp_topnav');
		var saved = null;
		try {
			saved = localStorage.getItem(NAV_COLLAPSE_KEY);
		} catch (e) {}
		// With top mega-menu, default to a collapsed rail for more content width.
		var collapsed = saved === null ? hasTopnav : saved === '1';
		applyNavCollapsed(collapsed);
		if (btn && !btn._epcErpCollapseBound) {
			btn._epcErpCollapseBound = true;
			btn.addEventListener('click', function () {
				var next = !document.body.classList.contains('epc-erp-nav-collapsed');
				applyNavCollapsed(next);
				try {
					localStorage.setItem(NAV_COLLAPSE_KEY, next ? '1' : '0');
				} catch (e) {}
			});
		}
	}

	function bindTopNav() {
		var root = document.getElementById('epc_erp_topnav');
		if (!root || root._epcErpTopnavBound) {
			return;
		}
		root._epcErpTopnavBound = true;
		var items = root.querySelectorAll('.epc-erp-topnav-item');
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
			document.body.classList.remove('epc-erp-topnav-open');
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
				panel.hidden = false;
			}
			document.body.classList.add('epc-erp-topnav-open');
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

		var moreBtn = document.getElementById('epc_erp_topnav_more');
		if (moreBtn && !moreBtn._epcErpTopnavMoreBound) {
			moreBtn._epcErpTopnavMoreBound = true;
			moreBtn.addEventListener('click', function (e) {
				e.preventDefault();
				closeAll();
				applyNavCollapsed(false);
				try {
					localStorage.setItem(NAV_COLLAPSE_KEY, '0');
				} catch (err) {}
				document.body.classList.add('epc-erp-sidebar-open');
				var toggleBtn = document.getElementById('epc_erp_sidebar_toggle');
				if (toggleBtn) {
					toggleBtn.setAttribute('aria-expanded', 'true');
				}
				var sidebar = document.getElementById('epc_erp_sidebar');
				if (sidebar) {
					sidebar.scrollTop = 0;
				}
			});
		}
	}

	function clearCpCollapseForErp() {
		document.documentElement.classList.remove('epc-cp-sidebar-collapsed');
		if (document.body) {
			document.body.classList.remove('epc-cp-sidebar-collapsed', 'hide-sidebar');
		}
		try {
			localStorage.setItem('epc_cp_sidebar_collapsed', '0');
		} catch (e) {}
	}

	function boot() {
		clearCpCollapseForErp();
		ensureShellNavLinks();
		interceptShellNav();
		bindAccordion();
		bindMobileSidebar();
		bindNavCollapse();
		bindTopNav();
	}

	if (document.readyState === 'loading') {
		document.addEventListener('DOMContentLoaded', boot);
	} else {
		boot();
	}
})();
