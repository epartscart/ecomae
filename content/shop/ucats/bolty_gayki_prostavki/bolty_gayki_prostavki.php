<?php
/*
Скрипт для вывода каталога автохимии
*/
defined('_ASTEXE_') or die('No access');
?>
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