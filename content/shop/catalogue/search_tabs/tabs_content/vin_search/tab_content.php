<?php
//Скрипт для вывода содержимого таба "VIN-запрос"
defined('_ASTEXE_') or die('No access');
?>
<div class="search_tab_clar"><?php echo translate_str_by_id(4189); ?></div>
<div class="input-group">
	<div class="search_tab_clar"><?php echo translate_str_by_id(4190); ?></div>
</div>

<?php
// Laximo VIN Search - redirects to /katalog-laximo with VIN results
?>
<div style="margin-top:15px; padding:15px; background:#e8f4fd; border:1px solid #bee5eb; border-radius:4px;">
	<label style="font-weight:600; margin-bottom:8px; display:block; font-size:13px; color:#0c5460;">Search by VIN / Frame Number</label>
	<form role="form" action="/katalog-laximo" method="GET" style="display:flex; max-width:450px;">
		<input value="vehicles" type="hidden" name="task" />
		<input value="" type="hidden" name="c" />
		<input value="FindVehicle" type="hidden" name="ft" />
		<input value="<?php echo isset($_GET['vin']) ? htmlspecialchars($_GET['vin']) : ''; ?>" type="text" class="form-control" placeholder="Enter VIN or Frame number" name="identString" style="flex:1; border:1px solid #ddd; padding:8px 12px; border-radius:4px 0 0 4px;" />
		<input value="" type="hidden" name="ssd" />
		<button class="btn btn-ar btn-default" type="submit" style="background:#337ab7; color:#fff; border:1px solid #337ab7; padding:8px 16px; border-radius:0 4px 4px 0; cursor:pointer;">Search</button>
	</form>
</div>
