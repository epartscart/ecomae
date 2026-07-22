<?php
/**
 * POS Terminal HTML — eval-safe (no ?> before markup; CP template uses eval()).
 */
defined('_ASTEXE_') or die('No access');

/**
 * @param array<string,mixed> $ctx
 */
function epc_pos_terminal_render_markup(array $ctx): void
{
	$stats = (array) ($ctx['stats'] ?? array());
	$openSession = $ctx['open_session'] ?? null;
	$taxCtx = (array) ($ctx['tax_ctx'] ?? array());
	$settings = (array) ($ctx['settings'] ?? array());
	$warehouses = (array) ($ctx['warehouses'] ?? array());
	$ajaxUrl = (string) ($ctx['ajax_url'] ?? '');
	$posUrl = (string) ($ctx['pos_url'] ?? '');
	$csrf = (string) ($ctx['csrf'] ?? '');
	$erpUrl = (string) ($ctx['erp_url'] ?? '');
	$whUrl = (string) ($ctx['warehouse_url'] ?? '');
	$settingsUrl = (string) ($ctx['settings_url'] ?? '');
	$currency = (string) ($ctx['currency'] ?? 'AED');
	$warehouseId = (int) ($ctx['warehouse_id'] ?? 0);
	$warehouseName = (string) ($ctx['warehouse_name'] ?? 'Default warehouse');
	$countryCode = (string) ($ctx['country_code'] ?? '');

	$sessionId = (int) ($openSession ? (int) $openSession['id'] : 0);
	$sessionOpen = $openSession ? '1' : '0';
	$registerName = epc_pos_h($settings['register_name'] ?? 'Register 1');
	$todaySales = (int) ($stats['today_sales'] ?? 0);
	$todayTotal = number_format((float) ($stats['today_total'] ?? 0), 2);
	$weekSales = (int) ($stats['week_sales'] ?? 0);
	$weekTotal = number_format((float) ($stats['week_total'] ?? 0), 2);
	$taxRate = number_format((float) ($taxCtx['tax_rate'] ?? 0), 1);
	$taxLabel = epc_pos_h($taxCtx['tax_label'] ?? 'VAT');
	$cur = epc_pos_h($currency);

	echo '<div class="col-lg-12 epc-pos-wrap" id="epc-pos-app"'
		. ' data-ajax-url="' . epc_pos_h($ajaxUrl) . '"'
		. ' data-pos-url="' . epc_pos_h($posUrl) . '"'
		. ' data-csrf="' . epc_pos_h($csrf) . '"'
		. ' data-session-id="' . $sessionId . '"'
		. ' data-session-open="' . $sessionOpen . '"'
		. ' data-currency="' . $cur . '"'
		. ' data-tax-label="' . $taxLabel . '"'
		. ' data-tax-rate="' . epc_pos_h((string) $taxRate) . '"'
		. ' data-warehouse-id="' . $warehouseId . '">';

	/* —— Brand strip —— */
	echo '<header class="epc-pos-brand">';
	echo '<div class="epc-pos-brand__mark"><i class="fa fa-cash-register" aria-hidden="true"></i></div>';
	echo '<div class="epc-pos-brand__text">';
	echo '<div class="epc-pos-brand__name">POS Terminal</div>';
	echo '<div class="epc-pos-brand__sub">' . $registerName;
	if ($countryCode !== '') {
		echo ' · ' . epc_pos_h($countryCode);
	}
	echo ' · ' . $cur . ' · ' . $taxLabel . ' ' . $taxRate . '%</div>';
	echo '</div>';
	echo '<div class="epc-pos-brand__links">';
	if ($settingsUrl !== '') {
		echo '<a class="epc-pos-chip-link" href="' . epc_pos_h($settingsUrl) . '"><i class="fa fa-cog"></i> Settings</a>';
	}
	if ($whUrl !== '') {
		echo '<a class="epc-pos-chip-link" href="' . epc_pos_h($whUrl) . '"><i class="fa fa-warehouse"></i> Warehouses</a>';
	}
	echo '<a class="epc-pos-chip-link" href="' . epc_pos_h($erpUrl) . '"><i class="fa fa-file-invoice"></i> ERP sales</a>';
	echo '</div>';
	echo '</header>';

	/* —— KPI strip —— */
	echo '<div class="epc-pos-kpi" role="group" aria-label="Shift metrics">';
	echo '<div class="epc-pos-kpi__card"><div class="epc-pos-kpi__icon"><i class="fa fa-receipt"></i></div><div><div class="epc-pos-kpi__label">Today sales</div><div class="epc-pos-kpi__value" id="epc-pos-kpi-today">' . $todaySales . '</div></div></div>';
	echo '<div class="epc-pos-kpi__card"><div class="epc-pos-kpi__icon"><i class="fa fa-coins"></i></div><div><div class="epc-pos-kpi__label">Today total</div><div class="epc-pos-kpi__value" id="epc-pos-kpi-today-total">' . $cur . ' ' . $todayTotal . '</div></div></div>';
	echo '<div class="epc-pos-kpi__card"><div class="epc-pos-kpi__icon"><i class="fa fa-chart-line"></i></div><div><div class="epc-pos-kpi__label">Week sales</div><div class="epc-pos-kpi__value">' . $weekSales . ' · ' . $cur . ' ' . $weekTotal . '</div></div></div>';
	echo '<div class="epc-pos-kpi__card"><div class="epc-pos-kpi__icon"><i class="fa fa-percent"></i></div><div><div class="epc-pos-kpi__label">' . $taxLabel . ' rate</div><div class="epc-pos-kpi__value">' . $taxRate . '%</div></div></div>';
	echo '<div class="epc-pos-kpi__card"><div class="epc-pos-kpi__icon"><i class="fa fa-store"></i></div><div><div class="epc-pos-kpi__label">Register</div><div class="epc-pos-kpi__value epc-pos-kpi__value--sm">' . $registerName . '</div></div></div>';
	echo '</div>';

	echo '<div id="epc-pos-msg" class="epc-pos-msg" role="status" aria-live="polite"></div>';

	/* —— Session bar —— */
	echo '<div class="epc-pos-session" id="epc-pos-session-bar">';
	echo '<div class="epc-pos-session__status">';
	if ($openSession) {
		echo '<span class="epc-pos-badge epc-pos-badge-open"><i class="fa fa-circle"></i> Shift open</span>';
		echo '<span class="epc-pos-session__meta"><strong>' . epc_pos_h($openSession['session_no']) . '</strong>';
		echo ' · Float ' . $cur . ' ' . number_format((float) $openSession['opening_float'], 2) . '</span>';
		echo '<button type="button" class="epc-pos-btn epc-pos-btn-muted" id="epc-pos-close-session"><i class="fa fa-lock"></i> Close shift</button>';
	} else {
		echo '<span class="epc-pos-badge epc-pos-badge-closed"><i class="fa fa-lock"></i> No open shift</span>';
		echo '<span class="epc-pos-session__meta">Open the register before checkout</span>';
		echo '<button type="button" class="epc-pos-btn epc-pos-btn-primary" id="epc-pos-open-session"><i class="fa fa-unlock"></i> Open register</button>';
	}
	echo '</div>';
	echo '<div class="epc-pos-session__warehouse">';
	echo '<label for="epc-pos-warehouse">Stock warehouse</label>';
	echo '<select id="epc-pos-warehouse" name="warehouse_id" aria-label="Stock warehouse">';
	if (empty($warehouses)) {
		echo '<option value="' . $warehouseId . '">' . epc_pos_h($warehouseName) . '</option>';
	} else {
		foreach ($warehouses as $wh) {
			$wid = (int) ($wh['id'] ?? 0);
			$wlabel = trim((string) (($wh['name'] ?? '') !== '' ? $wh['name'] : ($wh['code'] ?? 'Warehouse')));
			$sel = ($wid === $warehouseId) ? ' selected' : '';
			echo '<option value="' . $wid . '"' . $sel . '>' . epc_pos_h($wlabel) . '</option>';
		}
	}
	echo '</select>';
	echo '</div>';
	echo '<div class="epc-pos-session__keys" aria-label="Keyboard shortcuts">';
	echo '<span><kbd>Enter</kbd> search</span>';
	echo '<span><kbd>F2</kbd> focus scan</span>';
	echo '<span><kbd>F4</kbd> customer</span>';
	echo '<span><kbd>F9</kbd> checkout</span>';
	echo '</div>';
	echo '</div>';

	/* —— Main workspace —— */
	echo '<div class="epc-pos-grid">';

	/* Left: catalogue */
	echo '<section class="epc-pos-panel epc-pos-panel--catalog" aria-label="Product search">';
	echo '<div class="epc-pos-panel-head">';
	echo '<div><i class="fa fa-barcode"></i> Scan / search products</div>';
	echo '<span class="epc-pos-panel-head__hint">SKU · barcode · brand · name</span>';
	echo '</div>';
	echo '<div class="epc-pos-panel-body">';
	echo '<div class="epc-pos-search">';
	echo '<input type="search" id="epc-pos-q" placeholder="Scan barcode or type SKU / name…" autocomplete="off" autofocus aria-label="Product search">';
	echo '<button type="button" class="epc-pos-btn epc-pos-btn-primary" id="epc-pos-search-btn" title="Search"><i class="fa fa-search"></i> Search</button>';
	echo '</div>';
	echo '<div id="epc-pos-products" class="epc-pos-products">';
	echo '<div class="epc-pos-empty epc-pos-empty--graphical">';
	echo '<div class="epc-pos-empty__icon"><i class="fa fa-search"></i></div>';
	echo '<div class="epc-pos-empty__title">Ready to scan</div>';
	echo '<p class="epc-pos-empty__text">Type a SKU, barcode, or product name. A single exact match adds to the cart automatically.</p>';
	echo '<ol class="epc-pos-empty__steps"><li>Scan or search</li><li>Adjust qty &amp; discount</li><li>Take payment</li></ol>';
	echo '</div>';
	echo '</div>';
	echo '</div>';
	echo '</section>';

	/* Right: cart + pay */
	echo '<section class="epc-pos-panel epc-pos-panel--cart" aria-label="Cart and payment">';
	echo '<div class="epc-pos-panel-head">';
	echo '<div><i class="fa fa-shopping-basket"></i> Cart &amp; checkout</div>';
	echo '<span class="epc-pos-panel-head__hint" id="epc-pos-cart-count">0 lines</span>';
	echo '</div>';
	echo '<div class="epc-pos-panel-body epc-pos-panel-body--cart">';

	/* Customer */
	echo '<div class="epc-pos-block epc-pos-customer">';
	echo '<div class="epc-pos-block__label"><i class="fa fa-user"></i> Customer</div>';
	echo '<div class="epc-pos-customer__row">';
	echo '<input type="text" id="epc-pos-customer-q" placeholder="Search name, email, phone…" autocomplete="off" aria-label="Customer search">';
	echo '<button type="button" class="epc-pos-btn epc-pos-btn-muted" id="epc-pos-customer-walkin" title="Reset to walk-in"><i class="fa fa-walking"></i> Walk-in</button>';
	echo '</div>';
	echo '<div id="epc-pos-customer-pick" class="epc-pos-customer-pick" style="display:none"></div>';
	echo '<input type="hidden" id="epc-pos-customer-user" value="0">';
	echo '<input type="hidden" id="epc-pos-customer-contact" value="0">';
	echo '<div id="epc-pos-customer-label" class="epc-pos-customer__selected"><i class="fa fa-check-circle"></i> Walk-in guest</div>';
	echo '</div>';

	/* Cart lines */
	echo '<div class="epc-pos-block epc-pos-block--grow">';
	echo '<div class="epc-pos-block__label"><i class="fa fa-list"></i> Line items <span class="epc-pos-block__hint">qty · price · % off · amount off</span></div>';
	echo '<div id="epc-pos-cart" class="epc-pos-cart-lines">';
	echo '<div class="epc-pos-empty epc-pos-empty--compact">Cart is empty — add products from search</div>';
	echo '</div>';
	echo '</div>';

	/* Totals */
	echo '<div class="epc-pos-block epc-pos-totals" aria-label="Totals">';
	echo '<div class="epc-pos-total-row"><span>Subtotal (ex ' . $taxLabel . ')</span><span><span class="epc-pos-cur">' . $cur . '</span> <span id="epc-pos-subtotal">0.00</span></span></div>';
	echo '<div class="epc-pos-total-row"><span>Discount</span><span><span class="epc-pos-cur">' . $cur . '</span> <span id="epc-pos-discount">0.00</span></span></div>';
	echo '<div class="epc-pos-total-row"><span>' . $taxLabel . ' (<span id="epc-pos-tax-rate-lbl">' . $taxRate . '</span>%)</span><span><span class="epc-pos-cur">' . $cur . '</span> <span id="epc-pos-vat">0.00</span></span></div>';
	echo '<div class="epc-pos-total-row grand"><span>Total due</span><span><span class="epc-pos-cur">' . $cur . '</span> <span id="epc-pos-total">0.00</span></span></div>';
	echo '</div>';

	/* Payment */
	echo '<div class="epc-pos-block epc-pos-pay">';
	echo '<div class="epc-pos-block__label"><i class="fa fa-credit-card"></i> Payment method</div>';
	echo '<div class="epc-pos-pay-btns" id="epc-pos-pay-mode" role="group" aria-label="Payment method">';
	echo '<button type="button" class="epc-pos-btn epc-pos-btn-muted active" data-pay="cash"><i class="fa fa-money-bill"></i> Cash</button>';
	echo '<button type="button" class="epc-pos-btn epc-pos-btn-muted" data-pay="card"><i class="fa fa-credit-card"></i> Card</button>';
	echo '<button type="button" class="epc-pos-btn epc-pos-btn-muted" data-pay="split"><i class="fa fa-divide"></i> Split</button>';
	echo '</div>';

	echo '<div id="epc-pos-cash-fields" class="epc-pos-pay-fields">';
	echo '<div class="epc-pos-field">';
	echo '<label for="epc-pos-tendered">Cash tendered (' . $cur . ')</label>';
	echo '<input type="number" step="0.01" min="0" id="epc-pos-tendered" placeholder="0.00" inputmode="decimal">';
	echo '</div>';
	echo '<div class="epc-pos-change" id="epc-pos-change-wrap">Change <strong id="epc-pos-change">' . $cur . ' 0.00</strong></div>';
	echo '</div>';

	echo '<div id="epc-pos-split-fields" class="epc-pos-pay-fields" style="display:none">';
	echo '<div class="epc-pos-field">';
	echo '<label for="epc-pos-cash-amt">Cash amount (' . $cur . ')</label>';
	echo '<input type="number" step="0.01" min="0" id="epc-pos-cash-amt" placeholder="0.00" inputmode="decimal">';
	echo '</div>';
	echo '<div class="epc-pos-field">';
	echo '<label for="epc-pos-card-amt">Card amount (' . $cur . ')</label>';
	echo '<input type="number" step="0.01" min="0" id="epc-pos-card-amt" placeholder="0.00" inputmode="decimal">';
	echo '</div>';
	echo '</div>';
	echo '</div>';

	/* Notes */
	echo '<div class="epc-pos-block">';
	echo '<div class="epc-pos-field">';
	echo '<label for="epc-pos-notes">Sale notes <span class="epc-pos-block__hint">(optional · saved on ERP order)</span></label>';
	echo '<input type="text" id="epc-pos-notes" maxlength="240" placeholder="e.g. counter pickup, invoice reference…">';
	echo '</div>';
	echo '</div>';

	/* Actions */
	echo '<div class="epc-pos-actions">';
	echo '<button type="button" class="epc-pos-btn epc-pos-btn-success epc-pos-btn-lg" id="epc-pos-checkout" disabled>';
	echo '<i class="fa fa-check-circle"></i> Complete sale';
	echo '</button>';
	echo '<button type="button" class="epc-pos-btn epc-pos-btn-muted epc-pos-btn-lg" id="epc-pos-clear">';
	echo '<i class="fa fa-trash"></i> Clear cart';
	echo '</button>';
	echo '</div>';
	echo '<p class="epc-pos-footnote">Checkout creates ERP sales order → invoice → receipt voucher and stock out from the selected warehouse.</p>';

	echo '</div>';
	echo '</section>';
	echo '</div>';
	echo '</div>';
}
