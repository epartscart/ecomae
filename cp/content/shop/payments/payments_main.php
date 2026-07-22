<?php
/**
 * CP — Payment gateways hub (GCC, Pakistan, crypto, international, legacy).
 */
defined('_ASTEXE_') or die('No access');

require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/payments/epc_payment_helpers.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_cp_page_frame.php';

if ($_SERVER['REQUEST_METHOD'] === 'POST' && !empty($_POST['action'])) {
	require __DIR__ . '/ajax_payments.php';
	exit;
}

$tab = isset($_GET['tab']) ? preg_replace('/[^a-z_]/', '', (string)$_GET['tab']) : 'dashboard';
if ($tab === '') {
	$tab = 'dashboard';
}

// Ensure new regional gateways exist when hub opens
try {
	epc_payment_seed_all_gateways($db_link);
} catch (Throwable $e) {
	// non-fatal — configure tab still works for existing rows
}

$report = epc_payment_demo_report($db_link);
$gateways = $report['gateways'];
$regionLabels = epc_payment_region_labels();
$byRegion = array('gcc' => array(), 'pakistan' => array(), 'crypto' => array(), 'international' => array(), 'legacy' => array());
foreach ($gateways as $g) {
	$region = epc_payment_handler_region($g['handler']);
	if (!isset($byRegion[$region])) {
		$byRegion[$region] = array();
	}
	$byRegion[$region][] = $g;
}

$paymentsUrl = '/' . $DP_Config->backend_dir . '/shop/payments/payments';
$guideUrl = '/' . $DP_Config->backend_dir . '/shop/payments/payments/guide';
$csrf = isset($user_session['csrf_guard_key']) ? (string)$user_session['csrf_guard_key'] : '';
$domain = rtrim($DP_Config->domain_path, '/');

