/**
 * BOS — Business Operating System
 * Client-side shell controller
 */
(function () {
    'use strict';

    function bosCsrf() {
        if (window.EPC_BOS_CSRF) return String(window.EPC_BOS_CSRF);
        var meta = document.querySelector('meta[name="epc-bos-csrf"]');
        return meta ? String(meta.getAttribute('content') || '') : '';
    }

    function bosAjax(fd) {
        if (!(fd instanceof FormData)) {
            fd = new FormData();
        }
        var token = bosCsrf();
        if (token) {
            fd.append('epc_csrf', token);
        }
        return fetch('/bos/?action=ajax', {
            method: 'POST',
            body: fd,
            credentials: 'same-origin',
            headers: token ? { 'X-EPC-CSRF': token } : {}
        }).then(function (r) { return r.json(); });
    }
    window.bosAjax = bosAjax;

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

    /* ═══════════════════ BLACK TOP MEGA-MENU ═══════════════════ */
    (function bindBosTopnav() {
        var root = document.getElementById('bosTopnav');
        if (!root || root._bosTopBound) return;
        root._bosTopBound = true;
        var items = root.querySelectorAll('.bos-topnav__item');

        function closeAll(except) {
            items.forEach(function (it) {
                if (except && it === except) return;
                it.classList.remove('is-open');
                var b = it.querySelector('[data-bos-topnav-toggle]');
                var p = it.querySelector('[data-bos-topnav-panel]');
                if (b) b.setAttribute('aria-expanded', 'false');
                if (p) p.hidden = true;
            });
            document.body.classList.remove('bos-topnav-open');
        }

        function place(it, p) {
            var rr = root.getBoundingClientRect();
            var btn = it.querySelector('.bos-topnav__btn');
            var br = btn ? btn.getBoundingClientRect() : rr;
            var top = Math.round(rr.bottom);
            p.style.top = top + 'px';
            p.style.maxHeight = Math.max(180, Math.floor(window.innerHeight - top - 12)) + 'px';
            p.hidden = false;
            var w = p.offsetWidth || 640;
            var left = Math.round(br.left);
            var maxLeft = Math.max(8, window.innerWidth - w - 8);
            if (left > maxLeft) left = maxLeft;
            if (left < 8) left = 8;
            p.style.left = left + 'px';
            p.style.right = 'auto';
        }

        function open(it) {
            closeAll(it);
            it.classList.add('is-open');
            var b = it.querySelector('[data-bos-topnav-toggle]');
            var p = it.querySelector('[data-bos-topnav-panel]');
            if (b) b.setAttribute('aria-expanded', 'true');
            if (p) place(it, p);
            document.body.classList.add('bos-topnav-open');
        }

        items.forEach(function (it) {
            var b = it.querySelector('[data-bos-topnav-toggle]');
            if (!b) return;
            b.addEventListener('click', function (e) {
                e.preventDefault();
                e.stopPropagation();
                if (it.classList.contains('is-open')) {
                    closeAll();
                } else {
                    open(it);
                }
            });
        });

        document.addEventListener('click', function (e) {
            if (!root.contains(e.target)) closeAll();
        });
        document.addEventListener('keydown', function (e) {
            if (e.key === 'Escape') closeAll();
        });
        window.addEventListener('resize', function () {
            items.forEach(function (it) {
                if (!it.classList.contains('is-open')) return;
                var p = it.querySelector('[data-bos-topnav-panel]');
                if (p) place(it, p);
            });
        });
    })();

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
        var totalParticles = 220;
        for (var i = 0; i < totalParticles; i++) {
            var p = document.createElement('div');
            p.className = 'bos-login__particle';
            p.style.left = Math.random() * 100 + '%';
            p.style.top = Math.random() * 10 + '%';
            var speed = Math.random();
            var duration;
            if (speed < 0.3) {
                duration = 2.5 + Math.random() * 3.5;
            } else if (speed < 0.7) {
                duration = 6 + Math.random() * 6;
            } else {
                duration = 12 + Math.random() * 14;
            }
            p.style.animationDuration = duration + 's';
            p.style.animationDelay = (Math.random() * duration) + 's';
            var sizeRand = Math.random();
            var size;
            if (sizeRand < 0.50) {
                size = 1 + Math.random() * 1.5;
            } else if (sizeRand < 0.80) {
                size = 2.5 + Math.random() * 2;
            } else if (sizeRand < 0.95) {
                size = 4.5 + Math.random() * 3.5;
            } else {
                size = 1.5 + Math.random() * 1;
            }
            p.style.width = size + 'px';
            p.style.height = (sizeRand >= 0.95 ? size * 4 : size) + 'px';
            if (sizeRand >= 0.95) {
                p.style.borderRadius = size + 'px';
            }
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
