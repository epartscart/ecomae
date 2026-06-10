<?php
/**
 * Скрипт для обработки различных операций над автомобилями гаража
*/
header('Content-Type: application/json;charset=utf-8;');



//Соединение с БД
require_once($_SERVER["DOCUMENT_ROOT"]."/config.php");
$DP_Config = new DP_Config;
try
{
	$db_link = new PDO('mysql:host='.$DP_Config->host.';dbname='.$DP_Config->db, $DP_Config->user, $DP_Config->password);
}
catch (PDOException $e) 
{
    $answer = array();
	$answer["status"] = false;
	$answer["message"] = "No DB connect";
	exit( json_encode($answer) );
}
$db_link->query("SET NAMES utf8;");


// -------------------------------------------------------------------------------
//Подключение мультиязычности
require_once($_SERVER["DOCUMENT_ROOT"]."/lang/dp_lang.php");
$multilang_params = multilang_init();
// -------------------------------------------------------------------------------


// -------------------------------------------------------------------------------
//Защита от CSRF-атак
require_once($_SERVER["DOCUMENT_ROOT"]."/content/users/stop_csrf.php");
// -------------------------------------------------------------------------------



$answer = array('status'=>false);
$request_object = json_decode($_POST['request_object'], true);



//Для работы с пользователем
require_once($_SERVER["DOCUMENT_ROOT"]."/content/users/dp_user.php");
$user_id = DP_User::getUserId();



//Проверяем право менеджера или пренадлежность автомобиля пользователю
if($request_object['action'] != 'search')
{
	$query_garage = $db_link->prepare('SELECT `id` FROM `shop_docpart_garage` WHERE `id` = ? AND `user_id` = ?;');
	$query_garage->execute( array($request_object["car_id"], $user_id) );
	$record_garage = $query_garage->fetch();
	if( ! DP_User::isAdmin() && (int) $record_garage['id'] <= 0 )
	{
		$answer = array();
		$answer["status"] = false;
		$answer["message"] = "No Access";
		exit( json_encode($answer) );
	}
}



