<?php
defined('_ASTEXE_') or die('No access');
/**
 * Sales and marketing — Prospects / Leads (top of the sales pipeline).
 * Lead capture and qualification, then convert a qualified lead into an
 * opportunity. Reuses the CRM lead engine; CRUD posts the whitelisted
 * save_lead / convert_lead actions handled by ajax_crm.php.
 */
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_crm_helpers.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_crm_access.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_ui.php';

if (!function_exists('epc_crm_pack_enabled') || !epc_crm_pack_enabled() || !epc_crm_user_can_access($db_link)) {
	echo '<div class="alert alert-warning"><strong>Prospects/Leads</strong> requires the CRM/Finance pack.</div>';
	return;
}
epc_crm_ensure_schema($db_link);

$csrfLocal = isset($csrf) ? $csrf : '';
$statusFilter = isset($_GET['lead_status']) ? (string) $_GET['lead_status'] : '';
$leads = epc_crm_list_leads($db_link, $statusFilter);
$statuses = epc_crm_lead_statuses();
$oppUrl = epc_erp_tab_url($erpUrl, 'opportunities', $date_from_str, $date_to_str, 'sales');

$counts = array('new' => 0, 'contacted' => 0, 'qualified' => 0, 'converted' => 0);
foreach ($leads as $l) {
	$s = (string) ($l['status'] ?? '');
	if (isset($counts[$s])) {
		$counts[$s]++;
	}
}

erp_page_header(
	'<i class="fa fa-user-plus"></i> Prospects &amp; leads',
	'Capture and qualify prospects, then convert a qualified lead into an opportunity.',
	array(
		array('label' => 'ERP', 'url' => epc_erp_tab_url($erpUrl, 'dashboard', $date_from_str, $date_to_str)),
		array('label' => 'Sales and marketing'),
		array('label' => 'Prospects & leads'),
	),
	array(array('label' => 'Pipeline / opportunities', 'url' => $oppUrl, 'class' => 'btn-primary', 'icon' => 'fa-filter'))
);

erp_stat_cards(array(
	array('label' => 'Total leads', 'value' => (string) count($leads)),
	array('label' => 'New', 'value' => (string) $counts['new']),
	array('label' => 'Qualified', 'value' => (string) $counts['qualified']),
	array('label' => 'Converted', 'value' => (string) $counts['converted']),
));

erp_filter_bar($erpUrl, 'leads', $date_from_str, $date_to_str,
	'<label>Status</label> <select name="lead_status" class="form-control input-sm" onchange="this.form.submit()"><option value="">All</option>'
	. implode('', array_map(function ($k, $v) use ($statusFilter) {
		return '<option value="' . epc_erp_h($k) . '"' . ($statusFilter === $k ? ' selected' : '') . '>' . epc_erp_h($v) . '</option>';
	}, array_keys($statuses), array_values($statuses)))
	. '</select>'
);
?>
<div id="epc_erp_msg" class="alert" style="display:none;"></div>

<div class="row">
<div class="col-md-5">
<?php
ob_start(); ?>
<form id="epc_lead_form" class="form">
	<input type="hidden" name="csrf_guard_key" value="<?php echo epc_erp_h($csrfLocal); ?>">
	<input type="hidden" name="id" value="0">
	<div class="form-group"><label>Company</label><input type="text" name="company" class="form-control input-sm" required></div>
	<div class="row">
		<div class="col-xs-6 form-group"><label>Contact name</label><input type="text" name="contact_name" class="form-control input-sm"></div>
		<div class="col-xs-6 form-group"><label>Source</label>
			<select name="source" class="form-control input-sm">
				<option value="web">Web</option><option value="referral">Referral</option>
				<option value="campaign">Campaign</option><option value="event">Event</option>
				<option value="cold">Cold call</option><option value="other">Other</option>
			</select>
		</div>
	</div>
	<div class="row">
		<div class="col-xs-6 form-group"><label>Email</label><input type="email" name="email" class="form-control input-sm"></div>
		<div class="col-xs-6 form-group"><label>Phone</label><input type="text" name="phone" class="form-control input-sm"></div>
	</div>
	<div class="row">
		<div class="col-xs-6 form-group"><label>Status</label>
			<select name="status" class="form-control input-sm">
				<?php foreach ($statuses as $k => $v): ?><option value="<?php echo epc_erp_h($k); ?>"><?php echo epc_erp_h($v); ?></option><?php endforeach; ?>
			</select>
		</div>
		<div class="col-xs-6 form-group"><label>Expected value</label><input type="number" step="0.01" name="expected_value" class="form-control input-sm"></div>
	</div>
	<div class="form-group"><label>Notes</label><textarea name="notes" rows="2" class="form-control input-sm"></textarea></div>
	<button class="btn btn-success btn-sm" type="submit"><i class="fa fa-plus"></i> Save lead</button>
