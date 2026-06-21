<?php
/**
 * CP — WhatsApp sharing guide (Phase 1: wa.me prefilled messages).
 */
defined('_ASTEXE_') or die('No access');

require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_whatsapp_share.php';

$backend = '/' . $DP_Config->backend_dir;
$guideUrl = $backend . '/shop/orders/whatsapp-guide';
$ordersUrl = $backend . '/shop/orders/orders';
$fulfilmentUrl = $backend . '/shop/orders/guide';
$configUrl = $backend . '/control/config';
$storagesUrl = $backend . '/shop/logistics/storages';
$salesDisplay = epc_wa_sales_display($DP_Config);
$domain = rtrim((string)$DP_Config->domain_path, '/');

function epc_wa_guide_h($v): string
{
	return htmlspecialchars((string)$v, ENT_QUOTES, 'UTF-8');
}
?>
<link rel="stylesheet" href="/content/shop/finance/epc_erp_ui.css?v=20260525">

<div class="col-lg-12 epc-erp-shell">
	<div class="hpanel">
		<div class="panel-heading hbuilt">
			<i class="fa fa-whatsapp"></i> WhatsApp sharing — guide
			<span class="pull-right">
				<a class="btn btn-primary btn-xs" href="<?php echo epc_wa_guide_h($ordersUrl); ?>"><i class="fa fa-shopping-cart"></i> Orders</a>
				<a class="btn btn-default btn-xs" href="<?php echo epc_wa_guide_h($fulfilmentUrl); ?>"><i class="fa fa-book"></i> Order fulfilment guide</a>
			</span>
		</div>
		<div class="panel-body epc-erp-flow">

			<div class="alert alert-success">
				<strong>Phase 1 is live.</strong> Share buttons open WhatsApp (<code>wa.me</code>) with bilingual <strong>English + Arabic</strong> prefilled text.
				No WhatsApp Business API yet — staff send messages manually from their phone or desktop WhatsApp.
			</div>

			<h4>Default sales number</h4>
			<p>
				Storefront header, parts search, cart, and “Share with sales” on orders use:
				<strong><?php echo epc_wa_guide_h($salesDisplay); ?></strong>
			</p>
			<p>Change it in <a href="<?php echo epc_wa_guide_h($configUrl); ?>">Configuration</a> → <code>epc_whatsapp_number</code> (Frontend WhatsApp number).</p>

			<h4>Who shares what</h4>
			<table class="table table-bordered table-condensed">
				<thead>
					<tr><th>Actor</th><th>Where</th><th>Recipient</th><th>Message</th></tr>
				</thead>
				<tbody>
					<tr>
						<td><strong>Customer</strong></td>
						<td>Part search results, cart page, site header</td>
						<td>Sales WhatsApp</td>
						<td>Quote request (part) or cart summary</td>
					</tr>
					<tr>
						<td><strong>Staff</strong></td>
						<td>CP → open order → WhatsApp share panel</td>
						<td>Customer phone (if on order/profile)</td>
						<td>Order summary + line items</td>
					</tr>
					<tr>
						<td><strong>Staff</strong></td>
						<td>Same panel → Share with sales</td>
						<td>Sales WhatsApp</td>
						<td>Order summary + CP link</td>
					</tr>
					<tr>
						<td><strong>Staff</strong></td>
						<td>Same panel → Supplier LPO buttons</td>
						<td>Supplier <code>contact_phone</code> in ERP, or sales if missing</td>
						<td>LPO text grouped by warehouse</td>
					</tr>
				</tbody>
			</table>

			<h4>Staff workflow (order card)</h4>
			<ol>
				<li>Open <a href="<?php echo epc_wa_guide_h($ordersUrl); ?>">Orders</a> → click an order.</li>
				<li>Scroll to the green <strong>WhatsApp share</strong> panel.</li>
				<li><em>Message customer</em> — only shown when the order or user profile has a phone number.</li>
				<li><em>Share with sales</em> — internal handoff with order lines and CP URL.</li>
				<li><em>LPO: [warehouse]</em> — one button per warehouse on the order; uses supplier phone from
					<a href="<?php echo epc_wa_guide_h($storagesUrl); ?>">Warehouses / ERP suppliers</a> when set.</li>
			</ol>

			<h4>Storefront (customer → sales)</h4>
			<ul>
				<li><strong>Header</strong> — “WhatsApp chat” opens sales line (no prefilled text).</li>
				<li><strong>Part search</strong> — green WhatsApp button on each product row → quote request for that part.</li>
				<li><strong>Cart</strong> — “Share cart on WhatsApp” sends up to 15 lines + estimated total.</li>
			</ul>
			<p>Example storefront cart: <code><?php echo epc_wa_guide_h($domain); ?>/shop/cart</code></p>

			<h4>Language</h4>
			<p>All prefilled share text is <strong>bilingual EN then AR</strong> in one message (customer can reply in either language).</p>

			<h4>Supplier phone for LPO shares</h4>
			<ol>
				<li>ERP → link supplier to warehouse (<code>epc_erp_suppliers.storage_id</code>).</li>
				<li>Set <code>contact_phone</code> on that supplier (mobile with country code, e.g. <code>971501234567</code>).</li>
				<li>If empty, the LPO button still works but targets <strong>sales</strong> so staff can forward manually.</li>
			</ol>

			<h4>Phase 2 — automated notifications (Cloud API)</h4>
			<div class="alert alert-success" style="margin-bottom:12px;">
				<strong>Phase 2 is installed.</strong> When you add Meta WhatsApp Cloud API credentials and set
				<code>epc_whatsapp_api_enabled = 1</code>, order and status e-mails also trigger WhatsApp messages
				via <code>send_notify_dispatch.php</code> (same events as e-mail/SMS).
			</div>
			<ol>
				<li>Meta Business → WhatsApp → API setup → copy <strong>phone_number_id</strong> and <strong>permanent token</strong>.</li>
				<li><a href="<?php echo epc_wa_guide_h($configUrl); ?>">Configuration</a> → set token, phone_number_id, enable API (1).</li>
				<li>Customer must have a <strong>phone on profile</strong> (or guest order phone) — same as SMS path.</li>
				<li>Messages use SMS template text when set, else plain text from e-mail body — bilingual EN+AR when enabled.</li>
				<li>Log table: <code>epc_whatsapp_notify_log</code> (success/fail per send).</li>
			</ol>
			<p>Setup script: <code>whatsapp-phase2-setup.php?token=epartscart-deploy-2026</code></p>
			<p>Test send: add <code>&amp;test=1&amp;phone=971567607011</code> (requires token + API enabled in config).</p>

			<h4>Phase 1 — manual share (still active)</h4>

			<h4>Technical files</h4>
			<table class="table table-condensed table-bordered">
				<thead><tr><th>File</th><th>Role</th></tr></thead>
				<tbody>
					<tr><td><code>content/general_pages/epc_whatsapp_share.php</code></td><td>Phase 1 message builders, styles, storefront JS</td></tr>
					<tr><td><code>content/notifications/epc_whatsapp_notify.php</code></td><td>Phase 2 Cloud API sender + log</td></tr>
					<tr><td><code>content/notifications/send_notify_dispatch.php</code></td><td>Hooks WA into notify pipeline</td></tr>
					<tr><td><code>cp/content/shop/order_process/epc_order_whatsapp_share.php</code></td><td>CP order card panel</td></tr>
					<tr><td><code>content/shop/docpart/part_search_page.php</code></td><td>Product row WhatsApp button</td></tr>
					<tr><td><code>content/shop/order_process/cart.php</code></td><td>Share cart button</td></tr>
					<tr><td><code>templates/nero/desktop.php</code></td><td>Header WhatsApp link</td></tr>
				</tbody>
			</table>

			<h4>Setup after deploy</h4>
			<pre>epc-whatsapp-setup.php?token=epartscart-deploy-2026</pre>
			<p>Registers this CP guide URL and ensures contact settings include <code>epc_whatsapp_number</code>.</p>
			<p>Contact numbers only: <code>epc-contact-settings-setup.php?token=epartscart-deploy-2026</code></p>

			<h4>FAQ</h4>
			<dl>
				<dt>“Message customer” is missing</dt>
				<dd>Add phone on the order (guest checkout) or in the customer profile in User manager.</dd>
				<dt>Wrong number opens</dt>
				<dd>Update <code>epc_whatsapp_number</code> in Configuration; hard-refresh the storefront.</dd>
				<dt>CP page shows login</dt>
				<dd>Log in at <code><?php echo epc_wa_guide_h($backend); ?>/</code> first — direct URLs without session always show the login form.</dd>
				<dt>Does this replace e-mail LPO?</dt>
				<dd>No. E-mail LPO from order fulfilment still runs; WhatsApp is an extra manual channel for speed.</dd>
			</dl>

			<p class="text-muted" style="font-size:12px;margin-top:16px;">
				Last updated: <?php echo epc_wa_guide_h(date('Y-m-d')); ?> ·
				<a href="<?php echo epc_wa_guide_h($guideUrl); ?>">WhatsApp guide</a> ·
				<a href="<?php echo epc_wa_guide_h($fulfilmentUrl); ?>">Order fulfilment guide</a>
			</p>
		</div>
	</div>
</div>
