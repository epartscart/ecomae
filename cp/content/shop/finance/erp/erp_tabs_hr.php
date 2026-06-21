<?php
defined('_ASTEXE_') or die('No access');

require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_staff.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_payroll.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_hr_law.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_localization.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_company.php';
$hrRows = epc_erp_hr_list($db_link);
epc_erp_payroll_ensure_schema($db_link);
$stdDays = epc_erp_payroll_standard_days();
$csrfLocal = isset($csrf) ? $csrf : '';

// Country master switch: resolve the tenant country once and localize HR law.
$erpCo = function_exists('epc_co_profile_get') ? epc_co_profile_get($db_link) : array();
$erpCountry = !empty($erpCo['country']) ? strtoupper(substr((string)$erpCo['country'], 0, 2)) : 'AE';
$locProf = function_exists('epc_country_profile') ? epc_country_profile($erpCountry) : array('name' => 'United Arab Emirates', 'currency' => 'AED', 'hr_country' => 'AE');
$hrCountry = !empty($locProf['hr_country']) ? (string)$locProf['hr_country'] : $erpCountry;
$curr = !empty($locProf['currency']) ? (string)$locProf['currency'] : 'AED';
$gratNote = function_exists('epc_hr_gratuity') ? (string)(epc_hr_gratuity($hrCountry, 10000.0, 6.0)['notes'] ?? '') : '';
?>

<div class="epc-erp-section">
	<h4><i class="fa fa-id-card"></i> HR — employee records &amp; salaries</h4>
	<p class="text-muted">
		<strong>Basic + allowances</strong> = fixed monthly salary for a <?php echo (int)$stdDays; ?>-day month.
		Set <strong>days worked</strong> before generating payroll (pro-rata: salary ÷ <?php echo (int)$stdDays; ?> × days).
		Salaries feed the <a href="<?php echo epc_erp_h(epc_erp_tab_url($erpUrl, 'payroll', $date_from_str, $date_to_str)); ?>">Payroll</a> tab.
	</p>
	<table class="table table-striped table-bordered">
		<thead><tr><th>Name</th><th>Department</th><th>Job title</th><th>Fixed basic</th><th>Allowances</th><th>Monthly (30d)</th><th>Days worked</th><th>Est. pay</th><th>Bank</th><th>Leave</th></tr></thead>
		<tbody>
		<?php foreach ($hrRows as $h): ?>
			<?php
			$monthly = (float)$h['basic_salary'] + (float)$h['allowances'];
			$days = (float)($h['days_worked'] ?? 30);
			if ($days <= 0) {
				$days = 30;
			}
			$est = epc_erp_payroll_calc((float)$h['basic_salary'], (float)$h['allowances'], $days, $stdDays);
			?>
			<tr data-profile-id="<?php echo (int)$h['staff_profile_id']; ?>">
				<td><?php echo epc_erp_h($h['display_name']); ?></td>
				<td><?php echo epc_erp_h(epc_erp_staff_department_name($h['department_code'])); ?></td>
				<td><?php echo epc_erp_h($h['job_title']); ?></td>
				<td><?php echo epc_erp_money($h['basic_salary']); ?> AED</td>
				<td><?php echo epc_erp_money($h['allowances']); ?> AED</td>
				<td><strong><?php echo epc_erp_money($monthly); ?> AED</strong></td>
				<td>
					<input type="number" class="form-control input-sm epc-hr-days" step="0.5" min="0" max="62" value="<?php echo epc_erp_h($days); ?>" style="width:70px;">
				</td>
				<td><strong class="epc-hr-est"><?php echo epc_erp_money($est['net_pay']); ?></strong> AED</td>
				<td><small><?php echo epc_erp_h($h['bank_name']); ?><br><?php echo epc_erp_h($h['bank_account']); ?></small></td>
				<td><?php echo epc_erp_h(number_format((float)$h['leave_balance_days'], 1)); ?> d</td>
			</tr>
		<?php endforeach; ?>
		<?php if (empty($hrRows)): ?>
			<tr><td colspan="10" class="text-muted">No HR records — run staff setup with sample=1</td></tr>
		<?php endif; ?>
		</tbody>
	</table>
</div>

