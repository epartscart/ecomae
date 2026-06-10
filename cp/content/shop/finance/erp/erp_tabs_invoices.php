<?php
defined('_ASTEXE_') or die('No access');
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_invoices.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_ui.php';

epc_erp_invoices_ensure_schema($db_link);

$invId = isset($_GET['inv_id']) ? (int)$_GET['inv_id'] : 0;
$invAction = isset($_GET['inv_action']) ? (string)$_GET['inv_action'] : 'list';
$prefillOrder = isset($_GET['order_id']) ? (int)$_GET['order_id'] : 0;
$filters = array(
	'status' => isset($_GET['inv_status']) ? (string)$_GET['inv_status'] : '',
	'q' => isset($_GET['inv_q']) ? trim((string)$_GET['inv_q']) : '',
);
$kpis = epc_erp_invoice_kpis($db_link, $date_from, $date_to);
$invBase = epc_erp_tab_url($erpUrl, 'invoices', $date_from_str, $date_to_str, 'sales');
$einvSettingsUrl = epc_erp_tab_url($erpUrl, 'einvoice', $date_from_str, $date_to_str, 'finance') . '&einv_section=seller';

erp_page_header(
	'<i class="fa fa-file-text-o"></i> Invoices (e-invoice)',
	'Customer tax invoices with UAE PINT-AE fields — create, print, download JSON/XML.',
	array(
		array('label' => 'ERP', 'url' => epc_erp_tab_url($erpUrl, 'dashboard', $date_from_str, $date_to_str)),
		array('label' => 'Invoices'),
	),
	array(
		array('label' => 'New invoice', 'url' => $invBase . '&inv_action=edit', 'class' => 'btn-primary', 'icon' => 'fa-plus'),
		array('label' => 'E-Invoicing ASP', 'url' => $einvSettingsUrl, 'class' => 'btn-default', 'icon' => 'fa-cog'),
	)
);
erp_filter_bar($erpUrl, 'invoices', $date_from_str, $date_to_str,
	'<input type="hidden" name="area" value="sales">'
	. '<label>Status</label> <select name="inv_status" class="form-control input-sm"><option value="">All</option>'
	. '<option value="draft"' . ($filters['status'] === 'draft' ? ' selected' : '') . '>Draft</option>'
	. '<option value="validated"' . ($filters['status'] === 'validated' ? ' selected' : '') . '>Validated</option>'
	. '<option value="submitted"' . ($filters['status'] === 'submitted' ? ' selected' : '') . '>Submitted</option></select>'
	. ' <input type="text" name="inv_q" class="form-control input-sm" placeholder="Invoice # or order" value="' . epc_erp_h($filters['q']) . '">'
);
erp_stat_cards(array(
	array('label' => 'Invoices (period)', 'value' => (string)$kpis['total']),
	array('label' => 'Validated', 'value' => (string)$kpis['validated'], 'class' => 'green'),
	array('label' => 'Submitted', 'value' => (string)$kpis['submitted']),
	array('label' => 'Total incl. VAT', 'value' => epc_erp_money($kpis['amount_incl_vat']) . ' AED'),
));

if ($invAction === 'view' || ($invId > 0 && $invAction !== 'edit')) {
	$invAction = 'view';
	$invId = $invId > 0 ? $invId : (int)($_GET['inv_id'] ?? 0);
}

if ($invAction === 'view' && $invId > 0):
	$doc = epc_einvoice_get_document($db_link, $invId);
	if (!$doc):
		erp_empty_state('Invoice not found.');
		echo '<p><a href="' . epc_erp_h($invBase) . '" class="btn btn-default btn-sm">Back to list</a></p>';
	else:
		$printUrl = $erpUrl . '?area=sales&tab=invoices&action=invoice_print&invoice_id=' . (int)$invId
			. '&from=' . rawurlencode($date_from_str) . '&to=' . rawurlencode($date_to_str);
		$jsonUrl = $erpUrl . '?area=sales&tab=invoices&action=invoice_download_json&invoice_id=' . (int)$invId;
		$xmlUrl = $erpUrl . '?action=einvoice_download_xml&document_id=' . (int)$invId;
