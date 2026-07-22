<?php
/*
 * Registration fields editor — UAE e-invoice / KYC / AML compliance workbench.
 */
defined('_ASTEXE_') or die('No access');

require_once $_SERVER['DOCUMENT_ROOT'] . '/content/users/epc_reg_fields_compliance.php';

function tree_htmlentities($data)
{
	foreach ($data as $key => $item) {
		if (htmlentities($key) != $key) {
			unset($data[$key]);
			continue;
		}
		if (is_array($item)) {
			$item = tree_htmlentities($item);
		} else {
			if ($item === (int) $item) {
				$item = (int) $item;
			} else {
				$item = htmlentities($item, ENT_QUOTES, 'UTF-8', false);
			}
		}
		$data[$key] = $item;
	}
	return $data;
}

function epc_rf_t($id, $fallback = '')
{
	$t = function_exists('translate_str_by_id') ? translate_str_by_id($id) : '';
	if ($t === null || $t === false) {
		$t = '';
	}
	$t = trim((string) $t);
	if ($t === '' || strcasecmp($t, 'null') === 0) {
		return $fallback;
	}
	return $t;
}

function epc_rf_norm_widget($w)
{
	$w = strtolower(trim((string) $w));
	return in_array($w, array('text', 'file', 'select'), true) ? $w : 'text';
}

function epc_rf_norm_category($c)
{
	$c = strtolower(trim((string) $c));
	$allowed = array_keys(epc_rf_categories());
	return in_array($c, $allowed, true) ? $c : 'general';
}

if (!empty($_POST['seed_uae_pack']) || !empty($_POST['mark_all_approval']) || !empty($_POST['save_action'])) {
	require_once $_SERVER['DOCUMENT_ROOT'] . '/content/users/stop_csrf.php';
}

if (!empty($_POST['seed_uae_pack'])) {
	epc_rf_ensure_schema($db_link);
	$result = epc_rf_seed_uae_pack($db_link);
	$msg = 'UAE compliance pack: added ' . (int) $result['added'] . ', skipped existing ' . (int) $result['skipped'] . '.';
	?>
	<script>location="<?php echo $DP_Config->domain_path . $DP_Config->backend_dir; ?>/users/polya-registracii?success_message=<?php echo rawurlencode($msg); ?>";</script>
	<?php
	exit;
}

if (!empty($_POST['mark_all_approval'])) {
	epc_rf_ensure_schema($db_link);
	$n = epc_rf_mark_all_for_approval($db_link);
	$msg = 'Marked ' . (int) $n . ' fields available for customer approval.';
	?>
	<script>location="<?php echo $DP_Config->domain_path . $DP_Config->backend_dir; ?>/users/polya-registracii?success_message=<?php echo rawurlencode($msg); ?>";</script>
	<?php
	exit;
}

