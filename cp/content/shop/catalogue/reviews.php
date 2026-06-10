<?php
/**
 * Скрипт страницы Панели управления "Отзывы покупателей"
*/
defined('_ASTEXE_') or die('No access');



if( isset($_POST["action"]) )
{
	// -------------------------------------------------------------------------------
	//Защита от CSRF-атак
	require_once($_SERVER["DOCUMENT_ROOT"]."/content/users/stop_csrf.php");
	// -------------------------------------------------------------------------------
	
	
	if( $_POST["action"] == 'delete' )
	{
		$id = (int) $_POST["id"];
		
		if($db_link->prepare("DELETE FROM `shop_products_evaluations` WHERE `id` = ?")->execute(array($id)))
		{		
			?>
			<script>
				location="<?php echo $DP_Config->domain_path.$DP_Config->backend_dir; ?>/shop/catalogue/otzyvy-pokupatelej?success_message=<?php echo urlencode(translate_str_by_id(2999)); ?>";
			</script>
			<?php
			exit;
		}else{
			?>
			<script>
				location="<?php echo $DP_Config->domain_path.$DP_Config->backend_dir; ?>/shop/catalogue/otzyvy-pokupatelej?error_message=<?php echo urlencode(translate_str_by_id(2610)); ?>";
			</script>
			<?php
			exit;
		}
	}
}



// формируем пагинацию
// $all 		= количество постов в категории (определяем количество постов в базе данных)
// $lim 		= количество постов, размещаемых на одной странице
// $prev 		= количество отображаемых ссылок до и после номера текущей страницы
// $curr_link 	= номер текущей страницы (получаем из URL)
// $curr_css 	= css-стиль для ссылки на "текущую (активную)" страницу
// $link 		= часть адреса, используемый для формирования линков на другие страницы
function pagination($all, $lim, $prev, $curr_link, $curr_css, $link='')
{
    global $DP_Config, $DP_Content;
	
	$html = '';
	// осуществляем проверку, чтобы выводимые первая и последняя страницы
    // не вышли за границы нумерации
    $first = $curr_link - $prev;
    if ($first < 1) $first = 1;
    $last = $curr_link + $prev;
    if ($last > ceil($all/$lim)) $last = ceil($all/$lim);

    // начало вывода нумерации
    // выводим первую страницу
    $y = 1;
    if ($first > 1) $html .= "<li><a href='/{$DP_Config->backend_dir}/{$DP_Content->url}'>1</a></li>";
    // Если текущая страница далеко от 1-й (>10), то часть предыдущих страниц
    // скрываем троеточием
    // Если текущая страница имеет номер до 10, то выводим все номера
    // перед заданным диапазоном без скрытия
	// $prev
    $y = $first - 1;
    if ($first > $prev) {
        $html .= "<li><a href='/{$DP_Config->backend_dir}/{$DP_Content->url}?page={$y}' >...</a></li>";
    } else {
        for($i = 2;$i < $first;$i++){
            $html .=  "<li><a href='/{$DP_Config->backend_dir}/{$DP_Content->url}?page={$y}' >$i</a></li>";
        }
    }
    // отображаем заданный диапазон: текущая страница +-$prev
    for($i = $first;$i < $last + 1;$i++){
        // если выводится текущая страница, то ей назначается особый стиль css
        if($i == $curr_link) {
			$html .= '<li class="'.$curr_css.'"><a>'. $i .'</a></li>';
        } else {
            $alink = "<li><a href='/{$DP_Config->backend_dir}/{$DP_Content->url}";
            if($i != 1) $alink .= "?page={$i}";
            $alink .= "'>$i</a></li>";
            $html .= $alink;
        }
    }
    $y = $last + 1;
    // часть страниц скрываем троеточием
    if ($last < ceil($all / $lim) && ceil($all / $lim) - $last > 2) $html .=  "<li><a href='/{$DP_Config->backend_dir}/{$DP_Content->url}?page={$y}' >...</a></li>";
    // выводим последнюю страницу
    $e = ceil($all / $lim);
    if ($last < ceil($all / $lim)) $html .=  "<li><a href='/{$DP_Config->backend_dir}/{$DP_Content->url}?page={$e}' >$e</a></li>";
	
	return $html;
}