?>
	<p>
		<a href="<?php echo epc_erp_h($invBase); ?>" class="btn btn-default btn-sm"><i class="fa fa-arrow-left"></i> List</a>
		<a href="<?php echo epc_erp_h($invBase . '&inv_action=edit&inv_id=' . (int)$invId); ?>" class="btn btn-default btn-sm"><i class="fa fa-pencil"></i> Edit</a>
		<a href="<?php echo epc_erp_h($printUrl); ?>" class="btn btn-primary btn-sm" target="_blank"><i class="fa fa-print"></i> Print</a>
		<a href="<?php echo epc_erp_h($jsonUrl); ?>" class="btn btn-default btn-sm" target="_blank"><i class="fa fa-code"></i> JSON</a>
		<a href="<?php echo epc_erp_h($xmlUrl); ?>" class="btn btn-default btn-sm" target="_blank"><i class="fa fa-download"></i> PINT-AE XML</a>
	</p>
	<?php if (!$doc['validation_ok']): ?>
		<div class="alert alert-warning"><strong>Validation:</strong>
			<ul style="margin:8px 0 0;"><?php foreach ($doc['validation_errors'] as $err): ?><li><?php echo epc_erp_h($err); ?></li><?php endforeach; ?></ul>
		</div>
	<?php endif; ?>
	<div class="well" style="background:#fff;padding:20px;border:1px solid #e2e8f0;">
		<div class="row">
			<div class="col-sm-6">
				<p><strong>Seller:</strong> <?php echo epc_erp_h($doc['seller']['seller_name'] ?? ''); ?><br>
				TRN <?php echo epc_erp_h($doc['seller']['seller_trn'] ?? '—'); ?></p>
				<p><strong>Buyer:</strong> <?php echo epc_erp_h($doc['buyer']['buyer_name'] ?? ''); ?><br>
				TRN <?php echo epc_erp_h($doc['buyer']['buyer_trn'] ?? '—'); ?></p>
			</div>
			<div class="col-sm-6 text-right">
				<p><strong><?php echo epc_erp_h($doc['invoice_number']); ?></strong><br>
				<?php echo epc_erp_h(date('Y-m-d', (int)$doc['issue_date'])); ?> · Due <?php echo epc_erp_h(date('Y-m-d', (int)$doc['payment_due_date'])); ?><br>
				<strong><?php echo epc_erp_money($doc['total_incl_vat']); ?> <?php echo epc_erp_h($doc['currency_code']); ?></strong> incl. VAT</p>
			</div>
		</div>
		<table class="table table-condensed table-bordered">
			<thead><tr><th>#</th><th>Item</th><th>Qty</th><th>Net</th><th>VAT</th><th>Gross</th></tr></thead>
			<tbody>
			<?php foreach ($doc['lines'] as $ln): ?>
				<tr>
					<td><?php echo (int)$ln['line_no']; ?></td>
					<td><?php echo epc_erp_h($ln['item_name']); ?></td>
					<td><?php echo epc_erp_h(number_format((float)$ln['quantity'], 2)); ?></td>
					<td><?php echo epc_erp_money($ln['line_net']); ?></td>
					<td><?php echo epc_erp_money($ln['vat_line_aed']); ?></td>
					<td><?php echo epc_erp_money($ln['gross_amount']); ?></td>
				</tr>
			<?php endforeach; ?>
			</tbody>
		</table>
	</div>
	<?php
		$invoiceId = $invId;
		include __DIR__ . '/erp_invoice_attachments_panel.php';
	endif;

elseif ($invAction === 'edit'):
	$editDoc = null;
	if ($invId > 0) {
		$editDoc = epc_einvoice_get_document($db_link, $invId);
	}
	$seller = epc_einvoice_seller_profile($db_link);
	$lines = $editDoc ? $editDoc['lines'] : array();
	if ($prefillOrder > 0 && !$editDoc) {
		try {
			$built = epc_einvoice_build_from_order($db_link, $prefillOrder);
			$editDoc = $built;
			$lines = $built['lines'];
		} catch (Exception $e) {
			echo '<div class="alert alert-danger">' . epc_erp_h($e->getMessage()) . '</div>';
		}
	}
	if (empty($lines)) {
		$lines = array(array(
			'line_no' => 1, 'item_name' => '', 'item_description' => '', 'quantity' => 1,
			'unit_price' => 0, 'line_net' => 0, 'tax_rate' => 5, 'tax_category' => 'S',
			'tax_amount' => 0, 'gross_amount' => 0, 'vat_line_aed' => 0, 'line_amount_aed' => 0,
		));
	}
	$buyer = $editDoc['buyer'] ?? array();
