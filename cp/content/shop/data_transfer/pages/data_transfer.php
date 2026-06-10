<?php
/**
Страничный скрипт для раздела "Перенос данных"
*/
defined('_ASTEXE_') or die('No access');

//Для работы с пользователем
require_once( $_SERVER['DOCUMENT_ROOT']."/content/users/dp_user.php" );
$user_session = DP_User::getAdminSession();
?>



<div class="col-lg-12">
	<div class="hpanel">
		<div class="panel-heading hbuilt">
			<?php echo translate_str_by_id(3161); ?>
		</div>
		<div class="panel-body">
			<a class="panel_a" href="/<?php echo $DP_Config->backend_dir; ?>/shop/perenos-dannyx/eksport-kataloga-tovarov-v-xml-i-json">
				<div class="panel_a_img" style="background: url('<?php echo "/".$DP_Config->backend_dir."/templates/".$DP_Template->name."/images/"; ?>catalogue_export.png') 0 0 no-repeat;"></div>
				<div class="panel_a_caption"><?php echo translate_str_by_id(3229); ?></div>
			</a>
			
			<!--
			<a class="panel_a" href="/<?php echo $DP_Config->backend_dir; ?>/shop/perenos-dannyx/vygruzka-na-yandeksmarket">
				<div class="panel_a_img" style="background: url('<?php echo "/".$DP_Config->backend_dir."/templates/".$DP_Template->name."/images/"; ?>yml.png') 0 0 no-repeat;"></div>
				<div class="panel_a_caption"><?php echo translate_str_by_id(3230); ?></div>
			</a>
			-->
			
			<a class="panel_a" href="/<?php echo $DP_Config->backend_dir; ?>/shop/perenos-dannyx/import-kataloga-tovarov-iz-xml-i-json">
				<div class="panel_a_img" style="background: url('<?php echo "/".$DP_Config->backend_dir."/templates/".$DP_Template->name."/images/"; ?>catalogue_import.png') 0 0 no-repeat;"></div>
				<div class="panel_a_caption"><?php echo translate_str_by_id(3231); ?></div>
			</a>
			
			
			<a class="panel_a" href="/<?php echo $DP_Config->backend_dir; ?>/shop/perenos-dannyx/import-tovarov-v-katalog-iz-csv">
				<div class="panel_a_img" style="background: url('<?php echo "/".$DP_Config->backend_dir."/templates/".$DP_Template->name."/images/"; ?>catalogue_import_csv.png') 0 0 no-repeat;"></div>
				<div class="panel_a_caption"><?php echo translate_str_by_id(3232); ?></div>
			</a>
			
			
			<a class="panel_a" href="/<?php echo $DP_Config->backend_dir?>">
				<div class="panel_a_img" style="background: url('/<?php echo $DP_Config->backend_dir; ?>/templates/<?php echo $DP_Template->name; ?>/images/power_off.png') 0 0 no-repeat;"></div>
				<div class="panel_a_caption"><?php echo translate_str_by_id(2116); ?></div>
			</a>
		</div>
	</div>
</div>