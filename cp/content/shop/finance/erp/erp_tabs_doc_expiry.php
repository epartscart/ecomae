<?php
defined('_ASTEXE_') or die('No access');
/**
 * Document Expiry Tracker — central register for every expiring document
 * (legal / customer / insurance / banking + custom), with derived status and
 * configurable auto-email reminder lead times. Country-driven compliance
 * checklist is resolved from the tenant's registration country.
 */
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_doc_expiry.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_company_context.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_ui.php';

epc_docx_ensure_schema($db_link);
$csrfLocal = isset($csrf) ? $csrf : '';
$activeCompanyId = function_exists('epc_erp_active_company_id') ? epc_erp_active_company_id($db_link) : 0;
$catFilter = isset($_GET['cat']) ? (string) $_GET['cat'] : '';
$cats = epc_docx_categories();
if ($catFilter !== '' && !isset($cats[$catFilter])) {
	$catFilter = '';
}
$docId = isset($_GET['doc']) ? (int) $_GET['doc'] : 0;

$tenantCountry = epc_docx_resolve_country($db_link);
$countryProfile = epc_docx_country_profile($tenantCountry);
$summary = epc_docx_summary($db_link, $activeCompanyId);
$rows = epc_docx_list($db_link, $activeCompanyId, $catFilter);

$statusLabel = array('valid' => 'success', 'expiring' => 'warning', 'expired' => 'danger', 'none' => 'default');
$statusText = array('valid' => 'Valid', 'expiring' => 'Expiring soon', 'expired' => 'Expired', 'none' => 'No expiry');

erp_page_header(
	'<i class="fa fa-calendar-times-o"></i> Document Expiry Tracker',
	'One central register for every expiring document — legal, customer, insurance and banking — with auto-email reminders at configurable lead times.',
	array(
		array('label' => 'ERP', 'url' => epc_erp_tab_url($erpUrl, 'dashboard', $date_from_str, $date_to_str)),
		array('label' => 'Document Expiry'),
	)
);

erp_stat_cards(array(
	array('label' => 'Tracked', 'value' => (string) $summary['total']),
	array('label' => 'Valid', 'value' => (string) $summary['valid']),
	array('label' => 'Expiring soon', 'value' => (string) $summary['expiring']),
	array('label' => 'Expired', 'value' => (string) $summary['expired']),
));

$tabBase = epc_erp_tab_url($erpUrl, 'doc_expiry', $date_from_str, $date_to_str);
$sep = strpos($tabBase, '?') === false ? '?' : '&';
$editRow = $docId > 0 ? epc_docx_get($db_link, $docId) : null;
?>
<div id="epc_erp_msg" class="alert" style="display:none;"></div>

