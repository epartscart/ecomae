<?php
/**
 * ERP tab — BOS Industry Intelligence pillar: per-industry KPIs & controls.
 */
defined('_ASTEXE_') or die('No access');

require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_bos_intelligence.php';

$ctx = epc_bos_intel_context($db_link);
$dFrom = isset($date_from) ? (int) $date_from : strtotime(date('Y-m-01'));
$dTo = isset($date_to) ? (int) $date_to : time();
$kpis = epc_bos_intel_kpis($db_link, $dFrom, $dTo);
$controls = epc_bos_intel_controls($db_link, $ctx);
$state = epc_bos_intel_control_state($db_link);
$csrfLocal = isset($csrf) ? $csrf : '';

$industryLabel = $ctx['pack_label'] !== '' ? $ctx['pack_label'] : ($ctx['profile_label'] !== '' ? $ctx['profile_label'] : 'General (no industry pack applied)');
$healthColor = array('good' => '#27ae60', 'warn' => '#e67e22', 'bad' => '#c0392b', 'info' => '#2980b9');
$doneCount = 0;
foreach ($controls as $c) {
	if (!empty($state[$c['code']])) {
		$doneCount++;
	}
}
?>

<div class="epc-erp-section">
	<div class="alert alert-info" style="margin-bottom:14px;">
		<strong><i class="fa fa-line-chart"></i> Industry intelligence pillar</strong> — operational KPIs and recommended controls driven by your industry profile.
		Active industry: <strong><?php echo epc_erp_h($industryLabel); ?></strong>. KPIs computed live for
		<strong><?php echo epc_erp_h(date('d M Y', $dFrom)); ?> – <?php echo epc_erp_h(date('d M Y', $dTo)); ?></strong>.
		<?php if ($ctx['profile_key'] === '' && $ctx['pack_key'] === ''): ?>
		<br><em>Apply an industry pack in Accounting setup to unlock specialised controls.</em>
		<?php endif; ?>
	</div>

	<h4 style="margin-top:0;"><i class="fa fa-tachometer"></i> Operational KPIs</h4>
	<div class="row" style="margin-bottom:8px;">
		<?php foreach ($kpis as $k): $col = $healthColor[$k['health']] ?? '#2980b9'; ?>
		<div class="col-sm-3" style="margin-bottom:14px;">
			<div style="border-left:4px solid <?php echo $col; ?>;padding:12px 14px;background:#fff;border-radius:6px;box-shadow:0 1px 3px rgba(0,0,0,.08);min-height:84px;">
				<div style="font-size:13px;color:#666;"><?php echo epc_erp_h($k['label']); ?></div>
				<div style="font-size:22px;font-weight:700;color:<?php echo $col; ?>;"><?php echo epc_erp_h(epc_bos_intel_format((float) $k['value'], (string) $k['format'])); ?></div>
				<div style="font-size:11px;color:#999;"><?php echo epc_erp_h($k['hint']); ?></div>
			</div>
		</div>
		<?php endforeach; ?>
	</div>

	<h4><i class="fa fa-check-square-o"></i> Recommended controls
		<small class="text-muted">(<?php echo $doneCount; ?>/<?php echo count($controls); ?> in place)</small>
	</h4>
	<p class="text-muted">Best-practice operational controls for your industry. Tick the ones you have in place; the checklist is saved per tenant.</p>
	<table class="table table-bordered table-condensed">
		<thead><tr><th style="width:60px;">In place</th><th>Control</th><th>What to do</th></tr></thead>
		<tbody>
		<?php foreach ($controls as $c): $checked = !empty($state[$c['code']]); ?>
			<tr class="<?php echo $checked ? 'success' : ''; ?>">
				<td style="text-align:center;">
					<form data-bos-action="bos_intel_toggle_control" style="margin:0;">
						<input type="hidden" name="csrf_guard_key" value="<?php echo epc_erp_h($csrfLocal); ?>">
						<input type="hidden" name="code" value="<?php echo epc_erp_h($c['code']); ?>">
						<input type="hidden" name="checked" value="<?php echo $checked ? '0' : '1'; ?>">
						<button type="submit" class="btn btn-xs <?php echo $checked ? 'btn-success' : 'btn-default'; ?>">
							<i class="fa fa-<?php echo $checked ? 'check-square-o' : 'square-o'; ?>"></i>
						</button>
					</form>
				</td>
				<td><strong><?php echo epc_erp_h($c['title']); ?></strong></td>
				<td><small class="text-muted"><?php echo epc_erp_h($c['desc']); ?></small></td>
			</tr>
		<?php endforeach; ?>
		</tbody>
	</table>
</div>