epc_cp_page_frame_open(array(
	'class' => 'epc-erp-shell',
	'hero' => array(
		'badge' => 'Payments',
		'title' => 'Payment gateways',
		'sub' => 'Platform gateways plus individual office/vendor accounts so each seller can receive payment.',
		'actions' => array(
			array('label' => 'Individual accounts', 'url' => $paymentsUrl . '?tab=accounts', 'icon' => 'fa-users', 'primary' => true),
			array('label' => 'Configure', 'url' => $paymentsUrl . '?tab=configure', 'icon' => 'fa-cog'),
			array('label' => 'Guide', 'url' => $guideUrl, 'icon' => 'fa-book'),
		),
	),
));
?>
<link rel="stylesheet" href="/content/shop/finance/epc_erp_ui.css?v=20260722pay">
<style>
.epc-pay-hub { --ink:#0f172a; --muted:#64748b; --line:#e2e8f0; --accent:#0f766e; --soft:#f8fafc; margin:0 0 20px; }
.epc-pay-kpi { display:grid; grid-template-columns:repeat(5,minmax(0,1fr)); gap:10px; margin:0 0 16px; }
@media (max-width:991px){ .epc-pay-kpi{ grid-template-columns:repeat(2,minmax(0,1fr)); } }
.epc-pay-kpi .kpi { background:#fff; border:1px solid var(--line); border-radius:12px; padding:12px 14px; box-shadow:0 1px 2px rgba(15,23,42,.04); }
.epc-pay-kpi .lbl { font-size:11px; font-weight:700; letter-spacing:.04em; text-transform:uppercase; color:var(--muted); }
.epc-pay-kpi .val { margin-top:4px; font-size:22px; font-weight:750; color:var(--ink); }
.epc-pay-tabs { display:flex; flex-wrap:wrap; gap:8px; margin:0 0 16px; }
.epc-pay-tabs a { border:1px solid var(--line); background:#fff; color:var(--ink); border-radius:9px; padding:8px 12px; font-size:13px; font-weight:650; text-decoration:none !important; }
.epc-pay-tabs a.active, .epc-pay-tabs a:hover { border-color:#5eead4; background:#f0fdfa; color:#0f766e; }
.epc-pay-hint { margin:0 0 14px; padding:10px 12px; border:1px solid #99f6e4; border-radius:10px; background:#f0fdfa; color:#115e59; font-size:13px; }
.epc-pay-section { margin:0 0 18px; border:1px solid var(--line); border-radius:12px; background:#fff; overflow:hidden; }
.epc-pay-section h4 { margin:0; padding:12px 14px; border-bottom:1px solid var(--line); background:linear-gradient(180deg,#fff,var(--soft)); font-size:15px; font-weight:700; }
.epc-pay-section .body { padding:0; }
.badge-region { background:#ecfeff; color:#0e7490; font-size:11px; font-weight:700; padding:2px 8px; border-radius:8px; }
.badge-active { background:#dcfce7; color:#166534; font-size:11px; font-weight:700; padding:2px 8px; border-radius:8px; }
.badge-demo { background:#fef3c7; color:#92400e; font-size:11px; font-weight:700; padding:2px 8px; border-radius:8px; }
.epc-pay-actions { display:flex; flex-wrap:wrap; gap:8px; margin:0 0 14px; }
.epc-pay-actions .btn { border-radius:9px; }
</style>

<div class="epc-pay-hub">
	<p class="epc-pay-hint">
		<strong>Multi-method checkout:</strong> enable gateways below, then customers choose Card / BNPL / JazzCash / Crypto on the order page.
		Active default: <strong><?php echo epc_payment_h($report['active_name'] ?: 'None'); ?></strong>
		<?php if ($report['active_handler']): ?> (<code><?php echo epc_payment_h($report['active_handler']); ?></code>)<?php endif; ?>.
	</p>

	<div class="epc-pay-tabs">
		<a class="<?php echo $tab === 'dashboard' ? 'active' : ''; ?>" href="<?php echo epc_payment_h($paymentsUrl . '?tab=dashboard'); ?>">Dashboard</a>
		<a class="<?php echo $tab === 'accounts' ? 'active' : ''; ?>" href="<?php echo epc_payment_h($paymentsUrl . '?tab=accounts'); ?>">Individual accounts</a>
		<a class="<?php echo $tab === 'configure' ? 'active' : ''; ?>" href="<?php echo epc_payment_h($paymentsUrl . '?tab=configure'); ?>">Configure</a>
		<a class="<?php echo $tab === 'legacy' ? 'active' : ''; ?>" href="<?php echo epc_payment_h($paymentsUrl . '?tab=legacy'); ?>">Legacy (CIS)</a>
	</div>

	<div id="epc_pay_msg" class="alert" style="display:none;"></div>

	<?php if ($tab === 'dashboard'): ?>
	<div class="epc-pay-kpi">
		<div class="kpi"><div class="lbl">Total</div><div class="val"><?php echo (int)$report['total']; ?></div></div>
		<div class="kpi"><div class="lbl">GCC / MENA</div><div class="val"><?php echo (int)$report['gcc_gateways']; ?></div></div>
		<div class="kpi"><div class="lbl">Pakistan</div><div class="val"><?php echo (int)$report['pakistan_gateways']; ?></div></div>
		<div class="kpi"><div class="lbl">Crypto</div><div class="val"><?php echo (int)$report['crypto_gateways']; ?></div></div>
		<div class="kpi"><div class="lbl">International</div><div class="val"><?php echo (int)$report['international_gateways']; ?></div></div>
	</div>

	<div class="epc-pay-actions">
		<button type="button" class="btn btn-success btn-sm" id="epc_btn_seed"><i class="fa fa-database"></i> Seed / refresh gateways</button>
		<button type="button" class="btn btn-primary btn-sm" id="epc_btn_activate_stripe"><i class="fa fa-check"></i> Activate Stripe</button>
		<button type="button" class="btn btn-default btn-sm" id="epc_btn_activate_crypto"><i class="fa fa-bitcoin"></i> Activate Crypto</button>
		<a class="btn btn-default btn-sm" href="/epc-payments-demo.php?token=epartscart-deploy-2026" target="_blank"><i class="fa fa-external-link"></i> JSON report</a>
	</div>

	<?php
	$orderRegions = array('gcc', 'pakistan', 'crypto', 'international');
	foreach ($orderRegions as $regionKey):
		$list = $byRegion[$regionKey] ?? array();
		if (empty($list)) {
			continue;
		}
	?>
	<div class="epc-pay-section">
		<h4><?php echo epc_payment_h($regionLabels[$regionKey] ?? $regionKey); ?></h4>
		<div class="body table-responsive">
			<table class="table table-condensed" style="margin:0;">
				<thead><tr><th>Gateway</th><th>Handler</th><th>Mode</th><th>Webhook</th><th></th></tr></thead>
				<tbody>
				<?php foreach ($list as $g):
					$vals = json_decode((string)$g['parameters_values'], true);
					$demo = !empty($vals['demo_mode']);
				?>
					<tr>
						<td><strong><?php echo epc_payment_h(epc_payment_gateway_label($g)); ?></strong></td>
						<td><code><?php echo epc_payment_h($g['handler']); ?></code></td>
						<td>
							<?php echo $demo ? '<span class="badge-demo">Demo</span>' : '<span class="label label-warning">Live keys</span>'; ?>
							<?php if ((int)$g['active']): ?> <span class="badge-active">Default</span><?php endif; ?>
						</td>
						<td><small><?php echo epc_payment_h($domain . '/content/shop/finance/payment_systems/' . $g['handler'] . '/notification.php'); ?></small></td>
						<td><button type="button" class="btn btn-xs btn-default epc-activate-gw" data-handler="<?php echo epc_payment_h($g['handler']); ?>">Set default</button></td>
					</tr>
				<?php endforeach; ?>
				</tbody>
			</table>
		</div>
	</div>
	<?php endforeach; ?>
	<?php endif; ?>

	<?php if ($tab === 'legacy'): ?>
	<div class="epc-pay-section">
		<h4>Legacy payment systems (CIS / DocPart)</h4>
		<div class="body table-responsive">
			<table class="table table-striped table-condensed" style="margin:0;">
				<thead><tr><th>Name</th><th>Handler</th><th>Enabled</th><th>Default</th></tr></thead>
				<tbody>
				<?php foreach (($byRegion['legacy'] ?? array()) as $g): ?>
					<tr>
						<td><?php echo epc_payment_h(epc_payment_gateway_label($g)); ?></td>
						<td><code><?php echo epc_payment_h($g['handler']); ?></code></td>
						<td><?php echo (int)$g['anable'] ? 'Yes' : 'No'; ?></td>
						<td><?php echo (int)$g['active'] ? '<strong>Default</strong>' : '—'; ?></td>
					</tr>
				<?php endforeach; ?>
				</tbody>
			</table>
		</div>
	</div>
	<?php endif; ?>

	<?php if ($tab === 'accounts'): ?>
	<?php require __DIR__ . '/payments_accounts.php'; ?>
	<?php endif; ?>

	<?php if ($tab === 'configure'): ?>
	<?php require __DIR__ . '/payments_configure.php'; ?>
	<?php endif; ?>
</div>

<script>
(function(){
	var url = <?php echo json_encode('/' . trim((string)$DP_Config->backend_dir, '/') . '/content/shop/payments/ajax_payments_endpoint.php'); ?>;
	var csrf = <?php echo json_encode($csrf); ?>;
	function post(action, extra) {
		var fd = new FormData();
		fd.append('action', action);
		fd.append('csrf_guard_key', csrf);
		if (extra) Object.keys(extra).forEach(function(k){ fd.append(k, extra[k]); });
		return fetch(url, { method:'POST', body:fd, credentials:'same-origin' })
			.then(function(r){ return r.text().then(function(t){
				try { return JSON.parse(t); }
				catch (e) { return { status:false, message:'Invalid JSON (HTTP '+r.status+')' }; }
			}); });
	}
	function msg(j) {
		var el = document.getElementById('epc_pay_msg');
		if (!el) return;
		el.className = 'alert alert-' + (j && j.status ? 'success' : 'danger');
		el.textContent = (j && j.message) || 'Request failed';
		el.style.display = 'block';
		if (j && j.status) setTimeout(function(){ location.reload(); }, 700);
	}
	window.epcPayPost = function(action, extra){ return post(action, extra).then(msg).catch(function(e){ msg({status:false, message:e.message||'Request failed'}); }); };
	var seed = document.getElementById('epc_btn_seed');
	if (seed) seed.addEventListener('click', function(){ post('seed_dummy', {}).then(msg); });
	var actStripe = document.getElementById('epc_btn_activate_stripe');
	if (actStripe) actStripe.addEventListener('click', function(){ post('activate', {handler:'stripe'}).then(msg); });
	var actCrypto = document.getElementById('epc_btn_activate_crypto');
	if (actCrypto) actCrypto.addEventListener('click', function(){ post('activate', {handler:'nowpayments'}).then(msg); });
	document.querySelectorAll('.epc-activate-gw').forEach(function(btn){
		btn.addEventListener('click', function(){ post('activate', {handler: btn.getAttribute('data-handler')}).then(msg); });
	});

	// Individual accounts tab
	function toggleOwner() {
		var ot = document.getElementById('epc_acc_owner_type');
		if (!ot) return;
		var t = ot.value;
		var ow = document.getElementById('epc_acc_office_wrap');
		var vw = document.getElementById('epc_acc_vendor_wrap');
		if (ow) ow.style.display = (t === 'office') ? '' : 'none';
		if (vw) vw.style.display = (t === 'vendor') ? '' : 'none';
	}
	var otEl = document.getElementById('epc_acc_owner_type');
	if (otEl) {
		otEl.addEventListener('change', toggleOwner);
		toggleOwner();
	}
	var accForm = document.getElementById('epc_pay_account_form');
	if (accForm) {
		var saveBtn = accForm.querySelector('button[type="submit"]');
		function saveAccount(ev) {
			if (ev) ev.preventDefault();
			var fd = new FormData(accForm);
			var ownerType = String(fd.get('owner_type') || 'platform');
			var ownerId = 0;
			if (ownerType === 'office') ownerId = parseInt(fd.get('office_id') || '0', 10) || 0;
			if (ownerType === 'vendor') ownerId = parseInt(fd.get('vendor_id') || '0', 10) || 0;
			if ((ownerType === 'office' || ownerType === 'vendor') && ownerId <= 0) {
				msg({ status:false, message:'Select an office or vendor' });
				return false;
			}
			var credsRaw = String(fd.get('credentials_json') || '{}');
			try { JSON.parse(credsRaw); } catch (e) { msg({ status:false, message:'Credentials JSON is invalid' }); return false; }
			var demoEl = accForm.querySelector('[name=demo_mode]');
			var defEl = accForm.querySelector('[name=is_default]');
			post('save_account', {
				id: fd.get('id') || 0,
				owner_type: ownerType,
				owner_id: ownerId,
				title: fd.get('title') || '',
				handler: fd.get('handler') || '',
				mode: fd.get('mode') || 'direct',
				connected_account_id: fd.get('connected_account_id') || '',
				payout_iban: fd.get('payout_iban') || '',
				payout_bank: fd.get('payout_bank') || '',
				payout_name: fd.get('payout_name') || '',
				platform_fee_pct: fd.get('platform_fee_pct') || 0,
				status: fd.get('status') || 'active',
				demo_mode: demoEl && demoEl.checked ? 1 : 0,
				is_default: defEl && defEl.checked ? 1 : 0,
				credentials_json: credsRaw
			}).then(msg);
			return false;
		}
		accForm.addEventListener('submit', saveAccount);
		if (saveBtn) saveBtn.addEventListener('click', saveAccount);
	}
	var seedAcc = document.getElementById('epc_btn_seed_platform_account');
	if (seedAcc) seedAcc.addEventListener('click', function(){ post('seed_platform_account', {}).then(msg); });
	document.querySelectorAll('.epc-acc-disable').forEach(function(btn){
		btn.addEventListener('click', function(){ post('disable_account', {id: btn.getAttribute('data-id')}).then(msg); });
	});
	document.querySelectorAll('.epc-settle-paid').forEach(function(btn){
		btn.addEventListener('click', function(){ post('mark_settlement', {id: btn.getAttribute('data-id'), status: 'paid_out'}).then(msg); });
	});
})();
</script>
<?php
epc_cp_page_frame_close();
