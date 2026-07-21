<?php
/**
 * Warehouse groups for async price-search polling.
 * Route: /cp/shop/logistics/storages/groups
 */
defined('_ASTEXE_') or die('No access');

require_once $_SERVER['DOCUMENT_ROOT'] . '/content/users/dp_user.php';
$user_session = DP_User::getAdminSession();
$csrf = is_array($user_session) ? (string) ($user_session['csrf_guard_key'] ?? '') : '';
$backend = htmlspecialchars((string) ($DP_Config->backend_dir ?? 'cp'), ENT_QUOTES, 'UTF-8');

function epc_sg_page_h($v): string
{
	return htmlspecialchars((string) $v, ENT_QUOTES, 'UTF-8');
}

// Auto-handled warehouses (shown for clarity; not assignable to custom groups)
$autoStorages = array();
$st = $db_link->prepare('SELECT `id`, `name`, `interface_type` FROM `shop_storages` WHERE `interface_type` IN (1, 2, 6) ORDER BY `id`');
$st->execute();
while ($row = $st->fetch(PDO::FETCH_ASSOC)) {
	$autoStorages[] = $row;
}

// Eligible for custom groups (initial server-side list; AJAX refreshes)
$assigned = array();
$gq = $db_link->query('SELECT `storages` FROM `shop_storages_groups`');
while ($grow = $gq->fetch(PDO::FETCH_ASSOC)) {
	foreach (preg_split('/\s*,\s*/', trim((string) ($grow['storages'] ?? '')), -1, PREG_SPLIT_NO_EMPTY) as $part) {
		$id = (int) $part;
		if ($id > 0) {
			$assigned[$id] = true;
		}
	}
}
$eligible = array();
$eq = $db_link->query('SELECT `id`, `name`, `interface_type` FROM `shop_storages` WHERE `interface_type` NOT IN (1, 2, 6) ORDER BY `name`');
while ($row = $eq->fetch(PDO::FETCH_ASSOC)) {
	if (isset($assigned[(int) $row['id']])) {
		continue;
	}
	$eligible[] = $row;
}

$groupCount = (int) $db_link->query('SELECT COUNT(*) FROM `shop_storages_groups`')->fetchColumn();
?>

