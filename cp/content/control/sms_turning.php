<?php
// SMS operators — Etisalat / du / GCC·MENA / Pakistan (+ legacy)
defined('_ASTEXE_') or die('No access');

$epc_sms_default_sender = '+971567607011';
$epc_helpers = rtrim((string) ($_SERVER['DOCUMENT_ROOT'] ?? ''), '/') . '/content/sms/epc_sms_helpers.php';
if (is_file($epc_helpers)) {
	require_once $epc_helpers;
	$epc_sms_default_sender = epc_sms_default_sender_number();
}

if (!empty($_POST['save_action'])) {
	require_once $_SERVER['DOCUMENT_ROOT'] . '/content/users/stop_csrf.php';

	$result = true;
	$system_id = (int) ($_POST['system_id'] ?? 0);

	if ($system_id > 0) {
		if ($db_link->prepare('UPDATE `sms_api` SET `active`=0;')->execute() != true) {
			$result = false;
		}
		if ($db_link->prepare('UPDATE `sms_api` SET `active`=1, `parameters_values` = ? WHERE `id` = ?;')->execute(array($_POST['parameters_values'], $system_id)) != true) {
			$result = false;
		}
	} else {
		if ($db_link->prepare('UPDATE `sms_api` SET `active`=0;')->execute() != true) {
			$result = false;
		}
	}

	if ($result) {
		$success_message = translate_str_by_id(2157);
		?>
		<script>
			location="/<?php echo $DP_Config->backend_dir; ?>/control/sms-operatory?success_message=<?php echo $success_message; ?>";
		</script>
		<?php
		exit;
	}

	$error_message = translate_str_by_id(2122);
	?>
	<script>
		location="/<?php echo $DP_Config->backend_dir; ?>/control/sms-operatory?error_message=<?php echo $error_message; ?>";
	</script>
	<?php
	exit;
}

require_once $_SERVER['DOCUMENT_ROOT'] . '/content/users/dp_user.php';
$user_session = DP_User::getAdminSession();

$sms_systems_for_js = array();
$sms_systems_query = $db_link->prepare('SELECT * FROM `sms_api` WHERE `control_available` = ? ORDER BY CASE WHEN `handler` LIKE ? THEN 0 ELSE 1 END, `id` ASC;');
$sms_systems_query->execute(array(1, 'epc_%'));
while ($sms_system = $sms_systems_query->fetch()) {
	$parameters = json_decode((string) ($sms_system['parameters'] ?? ''), true);
	if (!is_array($parameters)) {
		$parameters = array();
	}
	// Normalize associative maps → list (defensive)
	if ($parameters !== array() && array_keys($parameters) !== range(0, count($parameters) - 1)) {
		$normalized = array();
		foreach ($parameters as $k => $caption) {
			$normalized[] = array(
				'name' => (string) $k,
				'type' => 'text',
				'caption' => is_string($caption) ? $caption : (string) $k,
			);
		}
		$parameters = $normalized;
	}

	for ($i = 0; $i < count($parameters); $i++) {
		$rawCaption = (string) ($parameters[$i]['caption'] ?? $parameters[$i]['name'] ?? '');
		$translated = translate_str_by_id($rawCaption);
		if ($translated === null || $translated === '' || $translated === false || $translated === '==Empty string==') {
			$parameters[$i]['caption'] = $rawCaption;
		} else {
			$parameters[$i]['caption'] = (string) $translated;
		}
		if (($parameters[$i]['type'] ?? '') === 'select' && !empty($parameters[$i]['options']) && is_array($parameters[$i]['options'])) {
			for ($o = 0; $o < count($parameters[$i]['options']); $o++) {
				$oc = (string) ($parameters[$i]['options'][$o]['caption'] ?? '');
				$ot = translate_str_by_id($oc);
				$parameters[$i]['options'][$o]['caption'] = ($ot === null || $ot === '' || $ot === false || $ot === '==Empty string==') ? $oc : (string) $ot;
			}
		}
	}

	$values = json_decode((string) ($sms_system['parameters_values'] ?? ''), true);
	if (!is_array($values)) {
		$values = array();
	}

	$handler = (string) ($sms_system['handler'] ?? '');
	$group = (strpos($handler, 'epc_') === 0) ? 'mena' : 'other';

	$sms_systems_for_js[] = array(
		'id' => (int) $sms_system['id'],
		'name' => (string) $sms_system['name'],
		'handler' => $handler,
		'group' => $group,
		'parameters' => $parameters,
		'parameters_values' => $values,
		'description' => (string) ($sms_system['description'] ?? ''),
		'active' => (int) $sms_system['active'],
	);
}

