<?php
error_reporting(0); // Set E_ALL for debuging

//Конфигурация CMS
require_once($_SERVER["DOCUMENT_ROOT"]."/config.php");
$DP_Config = new DP_Config;


//Подключение к БД
try
{
	$db_link = new PDO('mysql:host='.$DP_Config->host.';dbname='.$DP_Config->db, $DP_Config->user, $DP_Config->password);
}
catch (PDOException $e) 
{
	exit( json_encode( array('error'=>'No DB Connect') ) );
}
$db_link->query("SET NAMES utf8;");



//1. ПРОВЕРКА ПРАВ НА ЗАПУСК СКРИПТА
$user_id = 0;
$check_authentication_query = $db_link->prepare("SELECT COUNT(*) FROM `sessions` WHERE `session`=? AND `type`=1;");
$check_authentication_query->execute( array($_COOKIE["admin_session"]) );
$check_authentication_query = $check_authentication_query->fetchColumn();
if( $check_authentication_query == 0)
{
	exit( json_encode( array('error'=>'Forbidden') ) );
}
else if( $check_authentication_query != 1)
{
	exit( json_encode( array('error'=>'Duplicate of session') ) );
}


//2. ПРОВЕРКА ПРИВЕЛЕГИЙ ПОЛЬЗОВАТЕЛЯ
//Подключаем класс пользователя
require_once( $_SERVER['DOCUMENT_ROOT']."/content/users/dp_user.php" );
//ПРОФИЛЬ АДМИНИСТРАТОРА:
$user_profile = DP_User::getAdminProfile();
/*Проверка привелегий пользователя. Правило такое: пользователь (группа пользователя) должен иметь доступ ко ВСЕМ страницам, которые работают с файлами:
- файловый менеджер
- слайдер
- редактирование шаблонов
Следует понимать, что НЕЛЬЗЯ давать доступ недоверенным лицам ни к одной из этих страниц, например, продавцу.
При этом доступ должен быть указан непосредственно для группы пользователя, т.е. правило более строгое, чем просто для открытия страниц - должна быть установлена галка конкренто для его группы.
*/
//Массив страниц, доступ к которым нужно проверить
$pages_with_elFinder = array();
$pages_with_elFinder[] = array('id'=>223, 'url'=>'filemanager');//Файловый менеджер
$pages_with_elFinder[] = array('id'=>244, 'url'=>'template');//Шаблон
$pages_with_elFinder[] = array('id'=>381, 'url'=>'slider');//Слайдер
//Цикл - проверяем доступ. Если доступ есть хотя бы к одной из страниц - разрешаем работать
for( $i=0 ; $i < count($pages_with_elFinder) ; $i++)
{
	//СПИСОК ДОПУЩЕННЫХ ГРУПП К ЭТОЙ СТРАНИЦЕ
	$allowed_groups = array();//Список допущенных групп
	$content_access_query = $db_link->prepare("SELECT * FROM `content_access` WHERE `content_id` = ?;");
	$content_access_query->execute( array( $pages_with_elFinder[$i]['id'] ) );
	//Получаем список ЯВНО-допущенных
	while( $content_access_record = $content_access_query->fetch() )
	{
		$allowed_groups[] = (int)$content_access_record["group_id"];
	}
	
	
	//ПРОВЕРКА ДОСТУПА К МАТЕРИАЛУ
	$access_allowed = false;//Флаг "Доступ разрешен"
	//Доступ разрешен, если хотя бы одна из групп пользователя имеет доступ.
	//По всем группам пользователя:
	for($g=0; $g < count($user_profile["groups"]); $g++)
	{
		if(array_search($user_profile["groups"][$g], $allowed_groups) !== false)
		{
			$access_allowed = true;
			break;
		}
	}
	//Доступа к материалу у пользователя нет
	if(!$access_allowed)
	{
		exit( json_encode( array('error'=>'Forbidden') ) );
	}
}




//Защита от CSRF-атак
require_once($_SERVER["DOCUMENT_ROOT"]."/content/users/stop_csrf.php");





include_once dirname(__FILE__).DIRECTORY_SEPARATOR.'elFinderConnector.class.php';
include_once dirname(__FILE__).DIRECTORY_SEPARATOR.'elFinder.class.php';
include_once dirname(__FILE__).DIRECTORY_SEPARATOR.'elFinderVolumeDriver.class.php';
include_once dirname(__FILE__).DIRECTORY_SEPARATOR.'elFinderVolumeLocalFileSystem.class.php';
// Required for MySQL storage connector
// include_once dirname(__FILE__).DIRECTORY_SEPARATOR.'elFinderVolumeMySQL.class.php';
// Required for FTP connector support
// include_once dirname(__FILE__).DIRECTORY_SEPARATOR.'elFinderVolumeFTP.class.php';


/**
 * Simple function to demonstrate how to control file access using "accessControl" callback.
 * This method will disable accessing files/folders starting from  '.' (dot)
 *
 * @param  string  $attr  attribute name (read|write|locked|hidden)
 * @param  string  $path  file path relative to volume root directory started with directory separator
 * @return bool|null
 **/
function access($attr, $path, $data, $volume) {
	return strpos(basename($path), '.') === 0       // if file/folder begins with '.' (dot)
		? !($attr == 'read' || $attr == 'write')    // set read+write to false, other (locked+hidden) set to true
		:  null;                                    // else elFinder decide it itself
}

$opts = array(
	// 'debug' => true,
	'roots' => array(
		array(
			'driver'        => 'LocalFileSystem',   // driver for accessing file system (REQUIRED)
			'path'          => '../../../../content/files/',         // path to files (REQUIRED)
			'URL'           => '/../../../../content/files/', // URL to files (REQUIRED)
			'accessControl' => 'access',             // disable and hide dot starting files (OPTIONAL)
			
			//Ограничения на загрузку файлов и переименование. При необходимости, под конкретного клиента, можно снимать эти ограничения по его запросу.
			'uploadOrder'	=> array('allow', 'deny'),
			'uploadAllow' 	=> array('image/jpeg', 'image/png', 'image/gif', 'application/pdf'),
			'disabled' 		=> array('archive', 'duplicate', 'editor', 'extract', 'file', 'get', 'mkfile', 'rename')
		)
	)
);

// run elFinder
$connector = new elFinderConnector(new elFinder($opts));
$connector->run();

