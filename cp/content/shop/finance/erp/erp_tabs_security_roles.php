<?php
defined('_ASTEXE_') or die('No access');
/**
 * Platform — role-based security (D365 F&O-style privileges -> duties -> roles
 * -> user assignment) with an effective-access explorer.
 */
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_rbac.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_company_context.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_ui.php';

epc_rbac_ensure_schema($db_link);
$csrfLocal = isset($csrf) ? $csrf : '';
$companyId = function_exists('epc_erp_active_company_id') ? epc_erp_active_company_id($db_link) : 0;
$view = isset($_GET['sv']) ? (string) $_GET['sv'] : 'roles';
$summary = epc_rbac_summary($db_link, $companyId);

erp_page_header(
	'<i class="fa fa-shield"></i> Security roles',
	'D365 F&amp;O-style role-based security: privileges → duties → roles → user assignment, with an effective-access explorer.',
	array(
		array('label' => 'ERP', 'url' => epc_erp_tab_url($erpUrl, 'dashboard', $date_from_str, $date_to_str)),
		array('label' => 'Security roles'),
	)
);

erp_stat_cards(array(
	array('label' => 'Privileges', 'value' => (string) $summary['privileges']),
	array('label' => 'Duties', 'value' => (string) $summary['duties']),
	array('label' => 'Roles', 'value' => (string) $summary['roles']),
	array('label' => 'User assignments', 'value' => (string) $summary['assignments']),
));

$tabBase = epc_erp_tab_url($erpUrl, 'security_roles', $date_from_str, $date_to_str);
$sep = strpos($tabBase, '?') === false ? '?' : '&';
$views = array('roles' => 'Roles & duties', 'privileges' => 'Privileges & duties', 'assign' => 'User assignment', 'explorer' => 'Access explorer');
$privileges = epc_rbac_privileges($db_link, $companyId);
$duties = epc_rbac_duties($db_link, $companyId);
$roles = epc_rbac_roles($db_link, $companyId);
?>
<div id="epc_erp_msg" class="alert" style="display:none;"></div>

<div class="btn-group btn-group-sm" style="margin-bottom:10px;">
	<?php foreach ($views as $k => $lbl): ?>
		<a class="btn btn-<?php echo $view === $k ? 'primary' : 'default'; ?>" href="<?php echo epc_erp_h($tabBase . $sep . 'sv=' . $k); ?>"><?php echo epc_erp_h($lbl); ?></a>
	<?php endforeach; ?>
</div>