<style>
.epc-sg .epc-sg-note{margin:0 0 14px;padding:12px 14px;border-radius:10px;background:#eff6ff;border:1px solid #bfdbfe;color:#1e3a8a;font-size:13px;line-height:1.45}
.epc-sg .epc-sg-loading{padding:28px;text-align:center;color:#64748b}
.epc-sg .epc-sg-loading img{height:28px;margin-bottom:8px}
.epc-sg #container_A_storages{border:1px solid #e2e8f0;border-radius:8px;padding:8px;background:#fff}
.epc-sg .epc-sg-empty-tree{padding:18px;color:#78716c;font-size:13px}
</style>

<div class="col-lg-12 epc-sg">
	<div class="hpanel">
		<div class="panel-heading hbuilt"><?php echo translate_str_by_id(2113); ?></div>
		<div class="panel-body">
			<a class="panel_a" href="/<?php echo $backend; ?>/shop/logistics/storages">
				<div class="panel_a_img" style="background: url('/<?php echo $backend; ?>/templates/<?php echo epc_sg_page_h($DP_Template->name); ?>/images/storage.png') 0 0 no-repeat;"></div>
				<div class="panel_a_caption"><?php echo translate_str_by_id(763); ?></div>
			</a>
			<a class="panel_a" href="/<?php echo $backend; ?>">
				<div class="panel_a_img" style="background: url('/<?php echo $backend; ?>/templates/<?php echo epc_sg_page_h($DP_Template->name); ?>/images/power_off.png') 0 0 no-repeat;"></div>
				<div class="panel_a_caption"><?php echo translate_str_by_id(2116); ?></div>
			</a>
		</div>
	</div>

	<div class="epc-sg-note">
		<strong>Warehouse groups</strong> control how async API warehouses are polled in batches during part search.
		Own warehouse / Docpart price lists (types 1, 2, 6) are automatic — they appear below for reference and cannot be added to a custom group.
		<?php if ($groupCount === 0 && count($eligible) === 0) { ?>
		<br><br>Right now every warehouse is already auto-handled, so there is nothing to group. Add an API-connector warehouse if you need custom batches.
		<?php } ?>
	</div>
</div>

<div class="col-lg-12 epc-sg">
	<div class="hpanel">
		<div class="panel-heading hbuilt"><?php echo translate_str_by_id(3351); ?> <span class="text-muted">(auto)</span></div>
		<div class="panel-body">
			<?php if (!$autoStorages) { ?>
			<p class="text-muted" style="margin:0">No auto-handled warehouses found.</p>
			<?php } else { ?>
			<table class="table" style="margin:0">
				<thead><tr><th style="width:70px">ID</th><th><?php echo translate_str_by_id(2102); ?></th><th style="width:100px">Type</th></tr></thead>
				<tbody>
				<?php foreach ($autoStorages as $storage) { ?>
					<tr>
						<td><?php echo (int) $storage['id']; ?></td>
						<td><?php echo epc_sg_page_h($storage['name']); ?></td>
						<td><span class="label label-default"><?php echo (int) $storage['interface_type']; ?></span></td>
					</tr>
				<?php } ?>
				</tbody>
			</table>
			<?php } ?>
		</div>
	</div>
</div>

<div id="div_table" class="epc-sg">
	<div class="epc-sg-loading">
		<img src="/content/files/images/ajax-loader-transparent.gif" alt="">
		<div>Loading warehouse groups…</div>
	</div>
</div>

<div class="col-lg-6 epc-sg">
	<div class="hpanel">
		<div class="panel-heading hbuilt"><?php echo translate_str_by_id(2073); ?></div>
		<div class="panel-body" style="min-height: 200px;">
			<?php echo translate_str_by_id(3352); ?>
			<hr>
			<p class="text-muted" style="margin:0;font-size:13px">Tip: groups only include API / connector warehouses. Price-list warehouses stay in the auto list on the left.</p>
		</div>
	</div>
</div>

<div class="col-lg-6 epc-sg">
	<div class="hpanel">
		<div class="panel-heading hbuilt"><?php echo translate_str_by_id(3353); ?></div>
		<div class="panel-body">
			<div class="col-lg-12"><label><?php echo translate_str_by_id(2102); ?>:</label><input style="border-color: #a4bed4 !important;" class="form-control" type="text" id="new_name" placeholder="e.g. UAE API batch"/></div>
			<div class="col-lg-12">
				<div style="margin-top:15px;"><label><?php echo translate_str_by_id(763); ?> available for grouping:</label></div>
				<div id="container_A_storages" style="height:250px;">
					<?php if (!$eligible) { ?>
					<div class="epc-sg-empty-tree" id="epc_sg_empty_tree">No API warehouses available to group. All current warehouses are auto-handled (types 1 / 2 / 6).</div>
					<?php } ?>
				</div>
			</div>
		</div>
		<div class="panel-footer text-right">
			<img id="img_add" style="height: 31px; margin-right: 5px;" class="hidden" src="/content/files/images/ajax-loader-transparent.gif" alt=""/><a id="btn_add" onclick="add();" class="btn btn-ar btn-primary"><i class="fa fa-plus"></i> <?php echo translate_str_by_id(2267); ?></a>
		</div>
	</div>
</div>

<script>
(function () {
	'use strict';
	var ajax_url = '/<?php echo $backend; ?>/content/shop/logistics/groups/ajax_operations.php';
	var csrf = <?php echo json_encode($csrf, JSON_UNESCAPED_UNICODE); ?>;
	var storages_list = [];
	var storages_tree = null;

	<?php foreach ($eligible as $storage) { ?>
	storages_list.push({
		id: <?php echo (int) $storage['id']; ?>,
		checked: false,
		name: <?php echo json_encode((string) $storage['name'], JSON_UNESCAPED_UNICODE); ?>,
		selected: false
	});
	<?php } ?>

	function ensureTree() {
		if (typeof webix === 'undefined') {
			return false;
		}
		if (storages_tree) {
			return true;
		}
		var empty = document.getElementById('epc_sg_empty_tree');
		if (empty) {
			empty.parentNode.removeChild(empty);
		}
		storages_tree = new webix.ui({
			template: function (obj, common) {
				return common.icon(obj, common) + common.checkbox(obj, common) + common.folder(obj, common) + '<span>' + obj.value + '</span>';
			},
			editable: false,
			container: 'container_A_storages',
			view: 'tree',
			select: false,
			drag: false
		});
		webix.event(window, 'resize', function () { if (storages_tree) { storages_tree.adjust(); } });
		return true;
	}

	function storages_tree_start_init() {
		var box = document.getElementById('container_A_storages');
		if (!storages_list.length) {
			if (storages_tree) {
				try { storages_tree.destructor(); } catch (e) {}
				storages_tree = null;
			}
			if (box) {
				box.innerHTML = '<div class="epc-sg-empty-tree">No API warehouses available to group right now.</div>';
			}
			return;
		}
		if (!ensureTree()) {
			if (box) {
				box.innerHTML = '<div class="epc-sg-empty-tree">Warehouse picker failed to load (Webix). Hard-refresh and try again.</div>';
			}
			return;
		}
		storages_tree.clearAll();
		for (var i = 0; i < storages_list.length; i++) {
			storages_tree.add({ id: storages_list[i].id, value: storages_list[i].name }, storages_tree.count(), 0);
		}
	}

	function setTableHtml(html) {
		var el = document.getElementById('div_table');
		if (el) {
			el.innerHTML = html;
		}
	}

	function show_table() {
		setTableHtml('<div class="epc-sg-loading"><img src="/content/files/images/ajax-loader-transparent.gif" alt=""><div>Loading warehouse groups…</div></div>');
		get_storages();

		var request_object = { action: 'get_table' };
		jQuery.ajax({
			type: 'POST',
			async: true,
			url: ajax_url,
			dataType: 'text',
			data: 'request_object=' + encodeURIComponent(JSON.stringify(request_object)) + '&csrf_guard_key=' + encodeURIComponent(csrf),
			success: function (answer) {
				setTableHtml(answer && String(answer).trim() !== '' ? answer : '<div class="col-lg-12"><div class="alert alert-warning">No group data returned.</div></div>');
			},
			error: function (xhr) {
				setTableHtml('<div class="col-lg-12"><div class="alert alert-danger"><strong>Could not load groups.</strong> HTTP ' + (xhr && xhr.status ? xhr.status : '?') + '. Check that ajax_operations.php is deployed.</div></div>');
			}
		});
	}

	function get_storages() {
		if (document.getElementById('btn_add') && document.getElementById('btn_add').classList.contains('disabled')) {
			return;
		}
		var request_object = { action: 'get_storages' };
		jQuery.ajax({
			type: 'POST',
			async: true,
			url: ajax_url,
			dataType: 'json',
			data: 'request_object=' + encodeURIComponent(JSON.stringify(request_object)) + '&csrf_guard_key=' + encodeURIComponent(csrf),
			success: function (answer) {
				storages_list = (answer && answer.storages_list && answer.storages_list.length) ? answer.storages_list : [];
				storages_tree_start_init();
			},
			error: function () {
				storages_tree_start_init();
			}
		});
	}

	window.add = function add() {
		if (document.getElementById('btn_add').classList.contains('disabled')) {
			return;
		}
		var name = document.getElementById('new_name').value;
		if (name === '') {
			alert(<?php echo json_encode(translate_str_by_id(3354), JSON_UNESCAPED_UNICODE); ?>);
			return;
		}
		if (!storages_tree) {
			alert(<?php echo json_encode(translate_str_by_id(3355), JSON_UNESCAPED_UNICODE); ?>);
			return;
		}
		var ckecked_storages = storages_tree.getChecked();
		if (!ckecked_storages.length) {
			alert(<?php echo json_encode(translate_str_by_id(3355), JSON_UNESCAPED_UNICODE); ?>);
			return;
		}
		var request_object = {
			action: 'add_group',
			name: encodeURIComponent(name),
			storages: ckecked_storages
		};
		document.getElementById('new_name').value = '';
		jQuery('#btn_add').addClass('disabled');
		jQuery('#img_add').removeClass('hidden');
		jQuery.ajax({
			type: 'POST',
			async: true,
			url: ajax_url,
			dataType: 'json',
			data: 'request_object=' + encodeURIComponent(JSON.stringify(request_object)) + '&csrf_guard_key=' + encodeURIComponent(csrf),
			success: function (answer) {
				jQuery('#btn_add').removeClass('disabled');
				jQuery('#img_add').addClass('hidden');
				if (answer && answer.status === true) {
					show_table();
				} else {
					alert(<?php echo json_encode(translate_str_by_id(3356), JSON_UNESCAPED_UNICODE); ?>);
				}
			},
			error: function () {
				jQuery('#btn_add').removeClass('disabled');
				jQuery('#img_add').addClass('hidden');
				alert(<?php echo json_encode(translate_str_by_id(3356), JSON_UNESCAPED_UNICODE); ?>);
			}
		});
	};

	window.del = function del(id) {
		if (!confirm(<?php echo json_encode(translate_str_by_id(3357), JSON_UNESCAPED_UNICODE); ?>)) {
			return;
		}
		var request_object = { action: 'del', id: id };
		jQuery.ajax({
			type: 'POST',
			async: true,
			url: ajax_url,
			dataType: 'json',
			data: 'request_object=' + encodeURIComponent(JSON.stringify(request_object)) + '&csrf_guard_key=' + encodeURIComponent(csrf),
			success: function (answer) {
				if (answer && answer.status === true) {
					show_table();
				} else {
					alert(<?php echo json_encode(translate_str_by_id(2610), JSON_UNESCAPED_UNICODE); ?>);
				}
			},
			error: function () {
				alert(<?php echo json_encode(translate_str_by_id(2610), JSON_UNESCAPED_UNICODE); ?>);
			}
		});
	};

	if (document.readyState === 'loading') {
		document.addEventListener('DOMContentLoaded', function () {
			storages_tree_start_init();
			show_table();
		});
	} else {
		storages_tree_start_init();
		show_table();
	}
})();
</script>
