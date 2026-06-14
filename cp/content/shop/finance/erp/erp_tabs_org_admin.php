<?php
defined('_ASTEXE_') or die('No access');
/**
 * Organization administration / Enterprise — legal entities & org hierarchy
 * (from epc_erp_org), global address book (parties / addresses / contacts) and
 * working calendars (working week + holidays) with due-date arithmetic.
 */
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_orgadmin.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_org.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_company_context.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_ui.php';

epc_oa_ensure_schema($db_link);
$csrfLocal = isset($csrf) ? $csrf : '';
$companyId = function_exists('epc_erp_active_company_id') ? epc_erp_active_company_id($db_link) : 0;
$view = isset($_GET['ov']) ? (string) $_GET['ov'] : 'entities';
$summary = epc_oa_summary($db_link, $companyId);

erp_page_header(
	'<i class="fa fa-sitemap"></i> Organization administration',
	'Legal entities &amp; org hierarchy, global address book, and working calendars (D365 F&amp;O Enterprise).',
	array(
		array('label' => 'ERP', 'url' => epc_erp_tab_url($erpUrl, 'dashboard', $date_from_str, $date_to_str)),
		array('label' => 'Organization administration'),
	)
);

erp_stat_cards(array(
	array('label' => 'Parties', 'value' => (string) $summary['parties']),
	array('label' => 'Addresses', 'value' => (string) $summary['addresses']),
	array('label' => 'Calendars', 'value' => (string) $summary['calendars']),
	array('label' => 'Holidays', 'value' => (string) $summary['holidays']),
));

$tabBase = epc_erp_tab_url($erpUrl, 'org_admin', $date_from_str, $date_to_str);
$sep = strpos($tabBase, '?') === false ? '?' : '&';
$views = array('entities' => 'Legal entities & hierarchy', 'addressbook' => 'Global address book', 'calendars' => 'Working calendars');
?>
<div id="epc_erp_msg" class="alert" style="display:none;"></div>

<div class="btn-group btn-group-sm" style="margin-bottom:10px;">
	<?php foreach ($views as $k => $lbl): ?>
		<a class="btn btn-<?php echo $view === $k ? 'primary' : 'default'; ?>" href="<?php echo epc_erp_h($tabBase . $sep . 'ov=' . $k); ?>"><?php echo epc_erp_h($lbl); ?></a>
	<?php endforeach; ?>
</div>

<?php if ($view === 'entities'):
	$tree = epc_org_branch_tree($db_link); ?>
	<p class="text-muted">Legal entities (companies) and their operating units / branches. Number sequences are configured under <strong>Setup &amp; Data ▸ Accounting setup</strong>.</p>
	<?php if (empty($tree)): ?><p class="text-muted">No legal entities defined.</p>
	<?php else: foreach ($tree as $node): ?>
		<div class="panel panel-default">
			<div class="panel-heading"><i class="fa fa-building"></i> <strong><?php echo epc_erp_h($node['company']['code'] ?? ''); ?></strong> — <?php echo epc_erp_h($node['company']['name'] ?? ''); ?></div>
			<table class="table table-condensed" style="margin-bottom:0;">
				<thead><tr><th>Unit code</th><th>Name</th><th>Type</th></tr></thead>
				<tbody>
				<?php if (empty($node['units'])): ?><tr><td colspan="3" class="text-muted">No operating units.</td></tr>
				<?php else: foreach ($node['units'] as $u): ?>
					<tr><td><?php echo epc_erp_h($u['code'] ?? ''); ?></td><td><?php echo epc_erp_h($u['name'] ?? ''); ?></td><td><span class="label label-default"><?php echo epc_erp_h($u['type'] ?? ''); ?></span></td></tr>
				<?php endforeach; endif; ?>
				</tbody>
			</table>
		</div>
	<?php endforeach; endif; ?>

