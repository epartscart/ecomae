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

    var save = document.getElementById('so-save');
    if (save) {
        save.addEventListener('click', function () {
            post('/erp/ajax/so-save', {
                confirmWrites: true,
                id: numeric('so-id'),
                customerUserId: numeric('so-customer'),
                title: text('so-title'),
                amountExVat: numeric('so-amount'),
                notes: text('so-notes')
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
