<?php
/**
 * Скрипт для модуля новостей
 * 
 * Принцип работы модуля:
 * 
 * - блок новостей - это отдельный раздел в дереве материалов;
 * - корень должен быть в первом уровне;
 * - каждая новость должна быть во втором уровне;
 * - данный модуль выводит несколько ссылок на эти страницы и дополнительно выводит ссылку на корень новостей - на страницу, где отображаются все новости
 * 
 * 
 * Параметры модуля:
 * - ID корня новостей;
 * - количество новостей для вывода в модуль
*/
defined('_ASTEXE_') or die('No access');


$module_id = (integer)"<module_id>";//Получаем ID модуля из ядра


//Получаем специальные настройки модуля
$stmt = $db_link->prepare('SELECT * FROM `modules` WHERE `id` = :id;');
$stmt->bindValue(':id', $module_id);
$stmt->execute();
$module_record = $stmt->fetch(PDO::FETCH_ASSOC);
$module_data = json_decode($module_record["data"], true);//Свои спецнастройки

//var_dump($module_data);

$root_content = $module_data["root_content"];
$news_count = $module_data["news_count"];

//Получаем новости из БД
$stmt = $db_link->prepare('SELECT `value`, `time_created`, `description_tag`, `url` FROM `content` WHERE `parent` = :parent ORDER BY `id` DESC LIMIT :limit;');
$stmt->bindValue(':parent', (int)$root_content);
$stmt->bindValue(':limit', (int)$news_count, PDO::PARAM_INT);
$stmt->execute();
while($news = $stmt->fetch(PDO::FETCH_ASSOC))
{
    ?>
	<div class="media news_block">
		<div class="media-body">
			<h4 class="media-heading"><a href="<?php echo $multilang_params['lang_href']; ?><?php echo "/".$news["url"]; ?>"><?php echo translate_str_by_id($news["value"]); ?><br><?php echo translate_str_by_id($news["description_tag"]); ?></a></h4>
			<small><?php echo date("d.m.Y", $news["time_created"]); ?></small>
		</div>
	</div>
    <?php
}


//Получаем url корневой страницы новостей
$stmt = $db_link->prepare('SELECT `url` FROM `content` WHERE `id` = :id;');
$stmt->bindValue(':id', $root_content);
$stmt->execute();
$root_content_record = $stmt->fetch(PDO::FETCH_ASSOC);
?>
<a href="<?php echo $multilang_params['lang_href']; ?><?php echo "/".$root_content_record["url"]; ?>" class="all_news_link"><?php echo translate_str_by_id(4763); ?></a>