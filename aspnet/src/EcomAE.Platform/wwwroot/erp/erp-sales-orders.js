// ERP sales orders — live writes against the ASP.NET endpoints ported from PHP ajax_erp.php
// (so_save / so_status / so_delete / so_to_invoice). Every call sends confirmWrites so the dry-run gate is bypassed.
(function () {
    'use strict';

    var feedback = document.getElementById('so-feedback');

    function say(text, ok) {
        if (!feedback) {
            return;
        }
        feedback.textContent = text;
        feedback.className = ok ? 'text-success' : 'text-danger';
    }

    function post(url, payload, onSuccess) {
        say('Working…', true);
        fetch(url, {
            method: 'POST',
            credentials: 'same-origin',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify(payload)
        }).then(function (response) {
            return response.json().catch(function () {
                return { status: false, message: 'HTTP ' + response.status };
            });
        }).then(function (data) {
            var ok = !!data && (data.status === true || data.ok === true);
            say((data && data.message) || (ok ? 'Saved' : 'Failed'), ok);
            if (ok) {
                onSuccess();
            }
        }).catch(function (error) {
            say(String(error), false);
        });
    }

    function reload() {
        window.location.reload();
    }

    function numeric(id) {
        var el = document.getElementById(id);
        var value = parseFloat(el && el.value ? el.value : '0');
        return isNaN(value) ? 0 : value;
    }

    function text(id) {
        var el = document.getElementById(id);
        return el && el.value ? el.value : '';
    }

    // Line grid — mirrors the PHP tab fields item_code[] / line_desc[] / line_qty[] / line_unit[].
    var lineBody = document.querySelector('#so-lines tbody');
    var linesTotal = document.getElementById('so-lines-total');

    function round2(value) {
        return Math.round((value + Number.EPSILON) * 100) / 100;
    }

    function recalc() {
        var total = 0;
        Array.prototype.forEach.call(lineBody ? lineBody.rows : [], function (row) {
            var qty = parseFloat(row.querySelector('.epc-so-qty').value) || 0;
            var unit = parseFloat(row.querySelector('.epc-so-unit').value) || 0;
            var line = round2(qty * unit);
            row.querySelector('.epc-so-line-total').textContent = line.toFixed(2);
            total += line;
        });

        total = round2(total);
        if (linesTotal) {
            linesTotal.textContent = total.toFixed(2);
        }

        var amount = document.getElementById('so-amount');
        if (amount) {
            amount.value = total.toFixed(2);
        }
    }

    function addLine() {
        if (!lineBody) {
            return null;
        }

        var row = lineBody.insertRow(-1);
        row.innerHTML = '<td><input type="text" class="form-control input-sm epc-so-code" list="erp-item-list" placeholder="Item Code" maxlength="32" /></td>'
            + '<td><input type="text" class="form-control input-sm epc-so-desc" placeholder="Description" maxlength="255" /></td>'
            + '<td><input type="number" step="0.001" min="0.001" value="1" class="form-control input-sm epc-so-qty" /></td>'
            + '<td><input type="number" step="0.01" min="0" value="0.00" class="form-control input-sm epc-so-unit" /></td>'
            + '<td class="epc-so-line-total text-right">0.00</td>'
            + '<td><button type="button" class="btn btn-link btn-xs epc-so-line-del">Remove</button></td>';
        row.querySelector('.epc-so-qty').addEventListener('input', recalc);
        row.querySelector('.epc-so-unit').addEventListener('input', recalc);
        row.querySelector('.epc-so-line-del').addEventListener('click', function () {
            row.parentNode.removeChild(row);
            recalc();
        });
        recalc();
        return row;
    }

    function collectLines() {
        var lines = [];
        Array.prototype.forEach.call(lineBody ? lineBody.rows : [], function (row) {
            var description = row.querySelector('.epc-so-desc').value.trim();
            if (!description) {
                return;
            }

            var qty = Math.max(0.001, parseFloat(row.querySelector('.epc-so-qty').value) || 0);
            var unit = parseFloat(row.querySelector('.epc-so-unit').value) || 0;
            lines.push({
                item_code: row.querySelector('.epc-so-code').value.trim(),
                description: description,
                qty: qty,
                unit_price_ex_vat: unit,
                line_ex_vat: round2(qty * unit)
            });
        });

        return lines;
    }

    var addButton = document.getElementById('so-line-add');
    if (addButton) {
        addButton.addEventListener('click', addLine);
        addLine();
    }

    var picker = document.getElementById('so-item-picker');
    if (picker) {
        picker.addEventListener('change', function () {
            var option = picker.options[picker.selectedIndex];
            if (!option || !option.value) {
                return;
            }

            var row = (lineBody && lineBody.rows.length > 0) ? lineBody.rows[lineBody.rows.length - 1] : addLine();
            if (!row) {
                return;
            }

            row.querySelector('.epc-so-code').value = option.getAttribute('data-sku') || '';
            row.querySelector('.epc-so-desc').value = option.getAttribute('data-name') || '';
            row.querySelector('.epc-so-unit').value = option.getAttribute('data-price') || '0';
            picker.selectedIndex = 0;
            recalc();
        });
    }

    var save = document.getElementById('so-save');
    if (save) {
        save.addEventListener('click', function () {
            var lines = collectLines();
            if (lines.length === 0) {
                say('Add at least one line with a description.', false);
                return;
            }

            post('/erp/ajax/so-save', {
                confirmWrites: true,
                id: numeric('so-id'),
                customerUserId: numeric('so-customer'),
                title: text('so-title'),
                amountExVat: numeric('so-amount'),
                notes: text('so-notes'),
                linesJson: JSON.stringify(lines)
            }, reload);
        });
    }

    function ownerId(button) {
        var owner = button.closest('.epc-so-actions');
        return owner ? parseInt(owner.getAttribute('data-so-id'), 10) : 0;
    }

    Array.prototype.forEach.call(document.querySelectorAll('.epc-so-status'), function (button) {
        button.addEventListener('click', function () {
            post('/erp/ajax/so-status', {
                confirmWrites: true,
                id: ownerId(button),
                targetStatus: button.getAttribute('data-status')
            }, reload);
        });
    });

    Array.prototype.forEach.call(document.querySelectorAll('.epc-so-invoice'), function (button) {
        button.addEventListener('click', function () {
            var id = ownerId(button);
            if (!window.confirm('Convert sales order #' + id + ' to a tax invoice?')) {
                return;
            }

            post('/erp/ajax/so-to-invoice', { confirmWrites: true, id: id }, reload);
        });
    });

    Array.prototype.forEach.call(document.querySelectorAll('.epc-so-delete'), function (button) {
        button.addEventListener('click', function () {
            var id = ownerId(button);
            if (!window.confirm('Delete draft sales order #' + id + '?')) {
                return;
            }

            post('/erp/sales-orders/delete', { confirmWrites: true, salesOrderId: id }, reload);
        });
    });
})();
