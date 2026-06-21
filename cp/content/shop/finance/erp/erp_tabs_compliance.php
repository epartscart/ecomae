<?php
/**
 * ERP tab — BOS Compliance pillar: obligations, filing calendar, retention.
 */
defined('_ASTEXE_') or die('No access');

require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_bos_compliance.php';

epc_bos_compliance_seed($db_link);

$asOf = isset($date_to) && (int) $date_to > 0 ? (int) $date_to : time();
$cpanel = isset($_GET['cmp_panel']) ? (string) $_GET['cmp_panel'] : 'calendar';
$summary = epc_bos_compliance_summary($db_link, $asOf);
$calendar = epc_bos_compliance_calendar($db_link, $asOf);
$obligations = epc_bos_compliance_obligations($db_link);
$retention = epc_bos_retention_rules($db_link);
$cmpCountry = strtoupper((string) epc_bos_compliance_company_country($db_link));
$cmpDnfbp = epc_bos_compliance_dnfbp_profile($db_link);
$cmpSync = epc_bos_compliance_sync_status($db_link);
$csrfLocal = isset($csrf) ? $csrf : '';

$cmpUrl = function ($panel) use ($erpUrl, $date_from_str, $date_to_str) {
	return epc_erp_h(epc_erp_tab_url($erpUrl, 'compliance', $date_from_str, $date_to_str, 'finance') . '&cmp_panel=' . $panel);
};
$statusLabel = array(
	'overdue' => array('danger', 'Overdue'),
	'due_soon' => array('warning', 'Due soon'),
	'open' => array('default', 'Open'),
	'filed' => array('success', 'Filed'),
	'waived' => array('info', 'Waived'),
);
?>

<div class="epc-erp-section">
	<div class="alert alert-info" style="margin-bottom:14px;">
		<div class="pull-right" style="text-align:right;">
			<form data-bos-action="bos_compliance_fetch" style="display:inline;">
				<input type="hidden" name="csrf_guard_key" value="<?php echo epc_erp_h($csrfLocal); ?>">
				<button class="btn btn-sm btn-primary" type="submit"><i class="fa fa-cloud-download"></i> Fetch latest updates</button>
			</form>
			<div class="text-muted" style="font-size:11px;margin-top:4px;">
				Catalog <strong><?php echo epc_erp_h($cmpSync['current']); ?></strong>
				<?php if ($cmpSync['up_to_date']): ?><span class="label label-success">up to date</span><?php else: ?><span class="label label-warning">update available</span><?php endif; ?>
				<?php if ((int) $cmpSync['last_fetch'] > 0): ?><br>Last fetched <?php echo epc_erp_h(date('d M Y H:i', (int) $cmpSync['last_fetch'])); ?><?php endif; ?>
			</div>
		</div>
		<strong><i class="fa fa-shield"></i> Compliance pillar</strong> — config-driven obligations, filing calendar and document retention, keyed to your tax jurisdiction.
		Tax area / country: <strong><?php echo epc_erp_h($cmpCountry); ?></strong><?php if ($cmpDnfbp['is_dnfbp']): ?> · <span class="label label-warning">AML/CFT applies (DNFBP)</span><?php endif; ?>.
		Add, edit or disable any item. As-at date: <strong><?php echo epc_erp_h(date('d M Y', $asOf)); ?></strong>.
	</div>

	<div class="row" style="margin-bottom:16px;">
		<div class="col-sm-3"><div class="epc-erp-kpi" style="border-left:4px solid #c0392b;padding:10px 14px;background:#fff;border-radius:6px;box-shadow:0 1px 3px rgba(0,0,0,.08);"><div style="font-size:24px;font-weight:700;"><?php echo (int) $summary['overdue']; ?></div><div class="text-muted">Overdue</div></div></div>
		<div class="col-sm-3"><div class="epc-erp-kpi" style="border-left:4px solid #e67e22;padding:10px 14px;background:#fff;border-radius:6px;box-shadow:0 1px 3px rgba(0,0,0,.08);"><div style="font-size:24px;font-weight:700;"><?php echo (int) $summary['due_soon']; ?></div><div class="text-muted">Due soon (14d)</div></div></div>
		<div class="col-sm-3"><div class="epc-erp-kpi" style="border-left:4px solid #2980b9;padding:10px 14px;background:#fff;border-radius:6px;box-shadow:0 1px 3px rgba(0,0,0,.08);"><div style="font-size:24px;font-weight:700;"><?php echo (int) $summary['open']; ?></div><div class="text-muted">Open</div></div></div>
		<div class="col-sm-3"><div class="epc-erp-kpi" style="border-left:4px solid #27ae60;padding:10px 14px;background:#fff;border-radius:6px;box-shadow:0 1px 3px rgba(0,0,0,.08);"><div style="font-size:24px;font-weight:700;"><?php echo (int) $summary['filed']; ?></div><div class="text-muted">Filed</div></div></div>
	</div>

	<ul class="nav nav-pills" style="margin-bottom:16px;">
		<li class="<?php echo $cpanel === 'calendar' ? 'active' : ''; ?>"><a href="<?php echo $cmpUrl('calendar'); ?>"><i class="fa fa-calendar"></i> Filing calendar</a></li>
		<li class="<?php echo $cpanel === 'obligations' ? 'active' : ''; ?>"><a href="<?php echo $cmpUrl('obligations'); ?>"><i class="fa fa-list-alt"></i> Obligations</a></li>
		<li class="<?php echo $cpanel === 'retention' ? 'active' : ''; ?>"><a href="<?php echo $cmpUrl('retention'); ?>"><i class="fa fa-archive"></i> Document retention</a></li>
	</ul>

