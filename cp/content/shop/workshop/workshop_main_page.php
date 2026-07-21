<?php
/**
 * CP — Professional garage / workshop desk.
 * URL: /cp/shop/workshop/workshop
 */
if (!defined('_ASTEXE_')) {
	header('Location: /cp/shop/workshop/workshop', true, 302);
	exit;
}

require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_cp_page_frame.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/workshop/epc_workshop_helpers.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/users/dp_user.php';

function epc_ws_h($v): string
{
	return htmlspecialchars((string) $v, ENT_QUOTES, 'UTF-8');
}

$backend = trim((string) ($DP_Config->backend_dir ?? 'cp'), '/');
$baseCp = '/' . $backend;
$guideUrl = $baseCp . '/control/portal/epc_autoworkshop_guide';
$erpUrl = $baseCp . '/shop/finance/erp?epc_erp_shell=1';
$ordersUrl = $baseCp . '/shop/orders/orders';
$storefront = rtrim((string) ($DP_Config->domain_path ?? ''), '/') . '/en/auto-workshop';
$pageUrl = $baseCp . '/shop/workshop/workshop';
$ajaxUrl = $baseCp . '/content/shop/workshop/ajax_workshop_endpoint.php';

$user_session = DP_User::getAdminSession();
$csrf = (string) ($user_session['csrf_guard_key'] ?? '');

$tab = (string) ($_GET['tab'] ?? 'board');
$allowed = array('board' => 'Board', 'jobs' => 'Jobs', 'checkin' => 'Check-in', 'resources' => 'Bays & techs', 'guide' => 'Guide');
if (!isset($allowed[$tab])) {
	$tab = 'board';
}

$db = $db_link;
$loadError = '';
$dash = array('open' => 0, 'in_progress' => 0, 'ready' => 0, 'delivered_today' => 0, 'revenue_open' => 0);
$jobs = array();
$bays = array();
$techs = array();
$statuses = epc_ws_statuses();

try {
	if (!($db instanceof PDO)) {
		throw new RuntimeException('No database');
	}
	epc_ws_ensure_schema($db);
	// Auto-seed once if empty so the desk is immediately usable
	$cnt = (int) $db->query('SELECT COUNT(*) FROM `epc_ws_jobs`')->fetchColumn();
	if ($cnt === 0) {
		epc_ws_seed_demo($db);
	}
	$dash = epc_ws_dashboard($db);
	$jobs = epc_ws_list_jobs($db);
	$bays = epc_ws_list_bays($db);
	$techs = epc_ws_list_techs($db);
} catch (Throwable $e) {
	$loadError = $e->getMessage();
}

$boardCols = array('checkin', 'estimate', 'approved', 'in_progress', 'qc', 'ready');
$byStatus = array();
foreach ($boardCols as $s) {
	$byStatus[$s] = array();
}
foreach ($jobs as $j) {
	$s = (string) $j['status'];
	if (isset($byStatus[$s])) {
		$byStatus[$s][] = $j;
	}
}

