<?php
/**
 * CP — Procurement & supplier management (separate from warehouse/stock).
 * URL: /cp/shop/procurement/procurement
 */
defined('_ASTEXE_') or die('No access');

require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/procurement/epc_procurement_helpers.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_fulfilment.php';

if ($_SERVER['REQUEST_METHOD'] === 'POST' && !empty($_POST['action'])) {
	require __DIR__ . '/ajax_procurement.php';
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

epc_procurement_ensure_schema($db_link);

$backend = (string)$DP_Config->backend_dir;
$procUrl = '/' . $backend . '/shop/procurement/procurement';
$procAjaxUrl = '/' . $backend . '/content/shop/procurement/ajax_procurement_endpoint.php';
$erpUrl = '/' . $backend . '/shop/finance/erp';
$ordersUrl = '/' . $backend . '/shop/order_process/orders';
$storagesUrl = '/' . $backend . '/shop/logistics/storages';
$priceUrl = '/' . $backend . '/shop/pricing/price_management';
$guideUrl = '/' . $backend . '/shop/procurement/procurement_guide';

$tabs = array(
	'dashboard' => 'Dashboard',
	'suppliers' => 'Suppliers',
	'purchases' => 'Purchase bills',
	'payments' => 'Payments',
	'advances' => 'Advances',
	'fulfillment' => 'Fulfillment',
	'warehouses' => 'Warehouses',
	'guide' => 'Guide',
);
$tab = isset($_GET['tab']) ? (string)$_GET['tab'] : 'dashboard';
if (!isset($tabs[$tab])) {
	$tab = 'dashboard';
}

$csrf = isset($user_session['csrf_guard_key']) ? (string)$user_session['csrf_guard_key'] : '';
$dash = epc_procurement_dashboard($db_link);
$suppliers = in_array($tab, array('suppliers', 'purchases', 'payments', 'advances', 'fulfillment'), true)
	? epc_procurement_list_suppliers($db_link) : array();
$accounts = in_array($tab, array('payments', 'advances'), true)
	? epc_erp_list_cash_accounts($db_link) : array();
$purchases = $tab === 'purchases' ? epc_erp_list_purchases($db_link) : array();
$advances = $tab === 'advances' ? epc_procurement_list_advances($db_link) : array();
$warehouses = $tab === 'warehouses' ? epc_procurement_list_warehouses($db_link) : array();
$view_sup = isset($_GET['supplier_id']) ? (int)$_GET['supplier_id'] : 0;
$edit_sup = $view_sup > 0 ? epc_procurement_get_supplier($db_link, $view_sup) : null;
$ff_summary = $tab === 'fulfillment'
	? epc_erp_fulfilment_summary_light($db_link, strtotime('-30 days'), time())
	: array('total_orders' => 0, 'pipeline' => array());
$vatRate = epc_uae_vat_rate_percent($db_link);
?>

<link rel="stylesheet" href="/content/shop/finance/epc_erp_ui.css?v=20260518">
<style>
.epc-proc-hero { background: linear-gradient(135deg, #0f172a 0%, #1e4d3a 100%); color: #fff; border-radius: 10px; padding: 20px 22px; margin-bottom: 16px; }
.epc-proc-hero h3 { margin: 0 0 8px; color: #fff; }
.epc-proc-kpi { display: flex; flex-wrap: wrap; gap: 12px; margin-bottom: 16px; }
.epc-proc-kpi .kpi { flex: 1 1 120px; min-width: 100px; background: #f8fafc; border: 1px solid #e2e8f0; border-radius: 8px; padding: 12px; }
.epc-proc-kpi .lbl { font-size: 10px; color: #64748b; text-transform: uppercase; }
.epc-proc-kpi .val { font-size: 18px; font-weight: 700; color: #1e4d3a; }
.epc-proc-nav .btn { margin: 0 4px 6px 0; }
.epc-proc-msg { display: none; margin: 10px 0; }
.epc-proc-note { border-left: 4px solid #f59e0b; padding: 10px 14px; background: #fffbeb; margin-bottom: 14px; border-radius: 0 6px 6px 0; }
</style>

<div class="col-lg-12 epc-erp-shell">
	<div class="hpanel">
		<div class="panel-heading hbuilt">
			<i class="fa fa-industry"></i> Procurement &amp; suppliers
			<span class="pull-right">
				<a class="btn btn-default btn-xs" href="<?php echo epc_proc_h($erpUrl); ?>?tab=payables"><i class="fa fa-calculator"></i> ERP payables</a>
				<a class="btn btn-default btn-xs" href="<?php echo epc_proc_h($priceUrl); ?>"><i class="fa fa-tags"></i> Price lists</a>
			</span>
		</div>
		<div class="panel-body">

			<div class="epc-proc-hero">
				<h3><i class="fa fa-truck"></i> Supplier procurement — not warehouse stock</h3>
				<p style="margin:0;opacity:.92;">
					Manage <strong>suppliers</strong> (legal entity, TRN, payment terms), <strong>purchase bills</strong>,
					<strong>payments</strong>, and <strong>advances</strong>. Warehouse/storages hold stock and price lists — link optionally here.
				</p>
			</div>

			<div id="epc_proc_msg" class="alert epc-proc-msg"></div>

			<div class="epc-proc-nav">
				<?php foreach ($tabs as $k => $lbl): ?>
					<a class="btn btn-sm <?php echo $tab === $k ? 'btn-primary' : 'btn-default'; ?>"
					   href="<?php echo epc_proc_h(epc_procurement_tab_url($procUrl, $k)); ?>"><?php echo epc_proc_h($lbl); ?></a>
				<?php endforeach; ?>
			</div>

			<?php if ($tab === 'dashboard'): ?>
				<div class="epc-proc-kpi">
					<div class="kpi"><div class="lbl">Active suppliers</div><div class="val"><?php echo (int)$dash['suppliers']; ?></div></div>
					<div class="kpi"><div class="lbl">With TRN</div><div class="val"><?php echo (int)$dash['suppliers_with_trn']; ?></div></div>
					<div class="kpi"><div class="lbl">Purchase bills</div><div class="val"><?php echo (int)$dash['purchase_invoices']; ?></div></div>
					<div class="kpi"><div class="lbl">Payable balance</div><div class="val"><?php echo epc_proc_money($dash['payable_balance']); ?></div></div>
					<div class="kpi"><div class="lbl">Advances paid</div><div class="val"><?php echo epc_proc_money($dash['advances_paid']); ?></div></div>
					<div class="kpi"><div class="lbl">Warehouses</div><div class="val"><?php echo (int)$dash['warehouses']; ?> <small>(<?php echo (int)$dash['warehouses_linked']; ?> linked)</small></div></div>
				</div>
				<div class="epc-proc-note">
					<strong>Warehouse ≠ supplier.</strong> Warehouses (<a href="<?php echo epc_proc_h($storagesUrl); ?>">Logistics → Storages</a>) are operational price/stock sources.
					Suppliers are financial/legal entities for purchase bills and UAE input VAT. Optional link: one supplier per warehouse for auto-naming.
				</div>
				<h4>Quick actions</h4>
				<p>
					<a class="btn btn-primary btn-sm" href="<?php echo epc_proc_h(epc_procurement_tab_url($procUrl, 'suppliers')); ?>">Add supplier</a>
					<a class="btn btn-default btn-sm" href="<?php echo epc_proc_h(epc_procurement_tab_url($procUrl, 'purchases')); ?>">Record purchase bill</a>
					<a class="btn btn-default btn-sm" href="<?php echo epc_proc_h(epc_procurement_tab_url($procUrl, 'payments')); ?>">Pay supplier</a>
					<a class="btn btn-default btn-sm" href="<?php echo epc_proc_h(epc_procurement_tab_url($procUrl, 'fulfillment')); ?>">Order fulfillment</a>
				</p>

			<?php elseif ($tab === 'suppliers'): ?>
				<div class="epc-proc-note">
					Complete supplier profile for UAE purchase VAT (TRN, country AE, legal registration, address).
					Prices come from linked warehouse price list — configure in <a href="<?php echo epc_proc_h($priceUrl); ?>">Price management</a>.
				</div>
				<p>
					<button type="button" class="btn btn-sm btn-default" id="epc_proc_sync_wh"><i class="fa fa-link"></i> Link warehouses as suppliers (names only)</button>
				</p>
				<table class="table table-striped table-bordered table-condensed">
					<thead><tr><th>Supplier</th><th>TRN</th><th>Country</th><th>Warehouse link</th><th>Payable</th><th></th></tr></thead>
					<tbody>
					<?php foreach ($suppliers as $s): ?>
						<tr>
							<td><?php echo epc_proc_h($s['name']); ?></td>
							<td><?php echo epc_proc_h($s['trn'] ?: '—'); ?></td>
							<td><?php echo epc_proc_h($s['country_code'] ?? 'AE'); ?>
								<?php if (!empty($s['vat_registered'])): ?><span class="label label-success">VAT</span><?php endif; ?>
							</td>
							<td><?php echo epc_proc_h($s['warehouse_name'] ?: '—'); ?></td>
							<td><?php echo epc_proc_money($s['balance']); ?></td>
							<td><a class="btn btn-xs btn-primary" href="<?php echo epc_proc_h(epc_procurement_tab_url($procUrl, 'suppliers', 'supplier_id=' . (int)$s['id'])); ?>">Edit</a></td>
						</tr>
					<?php endforeach; ?>
					</tbody>
				</table>

				<?php if ($edit_sup): ?>
				<h4>Edit supplier #<?php echo (int)$edit_sup['id']; ?></h4>
				<form id="epc_proc_form_update_sup" class="form-horizontal">
					<input type="hidden" name="csrf_guard_key" value="<?php echo epc_proc_h($csrf); ?>">
					<input type="hidden" name="supplier_id" value="<?php echo (int)$edit_sup['id']; ?>">
					<div class="row">
						<div class="col-md-6">
							<div class="form-group"><label class="col-sm-4">Legal name</label><div class="col-sm-8"><input type="text" name="name" class="form-control input-sm" value="<?php echo epc_proc_h($edit_sup['name']); ?>" required></div></div>
							<div class="form-group"><label class="col-sm-4">TRN (VAT)</label><div class="col-sm-8"><input type="text" name="trn" class="form-control input-sm" value="<?php echo epc_proc_h($edit_sup['trn']); ?>"></div></div>
							<div class="form-group"><label class="col-sm-4">Country</label><div class="col-sm-8"><input type="text" name="country_code" class="form-control input-sm" value="<?php echo epc_proc_h($edit_sup['country_code'] ?? 'AE'); ?>"></div></div>
							<div class="form-group"><label class="col-sm-4">VAT registered</label><div class="col-sm-8"><label class="checkbox-inline"><input type="checkbox" name="vat_registered" value="1" <?php echo !empty($edit_sup['vat_registered']) ? 'checked' : ''; ?>> UAE 5% input VAT</label></div></div>
							<div class="form-group"><label class="col-sm-4">Legal reg no.</label><div class="col-sm-8"><input type="text" name="legal_reg_no" class="form-control input-sm" value="<?php echo epc_proc_h($edit_sup['legal_reg_no'] ?? ''); ?>"></div></div>
							<div class="form-group"><label class="col-sm-4">Reg type</label><div class="col-sm-8">
								<select name="legal_reg_type" class="form-control input-sm">
									<?php foreach (array('TL' => 'Trade licence', 'EID' => 'Emirates ID', 'PAS' => 'Passport', 'CD' => 'Other') as $k => $lbl): ?>
									<option value="<?php echo epc_proc_h($k); ?>" <?php echo ($edit_sup['legal_reg_type'] ?? 'TL') === $k ? 'selected' : ''; ?>><?php echo epc_proc_h($lbl); ?></option>
									<?php endforeach; ?>
								</select>
							</div></div>
							<div class="form-group"><label class="col-sm-4">Authority</label><div class="col-sm-8"><input type="text" name="authority_name" class="form-control input-sm" value="<?php echo epc_proc_h($edit_sup['authority_name'] ?? ''); ?>"></div></div>
						</div>
						<div class="col-md-6">
							<div class="form-group"><label class="col-sm-4">Address</label><div class="col-sm-8"><input type="text" name="address_line1" class="form-control input-sm" value="<?php echo epc_proc_h($edit_sup['address_line1'] ?? ''); ?>"></div></div>
							<div class="form-group"><label class="col-sm-4">City</label><div class="col-sm-8"><input type="text" name="city" class="form-control input-sm" value="<?php echo epc_proc_h($edit_sup['city'] ?? 'Dubai'); ?>"></div></div>
							<div class="form-group"><label class="col-sm-4">Emirate</label><div class="col-sm-8"><input type="text" name="emirate" class="form-control input-sm" value="<?php echo epc_proc_h($edit_sup['emirate'] ?? 'Dubai'); ?>"></div></div>
							<div class="form-group"><label class="col-sm-4">E-mail</label><div class="col-sm-8"><input type="email" name="contact_email" class="form-control input-sm" value="<?php echo epc_proc_h($edit_sup['contact_email'] ?? ''); ?>"></div></div>
							<div class="form-group"><label class="col-sm-4">Phone</label><div class="col-sm-8"><input type="text" name="contact_phone" class="form-control input-sm" value="<?php echo epc_proc_h($edit_sup['contact_phone'] ?? ''); ?>"></div></div>
							<div class="form-group"><label class="col-sm-4">Payment terms</label><div class="col-sm-8"><input type="text" name="payment_terms" class="form-control input-sm" value="<?php echo epc_proc_h($edit_sup['payment_terms'] ?? ''); ?>" placeholder="Net 30, advance, etc."></div></div>
							<div class="form-group"><label class="col-sm-4">Link warehouse</label><div class="col-sm-8">
								<select name="storage_id" class="form-control input-sm">
									<option value="">— None —</option>
									<?php foreach (epc_procurement_list_warehouses($db_link) as $w): ?>
									<option value="<?php echo (int)$w['id']; ?>" <?php echo (int)($edit_sup['storage_id'] ?? 0) === (int)$w['id'] ? 'selected' : ''; ?>><?php echo epc_proc_h($w['name']); ?></option>
									<?php endforeach; ?>
								</select>
							</div></div>
							<div class="form-group"><label class="col-sm-4">Notes</label><div class="col-sm-8"><textarea name="notes" class="form-control input-sm" rows="2"><?php echo epc_proc_h($edit_sup['notes'] ?? ''); ?></textarea></div></div>
						</div>
					</div>
					<button type="submit" class="btn btn-primary">Save supplier profile</button>
				</form>
				<?php else: ?>
				<h4>Add supplier</h4>
				<form id="epc_proc_form_supplier" class="form-inline">
					<input type="hidden" name="csrf_guard_key" value="<?php echo epc_proc_h($csrf); ?>">
					<input type="text" name="name" class="form-control input-sm" placeholder="Supplier legal name" required>
					<input type="text" name="trn" class="form-control input-sm" placeholder="TRN">
					<input type="text" name="country_code" class="form-control input-sm" value="AE" placeholder="Country">
					<input type="email" name="contact_email" class="form-control input-sm" placeholder="E-mail">
					<label class="checkbox-inline"><input type="checkbox" name="vat_registered" value="1" checked> VAT reg.</label>
					<button type="submit" class="btn btn-sm btn-primary">Create</button>
				</form>
				<?php endif; ?>

			<?php elseif ($tab === 'purchases'): ?>
				<p class="text-muted">Record supplier purchase bills (ex VAT). <?php echo epc_proc_h(number_format($vatRate, 2)); ?>% input VAT auto for UAE VAT-registered suppliers.</p>
				<table class="table table-striped table-bordered table-condensed">
					<thead><tr><th>ID</th><th>Date</th><th>Supplier</th><th>Invoice #</th><th>Order</th><th>Ex VAT</th><th>VAT</th><th>Total</th></tr></thead>
					<tbody>
					<?php foreach ($purchases as $p): ?>
						<tr>
							<td><?php echo (int)$p['id']; ?></td>
							<td><?php echo epc_proc_h(date('Y-m-d', (int)$p['purchase_date'])); ?></td>
							<td><?php echo epc_proc_h($p['supplier_name']); ?></td>
							<td><?php echo epc_proc_h($p['invoice_number']); ?></td>
							<td><?php echo (int)$p['order_id'] ? ('#' . (int)$p['order_id']) : '—'; ?></td>
							<td><?php echo epc_proc_money($p['amount_ex_vat']); ?></td>
							<td><?php echo epc_proc_money($p['vat_amount']); ?></td>
							<td><?php echo epc_proc_money($p['total_amount']); ?></td>
						</tr>
					<?php endforeach; ?>
					</tbody>
				</table>
				<h4>Record purchase bill</h4>
				<form id="epc_proc_form_purchase" class="form-inline">
					<input type="hidden" name="csrf_guard_key" value="<?php echo epc_proc_h($csrf); ?>">
					<select name="supplier_id" class="form-control input-sm" required><option value="">Supplier</option>
					<?php foreach ($suppliers as $s): ?><option value="<?php echo (int)$s['id']; ?>"><?php echo epc_proc_h($s['name']); ?></option><?php endforeach; ?>
					</select>
					<input type="text" name="invoice_number" class="form-control input-sm" placeholder="Supplier invoice #">
					<input type="number" step="0.01" name="amount_ex_vat" class="form-control input-sm" placeholder="Amount ex VAT" required>
					<input type="number" name="order_id" class="form-control input-sm" placeholder="Order ID (opt.)">
					<input type="text" name="note" class="form-control input-sm" placeholder="Note">
					<button type="submit" class="btn btn-sm btn-primary">Record bill</button>
				</form>
				<h4>From order cost</h4>
				<p class="text-muted" style="font-size:12px;">Creates ERP purchase bill and receives inventory from order line articles (qty from <code>count_need</code>, cost from <code>t2_price_purchase</code>).</p>
				<form id="epc_proc_form_purchase_order" class="form-inline">
					<input type="hidden" name="csrf_guard_key" value="<?php echo epc_proc_h($csrf); ?>">
					<input type="number" name="order_id" class="form-control input-sm" placeholder="Order ID" required>
					<select name="supplier_id" class="form-control input-sm" required><option value="">Supplier</option>
					<?php foreach ($suppliers as $s): ?><option value="<?php echo (int)$s['id']; ?>"><?php echo epc_proc_h($s['name']); ?></option><?php endforeach; ?>
					</select>
					<button type="submit" class="btn btn-sm btn-default">Generate bill</button>
				</form>

			<?php elseif ($tab === 'payments'): ?>
				<table class="table table-striped table-bordered table-condensed">
					<thead><tr><th>Supplier</th><th>TRN</th><th>Payable balance</th><th></th></tr></thead>
					<tbody>
					<?php foreach ($suppliers as $s): ?>
						<tr>
							<td><?php echo epc_proc_h($s['name']); ?></td>
							<td><?php echo epc_proc_h($s['trn'] ?: '—'); ?></td>
							<td><strong><?php echo epc_proc_money($s['balance']); ?></strong></td>
							<td><a class="btn btn-xs btn-default" href="<?php echo epc_proc_h($erpUrl); ?>?tab=payables&supplier_id=<?php echo (int)$s['id']; ?>">Ledger</a></td>
						</tr>
					<?php endforeach; ?>
					</tbody>
				</table>
				<h4>Record supplier payment</h4>
				<form id="epc_proc_form_pay" class="form-inline">
					<input type="hidden" name="csrf_guard_key" value="<?php echo epc_proc_h($csrf); ?>">
					<select name="supplier_id" class="form-control input-sm" required><option value="">Supplier</option>
					<?php foreach ($suppliers as $s): ?><option value="<?php echo (int)$s['id']; ?>"><?php echo epc_proc_h($s['name']); ?></option><?php endforeach; ?>
					</select>
					<select name="account_id" class="form-control input-sm" required><option value="">Pay from</option>
					<?php foreach ($accounts as $a): ?><option value="<?php echo (int)$a['id']; ?>"><?php echo epc_proc_h($a['name']); ?> (<?php echo epc_proc_money($a['balance']); ?>)</option><?php endforeach; ?>
					</select>
					<input type="number" step="0.01" name="amount" class="form-control input-sm" placeholder="Amount AED" required>
					<input type="text" name="reference" class="form-control input-sm" placeholder="Reference">
					<button type="submit" class="btn btn-sm btn-warning">Post payment</button>
				</form>
				<p class="text-muted">For advance payments before goods received, use the <a href="<?php echo epc_proc_h(epc_procurement_tab_url($procUrl, 'advances')); ?>">Advances</a> tab.</p>

			<?php elseif ($tab === 'advances'): ?>
				<p class="text-muted">Advance payments to suppliers (pre-payment before invoice). Recorded as supplier payment + procurement advance log.</p>
				<table class="table table-striped table-bordered table-condensed">
					<thead><tr><th>Date</th><th>Supplier</th><th>Amount</th><th>Reference</th><th>Note</th></tr></thead>
					<tbody>
					<?php foreach ($advances as $a): ?>
						<tr>
							<td><?php echo epc_proc_h(date('Y-m-d H:i', (int)$a['time'])); ?></td>
							<td><?php echo epc_proc_h($a['supplier_name']); ?></td>
							<td><?php echo epc_proc_money($a['amount']); ?></td>
							<td><?php echo epc_proc_h($a['reference']); ?></td>
							<td><?php echo epc_proc_h($a['note']); ?></td>
						</tr>
					<?php endforeach; ?>
					</tbody>
				</table>
				<form id="epc_proc_form_advance" class="form-inline">
					<input type="hidden" name="csrf_guard_key" value="<?php echo epc_proc_h($csrf); ?>">
					<select name="supplier_id" class="form-control input-sm" required><option value="">Supplier</option>
					<?php foreach ($suppliers as $s): ?><option value="<?php echo (int)$s['id']; ?>"><?php echo epc_proc_h($s['name']); ?></option><?php endforeach; ?>
					</select>
					<select name="account_id" class="form-control input-sm" required><option value="">Pay from</option>
					<?php foreach ($accounts as $a): ?><option value="<?php echo (int)$a['id']; ?>"><?php echo epc_proc_money($a['balance']); ?> — <?php echo epc_proc_h($a['name']); ?></option><?php endforeach; ?>
					</select>
					<input type="number" step="0.01" name="amount" class="form-control input-sm" placeholder="Advance AED" required>
					<input type="text" name="reference" class="form-control input-sm" placeholder="Reference">
					<input type="text" name="note" class="form-control input-sm" placeholder="Note">
					<button type="submit" class="btn btn-sm btn-warning">Record advance</button>
				</form>

			<?php elseif ($tab === 'fulfillment'): ?>
				<p>Track supplier → stock → customer delivery. Full pipeline in ERP Fulfilment tab.</p>
				<div class="epc-proc-kpi">
					<div class="kpi"><div class="lbl">Orders (30d)</div><div class="val"><?php echo (int)$ff_summary['total_orders']; ?></div></div>
					<?php if (!empty($ff_summary['pipeline'])): foreach ($ff_summary['pipeline'] as $pk => $pv): ?>
					<div class="kpi"><div class="lbl"><?php echo epc_proc_h(str_replace('_', ' ', $pk)); ?></div><div class="val"><?php echo (int)$pv; ?></div></div>
					<?php endforeach; endif; ?>
				</div>
				<p>
					<a class="btn btn-primary btn-sm" href="<?php echo epc_proc_h($erpUrl); ?>?tab=fulfilment"><i class="fa fa-exchange"></i> ERP Fulfilment</a>
					<a class="btn btn-default btn-sm" href="<?php echo epc_proc_h($ordersUrl); ?>"><i class="fa fa-shopping-cart"></i> Orders</a>
				</p>

			<?php elseif ($tab === 'warehouses'): ?>
				<div class="epc-proc-note">
					<strong>Warehouses are not suppliers.</strong> They store price lists and stock locations.
					Configure prices in <a href="<?php echo epc_proc_h($priceUrl); ?>">Price management</a>.
					Optionally link a warehouse to a supplier record for naming convenience only.
				</div>
				<table class="table table-striped table-bordered table-condensed">
					<thead><tr><th>Warehouse</th><th>Linked supplier</th><th></th></tr></thead>
					<tbody>
					<?php foreach ($warehouses as $w): ?>
						<tr>
							<td><?php echo epc_proc_h($w['name']); ?> <small class="text-muted">#<?php echo (int)$w['id']; ?></small></td>
							<td><?php echo (int)$w['supplier_id'] ? ('Supplier #' . (int)$w['supplier_id']) : '—'; ?></td>
							<td>
								<a class="btn btn-xs btn-default" href="<?php echo epc_proc_h($storagesUrl); ?>">Open storages</a>
								<?php if ((int)$w['supplier_id']): ?>
								<a class="btn btn-xs btn-primary" href="<?php echo epc_proc_h(epc_procurement_tab_url($procUrl, 'suppliers', 'supplier_id=' . (int)$w['supplier_id'])); ?>">Edit supplier</a>
								<?php endif; ?>
							</td>
						</tr>
					<?php endforeach; ?>
					</tbody>
				</table>

			<?php elseif ($tab === 'guide'): ?>
				<?php require __DIR__ . '/procurement_guide.php'; ?>
			<?php endif; ?>

		</div>
	</div>
</div>

<script>
(function(){
	var postUrl = <?php echo json_encode($procAjaxUrl); ?>;
	var msgEl = document.getElementById('epc_proc_msg');
	function showMsg(ok, text) {
		if (!msgEl) return;
		msgEl.className = 'alert epc-proc-msg ' + (ok ? 'alert-success' : 'alert-danger');
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
		var fd = new FormData(form || undefined);
		if (form) fd = new FormData(form);
		else fd = new FormData();
		fd.append('action', action);
		var csrf = document.querySelector('input[name="csrf_guard_key"]');
		if (csrf && !fd.has('csrf_guard_key')) fd.append('csrf_guard_key', csrf.value);
		return fetch(postUrl, { method: 'POST', body: fd, credentials: 'same-origin' })
			.then(parseJsonResponse)
			.then(function(j){
				showMsg(!!j.status, j.message || (j.status ? 'OK' : 'Error'));
				if (j.status) setTimeout(function(){ location.reload(); }, 800);
			})
			.catch(function(e){ showMsg(false, e.message || 'Request failed'); });
	}
	function bindForm(id, action) {
		var f = document.getElementById(id);
		if (!f) return;
		f.addEventListener('submit', function(ev){ ev.preventDefault(); postAction(action, f); });
	}
	bindForm('epc_proc_form_supplier', 'create_supplier');
	bindForm('epc_proc_form_update_sup', 'update_supplier');
	bindForm('epc_proc_form_purchase', 'create_purchase');
	bindForm('epc_proc_form_purchase_order', 'purchase_from_order');
	bindForm('epc_proc_form_pay', 'supplier_payment');
	bindForm('epc_proc_form_advance', 'record_advance');
	var syncBtn = document.getElementById('epc_proc_sync_wh');
	if (syncBtn) syncBtn.addEventListener('click', function(){ postAction('sync_suppliers'); });
})();
</script>
