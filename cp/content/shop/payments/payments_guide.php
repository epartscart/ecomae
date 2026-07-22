<?php
/**
 * CP — Payment gateways step-by-step guide.
 */
defined('_ASTEXE_') or die('No access');

require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/payments/epc_payment_helpers.php';

$paymentsUrl = '/' . $DP_Config->backend_dir . '/shop/payments/payments';
$configureUrl = $paymentsUrl . '?tab=configure';
$domain = rtrim($DP_Config->domain_path, '/');
$host = parse_url($domain, PHP_URL_HOST);
if (!$host && function_exists('epc_portal_host')) {
	$host = epc_portal_host();
}
if (!$host) {
	$host = 'your domain';
}
?>
<link rel="stylesheet" href="/content/shop/finance/epc_erp_ui.css?v=20260722pay">

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
				GCC, Pakistan, international, and <strong>crypto (NOWPayments)</strong> gateways ship with demo credentials.
				Customers pick a method on the order page. Crypto supports live API keys; other acquirers use demo checkout until live redirect is wired.
			</div>

			<h4>Quick start</h4>
			<ol>
				<li>Open <a href="<?php echo epc_payment_h($paymentsUrl); ?>">Payment gateways</a> → <em>Seed / refresh gateways</em>.</li>
				<li>Set a default (e.g. Stripe or Telr) and keep Crypto / JazzCash / Tabby enabled for the customer picker.</li>
				<li><strong>Individual accounts:</strong> open <a href="<?php echo epc_payment_h($paymentsUrl . '?tab=accounts'); ?>">Individual accounts</a> and attach merchant keys / connected account ID / payout IBAN to each office or vendor.</li>
				<li>On the storefront order page, choose <em>Pay with</em> → Card / BNPL / JazzCash / Crypto. Funds are attributed to that order’s office/vendor account.</li>
				<li>For crypto live: Configure → Crypto (NOWPayments) → paste API key + IPN secret → turn off Demo mode.</li>
			</ol>

			<h4>Individual accounts (who receives the money)</h4>
			<ul>
				<li><strong>Direct</strong> — office/vendor merchant credentials are used for the charge.</li>
				<li><strong>Connected</strong> — store connected account ID (e.g. Stripe Connect <code>acct_…</code>).</li>
				<li><strong>Payout</strong> — platform collects; settlement ledger shows net due to IBAN for manual/batch payout.</li>
				<li>Multi-vendor orders create settlement rows per vendor storage share.</li>
			</ul>

			<h4>Checkout flow on <?php echo htmlspecialchars($host, ENT_QUOTES, 'UTF-8'); ?></h4>
			<ol>
				<li>Customer selects a payment method and clicks pay.</li>
				<li><code>ajax_create_operation.php</code> creates a pending <code>shop_users_accounting</code> row (optional <code>pay_handler</code>).</li>
				<li>Browser opens <code>/content/shop/finance/payment_systems/{handler}/go_to_pay.php</code>.</li>
				<li>Webhook / IPN hits <code>notification.php</code> → <code>pay_for_order.php</code> marks the order paid.</li>
			</ol>

			<h4>GCC &amp; MENA</h4>
			<table class="table table-bordered table-condensed">
				<thead><tr><th>Gateway</th><th>Handler</th><th>Markets</th></tr></thead>
				<tbody>
				<tr><td>Telr</td><td><code>telr</code></td><td>UAE + GCC cards / Apple Pay</td></tr>
				<tr><td>PayTabs</td><td><code>paytabs</code></td><td>MENA cards &amp; wallets</td></tr>
				<tr><td>Tabby</td><td><code>tabby</code></td><td>BNPL AE/SA/KW/BH</td></tr>
				<tr><td>Tamara</td><td><code>tamara</code></td><td>BNPL AE/SA/KW</td></tr>
				<tr><td>MyFatoorah</td><td><code>myfatoorah</code></td><td>KNET, MADA, GCC</td></tr>
				<tr><td>Tap Payments</td><td><code>tap</code></td><td>GCC cards &amp; wallets</td></tr>
				<tr><td>HyperPay</td><td><code>hyperpay</code></td><td>KSA MADA / UAE</td></tr>
				<tr><td>Checkout.com</td><td><code>checkout_com</code></td><td>UAE enterprise</td></tr>
				<tr><td>Network International</td><td><code>network_intl</code></td><td>N-Genius UAE</td></tr>
				<tr><td>Amazon Payment Services</td><td><code>amazon_ps</code></td><td>AE/SA</td></tr>
				</tbody>
			</table>

			<h4>Pakistan</h4>
			<table class="table table-bordered table-condensed">
				<thead><tr><th>Gateway</th><th>Handler</th><th>Notes</th></tr></thead>
				<tbody>
				<tr><td>JazzCash</td><td><code>jazzcash</code></td><td>Mobile wallet &amp; cards (PKR)</td></tr>
				<tr><td>Easypaisa</td><td><code>easypaisa</code></td><td>Wallet / OTC (PKR)</td></tr>
				</tbody>
			</table>

			<h4>Cryptocurrency</h4>
			<p><strong>Crypto (NOWPayments)</strong> — handler <code>nowpayments</code>.</p>
			<ul>
				<li>Demo: coin picker + simulated invoice + confirm button.</li>
				<li>Live: set API key + IPN secret, disable Demo mode. IPN URL:
					<code><?php echo epc_payment_h($domain); ?>/content/shop/finance/payment_systems/nowpayments/notification.php</code>
				</li>
				<li>Supported coins (configurable): USDT TRC20/BEP20, BTC, ETH, LTC.</li>
			</ul>

			<h4>International</h4>
			<p>Stripe, PayPal, Adyen, 2Checkout, Razorpay, Skrill, Payoneer, Authorize.net, CyberSource, CCAvenue — same hub.</p>

			<h4>Legacy CIS</h4>
			<p>Tinkoff, YooKassa, Robokassa, etc. — <a href="<?php echo epc_payment_h($paymentsUrl . '?tab=legacy'); ?>">Legacy tab</a>.</p>

			<h4>Going live checklist</h4>
			<ul>
				<li>Trade license + merchant account with the acquirer</li>
				<li>HTTPS on <strong><?php echo htmlspecialchars($host, ENT_QUOTES, 'UTF-8'); ?></strong></li>
				<li>Paste live keys; disable Demo mode</li>
				<li>Register webhook / IPN URL</li>
				<li>For crypto: fund NOWPayments payout wallet and verify IPN secret</li>
				<li>Run a small live test payment</li>
			</ul>

			<h4>Setup script</h4>
			<pre>epc-payments-setup.php?token=epartscart-deploy-2026&amp;reseed=1</pre>
		</div>
	</div>
</div>
