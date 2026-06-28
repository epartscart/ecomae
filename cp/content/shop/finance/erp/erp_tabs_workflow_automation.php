<?php
/**
 * ERP tab — Workflow Automation (visual process builder).
 */
defined('_ASTEXE_') or die('No access');
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_ui.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_helpers.php';

$_wfBackendOk = false;
$_wfBackendErr = '';
$wfBackendFile = $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_workflow_builder.php';
if (is_file($wfBackendFile)) {
	try {
		require_once $wfBackendFile;
		$_wfBackendOk = true;
	} catch (\Throwable $e) {
		$_wfBackendErr = $e->getMessage();
	} catch (\Exception $e) {
		$_wfBackendErr = $e->getMessage();
	}
} else {
	$_wfBackendErr = 'File not found: ' . $wfBackendFile;
}

if ($_wfBackendOk && isset($db_link) && $db_link instanceof PDO) {
	try {
		if (function_exists('epc_workflow_ensure_schema')) {
			epc_workflow_ensure_schema($db_link);
		}
	} catch (\Throwable $e) {
		// Schema init failed
	} catch (\Exception $e) {
		// Schema init failed
	}
}

$wfAction = isset($_GET['wf_action']) ? (string)$_GET['wf_action'] : 'list';
$wfId = isset($_GET['wf_id']) ? (int)$_GET['wf_id'] : 0;
$csrfLocal = isset($csrf) ? $csrf : '';
$siteKey = (isset($DP_Config) && isset($DP_Config->site_key)) ? (string)$DP_Config->site_key : '';
$wfBase = epc_erp_tab_url($erpUrl, 'workflow_automation', $date_from_str, $date_to_str, 'setup');

erp_page_header(
	'<i class="fa fa-cogs"></i> Workflow automation',
	'Build approval chains, auto-actions, scheduled tasks, and event-driven processes — no coding required.',
	array(
		array('label' => 'ERP', 'url' => epc_erp_tab_url($erpUrl, 'dashboard', $date_from_str, $date_to_str)),
		array('label' => 'System administration'),
		array('label' => 'Workflow automation'),
	),
	array(
		array('label' => 'New workflow', 'url' => $wfBase . '&wf_action=edit', 'class' => 'btn-primary', 'icon' => 'fa-plus'),
	)
);

$workflows = array();
if (isset($db_link) && $db_link instanceof PDO) {
	try {
		$st = $db_link->prepare('SELECT * FROM epc_workflows WHERE site_key = ? ORDER BY active DESC, name');
		$st->execute(array($siteKey));
		$workflows = $st->fetchAll(PDO::FETCH_ASSOC);
		if (!is_array($workflows)) {
			$workflows = array();
		}
	} catch (\Throwable $e) {
		$workflows = array();
	} catch (\Exception $e) {
		$workflows = array();
	}
}

$presets = array(
	array('name' => 'PO Approval — 3-Tier', 'description' => 'Purchase orders require Manager, Finance, Director approval when amount exceeds threshold.', 'trigger_type' => 'event', 'category' => 'Procurement', 'icon' => 'fa-shopping-cart'),
	array('name' => 'Invoice auto-send on validation', 'description' => 'Automatically email the PDF invoice to the customer when invoice status changes to Validated.', 'trigger_type' => 'event', 'category' => 'Sales', 'icon' => 'fa-paper-plane'),
	array('name' => 'Low stock alert', 'description' => 'Send notification when inventory item quantity drops below reorder level.', 'trigger_type' => 'event', 'category' => 'Inventory', 'icon' => 'fa-exclamation-triangle'),
	array('name' => 'Monthly VAT preparation reminder', 'description' => 'Send reminder 7 days before VAT return due date to prepare filing.', 'trigger_type' => 'schedule', 'category' => 'Compliance', 'icon' => 'fa-calendar-check-o'),
	array('name' => 'Customer payment overdue escalation', 'description' => 'Escalate to finance manager when customer payment is 30+ days overdue.', 'trigger_type' => 'schedule', 'category' => 'Finance', 'icon' => 'fa-clock-o'),
	array('name' => 'New employee onboarding checklist', 'description' => 'Create onboarding tasks when new employee is added to HR module.', 'trigger_type' => 'event', 'category' => 'HR', 'icon' => 'fa-user-plus'),
	array('name' => 'Daily sales summary email', 'description' => 'Send daily summary of sales, returns, and top items to management.', 'trigger_type' => 'schedule', 'category' => 'Sales', 'icon' => 'fa-bar-chart'),
	array('name' => 'AML — large cash transaction alert', 'description' => 'Flag cash transactions above AED 55,000 for AML review (jewellery DPMS).', 'trigger_type' => 'event', 'category' => 'Compliance', 'icon' => 'fa-shield'),
);

