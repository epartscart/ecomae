<?php
defined('_ASTEXE_') or die('No access');
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_vouchers.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_ui.php';
if (is_file($_SERVER['DOCUMENT_ROOT'] . '/content/users/epc_countries.php')) {
	require_once $_SERVER['DOCUMENT_ROOT'] . '/content/users/epc_countries.php';
}

$erpOnly = epc_erp_is_erp_only_context();
$filters = array(
	'status' => isset($_GET['order_status']) ? (string) $_GET['order_status'] : '',
	'q' => isset($_GET['q']) ? trim((string) $_GET['q']) : '',
);
$orders = epc_erp_sales_orders_list($db_link, $date_from, $date_to, $filters, 200);

if ($erpOnly) {
	// Customer master = users rows; show the linked ERP contact name when present
	// so a standalone tenant sees friendly names, not synthesized emails.
	$customers = $db_link->query(
		"SELECT u.`user_id`, u.`email`, c.`name` AS contact_name, c.`company` AS contact_company
		 FROM `users` u
		 LEFT JOIN `epc_erp_contacts` c ON c.`linked_user_id` = u.`user_id`
		 WHERE u.`user_id` > 0
		 ORDER BY (c.`name` IS NULL OR c.`name` = ''), c.`name`, u.`email`
		 LIMIT 500"
	)->fetchAll(PDO::FETCH_ASSOC);
	erp_page_header(
		'<i class="fa fa-shopping-cart"></i> Sales orders',
		'Prepare sales orders (SO-) and convert to tax invoices (SI-) — manual ERP workflow, no storefront.',
		array(
			array('label' => 'ERP', 'url' => epc_erp_tab_url($erpUrl, 'dashboard', $date_from_str, $date_to_str)),
			array('label' => 'Sales orders'),
		),
		array(
			array('label' => 'New sales order', 'icon' => 'fa-plus', 'class' => 'btn-primary', 'url' => '#epc_erp_form_so'),
		)
	);
	erp_d365_assets();
	erp_action_pane(array(
		array('label' => 'New', 'buttons' => array(
			array('label' => 'Sales order', 'icon' => 'fa-plus', 'class' => 'is-primary', 'target' => '#epc_erp_form_so'),
			array('label' => 'Customer', 'icon' => 'fa-user-plus', 'target' => '#epc_erp_form_customer'),
		)),
		array('label' => 'Process', 'buttons' => array(
			array('label' => 'Confirm', 'icon' => 'fa-check'),
			array('label' => 'Generate invoice', 'icon' => 'fa-file-text-o'),
		)),
		array('label' => 'View', 'buttons' => array(
			array('label' => 'Refresh', 'icon' => 'fa-refresh', 'url' => epc_erp_tab_url($erpUrl, 'sales_orders', $date_from_str, $date_to_str)),
		)),
	));
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
		erp_table_open(array('SO #', 'Date', 'Customer', 'Title', 'Total incl. VAT', 'Dimensions', 'Status', 'Invoice', 'Actions'));
		foreach ($orders as $r) {
			echo '<tr><td>' . epc_erp_h($r['so_no']) . '</td>';
			echo '<td>' . epc_erp_h(date('Y-m-d', (int) $r['time_created'])) . '</td>';
			$custLabel = trim((string) ($r['customer_name'] ?? ''));
			if ($custLabel === '') {
				$custLabel = trim((string) ($r['customer_company'] ?? ''));
			}
			if ($custLabel === '') {
				$custLabel = (string) ($r['customer_email'] ?? '');
			}
			if ($custLabel === '') {
				$custLabel = 'User #' . (int) $r['customer_user_id'];
			}
			echo '<td>' . epc_erp_h($custLabel) . '</td>';
			echo '<td>' . epc_erp_h($r['title']) . '</td>';
			echo '<td>' . epc_erp_money($r['total_amount']) . '</td>';
			echo '<td>' . epc_erp_dim_badges($db_link, 'sales_order', (int) $r['id']) . '</td>';
			echo '<td>' . erp_status_pill($r['status']) . '</td>';
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
			if ($r['status'] === 'draft') {
				echo ' <form class="epc-erp-so-delete" style="display:inline;"><input type="hidden" name="csrf_guard_key" value="' . epc_erp_h($csrf) . '">';
				echo '<input type="hidden" name="so_id" value="' . (int) $r['id'] . '">';
				echo '<button type="submit" class="btn btn-xs btn-danger">Delete</button></form>';
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
				<?php
				$cLabel = trim((string) ($c['contact_name'] ?? ''));
				if ($cLabel === '') {
					$cLabel = (string) ($c['email'] ?? '');
				}
				if ($cLabel === '' || strpos($cLabel, '@erp.local') !== false) {
					$cLabel = 'Customer #' . (int) $c['user_id'];
				}
				if (!empty($c['contact_company'])) {
					$cLabel .= ' (' . $c['contact_company'] . ')';
				}
				?>
				<option value="<?php echo (int) $c['user_id']; ?>"><?php echo epc_erp_h($cLabel); ?></option>
			<?php endforeach; ?>
			</select>
			<p class="help-block" style="margin:4px 0 0;">No customer yet? Add one with <strong>New customer</strong> below — works fully standalone, no storefront needed.</p>
			</div></div>
		<div class="form-group"><label class="col-sm-3">Title</label><div class="col-sm-9"><input name="title" class="form-control input-sm" required></div></div>
		<div class="form-group"><label class="col-sm-3">Line (ex VAT)</label><div class="col-sm-9 form-inline">
			<input name="line_desc[]" class="form-control input-sm" placeholder="Description" required>
			<input name="line_qty[]" type="number" step="0.001" value="1" class="form-control input-sm" style="width:70px">
			<input name="line_unit[]" type="number" step="0.01" class="form-control input-sm" placeholder="Unit AED" required>
		</div></div>
		<?php echo epc_erp_dim_render_fields($db_link); ?>
		<div class="form-group"><div class="col-sm-offset-3 col-sm-9"><button type="submit" class="btn btn-primary btn-sm">Create draft SO</button></div></div>
	</form>
	<?php
	$epcSoFormHtml = ob_get_clean();
	erp_fasttab_open('New sales order', array('open' => false, 'icon' => 'fa-plus'));
	echo $epcSoFormHtml;
	erp_fasttab_close();

	// Standalone customer creation — an ERP-only tenant can add a customer master
	// here without any storefront registration; it appears in the picker above.
	$soaCountries = function_exists('epc_countries_iso3166_alpha2')
		? epc_countries_iso3166_alpha2()
		: array('AE' => 'United Arab Emirates', 'SA' => 'Saudi Arabia', 'OM' => 'Oman', 'IN' => 'India', 'GB' => 'United Kingdom', 'US' => 'United States');
	ob_start();
	?>
	<form id="epc_erp_form_customer" class="form-horizontal" style="max-width:760px;">
		<input type="hidden" name="csrf_guard_key" value="<?php echo epc_erp_h($csrf); ?>">
		<div class="form-group"><label class="col-sm-3">Name</label><div class="col-sm-9"><input name="name" class="form-control input-sm" placeholder="Customer / company name" required></div></div>
		<div class="form-group"><label class="col-sm-3">Email / Phone</label><div class="col-sm-9 form-inline">
			<input name="email" type="email" class="form-control input-sm" placeholder="Email (optional)">
			<input name="phone" class="form-control input-sm" placeholder="Phone (optional)">
		</div></div>
		<div class="form-group"><label class="col-sm-3">TRN / Country</label><div class="col-sm-9 form-inline">
			<input name="trn" class="form-control input-sm" placeholder="Tax / TRN no.">
			<select name="country_code" class="form-control input-sm" style="width:200px;">
			<?php foreach ($soaCountries as $cc => $cname): ?>
				<option value="<?php echo epc_erp_h($cc); ?>"<?php echo $cc === 'AE' ? ' selected' : ''; ?>><?php echo epc_erp_h($cc . ' — ' . $cname); ?></option>
			<?php endforeach; ?>
			</select>
		</div></div>
		<?php echo epc_erp_dim_render_fields($db_link); ?>
		<div class="form-group"><div class="col-sm-offset-3 col-sm-9"><button type="submit" class="btn btn-success btn-sm"><i class="fa fa-user-plus"></i> Add customer</button></div></div>
	</form>
	<?php
	$epcCustFormHtml = ob_get_clean();
	erp_fasttab_open('New customer (standalone)', array('open' => false, 'icon' => 'fa-user-plus'));
	echo $epcCustFormHtml;
	erp_fasttab_close();
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
