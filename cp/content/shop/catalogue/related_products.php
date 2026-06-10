<?php
/**
Страничный скрипт для редактирования сопутствующих товаров
*/
defined('_ASTEXE_') or die('No access');
?>

<div class="col-lg-12">
	<div class="hpanel">
		<div class="panel-heading hbuilt">
			<?php echo translate_str_by_id(2307); ?>
		</div>
		<div class="panel-body">
			
			<?php echo translate_str_by_id(2996); ?><br/>
			<?php echo translate_str_by_id(2997); ?><br/>
			<?php echo translate_str_by_id(2998); ?>
			
		</div>
	</div>
</div>

<?php
require_once($_SERVER["DOCUMENT_ROOT"].'/'.$DP_Config->backend_dir."/content/shop/catalogue/products.php");
?>