<?php if ($cpanel === 'calendar'): ?>
	<table class="table table-bordered table-condensed">
		<thead><tr><th>Obligation</th><th>Regime</th><th>Authority</th><th>Period</th><th>Due</th><th>Status</th><th>Documents</th><th></th></tr></thead>
		<tbody>
		<?php foreach ($calendar as $c): $sl = $statusLabel[$c['status']] ?? array('default', $c['status']); ?>
			<tr>
				<td><strong><?php echo epc_erp_h($c['title']); ?></strong></td>
				<td><span class="label label-default"><?php echo epc_erp_h($c['regime']); ?></span></td>
				<td><small><?php echo epc_erp_h($c['authority']); ?></small></td>
				<td><?php echo epc_erp_h($c['period_label']); ?></td>
				<td><?php echo epc_erp_h(date('d M Y', (int) $c['due_date'])); ?></td>
				<td><span class="label label-<?php echo $sl[0]; ?>"><?php echo epc_erp_h($sl[1]); ?></span></td>
				<td><small class="text-muted"><?php echo epc_erp_h($c['doc_requirements']); ?></small></td>
				<td style="white-space:nowrap;">
					<?php if ($c['status'] !== 'filed'): ?>
					<form class="form-inline" data-bos-action="bos_compliance_file" style="display:inline;">
						<input type="hidden" name="csrf_guard_key" value="<?php echo epc_erp_h($csrfLocal); ?>">
						<input type="hidden" name="obligation_id" value="<?php echo (int) $c['obligation_id']; ?>">
						<input type="hidden" name="period_label" value="<?php echo epc_erp_h($c['period_label']); ?>">
						<input type="hidden" name="period_end" value="<?php echo (int) $c['period_end']; ?>">
						<input type="hidden" name="due_date" value="<?php echo (int) $c['due_date']; ?>">
						<input type="hidden" name="status" value="filed">
						<input type="text" name="reference" class="form-control input-sm" placeholder="Ref." style="width:90px;">
						<button class="btn btn-xs btn-success" type="submit">Mark filed</button>
					</form>
					<?php else: ?>
					<small class="text-success">✓ <?php echo epc_erp_h($c['reference']); ?></small>
					<?php endif; ?>
				</td>
			</tr>
		<?php endforeach; ?>
		<?php if (empty($calendar)): ?><tr><td colspan="8" class="text-muted">No obligations configured.</td></tr><?php endif; ?>
		</tbody>
	</table>

