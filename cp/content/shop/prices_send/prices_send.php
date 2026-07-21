<?php
/*
Страница настроек для формирования и отправки прайс листа клиентам
*/
defined('_ASTEXE_') or die('No access');

//Для работы с пользователями
require_once($_SERVER["DOCUMENT_ROOT"]."/content/users/dp_user.php");
$admin_id = DP_User::getAdminId();

//Для работы с пользователем
require_once( $_SERVER['DOCUMENT_ROOT']."/content/users/dp_user.php" );
$user_session = DP_User::getAdminSession();

// Linked storages for office warning
$epc_ps_linked_storage_ids = array();
try {
	$office_id_probe = 1;
	$oq = $db_link->query('SELECT `id` FROM `shop_offices` ORDER BY `id` ASC LIMIT 1');
	if ($oq) {
		$office_id_probe = (int)$oq->fetchColumn();
	}
	$lq = $db_link->prepare('SELECT DISTINCT `storage_id` FROM `shop_offices_storages_map` WHERE `office_id` = ?');
	$lq->execute(array($office_id_probe));
	while ($lr = $lq->fetch(PDO::FETCH_ASSOC)) {
		$epc_ps_linked_storage_ids[] = (int)$lr['storage_id'];
	}
} catch (Throwable $e) {
}

$user_id = "";
$group_id = -1;
$email = "";
$cellphone = "";
$surname = "";
$users_filter_send_prices = NULL;
if (isset($_COOKIE["users_filter_send_prices"])) {
	$users_filter_send_prices = $_COOKIE["users_filter_send_prices"];
}
if ($users_filter_send_prices != NULL) {
	$users_filter_send_prices = json_decode($users_filter_send_prices, true);
	$user_id = $users_filter_send_prices["user_id"];
	$group_id = $users_filter_send_prices["group_id"];
	$email = $users_filter_send_prices["email"];
	$cellphone = $users_filter_send_prices["cellphone"];
	$surname = $users_filter_send_prices["surname"];
}