if ($wfAction === 'edit'):
	$wf = null;
	$steps = array();
	if ($wfId > 0 && isset($db_link) && $db_link instanceof PDO) {
		try {
			$st = $db_link->prepare('SELECT * FROM epc_workflows WHERE id = ? AND site_key = ?');
			$st->execute(array($wfId, $siteKey));
			$wf = $st->fetch(PDO::FETCH_ASSOC);
			if ($wf) {
				$sSt = $db_link->prepare('SELECT * FROM epc_workflow_steps WHERE workflow_id = ? ORDER BY step_order');
				$sSt->execute(array($wfId));
				$steps = $sSt->fetchAll(PDO::FETCH_ASSOC);
				if (!is_array($steps)) { $steps = array(); }
			}
		} catch (\Throwable $e) {
			$wf = null;
			$steps = array();
		} catch (\Exception $e) {
			$wf = null;
			$steps = array();
		}
	}
?>
<div class="ef-window">
	<div class="ef-title"><i class="fa fa-cogs"></i> <?php echo $wf ? 'Edit' : 'New'; ?> Workflow</div>
	<div class="ef-body">
		<form method="post" action="<?php echo epc_erp_h(isset($erpAjaxUrl) ? $erpAjaxUrl : ''); ?>" class="epc-erp-form">
			<input type="hidden" name="action" value="workflow_save">
			<input type="hidden" name="csrf_guard_key" value="<?php echo epc_erp_h($csrfLocal); ?>">
			<input type="hidden" name="id" value="<?php echo (int)(isset($wf['id']) ? $wf['id'] : 0); ?>">

			<div class="ef-section">
				<span class="ef-section-title"><i class="fa fa-info-circle"></i> Workflow details</span>
				<table class="ef-grid">
					<tr><td style="width:140px"><label>Name</label></td><td><input type="text" name="name" class="form-control input-sm" value="<?php echo epc_erp_h(isset($wf['name']) ? $wf['name'] : ''); ?>" required></td></tr>
					<tr><td><label>Description</label></td><td><textarea name="description" class="form-control input-sm" rows="2"><?php echo epc_erp_h(isset($wf['description']) ? $wf['description'] : ''); ?></textarea></td></tr>
					<tr><td><label>Trigger type</label></td><td>
						<select name="trigger_type" class="form-control input-sm">
							<option value="event"<?php echo (isset($wf['trigger_type']) && $wf['trigger_type'] === 'event') ? ' selected' : ''; ?>>Event-driven (on record change)</option>
							<option value="schedule"<?php echo (isset($wf['trigger_type']) && $wf['trigger_type'] === 'schedule') ? ' selected' : ''; ?>>Scheduled (cron)</option>
							<option value="manual"<?php echo (!isset($wf['trigger_type']) || $wf['trigger_type'] === 'manual') ? ' selected' : ''; ?>>Manual (button trigger)</option>
							<option value="webhook"<?php echo (isset($wf['trigger_type']) && $wf['trigger_type'] === 'webhook') ? ' selected' : ''; ?>>Webhook (external API call)</option>
						</select>
					</td></tr>
					<tr><td><label>Active</label></td><td><label><input type="checkbox" name="active" value="1"<?php echo !empty($wf['active']) ? ' checked' : ''; ?>> Enable this workflow</label></td></tr>
				</table>
			</div>

			<div class="ef-section" style="margin-top:8px">
				<span class="ef-section-title"><i class="fa fa-list-ol"></i> Steps (<?php echo count($steps); ?>)</span>
				<?php if (!empty($steps)): ?>
				<table class="ef-grid">
					<thead><tr><th>#</th><th>Type</th><th>Action</th><th>On failure</th></tr></thead>
					<tbody>
					<?php foreach ($steps as $s): ?>
						<tr>
							<td><?php echo (int)$s['step_order']; ?></td>
							<td><span class="label label-info"><?php echo epc_erp_h($s['step_type']); ?></span></td>
							<td><?php echo epc_erp_h($s['action_type']); ?></td>
							<td><span class="label label-default"><?php echo epc_erp_h($s['on_failure']); ?></span></td>
						</tr>
					<?php endforeach; ?>
					</tbody>
				</table>
				<?php else: ?>
				<div style="padding:10px;color:#999;font-size:12px;"><i class="fa fa-info-circle"></i> Steps will be configurable after saving the workflow.</div>
				<?php endif; ?>
			</div>

			<div class="ef-actions" style="margin-top:12px">
				<button type="submit" class="btn btn-primary btn-sm"><i class="fa fa-save"></i> Save workflow</button>
				<a href="<?php echo epc_erp_h($wfBase); ?>" class="btn btn-default btn-sm">Cancel</a>
			</div>
		</form>
	</div>
