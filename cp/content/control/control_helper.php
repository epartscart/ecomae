<?php
//Определение функции проверки доступа к странице
function is_anable($item=null)
{
	if( !$item )
	{
		return false;
	}
	
	global $db_link;
	global $DP_Config;
	
	
	//Если в URL добавлен аргумент - отбрасываем его, чтобы искать страницу по чистому URL
	$item["url"] = explode('?', $item["url"]);
	$item["url"] = $item["url"][0];
	

	//Определяем ID материала
	$content_query = $db_link->prepare("SELECT * FROM `content` WHERE `url` = ?;");
	$content_query->execute( array(str_replace('/'.$DP_Config->backend_dir.'/', '', $item["url"])) );
	$content_record = $content_query->fetch();
	if( $content_record == false )
	{
		return 0;//Такой страницы вообще нет
	}
	$content_id = $content_record["id"];
	
	
	//ПРОФИЛЬ АДМИНИСТРАТОРА:
	$user_profile = DP_User::getAdminProfile();
	if (!is_array($user_profile)) {
		$user_profile = array('groups' => array());
	}
	if (!isset($user_profile['groups']) || !is_array($user_profile['groups'])) {
		$user_profile['groups'] = array();
	}
	
	
	//СПИСОК ДОПУЩЕННЫХ ГРУПП
	$explicit_groups = array();
	$content_access_query = $db_link->prepare("SELECT * FROM `content_access` WHERE `content_id` = ?;");
	$content_access_query->execute( array($content_id) );
	while( $content_access_record = $content_access_query->fetch() )
	{
		array_push($explicit_groups, (int)$content_access_record["group_id"]);
	}
	global $inserted_groups;
	global $allowed_groups;
	$inserted_groups = array();
	$allowed_groups = $explicit_groups;
	if (function_exists('getAllowedGroups')) {
		for ($i = 0; $i < count($allowed_groups); $i++) {
			getAllowedGroups($allowed_groups[$i]);
		}
		$allowed_groups = array_merge($allowed_groups, $inserted_groups);
	}
	
	
	
	//ПРОВЕРКА ДОСТУПА К МАТЕРИАЛУ
	$access_allowed = false;//Флаг "Доступ разрешен"
	if (count($allowed_groups) === 0) {
		$access_allowed = !empty($user_profile['groups']);
	} else {
		//Доступ разрешен, если хотя бы одна из групп пользователя имеет доступ
		for ($i = 0; $i < count($user_profile['groups']); $i++) {
			if (array_search($user_profile['groups'][$i], $allowed_groups) !== false) {
				$access_allowed = true;
				break;
			}
		}
	}
	//ДЕЙСТВИЯ С РЕЗУЛЬТАТОМ ПРОВЕРКИ ДОСТУПА К МАТЕРИАЛУ:
	if(!$access_allowed)
	{
		return false;
	}
	
	return true;
}//~function is_anable($item)
?>