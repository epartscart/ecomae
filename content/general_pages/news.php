<?php
/**
 * Страничный скрипт для вывода корневой страницы новостей
 * 
 * Скрипт знает об ID страницы новостей по $DP_Content->id
 * 
*/
defined('_ASTEXE_') or die('No access');


//Получаем количество новостей
$stmt = $db_link->prepare('SELECT COUNT(*) FROM `content` WHERE `parent` = :parent ORDER BY `id` DESC;');
$stmt->bindValue(':parent', $DP_Content->id);
$stmt->execute();
$news_num_rows = $stmt->fetchColumn();

//Получаем новости из БД
$stmt = $db_link->prepare('SELECT `value`, `time_created`, `description_tag`, `url` FROM `content` WHERE `parent` = :parent ORDER BY `id` DESC;');
$stmt->bindValue(':parent', $DP_Content->id);
$stmt->execute();


//ОБЕСПЕЧИВАЕМ ПОСТРАНИЧНЫЙ ВЫВОД:
//---------------------------------------------------------------------------------------------->
$page = 0;
if( isset($_GET["page"]) )
{
	if( (int)$_GET["page"] > 0 )
	{
		$page = (int)$_GET["page"] - 1;
	}
}
//----------------------------------------------------------------------------------------------|


$p = $DP_Config->list_page_limit;
$news_counter_total = 0;
$news_counter_printed = 0;
while($news = $stmt->fetch(PDO::FETCH_ASSOC) )
{
    $news_counter_total++;
    
    //Пропускаем нужное количество блоков в соответствии с номером требуемой страницы
    if($news_counter_total <= $page*$p)
    {
        continue;
    }
    ?>
    <div class="news_block">
        <h4><a href="<?php echo $multilang_params['lang_href']; ?><?php echo "/".$news["url"]; ?>"><?php echo translate_str_by_id($news["value"]); ?></a></h4>
        <p><?php echo translate_str_by_id($news["description_tag"]); ?></p>
        <p><?php echo date("d.m.Y", $news["time_created"]); ?></p>
    </div>
    <?php
    $news_counter_printed++;
    
    if($news_counter_printed >= $p)
    {
        break;
    }
}
?>



<div class="row" style="margin:0;">
	<div class="col-lg-12 text-center">
		<div id="bottom_pagination_div" class="btn-group" style="margin-top:10px;">
			<?php
			$url_for_link = $multilang_params['lang_href']."/".$DP_Content->url."?page=";
			$current_page = NULL;
			if( isset($_GET["page"]) )
			{
				$current_page = (int)$_GET["page"];
			}
			if( $current_page < 1 )
			{
				$current_page = 1;
			}
			//КНОПКА "ВЛЕВО"
			$to_left_disabled = "";
			if( $current_page == 1 )
			{
				$to_left_disabled = "disabled";
			}
			?>
			
			<a class="btn btn-default <?php echo $to_left_disabled; ?>" data-page="<?="1";?>" href="<?php echo $url_for_link."1"; ?>"><?php echo translate_str_by_id(4038); ?></a>
			<a class="btn btn-default <?php echo $to_left_disabled; ?>" data-page="<?=($current_page-1);?>" href="<?php echo $url_for_link.($current_page-1); ?>"><i class="fa fa-chevron-left"></i></a>

			<?php
			//Определяем количество страниц
			$pages_count = (int)($news_num_rows/$DP_Config->list_page_limit);
			if( ($news_num_rows%$DP_Config->list_page_limit) > 0 )
			{
				$pages_count++;
			}


			//Выводим кнопки для конкретных страниц (с номерами). Если будет критично для скорости работы - можно чуть доработать цикл - начать не сначала и break после вывода нужных ссылок
			for($i=1; $i <= $pages_count; $i++)
			{
				//Две кнопки до текущей - показываем
				if( ($current_page - $i) > 2  )
				{
					continue;
				}
				
				
				//Две кнопки после текущей - показываем
				if( ($i - $current_page) > 2  )
				{
					break;
				}
				
				
				
				$active = "";
				if($i == $current_page)
				{
					$active = "active";
				}
				?>
				<a class="btn btn-default <?php echo $active; ?>" data-page="<?=($i);?>" href="<?php echo $url_for_link.$i; ?>"><?php echo $i; ?></a>
				<?php
			}


			//КНОПКА "ВПРАВО"
			$to_right_disabled = "";
			if( ($current_page) == $pages_count )
			{
				$to_right_disabled = "disabled";
			}
			?>
			<a class="btn btn-default <?php echo $to_right_disabled; ?>" data-page="<?=($current_page+1);?>" href="<?php echo $url_for_link.($current_page+1); ?>"><i class="fa fa-chevron-right"></i></a>
			<a class="btn btn-default <?php echo $to_right_disabled; ?>" data-page="<?=($pages_count);?>" href="<?php echo $url_for_link.$pages_count; ?>"><?php echo translate_str_by_id(2184); ?></a>
		</div>
	</div>
</div>
