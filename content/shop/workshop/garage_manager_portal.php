<?php
/**
 * Storefront — Garage Manager portal (staff).
 * URL: /en/garage/manager
 * End-to-end GMS: board, appointments, check-in, job actions.
 */
defined('_ASTEXE_') or die('No access');

require_once $_SERVER['DOCUMENT_ROOT'] . '/content/users/dp_user.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/workshop/epc_workshop_helpers.php';

$lang = isset($multilang_params['lang_href']) ? rtrim((string)$multilang_params['lang_href'], '/') : '/en';
$loginUrl = $lang . '/garage/login';
$cpWorkshop = '/' . (isset($DP_Config->backend_dir) ? $DP_Config->backend_dir : 'cp') . '/shop/workshop/workshop';
$ajaxUrl = '/content/shop/workshop/ajax_garage_manager.php';

if (!epc_ws_staff_ok()) {
	echo '<div class="col-lg-12"><div class="alert alert-warning" style="margin:24px auto;max-width:640px">'
		. '<strong>Garage staff access required.</strong> '
		. '<a href="' . htmlspecialchars($loginUrl, ENT_QUOTES, 'UTF-8') . '">Garage login</a> '
		. 'or open the <a href="' . htmlspecialchars($cpWorkshop, ENT_QUOTES, 'UTF-8') . '">CP workshop desk</a>.'
		. '</div></div>';
	return;
}

if (!isset($db_link) || !($db_link instanceof PDO)) {
	echo '<div class="alert alert-danger">Database unavailable.</div>';
	return;
}

$dash = array('open' => 0, 'in_progress' => 0, 'ready' => 0, 'delivered_today' => 0, 'revenue_open' => 0);
$jobs = array();
$appts = array();
$bays = array();
$techs = array();
$labour = array();
$statuses = epc_ws_statuses();
$err = '';
try {
	epc_ws_ensure_schema($db_link);
	if ((int)$db_link->query('SELECT COUNT(*) FROM `epc_ws_jobs`')->fetchColumn() === 0) {
		epc_ws_seed_demo($db_link);
	}
	epc_ws_seed_labour_ops($db_link);
	$dash = epc_ws_dashboard($db_link);
	$jobs = epc_ws_list_jobs($db_link);
	$appts = epc_ws_list_appointments($db_link, time() - 86400, time() + 14 * 86400, 40);
	$bays = epc_ws_list_bays($db_link, true);
	$techs = epc_ws_list_techs($db_link, true);
	$labour = epc_ws_list_labour_ops($db_link, true);
} catch (Throwable $e) {
	$err = $e->getMessage();
}

$boardCols = array('checkin', 'estimate', 'approved', 'in_progress', 'qc', 'ready');
$byStatus = array();
foreach ($boardCols as $s) {
	$byStatus[$s] = array();
}
foreach ($jobs as $j) {
	$s = (string)$j['status'];
	if (isset($byStatus[$s])) {
		$byStatus[$s][] = $j;
	}
}

$adminSession = DP_User::getAdminSession();
$csrf = is_array($adminSession) ? (string)($adminSession['csrf_guard_key'] ?? '') : '';
$h = static function ($v) {
	return htmlspecialchars((string)$v, ENT_QUOTES, 'UTF-8');
};
?>
<link rel="stylesheet" href="/content/general_pages/epc_gms_portal.css?v=<?php echo (int)(@filemtime($_SERVER['DOCUMENT_ROOT'].'/content/general_pages/epc_gms_portal.css') ?: time()); ?>">

