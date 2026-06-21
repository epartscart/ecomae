<?php
defined('_ASTEXE_') or die('No access');
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_extended.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_ui.php';

$month = isset($_GET['agenda_month']) ? (string) $_GET['agenda_month'] : date('Y-m');
if (!preg_match('/^\d{4}-\d{2}$/', $month)) {
	$month = date('Y-m');
}
$events = epc_erp_agenda_list($db_link, $month);

erp_page_header(
	'<i class="fa fa-calendar"></i> Shared agenda',
	'Team calendar for meetings, calls, and CRM-linked activities.',
	array(
		array('label' => 'ERP', 'url' => epc_erp_tab_url($erpUrl, 'dashboard', $date_from_str, $date_to_str)),
		array('label' => 'Agenda'),
	)
);
erp_filter_bar($erpUrl, 'agenda', $date_from_str, $date_to_str,
	'<label>Month</label> <input type="month" name="agenda_month" class="form-control input-sm" value="' . epc_erp_h($month) . '">'
);
erp_stat_cards(array(
	array('label' => 'Events this month', 'value' => (string) count($events)),
));
ob_start();
if (empty($events)) {
	erp_empty_state('No events for ' . $month . '. Add a meeting or link a CRM activity.', 'fa-calendar-o');
} else {
	erp_table_open(array('Date', 'Time', 'Title', 'Type', 'Location', 'Linked'));
	foreach ($events as $e) {
		echo '<tr><td>' . epc_erp_h(date('Y-m-d', (int) $e['start_at'])) . '</td>';
		echo '<td>' . epc_erp_h(date('H:i', (int) $e['start_at'])) . '–' . epc_erp_h(date('H:i', (int) $e['end_at'])) . '</td>';
		echo '<td>' . epc_erp_h($e['title']) . '</td><td>' . epc_erp_h($e['event_type']) . '</td>';
		echo '<td>' . epc_erp_h($e['location'] ?: '—') . '</td>';
		echo '<td>' . (!empty($e['entity_type']) ? epc_erp_h($e['entity_type'] . ' #' . (int) $e['entity_id']) : '—') . '</td></tr>';
	}
	erp_table_close();
}
erp_section_card('Month list view', ob_get_clean(), array('icon' => 'fa-list'));
ob_start();
?>
<form id="epc_erp_form_agenda" class="form-horizontal" style="max-width:720px;">
	<input type="hidden" name="csrf_guard_key" value="<?php echo epc_erp_h($csrf); ?>">
	<div class="form-group"><label class="col-sm-3">Title</label><div class="col-sm-9"><input name="title" class="form-control input-sm" required></div></div>
	<div class="form-group"><label class="col-sm-3">Start / end</label><div class="col-sm-9 form-inline">
		<input name="start_at" type="datetime-local" class="form-control input-sm" required>
		<input name="end_at" type="datetime-local" class="form-control input-sm"></div></div>
	<div class="form-group"><label class="col-sm-3">Type / location</label><div class="col-sm-9 form-inline">
		<select name="event_type" class="form-control input-sm"><option value="meeting">Meeting</option><option value="call">Call</option><option value="task">Task</option></select>
		<input name="location" class="form-control input-sm" placeholder="Location"></div></div>
	<div class="form-group"><div class="col-sm-offset-3 col-sm-9"><button type="submit" class="btn btn-primary btn-sm">Add event</button></div></div>
</form>
<?php
erp_section_card('New event', ob_get_clean(), array('icon' => 'fa-plus'));
