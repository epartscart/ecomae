<?php
defined('_ASTEXE_') or die('No access');
/**
 * Labour law & compliance — country-aware statutory employment-law reference
 * plus a per-employee compliance monitor.
 *
 * The active country is taken from the company profile and localizes the whole
 * statutory rule-set (working hours, overtime, probation, notice, leave,
 * end-of-service, wage protection). A worldwide reference table and a country
 * preview let HR look up any jurisdiction. The compliance monitor runs every
 * employee through their country's rules and flags issues + accrued
 * end-of-service liability. Informational only — verify with local counsel.
 *
 * Backed by epc_hr_* in epc_erp_hr_law.php.
 */
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_staff.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_hr_law.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_localization.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_company.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_ui.php';

$hrRows = epc_erp_hr_list($db_link);

// Resolve the tenant country once → localizes the entire labour-law pack.
$erpCo = function_exists('epc_co_profile_get') ? epc_co_profile_get($db_link) : array();
$erpCountry = !empty($erpCo['country']) ? strtoupper(substr((string) $erpCo['country'], 0, 2)) : 'AE';
$locProf = function_exists('epc_country_profile') ? epc_country_profile($erpCountry) : array('currency' => 'AED', 'hr_country' => 'AE');
$hrCountry = !empty($locProf['hr_country']) ? (string) $locProf['hr_country'] : $erpCountry;
$curr = !empty($locProf['currency']) ? (string) $locProf['currency'] : 'AED';

// Country to preview in the statutory card (defaults to the tenant country).
$viewCode = isset($_GET['law_country']) ? strtoupper(preg_replace('/[^A-Za-z]/', '', (string) $_GET['law_country'])) : $hrCountry;
$allProfiles = epc_hr_law_profiles_all();
if ($viewCode === '' || !isset($allProfiles[$viewCode])) {
	$viewCode = isset($allProfiles[$hrCountry]) ? $hrCountry : 'generic';
}
$lawAsOf = !empty($date_to) ? (int) $date_to : time();
if ($lawAsOf <= 0) {
	$lawAsOf = (int) strtotime((string) ($date_to_str ?? 'now'));
}
if ($lawAsOf <= 0) {
	$lawAsOf = time();
}
$view = epc_hr_law_profile($viewCode, $lawAsOf);
$pol = epc_hr_policy($viewCode);

$baseUrl = epc_erp_tab_url($erpUrl, 'hr_law', $date_from_str, $date_to_str);
$sep = (strpos($baseUrl, '?') !== false) ? '&' : '?';

// Official government / labour-authority source for the previewed country, plus
// a "fetch / check for updates" link that re-pulls the built-in pack and opens
// the official source so the figures can be verified worldwide.
$officialUrl = (string) ($view['authority_url'] ?? '');
$refreshUrl = $baseUrl . $sep . 'law_country=' . urlencode($viewCode) . '&law_fetch=' . time();
$lawFetched = isset($_GET['law_fetch']);
$packLabel = (string) ($view['pack_label'] ?? '');
$isAePack = ($viewCode === 'AE');
$wps340Live = $isAePack && $lawAsOf >= (int) strtotime('2026-06-01');

erp_page_header(
	'<i class="fa fa-gavel"></i> Labour law &amp; compliance',
	'Country-aware statutory employment law and a per-employee compliance monitor — auto-localized to the company country.',
	array(
		array('label' => 'ERP', 'url' => epc_erp_tab_url($erpUrl, 'dashboard', $date_from_str, $date_to_str)),
		array('label' => 'People'),
		array('label' => 'Labour law & compliance'),
	)
);

