<?php
//Здесь фрагменты кода по мультиязычности, которые могут использоваться в разных частях каталога
/*
//Подключаем строки с подзапросами, с учетом мультиязычности
require_once( $_SERVER['DOCUMENT_ROOT']."/content/shop/catalogue/cat_lang_general.php" );
*/




$manufacturer_lang = '(SELECT `str_key` FROM `lang_text_strings_translation` WHERE `value` IN ("Производитель", "Manufacturer") )';

$article_lang = '(SELECT `str_key` FROM `lang_text_strings_translation` WHERE `value` IN ("Артикул", "Article") )';
?>