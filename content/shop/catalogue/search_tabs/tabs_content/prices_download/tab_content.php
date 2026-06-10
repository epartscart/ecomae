<?php
//Скрипт для вывода содержимого таба "VIN-запрос"
defined('_ASTEXE_') or die('No access');
?>
<!--<div class="search_tab_clar">Скачивание прайс-листа в формате Excel</div>-->
<div class="input-group">
	<div class="search_tab_clar">
		<?php
		//Получаем группу пользователя
		//ДЛЯ РАБОТЫ С ПОЛЬЗОВАТЕЛЕМ
		require_once($_SERVER["DOCUMENT_ROOT"]."/content/users/dp_user.php");
		$userProfile = DP_User::getUserProfile();
		$group_id = $userProfile["groups"][0];//Берем первую группу пользователя
		
		
		//Проверяем наличие файла для соответствующей группы
		if( file_exists($_SERVER["DOCUMENT_ROOT"]."/content/files/Documents/prices_tmp/prices_".$group_id.".csv") )
		{
			?>
			<div><?php echo translate_str_by_id(4185); ?></div>
			<div>
				<?php echo translate_str_by_id(3763); ?>: <?php echo date ("d.m.Y в H:i:s", filemtime($_SERVER["DOCUMENT_ROOT"]."/content/files/Documents/prices_tmp/prices_".$group_id.".csv")); ?>
			</div>
			<div><a style="color:#FFF;" href="<?php echo "/content/files/Documents/prices_tmp/prices_".$group_id.".csv?v=".time(); ?>"><i class="fa fa-sm fa-download"></i> <?php echo translate_str_by_id(4186); ?></a></div>
			<?php
		}
		else
		{
			?>
			<p><?php echo translate_str_by_id(4187); ?></p>
			<?php
		}
		
		
		//Выводим ссылку на скачивание
		?>
	</div>
</div>