</form>
<?php
erp_section_card('New / edit lead', ob_get_clean(), array('icon' => 'fa-user-plus'));
?>
</div>
<div class="col-md-7">
<?php
ob_start();
if (empty($leads)) {
	erp_empty_state('No leads yet. Capture your first prospect on the left.', 'fa-user-plus');
} else {
	erp_table_open(array('Company', 'Contact', 'Source', 'Status', 'Value', ''));
	foreach ($leads as $l) {
		$converted = ((string) ($l['status'] ?? '') === 'converted');
		echo '<tr><td><strong>' . epc_erp_h($l['company']) . '</strong><br><small class="text-muted">' . epc_erp_h($l['email'] ?: '') . '</small></td>';
		echo '<td>' . epc_erp_h($l['contact_name'] ?: '—') . '</td>';
		echo '<td>' . epc_erp_h($l['source']) . '</td>';
		echo '<td><span class="label label-' . ($converted ? 'success' : 'default') . '">' . epc_erp_h($statuses[$l['status']] ?? $l['status']) . '</span></td>';
		echo '<td>' . epc_erp_money($l['expected_value']) . '</td>';
		echo '<td>';
		if (!$converted) {
			echo '<button class="btn btn-xs btn-primary epc-lead-convert" data-id="' . (int) $l['id'] . '" data-title="' . epc_erp_h($l['company']) . '"><i class="fa fa-arrow-right"></i> Convert</button>';
		} else {
			echo '<a class="btn btn-xs btn-default" href="' . epc_erp_h($oppUrl) . '">View pipeline</a>';
		}
		echo '</td></tr>';
	}
	erp_table_close();
}
erp_section_card('Lead list', ob_get_clean(), array('icon' => 'fa-list'));
?>
</div>
</div>

<script>
(function () {
	var endpoint = <?php echo json_encode($erpAjaxEndpoint); ?>;
	var msg = document.getElementById('epc_erp_msg');
	function flash(ok, text) {
		if (!msg) { return; }
		msg.className = 'alert alert-' + (ok ? 'success' : 'danger');
		msg.style.display = 'block';
		msg.textContent = text;
	}
	function post(data, cb) {
		var fd = new FormData();
		Object.keys(data).forEach(function (k) { fd.append(k, data[k]); });
		fetch(endpoint, { method: 'POST', body: fd, credentials: 'same-origin' })
			.then(function (r) { return r.json(); })
			.then(function (j) { flash(!!j.status, j.message || (j.status ? 'Saved' : 'Error')); if (j.status) { cb && cb(j); } })
			.catch(function () { flash(false, 'Network error'); });
	}
	var form = document.getElementById('epc_lead_form');
	if (form) {
		form.addEventListener('submit', function (ev) {
			ev.preventDefault();
			var d = { action: 'save_lead' };
			new FormData(form).forEach(function (v, k) { d[k] = v; });
			post(d, function () { setTimeout(function () { location.reload(); }, 600); });
		});
	}
	document.querySelectorAll('.epc-lead-convert').forEach(function (b) {
		b.addEventListener('click', function () {
			var d = { action: 'convert_lead', lead_id: b.getAttribute('data-id'),
				title: b.getAttribute('data-title'), csrf_guard_key: <?php echo json_encode($csrfLocal); ?> };
			post(d, function () { setTimeout(function () { location.reload(); }, 700); });
		});
	});
})();
</script>
