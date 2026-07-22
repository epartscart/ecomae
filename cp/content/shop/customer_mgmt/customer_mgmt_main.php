<?php
/**
 * CP — Customer management (orders, invoices, advances, returns, e-invoice profile).
 * URL: /cp/shop/customer_mgmt/customer_mgmt
 */
defined('_ASTEXE_') or die('No access');

require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/customer_mgmt/epc_customer_mgmt_helpers.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_einvoice.php';

if ($_SERVER['REQUEST_METHOD'] === 'POST' && !empty($_POST['action'])) {
	require __DIR__ . '/ajax_customer_mgmt.php';
	exit;
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
		echo '<div class="alert alert-danger">Database connection failed.</div>';
		return;
	}
}

$backend = (string)$DP_Config->backend_dir;
$cmUrl = '/' . $backend . '/shop/customer_mgmt/customer_mgmt';
$cmAjaxUrl = '/' . $backend . '/content/shop/customer_mgmt/ajax_customer_mgmt_endpoint.php';
$ordersUrl = '/' . $backend . '/shop/order_process/orders';
$erpUrl = '/' . $backend . '/shop/finance/erp';
$approvalsUrl = '/' . $backend . '/users/customer_approvals';
$financeOpsUrl = '/' . $backend . '/shop/finance/finance_operations';

$tabs = array(
	'dashboard' => 'Dashboard',
	'customers' => 'Customers',
	'orders' => 'Orders',
	'invoices' => 'Invoices',
	'advances' => 'Advances',
	'returns' => 'Returns',
	'approvals' => 'Approvals',
	'guide' => 'Guide',
);
$tab = isset($_GET['tab']) ? (string)$_GET['tab'] : 'dashboard';
if (!isset($tabs[$tab])) {
	$tab = 'dashboard';
}

$search = isset($_GET['q']) ? trim((string)$_GET['q']) : '';
$view_user = isset($_GET['user_id']) ? (int)$_GET['user_id'] : 0;
$cmPage = max(1, (int)($_GET['page'] ?? 1));
$cmPerPage = 50;

if (in_array($tab, array('dashboard', 'invoices', 'customers'), true) || $view_user > 0) {
	epc_einvoice_ensure_schema($db_link);
}

$csrf = isset($user_session['csrf_guard_key']) ? (string)$user_session['csrf_guard_key'] : '';
$cmLoadError = '';
$dash = array();
$customer = null;
$cmTotal = 0;
$customers = array();
$orders = array();
$returns = array();
$einvoices = array();
$advances_list = array();

try {
	if ($tab === 'dashboard') {
		$dash = epc_cm_dashboard($db_link);
	}
	if ($view_user > 0) {
		$customer = epc_cm_get_customer($db_link, $view_user);
	}
	if ($tab === 'customers' && $view_user <= 0) {
		$cmTotal = epc_cm_count_customers($db_link, $search);
		$customers = epc_cm_list_customers($db_link, $search, $cmPerPage, ($cmPage - 1) * $cmPerPage);
	}
	if ($tab === 'orders') {
		$orders = epc_cm_recent_orders($db_link);
	} elseif ($view_user > 0) {
		$orders = epc_cm_customer_orders($db_link, $view_user);
	}
	if ($tab === 'returns') {
		$returns = epc_cm_recent_returns($db_link);
	}
	if ($tab === 'invoices') {
		$st = $db_link->query('SELECT d.*, u.`email` FROM `epc_einvoice_documents` d LEFT JOIN `users` u ON u.`user_id` = d.`user_id` WHERE d.`active` = 1 ORDER BY d.`issue_date` DESC LIMIT 50');
		$einvoices = $st->fetchAll(PDO::FETCH_ASSOC);
	} elseif ($view_user > 0) {
		$einvoices = epc_cm_customer_einvoices($db_link, $view_user);
	}
	if ($tab === 'advances') {
		$st = $db_link->query(
			'SELECT a.*, u.`email` FROM `shop_users_accounting` a
			LEFT JOIN `users` u ON u.`user_id` = a.`user_id`
			WHERE a.`active` = 1 AND a.`income` = 1
			ORDER BY a.`time` DESC LIMIT 80'
		);
		$advances_list = $st->fetchAll(PDO::FETCH_ASSOC);
	}
} catch (Throwable $e) {
	$cmLoadError = 'Could not load customer data: ' . $e->getMessage();
}
?>