<div class="row">
	<div class="col-md-4">
		<div class="well well-sm">
			<h5><i class="fa fa-plus-circle"></i> <?php echo $editRow ? 'Edit document' : 'Add document'; ?></h5>
			<form id="epc_docx_save" class="form">
				<input type="hidden" name="csrf_guard_key" value="<?php echo epc_erp_h($csrfLocal); ?>">
				<input type="hidden" name="id" value="<?php echo (int) ($editRow['id'] ?? 0); ?>">
				<div class="form-group">
					<label>Category</label>
					<select name="category" class="form-control input-sm">
						<?php foreach ($cats as $k => $lbl): ?>
							<option value="<?php echo epc_erp_h($k); ?>"<?php echo (($editRow['category'] ?? 'legal') === $k) ? ' selected' : ''; ?>><?php echo epc_erp_h($lbl); ?></option>
						<?php endforeach; ?>
					</select>
				</div>
				<div class="form-group">
					<label>Document type</label>
					<input type="text" name="doc_type" class="form-control input-sm" list="epc_docx_types" placeholder="e.g. Trade Licence" value="<?php echo epc_erp_h($editRow['doc_type'] ?? ''); ?>" required>
					<datalist id="epc_docx_types">
						<?php foreach ($countryProfile['documents'] as $d): ?>
							<option value="<?php echo epc_erp_h($d['type']); ?>"><?php echo epc_erp_h($d['authority']); ?></option>
						<?php endforeach; ?>
					</datalist>
				</div>
				<div class="form-group"><label>Title / description</label><input type="text" name="title" class="form-control input-sm" value="<?php echo epc_erp_h($editRow['title'] ?? ''); ?>"></div>
				<div class="row">
					<div class="col-xs-6 form-group"><label>Reference no</label><input type="text" name="ref_no" class="form-control input-sm" value="<?php echo epc_erp_h($editRow['ref_no'] ?? ''); ?>"></div>
					<div class="col-xs-6 form-group"><label>Issuer / authority</label><input type="text" name="issuer" class="form-control input-sm" value="<?php echo epc_erp_h($editRow['issuer'] ?? ''); ?>"></div>
				</div>
				<div class="form-group"><label>Owner / holder</label><input type="text" name="owner" class="form-control input-sm" value="<?php echo epc_erp_h($editRow['owner'] ?? ''); ?>"></div>
				<div class="form-group"><label>Reminder email</label><input type="email" name="owner_email" class="form-control input-sm" placeholder="who gets the reminder" value="<?php echo epc_erp_h($editRow['owner_email'] ?? ''); ?>"></div>
				<div class="row">
					<div class="col-xs-6 form-group"><label>Issue date</label><input type="date" name="issue_date_str" class="form-control input-sm" value="<?php echo $editRow && (int) $editRow['issue_date'] > 0 ? date('Y-m-d', (int) $editRow['issue_date']) : ''; ?>"></div>
					<div class="col-xs-6 form-group"><label>Expiry date</label><input type="date" name="expiry_date_str" class="form-control input-sm" value="<?php echo $editRow && (int) $editRow['expiry_date'] > 0 ? date('Y-m-d', (int) $editRow['expiry_date']) : ''; ?>" required></div>
				</div>
				<div class="form-group">
					<label>Reminder lead days <small class="text-muted">(comma-separated, before expiry)</small></label>
					<input type="text" name="reminder_days" class="form-control input-sm" value="<?php echo epc_erp_h($editRow['reminder_days'] ?? $countryProfile['default_reminder_days']); ?>">
				</div>
				<div class="form-group"><label>Attachment path / URL</label><input type="text" name="attachment_path" class="form-control input-sm" value="<?php echo epc_erp_h($editRow['attachment_path'] ?? ''); ?>"></div>
				<div class="form-group"><label>Note</label><textarea name="note" class="form-control input-sm" rows="2"><?php echo epc_erp_h($editRow['note'] ?? ''); ?></textarea></div>
				<button type="submit" class="btn btn-primary btn-sm"><?php echo $editRow ? 'Save changes' : 'Add to register'; ?></button>
				<?php if ($editRow): ?><a class="btn btn-default btn-sm" href="<?php echo epc_erp_h($tabBase); ?>">Cancel</a><?php endif; ?>
			</form>
		</div>

		<div class="well well-sm">
			<h5><i class="fa fa-flag"></i> Compliance checklist
				<small class="text-muted">— <?php echo epc_erp_h($tenantCountry !== '' ? $tenantCountry : 'generic'); ?></small></h5>
			<p class="text-muted" style="font-size:12px;">Statutory documents for your registered country. Add the ones you hold so renewals are never missed.</p>
			<ul class="list-unstyled" style="font-size:12px;margin-bottom:0;">
				<?php foreach ($countryProfile['documents'] as $d): ?>
					<li><i class="fa fa-angle-right text-muted"></i> <strong><?php echo epc_erp_h($d['type']); ?></strong> <span class="text-muted">— <?php echo epc_erp_h($d['authority']); ?></span></li>
				<?php endforeach; ?>
			</ul>
		</div>
	</div>

	<div class="col-md-8">
		<div class="clearfix" style="margin-bottom:8px;">
			<div class="btn-group btn-group-sm pull-left" role="group">
				<a class="btn btn-<?php echo $catFilter === '' ? 'primary' : 'default'; ?>" href="<?php echo epc_erp_h($tabBase); ?>">All</a>
				<?php foreach ($cats as $k => $lbl): ?>
					<a class="btn btn-<?php echo $catFilter === $k ? 'primary' : 'default'; ?>" href="<?php echo epc_erp_h($tabBase . $sep . 'cat=' . urlencode($k)); ?>"><?php echo epc_erp_h($lbl); ?></a>
				<?php endforeach; ?>
			</div>
			<button id="epc_docx_run" class="btn btn-success btn-sm pull-right"><i class="fa fa-paper-plane"></i> Run reminders now</button>
		</div>
		<div class="table-responsive">
		<table class="table table-bordered table-condensed">
			<thead><tr><th>Type / title</th><th>Category</th><th>Ref</th><th>Expiry</th><th class="text-right">Days left</th><th>Status</th><th>Reminders</th><th></th></tr></thead>
			<tbody>
			<?php if (empty($rows)): ?>
				<tr><td colspan="8" class="text-muted">No documents tracked yet. Add one on the left, or pull from the compliance checklist.</td></tr>
			<?php else: foreach ($rows as $r):
				$st = (string) $r['status'];
				$daysLeft = (int) $r['days_left'];
				$fromIns = $r['source_module'] === 'insurance'; ?>
				<tr<?php echo $docId === (int) $r['id'] ? ' class="info"' : ''; ?>>
					<td>
						<strong><?php echo epc_erp_h($r['doc_type']); ?></strong>
						<?php if ($fromIns): ?><span class="label label-info" style="font-size:9px;">Insurance</span><?php endif; ?>
						<?php if (trim((string) $r['title']) !== ''): ?><br><small class="text-muted"><?php echo epc_erp_h($r['title']); ?></small><?php endif; ?>
					</td>
					<td><small><?php echo epc_erp_h($cats[$r['category']] ?? $r['category']); ?></small></td>
					<td><small><?php echo epc_erp_h($r['ref_no']); ?></small></td>
					<td><small><?php echo (int) $r['expiry_date'] > 0 ? epc_erp_h(date('Y-m-d', (int) $r['expiry_date'])) : '—'; ?></small></td>
					<td class="text-right"><?php echo $st === 'none' ? '—' : (int) $daysLeft; ?></td>
					<td><span class="label label-<?php echo $statusLabel[$st] ?? 'default'; ?>"><?php echo epc_erp_h($statusText[$st] ?? $st); ?></span></td>
					<td><small class="text-muted"><?php echo epc_erp_h($r['reminder_days']); ?></small></td>
					<td>
						<a class="btn btn-link btn-xs" href="<?php echo epc_erp_h($tabBase . $sep . 'doc=' . (int) $r['id']); ?>"><?php echo $fromIns ? 'View' : 'Edit'; ?></a>
						<?php if (!$fromIns): ?><button class="btn btn-link btn-xs text-danger epc-docx-del" data-id="<?php echo (int) $r['id']; ?>">Delete</button><?php endif; ?>
					</td>
				</tr>
			<?php endforeach; endif; ?>
			</tbody>
		</table>
		</div>
		<p class="text-muted" style="font-size:11px;">
			<i class="fa fa-info-circle"></i> Reminders auto-email the document owner (falling back to the admin inbox) at each lead day; each reminder fires once. Wire <code>epc_docx_run_reminders()</code> to a daily cron for unattended alerts. Insurance-sourced rows are managed from the Insurance module.
		</p>
	</div>