$users_sort_send_prices = NULL;
if (isset($_COOKIE["users_sort_send_prices"])) {
	$users_sort_send_prices = $_COOKIE["users_sort_send_prices"];
}
$sort_field = "user_id";
$sort_asc_desc = "desc";
if ($users_sort_send_prices != NULL) {
	$users_sort_send_prices = json_decode($users_sort_send_prices, true);
	$sort_field = $users_sort_send_prices["field"];
	$sort_asc_desc = $users_sort_send_prices["asc_desc"];
}
if (strtolower($sort_asc_desc) == "asc") {
	$sort_asc_desc = "asc";
} else {
	$sort_asc_desc = "desc";
}
if (array_search($sort_field, array('user_id', 'email', 'fio')) === false) {
	$sort_field = "user_id";
}
?>
<style>
.epc-ps-page{
	--ps-ink:#0f172a;
	--ps-muted:#64748b;
	--ps-line:#e2e8f0;
	--ps-bg:#f1f5f9;
	--ps-card:#ffffff;
	--ps-accent:#0f766e;
	--ps-accent-2:#b45309;
	--ps-ok:#047857;
	--ps-warn:#b45309;
	margin:0 -5px 28px;
}
.epc-ps-page .epc-ps-hero{
	background:linear-gradient(135deg,#0f172a 0%,#134e4a 52%,#0f766e 100%);
	color:#f8fafc;
	border-radius:12px;
	padding:18px 20px;
	margin:0 5px 14px;
	display:flex;
	flex-wrap:wrap;
	gap:14px 20px;
	align-items:center;
	justify-content:space-between;
}
.epc-ps-page .epc-ps-hero h2{
	margin:0 0 4px;
	font-size:22px;
	font-weight:700;
	letter-spacing:-.02em;
}
.epc-ps-page .epc-ps-hero p{
	margin:0;
	opacity:.88;
	font-size:13px;
	max-width:640px;
	line-height:1.45;
}
.epc-ps-page .epc-ps-hero-steps{
	display:flex;
	flex-wrap:wrap;
	gap:8px;
}
.epc-ps-page .epc-ps-hero-steps span{
	display:inline-flex;
	align-items:center;
	gap:6px;
	background:rgba(255,255,255,.12);
	border:1px solid rgba(255,255,255,.18);
	border-radius:999px;
	padding:6px 12px;
	font-size:12px;
	white-space:nowrap;
}
.epc-ps-page .epc-ps-hero-steps b{
	font-weight:700;
	opacity:.95;
}
.epc-ps-page .epc-ps-section{
	margin:0 5px 14px;
}
.epc-ps-page .hpanel{
	border-radius:10px;
	overflow:hidden;
	border:1px solid var(--ps-line);
	box-shadow:0 1px 2px rgba(15,23,42,.04);
	background:var(--ps-card);
	margin-bottom:0;
}
.epc-ps-page .hpanel .panel-heading.hbuilt{
	background:linear-gradient(180deg,#fff 0%,#f8fafc 100%);
	border-bottom:1px solid var(--ps-line);
	font-weight:600;
	color:var(--ps-ink);
	display:flex;
	align-items:center;
	flex-wrap:wrap;
	gap:8px;
}
.epc-ps-page .epc-ps-section-label{
	display:inline-block;
	font-size:11px;
	text-transform:uppercase;
	letter-spacing:.05em;
	color:var(--ps-accent);
	background:rgba(15,118,110,.08);
	border:1px solid rgba(15,118,110,.18);
	border-radius:999px;
	padding:2px 8px;
	font-weight:700;
}
.epc-ps-page .epc-ps-heading-note{
	margin-left:auto;
	font-size:12px;
	font-weight:400;
	color:var(--ps-muted);
}
.epc-ps-page .epc-ps-filter-grid{
	display:grid;
	grid-template-columns:repeat(5,minmax(0,1fr));
	gap:12px;
}
@media(max-width:1100px){
	.epc-ps-page .epc-ps-filter-grid{grid-template-columns:repeat(3,minmax(0,1fr))}
}
@media(max-width:700px){
	.epc-ps-page .epc-ps-filter-grid{grid-template-columns:1fr 1fr}
}
.epc-ps-page .epc-ps-field label{
	display:block;
	font-size:12px;
	font-weight:600;
	color:var(--ps-muted);
	margin:0 0 5px;
}
.epc-ps-page .epc-ps-field .form-control{height:34px}
.epc-ps-page .epc-ps-actions{
	display:flex;
	flex-wrap:wrap;
	gap:8px;
	align-items:center;
	margin-top:14px;
	padding-top:12px;
	border-top:1px solid var(--ps-line);
}
.epc-ps-page .epc-ps-split{
	display:grid;
	grid-template-columns:minmax(0,1.6fr) minmax(280px,.9fr);
	gap:14px;
}
@media(max-width:1100px){
	.epc-ps-page .epc-ps-split{grid-template-columns:1fr}
}
.epc-ps-page .epc-ps-scroll{
	max-height:420px;
	overflow:auto;
	border:1px solid var(--ps-line);
	border-radius:8px;
}
.epc-ps-page .epc-ps-scroll table{
	margin:0;
	font-size:13px;
}
.epc-ps-page .epc-ps-scroll th,
.epc-ps-page .epc-ps-scroll td{
	vertical-align:middle !important;
}
.epc-ps-page .epc-ps-group-chip{
	display:inline-block;
	background:#f1f5f9;
	border:1px solid var(--ps-line);
	border-radius:999px;
	padding:1px 8px;
	font-size:11px;
	margin:1px 2px 1px 0;
	color:var(--ps-ink);
	max-width:100%;
	white-space:nowrap;
	overflow:hidden;
	text-overflow:ellipsis;
}
.epc-ps-page #my_list_emails{
	width:100%;
	min-height:280px;
	height:320px;
	resize:vertical;
	border:1px solid var(--ps-line);
	border-radius:8px;
	padding:10px 12px;
	font-size:13px;
	line-height:1.4;
}
.epc-ps-page .epc-ps-inline-shop{
	display:flex;
	flex-wrap:wrap;
	gap:10px 14px;
	align-items:center;
	margin-bottom:12px;
}
.epc-ps-page .epc-ps-inline-shop label{
	margin:0;
	font-weight:600;
	color:var(--ps-ink);
}
.epc-ps-page .epc-ps-inline-shop .form-control{
	width:auto;
	min-width:220px;
	display:inline-block;
}
.epc-ps-page .epc-ps-filters-row{
	display:grid;
	grid-template-columns:repeat(3,minmax(0,1fr));
	gap:14px;
	margin-bottom:4px;
}
@media(max-width:900px){
	.epc-ps-page .epc-ps-filters-row{grid-template-columns:1fr}
}
.epc-ps-page .epc-ps-help{
	font-size:12px;
	color:var(--ps-muted);
	margin:6px 0 0;
	line-height:1.4;
}
.epc-ps-page .epc-ps-tree-tools{
	display:flex;
	flex-wrap:wrap;
	gap:8px;
	margin-bottom:10px;
}
.epc-ps-page #container_A{
	height:320px;
	border:1px solid var(--ps-line);
	border-radius:8px;
	overflow:auto;
	background:#fff;
}
.epc-ps-page .epc-ps-generate{
	display:flex;
	flex-wrap:wrap;
	gap:10px;
	align-items:center;
}
.epc-ps-page .epc-ps-generate .btn{margin:0}
.epc-ps-page #create_prices_status{margin-top:12px;font-size:13px}
.epc-ps-page .epc-ps-file-list a{display:inline-block;margin:4px 8px 4px 0}
.epc-ps-page .btn-success{background:var(--ps-ok);border-color:var(--ps-ok)}
.epc-ps-page .btn-primary{background:var(--ps-accent);border-color:var(--ps-accent)}
.epc-ps-page .label-success{background:var(--ps-ok)}
.epc-ps-page .label-warning{background:var(--ps-warn)}
.epc-ps-page .panel-body{padding:16px 18px}
.epc-ps-page .panel-footer{
	background:#f8fafc;
	border-top:1px solid var(--ps-line);
	padding:12px 18px;
}
.epc-ps-page .table-condensed>thead>tr>th{
	border-bottom:1px solid var(--ps-line);
	color:var(--ps-muted);
	font-size:11px;
	text-transform:uppercase;
	letter-spacing:.04em;
	background:#f8fafc;
	position:sticky;
	top:0;
	z-index:1;
}
</style>