<link rel="stylesheet" href="/content/shop/finance/epc_erp_ui.css?v=20260518">
<style>
.epc-cm-shell { --cm-ink:#0f172a; --cm-muted:#64748b; --cm-line:#e2e8f0; --cm-soft:#f1f5f9; --cm-accent:#0f766e; --cm-accent-2:#134e4a; --cm-warn:#b45309; --cm-ok:#15803d; }
.epc-cm-hero {
	background:
		radial-gradient(900px 220px at 12% -20%, rgba(255,255,255,.18), transparent 55%),
		linear-gradient(125deg, #134e4a 0%, #0f766e 48%, #115e59 100%);
	color: #fff; border-radius: 14px; padding: 22px 24px; margin-bottom: 18px;
	box-shadow: 0 10px 28px rgba(15, 118, 110, .18);
}
.epc-cm-hero h3 { margin: 0 0 8px; color: #fff; font-size: 22px; letter-spacing: -.02em; }
.epc-cm-hero p { margin: 0; opacity: .92; max-width: 720px; line-height: 1.45; }
.epc-cm-kpi { display: flex; flex-wrap: wrap; gap: 12px; margin-bottom: 16px; }
.epc-cm-kpi .kpi { flex: 1 1 120px; background: linear-gradient(180deg,#fff,#f8fafc); border: 1px solid var(--cm-line); border-radius: 12px; padding: 14px 14px 12px; }
.epc-cm-kpi .lbl { font-size: 10px; color: var(--cm-muted); text-transform: uppercase; letter-spacing: .04em; }
.epc-cm-kpi .val { font-size: 20px; font-weight: 700; color: var(--cm-accent-2); margin-top: 4px; }
.epc-cm-nav { display:flex; flex-wrap:wrap; gap:6px; margin: 0 0 16px; padding: 8px; background: var(--cm-soft); border: 1px solid var(--cm-line); border-radius: 12px; }
.epc-cm-nav .btn { margin: 0; border-radius: 8px !important; }
.epc-cm-nav .btn-primary { background: var(--cm-accent) !important; border-color: var(--cm-accent) !important; }
.epc-cm-msg { display: none; margin: 10px 0; }
.epc-cm-toolbar { display:flex; flex-wrap:wrap; gap:10px; align-items:center; justify-content:space-between; margin-bottom:14px; }
.epc-cm-search { display:flex; flex:1 1 280px; gap:8px; align-items:center; background:#fff; border:1px solid var(--cm-line); border-radius:12px; padding:6px 8px 6px 12px; }
.epc-cm-search input { border:0; box-shadow:none !important; outline:none; flex:1; min-width:0; height:32px; }
.epc-cm-meta { color:var(--cm-muted); font-size:13px; }
.epc-cm-dir { display:flex; flex-direction:column; gap:10px; }
.epc-cm-row {
	display:grid; grid-template-columns: 52px minmax(0,1.4fr) minmax(0,1fr) 90px 100px 88px;
	gap:12px; align-items:center; background:#fff; border:1px solid var(--cm-line);
	border-radius:14px; padding:12px 14px; text-decoration:none !important; color:inherit;
	transition: border-color .15s ease, box-shadow .15s ease, transform .15s ease;
}
.epc-cm-row:hover { border-color:#99f6e4; box-shadow:0 8px 20px rgba(15,118,110,.08); transform: translateY(-1px); }
.epc-cm-av {
	width:44px; height:44px; border-radius:12px; display:flex; align-items:center; justify-content:center;
	font-weight:700; color:#fff; background: linear-gradient(145deg,#0f766e,#115e59); letter-spacing:.02em;
}
.epc-cm-name { font-weight:700; color:var(--cm-ink); font-size:14px; line-height:1.25; }
.epc-cm-sub { color:var(--cm-muted); font-size:12px; margin-top:2px; word-break:break-all; }
.epc-cm-chip { display:inline-block; font-size:11px; padding:3px 8px; border-radius:999px; background:#ecfdf5; color:#065f46; border:1px solid #a7f3d0; }
.epc-cm-chip.is-muted { background:#f8fafc; color:#64748b; border-color:#e2e8f0; }
.epc-cm-chip.is-warn { background:#fffbeb; color:#92400e; border-color:#fde68a; }
.epc-cm-stat { font-size:12px; color:var(--cm-muted); }
.epc-cm-stat strong { display:block; color:var(--cm-ink); font-size:15px; }
.epc-cm-empty {
	border:1px dashed #cbd5e1; border-radius:14px; padding:28px 18px; text-align:center; color:var(--cm-muted);
	background: linear-gradient(180deg,#fff,#f8fafc);
}
.epc-cm-empty h4 { margin:0 0 6px; color:var(--cm-ink); }
@media (max-width: 900px) {
	.epc-cm-row { grid-template-columns: 44px 1fr; grid-template-areas: "av main" "av meta"; }
	.epc-cm-row > :nth-child(1){ grid-area: av; }
	.epc-cm-row > :nth-child(2){ grid-area: main; }
	.epc-cm-row > :nth-child(n+3){ display:none; }
}
</style>

<div class="col-lg-12 epc-erp-shell epc-cm-shell">
	<div class="hpanel">
		<div class="panel-heading hbuilt">
			<i class="fa fa-users"></i> Customer management
			<span class="pull-right">
				<a class="btn btn-default btn-xs" href="<?php echo epc_cm_h($ordersUrl); ?>"><i class="fa fa-shopping-cart"></i> Orders</a>
				<a class="btn btn-default btn-xs" href="<?php echo epc_cm_h($erpUrl); ?>?tab=einvoice"><i class="fa fa-file-text-o"></i> E-Invoicing</a>
			</span>
		</div>
		<div class="panel-body">

			<div class="epc-cm-hero">
				<h3><i class="fa fa-address-book"></i> Customer directory</h3>
				<p>Find buyers, keep UAE e-invoice profiles current, and jump into orders, advances, and returns from one place.</p>
			</div>

			<?php if ($cmLoadError !== ''): ?>
				<div class="alert alert-danger"><?php echo epc_cm_h($cmLoadError); ?></div>
			<?php endif; ?>

			<div id="epc_cm_msg" class="alert epc-cm-msg"></div>

			<div class="epc-cm-nav">
				<?php foreach ($tabs as $k => $lbl): ?>
					<a class="btn btn-sm <?php echo $tab === $k ? 'btn-primary' : 'btn-default'; ?>"
					   href="<?php echo epc_cm_h(epc_cm_tab_url($cmUrl, $k)); ?>"><?php echo epc_cm_h($lbl); ?></a>
				<?php endforeach; ?>
			</div>

			<?php if ($tab === 'dashboard'): ?>
				<div class="epc-cm-kpi">
					<div class="kpi"><div class="lbl">Customers</div><div class="val"><?php echo (int)($dash['customers'] ?? 0); ?></div></div>
					<div class="kpi"><div class="lbl">Orders (30d)</div><div class="val"><?php echo (int)($dash['orders_30d'] ?? 0); ?></div></div>
					<div class="kpi"><div class="lbl">Open orders</div><div class="val"><?php echo (int)($dash['open_orders'] ?? 0); ?></div></div>
					<div class="kpi"><div class="lbl">Buyers with TRN</div><div class="val"><?php echo (int)($dash['buyers_with_trn'] ?? 0); ?></div></div>
					<div class="kpi"><div class="lbl">E-invoices</div><div class="val"><?php echo (int)($dash['einvoices'] ?? 0); ?></div></div>
					<div class="kpi"><div class="lbl">Returns</div><div class="val"><?php echo (int)($dash['returns'] ?? 0); ?></div></div>
					<div class="kpi"><div class="lbl">Customer ledger</div><div class="val"><?php echo epc_cm_money($dash['customer_ledger_balance'] ?? 0); ?></div></div>
				</div>
				<p>
					<a class="btn btn-primary btn-sm" href="<?php echo epc_cm_h(epc_cm_tab_url($cmUrl, 'customers')); ?>">Open customer directory</a>
					<a class="btn btn-default btn-sm" href="<?php echo epc_cm_h(epc_cm_tab_url($cmUrl, 'orders')); ?>">View orders</a>
					<a class="btn btn-default btn-sm" href="<?php echo epc_cm_h(epc_cm_tab_url($cmUrl, 'invoices')); ?>">Tax invoices</a>
				</p>

			<?php elseif ($tab === 'customers'): ?>
				<?php if ($customer): ?>
				<div class="epc-cm-toolbar">
					<div>
						<div class="epc-cm-name" style="font-size:18px;"><?php echo epc_cm_h($customer['display_name'] ?? ('Customer #' . (int)$view_user)); ?></div>
						<div class="epc-cm-sub">#<?php echo (int)$view_user; ?> · <?php echo epc_cm_h($customer['email'] ?? ''); ?> · <?php echo (int)($customer['order_count'] ?? 0); ?> orders</div>
					</div>
					<a class="btn btn-default btn-sm" href="<?php echo epc_cm_h(epc_cm_tab_url($cmUrl, 'customers')); ?>"><i class="fa fa-arrow-left"></i> Back to directory</a>
				</div>
				<form id="epc_cm_form_customer" class="form-horizontal" style="background:#fff;border:1px solid #e2e8f0;border-radius:14px;padding:16px 12px 8px;">
					<input type="hidden" name="csrf_guard_key" value="<?php echo epc_cm_h($csrf); ?>">
					<input type="hidden" name="user_id" value="<?php echo (int)$view_user; ?>">
					<div class="row">
						<div class="col-md-6">
							<div class="form-group"><label class="col-sm-4">Buyer name</label><div class="col-sm-8"><input type="text" name="buyer_name" class="form-control input-sm" value="<?php echo epc_cm_h($customer['buyer_name'] ?? trim(($customer['company'] ?? '') ?: (($customer['fname'] ?? '') . ' ' . ($customer['sname'] ?? '')))); ?>"></div></div>
							<div class="form-group"><label class="col-sm-4">TRN</label><div class="col-sm-8"><input type="text" name="trn" class="form-control input-sm" value="<?php echo epc_cm_h($customer['trn'] ?? ''); ?>"></div></div>
							<div class="form-group"><label class="col-sm-4">TIN (Peppol)</label><div class="col-sm-8"><input type="text" name="tin" class="form-control input-sm" value="<?php echo epc_cm_h($customer['tin'] ?? ''); ?>" placeholder="Auto from TRN"></div></div>
							<div class="form-group"><label class="col-sm-4">Legal reg no.</label><div class="col-sm-8"><input type="text" name="legal_reg_no" class="form-control input-sm" value="<?php echo epc_cm_h($customer['legal_reg_no'] ?? ''); ?>"></div></div>
							<div class="form-group"><label class="col-sm-4">Peppol endpoint</label><div class="col-sm-8"><input type="text" name="peppol_endpoint" class="form-control input-sm" value="<?php echo epc_cm_h($customer['peppol_endpoint'] ?? ''); ?>"></div></div>
							<div class="form-group"><label class="col-sm-4">Onboarded</label><div class="col-sm-8"><label class="checkbox-inline"><input type="checkbox" name="buyer_onboarded" value="1" <?php echo !empty($customer['buyer_onboarded']) ? 'checked' : ''; ?>> Peppol buyer</label></div></div>
						</div>
						<div class="col-md-6">
							<div class="form-group"><label class="col-sm-4">Company</label><div class="col-sm-8"><input type="text" name="company" class="form-control input-sm" value="<?php echo epc_cm_h($customer['company'] ?? ''); ?>"></div></div>
							<div class="form-group"><label class="col-sm-4">Address</label><div class="col-sm-8"><input type="text" name="address_line1" class="form-control input-sm" value="<?php echo epc_cm_h($customer['address_line1'] ?? ''); ?>"></div></div>
							<div class="form-group"><label class="col-sm-4">City</label><div class="col-sm-8"><input type="text" name="city" class="form-control input-sm" value="<?php echo epc_cm_h($customer['city'] ?? 'Dubai'); ?>"></div></div>
							<div class="form-group"><label class="col-sm-4">Emirate</label><div class="col-sm-8"><input type="text" name="emirate" class="form-control input-sm" value="<?php echo epc_cm_h($customer['emirate'] ?? 'Dubai'); ?>"></div></div>
							<div class="form-group"><label class="col-sm-4">Country</label><div class="col-sm-8"><input type="text" name="country_code" class="form-control input-sm" value="<?php echo epc_cm_h($customer['country_code'] ?? 'AE'); ?>"></div></div>
							<div class="form-group"><label class="col-sm-4">E-mail / phone</label><div class="col-sm-8">
								<input type="email" name="email" class="form-control input-sm" value="<?php echo epc_cm_h($customer['email'] ?? ''); ?>" readonly style="margin-bottom:4px;">
								<input type="text" name="phone" class="form-control input-sm" value="<?php echo epc_cm_h($customer['phone'] ?? ''); ?>">
							</div></div>
						</div>
					</div>
					<div style="padding:0 15px 12px;">
						<button type="submit" class="btn btn-primary">Save customer profile</button>
						<a class="btn btn-default" href="<?php echo epc_cm_h(epc_cm_tab_url($cmUrl, 'customers')); ?>">Cancel</a>
					</div>
				</form>
				<?php if (!empty($orders)): ?>
				<h4 style="margin-top:18px;">Recent orders</h4>
				<table class="table table-condensed table-bordered"><thead><tr><th>Order</th><th>Date</th><th>Paid</th><th>Ex VAT</th></tr></thead><tbody>
				<?php foreach (array_slice($orders, 0, 10) as $o): ?>
				<tr><td><a href="<?php echo epc_cm_h($ordersUrl); ?>?order_id=<?php echo (int)$o['id']; ?>">#<?php echo (int)$o['id']; ?></a></td>
				<td><?php echo epc_cm_h(date('Y-m-d', (int)$o['time'])); ?></td>
				<td><?php echo (int)$o['paid'] ? 'Yes' : 'No'; ?></td>
				<td><?php echo epc_cm_money($o['sale_ex'] ?? 0); ?></td></tr>
				<?php endforeach; ?>
				</tbody></table>
				<?php endif; ?>
				<?php elseif ($view_user > 0 && !$customer): ?>
					<div class="epc-cm-empty">
						<h4>Customer not found</h4>
						<p>No user matches #<?php echo (int)$view_user; ?>.</p>
						<a class="btn btn-default btn-sm" href="<?php echo epc_cm_h(epc_cm_tab_url($cmUrl, 'customers')); ?>">Back to directory</a>
					</div>
				<?php else: ?>
				<?php
				$cmPages = $cmTotal > 0 ? (int)ceil($cmTotal / $cmPerPage) : 1;
				if ($cmPage > $cmPages) {
					$cmPage = $cmPages;
				}
				?>
				<form method="get" class="epc-cm-toolbar">
					<input type="hidden" name="tab" value="customers">
					<div class="epc-cm-search">
						<i class="fa fa-search" style="color:#94a3b8;"></i>
						<input type="text" name="q" placeholder="Search email, phone, company, TRN, or ID" value="<?php echo epc_cm_h($search); ?>">
						<button type="submit" class="btn btn-sm btn-primary">Search</button>
						<?php if ($search !== ''): ?>
							<a class="btn btn-sm btn-default" href="<?php echo epc_cm_h(epc_cm_tab_url($cmUrl, 'customers')); ?>">Clear</a>
						<?php endif; ?>
					</div>
					<div class="epc-cm-meta">
						Showing <strong><?php echo count($customers); ?></strong> of <strong><?php echo (int)$cmTotal; ?></strong>
						<?php if ($search !== ''): ?> for “<?php echo epc_cm_h($search); ?>”<?php endif; ?>
					</div>
				</form>
				<?php if ($cmPages > 1): ?>
				<ul class="pagination pagination-sm" style="margin-top:0;">
					<?php for ($p = 1; $p <= $cmPages && $p <= 20; $p++): ?>
						<li class="<?php echo $p === $cmPage ? 'active' : ''; ?>">
							<a href="<?php echo epc_cm_h(epc_cm_tab_url($cmUrl, 'customers', 'page=' . $p . ($search !== '' ? '&q=' . urlencode($search) : ''))); ?>"><?php echo $p; ?></a>
						</li>
					<?php endfor; ?>
					<?php if ($cmPages > 20): ?><li class="disabled"><span>…</span></li><?php endif; ?>
				</ul>
				<?php endif; ?>

				<?php if (empty($customers)): ?>
					<div class="epc-cm-empty">
						<h4>No customers found</h4>
						<p><?php echo $search !== '' ? 'Try a different search.' : 'Registered shop customers will appear here.'; ?></p>
					</div>
				<?php else: ?>
				<div class="epc-cm-dir">
					<?php foreach ($customers as $c):
						$openUrl = epc_cm_tab_url($cmUrl, 'customers', 'user_id=' . (int)$c['user_id']);
						$regTs = (int)($c['time_registered'] ?? 0);
						$regLabel = $regTs > 0 ? date('Y-m-d', $regTs) : '—';
						$trn = trim((string)($c['trn'] ?? ''));
						$hasTrn = $trn !== '';
						$onboarded = !empty($c['buyer_onboarded']);
					?>
						<a class="epc-cm-row" href="<?php echo epc_cm_h($openUrl); ?>">
							<div class="epc-cm-av"><?php echo epc_cm_h(epc_cm_customer_initials($c)); ?></div>
							<div>
								<div class="epc-cm-name"><?php echo epc_cm_h($c['display_name'] ?? epc_cm_customer_display_name($c)); ?></div>
								<div class="epc-cm-sub"><?php echo epc_cm_h($c['email'] ?: ('User #' . (int)$c['user_id'])); ?><?php if (!empty($c['phone'])): ?> · <?php echo epc_cm_h($c['phone']); ?><?php endif; ?></div>
							</div>
							<div>
								<?php if ($hasTrn): ?>
									<span class="epc-cm-chip">TRN <?php echo epc_cm_h($trn); ?></span>
								<?php else: ?>
									<span class="epc-cm-chip is-muted">No TRN</span>
								<?php endif; ?>
								<div class="epc-cm-sub" style="margin-top:4px;">
									<?php echo $onboarded ? '<span class="epc-cm-chip">Peppol</span>' : '<span class="epc-cm-chip is-warn">Profile incomplete</span>'; ?>
								</div>
							</div>
							<div class="epc-cm-stat"><strong><?php echo (int)$c['order_count']; ?></strong>orders</div>
							<div class="epc-cm-stat"><strong>#<?php echo (int)$c['user_id']; ?></strong>joined <?php echo epc_cm_h($regLabel); ?></div>
							<div style="text-align:right;"><span class="btn btn-xs btn-primary">Open</span></div>
						</a>
					<?php endforeach; ?>
				</div>
				<?php endif; ?>
				<?php endif; ?>

			<?php elseif ($tab === 'orders'): ?>
				<table class="table table-striped table-bordered table-condensed">
					<thead><tr><th>Order</th><th>Date</th><th>Customer</th><th>Paid</th><th>Ex VAT</th><th></th></tr></thead>
					<tbody>
					<?php foreach ($orders as $o): ?>
						<tr>
							<td>#<?php echo (int)$o['id']; ?></td>
							<td><?php echo epc_cm_h(date('Y-m-d H:i', (int)$o['time'])); ?></td>
							<td><?php echo epc_cm_h($o['email'] ?? ('User #' . (int)$o['user_id'])); ?></td>
							<td><?php echo (int)$o['paid'] ? '<span class="label label-success">Paid</span>' : '<span class="label label-warning">Open</span>'; ?></td>
							<td><?php echo epc_cm_money($o['sale_ex'] ?? 0); ?></td>
							<td><a class="btn btn-xs btn-default" href="<?php echo epc_cm_h($ordersUrl); ?>?order_id=<?php echo (int)$o['id']; ?>">Open</a></td>
						</tr>
					<?php endforeach; ?>
					</tbody>
				</table>

			<?php elseif ($tab === 'invoices'): ?>
				<p class="text-muted">UAE e-invoices (PINT-AE). Generate from completed orders; full workflow in <a href="<?php echo epc_cm_h($erpUrl); ?>?tab=einvoice">ERP E-Invoicing</a>.</p>
				<table class="table table-striped table-bordered table-condensed">
					<thead><tr><th>Invoice #</th><th>Date</th><th>Customer</th><th>Total</th><th>Status</th><th></th></tr></thead>
					<tbody>
					<?php foreach ($einvoices as $d): ?>
						<tr>
							<td><?php echo epc_cm_h($d['invoice_number']); ?></td>
							<td><?php echo epc_cm_h($d['issue_date']); ?></td>
							<td><?php echo epc_cm_h($d['email'] ?? ('#' . (int)$d['user_id'])); ?></td>
							<td><?php echo epc_cm_money($d['total_with_tax'] ?? 0); ?></td>
							<td><?php echo epc_cm_h($d['status']); ?></td>
							<td><a class="btn btn-xs btn-default" href="<?php echo epc_cm_h($erpUrl); ?>?tab=einvoice&einv_section=view&einv_doc=<?php echo (int)$d['id']; ?>">View</a></td>
						</tr>
					<?php endforeach; ?>
					</tbody>
				</table>
				<h4>Generate e-invoice from order</h4>
				<form id="epc_cm_form_einvoice" class="form-inline">
					<input type="hidden" name="csrf_guard_key" value="<?php echo epc_cm_h($csrf); ?>">
					<input type="number" name="order_id" class="form-control input-sm" placeholder="Order ID" required>
					<button type="submit" class="btn btn-sm btn-primary">Generate</button>
				</form>

			<?php elseif ($tab === 'advances'): ?>
				<p class="text-muted">Customer advance payments (prepaid balance). Also post via <a href="<?php echo epc_cm_h($financeOpsUrl); ?>">Finance operations</a>.</p>
				<table class="table table-striped table-bordered table-condensed">
					<thead><tr><th>Date</th><th>Customer</th><th>Amount</th><th>Reference</th></tr></thead>
					<tbody>
					<?php foreach ($advances_list as $a): ?>
						<tr>
							<td><?php echo epc_cm_h(date('Y-m-d', (int)$a['time'])); ?></td>
							<td><?php echo epc_cm_h($a['email'] ?? ('#' . (int)$a['user_id'])); ?></td>
							<td><?php echo epc_cm_money($a['amount']); ?></td>
							<td><?php echo epc_cm_h($a['reference'] ?? ''); ?></td>
						</tr>
					<?php endforeach; ?>
					</tbody>
				</table>
				<h4>Record customer advance</h4>
				<form id="epc_cm_form_advance" class="form-inline">
					<input type="hidden" name="csrf_guard_key" value="<?php echo epc_cm_h($csrf); ?>">
					<input type="number" name="user_id" class="form-control input-sm" placeholder="Customer user ID" required>
					<input type="number" step="0.01" name="amount" class="form-control input-sm" placeholder="Amount AED" required>
					<input type="text" name="reference" class="form-control input-sm" placeholder="Reference">
					<input type="text" name="note" class="form-control input-sm" placeholder="Note">
					<label class="checkbox-inline"><input type="checkbox" name="post_gl" value="1"> Post GL</label>
					<button type="submit" class="btn btn-sm btn-warning">Record advance</button>
				</form>

			<?php elseif ($tab === 'returns'): ?>
				<table class="table table-striped table-bordered table-condensed">
					<thead><tr><th>Return ID</th><th>Order</th><th>Customer</th><th>Status</th></tr></thead>
					<tbody>
					<?php if (empty($returns)): ?>
						<tr><td colspan="4" class="text-muted">No returns found or returns module not installed.</td></tr>
					<?php else: foreach ($returns as $r): ?>
						<tr>
							<td><?php echo (int)$r['id']; ?></td>
							<td>#<?php echo (int)$r['order_id']; ?></td>
							<td>#<?php echo (int)($r['user_id'] ?? 0); ?></td>
							<td><?php echo epc_cm_h($r['status'] ?? '—'); ?></td>
						</tr>
					<?php endforeach; endif; ?>
					</tbody>
				</table>
				<p><a class="btn btn-default btn-sm" href="<?php echo epc_cm_h($ordersUrl); ?>">Process returns in Orders</a></p>

			<?php elseif ($tab === 'approvals'): ?>
				<p>Retail/wholesale registration approvals and dealing currency assignment.</p>
				<p><a class="btn btn-primary" href="<?php echo epc_cm_h($approvalsUrl); ?>"><i class="fa fa-user-check"></i> Open customer approvals</a></p>

			<?php elseif ($tab === 'guide'): ?>
				<?php require __DIR__ . '/customer_mgmt_guide.php'; ?>
			<?php endif; ?>

		</div>
	</div>
</div>

<script>
(function(){
	var postUrl = <?php echo json_encode($cmAjaxUrl); ?>;
	var msgEl = document.getElementById('epc_cm_msg');
	function showMsg(ok, text) {
		if (!msgEl) return;
		msgEl.className = 'alert epc-cm-msg ' + (ok ? 'alert-success' : 'alert-danger');
		msgEl.textContent = text;
		msgEl.style.display = 'block';
	}
	function parseJsonResponse(r) {
		return r.text().then(function(t) {
			try { return JSON.parse(t); }
			catch (e) { throw new Error('Invalid JSON (HTTP ' + r.status + '). Refresh and try again.'); }
		});
	}
	function postAction(action, form) {
		var fd = new FormData(form);
		fd.append('action', action);
		return fetch(postUrl, { method: 'POST', body: fd, credentials: 'same-origin' })
			.then(parseJsonResponse)
			.then(function(j){
				showMsg(!!j.status, j.message || (j.status ? 'OK' : 'Error'));
				if (j.status && j.redirect) { window.location = j.redirect; return; }
				if (j.status) setTimeout(function(){ location.reload(); }, 800);
			})
			.catch(function(e){ showMsg(false, e.message || 'Request failed'); });
	}
	function bindForm(id, action) {
		var f = document.getElementById(id);
		if (!f) return;
		f.addEventListener('submit', function(ev){ ev.preventDefault(); postAction(action, f); });
	}
	bindForm('epc_cm_form_customer', 'save_customer');
	bindForm('epc_cm_form_advance', 'customer_advance');
	bindForm('epc_cm_form_einvoice', 'einvoice_create');
})();
</script>
