<?php
defined('_ASTEXE_') or die('No access');
/**
 * Insurance Management — corporate policy register (all classes), document
 * store, claims advice & tracking, and per-policy auto-email renewal reminders
 * (fed into the central Document Expiry Tracker). Recommended cover is
 * country-driven from the tenant's registration country.
 */
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_insurance.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_company_context.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_ui.php';

epc_ins_ensure_schema($db_link);
$csrfLocal = isset($csrf) ? $csrf : '';
$activeCompanyId = function_exists('epc_erp_active_company_id') ? epc_erp_active_company_id($db_link) : 0;
$classes = epc_ins_classes();
$claimStatuses = epc_ins_claim_statuses();
$classFilter = isset($_GET['cls']) ? (string) $_GET['cls'] : '';
if ($classFilter !== '' && !isset($classes[$classFilter])) {
	$classFilter = '';
}
$polId = isset($_GET['pol']) ? (int) $_GET['pol'] : 0;

$tenantCountry = epc_docx_resolve_country($db_link);
$recommended = epc_ins_country_recommended($tenantCountry);
$summary = epc_ins_summary($db_link, $activeCompanyId);
$rows = epc_ins_list($db_link, $activeCompanyId, $classFilter);

$statusLabel = array('valid' => 'success', 'expiring' => 'warning', 'expired' => 'danger', 'cancelled' => 'default', 'lapsed' => 'default', 'none' => 'default');
$statusText = array('valid' => 'Active', 'expiring' => 'Renew soon', 'expired' => 'Expired', 'cancelled' => 'Cancelled', 'lapsed' => 'Lapsed', 'none' => 'No expiry');

erp_page_header(
	'<i class="fa fa-shield"></i> Insurance Management',
	'Corporate insurance register across all classes — policy details, document store, claims tracking and timeframe auto-email renewal reminders, built to international best practice.',
	array(
		array('label' => 'ERP', 'url' => epc_erp_tab_url($erpUrl, 'dashboard', $date_from_str, $date_to_str)),
		array('label' => 'Insurance'),
	)
);

erp_stat_cards(array(
	array('label' => 'Policies', 'value' => (string) $summary['policies']),
	array('label' => 'Active', 'value' => (string) $summary['active']),
	array('label' => 'Renew soon', 'value' => (string) $summary['expiring']),
	array('label' => 'Sum insured', 'value' => epc_erp_money($summary['sum_insured'])),
	array('label' => 'Annual premium', 'value' => epc_erp_money($summary['annual_premium'])),
	array('label' => 'Open claims', 'value' => (string) $summary['open_claims']),
));

$tabBase = epc_erp_tab_url($erpUrl, 'insurance', $date_from_str, $date_to_str);
$sep = strpos($tabBase, '?') === false ? '?' : '&';
$editPol = $polId > 0 ? epc_ins_get($db_link, $polId) : null;
?>
<div id="epc_erp_msg" class="alert" style="display:none;"></div>

