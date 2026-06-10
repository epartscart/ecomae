<?php
//Страничный скрипт - Главная страница раздела Языки
defined('_ASTEXE_') or die('No access');



if( isset( $_POST["action"] ) )
{
	
}
else//Действий нет - выводим страницу
{
	
	?>
	<div class="col-lg-12">
		<div class="hpanel">
			<div class="panel-heading hbuilt">
				<?php echo translate_str_by_id(2113); ?>
			</div>
			<div class="panel-body">
				
				
				<?php
				//Настройки Языков
				print_backend_button( array("background_color"=>"#8e44ad", "fontawesome_class"=>"fas fa-tasks", "caption"=>translate_str_by_id(2539), "url"=>"/".$DP_Config->backend_dir."/lang/configurator") );
				?>
				
				
				<?php
				//Редактор переводов строк
				print_backend_button( array("background_color"=>"#00b05a", "fontawesome_class"=>"fas fa-pencil-alt", "caption"=>translate_str_by_id(2528), "url"=>"/".$DP_Config->backend_dir."/lang/editor") );
				?>
				
				
				
				<a class="panel_a" href="/<?php echo $DP_Config->backend_dir?>">
					<div class="panel_a_img" style="background: url('/<?php echo $DP_Config->backend_dir; ?>/templates/<?php echo $DP_Template->name; ?>/images/power_off.png') 0 0 no-repeat;"></div>
					<div class="panel_a_caption"><?php echo translate_str_by_id(2116); ?></div>
				</a>
			</div>
		</div>
	</div>
	<?php
}
?>