$cp_base = '/' . trim((string) $DP_Config->backend_dir, '/');
?>

<?php require_once 'content/control/actions_alert.php'; ?>

<style>
.epc-sms-guide {
	background: linear-gradient(135deg, #f0f7f4 0%, #e8f0fa 100%);
	border: 1px solid #c5d6ce;
	border-radius: 4px;
	padding: 14px 16px;
	margin-bottom: 0;
	line-height: 1.45;
}
.epc-sms-guide h4 {
	margin: 0 0 8px;
	font-size: 15px;
	font-weight: 700;
	color: #1a3a2f;
}
.epc-sms-guide ul {
	margin: 0 0 0 18px;
	padding: 0;
}
.epc-sms-guide li { margin-bottom: 4px; }
.epc-sms-guide code {
	background: rgba(255,255,255,.7);
	padding: 1px 5px;
	border-radius: 3px;
	font-size: 12px;
}
.epc-sms-sender-hint {
	display: inline-block;
	margin-top: 6px;
	font-size: 12px;
	color: #2c5f4a;
}
.epc-sms-desc {
	color: #555;
	font-size: 13px;
	margin-top: 10px;
	padding-top: 10px;
	border-top: 1px dashed #ddd;
}
#mysql_options_div_fields .form-group label[for="sender_number"],
#mysql_options_div_fields label.epc-sms-sender-label {
	color: #1a5c40;
	font-weight: 600;
}
</style>

<div class="col-lg-12">
	<div class="hpanel">
		<div class="panel-heading hbuilt">
			<?php echo translate_str_by_id(2113); ?>
		</div>
		<div class="panel-body">
			<a class="panel_a" href="javascript:void(0);" onclick="save_action();">
				<div class="panel_a_img" style="background: url('/<?php echo $DP_Config->backend_dir; ?>/templates/<?php echo $DP_Template->name; ?>/images/save.png') 0 0 no-repeat;"></div>
				<div class="panel_a_caption"><?php echo translate_str_by_id(2114); ?></div>
			</a>
			<a class="panel_a" href="/<?php echo $DP_Config->backend_dir; ?>">
				<div class="panel_a_img" style="background: url('/<?php echo $DP_Config->backend_dir; ?>/templates/<?php echo $DP_Template->name; ?>/images/power_off.png') 0 0 no-repeat;"></div>
				<div class="panel_a_caption"><?php echo translate_str_by_id(2116); ?></div>
			</a>
		</div>
	</div>
</div>

<div class="col-lg-12">
	<div class="hpanel">
		<div class="panel-heading hbuilt">UAE / GCC / Pakistan SMS</div>
		<div class="panel-body">
			<div class="epc-sms-guide">
				<h4>How messaging is structured</h4>
				<ul>
					<li><strong>From number</strong> defaults to <code><?php echo htmlspecialchars($epc_sms_default_sender, ENT_QUOTES, 'UTF-8'); ?></code>. Change it anytime in the <em>Sender number</em> field — no code deploy needed.</li>
					<li><strong>Etisalat (e&amp;)</strong> and <strong>du</strong> need the partner API URL + key from your UAE Business Messaging contract (A2P / Sender ID may need TDRA registration).</li>
					<li><strong>Unifonic</strong> is the general GCC / MENA REST path — paste AppSid, keep or change the sender.</li>
					<li><strong>Pakistan / Jazz</strong> is for +92 recipients; sender can stay UAE or switch to a local mask later.</li>
					<li>Only <strong>one</strong> operator can be active. Save after selecting and filling credentials. Test under <a href="<?php echo htmlspecialchars($cp_base, ENT_QUOTES, 'UTF-8'); ?>/control/communications">Communications</a>.</li>
				</ul>
			</div>
		</div>
	</div>