<div class="row">
	<div class="col-md-4">
		<div class="well well-sm">
			<h5><i class="fa fa-plus-circle"></i> <?php echo $editPol ? 'Edit policy' : 'New policy'; ?></h5>
			<form id="epc_ins_save" class="form">
				<input type="hidden" name="csrf_guard_key" value="<?php echo epc_erp_h($csrfLocal); ?>">
				<input type="hidden" name="id" value="<?php echo (int) ($editPol['id'] ?? 0); ?>">
				<div class="row">
					<div class="col-xs-6 form-group"><label>Policy no</label><input type="text" name="policy_no" class="form-control input-sm" value="<?php echo epc_erp_h($editPol['policy_no'] ?? ''); ?>" required></div>
					<div class="col-xs-6 form-group">
						<label>Class</label>
						<select name="class" class="form-control input-sm">
							<?php foreach ($classes as $k => $lbl): ?>
								<option value="<?php echo epc_erp_h($k); ?>"<?php echo (($editPol['class'] ?? 'marine') === $k) ? ' selected' : ''; ?>><?php echo epc_erp_h($lbl); ?></option>
							<?php endforeach; ?>
						</select>
					</div>
				</div>
				<div class="form-group"><label>Title / description</label><input type="text" name="title" class="form-control input-sm" value="<?php echo epc_erp_h($editPol['title'] ?? ''); ?>"></div>
				<div class="row">
					<div class="col-xs-6 form-group"><label>Insurer</label><input type="text" name="insurer" class="form-control input-sm" value="<?php echo epc_erp_h($editPol['insurer'] ?? ''); ?>"></div>
					<div class="col-xs-6 form-group"><label>Broker</label><input type="text" name="broker" class="form-control input-sm" value="<?php echo epc_erp_h($editPol['broker'] ?? ''); ?>"></div>
				</div>
				<div class="form-group"><label>Insured name</label><input type="text" name="insured_name" class="form-control input-sm" value="<?php echo epc_erp_h($editPol['insured_name'] ?? ''); ?>"></div>
				<div class="row">
					<div class="col-xs-4 form-group"><label>Sum insured</label><input type="number" step="0.01" name="sum_insured" class="form-control input-sm" value="<?php echo epc_erp_h($editPol['sum_insured'] ?? '0'); ?>"></div>
					<div class="col-xs-4 form-group"><label>Premium</label><input type="number" step="0.01" name="premium" class="form-control input-sm" value="<?php echo epc_erp_h($editPol['premium'] ?? '0'); ?>"></div>
					<div class="col-xs-4 form-group"><label>Currency</label><input type="text" name="currency" class="form-control input-sm" maxlength="3" value="<?php echo epc_erp_h($editPol['currency'] ?? 'AED'); ?>"></div>
				</div>
				<div class="row">
					<div class="col-xs-6 form-group"><label>Deductible / excess</label><input type="number" step="0.01" name="deductible" class="form-control input-sm" value="<?php echo epc_erp_h($editPol['deductible'] ?? '0'); ?>"></div>
					<div class="col-xs-6 form-group">
						<label>Status</label>
						<select name="status" class="form-control input-sm">
							<?php foreach (array('active' => 'Active', 'cancelled' => 'Cancelled', 'lapsed' => 'Lapsed') as $k => $lbl): ?>
								<option value="<?php echo $k; ?>"<?php echo (($editPol['status'] ?? 'active') === $k) ? ' selected' : ''; ?>><?php echo $lbl; ?></option>
							<?php endforeach; ?>
						</select>
					</div>
				</div>
				<div class="row">
					<div class="col-xs-6 form-group"><label>Start date</label><input type="date" name="start_date_str" class="form-control input-sm" value="<?php echo $editPol && (int) $editPol['start_date'] > 0 ? date('Y-m-d', (int) $editPol['start_date']) : ''; ?>"></div>
					<div class="col-xs-6 form-group"><label>Expiry date</label><input type="date" name="expiry_date_str" class="form-control input-sm" value="<?php echo $editPol && (int) $editPol['expiry_date'] > 0 ? date('Y-m-d', (int) $editPol['expiry_date']) : ''; ?>" required></div>
				</div>
				<div class="row">
					<div class="col-xs-6 form-group"><label>Reminder lead days</label><input type="text" name="reminder_days" class="form-control input-sm" value="<?php echo epc_erp_h($editPol['reminder_days'] ?? '90,60,30,7'); ?>"></div>
					<div class="col-xs-6 form-group"><label>Reminder email</label><input type="email" name="contact_email" class="form-control input-sm" value="<?php echo epc_erp_h($editPol['contact_email'] ?? ''); ?>"></div>
				</div>
				<div class="form-group"><label>Note</label><textarea name="note" class="form-control input-sm" rows="2"><?php echo epc_erp_h($editPol['note'] ?? ''); ?></textarea></div>
				<button type="submit" class="btn btn-primary btn-sm"><?php echo $editPol ? 'Save policy' : 'Add policy'; ?></button>
				<?php if ($editPol): ?><a class="btn btn-default btn-sm" href="<?php echo epc_erp_h($tabBase); ?>">Cancel</a><?php endif; ?>
			</form>
		</div>

		<div class="well well-sm">
			<h5><i class="fa fa-flag"></i> Recommended cover
				<small class="text-muted">— <?php echo epc_erp_h($tenantCountry !== '' ? $tenantCountry : 'generic'); ?></small></h5>
			<ul class="list-unstyled" style="font-size:12px;margin-bottom:0;">
				<?php foreach ($recommended as $rec): ?>
					<li><i class="fa fa-shield text-muted"></i> <strong><?php echo epc_erp_h(epc_ins_class_label($rec['class'])); ?></strong> <span class="text-muted">— <?php echo epc_erp_h($rec['basis']); ?></span></li>
				<?php endforeach; ?>
			</ul>
		</div>
	</div>

	<div class="col-md-8">
		<div class="clearfix" style="margin-bottom:8px;">
			<form class="form-inline pull-left" method="get" style="display:inline-block;">
				<?php foreach ($_GET as $gk => $gv): if (in_array($gk, array('cls'), true)) continue; ?>
					<input type="hidden" name="<?php echo epc_erp_h($gk); ?>" value="<?php echo epc_erp_h((string) $gv); ?>">
				<?php endforeach; ?>
				<select name="cls" class="form-control input-sm" onchange="this.form.submit()">
					<option value="">All classes</option>
					<?php foreach ($classes as $k => $lbl): ?>
						<option value="<?php echo epc_erp_h($k); ?>"<?php echo $classFilter === $k ? ' selected' : ''; ?>><?php echo epc_erp_h($lbl); ?></option>
					<?php endforeach; ?>
				</select>
			</form>
			<button id="epc_ins_run" class="btn btn-success btn-sm pull-right"><i class="fa fa-paper-plane"></i> Run renewal reminders</button>
		</div>
		<div class="table-responsive">
		<table class="table table-bordered table-condensed">
			<thead><tr><th>Policy / class</th><th>Insurer</th><th class="text-right">Sum insured</th><th>Expiry</th><th class="text-right">Days</th><th>Status</th><th>Claims</th><th></th></tr></thead>
			<tbody>
			<?php if (empty($rows)): ?>
				<tr><td colspan="8" class="text-muted">No policies yet. Add one on the left.</td></tr>
			<?php else: foreach ($rows as $r):
				$st = (string) $r['eff_status']; ?>
				<tr<?php echo $polId === (int) $r['id'] ? ' class="info"' : ''; ?>>
					<td><strong><?php echo epc_erp_h($r['policy_no']); ?></strong><br><small class="text-muted"><?php echo epc_erp_h($r['class_label']); ?></small></td>
					<td><small><?php echo epc_erp_h($r['insurer']); ?></small></td>
					<td class="text-right"><?php echo epc_erp_money($r['sum_insured']); ?> <small class="text-muted"><?php echo epc_erp_h($r['currency']); ?></small></td>
					<td><small><?php echo (int) $r['expiry_date'] > 0 ? epc_erp_h(date('Y-m-d', (int) $r['expiry_date'])) : '—'; ?></small></td>
					<td class="text-right"><?php echo in_array($st, array('cancelled', 'lapsed', 'none'), true) ? '—' : (int) $r['days_left']; ?></td>
					<td><span class="label label-<?php echo $statusLabel[$st] ?? 'default'; ?>"><?php echo epc_erp_h($statusText[$st] ?? $st); ?></span></td>
					<td><?php echo (int) $r['open_claims'] > 0 ? '<span class="label label-warning">' . (int) $r['open_claims'] . ' open</span>' : '<small class="text-muted">—</small>'; ?></td>
					<td>
						<a class="btn btn-link btn-xs" href="<?php echo epc_erp_h($tabBase . $sep . 'pol=' . (int) $r['id']); ?>">Open</a>
						<button class="btn btn-link btn-xs text-danger epc-ins-del" data-id="<?php echo (int) $r['id']; ?>">Delete</button>
					</td>
				</tr>
			<?php endforeach; endif; ?>
			</tbody>
		</table>
		</div>
		<p class="text-muted" style="font-size:11px;"><i class="fa fa-info-circle"></i> Active policy renewals feed the central <strong>Document Expiry Tracker</strong>; reminders email the policy contact at each lead day. Wire the reminder runner to a daily cron for unattended alerts.</p>
	</div>