?>
	<p><a href="<?php echo epc_erp_h($invBase); ?>" class="btn btn-default btn-sm"><i class="fa fa-arrow-left"></i> Cancel</a></p>
	<form id="epc_erp_form_invoice" class="form-horizontal" style="max-width:960px;">
		<input type="hidden" name="csrf_guard_key" value="<?php echo epc_erp_h($csrf); ?>">
		<input type="hidden" name="id" value="<?php echo $editDoc && isset($editDoc['id']) ? (int)$editDoc['id'] : 0; ?>">
		<div class="row">
			<div class="col-md-6">
				<h4>Invoice header</h4>
				<div class="form-group"><label class="col-sm-4">Invoice number</label><div class="col-sm-8">
					<input type="text" name="invoice_number" class="form-control" value="<?php echo epc_erp_h($editDoc['invoice_number'] ?? ''); ?>" placeholder="Auto if blank"></div></div>
				<div class="form-group"><label class="col-sm-4">Issue date</label><div class="col-sm-8">
					<input type="date" name="issue_date" class="form-control" value="<?php echo epc_erp_h(isset($editDoc['issue_date']) ? date('Y-m-d', (int)$editDoc['issue_date']) : date('Y-m-d')); ?>"></div></div>
				<div class="form-group"><label class="col-sm-4">Due date</label><div class="col-sm-8">
					<input type="date" name="due_date" class="form-control" value="<?php echo epc_erp_h(isset($editDoc['payment_due_date']) ? date('Y-m-d', (int)$editDoc['payment_due_date']) : date('Y-m-d', strtotime('+7 days'))); ?>"></div></div>
				<div class="form-group"><label class="col-sm-4">Currency</label><div class="col-sm-8">
					<input type="text" name="currency_code" class="form-control" value="<?php echo epc_erp_h($editDoc['currency_code'] ?? 'AED'); ?>" maxlength="8"></div></div>
				<div class="form-group"><label class="col-sm-4">Order ID</label><div class="col-sm-8">
					<input type="number" name="order_id" class="form-control" value="<?php echo (int)($editDoc['order_id'] ?? $prefillOrder); ?>"></div></div>
				<div class="form-group"><label class="col-sm-4">Customer user ID</label><div class="col-sm-8">
					<input type="number" name="user_id" class="form-control" value="<?php echo (int)($editDoc['user_id'] ?? 0); ?>"></div></div>
				<div class="form-group"><label class="col-sm-4">Payment terms</label><div class="col-sm-8">
					<input type="text" name="payment_terms" class="form-control" value="<?php echo epc_erp_h($editDoc['payment_terms'] ?? ''); ?>"></div></div>
			</div>
			<div class="col-md-6">
				<h4>Buyer (B2B)</h4>
				<div class="form-group"><label class="col-sm-4">Name</label><div class="col-sm-8">
					<input type="text" name="buyer_name" class="form-control" value="<?php echo epc_erp_h($buyer['buyer_name'] ?? ''); ?>"></div></div>
				<div class="form-group"><label class="col-sm-4">TRN</label><div class="col-sm-8">
					<input type="text" name="buyer_trn" class="form-control" value="<?php echo epc_erp_h($buyer['buyer_trn'] ?? $buyer['trn'] ?? ''); ?>"></div></div>
				<div class="form-group"><label class="col-sm-4">Address</label><div class="col-sm-8">
					<input type="text" name="buyer_address_line1" class="form-control" value="<?php echo epc_erp_h($buyer['buyer_address_line1'] ?? ''); ?>"></div></div>
				<div class="form-group"><label class="col-sm-4">City</label><div class="col-sm-8">
					<input type="text" name="buyer_city" class="form-control" value="<?php echo epc_erp_h($buyer['buyer_city'] ?? 'Dubai'); ?>"></div></div>
				<p class="text-muted col-sm-offset-4 col-sm-8">Seller: <strong><?php echo epc_erp_h($seller['seller_name'] ?: '—'); ?></strong> · TRN <?php echo epc_erp_h($seller['seller_trn'] ?: 'configure in E-Invoicing'); ?></p>
			</div>
		</div>
		<h4>Line items</h4>
		<table class="table table-bordered" id="epc_inv_lines_table">
			<thead><tr><th>Description</th><th>Detail</th><th>Qty</th><th>Unit (ex VAT)</th><th>VAT %</th><th></th></tr></thead>
			<tbody>
			<?php foreach ($lines as $i => $ln): ?>
				<tr class="epc-inv-line-row">
					<td><input type="text" name="line_desc[]" class="form-control input-sm" value="<?php echo epc_erp_h($ln['item_name']); ?>"></td>
					<td><input type="text" name="line_detail[]" class="form-control input-sm" value="<?php echo epc_erp_h($ln['item_description'] ?? ''); ?>"></td>
					<td><input type="number" step="0.01" name="line_qty[]" class="form-control input-sm" value="<?php echo epc_erp_h($ln['quantity']); ?>"></td>
					<td><input type="number" step="0.01" name="line_unit[]" class="form-control input-sm" value="<?php echo epc_erp_h($ln['unit_price']); ?>"></td>
					<td><input type="number" step="0.01" name="line_vat_rate[]" class="form-control input-sm" value="<?php echo epc_erp_h($ln['tax_rate']); ?>"></td>
					<td><button type="button" class="btn btn-xs btn-danger epc-inv-rm-line">&times;</button></td>
				</tr>
			<?php endforeach; ?>
			</tbody>
		</table>
		<button type="button" class="btn btn-default btn-sm" id="epc_inv_add_line"><i class="fa fa-plus"></i> Add line</button>
		<div class="form-group" style="margin-top:20px;">
			<button type="submit" class="btn btn-primary"><i class="fa fa-save"></i> Save &amp; validate</button>
		</div>
	</form>
	<script>
	(function(){
		var tbl = document.getElementById('epc_inv_lines_table');
		if (!tbl) return;
		document.getElementById('epc_inv_add_line').addEventListener('click', function(){
			var tbody = tbl.querySelector('tbody');
			var tr = document.createElement('tr');
			tr.className = 'epc-inv-line-row';
			tr.innerHTML = '<td><input type="text" name="line_desc[]" class="form-control input-sm"></td>'
				+ '<td><input type="text" name="line_detail[]" class="form-control input-sm"></td>'
				+ '<td><input type="number" step="0.01" name="line_qty[]" class="form-control input-sm" value="1"></td>'
				+ '<td><input type="number" step="0.01" name="line_unit[]" class="form-control input-sm" value="0"></td>'
				+ '<td><input type="number" step="0.01" name="line_vat_rate[]" class="form-control input-sm" value="5"></td>'
				+ '<td><button type="button" class="btn btn-xs btn-danger epc-inv-rm-line">&times;</button></td>';
			tbody.appendChild(tr);
		});
		tbl.addEventListener('click', function(ev){
			if (ev.target.classList.contains('epc-inv-rm-line')) {
				var row = ev.target.closest('tr');
				if (tbl.querySelectorAll('.epc-inv-line-row').length > 1) row.remove();
			}
		});
	})();
	</script>