</div>

<div class="col-lg-12">
	<div class="hpanel">
		<div class="panel-heading hbuilt">
			<?php echo translate_str_by_id(2487); ?>
		</div>
		<div class="panel-body" id="current_system_indicator"></div>
	</div>
</div>

<div class="col-lg-6">
	<div class="hpanel">
		<div class="panel-heading hbuilt">
			<?php echo translate_str_by_id(2488); ?>
		</div>
		<div class="panel-body">
			<select id="system_selector" name="system_selector" onchange="on_system_changed();" class="form-control"></select>
			<div id="system_description" class="epc-sms-desc" style="display:none;"></div>
		</div>
	</div>
</div>

<div class="col-lg-6">
	<div class="hpanel">
		<div class="panel-heading hbuilt">
			<?php echo translate_str_by_id(2490); ?>
		</div>
		<div class="panel-body" id="mysql_options_div_fields"></div>
	</div>
</div>

<form method="POST" name="form_to_save">
	<input type="hidden" name="save_action" value="ok" />
	<input type="hidden" name="system_id" id="system_id" value="" />
	<input type="hidden" name="parameters_values" id="parameters_values" value="" />
	<input type="hidden" name="csrf_guard_key" value="<?php echo htmlspecialchars((string) $user_session['csrf_guard_key'], ENT_QUOTES, 'UTF-8'); ?>" />
</form>

<script>
var sms_systems = <?php echo json_encode($sms_systems_for_js, JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES); ?>;
var epcSmsDefaultSender = <?php echo json_encode($epc_sms_default_sender, JSON_UNESCAPED_UNICODE); ?>;

function on_system_changed()
{
	var current_system_selected = document.getElementById('system_selector').value;
	var mysql_options_div_fields = document.getElementById('mysql_options_div_fields');
	var descEl = document.getElementById('system_description');
	var html = '';
	var selected = null;

	for (var i = 0; i < sms_systems.length; i++) {
		if (String(sms_systems[i].id) !== String(current_system_selected)) {
			sms_systems[i].active = 0;
			continue;
		}
		sms_systems[i].active = 1;
		selected = sms_systems[i];

		for (var j = 0; j < sms_systems[i].parameters.length; j++) {
			if (j > 0) {
				html += '<div class="hr-line-dashed col-lg-12"></div>';
			}
			var p = sms_systems[i].parameters[j];
			var isSender = (p.name === 'sender_number' || p.name === 'sender' || p.name === 'from' || p.name === 'sender_id');
			var labelClass = isSender ? 'epc-sms-sender-label' : '';
			html += '<div class="form-group"><label for="' + p.name + '" class="col-lg-6 control-label ' + labelClass + '">' + p.caption + '</label><div class="col-lg-6">';

			if (p.type === 'text' || p.type === 'number' || p.type === 'color' || p.type === 'password' || p.type === 'checkbox') {
				html += '<input type="' + p.type + '" id="' + p.name + '" name="' + p.name + '" value="" class="form-control" />';
				if (isSender) {
					html += '<span class="epc-sms-sender-hint">Default: ' + epcSmsDefaultSender + ' — edit anytime for future sends.</span>';
				}
			} else if (p.type === 'select') {
				html += '<select name="' + p.name + '" id="' + p.name + '" class="form-control">';
				for (var o = 0; o < p.options.length; o++) {
					html += '<option value="' + p.options[o].value + '">' + p.options[o].caption + '</option>';
				}
				html += '</select>';
			}
			html += '</div></div>';
		}
		break;
	}

	if (html === '') {
		html = <?php echo json_encode((string) translate_str_by_id(2489), JSON_UNESCAPED_UNICODE); ?>;
	}
	mysql_options_div_fields.innerHTML = html;

	if (selected && selected.description) {
		descEl.style.display = 'block';
		descEl.textContent = selected.description;
	} else {
		descEl.style.display = 'none';
		descEl.textContent = '';
	}

	// Load saved values for the selected operator (not only DB-active)
	if (selected) {
		for (var j = 0; j < selected.parameters.length; j++) {
			var pname = selected.parameters[j].name;
			var el = document.getElementById(pname);
			if (!el) continue;
			var val = selected.parameters_values[pname];
			if (selected.parameters[j].type === 'checkbox') {
				el.checked = !!val && val != 0 && val !== '0';
			} else {
				el.value = (val !== undefined && val !== null) ? val : '';
				if ((pname === 'sender_number' || pname === 'sender' || pname === 'from') && !el.value) {
					el.value = epcSmsDefaultSender;
				}
			}
		}
	}
}