// ---- compliance pass over all employees (tenant country) -------------------
$totLiability = 0.0;
$nWarn = 0;
$nError = 0;
$nProbation = 0;
$rows = array();
foreach ($hrRows as $h) {
	$emp = array(
		'hire_date' => (int) ($h['hire_date'] ?? 0),
		'basic_salary' => (float) ($h['basic_salary'] ?? 0),
		'allowances' => (float) ($h['allowances'] ?? 0),
		'leave_balance_days' => (float) ($h['leave_balance_days'] ?? 0),
		'name' => (string) ($h['display_name'] ?? ''),
	);
	$chk = epc_hr_compliance_check($hrCountry, $emp, $lawAsOf);
	$worst = epc_hr_compliance_worst_severity($chk['flags']);
	$totLiability += (float) $chk['eos_liability'];
	if ($worst === 'warn') { $nWarn++; }
	if ($worst === 'error') { $nError++; }
	if (!empty($chk['in_probation'])) { $nProbation++; }
	$rows[] = array('h' => $h, 'chk' => $chk, 'worst' => $worst);
}

erp_stat_cards(array(
	array('label' => 'Statutory country', 'value' => $view['name'] . ' (' . $viewCode . ')'),
	array('label' => 'Employees checked', 'value' => (string) count($rows)),
	array('label' => 'In probation', 'value' => (string) $nProbation),
	array('label' => 'Needs attention', 'value' => (string) ($nWarn + $nError), 'class' => ($nWarn + $nError) > 0 ? 'red' : 'green'),
	array('label' => 'End-of-service liability', 'value' => epc_erp_money($totLiability) . ' ' . $curr),
));

$sevBadge = function ($sev) {
	$map = array(
		'ok' => array('label', 'success', 'OK'),
		'info' => array('label', 'info', 'Info'),
		'warn' => array('label', 'warning', 'Review'),
		'error' => array('label', 'danger', 'Action'),
	);
	$m = $map[$sev] ?? $map['ok'];
	return '<span class="label label-' . $m[1] . '">' . $m[2] . '</span>';
};
?>

<div class="alert alert-info" style="margin-top:10px;">
	<i class="fa fa-info-circle"></i>
	<strong>Informational compliance aid.</strong> Statutory figures are representative minimums localized from the company country
	(<strong><?php echo epc_erp_h($locProf['name'] ?? $erpCountry); ?></strong>), resolved as of
	<strong><?php echo epc_erp_h(date('d M Y', $lawAsOf)); ?></strong>. Always confirm against the current local law,
	any collective/contractual agreement and qualified counsel before acting.
</div>

<?php if ($isAePack): ?>
<div class="epc-erp-section" style="margin-bottom:14px;background:<?php echo $wps340Live ? '#f3fafd' : '#fff8e8'; ?>;border:1px solid <?php echo $wps340Live ? '#0b6e99' : '#d4a017'; ?>;border-radius:8px;padding:12px 16px;">
	<div style="font-weight:800;color:<?php echo $wps340Live ? '#0b6e99' : '#8a6d1b'; ?>;font-size:14px;">
		<i class="fa fa-balance-scale"></i> UAE labour-law pack refresh — <?php echo epc_erp_h($packLabel !== '' ? $packLabel : 'FDL 33/2021'); ?>
	</div>
	<div class="text-muted" style="font-size:12px;margin-top:6px;line-height:1.55;">
		<strong>Governing law:</strong> Federal Decree-Law 33/2021 (as amended by FDL 20/2023 &amp; FDL 9/2024) + Executive Regulations.
		&nbsp;·&nbsp; <strong>WPS:</strong>
		<?php if ($wps340Live): ?>
			<strong>Ministerial Resolution 340/2026</strong> is in force — prior month’s wages due on the <strong>1st</strong> of each Gregorian month; establishment compliant at ≥<strong>85%</strong> of wages transferred; new hires on WPS from the first pay cycle; MOHRE escalation from day 2.
		<?php else: ?>
			Ministerial Resolution 598/2022 still applies until <strong>31 May 2026</strong>; from <strong>1 Jun 2026</strong> MR 340/2026 switches the due date to the 1st and raises the threshold to 85%.
		<?php endif; ?>
		&nbsp;·&nbsp; <strong>Emirati private-sector minimum wage</strong> AED 6,000/month (new/renewed permits from 1 Jan 2026).
		&nbsp;·&nbsp; Fixed-term contracts only · probation ≤6 months (14-day notice) · EOS Arts. 51–52.
	</div>
