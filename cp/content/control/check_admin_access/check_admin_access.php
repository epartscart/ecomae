<?php
//Подключаемый скрипт для проверки доступа пользователя ПУ к определенным страницам. Скрипт подключается там, где идет загрузка файлов. Обычно принцип такой: если пользователь имеет доступ к страницам, на которых происходит работа с файлами, то, загружать файлы можно. Если доступа у пользователя к таким страницам нет (например, это простой продавец), то, exit.
//Правило такое: пользователь (группа пользователя) должен иметь доступ ко ВСЕМ страницам, указанным в массиве. Следует понимать, что НЕЛЬЗЯ давать доступ недоверенным лицам ни к одной из этих страниц, например, продавцу. При этом доступ должен быть указан непосредственно для группы пользователя, т.е. правило более строгое, чем просто для открытия страниц - должна быть установлена галка конкренто для его группы.
//Перед подключением скрипта нужно определить перечень страниц, доступ к которым нужно проверить в $pages_to_check

/*
Пример:
-----
//Проверка привелегий (пользователь должен иметь доступ к следующим страницам)
$pages_to_check = array();
$pages_to_check[] = array('id'=>241, 'url'=>'packs');//Корневой раздел "Пакеты"
$pages_to_check[] = array('id'=>242, 'url'=>'packs/packs_manager');//Менеджер пакетов
$pages_to_check[] = array('id'=>247, 'url'=>'packs/setup');//Установить пакет
$pages_to_check[] = array('id'=>248, 'url'=>'packs/pack_control');//Управление пакетом
require_once($_SERVER["DOCUMENT_ROOT"]."/".$DP_Config->backend_dir."/content/control/check_admin_access/check_admin_access.php");
-----
*/


// -------------------------------------------------------------------------------
//Подключение мультиязычности
require_once($_SERVER["DOCUMENT_ROOT"]."/lang/dp_lang.php");
$multilang_params = multilang_init();
// -------------------------------------------------------------------------------



//Подключаем класс пользователя
require_once( $_SERVER['DOCUMENT_ROOT']."/content/users/dp_user.php" );
//ПРОФИЛЬ АДМИНИСТРАТОРА:
$user_profile = DP_User::getAdminProfile();
//Должны быть указаны страницы для проверки
if( !isset($pages_to_check) || !is_array($pages_to_check) || count($pages_to_check)==0 )
{
	$answer = array();
	$answer['status'] = false;
	$answer['error'] = translate_str_by_id(2387);
	$answer['message'] = translate_str_by_id(2387);
	
	exit( json_encode( $answer ) );
}
//Цикл - проверяем доступ. Если доступа нет хотя бы к одной из страниц - запрещаем работать
for( $i=0 ; $i < count($pages_to_check) ; $i++)
{
	//СПИСОК ДОПУЩЕННЫХ ГРУПП К ЭТОЙ СТРАНИЦЕ
	$allowed_groups = array();//Список допущенных групп
	$content_access_query = $db_link->prepare("SELECT * FROM `content_access` WHERE `content_id` = ?;");
	$content_access_query->execute( array( $pages_to_check[$i]['id'] ) );
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
		$answer = array();
		$answer['status'] = false;
		$answer['error'] = translate_str_by_id(2388);
		$answer['message'] = translate_str_by_id(2388);
		
		exit( json_encode( $answer ) );
	}
}
?>