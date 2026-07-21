<?php
/**
 * CP: publish downloadable price lists for storefront customers (by markup group).
 * Customers see / download prices_{group_id}.csv in the search “Price list” tab.
 */
defined('_ASTEXE_') or die('No access');

require_once $_SERVER['DOCUMENT_ROOT'] . '/content/users/dp_user.php';
$user_session = DP_User::getAdminSession();
$admin_id = DP_User::getAdminId();

$epc_pdl_linked = array();
$epc_pdl_existing = array();
$prices_tmp = $_SERVER['DOCUMENT_ROOT'] . '/content/files/Documents/prices_tmp';
try {
	$office_id_probe = 1;
	$oq = $db_link->query('SELECT `id` FROM `shop_offices` ORDER BY `id` ASC LIMIT 1');
	if ($oq) {
		$office_id_probe = (int) $oq->fetchColumn();
	}
	$lq = $db_link->prepare('SELECT DISTINCT `storage_id` FROM `shop_offices_storages_map` WHERE `office_id` = ?');
	$lq->execute(array($office_id_probe));
	while ($lr = $lq->fetch(PDO::FETCH_ASSOC)) {
		$epc_pdl_linked[] = (int) $lr['storage_id'];
	}
} catch (Throwable $e) {
}

if (is_dir($prices_tmp)) {
	foreach (glob($prices_tmp . '/prices_*.csv') ?: array() as $f) {
		if (preg_match('/prices_(\d+)\.csv$/', basename($f), $m)) {
			$epc_pdl_existing[(int) $m[1]] = array(
				'bytes' => filesize($f),
				'mtime' => filemtime($f),
				'url' => '/content/files/Documents/prices_tmp/' . basename($f),
			);
		}
	}
}

