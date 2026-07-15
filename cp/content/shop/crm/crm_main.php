<?php
/**
 * CRM module — main CP page (dashboard, pipeline, leads, opportunities, activities).
 */
defined('_ASTEXE_') or die('No access');

require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_crm_helpers.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_crm_access.php';

if (!epc_crm_pack_enabled()) {
	echo '<div class="alert alert-warning"><strong>CRM pack not enabled.</strong> Enable the CRM pack in CP → Industry / portal settings.</div>';
	return;
}

if (!epc_crm_user_can_access($db_link)) {
	echo '<div class="alert alert-danger"><strong>Access denied.</strong> You need ERP or administrator access to open CRM.</div>';
	return;
}

if ($_SERVER['REQUEST_METHOD'] === 'POST' && !empty($_POST['action'])) {
	require __DIR__ . '/ajax_crm.php';
	exit;
}

epc_crm_ensure_schema($db_link);

$embedInErp = !empty($GLOBALS['epc_crm_embed_in_erp']);
$urls = epc_crm_configure_urls($embedInErp);
extract($urls);

$allTabs = array(
	'dashboard' => 'Dashboard',
	'pipeline' => 'Pipeline',
	'leads' => 'Leads',
	'opportunities' => 'Opps',
	'quotes' => 'Quotes',
	'activities' => 'Activities',
	'tickets' => 'Support',
	'projects' => 'Projects',
	'contracts' => 'Contracts',
	'expenses' => 'Expenses',
);
$tabParam = $embedInErp ? 'crm_tab' : 'tab';
$tab = isset($_GET[$tabParam]) ? (string) $_GET[$tabParam] : 'dashboard';
if (!isset($allTabs[$tab])) {
	$tab = 'dashboard';
}

if (!isset($user_session) || !is_array($user_session)) {
	require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_access.php';
	$user_session = epc_erp_resolve_user_session();
}
$csrf = isset($user_session['csrf_guard_key']) ? (string)$user_session['csrf_guard_key'] : '';

$crmListLimit = max(50, min(2000, (int)($_GET['list_limit'] ?? 200)));
$dash = ($tab === 'dashboard') ? epc_crm_dashboard_extended($db_link) : array();
$board = ($tab === 'pipeline' || $tab === 'dashboard') ? epc_crm_pipeline_board($db_link) : array();
$leads = ($tab === 'leads') ? epc_crm_list_leads($db_link, '', $crmListLimit) : array();
$opps = ($tab === 'opportunities') ? epc_crm_list_opportunities($db_link, '', $crmListLimit) : array();
$activities = ($tab === 'activities') ? epc_crm_list_activities($db_link, '', 0, $crmListLimit) : array();
$quotes = ($tab === 'quotes') ? epc_crm_list_quotes($db_link) : array();
$tickets = ($tab === 'tickets') ? epc_crm_list_tickets($db_link) : array();
$projects = ($tab === 'projects') ? epc_crm_list_projects($db_link) : array();
$contracts = ($tab === 'contracts') ? epc_crm_list_contracts($db_link) : array();
$contractsDue = ($tab === 'contracts') ? epc_crm_contracts_due_schedule($db_link) : array();
$expenses = ($tab === 'expenses') ? epc_crm_list_expenses($db_link) : array();
$crmActionPrefix = $embedInErp ? 'crm_' : '';

function epc_crm_tab_url($base, $t)
{
	if (!empty($GLOBALS['epc_crm_embed_in_erp'])) {
		$sep = (strpos($base, '?') !== false) ? '&' : '?';
		return $base . $sep . 'crm_tab=' . urlencode($t);
	}
	return $base . '?tab=' . urlencode($t);
}
?>

