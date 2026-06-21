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

    /* ═══════════════════ PARTICLE SYSTEM — MATRIX RAIN ═══════════════════ */
    function bosCreateParticles() {
        var container = document.getElementById('bosParticles');
        if (!container) return;
        var colors = [
            'rgba(14, 165, 233, .8)',   // sky blue bright
            'rgba(14, 165, 233, .5)',   // sky blue mid
            'rgba(14, 165, 233, .3)',   // sky blue faint
            'rgba(56, 189, 248, .7)',   // light blue
            'rgba(56, 189, 248, .4)',   // light blue faint
            'rgba(99, 102, 241, .6)',   // indigo
            'rgba(99, 102, 241, .3)',   // indigo faint
            'rgba(168, 85, 247, .5)',   // purple
            'rgba(16, 185, 129, .4)',   // emerald
            'rgba(255, 255, 255, .4)',  // white bright
            'rgba(255, 255, 255, .15)'  // white faint
        ];
        var animations = ['bosFloat', 'bosFloatDrift', 'bosFloatStreak'];
        var totalParticles = 220;
        for (var i = 0; i < totalParticles; i++) {
            var p = document.createElement('div');
            p.className = 'bos-login__particle';
            p.style.left = Math.random() * 100 + '%';
            p.style.top = Math.random() * 10 + '%';
            // Speed distribution: 30% fast, 40% medium, 30% slow
            var speed = Math.random();
            var duration;
            if (speed < 0.3) {
                duration = 2.5 + Math.random() * 3.5; // fast: 2.5-6s
            } else if (speed < 0.7) {
                duration = 6 + Math.random() * 6; // medium: 6-12s
            } else {
                duration = 12 + Math.random() * 14; // slow: 12-26s
            }
            p.style.animationDuration = duration + 's';
            // Stagger starts so rain is constant, not in waves
            p.style.animationDelay = (Math.random() * duration) + 's';
            // Size distribution: 50% tiny, 30% medium, 15% large, 5% streak
            var sizeRand = Math.random();
            var size;
            if (sizeRand < 0.50) {
                size = 1 + Math.random() * 1.5; // tiny: 1-2.5px
            } else if (sizeRand < 0.80) {
                size = 2.5 + Math.random() * 2; // medium: 2.5-4.5px
            } else if (sizeRand < 0.95) {
                size = 4.5 + Math.random() * 3.5; // large: 4.5-8px
            } else {
                size = 1.5 + Math.random() * 1; // streak particles (thin, elongated)
            }
            p.style.width = size + 'px';
            p.style.height = (sizeRand >= 0.95 ? size * 4 : size) + 'px';
            if (sizeRand >= 0.95) {
                p.style.borderRadius = size + 'px';
            }
            // Animation type: streaks for thin ones, drift for some, default for rest
            var anim;
            if (sizeRand >= 0.95) {
                anim = 'bosFloatStreak';
            } else if (Math.random() < 0.35) {
                anim = 'bosFloatDrift';
            } else {
                anim = 'bosFloat';
            }
            p.style.animationName = anim;
            var color = colors[Math.floor(Math.random() * colors.length)];
            p.style.background = color;
            // Glow effect — stronger for larger particles
            if (size > 4) {
                p.style.boxShadow = '0 0 ' + (size * 3) + 'px ' + color + ', 0 0 ' + (size * 6) + 'px ' + color.replace(/[\d.]+\)$/, '0.15)');
            } else if (size > 2) {
                p.style.boxShadow = '0 0 ' + (size * 2) + 'px ' + color;
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