</div>

<script>
(function(){
	var url = <?php echo json_encode(isset($erpAjaxEndpoint) ? $erpAjaxEndpoint : ('/' . (isset($DP_Config->backend_dir) ? $DP_Config->backend_dir : 'cp') . '/content/shop/finance/erp/ajax_erp_endpoint.php')); ?>;
	var csrf = <?php echo json_encode($csrfLocal); ?>;
	function post(action, fd){ fd.append('action', action); return fetch(url,{method:'POST',body:fd,credentials:'same-origin'}).then(function(r){return r.json();}); }
	function msg(j){ var el=document.getElementById('epc_erp_msg'); if(el){ el.className='alert alert-'+(j.status?'success':'danger'); el.textContent=j.message||''; el.style.display='block'; el.scrollIntoView({behavior:'smooth',block:'center'}); } if(j.status) setTimeout(function(){ location.reload(); }, 900); }
	var f=document.getElementById('epc_docx_save');
	if(f) f.addEventListener('submit', function(e){ e.preventDefault(); post('docx_save', new FormData(f)).then(msg); });
	var run=document.getElementById('epc_docx_run');
	if(run) run.addEventListener('click', function(){ var fd=new FormData(); fd.append('csrf_guard_key',csrf); post('docx_run_reminders', fd).then(msg); });
	document.querySelectorAll('.epc-docx-del').forEach(function(b){ b.addEventListener('click', function(){ if(!confirm('Remove this document from the register?')) return; var fd=new FormData(); fd.append('csrf_guard_key',csrf); fd.append('id',b.getAttribute('data-id')); post('docx_delete', fd).then(msg); }); });
})();
</script>