<?php elseif ($view === 'addressbook'):
	$parties = epc_oa_parties($db_link, $companyId);
	$selParty = (int) ($_GET['party_id'] ?? 0); ?>
	<div class="row"><div class="col-md-5">
		<div class="well well-sm">
			<h5><i class="fa fa-plus-circle"></i> New party</h5>
			<form id="epc_oa_party" class="form-inline">
				<input type="hidden" name="csrf_guard_key" value="<?php echo epc_erp_h($csrfLocal); ?>">
				<select name="party_type" class="form-control input-sm"><option value="organization">organization</option><option value="person">person</option></select>
				<input type="text" name="name" class="form-control input-sm" placeholder="Name" style="width:180px;" required>
				<button class="btn btn-primary btn-sm">Save</button>
			</form>
		</div>
		<table class="table table-bordered table-condensed">
			<thead><tr><th>Name</th><th>Type</th><th class="text-right">Addr</th><th></th></tr></thead>
			<tbody>
			<?php if (empty($parties)): ?><tr><td colspan="4" class="text-muted">No parties.</td></tr>
			<?php else: foreach ($parties as $p): ?>
				<tr><td><strong><?php echo epc_erp_h($p['name']); ?></strong></td><td><span class="label label-default"><?php echo epc_erp_h($p['party_type']); ?></span></td>
				<td class="text-right"><?php echo (int) $p['address_count']; ?></td>
				<td><a class="btn btn-default btn-xs" href="<?php echo epc_erp_h($tabBase . $sep . 'ov=addressbook&party_id=' . (int) $p['id']); ?>">Open</a></td></tr>
			<?php endforeach; endif; ?>
			</tbody>
		</table>
	</div><div class="col-md-7">
		<?php if ($selParty > 0):
			$addrs = epc_oa_addresses($db_link, $selParty);
			$contacts = epc_oa_contacts($db_link, $selParty); ?>
			<div class="panel panel-default">
				<div class="panel-heading"><strong>Addresses</strong></div>
				<div class="panel-body">
					<form id="epc_oa_addr" class="form-inline" style="margin-bottom:8px;">
						<input type="hidden" name="csrf_guard_key" value="<?php echo epc_erp_h($csrfLocal); ?>">
						<input type="hidden" name="party_id" value="<?php echo (int) $selParty; ?>">
						<select name="purpose" class="form-control input-sm"><option value="business">business</option><option value="invoice">invoice</option><option value="delivery">delivery</option><option value="home">home</option><option value="other">other</option></select>
						<input type="text" name="line1" class="form-control input-sm" placeholder="Street" style="width:140px;">
						<input type="text" name="city" class="form-control input-sm" placeholder="City" style="width:90px;">
						<input type="text" name="country" class="form-control input-sm" placeholder="Country" style="width:80px;">
						<label><input type="checkbox" name="is_primary" value="1"> primary</label>
						<button class="btn btn-primary btn-sm">Add</button>
					</form>
					<table class="table table-condensed">
						<thead><tr><th>Purpose</th><th>Address</th><th>Primary</th></tr></thead>
						<tbody>
						<?php if (empty($addrs)): ?><tr><td colspan="3" class="text-muted">No addresses.</td></tr>
						<?php else: foreach ($addrs as $a): ?>
							<tr><td><span class="label label-info"><?php echo epc_erp_h($a['purpose']); ?></span></td>
							<td><?php echo epc_erp_h(trim($a['line1'] . ', ' . $a['city'] . ', ' . $a['country'], ', ')); ?></td>
							<td><?php echo (int) $a['is_primary'] ? '<i class="fa fa-check text-success"></i>' : ''; ?></td></tr>
						<?php endforeach; endif; ?>
						</tbody>
					</table>
				</div>
			</div>
			<div class="panel panel-default">
				<div class="panel-heading"><strong>Electronic contacts</strong></div>
				<div class="panel-body">
					<form id="epc_oa_contact" class="form-inline" style="margin-bottom:8px;">
						<input type="hidden" name="csrf_guard_key" value="<?php echo epc_erp_h($csrfLocal); ?>">
						<input type="hidden" name="party_id" value="<?php echo (int) $selParty; ?>">
						<select name="contact_type" class="form-control input-sm"><option value="email">email</option><option value="phone">phone</option><option value="mobile">mobile</option><option value="fax">fax</option><option value="url">url</option></select>
						<input type="text" name="value" class="form-control input-sm" placeholder="Value" style="width:180px;">
						<label><input type="checkbox" name="is_primary" value="1"> primary</label>
						<button class="btn btn-primary btn-sm">Add</button>
					</form>
					<table class="table table-condensed">
						<tbody>
						<?php if (empty($contacts)): ?><tr><td class="text-muted">No contacts.</td></tr>
						<?php else: foreach ($contacts as $c): ?>
							<tr><td><span class="label label-default"><?php echo epc_erp_h($c['contact_type']); ?></span> <?php echo epc_erp_h($c['value']); ?> <?php echo (int) $c['is_primary'] ? '<i class="fa fa-check text-success"></i>' : ''; ?></td></tr>
						<?php endforeach; endif; ?>
						</tbody>
					</table>
				</div>
			</div>
		<?php else: ?><p class="text-muted">Pick a party to manage addresses &amp; contacts.</p><?php endif; ?>
	</div></div>