$csrf = isset($user_session['csrf_guard_key']) ? (string) $user_session['csrf_guard_key'] : '';
$ajaxUrl = '/' . $DP_Config->backend_dir . '/content/shop/prices_send/ajax_operations.php';
$domain = rtrim((string) $DP_Config->domain_path, '/') . '/';
?>
<style>
.epc-pdl-page{--pdl-ink:#0f172a;--pdl-muted:#64748b;--pdl-line:#e2e8f0;--pdl-accent:#1d4ed8;--pdl-ok:#047857;margin:0 -5px 24px}
.epc-pdl-page .epc-pdl-hero{background:linear-gradient(135deg,#0f172a 0%,#1e3a8a 55%,#1d4ed8 100%);color:#eff6ff;border-radius:12px;padding:18px 20px;margin:0 5px 14px}
.epc-pdl-page .epc-pdl-hero h2{margin:0 0 4px;font-size:22px;font-weight:700;letter-spacing:-.02em}
.epc-pdl-page .epc-pdl-hero p{margin:0;opacity:.88;font-size:13px;max-width:720px}
.epc-pdl-page .epc-pdl-steps{display:grid;grid-template-columns:repeat(4,minmax(0,1fr));gap:10px;margin:0 5px 14px}
@media(max-width:1100px){.epc-pdl-page .epc-pdl-steps{grid-template-columns:1fr 1fr}}
@media(max-width:700px){.epc-pdl-page .epc-pdl-steps{grid-template-columns:1fr}}
.epc-pdl-page .epc-pdl-step{background:#fff;border:1px solid var(--pdl-line);border-radius:10px;padding:12px 14px}
.epc-pdl-page .epc-pdl-step b{display:block;font-size:12px;text-transform:uppercase;letter-spacing:.05em;color:var(--pdl-accent);margin-bottom:4px}
.epc-pdl-page .epc-pdl-step span{font-size:13px;color:var(--pdl-ink);line-height:1.4}
.epc-pdl-page .epc-pdl-guide{background:#eff6ff;border:1px solid #bfdbfe;color:#1e3a8a;border-radius:8px;padding:12px 14px;margin:0 5px 14px;font-size:13px;line-height:1.55}
.epc-pdl-page .epc-pdl-guide code{background:rgba(255,255,255,.75);padding:1px 5px;border-radius:4px}
.epc-pdl-page .hpanel{border-radius:10px;overflow:hidden;border:1px solid var(--pdl-line);box-shadow:0 1px 2px rgba(15,23,42,.04)}
.epc-pdl-page .hpanel .panel-heading.hbuilt{background:linear-gradient(180deg,#fff 0%,#f8fafc 100%);border-bottom:1px solid var(--pdl-line);font-weight:600;color:var(--pdl-ink)}
.epc-pdl-page .epc-pdl-label{display:inline-block;font-size:11px;text-transform:uppercase;letter-spacing:.05em;color:var(--pdl-muted);margin-right:8px}
.epc-pdl-page .btn-primary{background:var(--pdl-accent);border-color:var(--pdl-accent)}
.epc-pdl-page .btn-success{background:var(--pdl-ok);border-color:var(--pdl-ok)}
.epc-pdl-page #create_prices_status{margin-top:10px;font-size:13px}
.epc-pdl-page .epc-pdl-file-list a{display:inline-block;margin:4px 8px 4px 0}
.epc-pdl-page .table-responsive{border:1px solid var(--pdl-line);border-radius:8px}
.epc-pdl-page .epc-pdl-status-table td{vertical-align:middle!important}
</style>
<div class="epc-pdl-page">
	<div class="epc-pdl-hero">
		<h2>Downloadable price lists</h2>
		<p>Publish a CSV for each customer markup group. Logged-in buyers download the file for <em>their</em> group from the storefront search tab. Same generator as Prices Send — here the goal is publish-for-download, not email.</p>
	</div>
	<div class="epc-pdl-steps">
		<div class="epc-pdl-step"><b>1 · Group</b><span>Choose the markup profile (Retail, Wholesale, CIS…). Output file: <code>prices_{id}.csv</code>.</span></div>
		<div class="epc-pdl-step"><b>2 · Sources</b><span>Shop + Docpart / catalogue storages (must be linked for markup).</span></div>
		<div class="epc-pdl-step"><b>3 · Scope</b><span>Optional brand / article filters, or catalogue categories for warehouse items.</span></div>
		<div class="epc-pdl-step"><b>4 · Publish</b><span>Generate CSV → buyers in that group can download it immediately.</span></div>
	</div>
	<div class="epc-pdl-guide">
		<strong>How sharing works</strong><br>
		• CP publishes <code>/content/files/Documents/prices_tmp/prices_{group}.csv</code><br>
		• Storefront “Price list” tab shows the file for the buyer’s first user group<br>
		• Rebuild after price updates so customers get fresh markups<br>
		• Use <em>Link selected storages</em> if generation says warehouses are not connected to the shop
	</div>

	<div class="row" style="margin:0;">
		<div class="col-lg-5">
			<div class="hpanel">
				<div class="panel-heading hbuilt"><span class="epc-pdl-label">Published now</span> Files customers can download</div>
				<div class="panel-body" style="max-height:420px;overflow:auto;">
					<?php if (empty($epc_pdl_existing)) { ?>
						<p class="text-muted" style="margin:0;">No published CSVs yet. Generate one below for a markup group.</p>
					<?php } else {
						$gNames = array();
						$gq = $db_link->query('SELECT `id`, `value` FROM `groups`');
						while ($gr = $gq->fetch(PDO::FETCH_ASSOC)) {
							$gNames[(int) $gr['id']] = function_exists('translate_str_by_id') ? translate_str_by_id($gr['value']) : (string) $gr['value'];
						}
						?>
						<table class="table table-striped table-condensed epc-pdl-status-table">
							<thead><tr><th>Group</th><th>Updated</th><th>Size</th><th></th></tr></thead>
							<tbody>
							<?php foreach ($epc_pdl_existing as $gid => $meta) {
								$name = isset($gNames[$gid]) ? $gNames[$gid] : ('Group ' . $gid);
								?>
								<tr>
									<td><strong><?php echo htmlspecialchars($name, ENT_QUOTES, 'UTF-8'); ?></strong><br><code>prices_<?php echo (int) $gid; ?>.csv</code></td>
									<td><?php echo date('Y-m-d H:i', (int) $meta['mtime']); ?></td>
									<td><?php echo number_format((int) $meta['bytes']); ?> B</td>
									<td><a class="btn btn-xs btn-default" href="<?php echo htmlspecialchars($meta['url'], ENT_QUOTES, 'UTF-8'); ?>" download><i class="fa fa-download"></i></a></td>
								</tr>
							<?php } ?>
							</tbody>
						</table>
					<?php } ?>
				</div>
			</div>
		</div>
		<div class="col-lg-7">
			<div class="hpanel">
				<div class="panel-heading hbuilt"><span class="epc-pdl-label">Step 1–2</span> Group, shop &amp; storages</div>
				<div class="panel-body">
					<div class="row">
						<div class="col-lg-6">
							<label>Customer markup group</label>
							<select class="form-control" id="group_select">
							<?php
							$groups_query = $db_link->prepare('SELECT * FROM `groups` ORDER BY `id`;');
							$groups_query->execute();
							while ($group = $groups_query->fetch()) {
								$gid = (int) $group['id'];
								$pub = isset($epc_pdl_existing[$gid]) ? ' · published' : '';
								?>
								<option value="<?php echo $gid; ?>"><?php echo htmlspecialchars(translate_str_by_id($group['value']) . ' (ID ' . $gid . ')' . $pub, ENT_QUOTES, 'UTF-8'); ?></option>
								<?php
							}
							?>
							</select>
						</div>
						<div class="col-lg-6">
							<label>Shop / office</label>
							<select id="office_select" name="office_select" class="form-control">
							<?php
							$query = $db_link->prepare('SELECT * FROM `shop_offices`');
							$query->execute();
							while ($array = $query->fetch()) {
								?>
								<option value="<?php echo (int) $array['id']; ?>"><?php echo htmlspecialchars(translate_str_by_id($array['caption']) . ' (ID ' . $array['id'] . ')', ENT_QUOTES, 'UTF-8'); ?></option>
								<?php
							}
							?>
							</select>
						</div>
					</div>
					<hr>
					<label>Price sources (Docpart lists + catalogue warehouses)</label>
					<div class="table-responsive" style="max-height:280px;overflow:auto;margin-top:8px;">
						<table class="table table-condensed table-striped">
							<thead>
								<tr>
									<th><input checked type="checkbox" id="check_uncheck_all" onchange="on_check_uncheck_all();"/></th>
									<th>ID</th>
									<th><?php echo translate_str_by_id(2277); ?></th>
									<th><?php echo translate_str_by_id(3474); ?></th>
									<th>Linked</th>
								</tr>
							</thead>
							<tbody>
							<?php
							$for_js = "var elements_array = new Array();\n";
							$for_js .= "var elements_id_array = new Array();\n";
							$elements_query = $db_link->prepare("SELECT *, (SELECT `name` FROM `shop_storages_interfaces_types` WHERE `id` = `shop_storages`.`interface_type`) AS `interface_type_name` FROM `shop_storages` WHERE `interface_type` = 1 OR `interface_type` = 2;");
							$elements_query->execute();
							while ($element_record = $elements_query->fetch()) {
								$sid = (int) $element_record['id'];
								$for_js .= 'elements_array[elements_array.length] = "checked_' . $sid . "\";\n";
								$for_js .= 'elements_id_array[elements_id_array.length] = ' . $sid . ";\n";
								$is_linked = in_array($sid, $epc_pdl_linked, true);
								?>
								<tr>
									<td><input checked type="checkbox" onchange="on_one_check_changed('checked_<?php echo $sid; ?>');" id="checked_<?php echo $sid; ?>" name="checked_<?php echo $sid; ?>"/></td>
									<td><?php echo $sid; ?></td>
									<td><?php echo htmlspecialchars((string) $element_record['name'], ENT_QUOTES, 'UTF-8'); ?></td>
									<td><?php echo htmlspecialchars((string) $element_record['interface_type_name'], ENT_QUOTES, 'UTF-8'); ?></td>
									<td><?php echo $is_linked ? '<span class="label label-success">Yes</span>' : '<span class="label label-warning">No</span>'; ?></td>
								</tr>
								<?php
							}
							?>
							</tbody>
						</table>
					</div>
				</div>
			</div>
		</div>
	</div>

<?php
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/catalogue/dp_category_record.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/catalogue/get_catalogue_tree.php';
?>

	<div class="row" style="margin:0;">
		<div class="col-lg-5">
			<div class="hpanel">
				<div class="panel-heading hbuilt"><span class="epc-pdl-label">Step 3</span> Optional filters</div>
				<div class="panel-body">
					<div class="form-group">
						<label>Brand (optional)</label>
						<input type="text" id="epc_pdl_filter_brand" class="form-control" placeholder="e.g. TOYOTA" list="epc_pdl_brand_list"/>
						<datalist id="epc_pdl_brand_list"></datalist>
					</div>
					<div class="form-group">
						<label>Article / item (optional)</label>
						<input type="text" id="epc_pdl_filter_article" class="form-control" placeholder="e.g. 8114560Q51"/>
					</div>
					<p class="text-muted" style="font-size:12px;margin:0;">Leave empty to publish the full selected sources for this group’s markup.</p>
				</div>
			</div>
		</div>
		<div class="col-lg-7">
			<div class="hpanel">
				<div class="panel-heading hbuilt"><span class="epc-pdl-label">Step 3</span> Catalogue categories (warehouse / type&nbsp;1)</div>
				<div class="panel-body">
					<div style="padding:0 0 10px 0;">
						<button type="button" onclick="catalogue_tree.checkAll();" class="btn btn-success btn-sm"><?php echo translate_str_by_id(2293); ?></button>
						<button type="button" onclick="catalogue_tree.uncheckAll();" class="btn btn-default btn-sm"><?php echo translate_str_by_id(2294); ?></button>
					</div>
					<div id="container_A" style="height:280px;"></div>
					<div class="hidden">
						<select id="storages" name="storages" class="form-control">
						<?php
						$storages_query = $db_link->prepare('SELECT * FROM `shop_storages`');
						$storages_query->execute();
						while ($storages = $storages_query->fetch()) {
							if ((int) $storages['interface_type'] === 1) {
								$arr_users = json_decode((string) $storages['users'], true);
								if (!is_array($arr_users)) {
									$arr_users = array();
								}
								foreach ($arr_users as $id_user) {
									if ((int) $id_user === (int) $admin_id) {
										echo '<option value="' . (int) $storages['id'] . '">' . htmlspecialchars($storages['name'] . ' (ID ' . $storages['id'] . ')', ENT_QUOTES, 'UTF-8') . '</option>';
									}
								}
							}
						}
						?>
						</select>
					</div>
				</div>
			</div>
		</div>
	</div>

	<div class="col-lg-12" style="padding:0 5px;">
		<div class="hpanel">
			<div class="panel-heading hbuilt"><span class="epc-pdl-label">Step 4</span> Publish for storefront download</div>
			<div class="panel-body">
				<button type="button" onclick="create_prices();" class="btn btn-success"><i class="fa fa-cogs"></i> Generate &amp; publish</button>
				<button type="button" onclick="epcPdlLinkStorages();" class="btn btn-warning"><i class="fa fa-link"></i> Link selected storages</button>
				<button type="button" onclick="epcPdlCheckMap();" class="btn btn-default"><i class="fa fa-check"></i> Check links</button>
				<div id="create_prices_status"></div>
				<p class="text-muted" style="margin:12px 0 0;font-size:12px;">Public path pattern: <code><?php echo htmlspecialchars($domain, ENT_QUOTES, 'UTF-8'); ?>content/files/Documents/prices_tmp/prices_{group}.csv</code></p>
			</div>
		</div>
	</div>
</div>

<script>
var epcPdlAjax = <?php echo json_encode($ajaxUrl); ?>;
var epcPdlCsrf = <?php echo json_encode($csrf); ?>;

function epcPdlPost(request_object, cb) {
	jQuery.ajax({
		type: 'POST',
		url: epcPdlAjax,
		dataType: 'json',
		data: 'request_object=' + encodeURIComponent(JSON.stringify(request_object)) + '&csrf_guard_key=' + encodeURIComponent(epcPdlCsrf),
		success: cb,
		error: function (xhr) {
			cb({ status: false, message: 'HTTP ' + (xhr && xhr.status ? xhr.status : '?') });
		}
	});
}

function create_prices() {
	var request_object = {};
	request_object.group_id_my_list_emails = document.getElementById('group_select').value;
	request_object.profile_group_ids = [parseInt(request_object.group_id_my_list_emails, 10) || 0];
	request_object.offices = document.getElementById('office_select').value;
	request_object.arr_storages = getCheckedElements();
	if (request_object.arr_storages.length === 0) {
		alert('<?php echo translate_str_by_id(3727); ?>');
		return;
	}
	request_object.arr_category = catalogue_tree.getChecked();
	request_object.filter_brand = (document.getElementById('epc_pdl_filter_brand').value || '').trim();
	request_object.filter_article = (document.getElementById('epc_pdl_filter_article').value || '').trim();
	request_object.users_list = [];
	request_object.emails_list = '';
	request_object.action = 'check_office_storages_map';

	document.getElementById('create_prices_status').innerHTML = '<div class="text-muted"><i class="fa fa-spinner fa-spin"></i> Checking warehouse links…</div>';

	epcPdlPost(request_object, function (check) {
		if (!check || !check.status) {
			var msg = (check && check.message) ? check.message : 'Storages not linked';
			document.getElementById('create_prices_status').innerHTML = '<div class="alert alert-danger">Not linked: ' + msg + '. Click “Link selected storages”, then Generate again.</div>';
			return;
		}
		request_object.action = 'create_prices';
		document.getElementById('create_prices_status').innerHTML = '<div class="text-muted"><i class="fa fa-spinner fa-spin"></i> Publishing price list…</div>';
		epcPdlPost(request_object, function (answer) {
			if (answer && answer.status) {
				var gid = request_object.group_id_my_list_emails;
				var html = '<div class="alert alert-success">' + (answer.message || 'Published.') + '</div>';
				html += '<div class="epc-pdl-file-list">';
				if (answer.files && answer.files.length) {
					for (var i = 0; i < answer.files.length; i++) {
						var f = answer.files[i];
						html += '<a class="btn btn-default btn-sm" href="' + f.url + '" download><i class="fa fa-download"></i> ' + f.file + ' (' + f.rows + ' rows)</a>';
					}
				} else {
					html += '<a class="btn btn-default btn-sm" href="/content/files/Documents/prices_tmp/prices_' + gid + '.csv" download><i class="fa fa-download"></i> prices_' + gid + '.csv</a>';
				}
				html += '</div><p class="text-muted" style="margin-top:8px;">Customers in this group can download it from the storefront Price list tab.</p>';
				document.getElementById('create_prices_status').innerHTML = html;
			} else {
				alert((answer && answer.message) ? answer.message : '<?php echo translate_str_by_id(3682); ?>');
				document.getElementById('create_prices_status').innerHTML = '';
			}
		});
	});
}

function epcPdlLinkStorages() {
	var gid = parseInt(document.getElementById('group_select').value, 10) || 0;
	var request_object = {
		action: 'ensure_office_storage_links',
		offices: document.getElementById('office_select').value,
		arr_storages: getCheckedElements(),
		group_ids: gid > 0 ? [gid, 2, 4, 5, 6, 7] : [2, 4, 5, 6, 7]
	};
	if (!request_object.arr_storages.length) {
		alert('<?php echo translate_str_by_id(3727); ?>');
		return;
	}
	document.getElementById('create_prices_status').innerHTML = '<div class="text-muted"><i class="fa fa-spinner fa-spin"></i> Linking…</div>';
	epcPdlPost(request_object, function (answer) {
		document.getElementById('create_prices_status').innerHTML = '<div class="alert alert-' + (answer && answer.status ? 'success' : 'danger') + '">' + ((answer && answer.message) ? answer.message : 'Failed') + '</div>';
	});
}

function epcPdlCheckMap() {
	var request_object = {
		action: 'check_office_storages_map',
		offices: document.getElementById('office_select').value,
		arr_storages: getCheckedElements()
	};
	epcPdlPost(request_object, function (answer) {
		if (answer && answer.status) {
			document.getElementById('create_prices_status').innerHTML = '<div class="alert alert-success">All selected storages are linked to this shop.</div>';
		} else {
			document.getElementById('create_prices_status').innerHTML = '<div class="alert alert-warning">Unlinked: ' + ((answer && answer.message) ? answer.message : '?') + '</div>';
		}
	});
}

<?php echo $for_js; ?>
function on_check_uncheck_all() {
	var state = document.getElementById('check_uncheck_all').checked;
	for (var i = 0; i < elements_array.length; i++) {
		document.getElementById(elements_array[i]).checked = state;
	}
}
function on_one_check_changed(id) {
	for (var i = 0; i < elements_array.length; i++) {
		if (document.getElementById(elements_array[i]).checked === false) {
			document.getElementById('check_uncheck_all').checked = false;
			break;
		}
	}
}
function getCheckedElements() {
	var checked_ids = [];
	for (var i = 0; i < elements_array.length; i++) {
		if (document.getElementById(elements_array[i]).checked === true) {
			checked_ids.push(elements_id_array[i]);
		}
	}
	return checked_ids;
}

webix.protoUI({ name: 'edittree' }, webix.EditAbility, webix.ui.tree);
catalogue_tree = new webix.ui({
	editable: false,
	container: 'container_A',
	view: 'tree',
	select: false,
	drag: false,
	template: function (obj, common) {
		return common.icon(obj, common) + common.checkbox(obj, common) + common.folder(obj, common) + '<span>' + obj.value + '</span>';
	}
});
webix.event(window, 'resize', function () { catalogue_tree.adjust(); });
var saved_catalogue = <?php echo $catalogue_tree_dump_JSON; ?>;
catalogue_tree.parse(saved_catalogue);
catalogue_tree.openAll();

epcPdlPost({ action: 'list_brands', limit: 40 }, function (answer) {
	if (!answer || !answer.status || !answer.brands) return;
	var dl = document.getElementById('epc_pdl_brand_list');
	if (!dl) return;
	dl.innerHTML = '';
	answer.brands.forEach(function (b) {
		var opt = document.createElement('option');
		opt.value = b.brand;
		dl.appendChild(opt);
	});
});
</script>