</div>

<?php if ($editPol):
	$docs = epc_ins_docs($db_link, $polId);
	$claims = epc_ins_claims($db_link, $polId);
?>
<hr>
<div class="row">
	<div class="col-md-6">
		<div class="well well-sm">
			<h5><i class="fa fa-folder-open-o"></i> Documents — <?php echo epc_erp_h($editPol['policy_no']); ?></h5>
			<form id="epc_ins_doc_add" class="form">
				<input type="hidden" name="csrf_guard_key" value="<?php echo epc_erp_h($csrfLocal); ?>">
				<input type="hidden" name="policy_id" value="<?php echo (int) $polId; ?>">
				<div class="row">
					<div class="col-xs-4 form-group"><label>Type</label>
						<select name="doc_type" class="form-control input-sm">
							<?php foreach (array('policy' => 'Policy schedule', 'endorsement' => 'Endorsement', 'invoice' => 'Premium invoice', 'cover_note' => 'Cover note', 'claim' => 'Claim document', 'other' => 'Other') as $k => $lbl): ?>
								<option value="<?php echo $k; ?>"><?php echo $lbl; ?></option>
							<?php endforeach; ?>
						</select>
					</div>
					<div class="col-xs-8 form-group"><label>Title</label><input type="text" name="title" class="form-control input-sm"></div>
				</div>
				<div class="form-group"><label>File path / URL</label><input type="text" name="file_path" class="form-control input-sm" placeholder="/uploads/policy.pdf"></div>
				<button type="submit" class="btn btn-default btn-sm">Add document</button>
			</form>
			<table class="table table-condensed" style="margin-top:8px;">
				<thead><tr><th>Type</th><th>Title</th><th>File</th><th></th></tr></thead>
				<tbody>
				<?php if (empty($docs)): ?>
					<tr><td colspan="4" class="text-muted">No documents stored.</td></tr>
				<?php else: foreach ($docs as $d): ?>
					<tr>
						<td><small><?php echo epc_erp_h($d['doc_type']); ?></small></td>
						<td><small><?php echo epc_erp_h($d['title']); ?></small></td>
						<td><small><?php echo $d['file_path'] !== '' ? '<a href="' . epc_erp_h($d['file_path']) . '" target="_blank">open</a>' : '—'; ?></small></td>
						<td><button class="btn btn-link btn-xs text-danger epc-ins-doc-del" data-id="<?php echo (int) $d['id']; ?>">×</button></td>
					</tr>
				<?php endforeach; endif; ?>
				</tbody>
			</table>
		</div>
	</div>

	<div class="col-md-6">
		<div class="well well-sm">
			<h5><i class="fa fa-gavel"></i> Claims — advice &amp; tracking</h5>
			<form id="epc_ins_claim_add" class="form">
				<input type="hidden" name="csrf_guard_key" value="<?php echo epc_erp_h($csrfLocal); ?>">
				<input type="hidden" name="policy_id" value="<?php echo (int) $polId; ?>">
				<div class="row">
					<div class="col-xs-6 form-group"><label>Claim no</label><input type="text" name="claim_no" class="form-control input-sm"></div>
					<div class="col-xs-6 form-group"><label>Loss date</label><input type="date" name="loss_date_str" class="form-control input-sm"></div>
				</div>
				<div class="row">
					<div class="col-xs-6 form-group"><label>Claim amount</label><input type="number" step="0.01" name="claim_amount" class="form-control input-sm" value="0"></div>
					<div class="col-xs-6 form-group"><label>Doc deadline</label><input type="date" name="deadline_date_str" class="form-control input-sm"></div>
				</div>
				<div class="form-group"><label>Description</label><textarea name="description" class="form-control input-sm" rows="2"></textarea></div>
				<button type="submit" class="btn btn-default btn-sm">Log claim</button>
			</form>
			<table class="table table-condensed" style="margin-top:8px;">
				<thead><tr><th>Claim</th><th class="text-right">Amount</th><th>Status</th><th></th></tr></thead>
				<tbody>
				<?php if (empty($claims)): ?>
					<tr><td colspan="4" class="text-muted">No claims logged.</td></tr>
				<?php else: foreach ($claims as $cl):
					$cls = (string) $cl['status'];
					$lbl = in_array($cls, array('settled', 'closed'), true) ? 'success' : ($cls === 'rejected' ? 'danger' : 'warning'); ?>
					<tr>
						<td><strong><?php echo epc_erp_h($cl['claim_no']); ?></strong><?php if (trim((string) $cl['description']) !== ''): ?><br><small class="text-muted"><?php echo epc_erp_h(mb_substr((string) $cl['description'], 0, 50)); ?></small><?php endif; ?></td>
						<td class="text-right"><?php echo epc_erp_money($cl['claim_amount']); ?><?php if ((float) $cl['settled_amount'] > 0): ?><br><small class="text-success">settled <?php echo epc_erp_money($cl['settled_amount']); ?></small><?php endif; ?></td>
						<td>
							<select class="form-control input-sm epc-ins-claim-status" data-id="<?php echo (int) $cl['id']; ?>" style="font-size:11px;padding:2px;">
								<?php foreach ($claimStatuses as $k => $lblc): ?>
									<option value="<?php echo $k; ?>"<?php echo $cls === $k ? ' selected' : ''; ?>><?php echo epc_erp_h($lblc); ?></option>
								<?php endforeach; ?>
							</select>
						</td>
						<td><span class="label label-<?php echo $lbl; ?>" style="font-size:9px;"><?php echo epc_erp_h($claimStatuses[$cls] ?? $cls); ?></span></td>
					</tr>
				<?php endforeach; endif; ?>
				</tbody>
			</table>
			<p class="text-muted" style="font-size:11px;margin-bottom:0;">Lifecycle: Notified → Survey → Documents → Assessed → Settled / Rejected → Closed.</p>
		</div>
	</div>