</div>

<?php else: // list view ?>
<div class="ef-window">
	<div class="ef-title"><i class="fa fa-cogs"></i> Active Workflows</div>
	<div class="ef-toolbar">
		<a href="<?php echo epc_erp_h($wfBase . '&wf_action=edit'); ?>" class="btn btn-primary btn-xs"><i class="fa fa-plus"></i> New</a>
	</div>
	<div class="ef-body">
		<?php if (!empty($_wfBackendErr)): ?>
			<div class="alert alert-warning" style="margin:10px"><i class="fa fa-exclamation-triangle"></i> Workflow backend: <?php echo epc_erp_h($_wfBackendErr); ?></div>
		<?php endif; ?>
		<?php if (empty($workflows)): ?>
			<div style="padding:20px;text-align:center;color:#999;"><i class="fa fa-cogs fa-3x"></i><br>No workflows yet.</div>
		<?php else: ?>
			<table class="ef-grid">
				<thead><tr><th>Name</th><th>Trigger</th><th>Runs</th><th>Last run</th><th>Status</th><th></th></tr></thead>
				<tbody>
				<?php foreach ($workflows as $w): ?>
					<tr>
						<td><strong><?php echo epc_erp_h($w['name']); ?></strong><br><small class="text-muted"><?php echo epc_erp_h(substr(isset($w['description']) ? $w['description'] : '', 0, 60)); ?></small></td>
						<td><span class="label label-info"><?php echo epc_erp_h($w['trigger_type']); ?></span></td>
						<td><?php echo (int)$w['run_count']; ?></td>
						<td><small><?php echo !empty($w['last_run_at']) ? epc_erp_h($w['last_run_at']) : '&mdash;'; ?></small></td>
						<td><?php echo $w['active'] ? '<span class="label label-success">Active</span>' : '<span class="label label-default">Inactive</span>'; ?></td>
						<td><a href="<?php echo epc_erp_h($wfBase . '&wf_action=edit&wf_id=' . (int)$w['id']); ?>" class="btn btn-default btn-xs"><i class="fa fa-pencil"></i></a></td>
					</tr>
				<?php endforeach; ?>
				</tbody>
			</table>
		<?php endif; ?>
	</div>
</div>

<div class="ef-window" style="margin-top:12px">
	<div class="ef-title"><i class="fa fa-magic"></i> Workflow Templates — Quick Start</div>
	<div class="ef-body">
		<div style="font-size:12px;color:#4a6a7a;margin-bottom:10px;">Click any preset to create a workflow from a proven template. Customise triggers, conditions, and actions after creation.</div>
		<div class="row">
			<?php foreach ($presets as $p): ?>
			<div class="col-sm-6 col-md-3" style="margin-bottom:10px;">
				<div style="border:1px solid #e0e8f0;border-radius:6px;padding:12px;background:#fff;height:100%;cursor:pointer;" onclick="location='<?php echo epc_erp_h($wfBase . '&wf_action=edit'); ?>'">
					<div style="font-size:22px;color:#1565c0;margin-bottom:6px;"><i class="fa <?php echo epc_erp_h($p['icon']); ?>"></i></div>
					<div style="font-weight:600;font-size:12px;"><?php echo epc_erp_h($p['name']); ?></div>
					<div style="font-size:11px;color:#888;margin-top:4px;"><?php echo epc_erp_h($p['description']); ?></div>
					<div style="margin-top:6px;">
						<span class="label label-default"><?php echo epc_erp_h($p['category']); ?></span>
						<span class="label label-info"><?php echo epc_erp_h($p['trigger_type']); ?></span>
					</div>
				</div>
			</div>
			<?php endforeach; ?>
		</div>
	</div>
</div>
<?php endif; ?>
