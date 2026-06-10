<?php
/**
 * Скрипт для страницы файлового менеджера
*/
defined('_ASTEXE_') or die('No access');
?>


<div class="col-lg-12">
	<div class="hpanel">
		<div class="panel-heading hbuilt">
			<?php echo translate_str_by_id(2113); ?>
		</div>
		<div class="panel-body">
			<a class="panel_a" href="/<?php echo $DP_Config->backend_dir; ?>">
				<div class="panel_a_img" style="background: url('/<?php echo $DP_Config->backend_dir; ?>/templates/<?php echo $DP_Template->name; ?>/images/power_off.png') 0 0 no-repeat;"></div>
				<div class="panel_a_caption"><?php echo translate_str_by_id(2116); ?></div>
			</a>
		</div>
	</div>
</div>

<?php
require_once( $_SERVER['DOCUMENT_ROOT']."/content/users/dp_user.php" );
$user_session = DP_User::getAdminSession();
?>


<!-- elFinder initialization (REQUIRED) -->
<script type="text/javascript" charset="utf-8">
	jQuery().ready(function() {
		var elf = $('#elfinder').elfinder({
			url : '/<?php echo $DP_Config->backend_dir; ?>/lib/elfinder/php/connector.php',  // connector URL (REQUIRED)
			lang: '<?php echo $multilang_params['lang']; ?>',             // language (OPTIONAL)
			customData: { 
				csrf_guard_key: '<?php echo $user_session["csrf_guard_key"]; ?>'
			},
		}).elfinder('instance');
	});
</script>

<!-- Element where elFinder will be created (REQUIRED) -->


<div class="col-lg-12" style="margin-bottom:20px;">
	<div id="elfinder">
	</div>
</div>

