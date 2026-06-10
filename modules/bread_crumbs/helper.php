<?php
/*
Скрипт для вспомогательных функций модуля "Хлебные крошки"
*/
defined('_ASTEXE_') or die('No access');

//Получение альтернативного варианта хлебных крошек
/*
Функция работает с таблицей bread_crumbs_rules.
В этой таблице прописаны альтернативные правила формирования хлебных крошек.

Альтернативное правило подразуевает формирование ОПРЕДЕЛЕННОЙ($bread_crumb) хлебной крошки для ДАННОЙ СТРАНИЦЫ($url)
В колонке bread_crumb_caption прописано правило формирования заголовка для хлебной крошки

В колонке bread_crumb_href_args прописано правило формирования аргументов для href хлебной крошки. При этом сам путь без аргументов остается без изменений
*/
function get_alternative_bread_crumbs($url, $bread_crumb)
{
	global $db_link;
	global $DP_Config;
	
	//Определяем, есть для данных url правило генерации альтернативных вариантов
	$stmt = $db_link->prepare('SELECT * FROM `bread_crumbs_rules` WHERE `url` = :url AND `bread_crumb` = :bread_crumb;');
	$stmt->bindValue(':url', $url);
	$stmt->bindValue(':bread_crumb', $bread_crumb);
	$stmt->execute();
	
	//Запись правила
	$rule = $stmt->fetch(PDO::FETCH_ASSOC);
	
	
	
	if($rule == false)
	{
		return false;//Правил для данного набора нет
	}
	
	
	//Массив с ответом
	$alternative_bread_crumbs = array();
	
	//Генерируем заголовок
	if($rule["bread_crumb_caption"] != "")
	{
		$bread_crumb_caption = json_decode($rule["bread_crumb_caption"], true);
		
		//В зависимости от типа, определяем источник строки заголока
		switch($bread_crumb_caption["type"])
		{
			//Из массива $_GET
			case "get":
				$alternative_bread_crumbs["step_caption"] = $_GET[$bread_crumb_caption["value"]];
				break;
			//Записано явно
			case "text":
				$alternative_bread_crumbs["step_caption"] = $bread_crumb_caption["value"];
				break;
			//Получаем из url
			case "url":
				$url = $bread_crumb_caption["value"];//Адрес
				//Аргументы
				for($a=0; $a < count($bread_crumb_caption["args"]); $a++)
				{
					switch($bread_crumb_caption["args"][$a]["type"])
					{
						//Получаем из конфигурационного файла
						case "config":
							$url = str_replace("%".$a, $DP_Config->{$bread_crumb_caption["args"][$a]["value"]}, $url);
							break;
						//Получаем из аргументов сктраницы
						case "get":
							$url = str_replace("%".$a, $_GET[$bread_crumb_caption["args"][$a]["value"]],$url);
							break;
						//Записано явно
						case "text":
							$url = str_replace("%".$a, $bread_crumb_caption["args"][$a]["value"], $url);
							break;
					}
				}
				
				
				$curl = curl_init();
				curl_setopt($curl, CURLOPT_URL, $url);
				curl_setopt($curl, CURLOPT_HEADER, 0);
				curl_setopt($curl, CURLOPT_RETURNTRANSFER, 1);
				curl_setopt($curl, CURLOPT_SSL_VERIFYHOST, 0);
				curl_setopt($curl, CURLOPT_SSL_VERIFYPEER, 0);
				$curl_result = curl_exec($curl);
				curl_close($curl);
				$alternative_bread_crumbs["step_caption"] = $curl_result;
				break;
		}
	}
	//Генерируем href
	$href = $rule["bread_crumb"];//Чистый путь
	if($rule["bread_crumb_href_args"] != "")
	{
		$bread_crumb_href_args = json_decode($rule["bread_crumb_href_args"], true);
		for($i=0; $i < count($bread_crumb_href_args); $i++)
		{
			$ampersant = "?";
			if($i > 0)
			{
				$ampersant = "&";
			}
			$href = $href . $ampersant . $bread_crumb_href_args[$i]["name"]."=";
			
			//В зависимости от способа получения значения аргумента
			switch($bread_crumb_href_args[$i]["type"])
			{
				//Берется из аргумента данной страницы:
				case "get":
					$href = $href . $_GET[$bread_crumb_href_args[$i]["value"]];
					break;
				case "text":
					$href = $href . $bread_crumb_href_args[$i]["value"];
					break;
			};
		}
	}
	$alternative_bread_crumbs["step_url"] = $href;

	return $alternative_bread_crumbs;
}
?>