switch($request_object['action'])
{
	case 'search':
		
		$search_str = htmlentities(trim($request_object["search_str"]));
		
		$query = $db_link->prepare('SELECT `id` FROM `shop_docpart_garage` WHERE `user_id` = ? AND (`caption` LIKE ? OR `marka` LIKE ? OR `vin` LIKE ? OR `note` LIKE ?);');
		$query->execute( array($user_id, '%'.$search_str.'%', $search_str.'%', $search_str.'%', '%'.$search_str.'%') );
		
		$list = array();
		while($record = $query->fetch()){
			$list[] = $record['id'];
		}
		
		$answer["list"] = $list;
		$answer["status"] = true;
		
	break;
	case 'check_car':
		
		$user_id  = (int) $request_object['user_id'];
		$car_id   = (int) $request_object["car_id"];
		$order_id = (int) $request_object["order_id"];
		
		$query = $db_link->prepare('SELECT `id` FROM `shop_docpart_garage_orders` WHERE `order_id` = ? AND `garage_id` = ?;');
		$query->execute(array($order_id, $car_id));
		$row = $query->fetch();
		
		if($row['id'] > 0){
			$delete_query = $db_link->prepare('DELETE FROM `shop_docpart_garage_orders` WHERE `order_id` = ? AND `garage_id` = ?;');
			$delete_query->execute(array($order_id, $car_id));
			$answer["flag"] = 0;
		}else{
			$query = $db_link->prepare('INSERT INTO `shop_docpart_garage_orders`(`id`, `garage_id`, `order_id`) VALUES (NULL,?,?);');
			$query->execute(array($car_id, $order_id));
			$answer["flag"] = 1;
		}
		
		$answer["status"] = true;
		
	break;
	case 'active_car':
		
		//Выбор автомобиля в качестве основного
		
		$user_id  = (int) $request_object['user_id'];
		$car_id   = (int) $request_object["car_id"];
		
		$query = $db_link->prepare('SELECT `id` FROM `shop_docpart_garage` WHERE `user_id` = ?  AND `active` = 1;');
		$query->execute(array($user_id));
		$row = $query->fetch();
		
		$query = $db_link->prepare('UPDATE `shop_docpart_garage` SET `active` = 0 WHERE `user_id` = ?;');
		$query->execute(array($user_id));
		
		if($row['id'] != $car_id){
			$query = $db_link->prepare('UPDATE `shop_docpart_garage` SET `active` = 1 WHERE `id` = ?;');
			$query->execute(array($car_id));
		}
		
		$answer["status"] = true;
		
	break;
	case 'delete_car':
		
		$user_id = $request_object['user_id'];
		$car_id = (int)$request_object["car_id"];
		
		$delete_query = $db_link->prepare('DELETE FROM `shop_docpart_garage` WHERE `id` = :car_id AND `user_id` = :user_id;');
		$delete_query->bindValue(':car_id', $car_id);
		$delete_query->bindValue(':user_id', $user_id);
		
		if($delete_query->execute())
		{
			$answer = array('status'=>true);
		}
		
	break;
	case 'get_table_cars':
		$html = '';
		$customer_id = $request_object['customer_id'];
		$order_id = $request_object['order_id'];
		
		ob_start();
		
		if($order_id > 0){
			$query = $db_link->prepare('SELECT SQL_CALC_FOUND_ROWS *, (SELECT COUNT(*) FROM `shop_docpart_garage_orders` WHERE `order_id` = ? AND `garage_id` = `shop_docpart_garage`.`id`) AS `link` FROM `shop_docpart_garage` WHERE `user_id` = ?;');
			$query->execute( array($order_id, $customer_id) );
		}else{
			$query = $db_link->prepare('SELECT SQL_CALC_FOUND_ROWS * FROM `shop_docpart_garage` WHERE `user_id` = ?;');
			$query->execute( array($customer_id) );
		}
		
		$elements_count_rows_query = $db_link->prepare('SELECT FOUND_ROWS();');
		$elements_count_rows_query->execute();
		$elements_count_rows = $elements_count_rows_query->fetchColumn();
			
		if($elements_count_rows > 0){
			?>
			<table class="table">
				<tr>
					
					<?php
					if($order_id > 0){
					?>
					<th>
					<?php echo translate_str_by_id(5608); ?>
					</th>
					<?php
					}
					?>
					
					<th><?php echo translate_str_by_id(630); ?></th>
					<th><?php echo translate_str_by_id(4044); ?></th>
					<th>VIN</th>
					<th></th>
				</tr>
				<?php
				while( $record = $query->fetch() )
				{
					$id = $record["id"];
					$link = (int) $record["link"];
					$caption = $record["caption"];
					$mark_id = $record["mark_id"];
					$model = $record["model"];
					$vin = $record["vin"];
					$year = $record["year"];
					
					$mark_model_str = '';
					
					if($mark_id > 0){
						$query_mark = $db_link->prepare('SELECT * FROM `shop_docpart_cars` WHERE `id` = ?;');
						$query_mark->execute( array($mark_id) );
						$record_mark = $query_mark->fetch();
						$mark_model_str .= $record_mark['caption'];
					}
					
					if(!empty($model)){
						if(!empty($mark_model_str)){
							$mark_model_str .= ' - ';
						}
						$mark_model_str .= $model;
					}
					?>
					<tr>
						
						<?php
						if($order_id > 0){
							echo '<td>';
							if($link > 0){
							?>
							<a style="color:#66bf05; font-size: 16px;" onclick="check_car(0, <?=$id;?>);"><i class="fa fa-check" aria-hidden="true"></i></a>
							<?php
							}else{
							?>
							<a style="color:#a9a9a9; font-size: 16px;" onclick="check_car(1, <?=$id;?>);"><i class="fa fa-check" aria-hidden="true"></i></a>
							<?php
							}
							echo '</td>';
						}
						?>
						
						<td>
							<div><?=$caption;?></div>
							<div><?=$mark_model_str;?></div>
						</td>
						<td><?=$year;?></td>
						<td><?=$vin;?></td>
						<td style="text-align:right;">
							<a onclick="edit_car(<?=$id;?>);" class="btn btn-ar btn-primary" title="<?php echo translate_str_by_id(2270); ?>"><i class="far fa-edit"></i></a>
							<a class="btn btn-ar btn-primary" href="javascript:void(0);" onclick="delete_car(<?php echo $id; ?>);" title="<?php echo translate_str_by_id(2224); ?>"><i class="fa fa-trash"></i></a>
						</td>
					</tr>
					<?php
				}
				?>
			</table>
			<?php
		}else{
			echo translate_str_by_id(5609);
		}
		
		$html = ob_get_contents();
		ob_end_clean();
		
		exit($html);
	break;
}
exit(json_encode($answer));
?>