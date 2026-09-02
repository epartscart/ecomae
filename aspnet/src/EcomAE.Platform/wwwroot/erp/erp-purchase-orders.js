// ERP purchase orders — live writes against the ASP.NET endpoints ported from PHP ajax_erp.php
// (po_save / po_status / po_receive_lines / po_to_invoice / po delete). Every call sends confirmWrites.
(function () {
    'use strict';

    var feedback = document.getElementById('po-feedback');

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

    function round2(value) {
        return Math.round((value + Number.EPSILON) * 100) / 100;
    }

    // Line grid — mirrors the PHP purchase-order tab fields item_code[] / line_desc[] / line_qty[] / line_unit[].
    var lineBody = document.querySelector('#po-lines tbody');
    var linesTotal = document.getElementById('po-lines-total');

    function recalc() {
        var total = 0;
        Array.prototype.forEach.call(lineBody ? lineBody.rows : [], function (row) {
            var qty = parseFloat(row.querySelector('.epc-po-qty').value) || 0;
            var unit = parseFloat(row.querySelector('.epc-po-unit').value) || 0;
            var line = round2(qty * unit);
            row.querySelector('.epc-po-line-total').textContent = line.toFixed(2);
            total += line;
        });

        total = round2(total);
        if (linesTotal) {
            linesTotal.textContent = total.toFixed(2);
        }

        var amount = document.getElementById('po-amount');
        if (amount) {
            amount.value = total.toFixed(2);
        }
    }

    function addLine() {
        if (!lineBody) {
            return null;
        }

        var row = lineBody.insertRow(-1);
        row.innerHTML = '<td><input type="text" class="form-control input-sm epc-po-code" list="erp-item-list" placeholder="Item Code" maxlength="32" /></td>'
            + '<td><input type="text" class="form-control input-sm epc-po-desc" placeholder="Description" maxlength="255" /></td>'
            + '<td><input type="number" step="0.001" min="0.001" value="1" class="form-control input-sm epc-po-qty" /></td>'
            + '<td><input type="number" step="0.01" min="0" value="0.00" class="form-control input-sm epc-po-unit" /></td>'
            + '<td class="epc-po-line-total text-right">0.00</td>'
            + '<td><button type="button" class="btn btn-link btn-xs epc-po-line-del">Remove</button></td>';
        row.querySelector('.epc-po-qty').addEventListener('input', recalc);
        row.querySelector('.epc-po-unit').addEventListener('input', recalc);
        row.querySelector('.epc-po-line-del').addEventListener('click', function () {
            row.parentNode.removeChild(row);
            recalc();
        });
        recalc();
        return row;
    }

    function collectLines() {
        var lines = [];
        Array.prototype.forEach.call(lineBody ? lineBody.rows : [], function (row) {
            var description = row.querySelector('.epc-po-desc').value.trim();
            if (!description) {
                return;
            }

            var qty = Math.max(0.001, parseFloat(row.querySelector('.epc-po-qty').value) || 0);
            var unit = parseFloat(row.querySelector('.epc-po-unit').value) || 0;
            lines.push({
                item_code: row.querySelector('.epc-po-code').value.trim(),
                description: description,
                qty: qty,
                unit_cost_ex_vat: unit,
                line_ex_vat: round2(qty * unit)
            });
        });

        return lines;
    }

    var addButton = document.getElementById('po-line-add');
    if (addButton) {
        addButton.addEventListener('click', addLine);
        addLine();
    }

    var picker = document.getElementById('po-item-picker');
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

            row.querySelector('.epc-po-code').value = option.getAttribute('data-sku') || '';
            row.querySelector('.epc-po-desc').value = option.getAttribute('data-name') || '';
            row.querySelector('.epc-po-unit').value = option.getAttribute('data-price') || '0';
            picker.selectedIndex = 0;
            recalc();
        });
    }

    var save = document.getElementById('po-save');
    if (save) {
        save.addEventListener('click', function () {
            var lines = collectLines();
            if (lines.length === 0) {
                say('Add at least one line with a description.', false);
                return;
            }

            post('/erp/ajax/po-save', {
                confirmWrites: true,
                id: numeric('po-id'),
                supplierId: numeric('po-supplier'),
                title: text('po-title'),
                amountExVat: numeric('po-amount'),
                notes: text('po-notes'),
                linesJson: JSON.stringify(lines)
            }, reload);
        });
    }

    function ownerId(button) {
        var owner = button.closest('.epc-po-actions');
        return owner ? parseInt(owner.getAttribute('data-po-id'), 10) : 0;
    }

    Array.prototype.forEach.call(document.querySelectorAll('.epc-po-status'), function (button) {
        button.addEventListener('click', function () {
            post('/erp/ajax/po-status', {
                confirmWrites: true,
                id: ownerId(button),
                targetStatus: button.getAttribute('data-status')
            }, reload);
        });
    });

    Array.prototype.forEach.call(document.querySelectorAll('.epc-po-invoice'), function (button) {
        button.addEventListener('click', function () {
            var id = ownerId(button);
            if (!window.confirm('Convert purchase order #' + id + ' to a purchase invoice?')) {
                return;
            }

            post('/erp/ajax/po-to-invoice', { confirmWrites: true, id: id }, reload);
        });
    });

    Array.prototype.forEach.call(document.querySelectorAll('.epc-po-delete'), function (button) {
        button.addEventListener('click', function () {
            var id = ownerId(button);
            if (!window.confirm('Delete draft purchase order #' + id + '?')) {
                return;
            }

            post('/erp/purchase-orders/delete', { confirmWrites: true, purchaseOrderId: id }, reload);
        });
    });

    // Goods receipt — posts the per-line qty map the PHP receive form builds.
    Array.prototype.forEach.call(document.querySelectorAll('.epc-po-receive'), function (button) {
        button.addEventListener('click', function () {
            var table = button.closest('.epc-po-lines');
            if (!table) {
                return;
            }

            var received = {};
            var any = false;
            Array.prototype.forEach.call(table.querySelectorAll('.epc-po-receive-qty'), function (input) {
                var qty = parseFloat(input.value);
                if (!isNaN(qty) && qty > 0) {
                    received[input.getAttribute('data-line-id')] = qty;
                    any = true;
                }
            });

            if (!any) {
                say('Enter a receive quantity on at least one line.', false);
                return;
            }

            post('/erp/ajax/po-receive-lines', {
                confirmWrites: true,
                id: parseInt(table.getAttribute('data-po-id'), 10),
                receivedJson: JSON.stringify(received)
            }, reload);
        });
    });
})();
