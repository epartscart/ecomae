<?php
defined('_ASTEXE_') or die('No access');
/**
 * HR Operations & Employee Self-Service — employees, attendance, leave
 * management (request + approve), and expense claims (submit + approve).
 * Backed by epc_hr_* in epc_erp_hr.php.
 */
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_hr.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_ui.php';

epc_hr_ensure_schema($db_link);
$csrfLocal  = isset($csrf) ? $csrf : '';
$hrSummary  = epc_hr_ops_summary($db_link);
$hrEmps     = epc_hr_employees_list($db_link);
$hrLeave    = epc_hr_leave_list($db_link, 60);
$hrExpenses = epc_hr_expenses_list($db_link, 60);
$hrAtt      = epc_hr_attendance_list($db_link, 40);

erp_page_header(
	'<i class="fa fa-users"></i> HR operations &amp; self-service',
	'Employees, attendance, leave management (request &amp; approve) and expense claims (submit &amp; approve).',
	array(
		array('label' => 'ERP', 'url' => epc_erp_tab_url($erpUrl, 'dashboard', $date_from_str, $date_to_str)),
		array('label' => 'HR operations'),
	)
);

erp_stat_cards(array(
	array('label' => 'Active employees', 'value' => (string) $hrSummary['employees']),
	array('label' => 'Pending leave', 'value' => (string) $hrSummary['pending_leave']),
	array('label' => 'Expenses to approve', 'value' => epc_erp_money($hrSummary['pending_expense']) . ' AED'),
	array('label' => 'Present today', 'value' => (string) $hrSummary['present_today']),
));

$empOptions = '';
foreach ($hrEmps as $e) {
	$empOptions .= '<option value="' . (int) $e['id'] . '">' . epc_erp_h($e['code'] . ' · ' . $e['name']) . '</option>';
}
?>
<div id="epc_erp_msg" class="alert" style="display:none;"></div>

