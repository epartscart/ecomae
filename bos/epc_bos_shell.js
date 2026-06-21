/**
 * BOS — Business Operating System
 * Client-side shell controller
 */
(function () {
    'use strict';

    /* ═══════════════════ LOGIN PAGE ═══════════════════ */
    var loginForm = document.getElementById('bosLoginForm');
    var loginFormErp = document.getElementById('bosLoginFormErp');

    if (loginForm || loginFormErp) {
        // Particle generator
        bosCreateParticles();

        // Animated counter
        bosAnimateCounters();

        // Role tab switching
        var tabProvider = document.getElementById('bosTabProvider');
        var tabErp = document.getElementById('bosTabErp');
        var formProvider = document.getElementById('bosFormProvider');
        var formErp = document.getElementById('bosFormErp');

        if (tabProvider && tabErp) {
            tabProvider.addEventListener('click', function () {
                tabProvider.classList.add('bos-login__role-tab--active');
                tabErp.classList.remove('bos-login__role-tab--active');
                formProvider.classList.add('bos-login__form-wrap--active');
                formErp.classList.remove('bos-login__form-wrap--active');
            });
            tabErp.addEventListener('click', function () {
                tabErp.classList.add('bos-login__role-tab--active');
                tabProvider.classList.remove('bos-login__role-tab--active');
                formErp.classList.add('bos-login__form-wrap--active');
                formProvider.classList.remove('bos-login__form-wrap--active');
            });
        }

        // Attach submit handler to both forms
        [loginForm, loginFormErp].forEach(function (form) {
            if (!form) return;
            form.addEventListener('submit', function (e) {
                e.preventDefault();
                var btn = form.querySelector('button[type="submit"]');
                var errEl = form.querySelector('.bos-login__error');
                if (errEl) errEl.textContent = '';
                btn.classList.add('bos-login__btn--loading');
                btn.disabled = true;

                var fd = new FormData(form);
                fetch('/bos/?action=login', { method: 'POST', body: fd })
                    .then(function (r) { return r.json(); })
                    .then(function (data) {
                        if (data.ok) {
                            window.location.href = data.redirect || '/bos/';
                        } else {
                            if (errEl) errEl.innerHTML = '<i class="fa fa-exclamation-circle"></i> ' + (data.error || 'Login failed');
                            btn.classList.remove('bos-login__btn--loading');
                            btn.disabled = false;
                        }
                    })
                    .catch(function () {
                        if (errEl) errEl.innerHTML = '<i class="fa fa-exclamation-circle"></i> Network error — please try again';
                        btn.classList.remove('bos-login__btn--loading');
                        btn.disabled = false;
                    });
            });
        });

        return; // login page — don't initialize dashboard JS
    }

    /* ═══════════════════ SIDEBAR TOGGLE ═══════════════════ */
    var sidebar = document.getElementById('bosSidebar');
    var toggleBtn = document.getElementById('bosSidebarToggle');

    if (toggleBtn && sidebar) {
        var collapsed = localStorage.getItem('bos_sidebar_collapsed') === '1';
        if (collapsed) {
            sidebar.classList.add('bos-sidebar--collapsed');
            document.body.classList.add('bos-sidebar-collapsed');
        }
        toggleBtn.addEventListener('click', function () {
            var isCollapsed = sidebar.classList.toggle('bos-sidebar--collapsed');
            document.body.classList.toggle('bos-sidebar-collapsed', isCollapsed);
            localStorage.setItem('bos_sidebar_collapsed', isCollapsed ? '1' : '0');

            if (window.innerWidth <= 768) {
                sidebar.classList.toggle('bos-sidebar--mobile-open');
            }
        });
    }

    /* ═══════════════════ SIDEBAR SECTIONS ═══════════════════ */
    var sectionHeaders = document.querySelectorAll('.bos-sidebar__section-header');
    sectionHeaders.forEach(function (header) {
        header.addEventListener('click', function () {
            var section = header.closest('.bos-sidebar__section');
            section.classList.toggle('bos-sidebar__section--collapsed');
        });
    });

    /* ═══════════════════ TENANT SWITCHER ═══════════════════ */
    var switcherBtn = document.getElementById('bosTenantBtn');
    var switcherDropdown = document.getElementById('bosTenantDropdown');
    var switcherContainer = document.getElementById('bosTenantSwitcher');
    var tenantSearch = document.getElementById('bosTenantSearch');
    var tenantList = document.getElementById('bosTenantList');
    var filterBtns = document.querySelectorAll('.bos-filter-btn');

    if (switcherBtn && switcherDropdown && switcherContainer) {
        switcherBtn.addEventListener('click', function (e) {
            e.stopPropagation();
            var isOpen = switcherContainer.classList.toggle('bos-tenant-switcher--open');
            if (isOpen && tenantSearch) {
                setTimeout(function () { tenantSearch.focus(); }, 100);
            }
        });

        document.addEventListener('click', function (e) {
            if (!switcherContainer.contains(e.target)) {
                switcherContainer.classList.remove('bos-tenant-switcher--open');
            }
        });

        renderTenantList(window.BOS.tenants, '');

        if (tenantSearch) {
            tenantSearch.addEventListener('input', function () {
                var q = tenantSearch.value.toLowerCase().trim();
                var filter = document.querySelector('.bos-filter-btn--active');
                var type = filter ? filter.getAttribute('data-filter') : 'all';
                renderTenantList(window.BOS.tenants, q, type);
            });
        }

        filterBtns.forEach(function (btn) {
            btn.addEventListener('click', function () {
                filterBtns.forEach(function (b) { b.classList.remove('bos-filter-btn--active'); });
                btn.classList.add('bos-filter-btn--active');
                var type = btn.getAttribute('data-filter');
                var q = tenantSearch ? tenantSearch.value.toLowerCase().trim() : '';
                renderTenantList(window.BOS.tenants, q, type);
            });
        });
    }

    function renderTenantList(tenants, query, typeFilter) {
        if (!tenantList) return;
        typeFilter = typeFilter || 'all';
        var html = '';
        var filtered = tenants.filter(function (t) {
            if (typeFilter !== 'all' && t.type !== typeFilter) return false;
            if (query === '') return true;
            var hay = (t.trade_name + ' ' + t.site_key + ' ' + t.hostname + ' ' + t.industry_name).toLowerCase();
            return hay.indexOf(query) !== -1;
        });

        if (filtered.length === 0) {
            html = '<div style="padding:20px;text-align:center;color:#94a3b8;font-size:13px;">No tenants found</div>';
        } else {
            filtered.forEach(function (t) {
                var isActive = t.site_key === window.BOS.activeTenant;
                html += '<a href="/bos/?t=' + encodeURIComponent(t.site_key) + '" class="bos-tenant-switcher__item' + (isActive ? ' bos-tenant-switcher__item--active' : '') + '">';
                html += '<div class="bos-tenant-switcher__item-icon"><i class="fa ' + esc(t.industry_icon) + '"></i></div>';
                html += '<div class="bos-tenant-switcher__item-info">';
                html += '<div class="bos-tenant-switcher__item-name">' + esc(t.trade_name || t.site_key) + '</div>';
                html += '<div class="bos-tenant-switcher__item-meta">' + esc(t.industry_name);
                if (t.hostname) html += ' &middot; ' + esc(t.hostname);
                html += '</div></div>';
                html += '<span class="bos-badge ' + badgeClass(t.type) + '">' + esc(typeLabel(t.type)) + '</span>';
                html += '</a>';
            });
        }
        tenantList.innerHTML = html;
    }

    function badgeClass(type) {
        var m = { commerce: 'bos-badge--commerce', erp_only: 'bos-badge--erp', demo: 'bos-badge--demo', platform: 'bos-badge--platform' };
        return m[type] || 'bos-badge--default';
    }

    function typeLabel(type) {
        var m = { commerce: 'Commerce', erp_only: 'ERP', demo: 'Demo', platform: 'Platform' };
        return m[type] || 'Tenant';
    }

    function esc(s) {
        if (!s) return '';
        var d = document.createElement('div');
        d.textContent = s;
        return d.innerHTML;
    }

    /* ═══════════════════ KEYBOARD SHORTCUTS ═══════════════════ */
    document.addEventListener('keydown', function (e) {
        if (e.key === 'Escape' && switcherContainer) {
            switcherContainer.classList.remove('bos-tenant-switcher--open');
        }
        if ((e.ctrlKey || e.metaKey) && e.key === 'k' && switcherBtn) {
            e.preventDefault();
            switcherBtn.click();
        }
    });

    /* ═══════════════════ PARTICLE SYSTEM ═══════════════════ */
    function bosCreateParticles() {
        var container = document.getElementById('bosParticles');
        if (!container) return;
        for (var i = 0; i < 30; i++) {
            var p = document.createElement('div');
            p.className = 'bos-login__particle';
            p.style.left = Math.random() * 100 + '%';
            p.style.animationDuration = (8 + Math.random() * 12) + 's';
            p.style.animationDelay = (Math.random() * 10) + 's';
            p.style.width = p.style.height = (2 + Math.random() * 3) + 'px';
            if (Math.random() > 0.5) {
                p.style.background = 'rgba(99, 102, 241, .4)';
            }
            container.appendChild(p);
        }
    }

    /* ═══════════════════ ANIMATED COUNTERS ═══════════════════ */
    function bosAnimateCounters() {
        var counters = document.querySelectorAll('.bos-login__stat-num[data-count]');
        if (!counters.length) return;
        setTimeout(function () {
            counters.forEach(function (el) {
                var target = parseInt(el.getAttribute('data-count'), 10);
                var duration = 2000;
                var start = 0;
                var startTime = null;
                function step(ts) {
                    if (!startTime) startTime = ts;
                    var progress = Math.min((ts - startTime) / duration, 1);
                    var eased = 1 - Math.pow(1 - progress, 3);
                    el.textContent = Math.floor(eased * target);
                    if (progress < 1) {
                        requestAnimationFrame(step);
                    } else {
                        el.textContent = target + '+';
                    }
                }
                requestAnimationFrame(step);
            });
        }, 1000);
    }

})();