<?php elseif ($cpanel === 'obligations'): ?>
	<form class="form-inline epc-erp-form-inline" data-bos-action="bos_compliance_add_obligation" style="margin-bottom:14px;">
		<input type="hidden" name="csrf_guard_key" value="<?php echo epc_erp_h($csrfLocal); ?>">
		<input type="text" name="title" class="form-control input-sm" placeholder="Obligation title" required>
		<input type="text" name="regime" class="form-control input-sm" placeholder="Regime (VAT/CT/...)">
		<input type="text" name="authority" class="form-control input-sm" placeholder="Authority">
		<select name="frequency" class="form-control input-sm">
			<option value="monthly">Monthly</option>
			<option value="quarterly">Quarterly</option>
			<option value="annual">Annual</option>
			<option value="one_off">One-off</option>
		</select>
		<input type="number" name="lead_days" class="form-control input-sm" placeholder="Lead days" value="28" style="width:90px;">
		<input type="text" name="doc_requirements" class="form-control input-sm" placeholder="Documents required" style="width:200px;">
		<button class="btn btn-sm btn-primary" type="submit">Add obligation</button>
	</form>
	<table class="table table-bordered table-condensed">
		<thead><tr><th>Title</th><th>Regime</th><th>Authority</th><th>Frequency</th><th>Lead days</th><th>Documents</th><th></th></tr></thead>
		<tbody>
		<?php foreach ($obligations as $o): ?>
			<tr>
				<td><strong><?php echo epc_erp_h($o['title']); ?></strong></td>
				<td><?php echo epc_erp_h($o['regime']); ?></td>
				<td><small><?php echo epc_erp_h($o['authority']); ?></small></td>
				<td><?php echo epc_erp_h($o['frequency']); ?></td>
				<td><?php echo (int) $o['lead_days']; ?></td>
				<td><small class="text-muted"><?php echo epc_erp_h($o['doc_requirements']); ?></small></td>
				<td>
					<form data-bos-action="bos_compliance_disable_obligation" style="display:inline;" onsubmit="return confirm('Disable this obligation?');">
						<input type="hidden" name="csrf_guard_key" value="<?php echo epc_erp_h($csrfLocal); ?>">
						<input type="hidden" name="id" value="<?php echo (int) $o['id']; ?>">
						<button class="btn btn-xs btn-default" type="submit">Disable</button>
					</form>
				</td>
			</tr>
		<?php endforeach; ?>
		</tbody>
	</table>

<?php else: ?>
	<form class="form-inline epc-erp-form-inline" data-bos-action="bos_compliance_save_retention" style="margin-bottom:14px;">
		<input type="hidden" name="csrf_guard_key" value="<?php echo epc_erp_h($csrfLocal); ?>">
		<input type="text" name="label" class="form-control input-sm" placeholder="Document type" required>
		<input type="number" name="retention_years" class="form-control input-sm" placeholder="Years" value="5" style="width:80px;">
		<input type="text" name="basis" class="form-control input-sm" placeholder="Basis (from ...)">
		<input type="text" name="legal_ref" class="form-control input-sm" placeholder="Legal reference">
		<button class="btn btn-sm btn-primary" type="submit">Save rule</button>
	</form>
	<table class="table table-bordered table-condensed">
		<thead><tr><th>Document type</th><th>Retention</th><th>Basis</th><th>Legal reference</th></tr></thead>
		<tbody>
		<?php foreach ($retention as $r): ?>
			<tr>
				<td><strong><?php echo epc_erp_h($r['label']); ?></strong></td>
				<td><span class="label label-info"><?php echo (int) $r['retention_years']; ?> years</span></td>
				<td><?php echo epc_erp_h($r['basis']); ?></td>
				<td><small class="text-muted"><?php echo epc_erp_h($r['legal_ref']); ?></small></td>
			</tr>
		<?php endforeach; ?>
		</tbody>
	</table>
<?php endif; ?>
</div>
