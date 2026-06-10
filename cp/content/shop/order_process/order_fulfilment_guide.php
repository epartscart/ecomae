<?php
/**
 * CP documentation: order fulfilment process, notifications, supplier LPO, staff workflow.
 */
defined('_ASTEXE_') or die('No access');

require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/usefull/epc_order_fulfilment_guide_data.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/usefull/epc_admin_notifications.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/usefull/epc_order_communication_test.php';

if (!isset($user_session) || !is_array($user_session)) {
	require_once $_SERVER['DOCUMENT_ROOT'] . '/content/users/dp_user.php';
	$user_session = DP_User::getAdminSession();
}

if (!isset($db_link) || !($db_link instanceof PDO)) {
	try {
		$db_link = new PDO(
			'mysql:host=' . $DP_Config->host . ';dbname=' . $DP_Config->db,
			$DP_Config->user,
			$DP_Config->password,
			array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION)
		);
		$db_link->query('SET NAMES utf8;');
	} catch (Exception $e) {
		echo '<div class="alert alert-danger">Database connection failed: '
			. htmlspecialchars($e->getMessage(), ENT_QUOTES, 'UTF-8') . '</div>';
		return;
	}
}

$config = array(
	'backend_dir' => $DP_Config->backend_dir,
	'domain_path' => $DP_Config->domain_path,
);
$snapshotError = '';
try {
	$snapshot = epc_order_guide_snapshot($db_link, $config);
} catch (Exception $e) {
	$snapshotError = $e->getMessage();
	$snapshot = array(
		'generated_at' => date('Y-m-d H:i:s'),
		'backend' => $DP_Config->backend_dir,
		'domain' => rtrim($DP_Config->domain_path, '/'),
		'notifications' => array(),
		'storages' => array(),
		'storages_lpo_ready' => 0,
		'storages_total' => 0,
		'order_stats' => array('total' => 0, 'today' => 0, 'last_7_days' => 0),
		'pending_trade_approvals' => 0,
		'recent_lpo_logs' => array(),
		'order_statuses' => array(),
		'item_statuses' => array(),
		'checklist' => epc_order_guide_checklist($DP_Config->backend_dir),
	);
}

$backend = '/' . $DP_Config->backend_dir;
$guideUrl = $backend . '/shop/orders/guide';
$ordersUrl = $backend . '/shop/orders/orders';
$adminEmail = function_exists('epc_admin_notify_email')
	? epc_admin_notify_email()
	: (string)$DP_Config->email_from;

$commDefinitions = epc_comm_test_definitions();
$commLastRun = epc_comm_test_load_last();
$commTestUrl = '/epc-order-communication-test.php?token=epartscart-deploy-2026&key=' . urlencode((string)$DP_Config->tech_key) . '&run=1';
$perfTestUrl = '/epc-site-performance-probe.php?token=epartscart-deploy-2026&key=' . urlencode((string)$DP_Config->tech_key) . '&save=1';
$perfLastRun = null;
$perfJsonPath = $_SERVER['DOCUMENT_ROOT'] . '/content/files/epc_performance_last.json';
if (is_readable($perfJsonPath)) {
	$perfDecoded = json_decode((string)file_get_contents($perfJsonPath), true);
	if (is_array($perfDecoded)) {
		$perfLastRun = $perfDecoded;
	}
}
?>

