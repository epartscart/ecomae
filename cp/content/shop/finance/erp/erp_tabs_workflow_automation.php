<?php
/**
 * ERP tab — Workflow Automation (no-code workflow builder).
 *
 * Build trigger -> condition -> action workflows for automating
 * ERP processes: approvals, notifications, status updates, etc.
 */
defined('_ASTEXE_') or die('No access');
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_ui.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_helpers.php';

/* ── Load backend ── */
$_wfOk = false;
$_wfErr = '';
$_wfFile = $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_workflow_builder.php';
if (is_file($_wfFile)) {
	try { require_once $_wfFile; $_wfOk = true; }
	catch (\Throwable $e) { $_wfErr = $e->getMessage(); }
}

/* ── Schema ── */
if ($_wfOk && isset($db_link) && $db_link instanceof PDO && function_exists('epc_workflow_ensure_schema')) {
	try { epc_workflow_ensure_schema($db_link); }
	catch (\Throwable $e) { /* schema failed — continue */ }
}

/* ── Request params ── */
$wfAction = isset($_GET['wf_action']) ? (string)$_GET['wf_action'] : 'list';
$wfId     = isset($_GET['wf_id'])     ? (int)$_GET['wf_id']       : 0;
$csrfLocal = isset($csrf) ? $csrf : '';
$wfBase   = epc_erp_tab_url($erpUrl, 'workflow_automation', $date_from_str, $date_to_str, 'setup');
$triggerTypes = function_exists('epc_workflow_trigger_types') ? epc_workflow_trigger_types() : array();
$actionTypes  = function_exists('epc_workflow_action_types')  ? epc_workflow_action_types()  : array();

/* ── Site key for tenant isolation ── */
$_wfSiteKey = '';
if (function_exists('epc_erp_site_key')) {
	$_wfSiteKey = epc_erp_site_key();
} elseif (isset($site_key)) {
	$_wfSiteKey = (string)$site_key;
}

/* ── Page header ── */
erp_page_header(
	'<i class="fa fa-cogs"></i> Workflow automation',
	'Build no-code workflows: trigger &rarr; condition &rarr; action.',
	array(
		array('label' => 'ERP', 'url' => epc_erp_tab_url($erpUrl, 'dashboard', $date_from_str, $date_to_str)),
		array('label' => 'System administration'),
		array('label' => 'Workflow automation'),
	),
	array(
		array('label' => 'New workflow', 'url' => $wfBase . '&wf_action=edit', 'class' => 'btn-primary', 'icon' => 'fa-plus'),
	)
);

/* ── Backend warning ── */
if ($_wfErr !== '') {
	echo '<div class="alert alert-warning" style="margin:10px"><i class="fa fa-exclamation-triangle"></i> Workflow backend: ' . epc_erp_h($_wfErr) . '</div>';
}

/* ── Preset workflow templates ── */
$presets = array(
	array('name' => 'PO Approval Chain',         'trigger' => 'event',    'icon' => 'fa-check-circle',   'desc' => 'Route purchase orders through manager &rarr; finance &rarr; director approval based on amount thresholds.'),
	array('name' => 'Invoice Auto-Send',          'trigger' => 'event',    'icon' => 'fa-paper-plane',    'desc' => 'Automatically email invoices to customers when an order is completed.'),
	array('name' => 'Low Stock Alert',            'trigger' => 'event',    'icon' => 'fa-exclamation-triangle', 'desc' => 'Notify procurement when inventory falls below reorder point.'),
	array('name' => 'VAT Filing Reminder',        'trigger' => 'schedule', 'icon' => 'fa-calendar-check-o','desc' => 'Send reminders 7 days before VAT return deadline.'),
	array('name' => 'Overdue Invoice Escalation', 'trigger' => 'schedule', 'icon' => 'fa-clock-o',        'desc' => 'Escalate overdue invoices through dunning sequence after 30/60/90 days.'),
	array('name' => 'Employee Onboarding',        'trigger' => 'event',    'icon' => 'fa-user-plus',      'desc' => 'Create onboarding tasks when a new employee record is created.'),
	array('name' => 'Daily Sales Summary',        'trigger' => 'schedule', 'icon' => 'fa-bar-chart',      'desc' => 'Email daily sales summary to management at 6 PM.'),
	array('name' => 'AML Compliance Alert',       'trigger' => 'event',    'icon' => 'fa-shield',         'desc' => 'Flag transactions exceeding AML thresholds for compliance review.'),
);

/* ============================================================
 *  EDIT VIEW
 * ============================================================ */