<div class="epc-ps-page">
	<div class="epc-ps-hero">
		<div>
			<h2>Send price lists</h2>
			<p>Build CSV price profiles with group markup, optional brand/article filters, then download or email customers.</p>
		</div>
		<div class="epc-ps-hero-steps" aria-label="Workflow">
			<span><b>1</b> Recipients</span>
			<span><b>2</b> Sources</span>
			<span><b>3</b> Filters</span>
			<span><b>4</b> Generate</span>
		</div>
	</div>

	<!-- Step 1: Recipients -->
	<div class="epc-ps-section">
		<div class="hpanel">
			<div class="panel-heading hbuilt">
				<span class="epc-ps-section-label">Step 1</span>
				<?php echo translate_str_by_id(3662); ?>
				<span class="epc-ps-heading-note">Select customers and/or paste emails</span>
			</div>
			<div class="panel-body">
				<div class="epc-ps-filter-grid">
					<div class="epc-ps-field">
						<label for="user_id">ID</label>
						<input type="text" id="user_id" value="<?php echo htmlspecialchars((string)$user_id, ENT_QUOTES, 'UTF-8'); ?>" class="form-control" />
					</div>
					<div class="epc-ps-field">
						<label for="group_id"><?php echo translate_str_by_id(3664); ?></label>
						<select id="group_id" class="form-control">
							<option value="-1"><?php echo translate_str_by_id(2094); ?></option>
							<?php
							$groups_query = $db_link->prepare("SELECT * FROM `groups`");
							$groups_query->execute();
							while ($group = $groups_query->fetch()) {
								?>
								<option value="<?php echo (int)$group["id"]; ?>"><?php echo translate_str_by_id($group["value"])." (ID ".(int)$group["id"].")"; ?></option>
								<?php
							}
							?>
						</select>
						<script>document.getElementById("group_id").value = <?php echo (int)$group_id; ?>;</script>
					</div>
					<div class="epc-ps-field">
						<label for="email">E-mail</label>
						<input type="text" id="email" value="<?php echo htmlspecialchars((string)$email, ENT_QUOTES, 'UTF-8'); ?>" class="form-control"/>
					</div>
					<div class="epc-ps-field">
						<label for="cellphone"><?php echo translate_str_by_id(1312); ?></label>
						<input type="text" id="cellphone" value="<?php echo htmlspecialchars((string)$cellphone, ENT_QUOTES, 'UTF-8'); ?>" class="form-control"/>
					</div>
					<div class="epc-ps-field">
						<label for="surname"><?php echo translate_str_by_id(3665); ?></label>
						<input type="text" id="surname" value="<?php echo htmlspecialchars((string)$surname, ENT_QUOTES, 'UTF-8'); ?>" class="form-control"/>
					</div>
				</div>
				<div class="epc-ps-actions">
					<button class="btn btn-success" type="button" onclick="filterUsers();"><i class="fa fa-filter"></i> <?php echo translate_str_by_id(2232); ?></button>
					<button class="btn btn-default" type="button" onclick="unsetFilterUsers();"><i class="fa fa-times"></i> <?php echo translate_str_by_id(2555); ?></button>
				</div>
			</div>
		</div>
	</div>

	<script>
	function filterUsers()
	{
		var users_filter_send_prices = new Object;
		users_filter_send_prices.user_id = document.getElementById("user_id").value;
		users_filter_send_prices.group_id = document.getElementById("group_id").value;
		users_filter_send_prices.email = document.getElementById("email").value;
		users_filter_send_prices.cellphone = document.getElementById("cellphone").value;
		users_filter_send_prices.surname = document.getElementById("surname").value;
		var date = new Date(new Date().getTime() + 15552000 * 1000);
		document.cookie = "users_filter_send_prices="+JSON.stringify(users_filter_send_prices)+"; path=/; expires=" + date.toUTCString();
		location='/<?php echo $DP_Config->backend_dir; ?>/shop/prices_send';
	}
	function unsetFilterUsers()
	{
		var users_filter_send_prices = new Object;
		users_filter_send_prices.user_id = "";
		users_filter_send_prices.group_id = -1;
		users_filter_send_prices.email = "";
		users_filter_send_prices.cellphone = "";
		users_filter_send_prices.surname = "";
		var date = new Date(new Date().getTime() + 15552000 * 1000);
		document.cookie = "users_filter_send_prices="+JSON.stringify(users_filter_send_prices)+"; path=/; expires=" + date.toUTCString();
		location='/<?php echo $DP_Config->backend_dir; ?>/shop/prices_send';
	}
	function sortUsers(field)
	{
		var asc_desc = "asc";
		var current_sort_cookie = getCookie("users_sort_send_prices");
		if(current_sort_cookie != undefined)
		{
			current_sort_cookie = JSON.parse(getCookie("users_sort_send_prices"));
			if(current_sort_cookie.field == field)
			{
				asc_desc = (current_sort_cookie.asc_desc == "asc") ? "desc" : "asc";
			}
		}
		var users_sort_send_prices = new Object;
		users_sort_send_prices.field = field;
		users_sort_send_prices.asc_desc = asc_desc;
		var date = new Date(new Date().getTime() + 15552000 * 1000);
		document.cookie = "users_sort_send_prices="+JSON.stringify(users_sort_send_prices)+"; path=/; expires=" + date.toUTCString();
		location='/<?php echo $DP_Config->backend_dir; ?>/shop/prices_send';
	}
	function getCookie(name)
	{
		name = String(name || '');
		var prefix = name + '=';
		var parts = String(document.cookie || '').split(';');
		for (var i = 0; i < parts.length; i++) {
			var part = parts[i].replace(/^\s+/, '');
			if (part.indexOf(prefix) === 0) {
				return decodeURIComponent(part.substring(prefix.length));
			}
		}
		return undefined;
	}
	</script>

	<div class="epc-ps-section">
		<div class="epc-ps-split">
			<div class="hpanel">
				<div class="panel-heading hbuilt">
					<?php echo translate_str_by_id(3666); ?>
				</div>
				<div class="panel-body">
					<div class="epc-ps-scroll">
						<table cellpadding="1" cellspacing="1" class="table table-condensed table-striped">
							<thead>
								<tr>
									<th><input type="checkbox" id="check_uncheck_all_users" name="check_uncheck_all_users" onchange="on_check_uncheck_all_users();"/></th>
									<th><a href="javascript:void(0);" onclick="sortUsers('user_id');" id="user_id_sorter">ID</a></th>
									<th><?php echo translate_str_by_id(3664); ?></th>
									<th><a href="javascript:void(0);" onclick="sortUsers('email');" id="email_sorter">E-mail</a></th>
									<th><a href="javascript:void(0);" onclick="sortUsers('fio');" id="fio_sorter"><?php echo translate_str_by_id(3667); ?></a></th>
								</tr>
								<script>
									document.getElementById("<?php echo $sort_field; ?>_sorter").innerHTML += "<img src=\"/content/files/images/sort_<?php echo $sort_asc_desc; ?>.png\" style=\"width:15px\" />";
								</script>
							</thead>
							<tbody>
							<?php
							$groups_list_query = $db_link->prepare("SELECT * FROM `groups`");
							$groups_list_query->execute();
							$groups_list = array();
							while ($groups_list_record = $groups_list_query->fetch()) {
								$groups_list[$groups_list_record["id"]] = $groups_list_record["value"];
							}

							$for_js = "var users_array = new Array();\n";
							$for_js = $for_js."var users_id_array = new Array();\n";

							$WHERE_CONDITIONS = "";
							$binding_values = array();
							$users_filter_send_prices = NULL;
							if (isset($_COOKIE["users_filter_send_prices"])) {
								$users_filter_send_prices = $_COOKIE["users_filter_send_prices"];
							}
							if ($users_filter_send_prices != NULL) {
								$users_filter_send_prices = json_decode($users_filter_send_prices, true);

								if ($users_filter_send_prices["user_id"] != "") {
									if ($WHERE_CONDITIONS != "") { $WHERE_CONDITIONS .= " AND "; }
									$WHERE_CONDITIONS .= " `users`.`user_id` = ?";
									array_push($binding_values, $users_filter_send_prices["user_id"]);
								}
								if ($users_filter_send_prices["group_id"] != -1) {
									if ($WHERE_CONDITIONS != "") { $WHERE_CONDITIONS .= " AND "; }
									$WHERE_CONDITIONS .= " `users_groups_bind`.`group_id` = ?";
									array_push($binding_values, $users_filter_send_prices["group_id"]);
								}
								if ($users_filter_send_prices["email"] != "") {
									if ($WHERE_CONDITIONS != "") { $WHERE_CONDITIONS .= " AND "; }
									$WHERE_CONDITIONS .= " `users`.`email` = ?";
									array_push($binding_values, htmlentities($users_filter_send_prices["email"]));
								}
								if ($users_filter_send_prices["cellphone"] != "") {
									if ($WHERE_CONDITIONS != "") { $WHERE_CONDITIONS .= " AND "; }
									$WHERE_CONDITIONS .= " IF( (SELECT COUNT(`users_profiles`.`user_id`) FROM users_profiles WHERE `users_profiles`.`data_key` ='cellphone' AND `users_profiles`.`data_value` = ? AND `users_profiles`.`user_id` = `users`.`user_id`)=1 , 1, 0 )=1";
									array_push($binding_values, $users_filter_send_prices["cellphone"]);
								}
								if ($users_filter_send_prices["surname"] != "") {
									if ($WHERE_CONDITIONS != "") { $WHERE_CONDITIONS .= " AND "; }
									$WHERE_CONDITIONS .= " IF( (SELECT COUNT(`users_profiles`.`user_id`) FROM users_profiles WHERE `users_profiles`.`data_key` ='surname' AND `users_profiles`.`data_value` = ? AND `users_profiles`.`user_id` = `users`.`user_id`)=1 , 1, 0 )=1";
									array_push($binding_values, $users_filter_send_prices["surname"]);
								}
								if ($WHERE_CONDITIONS != "") {
									$WHERE_CONDITIONS = " WHERE ".$WHERE_CONDITIONS;
								}
							}

							$users_list_SQL = "SELECT SQL_CALC_FOUND_ROWS DISTINCT(`users`.`user_id`) AS `user_id`,
							`users`.`email` AS `email`,
							`users`.`email_confirmed` AS `email_confirmed`,
							trim(concat((SELECT `data_value` FROM `users_profiles` WHERE `users_profiles`.`data_key` ='surname' AND `users_profiles`.`user_id` = `users`.`user_id`), ' ', (SELECT `data_value` FROM `users_profiles` WHERE `users_profiles`.`data_key` ='name' AND `users_profiles`.`user_id` = `users`.`user_id`), ' ', (SELECT `data_value` FROM `users_profiles` WHERE `users_profiles`.`data_key` ='patronymic' AND `users_profiles`.`user_id` = `users`.`user_id`))) AS 'fio'
								FROM
							users
							INNER JOIN `users_profiles` ON `users`.`user_id` = `users_profiles`.`user_id`
							INNER JOIN `users_groups_bind` ON `users_groups_bind`.`user_id` = `users`.`user_id`".$WHERE_CONDITIONS." ORDER BY `$sort_field` $sort_asc_desc";

							$users_list_query = $db_link->prepare($users_list_SQL);
							$users_list_query->execute($binding_values);

							$elements_count_rows_query = $db_link->prepare('SELECT FOUND_ROWS();');
							$elements_count_rows_query->execute();
							$elements_count_rows = $elements_count_rows_query->fetchColumn();

							for ($i = 0; $i < $elements_count_rows; $i++) {
								$users_list_array = $users_list_query->fetch();
								?>
								<tr>
									<td>
										<?php
										if (!empty($users_list_array["email"])) {
											?>
											<input type="checkbox" onchange="on_one_check_changed_users('checked_users_<?php echo (int)$users_list_array["user_id"]; ?>');" id="checked_users_<?php echo (int)$users_list_array["user_id"]; ?>" name="checked_users_<?php echo (int)$users_list_array["user_id"]; ?>"/>
											<?php
											$for_js = $for_js."users_array[users_array.length] = \"checked_users_".$users_list_array["user_id"]."\";\n";
											$for_js = $for_js."users_id_array[users_id_array.length] = ".$users_list_array["user_id"].";\n";
										}
										?>
									</td>
									<td><?php echo (int)$users_list_array["user_id"]; ?></td>
									<td>
										<?php
										$user_groups_list_query = $db_link->prepare("SELECT DISTINCT `group_id` FROM `users_groups_bind` WHERE `user_id` = ?;");
										$user_groups_list_query->execute(array($users_list_array["user_id"]));
										$seen_groups = array();
										while ($user_group_record = $user_groups_list_query->fetch()) {
											$gid = (int)$user_group_record["group_id"];
											if (isset($seen_groups[$gid])) {
												continue;
											}
											$seen_groups[$gid] = true;
											$gname = isset($groups_list[$gid]) ? translate_str_by_id($groups_list[$gid]) : ('#'.$gid);
											echo '<span class="epc-ps-group-chip" title="'.htmlspecialchars($gname, ENT_QUOTES, 'UTF-8').'">'.htmlspecialchars($gname, ENT_QUOTES, 'UTF-8').'</span>';
										}
										?>
									</td>
									<td>
										<?php
										if (!empty($users_list_array["email"])) {
											echo htmlspecialchars((string)$users_list_array["email"], ENT_QUOTES, 'UTF-8');
											if ($users_list_array["email_confirmed"]) {
												?>
												<i class="fa fa-check-circle" style="color:#0A0;cursor:pointer;" title="<?php echo translate_str_by_id(3546); ?>"></i>
												<?php
											} else {
												?>
												<i class="fa fa-exclamation-triangle" style="color:#F00;cursor:pointer;" title="<?php echo translate_str_by_id(3545); ?>"></i>
												<?php
											}
										} else {
											echo "E-mail ".translate_str_by_id(3253);
										}
										?>
									</td>
									<td><?php echo htmlspecialchars((string)$users_list_array["fio"], ENT_QUOTES, 'UTF-8'); ?></td>
								</tr>
								<?php
							}
							?>
							</tbody>
						</table>
					</div>
				</div>
			</div>

			<div class="hpanel">
				<div class="panel-heading hbuilt">
					<?php echo translate_str_by_id(3668); ?>
				</div>
				<div class="panel-body">
					<textarea id="my_list_emails" placeholder="email1@example.com, email2@example.com"></textarea>
					<div class="epc-ps-field" style="margin-top:12px;">
						<label for="group_id_my_list_emails"><?php echo translate_str_by_id(3669); ?></label>
						<select id="group_id_my_list_emails" class="form-control">
							<?php
							$groups_query = $db_link->prepare("SELECT * FROM `groups`");
							$groups_query->execute();
							while ($group = $groups_query->fetch()) {
								?>
								<option value="<?php echo (int)$group["id"]; ?>"><?php echo translate_str_by_id($group["value"])." (ID ".(int)$group["id"].")"; ?></option>
								<?php
							}
							?>
						</select>
					</div>
				</div>
			</div>
		</div>
	</div>

	<script>
	<?php echo $for_js; ?>
	function on_check_uncheck_all_users()
	{
		var state = document.getElementById("check_uncheck_all_users").checked;
		for(var i=0; i<users_array.length;i++)
		{
			document.getElementById(users_array[i]).checked = state;
		}
	}
	function on_one_check_changed_users(id)
	{
		for(var i=0; i<users_array.length;i++)
		{
			if(document.getElementById(users_array[i]).checked == false)
			{
				document.getElementById("check_uncheck_all_users").checked = false;
				break;
			}
		}
	}
	function get_users_list(){
		var users_list = new Array();
		for(var i=0; i < users_array.length; i++)
		{
			if(document.getElementById(users_array[i]).checked == true)
			{
				users_list.push(users_id_array[i]);
			}
		}
		return users_list;
	}
	</script>

	<!-- Step 2: Sources -->
	<div class="epc-ps-section">
		<div class="hpanel">
			<div class="panel-heading hbuilt">
				<span class="epc-ps-section-label">Step 2</span>
				<?php echo translate_str_by_id(3671); ?>
				<span class="epc-ps-heading-note">Shop + Docpart / catalogue warehouses</span>
			</div>
			<div class="panel-body">
				<div class="epc-ps-inline-shop">
					<label for="offices"><?php echo translate_str_by_id(3670); ?></label>
					<select id="offices" name="offices" class="form-control">
					<?php
					$SQL = "SELECT * FROM `shop_offices`";
					$query = $db_link->prepare($SQL);
					$query->execute();
					while ($array = $query->fetch()) {
						?>
						<option value="<?php echo (int)$array["id"]; ?>"><?php echo translate_str_by_id($array["caption"])." (ID ".(int)$array["id"].")"; ?></option>
						<?php
					}
					?>
					</select>
				</div>
				<div class="epc-ps-scroll" style="max-height:360px;">
					<table cellpadding="1" cellspacing="1" class="table table-condensed table-striped">
						<thead>
							<tr>
								<th><input checked type="checkbox" id="check_uncheck_all" name="check_uncheck_all" onchange="on_check_uncheck_all();"/></th>
								<th>ID</th>
								<th><?php echo translate_str_by_id(2277); ?></th>
								<th><?php echo translate_str_by_id(3474); ?></th>
								<th>Linked</th>
							</tr>
						</thead>
						<tbody>
						<?php
						$for_js = "var elements_array = new Array();\n";
						$for_js = $for_js."var elements_id_array = new Array();\n";

						$elements_query = $db_link->prepare("SELECT *, (SELECT `name` FROM `shop_storages_interfaces_types` WHERE `id` = `shop_storages`.`interface_type`) AS `interface_type_name` FROM `shop_storages` WHERE `interface_type` = 1 OR `interface_type` = 2;");
						$elements_query->execute();

						while ($element_record = $elements_query->fetch()) {
							$for_js = $for_js."elements_array[elements_array.length] = \"checked_".$element_record["id"]."\";\n";
							$for_js = $for_js."elements_id_array[elements_id_array.length] = ".$element_record["id"].";\n";
							$is_linked = in_array((int)$element_record['id'], $epc_ps_linked_storage_ids, true);
							?>
							<tr>
								<td><input checked type="checkbox" onchange="on_one_check_changed('checked_<?php echo (int)$element_record["id"]; ?>');" id="checked_<?php echo (int)$element_record["id"]; ?>" name="checked_<?php echo (int)$element_record["id"]; ?>"/></td>
								<td><?php echo (int)$element_record["id"]; ?></td>
								<td><?php echo htmlspecialchars($element_record["name"], ENT_QUOTES, 'UTF-8'); ?></td>
								<td><?php echo htmlspecialchars((string)$element_record["interface_type_name"], ENT_QUOTES, 'UTF-8'); ?></td>
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

<?php
require_once($_SERVER["DOCUMENT_ROOT"]."/content/shop/catalogue/dp_category_record.php");
require_once($_SERVER["DOCUMENT_ROOT"]."/content/shop/catalogue/get_catalogue_tree.php");
?>

	<!-- Step 3: Filters -->
	<div class="epc-ps-section">
		<div class="hpanel">
			<div class="panel-heading hbuilt">
				<span class="epc-ps-section-label">Step 3</span>
				Filters
				<span class="epc-ps-heading-note">Markup profile, brand, article, catalogue groups</span>
			</div>
			<div class="panel-body">
				<div class="epc-ps-filters-row">
					<div class="epc-ps-field">
						<label for="epc_ps_profile_group">Markup profile (by group)</label>
						<select id="epc_ps_profile_group" class="form-control">
							<option value="0">— Use selected customers / email group —</option>
							<?php
							$groups_query2 = $db_link->prepare("SELECT * FROM `groups` ORDER BY `id`");
							$groups_query2->execute();
							while ($g2 = $groups_query2->fetch()) {
								$label = function_exists('translate_str_by_id') ? translate_str_by_id($g2['value']) : $g2['value'];
								echo '<option value="'.(int)$g2['id'].'">'.htmlspecialchars($label.' (ID '.$g2['id'].')', ENT_QUOTES, 'UTF-8').'</option>';
							}
							?>
						</select>
						<p class="epc-ps-help">Generates CSV using this group's markup even without selecting customers.</p>
					</div>
					<div class="epc-ps-field">
						<label for="epc_ps_filter_brand">Brand filter (optional)</label>
						<input type="text" id="epc_ps_filter_brand" class="form-control" placeholder="e.g. TOYOTA" list="epc_ps_brand_list"/>
						<datalist id="epc_ps_brand_list"></datalist>
						<p class="epc-ps-help">Limits Docpart / catalogue rows to matching manufacturer.</p>
					</div>
					<div class="epc-ps-field">
						<label for="epc_ps_filter_article">Article / item filter (optional)</label>
						<input type="text" id="epc_ps_filter_article" class="form-control" placeholder="e.g. 8114560Q51"/>
						<p class="epc-ps-help">Partial match on part number.</p>
					</div>
				</div>
			</div>
		</div>
	</div>

	<div class="epc-ps-section">
		<div class="hpanel">
			<div class="panel-heading hbuilt">
				<span class="epc-ps-section-label">Step 3</span>
				<?php echo translate_str_by_id(3672); ?>
				<span class="epc-ps-heading-note">Optional catalogue categories</span>
			</div>
			<div class="panel-body">
				<div class="epc-ps-tree-tools">
					<button type="button" onclick="catalogue_tree.checkAll();" class="btn btn-sm btn-success"><?php echo translate_str_by_id(2293); ?></button>
					<button type="button" onclick="catalogue_tree.uncheckAll();" class="btn btn-sm btn-default"><?php echo translate_str_by_id(2294); ?></button>
				</div>
				<div id="container_A"></div>
				<div class="hidden" style="padding:15px 0;">
					<label for="storages"><?php echo translate_str_by_id(2750); ?>: </label>
					<select id="storages" name="storages" class="form-control" style="display:inline-block; width: auto;">
					<?php
						$storages_query = $db_link->prepare("SELECT * FROM `shop_storages`");
						$storages_query->execute();
						while ($storages = $storages_query->fetch()) {
							if ((int)$storages['interface_type'] === 1) {
								$arr_users = json_decode($storages['users']);
								foreach ($arr_users as $id_user) {
									if ((int)$id_user === (int)$admin_id) {
										?>
										<option value="<?php echo (int)$storages["id"]; ?>"><?php echo htmlspecialchars($storages["name"]." (ID ".$storages["id"].")", ENT_QUOTES, 'UTF-8'); ?></option>
										<?php
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

	<!-- Step 4: Generate -->
	<div class="epc-ps-section">
		<div class="hpanel">
			<div class="panel-heading hbuilt">
				<span class="epc-ps-section-label">Step 4</span>
				<?php echo translate_str_by_id(2755); ?>
			</div>
			<div class="panel-body">
				<div class="epc-ps-generate">
					<button type="button" onclick="create_prices();" class="btn btn-success"><i class="fa fa-cogs"></i> <?php echo translate_str_by_id(3673); ?></button>
					<button type="button" onclick="epcPsLinkStorages();" class="btn btn-warning"><i class="fa fa-link"></i> Link selected storages</button>
					<button type="button" id="send_prices_btn" onclick="send_prices();" disabled class="btn btn-primary"><i class="fa fa-envelope"></i> <?php echo translate_str_by_id(3674); ?></button>
				</div>
				<div id="create_prices_status"></div>
			</div>
		</div>
	</div>
</div>

<script>
function send_prices(){
	if(!document.getElementById('send_prices_btn').hasAttribute('disabled')){
		document.getElementById('send_prices_btn').setAttribute('disabled', 'disabled');
	}
	var users_list = get_users_list();
	var group_id_my_list_emails = 0;
	var emails_list = document.getElementById('my_list_emails').value;
	if(emails_list != ''){
		var n = document.getElementById("group_id_my_list_emails").options.selectedIndex;
		group_id_my_list_emails = document.getElementById("group_id_my_list_emails").options[n].value;
	}
	if(users_list.length == 0 && emails_list == ''){
		alert('<?php echo translate_str_by_id(3675); ?>');
		document.getElementById('send_prices_btn').removeAttribute('disabled');
		return;
	}
	document.getElementById('create_prices_status').innerHTML = '<div class="text-muted"><i class="fa fa-spinner fa-spin"></i> Sending…</div>';
	var request_object = new Object;
	request_object.action = 'send_prices';
	request_object.users_list = users_list;
	request_object.emails_list = emails_list;
	request_object.group_id_my_list_emails = group_id_my_list_emails;
	jQuery.ajax({
		type: "POST",
		async: true,
		url: "/<?php echo $DP_Config->backend_dir; ?>/content/shop/prices_send/ajax_operations.php",
		dataType: "json",
		data: "request_object="+encodeURIComponent(JSON.stringify(request_object))+"&csrf_guard_key=<?php echo $user_session["csrf_guard_key"]; ?>",
		success: function(answer)
		{
			if(answer && answer.status == true)
			{
			   document.getElementById('create_prices_status').innerHTML = '<div class="alert alert-success"><?php echo translate_str_by_id(3676); ?>'+(answer.sent?(' ('+answer.sent+' sent)'):'')+'</div>';
			   document.getElementById('send_prices_btn').removeAttribute('disabled');
			}
			else
			{
				alert((answer && answer.message) ? answer.message : "<?php echo translate_str_by_id(3677); ?>");
				document.getElementById('create_prices_status').innerHTML = '';
				document.getElementById('send_prices_btn').removeAttribute('disabled');
			}
		},
		error: function(xhr){
			alert('Send failed (HTTP '+(xhr&&xhr.status?xhr.status:'?')+')');
			document.getElementById('send_prices_btn').removeAttribute('disabled');
		}
	});
}

function epcPsCollectFilters(request_object){
	var profile = parseInt(document.getElementById('epc_ps_profile_group').value, 10) || 0;
	request_object.profile_group_ids = profile > 0 ? [profile] : [];
	request_object.filter_brand = (document.getElementById('epc_ps_filter_brand').value || '').trim();
	request_object.filter_article = (document.getElementById('epc_ps_filter_article').value || '').trim();
	if (profile > 0 && (!request_object.group_id_my_list_emails || request_object.group_id_my_list_emails == 0)) {
		request_object.group_id_my_list_emails = profile;
	}
}

function epcPsLinkStorages(){
	var offices = document.getElementById("offices").options[document.getElementById("offices").options.selectedIndex].value;
	var arr_storages = getCheckedElements();
	if(arr_storages.length == 0){ alert('<?php echo translate_str_by_id(3678); ?>'); return; }
	var profile = parseInt(document.getElementById('epc_ps_profile_group').value, 10) || 0;
	var request_object = {
		action: 'ensure_office_storage_links',
		offices: offices,
		arr_storages: arr_storages,
		group_ids: profile > 0 ? [profile, 2, 4, 5, 6, 7] : [2, 4, 5, 6, 7]
	};
	document.getElementById('create_prices_status').innerHTML = '<div class="text-muted"><i class="fa fa-spinner fa-spin"></i> Linking storages…</div>';
	jQuery.ajax({
		type: "POST",
		url: "/<?php echo $DP_Config->backend_dir; ?>/content/shop/prices_send/ajax_operations.php",
		dataType: "json",
		data: "request_object="+encodeURIComponent(JSON.stringify(request_object))+"&csrf_guard_key=<?php echo $user_session["csrf_guard_key"]; ?>",
		success: function(answer){
			if(answer && answer.status){
				document.getElementById('create_prices_status').innerHTML = '<div class="alert alert-success">'+(answer.message||'Linked')+'. Reload to refresh Linked column.</div>';
			} else {
				document.getElementById('create_prices_status').innerHTML = '<div class="alert alert-danger">'+(answer&&answer.message?answer.message:'Link failed')+'</div>';
			}
		},
		error: function(xhr){ document.getElementById('create_prices_status').innerHTML = '<div class="alert alert-danger">Link failed HTTP '+(xhr.status||'?')+'</div>'; }
	});
}

var check_office_storages_map = false;
function create_prices(){
	if(!document.getElementById('send_prices_btn').hasAttribute('disabled')){
		document.getElementById('send_prices_btn').setAttribute('disabled', 'disabled');
	}
	var users_list = get_users_list();
	var group_id_my_list_emails = 0;
	var emails_list = document.getElementById('my_list_emails').value;
	if(emails_list != ''){
		var n = document.getElementById("group_id_my_list_emails").options.selectedIndex;
		group_id_my_list_emails = document.getElementById("group_id_my_list_emails").options[n].value;
	}
	var profile = parseInt(document.getElementById('epc_ps_profile_group').value, 10) || 0;
	if(users_list.length == 0 && emails_list == '' && profile <= 0){
		alert('Select customers, enter emails, or choose a markup profile group.');
		return;
	}
	var storages = 0;
	var arr_category = catalogue_tree.getChecked();
	if(arr_category.length > 0){
		var n = document.getElementById("storages").options.selectedIndex;
		if (n >= 0) {
			storages = document.getElementById("storages").options[n].value;
		}
	}
	var arr_storages = getCheckedElements();
	if(arr_storages.length == 0)
	{
		alert('<?php echo translate_str_by_id(3678); ?>');
		return false;
	}
	var offices = document.getElementById("offices").options[document.getElementById("offices").options.selectedIndex].value;
	var request_object = new Object;
	request_object.action = 'check_office_storages_map';
	request_object.offices = offices;
	request_object.arr_storages = arr_storages;
	request_object.arr_category = arr_category;
	request_object.users_list = users_list;
	request_object.emails_list = emails_list;
	request_object.group_id_my_list_emails = group_id_my_list_emails;
	request_object.storages = storages;
	epcPsCollectFilters(request_object);
	check_office_storages_map = false;
	jQuery.ajax({
		type: "POST",
		async: false,
		url: "/<?php echo $DP_Config->backend_dir; ?>/content/shop/prices_send/ajax_operations.php",
		dataType: "json",
		data: "request_object="+encodeURIComponent(JSON.stringify(request_object))+"&csrf_guard_key=<?php echo $user_session["csrf_guard_key"]; ?>",
		success: function(answer)
		{
			if(answer.status != true)
			{
				check_office_storages_map = false;
				var msg = "<?php echo translate_str_by_id(3679); ?>: " + (answer.message||'') + ". <?php echo translate_str_by_id(3680); ?>.";
				if (answer.can_link) {
					msg += "\n\nClick “Link selected storages” then try Generate again.";
				}
				alert(msg);
			}
			else
			{
				check_office_storages_map = true;
			}
		}
	});
	if( check_office_storages_map == false )
	{
		return;
	}
	else
	{
		request_object.action = 'create_prices';
	}
	document.getElementById('create_prices_status').innerHTML = '<div class="text-muted"><i class="fa fa-spinner fa-spin"></i> Generating price profile…</div>';
	jQuery.ajax({
		type: "POST",
		async: true,
		url: "/<?php echo $DP_Config->backend_dir; ?>/content/shop/prices_send/ajax_operations.php",
		dataType: "json",
		data: "request_object="+encodeURIComponent(JSON.stringify(request_object))+"&csrf_guard_key=<?php echo $user_session["csrf_guard_key"]; ?>",
		success: function(answer)
		{
			if(answer && answer.status == true)
			{
				var html = '<div class="alert alert-success">'+(answer.message || '<?php echo translate_str_by_id(3681); ?>.')+'</div>';
				if (answer.files && answer.files.length) {
					html += '<div class="epc-ps-file-list">';
					for (var i=0;i<answer.files.length;i++) {
						var f = answer.files[i];
						html += '<a class="btn btn-default btn-sm" href="'+f.url+'" download><i class="fa fa-download"></i> '+f.file+' ('+f.rows+' rows, group '+f.group_id+')</a>';
					}
					html += '</div>';
				}
				document.getElementById('create_prices_status').innerHTML = html;
				document.getElementById('send_prices_btn').removeAttribute('disabled');
			}
			else
			{
				alert((answer && answer.message) ? answer.message : "<?php echo translate_str_by_id(3682); ?>");
				document.getElementById('create_prices_status').innerHTML = '';
			}
		},
		error: function(xhr){
			alert('Generate failed (HTTP '+(xhr&&xhr.status?xhr.status:'?')+')');
			document.getElementById('create_prices_status').innerHTML = '';
		}
	});
}

<?php echo $for_js; ?>
function on_check_uncheck_all()
{
	var state = document.getElementById("check_uncheck_all").checked;
	for(var i=0; i<elements_array.length;i++)
	{
		document.getElementById(elements_array[i]).checked = state;
	}
}
function on_one_check_changed(id)
{
	for(var i=0; i<elements_array.length;i++)
	{
		if(document.getElementById(elements_array[i]).checked == false)
		{
			document.getElementById("check_uncheck_all").checked = false;
			break;
		}
	}
}
function getCheckedElements()
{
	var checked_ids = new Array();
	for(var i=0; i<elements_array.length;i++)
	{
		if(document.getElementById(elements_array[i]).checked == true)
		{
			checked_ids.push(elements_id_array[i]);
		}
	}
	return checked_ids;
}
function update_price(){
	var sel = document.getElementById("storages");
	var val = sel.options[sel.selectedIndex].value;
	if(document.getElementById("delete_price_all").checked){
		var delete_price_all = 1;
	}else{
		var delete_price_all = 0;
	}
	if( (val*1) == 0 ){
		alert('Выберите склад');
		return false;
	}
	var arr = getCheckedElements();
	if(arr.length == 0){
		alert('<?php echo translate_str_by_id(3683); ?>');
		return false;
	}
	var arr_category = catalogue_tree.getChecked();
	if(arr_category.length == 0){
		alert('<?php echo translate_str_by_id(3222); ?>');
		return false;
	}
	document.getElementById("array_id_prices").value = JSON.stringify(arr);
	document.getElementById("array_id_category").value = JSON.stringify(arr_category);
	document.getElementById("storage_id").value = val;
	document.getElementById("inp_delete_price_all").value = delete_price_all;
	document.forms["update_price_form"].submit();
	return false;
}

/*ДЕРЕВО КАТАЛОГА ТОВАРОВ*/
webix.protoUI({
	name:"edittree"
}, webix.EditAbility, webix.ui.tree);
catalogue_tree = new webix.ui({
	editable:false,
	container:"container_A",
	view:"tree",
	select:false,
	drag:false,
	template:function(obj, common)
	{
		var folder = common.folder(obj, common);
		var value_text = "<span>" + obj.value + "</span>";
		var checkbox = common.checkbox(obj, common);
		return common.icon(obj, common) + checkbox + folder + value_text;
	},
});
webix.event(window, "resize", function(){ catalogue_tree.adjust(); });
var saved_catalogue = <?php echo $catalogue_tree_dump_JSON; ?>;
catalogue_tree.parse(saved_catalogue);
catalogue_tree.openAll();

(function(){
	var request_object = {action:'list_brands', limit:40};
	jQuery.ajax({
		type:'POST',
		url: "/<?php echo $DP_Config->backend_dir; ?>/content/shop/prices_send/ajax_operations.php",
		dataType:'json',
		data: "request_object="+encodeURIComponent(JSON.stringify(request_object))+"&csrf_guard_key=<?php echo $user_session["csrf_guard_key"]; ?>",
		success: function(answer){
			if(!answer || !answer.status || !answer.brands) return;
			var dl = document.getElementById('epc_ps_brand_list');
			if(!dl) return;
			dl.innerHTML = '';
			answer.brands.forEach(function(b){
				var opt = document.createElement('option');
				opt.value = b.brand;
				opt.label = b.brand + ' (' + b.count + ')';
				dl.appendChild(opt);
			});
		}
	});
})();
</script>