<div class="row">
	<div class="col-md-4">
		<div class="well well-sm">
			<h5><i class="fa fa-user-plus"></i> Add employee</h5>
			<form id="epc_hr_emp" class="form">
				<input type="hidden" name="csrf_guard_key" value="<?php echo epc_erp_h($csrfLocal); ?>">
				<div class="row">
					<div class="col-xs-5 form-group"><label>Code</label><input type="text" name="code" class="form-control input-sm" placeholder="EMP-001" required></div>
					<div class="col-xs-7 form-group"><label>Name</label><input type="text" name="name" class="form-control input-sm" required></div>
				</div>
				<div class="form-group"><label>Department</label><input type="text" name="department" class="form-control input-sm"></div>
				<div class="row">
					<div class="col-xs-6 form-group"><label>Basic salary</label><input type="number" step="0.01" name="basic_salary" class="form-control input-sm" value="0"></div>
					<div class="col-xs-6 form-group"><label>Allowances</label><input type="number" step="0.01" name="allowances" class="form-control input-sm" value="0"></div>
				</div>
				<div class="row">
					<div class="col-xs-6 form-group"><label>Annual leave (d)</label><input type="number" step="0.5" name="annual_leave_days" class="form-control input-sm" value="30"></div>
					<div class="col-xs-6 form-group"><label>Join date</label><input type="date" name="join_date_str" class="form-control input-sm" value="<?php echo date('Y-m-d'); ?>"></div>
				</div>
				<a href="#" onclick="var d=document.getElementById('epc_hr_more');d.style.display=(d.style.display==='none'?'block':'none');return false;" style="font-size:12px;"><i class="fa fa-plus-square-o"></i> More details (worker, personal, contact, IDs, banking)</a>
				<div id="epc_hr_more" style="display:none;margin-top:8px;">
					<div class="row">
						<div class="col-xs-6 form-group"><label>First name</label><input type="text" name="first_name" class="form-control input-sm"></div>
						<div class="col-xs-6 form-group"><label>Last name</label><input type="text" name="last_name" class="form-control input-sm"></div>
					</div>
					<div class="row">
						<div class="col-xs-6 form-group"><label>Worker type</label>
							<select name="worker_type" class="form-control input-sm"><option value="employee">Employee</option><option value="contractor">Contractor</option></select>
						</div>
						<div class="col-xs-6 form-group"><label>Employment type</label>
							<select name="employment_type" class="form-control input-sm"><option value="">—</option><option value="full_time">Full time</option><option value="part_time">Part time</option><option value="temporary">Temporary</option><option value="intern">Intern</option></select>
						</div>
					</div>
					<div class="row">
						<div class="col-xs-6 form-group"><label>Position title</label><input type="text" name="position_title" class="form-control input-sm"></div>
						<div class="col-xs-6 form-group"><label>Job title</label><input type="text" name="job_title" class="form-control input-sm"></div>
					</div>
					<div class="row">
						<div class="col-xs-6 form-group"><label>Manager (employee ID)</label><input type="number" name="manager_id" class="form-control input-sm"></div>
						<div class="col-xs-6 form-group"><label>Legal entity ID</label><input type="number" name="legal_entity_id" class="form-control input-sm"></div>
					</div>
					<div class="row">
						<div class="col-xs-6 form-group"><label>Business unit ID</label><input type="number" name="business_unit_id" class="form-control input-sm"></div>
						<div class="col-xs-6 form-group"><label>Seniority date</label><input type="date" name="seniority_date" class="form-control input-sm"></div>
					</div>
					<div class="row">
						<div class="col-xs-6 form-group"><label>Termination date</label><input type="date" name="termination_date" class="form-control input-sm"></div>
						<div class="col-xs-6 form-group"><label>Date of birth</label><input type="date" name="date_of_birth" class="form-control input-sm"></div>
					</div>
					<div class="row">
						<div class="col-xs-6 form-group"><label>Gender</label>
							<select name="gender" class="form-control input-sm"><option value="">—</option><option value="male">Male</option><option value="female">Female</option><option value="other">Other</option></select>
						</div>
						<div class="col-xs-6 form-group"><label>Marital status</label>
							<select name="marital_status" class="form-control input-sm"><option value="">—</option><option value="single">Single</option><option value="married">Married</option><option value="divorced">Divorced</option><option value="widowed">Widowed</option></select>
						</div>
					</div>
					<div class="form-group"><label>Nationality</label><input type="text" name="nationality" class="form-control input-sm"></div>
					<div class="row">
						<div class="col-xs-6 form-group"><label>Personal email</label><input type="email" name="personal_email" class="form-control input-sm"></div>
						<div class="col-xs-6 form-group"><label>Work email</label><input type="email" name="work_email" class="form-control input-sm"></div>
					</div>
					<div class="row">
						<div class="col-xs-6 form-group"><label>Work phone</label><input type="text" name="work_phone" class="form-control input-sm"></div>
						<div class="col-xs-6 form-group"><label>Mobile</label><input type="text" name="mobile" class="form-control input-sm"></div>
					</div>
					<div class="form-group"><label>Address</label><input type="text" name="address" class="form-control input-sm"></div>
					<div class="row">
						<div class="col-xs-6 form-group"><label>City</label><input type="text" name="city" class="form-control input-sm"></div>
						<div class="col-xs-6 form-group"><label>Country code</label><input type="text" name="country_code" class="form-control input-sm" placeholder="AE"></div>
					</div>
					<div class="row">
						<div class="col-xs-6 form-group"><label>National ID</label><input type="text" name="national_id" class="form-control input-sm"></div>
						<div class="col-xs-6 form-group"><label>Passport no.</label><input type="text" name="passport_no" class="form-control input-sm"></div>
					</div>
					<div class="row">
						<div class="col-xs-6 form-group"><label>Visa no.</label><input type="text" name="visa_no" class="form-control input-sm"></div>
						<div class="col-xs-6 form-group"><label>Visa expiry</label><input type="date" name="visa_expiry" class="form-control input-sm"></div>
					</div>
					<div class="row">
						<div class="col-xs-6 form-group"><label>Emergency contact</label><input type="text" name="emergency_contact" class="form-control input-sm"></div>
						<div class="col-xs-6 form-group"><label>Emergency phone</label><input type="text" name="emergency_phone" class="form-control input-sm"></div>
					</div>
					<div class="form-group"><label>Bank name</label><input type="text" name="bank_name" class="form-control input-sm"></div>
					<div class="row">
						<div class="col-xs-6 form-group"><label>Bank IBAN</label><input type="text" name="bank_iban" class="form-control input-sm"></div>
						<div class="col-xs-6 form-group"><label>Bank account no.</label><input type="text" name="bank_account_no" class="form-control input-sm"></div>
					</div>
				</div>
				<button type="submit" class="btn btn-primary btn-sm">Save employee</button>
			</form>
		</div>

		<div class="well well-sm">
			<h5><i class="fa fa-calendar-check-o"></i> Record attendance</h5>
			<form id="epc_hr_att" class="form">
				<input type="hidden" name="csrf_guard_key" value="<?php echo epc_erp_h($csrfLocal); ?>">
				<div class="form-group"><label>Employee</label><select name="employee_id" class="form-control input-sm" required><option value="">— select —</option><?php echo $empOptions; ?></select></div>
				<div class="row">
					<div class="col-xs-5 form-group"><label>Date</label><input type="date" name="work_date_str" class="form-control input-sm" value="<?php echo date('Y-m-d'); ?>"></div>
					<div class="col-xs-3 form-group"><label>Hours</label><input type="number" step="0.5" name="hours" class="form-control input-sm" value="8"></div>
					<div class="col-xs-4 form-group"><label>Status</label>
						<select name="status" class="form-control input-sm">
							<option value="present">Present</option>
							<option value="remote">Remote</option>
							<option value="leave">Leave</option>
							<option value="absent">Absent</option>
						</select>
					</div>
				</div>
				<button type="submit" class="btn btn-default btn-sm">Record</button>
			</form>
		</div>
	</div>

	<div class="col-md-8">
		<h5>Employees</h5>
		<div class="table-responsive">
		<table class="table table-bordered table-condensed">
			<thead><tr><th>Code</th><th>Name</th><th>Department</th><th class="text-right">Basic</th><th class="text-right">Allow.</th><th class="text-right">Leave bal.</th></tr></thead>
			<tbody>
			<?php if (empty($hrEmps)): ?>
				<tr><td colspan="6" class="text-muted">No employees yet. Add one on the left.</td></tr>
			<?php else: foreach ($hrEmps as $e): $bal = epc_hr_leave_balance($db_link, (int) $e['id']); ?>
				<tr>
					<td><strong><?php echo epc_erp_h($e['code']); ?></strong></td>
					<td><?php echo epc_erp_h($e['name']); ?></td>
					<td><small><?php echo epc_erp_h((string) $e['department']); ?></small></td>
					<td class="text-right"><?php echo epc_erp_money($e['basic_salary']); ?></td>
					<td class="text-right"><?php echo epc_erp_money($e['allowances']); ?></td>
					<td class="text-right"><?php echo (float) $bal['balance']; ?> / <?php echo (float) $bal['entitlement']; ?> d</td>
				</tr>
			<?php endforeach; endif; ?>
			</tbody>
		</table>
		</div>

		<h5 style="margin-top:6px;">Recent attendance</h5>
		<div class="table-responsive">
		<table class="table table-condensed table-bordered">
			<thead><tr><th>Date</th><th>Employee</th><th class="text-right">Hours</th><th>Status</th></tr></thead>
			<tbody>
			<?php if (empty($hrAtt)): ?>
				<tr><td colspan="4" class="text-muted">No attendance recorded.</td></tr>
			<?php else: foreach ($hrAtt as $a): ?>
				<tr><td><?php echo epc_erp_h(date('Y-m-d', (int) $a['work_date'])); ?></td><td><?php echo epc_erp_h((string) $a['employee_name']); ?></td><td class="text-right"><?php echo (float) $a['hours']; ?></td><td><span class="label label-default"><?php echo epc_erp_h($a['status']); ?></span></td></tr>
			<?php endforeach; endif; ?>
			</tbody>
		</table>
		</div>
	</div>
