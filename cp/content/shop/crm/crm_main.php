<?php
/**
 * CRM module — enterprise sales / support / delivery suite (ERP-embedded).
 */
defined('_ASTEXE_') or die('No access');

require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_crm_helpers.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_crm_access.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_crm_advanced.php';

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
	'dashboard' => 'Command Centre',
	'intelligence' => 'Intelligence',
	'pipeline' => 'Pipeline',
	'leads' => 'Leads',
	'opportunities' => 'Opps',
	'accounts' => 'Accounts',
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
$leadStatusFilter = isset($_GET['lead_status']) ? (string)$_GET['lead_status'] : '';
$dash = ($tab === 'dashboard') ? epc_crm_dashboard_extended($db_link) : array();
$crmAdv = ($tab === 'dashboard' || $tab === 'intelligence') ? epc_crm_adv_dashboard($db_link) : array();
$crmAccounts = ($tab === 'accounts') ? epc_crm_adv_accounts($db_link, 150) : array();
$board = ($tab === 'pipeline' || $tab === 'dashboard') ? epc_crm_pipeline_board($db_link) : array();
$leads = ($tab === 'leads') ? epc_crm_list_leads($db_link, $leadStatusFilter, $crmListLimit) : array();
if ($tab === 'leads' && !empty($leads)) {
	foreach ($leads as &$crmLeadRow) {
		$scored = epc_crm_adv_score_lead($db_link, $crmLeadRow);
		$crmLeadRow['lead_score'] = $scored['score'];
		$crmLeadRow['lead_band'] = $scored['band'];
		$crmLeadRow['score_reasons'] = $scored['reasons'];
	}
	unset($crmLeadRow);
}
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

