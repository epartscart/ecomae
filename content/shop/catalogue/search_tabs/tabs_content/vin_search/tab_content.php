<?php
//Скрипт для вывода содержимого таба "VIN-запрос"
defined('_ASTEXE_') or die('No access');
?>
<div class="search_tab_clar"><?php echo translate_str_by_id(4189); ?></div>
<div class="input-group">
	<div class="search_tab_clar"><?php echo translate_str_by_id(4190); ?></div>
</div>

<?php


/*
 //Поиск по Vin для каталога Laximo
 <form role="form" action="/katalog-laximo" method="GET">
	<div class="input-group">
		<input value="vehicles" type="hidden" name="task" />
		<input value="" type="hidden" name="c" />
		<input value="FindVehicle" type="hidden" name="ft" />
		<input value="<?php echo $_GET['vin']; ?>" type="text" class="form-control" placeholder="Поиск по VIN-коду" name="identString" />
		<input value="" type="hidden" name="ssd" />
		<span class="input-group-btn">
			<button class="btn btn-ar btn-default" type="submit">Поиск</button>
		</span>
	</div>
</form>
*/

?>