</div>

<hr>
<div class="row">
	<div class="col-md-6">
		<h5><i class="fa fa-plane"></i> Leave management</h5>
		<div class="well well-sm">
			<form id="epc_hr_leave" class="form-inline">
				<input type="hidden" name="csrf_guard_key" value="<?php echo epc_erp_h($csrfLocal); ?>">
				<select name="employee_id" class="form-control input-sm" style="width:160px;" required><option value="">— employee —</option><?php echo $empOptions; ?></select>
				<select name="type" class="form-control input-sm">
					<option value="annual">Annual</option>
					<option value="sick">Sick</option>
					<option value="unpaid">Unpaid</option>
				</select>
				<input type="number" step="0.5" name="days" class="form-control input-sm" placeholder="Days" style="width:80px;" required>
				<input type="date" name="date_from_str" class="form-control input-sm">
				<button type="submit" class="btn btn-default btn-sm">Request</button>
			</form>
		</div>
		<table class="table table-condensed table-bordered">
			<thead><tr><th>Employee</th><th>Type</th><th class="text-right">Days</th><th>Status</th><th></th></tr></thead>
			<tbody>
			<?php if (empty($hrLeave)): ?>
				<tr><td colspan="5" class="text-muted">No leave requests.</td></tr>
			<?php else: foreach ($hrLeave as $l): ?>
				<tr>
					<td><?php echo epc_erp_h((string) $l['employee_name']); ?></td>
					<td><small><?php echo epc_erp_h($l['type']); ?></small></td>
					<td class="text-right"><?php echo (float) $l['days']; ?></td>
					<td><span class="label label-<?php echo $l['status'] === 'approved' ? 'success' : ($l['status'] === 'rejected' ? 'danger' : 'warning'); ?>"><?php echo epc_erp_h($l['status']); ?></span></td>
					<td>
						<?php if ($l['status'] === 'pending'): ?>
							<button class="btn btn-xs btn-success epc-hr-leave" data-id="<?php echo (int) $l['id']; ?>" data-status="approved">Approve</button>
							<button class="btn btn-xs btn-default epc-hr-leave" data-id="<?php echo (int) $l['id']; ?>" data-status="rejected">Reject</button>
						<?php endif; ?>
					</td>
				</tr>
			<?php endforeach; endif; ?>
			</tbody>
		</table>
	</div>

	<div class="col-md-6">
		<h5><i class="fa fa-credit-card"></i> Expense claims</h5>
		<div class="well well-sm">
			<form id="epc_hr_expense" class="form">
				<input type="hidden" name="csrf_guard_key" value="<?php echo epc_erp_h($csrfLocal); ?>">
				<div class="row">
					<div class="col-xs-6 form-group"><select name="employee_id" class="form-control input-sm" required><option value="">— employee —</option><?php echo $empOptions; ?></select></div>
					<div class="col-xs-6 form-group"><input type="text" name="title" class="form-control input-sm" placeholder="Claim title" required></div>
				</div>
				<table class="table table-condensed" style="margin-bottom:6px;">
					<thead><tr><th>Description</th><th style="width:110px;">Amount</th><th style="width:30px;"></th></tr></thead>
					<tbody id="epc_hr_exp_lines"></tbody>
				</table>
				<button type="button" class="btn btn-default btn-xs" id="epc_hr_exp_add"><i class="fa fa-plus"></i> Add line</button>
				<button type="submit" class="btn btn-primary btn-sm pull-right">Submit claim</button>
				<div style="clear:both;"></div>
			</form>
		</div>
		<table class="table table-condensed table-bordered">
			<thead><tr><th>Employee</th><th>Title</th><th class="text-right">Amount</th><th>Status</th><th></th></tr></thead>
			<tbody>
			<?php if (empty($hrExpenses)): ?>
				<tr><td colspan="5" class="text-muted">No expense claims.</td></tr>
			<?php else: foreach ($hrExpenses as $x): ?>
				<tr>
					<td><?php echo epc_erp_h((string) $x['employee_name']); ?></td>
					<td><small><?php echo epc_erp_h($x['title']); ?></small></td>
					<td class="text-right"><?php echo epc_erp_money($x['amount']); ?></td>
					<td><span class="label label-<?php echo $x['status'] === 'approved' ? 'success' : ($x['status'] === 'rejected' ? 'danger' : ($x['status'] === 'paid' ? 'info' : 'warning')); ?>"><?php echo epc_erp_h($x['status']); ?></span></td>
					<td>
						<?php if (in_array($x['status'], array('draft', 'submitted'), true)): ?>
							<button class="btn btn-xs btn-success epc-hr-exp" data-id="<?php echo (int) $x['id']; ?>" data-status="approved">Approve</button>
							<button class="btn btn-xs btn-default epc-hr-exp" data-id="<?php echo (int) $x['id']; ?>" data-status="rejected">Reject</button>
						<?php elseif ($x['status'] === 'approved'): ?>
							<button class="btn btn-xs btn-info epc-hr-exp" data-id="<?php echo (int) $x['id']; ?>" data-status="paid">Mark paid</button>
						<?php endif; ?>
					</td>
				</tr>
			<?php endforeach; endif; ?>
			</tbody>
		</table>
	</div>