<link rel="stylesheet" href="/content/shop/finance/epc_erp_ui.css?v=20260720">
<link rel="stylesheet" href="/content/shop/finance/epc_crm_ui.css?v=20260720">
<link rel="stylesheet" href="/content/shop/finance/epc_erp_professional.css?v=20260720">
<link rel="stylesheet" href="/content/shop/finance/epc_crm_enterprise.css?v=20260720">
<style>
.epc-crm-shell .epc-crm-form-inline input, .epc-crm-shell .epc-crm-form-inline select { margin: 2px 4px 2px 0; }
.epc-crm-pipeline-drop { min-height: 40px; }
.epc-crm-pipeline-drop.epc-crm-drop-over { background: rgba(29,78,216,0.08); outline: 2px dashed #1d4ed8; border-radius: 4px; }
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

<div class="col-lg-12 epc-crm-shell epc-crm-enterprise<?php echo $embedInErp ? ' epc-crm-embed' : ''; ?>">
<?php if (!$embedInErp): ?>
	<div class="hpanel">
		<div class="panel-heading hbuilt">
			Enterprise CRM — Sales, support &amp; delivery
			<span class="pull-right">
				<a class="btn btn-default btn-xs" href="<?php echo epc_crm_h($erpUrl); ?>"><i class="fa fa-university"></i> ERP Finance</a>
				<?php if ($ordersUrl !== ''): ?><a class="btn btn-default btn-xs" href="<?php echo epc_crm_h($ordersUrl); ?>"><i class="fa fa-shopping-cart"></i> Orders</a><?php endif; ?>
			</span>
		</div>
		<div class="panel-body">
<?php else: ?>
	<div class="epc-crm-embed-wrap">
<?php endif; ?>

			<div class="epc-crm-hero">
				<div>
					<h2>Enterprise CRM</h2>
					<p>Lead scoring, weighted pipeline forecast, accounts &amp; customer 360, quotes, support, projects, contracts, and expenses — one sales command centre inside ERP.</p>
				</div>
				<div class="epc-crm-hero-actions">
					<a class="btn btn-sm btn-primary" href="<?php echo epc_crm_h(epc_crm_tab_url($crmUrl, 'leads')); ?>"><i class="fa fa-user-plus"></i> New lead</a>
					<a class="btn btn-sm btn-default" href="<?php echo epc_crm_h(epc_crm_tab_url($crmUrl, 'intelligence')); ?>"><i class="fa fa-line-chart"></i> Intelligence</a>
					<a class="btn btn-sm btn-default" href="<?php echo epc_crm_h(epc_crm_tab_url($crmUrl, 'pipeline')); ?>"><i class="fa fa-columns"></i> Pipeline</a>
					<?php if ($ordersUrl !== ''): ?><a class="btn btn-sm btn-default" href="<?php echo epc_crm_h($ordersUrl); ?>"><i class="fa fa-shopping-cart"></i> Orders</a><?php endif; ?>
				</div>
			</div>

			<div class="epc-crm-nav epc-erp-subnav epc-cp-tabs--pill">
				<?php
				$tabIcons = array(
					'dashboard' => 'fa-dashboard', 'intelligence' => 'fa-line-chart', 'pipeline' => 'fa-columns',
					'leads' => 'fa-user-plus', 'opportunities' => 'fa-briefcase', 'accounts' => 'fa-building',
					'quotes' => 'fa-file-text-o', 'activities' => 'fa-calendar',
					'tickets' => 'fa-life-ring', 'projects' => 'fa-tasks', 'contracts' => 'fa-refresh', 'expenses' => 'fa-money',
				);
				foreach ($allTabs as $key => $label):
					$ico = isset($tabIcons[$key]) ? $tabIcons[$key] : 'fa-circle-o';
				?>
					<a class="btn btn-sm <?php echo $tab === $key ? 'btn-primary' : 'btn-default'; ?>" href="<?php echo epc_crm_h(epc_crm_tab_url($crmUrl, $key)); ?>"><i class="fa <?php echo epc_crm_h($ico); ?>"></i> <?php echo epc_crm_h($label); ?></a>
				<?php endforeach; ?>
			</div>

			<div id="epc_crm_msg" class="alert epc-crm-msg"></div>

			<?php if ($tab === 'dashboard'):
				$bands = isset($crmAdv['lead_bands']) ? $crmAdv['lead_bands'] : array();
				$forecast = isset($crmAdv['forecast']) ? $crmAdv['forecast'] : array();
				$nextActions = isset($crmAdv['next_actions']) ? $crmAdv['next_actions'] : array();
				$topLeads = isset($crmAdv['top_leads']) ? $crmAdv['top_leads'] : array();
			?>
				<div class="epc-crm-kpi">
					<div class="kpi"><div class="lbl">Leads</div><div class="val"><?php echo (int)$dash['leads_total']; ?></div><div class="hint"><?php echo (int)$dash['leads_new']; ?> new</div></div>
					<div class="kpi"><div class="lbl">Hot / Warm / Cold</div><div class="val blue"><?php echo (int)($bands['hot'] ?? 0); ?> / <?php echo (int)($bands['warm'] ?? 0); ?> / <?php echo (int)($bands['cold'] ?? 0); ?></div><div class="hint">Lead score bands</div></div>
					<div class="kpi"><div class="lbl">Open opportunities</div><div class="val"><?php echo (int)$dash['opportunities_open']; ?></div></div>
					<div class="kpi"><div class="lbl">Weighted forecast</div><div class="val blue"><?php echo epc_crm_money($forecast['weighted_value'] ?? $dash['pipeline_weighted']); ?> AED</div></div>
					<div class="kpi"><div class="lbl">Won MTD</div><div class="val green"><?php echo epc_crm_money($dash['won_mtd']); ?> AED</div></div>
					<div class="kpi"><div class="lbl">Win rate</div><div class="val"><?php echo epc_crm_h((string)($forecast['win_rate'] ?? 0)); ?>%</div></div>
					<div class="kpi"><div class="lbl">Activities due</div><div class="val amber"><?php echo (int)$dash['activities_due']; ?></div><div class="hint">Next 7 days</div></div>
					<div class="kpi"><div class="lbl">Open quotes</div><div class="val"><?php echo (int)($dash['quotes_open'] ?? 0); ?></div></div>
					<div class="kpi"><div class="lbl">Open tickets</div><div class="val blue"><?php echo (int)($dash['tickets_open'] ?? 0); ?></div></div>
					<div class="kpi"><div class="lbl">Active projects</div><div class="val"><?php echo (int)($dash['projects_active'] ?? 0); ?></div></div>
				</div>

				<div class="epc-crm-grid-2">
					<div class="epc-crm-panel">
						<div class="epc-crm-panel-hd">
							<h4><i class="fa fa-fire"></i> Priority leads</h4>
							<a class="btn btn-xs btn-default" href="<?php echo epc_crm_h(epc_crm_tab_url($crmUrl, 'intelligence')); ?>">Full intelligence</a>
						</div>
						<div class="epc-crm-panel-bd" style="padding:0;">
							<table class="table table-condensed" style="margin:0;">
								<thead><tr><th>Company</th><th>Score</th><th>Band</th><th>Expected</th></tr></thead>
								<tbody>
								<?php foreach (array_slice($topLeads, 0, 6) as $L):
									$band = (string)($L['lead_band'] ?? 'cold');
								?>
									<tr>
										<td><strong><?php echo epc_crm_h($L['company']); ?></strong></td>
										<td><span class="epc-crm-score"><?php echo (int)($L['lead_score'] ?? 0); ?></span></td>
										<td><span class="epc-crm-band epc-crm-band-<?php echo epc_crm_h($band); ?>"><?php echo epc_crm_h($band); ?></span></td>
										<td><?php echo epc_crm_money($L['expected_value'] ?? 0); ?></td>
									</tr>
								<?php endforeach; ?>
								<?php if (empty($topLeads)): ?><tr><td colspan="4" class="text-muted">No scored leads yet.</td></tr><?php endif; ?>
								</tbody>
							</table>
						</div>
					</div>
					<div class="epc-crm-panel">
						<div class="epc-crm-panel-hd">
							<h4><i class="fa fa-bolt"></i> Next-best actions</h4>
							<a class="btn btn-xs btn-default" href="<?php echo epc_crm_h(epc_crm_tab_url($crmUrl, 'activities')); ?>">Activities</a>
						</div>
						<div class="epc-crm-panel-bd">
							<?php if (empty($nextActions)): ?>
								<p class="text-muted" style="margin:0;">No overdue or upcoming follow-ups. Log activities to drive the queue.</p>
							<?php else: ?>
							<ul class="epc-crm-action-list">
								<?php foreach (array_slice($nextActions, 0, 6) as $a): ?>
								<li>
									<div>
										<strong><?php echo epc_crm_h($a['activity_type']); ?></strong>
										· <?php echo epc_crm_h($a['related_type'] . ' #' . (int)$a['related_id']); ?>
									</div>
									<div style="text-align:right;">
										<?php if (!empty($a['is_overdue'])): ?><span class="overdue">OVERDUE</span><?php else: ?><span class="due">Due</span><?php endif; ?>
										<div style="font-size:12px;font-weight:700;"><?php echo (int)$a['due_date'] ? epc_crm_h(date('d M', (int)$a['due_date'])) : '—'; ?></div>
									</div>
								</li>
								<?php endforeach; ?>
							</ul>
							<?php endif; ?>
						</div>
					</div>
				</div>

				<div class="epc-crm-panel" style="margin-bottom:16px;">
					<div class="epc-crm-panel-hd">
						<h4><i class="fa fa-columns"></i> Pipeline snapshot</h4>
						<a class="btn btn-xs btn-primary" href="<?php echo epc_crm_h(epc_crm_tab_url($crmUrl, 'pipeline')); ?>">Open board</a>
					</div>
					<div class="epc-crm-panel-bd">
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
					</div>
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
					<h4><i class="fa fa-user-plus"></i> Lead management</h4>
					<p class="text-muted" style="margin-top:-4px;">Score, qualify, convert, and edit leads. Hot leads surface first in Intelligence.</p>
					<div class="epc-crm-form-card">
					<form id="epc_crm_lead_form" class="form-inline epc-crm-form-inline">
						<input type="hidden" name="csrf_guard_key" value="<?php echo epc_crm_h($csrf); ?>">
						<input type="hidden" name="id" id="epc_crm_lead_id" value="0">
						<input type="text" name="company" id="epc_crm_lead_company" class="form-control input-sm" placeholder="Company" required>
						<input type="text" name="contact_name" id="epc_crm_lead_contact" class="form-control input-sm" placeholder="Contact">
						<input type="email" name="email" id="epc_crm_lead_email" class="form-control input-sm" placeholder="Email">
						<input type="text" name="phone" id="epc_crm_lead_phone" class="form-control input-sm" placeholder="Phone">
						<input type="text" name="source" id="epc_crm_lead_source" class="form-control input-sm" placeholder="Source" value="web">
						<select name="status" id="epc_crm_lead_status" class="form-control input-sm">
							<?php foreach (epc_crm_lead_statuses() as $k => $lbl): ?>
								<option value="<?php echo epc_crm_h($k); ?>"><?php echo epc_crm_h($lbl); ?></option>
							<?php endforeach; ?>
						</select>
						<input type="number" step="0.01" name="expected_value" id="epc_crm_lead_value" class="form-control input-sm" placeholder="Expected AED">
						<input type="text" name="notes" id="epc_crm_lead_notes" class="form-control input-sm" placeholder="Notes" style="max-width:220px;">
						<button type="submit" class="btn btn-sm btn-primary" id="epc_crm_lead_submit">Add lead</button>
						<button type="button" class="btn btn-sm btn-default" id="epc_crm_lead_reset" style="display:none;">Cancel edit</button>
					</form>
					</div>
					<div style="margin-bottom:10px;">
						<span class="text-muted" style="margin-right:8px;">Filter:</span>
						<a class="btn btn-xs <?php echo $leadStatusFilter === '' ? 'btn-primary' : 'btn-default'; ?>" href="<?php echo epc_crm_h(epc_crm_tab_url($crmUrl, 'leads')); ?>">All</a>
						<?php foreach (epc_crm_lead_statuses() as $sk => $sl): ?>
							<a class="btn btn-xs <?php echo $leadStatusFilter === $sk ? 'btn-primary' : 'btn-default'; ?>" href="<?php echo epc_crm_h(epc_crm_tab_url($crmUrl, 'leads') . '&lead_status=' . rawurlencode($sk)); ?>"><?php echo epc_crm_h($sl); ?></a>
						<?php endforeach; ?>
					</div>
					<table class="table table-striped table-bordered table-condensed">
						<thead><tr><th>Company</th><th>Contact</th><th>Score</th><th>Status</th><th>Source</th><th>Expected</th><th>Created</th><th></th></tr></thead>
						<tbody>
						<?php foreach ($leads as $L):
							$band = (string)($L['lead_band'] ?? 'cold');
						?>
							<tr>
								<td>
									<strong><?php echo epc_crm_h($L['company']); ?></strong>
									<?php if (!empty($L['email'])): ?><div class="text-muted" style="font-size:11px;"><?php echo epc_crm_h($L['email']); ?></div><?php endif; ?>
								</td>
								<td><?php echo epc_crm_h($L['contact_name']); ?><?php if (!empty($L['phone'])): ?><div class="text-muted" style="font-size:11px;"><?php echo epc_crm_h($L['phone']); ?></div><?php endif; ?></td>
								<td>
									<span class="epc-crm-score" title="<?php echo epc_crm_h(implode(' · ', $L['score_reasons'] ?? array())); ?>"><?php echo (int)($L['lead_score'] ?? 0); ?></span>
									<span class="epc-crm-band epc-crm-band-<?php echo epc_crm_h($band); ?>"><?php echo epc_crm_h($band); ?></span>
								</td>
								<td><span class="label label-info"><?php echo epc_crm_h($L['status']); ?></span></td>
								<td><?php echo epc_crm_h($L['source']); ?></td>
								<td><?php echo epc_crm_money($L['expected_value']); ?></td>
								<td><?php echo epc_crm_h(date('Y-m-d', (int)$L['time_created'])); ?></td>
								<td style="white-space:nowrap;">
									<button type="button" class="btn btn-xs btn-default epc-crm-edit-lead" data-id="<?php echo (int)$L['id']; ?>"><i class="fa fa-pencil"></i></button>
									<button type="button" class="btn btn-xs btn-default epc-crm-timeline" data-entity-type="lead" data-entity-id="<?php echo (int)$L['id']; ?>" data-label="<?php echo epc_crm_h($L['company']); ?>"><i class="fa fa-clock-o"></i></button>
									<?php if ($L['status'] !== 'converted'): ?>
									<button type="button" class="btn btn-xs btn-success epc-crm-convert" data-id="<?php echo (int)$L['id']; ?>">→ Opp</button>
									<?php endif; ?>
									<button type="button" class="btn btn-xs btn-danger epc-crm-delete-lead" data-id="<?php echo (int)$L['id']; ?>"><i class="fa fa-trash"></i></button>
								</td>
							</tr>
						<?php endforeach; ?>
						</tbody>
					</table>
					<?php if (count($leads) >= $crmListLimit): ?>
					<p class="text-center"><a class="btn btn-xs btn-default" href="<?php echo epc_crm_h(epc_crm_tab_url($crmUrl, 'leads') . '&list_limit=' . ($crmListLimit + 500) . ($leadStatusFilter !== '' ? '&lead_status=' . rawurlencode($leadStatusFilter) : '')); ?>"><i class="fa fa-chevron-down"></i> Show more (currently showing latest <?php echo (int)$crmListLimit; ?>)</a></p>
					<?php endif; ?>
				</div>

			<?php elseif ($tab === 'opportunities'): ?>
				<div class="epc-crm-section">
					<h4><i class="fa fa-briefcase"></i> Opportunities</h4>
					<p class="text-muted" style="margin-top:-4px;">Deal desk with stage control, linked customers, and order handoff on win.</p>
					<div class="epc-crm-form-card">
					<form id="epc_crm_opp_form" class="form-inline epc-crm-form-inline">
						<input type="hidden" name="csrf_guard_key" value="<?php echo epc_crm_h($csrf); ?>">
						<input type="text" name="title" class="form-control input-sm" placeholder="Title" required>
						<input type="number" name="lead_id" class="form-control input-sm" placeholder="Lead ID" value="0">
						<input type="number" name="linked_user_id" class="form-control input-sm" placeholder="Customer user ID" value="0">
						<select name="stage" class="form-control input-sm">
							<?php foreach (epc_crm_opportunity_stages() as $k => $lbl): ?>
								<option value="<?php echo epc_crm_h($k); ?>"><?php echo epc_crm_h($lbl); ?></option>
							<?php endforeach; ?>
						</select>
						<input type="number" step="0.01" name="amount" class="form-control input-sm" placeholder="Amount AED">
						<input type="number" name="probability" class="form-control input-sm" placeholder="%" value="20" min="0" max="100">
						<input type="date" name="close_date" class="form-control input-sm" value="<?php echo epc_crm_h(date('Y-m-d', time() + 86400 * 30)); ?>">
						<button type="submit" class="btn btn-sm btn-primary">Add opportunity</button>
					</form>
					</div>
					<table class="table table-striped table-bordered table-condensed">
						<thead><tr><th>Title</th><th>Stage</th><th>Amount</th><th>%</th><th>Close</th><th>Account</th><th>Customer</th><th></th></tr></thead>
						<tbody>
						<?php foreach ($opps as $o): ?>
							<tr>
								<td><strong><?php echo epc_crm_h($o['title']); ?></strong></td>
								<td><span class="label label-primary"><?php echo epc_crm_h($o['stage']); ?></span></td>
								<td><?php echo epc_crm_money($o['amount']); ?></td>
								<td><?php echo (int)$o['probability']; ?>%</td>
								<td><?php echo (int)$o['close_date'] ? epc_crm_h(date('Y-m-d', (int)$o['close_date'])) : '—'; ?></td>
								<td><?php echo epc_crm_h($o['lead_company'] ?: ('#' . (int)$o['lead_id'])); ?></td>
								<td><?php echo (int)$o['linked_user_id'] ? ('#' . (int)$o['linked_user_id']) : '—'; ?></td>
								<td style="white-space:nowrap;">
									<button type="button" class="btn btn-xs btn-default epc-crm-edit-opp" data-id="<?php echo (int)$o['id']; ?>" title="Link customer"><i class="fa fa-link"></i></button>
									<button type="button" class="btn btn-xs btn-default epc-crm-timeline" data-entity-type="opportunity" data-entity-id="<?php echo (int)$o['id']; ?>" data-label="<?php echo epc_crm_h($o['title']); ?>"><i class="fa fa-clock-o"></i></button>
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

			<?php require __DIR__ . '/crm_enterprise_panels.php'; ?>
			<?php require __DIR__ . '/crm_tabs_extended.php'; ?>

			<div id="epc_crm_drawer" class="epc-crm-drawer">
				<div class="epc-crm-drawer-backdrop"></div>
				<div class="epc-crm-drawer-panel">
					<div class="epc-crm-drawer-hd">
						<strong id="epc_crm_drawer_title">Details</strong>
						<button type="button" class="btn btn-xs btn-default epc-crm-drawer-close">&times; Close</button>
					</div>
					<div class="epc-crm-drawer-bd" id="epc_crm_drawer_body"><p class="text-muted">Loading…</p></div>
				</div>
			</div>

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

	// Lead edit / delete
	var leadReset = document.getElementById('epc_crm_lead_reset');
	var leadSubmit = document.getElementById('epc_crm_lead_submit');
	function resetLeadForm() {
		if (!leadForm) return;
		leadForm.reset();
		var idEl = document.getElementById('epc_crm_lead_id');
		if (idEl) idEl.value = '0';
		if (leadSubmit) leadSubmit.textContent = 'Add lead';
		if (leadReset) leadReset.style.display = 'none';
	}
	if (leadReset) leadReset.addEventListener('click', resetLeadForm);
	document.querySelectorAll('.epc-crm-edit-lead').forEach(function(btn) {
		btn.addEventListener('click', function() {
			post(act('get_lead'), { id: btn.getAttribute('data-id') }, function(j) {
				var L = (j && j.lead) ? j.lead : null;
				if (!j || !j.status || !L) { showMsg(false, (j && j.message) || 'Lead not found'); return; }
				document.getElementById('epc_crm_lead_id').value = L.id || 0;
				document.getElementById('epc_crm_lead_company').value = L.company || '';
				document.getElementById('epc_crm_lead_contact').value = L.contact_name || '';
				document.getElementById('epc_crm_lead_email').value = L.email || '';
				document.getElementById('epc_crm_lead_phone').value = L.phone || '';
				document.getElementById('epc_crm_lead_source').value = L.source || 'web';
				document.getElementById('epc_crm_lead_status').value = L.status || 'new';
				document.getElementById('epc_crm_lead_value').value = L.expected_value || '';
				document.getElementById('epc_crm_lead_notes').value = L.notes || '';
				if (leadSubmit) leadSubmit.textContent = 'Save lead #' + L.id;
				if (leadReset) leadReset.style.display = 'inline-block';
				window.scrollTo({ top: 0, behavior: 'smooth' });
				showMsg(true, 'Editing lead #' + L.id + ' (score ' + (L.lead_score || 0) + ' · ' + (L.lead_band || '') + ')');
			});
		});
	});
	document.querySelectorAll('.epc-crm-delete-lead').forEach(function(btn) {
		btn.addEventListener('click', function() {
			if (!confirm('Archive this lead?')) return;
			post(act('delete_lead'), { id: btn.getAttribute('data-id') });
		});
	});

	// Detail drawer (tickets / projects)
	var drawer = document.getElementById('epc_crm_drawer');
	var drawerBody = document.getElementById('epc_crm_drawer_body');
	var drawerTitle = document.getElementById('epc_crm_drawer_title');
	function openDrawer(title) {
		if (!drawer) return;
		drawerTitle.textContent = title || 'Details';
		drawerBody.innerHTML = '<p class="text-muted">Loading…</p>';
		drawer.classList.add('open');
	}
	function closeDrawer() { if (drawer) drawer.classList.remove('open'); }
	if (drawer) {
		drawer.querySelector('.epc-crm-drawer-backdrop').addEventListener('click', closeDrawer);
		drawer.querySelector('.epc-crm-drawer-close').addEventListener('click', closeDrawer);
	}

	document.querySelectorAll('.epc-crm-open-ticket').forEach(function(btn) {
		btn.addEventListener('click', function() {
			var id = btn.getAttribute('data-id');
			openDrawer('Ticket #' + id);
			post(act('get_ticket'), { id: id }, function(j) {
				var t = (j && j.ticket) ? j.ticket : null;
				if (!j || !j.status || !t) { drawerBody.innerHTML = '<p class="text-danger">Failed to load ticket</p>'; return; }
				var html = '<p><strong>' + escapeHtml(t.subject || '') + '</strong><br>';
				html += '<span class="label label-warning">' + escapeHtml(t.status || '') + '</span> ';
				html += '<span class="label label-info">' + escapeHtml(t.priority || '') + '</span></p>';
				html += '<form id="epc_crm_ticket_update" class="form-inline" style="margin-bottom:12px;">';
				html += '<select name="status" class="form-control input-sm">';
				['open','pending','resolved','closed'].forEach(function(s) {
					html += '<option value="' + s + '"' + (t.status === s ? ' selected' : '') + '>' + s + '</option>';
				});
				html += '</select> ';
				html += '<select name="priority" class="form-control input-sm">';
				['low','normal','high','urgent'].forEach(function(s) {
					html += '<option value="' + s + '"' + (t.priority === s ? ' selected' : '') + '>' + s + '</option>';
				});
				html += '</select> ';
				html += '<input type="text" name="message" class="form-control input-sm" placeholder="Reply / note" style="max-width:220px;"> ';
				html += '<button type="submit" class="btn btn-sm btn-primary">Update</button></form>';
				html += '<div class="epc-crm-ticket-thread">';
				(t.messages || []).forEach(function(m) {
					html += '<div class="msg"><div class="meta">' + (m.time_created ? new Date(m.time_created * 1000).toISOString().slice(0,16).replace('T',' ') : '') + (Number(m.is_staff) ? ' · Staff' : ' · Customer') + '</div>' + escapeHtml(m.body || '') + '</div>';
				});
				if (!(t.messages || []).length) html += '<p class="text-muted">No messages yet.</p>';
				html += '</div>';
				drawerBody.innerHTML = html;
				var f = document.getElementById('epc_crm_ticket_update');
				if (f) {
					f.addEventListener('submit', function(e) {
						e.preventDefault();
						var fd = new FormData(f);
						post(act('update_ticket_status'), {
							id: id,
							status: fd.get('status'),
							priority: fd.get('priority'),
							message: fd.get('message') || ''
						});
					});
				}
			});
		});
	});

	document.querySelectorAll('.epc-crm-open-project').forEach(function(btn) {
		btn.addEventListener('click', function() {
			var id = btn.getAttribute('data-id');
			openDrawer('Project #' + id);
			post(act('get_project'), { id: id }, function(j) {
				var p = (j && j.project) ? j.project : null;
				if (!j || !j.status || !p) { drawerBody.innerHTML = '<p class="text-danger">Failed to load project</p>'; return; }
				var html = '<p><strong>' + escapeHtml(p.name || '') + '</strong><br>';
				html += 'Status: ' + escapeHtml(p.status || '') + ' · Progress: ' + Number(p.progress_pct || 0) + '%</p>';
				html += '<form id="epc_crm_task_form" class="form-inline" style="margin-bottom:12px;">';
				html += '<input type="text" name="title" class="form-control input-sm" placeholder="Task title" required> ';
				html += '<select name="status" class="form-control input-sm"><option value="todo">To do</option><option value="doing">Doing</option><option value="done">Done</option></select> ';
				html += '<input type="number" name="progress_pct" class="form-control input-sm" placeholder="%" value="0" min="0" max="100" style="width:70px;"> ';
				html += '<button type="submit" class="btn btn-sm btn-primary">Add task</button></form>';
				html += '<table class="table table-condensed"><thead><tr><th>Task</th><th>Status</th><th>%</th></tr></thead><tbody>';
				(p.tasks || []).forEach(function(t) {
					html += '<tr><td>' + escapeHtml(t.title || '') + '</td><td>' + escapeHtml(t.status || '') + '</td><td>' + Number(t.progress_pct || 0) + '%</td></tr>';
				});
				if (!(p.tasks || []).length) html += '<tr><td colspan="3" class="text-muted">No tasks yet.</td></tr>';
				html += '</tbody></table>';
				drawerBody.innerHTML = html;
				var f = document.getElementById('epc_crm_task_form');
				if (f) {
					f.addEventListener('submit', function(e) {
						e.preventDefault();
						var fd = new FormData(f);
						post(act('save_project_task'), {
							project_id: id,
							title: fd.get('title'),
							status: fd.get('status'),
							progress_pct: fd.get('progress_pct') || 0
						});
					});
				}
			});
		});
	});

	document.querySelectorAll('.epc-crm-quote-tax').forEach(function(btn) {
		btn.addEventListener('click', function() {
			post(act('quote_tax'), { quote_id: btn.getAttribute('data-id') }, function(j) {
				var tax = (j && j.tax) ? j.tax : null;
				if (!j || !j.status || !tax) { showMsg(false, (j && j.message) || 'Tax calc failed'); return; }
				showMsg(true, 'Quote tax: ' + Number(tax.subtotal || 0).toFixed(2) + ' + ' + Number(tax.tax_amount || 0).toFixed(2) + ' ' + (tax.tax_label || 'Tax') + ' = ' + Number(tax.total || 0).toFixed(2) + ' ' + (tax.currency || 'AED'));
			});
		});
	});

	function render360(j) {
		var box = document.getElementById('epc_crm_360_result');
		if (!box) return;
		var c = (j && j.customer360) ? j.customer360 : null;
		if (!j || !j.status || !c) {
			box.style.display = 'block';
			box.innerHTML = '<p class="text-danger">' + escapeHtml((j && j.message) || 'Failed') + '</p>';
			return;
		}
		var html = '<div class="epc-crm-360-grid">';
		html += '<div class="epc-crm-360-tile"><div class="lbl">Opportunities</div><div class="val">' + Number((c.opportunities && c.opportunities.count) || 0) + '</div><div class="text-muted" style="font-size:11px;">Open ' + Number((c.opportunities && c.opportunities.open_value) || 0).toFixed(0) + ' · Won ' + Number((c.opportunities && c.opportunities.won_value) || 0).toFixed(0) + '</div></div>';
		html += '<div class="epc-crm-360-tile"><div class="lbl">Quotes</div><div class="val">' + Number((c.quotes && c.quotes.count) || 0) + '</div><div class="text-muted" style="font-size:11px;">Accepted ' + Number((c.quotes && c.quotes.accepted) || 0) + ' · ' + Number((c.quotes && c.quotes.value) || 0).toFixed(0) + ' AED</div></div>';
		html += '<div class="epc-crm-360-tile"><div class="lbl">Tickets</div><div class="val">' + Number((c.tickets && c.tickets.open) || 0) + '</div><div class="text-muted" style="font-size:11px;">Open of ' + Number((c.tickets && c.tickets.total) || 0) + '</div></div>';
		html += '<div class="epc-crm-360-tile"><div class="lbl">Customer ID</div><div class="val">#' + Number(c.user_id || 0) + '</div><div class="text-muted" style="font-size:11px;">CRM + commerce link</div></div>';
		html += '</div>';
		box.style.display = 'block';
		box.innerHTML = html;
	}
	var form360 = document.getElementById('epc_crm_360_form');
	if (form360) {
		form360.addEventListener('submit', function(e) {
			e.preventDefault();
			var uid = document.getElementById('epc_crm_360_user').value;
			post(act('customer_360'), { user_id: uid }, render360);
		});
	}
	document.querySelectorAll('.epc-crm-load-360').forEach(function(btn) {
		btn.addEventListener('click', function() {
			var uid = btn.getAttribute('data-user-id');
			var input = document.getElementById('epc_crm_360_user');
			if (input) input.value = uid;
			post(act('customer_360'), { user_id: uid }, render360);
			window.scrollTo({ top: 0, behavior: 'smooth' });
		});
	});

	// Opportunity edit (linked customer)
	document.querySelectorAll('.epc-crm-edit-opp').forEach(function(btn) {
		btn.addEventListener('click', function() {
			post(act('get_opportunity'), { id: btn.getAttribute('data-id') }, function(j) {
				var o = (j && j.opportunity) ? j.opportunity : null;
				if (!j || !j.status || !o) { showMsg(false, (j && j.message) || 'Not found'); return; }
				var linked = prompt('Linked shop customer user ID (0 = none):', String(o.linked_user_id || 0));
				if (linked === null) return;
				post(act('save_opportunity'), {
					id: o.id,
					title: o.title,
					lead_id: o.lead_id || 0,
					stage: o.stage,
					amount: o.amount,
					probability: o.probability,
					close_date: o.close_date ? new Date(o.close_date * 1000).toISOString().slice(0, 10) : '',
					linked_user_id: linked,
					notes: o.notes || ''
				});
			});
		});
	});
})();
</script>