if (!empty($_POST['save_action'])) {
	epc_rf_ensure_schema($db_link);

	if ($db_link->prepare('UPDATE `reg_fields` SET `order` = 0 WHERE `main_flag` != 1;')->execute() != true) {
		$error_message = epc_rf_t(3868, 'Could not prepare fields for save');
		?>
		<script>location="<?php echo $DP_Config->domain_path . $DP_Config->backend_dir; ?>/users/polya-registracii?error_message=<?php echo rawurlencode($error_message); ?>";</script>
		<?php
		exit;
	}

	$reg_fields = json_decode($_POST['tree_json'], true);
	$reg_fields = tree_htmlentities($reg_fields);

	for ($i = 0; $i < count($reg_fields); $i++) {
		$order = $i + 1;
		$show_for = json_encode($reg_fields[$i]['show_for']);
		$required_for = json_encode($reg_fields[$i]['required_for']);

		$reg_fields[$i]['value'] = save_custom_translation($reg_fields[$i]['value_lang_str_id'], $reg_fields[$i]['value']);
		$reg_fields[$i]['example'] = save_custom_translation($reg_fields[$i]['example_lang_str_id'], $reg_fields[$i]['example']);

		$widget = epc_rf_norm_widget($reg_fields[$i]['widget_type'] ?? 'text');
		$category = epc_rf_norm_category($reg_fields[$i]['field_category'] ?? 'general');
		$avail = !empty($reg_fields[$i]['available_for_approval']) ? 1 : 0;
		$forCust = !empty($reg_fields[$i]['for_customer_approval']) ? 1 : 0;
		$forVend = !empty($reg_fields[$i]['for_vendor_approval']) ? 1 : 0;
		$tag = preg_replace('/[^a-z0-9_]/', '', strtolower((string) ($reg_fields[$i]['compliance_tag'] ?? '')));
		$to_filter = !empty($reg_fields[$i]['to_filter']) ? 1 : 0;
		$to_users_table = !empty($reg_fields[$i]['to_users_table']) ? 1 : 0;
		$maxlen = (int) $reg_fields[$i]['maxlen'];
		$regexp = (string) $reg_fields[$i]['regexp'];
		$name = (string) $reg_fields[$i]['name'];
		$caption = $reg_fields[$i]['value'];
		$example = $reg_fields[$i]['example'];

		if ($reg_fields[$i]['is_new'] == true) {
			$ok = $db_link->prepare('INSERT INTO `reg_fields` (`main_flag`, `name`, `caption`, `show_for`, `required_for`, `maxlen`, `regexp`, `widget_type`, `widget_options`, `example`, `order`, `to_filter`, `to_users_table`, `field_category`, `available_for_approval`, `for_customer_approval`, `for_vendor_approval`, `compliance_tag`) VALUES (?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?);')
				->execute(array(0, $name, $caption, $show_for, $required_for, $maxlen, $regexp, $widget, '[]', $example, $order, $to_filter, $to_users_table, $category, $avail, $forCust, $forVend, $tag));
			if ($ok != true) {
				$error_message = epc_rf_t(3869, 'Could not create field');
				?>
				<script>location="<?php echo $DP_Config->domain_path . $DP_Config->backend_dir; ?>/users/polya-registracii?error_message=<?php echo rawurlencode($error_message); ?>";</script>
				<?php
				exit;
			}
		} else {
			$ok = $db_link->prepare('UPDATE `reg_fields` SET `main_flag` = ?, `name` = ?, `caption` = ?, `show_for` = ?, `required_for` = ?, `maxlen` = ?, `regexp` = ?, `widget_type` = ?, `widget_options` = ?, `example` = ?, `order` = ?, `to_filter` = ?, `to_users_table` = ?, `field_category` = ?, `available_for_approval` = ?, `for_customer_approval` = ?, `for_vendor_approval` = ?, `compliance_tag` = ? WHERE `record_id` = ?;')
				->execute(array(0, $name, $caption, $show_for, $required_for, $maxlen, $regexp, $widget, '[]', $example, $order, $to_filter, $to_users_table, $category, $avail, $forCust, $forVend, $tag, $reg_fields[$i]['id']));
			if ($ok != true) {
				$error_message = epc_rf_t(3870, 'Could not update field');
				?>
				<script>location="<?php echo $DP_Config->domain_path . $DP_Config->backend_dir; ?>/users/polya-registracii?error_message=<?php echo rawurlencode($error_message); ?>";</script>
				<?php
				exit;
			}
		}
	}

	if ($db_link->prepare('DELETE FROM `reg_fields` WHERE `order` = 0 AND `main_flag` = 0;')->execute() != true) {
		$error_message = epc_rf_t(3871, 'Could not delete removed fields');
		?>
		<script>location="<?php echo $DP_Config->domain_path . $DP_Config->backend_dir; ?>/users/polya-registracii?error_message=<?php echo rawurlencode($error_message); ?>";</script>
		<?php
		exit;
	}

	$success_message = epc_rf_t(2157, 'Saved');
	?>
	<script>location="<?php echo $DP_Config->domain_path . $DP_Config->backend_dir; ?>/users/polya-registracii?success_message=<?php echo rawurlencode($success_message); ?>";</script>
	<?php
	exit;
}

// Display
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/users/dp_user.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_cp_page_frame.php';
$user_session = DP_User::getAdminSession();
epc_rf_ensure_schema($db_link);

$backend = trim((string) ($DP_Config->backend_dir ?? 'cp'), '/');
$backendH = htmlspecialchars($backend, ENT_QUOTES, 'UTF-8');
$categories = epc_rf_categories();

