<?php
defined('_ASTEXE_') or die('No access');
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_vouchers.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_ui.php';

$erpOnly = epc_erp_is_erp_only_context();
$filters = array(
	'status' => isset($_GET['order_status']) ? (string) $_GET['order_status'] : '',
	'q' => isset($_GET['q']) ? trim((string) $_GET['q']) : '',
);
$orders = epc_erp_sales_orders_list($db_link, $date_from, $date_to, $filters, 200);

if ($erpOnly) {
	$customers = $db_link->query(
		'SELECT `user_id`, `email` FROM `users` WHERE `user_id` > 0 ORDER BY `email` LIMIT 500'
	)->fetchAll(PDO::FETCH_ASSOC);
	erp_page_header(
		'<i class="fa fa-shopping-cart"></i> Sales orders',
		'Prepare sales orders (SO-) and convert to tax invoices (SI-) — manual ERP workflow, no storefront.',
		array(
			array('label' => 'ERP', 'url' => epc_erp_tab_url($erpUrl, 'dashboard', $date_from_str, $date_to_str)),
			array('label' => 'Sales orders'),
		)
	);
	erp_stat_cards(array(
		array('label' => 'Orders in list', 'value' => (string) count($orders)),
		array('label' => 'Open (draft/confirmed)', 'value' => (string) count(array_filter($orders, function ($o) {
			return in_array($o['status'] ?? '', array('draft', 'confirmed'), true);
		}))),
	));
	erp_filter_bar($erpUrl, 'sales_orders', $date_from_str, $date_to_str,
		'<label>Status</label> <select name="order_status" class="form-control input-sm">'
		. '<option value="">All</option><option value="draft">Draft</option><option value="confirmed">Confirmed</option>'
		. '<option value="invoiced">Invoiced</option></select>'
		. ' <input type="text" name="q" class="form-control input-sm" placeholder="SO # or customer" value="' . epc_erp_h($filters['q']) . '">'
	);
	ob_start();
	if (empty($orders)) {
		erp_empty_state('No sales orders yet. Create a draft SO below.');
	} else {
		erp_table_open(array('SO #', 'Date', 'Customer', 'Title', 'Total incl. VAT', 'Status', 'Invoice', 'Actions'));
		foreach ($orders as $r) {
			echo '<tr><td>' . epc_erp_h($r['so_no']) . '</td>';
			echo '<td>' . epc_erp_h(date('Y-m-d', (int) $r['time_created'])) . '</td>';
			echo '<td>' . epc_erp_h($r['customer_email'] ?: ('User #' . (int) $r['customer_user_id'])) . '</td>';
			echo '<td>' . epc_erp_h($r['title']) . '</td>';
			echo '<td>' . epc_erp_money($r['total_amount']) . '</td>';
			echo '<td><span class="label label-info">' . epc_erp_h($r['status']) . '</span></td>';
			echo '<td>' . epc_erp_h($r['invoice_no'] ?: '—') . '</td><td class="epc-erp-form-inline">';
			if (in_array($r['status'], array('draft', 'confirmed'), true)) {
				if ($r['status'] === 'draft') {
					echo '<form class="epc-erp-so-status"><input type="hidden" name="csrf_guard_key" value="' . epc_erp_h($csrf) . '">';
					echo '<input type="hidden" name="so_id" value="' . (int) $r['id'] . '"><input type="hidden" name="status" value="confirmed">';
					echo '<button type="submit" class="btn btn-xs btn-success">Confirm</button></form> ';
				}
				echo '<form class="epc-erp-so-invoice"><input type="hidden" name="csrf_guard_key" value="' . epc_erp_h($csrf) . '">';
				echo '<input type="hidden" name="so_id" value="' . (int) $r['id'] . '">';
				echo '<button type="submit" class="btn btn-xs btn-primary">→ Sales invoice</button></form>';
			}
			echo '</td></tr>';
		}
		erp_table_close();
	}
	erp_section_card('Sales order list', ob_get_clean(), array('icon' => 'fa-list'));
	ob_start();
	?>
	<form id="epc_erp_form_so" class="form-horizontal" style="max-width:760px;">
		<input type="hidden" name="csrf_guard_key" value="<?php echo epc_erp_h($csrf); ?>">
		<div class="form-group"><label class="col-sm-3">Customer</label><div class="col-sm-9">
			<select name="customer_user_id" class="form-control input-sm" required><option value="">—</option>
			<?php foreach ($customers as $c): ?>
				<option value="<?php echo (int) $c['user_id']; ?>"><?php echo epc_erp_h($c['email'] ?: ('User #' . (int) $c['user_id'])); ?></option>
			<?php endforeach; ?>
			</select></div></div>
		<div class="form-group"><label class="col-sm-3">Title</label><div class="col-sm-9"><input name="title" class="form-control input-sm" required></div></div>
		<div class="form-group"><label class="col-sm-3">Line (ex VAT)</label><div class="col-sm-9 form-inline">
			<input name="line_desc[]" class="form-control input-sm" placeholder="Description" required>
			<input name="line_qty[]" type="number" step="0.001" value="1" class="form-control input-sm" style="width:70px">
			<input name="line_unit[]" type="number" step="0.01" class="form-control input-sm" placeholder="Unit AED" required>
		</div></div>
		<div class="form-group"><div class="col-sm-offset-3 col-sm-9"><button type="submit" class="btn btn-primary btn-sm">Create draft SO</button></div></div>
	</form>
	<?php
	erp_section_card('New sales order', ob_get_clean(), array('icon' => 'fa-plus'));
	return;
}

