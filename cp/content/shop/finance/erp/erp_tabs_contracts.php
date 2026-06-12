<?php
defined('_ASTEXE_') or die('No access');
/**
 * Contracts, e-signature & OCR — contract lifecycle (draft → sent → signed →
 * active), a tamper-evident e-signature ledger, and OCR text capture.
 * The file repository / versioning lives in Documents (ECM).
 */
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_contracts.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_ui.php';

epc_ctr_ensure_schema($db_link);
$csrfLocal = isset($csrf) ? $csrf : '';
$ctrId     = isset($_GET['ctr']) ? (int) $_GET['ctr'] : 0;
$ctrSummary = epc_ctr_summary($db_link);
$contracts  = epc_ctr_list($db_link, 200);

erp_page_header(
	'<i class="fa fa-file-text-o"></i> Contracts, e-sign &amp; OCR',
	'Contract lifecycle, tamper-evident e-signature ledger and OCR text capture. File storage &amp; versioning live in Documents.',
	array(
		array('label' => 'ERP', 'url' => epc_erp_tab_url($erpUrl, 'dashboard', $date_from_str, $date_to_str)),
		array('label' => 'Contracts'),
	)
);

erp_stat_cards(array(
	array('label' => 'Contracts', 'value' => (string) $ctrSummary['contracts']),
	array('label' => 'Signed', 'value' => (string) $ctrSummary['signed']),
	array('label' => 'Active', 'value' => (string) $ctrSummary['active']),
	array('label' => 'Signed value', 'value' => epc_erp_money($ctrSummary['value']) . ' AED'),
));

$tabBase = epc_erp_tab_url($erpUrl, 'contracts', $date_from_str, $date_to_str);
$sep = strpos($tabBase, '?') === false ? '?' : '&';
?>
<div id="epc_erp_msg" class="alert" style="display:none;"></div>

<div class="row">
	<div class="col-md-4">
		<div class="well well-sm">
			<h5><i class="fa fa-plus-circle"></i> New contract</h5>
			<form id="epc_ctr_new" class="form">
				<input type="hidden" name="csrf_guard_key" value="<?php echo epc_erp_h($csrfLocal); ?>">
				<div class="row">
					<div class="col-xs-5 form-group"><label>Code</label><input type="text" name="code" class="form-control input-sm" placeholder="CTR-001" required></div>
					<div class="col-xs-7 form-group"><label>Title</label><input type="text" name="title" class="form-control input-sm" required></div>
				</div>
				<div class="form-group"><label>Counterparty</label><input type="text" name="counterparty" class="form-control input-sm" placeholder="Customer / supplier"></div>
				<div class="row">
					<div class="col-xs-6 form-group"><label>Value</label><input type="number" step="0.01" name="contract_value" class="form-control input-sm" value="0"></div>
					<div class="col-xs-6 form-group"><label>Currency</label><input type="text" name="currency" class="form-control input-sm" value="AED" maxlength="3"></div>
				</div>
				<div class="row">
					<div class="col-xs-6 form-group"><label>Start</label><input type="date" name="start_date_str" class="form-control input-sm"></div>
					<div class="col-xs-6 form-group"><label>End</label><input type="date" name="end_date_str" class="form-control input-sm"></div>
				</div>
				<div class="form-group"><label>Body / terms</label><textarea name="body_text" class="form-control input-sm" rows="3"></textarea></div>
				<button type="submit" class="btn btn-primary btn-sm">Create contract</button>
			</form>
		</div>
	</div>

	<div class="col-md-8">
		<h5>Contract register</h5>
		<div class="table-responsive">
		<table class="table table-bordered table-condensed">
			<thead><tr><th>Code</th><th>Title</th><th>Counterparty</th><th class="text-right">Value</th><th>Status</th><th>v</th><th>Sigs</th><th></th></tr></thead>
			<tbody>
			<?php if (empty($contracts)): ?>
				<tr><td colspan="8" class="text-muted">No contracts yet. Create one on the left.</td></tr>
			<?php else: foreach ($contracts as $c):
				$lbl = $c['status'] === 'active' ? 'success' : ($c['status'] === 'signed' ? 'info' : ($c['status'] === 'sent' ? 'warning' : ($c['status'] === 'terminated' || $c['status'] === 'expired' ? 'danger' : 'default'))); ?>
				<tr<?php echo $ctrId === (int) $c['id'] ? ' class="info"' : ''; ?>>
					<td><strong><?php echo epc_erp_h($c['code']); ?></strong></td>
					<td><?php echo epc_erp_h($c['title']); ?></td>
					<td><small><?php echo epc_erp_h($c['counterparty']); ?></small></td>
					<td class="text-right"><?php echo epc_erp_money($c['contract_value']); ?></td>
					<td><span class="label label-<?php echo $lbl; ?>"><?php echo epc_erp_h($c['status']); ?></span></td>
					<td><?php echo (int) $c['version']; ?></td>
					<td><?php echo (int) $c['signature_count']; ?></td>
					<td>
						<a class="btn btn-link btn-xs" href="<?php echo epc_erp_h($tabBase . $sep . 'ctr=' . (int) $c['id']); ?>">Open</a>
						<?php if ($c['status'] === 'draft'): ?><button class="btn btn-xs btn-default epc-ctr-status" data-id="<?php echo (int) $c['id']; ?>" data-status="sent">Send</button><?php endif; ?>
						<?php if ($c['status'] === 'signed'): ?><button class="btn btn-xs btn-success epc-ctr-status" data-id="<?php echo (int) $c['id']; ?>" data-status="active">Activate</button><?php endif; ?>
					</td>
				</tr>
			<?php endforeach; endif; ?>
			</tbody>
		</table>
		</div>
	</div>