$reg_fields = array();
$reg_fields_query = $db_link->prepare('SELECT * FROM `reg_fields` WHERE `main_flag` = 0 ORDER BY `order` ASC;');
$reg_fields_query->execute();
while ($reg_field = $reg_fields_query->fetch(PDO::FETCH_ASSOC)) {
	$reg_field['caption_lang_str_id'] = $reg_field['caption'];
	$reg_field['caption'] = translate_str_by_id($reg_field['caption']);
	$reg_field['example_lang_str_id'] = $reg_field['example'];
	$ex = translate_str_by_id($reg_field['example']);
	$reg_field['example'] = ($ex === null || $ex === false || strcasecmp((string) $ex, 'null') === 0) ? '' : (string) $ex;

	$reg_fields[] = array(
		'id' => (int) $reg_field['record_id'],
		'to_filter' => (int) $reg_field['to_filter'],
		'to_users_table' => (int) $reg_field['to_users_table'],
		'is_new' => 0,
		'value' => $reg_field['caption'],
		'value_lang_str_id' => $reg_field['caption_lang_str_id'],
		'name' => $reg_field['name'],
		'old_name' => $reg_field['name'],
		'maxlen' => (int) $reg_field['maxlen'],
		'regexp' => (string) $reg_field['regexp'],
		'example' => $reg_field['example'],
		'example_lang_str_id' => $reg_field['example_lang_str_id'],
		'show_for' => json_decode((string) $reg_field['show_for'], true) ?: array(),
		'required_for' => json_decode((string) $reg_field['required_for'], true) ?: array(),
		'widget_type' => epc_rf_norm_widget($reg_field['widget_type'] ?? 'text'),
		'field_category' => epc_rf_norm_category($reg_field['field_category'] ?? 'general'),
		'available_for_approval' => (int) ($reg_field['available_for_approval'] ?? 1),
		'for_customer_approval' => (int) ($reg_field['for_customer_approval'] ?? 0),
		'for_vendor_approval' => (int) ($reg_field['for_vendor_approval'] ?? 0),
		'compliance_tag' => (string) ($reg_field['compliance_tag'] ?? ''),
	);
}
$reg_fields_json = json_encode($reg_fields);

$reg_variants = array();
$reg_variants_query = $db_link->prepare('SELECT * FROM `reg_variants` ORDER BY `order`;');
$reg_variants_query->execute();
while ($reg_variant = $reg_variants_query->fetch(PDO::FETCH_ASSOC)) {
	$reg_variant['caption'] = translate_str_by_id($reg_variant['caption']);
	$reg_variants[] = array('id' => (int) $reg_variant['id'], 'value' => $reg_variant['caption'], 'is_new' => 0);
}

$countByCat = array();
foreach ($reg_fields as $rf) {
	$c = $rf['field_category'];
	$countByCat[$c] = ($countByCat[$c] ?? 0) + 1;
}
$approvalCount = 0;
foreach ($reg_fields as $rf) {
	if (!empty($rf['available_for_approval'])) {
		$approvalCount++;
	}
}

if (function_exists('epc_cp_page_frame_open')) {
	epc_cp_page_frame_open(array(
		'class' => 'epc-rf-cp-frame',
		'hero' => array(
			'badge' => 'Users',
			'title' => 'Registration & compliance fields',
			'sub' => 'Configure customer/vendor registration data for UAE e-invoice (FTA), KYC/AML, and document approval.',
			'actions' => array(
				array('url' => '/' . $backend . '/users/customer_approvals', 'label' => 'Customer approvals', 'icon' => 'fa-check-square', 'primary' => true),
				array('url' => '/' . $backend . '/users/vendor_approvals', 'label' => 'Vendor approvals', 'icon' => 'fa-truck'),
				array('url' => '/' . $backend . '/users/registracionnye-varianty', 'label' => 'Reg. variants', 'icon' => 'fa-list'),
			),
		),
	));
}

require_once 'content/control/actions_alert.php';
?>

