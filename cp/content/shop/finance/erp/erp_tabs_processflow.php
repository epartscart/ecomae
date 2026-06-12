<?php
defined('_ASTEXE_') or die('No access');
/**
 * Process Flow — chained task routing. Sub-views:
 *   monitor (default) · inbox · processes · heads · case detail (pf_case=ID)
 */
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_processflow.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_ui.php';

epc_pf_ensure_schema($db_link);
$csrfLocal = isset($csrf) ? $csrf : '';
$pfView = (string) ($_GET['pf_view'] ?? 'monitor');
$pfCaseId = (int) ($_GET['pf_case'] ?? 0);
$pfProcId = (int) ($_GET['pf_proc'] ?? 0);
$me = epc_pf_user_id();

$deptCfg = epc_erp_departments_config();
$staff = epc_erp_staff_list($db_link);

erp_page_header(
	'<i class="fa fa-sitemap"></i> Process flow',
	'Define a process as a chain of steps, route work automatically from one person/department head to the next, and monitor exactly where every case has reached.',
	array(
		array('label' => 'ERP', 'url' => epc_erp_tab_url($erpUrl, 'dashboard', $date_from_str, $date_to_str)),
		array('label' => 'Process flow'),
	)
);

$tabBase = epc_erp_tab_url($erpUrl, 'processflow', $date_from_str, $date_to_str);
$sep = strpos($tabBase, '?') === false ? '?' : '&';
function pf_url($tabBase, $sep, $view, $extra = '') { return $tabBase . $sep . 'pf_view=' . $view . $extra; }

$views = array('monitor' => 'Monitor', 'inbox' => 'My inbox', 'processes' => 'Processes', 'heads' => 'Department heads');
?>
<div id="epc_erp_msg" class="alert" style="display:none;"></div>

<ul class="nav nav-tabs" style="margin-bottom:16px;">
	<?php foreach ($views as $v => $lbl): $active = ($pfView === $v && $pfCaseId === 0); ?>
		<li class="<?php echo $active ? 'active' : ''; ?>"><a href="<?php echo epc_erp_h(pf_url($tabBase, $sep, $v)); ?>"><?php echo epc_erp_h($lbl); ?>
			<?php if ($v === 'inbox') { $n = count(epc_pf_cases($db_link, array('mine_open' => $me))); if ($n > 0) echo ' <span class="badge">' . (int)$n . '</span>'; } ?>
		</a></li>
	<?php endforeach; ?>
</ul>

<?php
/* =================== CASE DETAIL =================== */
if ($pfCaseId > 0):
	$case = epc_pf_case_get($db_link, $pfCaseId);
	if (!$case) { echo '<p class="text-muted">Case not found.</p>'; return; }
	$timeline = epc_pf_case_timeline($db_link, $pfCaseId);
	$statusLabel = array('open' => 'info', 'done' => 'success', 'rejected' => 'danger', 'cancelled' => 'default');
