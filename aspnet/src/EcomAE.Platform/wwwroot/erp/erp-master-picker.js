// ERP master lookup — one box. Opening it lists saved masters, typing filters
// that list and fetches /erp/lookups, the value is a link, and a right-click
// on the corner opens the master new-field area.
(function () {
    'use strict';

    if (window.__epcErpMasterPickerBound) {
        return;
    }
    window.__epcErpMasterPickerBound = true;

    function optionLabel(option) {
        if (!option) {
            return '';
        }
        return [option.getAttribute('data-code') || option.value || '', option.getAttribute('data-name') || '', option.getAttribute('data-extra') || '']
            .join(' ')
            .toLowerCase();
    }

    function fillSiblings(root, option) {
        if (!root || !option) {
            return;
        }
        var codeName = root.getAttribute('data-erp-fill-code');
        var nameName = root.getAttribute('data-erp-fill-name');
        var form = root.closest('form') || document;
        if (codeName) {
            var code = form.querySelector('[name="' + codeName + '"]');
            if (code && !code.value) {
                code.value = option.getAttribute('data-code') || '';
            } else if (code) {
                code.value = option.getAttribute('data-code') || code.value;
            }
        }
        if (nameName) {
            var name = form.querySelector('[name="' + nameName + '"]');
            if (name) {
                name.value = option.getAttribute('data-name') || name.value;
            }
        }
        var extraName = root.getAttribute('data-erp-fill-extra');
        if (extraName) {
            var extra = form.querySelector('[name="' + extraName + '"]');
            if (extra) {
                extra.value = option.getAttribute('data-extra') || extra.value;
            }
        }
    }

    function filterSelect(select, query) {
        if (!select) {
            return;
        }
        var needle = (query || '').trim().toLowerCase();
        Array.prototype.forEach.call(select.options, function (option) {
            if (!option.value || option.value === '0') {
                option.hidden = false;
                return;
            }
            option.hidden = needle.length > 0 && optionLabel(option).indexOf(needle) < 0;
        });
    }

    function findComboOption(input) {
        if (!input || !input.list) {
            return null;
        }
        var value = (input.value || '').trim().toLowerCase();
        if (!value) {
            return null;
        }
        var options = input.list.options;
        for (var i = 0; i < options.length; i++) {
            var option = options[i];
            if ((option.value || '').trim().toLowerCase() === value) {
                return option;
            }
        }
        return null;
    }

    function postedToken(root, row) {
        var mode = (root.getAttribute('data-erp-value-mode') || 'id').toLowerCase();
        if (mode === 'name') {
            return String(row.name || row.code || row.id);
        }
        if (mode === 'code') {
            return String(row.code || row.id);
        }
        return String(row.id);
    }

    function recordHref(root, id) {
        var app = root.getAttribute('data-erp-record-app') || '';
        var key = root.getAttribute('data-erp-record-key') || '';
        if (!app || !id) {
            return root.getAttribute('data-erp-manage') || app || '/erp';
        }
        var manage = root.getAttribute('data-erp-manage') || '';
        var company = '';
        var match = manage.match(/[?&]company=([^&#]+)/);
        if (match) {
            company = match[1];
        }
        var sep = app.indexOf('?') >= 0 ? '&' : '?';
        var href = app + sep + key + '=' + encodeURIComponent(id);
        if (company && href.indexOf('company=') < 0) {
            href += '&company=' + company;
        }
        return href + '#erp-row-' + id;
    }

    function upsertDatalist(list, row, root) {
        if (!list || !row || !row.id) {
            return;
        }
        var value = postedToken(root, row);
        var existing = null;
        Array.prototype.forEach.call(list.options, function (option) {
            if (option.value === value) {
                existing = option;
            }
        });
        var label = [row.code || value, row.name || '', row.extra || ''].filter(Boolean).join(' · ');
        var option = existing || document.createElement('option');
        option.value = value;
        option.textContent = label;
        option.setAttribute('data-id', String(row.id));
        option.setAttribute('data-code', row.code || '');
        option.setAttribute('data-name', row.name || '');
        option.setAttribute('data-extra', row.extra || '');
        option.setAttribute('data-href', recordHref(root, row.id));
        if (!existing) {
            list.appendChild(option);
        }
    }

    function upsertOption(select, row, root) {
        if (!select || !row || !row.id) {
            return;
        }
        var value = postedToken(root, row);
        var existing = null;
        Array.prototype.forEach.call(select.options, function (option) {
            if (option.value === value) {
                existing = option;
            }
        });
        var label = [row.code || value, row.name || '', row.extra || ''].filter(Boolean).join(' · ');
        var option = existing || document.createElement('option');
        option.value = value;
        option.textContent = label;
        option.setAttribute('data-id', String(row.id));
        option.setAttribute('data-code', row.code || '');
        option.setAttribute('data-name', row.name || '');
        option.setAttribute('data-extra', row.extra || '');
        option.setAttribute('data-href', recordHref(root, row.id));
        option.hidden = false;
        if (!existing) {
            select.appendChild(option);
        }
    }

    function searchRemote(root, query) {
        var kind = root.getAttribute('data-erp-kind') || '';
        var select = root.querySelector('[data-erp-picker-select]');
        var combo = root.querySelector('[data-erp-picker-combo]');
        var list = root.querySelector('datalist');
        if (!kind || (!select && !list) || !query || query.length < 1) {
            return;
        }
        fetch('/erp/lookups?kind=' + encodeURIComponent(kind) + '&q=' + encodeURIComponent(query) + '&limit=40', {
            credentials: 'same-origin'
        }).then(function (response) {
            return response.json().catch(function () { return null; });
        }).then(function (data) {
            if (!data || !data.ok || !data.options) {
                return;
            }
            if (list) {
                data.options.forEach(function (row) { upsertDatalist(list, row, root); });
            }
            if (select) {
                data.options.forEach(function (row) { upsertOption(select, row, root); });
                filterSelect(select, query);
            }
            if (combo && !list && combo.list) {
                data.options.forEach(function (row) { upsertDatalist(combo.list, row, root); });
            }
            refreshMenu(root);
            if (typeof root.__epcFilterMenu === 'function') {
                root.__epcFilterMenu(query);
            }
        }).catch(function () { /* keep the SSR list */ });
    }

    function refreshMenu(root) {
        var menu = root.querySelector('[data-erp-picker-menu]');
        if (!menu) {
            return;
        }
        var select = root.querySelector('[data-erp-picker-select]');
        var datalist = root.querySelector('datalist');
        var source = select ? select.options : (datalist ? datalist.options : []);
        var empty = menu.querySelector('[data-erp-lookup-empty]');
        menu.querySelectorAll('[data-erp-lookup-opt]').forEach(function (node) { node.remove(); });
        Array.prototype.forEach.call(source, function (option) {
            if (!option.value || option.value === '0') {
                return;
            }
            var button = document.createElement('button');
            button.type = 'button';
            button.className = 'epc-erp-lookup__opt';
            button.setAttribute('role', 'option');
            button.setAttribute('data-erp-lookup-opt', '1');
            button.setAttribute('data-value', option.value);
            ['data-id', 'data-code', 'data-name', 'data-extra', 'data-href'].forEach(function (attr) {
                if (option.hasAttribute(attr)) {
                    button.setAttribute(attr, option.getAttribute(attr) || '');
                }
            });
            if (!button.getAttribute('data-href')) {
                button.setAttribute('data-href', recordHref(root, button.getAttribute('data-id')));
            }
            button.textContent = option.textContent || option.value;
            button.setAttribute('data-search', (optionLabel(option) + ' ' + (option.textContent || '')).toLowerCase());
            if (empty) {
                menu.insertBefore(button, empty);
            } else {
                menu.appendChild(button);
            }
            bindOption(root, button);
        });
    }

    function filterMenu(root, query) {
        var menu = root.querySelector('[data-erp-picker-menu]');
        if (!menu) {
            return;
        }
        var needle = (query || '').trim().toLowerCase();
        var shown = 0;
        menu.querySelectorAll('[data-erp-lookup-opt]').forEach(function (opt) {
            var hay = opt.getAttribute('data-search') || (opt.textContent || '').toLowerCase();
            var hide = needle.length > 0 && hay.indexOf(needle) < 0;
            opt.hidden = hide;
            if (!hide) {
                shown += 1;
            }
        });
        var empty = menu.querySelector('[data-erp-lookup-empty]');
        if (empty) {
            empty.hidden = shown > 0;
            if (shown === 0 && needle.length > 0) {
                empty.textContent = 'No matches. Right-click the corner to add one.';
            }
        }
    }

    function placeMenu(root) {
        var box = root.querySelector('.epc-erp-lookup');
        var menu = root.querySelector('[data-erp-picker-menu]');
        if (!box || !menu) {
            return;
        }
        var rect = box.getBoundingClientRect();
        menu.style.position = 'fixed';
        menu.style.left = Math.max(8, rect.left) + 'px';
        menu.style.top = (rect.bottom + 2) + 'px';
        menu.style.width = Math.max(rect.width, 220) + 'px';
        menu.style.zIndex = '4000';
    }

    function openMenu(root, query) {
        document.querySelectorAll('.epc-erp-lookup.is-open').forEach(function (box) {
            var other = box.closest('.epc-erp-master-picker');
            if (other && other !== root) {
                closeMenu(other, true);
            }
        });
        var box = root.querySelector('.epc-erp-lookup');
        var menu = root.querySelector('[data-erp-picker-menu]');
        var input = root.querySelector('[data-erp-picker-q]');
        if (!box || !menu) {
            return;
        }
        box.classList.add('is-open', 'is-editing');
        filterMenu(root, query || '');
        placeMenu(root);
        menu.hidden = false;
        if (input) {
            input.setAttribute('aria-expanded', 'true');
        }
    }

    function closeMenu(root, restore) {
        var box = root.querySelector('.epc-erp-lookup');
        var menu = root.querySelector('[data-erp-picker-menu]');
        var input = root.querySelector('[data-erp-picker-q]');
        var select = root.querySelector('[data-erp-picker-select]');
        var link = root.querySelector('[data-erp-picker-link]');
        if (menu) {
            menu.hidden = true;
        }
        if (box) {
            box.classList.remove('is-open', 'is-editing');
        }
        if (input) {
            input.setAttribute('aria-expanded', 'false');
        }
        if (!restore) {
            return;
        }
        var combo = root.querySelector('[data-erp-picker-combo]');
        var chosen = select && select.selectedIndex >= 0 ? select.options[select.selectedIndex] : null;
        var hasValue = chosen && chosen.value && chosen.value !== '0';
        if (!hasValue && combo && input && (input.value || '').trim()) {
            hasValue = true;
            if (link) {
                link.textContent = input.value;
            }
        }
        if (hasValue) {
            if (input && !combo && chosen) {
                input.value = chosen.textContent || chosen.value;
            }
            if (link) {
                if (chosen && !combo) {
                    link.textContent = chosen.textContent || chosen.value;
                    if (chosen.getAttribute('data-href')) {
                        link.setAttribute('href', chosen.getAttribute('data-href'));
                    }
                }
                link.classList.remove('is-empty');
            }
            if (box) {
                box.classList.add('is-linked');
            }
            return;
        }
        if (input && !combo) {
            input.value = '';
        }
        if (link) {
            link.textContent = '';
            link.classList.add('is-empty');
        }
        if (box) {
            box.classList.remove('is-linked');
        }
    }

    function choose(root, button) {
        var value = button.getAttribute('data-value') || '';
        var select = root.querySelector('[data-erp-picker-select]');
        var input = root.querySelector('[data-erp-picker-q]');
        var combo = root.querySelector('[data-erp-picker-combo]');
        var link = root.querySelector('[data-erp-picker-link]');
        var box = root.querySelector('.epc-erp-lookup');
        if (select) {
            var match = null;
            Array.prototype.forEach.call(select.options, function (option) {
                if (option.value === value) {
                    match = option;
                }
            });
            if (match) {
                select.value = value;
                fillSiblings(root, match);
                select.dispatchEvent(new Event('change', { bubbles: true }));
            }
        } else {
            fillSiblings(root, button);
        }
        if (input) {
            input.value = combo ? value : (button.textContent || value);
        }
        if (link) {
            link.textContent = button.textContent || value;
            if (button.getAttribute('data-href')) {
                link.setAttribute('href', button.getAttribute('data-href'));
            }
            link.classList.remove('is-empty');
        }
        if (box) {
            box.classList.add('is-linked');
            box.classList.remove('is-open', 'is-editing');
        }
        var menu = root.querySelector('[data-erp-picker-menu]');
        if (menu) {
            menu.hidden = true;
        }
        if (input) {
            input.setAttribute('aria-expanded', 'false');
        }
    }

    function bindOption(root, button) {
        if (button.getAttribute('data-erp-opt-bound') === '1') {
            return;
        }
        button.setAttribute('data-erp-opt-bound', '1');
        button.addEventListener('mousedown', function (event) {
            event.preventDefault();
            choose(root, button);
        });
    }

    function bindLookup(root) {
        var box = root.querySelector('.epc-erp-lookup');
        var input = root.querySelector('[data-erp-picker-q]');
        var corner = root.querySelector('[data-erp-picker-corner]');
        var menu = root.querySelector('[data-erp-picker-menu]');
        if (!box || !input || !corner || !menu || box.getAttribute('data-erp-lookup-bound') === '1') {
            return;
        }
        box.setAttribute('data-erp-lookup-bound', '1');
        box.classList.add('is-ready');
        var timer = null;
        root.__epcFilterMenu = function (query) { filterMenu(root, query); };
        menu.querySelectorAll('[data-erp-lookup-opt]').forEach(function (button) {
            bindOption(root, button);
        });
        input.addEventListener('focus', function () {
            openMenu(root, '');
            input.select();
        });
        input.addEventListener('input', function () {
            openMenu(root, input.value);
            if (timer) {
                clearTimeout(timer);
            }
            timer = setTimeout(function () {
                searchRemote(root, input.value);
            }, 180);
        });
        input.addEventListener('keydown', function (event) {
            var options = Array.prototype.filter.call(menu.querySelectorAll('[data-erp-lookup-opt]'), function (opt) {
                return !opt.hidden;
            });
            var active = menu.querySelector('.epc-erp-lookup__opt.is-active');
            var index = options.indexOf(active);
            if (event.key === 'ArrowDown') {
                event.preventDefault();
                openMenu(root, menu.hidden ? '' : input.value);
                var next = options[Math.min(options.length - 1, index + 1)] || options[0];
                if (active) active.classList.remove('is-active');
                if (next) next.classList.add('is-active');
            } else if (event.key === 'ArrowUp') {
                event.preventDefault();
                var prev = options[Math.max(0, index - 1)];
                if (active) active.classList.remove('is-active');
                if (prev) prev.classList.add('is-active');
            } else if (event.key === 'Enter') {
                if (active) {
                    event.preventDefault();
                    choose(root, active);
                }
            } else if (event.key === 'Escape') {
                closeMenu(root, true);
            }
        });
        input.addEventListener('blur', function () {
            setTimeout(function () {
                if (!box.classList.contains('is-open')) {
                    return;
                }
                closeMenu(root, true);
            }, 120);
        });
        corner.addEventListener('click', function () {
            if (!menu.hidden) {
                closeMenu(root, true);
                return;
            }
            input.focus();
            openMenu(root, '');
        });
        corner.addEventListener('contextmenu', function (event) {
            event.preventDefault();
            var href = root.getAttribute('data-erp-manage');
            if (href) {
                window.location.assign(href);
            }
        });
    }

    function bindRoot(root) {
        if (!root || root.getAttribute('data-erp-bound') === '1') {
            return;
        }
        root.setAttribute('data-erp-bound', '1');
        var select = root.querySelector('[data-erp-picker-select]');
        var query = root.querySelector('[data-erp-picker-q]');
        var combo = root.querySelector('[data-erp-picker-combo]');
        var lookup = root.querySelector('.epc-erp-lookup');
        if (combo && !lookup) {
            var comboTimer = null;
            combo.addEventListener('input', function () {
                var option = findComboOption(combo);
                if (option) {
                    fillSiblings(root, option);
                }
                if (comboTimer) {
                    clearTimeout(comboTimer);
                }
                comboTimer = setTimeout(function () {
                    searchRemote(root, combo.value);
                }, 180);
            });
            var selected = findComboOption(combo);
            if (selected) {
                fillSiblings(root, selected);
            }
        }
        if (select) {
            select.addEventListener('change', function () {
                var option = select.options[select.selectedIndex];
                fillSiblings(root, option);
            });
            if (select.selectedIndex >= 0) {
                fillSiblings(root, select.options[select.selectedIndex]);
            }
        }
        if (query && select && !lookup) {
            var timer = null;
            query.addEventListener('input', function () {
                filterSelect(select, query.value);
                if (timer) {
                    clearTimeout(timer);
                }
                timer = setTimeout(function () {
                    searchRemote(root, query.value);
                }, 180);
            });
        }
        if (lookup) {
            bindLookup(root);
        }
    }

    function focusNewField() {
        if ((location.hash || '') !== '#erp-module-new') {
            return;
        }
        var node = document.getElementById('erp-module-new');
        if (!node) {
            node = document.querySelector('.epc-erp-create-card')
                || document.querySelector('form[action*="create"], form[action*="Create"], form[action*="master-save"]')
                || document.querySelector('.epc-erp-content-body form, .epc-cp-content-inner form');
            if (node && !node.id) {
                node.id = 'erp-module-new';
            }
        }
        if (!node) {
            return;
        }
        node.scrollIntoView({ block: 'start' });
        var field = node.querySelector('[data-erp-new-field], input:not([type="hidden"]):not([readonly]), textarea, select');
        if (field && typeof field.focus === 'function') {
            field.focus();
        }
    }

    function bindAll() {
        document.querySelectorAll('.epc-erp-master-picker').forEach(bindRoot);
        focusNewField();
    }

    if (!window.__epcErpLookupOutside) {
        window.__epcErpLookupOutside = true;
        document.addEventListener('mousedown', function (event) {
            document.querySelectorAll('.epc-erp-lookup.is-open').forEach(function (box) {
                if (box.contains(event.target)) {
                    return;
                }
                var root = box.closest('.epc-erp-master-picker');
                if (root) {
                    closeMenu(root, true);
                }
            });
        });
        window.addEventListener('resize', function () {
            document.querySelectorAll('.epc-erp-lookup.is-open').forEach(function (box) {
                var root = box.closest('.epc-erp-master-picker');
                if (root) {
                    placeMenu(root);
                }
            });
        });
    }

    // Re-run binding on dynamically added picker roots (e.g. cloned journal lines).
    window.ErpMasterPickerRefresh = bindAll;
    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', bindAll);
    } else {
        bindAll();
    }
})();