<div class="epc-rf-cp">
	<div class="epc-rf-cp__toolbar" role="toolbar">
		<button type="button" class="epc-rf-btn" onclick="add_new_item();"><i class="fa fa-plus"></i> <?php echo htmlspecialchars(epc_rf_t(2267, 'Add'), ENT_QUOTES, 'UTF-8'); ?></button>
		<button type="button" class="epc-rf-btn epc-rf-btn--danger" onclick="delete_selected_item();"><i class="fa fa-trash"></i> <?php echo htmlspecialchars(epc_rf_t(2224, 'Delete'), ENT_QUOTES, 'UTF-8'); ?></button>
		<button type="button" class="epc-rf-btn" onclick="unselect_tree();"><i class="fa fa-times"></i> <?php echo htmlspecialchars(epc_rf_t(2268, 'Clear'), ENT_QUOTES, 'UTF-8'); ?></button>
		<button type="button" class="epc-rf-btn epc-rf-btn--primary" onclick="save_tree();"><i class="fa fa-save"></i> <?php echo htmlspecialchars(epc_rf_t(2114, 'Save'), ENT_QUOTES, 'UTF-8'); ?></button>
		<span class="epc-rf-cp__spacer"></span>
		<button type="button" class="epc-rf-btn epc-rf-btn--accent" onclick="document.getElementById('seed_uae_form').submit();"><i class="fa fa-magic"></i> Seed UAE pack</button>
		<button type="button" class="epc-rf-btn" onclick="document.getElementById('mark_all_form').submit();"><i class="fa fa-flag"></i> Enable all for approval</button>
		<a class="epc-rf-btn epc-rf-btn--muted" href="/<?php echo $backendH; ?>"><i class="fa fa-home"></i> <?php echo htmlspecialchars(epc_rf_t(2116, 'Control panel'), ENT_QUOTES, 'UTF-8'); ?></a>
	</div>

	<p class="epc-rf-cp__hint">
		Fields marked <strong>Available for approval</strong> appear in customer/vendor review queues. Seed the UAE pack to add TRN, trade licence, Emirates ID, UBO, PEP, and document slots aligned with FTA e-invoicing and AML/KYC practice.
	</p>

	<div class="epc-rf-cp__packs">
		<div class="epc-rf-cp__pack"><strong>E-invoice / VAT</strong><span>TRN (15 digits), legal name, trade licence, legal ID type for FTA buyer profiles.</span></div>
		<div class="epc-rf-cp__pack"><strong>KYC / AML</strong><span>Signatory, UBO, PEP &amp; sanctions declarations per UAE compliance expectations.</span></div>
		<div class="epc-rf-cp__pack"><strong>Documents</strong><span>Trade licence, VAT cert, Emirates ID, passport, POA, MOA — reviewable on approval.</span></div>
		<div class="epc-rf-cp__pack"><strong>Approval coverage</strong><span><?php echo (int) $approvalCount; ?> / <?php echo count($reg_fields); ?> fields available for approval review.</span></div>
	</div>

	<div class="epc-rf-cp__chips" aria-label="Filter by category">
		<button type="button" class="epc-rf-cp__chip is-active" data-category="all">All (<?php echo count($reg_fields); ?>)</button>
		<?php foreach ($categories as $catKey => $catLabel) {
			$n = (int) ($countByCat[$catKey] ?? 0);
			if ($n === 0 && $catKey === 'general' && empty($countByCat)) { /* still show */ }
			?>
		<button type="button" class="epc-rf-cp__chip" data-category="<?php echo htmlspecialchars($catKey, ENT_QUOTES, 'UTF-8'); ?>"><?php echo htmlspecialchars($catLabel, ENT_QUOTES, 'UTF-8'); ?> (<?php echo $n; ?>)</button>
		<?php } ?>
	</div>

	<div class="epc-rf-cp__workspace">
		<div class="epc-rf-cp__pane">
			<div class="epc-rf-cp__pane-h">
				<h3><?php echo htmlspecialchars(epc_rf_t(3872, 'Fields list'), ENT_QUOTES, 'UTF-8'); ?></h3>
				<span>Drag to reorder · double-click to rename caption</span>
			</div>
			<div class="epc-rf-cp__search">
				<i class="fa fa-search" aria-hidden="true"></i>
				<input type="search" id="epc_rf_tree_filter" placeholder="Filter by name, key, category…" autocomplete="off" />
			</div>
			<div class="epc-rf-cp__tree-wrap">
				<div id="container_A"></div>
			</div>
		</div>

		<div class="epc-rf-cp__pane" id="content_info_div_col">
			<div class="epc-rf-cp__pane-h">
				<h3><?php echo htmlspecialchars(epc_rf_t(3873, 'Field details'), ENT_QUOTES, 'UTF-8'); ?></h3>
				<span>Category, widget, validation, variants, approval flags</span>
			</div>
			<div class="epc-rf-cp__detail" id="content_info_div">
				<div class="epc-rf-empty">Select a field to configure it for registration and approval.</div>
			</div>
		</div>
	</div>
</div>

<form name="form_to_save" method="post" style="display:none" aria-hidden="true">
	<input name="save_action" id="save_action" type="hidden" value="ok" />
	<input name="tree_json" id="tree_json" type="hidden" value="" />
	<input type="hidden" name="csrf_guard_key" value="<?php echo htmlspecialchars((string) $user_session['csrf_guard_key'], ENT_QUOTES, 'UTF-8'); ?>" />
