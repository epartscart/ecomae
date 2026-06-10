<?php
/**
 * CP — Document Control System (English, FTA templates, attachments).
 * URL: /cp/shop/document_control/document_control
 */
defined('_ASTEXE_') or die('No access');

require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/document_control/epc_document_control_helpers.php';

if ($_SERVER['REQUEST_METHOD'] === 'POST' && !empty($_POST['action'])) {
	$epc_dc_backend = isset($GLOBALS['DP_Config']->backend_dir) ? trim((string) $GLOBALS['DP_Config']->backend_dir, '/') : 'cp';
	require $_SERVER['DOCUMENT_ROOT'] . '/' . $epc_dc_backend . '/content/shop/document_control/ajax_document_control.php';
	exit;
}

if (!function_exists('epc_dc_ensure_db_link') || !epc_dc_ensure_db_link()) {
	echo '<div class="alert alert-danger"><strong>Document Control unavailable.</strong> Could not connect to the tenant database. Hard-refresh after login or run document-control setup for this site.</div>';
	return;
}

epc_dc_ensure($db_link);
epc_dc_sync_seller_from_einvoice($db_link);

$backend = isset($GLOBALS['DP_Config']->backend_dir)
	? trim((string) $GLOBALS['DP_Config']->backend_dir, '/')
	: 'cp';
if ($backend === '') {
	$backend = 'cp';
}
$dcUrl = function_exists('epc_document_control_cp_url') ? epc_document_control_cp_url() : ('/' . $backend . '/shop/document_control/document_control');
$dcAjaxUrl = function_exists('epc_document_control_cp_ajax_url')
	? epc_document_control_cp_ajax_url()
	: ('/' . $backend . '/content/shop/document_control/ajax_document_control_endpoint.php');
$printBase = '/content/shop/document_control/service/print.php';
$legacyUrl = '/' . $backend . '/shop/modul-pechati-dokumentov';
$erpUrl = '/' . $backend . '/shop/finance/erp?tab=einvoice';
$ordersUrl = '/' . $backend . '/shop/orders/orders';

$tabs = array(
	'dashboard' => 'Dashboard',
	'company' => 'Company profile',
	'templates' => 'Templates',
	'print' => 'Print documents',
	'attachments' => 'Attachments',
	'guide' => 'Guide',
);
$tab = isset($_GET['tab']) ? (string)$_GET['tab'] : 'dashboard';
if (!isset($tabs[$tab])) {
	$tab = 'dashboard';
}

if (!isset($user_session) || !is_array($user_session)) {
	require_once $_SERVER['DOCUMENT_ROOT'] . '/content/users/dp_user.php';
	$user_session = DP_User::getAdminSession();
}
$csrf = isset($user_session['csrf_guard_key']) ? (string)$user_session['csrf_guard_key'] : '';
$dash = epc_dc_dashboard($db_link);
$company = epc_dc_get_company($db_link);
$templates = epc_dc_list_templates($db_link);
$orders = ($tab === 'print' || $tab === 'attachments') ? epc_dc_recent_orders($db_link) : array();
$editCode = isset($_GET['tpl']) ? (string)$_GET['tpl'] : '';
$editTpl = $editCode !== '' ? epc_dc_get_template($db_link, $editCode) : null;
$filterOrder = isset($_GET['order_id']) ? (int)$_GET['order_id'] : 0;
$attachments = $tab === 'attachments' ? epc_dc_list_attachments($db_link, $filterOrder > 0 ? 'order' : '', $filterOrder) : array();
?>