<div class="col-lg-12">
	<div class="hpanel">
		<div class="panel-heading hbuilt">
			Order fulfilment — documentation &amp; system status
			<span class="pull-right">
				<a class="btn btn-default btn-xs" href="<?php echo htmlspecialchars($ordersUrl, ENT_QUOTES, 'UTF-8'); ?>"><i class="fa fa-arrow-left"></i> Back to orders</a>
				<a class="btn btn-default btn-xs" href="<?php echo $backend; ?>/shop/prices/guide"><i class="fa fa-book"></i> Price upload guide</a>
			</span>
		</div>
		<div class="panel-body">

			<div class="alert alert-info">
				<strong>Open this guide while logged into the control panel.</strong>
				It describes the full path from customer checkout to supplier LPO and staff processing — same style as the
				<a href="<?php echo $backend; ?>/shop/prices/guide">price upload guide</a>.
				<ul class="m-t-sm" style="margin-bottom:0;">
					<li><strong>URL:</strong> <a href="<?php echo htmlspecialchars($guideUrl, ENT_QUOTES, 'UTF-8'); ?>"><?php echo htmlspecialchars($guideUrl, ENT_QUOTES, 'UTF-8'); ?></a></li>
					<li>From <a href="<?php echo htmlspecialchars($ordersUrl, ENT_QUOTES, 'UTF-8'); ?>">Orders</a> → blue button <strong>Order fulfilment guide</strong>.</li>
				</ul>
			</div>

			<p class="text-muted">Generated <?php echo htmlspecialchars($snapshot['generated_at'], ENT_QUOTES, 'UTF-8'); ?>.
				Covers checkout, e-mail notifications, supplier LPO (purchase orders), trade approvals, and CP order processing.</p>
			<?php if ($snapshotError !== ''): ?>
				<div class="alert alert-warning"><strong>Partial data:</strong> <?php echo htmlspecialchars($snapshotError, ENT_QUOTES, 'UTF-8'); ?></div>
			<?php endif; ?>

			<h4><i class="fa fa-sitemap"></i> End-to-end flow (overview)</h4>
			<div class="well well-sm" style="font-size:13px;line-height:1.6;">
				<ol style="margin-bottom:0;">
					<li><strong>Customer</strong> registers (Retail or Wholesale) → wholesale may need CP approval + fixed dealing currency.</li>
					<li><strong>Shop</strong> — search, cart, checkout (blocked if trade approval pending).</li>
					<li><strong>Order created</strong> — automatic e-mails: manager, customer, and supplier LPO per warehouse.</li>
					<li><strong>Supplier</strong> receives LPO e-mail — <strong>LPO number = customer order number</strong> (<code>shop_orders.id</code>).</li>
					<li><strong>Staff (CP)</strong> — open order, confirm payment, update line statuses, message customer, arrange delivery/pickup.</li>
					<li><strong>Complete</strong> — order and line statuses set to finished; customer notified if configured on status change.</li>
				</ol>
			</div>

			<h4><i class="fa fa-bar-chart"></i> Live status</h4>
			<table class="table table-striped table-bordered">
				<tbody>
					<tr>
						<td><strong>Orders in system</strong></td>
						<td><?php echo number_format((int)$snapshot['order_stats']['total']); ?></td>
						<td><small>Today: <?php echo (int)$snapshot['order_stats']['today']; ?> · Last 7 days: <?php echo (int)$snapshot['order_stats']['last_7_days']; ?></small></td>
					</tr>
					<tr>
						<td><strong>Pending trade approvals</strong></td>
						<td><?php echo (int)$snapshot['pending_trade_approvals']; ?></td>
						<td><a href="<?php echo $backend; ?>/users/customer_approvals">Customer approvals</a></td>
					</tr>
					<tr>
						<td><strong>Warehouses LPO-ready</strong></td>
						<td><?php echo (int)$snapshot['storages_lpo_ready']; ?> / <?php echo (int)$snapshot['storages_total']; ?></td>
						<td><small>Each warehouse needs <em>Supplier order email (LPO)</em> or price list sender e-mail</small></td>
					</tr>
					<tr>
						<td><strong>Primary admin inbox</strong></td>
						<td colspan="2"><code><?php echo htmlspecialchars($adminEmail, ENT_QUOTES, 'UTF-8'); ?></code></td>
					</tr>
				</tbody>
			</table>

			<h4><i class="fa fa-envelope"></i> Checkout notifications</h4>
			<table class="table table-bordered table-condensed">
				<thead>
					<tr>
						<th>Notification</th>
						<th>Recipient</th>
						<th>E-mail on</th>
						<th>When</th>
						<th>Configure</th>
					</tr>
				</thead>
				<tbody>
				<?php
				$notifyMeta = array(
					'new_order_to_manager' => array(
						'recipient' => 'Admin + office managers + CRM (if assigned)',
						'when' => 'Immediately after checkout',
					),
					'new_order_to_user' => array(
						'recipient' => 'Customer (registered or guest e-mail)',
						'when' => 'Immediately after checkout',
					),
					'lpo_to_supplier' => array(
						'recipient' => 'Supplier inbox per warehouse on the order',
						'when' => 'Immediately after checkout — one e-mail per warehouse with lines',
					),
				);
				if (count($snapshot['notifications']) === 0):
				?>
					<tr><td colspan="5"><em>No notification records found. Run <code>/epc-supplier-lpo-notification-setup.php</code> and check CP → Notifications.</em></td></tr>
				<?php else: ?>
					<?php foreach ($snapshot['notifications'] as $n):
						$name = (string)$n['name'];
						$meta = isset($notifyMeta[$name]) ? $notifyMeta[$name] : array('recipient' => '—', 'when' => '—');
					?>
					<tr<?php echo ((int)$n['email_on'] !== 1) ? ' class="warning"' : ''; ?>>
						<td><strong><code><?php echo htmlspecialchars($name, ENT_QUOTES, 'UTF-8'); ?></code></strong><br><small><?php echo htmlspecialchars((string)$n['caption'], ENT_QUOTES, 'UTF-8'); ?></small></td>
						<td><small><?php echo htmlspecialchars($meta['recipient'], ENT_QUOTES, 'UTF-8'); ?></small></td>
						<td><?php echo (int)$n['email_on'] === 1 ? '<span class="label label-success">ON</span>' : '<span class="label label-danger">OFF</span>'; ?></td>
						<td><small><?php echo htmlspecialchars($meta['when'], ENT_QUOTES, 'UTF-8'); ?></small></td>
						<td><a class="btn btn-default btn-xs" href="<?php echo $backend; ?>/control/notifications/notification?id=<?php echo (int)$n['id']; ?>">Edit template</a></td>
					</tr>
					<?php endforeach; ?>
				<?php endif; ?>
				</tbody>
			</table>
			<p class="text-muted"><small>LPO subject default: <code>LPO #%lpo_number% — please supply parts (%storage_name%)</code>. LPO number is always the customer order ID.</small></p>

			<h4><i class="fa fa-truck"></i> Supplier LPO — warehouse e-mail configuration</h4>
			<div class="alert alert-warning">
				<strong>Important:</strong> One LPO e-mail is sent <em>per warehouse</em> that has lines on the order.
				If a warehouse has no supplier e-mail, that warehouse is skipped and a note is written to the order log.
			</div>
			<table class="table table-bordered table-condensed table-striped">
				<thead>
					<tr>
						<th>Warehouse</th>
						<th>Price list</th>
						<th>Supplier order email (LPO)</th>
						<th>Fallback (price sender)</th>
						<th>Resolved inbox</th>
						<th>Ready</th>
						<th></th>
					</tr>
				</thead>
				<tbody>
				<?php if (count($snapshot['storages']) === 0): ?>
					<tr><td colspan="7"><em>No warehouses found.</em></td></tr>
				<?php else: ?>
					<?php foreach ($snapshot['storages'] as $s): ?>
					<tr<?php echo empty($s['lpo_ready']) ? ' class="danger"' : ''; ?>>
						<td><strong><?php echo htmlspecialchars($s['name'], ENT_QUOTES, 'UTF-8'); ?></strong> <small>#<?php echo (int)$s['id']; ?></small></td>
						<td><?php echo htmlspecialchars($s['price_list'] !== '' ? $s['price_list'] : '—', ENT_QUOTES, 'UTF-8'); ?></td>
						<td><code><?php echo $s['order_email'] !== '' ? htmlspecialchars($s['order_email'], ENT_QUOTES, 'UTF-8') : '—'; ?></code></td>
						<td><code><?php echo $s['price_sender_email'] !== '' ? htmlspecialchars($s['price_sender_email'], ENT_QUOTES, 'UTF-8') : '—'; ?></code></td>
						<td><code><?php echo $s['resolved_order_email'] !== '' ? htmlspecialchars($s['resolved_order_email'], ENT_QUOTES, 'UTF-8') : '—'; ?></code></td>
						<td><?php echo !empty($s['lpo_ready']) ? '<span class="label label-success">Yes</span>' : '<span class="label label-danger">No</span>'; ?></td>
						<td><a class="btn btn-default btn-xs" href="<?php echo $backend; ?>/shop/logistics/storages/storage?id=<?php echo (int)$s['id']; ?>">Edit warehouse</a></td>
					</tr>
					<?php endforeach; ?>
				<?php endif; ?>
				</tbody>
			</table>
			<p><strong>Set supplier e-mail:</strong> CP → Shop → Logistics → Warehouses → edit → field <strong>Supplier order email (LPO)</strong>.
				If empty, the system uses the linked price list <strong>Sender e-mail</strong>.</p>

			<?php if (count($snapshot['recent_lpo_logs']) > 0): ?>
			<h5><i class="fa fa-history"></i> Recent supplier LPO log entries</h5>
			<table class="table table-condensed table-bordered">
				<thead><tr><th>Order</th><th>Time</th><th>Log</th></tr></thead>
				<tbody>
				<?php foreach ($snapshot['recent_lpo_logs'] as $log): ?>
					<tr>
						<td><a href="<?php echo $backend; ?>/shop/orders/order?order_id=<?php echo (int)$log['order_id']; ?>">#<?php echo (int)$log['order_id']; ?></a></td>
						<td><small><?php echo date('Y-m-d H:i', (int)$log['time']); ?></small></td>
						<td><small><?php echo htmlspecialchars((string)$log['text'], ENT_QUOTES, 'UTF-8'); ?></small></td>
					</tr>
				<?php endforeach; ?>
				</tbody>
			</table>
			<?php endif; ?>

			<hr>

			<h4><i class="fa fa-book"></i> Step-by-step: order fulfilment process</h4>
			<div class="panel-group" id="epc_order_guide_accordion">

				<div class="panel panel-default">
					<div class="panel-heading"><h5 class="panel-title"><a data-toggle="collapse" href="#guide_trade">1. Customer registration &amp; trade approval (Retail / Wholesale)</a></h5></div>
					<div id="guide_trade" class="panel-collapse collapse in">
						<div class="panel-body">
							<p><span class="label label-primary">Storefront</span> Registration form · <span class="label label-default">CP</span> Customer approvals</p>
							<ol>
								<li>Customer chooses <strong>Retail</strong> or <strong>Wholesale</strong> at registration.</li>
								<li>Both can browse and add to cart. <strong>Checkout is blocked</strong> until a manager approves wholesale (and retail if you use pending flow).</li>
								<li>CP manager opens <a href="<?php echo $backend; ?>/users/customer_approvals">Users → Customer approvals</a>.</li>
								<li>Approve customer and assign <strong>fixed dealing currency</strong> (required for wholesale).</li>
								<li>Customer receives access to checkout; prices show in assigned currency.</li>
							</ol>
							<p><strong>Test:</strong> Register wholesale test user → verify pending → approve in CP → complete checkout.</p>
						</div>
					</div>
				</div>

				<div class="panel panel-default">
					<div class="panel-heading"><h5 class="panel-title"><a data-toggle="collapse" href="#guide_checkout" class="collapsed">2. Browse, cart &amp; checkout</a></h5></div>
					<div id="guide_checkout" class="panel-collapse collapse">
						<div class="panel-body">
							<ol>
								<li>Customer searches parts (part number, VIN, vehicle catalog, crosses).</li>
								<li>Adds lines to cart — each line is tied to a <strong>warehouse / price list</strong> (e.g. S-UAE, R-UAE).</li>
								<li>Checkout: delivery/pickup mode, payment type, contact details.</li>
								<li>On confirm, order is saved to <code>shop_orders</code> + <code>shop_orders_items</code>.</li>
								<li>Customer is redirected to <code>/shop/orders/order?order_id=…</code> (or guest order page).</li>
							</ol>
							<p><strong>Staff margin view:</strong> Order card shows sale, purchase, margin (same data as manager e-mail).</p>
						</div>
					</div>
				</div>

				<div class="panel panel-default">
					<div class="panel-heading"><h5 class="panel-title"><a data-toggle="collapse" href="#guide_emails" class="collapsed">3. Automatic e-mails on new order (3 channels)</a></h5></div>
					<div id="guide_emails" class="panel-collapse collapse">
						<div class="panel-body">
							<p>Triggered by <code>epc_checkout_send_order_notifications()</code> immediately after order creation.</p>
							<table class="table table-condensed table-bordered">
								<thead><tr><th>#</th><th>Notification</th><th>Who receives</th><th>Content</th></tr></thead>
								<tbody>
									<tr><td>1</td><td><code>new_order_to_manager</code></td><td>Admin (<?php echo htmlspecialchars($adminEmail, ENT_QUOTES, 'UTF-8'); ?>), office managers, CRM</td><td>Full order HTML + CP link</td></tr>
									<tr><td>2</td><td><code>new_order_to_user</code></td><td>Customer</td><td>Order confirmation + storefront link</td></tr>
									<tr><td>3</td><td><code>lpo_to_supplier</code></td><td>Supplier per warehouse</td><td>Purchase request — parts list, LPO # = order ID</td></tr>
								</tbody>
							</table>
							<p>Each send is logged on the order card under <strong>Order log</strong>. Failed sends are retried once for admin/customer.</p>
							<p><strong>Edit templates:</strong> CP → Control panel → <a href="<?php echo $backend; ?>/control/notifications">Notifications</a>.</p>
						</div>
					</div>
				</div>

				<div class="panel panel-default">
					<div class="panel-heading"><h5 class="panel-title"><a data-toggle="collapse" href="#guide_lpo" class="collapsed">4. Supplier LPO — purchase order to supplier</a></h5></div>
					<div id="guide_lpo" class="panel-collapse collapse">
						<div class="panel-body">
							<p><span class="label label-warning">LPO number</span> Always equals the <strong>customer order number</strong> (<code>shop_orders.id</code>). Suppliers must reference this on invoices and delivery notes.</p>
							<ol>
								<li>Configure <strong>Supplier order email (LPO)</strong> on each warehouse (table above).</li>
								<li>When order contains lines from warehouse R-UAE and S-UAE → two separate LPO e-mails.</li>
								<li>E-mail body lists: brand, article, description, qty, purchase price.</li>
								<li>Supplier ships goods to your warehouse / customer — staff updates line statuses in CP.</li>
							</ol>
							<p><strong>Resend LPO for an existing order:</strong> deploy script <code>/epc-order-notifications-fix.php?token=…&amp;key=…&amp;order_id=</code> or probe <code>/epc-supplier-lpo-probe.php?…&amp;order_id=…&amp;send=1</code>.</p>
						</div>
					</div>
				</div>

				<div class="panel panel-default">
					<div class="panel-heading"><h5 class="panel-title"><a data-toggle="collapse" href="#guide_cp_order" class="collapsed">5. CP staff — process the order</a></h5></div>
					<div id="guide_cp_order" class="panel-collapse collapse">
						<div class="panel-body">
							<ol>
								<li>Open <a href="<?php echo htmlspecialchars($ordersUrl, ENT_QUOTES, 'UTF-8'); ?>">Orders list</a> — filter by status, office, payment, article.</li>
								<li>Click order number → <strong>Order card</strong> (<code>/shop/orders/order?order_id=</code>).</li>
								<li>Review <strong>Order intelligence (staff)</strong>: customer profile, CRM, margin totals.</li>
								<li>Confirm payment — record income in customer balance or mark paid.</li>
								<li>Update <strong>line item statuses</strong> (ordered → in stock → shipped → delivered).</li>
								<li>Use <strong>Messages</strong> tab to communicate with customer (e-mail notification optional).</li>
								<li>Add/remove lines if needed (edit order items — only when unpaid).</li>
							</ol>
						</div>
					</div>
				</div>

				<div class="panel panel-default">
					<div class="panel-heading"><h5 class="panel-title"><a data-toggle="collapse" href="#guide_statuses" class="collapsed">6. Order statuses &amp; line item statuses</a></h5></div>
					<div id="guide_statuses" class="panel-collapse collapse">
						<div class="panel-body">
							<p>Configure in CP → Shop → Orders → <a href="<?php echo $backend; ?>/shop/orders/statuses">Statuses</a>.
								Each status can trigger e-mail to manager and/or customer when applied.</p>
							<h5>Order-level statuses</h5>
							<table class="table table-condensed table-bordered">
								<thead><tr><th>ID</th><th>Name key</th><th>Created</th><th>Paid</th><th>Finish</th><th>E-mail flags</th></tr></thead>
								<tbody>
								<?php if (count($snapshot['order_statuses']) === 0): ?>
									<tr><td colspan="6"><em>Could not load statuses.</em></td></tr>
								<?php else: ?>
									<?php foreach ($snapshot['order_statuses'] as $st): ?>
									<tr>
										<td><?php echo (int)$st['id']; ?></td>
										<td><small><?php echo htmlspecialchars((string)$st['name'], ENT_QUOTES, 'UTF-8'); ?></small></td>
										<td><?php echo (int)$st['for_created'] ? '✓' : ''; ?></td>
										<td><?php echo (int)$st['for_paid'] ? '✓' : ''; ?></td>
										<td><?php echo (int)$st['for_finish'] ? '✓' : ''; ?></td>
										<td><small>M:<?php echo (int)$st['to_manager_email']; ?> C:<?php echo (int)$st['to_customer_email']; ?></small></td>
									</tr>
									<?php endforeach; ?>
								<?php endif; ?>
								</tbody>
							</table>
							<h5>Line item statuses (first 20)</h5>
							<table class="table table-condensed table-bordered">
								<thead><tr><th>ID</th><th>Name key</th><th>Created</th><th>Finish</th><th>E-mail flags</th></tr></thead>
								<tbody>
								<?php foreach ($snapshot['item_statuses'] as $st): ?>
									<tr>
										<td><?php echo (int)$st['id']; ?></td>
										<td><small><?php echo htmlspecialchars((string)$st['name'], ENT_QUOTES, 'UTF-8'); ?></small></td>
										<td><?php echo (int)$st['for_created'] ? '✓' : ''; ?></td>
										<td><?php echo (int)$st['for_finish'] ? '✓' : ''; ?></td>
										<td><small>M:<?php echo (int)$st['to_manager_email']; ?> C:<?php echo (int)$st['to_customer_email']; ?></small></td>
									</tr>
								<?php endforeach; ?>
								</tbody>
							</table>
						</div>
					</div>
				</div>

				<div class="panel panel-default">
					<div class="panel-heading"><h5 class="panel-title"><a data-toggle="collapse" href="#guide_payment" class="collapsed">7. Payment &amp; customer balance</a></h5></div>
					<div id="guide_payment" class="panel-collapse collapse">
						<div class="panel-body">
							<ol>
								<li>Order card shows <strong>amount due</strong> vs <strong>paid</strong> and customer balance.</li>
								<li>Record payment via order card or <a href="<?php echo $backend; ?>/shop/finance">Finance</a> / customer balance.</li>
								<li>When fully paid, set order status to the configured <em>paid</em> status.</li>
								<li>Wholesale customers may use pre-paid balance — check balance before shipping.</li>
							</ol>
						</div>
					</div>
				</div>

				<div class="panel panel-default">
					<div class="panel-heading"><h5 class="panel-title"><a data-toggle="collapse" href="#guide_delivery" class="collapsed">8. Delivery, pickup &amp; obtaining modes</a></h5></div>
					<div id="guide_delivery" class="panel-collapse collapse">
						<div class="panel-body">
							<p>Customer selects obtaining mode at checkout (delivery, pickup at office, etc.).</p>
							<ol>
								<li>Configure modes: CP → Shop → Logistics → <a href="<?php echo $backend; ?>/shop/logistics/sposoby-polucheniya">Obtaining modes</a>.</li>
								<li>Staff arranges shipment after supplier goods arrive and payment confirmed.</li>
								<li>Update line statuses to reflect dispatch and completion.</li>
							</ol>
						</div>
					</div>
				</div>

				<div class="panel panel-default">
					<div class="panel-heading"><h5 class="panel-title"><a data-toggle="collapse" href="#guide_api_warehouses" class="collapsed">9. API warehouses (SAO / external stock)</a></h5></div>
					<div id="guide_api_warehouses" class="panel-collapse collapse">
						<div class="panel-body">
							<p>Some warehouses use live API suppliers instead of price-list stock. Those lines may use SAO (supplier auto order) workflows in addition to LPO e-mail.</p>
							<p>Price-list warehouses (S-UAE, R-UAE, etc.) rely on <strong>LPO e-mail</strong> after checkout. Keep price lists updated via the <a href="<?php echo $backend; ?>/shop/prices/guide">price upload guide</a>.</p>
						</div>
					</div>
				</div>

				<div class="panel panel-default">
					<div class="panel-heading"><h5 class="panel-title"><a data-toggle="collapse" href="#guide_troubleshoot" class="collapsed">10. Troubleshooting &amp; resend</a></h5></div>
					<div id="guide_troubleshoot" class="panel-collapse collapse">
						<div class="panel-body">
							<table class="table table-bordered table-condensed">
								<thead><tr><th>Problem</th><th>Check</th></tr></thead>
								<tbody>
									<tr><td>No manager e-mail</td><td>Notifications → <code>new_order_to_manager</code> e-mail ON; SMTP in config; order log on card</td></tr>
									<tr><td>No customer e-mail</td><td><code>new_order_to_user</code>; guest e-mail on order; spam folder</td></tr>
									<tr><td>No supplier LPO</td><td>Warehouse LPO e-mail table above; order log says “skipped (no order e-mail)”</td></tr>
									<tr><td>LPO wrong warehouse</td><td>Line <code>t2_storage_id</code> / price list link — check item edit</td></tr>
									<tr><td>Checkout blocked</td><td><a href="<?php echo $backend; ?>/users/customer_approvals">Customer approvals</a> — pending wholesale user</td></tr>
								</tbody>
							</table>
							<p><strong>Probe scripts (tech_key):</strong></p>
							<ul>
								<li><code>/epc-supplier-lpo-probe.php?token=…&amp;key=…</code> — warehouse LPO readiness</li>
								<li><code>/epc-order-notifications-fix.php?token=…&amp;key=…&amp;order_id=</code> — resend all checkout e-mails</li>
							</ul>
						</div>
					</div>
				</div>

			</div>

			<hr>
			<h4><i class="fa fa-flask"></i> Test checklist</h4>
			<table class="table table-bordered">
				<thead><tr><th>Step</th><th>Where in CP</th><th>Pass criteria</th></tr></thead>
				<tbody>
				<?php foreach ($snapshot['checklist'] as $row): ?>
					<tr>
						<td><?php echo htmlspecialchars($row['step'], ENT_QUOTES, 'UTF-8'); ?></td>
						<td><small><a href="<?php echo htmlspecialchars($row['where'], ENT_QUOTES, 'UTF-8'); ?>"><?php echo htmlspecialchars($row['where'], ENT_QUOTES, 'UTF-8'); ?></a></small></td>
						<td><small><?php echo htmlspecialchars($row['test'], ENT_QUOTES, 'UTF-8'); ?></small></td>
					</tr>
				<?php endforeach; ?>
				</tbody>
			</table>

			<hr>
			<h4><i class="fa fa-envelope-o"></i> Communication test lab — all e-mail channels</h4>
			<div class="alert alert-info">
				<strong>Automated test</strong> creates a test customer, test order (lines on multiple warehouses), and fires every order-related notification.
				Results are saved below after each run. Check inboxes for <strong>customer</strong>, <strong>admin</strong>, and <strong>supplier (LPO)</strong> e-mails.
			</div>
			<div class="alert alert-warning">
				<strong>Reading results:</strong> Customer e-mails go to the test address above; admin e-mails to <code><?php echo htmlspecialchars($adminEmail, ENT_QUOTES, 'UTF-8'); ?></code>; supplier LPO e-mails to each warehouse’s configured inbox (currently R-UAE / RK-UAE / L-UAE → <code>786yawer@gmail.com</code> for testing). Rows with <code>user#1=FAILED</code> mean the CP manager user’s personal e-mail failed — the admin inbox line still counts. Check the order log on the test order for the authoritative send status.
			</div>

			<h5>Test customer (created/updated by automated run)</h5>
			<table class="table table-bordered table-condensed">
				<tbody>
					<tr><td><strong>E-mail</strong></td><td><code>786yawer@gmail.com</code> <small>(override with <code>&amp;email=</code> on script URL)</small></td></tr>
					<tr><td><strong>Password</strong></td><td><code>EpcCommTest2026!</code></td></tr>
					<tr><td><strong>Trade status</strong></td><td>Retail · Approved · Dealing currency AED</td></tr>
					<tr><td><strong>Storefront login</strong></td><td><a href="<?php echo htmlspecialchars(rtrim($DP_Config->domain_path, '/') . '/en/users/login', ENT_QUOTES, 'UTF-8'); ?>" target="_blank">/en/users/login</a></td></tr>
				</tbody>
			</table>

			<h5>Run automated test (tech staff)</h5>
			<p>Open in browser or curl (requires deploy token + tech_key):</p>
			<pre style="background:#f5f5f5;padding:10px;word-break:break-all;"><?php echo htmlspecialchars($commTestUrl, ENT_QUOTES, 'UTF-8'); ?></pre>
			<p class="text-muted"><small>Dry-run plan only: add <code>&amp;dry_run=1</code> (no e-mails). Catalog only: omit <code>&amp;run=1</code>.</small></p>

			<h5>All communication types (order fulfilment)</h5>
			<table class="table table-striped table-bordered table-condensed">
				<thead>
					<tr>
						<th>Notification</th>
						<th>Channel</th>
						<th>Trigger</th>
						<th>Recipient</th>
						<th>In CP</th>
						<th>E-mail ON</th>
					</tr>
				</thead>
				<tbody>
				<?php foreach ($commDefinitions as $def):
					$nrow = null;
					foreach ($snapshot['notifications'] as $sn) {
						if (($sn['name'] ?? '') === $def['key']) {
							$nrow = $sn;
							break;
						}
					}
					if ($nrow === null) {
						try {
							$q = $db_link->prepare('SELECT `email_on` FROM `notifications_settings` WHERE `name` = ? LIMIT 1');
							$q->execute(array($def['key']));
							$eon = $q->fetchColumn();
							$nrow = ($eon !== false) ? array('email_on' => (int)$eon) : null;
						} catch (Exception $e) {
							$nrow = null;
						}
					}
				?>
					<tr>
						<td><code><?php echo htmlspecialchars($def['key'], ENT_QUOTES, 'UTF-8'); ?></code></td>
						<td><?php echo htmlspecialchars($def['channel'], ENT_QUOTES, 'UTF-8'); ?></td>
						<td><small><?php echo htmlspecialchars($def['trigger'], ENT_QUOTES, 'UTF-8'); ?></small></td>
						<td><small><?php echo htmlspecialchars($def['recipient'], ENT_QUOTES, 'UTF-8'); ?></small></td>
						<td><small><?php echo htmlspecialchars($def['cp_path'], ENT_QUOTES, 'UTF-8'); ?></small></td>
						<td><?php
							if ($nrow === null) {
								echo '<span class="label label-default">n/a</span>';
							} else {
								echo ((int)($nrow['email_on'] ?? 0) === 1)
									? '<span class="label label-success">ON</span>'
									: '<span class="label label-warning">OFF</span>';
							}
						?></td>
					</tr>
				<?php endforeach; ?>
				</tbody>
			</table>

			<h5>Manual test sequence (same as automated run)</h5>
			<ol>
				<li>Log in as test customer → add parts from <strong>S-UAE</strong> and <strong>R-UAE</strong> to cart → checkout.</li>
				<li>Check <strong><?php echo htmlspecialchars($adminEmail, ENT_QUOTES, 'UTF-8'); ?></strong> — manager order e-mail with CP link.</li>
				<li>Check <strong>786yawer@gmail.com</strong> — customer confirmation.</li>
				<li>Check <strong>786yawer@gmail.com</strong> — supplier LPO e-mails (one per warehouse; LPO # = order number).</li>
				<li>On order page (storefront) send a message → manager e-mail. Reply from CP order card → customer e-mail.</li>
				<li>Change order / line status in CP (if status has e-mail flags) → status notifications.</li>
				<li>Record payment on order → payment notifications.</li>
				<li>Confirm every step in <strong>Order log</strong> on the order card.</li>
			</ol>

			<?php if (is_array($commLastRun)): ?>
			<h5><i class="fa fa-history"></i> Last automated test run</h5>
			<p class="text-muted">Run at <?php echo htmlspecialchars((string)($commLastRun['generated_at'] ?? ''), ENT_QUOTES, 'UTF-8'); ?>
				<?php if (!empty($commLastRun['summary'])): ?>
					— <?php echo (int)($commLastRun['summary']['sent_ok'] ?? 0); ?> OK,
					<?php echo (int)($commLastRun['summary']['sent_failed'] ?? 0); ?> failed,
					<?php echo (int)($commLastRun['summary']['unknown_or_log_only'] ?? 0); ?> log-only
				<?php endif; ?>
			</p>
			<?php if (!empty($commLastRun['test_order']['order_id'])): ?>
				<p>Test order:
					<a href="<?php echo $backend; ?>/shop/orders/order?order_id=<?php echo (int)$commLastRun['test_order']['order_id']; ?>">
						#<?php echo (int)$commLastRun['test_order']['order_id']; ?>
					</a>
					<?php if (!empty($commLastRun['test_customer']['user_id'])): ?>
						· Customer user #<?php echo (int)$commLastRun['test_customer']['user_id']; ?>
					<?php endif; ?>
				</p>
			<?php endif; ?>
			<table class="table table-condensed table-bordered">
				<thead><tr><th>Test</th><th>Result</th><th>Detail</th></tr></thead>
				<tbody>
				<?php foreach (($commLastRun['tests'] ?? array()) as $tr): ?>
					<tr<?php echo ($tr['sent'] ?? null) === false ? ' class="danger"' : (($tr['sent'] ?? null) === true ? ' class="success"' : ''); ?>>
						<td><code><?php echo htmlspecialchars((string)($tr['name'] ?? ''), ENT_QUOTES, 'UTF-8'); ?></code></td>
						<td><?php
							if (($tr['sent'] ?? null) === true) {
								echo '<span class="label label-success">Sent</span>';
							} elseif (($tr['sent'] ?? null) === false) {
								echo '<span class="label label-danger">Failed</span>';
							} else {
								echo '<span class="label label-default">Log / n/a</span>';
							}
						?></td>
						<td><small><?php echo htmlspecialchars((string)($tr['detail'] ?? ($tr['log'] ?? '')), ENT_QUOTES, 'UTF-8'); ?></small></td>
					</tr>
				<?php endforeach; ?>
				</tbody>
			</table>
			<?php else: ?>
			<p class="text-muted"><em>No automated test run saved yet. Run the script URL above with <code>&amp;run=1</code>.</em></p>
			<?php endif; ?>

			<hr>
			<h4><i class="fa fa-tachometer"></i> Site performance lab</h4>
			<div class="alert alert-info">
				<strong>Automated benchmark</strong> measures storefront pages, Epart catalog and cross-reference APIs, and static CSS/JS.
				Each URL gets a grade (A–D) from response time and payload size. Results are saved for this guide when you add <code>&amp;save=1</code>.
			</div>
			<p><strong>Run benchmark (tech staff):</strong></p>
			<pre style="background:#f5f5f5;padding:10px;word-break:break-all;"><?php echo htmlspecialchars($perfTestUrl, ENT_QUOTES, 'UTF-8'); ?></pre>
			<p class="text-muted"><small>Grades: A ≤1.5s &amp; ≤400KB · B ≤3s &amp; ≤600KB · C ≤6s · D slower/larger. Storefront HTML is PHP-dynamic (Cloudflare DYNAMIC) — API cache headers and gzip on origin help repeat visits.</small></p>

			<?php if (is_array($perfLastRun) && !empty($perfLastRun['results'])): ?>
			<h5><i class="fa fa-history"></i> Last performance run</h5>
			<p class="text-muted">
				Tested at <?php echo htmlspecialchars((string)($perfLastRun['tested_at'] ?? ''), ENT_QUOTES, 'UTF-8'); ?>
				— overall grade <strong><?php echo htmlspecialchars((string)($perfLastRun['overall_grade'] ?? ''), ENT_QUOTES, 'UTF-8'); ?></strong>
				<?php if (!empty($perfLastRun['summary'])): ?>
					· avg storefront <?php echo (int)($perfLastRun['summary']['avg_storefront_ms'] ?? 0); ?> ms
					· avg API <?php echo (int)($perfLastRun['summary']['avg_api_ms'] ?? 0); ?> ms
				<?php endif; ?>
			</p>
			<table class="table table-condensed table-bordered">
				<thead><tr><th>URL</th><th>Grade</th><th>Time</th><th>Size</th><th>Cache</th></tr></thead>
				<tbody>
				<?php foreach ($perfLastRun['results'] as $pr): ?>
					<tr<?php
						$g = (string)($pr['grade'] ?? '');
						echo $g === 'D' ? ' class="danger"' : ($g === 'A' ? ' class="success"' : '');
					?>>
						<td><small><code><?php echo htmlspecialchars((string)($pr['label'] ?? ''), ENT_QUOTES, 'UTF-8'); ?></code></small></td>
						<td><?php echo htmlspecialchars($g, ENT_QUOTES, 'UTF-8'); ?></td>
						<td><?php echo (int)($pr['time_ms'] ?? 0); ?> ms</td>
						<td><?php echo number_format((int)($pr['bytes'] ?? 0)); ?> B</td>
						<td><small><?php echo htmlspecialchars((string)($pr['cache_control'] ?? ''), ENT_QUOTES, 'UTF-8'); ?></small></td>
					</tr>
				<?php endforeach; ?>
				</tbody>
			</table>
			<?php else: ?>
			<p class="text-muted"><em>No performance run saved yet. Open the URL above in browser or curl.</em></p>
			<?php endif; ?>

			<h4><i class="fa fa-link"></i> Quick CP links</h4>
			<table class="table table-condensed table-bordered">
				<tbody>
					<tr><td>Orders list</td><td><a href="<?php echo htmlspecialchars($ordersUrl, ENT_QUOTES, 'UTF-8'); ?>"><?php echo htmlspecialchars($ordersUrl, ENT_QUOTES, 'UTF-8'); ?></a></td></tr>
					<tr><td>Order statuses</td><td><a href="<?php echo $backend; ?>/shop/orders/statuses"><?php echo $backend; ?>/shop/orders/statuses</a></td></tr>
					<tr><td>Customer approvals</td><td><a href="<?php echo $backend; ?>/users/customer_approvals"><?php echo $backend; ?>/users/customer_approvals</a></td></tr>
					<tr><td>Warehouses (LPO e-mail)</td><td><a href="<?php echo $backend; ?>/shop/logistics/storages"><?php echo $backend; ?>/shop/logistics/storages</a></td></tr>
					<tr><td>Notifications</td><td><a href="<?php echo $backend; ?>/control/notifications"><?php echo $backend; ?>/control/notifications</a></td></tr>
					<tr><td>Price upload guide</td><td><a href="<?php echo $backend; ?>/shop/prices/guide"><?php echo $backend; ?>/shop/prices/guide</a></td></tr>
				</tbody>
			</table>

		</div>
	</div>
</div>