<?php if ($view === 'privileges'):
	$selDuty = (int) ($_GET['duty_id'] ?? 0); ?>
	<div class="row"><div class="col-md-6">
		<div class="well well-sm">
			<h5><i class="fa fa-plus-circle"></i> New privilege</h5>
			<form id="epc_rbac_priv" class="form-inline">
				<input type="hidden" name="csrf_guard_key" value="<?php echo epc_erp_h($csrfLocal); ?>">
				<input type="text" name="code" class="form-control input-sm" placeholder="code (cust.view)" style="width:140px;" required>
				<input type="text" name="name" class="form-control input-sm" placeholder="Name" style="width:150px;">
				<select name="access_level" class="form-control input-sm"><option value="read">read</option><option value="update">update</option><option value="create">create</option><option value="delete">delete</option><option value="full">full</option></select>
				<button class="btn btn-primary btn-sm">Save</button>
			</form>
		</div>
		<table class="table table-bordered table-condensed">
			<thead><tr><th>Code</th><th>Name</th><th>Access</th></tr></thead>
			<tbody>
			<?php if (empty($privileges)): ?><tr><td colspan="3" class="text-muted">No privileges.</td></tr>
			<?php else: foreach ($privileges as $p): ?>
				<tr><td><code><?php echo epc_erp_h($p['code']); ?></code></td><td><?php echo epc_erp_h($p['name']); ?></td><td><span class="label label-default"><?php echo epc_erp_h($p['access_level']); ?></span></td></tr>
			<?php endforeach; endif; ?>
			</tbody>
		</table>
	</div><div class="col-md-6">
		<div class="well well-sm">
			<h5><i class="fa fa-plus-circle"></i> New duty</h5>
			<form id="epc_rbac_duty" class="form-inline">
				<input type="hidden" name="csrf_guard_key" value="<?php echo epc_erp_h($csrfLocal); ?>">
				<input type="text" name="code" class="form-control input-sm" placeholder="code" style="width:130px;" required>
				<input type="text" name="name" class="form-control input-sm" placeholder="Name" style="width:150px;">
				<button class="btn btn-primary btn-sm">Save</button>
			</form>
		</div>
		<table class="table table-bordered table-condensed">
			<thead><tr><th>Duty</th><th class="text-right">Privs</th><th></th></tr></thead>
			<tbody>
			<?php if (empty($duties)): ?><tr><td colspan="3" class="text-muted">No duties.</td></tr>
			<?php else: foreach ($duties as $d): ?>
				<tr><td><strong><?php echo epc_erp_h($d['code']); ?></strong> <small><?php echo epc_erp_h($d['name']); ?></small></td><td class="text-right"><?php echo (int) $d['priv_count']; ?></td>
				<td><a class="btn btn-default btn-xs" href="<?php echo epc_erp_h($tabBase . $sep . 'sv=privileges&duty_id=' . (int) $d['id']); ?>">Map</a></td></tr>
			<?php endforeach; endif; ?>
			</tbody>
		</table>
		<?php if ($selDuty > 0):
			$attached = epc_rbac_duty_privileges($db_link, $selDuty); ?>
			<div class="panel panel-default">
				<div class="panel-heading"><strong>Map privileges to duty</strong></div>
				<div class="panel-body">
					<?php foreach ($privileges as $p):
						$on = in_array((int) $p['id'], $attached, true); ?>
						<form class="epc_rbac_dp form-inline" style="display:block;margin-bottom:3px;">
							<input type="hidden" name="csrf_guard_key" value="<?php echo epc_erp_h($csrfLocal); ?>">
							<input type="hidden" name="duty_id" value="<?php echo (int) $selDuty; ?>">
							<input type="hidden" name="privilege_id" value="<?php echo (int) $p['id']; ?>">
							<input type="hidden" name="attach" value="<?php echo $on ? '0' : '1'; ?>">
							<button class="btn btn-<?php echo $on ? 'success' : 'default'; ?> btn-xs"><?php echo $on ? '✓ ' : '+ '; ?><?php echo epc_erp_h($p['code']); ?></button>
						</form>
					<?php endforeach; ?>
				</div>
			</div>
		<?php endif; ?>
	</div></div>

<?php elseif ($view === 'assign'):
	$selUser = (int) ($_GET['user_id'] ?? 0);
	$userRoles = $selUser > 0 ? epc_rbac_user_roles($db_link, $companyId, $selUser) : array(); ?>
	<div class="well well-sm">
		<h5><i class="fa fa-user-plus"></i> Find / assign user roles</h5>
		<form method="get" class="form-inline">
			<?php foreach ($_GET as $k => $v) { if ($k === 'user_id') { continue; } echo '<input type="hidden" name="' . epc_erp_h($k) . '" value="' . epc_erp_h((string) $v) . '">'; } ?>
			<input type="hidden" name="sv" value="assign">
			<input type="number" name="user_id" class="form-control input-sm" placeholder="User ID" value="<?php echo $selUser > 0 ? (int) $selUser : ''; ?>">
			<button class="btn btn-primary btn-sm">Load</button>
		</form>
	</div>
	<?php if ($selUser > 0): ?>
		<table class="table table-bordered table-condensed">
			<thead><tr><th>Role</th><th>Duties</th><th>Assigned</th></tr></thead>
			<tbody>
			<?php foreach ($roles as $r):
				$on = in_array((int) $r['id'], $userRoles, true); ?>
				<tr><td><strong><?php echo epc_erp_h($r['code']); ?></strong> <small><?php echo epc_erp_h($r['name']); ?></small></td><td><?php echo (int) $r['duty_count']; ?></td>
				<td>
					<form class="epc_rbac_ur form-inline" style="display:inline;">
						<input type="hidden" name="csrf_guard_key" value="<?php echo epc_erp_h($csrfLocal); ?>">
						<input type="hidden" name="user_id" value="<?php echo (int) $selUser; ?>">
						<input type="hidden" name="role_id" value="<?php echo (int) $r['id']; ?>">
						<input type="hidden" name="assign" value="<?php echo $on ? '0' : '1'; ?>">
						<button class="btn btn-<?php echo $on ? 'success' : 'default'; ?> btn-xs"><?php echo $on ? '✓ assigned' : 'assign'; ?></button>
					</form>
				</td></tr>
			<?php endforeach; ?>
			</tbody>
		</table>
	<?php else: ?><p class="text-muted">Enter a user ID to manage role assignments.</p><?php endif; ?>

