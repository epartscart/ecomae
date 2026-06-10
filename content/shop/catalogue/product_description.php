<?php
// -------------------------------------------------------------------------------
//Подключение мультиязычности
require_once($_SERVER["DOCUMENT_ROOT"]."/lang/dp_lang.php");
$multilang_params = multilang_init();
// -------------------------------------------------------------------------------


$text_content_query = $db_link->prepare('SELECT `content` FROM `shop_products_text` WHERE `product_id` = ? LIMIT 1;');
$text_content_query->execute(array($product_id));
$text_content_record = $text_content_query->fetch();
if(!empty($text_content_record["content"])){
	echo translate_str_by_id($text_content_record["content"]);
}else{
	echo translate_str_by_id(4147);
}
?>