epc_cp_page_frame_open(array(
	'class' => 'epc-workshop-cp',
	'hero' => array(
		'badge' => 'Garage',
		'title' => 'Workshop & service desk',
		'sub' => 'Check-in → estimate → repair → QC → ready. Job cards with parts, labour, bay & technician.',
		'actions' => array(
			array('url' => $pageUrl . '?tab=checkin', 'label' => 'New check-in', 'icon' => 'fa-plus', 'primary' => true),
			array('url' => $guideUrl, 'label' => 'Operator guide', 'icon' => 'fa-book'),
			array('url' => $storefront, 'label' => 'Storefront', 'icon' => 'fa-external-link'),
			array('url' => $erpUrl, 'label' => 'Client ERP', 'icon' => 'fa-university'),
		),
	),
));
?>
<div class="epc-ws" id="epc-ws-root">
	<div class="epc-ws-hero">
		<h3>Repair workshop (garage)</h3>
		<p>Operational board for UAE garage work — vehicle intake, job cards, bay/tech assignment, parts &amp; labour, QC and handover. Demo jobs load automatically when the desk is empty.</p>
		<div class="epc-ws-hero__actions">
			<a class="btn btn-sm btn-primary" href="<?php echo epc_ws_h($pageUrl . '?tab=checkin'); ?>"><i class="fa fa-car"></i> Check-in vehicle</a>
			<button type="button" class="btn btn-sm" id="epc-ws-seed"><i class="fa fa-database"></i> Load demo data</button>
			<a class="btn btn-sm" href="<?php echo epc_ws_h($storefront); ?>" target="_blank" rel="noopener"><i class="fa fa-globe"></i> Public booking</a>
			<a class="btn btn-sm" href="<?php echo epc_ws_h($ordersUrl); ?>"><i class="fa fa-shopping-cart"></i> Parts orders</a>
		</div>
	</div>

	<div id="epc-ws-msg" class="alert epc-ws-msg"></div>
	<?php if ($loadError !== '') { ?>
		<div class="alert alert-danger"><?php echo epc_ws_h($loadError); ?></div>
	<?php } ?>

	<div class="epc-ws-kpis">
		<div class="epc-ws-kpi"><div class="lbl">Open jobs</div><div class="val"><?php echo (int) $dash['open']; ?></div></div>
		<div class="epc-ws-kpi"><div class="lbl">In progress / QC</div><div class="val"><?php echo (int) $dash['in_progress']; ?></div></div>
		<div class="epc-ws-kpi"><div class="lbl">Ready for collection</div><div class="val"><?php echo (int) $dash['ready']; ?></div></div>
		<div class="epc-ws-kpi"><div class="lbl">Delivered today</div><div class="val"><?php echo (int) $dash['delivered_today']; ?></div></div>
		<div class="epc-ws-kpi"><div class="lbl">Open job value</div><div class="val" style="font-size:16px">AED <?php echo number_format((float) $dash['revenue_open'], 0); ?></div></div>
	</div>

	<nav class="epc-ws-tabs">
		<?php foreach ($allowed as $k => $lbl) { ?>
			<a class="<?php echo $tab === $k ? 'is-active' : ''; ?>" href="<?php echo epc_ws_h($pageUrl . '?tab=' . $k); ?>"><?php echo epc_ws_h($lbl); ?></a>
		<?php } ?>
	</nav>

	<?php if ($tab === 'board') { ?>
		<div class="epc-ws-guide">
			<h4>Floor board</h4>
			<ol>
				<li>Click a job card to change status, bay, technician, or add parts/labour.</li>
				<li>Move jobs: Check-in → Estimate → Approved → In progress → QC → Ready.</li>
				<li>Customers can book / track on <code>/auto-workshop</code>.</li>
			</ol>
		</div>
		<div class="epc-ws-board">
			<?php foreach ($boardCols as $col) { ?>
				<div class="epc-ws-col">
					<div class="epc-ws-col__title"><?php echo epc_ws_h($statuses[$col] ?? $col); ?> (<?php echo count($byStatus[$col]); ?>)</div>
					<?php foreach ($byStatus[$col] as $j) { ?>
						<div class="epc-ws-card" data-open-job="1" data-job-id="<?php echo (int) $j['id']; ?>">
							<div class="epc-ws-card__no"><?php echo epc_ws_h($j['job_no']); ?></div>
							<div class="epc-ws-card__meta">
								<?php echo epc_ws_h($j['plate']); ?> · <?php echo epc_ws_h(trim($j['make'] . ' ' . $j['model'])); ?><br>
								<?php echo epc_ws_h($j['customer_name']); ?>
								<?php if (!empty($j['tech_name'])) { ?> · <?php echo epc_ws_h($j['tech_name']); ?><?php } ?>
							</div>
							<div class="epc-ws-card__amt">AED <?php echo number_format((float) $j['grand_total'], 2); ?></div>
						</div>
					<?php } ?>
					<?php if (!$byStatus[$col]) { ?>
						<div class="text-muted" style="font-size:11px;padding:8px">Empty</div>
					<?php } ?>
				</div>
			<?php } ?>
		</div>
	<?php } ?>

	<?php if ($tab === 'jobs') { ?>
		<div class="epc-ws-panel">
			<h4>All job cards</h4>
			<div class="table-responsive">
				<table class="table table-hover epc-ws-table">
					<thead>
						<tr>
							<th>Job</th><th>Vehicle</th><th>Customer</th><th>Status</th><th>Bay / Tech</th><th>Total</th><th></th>
						</tr>
					</thead>
					<tbody>
					<?php foreach ($jobs as $j) {
						$pill = 'epc-ws-pill';
						if ($j['status'] === 'ready') $pill .= ' is-ready';
						elseif (in_array($j['status'], array('in_progress', 'qc'), true)) $pill .= ' is-progress';
						elseif (in_array($j['status'], array('delivered', 'cancelled'), true)) $pill .= ' is-done';
						?>
						<tr>
							<td><strong><?php echo epc_ws_h($j['job_no']); ?></strong></td>
							<td><?php echo epc_ws_h($j['plate']); ?><br><span class="text-muted"><?php echo epc_ws_h(trim($j['make'] . ' ' . $j['model'] . ' ' . $j['year'])); ?></span></td>
							<td><?php echo epc_ws_h($j['customer_name']); ?><br><span class="text-muted"><?php echo epc_ws_h($j['customer_phone']); ?></span></td>
							<td><span class="<?php echo $pill; ?>"><?php echo epc_ws_h($statuses[$j['status']] ?? $j['status']); ?></span></td>
							<td><?php echo epc_ws_h($j['bay_name'] ?? '—'); ?><br><span class="text-muted"><?php echo epc_ws_h($j['tech_name'] ?? '—'); ?></span></td>
							<td>AED <?php echo number_format((float) $j['grand_total'], 2); ?></td>
							<td><button type="button" class="btn btn-xs btn-default" data-open-job="1" data-job-id="<?php echo (int) $j['id']; ?>">Open</button></td>
						</tr>
					<?php } ?>
					<?php if (!$jobs) { ?>
						<tr><td colspan="7" class="text-muted">No jobs yet — use Check-in or Load demo data.</td></tr>
					<?php } ?>
					</tbody>
				</table>
			</div>
		</div>
	<?php } ?>

	<?php if ($tab === 'checkin') { ?>
		<div class="epc-ws-guide">
			<h4>Vehicle check-in</h4>
			<ol>
				<li>Capture plate, customer contact, and complaint first.</li>
				<li>Optional: VIN, make/model, odometer, first labour/part line.</li>
				<li>Job opens on the board in <strong>Check-in</strong> — assign bay/tech next.</li>
			</ol>
		</div>
		<div class="epc-ws-panel">
			<h4>New job card</h4>
			<form id="epc-ws-checkin-form" class="epc-ws-form form-horizontal">
				<div class="row">
					<div class="col-md-6">
						<div class="form-group"><label>Customer name *</label><input class="form-control" name="customer_name" required></div>
						<div class="form-group"><label>Phone *</label><input class="form-control" name="customer_phone" required placeholder="+971…"></div>
						<div class="form-group"><label>E-mail</label><input class="form-control" name="customer_email" type="email"></div>
						<div class="form-group"><label>Complaint / work requested *</label><textarea class="form-control" name="complaint" rows="3" required></textarea></div>
					</div>
					<div class="col-md-6">
						<div class="form-group"><label>Plate *</label><input class="form-control" name="plate" required placeholder="D-12345"></div>
						<div class="form-group"><label>VIN</label><input class="form-control" name="vin"></div>
						<div class="row">
							<div class="col-xs-4"><div class="form-group"><label>Make</label><input class="form-control" name="make"></div></div>
							<div class="col-xs-4"><div class="form-group"><label>Model</label><input class="form-control" name="model"></div></div>
							<div class="col-xs-4"><div class="form-group"><label>Year</label><input class="form-control" name="year"></div></div>
						</div>
						<div class="form-group"><label>Odometer (km)</label><input class="form-control" name="odometer" type="number" min="0"></div>
						<div class="row">
							<div class="col-xs-6">
								<div class="form-group"><label>Bay</label>
									<select class="form-control" name="bay_id"><option value="0">—</option>
									<?php foreach ($bays as $b) { if (!(int)$b['active']) continue; ?>
										<option value="<?php echo (int)$b['id']; ?>"><?php echo epc_ws_h($b['code'] . ' — ' . $b['name']); ?></option>
									<?php } ?>
									</select>
								</div>
							</div>
							<div class="col-xs-6">
								<div class="form-group"><label>Technician</label>
									<select class="form-control" name="tech_id"><option value="0">—</option>
									<?php foreach ($techs as $t) { if (!(int)$t['active']) continue; ?>
										<option value="<?php echo (int)$t['id']; ?>"><?php echo epc_ws_h($t['name']); ?></option>
									<?php } ?>
									</select>
								</div>
							</div>
						</div>
					</div>
				</div>
				<hr>
				<p class="text-muted" style="font-size:12px">Optional first lines (you can add more later from the job card).</p>
				<div class="row">
					<div class="col-md-6">
						<div class="form-group"><label>Labour description</label><input class="form-control" name="labour_desc" placeholder="e.g. Brake service"></div>
						<div class="row">
							<div class="col-xs-6"><div class="form-group"><label>Hours</label><input class="form-control" name="labour_hours" value="1"></div></div>
							<div class="col-xs-6"><div class="form-group"><label>Rate (AED)</label><input class="form-control" name="labour_rate" value="150"></div></div>
						</div>
					</div>
					<div class="col-md-6">
						<div class="form-group"><label>Part description</label><input class="form-control" name="part_desc" placeholder="e.g. Oil filter"></div>
						<div class="row">
							<div class="col-xs-6"><div class="form-group"><label>Qty</label><input class="form-control" name="part_qty" value="1"></div></div>
							<div class="col-xs-6"><div class="form-group"><label>Price (AED)</label><input class="form-control" name="part_price" value="0"></div></div>
						</div>
					</div>
				</div>
				<button type="submit" class="btn btn-primary"><i class="fa fa-save"></i> Create job card</button>
			</form>
		</div>
	<?php } ?>

	<?php if ($tab === 'resources') { ?>
		<div class="row">
			<div class="col-md-6">
				<div class="epc-ws-panel">
					<h4>Bays / ramps</h4>
					<table class="table epc-ws-table">
						<thead><tr><th>Code</th><th>Name</th><th>Active</th></tr></thead>
						<tbody>
						<?php foreach ($bays as $b) { ?>
							<tr><td><?php echo epc_ws_h($b['code']); ?></td><td><?php echo epc_ws_h($b['name']); ?></td><td><?php echo (int)$b['active'] ? 'Yes' : 'No'; ?></td></tr>
						<?php } ?>
						</tbody>
					</table>
					<p class="text-muted" style="font-size:12px;margin:0">Seeded with Bay 1–3. Add more via demo seed or DB as needed.</p>
				</div>
			</div>
			<div class="col-md-6">
				<div class="epc-ws-panel">
					<h4>Technicians</h4>
					<table class="table epc-ws-table">
						<thead><tr><th>Name</th><th>Skill</th><th>Phone</th></tr></thead>
						<tbody>
						<?php foreach ($techs as $t) { ?>
							<tr><td><?php echo epc_ws_h($t['name']); ?></td><td><?php echo epc_ws_h($t['skill']); ?></td><td><?php echo epc_ws_h($t['phone']); ?></td></tr>
						<?php } ?>
						</tbody>
					</table>
				</div>
			</div>
		</div>
	<?php } ?>

	<?php if ($tab === 'guide') { ?>
		<div class="epc-ws-panel">
			<h4>How this garage desk works</h4>
			<ol style="line-height:1.6">
				<li><strong>Check-in</strong> — create a job with plate, customer, complaint.</li>
				<li><strong>Estimate</strong> — add labour hours × rate and parts; send/approve estimate.</li>
				<li><strong>Repair</strong> — assign bay + technician; set status In progress.</li>
				<li><strong>QC</strong> — quality check before marking Ready.</li>
				<li><strong>Handover</strong> — Ready → Delivered; invoice value is on the job card (link Client ERP for formal invoice).</li>
			</ol>
			<p><a class="btn btn-default" href="<?php echo epc_ws_h($guideUrl); ?>">Open full operator guide</a>
			<a class="btn btn-default" href="<?php echo epc_ws_h($storefront); ?>" target="_blank" rel="noopener">Public Auto Workshop page</a></p>
		</div>
	<?php } ?>
</div>

<div class="epc-ws-detail" id="epc-ws-detail"><div class="epc-ws-detail__box" id="epc-ws-detail-body"></div></div>

<script>
window.EPC_WORKSHOP = <?php echo json_encode(array(
	'ajaxUrl' => $ajaxUrl,
	'csrf' => $csrf,
	'boardUrl' => $pageUrl . '?tab=board',
	'currency' => 'AED',
	'statuses' => $statuses,
	'bays' => array_map(static function ($b) {
		return array('id' => (int) $b['id'], 'code' => $b['code'], 'name' => $b['name']);
	}, $bays),
	'techs' => array_map(static function ($t) {
		return array('id' => (int) $t['id'], 'name' => $t['name']);
	}, $techs),
), JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES); ?>;
</script>
<?php
epc_cp_page_frame_close();
?>