if ($wfAction === 'edit') {
	$wf = null;
	if ($wfId > 0 && $_wfOk && isset($db_link) && $db_link instanceof PDO && function_exists('epc_workflow_get')) {
		try { $wf = epc_workflow_get($db_link, $wfId); } catch (\Throwable $e) { $wf = null; }
	}

	$_f = function ($key, $default = '') use ($wf) {
		return isset($wf[$key]) ? $wf[$key] : $default;
	};

	echo '<div class="ef-window">';
	echo '<div class="ef-title"><i class="fa fa-cogs"></i> ' . ($wf ? 'Edit' : 'New') . ' Workflow</div>';
	echo '<div class="ef-body">';
	echo '<form method="post" action="' . epc_erp_h(isset($erpAjaxUrl) ? $erpAjaxUrl : '') . '" class="epc-erp-form">';
	echo '<input type="hidden" name="action" value="workflow_save">';
	echo '<input type="hidden" name="csrf_guard_key" value="' . epc_erp_h($csrfLocal) . '">';
	echo '<input type="hidden" name="id" value="' . (int)$_f('id', 0) . '">';

	// General section
	echo '<div class="row"><div class="col-sm-6">';
	echo '<div class="ef-section"><span class="ef-section-title"><i class="fa fa-cog"></i> General</span>';
	echo '<table class="ef-grid">';
	echo '<tr><td style="width:140px"><label>Name</label></td><td><input type="text" name="name" class="form-control input-sm" value="' . epc_erp_h($_f('name')) . '" required></td></tr>';
	echo '<tr><td><label>Description</label></td><td><textarea name="description" class="form-control input-sm" rows="2">' . epc_erp_h($_f('description')) . '</textarea></td></tr>';
	echo '<tr><td><label>Active</label></td><td><label><input type="checkbox" name="active" value="1"' . (!empty($wf['active']) ? ' checked' : '') . '> Enable this workflow</label></td></tr>';
	echo '</table></div>';
	echo '</div>'; // col

	// Trigger section
	echo '<div class="col-sm-6">';
	echo '<div class="ef-section"><span class="ef-section-title"><i class="fa fa-bolt"></i> Trigger</span>';
	echo '<table class="ef-grid">';
	echo '<tr><td style="width:140px"><label>Trigger type</label></td><td><select name="trigger_type" class="form-control input-sm">';
	foreach ($triggerTypes as $tt) {
		$sel = ($_f('trigger_type', 'manual') === $tt['type']) ? ' selected' : '';
		echo '<option value="' . epc_erp_h($tt['type']) . '"' . $sel . '>' . epc_erp_h($tt['label']) . '</option>';
	}
	echo '</select></td></tr>';
	echo '<tr><td><label>Config (JSON)</label></td><td><textarea name="trigger_config" class="form-control input-sm" rows="3" placeholder=\'{"event_type":"order.placed"}\'>' . epc_erp_h($_f('trigger_config', '{}')) . '</textarea></td></tr>';
	echo '</table></div>';
	echo '</div></div>'; // col, row

	// Steps section
	echo '<div class="ef-section" style="margin-top:8px"><span class="ef-section-title"><i class="fa fa-list-ol"></i> Steps (actions)</span>';
	echo '<p class="text-muted">Add workflow steps. Each step performs an action when conditions are met.</p>';
	echo '<table class="ef-grid"><thead><tr><th>#</th><th>Action</th><th>Label</th><th>Config (JSON)</th></tr></thead><tbody>';
	$steps = (isset($wf['steps']) && is_array($wf['steps'])) ? $wf['steps'] : array();
	if (empty($steps)) {
		$steps = array(array('action_type' => 'send_notification', 'label' => 'Notify team', 'config' => '{}'));
	}
	foreach ($steps as $si => $step) {
		echo '<tr>';
		echo '<td>' . ($si + 1) . '</td>';
		echo '<td><select name="steps[' . $si . '][action_type]" class="form-control input-sm">';
		foreach ($actionTypes as $at) {
			$sel = (isset($step['action_type']) && $step['action_type'] === $at['type']) ? ' selected' : '';
			echo '<option value="' . epc_erp_h($at['type']) . '"' . $sel . '>' . epc_erp_h($at['label']) . '</option>';
		}
		echo '</select></td>';
		echo '<td><input type="text" name="steps[' . $si . '][label]" class="form-control input-sm" value="' . epc_erp_h(isset($step['label']) ? $step['label'] : '') . '"></td>';
		echo '<td><input type="text" name="steps[' . $si . '][config]" class="form-control input-sm" value="' . epc_erp_h(isset($step['config']) ? (is_string($step['config']) ? $step['config'] : json_encode($step['config'])) : '{}') . '"></td>';
		echo '</tr>';
	}
	echo '</tbody></table></div>';

	// Actions
	echo '<div class="ef-actions" style="margin-top:12px">';
	echo '<button type="submit" class="btn btn-primary btn-sm"><i class="fa fa-save"></i> Save workflow</button>';
	echo ' <a href="' . epc_erp_h($wfBase) . '" class="btn btn-default btn-sm">Cancel</a>';
	echo '</div>';

	echo '</form></div></div>'; // form, ef-body, ef-window

/* ============================================================
 *  LIST VIEW
 * ============================================================ */
} else {
	$workflows = array();
	if ($_wfOk && isset($db_link) && $db_link instanceof PDO && function_exists('epc_workflow_list')) {
		try { $workflows = epc_workflow_list($db_link, $_wfSiteKey); } catch (\Throwable $e) { $workflows = array(); }
	}

	// Preset templates section
	echo '<div class="ef-window" style="margin-bottom:12px">';
	echo '<div class="ef-title"><i class="fa fa-magic"></i> Workflow Templates</div>';
	echo '<div class="ef-body"><div class="row">';
	foreach ($presets as $p) {
		echo '<div class="col-sm-3" style="margin-bottom:10px">';
		echo '<div style="border:1px solid #e0e0e0;border-radius:6px;padding:12px;height:100%;background:#fafafa">';
		echo '<h5 style="margin-top:0"><i class="fa ' . epc_erp_h($p['icon']) . '"></i> ' . epc_erp_h($p['name']) . '</h5>';
		echo '<p class="text-muted" style="font-size:12px;margin-bottom:8px">' . $p['desc'] . '</p>';
		echo '<span class="label label-default">' . epc_erp_h($p['trigger']) . '</span>';
		echo '</div></div>';
	}
	echo '</div></div></div>'; // row, ef-body, ef-window

	// Active workflows
	echo '<div class="ef-window">';
	echo '<div class="ef-title"><i class="fa fa-cogs"></i> Active Workflows</div>';
	echo '<div class="ef-toolbar">';
	echo '<a href="' . epc_erp_h($wfBase . '&wf_action=edit') . '" class="btn btn-primary btn-xs"><i class="fa fa-plus"></i> New workflow</a>';
	echo '</div>';
	echo '<div class="ef-body">';

	if (empty($workflows)) {
		echo '<div style="padding:20px;text-align:center;color:#999"><i class="fa fa-cogs fa-3x"></i><br>No workflows yet. Use a template above or <a href="' . epc_erp_h($wfBase . '&wf_action=edit') . '">create one from scratch</a>.</div>';
	} else {
		echo '<table class="ef-grid"><thead><tr><th>Name</th><th>Trigger</th><th>Status</th><th>Runs</th><th>Last run</th><th></th></tr></thead><tbody>';
		foreach ($workflows as $w) {
			$statusLabel = !empty($w['active']) ? '<span class="label label-success">Active</span>' : '<span class="label label-default">Inactive</span>';
			echo '<tr>';
			echo '<td><strong>' . epc_erp_h(isset($w['name']) ? $w['name'] : '') . '</strong>';
			if (isset($w['description']) && $w['description'] !== '') {
				echo '<br><small class="text-muted">' . epc_erp_h($w['description']) . '</small>';
			}
			echo '</td>';
			echo '<td><span class="label label-info">' . epc_erp_h(isset($w['trigger_type']) ? $w['trigger_type'] : 'manual') . '</span></td>';
			echo '<td>' . $statusLabel . '</td>';
			echo '<td>' . (int)(isset($w['run_count']) ? $w['run_count'] : 0) . '</td>';
			echo '<td><small>' . (isset($w['last_run_at']) && $w['last_run_at'] ? epc_erp_h($w['last_run_at']) : '&mdash;') . '</small></td>';
			echo '<td><a href="' . epc_erp_h($wfBase . '&wf_action=edit&wf_id=' . (int)$w['id']) . '" class="btn btn-default btn-xs"><i class="fa fa-pencil"></i></a></td>';
			echo '</tr>';
		}
		echo '</tbody></table>';
	}

	echo '</div>'; // ef-body
	echo '<div class="ef-status"><span>Workflows: ' . count($workflows) . '</span> <span>Templates: ' . count($presets) . '</span></div>';
	echo '</div>'; // ef-window
}
