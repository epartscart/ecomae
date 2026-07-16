<?php
/**
 * ERP — After-sales RMA with Blockchain BOS proof badges.
 */
defined('_ASTEXE_') or die('No access');

require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_aftersales.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_ui.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_blockchain_bos.php';

$viewId = isset($_GET['rma_id']) ? (int) $_GET['rma_id'] : 0;
$detail = $viewId > 0 ? epc_as_rma_get($db_link, $viewId) : null;
$rows = epc_as_rma_list($db_link, 100);

erp_page_header(
	'<i class="fa fa-undo"></i> After-sales RMA',
	'Return merchandise authorisations with Blockchain BOS proof badges — verify authenticity of returns.',
	array(
		array('label' => 'ERP', 'url' => epc_erp_tab_url($erpUrl, 'dashboard', $date_from_str, $date_to_str)),
		array('label' => 'Service', 'url' => epc_erp_tab_url($erpUrl, 'aftersales', $date_from_str, $date_to_str, 'service_mgmt')),
		array('label' => 'After-sales RMA'),
	),
	array(
		array(
			'label' => 'Blockchain proofs',
			'url' => epc_erp_tab_url($erpUrl, 'blockchain_proofs', $date_from_str, $date_to_str),
			'class' => 'btn-default',
			'icon' => 'fa-link',
		),
	)
);

if ($detail):
	$hdr = $detail['header'];
	$lines = $detail['lines'];
	$rmaNo = (string) ($hdr['rma_no'] ?? '');
	$bcBadge = $rmaNo !== '' ? epc_bc_bos_document_badge_html('rma', $rmaNo, array('show_uid' => true)) : '';
	?>
	<p style="margin-bottom:12px">
		<a class="btn btn-default btn-sm" href="<?php echo epc_erp_h(epc_erp_tab_url($erpUrl, 'aftersales', $date_from_str, $date_to_str, 'service_mgmt')); ?>">
			<i class="fa fa-arrow-left"></i> All RMAs
		</a>
	</p>
	<?php if ($bcBadge !== ''): ?>
	<div class="alert alert-info" style="margin-bottom:14px">
		<strong><i class="fa fa-link"></i> Blockchain BOS proof</strong>
		<span style="margin-left:10px"><?php echo $bcBadge; ?></span>
		<a href="<?php echo epc_erp_h(epc_erp_tab_url($erpUrl, 'blockchain_proofs', $date_from_str, $date_to_str) . '&bc_type=rma'); ?>" class="btn btn-default btn-xs" style="margin-left:8px">All RMA proofs</a>
	</div>
	<?php endif; ?>
	<div class="well" style="background:#fff">
		<div class="row">
			<div class="col-sm-6">
				<p><strong><?php echo epc_erp_h($rmaNo); ?></strong><br>
				Status: <span class="label label-default"><?php echo epc_erp_h((string) ($hdr['status'] ?? '')); ?></span>
				· Disposition: <?php echo epc_erp_h((string) ($hdr['disposition'] ?? '')); ?></p>
				<p>Customer #<?php echo (int) ($hdr['customer_id'] ?? 0); ?><br>
				Source: <?php echo epc_erp_h((string) ($hdr['source_type'] ?? '')); ?> #<?php echo (int) ($hdr['source_id'] ?? 0); ?></p>
			</div>
			<div class="col-sm-6">
				<p>Reason: <?php echo epc_erp_h((string) ($hdr['reason'] ?? '—')); ?><br>
				Refund: <?php echo epc_erp_money($hdr['refund_amount'] ?? 0); ?><br>
				Created: <?php echo !empty($hdr['time_created']) ? epc_erp_h(date('Y-m-d H:i', (int) $hdr['time_created'])) : '—'; ?></p>
				<?php if ($bcBadge !== ''): ?><p><?php echo $bcBadge; ?></p><?php endif; ?>
			</div>
		</div>
		<table class="table table-condensed table-bordered">
			<thead><tr><th>Item</th><th>Qty</th><th>Unit price</th><th>Note</th></tr></thead>
			<tbody>
			<?php if (empty($lines)): ?>
				<tr><td colspan="4" class="text-muted">No lines</td></tr>
			<?php else: foreach ($lines as $ln): ?>
				<tr>
					<td>#<?php echo (int) ($ln['item_id'] ?? 0); ?></td>
					<td><?php echo epc_erp_h(number_format((float) ($ln['qty'] ?? 0), 2)); ?></td>
					<td><?php echo epc_erp_money($ln['unit_price'] ?? 0); ?></td>
					<td><?php echo epc_erp_h((string) ($ln['condition_note'] ?? '')); ?></td>
				</tr>
			<?php endforeach; endif; ?>
			</tbody>
		</table>
	</div>
	<?php
	return;
