(function () {
  'use strict';

  function watchOnHand(formSelector, whName, itemName, qtyName, outTypes, hintId) {
    var form = document.querySelector('form[action="' + formSelector + '"]');
    var hint = document.getElementById(hintId);
    if (!form || !hint) { return; }
    var whInput = form.querySelector('[name="' + whName + '"]');
    var itemInput = form.querySelector('[name="' + itemName + '"]');
    var qtyInput = qtyName ? form.querySelector('[name="' + qtyName + '"]') : null;
    var typeInput = form.querySelector('[name="movementType"]');
    if (!whInput || !itemInput) { return; }
    var seq = 0;
    function lookup() {
      var wh = parseInt(whInput.value, 10);
      var it = parseInt(itemInput.value, 10);
      if (!wh || !it || wh <= 0 || it <= 0) { hint.textContent = ''; return; }
      var my = ++seq;
      fetch('/erp/inv-on-hand?warehouseId=' + encodeURIComponent(wh) + '&itemId=' + encodeURIComponent(it), { credentials: 'same-origin' })
        .then(function (r) { if (my !== seq) { return null; } return r.json(); })
        .then(function (j) {
          if (!j) { return; }
          if (!j.ok) { hint.textContent = 'warehouse ' + wh + ' / item ' + it + ': no stock rows'; return; }
          var onHand = Number(j.qtyOnHand || 0);
          var label = (j.sku || ('#' + j.itemId)) + ' — on hand ' + onHand.toFixed(2) + ' @ avg ' + Number(j.avgUnitCost || 0).toFixed(2);
          if (j.atReorder) { label += ' — at/below reorder level'; }
          var qty = qtyInput ? parseFloat(qtyInput.value) : NaN;
          var isOut = outTypes === null || (typeInput && outTypes.indexOf(typeInput.value) !== -1);
          if (isOut && !isNaN(qty) && qty > onHand) {
            label += ' — EXCEEDS on hand by ' + (qty - onHand).toFixed(2);
            hint.style.color = '#b3261e';
          } else {
            hint.style.color = '';
          }
          hint.textContent = label;
        })
        .catch(function () { hint.textContent = ''; });
    }
    [whInput, itemInput, qtyInput, typeInput].forEach(function (el) {
      if (!el) { return; }
      el.addEventListener('input', lookup);
      el.addEventListener('change', lookup);
    });
    if (whInput.value && itemInput.value) { lookup(); }
  }

  watchOnHand('/erp/ajax/inv-record-movement', 'warehouseId', 'itemId', 'qty', ['sale_out', 'issue', 'adjustment_out'], 'mv_on_hand_hint');
  watchOnHand('/erp/ajax/inv-transfer', 'fromWarehouseId', 'itemId', 'qty', null, 'tf_on_hand_hint');
})();
