<?php
/**
 * CP — Payment gateways step-by-step guide.
 */
defined('_ASTEXE_') or die('No access');

require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/payments/epc_payment_helpers.php';

$paymentsUrl = '/' . $DP_Config->backend_dir . '/shop/payments/payments';
$configureUrl = $paymentsUrl . '?tab=configure';
$domain = rtrim($DP_Config->domain_path, '/');
$handlers = array_keys(epc_payment_uae_gateway_defs());
?>
<link rel="stylesheet" href="/content/shop/finance/epc_erp_ui.css?v=20260525">

<div class="col-lg-12 epc-erp-shell">
	<div class="hpanel">
		<div class="panel-heading hbuilt">
			<i class="fa fa-book"></i> Payment gateways — guide
			<span class="pull-right">
				<a class="btn btn-primary btn-xs" href="<?php echo epc_payment_h($paymentsUrl); ?>"><i class="fa fa-credit-card"></i> Payments hub</a>
			</span>
		</div>
		<div class="panel-body epc-erp-flow">

			<div class="alert alert-success">
				All UAE gateways ship with <strong>dummy API keys</strong> and <strong>demo checkout</strong>. Customers see a simulated payment page — no real charge until you disable demo mode and add live credentials.
			</div>

			<h4>Quick start</h4>
			<ol>
				<li>Open <a href="<?php echo epc_payment_h($paymentsUrl); ?>">Payment gateways</a> → click <em>Activate Stripe (demo)</em> or any gateway.</li>
				<li>Place a test order on the storefront → pay online → complete demo checkout.</li>
				<li>When your merchant account is ready → <a href="<?php echo epc_payment_h($configureUrl); ?>">Configure</a> → paste live keys → turn off <em>Demo mode</em>.</li>
			</ol>

			<h4>Checkout flow on <?php echo htmlspecialchars(parse_url($domain, PHP_URL_HOST) ?: epc_portal_host(), ENT_QUOTES, 'UTF-8'); ?></h4>
			<ol>
				<li>Customer clicks pay on order or balance page.</li>
				<li><code>ajax_create_operation.php</code> creates a pending row in <code>shop_users_accounting</code>.</li>
				<li>Browser redirects to <code>/content/shop/finance/payment_systems/{handler}/go_to_pay.php</code>.</li>
				<li>Gateway webhook hits <code>notification.php</code> → order marked paid via <code>pay_for_order.php</code>.</li>
			</ol>

			<h4>UAE gateways (dummy data pre-filled)</h4>
			<table class="table table-bordered table-condensed">
				<thead><tr><th>Gateway</th><th>Handler folder</th><th>Typical use</th></tr></thead>
				<tbody>
				<tr><td>Stripe</td><td><code>stripe</code></td><td>Cards, Apple Pay, international</td></tr>
				<tr><td>Telr</td><td><code>telr</code></td><td>UAE aggregator — AED, SADAD</td></tr>
				<tr><td>PayTabs</td><td><code>paytabs</code></td><td>MENA — invoicing, QR</td></tr>
				<tr><td>PayPal</td><td><code>paypal</code></td><td>Export / guest checkout</td></tr>
				<tr><td>Amazon Payment Services</td><td><code>amazon_ps</code></td><td>Amazon ecosystem UAE</td></tr>
				<tr><td>Others</td><td><code>adyen</code>, <code>razorpay</code>, …</td><td>See dashboard list</td></tr>
				</tbody>
			</table>

			<h4>Legacy CIS gateways</h4>
			<p>Moved from Shop → Finance → Payment systems. Same handlers (<code>tinkoff</code>, <code>yookassa</code>, <code>robokassa</code>, etc.) — configure on the <a href="<?php echo epc_payment_h($paymentsUrl . '?tab=legacy'); ?>">Legacy tab</a>.</p>

			<h4>Webhook URLs (replace YOUR_DOMAIN)</h4>
			<pre><?php echo epc_payment_h($domain); ?>/content/shop/finance/payment_systems/HANDLER/notification.php</pre>
			<p>Example for Stripe: <code><?php echo epc_payment_h($domain); ?>/content/shop/finance/payment_systems/stripe/notification.php</code></p>

			<h4>Going live checklist</h4>
			<ul>
				<li>UAE trade license + business bank account</li>
				<li>Merchant account approved with chosen acquirer</li>
				<li>SSL (HTTPS) — required on <strong><?php echo htmlspecialchars(parse_url($domain, PHP_URL_HOST) ?: 'your domain', ENT_QUOTES, 'UTF-8'); ?></strong></li>
				<li>Replace dummy keys in Configure tab</li>
				<li>Disable <strong>Demo mode</strong> for that gateway</li>
				<li>Register webhook URL in acquirer dashboard</li>
				<li>Test small real transaction before full launch</li>
			</ul>

			<h4>Setup script (after deploy)</h4>
			<pre>epc-payments-setup.php?token=epartscart-deploy-2026</pre>
			<p>Re-seed dummy credentials: add <code>&amp;reseed=1</code></p>

			<h4>FAQ</h4>
			<dl>
				<dt>Can I enable multiple gateways at checkout?</dt>
				<dd>Currently one active gateway at a time. Multi-select checkout can be added later.</dd>
				<dt>Apple Pay?</dt>
				<dd>Enable via Stripe or Telr — not a separate handler.</dd>
				<dt>ERP / bank posting?</dt>
				<dd>Online payments mark orders paid automatically. ERP cash/bank receipt posting is separate (Finance tab).</dd>
			</dl>

			<p class="text-muted">Guide URL: <a href="<?php echo epc_payment_h('/' . $DP_Config->backend_dir . '/shop/payments/payments/guide'); ?>"><?php echo epc_payment_h('/' . $DP_Config->backend_dir . '/shop/payments/payments/guide'); ?></a></p>
		</div>
	</div>
</div>