</div>
<?php endif; ?>

<div class="epc-erp-section">
	<form method="get" class="form-inline" style="margin-bottom:12px;">
		<?php foreach (array('area' => 'people', 'tab' => 'hr_law', 'from' => $date_from_str, 'to' => $date_to_str) as $k => $v): ?>
			<input type="hidden" name="<?php echo epc_erp_h($k); ?>" value="<?php echo epc_erp_h($v); ?>">
		<?php endforeach; ?>
		<label><i class="fa fa-globe"></i> Look up a country:</label>
		<select name="law_country" class="form-control input-sm" onchange="this.form.submit()" style="min-width:240px;">
			<?php foreach (epc_hr_law_countries() as $c): ?>
				<option value="<?php echo epc_erp_h($c['code']); ?>" <?php echo $c['code'] === $viewCode ? 'selected' : ''; ?>>
					<?php echo epc_erp_h($c['name'] . ($c['code'] !== 'generic' ? ' (' . $c['code'] . ')' : '') . ($c['region'] ? ' — ' . $c['region'] : '')); ?>
				</option>
			<?php endforeach; ?>
		</select>
		<noscript><button type="submit" class="btn btn-default btn-sm">Show</button></noscript>
		<?php if ($viewCode !== $hrCountry): ?>
			<a class="btn btn-link btn-sm" href="<?php echo epc_erp_h($baseUrl); ?>">Back to company country (<?php echo epc_erp_h($hrCountry); ?>)</a>
		<?php endif; ?>
	</form>

	<div style="margin:-2px 0 14px;">
		<a class="btn btn-primary btn-sm" href="<?php echo epc_erp_h($refreshUrl); ?>" title="Re-pull the statutory pack and verify against the official source">
			<i class="fa fa-refresh"></i> Fetch / check for updates
		</a>
		<?php if ($officialUrl !== ''): ?>
			<a class="btn btn-default btn-sm" href="<?php echo epc_erp_h($officialUrl); ?>" target="_blank" rel="noopener noreferrer" title="Open the official government / labour-authority website">
				<i class="fa fa-external-link"></i> Official source — <?php echo epc_erp_h($view['name']); ?>
			</a>
		<?php endif; ?>
		<?php if ($lawFetched): ?>
			<span class="text-success" style="margin-left:6px;"><i class="fa fa-check-circle"></i> Synced from the built-in statutory pack<?php echo $packLabel !== '' ? ' (' . epc_erp_h($packLabel) . ')' : ''; ?> · <?php echo date('d M Y H:i'); ?> — as of <?php echo epc_erp_h(date('d M Y', $lawAsOf)); ?>. Verify on the official source.</span>
		<?php endif; ?>
	</div>

	<h4><i class="fa fa-balance-scale"></i> Statutory employment law — <?php echo epc_erp_h($view['name']); ?>
		<small class="text-muted"><?php echo epc_erp_h($view['region'] ?? ''); ?><?php echo $packLabel !== '' ? ' · ' . epc_erp_h($packLabel) : ''; ?></small>
	</h4>
	<div class="row">
		<?php
		$probNoticeDays = (int) ($view['probation_notice_days'] ?? $pol['probation_notice_days'] ?? 0);
		$cards = array(
			array('fa-clock-o', 'Working week', $view['weekly_hours'] . ' h/week', $view['workweek']),
			array('fa-bolt', 'Overtime', $view['overtime'], ''),
			array('fa-hourglass-half', 'Probation (max)', ((int) $view['probation_max_months'] > 0 ? $view['probation_max_months'] . ' months' : 'No statutory cap'), ($probNoticeDays > 0 ? 'Employer notice during probation ≥' . $probNoticeDays . ' days' : '')),
			array('fa-bell', 'Notice period', ((int) $view['notice_days'] > 0 ? $view['notice_days'] . ' days' : 'No statutory minimum'), ''),
			array('fa-plane', 'Annual leave', ((float) $view['annual_leave_days'] > 0 ? $view['annual_leave_days'] . ' days/yr' : 'No statutory minimum'), ''),
			array('fa-medkit', 'Sick leave', $view['sick_leave'], ''),
			array('fa-child', 'Maternity', $view['maternity'], ''),
			array('fa-male', 'Parental / paternity', $view['paternity'], ''),
			array('fa-calendar', 'Public holidays', $view['public_holidays'], ''),
			array('fa-money', 'End of service', $view['eos'], ''),
			array('fa-university', 'Wage protection (WPS)', $view['wage_protection'], ($isAePack && !empty($view['wps_due']) ? 'Due: ' . $view['wps_due'] . ' · threshold ≥' . (int) ($view['wps_threshold_pct'] ?? 0) . '%' : '')),
			array('fa-institution', 'Governing law', $view['authority'], (!empty($view['key_articles']) ? (string) $view['key_articles'] : ''), $officialUrl),
		);
		if ($isAePack && !empty($view['contract_model'])) {
			$cards[] = array('fa-file-text-o', 'Contract model', (string) $view['contract_model'], ((float) ($view['emirati_min_wage'] ?? 0) > 0 ? 'Emirati private-sector min. wage AED ' . number_format((float) $view['emirati_min_wage'], 0) . '/month (new/renewed permits)' : ''));
		}
		foreach ($cards as $c):
			$cLink = isset($c[4]) ? (string) $c[4] : '';
		?>
			<div class="col-md-4 col-sm-6" style="margin-bottom:14px;">
				<div class="well well-sm" style="margin-bottom:0;height:100%;">
					<div class="text-muted" style="font-size:11px;text-transform:uppercase;letter-spacing:.5px;"><i class="fa <?php echo epc_erp_h($c[0]); ?>"></i> <?php echo epc_erp_h($c[1]); ?></div>
					<div style="font-weight:600;margin-top:3px;"><?php echo epc_erp_h($c[2]); ?></div>
					<?php if ($c[3] !== ''): ?><div class="text-muted" style="font-size:12px;"><?php echo epc_erp_h($c[3]); ?></div><?php endif; ?>
					<?php if ($cLink !== ''): ?><div style="font-size:12px;margin-top:3px;"><a href="<?php echo epc_erp_h($cLink); ?>" target="_blank" rel="noopener noreferrer"><i class="fa fa-external-link"></i> Official source</a></div><?php endif; ?>
				</div>
			</div>
		<?php endforeach; ?>
	</div>