</div>
<?php endif; ?>

<script>
(function(){
	var url = <?php echo json_encode(isset($erpAjaxEndpoint) ? $erpAjaxEndpoint : ('/' . (isset($DP_Config->backend_dir) ? $DP_Config->backend_dir : 'cp') . '/content/shop/finance/erp/ajax_erp_endpoint.php')); ?>;
	var csrf = <?php echo json_encode($csrfLocal); ?>;
	function post(action, fd){ fd.append('action', action); return fetch(url,{method:'POST',body:fd,credentials:'same-origin'}).then(function(r){return r.json();}); }
	function msg(j){ var el=document.getElementById('epc_erp_msg'); if(el){ el.className='alert alert-'+(j.status?'success':'danger'); el.textContent=j.message||''; el.style.display='block'; el.scrollIntoView({behavior:'smooth',block:'center'}); } if(j.status) setTimeout(function(){ location.reload(); }, 900); }
	function bind(id, action){ var f=document.getElementById(id); if(f) f.addEventListener('submit', function(e){ e.preventDefault(); post(action, new FormData(f)).then(msg); }); }
	bind('epc_ins_save', 'ins_save');
	bind('epc_ins_doc_add', 'ins_doc_add');
	bind('epc_ins_claim_add', 'ins_claim_add');
	var run=document.getElementById('epc_ins_run');
	if(run) run.addEventListener('click', function(){ var fd=new FormData(); fd.append('csrf_guard_key',csrf); post('docx_run_reminders', fd).then(msg); });
	document.querySelectorAll('.epc-ins-del').forEach(function(b){ b.addEventListener('click', function(){ if(!confirm('Delete this policy and its claims/documents?')) return; var fd=new FormData(); fd.append('csrf_guard_key',csrf); fd.append('id',b.getAttribute('data-id')); post('ins_delete', fd).then(msg); }); });
	document.querySelectorAll('.epc-ins-doc-del').forEach(function(b){ b.addEventListener('click', function(){ var fd=new FormData(); fd.append('csrf_guard_key',csrf); fd.append('id',b.getAttribute('data-id')); post('ins_doc_delete', fd).then(msg); }); });
	document.querySelectorAll('.epc-ins-claim-status').forEach(function(s){ s.addEventListener('change', function(){ var fd=new FormData(); fd.append('csrf_guard_key',csrf); fd.append('id',s.getAttribute('data-id')); fd.append('status',s.value); post('ins_claim_status', fd).then(msg); }); });
})();
</script>
