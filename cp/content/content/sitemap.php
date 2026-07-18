<?php
/**
 * CP sitemap manager — CMS-eval safe.
 *
 * Scripts are stripped by epc_cp_prepare_cp_page_content() BEFORE eval, so any
 * <?php inside <script> never runs and becomes literal footer HTML. Build JS
 * after PHP setup and append via epc_cp_footer_scripts_append().
 */
defined('_ASTEXE_') or die('No access');

require_once($_SERVER["DOCUMENT_ROOT"]."/".$DP_Config->backend_dir."/content/content/dp_content_record.php");
require_once($_SERVER["DOCUMENT_ROOT"]."/".$DP_Config->backend_dir."/content/content/get_content_records.php");

if (!isset($is_frontend)) {
	$is_frontend = 1;
}

$get_main_id_query = $db_link->prepare("SELECT * FROM `content` WHERE `main_flag`=1 AND `is_frontend`=?;");
$get_main_id_query->execute(array($is_frontend));
$get_main_id_record = $get_main_id_query->fetch();
$current_main_id = ($get_main_id_record && isset($get_main_id_record["id"])) ? (int)$get_main_id_record["id"] : 0;
unset($current_main_id); // reserved for future deep-link; tree dump covers all pages

if (!isset($content_tree_dump_JSON) || $content_tree_dump_JSON === '' || $content_tree_dump_JSON === false) {
	$content_tree_dump_JSON = '[]';
}

require_once($_SERVER['DOCUMENT_ROOT']."/content/users/dp_user.php");
$user_session = DP_User::getAdminSession();
$csrf = is_array($user_session) && isset($user_session["csrf_guard_key"]) ? (string)$user_session["csrf_guard_key"] : '';

$backend_dir = (string)$DP_Config->backend_dir;
$template_name = (string)$DP_Template->name;
$save_icon = '/'.$backend_dir.'/templates/'.$template_name.'/images/save.png';
$power_icon = '/'.$backend_dir.'/templates/'.$template_name.'/images/power_off.png';
$gear_icon = '/'.$backend_dir.'/templates/'.$template_name.'/images/gear.png';
$lock_icon = '/'.$backend_dir.'/templates/'.$template_name.'/images/lock.png';
$star_icon = '/'.$backend_dir.'/templates/'.$template_name.'/images/star.png';

$alert_empty = translate_str_by_id(2298);
$alert_ok = translate_str_by_id(2299);
$ajax_url = '/'.$backend_dir.'/content/content/ajax_create_sitemap.php';
?>

<div id="messages"></div>

<div class="col-lg-12">
	<div class="hpanel">
		<div class="panel-heading hbuilt">
			<?php echo translate_str_by_id(2113); ?>
		</div>
		<div class="panel-body">
			<a class="panel_a" onClick="create_sitemap();" href="javascript:void(0);">
				<div class="panel_a_img" style="background: url('<?php echo htmlspecialchars($save_icon, ENT_QUOTES, 'UTF-8'); ?>') 0 0 no-repeat;"></div>
				<div class="panel_a_caption"><?php echo translate_str_by_id(2292); ?></div>
			</a>
			<a class="panel_a" href="/<?php echo htmlspecialchars($backend_dir, ENT_QUOTES, 'UTF-8'); ?>">
				<div class="panel_a_img" style="background: url('<?php echo htmlspecialchars($power_icon, ENT_QUOTES, 'UTF-8'); ?>') 0 0 no-repeat;"></div>
				<div class="panel_a_caption"><?php echo translate_str_by_id(2116); ?></div>
			</a>
		</div>
	</div>
</div>

<div class="col-lg-6">
	<div class="hpanel">
		<div class="panel-heading hbuilt">
			<?php echo translate_str_by_id(2271); ?>
		</div>
		<div class="panel-body">
			<div style="padding:0 0 10px 0;">
				<button onclick="content_tree.checkAll();" class="btn w-xs btn-success"><?php echo translate_str_by_id(2293); ?></button>
				<button onclick="content_tree.uncheckAll();" class="btn w-xs btn-primary2"><?php echo translate_str_by_id(2294); ?></button>
			</div>
			<div id="container_A" style="height:350px;"></div>
		</div>
	</div>
</div>

<div class="col-lg-6">
	<div class="hpanel">
		<div class="panel-heading hbuilt">
			<?php echo translate_str_by_id(2295); ?>
		</div>
		<div class="panel-body">
			<?php echo translate_str_by_id(2296); ?>
			<br/>
			<?php echo translate_str_by_id(2297); ?>
		</div>
	</div>
</div>

<?php
$cfg = array(
	'ajaxUrl' => $ajax_url,
	'csrf' => $csrf,
	'alertEmpty' => $alert_empty,
	'alertOk' => $alert_ok,
	'gear' => $gear_icon,
	'lock' => $lock_icon,
	'star' => $star_icon,
);
$js = '<script>(function(){'
	. 'var CFG=' . json_encode($cfg, JSON_UNESCAPED_SLASHES | JSON_UNESCAPED_UNICODE) . ';'
	. 'window.create_sitemap=function(){'
	. 'document.getElementById("messages").innerHTML="";'
	. 'var url_list=[];'
	. 'var checked=content_tree.getChecked();'
	. 'for(var i=0;i<checked.length;i++){url_list.push({url:content_tree.getItem(checked[i]).url});}'
	. 'if(url_list.length==0){alert(CFG.alertEmpty);return;}'
	. 'jQuery.ajax({type:"POST",async:false,url:CFG.ajaxUrl,dataType:"text",'
	. 'data:"url_list="+JSON.stringify(url_list)+"&csrf_guard_key="+encodeURIComponent(CFG.csrf),'
	. 'success:function(){alert(CFG.alertOk);}});'
	. '};'
	. 'content_tree=new webix.ui({'
	. 'template:function(obj,common){'
	. 'var folder=common.folder(obj,common),icon="",value_text="<span>"+obj.value+"</span>",checkbox=common.checkbox(obj,common),icon_system="";'
	. 'if(obj.system_flag==true){icon_system="<img src=\""+CFG.gear+"\" class=\"col_img\" style=\"float:right;margin:0px 4px 8px 4px;\">";}'
	. 'if(obj.published_flag==false){icon_system+="<img src=\""+CFG.lock+"\" class=\"col_img\" style=\"float:right;margin:0px 4px 8px 4px;\">";value_text="<span style=\"color:#AAA\">"+obj.value+"</span>";}'
	. 'if(obj.main_flag==1){icon_system+="<img src=\""+CFG.star+"\" class=\"col_img\" style=\"float:right;margin:0px 4px 8px 4px;\">";value_text="<span style=\"font-weight:bold\">"+obj.value+"</span>";}'
	. 'return common.icon(obj,common)+checkbox+icon+folder+icon_system+value_text;'
	. '},'
	. 'editable:false,container:"container_A",view:"tree",select:true,drag:false'
	. '});'
	. 'webix.event(window,"resize",function(){content_tree.adjust();});'
	. 'content_tree.parse(' . $content_tree_dump_JSON . ');'
	. '})();</script>';

if (!function_exists('epc_cp_footer_scripts_append')) {
	$relocate = $_SERVER['DOCUMENT_ROOT'].'/content/general_pages/epc_cp_script_relocate.php';
	if (is_file($relocate)) {
		require_once $relocate;
	}
}
if (function_exists('epc_cp_footer_scripts_append')) {
	epc_cp_footer_scripts_append($js);
} else {
	echo $js;
}
?>