?>
	<p><a href="<?php echo epc_erp_h(pf_url($tabBase, $sep, 'monitor')); ?>">&laquo; Back to monitor</a></p>
	<div class="panel panel-default">
		<div class="panel-heading">
			<strong><?php echo epc_erp_h($case['title']); ?></strong>
			<span class="label label-<?php echo $statusLabel[$case['status']] ?? 'default'; ?>" style="margin-left:8px;"><?php echo epc_erp_h(strtoupper($case['status'])); ?></span>
			<span class="text-muted" style="float:right;"><?php echo epc_erp_h($case['process_name']); ?> · ref <?php echo epc_erp_h($case['reference'] ?: '—'); ?></span>
		</div>
		<div class="panel-body">
			<p class="text-muted">
				Initiated by <strong><?php echo epc_erp_h($case['initiator_name']); ?></strong>
				on <?php echo epc_erp_h(date('Y-m-d H:i', (int)$case['started_at'])); ?>.
				Priority: <strong><?php echo epc_erp_h(ucfirst($case['priority'])); ?></strong>.
				<?php if ($case['status'] === 'open'): ?>Currently with <strong><?php echo epc_erp_h($case['assignee_name']); ?></strong>.<?php endif; ?>
			</p>

			<!-- GPS-style tracking route -->
			<?php
			$deptName = function ($code) {
				if ($code === '') { return ''; }
				return function_exists('epc_erp_staff_department_name') ? (epc_erp_staff_department_name($code) ?: ucfirst($code)) : ucfirst($code);
			};
			$totalSteps = max(1, count($timeline));
			$doneSteps = 0; $activeIdx = -1;
			foreach ($timeline as $ti => $ts) {
				if ($ts['status'] === 'approved') { $doneSteps++; }
				if ($ts['status'] === 'active' && $activeIdx < 0) { $activeIdx = $ti; }
			}
			$reachedIdx = $activeIdx >= 0 ? $activeIdx : ($case['status'] === 'done' ? $totalSteps : $doneSteps);
			$pct = $totalSteps > 1 ? round(($reachedIdx) / ($totalSteps - 1) * 100) : 100;
			if ($case['status'] === 'done') { $pct = 100; }
			$curLoc = (string) ($case['current_location'] ?? '');
			?>
			<style>
			.pf-track-wrap{background:#0f172a;border-radius:10px;padding:22px 18px 14px;margin:14px 0;overflow-x:auto;}
			.pf-track-hd{color:#e2e8f0;font-size:13px;margin-bottom:18px;}
			.pf-track-hd .pin{color:#38bdf8;}
			.pf-route{position:relative;display:flex;min-width:680px;}
			.pf-route .pf-line{position:absolute;top:20px;left:5%;right:5%;height:4px;background:#334155;border-radius:2px;}
			.pf-route .pf-line-fill{position:absolute;top:20px;left:5%;height:4px;background:linear-gradient(90deg,#22c55e,#38bdf8);border-radius:2px;transition:width .4s;}
			.pf-node{position:relative;flex:1;text-align:center;z-index:2;padding:0 4px;}
			.pf-dot{width:42px;height:42px;line-height:42px;border-radius:50%;margin:0 auto;color:#fff;font-size:16px;box-shadow:0 0 0 4px #0f172a;}
			.pf-dot.done{background:#22c55e;} .pf-dot.active{background:#3b82f6;} .pf-dot.pending{background:#475569;color:#cbd5e1;} .pf-dot.rejected{background:#ef4444;}
			.pf-dot.active{animation:pfpulse 1.4s infinite;}
			@keyframes pfpulse{0%{box-shadow:0 0 0 0 rgba(59,130,246,.7),0 0 0 4px #0f172a;}70%{box-shadow:0 0 0 14px rgba(59,130,246,0),0 0 0 4px #0f172a;}100%{box-shadow:0 0 0 0 rgba(59,130,246,0),0 0 0 4px #0f172a;}}
			.pf-loc{color:#f1f5f9;font-weight:600;font-size:12px;margin-top:8px;}
			.pf-step{color:#cbd5e1;font-size:11px;margin-top:2px;}
			.pf-meta{color:#94a3b8;font-size:10px;margin-top:1px;}
			.pf-node.is-active .pf-loc{color:#38bdf8;}
			.pf-badge-here{display:inline-block;background:#38bdf8;color:#0f172a;font-size:9px;font-weight:700;padding:1px 6px;border-radius:8px;margin-top:4px;letter-spacing:.5px;}
			</style>
			<div class="pf-track-wrap">
				<div class="pf-track-hd">
					<i class="fa fa-map-marker pin"></i>
					<?php if ($case['status'] === 'done'): ?>
						Completed — final stop <strong><?php echo epc_erp_h($curLoc ?: '—'); ?></strong>.
					<?php elseif ($case['status'] === 'open'): ?>
						Currently at <strong><?php echo epc_erp_h($curLoc ?: 'Unassigned'); ?></strong>
						· <?php echo epc_erp_h($deptName((string) $case['current_department'])); ?>
						· with <strong><?php echo epc_erp_h($case['assignee_name']); ?></strong>
						&nbsp;<span style="color:#64748b;">(<?php echo (int) $pct; ?>% of route)</span>
					<?php else: ?>
						Route stopped (<?php echo epc_erp_h(strtoupper($case['status'])); ?>).
					<?php endif; ?>
				</div>
				<div class="pf-route">
					<div class="pf-line"></div>
					<div class="pf-line-fill" style="width:<?php echo max(0, min(90, ($pct / 100) * 90)); ?>%;"></div>
					<?php foreach ($timeline as $i => $s):
						$cls = 'pending'; $icon = 'fa-map-marker';
						if ($s['status'] === 'approved') { $cls = 'done'; $icon = 'fa-check'; }
						elseif ($s['status'] === 'active') { $cls = 'active'; $icon = 'fa-truck'; }
						elseif ($s['status'] === 'rejected') { $cls = 'rejected'; $icon = 'fa-times'; }
						$loc = (string) ($s['location'] ?? '');
						$when = (int) $s['completed_at'] > 0 ? date('d M H:i', (int) $s['completed_at']) : ((int) $s['activated_at'] > 0 ? date('d M H:i', (int) $s['activated_at']) : '');
					?>
						<div class="pf-node <?php echo $s['status'] === 'active' ? 'is-active' : ''; ?>">
							<div class="pf-dot <?php echo $cls; ?>"><i class="fa <?php echo $icon; ?>"></i></div>
							<div class="pf-loc"><?php echo epc_erp_h($loc ?: '—'); ?></div>
							<div class="pf-step"><?php echo epc_erp_h($s['name']); ?></div>
							<div class="pf-meta"><?php echo epc_erp_h($deptName((string) $s['department'])); ?><?php echo $s['assignee_name'] ? ' · ' . epc_erp_h($s['assignee_name']) : ''; ?></div>
							<div class="pf-meta"><?php echo epc_erp_h($when); ?></div>
							<?php if ($s['status'] === 'active'): ?><span class="pf-badge-here">YOU ARE HERE</span><?php endif; ?>
						</div>
					<?php endforeach; ?>
				</div>
			</div>

			<table class="table table-bordered table-condensed">
				<thead><tr><th>#</th><th>Step</th><th>Assignee</th><th>Status</th><th>Acted by</th><th>When</th><th>Comment</th></tr></thead>
				<tbody>
				<?php foreach ($timeline as $s): ?>
					<tr class="<?php echo $s['status'] === 'active' ? 'info' : ''; ?>">
						<td><?php echo (int)$s['step_no']; ?></td>
						<td><?php echo epc_erp_h($s['name']); ?></td>
						<td><?php echo epc_erp_h($s['assignee_name']); ?></td>
						<td><?php echo epc_erp_h(ucfirst($s['status'])); ?></td>
						<td><?php echo epc_erp_h($s['acted_by_name'] ?: '—'); ?></td>
						<td><?php echo (int)$s['completed_at'] > 0 ? epc_erp_h(date('Y-m-d H:i', (int)$s['completed_at'])) : '—'; ?></td>
						<td><small><?php echo epc_erp_h($s['comment'] ?: ''); ?></small></td>
					</tr>
				<?php endforeach; ?>
				</tbody>
			</table>

			<?php if ($case['status'] === 'open'): ?>
			<div class="well well-sm">
				<input type="hidden" id="pf_act_case" value="<?php echo (int)$case['id']; ?>">
				<textarea id="pf_act_comment" class="form-control" rows="2" placeholder="Comment (optional)" style="margin-bottom:8px;"></textarea>
				<button class="btn btn-success btn-sm pf-act" data-decision="approve"><i class="fa fa-check"></i> Approve &amp; route to next</button>
				<button class="btn btn-danger btn-sm pf-act" data-decision="reject"><i class="fa fa-times"></i> Reject</button>
				<button class="btn btn-default btn-sm" id="pf_cancel_case"><i class="fa fa-ban"></i> Cancel case</button>
			</div>
			<?php endif; ?>
		</div>
	</div>

<?php
/* =================== MONITOR =================== */
elseif ($pfView === 'monitor'):
	$sum = epc_pf_monitor_summary($db_link);
	$fStatus = (string) ($_GET['f_status'] ?? 'open');
	$cases = epc_pf_cases($db_link, array('status' => $fStatus !== 'all' ? $fStatus : '', 'limit' => 300));
	$processes = epc_pf_processes($db_link, true);
?>
	<div class="row" style="margin-bottom:6px;">
		<?php
		$tiles = array(
			array('Open cases', $sum['open'], '#2563eb', 'fa-folder-open'),
			array('Overdue', $sum['overdue'], '#dc2626', 'fa-exclamation-triangle'),
			array('Completed', $sum['done'], '#16a34a', 'fa-check-circle'),
			array('Rejected', $sum['rejected'], '#b45309', 'fa-times-circle'),
			array('Avg cycle (h)', $sum['avg_cycle_hours'], '#0891b2', 'fa-clock-o'),
		);
		foreach ($tiles as $t): ?>
			<div class="col-sm-2 col-xs-4" style="margin-bottom:10px;">
				<div style="background:#fff; border:1px solid #e5e7eb; border-left:4px solid <?php echo $t[2]; ?>; border-radius:6px; padding:10px;">
					<div style="font-size:11px; color:#6b7280;"><i class="fa <?php echo $t[3]; ?>"></i> <?php echo epc_erp_h($t[0]); ?></div>
					<div style="font-size:22px; font-weight:700; color:<?php echo $t[2]; ?>;"><?php echo epc_erp_h((string)$t[1]); ?></div>
				</div>
			</div>
		<?php endforeach; ?>
		<div class="col-sm-2 col-xs-4" style="margin-bottom:10px;">
			<div style="background:#fff; border:1px solid #e5e7eb; border-radius:6px; padding:10px;">
				<div style="font-size:11px; color:#6b7280;"><i class="fa fa-database"></i> Sample data</div>
				<button class="btn btn-xs btn-default" id="pf_seed" style="margin-top:4px;">Seed</button>
				<button class="btn btn-xs btn-link" id="pf_clear">Clear</button>
			</div>
		</div>
	</div>

	<!-- start a case -->
	<div class="panel panel-default">
		<div class="panel-heading"><strong><i class="fa fa-play-circle"></i> Start a new case</strong></div>
		<div class="panel-body">
			<?php if (empty($processes)): ?>
				<p class="text-muted">No active processes yet. Create one under <a href="<?php echo epc_erp_h(pf_url($tabBase, $sep, 'processes')); ?>">Processes</a>, or click <strong>Seed</strong> above to load sample processes &amp; cases.</p>
			<?php else: ?>
			<form id="pf_start_form" class="form-inline">
				<input type="hidden" name="csrf_guard_key" value="<?php echo epc_erp_h($csrfLocal); ?>">
				<select name="process_id" class="form-control input-sm" required>
					<option value="">Select process…</option>
					<?php foreach ($processes as $p): ?><option value="<?php echo (int)$p['id']; ?>"><?php echo epc_erp_h($p['name']); ?> (<?php echo (int)$p['step_count']; ?> steps)</option><?php endforeach; ?>
				</select>
				<input type="text" name="title" class="form-control input-sm" placeholder="Case title" required style="min-width:220px;">
				<input type="text" name="reference" class="form-control input-sm" placeholder="Reference (optional)">
				<select name="priority" class="form-control input-sm">
					<option value="normal">Normal</option><option value="high">High</option><option value="urgent">Urgent</option><option value="low">Low</option>
				</select>
				<button type="submit" class="btn btn-sm btn-primary">Start &amp; route</button>
			</form>
			<?php endif; ?>
		</div>
	</div>

	<form method="get" class="form-inline" style="margin-bottom:10px;">
		<?php foreach ($_GET as $k => $v) { if (in_array($k, array('f_status'), true)) continue; echo '<input type="hidden" name="' . epc_erp_h($k) . '" value="' . epc_erp_h((string)$v) . '">'; } ?>
		<label>Show</label>
		<select name="f_status" class="form-control input-sm" onchange="this.form.submit()">
			<?php foreach (array('open' => 'Open', 'all' => 'All', 'done' => 'Completed', 'rejected' => 'Rejected', 'cancelled' => 'Cancelled') as $k => $l): ?>
				<option value="<?php echo $k; ?>" <?php echo $fStatus === $k ? 'selected' : ''; ?>><?php echo $l; ?></option>
			<?php endforeach; ?>
		</select>
	</form>

	<?php $byLoc = $sum['by_location'] ?? array(); if (!empty($byLoc)): $maxLoc = max($byLoc); ?>
	<div class="panel panel-default">
		<div class="panel-heading"><strong><i class="fa fa-map-marker text-info"></i> Live site map — open cases by location</strong>
			<span class="text-muted" style="float:right;"><?php echo (int) ($sum['headcount'] ?? 0); ?> staff across <?php echo count(epc_pf_locations($db_link)); ?> sites</span>
		</div>
		<div class="panel-body" style="background:#0f172a; border-radius:0 0 4px 4px;">
			<div style="display:flex; flex-wrap:wrap; gap:14px;">
			<?php foreach ($byLoc as $loc => $cnt):
				$h = $maxLoc > 0 ? max(8, round($cnt / $maxLoc * 46)) : 8; ?>
				<div style="flex:1; min-width:130px; text-align:center; background:#1e293b; border-radius:8px; padding:12px 8px;">
					<div style="height:54px; display:flex; align-items:flex-end; justify-content:center;">
						<div style="width:30px; background:linear-gradient(180deg,#38bdf8,#2563eb); border-radius:4px 4px 0 0; height:<?php echo (int)$h; ?>px;"></div>
					</div>
					<div style="color:#f1f5f9; font-weight:700; font-size:18px; margin-top:4px;"><?php echo (int)$cnt; ?></div>
					<div style="color:#94a3b8; font-size:11px;"><i class="fa fa-map-marker"></i> <?php echo epc_erp_h((string)$loc); ?></div>
				</div>
			<?php endforeach; ?>
			</div>
		</div>
	</div>
	<?php endif; ?>

	<table class="table table-bordered table-condensed">
		<thead><tr><th>Case</th><th>Process</th><th>Progress</th><th>Current step</th><th>With</th><th>Dept</th><th>Location</th><th>Priority</th><th>Due</th><th>Status</th><th></th></tr></thead>
		<tbody>
		<?php foreach ($cases as $c):
			$pct = (int)$c['step_count'] > 0 ? round(((int)$c['current_step_no'] - ($c['status'] === 'done' ? 0 : 1)) / (int)$c['step_count'] * 100) : 0;
			if ($c['status'] === 'done') $pct = 100;
			$pColor = $c['status'] === 'done' ? '#16a34a' : ($c['overdue'] ? '#dc2626' : '#2563eb');
			$deptName = isset($deptCfg[$c['current_department']]['name']) ? $deptCfg[$c['current_department']]['name'] : $c['current_department'];
		?>
			<tr>
				<td><a href="<?php echo epc_erp_h(pf_url($tabBase, $sep, 'monitor', '&pf_case=' . (int)$c['id'])); ?>"><strong><?php echo epc_erp_h($c['title']); ?></strong></a><br><small class="text-muted"><?php echo epc_erp_h($c['reference'] ?: ''); ?></small></td>
				<td><small><?php echo epc_erp_h($c['process_name']); ?></small></td>
				<td style="min-width:130px;">
					<div style="background:#eef2f7; border-radius:4px; height:16px; position:relative;">
						<div style="background:<?php echo $pColor; ?>; width:<?php echo (int)$pct; ?>%; height:16px; border-radius:4px;"></div>
					</div>
					<small class="text-muted">step <?php echo (int)$c['current_step_no']; ?> of <?php echo (int)$c['step_count']; ?></small>
				</td>
				<td><small><?php echo epc_erp_h($c['current_step_name'] ?: '—'); ?></small></td>
				<td><?php echo epc_erp_h($c['assignee_name']); ?></td>
				<td><small><?php echo epc_erp_h($deptName ?: '—'); ?></small></td>
				<td><small><?php if (!empty($c['current_location'])): ?><i class="fa fa-map-marker text-info"></i> <?php echo epc_erp_h($c['current_location']); else: ?>—<?php endif; ?></small></td>
				<td><?php echo epc_erp_h(ucfirst($c['priority'])); ?></td>
				<td><?php if ((int)$c['due_at'] > 0): ?><span class="<?php echo $c['overdue'] ? 'text-danger' : ''; ?>"><?php echo epc_erp_h(date('m-d H:i', (int)$c['due_at'])); ?></span><?php else: ?>—<?php endif; ?></td>
				<td><span class="label label-<?php echo array('open'=>'info','done'=>'success','rejected'=>'danger','cancelled'=>'default')[$c['status']] ?? 'default'; ?>"><?php echo epc_erp_h($c['status']); ?></span></td>
				<td><a class="btn btn-xs btn-default" href="<?php echo epc_erp_h(pf_url($tabBase, $sep, 'monitor', '&pf_case=' . (int)$c['id'])); ?>">Open</a></td>
			</tr>
		<?php endforeach; ?>
		<?php if (empty($cases)): ?><tr><td colspan="11" class="text-muted">No cases. Start one above or click Seed for sample data.</td></tr><?php endif; ?>
		</tbody>
	</table>

<?php
/* =================== MY INBOX =================== */
elseif ($pfView === 'inbox'):
	$mine = epc_pf_cases($db_link, array('mine_open' => $me));
?>
	<p class="text-muted">Cases currently routed to you. Approving hands the case off automatically to the next step's assignee.</p>
	<table class="table table-bordered table-condensed">
		<thead><tr><th>Case</th><th>Process</th><th>Current step</th><th>Priority</th><th>Due</th><th>Action</th></tr></thead>
		<tbody>
		<?php foreach ($mine as $c): ?>
			<tr>
				<td><a href="<?php echo epc_erp_h(pf_url($tabBase, $sep, 'monitor', '&pf_case=' . (int)$c['id'])); ?>"><strong><?php echo epc_erp_h($c['title']); ?></strong></a></td>
				<td><small><?php echo epc_erp_h($c['process_name']); ?></small></td>
				<td><?php echo epc_erp_h($c['current_step_name'] ?: ('step ' . (int)$c['current_step_no'])); ?></td>
				<td><?php echo epc_erp_h(ucfirst($c['priority'])); ?></td>
				<td><?php echo (int)$c['due_at'] > 0 ? '<span class="' . ($c['overdue'] ? 'text-danger' : '') . '">' . epc_erp_h(date('m-d H:i', (int)$c['due_at'])) . '</span>' : '—'; ?></td>
				<td style="white-space:nowrap;">
					<button class="btn btn-xs btn-success pf-inbox-act" data-case="<?php echo (int)$c['id']; ?>" data-decision="approve">Approve</button>
					<button class="btn btn-xs btn-danger pf-inbox-act" data-case="<?php echo (int)$c['id']; ?>" data-decision="reject">Reject</button>
				</td>
			</tr>
		<?php endforeach; ?>
		<?php if (empty($mine)): ?><tr><td colspan="6" class="text-muted">Your inbox is empty — no cases are waiting on you.</td></tr><?php endif; ?>
		</tbody>
	</table>

<?php
/* =================== PROCESSES =================== */
elseif ($pfView === 'processes'):
	$processes = epc_pf_processes($db_link);
	if ($pfProcId > 0):
		$steps = epc_pf_process_steps($db_link, $pfProcId);
		$proc = null; foreach ($processes as $p) { if ((int)$p['id'] === $pfProcId) { $proc = $p; break; } }
		$assignTypes = epc_pf_assign_types();
?>
		<p><a href="<?php echo epc_erp_h(pf_url($tabBase, $sep, 'processes')); ?>">&laquo; All processes</a></p>
		<h4><?php echo epc_erp_h($proc['name'] ?? 'Process'); ?> — steps</h4>
		<table class="table table-bordered table-condensed">
			<thead><tr><th>#</th><th>Step</th><th>Routes to</th><th>SLA (h)</th><th></th></tr></thead>
			<tbody>
			<?php foreach ($steps as $s):
				$routeTo = $assignTypes[$s['assign_type']] ?? $s['assign_type'];
				if ($s['assign_type'] === 'user') $routeTo = epc_pf_user_name($db_link, (int)$s['assign_user_id']);
				elseif (in_array($s['assign_type'], array('dept_head','department'), true)) $routeTo .= ' — ' . (isset($deptCfg[$s['assign_department']]['name']) ? $deptCfg[$s['assign_department']]['name'] : $s['assign_department']);
			?>
				<tr><td><?php echo (int)$s['step_no']; ?></td><td><?php echo epc_erp_h($s['name']); ?></td><td><?php echo epc_erp_h($routeTo); ?></td><td><?php echo (int)$s['sla_hours']; ?></td>
				<td><button class="btn btn-xs btn-link text-danger pf-step-del" data-id="<?php echo (int)$s['id']; ?>">remove</button></td></tr>
			<?php endforeach; ?>
			<?php if (empty($steps)): ?><tr><td colspan="5" class="text-muted">No steps yet — add the first step below.</td></tr><?php endif; ?>
			</tbody>
		</table>
		<div class="panel panel-default"><div class="panel-heading"><strong>Add step</strong></div><div class="panel-body">
		<form id="pf_step_form" class="form-inline">
			<input type="hidden" name="csrf_guard_key" value="<?php echo epc_erp_h($csrfLocal); ?>">
			<input type="hidden" name="process_id" value="<?php echo (int)$pfProcId; ?>">
			<input type="text" name="name" class="form-control input-sm" placeholder="Step name" required style="min-width:200px;">
			<select name="assign_type" class="form-control input-sm" id="pf_assign_type">
				<?php foreach ($assignTypes as $k => $l): ?><option value="<?php echo $k; ?>"><?php echo epc_erp_h($l); ?></option><?php endforeach; ?>
			</select>
			<select name="assign_department" class="form-control input-sm">
				<option value="">— department —</option>
				<?php foreach ($deptCfg as $code => $row): ?><option value="<?php echo epc_erp_h($code); ?>"><?php echo epc_erp_h($row['name']); ?></option><?php endforeach; ?>
			</select>
			<select name="assign_user_id" class="form-control input-sm">
				<option value="0">— person (for "Specific person") —</option>
				<?php foreach ($staff as $u): ?><option value="<?php echo (int)$u['user_id']; ?>"><?php echo epc_erp_h($u['display_name']); ?></option><?php endforeach; ?>
			</select>
			<input type="number" name="sla_hours" class="form-control input-sm" value="24" style="width:90px;" title="SLA hours">
			<button type="submit" class="btn btn-sm btn-primary">Add step</button>
		</form>
		</div></div>
<?php else: ?>
		<div class="panel panel-default"><div class="panel-heading"><strong>Create process</strong></div><div class="panel-body">
		<form id="pf_proc_form" class="form-inline">
			<input type="hidden" name="csrf_guard_key" value="<?php echo epc_erp_h($csrfLocal); ?>">
			<input type="text" name="name" class="form-control input-sm" placeholder="Process name" required style="min-width:240px;">
			<input type="text" name="description" class="form-control input-sm" placeholder="Description" style="min-width:300px;">
			<button type="submit" class="btn btn-sm btn-primary">Create</button>
		</form>
		</div></div>
		<table class="table table-bordered table-condensed">
			<thead><tr><th>Process</th><th>Steps</th><th>Open cases</th><th>Active</th><th></th></tr></thead>
			<tbody>
			<?php foreach ($processes as $p): ?>
				<tr>
					<td><strong><?php echo epc_erp_h($p['name']); ?></strong><?php if ($p['description']): ?><br><small class="text-muted"><?php echo epc_erp_h($p['description']); ?></small><?php endif; ?></td>
					<td><?php echo (int)$p['step_count']; ?></td>
					<td><?php echo (int)$p['open_cases']; ?></td>
					<td><?php echo (int)$p['active'] ? '<span class="label label-success">yes</span>' : '<span class="label label-default">no</span>'; ?></td>
					<td><a class="btn btn-xs btn-default" href="<?php echo epc_erp_h(pf_url($tabBase, $sep, 'processes', '&pf_proc=' . (int)$p['id'])); ?>">Manage steps</a></td>
				</tr>
			<?php endforeach; ?>
			<?php if (empty($processes)): ?><tr><td colspan="5" class="text-muted">No processes yet — create one above, or Seed sample data from the Monitor tab.</td></tr><?php endif; ?>
			</tbody>
		</table>
<?php endif; ?>

<?php
/* =================== DEPARTMENT HEADS =================== */
elseif ($pfView === 'heads'):
	$heads = epc_pf_dept_heads($db_link);
?>
	<p class="text-muted">Set the head of each department. Steps routed to "Department head" go to the person named here.</p>
	<table class="table table-bordered table-condensed" style="max-width:620px;">
		<thead><tr><th>Department</th><th>Head</th></tr></thead>
		<tbody>
		<?php foreach ($deptCfg as $code => $row):
			$deptStaff = array_filter($staff, function ($u) use ($code) { return (string)$u['department_code'] === $code; });
			$cur = (int)($heads[$code] ?? 0);
		?>
			<tr>
				<td><i class="fa <?php echo epc_erp_h($row['icon'] ?? 'fa-users'); ?>"></i> <?php echo epc_erp_h($row['name']); ?></td>
				<td>
					<select class="form-control input-sm pf-head-select" data-dept="<?php echo epc_erp_h($code); ?>">
						<option value="0">— unassigned —</option>
						<?php foreach ($staff as $u): ?>
							<option value="<?php echo (int)$u['user_id']; ?>" <?php echo $cur === (int)$u['user_id'] ? 'selected' : ''; ?>><?php echo epc_erp_h($u['display_name']); ?> (<?php echo epc_erp_h($u['department_code']); ?>)</option>
						<?php endforeach; ?>
						<?php if ($cur > 0 && !array_filter($staff, function ($u) use ($cur) { return (int)$u['user_id'] === $cur; })): ?>
							<option value="<?php echo $cur; ?>" selected><?php echo epc_erp_h(epc_pf_user_name($db_link, $cur)); ?></option>
						<?php endif; ?>
					</select>
				</td>
			</tr>
		<?php endforeach; ?>
		</tbody>
	</table>
	<?php if (empty($staff)): ?><p class="text-muted"><em>No staff profiles found yet. Heads can still be auto-assigned to the admin user when you Seed sample data; add staff under People → Staff to route to real people.</em></p><?php endif; ?>
<?php endif; ?>

<?php
$pfEndpoint = isset($GLOBALS['erpAjaxEndpoint']) ? $GLOBALS['erpAjaxEndpoint'] : ('/' . (isset($GLOBALS['DP_Config']->backend_dir) ? $GLOBALS['DP_Config']->backend_dir : 'cp') . '/content/shop/finance/erp/ajax_erp_endpoint.php');
?>
<script>
(function(){
	var url = <?php echo json_encode($pfEndpoint); ?>;
	var csrf = <?php echo json_encode($csrfLocal); ?>;
	function post(action, fd){ fd.append('action', action); if(!fd.get('csrf_guard_key')) fd.append('csrf_guard_key', csrf); return fetch(url,{method:'POST',body:fd,credentials:'same-origin'}).then(function(r){return r.json();}); }
	function msg(j){ var el=document.getElementById('epc_erp_msg'); if(el){ el.className='alert alert-'+(j.status?'success':'danger'); el.textContent=j.message||''; el.style.display='block'; el.scrollIntoView({behavior:'smooth',block:'center'}); } if(j.status) setTimeout(function(){ location.reload(); }, 800); }

	var sf=document.getElementById('pf_start_form'); if(sf) sf.addEventListener('submit', function(e){ e.preventDefault(); post('pf_case_start', new FormData(sf)).then(msg); });
	var pf=document.getElementById('pf_proc_form'); if(pf) pf.addEventListener('submit', function(e){ e.preventDefault(); post('pf_process_save', new FormData(pf)).then(msg); });
	var stf=document.getElementById('pf_step_form'); if(stf) stf.addEventListener('submit', function(e){ e.preventDefault(); post('pf_step_save', new FormData(stf)).then(msg); });

	document.querySelectorAll('.pf-step-del').forEach(function(b){ b.addEventListener('click', function(){ if(!confirm('Remove this step?')) return; var fd=new FormData(); fd.append('step_id', b.getAttribute('data-id')); post('pf_step_delete', fd).then(msg); }); });
	document.querySelectorAll('.pf-inbox-act').forEach(function(b){ b.addEventListener('click', function(){ var d=b.getAttribute('data-decision'); if(d==='reject' && !confirm('Reject this case? It will stop here.')) return; var fd=new FormData(); fd.append('case_id', b.getAttribute('data-case')); fd.append('decision', d); post('pf_case_act', fd).then(msg); }); });
	document.querySelectorAll('.pf-act').forEach(function(b){ b.addEventListener('click', function(){ var d=b.getAttribute('data-decision'); var cid=document.getElementById('pf_act_case').value; var cmt=document.getElementById('pf_act_comment'); if(d==='reject' && !confirm('Reject this case? It will stop here.')) return; var fd=new FormData(); fd.append('case_id', cid); fd.append('decision', d); fd.append('comment', cmt?cmt.value:''); post('pf_case_act', fd).then(msg); }); });
	var cc=document.getElementById('pf_cancel_case'); if(cc) cc.addEventListener('click', function(){ if(!confirm('Cancel this case?')) return; var fd=new FormData(); fd.append('case_id', document.getElementById('pf_act_case').value); post('pf_case_cancel', fd).then(msg); });
	document.querySelectorAll('.pf-head-select').forEach(function(s){ s.addEventListener('change', function(){ var fd=new FormData(); fd.append('department_code', s.getAttribute('data-dept')); fd.append('head_user_id', s.value); post('pf_set_dept_head', fd).then(msg); }); });

	var sd=document.getElementById('pf_seed'); if(sd) sd.addEventListener('click', function(){ if(!confirm('Seed sample processes and running cases?')) return; sd.disabled=true; sd.textContent='…'; post('pf_seed_demo', new FormData()).then(msg); });
	var cl=document.getElementById('pf_clear'); if(cl) cl.addEventListener('click', function(){ if(!confirm('Clear sample cases and demo processes?')) return; post('pf_clear_demo', new FormData()).then(msg); });
})();
</script>