</div>

<div class="epc-erp-section">
	<h4><i class="fa fa-user-check"></i> Employee compliance monitor
		<small class="text-muted">— <?php echo epc_erp_h($locProf['name'] ?? $erpCountry); ?> rules · amounts in <?php echo epc_erp_h($curr); ?></small>
	</h4>
	<p class="text-muted">Every employee is checked against the company-country statutory rules. End-of-service shows the accrued liability where the jurisdiction uses an accrual-based gratuity; severance-only jurisdictions show the basis instead.</p>
	<div class="table-responsive">
	<table class="table table-striped table-bordered table-condensed">
		<thead><tr>
			<th>Name</th><th>Department</th><th>Joined</th><th>Service</th>
			<th>Status</th><th>End-of-service (accrued)</th><th>Findings</th>
		</tr></thead>
		<tbody>
		<?php foreach ($rows as $r):
			$h = $r['h']; $chk = $r['chk'];
			$hire = (int) ($h['hire_date'] ?? 0);
		?>
			<tr>
				<td><?php echo epc_erp_h($h['display_name']); ?></td>
				<td><?php echo epc_erp_h(epc_erp_staff_department_name($h['department_code'] ?? '')); ?></td>
				<td><?php echo $hire > 0 ? date('d M Y', $hire) : '—'; ?></td>
				<td><?php echo number_format((float) $chk['service_years'], 1); ?> yr</td>
				<td><?php echo $sevBadge($r['worst']); ?> <?php echo !empty($chk['in_probation']) ? '<span class="label label-default">Probation</span>' : ''; ?></td>
				<td>
					<?php if (!empty($chk['eos_eligible']) && (float) $chk['eos_liability'] > 0): ?>
						<strong><?php echo epc_erp_money($chk['eos_liability']); ?></strong> <?php echo epc_erp_h($curr); ?>
					<?php else: ?>
						<span class="text-muted">—</span>
					<?php endif; ?>
				</td>
				<td>
					<ul class="list-unstyled" style="margin-bottom:0;">
						<?php foreach ($chk['flags'] as $f): ?>
							<li style="margin-bottom:3px;">
								<?php echo $sevBadge((string) $f['severity']); ?>
								<?php echo epc_erp_h($f['message']); ?>
								<?php if (!empty($f['basis'])): ?><br><small class="text-muted" style="margin-left:4px;"><i class="fa fa-book"></i> <?php echo epc_erp_h($f['basis']); ?></small><?php endif; ?>
							</li>
						<?php endforeach; ?>
					</ul>
				</td>
			</tr>
		<?php endforeach; ?>
		<?php if (empty($rows)): ?>
			<tr><td colspan="7" class="text-muted">No HR records — add employees in HR operations or run staff setup.</td></tr>
		<?php endif; ?>
		</tbody>
	</table>
	</div>
