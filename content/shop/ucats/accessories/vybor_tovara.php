<?php
/*
Скрипт для вывода каталога автохимии
*/
defined('_ASTEXE_') or die('No access');

//Входные данные
$car_name = htmlentities($_GET["car_name"], ENT_QUOTES, "UTF-8");
$model = htmlentities($_GET["model"], ENT_QUOTES, "UTF-8");
$year = htmlentities($_GET["year"], ENT_QUOTES, "UTF-8");
$img = htmlentities($_GET["img"], ENT_QUOTES, "UTF-8");
?>

<table class="table">
	<tr>
		<td>
			<div align="left" style="padding:5px;"><b><?php echo translate_str_by_id(2085); ?>:</b> <a href="<?php echo $multilang_params['lang_href']; ?>/shop/katalogi-ucats/avtoaksessuary/vybor-modeli?car_name=<?php echo $car_name; ?>" class="bread_crumbs_a"><?php echo ucwords($car_name); ?></a></div>
			<div align="left" style="padding:5px;"><b><?php echo translate_str_by_id(2086); ?>:</b> <?php echo $model." ".$year; ?></div>
		</td>
		<td>
			
		</td>
	</tr>
</table>




<div id="products_block" style="width:100%;text-align:center;margin-top:25px;padding-top:10px;background-color:#FFF;border-radius:8px;">
	<img src="/content/files/images/ajax-loader-transparent.gif" />
</div>
<script>
groupChanged();//После загрузки страницы - обрабатываем выбранную группу товаров

var timer_id_m = setInterval(ucats_remove_btn_js, 500);
function ucats_remove_btn_js(){
	if($(".ucats_product_block .product_page_button .bread_crumbs_a")){
		if(!$('.ucats_product_block .product_page_button .bread_crumbs_a').hasClass('btn')){
			$(".ucats_product_block .product_page_button .bread_crumbs_a").addClass('btn btn-ar btn-primary');
			$(".ucats_product_block .product_page_button .bread_crumbs_a").attr('target', '_blank');
		}
	}
}
</script>