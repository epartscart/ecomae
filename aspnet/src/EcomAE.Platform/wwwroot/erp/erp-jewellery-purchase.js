// Jewellery metal-purchase: pick a metal-stock code and fill description / purity /
// metal, then Pure Wt = Gr.Wt × Purity and Metal Amt = Pure Wt × header rate.
(function () {
    'use strict';

    if (window.__epcJwPurchaseBound) {
        return;
    }
    window.__epcJwPurchaseBound = true;

    function num(value) {
        var n = parseFloat(String(value || '').replace(/,/g, ''));
        return isFinite(n) ? n : 0;
    }

    function setVal(form, name, value) {
        var el = form.querySelector('[name="' + name + '"]');
        if (el) {
            el.value = value;
        }
    }

    function getVal(form, name) {
        var el = form.querySelector('[name="' + name + '"]');
        return el ? el.value : '';
    }

    function recalc(form) {
        if (!form || !form.getAttribute('data-epc-jw-purchase')) {
            return;
        }
        var gr = num(getVal(form, 'line_gr_wt'));
        var purity = num(getVal(form, 'line_purity'));
        var rate = num(getVal(form, 'line_metal_rate')) || num(getVal(form, 'metal_rate'));
        var mkg = num(getVal(form, 'line_mkg_rate'));
        var stone = num(getVal(form, 'line_stone_amount'));
        var other = num(getVal(form, 'other_amount')) || num(getVal(form, 'addin_amount'));
        var vat = num(getVal(form, 'vat_amount'));
        var rnd = num(getVal(form, 'round_off')) || num(getVal(form, 'rnd_off_amount'));
        var pure = Math.round(gr * purity * 10000) / 10000;
        var metalAmt = Math.round(pure * rate * 100) / 100;
        var mkgAmt = Math.round(gr * mkg * 100) / 100;
        var net = Math.round((metalAmt + mkgAmt + stone) * 100) / 100;
        var gross = Math.round((net + other + vat + rnd) * 100) / 100;
        setVal(form, 'line_pure_wt', pure.toFixed(4));
        setVal(form, 'line_metal_rate', rate.toFixed(5));
        setVal(form, 'line_metal_amount', metalAmt.toFixed(2));
        setVal(form, 'line_mkg_amount', mkgAmt.toFixed(2));
        setVal(form, 'net_amount', net.toFixed(2));
        setVal(form, 'gross_total', (net + other + vat + rnd).toFixed(2));
        var otherTotal = form.querySelector('[data-jw-other-total]');
        if (otherTotal) {
            otherTotal.textContent = other.toFixed(2);
        }
        var itemTotal = form.querySelector('[data-jw-item-total]');
        if (itemTotal) {
            itemTotal.textContent = net.toFixed(2);
        }
        var grand = form.querySelector('[data-jw-gross-total]');
        if (grand) {
            grand.textContent = gross.toFixed(2);
        }
    }

    function applyStock(form, option) {
        if (!form || !option) {
            return;
        }
        var extra = (option.getAttribute('data-extra') || '').split('·').map(function (part) {
            return part.trim();
        });
        var metal = extra[0] || '';
        var purity = extra[1] || '';
        var karat = extra[2] || '';
        if (option.getAttribute('data-name')) {
            setVal(form, 'line_description', option.getAttribute('data-name'));
        }
        if (purity) {
            setVal(form, 'line_purity', purity);
        }
        if (metal) {
            setVal(form, 'line_division', metal);
        }
        if (karat) {
            setVal(form, 'line_karat', karat);
        }
        recalc(form);
    }

    function bindForm(form) {
        if (!form || form.getAttribute('data-epc-jw-bound') === '1') {
            return;
        }
        form.setAttribute('data-epc-jw-bound', '1');
        form.addEventListener('input', function (event) {
            var name = event.target && event.target.getAttribute('name');
            if (!name) {
                return;
            }
            if (name === 'line_gr_wt' || name === 'line_purity' || name === 'line_metal_rate'
                || name === 'metal_rate' || name === 'line_mkg_rate' || name === 'line_stone_amount'
                || name === 'other_amount' || name === 'addin_amount' || name === 'vat_amount'
                || name === 'round_off' || name === 'rnd_off_amount') {
                recalc(form);
            }
        });
        form.addEventListener('change', function (event) {
            var root = event.target && event.target.closest
                ? event.target.closest('[data-erp-jw-stock="1"]')
                : null;
            if (root) {
                var select = root.querySelector('[data-erp-picker-select]');
                if (select && select.options[select.selectedIndex]) {
                    applyStock(form, select.options[select.selectedIndex]);
                }
            }
            recalc(form);
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