<div class="col-lg-12 epc-erp-shell">
	<div class="hpanel">
		<div class="panel-heading hbuilt">
			<i class="fa fa-file-text-o"></i> Document Control System
			<span class="pull-right">
				<a class="btn btn-default btn-xs" href="<?php echo epc_dc_h($ordersUrl); ?>"><i class="fa fa-shopping-cart"></i> Orders</a>
				<a class="btn btn-default btn-xs" href="<?php echo epc_dc_h($erpUrl); ?>"><i class="fa fa-certificate"></i> E-Invoicing (FTA)</a>
			</span>
		</div>
		<div class="panel-body">

			<div class="epc-dc-hero">
				<h3><i class="fa fa-files-o"></i> Industrial document management — English</h3>
				<p style="margin:0;opacity:.92;">FTA-aligned tax invoices, packing slips, delivery notes, and payment receipts. Editable HTML templates with company logo, TRN, legal footer, and supplier purchase invoice attachments.</p>
			</div>

			<div id="epc_dc_msg" class="alert epc-dc-msg"></div>

			<div class="epc-dc-nav">
				<?php foreach ($tabs as $k => $lbl): ?>
					<a class="btn btn-sm <?php echo $tab === $k ? 'btn-primary' : 'btn-default'; ?>"
					   href="<?php echo epc_dc_h(epc_dc_tab_url($dcUrl, $k)); ?>"><?php echo epc_dc_h($lbl); ?></a>
				<?php endforeach; ?>
			</div>

			<?php if ($tab === 'dashboard'): ?>
				<div class="epc-dc-kpi">
					<div class="kpi"><div class="lbl">Active templates</div><div class="val"><?php echo (int)$dash['active_templates']; ?></div></div>
					<div class="kpi"><div class="lbl">Attachments</div><div class="val"><?php echo (int)$dash['attachments_total']; ?></div></div>
					<div class="kpi"><div class="lbl">Supplier invoices</div><div class="val"><?php echo (int)$dash['supplier_invoices']; ?></div></div>
					<div class="kpi"><div class="lbl">Company profile</div><div class="val"><?php echo $dash['company_configured'] ? 'Ready' : 'Setup needed'; ?></div></div>
				</div>
				<div class="alert alert-info">
					<strong>Quick start:</strong> Configure <a href="<?php echo epc_dc_h(epc_dc_tab_url($dcUrl, 'company')); ?>">Company profile</a> (logo, TRN, address, legal text),
					review <a href="<?php echo epc_dc_h(epc_dc_tab_url($dcUrl, 'templates')); ?>">Templates</a>, then print from
					<a href="<?php echo epc_dc_h(epc_dc_tab_url($dcUrl, 'print')); ?>">Print documents</a>.
					Legacy Russian print module: <a href="<?php echo epc_dc_h($legacyUrl); ?>"><?php echo epc_dc_h($legacyUrl); ?></a> (redirects here).
				</div>
				<h4>Document types included</h4>
				<ul>
					<li><strong>FTA Tax Invoice</strong> — TRN, VAT breakdown, line items (UAE Federal Tax Authority format)</li>
					<li><strong>Packing Slip</strong> — shipment contents without tax values</li>
					<li><strong>Delivery Note</strong> — proof of delivery with signature block</li>
					<li><strong>Payment Receipt</strong> — customer payment acknowledgment</li>
				</ul>
			<?php elseif ($tab === 'company'): ?>
				<form id="epc_dc_company_form" class="form-horizontal">
					<input type="hidden" name="action" value="save_company">
					<input type="hidden" name="csrf_guard_key" value="<?php echo epc_dc_h($csrf); ?>">
					<div class="row">
						<div class="col-md-8">
							<div class="form-group"><label class="col-sm-3 control-label">Legal name</label>
								<div class="col-sm-9"><input class="form-control" name="legal_name" value="<?php echo epc_dc_h($company['legal_name'] ?? ''); ?>"></div></div>
							<div class="form-group"><label class="col-sm-3 control-label">Trade name</label>
								<div class="col-sm-9"><input class="form-control" name="trade_name" value="<?php echo epc_dc_h($company['trade_name'] ?? ''); ?>"></div></div>
							<div class="form-group"><label class="col-sm-3 control-label">Address line 1</label>
								<div class="col-sm-9"><input class="form-control" name="address_line1" value="<?php echo epc_dc_h($company['address_line1'] ?? ''); ?>"></div></div>
							<div class="form-group"><label class="col-sm-3 control-label">Address line 2</label>
								<div class="col-sm-9"><input class="form-control" name="address_line2" value="<?php echo epc_dc_h($company['address_line2'] ?? ''); ?>"></div></div>
							<div class="form-group"><label class="col-sm-3 control-label">City / Country</label>
								<div class="col-sm-4"><input class="form-control" name="city" value="<?php echo epc_dc_h($company['city'] ?? ''); ?>"></div>
								<div class="col-sm-5"><input class="form-control" name="country" value="<?php echo epc_dc_h($company['country'] ?? 'United Arab Emirates'); ?>"></div></div>
							<div class="form-group"><label class="col-sm-3 control-label">TRN (VAT reg.)</label>
								<div class="col-sm-9"><input class="form-control" name="trn" value="<?php echo epc_dc_h($company['trn'] ?? ''); ?>" placeholder="15-digit UAE TRN"></div></div>
							<div class="form-group"><label class="col-sm-3 control-label">Phone / Email</label>
								<div class="col-sm-4"><input class="form-control" name="phone" value="<?php echo epc_dc_h($company['phone'] ?? ''); ?>"></div>
								<div class="col-sm-5"><input class="form-control" name="email" value="<?php echo epc_dc_h($company['email'] ?? ''); ?>"></div></div>
							<div class="form-group"><label class="col-sm-3 control-label">Website</label>
								<div class="col-sm-9"><input class="form-control" name="website" value="<?php echo epc_dc_h($company['website'] ?? ''); ?>"></div></div>
							<div class="form-group"><label class="col-sm-3 control-label">Bank / IBAN</label>
								<div class="col-sm-4"><input class="form-control" name="bank_name" value="<?php echo epc_dc_h($company['bank_name'] ?? ''); ?>"></div>
								<div class="col-sm-5"><input class="form-control" name="bank_iban" value="<?php echo epc_dc_h($company['bank_iban'] ?? ''); ?>"></div></div>
							<div class="form-group"><label class="col-sm-3 control-label">Legal footer</label>
								<div class="col-sm-9"><textarea class="form-control" rows="4" name="legal_footer"><?php echo epc_dc_h($company['legal_footer'] ?? ''); ?></textarea>
								<p class="help-block">Shown on all documents — FTA retention, TRN disclaimer, terms.</p></div></div>
							<div class="form-group"><div class="col-sm-offset-3 col-sm-9">
								<button type="submit" class="btn btn-primary"><i class="fa fa-save"></i> Save company profile</button>
								<button type="button" class="btn btn-default" id="epc_dc_sync_seller"><i class="fa fa-download"></i> Import from E-Invoicing</button>
							</div></div>
						</div>
						<div class="col-md-4">
							<h4>Company logo</h4>
							<?php $logo = trim((string)($company['logo_path'] ?? '')); ?>
							<?php if ($logo !== ''): ?><p><img src="<?php echo epc_dc_h($logo); ?>" class="epc-dc-logo" alt="Logo"></p><?php endif; ?>
							<input type="file" id="epc_dc_logo_file" accept="image/*">
							<button type="button" class="btn btn-sm btn-success" id="epc_dc_upload_logo"><i class="fa fa-upload"></i> Upload logo</button>
							<p class="help-block">PNG or JPG recommended. Appears on every document header.</p>
						</div>
					</div>
				</form>
			<?php elseif ($tab === 'templates'): ?>
				<?php if ($editTpl): ?>
					<p><a href="<?php echo epc_dc_h(epc_dc_tab_url($dcUrl, 'templates')); ?>">&larr; All templates</a></p>
					<h4>Edit: <?php echo epc_dc_h($editTpl['title']); ?></h4>
					<form id="epc_dc_tpl_form" class="epc-dc-tpl-editor">
						<input type="hidden" name="action" value="save_template">
						<input type="hidden" name="code" value="<?php echo epc_dc_h($editTpl['code']); ?>">
						<input type="hidden" name="csrf_guard_key" value="<?php echo epc_dc_h($csrf); ?>">
						<div class="form-group"><label>Title</label><input class="form-control" name="title" value="<?php echo epc_dc_h($editTpl['title']); ?>"></div>
						<div class="form-group"><label>Description</label><input class="form-control" name="description" value="<?php echo epc_dc_h($editTpl['description']); ?>"></div>
						<div class="form-group"><label>Header HTML</label><textarea class="form-control" name="header_html"><?php echo epc_dc_h($editTpl['header_html']); ?></textarea></div>
						<div class="form-group"><label>Body HTML</label><textarea class="form-control" name="body_html"><?php echo epc_dc_h($editTpl['body_html']); ?></textarea></div>
						<div class="form-group"><label>Footer HTML</label><textarea class="form-control" name="footer_html"><?php echo epc_dc_h($editTpl['footer_html']); ?></textarea></div>
						<div class="form-group"><label>CSS</label><textarea class="form-control" name="css_extra" rows="4"><?php echo epc_dc_h($editTpl['css_extra']); ?></textarea></div>
						<div class="checkbox"><label><input type="checkbox" name="active" value="1" <?php echo !empty($editTpl['active']) ? 'checked' : ''; ?>> Active</label></div>
						<p class="help-block">Placeholders: <code>{{company_logo}}</code>, <code>{{company_trn}}</code>, <code>{{document_number}}</code>, <code>{{lines_table}}</code>, <code>{{legal_footer}}</code>, etc.</p>
						<button type="submit" class="btn btn-primary"><i class="fa fa-save"></i> Save template</button>
						<a class="btn btn-default" target="_blank" href="<?php echo epc_dc_h($printBase . '?doc=' . urlencode($editTpl['code']) . '&preview=1'); ?>"><i class="fa fa-eye"></i> Preview</a>
					</form>
				<?php else: ?>
					<table class="table table-striped">
						<thead><tr><th>Template</th><th>Category</th><th>Status</th><th></th></tr></thead>
						<tbody>
						<?php foreach ($templates as $t): ?>
							<tr>
								<td><strong><?php echo epc_dc_h($t['title']); ?></strong><br><span class="text-muted"><?php echo epc_dc_h($t['description']); ?></span></td>
								<td><?php echo epc_dc_h($t['category']); ?></td>
								<td><?php echo !empty($t['active']) ? '<span class="label label-success">Active</span>' : '<span class="label label-default">Off</span>'; ?></td>
								<td>
									<a class="btn btn-xs btn-primary" href="<?php echo epc_dc_h(epc_dc_tab_url($dcUrl, 'templates', array('tpl' => $t['code']))); ?>">Edit</a>
									<a class="btn btn-xs btn-default" target="_blank" href="<?php echo epc_dc_h($printBase . '?doc=' . urlencode($t['code']) . '&preview=1'); ?>">Preview</a>
								</td>
							</tr>
						<?php endforeach; ?>
						</tbody>
					</table>
				<?php endif; ?>
			<?php elseif ($tab === 'print'): ?>
				<h4>Generate &amp; print documents</h4>
				<table class="table table-hover table-condensed">
					<thead><tr><th>Order</th><th>Date</th><th>Customer</th><th>Amount (excl.)</th><th>Documents</th></tr></thead>
					<tbody>
					<?php foreach ($orders as $o): ?>
						<tr>
							<td>#<?php echo (int)$o['id']; ?></td>
							<td><?php echo epc_dc_h(date('Y-m-d', (int)$o['time'])); ?></td>
							<td><?php echo epc_dc_h($o['email'] ?? ''); ?></td>
							<td><?php echo epc_dc_h(epc_dc_money((float)($o['sale_ex'] ?? 0))); ?></td>
							<td>
								<?php foreach ($templates as $t): if (empty($t['active'])) continue; ?>
									<a class="btn btn-xs btn-default" target="_blank"
									   href="<?php echo epc_dc_h($printBase . '?doc=' . urlencode($t['code']) . '&order_id=' . (int)$o['id']); ?>">
										<?php echo epc_dc_h($t['title']); ?>
									</a>
								<?php endforeach; ?>
								<a class="btn btn-xs btn-info" href="<?php echo epc_dc_h(epc_dc_tab_url($dcUrl, 'attachments', array('order_id' => (int)$o['id']))); ?>">Attach supplier docs</a>
							</td>
						</tr>
					<?php endforeach; ?>
					</tbody>
				</table>
			<?php elseif ($tab === 'attachments'): ?>
				<h4>Document attachments (supplier purchase invoices &amp; other files)</h4>
				<form id="epc_dc_att_form" class="well" enctype="multipart/form-data">
					<input type="hidden" name="action" value="upload_attachment">
					<input type="hidden" name="csrf_guard_key" value="<?php echo epc_dc_h($csrf); ?>">
					<div class="row">
						<div class="col-sm-2"><label>Order ID</label><input class="form-control" name="entity_id" type="number" value="<?php echo $filterOrder ?: ''; ?>" required></div>
						<div class="col-sm-3"><label>Category</label>
							<select class="form-control" name="doc_category">
								<option value="supplier_invoice">Supplier purchase invoice</option>
								<option value="delivery_proof">Delivery proof</option>
								<option value="customs">Customs / import</option>
								<option value="other">Other</option>
							</select>
						</div>
						<div class="col-sm-3"><label>Supplier name</label><input class="form-control" name="supplier_name"></div>
						<div class="col-sm-2"><label>Reference no.</label><input class="form-control" name="reference_no"></div>
						<div class="col-sm-2"><label>File</label><input type="file" name="file" required></div>
					</div>
					<div class="row" style="margin-top:8px"><div class="col-sm-12">
						<label>Notes</label><input class="form-control" name="notes">
						<button type="submit" class="btn btn-success" style="margin-top:8px"><i class="fa fa-paperclip"></i> Upload attachment</button>
					</div></div>
				</form>
				<?php if ($filterOrder > 0): ?>
					<p>Filtering order #<?php echo $filterOrder; ?> — <a href="<?php echo epc_dc_h(epc_dc_tab_url($dcUrl, 'attachments')); ?>">Show all</a></p>
				<?php endif; ?>
				<table class="table table-striped">
					<thead><tr><th>Order</th><th>Category</th><th>Supplier</th><th>Ref</th><th>File</th><th>Date</th><th></th></tr></thead>
					<tbody>
					<?php foreach ($attachments as $a): ?>
						<tr>
							<td>#<?php echo (int)$a['entity_id']; ?></td>
							<td><?php echo epc_dc_h($a['doc_category']); ?></td>
							<td><?php echo epc_dc_h($a['supplier_name']); ?></td>
							<td><?php echo epc_dc_h($a['reference_no']); ?></td>
							<td><a target="_blank" href="<?php echo epc_dc_h($a['file_path']); ?>"><?php echo epc_dc_h($a['file_name']); ?></a></td>
							<td><?php echo epc_dc_h(date('Y-m-d H:i', (int)$a['uploaded_at'])); ?></td>
							<td><button class="btn btn-xs btn-danger epc-dc-del-att" data-id="<?php echo (int)$a['id']; ?>">Delete</button></td>
						</tr>
					<?php endforeach; ?>
					<?php if (!$attachments): ?><tr><td colspan="7" class="text-muted">No attachments yet.</td></tr><?php endif; ?>
					</tbody>
				</table>
			<?php elseif ($tab === 'guide'): ?>
				<?php
				$epc_dc_backend = isset($GLOBALS['DP_Config']->backend_dir) ? trim((string) $GLOBALS['DP_Config']->backend_dir, '/') : 'cp';
				include $_SERVER['DOCUMENT_ROOT'] . '/' . $epc_dc_backend . '/content/shop/document_control/document_control_guide.php';
				?>
			<?php endif; ?>

		</div>
	</div>
</div>