</div>

<?php if ($ctrId > 0):
	$ctr = epc_ctr_get($db_link, $ctrId);
	if ($ctr):
		$sigs = epc_ctr_signatures($db_link, $ctrId);
?>
<hr>
<div class="epc-erp-section">
	<h4><i class="fa fa-file-text"></i> <?php echo epc_erp_h($ctr['code'] . ' · ' . $ctr['title']); ?>
		<small class="text-muted">v<?php echo (int) $ctr['version']; ?> · <?php echo epc_erp_h($ctr['status']); ?></small>
	</h4>
	<?php if (trim((string) $ctr['body_text']) !== ''): ?>
		<div class="well well-sm" style="white-space:pre-wrap;font-size:12px;max-height:160px;overflow:auto;"><?php echo epc_erp_h($ctr['body_text']); ?></div>
	<?php endif; ?>
</div>

<div class="row">
	<div class="col-md-6">
		<div class="well well-sm">
			<h5><i class="fa fa-pencil-square-o"></i> E-signature</h5>
			<form id="epc_ctr_sign" class="form-inline">
				<input type="hidden" name="csrf_guard_key" value="<?php echo epc_erp_h($csrfLocal); ?>">
				<input type="hidden" name="contract_id" value="<?php echo (int) $ctrId; ?>">
				<input type="text" name="signer_name" class="form-control input-sm" placeholder="Signer name" style="width:160px;" required>
				<input type="email" name="signer_email" class="form-control input-sm" placeholder="Email" style="width:170px;">
				<button type="submit" class="btn btn-primary btn-sm">Sign</button>
			</form>
			<table class="table table-condensed" style="margin-top:8px;">
				<thead><tr><th>Signer</th><th>Signed at</th><th>Hash</th></tr></thead>
				<tbody>
				<?php if (empty($sigs)): ?>
					<tr><td colspan="3" class="text-muted">Not yet signed.</td></tr>
				<?php else: foreach ($sigs as $s): ?>
					<tr><td><?php echo epc_erp_h($s['signer_name']); ?><br><small class="text-muted"><?php echo epc_erp_h($s['signer_email']); ?></small></td><td><small><?php echo epc_erp_h(date('Y-m-d H:i', (int) $s['signed_at'])); ?></small></td><td><code style="font-size:10px;"><?php echo epc_erp_h(substr((string) $s['signature_hash'], 0, 16)); ?>…</code></td></tr>
				<?php endforeach; endif; ?>
				</tbody>
			</table>
		</div>
	</div>

	<div class="col-md-6">
		<div class="well well-sm">
			<h5><i class="fa fa-eye"></i> OCR text capture</h5>
			<p class="text-muted" style="font-size:12px;">Paste text from a scanned document, or wire an OCR engine (Tesseract / Textract / Vision) to populate this. Stored against the contract for full-text search &amp; indexing.</p>
			<form id="epc_ctr_ocr" class="form">
				<input type="hidden" name="csrf_guard_key" value="<?php echo epc_erp_h($csrfLocal); ?>">
				<input type="hidden" name="contract_id" value="<?php echo (int) $ctrId; ?>">
				<div class="form-group"><textarea name="text" class="form-control input-sm" rows="4" placeholder="Extracted document text…"><?php echo epc_erp_h((string) $ctr['ocr_text']); ?></textarea></div>
				<button type="submit" class="btn btn-default btn-sm">Save OCR text</button>
			</form>
		</div>
	</div>
</div>
<?php endif; endif; ?>

<script>
(function(){
	var url = <?php echo json_encode(isset($erpAjaxEndpoint) ? $erpAjaxEndpoint : ('/' . (isset($DP_Config->backend_dir) ? $DP_Config->backend_dir : 'cp') . '/content/shop/finance/erp/ajax_erp_endpoint.php')); ?>;
	var csrf = <?php echo json_encode($csrfLocal); ?>;
	function post(action, fd){ fd.append('action', action); return fetch(url,{method:'POST',body:fd,credentials:'same-origin'}).then(function(r){return r.json();}); }
	function msg(j){ var el=document.getElementById('epc_erp_msg'); if(el){ el.className='alert alert-'+(j.status?'success':'danger'); el.textContent=j.message||''; el.style.display='block'; el.scrollIntoView({behavior:'smooth',block:'center'}); } if(j.status) setTimeout(function(){ location.reload(); }, 800); }
	function bind(id, action){ var f=document.getElementById(id); if(f) f.addEventListener('submit', function(e){ e.preventDefault(); post(action, new FormData(f)).then(msg); }); }
	bind('epc_ctr_new', 'ctr_save');
	bind('epc_ctr_sign', 'ctr_sign');
	bind('epc_ctr_ocr', 'ctr_ocr');
	document.querySelectorAll('.epc-ctr-status').forEach(function(b){ b.addEventListener('click', function(){ var fd=new FormData(); fd.append('csrf_guard_key',csrf); fd.append('id',b.getAttribute('data-id')); fd.append('status',b.getAttribute('data-status')); post('ctr_status', fd).then(msg); }); });
})();
</script>