</div>

<script>
(function(){
	var url = <?php echo json_encode(isset($erpAjaxEndpoint) ? $erpAjaxEndpoint : ('/' . (isset($DP_Config->backend_dir) ? $DP_Config->backend_dir : 'cp') . '/content/shop/finance/erp/ajax_erp_endpoint.php')); ?>;
	var csrf = <?php echo json_encode($csrfLocal); ?>;
	function post(action, fd){ fd.append('action', action); return fetch(url,{method:'POST',body:fd,credentials:'same-origin'}).then(function(r){return r.json();}); }
	function msg(j){ var el=document.getElementById('epc_erp_msg'); if(el){ el.className='alert alert-'+(j.status?'success':'danger'); el.textContent=j.message||''; el.style.display='block'; el.scrollIntoView({behavior:'smooth',block:'center'}); } if(j.status) setTimeout(function(){ location.reload(); }, 700); }
	function bind(id, action, extract){ var f=document.getElementById(id); if(f) f.addEventListener('submit', function(e){ e.preventDefault(); var fd=new FormData(f); if(extract && !extract(fd)) return; post(action, fd).then(msg); }); }
	bind('epc_hr_emp', 'hr_emp_save');
	bind('epc_hr_att', 'hr_attendance');
	bind('epc_hr_leave', 'hr_leave_request');

	function addExpLine(){
		var tb=document.getElementById('epc_hr_exp_lines'); var tr=document.createElement('tr');
		tr.innerHTML='<td><input type="text" class="form-control input-sm epc-exp-label"></td><td><input type="number" step="0.01" class="form-control input-sm epc-exp-amt"></td><td><button type="button" class="btn btn-link btn-xs epc-exp-del" style="color:#c00;">&times;</button></td>';
		tb.appendChild(tr);
	}
	var addBtn=document.getElementById('epc_hr_exp_add'); if(addBtn){ addBtn.addEventListener('click', addExpLine); addExpLine(); }
	document.addEventListener('click', function(ev){ if(ev.target && ev.target.classList.contains('epc-exp-del')){ var r=ev.target.closest('tr'); if(r) r.remove(); } });
	bind('epc_hr_expense', 'hr_expense_save', function(fd){
		var n=0;
		document.querySelectorAll('#epc_hr_exp_lines tr').forEach(function(row){
			var lab=row.querySelector('.epc-exp-label'); var amt=row.querySelector('.epc-exp-amt');
			if(amt && parseFloat(amt.value)){ fd.append('lines['+n+'][label]', lab?lab.value:''); fd.append('lines['+n+'][amount]', amt.value); n++; }
		});
		if(n===0){ msg({status:false, message:'Add at least one expense line.'}); return false; }
		return true;
	});

	document.querySelectorAll('.epc-hr-leave').forEach(function(b){ b.addEventListener('click', function(){ var fd=new FormData(); fd.append('csrf_guard_key',csrf); fd.append('id',b.getAttribute('data-id')); fd.append('status',b.getAttribute('data-status')); post('hr_leave_status', fd).then(msg); }); });
	document.querySelectorAll('.epc-hr-exp').forEach(function(b){ b.addEventListener('click', function(){ var fd=new FormData(); fd.append('csrf_guard_key',csrf); fd.append('id',b.getAttribute('data-id')); fd.append('status',b.getAttribute('data-status')); post('hr_expense_status', fd).then(msg); }); });
})();
</script>
