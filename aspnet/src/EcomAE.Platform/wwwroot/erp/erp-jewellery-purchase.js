// Jewellery metal purchase entry: per-line Net Wt = Gr.Wt - Stone Wt, Pure Wt = Net Wt x Purity,
// Wastage = Pure Wt x Wstg%, Metal Amount = (Pure Wt + Wastage) x header metal rate, Making = Gr.Wt x Mkg rate.
// VAT is charged on the taxable basis only (metal value stays out of input VAT unless "full" is chosen;
// the metal VAT is shown as RCM). Panels switch in-page and the draft survives refresh until saved.
(function () {
    'use strict';

    if (window.__epcJwPurchaseBound) {
        return;
    }
    window.__epcJwPurchaseBound = true;

    function num(value) {
        var n = parseFloat(String(value == null ? '' : value).replace(/,/g, ''));
        return isFinite(n) ? n : 0;
    }

    function r(value, dp) {
        var f = Math.pow(10, dp);
        return Math.round((value + (value >= 0 ? 1e-9 : -1e-9)) * f) / f;
    }

    function q(root, sel) {
        return root.querySelector(sel);
    }

    function setNamed(root, name, value) {
        var el = q(root, '[name="' + name + '"]');
        if (el) {
            el.value = value;
        }
    }

    function getNamed(root, name) {
        var el = q(root, '[name="' + name + '"]');
        return el ? el.value : '';
    }

    function setSum(form, key, value) {
        var el = q(form, '[data-jw-sum="' + key + '"]');
        if (el) {
            el.textContent = value;
        }
    }

    function setTot(form, key, value) {
        var el = q(form, '[data-jw-tot="' + key + '"]');
        if (el) {
            el.value = value;
        }
    }

    function recalc(form) {
        var rate = num(getNamed(form, 'metal_rate'));
        var t = { pcs: 0, gr: 0, stone: 0, net: 0, pure: 0, wstg: 0, metal: 0, mkg: 0, st: 0, total: 0 };
        form.querySelectorAll('[data-jw-line]').forEach(function (row) {
            var gr = num(getNamed(row, 'line_gr_wt'));
            var stoneWt = num(getNamed(row, 'line_stone_wt'));
            var purity = num(getNamed(row, 'line_purity'));
            var netWt = r(Math.max(0, gr - stoneWt), 4);
            var pure = r(netWt * purity, 4);
            var wstg = r(pure * num(getNamed(row, 'line_wastage_pct')) / 100, 4);
            var metal = r((pure + wstg) * rate, 2);
            var mkg = r(gr * num(getNamed(row, 'line_mkg_rate')), 2);
            var st = r(num(getNamed(row, 'line_stone_amount')), 2);
            var total = r(metal + mkg + st, 2);
            var netEl = q(row, '[data-jw-net-wt]');
            if (netEl) {
                netEl.value = netWt.toFixed(4);
            }
            setNamed(row, 'line_pure_wt', pure.toFixed(4));
            setNamed(row, 'line_wastage_qty', wstg.toFixed(4));
            setNamed(row, 'line_metal_rate', rate.toFixed(5));
            setNamed(row, 'line_metal_amount', metal.toFixed(2));
            setNamed(row, 'line_mkg_amount', mkg.toFixed(2));
            var totEl = q(row, '[data-jw-item-total]');
            if (totEl) {
                totEl.value = total.toFixed(2);
            }
            t.pcs += num(getNamed(row, 'line_pcs'));
            t.gr += gr; t.stone += stoneWt; t.net += netWt; t.pure += pure; t.wstg += wstg;
            t.metal += metal; t.mkg += mkg; t.st += st; t.total += total;
        });
        setSum(form, 'pcs', String(t.pcs));
        ['gr', 'stone', 'net', 'pure', 'wstg'].forEach(function (k) { setSum(form, k, r(t[k], 4).toFixed(4)); });
        ['metal', 'mkg', 'st', 'total'].forEach(function (k) { setSum(form, k, r(t[k], 2).toFixed(2)); });

        var other = 0;
        form.querySelectorAll('[name="addin_amount"]').forEach(function (amountEl) {
            var row = amountEl.closest('tr');
            var amount = num(amountEl.value);
            var pctValue = row ? num(getNamed(row, 'addin_pct')) : 0;
            if (pctValue > 0) {
                amount = r(t.total * pctValue / 100, 2);
                amountEl.value = amount.toFixed(2);
            }
            other += amount;
        });
        other = r(other, 2);
        var net = r(t.total, 2);
        var pct = num(getNamed(form, 'vat_pct'));
        var basisEl = q(form, '[data-jw-vat-basis]');
        var basis = basisEl ? basisEl.value : 'mkg';
        var nonMetal = r(t.mkg + t.st + other, 2);
        var taxable = basis === 'full' ? r(net + other, 2) : basis === 'none' ? 0 : nonMetal;
        var vat = r(taxable * pct / 100, 2);
        var rcm = basis === 'mkg' ? r(t.metal * pct / 100, 2) : 0;
        var rnd = num(getNamed(form, 'round_off'));
        var gross = r(net + other + vat + rnd, 2);
        setTot(form, 'metal', r(t.metal, 2).toFixed(2));
        setTot(form, 'mkg', r(t.mkg, 2).toFixed(2));
        setTot(form, 'st', r(t.st, 2).toFixed(2));
        setTot(form, 'taxable', taxable.toFixed(2));
        setNamed(form, 'net_amount', net.toFixed(2));
        setNamed(form, 'other_amount', other.toFixed(2));
        setNamed(form, 'vat_amount', vat.toFixed(2));
        setNamed(form, 'rcm_vat', rcm.toFixed(2));
        setNamed(form, 'gross_total', gross.toFixed(2));
        var note = q(form, '[data-jw-basis-note]');
        if (note) {
            note.textContent = basis === 'full' ? '(incl. metal)' : basis === 'none' ? '(zero-rated)' : '(excl. metal)';
        }
    }

    function applyStock(row, option) {
        if (!row || !option) {
            return;
        }
        var extra = (option.getAttribute('data-extra') || '').split('\u00b7').map(function (part) {
            return part.trim();
        });
        if (option.getAttribute('data-name')) {
            setNamed(row, 'line_description', option.getAttribute('data-name'));
        }
        if (extra[1] && num(extra[1]) > 0) {
            setNamed(row, 'line_purity', extra[1]);
        }
        if (extra[0]) {
            setNamed(row, 'line_division', extra[0]);
        }
        if (extra[2]) {
            setNamed(row, 'line_karat', extra[2]);
        }
    }

    function renumber(form) {
        var i = 0;
        form.querySelectorAll('[data-jw-line]').forEach(function (row) {
            i++;
            var cell = q(row, '[data-jw-line-no]');
            if (cell) {
                cell.textContent = String(i);
            }
        });
    }

    function addLine(form, template) {
        var body = q(form, '[data-jw-lines]');
        if (!body || !template) {
            return null;
        }
        var row = template.cloneNode(true);
        var n = body.querySelectorAll('[data-jw-line]').length + 1;
        row.querySelectorAll('[data-erp-bound],[data-erp-lookup-bound],[data-erp-opt-bound]').forEach(function (el) {
            el.removeAttribute('data-erp-bound');
            el.removeAttribute('data-erp-lookup-bound');
            el.removeAttribute('data-erp-opt-bound');
        });
        row.querySelectorAll('.epc-erp-lookup').forEach(function (box) {
            box.classList.remove('is-ready', 'is-linked', 'is-open', 'is-editing');
        });
        row.querySelectorAll('[id]').forEach(function (el) {
            el.id = el.id + '-' + n;
        });
        row.querySelectorAll('label[for]').forEach(function (el) {
            el.setAttribute('for', el.getAttribute('for') + '-' + n);
        });
        body.appendChild(row);
        if (typeof window.ErpMasterPickerRefresh === 'function') {
            window.ErpMasterPickerRefresh();
        }
        renumber(form);
        return row;
    }

    function draftKey(form) {
        return 'epc-jw-draft:' + (form.getAttribute('data-jw-draft') || 'form') + ':' + location.pathname + ':' + (new URLSearchParams(location.search).get('company') || '');
    }

    var skipDraft = { confirmWrites: 1, returnUrl: 1, action: 1, voc_type: 1, after_save: 1, company: 1 };

    function saveDraft(form) {
        var data = { lines: form.querySelectorAll('[data-jw-line]').length, fields: [] };
        Array.prototype.forEach.call(form.elements, function (el) {
            if (!el.name || skipDraft[el.name] || el.type === 'file' || el.type === 'submit' || el.type === 'button') {
                return;
            }
            data.fields.push([el.name, el.type === 'checkbox' ? (el.checked ? '1' : '') : el.value]);
        });
        try {
            sessionStorage.setItem(draftKey(form), JSON.stringify(data));
        } catch (e) { /* storage full or disabled */ }
    }

    function syncPicker(select) {
        var root = select.closest('.epc-erp-master-picker');
        if (!root) {
            return;
        }
        var option = select.options[select.selectedIndex];
        var input = q(root, '[data-erp-picker-q]');
        var link = q(root, '[data-erp-picker-link]');
        var box = q(root, '.epc-erp-lookup');
        var has = option && option.value && option.value !== '0';
        if (input && !q(root, '[data-erp-picker-combo]')) {
            input.value = has ? (option.textContent || option.value) : '';
        }
        if (link) {
            link.textContent = has ? (option.textContent || option.value) : '';
            link.classList.toggle('is-empty', !has);
            if (has && option.getAttribute('data-href')) {
                link.setAttribute('href', option.getAttribute('data-href'));
            }
        }
        if (box) {
            box.classList.toggle('is-linked', !!has);
        }
    }

    function restoreDraft(form, template) {
        var raw = null;
        try {
            raw = sessionStorage.getItem(draftKey(form));
        } catch (e) { raw = null; }
        if (!raw) {
            return false;
        }
        var data;
        try {
            data = JSON.parse(raw);
        } catch (e) {
            return false;
        }
        for (var i = form.querySelectorAll('[data-jw-line]').length; i < (data.lines || 1); i++) {
            addLine(form, template);
        }
        var seen = {};
        (data.fields || []).forEach(function (pair) {
            var name = pair[0];
            var idx = seen[name] || 0;
            seen[name] = idx + 1;
            var el = form.querySelectorAll('[name="' + name + '"]')[idx];
            if (!el) {
                return;
            }
            if (el.type === 'checkbox') {
                el.checked = pair[1] === '1';
            } else {
                el.value = pair[1];
            }
            if (el.tagName === 'SELECT' && el.hasAttribute('data-erp-picker-select')) {
                syncPicker(el);
            }
        });
        return true;
    }

    function clearDraft(form) {
        try {
            sessionStorage.removeItem(draftKey(form));
        } catch (e) { /* ignore */ }
    }

    function setStatus(form, text, dirty) {
        var el = q(form, '[data-jw-status]');
        if (el) {
            el.textContent = text;
            el.classList.toggle('is-dirty', !!dirty);
        }
    }

    function toast(text, err) {
        var t = document.createElement('div');
        t.className = 'jw-toast' + (err ? ' is-err' : '');
        t.setAttribute('role', 'status');
        t.textContent = text;
        document.body.appendChild(t);
        setTimeout(function () { t.remove(); }, 5000);
    }

    function showPanel(form, key) {
        form.querySelectorAll('[data-jw-panel]').forEach(function (panel) {
            panel.hidden = panel.getAttribute('data-jw-panel') !== key;
        });
        form.querySelectorAll('[data-jw-panel-btn]').forEach(function (btn) {
            btn.classList.toggle('is-active', btn.getAttribute('data-jw-panel-btn') === key);
        });
    }

    function bindForm(form) {
        if (!form || form.getAttribute('data-epc-jw-bound') === '1') {
            return;
        }
        form.setAttribute('data-epc-jw-bound', '1');
        var firstLine = q(form, '[data-jw-line]');
        var template = firstLine ? firstLine.cloneNode(true) : null;
        var dirty = false;
        var submitting = false;
        var params = new URLSearchParams(location.search);

        if (params.get('saved_id')) {
            clearDraft(form);
            toast('Voucher saved and posted.');
        } else if (params.get('err')) {
            if (restoreDraft(form, template)) {
                dirty = true;
            }
            toast(params.get('err'), true);
        } else if (restoreDraft(form, template)) {
            dirty = true;
            setStatus(form, 'Unsaved draft restored. Press Save voucher to post it.', true);
        }

        function touched() {
            dirty = true;
            recalc(form);
            saveDraft(form);
            setStatus(form, 'Unsaved changes (draft kept).', true);
        }

        form.addEventListener('input', touched);
        form.addEventListener('change', function (event) {
            var stock = event.target && event.target.closest ? event.target.closest('[data-erp-jw-stock="1"]') : null;
            if (stock) {
                var select = q(stock, '[data-erp-picker-select]');
                if (select && select.options[select.selectedIndex]) {
                    applyStock(stock.closest('[data-jw-line]'), select.options[select.selectedIndex]);
                }
            }
            touched();
        });
        form.addEventListener('keydown', function (event) {
            if (event.key === 'Enter' && event.target && event.target.tagName === 'INPUT') {
                event.preventDefault();
                var focusables = Array.prototype.filter.call(
                    form.querySelectorAll('input:not([type=hidden]):not([readonly]),select,textarea'),
                    function (el) { return el.offsetParent !== null && !el.disabled; });
                var at = focusables.indexOf(event.target);
                if (at >= 0 && at + 1 < focusables.length) {
                    focusables[at + 1].focus();
                }
            }
        });
        form.addEventListener('click', function (event) {
            var btn = event.target && event.target.closest ? event.target.closest('button') : null;
            if (!btn || !form.contains(btn)) {
                return;
            }
            if (btn.hasAttribute('data-jw-panel-btn')) {
                showPanel(form, btn.getAttribute('data-jw-panel-btn'));
            } else if (btn.hasAttribute('data-jw-line-add')) {
                addLine(form, template);
                touched();
            } else if (btn.hasAttribute('data-jw-line-remove')) {
                var rows = form.querySelectorAll('[data-jw-line]');
                if (rows.length > 1) {
                    btn.closest('[data-jw-line]').remove();
                    renumber(form);
                    touched();
                }
            } else if (btn.hasAttribute('data-jw-clear')) {
                if (window.confirm('Clear this entry? Unsaved values will be lost.')) {
                    clearDraft(form);
                    dirty = false;
                    location.href = location.pathname + location.search.replace(/[?&](ok|err|saved_id)=[^&]*/g, '').replace(/^&/, '?');
                }
            } else if (btn.hasAttribute('data-jw-submit')) {
                var after = q(form, '[data-jw-after-save]');
                if (after) {
                    after.value = btn.hasAttribute('data-jw-submit-print') ? 'print' : '';
                }
            }
        });
        form.addEventListener('submit', function (event) {
            recalc(form);
            if (num(getNamed(form, 'metal_rate')) <= 0) {
                event.preventDefault();
                toast('Enter the metal rate before saving.', true);
                return;
            }
            if (num(getNamed(form, 'gross_total')) <= 0) {
                event.preventDefault();
                toast('Enter at least one line with gross weight.', true);
                return;
            }
            saveDraft(form);
            submitting = true;
            form.querySelectorAll('[data-jw-submit]').forEach(function (b) {
                b.disabled = true;
            });
            setStatus(form, 'Saving and posting voucher\u2026', false);
            var after = q(form, '[data-jw-after-save]');
            if (after && after.value === 'print') {
                form.setAttribute('target', '_self');
            }
        });
        window.addEventListener('beforeunload', function (event) {
            if (dirty && !submitting) {
                event.preventDefault();
                event.returnValue = '';
            }
        });
        recalc(form);
    }

    function bindAll() {
        document.querySelectorAll('form[data-epc-jw-purchase]').forEach(bindForm);
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', bindAll);
    } else {
        bindAll();
    }
})();