function save_action()
{
	var system_id = document.getElementById('system_selector').value;
	document.getElementById('system_id').value = system_id;

	var parameters_values = {};
	for (var i = 0; i < sms_systems.length; i++) {
		if (sms_systems[i].active != 1) continue;
		for (var j = 0; j < sms_systems[i].parameters.length; j++) {
			var p = sms_systems[i].parameters[j];
			var el = document.getElementById(p.name);
			if (!el) continue;
			if (p.type === 'checkbox') {
				parameters_values[p.name] = el.checked ? 1 : 0;
			} else {
				parameters_values[p.name] = el.value;
			}
		}
	}
	document.getElementById('parameters_values').value = JSON.stringify(parameters_values);
	document.forms['form_to_save'].submit();
}

(function initSmsOperatorsPage() {
	var system_selector_html = '<option value="0">' + <?php echo json_encode((string) translate_str_by_id(2457), JSON_UNESCAPED_UNICODE); ?> + '</option>';
	var menaOpts = '';
	var otherOpts = '';
	var current_selected_id = 0;
	var indicator = document.getElementById('current_system_indicator');

	for (var i = 0; i < sms_systems.length; i++) {
		var opt = '<option value="' + sms_systems[i].id + '">' + sms_systems[i].name + '</option>';
		if (sms_systems[i].group === 'mena') {
			menaOpts += opt;
		} else {
			otherOpts += opt;
		}
		if (sms_systems[i].active == 1) {
			current_selected_id = sms_systems[i].id;
			indicator.innerHTML = 'SMS messages are currently sent via: <strong>' + sms_systems[i].name + '</strong>'
				+ (sms_systems[i].parameters_values && sms_systems[i].parameters_values.sender_number
					? ' &nbsp;·&nbsp; From <code>' + sms_systems[i].parameters_values.sender_number + '</code>'
					: ' &nbsp;·&nbsp; From <code>' + epcSmsDefaultSender + '</code>');
		}
	}

	if (menaOpts) {
		system_selector_html += '<optgroup label="UAE / GCC / Pakistan">' + menaOpts + '</optgroup>';
	}
	if (otherOpts) {
		system_selector_html += '<optgroup label="Other operators">' + otherOpts + '</optgroup>';
	}

	document.getElementById('system_selector').innerHTML = system_selector_html;
	document.getElementById('system_selector').value = current_selected_id;

	if (current_selected_id == 0) {
		indicator.innerHTML = <?php echo json_encode((string) translate_str_by_id(2491), JSON_UNESCAPED_UNICODE); ?>;
	}

	on_system_changed();
})();
</script>
<?php
?>
