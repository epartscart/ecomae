(function () {
	var app = document.getElementById('epc-pos-app');
	if (!app || app.getAttribute('data-pos-ready') === '1') return;
	app.setAttribute('data-pos-ready', '1');

	var ajaxUrl = app.getAttribute('data-ajax-url') || '';
	var posUrl = app.getAttribute('data-pos-url') || '';
	var csrf = app.getAttribute('data-csrf') || '';
	var sessionId = parseInt(app.getAttribute('data-session-id') || '0', 10);
	var sessionOpen = app.getAttribute('data-session-open') === '1';
	var currency = app.getAttribute('data-currency') || 'AED';
	var cart = [];
	var payMode = 'cash';
	var customer = { user_id: 0, contact_id: 0, label: 'Walk-in guest' };
	var searchTimer = null;
	var lastTotal = 0;

	function fmt(n) { return (Math.round(n * 100) / 100).toFixed(2); }

	function money(n) { return currency + ' ' + fmt(n); }

	function showMsg(text, ok) {
		var el = document.getElementById('epc-pos-msg');
		if (!el) return;
		el.textContent = text;
		el.className = 'epc-pos-msg ' + (ok ? 'ok' : 'err');
		if (ok) setTimeout(function () { el.className = 'epc-pos-msg'; el.textContent = ''; }, 4000);
	}

	function post(action, data, cb) {
		var fd = new FormData();
		fd.append('action', action);
		if (csrf) fd.append('csrf_guard_key', csrf);
		Object.keys(data || {}).forEach(function (k) {
			var v = data[k];
			if (v !== undefined && v !== null) {
				fd.append(k, typeof v === 'object' ? JSON.stringify(v) : v);
			}
		});
		fetch(ajaxUrl, { method: 'POST', body: fd, credentials: 'same-origin' })
			.then(function (r) { return r.json(); })
			.then(function (j) { cb(null, j); })
			.catch(function (e) { cb(e); });
	}

	function esc(s) {
		var d = document.createElement('div');
		d.textContent = s || '';
		return d.innerHTML;
	}

	function stockPill(stock) {
		if (stock === null || stock === undefined || stock === '') {
			return '<span class="epc-pos-pill">Stock n/a</span>';
		}
		var n = parseFloat(stock);
		if (isNaN(n)) return '<span class="epc-pos-pill">Stock n/a</span>';
		if (n <= 0) return '<span class="epc-pos-pill epc-pos-pill--stock-out">Out of stock</span>';
		if (n <= 5) return '<span class="epc-pos-pill epc-pos-pill--stock-low">Stock ' + n + '</span>';
		return '<span class="epc-pos-pill epc-pos-pill--stock-ok">Stock ' + n + '</span>';
	}

	function renderProducts(items) {
		var box = document.getElementById('epc-pos-products');
		if (!box) return;
		if (!items || !items.length) {
			box.innerHTML = '<div class="epc-pos-empty epc-pos-empty--compact">No products found — try another SKU or name</div>';
			return;
		}
		box.innerHTML = items.map(function (p, i) {
			var skuLine = esc(p.sku || p.barcode || '');
			if (p.brand) skuLine += (skuLine ? ' · ' : '') + esc(p.brand);
			var meta = stockPill(p.stock);
			if (p.storage) meta += '<span class="epc-pos-pill">' + esc(p.storage) + '</span>';
			if (p.source) meta += '<span class="epc-pos-pill">' + esc(p.source) + '</span>';
			return '<button type="button" class="epc-pos-product" data-idx="' + i + '">' +
				'<div class="name">' + esc(p.name) + '</div>' +
				'<div class="sku">' + skuLine + '</div>' +
				'<div class="price">' + money(p.price || 0) + '</div>' +
				'<div class="meta-row">' + meta + '</div></button>';
		}).join('');
		box._items = items;
		box.querySelectorAll('.epc-pos-product').forEach(function (btn) {
			btn.addEventListener('click', function () {
				addToCart(items[parseInt(btn.getAttribute('data-idx'), 10)]);
			});
		});
	}

	function addToCart(p) {
		if (!p) return;
		var key = (p.source || '') + '_' + (p.ref || p.sku);
		var existing = cart.find(function (l) { return l._key === key; });
		if (existing) {
			existing.qty += 1;
		} else {
			cart.push({
				_key: key,
				source: p.source || 'manual',
				ref: p.ref || '',
				sku: p.sku || '',
				barcode: p.barcode || p.sku || '',
				name: p.name || 'Item',
				brand: p.brand || '',
				stock: p.stock,
				storage: p.storage || '',
				qty: 1,
				unit_price_ex: parseFloat(p.price) || 0,
				line_discount_pct: 0,
				line_discount_amt: 0
			});
		}
		renderCart();
		recalc();
	}

	function updateCartCount() {
		var el = document.getElementById('epc-pos-cart-count');
		if (!el) return;
		var n = cart.length;
		var qty = cart.reduce(function (s, l) { return s + (parseFloat(l.qty) || 0); }, 0);
		el.textContent = n + (n === 1 ? ' line' : ' lines') + (qty ? ' · qty ' + fmt(qty).replace(/\.00$/, '') : '');
	}

	function lineNet(l) {
		var lineGross = l.qty * l.unit_price_ex;
		var disc = l.line_discount_amt || 0;
		if (l.line_discount_pct > 0) disc = lineGross * l.line_discount_pct / 100;
		return Math.max(0, lineGross - disc);
	}

	function renderCart() {
		var box = document.getElementById('epc-pos-cart');
		var checkoutBtn = document.getElementById('epc-pos-checkout');
		updateCartCount();
		if (!box) return;
		if (!cart.length) {
			box.innerHTML = '<div class="epc-pos-empty epc-pos-empty--compact">Cart is empty — add products from search</div>';
			if (checkoutBtn) checkoutBtn.disabled = true;
			updateChange();
			return;
		}
		if (checkoutBtn) checkoutBtn.disabled = !sessionOpen;
		box.innerHTML = cart.map(function (l, i) {
			return '<div class="epc-pos-cart-line" data-i="' + i + '">' +
				'<div><div class="title">' + esc(l.name) + '</div>' +
				'<div class="meta">' + esc(l.sku || l.barcode || '') +
				(l.brand ? ' · ' + esc(l.brand) : '') +
				(l.stock !== null && l.stock !== undefined ? ' · stock ' + esc(String(l.stock)) : '') +
				'</div>' +
				'<div class="epc-pos-qty-row">' +
				'<button type="button" class="epc-pos-qty-btn" data-act="minus" aria-label="Decrease quantity">−</button>' +
				'<span class="epc-pos-qty-val">' + l.qty + '</span>' +
				'<button type="button" class="epc-pos-qty-btn" data-act="plus" aria-label="Increase quantity">+</button>' +
				'<input type="number" min="0" step="0.01" class="epc-pos-line-field epc-pos-line-field--price" data-field="price" title="Unit price ex tax" value="' + fmt(l.unit_price_ex) + '" aria-label="Unit price">' +
				'<input type="number" min="0" max="100" step="1" class="epc-pos-line-field" data-field="pct" title="% discount" placeholder="% off" value="' + (l.line_discount_pct || '') + '" aria-label="Discount percent">' +
				'<input type="number" min="0" step="0.01" class="epc-pos-line-field epc-pos-line-field--wide" data-field="amt" title="Discount amount" placeholder="− amt" value="' + (l.line_discount_pct > 0 ? '' : (l.line_discount_amt || '')) + '" aria-label="Discount amount">' +
				'<button type="button" class="epc-pos-btn epc-pos-btn-muted" data-act="remove" style="min-height:32px;padding:4px 8px" aria-label="Remove line"><i class="fa fa-times"></i></button>' +
				'</div></div>' +
				'<div class="line-amt">' + money(lineNet(l)) + '</div></div>';
		}).join('');

		box.querySelectorAll('.epc-pos-cart-line').forEach(function (row) {
			var i = parseInt(row.getAttribute('data-i'), 10);
			row.querySelector('[data-act="minus"]').addEventListener('click', function () {
				if (cart[i].qty > 1) cart[i].qty--; else cart.splice(i, 1);
				renderCart(); recalc();
			});
			row.querySelector('[data-act="plus"]').addEventListener('click', function () {
				cart[i].qty++; renderCart(); recalc();
			});
			row.querySelector('[data-act="remove"]').addEventListener('click', function () {
				cart.splice(i, 1); renderCart(); recalc();
			});
			row.querySelector('[data-field="price"]').addEventListener('change', function (e) {
				cart[i].unit_price_ex = Math.max(0, parseFloat(e.target.value) || 0);
				renderCart(); recalc();
			});
			row.querySelector('[data-field="pct"]').addEventListener('change', function (e) {
				cart[i].line_discount_pct = Math.min(100, Math.max(0, parseFloat(e.target.value) || 0));
				if (cart[i].line_discount_pct > 0) cart[i].line_discount_amt = 0;
				renderCart(); recalc();
			});
			row.querySelector('[data-field="amt"]').addEventListener('change', function (e) {
				cart[i].line_discount_amt = Math.max(0, parseFloat(e.target.value) || 0);
				cart[i].line_discount_pct = 0;
				renderCart(); recalc();
			});
		});
		updateChange();
	}

	function updateChange() {
		var wrap = document.getElementById('epc-pos-change-wrap');
		var changeEl = document.getElementById('epc-pos-change');
		var tenderedEl = document.getElementById('epc-pos-tendered');
		if (!changeEl) return;
		var tendered = parseFloat(tenderedEl && tenderedEl.value ? tenderedEl.value : '0') || 0;
		var change = Math.max(0, tendered - lastTotal);
		changeEl.textContent = money(change);
		if (wrap) {
			wrap.style.opacity = (payMode === 'cash' && tendered > 0) ? '1' : '0.7';
		}
	}

	function syncPayFields() {
		var cashFields = document.getElementById('epc-pos-cash-fields');
		var split = document.getElementById('epc-pos-split-fields');
		if (cashFields) cashFields.style.display = payMode === 'cash' ? 'grid' : 'none';
		if (split) split.style.display = payMode === 'split' ? 'grid' : 'none';
		updateChange();
	}

	function recalc() {
		post('calc_cart', {
			lines: cart,
			customer_user_id: customer.user_id,
			contact_id: customer.contact_id
		}, function (err, res) {
			if (err || !res || !res.status) return;
			var t = res.totals || {};
			lastTotal = parseFloat(t.total_amount) || 0;
			var sub = document.getElementById('epc-pos-subtotal');
			var disc = document.getElementById('epc-pos-discount');
			var vat = document.getElementById('epc-pos-vat');
			var total = document.getElementById('epc-pos-total');
			var rateLbl = document.getElementById('epc-pos-tax-rate-lbl');
			if (sub) sub.textContent = fmt(t.subtotal_ex || 0);
			if (disc) disc.textContent = fmt(t.discount_total || 0);
			if (vat) vat.textContent = fmt(t.vat_amount || 0);
			if (total) total.textContent = fmt(t.total_amount || 0);
			if (rateLbl && t.tax_rate !== undefined) rateLbl.textContent = fmt(t.tax_rate).replace(/\.0$/, '');
			if (payMode === 'split') {
				var cashEl = document.getElementById('epc-pos-cash-amt');
				var cardEl = document.getElementById('epc-pos-card-amt');
				if (cashEl && !cashEl.value && cardEl && !cardEl.value) {
					cashEl.value = fmt(lastTotal);
					cardEl.value = '0.00';
				}
			}
			updateChange();
		});
	}

	function doSearch() {
		var qEl = document.getElementById('epc-pos-q');
		if (!qEl) return;
		var q = qEl.value.trim();
		if (q.length < 1) return;
		post('search_products', { q: q }, function (err, res) {
			if (err) { showMsg('Search failed', false); return; }
			if (!res.status) { showMsg(res.message || 'Search error', false); return; }
			renderProducts(res.products || []);
			if ((res.products || []).length === 1 && q.length >= 4) {
				addToCart(res.products[0]);
				qEl.value = '';
				renderProducts([]);
				showMsg('Added to cart', true);
			}
		});
	}

	function setCustomer(c) {
		customer = c || { user_id: 0, contact_id: 0, label: 'Walk-in guest' };
		var u = document.getElementById('epc-pos-customer-user');
		var ct = document.getElementById('epc-pos-customer-contact');
		var lbl = document.getElementById('epc-pos-customer-label');
		if (u) u.value = customer.user_id;
		if (ct) ct.value = customer.contact_id;
		if (lbl) lbl.innerHTML = '<i class="fa fa-check-circle"></i> ' + esc(customer.label);
		recalc();
	}

	var searchBtn = document.getElementById('epc-pos-search-btn');
	var qInput = document.getElementById('epc-pos-q');
	if (searchBtn) searchBtn.addEventListener('click', doSearch);
	if (qInput) {
		qInput.addEventListener('keydown', function (e) {
			if (e.key === 'Enter') { e.preventDefault(); doSearch(); }
		});
		qInput.addEventListener('input', function () {
			clearTimeout(searchTimer);
			var q = this.value.trim();
			if (q.length >= 2) searchTimer = setTimeout(doSearch, 350);
		});
	}

	var customerQ = document.getElementById('epc-pos-customer-q');
	if (customerQ) {
		customerQ.addEventListener('input', function () {
			var q = this.value.trim();
			var pick = document.getElementById('epc-pos-customer-pick');
			if (!pick) return;
			if (q.length < 2) { pick.style.display = 'none'; return; }
			post('search_customers', { q: q }, function (err, res) {
				if (!res || !res.status || !res.customers.length) { pick.style.display = 'none'; return; }
				pick.style.display = 'block';
				pick.innerHTML = res.customers.map(function (c, i) {
					return '<button type="button" class="epc-pos-btn epc-pos-btn-muted" data-ci="' + i + '">' +
						esc(c.label) + (c.email ? ' · ' + esc(c.email) : '') +
						(c.phone ? ' · ' + esc(c.phone) : '') + '</button>';
				}).join('');
				pick._customers = res.customers;
				pick.querySelectorAll('button').forEach(function (btn) {
					btn.addEventListener('click', function () {
						var c = pick._customers[parseInt(btn.getAttribute('data-ci'), 10)];
						setCustomer({ user_id: c.user_id || 0, contact_id: c.contact_id || 0, label: c.label });
						pick.style.display = 'none';
					});
				});
			});
		});
	}

	var walkinBtn = document.getElementById('epc-pos-customer-walkin');
	if (walkinBtn) {
		walkinBtn.addEventListener('click', function () {
			if (customerQ) customerQ.value = '';
			var pick = document.getElementById('epc-pos-customer-pick');
			if (pick) pick.style.display = 'none';
			setCustomer({ user_id: 0, contact_id: 0, label: 'Walk-in guest' });
		});
	}

	document.querySelectorAll('#epc-pos-pay-mode button').forEach(function (btn) {
		btn.addEventListener('click', function () {
			document.querySelectorAll('#epc-pos-pay-mode button').forEach(function (b) { b.classList.remove('active'); });
			btn.classList.add('active');
			payMode = btn.getAttribute('data-pay');
			syncPayFields();
		});
	});

	var tenderedEl = document.getElementById('epc-pos-tendered');
	if (tenderedEl) {
		tenderedEl.addEventListener('input', updateChange);
	}

	var clearBtn = document.getElementById('epc-pos-clear');
	if (clearBtn) {
		clearBtn.addEventListener('click', function () {
			cart = [];
			var notes = document.getElementById('epc-pos-notes');
			if (notes) notes.value = '';
			if (tenderedEl) tenderedEl.value = '';
			renderCart();
			recalc();
		});
	}

	var warehouseSel = document.getElementById('epc-pos-warehouse');
	if (warehouseSel) {
		warehouseSel.addEventListener('change', function () {
			app.setAttribute('data-warehouse-id', this.value || '0');
		});
	}

	var checkoutBtn = document.getElementById('epc-pos-checkout');
	if (checkoutBtn) {
		checkoutBtn.addEventListener('click', function () {
			if (!sessionOpen) { showMsg('Open the register first', false); return; }
			if (!cart.length) return;
			var cashEl = document.getElementById('epc-pos-cash-amt');
			var cardEl = document.getElementById('epc-pos-card-amt');
			var notesEl = document.getElementById('epc-pos-notes');
			var whEl = document.getElementById('epc-pos-warehouse');
			if (payMode === 'cash' && tenderedEl && tenderedEl.value) {
				var tendered = parseFloat(tenderedEl.value) || 0;
				if (tendered > 0 && tendered + 0.001 < lastTotal) {
					showMsg('Cash tendered is less than total due', false);
					return;
				}
			}
			var payload = {
				session_id: sessionId,
				lines: cart,
				payment_method: payMode,
				customer_user_id: customer.user_id,
				contact_id: customer.contact_id,
				customer_label: customer.label,
				cash_amount: parseFloat(cashEl && cashEl.value ? cashEl.value : '0') || 0,
				card_amount: parseFloat(cardEl && cardEl.value ? cardEl.value : '0') || 0,
				warehouse_id: parseInt(whEl && whEl.value ? whEl.value : (app.getAttribute('data-warehouse-id') || '0'), 10) || 0,
				sale_notes: notesEl ? notesEl.value.trim() : ''
			};
			checkoutBtn.disabled = true;
			post('complete_sale', payload, function (err, res) {
				checkoutBtn.disabled = false;
				if (err || !res || !res.status) {
					showMsg((res && res.message) || 'Checkout failed', false);
					return;
				}
				showMsg('Sale ' + res.sale_no + ' completed', true);
				cart = [];
				setCustomer({ user_id: 0, contact_id: 0, label: 'Walk-in guest' });
				if (customerQ) customerQ.value = '';
				if (notesEl) notesEl.value = '';
				if (tenderedEl) tenderedEl.value = '';
				renderCart();
				recalc();
				if (res.sale_id) {
					window.open(posUrl + '?action=receipt&sale_id=' + res.sale_id, '_blank', 'width=400,height=640');
				}
			});
		});
	}

	function openSessionModal() {
		var bg = document.createElement('div');
		bg.className = 'epc-pos-modal-bg';
		bg.innerHTML = '<div class="epc-pos-modal"><h4>Open register</h4>' +
			'<p class="epc-pos-modal__hint">Count the opening float before the first sale of this shift.</p>' +
			'<label for="epc-float-in">Opening float (cash in drawer) — ' + esc(currency) + '</label>' +
			'<input type="number" step="0.01" id="epc-float-in" value="0" inputmode="decimal">' +
			'<div style="display:flex;gap:8px">' +
			'<button type="button" class="epc-pos-btn epc-pos-btn-primary" style="flex:1" id="epc-float-ok">Open shift</button>' +
			'<button type="button" class="epc-pos-btn epc-pos-btn-muted" id="epc-float-cancel">Cancel</button></div></div>';
		document.body.appendChild(bg);
		bg.querySelector('#epc-float-cancel').onclick = function () { bg.remove(); };
		bg.querySelector('#epc-float-ok').onclick = function () {
			var fl = parseFloat(bg.querySelector('#epc-float-in').value) || 0;
			post('open_session', { opening_float: fl }, function (err, res) {
				if (res && res.status) { location.reload(); }
				else { showMsg((res && res.message) || 'Failed', false); bg.remove(); }
			});
		};
	}

	function closeSessionModal() {
		var bg = document.createElement('div');
		bg.className = 'epc-pos-modal-bg';
		bg.innerHTML = '<div class="epc-pos-modal"><h4>Close shift</h4>' +
			'<p class="epc-pos-modal__hint">Count cash in the drawer and note any variance.</p>' +
			'<label for="epc-close-cash">Cash counted in drawer — ' + esc(currency) + '</label>' +
			'<input type="number" step="0.01" id="epc-close-cash" value="0" inputmode="decimal">' +
			'<label for="epc-close-notes">Notes (optional)</label>' +
			'<input type="text" id="epc-close-notes" placeholder="Variance explanation">' +
			'<div style="display:flex;gap:8px">' +
			'<button type="button" class="epc-pos-btn epc-pos-btn-danger" style="flex:1" id="epc-close-ok">Close shift</button>' +
			'<button type="button" class="epc-pos-btn epc-pos-btn-muted" id="epc-close-cancel">Cancel</button></div></div>';
		document.body.appendChild(bg);
		bg.querySelector('#epc-close-cancel').onclick = function () { bg.remove(); };
		bg.querySelector('#epc-close-ok').onclick = function () {
			post('close_session', {
				session_id: sessionId,
				closing_cash: parseFloat(bg.querySelector('#epc-close-cash').value) || 0,
				notes: bg.querySelector('#epc-close-notes').value
			}, function (err, res) {
				if (res && res.status) { location.reload(); }
				else { showMsg((res && res.message) || 'Failed', false); bg.remove(); }
			});
		};
	}

	var openBtn = document.getElementById('epc-pos-open-session');
	var closeBtn = document.getElementById('epc-pos-close-session');
	if (openBtn) openBtn.addEventListener('click', openSessionModal);
	if (closeBtn) closeBtn.addEventListener('click', closeSessionModal);

	document.addEventListener('keydown', function (e) {
		var tag = (e.target && e.target.tagName) ? e.target.tagName.toLowerCase() : '';
		var typing = tag === 'input' || tag === 'textarea' || tag === 'select';
		if (e.key === 'F2') {
			e.preventDefault();
			if (qInput) qInput.focus();
		} else if (e.key === 'F4') {
			e.preventDefault();
			if (customerQ) customerQ.focus();
		} else if (e.key === 'F9') {
			e.preventDefault();
			if (checkoutBtn && !checkoutBtn.disabled) checkoutBtn.click();
		} else if (e.key === 'Escape' && !typing) {
			if (qInput) { qInput.value = ''; qInput.focus(); }
		}
	});

	syncPayFields();
	renderCart();
	recalc();
})();
