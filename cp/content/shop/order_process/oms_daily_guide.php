<?php
/**
 * OMS daily order management — step-by-step tenant guide (area by area).
 */
defined('_ASTEXE_') or die('No access');

$backend = '/' . trim((string) $DP_Config->backend_dir, '/');
$omsUrl = $backend . '/shop/orders/orders';
$guideUrl = $backend . '/shop/orders/oms-guide';
$fulfilmentUrl = $backend . '/shop/orders/guide';
$waUrl = $backend . '/shop/orders/whatsapp-guide';
$pricesUrl = $backend . '/shop/prices';
$h = static function ($v) {
	return htmlspecialchars((string) $v, ENT_QUOTES, 'UTF-8');
};
?>
<div class="col-lg-12 epc-oms-guide-page">
	<div class="epc-oms-guide-hero">
		<div>
			<p class="epc-oms-guide-kicker">Shop · Order Management System</p>
			<h1>OMS daily guide</h1>
			<p>One screen for daily order work. Use this checklist area by area — from opening the day to completing and messaging the customer.</p>
		</div>
		<div class="epc-oms-guide-hero-actions">
			<a class="btn btn-primary" href="<?php echo $h($omsUrl); ?>"><i class="fa fa-columns"></i> Open OMS</a>
			<a class="btn btn-default" href="<?php echo $h($fulfilmentUrl); ?>"><i class="fa fa-truck"></i> Fulfilment deep-dive</a>
			<a class="btn btn-default" href="<?php echo $h($waUrl); ?>"><i class="fa fa-whatsapp"></i> WhatsApp guide</a>
		</div>
	</div>

	<div class="epc-oms-guide-toc">
		<strong>Jump to area</strong>
		<ol>
			<li><a href="#oms-area-open">Open the day</a></li>
			<li><a href="#oms-area-list">Order list &amp; filters</a></li>
			<li><a href="#oms-area-console">OMS console (right pane)</a></li>
			<li><a href="#oms-area-items">Items &amp; stock</a></li>
			<li><a href="#oms-area-pay">Payment</a></li>
			<li><a href="#oms-area-docs">Documents &amp; print</a></li>
			<li><a href="#oms-area-status">Status &amp; timeline</a></li>
			<li><a href="#oms-area-msg">Customer messages / WhatsApp</a></li>
			<li><a href="#oms-area-complete">Complete or cancel</a></li>
			<li><a href="#oms-area-bulk">Bulk actions</a></li>
		</ol>
	</div>

	<section class="epc-oms-guide-section" id="oms-area-open">
		<h2><span class="epc-oms-step">1</span> Open the day</h2>
		<p>Start every shift from <strong>SHOP → OMS · Orders</strong> (not separate “items” or “statuses” pages).</p>
		<ol>
			<li>Go to <a href="<?php echo $h($omsUrl); ?>"><?php echo $h($omsUrl); ?></a>.</li>
			<li>Check the KPI cards: <strong>Open orders</strong>, <strong>Today</strong>, <strong>Pending ship</strong>.</li>
			<li>Use the <strong>Open</strong> tab (default) so unfinished work is listed first. Use <strong>Completed</strong> only when you need history.</li>
			<li>Sort by <strong>By last modified</strong> if you want the most recently touched orders on top.</li>
		</ol>
		<div class="epc-oms-guide-tip"><i class="fa fa-lightbulb-o"></i> If the list looks empty but KPIs show open orders, click <strong>Open</strong> again or clear sticky filters in Advanced filter.</div>
	</section>

	<section class="epc-oms-guide-section" id="oms-area-list">
		<h2><span class="epc-oms-step">2</span> Order list &amp; filters</h2>
		<p>The left (or full-width) table is your work queue.</p>
		<ol>
			<li><strong>Status pills</strong> — quick filter (Placed, Executing, Sent, etc.). Completed is its own tab, not mixed into Open.</li>
			<li><strong>Advanced filter</strong> — expand when you need date range, order #, customer, phone, article, paid flag, shop.</li>
			<li><strong>Click a row</strong> — opens that order in the OMS console on the right (same page).</li>
			<li><strong>Ctrl+click</strong> — classic full order card (rare; prefer OMS).</li>
			<li>Unread mail icon on a row means the customer sent a message — open that order first.</li>
		</ol>
	</section>

	<section class="epc-oms-guide-section" id="oms-area-console">
		<h2><span class="epc-oms-step">3</span> OMS console (right pane)</h2>
		<p>This is the daily workspace. You should not need separate “order items” or “order status” menu pages for routine work.</p>
		<ol>
			<li>Header shows order #, status badge, created/modified times, shop, delivery mode.</li>
			<li>Chips show paid state, payment method, item count.</li>
			<li>Totals strip: Amount · Paid · Balance due · Purchase · Benefit.</li>
			<li>Tabs inside the console:
				<ul>
					<li><strong>Manage</strong> — order status, notes, ERP map · shortcut <kbd>1</kbd></li>
					<li><strong>Items</strong> — editable lines, live margin, save all · <kbd>2</kbd></li>
					<li><strong>Fulfillment</strong> — per-supplier pipeline · <kbd>3</kbd></li>
					<li><strong>Customer</strong> — contact &amp; account · <kbd>4</kbd></li>
					<li><strong>Payment</strong> — pay / refund / courier fee + UAE VAT · <kbd>5</kbd></li>
					<li><strong>Documents</strong> — tax invoice, packing, receipts · <kbd>6</kbd></li>
					<li><strong>Timeline</strong> — status log · <kbd>7</kbd></li>
					<li><strong>Messages</strong> — chat + WhatsApp share · <kbd>8</kbd></li>
				</ul>
			</li>
			<li>Stay on the same tab after saves — OMS remembers your active tab.</li>
			<li>Queue keys: <kbd>j</kbd>/<kbd>k</kbd> (or ↑/↓) move between orders without leaving the console.</li>
		</ol>
	</section>

	<section class="epc-oms-guide-section" id="oms-area-items">
		<h2><span class="epc-oms-step">4</span> Items &amp; stock</h2>
		<ol>
			<li>Open the <strong>Items</strong> tab for the selected order.</li>
			<li>Confirm brand, article, qty, sale price, and purchase cost — margin &amp; USD update live as you type.</li>
			<li>Use <strong>Save all lines</strong> or <kbd>Ctrl</kbd>+<kbd>S</kbd> (saves focused line, or all when focus is not on a line).</li>
			<li>Use <strong>Refresh</strong> on a line to pull real purchase cost from warehouse / APAI details.</li>
			<li><strong>Set all line statuses</strong> when every line moves together (e.g. all received).</li>
			<li>Open <strong>Fulfillment</strong> for multi-supplier confirm → pay → ship → warehouse → pack → deliver.</li>
			<li>If the order is unpaid, you can edit lines and add a line; after payment, price edits lock.</li>
		</ol>
		<div class="epc-oms-guide-tip"><i class="fa fa-info-circle"></i> Price lists &amp; warehouses are managed under <a href="<?php echo $h($pricesUrl); ?>">Price lists</a> — keep stock/prices correct so OMS lines stay accurate.</div>
	</section>

	<section class="epc-oms-guide-section" id="oms-area-pay">
		<h2><span class="epc-oms-step">5</span> Payment</h2>
		<ol>
			<li>Open the <strong>Payment</strong> tab (shortcut <kbd>5</kbd>) — Amount due includes goods + customer-paid courier.</li>
			<li>Set <strong>Courier fee</strong> (ex-VAT) and ship country: UAE adds VAT on courier; outside UAE is zero-rated.</li>
			<li>Record payment (cash / card / transfer / wallet) for the balance due.</li>
			<li>Confirm paid badge updates (Paid / Partial / Not paid).</li>
			<li>Do not mark the order <strong>Completed</strong> until payment rules for your shop are satisfied (usually fully paid).</li>
			<li>Refunds: use refund actions only when reversing a recorded payment.</li>
		</ol>
		<div class="epc-oms-guide-tip"><i class="fa fa-truck"></i> KPI <strong>Pending ship</strong> = paid but not finished — click it to jump straight into that queue.</div>
	</section>

	<section class="epc-oms-guide-section" id="oms-area-docs">
		<h2><span class="epc-oms-step">6</span> Documents &amp; print</h2>
		<ol>
			<li>Open the <strong>Documents</strong> tab.</li>
			<li>Print or download invoice / packing / tax docs as configured for your tenant.</li>
			<li>Select specific lines when the print dialog asks for order items.</li>
			<li>Share documents with the customer via Messages or WhatsApp (see area 8).</li>
		</ol>
	</section>

	<section class="epc-oms-guide-section" id="oms-area-status">
		<h2><span class="epc-oms-step">7</span> Status &amp; timeline</h2>
		<ol>
			<li>On <strong>Manage</strong>, choose the next order status (e.g. Executing → Sent to client).</li>
			<li>Apply status — the badge and timeline update immediately.</li>
			<li>Use <strong>Timeline</strong> to see who changed what (staff / robot / customer).</li>
			<li>Add an internal note when something needs a handoff to the next shift.</li>
		</ol>
		<div class="epc-oms-guide-tip"><i class="fa fa-warning"></i> Status names are configured once by an admin. Daily staff only <em>apply</em> them from OMS — you do not need a separate “Orders statuses” menu for routine work.</div>
	</section>

	<section class="epc-oms-guide-section" id="oms-area-msg">
		<h2><span class="epc-oms-step">8</span> Customer messages / WhatsApp</h2>
		<ol>
			<li>Open <strong>Messages</strong> (shortcut <kbd>8</kbd>) — WhatsApp share buttons are at the top of this tab (customer, sales, supplier LPO).</li>
			<li>Use in-app chat for order-wide notes; use the envelope on a line for item-specific messages.</li>
			<li>Reply clearly: ETA, payment request, pickup ready, or delivery update.</li>
			<li>Full WhatsApp templates (EN/AR): <a href="<?php echo $h($waUrl); ?>">WhatsApp guide</a>.</li>
		</ol>
	</section>

	<section class="epc-oms-guide-section" id="oms-area-complete">
		<h2><span class="epc-oms-step">9</span> Complete or cancel</h2>
		<ol>
			<li><strong>Complete</strong> — when goods are delivered/picked up and payment is settled. Move order to a finished status (Completed tab).</li>
			<li><strong>Cancel</strong> — only when the sale will not proceed; confirm stock release and any refund.</li>
			<li>After complete, the order leaves the Open tab — find it under <strong>Completed</strong> or <strong>All</strong>.</li>
		</ol>
	</section>

	<section class="epc-oms-guide-section" id="oms-area-bulk">
		<h2><span class="epc-oms-step">10</span> Bulk actions (end of list)</h2>
		<ol>
			<li>Tick several rows in the order list.</li>
			<li>Set status / viewed flag for the selection, or delete only when you are sure.</li>
			<li>Prefer single-order OMS for payment and messages; use bulk for status sweeps.</li>
		</ol>
	</section>

	<section class="epc-oms-guide-section epc-oms-guide-section--checklist">
		<h2><i class="fa fa-check-square-o"></i> End-of-day checklist</h2>
		<ul class="epc-oms-checklist">
			<li>Open KPI is zero or every open order has a next action / note</li>
			<li>Pending ship (paid, not finished) reviewed</li>
			<li>Customer messages answered</li>
			<li>Payments recorded for today’s collections</li>
			<li>Completed today’s delivered/picked-up orders</li>
		</ul>
		<p class="text-muted" style="margin-top:12px;">
			Deep supplier LPO / notification setup: <a href="<?php echo $h($fulfilmentUrl); ?>">Order fulfilment guide</a>.
			Guide URL: <a href="<?php echo $h($guideUrl); ?>"><?php echo $h($guideUrl); ?></a>
		</p>
	</section>
</div>