<?php
else:
	$invoices = epc_erp_invoice_list($db_link, $date_from, $date_to, $filters, 200);
	ob_start();
	if (empty($invoices)) {
		erp_empty_state('No customer invoices in this period. Create one manually or generate from a completed sales order.');
		echo '<p><a href="' . epc_erp_h($invBase . '&inv_action=edit') . '" class="btn btn-primary btn-sm"><i class="fa fa-plus"></i> Create invoice</a></p>';
	} else {
		erp_table_open(array('Invoice', 'Date', 'Order', 'Customer', 'Ex VAT', 'VAT', 'Incl VAT', 'Due', 'Status', ''));
		foreach ($invoices as $d) {
			echo '<tr><td><strong>' . epc_erp_h($d['invoice_number']) . '</strong></td>';
			echo '<td>' . epc_erp_h(date('Y-m-d', (int)$d['issue_date'])) . '</td>';
			echo '<td>' . ((int)$d['order_id'] ? ('#' . (int)$d['order_id']) : '—') . '</td>';
			echo '<td>' . epc_erp_h($d['customer_email'] ?: ((int)$d['user_id'] ? 'User ' . (int)$d['user_id'] : 'Guest')) . '</td>';
			echo '<td>' . epc_erp_money($d['subtotal_ex_vat']) . '</td>';
			echo '<td>' . epc_erp_money($d['total_vat']) . '</td>';
			echo '<td>' . epc_erp_money($d['total_incl_vat']) . '</td>';
			echo '<td>' . epc_erp_money($d['amount_due']) . '</td>';
			echo '<td><span class="label label-' . ($d['status'] === 'validated' ? 'success' : 'default') . '">' . epc_erp_h($d['status']) . '</span></td>';
			echo '<td><a class="btn btn-xs btn-primary" href="' . epc_erp_h($invBase . '&inv_id=' . (int)$d['id']) . '">View</a> ';
			echo '<a class="btn btn-xs btn-default" href="' . epc_erp_h($invBase . '&inv_action=edit&inv_id=' . (int)$d['id']) . '">Edit</a></td></tr>';
		}
		erp_table_close();
	}
	erp_section_card('Customer invoices', ob_get_clean(), array('icon' => 'fa-list'));
endif;