// Текущая страница
$page = 1;
if( isset( $_GET['page'] ) )
{
	$page = (int) $_GET['page'];
}
if(empty($page))
{
	$page = 1;
}
$lim_rows = $DP_Config->list_page_limit;// Количество строк на страницу
$from_rows = ($page * $lim_rows) - $lim_rows;// С какой записи выводить



$elements_query = $db_link->prepare("SELECT SQL_CALC_FOUND_ROWS *, (SELECT `caption` FROM `shop_catalogue_products` WHERE `id` = `shop_products_evaluations`.`product_id`) AS 'product_caption' FROM `shop_products_evaluations` ORDER BY `id` DESC LIMIT ".$from_rows.", ".$lim_rows);
$elements_query->execute();

$elements_count_rows_query = $db_link->prepare('SELECT FOUND_ROWS();');
$elements_count_rows_query->execute();
$all_rows = $elements_count_rows_query->fetchColumn();// Всего записей в базе данных



if(empty($all_rows)){
	?>
	<div class="col-lg-12">
		<div class="hpanel">
			<div class="panel-heading hbuilt">
				<?php echo translate_str_by_id(3000); ?>
			</div>
			<div class="panel-body">
				<?php echo translate_str_by_id(3001); ?>
			</div>
		</div>
	</div>
	<?php
}else{
	?>
	<div class="col-lg-12">
		<div class="hpanel">
			<div class="panel-heading hbuilt">
				<?php echo translate_str_by_id(3000); ?>
			</div>
			<div class="panel-body">
				<div style="overflow: hidden; overflow-x: auto;">
					<table class="table">
						<tr>
							<th>ID</th>
							<th><?php echo translate_str_by_id(32); ?></th>
							<th><?php echo translate_str_by_id(3002); ?></th>
							<th><?php echo translate_str_by_id(3003); ?> 1-5</th>
							<th><?php echo translate_str_by_id(3004); ?></th>
							<th><?php echo translate_str_by_id(3005); ?></th>
							<th><?php echo translate_str_by_id(3006); ?></th>
							<th><?php echo translate_str_by_id(3007); ?></th>
							<th><?php echo translate_str_by_id(3008); ?></th>
							<th></th>
						</tr>
						
						<?php
						while($element_record = $elements_query->fetch())
						{
							?>
							<tr>
								<td><?php echo $element_record['id']; ?></td>
								<td><?php echo $element_record['product_id']; ?></td>
								<td><?php echo translate_str_by_id($element_record['product_caption']); ?></td>
								<td><?php echo $element_record['mark']; ?></td>
								<td><?php echo $element_record['text_plus']; ?></td>
								<td><?php echo $element_record['text_minus']; ?></td>
								<td><?php echo $element_record['text']; ?></td>
								<td><?php echo $element_record['user_id']; ?></td>
								<td><?php echo date("d.m.Y H:i", $element_record['time']); ?></td>
								<td><a class="btn btn-as btn-danger" onClick="delete_review(<?php echo (int) $element_record['id']; ?>)"><?php echo translate_str_by_id(2224); ?></a></td>
							</tr>
							<?php
						}
						?>
						
					</table>
				</div>
			</div>
		</div>
	</div>
	
	<?php
	//Для работы с пользователем
	require_once( $_SERVER['DOCUMENT_ROOT']."/content/users/dp_user.php" );
	$user_session = DP_User::getAdminSession();
	?>
	
	
	<form method="POST" name="delete_review_form">
		<input type="hidden" name="action" value="delete" />
		<input type="hidden" name="id" id="review_id" value="" />
		<input type="hidden" name="csrf_guard_key" value="<?php echo $user_session["csrf_guard_key"]; ?>" />
	</form>
	
	
	<script>
	// Удалить отзыв
	function delete_review(id){
		if( !confirm("<?php echo translate_str_by_id(3009); ?>") )
		{
			return;
		}
		
		document.getElementById('review_id').value = id;
		document.forms['delete_review_form'].submit();
	}
	</script>
	<?php
}



if(ceil($all_rows / $lim_rows) > 1){
	?>
	<div class="text-center">
		<ul class="pagination">
		<?php
		echo pagination($all_rows, $lim_rows, 2, $page, 'active');
		?>
		</ul>
	</div>
	<?php
}

?>