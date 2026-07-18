<?php
//Подключаемый скрипт для проверки доступа пользователя к определенным страницам. Подключается к AJAX-скриптам, в которых нужно проверять право на запуск скрипта в зависимости от наличия доступа к каким-то определенным страницам. Обычно принцип такой: если пользователь имеет доступ к страницам, на которых происходит работа с этими AJAX-скриптами, то, выполнять их можно. Если доступа у пользователя к таким страницам нет, то, exit.
//Правило такое: пользователь (группа пользователя) должен иметь доступ ко ВСЕМ страницам, указанным в массиве. Следует понимать, что НЕЛЬЗЯ давать доступ недоверенным пользователям ни к одной из этих страниц.
//Скрипт универсальный. Его можно использовать и в ПУ и в клиентской части.
//Перед подключением скрипта нужно определить перечень страниц, доступ к которым нужно проверить в $pages_to_check

/*
Пример:
-----
//Проверка привелегий (пользователь должен иметь доступ к следующим страницам)
$pages_to_check = array();
$pages_to_check[] = array('url'=>'packs', 'is_frontend' => 1);
$pages_to_check[] = array('url'=>'packs/packs_manager', 'is_frontend' => 1);
require_once($_SERVER["DOCUMENT_ROOT"]."/content/users/check_user_access.php");
-----
*/


// -------------------------------------------------------------------------------
//Подключение мультиязычности
require_once($_SERVER["DOCUMENT_ROOT"]."/lang/dp_lang.php");
$multilang_params = multilang_init();
// -------------------------------------------------------------------------------


//Должны быть указаны страницы для проверки
if( !isset($pages_to_check) || !is_array($pages_to_check) || count($pages_to_check)==0 )
{
	$answer = array();
	$answer['status'] = false;
	$answer['error'] = translate_str_by_id(2387);
	$answer['message'] = translate_str_by_id(2387);
	
	exit( json_encode( $answer ) );
}



//Подключаем класс пользователя
require_once( $_SERVER['DOCUMENT_ROOT']."/content/users/dp_user.php" );

//ПРОФИЛЬ АДМИНИСТРАТОРА:
$admin_profile = DP_User::getAdminProfile();

//ПРОВИЛЬ ОБЫЧНОГО ПОЛЬЗОВАТЕЛЯ:
$user_profile = DP_User::getUserProfile();




//Цикл - проверяем доступ. Если доступа нет хотя бы к одной из страниц - запрещаем работать
for( $i=0 ; $i < count($pages_to_check) ; $i++)
{
	//СПИСОК ДОПУЩЕННЫХ ГРУПП К ЭТОЙ СТРАНИЦЕ
	$allowed_groups = array();//Список допущенных групп
	$content_access_query = $db_link->prepare("SELECT * FROM `content_access` WHERE `content_id` = (SELECT `id` FROM `content` WHERE `url` = ? AND `is_frontend` = ?);");
	$content_access_query->execute( array( $pages_to_check[$i]['url'], $pages_to_check[$i]['is_frontend'] ) );
	//Получаем список ЯВНО-допущенных
	while( $content_access_record = $content_access_query->fetch() )
	{
		$allowed_groups[] = (int)$content_access_record["group_id"];
	}
	
	
	//Далее дополняем этот список вложенными группами, которые также имеют доступ
	$inserted_groups = array();//Массив для вложенных групп
	for( $g=0 ; $g < count($allowed_groups) ; $g++ )
	{
		//Рекурсивная функция наполнения массива inserted_groups (заполнятся группы, вложенные в $allowed_groups[$g])
		getInsertedGroups($allowed_groups[$g]);
	}
	$allowed_groups = array_merge($allowed_groups, $inserted_groups);//Объединяем
	
	
	//ПРОВЕРКА ДОСТУПА К МАТЕРИАЛУ
	$access_allowed = false;//Флаг "Доступ разрешен"
	$isFrontendPage = ((int) $pages_to_check[$i]['is_frontend'] === 1);

	// Empty ACL: frontend may stay open (public catalogue pages).
	// Backend (CP) empty ACL MUST deny — misconfigured pages must not run AJAX as guest.
	if (count($allowed_groups) === 0 && $isFrontendPage) {
		$access_allowed = true;
	}

	//В зависимости от страницы (фронтенд/бэкенд), будем проверять по соответствующему профилю
	$profile_to_check = $user_profile;//Обычный пользовательский профиль
	//Если материал относится к ПУ - проверяем по профилю админа
	if( $pages_to_check[$i]['is_frontend'] == 0 )
	{
		$profile_to_check = $admin_profile;//Профиль админа
	}
	//Если профиль равен false, значит материал относится к бэкенду, а пользователь не авторизован как админ - однозначно, доступа не имеет. Если профиль есть - можем проверять.
	if (!$access_allowed && $profile_to_check) {
		for ($g = 0; $g < count($profile_to_check["groups"]); $g++) {
			if (array_search($profile_to_check["groups"][$g], $allowed_groups) !== false) {
				$access_allowed = true;
				break;
			}
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



// ---------------------------------------------------------------------------------------
//Рекурсивная функция получения вложенных групп
function getInsertedGroups($group)
{
    global $db_link;
    global $DP_Config;
    global $inserted_groups;
    global $allowed_groups;
    
	
	$stmt = $db_link->prepare('SELECT * FROM `groups` WHERE `id` = ?;');
	$stmt->execute( array($group) );
	$group_record = $stmt->fetch();
    if($group_record["count"] == 0)
    {
        return;
    }
    else
    {
		$stmt = $db_link->prepare('SELECT * FROM `groups` WHERE `parent` = ?;');
		$stmt->execute( array($group) );
		while( $inserted_group_record = $stmt->fetch() )
		{
			//Если эта группа уже есть в списке вложенных или в списке явных - пропускаем, чтобы не дублировать
            if( array_search($inserted_group_record["id"], $inserted_groups) !== false || array_search($inserted_group_record["id"], $allowed_groups) !== false )
            {
                continue;
            }
			$inserted_groups[] = $inserted_group_record["id"];
            getInsertedGroups($inserted_group_record["id"]);//Рекурсивный вызов для этой группы
		}
    }
}
// ---------------------------------------------------------------------------------------
?>