<?php
defined('_ASTEXE_') or die('No access');

$paymentsUrl = '/' . $DP_Config->backend_dir . '/shop/payments/payments?tab=configure';
$csrfLocal = isset($user_session['csrf_guard_key']) ? $user_session['csrf_guard_key'] : '';
?>
<div class="col-lg-12">
	<p class="text-muted">Select the gateway used at checkout (one active at a time). Save dummy keys now — replace with live credentials from your acquirer later.</p>
</div>

<div class="col-lg-12">
	<div class="hpanel">
		<div class="panel-heading hbuilt">Active gateway</div>
		<div class="panel-body" id="current_system_indicator"></div>
	</div>
</div>

<div class="col-lg-6">
	<div class="hpanel">
		<div class="panel-heading hbuilt">Select gateway</div>
		<div class="panel-body">
			<select id="system_selector" class="form-control" onchange="on_system_changed();"></select>
		</div>
	</div>
</div>

<div class="col-lg-6">
	<div class="hpanel">
		<div class="panel-heading hbuilt">Credentials &amp; options</div>
		<div class="panel-body" id="mysql_options_div_fields"></div>
	</div>
</div>

<div class="col-lg-12">
	<button type="button" class="btn btn-success" onclick="save_payment_config();"><i class="fa fa-save"></i> Save &amp; activate</button>
</div>

<script>
var payment_systems = [];
<?php
$q = $db_link->prepare('SELECT * FROM `shop_payment_systems` WHERE `anable` = 1 ORDER BY `active` DESC, `id` ASC');
$q->execute();
while ($ps = $q->fetch(PDO::FETCH_ASSOC)) {
	$params = $ps['parameters'] === '' ? '[]' : $ps['parameters'];
	$pvals = $ps['parameters_values'] === '' ? '{}' : $ps['parameters_values'];
	$name = epc_payment_gateway_label($ps);
	$desc = function_exists('translate_str_by_id') ? translate_str_by_id($ps['description']) : (string)$ps['description'];
	?>
	payment_systems.push({
		id: <?php echo (int)$ps['id']; ?>,
		name: <?php echo json_encode($name); ?>,
		handler: <?php echo json_encode((string)$ps['handler']); ?>,
		parameters: JSON.parse(<?php echo json_encode($params); ?>),
		parameters_values: JSON.parse(<?php echo json_encode($pvals); ?>),
		description: <?php echo json_encode($desc); ?>,
		active: <?php echo (int)$ps['active']; ?>
	});
	<?php
}
?>

function on_system_changed() {
	var current_system_selected = document.getElementById('system_selector').value;
	var box = document.getElementById('mysql_options_div_fields');
	var html = '';
	for (var i = 0; i < payment_systems.length; i++) payment_systems[i].active = 0;
	for (var i = 0; i < payment_systems.length; i++) {
		if (String(payment_systems[i].id) !== String(current_system_selected)) continue;
		payment_systems[i].active = 1;
		var pv = payment_systems[i].parameters_values || {};
		for (var j = 0; j < payment_systems[i].parameters.length; j++) {
			var p = payment_systems[i].parameters[j];
			if (j > 0) html += '<div class="hr-line-dashed"></div>';
			html += '<div class="form-group"><label>' + p.caption + '</label>';
			if (p.type === 'checkbox') {
				html += '<div><input type="checkbox" id="' + p.name + '" ' + (pv[p.name] ? 'checked' : '') + '></div>';
			} else {
				html += '<input type="' + (p.type === 'password' ? 'password' : 'text') + '" class="form-control" id="' + p.name + '" value="' + (pv[p.name] || '') + '">';
			}
			html += '</div>';
		}
		if (payment_systems[i].description) {
			html += '<div class="well well-sm">' + payment_systems[i].description + '</div>';
		}
		html += '<p class="text-muted"><small>Webhook: <code><?php echo epc_payment_h(rtrim($DP_Config->domain_path, '/')); ?>/content/shop/finance/payment_systems/' + payment_systems[i].handler + '/notification.php</code></small></p>';
		break;
	}
	box.innerHTML = html || '<p class="text-muted">Select a gateway</p>';
}

function save_payment_config() {
	var system_id = document.getElementById('system_selector').value;
	var parameters_values = {};
	for (var i = 0; i < payment_systems.length; i++) {
		if (payment_systems[i].active !== 1) continue;
		for (var j = 0; j < payment_systems[i].parameters.length; j++) {
			var p = payment_systems[i].parameters[j];
			var el = document.getElementById(p.name);
			if (!el) continue;
			parameters_values[p.name] = p.type === 'checkbox' ? (el.checked ? 1 : 0) : el.value;
		}
	}
	var fd = new FormData();
	fd.append('action', 'save_config');
	fd.append('csrf_guard_key', <?php echo json_encode($csrfLocal); ?>);
	fd.append('system_id', system_id);
	fd.append('parameters_values', JSON.stringify(parameters_values));
	fetch(<?php echo json_encode($paymentsUrl); ?>, { method:'POST', body:fd, credentials:'same-origin' })
		.then(function(r){ return r.json(); })
		.then(function(j){
			var el = document.getElementById('epc_pay_msg');
			if (el) { el.className = 'alert alert-' + (j.status ? 'success' : 'danger'); el.textContent = j.message; el.style.display = 'block'; }
			if (j.status) setTimeout(function(){ location.reload(); }, 800);
		});
}

(function initConfigure(){
	var html = '<option value="0">— Disabled —</option>';
	var current = 0;
	for (var i = 0; i < payment_systems.length; i++) {
		html += '<option value="' + payment_systems[i].id + '">' + payment_systems[i].name + ' (' + payment_systems[i].handler + ')</option>';
		if (payment_systems[i].active === 1) {
			current = payment_systems[i].id;
			document.getElementById('current_system_indicator').innerHTML = '<strong>Active:</strong> ' + payment_systems[i].name + ' <code>' + payment_systems[i].handler + '</code>';
		}
	}
	document.getElementById('system_selector').innerHTML = html;
	document.getElementById('system_selector').value = current;
	if (!current) document.getElementById('current_system_indicator').innerHTML = '<span class="text-warning">No gateway active — select one below</span>';
	on_system_changed();
})();
</script>
