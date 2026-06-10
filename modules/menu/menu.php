<?php
/**
 * Скрипт модуля меню
*/
defined('_ASTEXE_') or die('No access');

require_once($_SERVER["DOCUMENT_ROOT"]."/modules/menu/menu_required.php");


$module_id = (integer)"<module_id>";//Получаем ID модуля из ядра

//Необходимо получить из своих специальных настроек ID выводимого меню

//Получаем специальные настройки модуля меню
$stmt = $db_link->prepare('SELECT * FROM `modules` WHERE `id` = :id;');
$stmt->bindValue(':id', $module_id);
$stmt->execute();
$module_record = $stmt->fetch(PDO::FETCH_ASSOC);
$module_data = json_decode($module_record["data"], true);//Свои спецнастройки

//Из спецнастроек получаем ID меню
$menu_id = $module_data["menu_id"];

//Теперь получаем объект описания меню:
$stmt = $db_link->prepare('SELECT * FROM `menu` WHERE `id` = :id;');
$stmt->bindValue(':id', $menu_id);
$stmt->execute();
$menu_record = $stmt->fetch(PDO::FETCH_ASSOC);//Запись меню

//Удостоверяемся, что меню не было удалено
if($menu_record != false)
{
    $menu_structure = json_decode($menu_record["structure"], true);//Получаем описание меню
    
    $class_ul = "";
    if($menu_record["menu_ul_class"] != "")
    {
        $class_ul = " class=\"".$menu_record["menu_ul_class"]."\"";
    }
    
    $id_ul = "";
    if($menu_record["menu_ul_id"] != "")
    {
        $id_ul = " id=\"".$menu_record["menu_ul_id"]."\"";
    }
    
    $menu_html = "<ul".$class_ul.$id_ul.">";//HTML-код меню
    $menu_html .= getHtmlOfMenu($menu_structure);//Запускаем рекурсивный метод получения html-кода меню
    $menu_html .= "</ul>";
    
    $menu_html = str_replace(array("&quot;"), "\"", $menu_html);
    
    echo $menu_html;//Вывод HTML-кода меню
}//~if(mysqli_num_rows($menu_query) == 1)
?>