</form>
<form id="seed_uae_form" method="post" style="display:none" aria-hidden="true">
	<input type="hidden" name="seed_uae_pack" value="1" />
	<input type="hidden" name="csrf_guard_key" value="<?php echo htmlspecialchars((string) $user_session['csrf_guard_key'], ENT_QUOTES, 'UTF-8'); ?>" />
</form>
<form id="mark_all_form" method="post" style="display:none" aria-hidden="true">
	<input type="hidden" name="mark_all_approval" value="1" />
	<input type="hidden" name="csrf_guard_key" value="<?php echo htmlspecialchars((string) $user_session['csrf_guard_key'], ENT_QUOTES, 'UTF-8'); ?>" />
</form>

<script type="text/javascript" charset="utf-8">
var EPC_RF_CATEGORIES = <?php echo json_encode($categories); ?>;

webix.protoUI({ name: 'edittree' }, webix.EditAbility, webix.ui.tree);

tree = new webix.ui({
	editable: true,
	editValue: 'value',
	editaction: 'dblclick',
	container: 'container_A',
	view: 'edittree',
	select: true,
	drag: true,
	editor: 'text',
	filterMode: { showSubItems: true },
	template: function (obj, common) {
		var label = (obj.value == null || obj.value === 'null') ? (obj.name || ('Field #' + obj.id)) : String(obj.value);
		var icons = '';
		if (parseInt(obj.available_for_approval, 10) === 1) { icons += ' <i class="fa fa-flag" title="Approval"></i>'; }
		if (String(obj.widget_type) === 'file') { icons += ' <i class="fa fa-paperclip" title="Document"></i>'; }
		if (String(obj.field_category) === 'einvoice') { icons += ' <i class="fa fa-file-text-o" title="E-invoice"></i>'; }
		if (String(obj.field_category) === 'kyc_aml') { icons += ' <i class="fa fa-shield" title="KYC/AML"></i>'; }
		return common.icon(obj, common) + common.folder(obj, common) + '<span>' + label.replace(/</g, '&lt;') + '</span>' + icons;
	}
});
webix.event(window, 'resize', function () { tree.adjust(); });
if (typeof window.epcRfBindTreeFilter === 'function') {
	window.epcRfBindTreeFilter('epc_rf_tree_filter', function () { return tree; });
}
if (typeof window.epcRfBindCategoryChips === 'function') {
	window.epcRfBindCategoryChips('.epc-rf-cp__chip', function () { return tree; });
}

tree.attachEvent('onAfterSelect', function () { onSelected(); });

function epcRfEsc(v) {
	return String(v == null ? '' : v)
		.replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;').replace(/"/g, '&quot;');
}

