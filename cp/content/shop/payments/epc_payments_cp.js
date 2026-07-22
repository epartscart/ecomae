/**
 * Payments CP hub — dashboard + individual accounts.
 */
(function () {
  'use strict';

  function ready(fn) {
    if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', fn);
    else fn();
  }

  ready(function () {
    var root = document.querySelector('.epc-pay-hub');
    if (!root) return;

    var ajaxUrl = root.getAttribute('data-ajax-url') || '/cp/content/shop/payments/ajax_payments_endpoint.php';
    var csrf = root.getAttribute('data-csrf') || '';

    function post(action, extra) {
      var fd = new FormData();
      fd.append('action', action);
      if (csrf) fd.append('csrf_guard_key', csrf);
      Object.keys(extra || {}).forEach(function (k) {
        fd.append(k, extra[k] == null ? '' : String(extra[k]));
      });
      return fetch(ajaxUrl, { method: 'POST', body: fd, credentials: 'same-origin' }).then(function (r) {
        return r.text().then(function (t) {
          try {
            return JSON.parse(t);
          } catch (e) {
            return { status: false, message: 'Invalid JSON (HTTP ' + r.status + ')' };
          }
        });
      });
    }

    function msg(j) {
      var el = document.getElementById('epc_pay_msg');
      if (!el) {
        if (j && !j.status) alert((j && j.message) || 'Request failed');
        return;
      }
      el.className = 'alert alert-' + (j && j.status ? 'success' : 'danger');
      el.textContent = (j && j.message) || 'Request failed';
      el.style.display = 'block';
      if (j && j.status) setTimeout(function () { location.reload(); }, 700);
    }

    window.epcPayPost = function (action, extra) {
      return post(action, extra).then(msg);
    };

    var seed = document.getElementById('epc_btn_seed');
    if (seed) seed.addEventListener('click', function () { post('seed_dummy', {}).then(msg); });
    var actStripe = document.getElementById('epc_btn_activate_stripe');
    if (actStripe) actStripe.addEventListener('click', function () { post('activate', { handler: 'stripe' }).then(msg); });
    var actCrypto = document.getElementById('epc_btn_activate_crypto');
    if (actCrypto) actCrypto.addEventListener('click', function () { post('activate', { handler: 'nowpayments' }).then(msg); });
    document.querySelectorAll('.epc-activate-gw').forEach(function (btn) {
      btn.addEventListener('click', function () {
        post('activate', { handler: btn.getAttribute('data-handler') }).then(msg);
      });
    });

    function toggleOwner() {
      var ot = document.getElementById('epc_acc_owner_type');
      if (!ot) return;
      var t = ot.value;
      var ow = document.getElementById('epc_acc_office_wrap');
      var vw = document.getElementById('epc_acc_vendor_wrap');
      if (ow) ow.style.display = t === 'office' ? '' : 'none';
      if (vw) vw.style.display = t === 'vendor' ? '' : 'none';
    }
    var otEl = document.getElementById('epc_acc_owner_type');
    if (otEl) {
      otEl.addEventListener('change', toggleOwner);
      toggleOwner();
    }

    var accForm = document.getElementById('epc_pay_account_form');
    if (accForm) {
      function saveAccount(ev) {
        if (ev) ev.preventDefault();
        var fd = new FormData(accForm);
        var ownerType = String(fd.get('owner_type') || 'platform');
        var ownerId = 0;
        if (ownerType === 'office') ownerId = parseInt(fd.get('office_id') || '0', 10) || 0;
        if (ownerType === 'vendor') ownerId = parseInt(fd.get('vendor_id') || '0', 10) || 0;
        if ((ownerType === 'office' || ownerType === 'vendor') && ownerId <= 0) {
          msg({ status: false, message: 'Select an office or vendor' });
          return false;
        }
        var credsRaw = String(fd.get('credentials_json') || '{}');
        try {
          JSON.parse(credsRaw);
        } catch (e) {
          msg({ status: false, message: 'Credentials JSON is invalid' });
          return false;
        }
        var demoEl = accForm.querySelector('[name=demo_mode]');
        var defEl = accForm.querySelector('[name=is_default]');
        post('save_account', {
          id: fd.get('id') || 0,
          owner_type: ownerType,
          owner_id: ownerId,
          title: fd.get('title') || '',
          handler: fd.get('handler') || '',
          mode: fd.get('mode') || 'direct',
          connected_account_id: fd.get('connected_account_id') || '',
          payout_iban: fd.get('payout_iban') || '',
          payout_bank: fd.get('payout_bank') || '',
          payout_name: fd.get('payout_name') || '',
          platform_fee_pct: fd.get('platform_fee_pct') || 0,
          status: fd.get('status') || 'active',
          demo_mode: demoEl && demoEl.checked ? 1 : 0,
          is_default: defEl && defEl.checked ? 1 : 0,
          credentials_json: credsRaw
        }).then(msg);
        return false;
      }
      accForm.addEventListener('submit', saveAccount);
      var saveBtn = accForm.querySelector('button[type="submit"]');
      if (saveBtn) saveBtn.addEventListener('click', saveAccount);
    }

    var seedAcc = document.getElementById('epc_btn_seed_platform_account');
    if (seedAcc) seedAcc.addEventListener('click', function () { post('seed_platform_account', {}).then(msg); });
    document.querySelectorAll('.epc-acc-disable').forEach(function (btn) {
      btn.addEventListener('click', function () {
        post('disable_account', { id: btn.getAttribute('data-id') }).then(msg);
      });
    });
    document.querySelectorAll('.epc-settle-paid').forEach(function (btn) {
      btn.addEventListener('click', function () {
        post('mark_settlement', { id: btn.getAttribute('data-id'), status: 'paid_out' }).then(msg);
      });
    });
  });
})();