endif;
?>

<div class="epc-erp-section">
	<h4><i class="fa fa-list"></i> Recent RMAs</h4>
	<table class="table table-striped table-bordered table-condensed">
		<thead>
			<tr>
				<th>ID</th>
				<th>RMA #</th>
				<th>Customer</th>
				<th>Status</th>
				<th>Lines</th>
				<th>Created</th>
				<th>Blockchain</th>
				<th></th>
			</tr>
		</thead>
		<tbody>
		<?php if (empty($rows)): ?>
			<tr><td colspan="8" class="text-muted">No RMAs yet — create one below.</td></tr>
		<?php else: foreach ($rows as $r):
			$rmaNo = (string) ($r['rma_no'] ?? '');
			$badge = $rmaNo !== '' ? epc_bc_bos_document_badge_html('rma', $rmaNo) : '';
			$viewUrl = epc_erp_tab_url($erpUrl, 'aftersales', $date_from_str, $date_to_str, 'service_mgmt') . '&rma_id=' . (int) $r['id'];
			?>
			<tr>
				<td><?php echo (int) $r['id']; ?></td>
				<td><code><?php echo epc_erp_h($rmaNo); ?></code></td>
				<td>#<?php echo (int) ($r['customer_id'] ?? 0); ?></td>
				<td><span class="label label-default"><?php echo epc_erp_h((string) ($r['status'] ?? '')); ?></span></td>
				<td><?php echo (int) ($r['line_count'] ?? 0); ?></td>
				<td><?php echo !empty($r['time_created']) ? epc_erp_h(date('Y-m-d', (int) $r['time_created'])) : '—'; ?></td>
				<td><?php echo $badge !== '' ? $badge : '<span class="text-muted">—</span>'; ?></td>
				<td><a class="btn btn-default btn-xs" href="<?php echo epc_erp_h($viewUrl); ?>">View</a></td>
			</tr>
		<?php endforeach; endif; ?>
		</tbody>
	</table>
</div>

<div class="epc-erp-section">
	<h4><i class="fa fa-plus"></i> Create RMA</h4>
	<p class="text-muted">Creates an after-sales return and records a Blockchain proof when mode is not off. Lines CSV: <code>item_id,qty,unit_price,note</code></p>
	<form id="epc_erp_form_as_rma" class="form-horizontal" style="max-width:720px">
		<input type="hidden" name="csrf_guard_key" value="<?php echo epc_erp_h($csrf); ?>">
		<div class="form-group form-inline">
			<input type="text" name="rma_no" class="form-control input-sm" placeholder="RMA no. (optional)">
			<input type="number" name="customer_id" class="form-control input-sm" placeholder="Customer ID" required>
			<input type="number" name="source_id" class="form-control input-sm" placeholder="Source order ID">
			<input type="text" name="reason" class="form-control input-sm" placeholder="Reason">
		</div>
		<textarea name="lines_csv" class="form-control input-sm" rows="4" placeholder="item_id,qty,unit_price,note&#10;101,1,50.00,defective" required></textarea>
		<label class="checkbox-inline" style="margin:10px 0"><input type="checkbox" name="restock" value="1" checked> Restock on resolve</label>
		<br>
		<button type="submit" class="btn btn-sm btn-primary">Create RMA</button>
	</form>
</div>

<script>
(function(){
	var form = document.getElementById('epc_erp_form_as_rma');
	if (!form) return;
	form.addEventListener('submit', function(e){
		e.preventDefault();
		var fn = (typeof window.epcErpPost === 'function') ? window.epcErpPost : (typeof postAction === 'function' ? postAction : null);
		if (fn) fn('as_rma_create', form);
	});
})();
</script>