<?php else:
	$cals = epc_oa_calendars($db_link, $companyId);
	$selCal = (int) ($_GET['calendar_id'] ?? 0); ?>
	<div class="row"><div class="col-md-5">
		<div class="well well-sm">
			<h5><i class="fa fa-calendar"></i> New calendar</h5>
			<form id="epc_oa_cal" class="form-inline">
				<input type="hidden" name="csrf_guard_key" value="<?php echo epc_erp_h($csrfLocal); ?>">
				<input type="text" name="code" class="form-control input-sm" placeholder="Code" style="width:100px;" required>
				<input type="text" name="name" class="form-control input-sm" placeholder="Name" style="width:130px;">
				<input type="text" name="working_days" class="form-control input-sm" placeholder="1,2,3,4,5" value="1,2,3,4,5" style="width:90px;" title="ISO weekdays 1=Mon..7=Sun">
				<button class="btn btn-primary btn-sm">Save</button>
			</form>
		</div>
		<table class="table table-bordered table-condensed">
			<thead><tr><th>Code</th><th>Working days</th><th class="text-right">Hol.</th><th></th></tr></thead>
			<tbody>
			<?php if (empty($cals)): ?><tr><td colspan="4" class="text-muted">No calendars.</td></tr>
			<?php else: foreach ($cals as $c): ?>
				<tr><td><strong><?php echo epc_erp_h($c['code']); ?></strong></td><td><?php echo epc_erp_h($c['working_days']); ?></td>
				<td class="text-right"><?php echo (int) $c['holiday_count']; ?></td>
				<td><a class="btn btn-default btn-xs" href="<?php echo epc_erp_h($tabBase . $sep . 'ov=calendars&calendar_id=' . (int) $c['id']); ?>">Holidays</a></td></tr>
			<?php endforeach; endif; ?>
			</tbody>
		</table>
	</div><div class="col-md-7">
		<?php if ($selCal > 0):
			$hols = epc_oa_holidays($db_link, $selCal); ?>
			<div class="panel panel-default">
				<div class="panel-heading"><strong>Holidays</strong></div>
				<div class="panel-body">
					<form id="epc_oa_hol" class="form-inline" style="margin-bottom:8px;">
						<input type="hidden" name="csrf_guard_key" value="<?php echo epc_erp_h($csrfLocal); ?>">
						<input type="hidden" name="calendar_id" value="<?php echo (int) $selCal; ?>">
						<input type="date" name="holiday_date" class="form-control input-sm" required>
						<input type="text" name="name" class="form-control input-sm" placeholder="Name" style="width:150px;">
						<button class="btn btn-primary btn-sm">Add</button>
					</form>
					<table class="table table-condensed">
						<thead><tr><th>Date</th><th>Name</th></tr></thead>
						<tbody>
						<?php if (empty($hols)): ?><tr><td colspan="2" class="text-muted">No holidays.</td></tr>
						<?php else: foreach ($hols as $h): ?>
							<tr><td><?php echo epc_erp_h($h['holiday_date']); ?></td><td><?php echo epc_erp_h($h['name']); ?></td></tr>
						<?php endforeach; endif; ?>
						</tbody>
					</table>
				</div>
			</div>
		<?php else: ?><p class="text-muted">Pick a calendar to manage its holidays. Working days are ISO weekday numbers (1=Mon … 7=Sun); calendars drive due-date arithmetic across the ERP.</p><?php endif; ?>
	</div></div>
<?php endif; ?>

<script>
(function(){
	var url = <?php echo json_encode(isset($erpAjaxEndpoint) ? $erpAjaxEndpoint : ('/' . (isset($DP_Config->backend_dir) ? $DP_Config->backend_dir : 'cp') . '/content/shop/finance/erp/ajax_erp_endpoint.php')); ?>;
	function post(action, fd){ fd.append('action', action); return fetch(url,{method:'POST',body:fd,credentials:'same-origin'}).then(function(r){return r.json();}); }
	function msg(j){ var el=document.getElementById('epc_erp_msg'); if(el){ el.className='alert alert-'+(j.status?'success':'danger'); el.textContent=j.message||''; el.style.display='block'; el.scrollIntoView({behavior:'smooth',block:'center'}); } if(j.status) setTimeout(function(){ location.reload(); }, 800); }
	function bind(id, action){ var f=document.getElementById(id); if(f) f.addEventListener('submit', function(e){ e.preventDefault(); post(action, new FormData(f)).then(msg); }); }
	bind('epc_oa_party', 'oa_party_save');
	bind('epc_oa_addr', 'oa_address_save');
	bind('epc_oa_contact', 'oa_contact_save');
	bind('epc_oa_cal', 'oa_calendar_save');
	bind('epc_oa_hol', 'oa_holiday_add');
})();
</script>
