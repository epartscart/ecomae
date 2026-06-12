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

$views = array('monitor' => 'Monitor', 'orgmap' => 'Org map', 'workforce' => 'Workforce', 'inbox' => 'My inbox', 'processes' => 'Processes', 'heads' => 'Department heads');
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

			<?php
			$pfDocs = function_exists('epc_pf_case_documents') ? epc_pf_case_documents($db_link, $case) : array();
			if (!empty($pfDocs)): ?>
			<div class="pf-docs" style="margin:0 0 14px;padding:10px 12px;background:#f6f8fb;border:1px solid #e3e9f1;border-radius:6px;">
				<strong style="margin-right:6px;"><i class="fa fa-folder-open-o"></i> Open document:</strong>
				<?php foreach ($pfDocs as $d): ?>
					<a class="btn btn-xs btn-default" href="<?php echo epc_erp_h($d['url']); ?>" target="_blank" rel="noopener" style="margin:2px 4px 2px 0;"><i class="fa <?php echo epc_erp_h($d['icon']); ?>"></i> <?php echo epc_erp_h($d['label']); ?></a>
				<?php endforeach; ?>
			</div>
			<?php endif; ?>

			<!-- GPS-style tracking route (animated, multi-level) -->
			<?php
			$deptName = function ($code) {
				if ($code === '') { return ''; }
				return function_exists('epc_erp_staff_department_name') ? (epc_erp_staff_department_name($code) ?: ucfirst($code)) : ucfirst($code);
			};
			$curLoc = (string) ($case['current_location'] ?? '');
			// staff photo/avatar chip
			$avatarHtml = function ($name, $userId = 0, $size = 22) use ($db_link) {
				$name = (string) $name;
				if ($name === '' || $name === '—') { return ''; }
				$url = epc_pf_avatar_url($name, epc_pf_user_photo($db_link, (int) $userId));
				$ini = '';
				$parts = preg_split('/\s+/', trim($name));
				if (!empty($parts)) { $ini = strtoupper(substr($parts[0], 0, 1) . (count($parts) > 1 ? substr($parts[count($parts) - 1], 0, 1) : '')); }
				$h = 0; for ($i = 0; $i < strlen($name); $i++) { $h = ($h * 31 + ord($name[$i])) % 360; }
				$fs = (int) round($size * 0.38);
				return '<span class="pf-av" style="width:' . (int) $size . 'px;height:' . (int) $size . 'px;font-size:' . $fs . 'px;background:hsl(' . $h . ',55%,45%);">' . epc_erp_h($ini) . '<img src="' . epc_erp_h($url) . '" alt="" onerror="this.remove()"></span>';
			};

			// status of an aggregated node from its child steps
			$aggStatus = function (array $kids) {
				$has = function ($st) use ($kids) { foreach ($kids as $k) { if ($k['status'] === $st) return true; } return false; };
				if ($has('active')) return 'active';
				if ($has('rejected')) return 'rejected';
				$allAppr = true; foreach ($kids as $k) { if ($k['status'] !== 'approved' && $k['status'] !== 'skipped') { $allAppr = false; break; } }
				if ($allAppr) return 'done';
				if ($has('approved')) return 'active';
				return 'pending';
			};
			$nodeWhen = function (array $kids) {
				$t = 0;
				foreach ($kids as $k) { $t = max($t, (int) $k['completed_at'], (int) $k['activated_at']); }
				return $t > 0 ? date('d M H:i', $t) : '';
			};
			// build the four zoom levels from the same timeline
			$buildLevel = function ($level) use ($timeline, $deptName, $aggStatus, $nodeWhen, $case, $curLoc, $avatarHtml) {
				$nodes = array();
				if ($level === 'task') {
					foreach ($timeline as $s) {
						$nodes[] = array(
							'title' => ((string) ($s['location'] ?? '')) ?: '—',
							'sub' => $s['name'],
							'meta' => trim($deptName((string) $s['department']) . ($s['assignee_name'] ? ' · ' . $s['assignee_name'] : ''), ' ·'),
							'avatar' => $avatarHtml((string) $s['assignee_name'], (int) $s['assignee_id'], 30),
							'when' => ((int) $s['completed_at'] > 0 ? date('d M H:i', (int) $s['completed_at']) : ((int) $s['activated_at'] > 0 ? date('d M H:i', (int) $s['activated_at']) : '')),
							'status' => $s['status'],
						);
					}
				} elseif ($level === 'department' || $level === 'location') {
					$keyFn = $level === 'department'
						? function ($s) { return (string) $s['department']; }
						: function ($s) { return (string) ($s['location'] ?? ''); };
					$groups = array(); $cur = null; $curKey = null;
					foreach ($timeline as $s) {
						$k = $keyFn($s);
						if ($cur === null || $k !== $curKey) { if ($cur !== null) $groups[] = $cur; $cur = array(); $curKey = $k; }
						$cur[] = $s;
					}
					if ($cur !== null) $groups[] = $cur;
					foreach ($groups as $g) {
						$first = $g[0];
						if ($level === 'department') {
							$locs = array(); foreach ($g as $x) { $l = (string) ($x['location'] ?? ''); if ($l !== '' && !in_array($l, $locs, true)) $locs[] = $l; }
							$title = $deptName((string) $first['department']) ?: '—';
							$sub = count($g) . ' step' . (count($g) > 1 ? 's' : '') . ' · ' . $first['name'];
							$meta = implode(' · ', $locs);
						} else {
							$deps = array(); foreach ($g as $x) { $d = $deptName((string) $x['department']); if ($d !== '' && !in_array($d, $deps, true)) $deps[] = $d; }
							$title = ((string) ($first['location'] ?? '')) ?: '—';
							$sub = implode(' · ', $deps);
							$meta = count($g) . ' step' . (count($g) > 1 ? 's' : '');
						}
						$nodes[] = array('title' => $title, 'sub' => $sub, 'meta' => $meta, 'when' => $nodeWhen($g), 'status' => $aggStatus($g));
					}
				} else { // overall
					$n = count($timeline);
					$first = $timeline[0] ?? null;
					$last = $timeline[$n - 1] ?? null;
					$active = null; foreach ($timeline as $s) { if ($s['status'] === 'active') { $active = $s; break; } }
					if ($first) $nodes[] = array('title' => 'Start', 'sub' => ((string) ($first['location'] ?? '')) ?: '—', 'meta' => $deptName((string) $first['department']), 'when' => ((int) $first['activated_at'] > 0 ? date('d M H:i', (int) $first['activated_at']) : ''), 'status' => 'done');
					if ($case['status'] === 'open') {
						$nodes[] = array('title' => 'In progress', 'sub' => $curLoc ?: 'Unassigned', 'meta' => $deptName((string) $case['current_department']) . ($case['assignee_name'] ? ' · ' . $case['assignee_name'] : ''), 'when' => ($active && (int) $active['activated_at'] > 0 ? date('d M H:i', (int) $active['activated_at']) : ''), 'status' => 'active');
					}
					if ($last) $nodes[] = array('title' => 'Finish', 'sub' => ((string) ($last['location'] ?? '')) ?: ($last['name'] ?? '—'), 'meta' => $deptName((string) $last['department']), 'when' => ((int) $last['completed_at'] > 0 ? date('d M H:i', (int) $last['completed_at']) : ''), 'status' => ($case['status'] === 'done' ? 'done' : 'pending'));
				}
				return $nodes;
			};
			$levels = array('overall' => 'Overall', 'location' => 'Location', 'department' => 'Department', 'task' => 'Task');
			$dotMeta = array(
				'approved' => array('done', 'fa-check'), 'done' => array('done', 'fa-check'),
				'active' => array('active', 'fa-truck'), 'rejected' => array('rejected', 'fa-times'),
				'pending' => array('pending', 'fa-map-marker'), 'skipped' => array('done', 'fa-angle-double-right'),
			);
			?>
			<style>
			.pf-track-wrap{background:#0f172a;border-radius:10px;padding:16px 18px 14px;margin:14px 0;}
			.pf-track-hd{color:#e2e8f0;font-size:13px;margin-bottom:6px;}
			.pf-track-hd .pin{color:#38bdf8;}
			.pf-levels{margin:8px 0 18px;}
			.pf-levels button{background:#1e293b;color:#cbd5e1;border:1px solid #334155;border-radius:6px;padding:4px 12px;font-size:12px;margin-right:6px;cursor:pointer;}
			.pf-levels button.on{background:#38bdf8;color:#0f172a;border-color:#38bdf8;font-weight:700;}
			.pf-route-scroll{overflow-x:auto;}
			.pf-route{position:relative;display:none;min-width:680px;padding-top:6px;}
			.pf-route.on{display:flex;}
			.pf-route .pf-line{position:absolute;top:26px;height:4px;background:#334155;border-radius:2px;}
			.pf-route .pf-line-fill{position:absolute;top:26px;height:4px;width:0;background:linear-gradient(90deg,#22c55e,#38bdf8);border-radius:2px;transition:width 1.3s ease;}
			.pf-marker{position:absolute;top:12px;z-index:5;color:#38bdf8;font-size:20px;transition:left 1.3s ease;transform:translateX(-50%);}
			.pf-node{position:relative;flex:1;text-align:center;z-index:2;padding:0 4px;}
			.pf-dot{width:42px;height:42px;line-height:42px;border-radius:50%;margin:0 auto;color:#fff;font-size:16px;box-shadow:0 0 0 4px #0f172a;}
			.pf-dot.done{background:#22c55e;} .pf-dot.active{background:#3b82f6;} .pf-dot.pending{background:#475569;color:#cbd5e1;} .pf-dot.rejected{background:#ef4444;}
			.pf-dot.active{animation:pfpulse 1.4s infinite;}
			@keyframes pfpulse{0%{box-shadow:0 0 0 0 rgba(59,130,246,.7),0 0 0 4px #0f172a;}70%{box-shadow:0 0 0 14px rgba(59,130,246,0),0 0 0 4px #0f172a;}100%{box-shadow:0 0 0 0 rgba(59,130,246,0),0 0 0 4px #0f172a;}}
			.pf-loc{color:#f1f5f9;font-weight:600;font-size:12px;margin-top:8px;}
			.pf-step{color:#cbd5e1;font-size:11px;margin-top:2px;}
			.pf-meta{color:#94a3b8;font-size:10px;margin-top:1px;}
			.pf-av{position:relative;display:inline-flex;align-items:center;justify-content:center;border-radius:50%;color:#fff;font-weight:700;overflow:hidden;vertical-align:middle;}
			.pf-av img{position:absolute;inset:0;width:100%;height:100%;object-fit:cover;}
			.pf-node .pf-av{margin:4px auto 0;}
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
						· <?php echo epc_erp_h(epc_pf_bu_for_dept((string) $case['current_department'])); ?>
						· <?php echo epc_erp_h(epc_pf_legal_entity_for_location((string) $curLoc)); ?>
						· with <?php echo $avatarHtml((string) $case['assignee_name'], (int) $case['current_assignee_id'], 24); ?> <strong><?php echo epc_erp_h($case['assignee_name']); ?></strong>
					<?php else: ?>
						Route stopped (<?php echo epc_erp_h(strtoupper($case['status'])); ?>).
					<?php endif; ?>
				</div>
				<div class="pf-levels">
					<span style="color:#64748b;font-size:11px;margin-right:6px;">Zoom:</span>
					<?php foreach ($levels as $lk => $lv): ?>
						<button type="button" class="pf-level-btn <?php echo $lk === 'task' ? 'on' : ''; ?>" data-level="<?php echo $lk; ?>"><?php echo $lv; ?></button>
					<?php endforeach; ?>
				</div>
				<div class="pf-route-scroll">
				<?php foreach ($levels as $lk => $lv):
					$nodes = $buildLevel($lk);
					$n = max(1, count($nodes));
					$reached = 0; $hasActive = false;
					foreach ($nodes as $ix => $nd) { if ($nd['status'] === 'active') { $reached = $ix; $hasActive = true; break; } if ($nd['status'] === 'approved' || $nd['status'] === 'done' || $nd['status'] === 'skipped') { $reached = $ix; } }
					if ($case['status'] === 'done') { $reached = $n - 1; }
					$centerL = $n > 0 ? (0.5 / $n * 100) : 50;
					$centerR = $n > 0 ? (($n - 0.5) / $n * 100) : 50;
					$reachedPct = $n > 1 ? (($reached + 0.5) / $n * 100) : 50;
					$lineW = $centerR - $centerL;
					$fillW = max(0, $reachedPct - $centerL);
				?>
					<div class="pf-route <?php echo $lk === 'task' ? 'on' : ''; ?>" data-level="<?php echo $lk; ?>">
						<div class="pf-line" style="left:<?php echo $centerL; ?>%; width:<?php echo $lineW; ?>%;"></div>
						<div class="pf-line-fill" data-target="<?php echo round($fillW, 2); ?>" style="left:<?php echo $centerL; ?>%;"></div>
						<?php if ($case['status'] === 'open'): ?><div class="pf-marker" data-target="<?php echo round($reachedPct, 2); ?>" style="left:<?php echo $centerL; ?>%;"><i class="fa fa-location-arrow"></i></div><?php endif; ?>
						<?php foreach ($nodes as $nd):
							$dm = $dotMeta[$nd['status']] ?? array('pending', 'fa-map-marker');
						?>
							<div class="pf-node <?php echo $nd['status'] === 'active' ? 'is-active' : ''; ?>">
								<div class="pf-dot <?php echo $dm[0]; ?>"><i class="fa <?php echo $dm[1]; ?>"></i></div>
								<div class="pf-loc"><?php echo epc_erp_h($nd['title']); ?></div>
								<div class="pf-step"><?php echo epc_erp_h((string) $nd['sub']); ?></div>
								<?php if (!empty($nd['avatar'])): ?><?php echo $nd['avatar']; ?><?php endif; ?>
								<?php if (!empty($nd['meta'])): ?><div class="pf-meta"><?php echo epc_erp_h((string) $nd['meta']); ?></div><?php endif; ?>
								<?php if (!empty($nd['when'])): ?><div class="pf-meta"><?php echo epc_erp_h((string) $nd['when']); ?></div><?php endif; ?>
								<?php if ($nd['status'] === 'active'): ?><span class="pf-badge-here">YOU ARE HERE</span><?php endif; ?>
							</div>
						<?php endforeach; ?>
					</div>
				<?php endforeach; ?>
				</div>
			</div>
			<script>
			(function(){
				function animate(route){
					if(!route) return;
					route.querySelectorAll('.pf-line-fill').forEach(function(el){ el.style.width='0%'; });
					var f=route.querySelector('.pf-line-fill'), m=route.querySelector('.pf-marker');
					setTimeout(function(){
						if(f) f.style.width=(f.getAttribute('data-target')||0)+'%';
						if(m) m.style.left=(m.getAttribute('data-target')||0)+'%';
					},120);
				}
				var btns=document.querySelectorAll('.pf-level-btn');
				btns.forEach(function(b){
					b.addEventListener('click',function(){
						var lvl=b.getAttribute('data-level');
						btns.forEach(function(x){x.classList.remove('on');}); b.classList.add('on');
						document.querySelectorAll('.pf-route').forEach(function(r){ r.classList.toggle('on', r.getAttribute('data-level')===lvl); });
						animate(document.querySelector('.pf-route.on'));
					});
				});
				animate(document.querySelector('.pf-route.on'));
			})();
			</script>

			<table class="table table-bordered table-condensed">
				<thead><tr><th>#</th><th>Step</th><th>Assignee</th><th>Status</th><th>Acted by</th><th>When</th><th>Comment</th></tr></thead>
				<tbody>
				<?php foreach ($timeline as $s): ?>
					<tr class="<?php echo $s['status'] === 'active' ? 'info' : ''; ?>">
						<td><?php echo (int)$s['step_no']; ?></td>
						<td><?php echo epc_erp_h($s['name']); ?></td>
						<td><?php echo $avatarHtml((string) $s['assignee_name'], (int) $s['assignee_id'], 20); ?> <?php echo epc_erp_h($s['assignee_name']); ?></td>
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
/* =================== ORG MAP (Verizon Reveal-style live process map) =================== */
elseif ($pfView === 'orgmap'):
	$orgmap = epc_pf_orgmap_data($db_link);
	$omJson = json_encode($orgmap, JSON_UNESCAPED_UNICODE | JSON_HEX_TAG | JSON_HEX_AMP | JSON_HEX_APOS | JSON_HEX_QUOT);
	$omCaseBase = pf_url($tabBase, $sep, 'monitor', '&pf_case=');
?>
	<style>
	.pf-om-bar{display:flex;flex-wrap:wrap;align-items:center;gap:10px;margin-bottom:10px;}
	.pf-om-bar .seg{display:inline-flex;border:1px solid #cbd5e1;border-radius:7px;overflow:hidden;}
	.pf-om-bar .seg button{background:#fff;color:#334155;border:0;border-right:1px solid #e2e8f0;padding:6px 14px;font-size:12px;font-weight:600;cursor:pointer;}
	.pf-om-bar .seg button:last-child{border-right:0;}
	.pf-om-bar .seg button.on{background:#2563eb;color:#fff;}
	.pf-om-bar select{border:1px solid #cbd5e1;border-radius:7px;padding:6px 10px;font-size:12px;}
	.pf-om-bar .tot{margin-left:auto;color:#475569;font-size:12px;}
	.pf-om-bar .tot strong{color:#0f172a;font-size:15px;}
	.pf-om-wrap{display:flex;border:1px solid #e2e8f0;border-radius:10px;overflow:hidden;height:660px;background:#fff;}
	.pf-om-side{width:330px;min-width:330px;border-right:1px solid #e2e8f0;display:flex;flex-direction:column;}
	.pf-om-shd{padding:10px;border-bottom:1px solid #eef2f7;}
	.pf-om-shd input,.pf-om-shd select{width:100%;border:1px solid #cbd5e1;border-radius:6px;padding:6px 9px;font-size:12px;margin-bottom:6px;}
	.pf-om-scount{font-size:11px;color:#64748b;padding:0 10px 6px;}
	.pf-om-list{overflow-y:auto;flex:1;}
	.pf-om-li{display:flex;gap:9px;padding:10px;border-bottom:1px solid #f1f5f9;cursor:pointer;}
	.pf-om-li:hover{background:#f8fafc;}
	.pf-om-li.sel{background:#eff6ff;}
	.pf-om-pri{width:8px;height:8px;border-radius:50%;margin-top:5px;flex:0 0 8px;}
	.pri-urgent{background:#dc2626;} .pri-high{background:#f59e0b;} .pri-normal{background:#3b82f6;} .pri-low{background:#94a3b8;}
	.pf-om-li .t{font-size:12px;font-weight:600;color:#0f172a;line-height:1.25;}
	.pf-om-li .m{font-size:11px;color:#64748b;margin-top:2px;}
	.pf-om-li .od{color:#dc2626;font-weight:600;}
	.pf-om-canvas{flex:1;overflow:auto;background:#f8fafc;background-image:linear-gradient(#eef2f7 1px,transparent 1px),linear-gradient(90deg,#eef2f7 1px,transparent 1px);background-size:26px 26px;padding:16px 18px;}
	.pf-om-lane{margin-bottom:26px;}
	.pf-om-lane h5{margin:0 0 12px;font-size:13px;color:#0f172a;font-weight:700;}
	.pf-om-lane h5 .pill{background:#e2e8f0;color:#334155;border-radius:10px;font-size:11px;padding:1px 8px;margin-left:6px;font-weight:600;}
	.pf-om-flow{display:flex;align-items:stretch;min-width:max-content;}
	.pf-om-node{width:172px;min-width:172px;background:#fff;border:1px solid #e2e8f0;border-radius:10px;box-shadow:0 1px 2px rgba(15,23,42,.06);overflow:hidden;cursor:pointer;transition:box-shadow .15s,transform .15s;}
	.pf-om-node:hover{box-shadow:0 6px 16px rgba(37,99,235,.16);transform:translateY(-1px);}
	.pf-om-node.live{border-color:#2563eb;}
	.pf-om-node.sel{border-color:#1d4ed8;box-shadow:0 0 0 2px rgba(37,99,235,.25);}
	.pf-om-nhd{padding:7px 10px;color:#fff;font-size:11px;font-weight:700;letter-spacing:.2px;display:flex;justify-content:space-between;align-items:center;}
	.pf-om-nbody{padding:9px 10px;}
	.pf-om-nname{font-size:12px;color:#0f172a;font-weight:600;line-height:1.25;min-height:30px;}
	.pf-om-nsub{font-size:11px;color:#64748b;margin-top:3px;}
	.pf-om-count{display:inline-flex;align-items:center;justify-content:center;min-width:22px;height:22px;border-radius:11px;background:rgba(255,255,255,.25);font-size:12px;padding:0 6px;}
	.pf-om-node.empty .pf-om-nhd{background:#94a3b8 !important;}
	.pf-om-emp{margin-top:7px;border-top:1px dashed #e2e8f0;padding-top:6px;}
	.pf-om-emp .r{display:flex;justify-content:space-between;font-size:11px;color:#334155;padding:2px 0;}
	.pf-om-emp .r b{background:#eff6ff;color:#1d4ed8;border-radius:8px;padding:0 6px;font-size:10px;}
	.pf-om-emp .none{font-size:11px;color:#94a3b8;}
	.pf-om-conn{width:46px;min-width:46px;position:relative;align-self:center;height:4px;}
	.pf-om-conn .ln{position:absolute;top:0;left:0;right:6px;height:4px;background:#cbd5e1;border-radius:2px;}
	.pf-om-conn.flow .ln{background:linear-gradient(90deg,#22c55e,#2563eb);}
	.pf-om-conn .hd{position:absolute;right:-1px;top:-4px;color:#94a3b8;font-size:12px;}
	.pf-om-conn.flow .hd{color:#2563eb;}
	.pf-om-conn .dot{position:absolute;top:-2px;left:0;width:8px;height:8px;border-radius:50%;background:#2563eb;box-shadow:0 0 6px rgba(37,99,235,.8);}
	.pf-om-conn.flow .dot{animation:pfmove 1.6s linear infinite;}
	@keyframes pfmove{0%{left:0;opacity:0;}10%{opacity:1;}90%{opacity:1;}100%{left:38px;opacity:0;}}
	.pf-om-empty-lane{color:#94a3b8;font-size:12px;padding:10px;}
	.pf-av{position:relative;display:inline-flex;align-items:center;justify-content:center;border-radius:50%;color:#fff;font-weight:700;overflow:hidden;flex:0 0 auto;vertical-align:middle;}
	.pf-av img{position:absolute;inset:0;width:100%;height:100%;object-fit:cover;}
	.pf-om-emp .r{align-items:center;gap:5px;}
	</style>

	<div class="pf-om-bar">
		<label style="font-size:12px;color:#475569;margin:0;">Process</label>
		<select id="pf_om_proc"></select>
		<label style="font-size:12px;color:#475569;margin:0 0 0 8px;">View level</label>
		<div class="seg" id="pf_om_level">
			<button data-lvl="overall">Overall</button>
			<button data-lvl="legalentity">Legal entity</button>
			<button data-lvl="bu">Business unit</button>
			<button data-lvl="department" class="on">Department</button>
			<button data-lvl="user">User</button>
			<button data-lvl="task">Task</button>
			<button data-lvl="location">Location</button>
		</div>
		<span class="tot" id="pf_om_tot"></span>
	</div>

	<div class="pf-om-wrap">
		<div class="pf-om-side">
			<div class="pf-om-shd">
				<input type="text" id="pf_om_search" placeholder="Search cases, ref, person…">
				<select id="pf_om_sort">
					<option value="priority">Sort: Priority</option>
					<option value="recent">Sort: Most recent</option>
					<option value="dept">Sort: Department</option>
					<option value="title">Sort: Title A–Z</option>
				</select>
			</div>
			<div class="pf-om-scount" id="pf_om_scount"></div>
			<div class="pf-om-list" id="pf_om_list"></div>
		</div>
		<div class="pf-om-canvas" id="pf_om_canvas"></div>
	</div>

	<script>
	(function(){
		var DATA = <?php echo $omJson ?: '{"processes":[]}'; ?>;
		var CASE_BASE = <?php echo json_encode($omCaseBase); ?>;
		var procs = DATA.processes || [];
		var COLORS = {sales:'#2563eb',logistics:'#0891b2',finance:'#16a34a',marketing:'#db2777',hr:'#7c3aed',it:'#0ea5e9',purchase:'#ea580c',accounts:'#ca8a04'};
		function colorFor(code){ return COLORS[code] || '#475569'; }
		var state = {proc:'all', level:'department', search:'', sort:'priority', node:null};

		// ---- process selector
		var sel = document.getElementById('pf_om_proc');
		var optAll = document.createElement('option'); optAll.value='all'; optAll.textContent='All processes ('+procs.length+')'; sel.appendChild(optAll);
		procs.forEach(function(p){ var o=document.createElement('option'); o.value=String(p.id); o.textContent=p.name+' ('+(p.cases?p.cases.length:0)+')'; sel.appendChild(o); });
		sel.value='all';

		function laneNodes(p, level){
			var steps=p.steps||[], cases=p.cases||[];
			function casesInStep(no){ return cases.filter(function(c){return c.stepNo===no;}); }
			if(level==='task'){
				return steps.map(function(s){ var cs=casesInStep(s.no); return {key:'s'+s.no, title:s.name, sub:s.deptName, dept:s.dept, count:cs.length, cases:cs}; });
			}
			if(level==='location'){
				var order=['Dubai HQ','Abu Dhabi Branch','Sharjah Branch','Jebel Ali Warehouse','Al Ain Branch'];
				var byLoc={};
				cases.forEach(function(c){ var l=c.location||'Unassigned'; (byLoc[l]=byLoc[l]||[]).push(c); });
				var keys=Object.keys(byLoc).sort(function(a,b){ var ia=order.indexOf(a),ib=order.indexOf(b); if(ia<0)ia=99; if(ib<0)ib=99; return ia-ib||a.localeCompare(b); });
				return keys.map(function(l){ return {key:'l_'+l, title:l, sub:'branch', dept:'', isLoc:true, count:byLoc[l].length, cases:byLoc[l]}; });
			}
			if(level==='bu' || level==='legalentity'){
				var fld=level==='bu'?'bu':'legalEntity', sub=level==='bu'?'business unit':'legal entity';
				var by={};
				cases.forEach(function(c){ var k=c[fld]||'Unassigned'; (by[k]=by[k]||[]).push(c); });
				var keys=Object.keys(by).sort(function(a,b){ return by[b].length-by[a].length || a.localeCompare(b); });
				return keys.map(function(k){ return {key:level+'_'+k, title:k, sub:sub, dept:'', isOrg:true, count:by[k].length, cases:by[k]}; });
			}
			if(level==='overall'){
				if(!steps.length) return [];
				var last=steps[steps.length-1].no;
				var atStart=cases.filter(function(c){return c.stepNo===steps[0].no;});
				var atEnd=cases.filter(function(c){return c.stepNo===last;});
				var mid=cases.filter(function(c){return c.stepNo!==steps[0].no && c.stepNo!==last;});
				return [
					{key:'start',title:'Start',sub:steps[0].deptName,dept:steps[0].dept,count:atStart.length,cases:atStart},
					{key:'prog',title:'In progress',sub:'Being worked',dept:'',count:mid.length,cases:mid},
					{key:'fin',title:'Finish',sub:steps[last-1]?steps[last-1].deptName:'',dept:steps[steps.length-1].dept,count:atEnd.length,cases:atEnd}
				];
			}
			// department / user: collapse consecutive steps by department
			var stages=[], cur=null;
			steps.forEach(function(s){
				if(!cur || cur.dept!==s.dept){ cur={dept:s.dept,deptName:s.deptName,nos:[],names:[]}; stages.push(cur); }
				cur.nos.push(s.no); cur.names.push(s.name);
			});
			return stages.map(function(st,i){
				var cs=cases.filter(function(c){return st.nos.indexOf(c.stepNo)>=0;});
				var node={key:'d'+i+'_'+st.dept, title:st.deptName, sub:st.names.length+' step'+(st.names.length>1?'s':''), dept:st.dept, count:cs.length, cases:cs};
				if(level==='user'){
					var by={};
					cs.forEach(function(c){ var k=c.assignee||'Unassigned'; if(!by[k])by[k]={name:k,count:0,avatar:c.avatar}; by[k].count++; });
					node.emp=Object.keys(by).map(function(k){return by[k];}).sort(function(a,b){return b.count-a.count;});
				}
				return node;
			});
		}

		function selectedProcs(){ return state.proc==='all' ? procs : procs.filter(function(p){return String(p.id)===state.proc;}); }

		function renderCanvas(){
			var box=document.getElementById('pf_om_canvas'); box.innerHTML='';
			var sp=selectedProcs(), totNodes=0, totCases=0;
			if(!sp.length){ box.innerHTML='<div class="pf-om-empty-lane">No processes yet — seed sample data from the Monitor tab.</div>'; }
			sp.forEach(function(p){
				var nodes=laneNodes(p, state.level);
				var open=(p.cases||[]).length; totCases+=open;
				var lane=document.createElement('div'); lane.className='pf-om-lane';
				var h=document.createElement('h5'); h.innerHTML=esc(p.name)+'<span class="pill">'+open+' open</span>'; lane.appendChild(h);
				if(!nodes.length){ var e=document.createElement('div'); e.className='pf-om-empty-lane'; e.textContent='No steps defined.'; lane.appendChild(e); box.appendChild(lane); return; }
				var flow=document.createElement('div'); flow.className='pf-om-flow';
				nodes.forEach(function(n,i){
					totNodes++;
					var node=document.createElement('div');
					node.className='pf-om-node'+(n.count>0?' live':' empty')+((state.node && state.node.proc===p.id && state.node.key===n.key)?' sel':'');
					var col=n.isLoc?'#0891b2':(n.isOrg?(state.level==='legalentity'?'#7c3aed':'#ea580c'):(n.dept?colorFor(n.dept):'#475569'));
					var emp='';
					if(state.level==='user'){
						if(n.emp && n.emp.length){ emp='<div class="pf-om-emp">'+n.emp.map(function(e){return '<div class="r">'+av(e.name,e.avatar,20)+'<span style="flex:1;">'+esc(e.name)+'</span><b>'+e.count+'</b></div>';}).join('')+'</div>'; }
						else { emp='<div class="pf-om-emp"><span class="none">No one holding work here</span></div>'; }
					}
					node.innerHTML='<div class="pf-om-nhd" style="background:'+col+';"><span>'+esc(n.title)+'</span><span class="pf-om-count">'+n.count+'</span></div>'+
						'<div class="pf-om-nbody"><div class="pf-om-nname">'+esc(state.level==='task'?n.title:(state.level==='overall'?n.title:n.title))+'</div><div class="pf-om-nsub">'+esc(n.sub||'')+'</div>'+emp+'</div>';
					(function(nn){ node.addEventListener('click', function(){ state.node=(state.node && state.node.proc===p.id && state.node.key===nn.key)?null:{proc:p.id,key:nn.key,cases:nn.cases}; renderCanvas(); renderList(); }); })(n);
					flow.appendChild(node);
					if(i<nodes.length-1){
						var nextLive=nodes[i+1].count>0 || n.count>0;
						var c=document.createElement('div'); c.className='pf-om-conn'+(nextLive?' flow':'');
						c.innerHTML='<div class="ln"></div>'+(nextLive?'<div class="dot"></div>':'')+'<div class="hd"><i class="fa fa-caret-right"></i></div>';
						flow.appendChild(c);
					}
				});
				lane.appendChild(flow); box.appendChild(lane);
			});
			document.getElementById('pf_om_tot').innerHTML='<strong>'+totCases+'</strong> open tasks · <strong>'+totNodes+'</strong> nodes';
		}

		function activeCases(){
			var sp=selectedProcs(), list=[];
			if(state.node){
				var p=procs.filter(function(x){return x.id===state.node.proc;})[0];
				list=(state.node.cases||[]).map(function(c){ c.__p=p?p.name:''; return c; });
			} else {
				sp.forEach(function(p){ (p.cases||[]).forEach(function(c){ c.__p=p.name; list.push(c); }); });
			}
			var q=state.search.toLowerCase();
			if(q){ list=list.filter(function(c){ return (c.title+' '+c.ref+' '+c.assignee+' '+c.deptName+' '+c.location+' '+c.stepName).toLowerCase().indexOf(q)>=0; }); }
			var pr={urgent:0,high:1,normal:2,low:3};
			list.sort(function(a,b){
				if(state.sort==='priority') return (pr[a.priority]-pr[b.priority])||(b.started-a.started);
				if(state.sort==='recent') return b.started-a.started;
				if(state.sort==='dept') return (a.deptName||'').localeCompare(b.deptName||'');
				return (a.title||'').localeCompare(b.title||'');
			});
			return list;
		}
		function renderList(){
			var list=activeCases(), box=document.getElementById('pf_om_list'); box.innerHTML='';
			document.getElementById('pf_om_scount').textContent=list.length+' case'+(list.length!==1?'s':'')+(state.node?' at selected node':'')+(state.node?' · ':'')+(state.node?'(click node again to clear)':'');
			list.forEach(function(c){
				var li=document.createElement('div'); li.className='pf-om-li';
				li.innerHTML='<span class="pf-om-pri pri-'+esc(c.priority)+'"></span>'+av(c.assignee,c.avatar,30)+
					'<div style="flex:1;"><div class="t">'+esc(c.title)+'</div>'+
					'<div class="m"><i class="fa fa-map-marker"></i> '+esc(c.location||'—')+' · '+esc(c.deptName)+(c.assignee?(' · '+esc(c.assignee)):'')+'</div>'+
					'<div class="m">Step: '+esc(c.stepName||'—')+(c.overdue?' · <span class="od">OVERDUE</span>':'')+'</div></div>';
				li.addEventListener('click', function(){ window.location.href=CASE_BASE+c.id; });
				box.appendChild(li);
			});
			if(!list.length){ box.innerHTML='<div class="pf-om-empty-lane">No cases match.</div>'; }
		}
		function esc(s){ return String(s==null?'':s).replace(/[&<>"]/g,function(m){return{'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;'}[m];}); }
		function initials(n){ n=(n||'').trim().split(/\s+/); return ((n[0]||'?')[0]+(n.length>1?n[n.length-1][0]:'')).toUpperCase(); }
		function avColor(n){ var h=0; n=n||''; for(var i=0;i<n.length;i++){h=(h*31+n.charCodeAt(i))%360;} return 'hsl('+h+',55%,45%)'; }
		function av(name,url,sz){ sz=sz||22; var u=url||('https://api.dicebear.com/7.x/avataaars/svg?radius=50&seed='+encodeURIComponent(name||'staff')); return '<span class="pf-av" style="width:'+sz+'px;height:'+sz+'px;font-size:'+Math.round(sz*0.38)+'px;background:'+avColor(name)+';">'+esc(initials(name))+'<img src="'+esc(u)+'" alt="" onerror="this.remove()"></span>'; }

		document.querySelectorAll('#pf_om_level button').forEach(function(b){
			b.addEventListener('click', function(){
				document.querySelectorAll('#pf_om_level button').forEach(function(x){x.classList.remove('on');}); b.classList.add('on');
				state.level=b.getAttribute('data-lvl'); state.node=null; renderCanvas(); renderList();
			});
		});
		sel.addEventListener('change', function(){ state.proc=sel.value; state.node=null; renderCanvas(); renderList(); });
		document.getElementById('pf_om_search').addEventListener('input', function(e){ state.search=e.target.value; renderList(); });
		document.getElementById('pf_om_sort').addEventListener('change', function(e){ state.sort=e.target.value; renderList(); });
		renderCanvas(); renderList();
	})();
	</script>

<?php
/* =================== WORKFORCE (all staff in one view: busy on which task, by dept/location/task) =================== */
elseif ($pfView === 'workforce'):
	$wf = epc_pf_workforce_data($db_link);
	$wfJson = json_encode($wf, JSON_UNESCAPED_UNICODE | JSON_HEX_TAG | JSON_HEX_AMP | JSON_HEX_APOS | JSON_HEX_QUOT);
	$wfCaseBase = pf_url($tabBase, $sep, 'monitor', '&pf_case=');
?>
	<style>
	.pf-wf-bar{display:flex;flex-wrap:wrap;align-items:center;gap:8px;margin-bottom:10px;}
	.pf-wf-bar select,.pf-wf-bar input{border:1px solid #cbd5e1;border-radius:7px;padding:6px 10px;font-size:12px;}
	.pf-wf-bar .seg{display:inline-flex;border:1px solid #cbd5e1;border-radius:7px;overflow:hidden;}
	.pf-wf-bar .seg button{background:#fff;color:#334155;border:0;border-right:1px solid #e2e8f0;padding:6px 12px;font-size:12px;font-weight:600;cursor:pointer;}
	.pf-wf-bar .seg button:last-child{border-right:0;}
	.pf-wf-bar .seg button.on{background:#2563eb;color:#fff;}
	.pf-wf-kpis{display:flex;gap:10px;margin-bottom:12px;flex-wrap:wrap;}
	.pf-wf-kpi{flex:1;min-width:120px;background:#fff;border:1px solid #e5e7eb;border-radius:8px;padding:10px 12px;}
	.pf-wf-kpi .l{font-size:11px;color:#6b7280;}
	.pf-wf-kpi .v{font-size:24px;font-weight:700;}
	.pf-wf-group{margin-bottom:18px;}
	.pf-wf-ghd{display:flex;align-items:center;gap:8px;font-size:13px;font-weight:700;color:#0f172a;margin:0 0 8px;padding-bottom:6px;border-bottom:2px solid #e2e8f0;}
	.pf-wf-ghd .cnt{font-weight:600;font-size:11px;color:#64748b;}
	.pf-wf-ghd .bz{background:#fee2e2;color:#b91c1c;border-radius:10px;font-size:11px;padding:1px 8px;font-weight:600;}
	.pf-wf-grid{display:grid;grid-template-columns:repeat(auto-fill,minmax(215px,1fr));gap:8px;}
	.pf-wf-card{border:1px solid #e5e7eb;border-radius:9px;padding:9px 10px;background:#fff;}
	.pf-wf-card.busy{border-left:4px solid #dc2626;}
	.pf-wf-card.idle{border-left:4px solid #cbd5e1;}
	.pf-wf-nm{font-size:12px;font-weight:700;color:#0f172a;display:flex;justify-content:space-between;align-items:center;gap:6px;}
	.pf-wf-st{font-size:10px;font-weight:700;border-radius:9px;padding:1px 7px;white-space:nowrap;}
	.pf-wf-st.b{background:#fee2e2;color:#b91c1c;} .pf-wf-st.i{background:#f1f5f9;color:#64748b;}
	.pf-wf-meta{font-size:11px;color:#64748b;margin-top:2px;}
	.pf-wf-tasks{margin-top:6px;border-top:1px dashed #e5e7eb;padding-top:5px;}
	.pf-wf-tk{font-size:11px;color:#334155;line-height:1.3;cursor:pointer;}
	.pf-wf-tk:hover{color:#1d4ed8;}
	.pf-wf-tk .s{color:#64748b;}
	.pf-wf-tk .od{color:#dc2626;font-weight:600;}
	.pf-av{position:relative;display:inline-flex;align-items:center;justify-content:center;border-radius:50%;color:#fff;font-weight:700;overflow:hidden;flex:0 0 auto;}
	.pf-av img{position:absolute;inset:0;width:100%;height:100%;object-fit:cover;}
	</style>

	<div class="pf-wf-kpis">
		<div class="pf-wf-kpi"><div class="l"><i class="fa fa-users"></i> Total staff</div><div class="v" style="color:#2563eb;" id="wf_total">0</div></div>
		<div class="pf-wf-kpi"><div class="l"><i class="fa fa-spinner"></i> Busy now</div><div class="v" style="color:#dc2626;" id="wf_busy">0</div></div>
		<div class="pf-wf-kpi"><div class="l"><i class="fa fa-coffee"></i> Idle / available</div><div class="v" style="color:#16a34a;" id="wf_idle">0</div></div>
		<div class="pf-wf-kpi"><div class="l"><i class="fa fa-tasks"></i> Open tasks assigned</div><div class="v" style="color:#0891b2;" id="wf_tasks">0</div></div>
		<div class="pf-wf-kpi"><div class="l"><i class="fa fa-trophy"></i> Tasks completed</div><div class="v" style="color:#7c3aed;" id="wf_done">0</div></div>
		<div class="pf-wf-kpi"><div class="l"><i class="fa fa-eye"></i> Showing</div><div class="v" style="color:#334155;" id="wf_shown">0</div></div>
	</div>

	<div id="wf_leaders" style="margin-bottom:14px;"></div>

	<div class="pf-wf-bar">
		<label style="font-size:12px;color:#475569;margin:0;">Group by</label>
		<div class="seg" id="wf_group">
			<button data-g="department" class="on">Department</button>
			<button data-g="bu">Business unit</button>
			<button data-g="legalentity">Legal entity</button>
			<button data-g="location">Location</button>
			<button data-g="task">Task</button>
			<button data-g="none">Flat</button>
		</div>
		<select id="wf_dept"><option value="">All departments</option></select>
		<select id="wf_loc"><option value="">All locations</option></select>
		<select id="wf_status">
			<option value="">All staff</option>
			<option value="busy">Busy only</option>
			<option value="idle">Idle only</option>
		</select>
		<input type="text" id="wf_search" placeholder="Search name / title / task…" style="min-width:200px;">
	</div>

	<div id="wf_body"></div>

	<script>
	(function(){
		var WF = <?php echo $wfJson ?: '{"staff":[],"total":0,"busy":0,"idle":0}'; ?>;
		var CASE_BASE = <?php echo json_encode($wfCaseBase); ?>;
		var staff = WF.staff || [];
		var state = {group:'department', dept:'', loc:'', status:'', search:''};
		function esc(s){ return String(s==null?'':s).replace(/[&<>"]/g,function(m){return{'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;'}[m];}); }
		function initials(n){ n=(n||'').trim().split(/\s+/); return ((n[0]||'?')[0]+(n.length>1?n[n.length-1][0]:'')).toUpperCase(); }
		function avColor(n){ var h=0; n=n||''; for(var i=0;i<n.length;i++){h=(h*31+n.charCodeAt(i))%360;} return 'hsl('+h+',55%,45%)'; }
		function av(name,url,sz){ sz=sz||34; var u=url||('https://api.dicebear.com/7.x/avataaars/svg?radius=50&seed='+encodeURIComponent(name||'staff')); return '<span class="pf-av" style="width:'+sz+'px;height:'+sz+'px;font-size:'+Math.round(sz*0.36)+'px;background:'+avColor(name)+';">'+esc(initials(name))+'<img src="'+esc(u)+'" alt="" onerror="this.remove()"></span>'; }

		// populate filters
		var depts={}, locs={};
		staff.forEach(function(s){ if(s.deptName)depts[s.deptName]=s.dept; if(s.location)locs[s.location]=1; });
		var dsel=document.getElementById('wf_dept');
		Object.keys(depts).sort().forEach(function(n){ var o=document.createElement('option'); o.value=n; o.textContent=n; dsel.appendChild(o); });
		var lsel=document.getElementById('wf_loc');
		Object.keys(locs).sort().forEach(function(n){ var o=document.createElement('option'); o.value=n; o.textContent=n; lsel.appendChild(o); });

		var totalTasks=staff.reduce(function(a,s){return a+s.busy;},0);
		document.getElementById('wf_total').textContent=WF.total;
		document.getElementById('wf_busy').textContent=WF.busy;
		document.getElementById('wf_idle').textContent=WF.idle;
		document.getElementById('wf_tasks').textContent=totalTasks;
		document.getElementById('wf_done').textContent=(WF.doneTotal!=null?WF.doneTotal:staff.reduce(function(a,s){return a+(s.done||0);},0));

		function leaderboard(list){
			var ranked=list.filter(function(s){return (s.done||0)>0;}).sort(function(a,b){return (b.done||0)-(a.done||0);}).slice(0,10);
			var el=document.getElementById('wf_leaders');
			if(!ranked.length){ el.innerHTML=''; return; }
			var medal=['#f59e0b','#94a3b8','#b45309'];
			var rows=ranked.map(function(s,i){
				var c=i<3?medal[i]:'#cbd5e1';
				return '<div'+(s.busy>0?' data-case="'+s.tasks[0].id+'"':'')+' style="display:flex;align-items:center;gap:8px;padding:5px 8px;border-radius:7px;background:#fff;border:1px solid #eef2f7;'+(s.busy>0?'cursor:pointer;':'')+'">'+
					'<span style="width:20px;height:20px;border-radius:50%;background:'+c+';color:#fff;font-size:11px;font-weight:700;display:inline-flex;align-items:center;justify-content:center;flex:0 0 auto;">'+(i+1)+'</span>'+
					av(s.name,s.avatar,26)+
					'<div style="flex:1;min-width:0;"><div style="font-size:12px;font-weight:700;color:#0f172a;white-space:nowrap;overflow:hidden;text-overflow:ellipsis;">'+esc(s.name)+'</div><div style="font-size:10px;color:#64748b;">'+esc(s.deptName||'')+' · '+esc(s.location||'')+'</div></div>'+
					'<span style="font-size:13px;font-weight:800;color:#7c3aed;white-space:nowrap;">'+(s.done||0)+' <span style="font-size:9px;font-weight:600;color:#94a3b8;">done</span></span></div>';
			}).join('');
			el.innerHTML='<div class="pf-wf-group" style="margin-bottom:0;"><div class="pf-wf-ghd"><i class="fa fa-trophy" style="color:#f59e0b;"></i> Top performers — tasks completed <span class="cnt">(by selected filters)</span></div><div style="display:grid;grid-template-columns:repeat(auto-fill,minmax(230px,1fr));gap:6px;">'+rows+'</div></div>';
			el.querySelectorAll('[data-case]').forEach(function(c){ c.addEventListener('click', function(){ window.location.href=CASE_BASE+c.getAttribute('data-case'); }); });
		}

		function filtered(){
			var q=state.search.toLowerCase();
			return staff.filter(function(s){
				if(state.dept && s.deptName!==state.dept) return false;
				if(state.loc && s.location!==state.loc) return false;
				if(state.status==='busy' && s.busy===0) return false;
				if(state.status==='idle' && s.busy>0) return false;
				if(q){
					var hay=(s.name+' '+s.title+' '+s.deptName+' '+s.location+' '+s.tasks.map(function(t){return t.title+' '+t.step;}).join(' ')).toLowerCase();
					if(hay.indexOf(q)<0) return false;
				}
				return true;
			});
		}
		function card(s){
			var cls=s.busy>0?'busy':'idle';
			var st=s.busy>0?'<span class="pf-wf-st b">BUSY · '+s.busy+'</span>':'<span class="pf-wf-st i">IDLE</span>';
			if((s.done||0)>0){ st+=' <span class="pf-wf-st" style="background:#ede9fe;color:#6d28d9;" title="Tasks completed">'+s.done+' done</span>'; }
			var tasks='';
			if(s.busy>0){
				tasks='<div class="pf-wf-tasks">'+s.tasks.map(function(t){
					return '<div class="pf-wf-tk" data-case="'+t.id+'"><i class="fa fa-circle" style="font-size:6px;vertical-align:middle;"></i> '+esc(t.title)+' <span class="s">· '+esc(t.step||'')+'</span>'+(t.overdue?' <span class="od">!</span>':'')+'</div>';
				}).join('')+'</div>';
			}
			var clickable=s.busy>0?(' data-case="'+s.tasks[0].id+'" style="cursor:pointer;"'):'';
			return '<div class="pf-wf-card '+cls+'"'+clickable+'><div style="display:flex;gap:8px;align-items:flex-start;">'+av(s.name,s.avatar,36)+
				'<div style="flex:1;min-width:0;"><div class="pf-wf-nm"><span>'+esc(s.name)+'</span>'+st+'</div>'+
				'<div class="pf-wf-meta">'+esc(s.title||'')+'</div>'+
				'<div class="pf-wf-meta"><i class="fa fa-building-o"></i> '+esc(s.deptName)+' · <i class="fa fa-map-marker"></i> '+esc(s.location||'—')+'</div>'+
				'<div class="pf-wf-meta"><i class="fa fa-sitemap"></i> '+esc(s.bu||'—')+' · <i class="fa fa-bank"></i> '+esc(s.legalEntity||'—')+'</div></div></div>'+tasks+'</div>';
		}
		function groupKeyList(list){
			var groups={};
			if(state.group==='none'){ groups['All staff']=list.slice(); }
			else if(state.group==='task'){
				list.forEach(function(s){
					if(s.busy>0){ s.tasks.forEach(function(t){ (groups[t.title]=groups[t.title]||[]).push(s); }); }
					else { (groups['Available (idle)']=groups['Available (idle)']||[]).push(s); }
				});
			} else {
				var f=state.group==='location'?function(s){return s.location||'Unassigned';}
					:state.group==='bu'?function(s){return s.bu||'Unassigned';}
					:state.group==='legalentity'?function(s){return s.legalEntity||'Unassigned';}
					:function(s){return s.deptName;};
				list.forEach(function(s){ var k=f(s); (groups[k]=groups[k]||[]).push(s); });
			}
			return groups;
		}
		function render(){
			var list=filtered(), groups=groupKeyList(list), body=document.getElementById('wf_body');
			document.getElementById('wf_shown').textContent=list.length;
			leaderboard(list);
			var keys=Object.keys(groups).sort(function(a,b){
				if(a.indexOf('Available')>=0) return 1; if(b.indexOf('Available')>=0) return -1;
				return groups[b].length-groups[a].length || a.localeCompare(b);
			});
			body.innerHTML='';
			if(!list.length){ body.innerHTML='<div style="color:#94a3b8;padding:14px;">No staff match these filters.</div>'; return; }
			keys.forEach(function(k){
				var arr=groups[k], busy=arr.filter(function(s){return s.busy>0;}).length;
				var g=document.createElement('div'); g.className='pf-wf-group';
				g.innerHTML='<div class="pf-wf-ghd">'+esc(k)+' <span class="cnt">· '+arr.length+' staff</span>'+(busy>0?' <span class="bz">'+busy+' busy</span>':'')+'</div>'+
					'<div class="pf-wf-grid">'+arr.map(card).join('')+'</div>';
				body.appendChild(g);
			});
			body.querySelectorAll('.pf-wf-tk').forEach(function(t){ t.addEventListener('click', function(e){ e.stopPropagation(); window.location.href=CASE_BASE+t.getAttribute('data-case'); }); });
			body.querySelectorAll('.pf-wf-card[data-case]').forEach(function(c){ c.addEventListener('click', function(){ window.location.href=CASE_BASE+c.getAttribute('data-case'); }); });
		}
		document.querySelectorAll('#wf_group button').forEach(function(b){ b.addEventListener('click', function(){ document.querySelectorAll('#wf_group button').forEach(function(x){x.classList.remove('on');}); b.classList.add('on'); state.group=b.getAttribute('data-g'); render(); }); });
		dsel.addEventListener('change', function(e){ state.dept=e.target.value; render(); });
		lsel.addEventListener('change', function(e){ state.loc=e.target.value; render(); });
		document.getElementById('wf_status').addEventListener('change', function(e){ state.status=e.target.value; render(); });
		document.getElementById('wf_search').addEventListener('input', function(e){ state.search=e.target.value; render(); });
		render();
	})();
	</script>

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

	<div style="background:#eff6ff;border:1px solid #bfdbfe;border-radius:6px;padding:10px 12px;margin-bottom:12px;display:flex;align-items:center;gap:10px;flex-wrap:wrap;">
		<span style="font-size:12px;color:#1e3a8a;"><i class="fa fa-truck"></i> <strong>Customer Order → Delivery</strong> runs automatically for every order (online &amp; manual): quote → payment → procured → out for delivery → delivered → invoiced. Each completed stage is credited to the responsible employee.</span>
		<button class="btn btn-xs btn-primary" id="pf_sync_orders" style="margin-left:auto;"><i class="fa fa-refresh"></i> Track customer orders now</button>
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
	var so=document.getElementById('pf_sync_orders'); if(so) so.addEventListener('click', function(){ so.disabled=true; var o=so.innerHTML; so.innerHTML='<i class="fa fa-spinner fa-spin"></i> Tracking…'; post('pf_sync_orders', new FormData()).then(function(r){ msg(r); so.disabled=false; so.innerHTML=o; }); });
})();
</script>
