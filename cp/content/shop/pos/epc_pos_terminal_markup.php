<?php
/**
 * POS Terminal HTML — eval-safe (no ?> before markup; CP template uses eval()).
 */
defined('_ASTEXE_') or die('No access');

function epc_pos_terminal_render_markup(
	array $stats,
	$openSession,
	array $taxCtx,
	array $settings,
	string $ajaxUrl,
	string $posUrl,
	string $csrf,
	string $erpUrl
): void {
	$sessionId = (int) ($openSession ? (int) $openSession['id'] : 0);
	$sessionOpen = $openSession ? '1' : '0';
	$registerName = epc_pos_h($settings['register_name'] ?? 'Register 1');
	$todaySales = (int) $stats['today_sales'];
	$todayTotal = number_format((float) $stats['today_total'], 2);
	$taxRate = number_format((float) $taxCtx['tax_rate'], 1);
	$taxLabel = epc_pos_h($taxCtx['tax_label']);

	echo '<div class="col-lg-12 epc-pos-wrap" id="epc-pos-app"'
		. ' data-ajax-url="' . epc_pos_h($ajaxUrl) . '"'
		. ' data-pos-url="' . epc_pos_h($posUrl) . '"'
		. ' data-csrf="' . epc_pos_h($csrf) . '"'
		. ' data-session-id="' . $sessionId . '"'
		. ' data-session-open="' . $sessionOpen . '">';

	echo '<div class="epc-pos-hero">';
	echo '<h3><i class="fa fa-cash-register"></i> POS Terminal</h3>';
	echo '<p class="sub">Scan or search products · apply discounts · checkout with cash, card, or split · ERP invoice &amp; receipt</p>';
	echo '</div>';

	echo '<div class="epc-pos-kpi">';
	echo '<div class="k"><div class="l">Today</div><div class="v">' . $todaySales . '</div></div>';
	echo '<div class="k"><div class="l">Today total</div><div class="v">' . $todayTotal . '</div></div>';
	echo '<div class="k"><div class="l">Tax rate</div><div class="v">' . $taxRate . '%</div></div>';
	echo '<div class="k"><div class="l">Register</div><div class="v" style="font-size:14px">' . $registerName . '</div></div>';
	echo '</div>';

	echo '<div id="epc-pos-msg" class="epc-pos-msg"></div>';

	echo '<div class="epc-pos-session-bar">';
	if ($openSession) {
		echo '<span class="epc-pos-badge epc-pos-badge-open"><i class="fa fa-circle"></i> Open: '
			. epc_pos_h($openSession['session_no']) . '</span>';
		echo '<span class="text-muted">Float ' . number_format((float) $openSession['opening_float'], 2) . '</span>';
		echo '<button type="button" class="epc-pos-btn epc-pos-btn-muted" id="epc-pos-close-session"><i class="fa fa-lock"></i> Close shift</button>';
	} else {
		echo '<span class="epc-pos-badge epc-pos-badge-closed">No open shift</span>';
		echo '<button type="button" class="epc-pos-btn epc-pos-btn-primary" id="epc-pos-open-session"><i class="fa fa-unlock"></i> Open register</button>';
	}
	echo '<a class="epc-pos-btn epc-pos-btn-muted" href="' . epc_pos_h($erpUrl) . '" style="text-decoration:none;margin-left:auto"><i class="fa fa-file-invoice"></i> ERP sales</a>';
	echo '</div>';

	echo '<div class="epc-pos-grid">';
	echo '<div class="epc-pos-panel">';
	echo '<div class="epc-pos-panel-head"><i class="fa fa-search"></i> Product search</div>';
	echo '<div class="epc-pos-panel-body">';
	echo '<div class="epc-pos-search">';
	echo '<input type="search" id="epc-pos-q" placeholder="SKU, barcode, name…" autocomplete="off" autofocus>';
	echo '<button type="button" class="epc-pos-btn epc-pos-btn-primary" id="epc-pos-search-btn"><i class="fa fa-search"></i></button>';
	echo '</div>';
	echo '<div id="epc-pos-products" class="epc-pos-products">';
	echo '<div class="epc-pos-empty">Type or scan to find products</div>';
	echo '</div>';
	echo '</div>';
	echo '</div>';

	echo '<div class="epc-pos-panel">';
	echo '<div class="epc-pos-panel-head"><i class="fa fa-shopping-basket"></i> Cart</div>';
	echo '<div class="epc-pos-panel-body" style="display:flex;flex-direction:column">';
	echo '<div class="epc-pos-customer">';
	echo '<input type="text" id="epc-pos-customer-q" placeholder="Customer search (optional) — leave blank for walk-in">';
	echo '<div id="epc-pos-customer-pick" style="margin-top:6px;display:none"></div>';
	echo '<input type="hidden" id="epc-pos-customer-user" value="0">';
	echo '<input type="hidden" id="epc-pos-customer-contact" value="0">';
	echo '<div id="epc-pos-customer-label" class="text-muted" style="font-size:12px;margin-top:4px">Walk-in guest</div>';
	echo '</div>';

	echo '<div id="epc-pos-cart" class="epc-pos-cart-lines">';
	echo '<div class="epc-pos-empty">Cart is empty</div>';
	echo '</div>';

	echo '<div class="epc-pos-totals">';
	echo '<div class="epc-pos-total-row"><span>Subtotal</span><span id="epc-pos-subtotal">0.00</span></div>';
	echo '<div class="epc-pos-total-row"><span>Discount</span><span id="epc-pos-discount">0.00</span></div>';
	echo '<div class="epc-pos-total-row"><span>' . $taxLabel . '</span><span id="epc-pos-vat">0.00</span></div>';
	echo '<div class="epc-pos-total-row grand"><span>Total</span><span id="epc-pos-total">0.00</span></div>';
	echo '</div>';

	echo '<div class="epc-pos-pay-btns" id="epc-pos-pay-mode">';
	echo '<button type="button" class="epc-pos-btn epc-pos-btn-muted active" data-pay="cash"><i class="fa fa-money-bill"></i> Cash</button>';
	echo '<button type="button" class="epc-pos-btn epc-pos-btn-muted" data-pay="card"><i class="fa fa-credit-card"></i> Card</button>';
	echo '<button type="button" class="epc-pos-btn epc-pos-btn-muted" data-pay="split"><i class="fa fa-divide"></i> Split</button>';
	echo '</div>';
	echo '<div id="epc-pos-split-fields" style="display:none;margin-bottom:8px">';
	echo '<div style="display:flex;gap:8px">';
	echo '<input type="number" step="0.01" id="epc-pos-cash-amt" placeholder="Cash" style="flex:1;padding:10px;border-radius:8px;border:1px solid #e2e8f0">';
	echo '<input type="number" step="0.01" id="epc-pos-card-amt" placeholder="Card" style="flex:1;padding:10px;border-radius:8px;border:1px solid #e2e8f0">';
	echo '</div>';
	echo '</div>';

	echo '<button type="button" class="epc-pos-btn epc-pos-btn-success epc-pos-btn-lg" id="epc-pos-checkout" disabled>';
	echo '<i class="fa fa-check-circle"></i> Complete sale';
	echo '</button>';
	echo '<button type="button" class="epc-pos-btn epc-pos-btn-muted epc-pos-btn-lg" id="epc-pos-clear" style="margin-top:8px">';
	echo '<i class="fa fa-trash"></i> Clear cart';
	echo '</button>';
	echo '</div>';
	echo '</div>';
	echo '</div>';
	echo '</div>';
}