<?php elseif ($view === 'explorer'):
	$selUser = (int) ($_GET['user_id'] ?? 0);
	$access = $selUser > 0 ? epc_rbac_user_privileges($db_link, $companyId, $selUser) : array(); ?>
	<div class="well well-sm">
		<h5><i class="fa fa-search"></i> Effective access for user</h5>
		<form method="get" class="form-inline">
			<?php foreach ($_GET as $k => $v) { if ($k === 'user_id') { continue; } echo '<input type="hidden" name="' . epc_erp_h($k) . '" value="' . epc_erp_h((string) $v) . '">'; } ?>
			<input type="hidden" name="sv" value="explorer">
			<input type="number" name="user_id" class="form-control input-sm" placeholder="User ID" value="<?php echo $selUser > 0 ? (int) $selUser : ''; ?>">
			<button class="btn btn-primary btn-sm">Resolve</button>
		</form>
	</div>
	<?php if ($selUser > 0): ?>
		<p>User <strong>#<?php echo (int) $selUser; ?></strong> has <strong><?php echo count($access); ?></strong> effective privilege(s) via assigned roles → duties.</p>
		<table class="table table-bordered table-condensed">
			<thead><tr><th>Privilege</th><th>Effective access</th></tr></thead>
			<tbody>
			<?php if (empty($access)): ?><tr><td colspan="2" class="text-muted">No effective access (no roles assigned).</td></tr>
			<?php else: foreach ($access as $code => $lvl): ?>
				<tr><td><code><?php echo epc_erp_h($code); ?></code></td><td><span class="label label-info"><?php echo epc_erp_h($lvl); ?></span></td></tr>
			<?php endforeach; endif; ?>
			</tbody>
		</table>
	<?php else: ?><p class="text-muted">Resolve a user's flattened access (roles → duties → privileges, highest level wins).</p><?php endif; ?>

