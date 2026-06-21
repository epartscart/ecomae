<?php
//Серверный скрипт для управления заданиями по раписанию (получить данные по заданию; отобразить список заданий; удалить задание по расписанию)
// -------------------------------------------------------------------------------
define('_PYPRICES_CRONTAB_', 1);
header('Content-Type: application/json; charset=utf-8');
// -------------------------------------------------------------------------------
//Конфигурация CMS
require_once($_SERVER["DOCUMENT_ROOT"]."/config.php");
$DP_Config = new DP_Config;
// -------------------------------------------------------------------------------
//Подключение к БД
try
{
	$db_link = new PDO('mysql:host='.$DP_Config->host.';dbname='.$DP_Config->db, $DP_Config->user, $DP_Config->password);
}
catch (PDOException $e) 
{
	$answer = array();
	$answer["status"] = false;
	$answer["message"] = 'No DB Connect';
	exit(json_encode($answer));
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
//Для работы с пользователями
require_once($_SERVER["DOCUMENT_ROOT"]."/content/users/dp_user.php");
// -------------------------------------------------------------------------------
//Проверка привелегий (пользователь должен иметь доступ к следующим страницам)
$pages_to_check = array();
$pages_to_check[] = array('url'=>'shop/prices', 'is_frontend' => 0);
require_once($_SERVER["DOCUMENT_ROOT"]."/content/users/check_user_access.php");
// -------------------------------------------------------------------------------
// -------------------------------------------------------------------------------

//Далее - смотря, что нужно клиенту.

// -------------------------------------------------------------------------------
//Если задан ID задания
if( isset( $_POST['cron_task_id'] ) )
{
	if( isset( $_POST['action'] ) && $_POST['action'] == "delete_cron_task" )
	{
		//Удаляем задание
		
		try
		{
			//Старт транзакции
			if( ! $db_link->beginTransaction()  )
			{
				throw new Exception( translate_str_by_id(2132) );
			}
			
			//Удаляем само задание
			if( ! $db_link->prepare("DELETE FROM `shop_docpart_pyprices_crontab` WHERE `id` = ?;")->execute( array($_POST['cron_task_id']) ) )
			{
				throw new Exception("Error deleting cron task");
			}
			
			//Удаяем привязку прайс-листов
			if( ! $db_link->prepare("DELETE FROM `shop_docpart_pyprices_crontab_prices` WHERE `crontab_task_id` = ?;")->execute( array($_POST['cron_task_id']) ) )
			{
				throw new Exception("Error deleting links between cron task and price lists");
			}
		}
		catch (Exception $e)
		{
			//Откатываем все изменения
			$db_link->rollBack();
			
			$answer = array();
			$answer["status"] = false;	
			$answer["message"] = $e->getMessage();
			exit(json_encode($answer));
		}

		//Дошли до сюда, значит выполнено ОК
		$db_link->commit();//Коммитим все изменения и закрываем транзакцию
		
		//Перезапись файла crontab (единый скрипт)
		require_once($_SERVER["DOCUMENT_ROOT"]."/".$DP_Config->backend_dir."/content/shop/prices_upload/for_pyprices/for_cron/crontab_writer.php");
		
		$answer = array();
		$answer["status"] = true;
		$answer["price_id"] = $_POST['price_id'];
		$answer["order_by"] = $_POST['order_by'];
		$answer["asc_desc"] = $_POST['asc_desc'];
		$answer["message"] = "OK";
		exit(json_encode($answer));
	}
	
	if( isset( $_POST['action'] ) && $_POST['action'] == "get_cron_task_objest" )
	{
		//Нужно просто выдать в клиент объект с описанием текущих настроек задания
		$cron_task_query = $db_link->prepare("SELECT * FROM `shop_docpart_pyprices_crontab` WHERE `id` = ?;");
		$cron_task_query->execute( array($_POST['cron_task_id']) );
		$cron_task = $cron_task_query->fetch();
		
		if( !$cron_task )
		{
			$answer = array();
			$answer["status"] = false;
			$answer["message"] = 'Cron task not found';
			exit(json_encode($answer));
		}
		else
		{
			//Getting bound prices
			$cron_task['prices'] = array();
			$prices_query = $db_link->prepare("SELECT * FROM `shop_docpart_pyprices_crontab_prices` WHERE `crontab_task_id` = ?;");
			$prices_query->execute( array($_POST['cron_task_id']) );
			while( $price = $prices_query->fetch() )
			{
				$cron_task['prices'][] = $price['price_id'];
			}
			
			
			//Handling some parameters
			$cron_task['day_week'] = explode(",", $cron_task['day_week']);//To array
			
			if( strlen($cron_task['hour']) == 1 )
			{
				$cron_task['hour'] = "0".$cron_task['hour'];
			}
			if( strlen($cron_task['minute']) == 1 )
			{
				$cron_task['minute'] = "0".$cron_task['minute'];
			}
			$cron_task['time'] = $cron_task['hour'].":".$cron_task['minute'];
			
			$answer = array();
			$answer["status"] = true;
			$answer['cron_task'] = $cron_task;
			$answer["message"] = 'OK';
			exit(json_encode($answer));
		}
	}
}
else if( isset($_POST['price_id']) && isset($_POST['action']) && $_POST['action'] == 'get_cron_tasks_html_table' )
{
	$_POST['price_id'] = (int)$_POST['price_id'];
	
	//Нужно вернуть перечень заданий по расписанию в виде таблицы в html-коде. Задания в списке нужны те, к которым привязан указанный прайс-листа
	
	//Вспомогалки
	$days = array('', translate_str_by_id(5359), translate_str_by_id(5360), translate_str_by_id(5361), translate_str_by_id(5362), translate_str_by_id(5363), translate_str_by_id(5364), translate_str_by_id(5365));
	
	//Сортировка по умолчанию - по id
	$order_by_field = "id";
	$order_by_asc_desc = "ASC";
	$order_to_query = "`".$order_by_field."` ".$order_by_asc_desc;//Строка в запрос
	
	//Направление сортировки - если было задано пользователем
	if( isset( $_POST['asc_desc'] ) )
	{
		//Защита от SQL-инъекций
		if( array_search( $_POST['asc_desc'] , array('ASC', 'DESC') ) === false )
		{
			exit;
		}
		
		$order_by_asc_desc = $_POST['asc_desc'];//Заменили направление
		$order_to_query = "`".$order_by_field."` ".$order_by_asc_desc;//Переформировали строку в запросе
	}
	
	
	//Если задано поле сортировки пользователем
	if( isset( $_POST['order_by'] ) )
	{
		//Защита от SQL-инъекций
		if( array_search( $_POST['order_by'] , array('id', 'active', 'time') ) === false )
		{
			exit;
		}
		
		$order_by_field = $_POST['order_by'];//Заменили поле
		
		//Если сортировка по времени - это по двум колонкам
		if( $_POST['order_by'] == 'time' )
		{
			$order_to_query = "`hour` ".$order_by_asc_desc.", `minute` ".$order_by_asc_desc;
		}
		else
		{
			$order_to_query = "`".$order_by_field."` ".$order_by_asc_desc;//Переформировали строку в запросе
		}
	}
	
	
	
	$SQL = "SELECT * FROM `shop_docpart_pyprices_crontab` ORDER BY ".$order_to_query.";";//Все задания
	$binding_values = array();
	//Если передан ID прайс-листа, по которому интересуют задания по расписанию
	if( $_POST['price_id'] > 0 )
	{
		$SQL = "SELECT * FROM `shop_docpart_pyprices_crontab` WHERE `id` IN (SELECT `crontab_task_id` FROM `shop_docpart_pyprices_crontab_prices` WHERE `price_id` = ?) ORDER BY ".$order_to_query.";";
		
		$binding_values[] = $_POST['price_id'];
	}
	
	//Индикатор направления сортировки
	if($order_by_asc_desc == "ASC")
	{
		$asc_desc_indicator = "<i class=\"fas fa-sort-up\"></i>";
	}
	else
	{
		$asc_desc_indicator = "<i class=\"fas fa-sort-down\"></i>";
	}
	?>
	
	
	
	<div class="row">
		<div class="col-md-12" style="margin-bottom:7px;margin-left:5px;">
			<?php
			if( $_POST['price_id'] > 0 )
			{
				//Получаем название этого прайс-листа
				$price_query = $db_link->prepare("SELECT `name` FROM `shop_docpart_prices` WHERE `id` = ?;");
				$price_query->execute( array($_POST['price_id']) );
				$price_name = $price_query->fetchColumn();
				?>
				<strong><?php echo translate_str_by_id(2540); ?>:</strong> <?php echo translate_str_by_id(5354); ?> <?php echo $_POST['price_id']; ?> "<?php echo $price_name; ?>"
				<?php
			}
			else
			{
				?>
				<strong><?php echo translate_str_by_id(2540); ?>:</strong> <?php echo translate_str_by_id(5355); ?>
				<?php
			}
			?>
		</div>
	</div>
	
	
	
	<?php
	//Для информативности - если список заданий пуст, не будем выводить таблицу ниже
	ob_start();
	$is_some_records = false;//Флаг - есть ли какие-нибудь строки
	?>
	
	
	
	
	<div class="table-responsive">
		<table cellpadding="1" cellspacing="1" class="table table-condensed">
			<thead>
				<tr>
					<th><a href="javascript:void(0);" onclick="cron_tasks_list_open(<?php echo $_POST['price_id']; ?>, 'id', '<?php if($_POST['order_by']=='id' && $order_by_asc_desc=='ASC') echo "DESC"; else echo "ASC"; ?>');">ID<?php if($_POST['order_by']=='id') echo $asc_desc_indicator; ?></a></th>
					<th><a href="javascript:void(0);" onclick="cron_tasks_list_open(<?php echo $_POST['price_id']; ?>, 'active', '<?php if($_POST['order_by']=='active' && $order_by_asc_desc=='ASC') echo "DESC"; else echo "ASC"; ?>');"><?php echo translate_str_by_id(3285); ?><?php if($_POST['order_by']=='active') echo $asc_desc_indicator; ?></a></th>
					<th><?php echo translate_str_by_id(5356); ?></th>
					<th><a href="javascript:void(0);" onclick="cron_tasks_list_open(<?php echo $_POST['price_id']; ?>, 'time', '<?php if($_POST['order_by']=='time' && $order_by_asc_desc=='ASC') echo "DESC"; else echo "ASC"; ?>');"><?php echo translate_str_by_id(5357); ?><?php if($_POST['order_by']=='time') echo $asc_desc_indicator; ?></a></th>
					<th><?php echo translate_str_by_id(5358); ?></th>
					<th><?php echo translate_str_by_id(2113); ?></th>
				</tr>
			</thead>
			<tbody>
			<?php
			$cron_tasks_query = $db_link->prepare($SQL);
			$cron_tasks_query->execute( $binding_values );
			while( $cron_task = $cron_tasks_query->fetch() )
			{
				$is_some_records = true;//Флаг - есть ли какие-нибудь строки
				?>
				<tr>
					<td><?php echo $cron_task['id']; ?></td>
					
					<td>
					<?php
					if($cron_task['active'])
					{
						?>
						<i class="fas fa-check-circle" style="color:#62cb31;"></i>
						<?php
					}
					else
					{
						?>
						<i class="fas fa-stop-circle" style="color:#ea6557"></i>
						<?php
					}
					?>
					</td>
					
					<td>
					<?php
					$day_week = explode(",", $cron_task['day_week']);
					for( $i = 0 ; $i < count($day_week) ; $i++ )
					{
						if( $i > 0 )
						{
							echo ", ";
						}
						echo $days[$day_week[$i]];
					}
					?>
					</td>
					
					<td>
					<?php
					if( strlen($cron_task['hour']) == 1 )
					{
						$cron_task['hour'] = "0".$cron_task['hour'];
					}
					if( strlen($cron_task['minute']) == 1 )
					{
						$cron_task['minute'] = "0".$cron_task['minute'];
					}
					echo $cron_task['hour'].":".$cron_task['minute'];
					?>
					</td>
					
					<td>
					<?php
					$prices_str = "";
					$prices_query = $db_link->prepare("SELECT * FROM `shop_docpart_prices` WHERE `id` IN (SELECT `price_id` FROM `shop_docpart_pyprices_crontab_prices` WHERE `crontab_task_id` = ?);");
					$prices_query->execute( array($cron_task['id']) );
					while( $price = $prices_query->fetch() )
					{
						if( $prices_str != "" )
						{
							$prices_str .= "<br>";
						}
						$prices_str .= "ID ".$price['id']." ".$price['name'];
					}
					echo $prices_str;
					?>
					</td>
					
					<td>
					<i class="fas fa-pen" title="<?php echo translate_str_by_id(5366); ?>" style="cursor:pointer;" onclick="cron_task_open(0, <?php echo $cron_task['id']; ?>);"></i>
					
					&nbsp;&nbsp;&nbsp;&nbsp;
					
					<i class="fas fa-trash-alt" title="<?php echo translate_str_by_id(5367); ?>" style="cursor:pointer;" onclick="delete_cron_task(<?php echo $cron_task['id']; ?>, <?php echo $_POST['price_id']; ?>, '<?php echo $order_by_field; ?>', '<?php echo $order_by_asc_desc; ?>');"></i>
					</td>
					
				</tr>
				<?php
			}
			?>
			</tbody>
        </table>
    </div>
	<?php
	
	if( $is_some_records )
	{
		ob_end_flush();
	}
	else
	{
		ob_end_clean();
		?>
		<div class="row">
			<div class="col-md-12" style="margin-bottom:7px;margin-left:5px;">
			<?php echo translate_str_by_id(5368); ?>
			</div>
		</div>
		<?php
	}
}
else if( isset( $_POST['action'] ) && $_POST['action'] == 'get_cron_tasks_count_by_prices' )
{
	//Нужно вернуть массив с указанием количества заданий по расписанию по всем прайс-листам
	$count_array = array();
	
	$count_query = $db_link->prepare("SELECT *, (SELECT COUNT(*) FROM `shop_docpart_pyprices_crontab_prices` WHERE `price_id` = `shop_docpart_prices`.`id`) AS `cron_tasks_count` FROM `shop_docpart_prices` WHERE `load_mode` != ?;");
	$count_query->execute( array(1) );
	while( $count_record = $count_query->fetch() )
	{
		$count_array[] = array("price_id"=>$count_record['id'], "cron_tasks_count"=>$count_record['cron_tasks_count'] );
	}
	
	$answer = array();
	$answer["status"] = true;
	$answer['count_array'] = $count_array;
	$answer["message"] = 'OK';
	exit(json_encode($answer));
}
?>