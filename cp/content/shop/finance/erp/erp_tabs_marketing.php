<?php
defined('_ASTEXE_') or die('No access');

require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_staff.php';
$campaigns = epc_erp_marketing_list($db_link);
?>

<div class="epc-erp-section">
	<h4><i class="fa fa-bullhorn"></i> Marketing campaigns</h4>
	<p class="text-muted">Track spend, leads and ROI. Finance uses this for expense planning; sales sees lead volume.</p>
	<form id="epc_erp_form_marketing_create" class="form-inline epc-erp-form-inline" style="margin-bottom:12px;">
		<input type="hidden" name="csrf_guard_key" value="<?php echo epc_erp_h(isset($csrf) ? $csrf : ''); ?>">
		<input type="text" name="name" class="form-control input-sm" placeholder="Campaign name" required>
		<input type="text" name="channel" class="form-control input-sm" placeholder="Channel" value="digital">
		<input type="number" step="0.01" name="budget" class="form-control input-sm" placeholder="Budget AED">
		<select name="status" class="form-control input-sm">
			<option value="active">Active</option>
			<option value="draft">Draft</option>
			<option value="paused">Paused</option>
		</select>
		<input type="date" name="time_start" class="form-control input-sm" value="<?php echo epc_erp_h(date('Y-m-d')); ?>">
		<input type="date" name="time_end" class="form-control input-sm" value="<?php echo epc_erp_h(date('Y-m-d', time() + 86400 * 30)); ?>">
		<button type="submit" class="btn btn-sm btn-primary">Create campaign</button>
	</form>
	<table class="table table-striped table-bordered">
		<thead><tr><th>Campaign</th><th>Channel</th><th>Budget</th><th>Spent</th><th>Leads</th><th>CPL</th><th>Status</th><th>Period</th></tr></thead>
		<tbody>
		<?php foreach ($campaigns as $c): ?>
			<?php $cpl = ((int)$c['leads'] > 0) ? round((float)$c['spent'] / (int)$c['leads'], 2) : 0; ?>
			<tr>
				<td><?php echo epc_erp_h($c['name']); ?></td>
				<td><?php echo epc_erp_h($c['channel']); ?></td>
				<td><?php echo epc_erp_money($c['budget']); ?> AED</td>
				<td><?php echo epc_erp_money($c['spent']); ?> AED</td>
				<td><?php echo (int)$c['leads']; ?></td>
				<td><?php echo $cpl > 0 ? epc_erp_money($cpl) . ' AED' : '—'; ?></td>
				<td><span class="label label-<?php echo $c['status'] === 'active' ? 'success' : 'default'; ?>"><?php echo epc_erp_h($c['status']); ?></span></td>
				<td><small><?php echo (int)$c['time_start'] ? epc_erp_h(date('Y-m-d', (int)$c['time_start'])) : '—'; ?> — <?php echo (int)$c['time_end'] ? epc_erp_h(date('Y-m-d', (int)$c['time_end'])) : '—'; ?></small></td>
			</tr>
		<?php endforeach; ?>
		<?php if (empty($campaigns)): ?>
			<tr><td colspan="8" class="text-muted">No campaigns — run staff setup with sample=1</td></tr>
		<?php endif; ?>
		</tbody>
	</table>
</div>
