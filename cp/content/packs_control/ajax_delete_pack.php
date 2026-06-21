<?php
/**
 * Скрипт удаления пакета
*/
header('Content-Type: application/json;charset=utf-8;');
require_once($_SERVER["DOCUMENT_ROOT"]."/config.php");

//ОБЪЕКТ РЕЗУЛЬТАТА
$ResultMessage = new ResultMessage;


//0. ПОДКЛЮЧЕНИЕ К БД
$DP_Config = new DP_Config;//Конфигурация CMS
//Подключение к БД
try
{
	$db_link = new PDO('mysql:host='.$DP_Config->host.';dbname='.$DP_Config->db, $DP_Config->user, $DP_Config->password);
}
catch (PDOException $e) 
{
    $ResultMessage->result_code = 1;
    $ResultMessage->message = "No DB connect";
	exit(json_encode($ResultMessage));
}
$db_link->query("SET NAMES utf8;");


// -------------------------------------------------------------------------------
//Подключение мультиязычности
require_once($_SERVER["DOCUMENT_ROOT"]."/lang/dp_lang.php");
$multilang_params = multilang_init();
// -------------------------------------------------------------------------------


// -------------------------------------------------------------------------------
//Проверка привелегий (пользователь должен иметь доступ к следующим страницам)
$pages_to_check = array();
$pages_to_check[] = array('id'=>241, 'url'=>'packs');//Корневой раздел "Пакеты"
$pages_to_check[] = array('id'=>242, 'url'=>'packs/packs_manager');//Менеджер пакетов
$pages_to_check[] = array('id'=>247, 'url'=>'packs/setup');//Установить пакет
$pages_to_check[] = array('id'=>248, 'url'=>'packs/pack_control');//Управление пакетом
require_once($_SERVER["DOCUMENT_ROOT"]."/".$DP_Config->backend_dir."/content/control/check_admin_access/check_admin_access.php");
// -------------------------------------------------------------------------------


// -------------------------------------------------------------------------------
//Защита от CSRF-атак
require_once($_SERVER["DOCUMENT_ROOT"]."/content/users/stop_csrf.php");
// -------------------------------------------------------------------------------


// -------------------------------------------------------------------------------------------


//1. ПРОВЕРКА ПРАВ НА ЗАПУСК СКРИПТА
$user_id = 0;
$check_authentication_query = $db_link->prepare("SELECT COUNT(*) FROM `sessions` WHERE `session`= ? AND `type` = ?;");
$check_authentication_query->execute( array($_COOKIE["admin_session"], 1) );
if($check_authentication_query->fetchColumn() == 0)
{
    exit("No access");
}
else if($check_authentication_query->fetchColumn() != 1)
{
    exit("Session duplication");
}


// - проверка пройдена




//2. ПОЛУЧАЕМ ОБЪЕКТ ОПИСАНИЯ ПАКЕТА
$pack_query = $db_link->prepare("SELECT * FROM `packs` WHERE `id` = ?;");
$pack_query->execute( array($_POST["pack_id"]) );
$pack_record = $pack_query->fetch();
$pack_json_ob = json_decode($pack_record["pack_json"], true);



//2.1 ПРОВЕРЯЕМ ДОПУСТИМОСТЬ УДАЛЕНИЯ ПАКЕТА
//2.1.1 ПРОВЕРЯЕМ ШАБЛОНЫ (НЕЛЬЗЯ УДАЛЯТЬ ТЕКУЩИЕ ШАБЛОНЫ)
$templates = $pack_json_ob["templates"];
for($i=0; $i < count($templates); $i++)
{
	$template_query = $db_link->prepare("SELECT * FROM `templates` WHERE `id` = ?;");
	$template_query->execute( array($templates[$i]["id"]) );
	$template_record = $template_query->fetch();
    if( $template_record != fasle )//Шаблон не удален
    {
        if($template_record["current"] == true)
        {
            $ResultMessage->result_code = 211;
            $ResultMessage->message = translate_str_by_id(2651);
            exit(json_encode($ResultMessage));
        }
    }
}
//МОДУЛИ И ПЛАГИНЫ МОЖНО НЕ ПРОВЕРЯТЬ - ОНИ ПРОСТО УДАЛЯТСЯ



//3. УДАЛЯЕМ КОМПОНЕНТЫ
//3.1 УДАЛЯЕМ ШАБЛОНЫ
for($i=0; $i < count($templates); $i++)
{
	$current_template_query = $db_link->prepare("SELECT * FROM `templates` WHERE `id` = ?;");
	$current_template_query->execute( array($templates[$i]["id"]) );
    $current_template_record = $current_template_query->fetch();
    
    $backend_dir = "";//Если работаем с шаблоном бэкэнда
    if(! $current_template_record["is_frontend"])
    {
        $backend_dir = $DP_Config->backend_dir."/";//Если работаем с шаблоном бэкэнда
    }
    
    $path = $_SERVER['DOCUMENT_ROOT']."/".$backend_dir."templates/".$current_template_record["name"];
    
    if(file_exists($path))
    {
        removeNotEmptyDir($path);//Рекурсивно удаляем каталог шаблона
    }
    
    //Удаление учетной записи шаблона
    if( $db_link->prepare("DELETE FROM `templates` WHERE `id` = ?;")->execute( array($templates[$i]["id"]) ) != true)
    {
        $ResultMessage->result_code = 31;
        $ResultMessage->message = translate_str_by_id(2652);
        exit(json_encode($ResultMessage));
    }
}


