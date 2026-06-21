<?php
/**
 * CP — Payment gateways hub (UAE + legacy).
 */
defined('_ASTEXE_') or die('No access');

require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/payments/epc_payment_helpers.php';

if ($_SERVER['REQUEST_METHOD'] === 'POST' && !empty($_POST['action'])) {
	require __DIR__ . '/ajax_payments.php';
	exit;
}

$tab = isset($_GET['tab']) ? preg_replace('/[^a-z_]/', '', (string)$_GET['tab']) : 'dashboard';
if ($tab === '') {
	$tab = 'dashboard';
}

$report = epc_payment_demo_report($db_link);
$gateways = $report['gateways'];
$uaeList = array();
$legacyList = array();
foreach ($gateways as $g) {
	if (epc_payment_is_uae($g['handler'])) {
		$uaeList[] = $g;
	} else {
		$legacyList[] = $g;
	}
}

$paymentsUrl = '/' . $DP_Config->backend_dir . '/shop/payments/payments';
$guideUrl = '/' . $DP_Config->backend_dir . '/shop/payments/payments/guide';
$csrf = isset($user_session['csrf_guard_key']) ? (string)$user_session['csrf_guard_key'] : '';
$domain = rtrim($DP_Config->domain_path, '/');
?>
<link rel="stylesheet" href="/content/shop/finance/epc_erp_ui.css?v=20260525">
<style>
.epc-pay-kpi { display:flex; flex-wrap:wrap; gap:12px; margin:0 0 18px; }
.epc-pay-kpi .kpi { flex:1 1 130px; min-width:110px; background:#f8fafc; border:1px solid #e2e8f0; border-radius:8px; padding:12px; }
.epc-pay-kpi .lbl { font-size:11px; color:#64748b; text-transform:uppercase; }
.epc-pay-kpi .val { font-size:20px; font-weight:700; color:#7c3aed; }
.epc-pay-tabs { margin-bottom:16px; }
.epc-pay-tabs a { margin-right:8px; }
.epc-pay-tabs a.active { font-weight:700; }
.badge-uae { background:#dbeafe; color:#1e40af; }
.badge-legacy { background:#f3f4f6; color:#374151; }
.badge-active { background:#dcfce7; color:#166534; }
</style>

<div class="col-lg-12 epc-erp-shell">
	<div class="hpanel">
		<div class="panel-heading hbuilt">
			<i class="fa fa-credit-card"></i> Payment gateways
			<span class="pull-right">
				<a class="btn btn-primary btn-xs" href="<?php echo epc_payment_h($guideUrl); ?>"><i class="fa fa-book"></i> Guide</a>
				<a class="btn btn-default btn-xs" href="<?php echo epc_payment_h($paymentsUrl . '?tab=configure'); ?>"><i class="fa fa-cog"></i> Activate &amp; configure</a>
			</span>
		</div>
		<div class="panel-body">

			<div class="alert alert-info">
				<strong>Dummy credentials loaded.</strong> All UAE gateways run in <em>demo mode</em> until you replace API keys.
				Active checkout gateway: <strong><?php echo epc_payment_h($report['active_name'] ?: 'None — select one in Configure'); ?></strong>.
			</div>

			<div class="epc-pay-tabs">
				<a class="btn btn-sm btn-<?php echo $tab === 'dashboard' ? 'primary' : 'default'; ?>" href="<?php echo epc_payment_h($paymentsUrl . '?tab=dashboard'); ?>">Dashboard</a>
				<a class="btn btn-sm btn-<?php echo $tab === 'configure' ? 'primary' : 'default'; ?>" href="<?php echo epc_payment_h($paymentsUrl . '?tab=configure'); ?>">Configure</a>
				<a class="btn btn-sm btn-<?php echo $tab === 'legacy' ? 'primary' : 'default'; ?>" href="<?php echo epc_payment_h($paymentsUrl . '?tab=legacy'); ?>">Legacy (CIS)</a>
			</div>

			<div id="epc_pay_msg" class="alert" style="display:none;"></div>

			<?php if ($tab === 'dashboard'): ?>
			<div class="epc-pay-kpi">
				<div class="kpi"><div class="lbl">Total gateways</div><div class="val"><?php echo (int)$report['total']; ?></div></div>
				<div class="kpi"><div class="lbl">UAE / MENA</div><div class="val"><?php echo (int)$report['uae_gateways']; ?></div></div>
				<div class="kpi"><div class="lbl">Legacy CIS</div><div class="val"><?php echo (int)$report['legacy_gateways']; ?></div></div>
				<div class="kpi"><div class="lbl">Active</div><div class="val" style="font-size:14px;padding-top:4px;"><?php echo epc_payment_h($report['active_handler'] ?: '—'); ?></div></div>
			</div>

			<p>
				<button type="button" class="btn btn-success btn-sm" id="epc_btn_seed"><i class="fa fa-database"></i> Refresh dummy credentials</button>
				<button type="button" class="btn btn-primary btn-sm" id="epc_btn_activate_stripe"><i class="fa fa-check"></i> Activate Stripe (demo)</button>
				<a class="btn btn-default btn-sm" href="/epc-payments-demo.php?token=epartscart-deploy-2026" target="_blank"><i class="fa fa-external-link"></i> JSON report</a>
			</p>

			<h4>UAE &amp; international gateways</h4>
			<table class="table table-bordered table-condensed">
				<thead><tr><th>Gateway</th><th>Handler</th><th>Mode</th><th>Webhook URL</th><th></th></tr></thead>
				<tbody>
				<?php foreach ($uaeList as $g):
					$vals = json_decode((string)$g['parameters_values'], true);
					$demo = !empty($vals['demo_mode']);
				?>
					<tr>
						<td><strong><?php echo epc_payment_h(epc_payment_gateway_label($g)); ?></strong></td>
						<td><code><?php echo epc_payment_h($g['handler']); ?></code></td>
						<td><?php echo $demo ? '<span class="label badge-uae">Demo</span>' : '<span class="label label-warning">Live keys</span>'; ?>
							<?php if ((int)$g['active']): ?> <span class="label badge-active">Active</span><?php endif; ?></td>
						<td><small><?php echo epc_payment_h($domain . '/content/shop/finance/payment_systems/' . $g['handler'] . '/notification.php'); ?></small></td>
						<td><button type="button" class="btn btn-xs btn-default epc-activate-gw" data-handler="<?php echo epc_payment_h($g['handler']); ?>">Activate</button></td>
					</tr>
				<?php endforeach; ?>
				</tbody>
			</table>
			<?php endif; ?>

			<?php if ($tab === 'legacy'): ?>
			<h4>Legacy payment systems (from Shop → Finance)</h4>
			<p class="text-muted">Russian/CIS acquirers already in DocPart CMS — dummy values filled where empty. Configure and activate like UAE gateways.</p>
			<table class="table table-striped table-condensed">
				<thead><tr><th>Name</th><th>Handler</th><th>Enabled</th><th>Active</th></tr></thead>
				<tbody>
				<?php foreach ($legacyList as $g): ?>
					<tr>
						<td><?php echo epc_payment_h(epc_payment_gateway_label($g)); ?></td>
						<td><code><?php echo epc_payment_h($g['handler']); ?></code></td>
						<td><?php echo (int)$g['anable'] ? 'Yes' : 'No'; ?></td>
						<td><?php echo (int)$g['active'] ? '<strong>Active</strong>' : '—'; ?></td>
					</tr>
				<?php endforeach; ?>
				</tbody>
			</table>
			<?php endif; ?>

			<?php if ($tab === 'configure'): ?>
			<?php require __DIR__ . '/payments_configure.php'; ?>
			<?php endif; ?>

		</div>
	</div>
</div>

<script>
(function(){
	var url = <?php echo json_encode($paymentsUrl); ?>;
	var csrf = <?php echo json_encode($csrf); ?>;
	function post(action, extra) {
		var fd = new FormData();
		fd.append('action', action);
		fd.append('csrf_guard_key', csrf);
		if (extra) Object.keys(extra).forEach(function(k){ fd.append(k, extra[k]); });
		return fetch(url, { method:'POST', body:fd, credentials:'same-origin' }).then(function(r){ return r.json(); });
	}
	function msg(j) {
		var el = document.getElementById('epc_pay_msg');
		if (!el) return;
		el.className = 'alert alert-' + (j.status ? 'success' : 'danger');
		el.textContent = j.message || '';
		el.style.display = 'block';
	}
	var seed = document.getElementById('epc_btn_seed');
	if (seed) seed.addEventListener('click', function(){ post('seed_dummy').then(function(j){ msg(j); if (j.status) setTimeout(function(){ location.reload(); }, 700); }); });
	var stripe = document.getElementById('epc_btn_activate_stripe');
	if (stripe) stripe.addEventListener('click', function(){ post('activate', { handler: 'stripe' }).then(function(j){ msg(j); if (j.status) setTimeout(function(){ location.reload(); }, 700); }); });
	document.querySelectorAll('.epc-activate-gw').forEach(function(btn){
		btn.addEventListener('click', function(){
			post('activate', { handler: btn.getAttribute('data-handler') }).then(function(j){ msg(j); if (j.status) setTimeout(function(){ location.reload(); }, 700); });
		});
	});
})();
</script>
