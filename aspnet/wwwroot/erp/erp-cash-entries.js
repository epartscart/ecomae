(function () {
  'use strict';

  function watchBalance(inputId, hintId, url, fill) {
    var input = document.getElementById(inputId);
    var hint = document.getElementById(hintId);
    if (!input || !hint) { return; }
    var seq = 0;
    function lookup() {
      var id = parseInt(input.value, 10);
      if (!id || id <= 0) { hint.textContent = ''; return; }
      var my = ++seq;
      fetch(url + encodeURIComponent(id), { credentials: 'same-origin' })
        .then(function (r) { if (my !== seq) { return null; } return r.json(); })
        .then(function (j) {
          if (!j) { return; }
          if (!j.ok) { hint.textContent = 'ID ' + id + ': not found'; return; }
          var due = Number(j.amountDue || 0);
          var label = (j.invoiceNumber || ('#' + j.id)) + ' — open ' + due.toFixed(2) + ' ' + (j.currency || '') +
            ' (' + (j.status || '') + ', paid ' + Number(j.paidAmount || 0).toFixed(2) + ')';
          if (due <= 0) { hint.textContent = label + ' — already settled'; return; }
          hint.innerHTML = label + ' <a href="javascript:void(0)">use open balance</a>';
          var apply = hint.querySelector('a');
          if (apply) {
            apply.onclick = function () { fill(j, due); };
          }
        })
        .catch(function () { hint.textContent = ''; });
    }
    input.addEventListener('input', lookup);
    input.addEventListener('change', lookup);
    if (input.value) { lookup(); }
  }

  var rvAmount = document.getElementById('rv_amount');
  var rvUser = document.getElementById('rv_user_id');
  watchBalance('rv_sales_invoice_id', 'rv_invoice_balance_hint', '/erp/invoice-open-balance?id=', function (j, due) {
    if (rvAmount) { rvAmount.value = due.toFixed(2); }
    if (rvUser && (!rvUser.value || rvUser.value === '0')) { rvUser.value = j.customerUserId || ''; }
  });

  var pvAmount = document.getElementById('pv_amount');
  var pvSupplier = document.getElementById('pv_supplier_id');
  watchBalance('pv_purchase_id', 'pv_bill_balance_hint', '/erp/purchase-open-balance?id=', function (j, due) {
    if (pvAmount) { pvAmount.value = due.toFixed(2); }
    if (pvSupplier && (!pvSupplier.value || pvSupplier.value === '0')) { pvSupplier.value = j.supplierId || ''; }
  });
})();
