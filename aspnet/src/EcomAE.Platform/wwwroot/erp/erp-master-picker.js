// ERP master picker — typeahead over customer/supplier/item/project/account/RE masters.
// Filters the SSR select, fetches /erp/lookups when the local list is not enough,
// and copies code/name onto sibling fields so a pick fills every linked box.
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

    function upsertDatalist(list, row, useCode) {
        if (!list || !row || !row.id) {
            return;
        }
        var value = useCode ? String(row.code || row.id) : String(row.id);
        var existing = list.querySelector('option[value="' + value.replace(/"/g, '') + '"]');
        var label = [row.code || value, row.name || '', row.extra || ''].filter(Boolean).join(' · ');
        if (existing) {
            existing.textContent = label;
            existing.setAttribute('data-id', String(row.id));
            existing.setAttribute('data-code', row.code || '');
            existing.setAttribute('data-name', row.name || '');
            existing.setAttribute('data-extra', row.extra || '');
            return;
        }
        var option = document.createElement('option');
        option.value = value;
        option.textContent = label;
        option.setAttribute('data-id', String(row.id));
        option.setAttribute('data-code', row.code || '');
        option.setAttribute('data-name', row.name || '');
        option.setAttribute('data-extra', row.extra || '');
        list.appendChild(option);
    }

    function upsertOption(select, row, useCode) {
        if (!select || !row || !row.id) {
            return;
        }
        var value = useCode ? String(row.code || row.id) : String(row.id);
        var existing = select.querySelector('option[value="' + value.replace(/"/g, '') + '"]');
        var label = [row.code || value, row.name || '', row.extra || ''].filter(Boolean).join(' · ');
        if (existing) {
            existing.textContent = label;
            existing.setAttribute('data-id', String(row.id));
            existing.setAttribute('data-code', row.code || '');
            existing.setAttribute('data-name', row.name || '');
            existing.setAttribute('data-extra', row.extra || '');
            existing.hidden = false;
            return;
        }
        var option = document.createElement('option');
        option.value = value;
        option.textContent = label;
        option.setAttribute('data-id', String(row.id));
        option.setAttribute('data-code', row.code || '');
        option.setAttribute('data-name', row.name || '');
        option.setAttribute('data-extra', row.extra || '');
        select.appendChild(option);
    }

    function searchRemote(root, query) {
        var kind = root.getAttribute('data-erp-kind') || '';
        var select = root.querySelector('[data-erp-picker-select]');
        var combo = root.querySelector('[data-erp-picker-combo]');
        var list = combo && combo.list ? combo.list : null;
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
            var useCode = root.getAttribute('data-erp-value-mode') === 'code';
            if (list) {
                data.options.forEach(function (row) { upsertDatalist(list, row, useCode); });
            }
            if (select) {
                data.options.forEach(function (row) { upsertOption(select, row, useCode); });
                filterSelect(select, query);
            }
        }).catch(function () { /* keep the SSR list */ });
    }

    function bindRoot(root) {
        if (!root || root.getAttribute('data-erp-bound') === '1') {
            return;
        }
        root.setAttribute('data-erp-bound', '1');
        var select = root.querySelector('[data-erp-picker-select]');
        var query = root.querySelector('[data-erp-picker-q]');
        var combo = root.querySelector('[data-erp-picker-combo]');
        if (combo) {
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
        if (query && select) {
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
    }

    function bindAll() {
        document.querySelectorAll('.epc-erp-master-picker').forEach(bindRoot);
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', bindAll);
    } else {
        bindAll();
    }
})();