<div class="epc-erp-section">
	<h4><i class="fa fa-globe"></i> End-of-service &amp; statutory leave
		<small class="text-muted">— country-aware (<?php echo epc_erp_h($locProf['name']); ?> · <?php echo epc_erp_h($hrCountry); ?>)</small>
	</h4>
	<p class="text-muted">
		Gratuity / end-of-service and annual-leave entitlement follow
		<strong><?php echo epc_erp_h($locProf['name']); ?></strong> labour law, selected automatically from the company country.
		Amounts in <strong><?php echo epc_erp_h($curr); ?></strong>. Change the country in Company profile to switch the whole rule set.
	</p>
	<table class="table table-striped table-bordered">
		<thead><tr><th>Name</th><th>Joined</th><th>Service</th><th>Gratuity (accrued)</th><th>Annual leave</th><th>Leave accrued</th><th>Leave salary (accrued)</th></tr></thead>
		<tbody>
		<?php foreach ($hrRows as $h):
			$hire = (int)($h['hire_date'] ?? 0);
			$basic = (float)$h['basic_salary'];
			$years = $hire > 0 ? max(0.0, (time() - $hire) / 31557600.0) : 0.0;
			$months = $years * 12.0;
			$grat = epc_hr_gratuity($hrCountry, $basic, $years);
			$leaveEnt = epc_hr_leave_entitlement($hrCountry, $months);
			$leaveSal = epc_hr_leave_salary($basic, (float)$leaveEnt['accrued_days']);
			$proj5 = epc_hr_gratuity($hrCountry, $basic, 5.0);
		?>
			<tr>
				<td><?php echo epc_erp_h($h['display_name']); ?></td>
				<td><?php echo $hire > 0 ? date('d M Y', $hire) : '—'; ?></td>
				<td><?php echo number_format($years, 1); ?> yr</td>
				<td>
					<?php if (!empty($grat['eligible'])): ?>
						<strong><?php echo epc_erp_money($grat['amount']); ?></strong> <?php echo epc_erp_h($curr); ?>
						<small class="text-muted">(<?php echo number_format((float)$grat['days'], 0); ?> days<?php echo !empty($grat['capped']) ? ', capped' : ''; ?>)</small>
					<?php else: ?>
						<span class="text-muted">Not yet eligible</span>
					<?php endif; ?>
					<br><small class="text-muted">Proj. @5 yrs: <?php echo epc_erp_money($proj5['amount']) . ' ' . epc_erp_h($curr); ?></small>
				</td>
				<td><?php echo number_format((float)$leaveEnt['annual_days'], 0); ?> d/yr</td>
				<td><?php echo number_format((float)$leaveEnt['accrued_days'], 1); ?> d</td>
				<td><strong><?php echo epc_erp_money($leaveSal['amount']); ?></strong> <?php echo epc_erp_h($curr); ?></td>
			</tr>
		<?php endforeach; ?>
		<?php if (empty($hrRows)): ?>
			<tr><td colspan="7" class="text-muted">No HR records.</td></tr>
		<?php endif; ?>
		</tbody>
	</table>
	<?php if ($gratNote !== ''): ?><p class="text-muted"><small><i class="fa fa-info-circle"></i> <?php echo epc_erp_h($gratNote); ?></small></p><?php endif; ?>
</div>

<script>
(function(){
	var url = <?php echo json_encode(isset($erpAjaxEndpoint) ? $erpAjaxEndpoint : ('/' . (isset($DP_Config->backend_dir) ? $DP_Config->backend_dir : 'cp') . '/content/shop/finance/erp/ajax_erp_endpoint.php')); ?>;
	var csrf = <?php echo json_encode($csrfLocal); ?>;
	var stdDays = <?php echo (int)$stdDays; ?>;
	function post(action, fd) {
		fd.append('action', action);
		return fetch(url, { method: 'POST', body: fd, credentials: 'same-origin' }).then(function(r){ return r.json(); });
	}
	function msg(j) {
		var el = document.getElementById('epc_erp_msg');
		if (!el) return;
		el.className = 'alert alert-' + (j.status ? 'success' : 'danger');
		el.textContent = j.message || '';
		el.style.display = 'block';
	}
	document.querySelectorAll('.epc-hr-days').forEach(function(inp){
		var timer;
		inp.addEventListener('change', function(){
			clearTimeout(timer);
			var row = inp.closest('tr');
			var profileId = row ? row.getAttribute('data-profile-id') : 0;
			timer = setTimeout(function(){
				var fd = new FormData();
				fd.append('csrf_guard_key', csrf);
				fd.append('staff_profile_id', profileId);
				fd.append('days_worked', inp.value);
				post('hr_update_days', fd).then(function(j){
					msg(j);
					if (j.status) {
						var cells = row.querySelectorAll('td');
						var basic = parseFloat((cells[3].textContent || '0').replace(/[^\d.]/g, '')) || 0;
						var allow = parseFloat((cells[4].textContent || '0').replace(/[^\d.]/g, '')) || 0;
						var days = parseFloat(inp.value) || 0;
						var gross = (basic + allow) / stdDays * days;
						var estEl = row.querySelector('.epc-hr-est');
						if (estEl) estEl.textContent = gross.toFixed(2);
					}
				});
			}, 400);
		});
	});
})();
</script>
