<?php
/**
 * Скрипт плагина для применения планшетных и мобильных шаблонов
*/
defined('_ASTEXE_') or die('No access');


require_once($_SERVER["DOCUMENT_ROOT"]."/lib/MobileDetect/Mobile_Detect.php");//Библиотека Mobile_Detect



$Mobile_Detect = new Mobile_Detect;

//Устройство является мобильным и ID текущего шаблона не равно 0 (т.к. если $DP_Template->id == 0, то это скорее всего идет вход в бэкэнд, пользователь не авторизован и выводится форма универсальная входа)
if($Mobile_Detect->isMobile() && $DP_Template->id != 0) 
{
    if(!empty($_GET["desktop_only"]))//Пользователь передал параметр "Перейти на настольную версию"
    {
        //Устновка cookie desktop_only
        $cookietime = time()+9999999;//Время cookie - на долго
        setcookie("desktop_only", "true", $cookietime, "/");
        header("Location: ".getPageUrl());//Перезагрузка страницы
    }
    else if(!empty($_COOKIE["desktop_only"]))//Cookie есть - настольная версия активна
    {
        if(!empty($_GET["desktop_off"]))//Пользователь передал параметр "Вернуться к мобильной версии"
        {
            //Удаление cookie desktop_only
            $cookietime = time()-3600;//Время в прошлом
            setcookie("desktop_only", "", $cookietime, "/");
            header("Location: ".getPageUrl());//Перезагрузка страницы
        }
    }
    else//Нет ни cookie, ни параметра - значит должно быть применение мобильной или планшетной версии
    {
        if($Mobile_Detect->isTablet())//Зашли с планшета
        {
            if($DP_Template->tablet_support)//Шаблон поддерживате планшеты
            {
                //HTML-КОД ШАБЛОНА
                //Путь с файлам шаблона
                $tpl_file_path = "templates/".$DP_Template->name."/tablet.php";
                
                $tpl_file = fopen($tpl_file_path, "r");
                $tpl_file_string = fread($tpl_file, filesize($tpl_file_path));//Строка с html/php кодом страницы шаблона
                fclose($tpl_file);
                
                $DP_Template->html = $tpl_file_string;//Присваиваем содержимое шаблона в HTML-коду страницы
            }
            else if($DP_Template->phone_support)//Не поддерживает планшеты, но поддерживает сартфоны
            {
                //HTML-КОД ШАБЛОНА
                //Путь с файлам шаблона
                $tpl_file_path = "templates/".$DP_Template->name."/phone.php";
                
                $tpl_file = fopen($tpl_file_path, "r");
                $tpl_file_string = fread($tpl_file, filesize($tpl_file_path));//Строка с html/php кодом страницы шаблона
                fclose($tpl_file);
                
                $DP_Template->html = $tpl_file_string;//Присваиваем содержимое шаблона в HTML-коду страницы
            }
        }
        else//Значит зашли со смартфона
        {
            if($DP_Template->phone_support)//Поддерживает сартфоны
            {
                //HTML-КОД ШАБЛОНА
                //Путь с файлам шаблона
                $tpl_file_path = "templates/".$DP_Template->name."/phone.php";
                
                $tpl_file = fopen($tpl_file_path, "r");
                $tpl_file_string = fread($tpl_file, filesize($tpl_file_path));//Строка с html/php кодом страницы шаблона
                fclose($tpl_file);
                
                $DP_Template->html = $tpl_file_string;//Присваиваем содержимое шаблона в HTML-коду страницы
            }
        }
    }
}
?>