</div>

<div class="epc-erp-section">
	<h4><i class="fa fa-globe"></i> Worldwide statutory reference</h4>
	<p class="text-muted">Built-in labour-law packs across <?php echo (int) (count($allProfiles) - 1); ?> countries. Type to filter.</p>
	<input type="text" id="epc_hrlaw_filter" class="form-control input-sm" placeholder="Filter by country or region…" style="max-width:320px;margin-bottom:10px;">
	<div class="table-responsive">
	<table class="table table-striped table-bordered table-condensed" id="epc_hrlaw_table">
		<thead><tr>
			<th>Country</th><th>Region</th><th>Hours/wk</th><th>Overtime</th>
			<th>Probation</th><th>Notice</th><th>Annual leave</th><th>End of service</th><th>Authority &amp; official source</th>
		</tr></thead>
		<tbody>
		<?php foreach ($allProfiles as $code => $p): if ($code === 'generic') { continue; }
			$rowUrl = epc_hr_law_authority_url((string) $code);
		?>
			<tr>
				<td><strong><?php echo epc_erp_h($p['name']); ?></strong> <small class="text-muted"><?php echo epc_erp_h($code); ?></small></td>
				<td><?php echo epc_erp_h($p['region']); ?></td>
				<td><?php echo (int) $p['weekly_hours']; ?></td>
				<td><?php echo epc_erp_h($p['overtime']); ?></td>
				<td><?php echo (int) $p['probation_max_months'] > 0 ? (int) $p['probation_max_months'] . ' mo' : '—'; ?></td>
				<td><?php echo (int) $p['notice_days'] > 0 ? (int) $p['notice_days'] . ' d' : '—'; ?></td>
				<td><?php echo (float) $p['annual_leave_days'] > 0 ? (float) $p['annual_leave_days'] . ' d' : '—'; ?></td>
				<td><?php echo epc_erp_h($p['eos']); ?></td>
				<td>
					<small><?php echo epc_erp_h($p['authority']); ?></small>
					<?php if ($rowUrl !== ''): ?><br><small><a href="<?php echo epc_erp_h($rowUrl); ?>" target="_blank" rel="noopener noreferrer"><i class="fa fa-external-link"></i> Official source</a></small><?php endif; ?>
				</td>
			</tr>
		<?php endforeach; ?>
		</tbody>
	</table>
	</div>
</div>

<script>
(function(){
	var f = document.getElementById('epc_hrlaw_filter');
	var t = document.getElementById('epc_hrlaw_table');
	if (!f || !t) return;
	f.addEventListener('input', function(){
		var q = f.value.toLowerCase();
		t.querySelectorAll('tbody tr').forEach(function(tr){
			tr.style.display = tr.textContent.toLowerCase().indexOf(q) !== -1 ? '' : 'none';
		});
	});
})();
</script>