<div class="epc-gms" id="epc-gms-root"
	data-ajax="<?php echo $h($ajaxUrl); ?>"
	data-csrf="<?php echo $h($csrf); ?>">
	<header class="epc-gms-brand">
		<div class="epc-gms-brand__mark"><i class="fa fa-wrench"></i></div>
		<div>
			<div class="epc-gms-brand__name">Garage Manager</div>
			<div class="epc-gms-brand__sub">Complete workshop system — appointments → check-in → estimate → repair → QC → ready → deliver.</div>
		</div>
		<div class="epc-gms-brand__actions">
			<a class="epc-gms-chip" href="<?php echo $h($cpWorkshop); ?>"><i class="fa fa-desktop"></i> CP desk</a>
			<a class="epc-gms-chip" href="<?php echo $h($lang . '/auto-workshop'); ?>"><i class="fa fa-globe"></i> Public book</a>
			<a class="epc-gms-chip" href="<?php echo $h($lang . '/garazh'); ?>"><i class="fa fa-car"></i> Customer garage</a>
		</div>
	</header>

	<?php if ($err !== ''): ?><div class="alert alert-danger"><?php echo $h($err); ?></div><?php endif; ?>
	<div id="epc-gms-msg" class="alert epc-gms-msg" style="display:none"></div>

	<div class="epc-gms-kpis">
		<div class="epc-gms-kpi"><span>Open</span><strong><?php echo (int)$dash['open']; ?></strong></div>
		<div class="epc-gms-kpi"><span>In progress</span><strong><?php echo (int)$dash['in_progress']; ?></strong></div>
		<div class="epc-gms-kpi"><span>Ready</span><strong><?php echo (int)$dash['ready']; ?></strong></div>
		<div class="epc-gms-kpi"><span>Delivered today</span><strong><?php echo (int)$dash['delivered_today']; ?></strong></div>
		<div class="epc-gms-kpi"><span>Open value</span><strong>AED <?php echo number_format((float)$dash['revenue_open'], 0); ?></strong></div>
	</div>

	<nav class="epc-gms-tabs">
		<button type="button" class="is-on" data-gms-tab="board">Board</button>
		<button type="button" data-gms-tab="schedule">Schedule</button>
		<button type="button" data-gms-tab="checkin">Check-in</button>
		<button type="button" data-gms-tab="jobs">Jobs</button>
		<button type="button" data-gms-tab="resources">Bays &amp; labour</button>
	</nav>

	<section class="epc-gms-panel is-on" data-gms-panel="board">
		<div class="epc-gms-board">
			<?php foreach ($boardCols as $col): ?>
				<div class="epc-gms-col">
					<div class="epc-gms-col__head"><?php echo $h($statuses[$col] ?? $col); ?> <em><?php echo count($byStatus[$col]); ?></em></div>
					<?php foreach ($byStatus[$col] as $j): ?>
						<article class="epc-gms-card" data-job="<?php echo (int)$j['id']; ?>">
							<strong><?php echo $h($j['job_no']); ?></strong>
							<div><?php echo $h($j['plate']); ?> · <?php echo $h(trim($j['make'] . ' ' . $j['model'])); ?></div>
							<div class="muted"><?php echo $h($j['customer_name']); ?></div>
							<div class="muted"><?php echo $h($j['bay_name'] ?: 'No bay'); ?> · <?php echo $h($j['tech_name'] ?: 'Unassigned'); ?></div>
							<div class="epc-gms-card__foot">
								<span>AED <?php echo number_format((float)$j['grand_total'], 2); ?></span>
								<select class="epc-gms-status" data-job-status="<?php echo (int)$j['id']; ?>">
									<?php foreach ($statuses as $sk => $sl): if ($sk === 'cancelled') continue; ?>
										<option value="<?php echo $h($sk); ?>" <?php echo $j['status'] === $sk ? 'selected' : ''; ?>><?php echo $h($sl); ?></option>
									<?php endforeach; ?>
								</select>
							</div>
						</article>
					<?php endforeach; ?>
					<?php if (empty($byStatus[$col])): ?><div class="epc-gms-empty">No jobs</div><?php endif; ?>
				</div>
			<?php endforeach; ?>
		</div>
	</section>

	<section class="epc-gms-panel" data-gms-panel="schedule">
		<div class="epc-gms-section">
			<div class="epc-gms-section__head">
				<h3>Appointments (next 14 days)</h3>
				<button type="button" class="btn btn-sm btn-primary" id="epc-gms-appt-refresh">Refresh</button>
			</div>
			<table class="epc-gms-table">
				<thead><tr><th>When</th><th>Ref</th><th>Customer</th><th>Vehicle</th><th>Service</th><th>Status</th><th></th></tr></thead>
				<tbody id="epc-gms-appt-body">
				<?php foreach ($appts as $a): ?>
					<tr>
						<td><?php echo $h(date('D d M H:i', (int)$a['time_slot'])); ?></td>
						<td><code><?php echo $h($a['ref_no']); ?></code></td>
						<td><?php echo $h($a['customer_name']); ?><div class="muted"><?php echo $h($a['customer_phone']); ?></div></td>
						<td><?php echo $h($a['plate']); ?> <?php echo $h(trim($a['make'].' '.$a['model'])); ?></td>
						<td><?php echo $h($a['service_type']); ?></td>
						<td><?php echo $h($a['status']); ?></td>
						<td>
							<?php if ((int)$a['job_id'] <= 0 && $a['status'] !== 'cancelled'): ?>
								<button type="button" class="btn btn-xs btn-success" data-convert-appt="<?php echo (int)$a['id']; ?>">Check-in</button>
							<?php elseif ((int)$a['job_id'] > 0): ?>
								<span class="muted">Job #<?php echo (int)$a['job_id']; ?></span>
							<?php endif; ?>
						</td>
					</tr>
				<?php endforeach; ?>
				<?php if (empty($appts)): ?><tr><td colspan="7" class="muted">No appointments — customers book on Auto Workshop or create below.</td></tr><?php endif; ?>
				</tbody>
			</table>
		</div>
		<div class="epc-gms-section">
			<div class="epc-gms-section__head"><h3>New appointment</h3></div>
			<form id="epc-gms-appt-form" class="epc-gms-form">
				<input name="customer_name" placeholder="Customer name *" required>
				<input name="customer_phone" placeholder="Phone *" required>
				<input name="plate" placeholder="Plate *" required>
				<input name="make" placeholder="Make">
				<input name="model" placeholder="Model">
				<input name="service_type" placeholder="Service type" value="General service">
				<input name="time_slot_local" type="datetime-local" required>
				<input name="notes" placeholder="Notes">
				<button type="submit" class="btn btn-primary">Schedule</button>
			</form>
		</div>
	</section>

	<section class="epc-gms-panel" data-gms-panel="checkin">
		<div class="epc-gms-section">
			<div class="epc-gms-section__head"><h3>Vehicle check-in → job card</h3></div>
			<form id="epc-gms-checkin-form" class="epc-gms-form epc-gms-form--wide">
				<input name="customer_name" placeholder="Customer name *" required>
				<input name="customer_phone" placeholder="Phone *" required>
				<input name="customer_email" placeholder="Email" type="email">
				<input name="plate" placeholder="Plate *" required>
				<input name="vin" placeholder="VIN">
				<input name="make" placeholder="Make">
				<input name="model" placeholder="Model">
				<input name="year" placeholder="Year">
				<input name="odometer" type="number" min="0" placeholder="Odometer km">
				<select name="bay_id">
					<option value="0">Bay (optional)</option>
					<?php foreach ($bays as $b): ?><option value="<?php echo (int)$b['id']; ?>"><?php echo $h($b['name']); ?></option><?php endforeach; ?>
				</select>
				<select name="tech_id">
					<option value="0">Technician (optional)</option>
					<?php foreach ($techs as $t): ?><option value="<?php echo (int)$t['id']; ?>"><?php echo $h($t['name']); ?></option><?php endforeach; ?>
				</select>
				<textarea name="complaint" rows="3" placeholder="Customer complaint / work required *" required></textarea>
				<select name="labour_code">
					<option value="">Add labour op (optional)</option>
					<?php foreach ($labour as $op): ?>
						<option value="<?php echo $h($op['code']); ?>" data-hours="<?php echo $h($op['hours']); ?>" data-rate="<?php echo $h($op['rate']); ?>" data-name="<?php echo $h($op['name']); ?>">
							<?php echo $h($op['name'] . ' · ' . $op['hours'] . 'h @ ' . $op['rate']); ?>
						</option>
					<?php endforeach; ?>
				</select>
				<button type="submit" class="btn btn-primary"><i class="fa fa-plus"></i> Create job card</button>
			</form>
		</div>
	</section>

	<section class="epc-gms-panel" data-gms-panel="jobs">
		<div class="epc-gms-section">
			<div class="epc-gms-section__head"><h3>All open &amp; recent jobs</h3></div>
			<table class="epc-gms-table">
				<thead><tr><th>Job</th><th>Vehicle</th><th>Customer</th><th>Status</th><th>Bay / Tech</th><th>Total</th></tr></thead>
				<tbody>
				<?php foreach (array_slice($jobs, 0, 40) as $j): ?>
					<tr>
						<td><strong><?php echo $h($j['job_no']); ?></strong></td>
						<td><?php echo $h($j['plate']); ?><div class="muted"><?php echo $h(trim($j['make'].' '.$j['model'])); ?></div></td>
						<td><?php echo $h($j['customer_name']); ?></td>
						<td><?php echo $h($statuses[$j['status']] ?? $j['status']); ?></td>
						<td><?php echo $h(($j['bay_name'] ?: '—') . ' / ' . ($j['tech_name'] ?: '—')); ?></td>
						<td>AED <?php echo number_format((float)$j['grand_total'], 2); ?></td>
					</tr>
				<?php endforeach; ?>
				</tbody>
			</table>
		</div>
	</section>

	<section class="epc-gms-panel" data-gms-panel="resources">
		<div class="epc-gms-two">
			<div class="epc-gms-section">
				<div class="epc-gms-section__head"><h3>Bays</h3></div>
				<ul class="epc-gms-list">
					<?php foreach ($bays as $b): ?><li><strong><?php echo $h($b['code']); ?></strong> — <?php echo $h($b['name']); ?></li><?php endforeach; ?>
				</ul>
			</div>
			<div class="epc-gms-section">
				<div class="epc-gms-section__head"><h3>Technicians</h3></div>
				<ul class="epc-gms-list">
					<?php foreach ($techs as $t): ?><li><strong><?php echo $h($t['name']); ?></strong> — <?php echo $h($t['skill']); ?></li><?php endforeach; ?>
				</ul>
			</div>
			<div class="epc-gms-section">
				<div class="epc-gms-section__head"><h3>Labour catalogue</h3></div>
				<ul class="epc-gms-list">
					<?php foreach ($labour as $op): ?>
						<li><strong><?php echo $h($op['code']); ?></strong> <?php echo $h($op['name']); ?> — <?php echo $h($op['hours']); ?>h @ AED <?php echo number_format((float)$op['rate'], 0); ?></li>
					<?php endforeach; ?>
				</ul>
			</div>
		</div>
	</section>
</div>
<script src="/content/shop/workshop/garage_manager.js?v=<?php echo (int)(@filemtime($_SERVER['DOCUMENT_ROOT'].'/content/shop/workshop/garage_manager.js') ?: time()); ?>"></script>
