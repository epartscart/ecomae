<?php
//Внешний доступ исключен
defined('_ASTEXE_') or die('No access');
/*
Скрипт для получения следующего ID (auto_increment) в определенной таблице. Подключается на страницах, где идет редактирование данных через дерево - когда ID следующего добавляемого элемента определяется локально в клиенте. Этот скрипт позвляет определить ID даже, если в конфиге MySQL включено кеширование статистики.

Usage:
1. Подключаем скрипт в начало вывода страницы:

//Определяем следующий ID ($next_id)
$table_name = "указать имя таблицы";
$col_name = "id";//Имя колонки, в которой содержится id записей (обычно имя равно id)
require_once($_SERVER["DOCUMENT_ROOT"]."/".$DP_Config->backend_dir."/lib/docpart/get_next_id.php");

2. После этого, следующий ID можем брать из переменной $next_id


ОСОБЕННОСТЬ:
Если открыть страницу редактора (материалы, группы пользователей, гео-узлы и т.д.), то, автоинкремент уже будет изменяться в БД. Т.е. если открыть страницу, и ничего на ней не сделать (не добавить новые элементы) и просто закрыть, то, ID будут инкрементироваться в холостую. На функционал это никак не влияет. Просто нужно иметь ввиду эту особенность - если у кого-то возникнут вопросы, куда делись те или иные элементы (почему какие-то ID пропущены, если никто ничего не удалял).
*/


//Допустимые значения имен таблиц и имен колонок (для исключения SQL-инъекций)
$tables_names_suitable = array('shop_geo', 'shop_catalogue_categories', 'shop_tree_lists_items', 'content', 'groups');
$cols_names_suitable = array('id');
//Проверяем исходные значения
if( array_search($table_name, $tables_names_suitable ) === false )
{
	exit;
}
if( array_search($col_name, $cols_names_suitable ) === false )
{
	exit;
}


//Делаем через транзакцию
try
{
	//Старт транзакции
	if( ! $db_link->beginTransaction()  )
	{
		throw new Exception("Could not start the transaction");
	}
	
	//Добавляем пустую запись
	if( !$db_link->prepare("INSERT INTO `$table_name` (`$col_name`) VALUES (NULL);")->execute() )
	{
		throw new Exception("Could not insert empty record");
	}
	
	//Получаем ID добавленной записи
	$next_id = $db_link->lastInsertId();
	if(!$next_id)
	{
		throw new Exception("Could not get record ID");
	}
	
	//Теперь удаляем пустую запись
	if( !$db_link->prepare("DELETE FROM `$table_name` WHERE `$col_name` = ?;")->execute( array($next_id) ) )
	{
		throw new Exception("Could not delete empty record");
	}
	
	//Дошли до сюда, значит выполнено ОК
	$db_link->commit();//Коммитим все изменения и закрываем транзакцию
	
	//Таким образом, можем использовать $next_id для добавления следующей записи и можем инкрементировать ID на клиенте, для добавления последующих строк
}
catch (Exception $e)
{
	//Откатываем все изменения и закрываем транзакцию
	$db_link->rollBack();
	//Показываем ошибку
	?>
	<script>
	alert("Error: <?php echo $e->getMessage(); ?>");
	</script>
	<?php
}
?>