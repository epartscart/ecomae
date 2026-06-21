(function () {
	window.save_action = function () {
		var priceEl = document.getElementById('inp_price');
		var priceZakupEl = document.getElementById('inp_price_zakup');
		var countEl = document.getElementById('inp_count_need');
		var orderIdEl = document.getElementById('inp_order_id');
		var storageEl = document.getElementById('inp_storage_id');
		if (!priceEl || !priceZakupEl || !countEl || !orderIdEl || !storageEl) {
			alert('Edit form is not ready. Reload the page or check that this order line exists.');
			return;
		}
		document.getElementById('price').value = priceEl.value;
		document.getElementById('price_zakup').value = priceZakupEl.value;
		document.getElementById('count_need').value = countEl.value;
		document.getElementById('order_id').value = orderIdEl.value;
		document.getElementById('storage_id').value = storageEl.value;
		document.getElementById('art').value = document.getElementById('art_item_inp').value;
		document.getElementById('man').value = document.getElementById('man_item_inp').value;
		document.getElementById('name').value = document.getElementById('name_item_inp').value;
		document.getElementById('t2_time_to_exe').value = document.getElementById('t2_time_to_exe_item_inp').value;
		document.getElementById('t2_time_to_exe_guaranteed').value = document.getElementById('t2_time_to_exe_guaranteed_item_inp').value;
		document.forms.save_form.submit();
	};
})();
