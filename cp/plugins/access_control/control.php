<?php
/**
 * Плагин контроля доступа к материалам и модулям бэкэнда
*/
defined('_ASTEXE_') or die('No access');
require_once($_SERVER["DOCUMENT_ROOT"]."/content/users/dp_user.php");
//ПРОФИЛЬ АДМИНИСТРАТОРА:
$user_profile = DP_User::getAdminProfile();
if ($user_profile === false) {
    $user_profile = array('groups' => array());
}

//Плагин выполняется только, если есть материал
if($DP_Content->id != 0)
{
    //Плагин выполняется только, если авторизован администратор. Иначе, плагин аутентификации бэкэнда все-равно не пустит
    if(DP_User::isAdmin())
    {
        //СПИСОК ДОПУЩЕННЫХ ГРУПП
        $allowed_groups = array();//Список допущенных групп
		$content_access_query = $db_link->prepare("SELECT * FROM `content_access` WHERE `content_id` = ?;");
		$content_access_query->execute( array($DP_Content->id) );
        //Получаем список ЯВНО-допущенных
        while( $content_access_record = $content_access_query->fetch() )
        {
			$allowed_groups[] = (int)$content_access_record["group_id"];
        }
        //Теперь получаем все их вложенные группы
        $inserted_groups = array();//Массив для вложенных групп
        for($i=0; $i < count($allowed_groups); $i++)
        {
            getAllowedGroups($allowed_groups[$i]);
        }
        $allowed_groups = array_merge($allowed_groups, $inserted_groups);//Объединяем
        
        
        
        //ПРОВЕРКА ДОСТУПА К МАТЕРИАЛУ
        $access_allowed = false;//Флаг "Доступ разрешен"
        // Нет записей в content_access — ограничений нет, любой авторизованный админ CP
        if (count($allowed_groups) === 0) {
            $access_allowed = true;
        } else {
            //Доступ разрешен, если хотя бы одна из групп пользователя имеет доступ
            for ($i = 0; $i < count($user_profile["groups"]); $i++) {
                if (array_search($user_profile["groups"][$i], $allowed_groups) !== false) {
                    $access_allowed = true;
                    break;
                }
            }
        }
        //ДЕЙСТВИЯ С РЕЗУЛЬТАТОМ ПРОВЕРКИ ДОСТУПА К МАТЕРИАЛУ:
        if(!$access_allowed)
        {
            header("HTTP/1.1 403 Forbidden");
			$DP_Content->value = translate_str_by_id(4014);
			$DP_Content->title_tag = translate_str_by_id(4014);
            $DP_Content->description_tag = translate_str_by_id(4014);
            $DP_Content->keywords_tag = translate_str_by_id(4014);
            $DP_Content->author_tag = translate_str_by_id(4014);
            $DP_Content->content_type = "text";
            $DP_Content->content = translate_str_by_id(4014);
			$DP_Content->service_data["error_page"] = 403;
        }
    }
}//~if($DP_Content->id != 0)
// ---------------------------------------------------------------------------------------------
//ОТДЕЛЬНО ПРОВЕРЯЕМ ПРАВА ДОСТУПА К МОДУЛЯМ
if(DP_User::isAdmin())
{
	//ПРОВЕРКА ПРАВ ДОСТУПА К МОДУЛЯМ:
	for($i=0; $i < count($DP_Module_array); $i++)//По каждому модулю
	{
		//Получаем список групп, допущенных к модулю ЯВНО
		$allowed_groups = array();//Список допущенных групп
		
		$module_access_query = $db_link->prepare("SELECT * FROM `modules_access` WHERE `module_id` = ?;");
		$module_access_query->execute( array($DP_Module_array[$i]->id) );
		while( $module_access_record = $module_access_query->fetch() )
		{
			$allowed_groups[] = (int)$module_access_record["group_id"];
		}
		
		//Далее дополняем этот список вложенными группами, которые также имеют доступ
		$inserted_groups = array();//Массив для вложенных групп
		for($g=0; $g < count($allowed_groups); $g++)
		{
			getAllowedGroups($allowed_groups[$g]);
		}
		$allowed_groups = array_merge($allowed_groups, $inserted_groups);//Объединяем
		
		
		$module_access_allowed = false;//Флаг - "Доступ к модулю разрешен"
		if (count($allowed_groups) === 0) {
			$module_access_allowed = true;
		} else {
			for ($ug = 0; $ug < count($user_profile["groups"]); $ug++) {
				if (array_search($user_profile["groups"][$ug], $allowed_groups) !== false) {
					$module_access_allowed = true;
					break;
				}
			}
		}
		
		//ДЕЙСТВИЯ С РЕЗУЛЬТАТОМ ПРОВЕРКИ ДОСТУПА К МОДУЛЮ:
		if(!$module_access_allowed)
		{
			array_splice($DP_Module_array, $i, 1);
		}
	}
}
// ---------------------------------------------------------------------------------------------
// ---------------------------------------------------------------------------------------------
//Рекурсивная функция получения вложенных групп
function getAllowedGroups($group)
{
    global $db_link;
    global $DP_Config;
    global $inserted_groups;
    global $allowed_groups;

    if (!is_array($inserted_groups)) {
        $inserted_groups = array();
    }
    if (!is_array($allowed_groups)) {
        $allowed_groups = array();
    }

    $group_query = $db_link->prepare("SELECT * FROM `groups` WHERE `id` = ?;");
	$group_query->execute( array($group) );
	$group_record = $group_query->fetch();
    if($group_record["count"] == 0)
    {
        return;
    }
    else
    {
		$inserted_groups_query = $db_link->prepare("SELECT * FROM `groups` WHERE `parent` = ?;");
		$inserted_groups_query->execute( array($group) );
        while( $inserted_group_record = $inserted_groups_query->fetch() )
        {
            //Если эта группа уже есть в списке вложенных или в списке явных - пропускаем, чтобы не дублировать
            if( array_search($inserted_group_record["id"], $inserted_groups) !== false || array_search($inserted_group_record["id"], $allowed_groups) !== false )
            {
                continue;
            }
			$inserted_groups[] = $inserted_group_record["id"];
            getAllowedGroups($inserted_group_record["id"]);//Рекурсивный вызов для этой группы
        }
    }
}
?>