function onSelected() {
	var box = document.getElementById('content_info_div');
	if (tree.count() == 0) {
		box.innerHTML = '<div class="epc-rf-empty">No custom fields yet. Click <strong>Seed UAE pack</strong> or Add.</div>';
		return;
	}
	var node_id = tree.getSelectedId();
	if (!node_id) {
		box.innerHTML = '<div class="epc-rf-empty">Select a field to configure it for registration and approval.</div>';
		return;
	}
	var node = tree.getItem(node_id);
	var ID_for_show = node.is_new == true ? <?php echo json_encode(epc_rf_t(3874, 'New')); ?> : node.id;
	var cat = String(node.field_category || 'general');
	var html = '';

	html += '<div class="epc-rf-cp__badges">';
	html += '<span class="epc-rf-cp__badge">' + epcRfEsc(EPC_RF_CATEGORIES[cat] || cat) + '</span>';
	if (parseInt(node.available_for_approval, 10) === 1) html += '<span class="epc-rf-cp__badge epc-rf-cp__badge--ok"><i class="fa fa-flag"></i> Approval</span>';
	if (parseInt(node.for_customer_approval, 10) === 1) html += '<span class="epc-rf-cp__badge epc-rf-cp__badge--ok">Customer</span>';
	if (parseInt(node.for_vendor_approval, 10) === 1) html += '<span class="epc-rf-cp__badge epc-rf-cp__badge--ok">Vendor</span>';
	if (String(node.widget_type) === 'file') html += '<span class="epc-rf-cp__badge epc-rf-cp__badge--doc"><i class="fa fa-paperclip"></i> Document</span>';
	if (cat === 'einvoice') html += '<span class="epc-rf-cp__badge epc-rf-cp__badge--einvoice">E-invoice</span>';
	if (cat === 'kyc_aml') html += '<span class="epc-rf-cp__badge epc-rf-cp__badge--kyc">KYC/AML</span>';
	html += '</div>';

	html += '<div class="epc-rf-section"><div class="epc-rf-section-title">Basics</div>';
	html += '<div class="form-group"><label class="col-lg-6 control-label">ID</label><div class="col-lg-6">' + epcRfEsc(ID_for_show) + '</div></div>';
	html += '<div class="form-group"><label class="col-lg-6 control-label"><?php echo htmlspecialchars(epc_rf_t(2277, 'Caption'), ENT_QUOTES, 'UTF-8'); ?></label><div class="col-lg-6">' + epcRfEsc(node.value) + '</div></div>';
	html += '<div class="form-group"><label class="col-lg-6 control-label"><?php echo htmlspecialchars(epc_rf_t(2460, 'Key'), ENT_QUOTES, 'UTF-8'); ?></label><div class="col-lg-6"><input type="text" onKeyUp="dynamicApplying(\'name\');" id="name" value="' + epcRfEsc(node.name) + '" class="form-control" /></div></div>';
	html += '<div class="form-group"><label class="col-lg-6 control-label">Category</label><div class="col-lg-6"><select id="field_category" class="form-control" onchange="dynamicSelectApplying(\'field_category\');">';
	for (var ck in EPC_RF_CATEGORIES) {
		if (!EPC_RF_CATEGORIES.hasOwnProperty(ck)) continue;
		html += '<option value="' + epcRfEsc(ck) + '"' + (cat === ck ? ' selected' : '') + '>' + epcRfEsc(EPC_RF_CATEGORIES[ck]) + '</option>';
	}
	html += '</select></div></div>';
	html += '<div class="form-group"><label class="col-lg-6 control-label">Widget</label><div class="col-lg-6"><select id="widget_type" class="form-control" onchange="dynamicSelectApplying(\'widget_type\');">';
	['text', 'file', 'select'].forEach(function (w) {
		html += '<option value="' + w + '"' + (String(node.widget_type) === w ? ' selected' : '') + '>' + w + '</option>';
	});
	html += '</select></div></div>';
	html += '<div class="form-group"><label class="col-lg-6 control-label">Compliance tag</label><div class="col-lg-6"><input type="text" onKeyUp="dynamicApplying(\'compliance_tag\');" id="compliance_tag" value="' + epcRfEsc(node.compliance_tag || '') + '" class="form-control" placeholder="trn, emirates_id, doc_tl…" /></div></div>';
	html += '</div>';

	html += '<div class="epc-rf-section"><div class="epc-rf-section-title">Approval &amp; listing</div>';
	html += flagCheckbox('available_for_approval', 'Available for approval', node.available_for_approval);
	html += flagCheckbox('for_customer_approval', 'Customer approval queue', node.for_customer_approval);
	html += flagCheckbox('for_vendor_approval', 'Vendor approval queue', node.for_vendor_approval);
	html += flagCheckbox('to_filter', <?php echo json_encode(epc_rf_t(3875, 'Use in users filter')); ?>, node.to_filter);
	html += flagCheckbox('to_users_table', <?php echo json_encode(epc_rf_t(3876, 'Show in users table')); ?>, node.to_users_table);
	html += '</div>';

	html += '<div class="epc-rf-section"><div class="epc-rf-section-title">Validation</div>';
	html += '<div class="form-group"><label class="col-lg-6 control-label"><?php echo htmlspecialchars(epc_rf_t(3877, 'Max length'), ENT_QUOTES, 'UTF-8'); ?></label><div class="col-lg-6"><input type="text" onKeyUp="dynamicApplying(\'maxlen\');" id="maxlen" value="' + epcRfEsc(node.maxlen) + '" class="form-control" /></div></div>';
	html += '<div class="form-group"><label class="col-lg-6 control-label"><?php echo htmlspecialchars(epc_rf_t(3878, 'Regexp'), ENT_QUOTES, 'UTF-8'); ?></label><div class="col-lg-6"><input type="text" onKeyUp="dynamicApplying(\'regexp\');" id="regexp" value="' + epcRfEsc(node.regexp) + '" class="form-control" /></div></div>';
	html += '<div class="form-group"><label class="col-lg-6 control-label"><?php echo htmlspecialchars(epc_rf_t(3879, 'Example'), ENT_QUOTES, 'UTF-8'); ?></label><div class="col-lg-6"><input type="text" onKeyUp="dynamicApplying(\'example\');" id="example" value="' + epcRfEsc(node.example) + '" class="form-control" /></div></div>';
	html += '</div>';

	html += '<div class="epc-rf-section"><div class="epc-rf-section-title"><?php echo htmlspecialchars(epc_rf_t(3880, 'Registration variants'), ENT_QUOTES, 'UTF-8'); ?></div>';
	html += '<div class="table-responsive"><table class="table table-condensed table-striped"><thead><tr><th><?php echo htmlspecialchars(epc_rf_t(3881, 'Variant'), ENT_QUOTES, 'UTF-8'); ?></th><th><?php echo htmlspecialchars(epc_rf_t(3882, 'Show'), ENT_QUOTES, 'UTF-8'); ?></th><th><?php echo htmlspecialchars(epc_rf_t(3883, 'Required'), ENT_QUOTES, 'UTF-8'); ?></th></tr></thead><tbody>';
	<?php for ($i = 0; $i < count($reg_variants); $i++) { ?>
	(function () {
		var show_for = (node.show_for.indexOf(<?php echo (int) $reg_variants[$i]['id']; ?>) >= 0) ? 'checked' : '';
		var required_for = (node.required_for.indexOf(<?php echo (int) $reg_variants[$i]['id']; ?>) >= 0) ? 'checked' : '';
		html += '<tr><td><?php echo htmlspecialchars((string) $reg_variants[$i]['value'], ENT_QUOTES, 'UTF-8'); ?></td>';
		html += '<td class="text-center"><input onchange="dynamicApplyingCheckboxes(\'show_for\', <?php echo (int) $reg_variants[$i]['id']; ?>);" type="checkbox" id="show_for_input_<?php echo (int) $reg_variants[$i]['id']; ?>" ' + show_for + '/></td>';
		html += '<td class="text-center"><input onchange="dynamicApplyingCheckboxes(\'required_for\', <?php echo (int) $reg_variants[$i]['id']; ?>);" type="checkbox" id="required_for_input_<?php echo (int) $reg_variants[$i]['id']; ?>" ' + required_for + '/></td></tr>';
	})();
	<?php } ?>
	html += '</tbody></table></div></div>';

	box.innerHTML = html;
}

