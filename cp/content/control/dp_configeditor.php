<?php
/**
 * Класс для работы с конфигурационным файлом
*/
defined('_ASTEXE_') or die('No access');



class DP_ConfigEditor
{
	//Метод установки значения параметра
	static public function setParameter($name, $value)
	{
		$DP_ConfigStrings = array();//Набор строк файла
	
		//1.Получить текущий DP_Config
		$config_path = $_SERVER["DOCUMENT_ROOT"]."/config.php";
		if(!file_exists($config_path))
		{
			exit(translate_str_by_id(2442));
		}
		$config_file = fopen($config_path, "r");
		fgets($config_file, 4096);//Одну строку пропускаем - это <?php
		while (!feof($config_file)) 
		{
			$string = fgets($config_file, 4096);
			$string = str_replace(array("\n"),"", $string);//Знаки переноса строк убираем
			
			$substr = array();//Массив для имен параметров, которые есть в формате импорта
			preg_match('#public([ ]{1,})\$([a-zA-Z0-9_]{1,})([ ]{0,})([=]{1,1})([ ]{0,})\'([\d\s\D]{0,})\';([\d\s\D]{0,})#', $string, $substr);//По регулярному выражению получаем массив имен параметров
			
			//Если это не параметр - заносим как обычную строку
			if(count($substr) == 0)
			{
				$DP_ConfigString = new DP_ConfigString;
				$DP_ConfigString->type = 'string';
				$DP_ConfigString->string = $string;
				
				array_push($DP_ConfigStrings, $DP_ConfigString);
			}
			else//Это параметр - парсим и вносим
			{
				$DP_ConfigString = new DP_ConfigString;
				$DP_ConfigString->type = 'parameter';
				$DP_ConfigString->string = $substr[0];
				
				//Парсим
				//1.Имя параметра
				$name_substr = str_replace(array("public "),"", $substr[0]);//Убираем public
				$name_substr = explode('$', $name_substr);//Делим строку на массив через знак $
				$name_substr = $name_substr[1];//Берем второй элемент (все справа от $)
				$name_substr = explode('=', $name_substr);//Делим строку на массив через знак =
				$name_substr = $name_substr[0];//Берем gthdsq элемент (все слева от =)
				$name_substr = str_replace(array(" "),"", $name_substr);//Убираем все пробелы
				$DP_ConfigString->name = $name_substr;
				//2. Значение параметра
				if($DP_ConfigString->name == $name)//Если это тот параметр, который требуется заменить
				{
					$DP_ConfigString->value = (string)$value;
				}
				else//Параметр остается прежним
				{
					$value_substr = array();//Массив для имен параметров, которые есть в формате импорта
					preg_match('#\'([\d\s\D]{0,})\';#', $DP_ConfigString->string, $value_substr);//По регулярному выражению получаем массив имен параметров
					$value_substr = $value_substr[0];
					$value_substr = str_replace(array("'",";"),"", $value_substr);//Убираем все лишнее
					$DP_ConfigString->value = $value_substr;
				}
				//3.Комментарий
				$comment_substr = explode("';", $DP_ConfigString->string);//Делим строку на массив через знак ';
				$comment_substr = $comment_substr[count($comment_substr)-1];//Берем последний элемент - все, что справа от точки с запятой
				$DP_ConfigString->comment = $comment_substr;
				array_push($DP_ConfigStrings, $DP_ConfigString);
			}
		}
		fclose($config_file);
		
		//ЗДЕСЬ ГЕНЕРАЦИЯ НОВОГО ФАЙЛА
		$new_file_string = "";//Строка для нового файла
		foreach($DP_ConfigStrings as $key => $value)
		{
			if($value->type == 'string')
			{
				$new_file_string .= $value->string."\n";
			}
			else
			{
				$new_file_string .= '	public $'.$value->name.' = \''.$value->value.'\';'.$value->comment."\n";
			}
		}
		
		
		//Перезаписываем файл
		$config_file = fopen($config_path, "w");
		fwrite($config_file, "<"."?"."php\n".rtrim($new_file_string));
		fclose($config_file);
	}
}



//Класс одной строки конфигурационного файла
class DP_ConfigString
{
	public $type;//Тип строки ()
	public $string;//Вся строка
	
	public $name;//Имя параметра
	public $value;//Значение параметра
	public $comment;//Комментарий
}
?>