<?php else:
	$selRole = (int) ($_GET['role_id'] ?? 0);
	$roleDuties = $selRole > 0 ? epc_rbac_role_duties($db_link, $selRole) : array(); ?>
	<div class="row"><div class="col-md-6">
		<div class="well well-sm">
			<h5><i class="fa fa-plus-circle"></i> New role</h5>
			<form id="epc_rbac_role" class="form-inline">
				<input type="hidden" name="csrf_guard_key" value="<?php echo epc_erp_h($csrfLocal); ?>">
				<input type="text" name="code" class="form-control input-sm" placeholder="code (AR_CLERK)" style="width:140px;" required>
				<input type="text" name="name" class="form-control input-sm" placeholder="Name" style="width:160px;">
				<button class="btn btn-primary btn-sm">Save</button>
			</form>
		</div>
		<table class="table table-bordered table-condensed">
			<thead><tr><th>Role</th><th class="text-right">Duties</th><th class="text-right">Users</th><th></th></tr></thead>
			<tbody>
			<?php if (empty($roles)): ?><tr><td colspan="4" class="text-muted">No roles.</td></tr>
			<?php else: foreach ($roles as $r): ?>
				<tr><td><strong><?php echo epc_erp_h($r['code']); ?></strong> <small><?php echo epc_erp_h($r['name']); ?></small></td>
				<td class="text-right"><?php echo (int) $r['duty_count']; ?></td><td class="text-right"><?php echo (int) $r['user_count']; ?></td>
				<td><a class="btn btn-default btn-xs" href="<?php echo epc_erp_h($tabBase . $sep . 'role_id=' . (int) $r['id']); ?>">Duties</a></td></tr>
			<?php endforeach; endif; ?>
			</tbody>
		</table>
	</div><div class="col-md-6">
		<?php if ($selRole > 0): ?>
			<div class="panel panel-default">
				<div class="panel-heading"><strong>Map duties to role</strong></div>
				<div class="panel-body">
					<?php if (empty($duties)): ?><p class="text-muted">Create duties first (Privileges & duties tab).</p>
					<?php else: foreach ($duties as $d):
						$on = in_array((int) $d['id'], $roleDuties, true); ?>
						<form class="epc_rbac_rd form-inline" style="display:block;margin-bottom:3px;">
							<input type="hidden" name="csrf_guard_key" value="<?php echo epc_erp_h($csrfLocal); ?>">
							<input type="hidden" name="role_id" value="<?php echo (int) $selRole; ?>">
							<input type="hidden" name="duty_id" value="<?php echo (int) $d['id']; ?>">
							<input type="hidden" name="attach" value="<?php echo $on ? '0' : '1'; ?>">
							<button class="btn btn-<?php echo $on ? 'success' : 'default'; ?> btn-xs"><?php echo $on ? '✓ ' : '+ '; ?><?php echo epc_erp_h($d['code']); ?> <small>(<?php echo (int) $d['priv_count']; ?> privs)</small></button>
						</form>
					<?php endforeach; endif; ?>
				</div>
			</div>
		<?php else: ?><p class="text-muted">Pick a role to map its duties.</p><?php endif; ?>
	</div></div>
<?php endif; ?>

<script>
(function(){
	var url = <?php echo json_encode(isset($erpAjaxEndpoint) ? $erpAjaxEndpoint : ('/' . (isset($DP_Config->backend_dir) ? $DP_Config->backend_dir : 'cp') . '/content/shop/finance/erp/ajax_erp_endpoint.php')); ?>;
	function post(action, fd){ fd.append('action', action); return fetch(url,{method:'POST',body:fd,credentials:'same-origin'}).then(function(r){return r.json();}); }
	function msg(j){ var el=document.getElementById('epc_erp_msg'); if(el){ el.className='alert alert-'+(j.status?'success':'danger'); el.textContent=j.message||''; el.style.display='block'; el.scrollIntoView({behavior:'smooth',block:'center'}); } if(j.status) setTimeout(function(){ location.reload(); }, 700); }
	function bind(id, action){ var f=document.getElementById(id); if(f) f.addEventListener('submit', function(e){ e.preventDefault(); post(action, new FormData(f)).then(msg); }); }
	bind('epc_rbac_priv', 'rbac_priv_save');
	bind('epc_rbac_duty', 'rbac_duty_save');
	bind('epc_rbac_role', 'rbac_role_save');
	function bindAll(cls, action){ Array.prototype.forEach.call(document.querySelectorAll('.'+cls), function(f){ f.addEventListener('submit', function(e){ e.preventDefault(); post(action, new FormData(f)).then(msg); }); }); }
	bindAll('epc_rbac_dp', 'rbac_duty_priv');
	bindAll('epc_rbac_rd', 'rbac_role_duty');
	bindAll('epc_rbac_ur', 'rbac_user_role');
})();
</script>