//3.2 УДАЛЯЕМ ПЛАГИНЫ
$plugins = $pack_json_ob["plugins"];
for($i=0; $i < count($plugins); $i++)
{
    //Получаем список файлов и каталогов плагина:
	$current_plugin_query = $db_link->prepare("SELECT * FROM `plugins` WHERE `id` = ?;");
	$current_plugin_query->execute( array($plugins[$i]["id"]) );
    $current_plugin_record = $current_plugin_query->fetch();
    $dirs_files = json_decode($current_plugin_record["dirs_files"], true);
    //По всему списку файлов и каталогов данного плагина:
    for($j=0; $j < count($dirs_files); $j++)
    {
        //Подставляем имя каталога бэкэнда (если оно обозначено в строке):
        $path = $_SERVER['DOCUMENT_ROOT']."/".str_replace(array("<backend_dir>"), $DP_Config->backend_dir, $dirs_files[$j]["path"]);
        
        if($dirs_files[$j]["type"] == "dir")//Рекурсивно удаляем каталог
        {
            if(file_exists($path))
            {
                removeNotEmptyDir($path);
            }
        }
        else if($dirs_files[$j]["type"] == "file")//Удаляем файл
        {
            if(file_exists($path))
            {
                unlink($path);
            }
        }
    }//for($j) - по удаляемым файлам и каталогам
    

    if( $db_link->prepare("DELETE FROM `plugins` WHERE `id` = ?;")->execute( array($plugins[$i]["id"]) ) != true)
    {
        $ResultMessage->result_code = 32;
        $ResultMessage->message = translate_str_by_id(2653);
        exit(json_encode($ResultMessage));
    }
}

//3.3 УДАЛЯЕМ ПРОТОТИПЫ МОДУЛЕЙ
$modules_prototypes = $pack_json_ob["modules_prototypes"];
for($i=0; $i < count($modules_prototypes); $i++)
{
    //1. Удаляем учетные записи модулей с данным прототипом
	if( $db_link->prepare("DELETE FROM `modules` WHERE `prototype_id` = ?;")->execute( array($modules_prototypes[$i]["id"]) ) != true)
    {
        $ResultMessage->result_code = 331;
        $ResultMessage->message = translate_str_by_id(2654);
        exit(json_encode($ResultMessage));
    }
    
    //2. Удаляем учетную запись прототипа
    if( $db_link->prepare("DELETE FROM `modules` WHERE `id` = ?;")->execute( array($modules_prototypes[$i]["id"]) ) != true)
    {
        $ResultMessage->result_code = 332;
        $ResultMessage->message = translate_str_by_id(2655);
        exit(json_encode($ResultMessage));
    }
}


//3.4 УДАЛЯЕМ ФАЙЛЫ
$files = $pack_json_ob["files"];
for($i=0; $i < count($files); $i++)
{
    if(file_exists($_SERVER['DOCUMENT_ROOT'].$files[$i]["server_path"].$files[$i]["file_name"]))
    {
        unlink($_SERVER['DOCUMENT_ROOT'].$files[$i]["server_path"].$files[$i]["file_name"]);
    }
}


//4. СТАВИМ ФЛАГ "ПАКЕТ УДАЛЕН" (УЧЕТНАЯ ЗАПИСЬ ПАКЕТА ОСТАЕТСЯ В ТАБЛИЦЕ)
if( $db_link->prepare("UPDATE `packs` SET `removed` = 1 WHERE `id` = ?;")->execute( array($_POST["pack_id"]) ) != true)
{
    $ResultMessage->result_code = 4;
    $ResultMessage->message = translate_str_by_id(2656);
    exit(json_encode($ResultMessage));
}




//5. ПАКЕТ УДАЛЕН УСПЕШНО
$ResultMessage->result_code = 0;
$ResultMessage->message = translate_str_by_id(2657);
exit(json_encode($ResultMessage));
?>



<?php
//Класс ответа
class ResultMessage
{
    public $result_code;//Код результата (0 - все корректно)
    public $message;//Текстовое сообщение
}
?>



<?php
//Метод удаления не пустого каталога
function removeNotEmptyDir($dir)
{
    if(is_file($dir)) return unlink($dir);
    
    $dh=opendir($dir);
    while(false!==($file=readdir($dh)))
    {
            if($file=='.'||$file=='..') continue;
            removeNotEmptyDir($dir."/".$file);
    }
    closedir($dh);
    
    return rmdir($dir);
}
?>