function flagCheckbox(attr, label, value) {
	var checked = parseInt(value, 10) === 1 ? ' checked="checked"' : '';
	return '<div class="form-group"><label class="col-lg-6 control-label">' + epcRfEsc(label) + '</label><div class="col-lg-6"><input type="checkbox" class="form-control" id="' + attr + '" onChange="flagHandler(\'' + attr + '\');"' + checked + ' /></div></div>';
}

function flagHandler(attr) {
	var node = tree.getItem(tree.getSelectedId());
	node[attr] = document.getElementById(attr).checked ? 1 : 0;
	onSelected();
}

function dynamicApplying(attribute) {
	var node = tree.getItem(tree.getSelectedId());
	node[attribute] = document.getElementById(attribute).value.replace(/"/g, '&quot;');
}

function dynamicSelectApplying(attribute) {
	var node = tree.getItem(tree.getSelectedId());
	node[attribute] = document.getElementById(attribute).value;
	onSelected();
}

function dynamicApplyingCheckboxes(type, reg_variant_id) {
	var node = tree.getItem(tree.getSelectedId());
	if (type == 'show_for') {
		if (document.getElementById('show_for_input_' + reg_variant_id).checked) {
			node.show_for.push(reg_variant_id);
		} else {
			node.show_for.splice(node.show_for.indexOf(reg_variant_id), 1);
		}
	} else {
		if (document.getElementById('required_for_input_' + reg_variant_id).checked) {
			node.required_for.push(reg_variant_id);
		} else {
			node.required_for.splice(node.required_for.indexOf(reg_variant_id), 1);
		}
	}
}

var reg_variants = {};
<?php foreach ($reg_variants as $rv) { ?>
reg_variants["<?php echo (int) $rv['id']; ?>"] = <?php echo json_encode((string) $rv['value']); ?>;
<?php } ?>

tree.attachEvent('onAfterEditStop', function () { onSelected(); });
tree.attachEvent('onAfterDrop', function () { onSelected(); });

function add_new_item() {
	tree.add({
		value: <?php echo json_encode(epc_rf_t(2908, 'New field')); ?>,
		value_lang_str_id: 0,
		is_new: true,
		name: '',
		maxlen: 0,
		regexp: '',
		example: '',
		example_lang_str_id: 0,
		show_for: [],
		required_for: [],
		to_filter: 0,
		to_users_table: 0,
		widget_type: 'text',
		field_category: 'general',
		available_for_approval: 1,
		for_customer_approval: 1,
		for_vendor_approval: 0,
		compliance_tag: ''
	}, 0, 0);
	onSelected();
}

function delete_selected_item() {
	var nodeId = tree.getSelectedId();
	if (!nodeId) return;
	tree.remove(nodeId);
	onSelected();
}

function unselect_tree() {
	tree.unselect();
	onSelected();
}

function save_tree() {
	var tree_json_to_save = tree.serialize();
	var unique_keys = [];
	for (var i = 0; i < tree_json_to_save.length; i++) {
		if (tree_json_to_save[i].name == '') {
			alert(<?php echo json_encode(epc_rf_t(3884, 'Fill the key for field')); ?> + ': ' + tree_json_to_save[i].value);
			return;
		}
		var current_value = tree_json_to_save[i].name;
		var regex = new RegExp('[a-z_]{1,}');
		var match = regex.exec(String(current_value));
		if (match == null || String(match[0]) != current_value) {
			alert(<?php echo json_encode(epc_rf_t(3885, 'Invalid key for')); ?> + ' ' + tree_json_to_save[i].value);
			return false;
		}
		<?php
		$users_table_columns_query = $db_link->prepare("SELECT `COLUMN_NAME` FROM `INFORMATION_SCHEMA`.`COLUMNS` WHERE TABLE_NAME = 'users' AND `TABLE_SCHEMA` = '" . $DP_Config->db . "';");
		$users_table_columns_query->execute();
		while ($col_record = $users_table_columns_query->fetch()) {
			?>
		if (String(tree_json_to_save[i].name) == <?php echo json_encode($col_record['COLUMN_NAME']); ?>) {
			alert(<?php echo json_encode(epc_rf_t(3889, 'Reserved key')); ?> + ' (' + <?php echo json_encode($col_record['COLUMN_NAME']); ?> + ')');
			return false;
		}
			<?php
		}
		?>
		if (tree_json_to_save[i].value == '') {
			alert(<?php echo json_encode(epc_rf_t(3892, 'Caption is required')); ?>);
			return false;
		}
		for (var r = 0; r < tree_json_to_save[i].required_for.length; r++) {
			if (tree_json_to_save[i].show_for.indexOf(tree_json_to_save[i].required_for[r]) < 0) {
				alert(<?php echo json_encode(epc_rf_t(3893, 'Field')); ?> + ' "' + tree_json_to_save[i].value + '" ' + <?php echo json_encode(epc_rf_t(3894, 'is required but not shown for variant')); ?>);
				return false;
			}
		}
		if (unique_keys.indexOf(tree_json_to_save[i].name) < 0) {
			unique_keys.push(tree_json_to_save[i].name);
		} else {
			alert(<?php echo json_encode(epc_rf_t(3898, 'Duplicate key')); ?> + ': ' + tree_json_to_save[i].name);
			return;
		}
		if (!(!isNaN(parseFloat(tree_json_to_save[i].maxlen)) && isFinite(tree_json_to_save[i].maxlen)) || parseInt(tree_json_to_save[i].maxlen, 10) != tree_json_to_save[i].maxlen || tree_json_to_save[i].maxlen < 0) {
			alert(<?php echo json_encode(epc_rf_t(3900, 'Max length must be a non-negative integer')); ?>);
			return;
		}
		if (tree_json_to_save[i].is_new == false && tree_json_to_save[i].name != tree_json_to_save[i].old_name) {
			if (!confirm(<?php echo json_encode(epc_rf_t(3901, 'Key changed for')); ?> + ' ' + tree_json_to_save[i].value + '. ' + <?php echo json_encode(epc_rf_t(3903, 'Continue?')); ?>)) {
				return;
			}
		}
	}
	document.getElementById('tree_json').value = JSON.stringify(tree_json_to_save);
	document.forms['form_to_save'].submit();
}

function tree_start_init() {
	tree.parse(<?php echo $reg_fields_json; ?>);
	tree.openAll();
}
tree_start_init();
onSelected();
</script>

<?php
if (function_exists('epc_cp_page_frame_close')) {
	epc_cp_page_frame_close();
}
?>
