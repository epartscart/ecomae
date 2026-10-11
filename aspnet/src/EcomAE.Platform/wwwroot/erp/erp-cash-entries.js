(function () {
  'use strict';
  var input = document.getElementById('rv_sales_invoice_id');
  var hint = document.getElementById('rv_invoice_balance_hint');
  var amount = document.getElementById('rv_amount');
  var user = document.getElementById('rv_user_id');
  if (!input || !hint) { return; }
  var seq = 0;
  function show(html) { hint.innerHTML = html; }
  function lookup() {
    var id = parseInt(input.value, 10);
    if (!id || id <= 0) { hint.textContent = ''; return; }
    var my = ++seq;
    fetch('/erp/invoice-open-balance?id=' + encodeURIComponent(id), { credentials: 'same-origin' })
      .then(function (r) { if (my !== seq) { return null; } return r.json(); })
      .then(function (j) {
        if (!j) { return; }
        if (!j.ok) { hint.textContent = 'INV-' + id + ': not found'; return; }
        var due = Number(j.amountDue || 0);
        var label = (j.invoiceNumber || ('INV-' + j.id)) + ' — open ' + due.toFixed(2) + ' ' + (j.currency || '') +
          ' (' + (j.status || '') + ', paid ' + Number(j.paidAmount || 0).toFixed(2) + ')';
        if (due <= 0) { show(label + ' — already settled'); return; }
        show(label + ' <a href="javascript:void(0)" id="rv_apply_due">use open balance</a>');
        var apply = document.getElementById('rv_apply_due');
        if (apply) {
          apply.onclick = function () {
            amount.value = due.toFixed(2);
            if (user && (!user.value || user.value === '0')) { user.value = j.customerUserId || ''; }
          };
        }
      })
      .catch(function () { hint.textContent = ''; });
  }
  input.addEventListener('input', lookup);
  input.addEventListener('change', lookup);
  if (input.value) { lookup(); }
})();