<link rel="stylesheet" href="/content/shop/finance/epc_erp_ui.css?v=20260527">
<link rel="stylesheet" href="/content/shop/finance/epc_crm_ui.css?v=20260527">
<link rel="stylesheet" href="/content/shop/finance/epc_erp_professional.css?v=20260527">
<style>
.epc-crm-shell .epc-crm-form-inline input, .epc-crm-shell .epc-crm-form-inline select { margin: 2px 4px 2px 0; }
.epc-crm-pipeline-drop { min-height: 40px; }
.epc-crm-pipeline-drop.epc-crm-drop-over { background: rgba(66,139,202,0.08); outline: 2px dashed #428bca; border-radius: 4px; }
.epc-crm-card[draggable="true"] { cursor: grab; }
.epc-crm-card.epc-crm-dragging { opacity: 0.4; }
.epc-crm-timeline-modal { position: fixed; top: 0; left: 0; right: 0; bottom: 0; z-index: 2000; }
.epc-crm-timeline-backdrop { position: absolute; top: 0; left: 0; right: 0; bottom: 0; background: rgba(0,0,0,0.45); }
.epc-crm-timeline-panel { position: absolute; top: 0; right: 0; bottom: 0; width: 480px; max-width: 92vw; background: #fff; box-shadow: -2px 0 12px rgba(0,0,0,0.2); display: flex; flex-direction: column; }
.epc-crm-timeline-hd { padding: 12px 16px; border-bottom: 1px solid #e5e5e5; }
.epc-crm-timeline-bd { padding: 14px 16px; overflow-y: auto; flex: 1; }
.epc-crm-timeline-bd h5 { margin: 16px 0 8px; }
.epc-crm-timeline-bd h5:first-child { margin-top: 0; }
.epc-crm-tl-item { border-left: 2px solid #e5e5e5; padding: 4px 0 8px 12px; margin-bottom: 4px; }
.epc-crm-tl-item .tl-meta { font-size: 11px; color: #888; }
</style>

<div class="col-lg-12 epc-crm-shell<?php echo $embedInErp ? ' epc-crm-embed' : ''; ?>">
<?php if (!$embedInErp): ?>
	<div class="hpanel">
		<div class="panel-heading hbuilt">
			CRM — Sales, support &amp; delivery
			<span class="pull-right">
				<a class="btn btn-default btn-xs" href="<?php echo epc_crm_h($erpUrl); ?>"><i class="fa fa-university"></i> ERP Finance</a>
				<?php if ($ordersUrl !== ''): ?><a class="btn btn-default btn-xs" href="<?php echo epc_crm_h($ordersUrl); ?>"><i class="fa fa-shopping-cart"></i> Orders</a><?php endif; ?>
			</span>
		</div>
		<div class="panel-body">
<?php else: ?>
	<div class="epc-crm-embed-wrap">
<?php endif; ?>

			<?php if (!$embedInErp): ?>
			<div class="alert alert-info" style="margin-bottom:14px;">
				<strong>Native CRM</strong> — embedded in ERP on client sites. Open via
				<a href="<?php echo epc_crm_h($erpUrl); ?>">ERP → CRM tab</a>.
			</div>
			<?php endif; ?>

			<div class="epc-crm-nav epc-erp-subnav epc-cp-tabs--pill">
				<?php
				$tabIcons = array(
					'dashboard' => 'fa-dashboard', 'pipeline' => 'fa-columns', 'leads' => 'fa-user-plus',
					'opportunities' => 'fa-briefcase', 'quotes' => 'fa-file-text-o', 'activities' => 'fa-calendar',
					'tickets' => 'fa-life-ring', 'projects' => 'fa-tasks', 'contracts' => 'fa-refresh', 'expenses' => 'fa-money',
				);
				foreach ($allTabs as $key => $label):
					$ico = isset($tabIcons[$key]) ? $tabIcons[$key] : 'fa-circle-o';
				?>
					<a class="btn btn-sm <?php echo $tab === $key ? 'btn-primary' : 'btn-default'; ?>" href="<?php echo epc_crm_h(epc_crm_tab_url($crmUrl, $key)); ?>"><i class="fa <?php echo epc_crm_h($ico); ?>"></i> <?php echo epc_crm_h($label); ?></a>
				<?php endforeach; ?>
				<?php if ($ordersUrl !== ''): ?><a class="btn btn-sm btn-default pull-right" href="<?php echo epc_crm_h($ordersUrl); ?>"><i class="fa fa-shopping-cart"></i> Orders</a><?php endif; ?>
			</div>

			<div id="epc_crm_msg" class="alert epc-crm-msg"></div>

			<?php if ($tab === 'dashboard'): ?>
				<div class="epc-crm-kpi">
					<div class="kpi"><div class="lbl">Leads</div><div class="val"><?php echo (int)$dash['leads_total']; ?></div></div>
					<div class="kpi"><div class="lbl">New leads</div><div class="val blue"><?php echo (int)$dash['leads_new']; ?></div></div>
					<div class="kpi"><div class="lbl">Open opportunities</div><div class="val"><?php echo (int)$dash['opportunities_open']; ?></div></div>
					<div class="kpi"><div class="lbl">Weighted pipeline</div><div class="val blue"><?php echo epc_crm_money($dash['pipeline_weighted']); ?> AED</div></div>
					<div class="kpi"><div class="lbl">Won MTD</div><div class="val green"><?php echo epc_crm_money($dash['won_mtd']); ?> AED</div></div>
					<div class="kpi"><div class="lbl">Activities due (7d)</div><div class="val"><?php echo (int)$dash['activities_due']; ?></div></div>
					<div class="kpi"><div class="lbl">Open quotes</div><div class="val"><?php echo (int)($dash['quotes_open'] ?? 0); ?></div></div>
					<div class="kpi"><div class="lbl">Open tickets</div><div class="val blue"><?php echo (int)($dash['tickets_open'] ?? 0); ?></div></div>
					<div class="kpi"><div class="lbl">Active projects</div><div class="val"><?php echo (int)($dash['projects_active'] ?? 0); ?></div></div>
				</div>
				<h4><i class="fa fa-columns"></i> Pipeline snapshot</h4>
				<div class="epc-crm-pipeline">
					<?php foreach (epc_crm_opportunity_stages() as $stage => $stageLabel): ?>
						<?php $cards = isset($board[$stage]) ? $board[$stage] : array(); ?>
						<div class="epc-crm-pipeline-col epc-crm-stage-<?php echo epc_crm_h($stage); ?>">
							<h5><?php echo epc_crm_h($stageLabel); ?> <span class="badge"><?php echo count($cards); ?></span></h5>
							<?php foreach (array_slice($cards, 0, 4) as $c): ?>
								<div class="epc-crm-card">
									<div><?php echo epc_crm_h($c['title']); ?></div>
									<div class="amt"><?php echo epc_crm_money($c['amount']); ?> AED</div>
									<div class="meta"><?php echo (int)$c['probability']; ?>% · <?php echo (int)$c['close_date'] ? epc_crm_h(date('d M', (int)$c['close_date'])) : '—'; ?></div>
								</div>
							<?php endforeach; ?>
							<?php if (count($cards) > 4): ?>
								<p class="text-muted" style="font-size:11px;margin:0;">+<?php echo count($cards) - 4; ?> more — see Pipeline tab</p>
							<?php endif; ?>
						</div>
					<?php endforeach; ?>
				</div>

			<?php elseif ($tab === 'pipeline'): ?>
				<p class="text-muted">Drag cards between columns, or use the stage dropdown on each card.</p>
				<div class="epc-crm-pipeline" id="epc_crm_pipeline">
					<?php foreach (epc_crm_opportunity_stages() as $stage => $stageLabel): ?>
						<?php $cards = isset($board[$stage]) ? $board[$stage] : array(); ?>
						<div class="epc-crm-pipeline-col epc-crm-stage-<?php echo epc_crm_h($stage); ?>" data-stage="<?php echo epc_crm_h($stage); ?>">
							<h5><?php echo epc_crm_h($stageLabel); ?> <span class="badge"><?php echo count($cards); ?></span></h5>
							<div class="epc-crm-pipeline-drop" data-stage="<?php echo epc_crm_h($stage); ?>">
							<?php foreach ($cards as $c): ?>
								<div class="epc-crm-card" data-id="<?php echo (int)$c['id']; ?>" draggable="true">
									<div><strong>#<?php echo (int)$c['id']; ?></strong> <?php echo epc_crm_h($c['title']); ?></div>
									<div class="amt"><?php echo epc_crm_money($c['amount']); ?> AED · <?php echo (int)$c['probability']; ?>%</div>
									<div class="meta"><?php echo epc_crm_h($c['lead_company'] ?: ''); ?></div>
									<select class="form-control input-sm epc-crm-stage-select" style="margin-top:6px;">
										<?php foreach (epc_crm_opportunity_stages() as $sk => $sl): ?>
											<option value="<?php echo epc_crm_h($sk); ?>" <?php echo $c['stage'] === $sk ? 'selected' : ''; ?>><?php echo epc_crm_h($sl); ?></option>
										<?php endforeach; ?>
									</select>
									<div style="margin-top:4px;">
										<button type="button" class="btn btn-xs btn-default epc-crm-timeline" data-entity-type="opportunity" data-entity-id="<?php echo (int)$c['id']; ?>" data-label="<?php echo epc_crm_h($c['title']); ?>"><i class="fa fa-clock-o"></i></button>
										<?php if ($c['stage'] === 'won'): ?>
											<button type="button" class="btn btn-xs btn-success epc-crm-won-hint" data-id="<?php echo (int)$c['id']; ?>">Order hint</button>
										<?php endif; ?>
									</div>
								</div>
							<?php endforeach; ?>
							</div>
						</div>
					<?php endforeach; ?>
				</div>

			<?php elseif ($tab === 'leads'): ?>
				<div class="epc-crm-section">
					<h4><i class="fa fa-user-plus"></i> Leads</h4>
					<form id="epc_crm_lead_form" class="form-inline epc-crm-form-inline" style="margin-bottom:12px;">
						<input type="hidden" name="csrf_guard_key" value="<?php echo epc_crm_h($csrf); ?>">
						<input type="hidden" name="id" value="0">
						<input type="text" name="company" class="form-control input-sm" placeholder="Company" required>
						<input type="text" name="contact_name" class="form-control input-sm" placeholder="Contact">
						<input type="email" name="email" class="form-control input-sm" placeholder="Email">
						<input type="text" name="phone" class="form-control input-sm" placeholder="Phone">
						<input type="text" name="source" class="form-control input-sm" placeholder="Source" value="web">
						<select name="status" class="form-control input-sm">
							<?php foreach (epc_crm_lead_statuses() as $k => $lbl): ?>
								<option value="<?php echo epc_crm_h($k); ?>"><?php echo epc_crm_h($lbl); ?></option>
							<?php endforeach; ?>
						</select>
						<input type="number" step="0.01" name="expected_value" class="form-control input-sm" placeholder="Expected AED">
						<button type="submit" class="btn btn-sm btn-primary">Add lead</button>
					</form>
					<table class="table table-striped table-bordered table-condensed">
						<thead><tr><th>Company</th><th>Contact</th><th>Email</th><th>Status</th><th>Expected</th><th>Created</th><th></th></tr></thead>
						<tbody>
						<?php foreach ($leads as $L): ?>
							<tr>
								<td><?php echo epc_crm_h($L['company']); ?></td>
								<td><?php echo epc_crm_h($L['contact_name']); ?></td>
								<td><?php echo epc_crm_h($L['email']); ?></td>
								<td><span class="label label-info"><?php echo epc_crm_h($L['status']); ?></span></td>
								<td><?php echo epc_crm_money($L['expected_value']); ?></td>
								<td><?php echo epc_crm_h(date('Y-m-d', (int)$L['time_created'])); ?></td>
								<td>
									<button type="button" class="btn btn-xs btn-default epc-crm-timeline" data-entity-type="lead" data-entity-id="<?php echo (int)$L['id']; ?>" data-label="<?php echo epc_crm_h($L['company']); ?>"><i class="fa fa-clock-o"></i> Timeline</button>
									<?php if ($L['status'] !== 'converted'): ?>
									<button type="button" class="btn btn-xs btn-success epc-crm-convert" data-id="<?php echo (int)$L['id']; ?>">→ Opportunity</button>
									<?php endif; ?>
								</td>
							</tr>
						<?php endforeach; ?>
						</tbody>
					</table>
					<?php if (count($leads) >= $crmListLimit): ?>
					<p class="text-center"><a class="btn btn-xs btn-default" href="<?php echo epc_crm_h(epc_crm_tab_url($crmUrl, 'leads') . '&list_limit=' . ($crmListLimit + 500)); ?>"><i class="fa fa-chevron-down"></i> Show more (currently showing latest <?php echo (int)$crmListLimit; ?>)</a></p>
					<?php endif; ?>
				</div>

			<?php elseif ($tab === 'opportunities'): ?>
				<div class="epc-crm-section">
					<h4><i class="fa fa-briefcase"></i> Opportunities</h4>
					<form id="epc_crm_opp_form" class="form-inline epc-crm-form-inline" style="margin-bottom:12px;">
						<input type="hidden" name="csrf_guard_key" value="<?php echo epc_crm_h($csrf); ?>">
						<input type="text" name="title" class="form-control input-sm" placeholder="Title" required>
						<input type="number" name="lead_id" class="form-control input-sm" placeholder="Lead ID" value="0">
						<select name="stage" class="form-control input-sm">
							<?php foreach (epc_crm_opportunity_stages() as $k => $lbl): ?>
								<option value="<?php echo epc_crm_h($k); ?>"><?php echo epc_crm_h($lbl); ?></option>
							<?php endforeach; ?>
						</select>
						<input type="number" step="0.01" name="amount" class="form-control input-sm" placeholder="Amount AED">
						<input type="number" name="probability" class="form-control input-sm" placeholder="%" value="20" min="0" max="100">
						<input type="date" name="close_date" class="form-control input-sm" value="<?php echo epc_crm_h(date('Y-m-d', time() + 86400 * 30)); ?>">
						<button type="submit" class="btn btn-sm btn-primary">Add</button>
					</form>
					<table class="table table-striped table-bordered table-condensed">
						<thead><tr><th>Title</th><th>Stage</th><th>Amount</th><th>%</th><th>Close</th><th>Lead</th><th></th></tr></thead>
						<tbody>
						<?php foreach ($opps as $o): ?>
							<tr>
								<td><?php echo epc_crm_h($o['title']); ?></td>
								<td><span class="label label-primary"><?php echo epc_crm_h($o['stage']); ?></span></td>
								<td><?php echo epc_crm_money($o['amount']); ?></td>
								<td><?php echo (int)$o['probability']; ?>%</td>
								<td><?php echo (int)$o['close_date'] ? epc_crm_h(date('Y-m-d', (int)$o['close_date'])) : '—'; ?></td>
								<td><?php echo epc_crm_h($o['lead_company'] ?: ('#' . (int)$o['lead_id'])); ?></td>
								<td>
									<button type="button" class="btn btn-xs btn-default epc-crm-timeline" data-entity-type="opportunity" data-entity-id="<?php echo (int)$o['id']; ?>" data-label="<?php echo epc_crm_h($o['title']); ?>"><i class="fa fa-clock-o"></i> Timeline</button>
									<?php if ($o['stage'] === 'won'): ?><button type="button" class="btn btn-xs btn-success epc-crm-won-hint" data-id="<?php echo (int)$o['id']; ?>">Order hint</button><?php endif; ?>
								</td>
							</tr>
						<?php endforeach; ?>
						</tbody>
					</table>
					<?php if (count($opps) >= $crmListLimit): ?>
					<p class="text-center"><a class="btn btn-xs btn-default" href="<?php echo epc_crm_h(epc_crm_tab_url($crmUrl, 'opportunities') . '&list_limit=' . ($crmListLimit + 500)); ?>"><i class="fa fa-chevron-down"></i> Show more (currently showing latest <?php echo (int)$crmListLimit; ?>)</a></p>
					<?php endif; ?>
				</div>

			<?php elseif ($tab === 'activities'): ?>
				<div class="epc-crm-section">
					<h4><i class="fa fa-calendar"></i> Activities</h4>
					<form id="epc_crm_act_form" class="form-inline epc-crm-form-inline" style="margin-bottom:12px;">
						<input type="hidden" name="csrf_guard_key" value="<?php echo epc_crm_h($csrf); ?>">
						<select name="activity_type" class="form-control input-sm">
							<?php foreach (epc_crm_activity_types() as $k => $lbl): ?>
								<option value="<?php echo epc_crm_h($k); ?>"><?php echo epc_crm_h($lbl); ?></option>
							<?php endforeach; ?>
						</select>
						<select name="related_type" class="form-control input-sm">
							<option value="lead">Lead</option>
							<option value="opportunity">Opportunity</option>
							<option value="user">User</option>
						</select>
						<input type="number" name="related_id" class="form-control input-sm" placeholder="Related ID" required>
						<input type="date" name="due_date" class="form-control input-sm" value="<?php echo epc_crm_h(date('Y-m-d')); ?>">
						<input type="text" name="notes" class="form-control input-sm" placeholder="Notes">
						<button type="submit" class="btn btn-sm btn-primary">Add activity</button>
					</form>
					<table class="table table-striped table-bordered table-condensed">
						<thead><tr><th>Type</th><th>Related</th><th>Due</th><th>Done</th><th>Notes</th><th></th></tr></thead>
						<tbody>
						<?php foreach ($activities as $a): ?>
							<tr>
								<td><?php echo epc_crm_h($a['activity_type']); ?></td>
								<td><?php echo epc_crm_h($a['related_type'] . ' #' . (int)$a['related_id']); ?></td>
								<td><?php echo (int)$a['due_date'] ? epc_crm_h(date('Y-m-d', (int)$a['due_date'])) : '—'; ?></td>
								<td><?php echo (int)$a['done'] ? '<span class="label label-success">Yes</span>' : '<span class="label label-warning">Open</span>'; ?></td>
								<td><?php echo epc_crm_h(mb_substr($a['notes'], 0, 80)); ?></td>
								<td><button type="button" class="btn btn-xs btn-default epc-crm-toggle-act" data-id="<?php echo (int)$a['id']; ?>" data-done="<?php echo (int)$a['done'] ? '0' : '1'; ?>"><?php echo (int)$a['done'] ? 'Reopen' : 'Done'; ?></button></td>
							</tr>
						<?php endforeach; ?>
						</tbody>
					</table>
					<?php if (count($activities) >= $crmListLimit): ?>
					<p class="text-center"><a class="btn btn-xs btn-default" href="<?php echo epc_crm_h(epc_crm_tab_url($crmUrl, 'activities') . '&list_limit=' . ($crmListLimit + 500)); ?>"><i class="fa fa-chevron-down"></i> Show more (currently showing latest <?php echo (int)$crmListLimit; ?>)</a></p>
					<?php endif; ?>
				</div>
			<?php endif; ?>

			<?php require __DIR__ . '/crm_tabs_extended.php'; ?>

			<div id="epc_crm_timeline_modal" class="epc-crm-timeline-modal" style="display:none;">
				<div class="epc-crm-timeline-backdrop"></div>
				<div class="epc-crm-timeline-panel">
					<div class="epc-crm-timeline-hd">
						<strong id="epc_crm_timeline_title">Timeline</strong>
						<button type="button" class="btn btn-xs btn-default epc-crm-timeline-close pull-right">&times; Close</button>
					</div>
					<div class="epc-crm-timeline-bd" id="epc_crm_timeline_body">
						<p class="text-muted">Loading…</p>
					</div>
				</div>
			</div>

<?php if (!$embedInErp): ?>
		</div>
	</div>
<?php else: ?>
	</div>
<?php endif; ?>
</div>

<script>
(function() {
	var ajaxUrl = <?php echo json_encode($crmAjax); ?>;
	var csrf = <?php echo json_encode($csrf); ?>;
	var actionPrefix = <?php echo json_encode($crmActionPrefix); ?>;
	var msg = document.getElementById('epc_crm_msg');

	function act(name) { return actionPrefix + name; }

	function showMsg(ok, text) {
		if (!msg) return;
		msg.style.display = 'block';
		msg.className = 'alert epc-crm-msg ' + (ok ? 'alert-success' : 'alert-danger');
		msg.textContent = text;
	}

	function post(action, data, cb) {
		var fd = new FormData();
		fd.append('action', action);
		fd.append('csrf_guard_key', csrf);
		for (var k in data) {
			if (data.hasOwnProperty(k)) fd.append(k, data[k]);
		}
		fetch(ajaxUrl, { method: 'POST', body: fd, credentials: 'same-origin' })
			.then(function(r) { return r.json(); })
			.then(function(res) {
				if (cb) cb(res);
				else if (res.status) { showMsg(true, res.message); setTimeout(function() { location.reload(); }, 600); }
				else showMsg(false, res.message || 'Error');
			})
			.catch(function() { showMsg(false, 'Request failed'); });
	}

	var leadForm = document.getElementById('epc_crm_lead_form');
	if (leadForm) {
		leadForm.addEventListener('submit', function(e) {
			e.preventDefault();
			var fd = new FormData(leadForm);
			var data = {};
			fd.forEach(function(v, k) { if (k !== 'csrf_guard_key') data[k] = v; });
			post(act('save_lead'), data);
		});
	}

	var oppForm = document.getElementById('epc_crm_opp_form');
	if (oppForm) {
		oppForm.addEventListener('submit', function(e) {
			e.preventDefault();
			var fd = new FormData(oppForm);
			var data = {};
			fd.forEach(function(v, k) { if (k !== 'csrf_guard_key') data[k] = v; });
			post(act('save_opportunity'), data);
		});
	}

	var actForm = document.getElementById('epc_crm_act_form');
	if (actForm) {
		actForm.addEventListener('submit', function(e) {
			e.preventDefault();
			var fd = new FormData(actForm);
			var data = {};
			fd.forEach(function(v, k) { if (k !== 'csrf_guard_key') data[k] = v; });
			post(act('save_activity'), data);
		});
	}

	document.querySelectorAll('.epc-crm-convert').forEach(function(btn) {
		btn.addEventListener('click', function() {
			post(act('convert_lead'), { lead_id: btn.getAttribute('data-id') });
		});
	});

	document.querySelectorAll('.epc-crm-stage-select').forEach(function(sel) {
		sel.addEventListener('change', function() {
			var card = sel.closest('.epc-crm-card');
			var id = card ? card.getAttribute('data-id') : 0;
			post(act('update_stage'), { id: id, stage: sel.value }, function(res) {
				if (res.status) location.reload();
				else showMsg(false, res.message);
			});
		});
	});

	// Pipeline drag-and-drop: drag a card into another column to change its stage.
	document.querySelectorAll('.epc-crm-card[draggable="true"]').forEach(function(card) {
		card.addEventListener('dragstart', function(e) {
			e.dataTransfer.setData('text/plain', card.getAttribute('data-id'));
			e.dataTransfer.effectAllowed = 'move';
			card.classList.add('epc-crm-dragging');
		});
		card.addEventListener('dragend', function() { card.classList.remove('epc-crm-dragging'); });
	});
	document.querySelectorAll('.epc-crm-pipeline-drop').forEach(function(zone) {
		zone.addEventListener('dragover', function(e) { e.preventDefault(); zone.classList.add('epc-crm-drop-over'); });
		zone.addEventListener('dragleave', function() { zone.classList.remove('epc-crm-drop-over'); });
		zone.addEventListener('drop', function(e) {
			e.preventDefault();
			zone.classList.remove('epc-crm-drop-over');
			var id = e.dataTransfer.getData('text/plain');
			var stage = zone.getAttribute('data-stage');
			if (!id || !stage) return;
			post(act('update_stage'), { id: id, stage: stage }, function(res) {
				if (res.status) location.reload();
				else showMsg(false, res.message);
			});
		});
	});

	// Customer 360 timeline modal (activities + quotes + linked order history).
	var tlModal = document.getElementById('epc_crm_timeline_modal');
	var tlBody = document.getElementById('epc_crm_timeline_body');
	var tlTitle = document.getElementById('epc_crm_timeline_title');
	function closeTimeline() { if (tlModal) tlModal.style.display = 'none'; }
	if (tlModal) {
		tlModal.querySelector('.epc-crm-timeline-backdrop').addEventListener('click', closeTimeline);
		tlModal.querySelector('.epc-crm-timeline-close').addEventListener('click', closeTimeline);
	}
	function escapeHtml(s) {
		return String(s == null ? '' : s).replace(/[&<>"']/g, function(c) {
			return { '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c];
		});
	}
	function renderTimeline(j) {
		if (!j || !j.status) { tlBody.innerHTML = '<p class="text-danger">' + escapeHtml(j && j.message || 'Failed to load') + '</p>'; return; }
		var html = '';
		var e = j.entity || {};
		html += '<div class="epc-crm-tl-item" style="border-left-color:#428bca;">';
		html += '<strong>' + escapeHtml(e.title || e.company || '') + '</strong>';
		if (e.contact_name) html += '<div class="tl-meta">' + escapeHtml(e.contact_name) + (e.email ? ' · ' + escapeHtml(e.email) : '') + '</div>';
		if (e.stage) html += '<div class="tl-meta">Stage: ' + escapeHtml(e.stage) + ' · ' + escapeHtml(e.probability) + '%</div>';
		if (e.status) html += '<div class="tl-meta">Status: ' + escapeHtml(e.status) + '</div>';
		html += '</div>';

		if (j.has_commerce) {
			html += '<h5><i class="fa fa-shopping-cart"></i> Order history</h5>';
			if (j.linked_user_id > 0 && j.orders && j.orders.length) {
				j.orders.forEach(function(o) {
					html += '<div class="epc-crm-tl-item">';
					html += '<strong>Order #' + o.id + '</strong> · ' + escapeHtml(o.status_name || '—');
					html += '<div class="tl-meta">' + new Date(o.time * 1000).toISOString().slice(0, 10) + ' · ' + Number(o.price_total_wt_vat || 0).toFixed(2) + ' AED' + (Number(o.paid) ? ' · Paid' : '') + '</div>';
					html += '</div>';
				});
			} else if (j.linked_user_id > 0) {
				html += '<p class="text-muted">No orders yet for this linked customer.</p>';
			} else {
				html += '<p class="text-muted">No storefront customer linked yet — set an opportunity\'s linked customer, or match by lead email.</p>';
			}
		}

		html += '<h5><i class="fa fa-file-text-o"></i> Quotes</h5>';
		if (j.quotes && j.quotes.length) {
			j.quotes.forEach(function(q) {
				html += '<div class="epc-crm-tl-item"><strong>' + escapeHtml(q.quote_number) + '</strong> · ' + escapeHtml(q.status) + '<div class="tl-meta">' + Number(q.subtotal || 0).toFixed(2) + ' AED</div></div>';
			});
		} else {
			html += '<p class="text-muted">No quotes yet.</p>';
		}

		html += '<h5><i class="fa fa-calendar"></i> Activities</h5>';
		if (j.activities && j.activities.length) {
			j.activities.forEach(function(a) {
				html += '<div class="epc-crm-tl-item"><strong>' + escapeHtml(a.activity_type) + '</strong>'
					+ (a.due_date ? ' · ' + new Date(a.due_date * 1000).toISOString().slice(0, 10) : '')
					+ (Number(a.done) ? ' <span class="label label-success">Done</span>' : ' <span class="label label-warning">Open</span>')
					+ '<div class="tl-meta">' + escapeHtml(a.notes || '') + '</div></div>';
			});
		} else {
			html += '<p class="text-muted">No activities logged yet.</p>';
		}
		tlBody.innerHTML = html;
	}
	document.querySelectorAll('.epc-crm-timeline').forEach(function(btn) {
		btn.addEventListener('click', function() {
			var type = btn.getAttribute('data-entity-type');
			var id = btn.getAttribute('data-entity-id');
			tlTitle.textContent = 'Timeline — ' + (btn.getAttribute('data-label') || '');
			tlBody.innerHTML = '<p class="text-muted">Loading…</p>';
			tlModal.style.display = 'block';
			post(act('get_timeline'), { entity_type: type, entity_id: id }, renderTimeline);
		});
	});

	document.querySelectorAll('.epc-crm-won-hint').forEach(function(btn) {
		btn.addEventListener('click', function() {
			post(act('won_hint'), { opportunity_id: btn.getAttribute('data-id') }, function(res) {
				showMsg(res.status, res.message || res.hint || 'OK');
			});
		});
	});

	document.querySelectorAll('.epc-crm-toggle-act').forEach(function(btn) {
		btn.addEventListener('click', function() {
			post(act('toggle_activity'), { id: btn.getAttribute('data-id'), done: btn.getAttribute('data-done') });
		});
	});

	function bindCrmForm(id, action) {
		var f = document.getElementById(id);
		if (!f) return;
		f.addEventListener('submit', function(e) {
			e.preventDefault();
			var fd = new FormData(f);
			var data = {};
			fd.forEach(function(v, k) { if (k !== 'csrf_guard_key') data[k] = v; });
			post(action, data);
		});
	}
	bindCrmForm('epc_crm_quote_form', act('save_quote'));
	bindCrmForm('epc_crm_ticket_form', act('save_ticket'));
	bindCrmForm('epc_crm_project_form', act('save_project'));
	bindCrmForm('epc_crm_contract_form', act('save_contract'));
	bindCrmForm('epc_crm_expense_form', act('save_expense'));

	document.querySelectorAll('.epc-crm-quote-preview').forEach(function(btn) {
		btn.addEventListener('click', function() {
			post(act('quote_preview'), { quote_id: btn.getAttribute('data-id') }, function(j) {
				if (j && j.preview_url) window.open(j.preview_url, '_blank');
			});
		});
	});
	document.querySelectorAll('.epc-crm-quote-email').forEach(function(btn) {
		btn.addEventListener('click', function() {
			var em = prompt('Send proposal to email (leave blank for customer default):', '');
			if (em === null) return;
			post(act('quote_email'), { quote_id: btn.getAttribute('data-id'), email: em });
		});
	});
	document.querySelectorAll('.epc-crm-accept-quote').forEach(function(btn) {
		btn.addEventListener('click', function() {
			if (!confirm('Accept quote and create shop order stub?')) return;
			post(act('accept_quote'), { quote_id: btn.getAttribute('data-id'), post_cash: '1' });
		});
	});
	document.querySelectorAll('.epc-crm-approve-expense').forEach(function(btn) {
		btn.addEventListener('click', function() {
			post(act('approve_expense'), { expense_id: btn.getAttribute('data-id'), post_cash: '1' });
		});
	});
})();
</script>