erp_page_header(
	'<i class="fa fa-shopping-cart"></i> Sales orders',
	'Read-only ERP view of shop orders for the selected period. Manage orders in CP Orders.',
	array(
		array('label' => 'ERP', 'url' => epc_erp_tab_url($erpUrl, 'dashboard', $date_from_str, $date_to_str)),
		array('label' => 'Sales orders'),
	),
	!empty($ordersUrl) ? array(array('label' => 'Open CP Orders', 'url' => $ordersUrl, 'class' => 'btn-primary', 'icon' => 'fa-external-link')) : array()
);
erp_filter_bar($erpUrl, 'sales_orders', $date_from_str, $date_to_str,
	'<label>Status</label> <select name="order_status" class="form-control input-sm">'
	. '<option value="">All</option><option value="complete"' . ($filters['status'] === 'complete' ? ' selected' : '') . '>Completed</option>'
	. '<option value="open"' . ($filters['status'] === 'open' ? ' selected' : '') . '>In progress</option></select>'
	. ' <input type="text" name="q" class="form-control input-sm" placeholder="Order # or email" value="' . epc_erp_h($filters['q']) . '">'
);
ob_start();
if (empty($orders)) {
	erp_empty_state('No orders match your filters for this period.');
} else {
	erp_table_open(array('Order', 'Date', 'Customer', 'Status', 'Sale ex VAT', 'Incl. VAT', 'Paid', 'Due', ''));
	foreach ($orders as $r) {
		$complete = !empty($r['order_complete']);
		echo '<tr><td>';
		if (!empty($ordersUrl)) {
			echo '<a href="' . epc_erp_h($ordersUrl . '?order_id=' . (int) $r['id']) . '">#' . (int) $r['id'] . '</a>';
		} else {
			echo '#' . (int) $r['id'];
		}
		echo '</td><td>' . epc_erp_h(date('Y-m-d H:i', (int) $r['time'])) . '</td>';
		echo '<td>' . epc_erp_h($r['customer_email'] ?: ('User ' . (int) $r['user_id'])) . '</td>';
		echo '<td>' . ($complete ? '<span class="label label-success">Complete</span>' : '<span class="label label-default">' . epc_erp_h($r['order_status_name'] ?: 'Open') . '</span>') . '</td>';
		echo '<td>' . ($complete ? epc_erp_money($r['sale_ex_vat']) : '—') . '</td>';
		echo '<td>' . ($complete ? epc_erp_money($r['sale_incl_vat'] ?? 0) : '—') . '</td>';
		echo '<td>' . ($complete ? epc_erp_money($r['paid_amount']) : '—') . '</td>';
		echo '<td>' . ($complete ? epc_erp_money($r['due_amount']) : '—') . '</td>';
		echo '<td>' . ((int) ($r['paid'] ?? 0) === 1 ? '<span class="label label-success">Paid</span>' : '<span class="label label-warning">Due</span>');
		if ($complete) {
			$invUrl = epc_erp_tab_url($erpUrl, 'invoices', $date_from_str, $date_to_str, 'sales')
				. '&inv_action=edit&order_id=' . (int) $r['id'];
			echo ' <a class="btn btn-xs btn-default" href="' . epc_erp_h($invUrl) . '" title="Create e-invoice"><i class="fa fa-file-text-o"></i></a>';
		}
		echo '</td></tr>';
	}
	erp_table_close();
}
erp_section_card('Order list', ob_get_clean(), array('icon' => 'fa-list'));
