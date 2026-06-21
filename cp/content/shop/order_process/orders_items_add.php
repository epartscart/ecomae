<?php
/**
 * Страница для добавления позиции в заказ
*/
defined('_ASTEXE_') or die('No access');
ini_set("display_errors",0);
// Функция возвращает email пользователя с переданным user_id
function get_email_user($user_id){
	global $db_link, $DP_Config;
	
	$query = $db_link->prepare("SELECT `email` FROM `users` WHERE `user_id` = ?;");
	$query->execute( array($user_id) );
	$record = $query->fetch();
	$email = $record["email"];
	return $email;
}

//Для работы с пользователями
require_once($_SERVER["DOCUMENT_ROOT"]."/content/users/dp_user.php");
//Технические данные для работы с заказами
require_once($_SERVER["DOCUMENT_ROOT"]."/".$DP_Config->backend_dir."/content/shop/order_process/orders_background.php");

$AdminId = (int) DP_User::getAdminId();

if(!empty($_POST["save_action"]))
{
	// -------------------------------------------------------------------------------
	//Защита от CSRF-атак
	require_once($_SERVER["DOCUMENT_ROOT"]."/content/users/stop_csrf.php");
	// -------------------------------------------------------------------------------
	
	if($_POST["save_action"] != 'insert')
	{
		$error_message = translate_str_by_id(2304);
	}
	else
	{
		$order_id = (int)$_POST['order_id'];
		
		
		//Первым делом проверяем состояние оплаты. Нельзя добавлять позиции в заказы, которые Оплачены или частично оплачены.
		$check_paid_query = $db_link->prepare('SELECT `paid` FROM `shop_orders` WHERE `id` = ?;');
		$check_paid_query->execute( array($order_id) );
		$check_paid = $check_paid_query->fetch();
		if( $check_paid['paid'] != 0 )
		{
			$location_url = '/'.$DP_Config->backend_dir.'/shop/orders/order?order_id='.$order_id;
			?>
			<script>
				location="<?=$location_url?>&error_message=<?php echo urlencode(translate_str_by_id(3620)); ?>";
			</script>
			<?php
			exit;
		}
		
		
		$product_type = (int)$_POST['item_product_type'];
		$article = htmlentities($_POST['art']);
		$manufacturer = htmlentities($_POST['man']);
		$name = htmlentities($_POST['name']);
		
		$storage_id = (int)$_POST['storage_id'];
		$user_id = (int)$_POST['user_id'];
		
		$count_need = (int)$_POST['count_need'];
		$price = (float)$_POST['price'];//$t2_price_purchase + (($t2_price_purchase / 100) * ((float)$_POST['markup'])); 
		$price_zakup = (float)$_POST['price_zakup'];
		
		$time_to_exe = (int)$_POST['time_to_exe'];
		$time_to_exe_guaranteed = (int)$_POST['time_to_exe_guaranteed'];
		
		$office_id = (int)$_POST['office_id'];
		
		/******************/
		
		$product_id = 0;
		$t2_exist = 100;
		
		//Получаем статус позиции заказа, который присваивается для вновь-созданной позиции
		$for_created_status_query = $db_link->prepare("SELECT `id` FROM `shop_orders_items_statuses_ref` WHERE `for_created`=1;");
		$for_created_status_query->execute();
		$for_created_status_record = $for_created_status_query->fetch();
		$for_created_status = $for_created_status_record["id"];
		
		/******************/
		
		
		 $SQL_INSERT_ORDER_ITEM = "INSERT INTO `shop_orders_items` 
		(`order_id`, 
		`product_type`, 
		`price`, 
		`count_need`, 
		`product_id`, 
		`status`,
		`t2_manufacturer`,
		`t2_article`,
		`t2_article_show`,
		`t2_name`,
		`t2_exist`,
		`t2_time_to_exe`,
		`t2_time_to_exe_guaranteed`,
		`t2_storage`,
		`t2_min_order`,
		`t2_probability`,
		`t2_markup`,
		`t2_price_purchase`,
		`t2_office_id`,
		`t2_storage_id`,
		`t2_product_json`,
		`sao_state`,
		`sao_robot`,
		`t2_json_params`
		) 
		VALUES 
		(?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?);";
		
		$binding_values = array($order_id, $product_type, $price, $count_need, $product_id, $for_created_status,$manufacturer,$article,$article,$name,$t2_exist,$time_to_exe,$time_to_exe_guaranteed,'',1,100,0,$price_zakup,$office_id,$storage_id,'',0,0,'');
		

		if( $db_link->prepare($SQL_INSERT_ORDER_ITEM)->execute($binding_values) != true)
		{
			$error_message = translate_str_by_id(2122).": <br/> ".translate_str_by_id(3621);
		}
		else
		{
			$success_message = translate_str_by_id(2300);
		}
	}
	
	$location_url = '/'.$DP_Config->backend_dir.'/shop/orders/order?order_id='.$order_id;
	?>
	<script>
		location="<?=$location_url?>&error_message=<?=$error_message;?>&success_message=<?=$success_message;?>";
	</script>
	<?php
	exit;
}
else
{
	//Для работы с пользователем
	require_once( $_SERVER['DOCUMENT_ROOT']."/content/users/dp_user.php" );
	$user_session = DP_User::getAdminSession();
	
	$order_id = (int)$_GET['id'];
	
	
	//Первым делом проверяем состояние оплаты. Нельзя добавлять позиции в заказы, которые Оплачены или частично оплачены.
	$check_paid_query = $db_link->prepare('SELECT `paid` FROM `shop_orders` WHERE `id` = ?;');
	$check_paid_query->execute( array($order_id) );
	$check_paid = $check_paid_query->fetch();
	if( $check_paid['paid'] != 0 )
	{
		$_GET["warning_message"] = translate_str_by_id(3620);
	}
	
	
	
	require_once("content/control/actions_alert.php");//Вывод сообщений о результатах действий
	?>
	<div class="col-lg-12">
		<div class="hpanel">
			<div class="panel-heading hbuilt">
				<?php echo translate_str_by_id(2113); ?>
			</div>
			<div class="panel-body">
				<a class="panel_a" href="javascript:void(0);" onclick="save_action();">
					<div class="panel_a_img" style="background: url('/<?php echo $DP_Config->backend_dir; ?>/templates/bootstrap_admin/images/save.png') 0 0 no-repeat;"></div>
					<div class="panel_a_caption"><?php echo translate_str_by_id(2114); ?></div>
				</a>
				<a class="panel_a" href="/<?php echo $DP_Config->backend_dir; ?>/shop/orders/items">
					<div class="panel_a_img" style="background: url('/<?php echo $DP_Config->backend_dir; ?>/templates/bootstrap_admin/images/orders_items.png') 0 0 no-repeat;"></div>
					<div class="panel_a_caption"><?php echo translate_str_by_id(3622); ?></div>
				</a>
				<a id="order_id_a" class="panel_a" href="">
					<div class="panel_a_img" style="background: url('/<?php echo $DP_Config->backend_dir; ?>/templates/bootstrap_admin/images/store.png') 0 0 no-repeat;"></div>
					<div class="panel_a_caption"><?php echo translate_str_by_id(3623); ?></div>
				</a>
			</div>
		</div>
	</div>
	<div class="col-lg-12">
	<div class="table-responsive">
	<div class="hpanel">
	<div class="panel-heading hbuilt">
		<?php echo translate_str_by_id(2627); ?>
	</div>
	<div class="panel-body">

	<div style="padding:20px 0px;">
		<label><?php echo translate_str_by_id(2071); ?>:</label><br/>
		<input style="width:500px;" id="art_item_inp" type="text" value="" class="form-control" />
		<br/><label><?php echo translate_str_by_id(2070); ?>:</label><br/>
		<input style="width:500px;" id="man_item_inp" type="text" value="" class="form-control" /><br/>
		<label><?php echo translate_str_by_id(2102); ?>:</label><br/>
		<input style="width:500px;" id="name_item_inp" type="text" value="" class="form-control" />
		
	</div>




	<table>
		<tr>
			<td>
			<div class="row">
				<div class="col-lg-12">
					<table cellpadding="1" cellspacing="1" class="table table-condensed table-striped table-bordered">
						<thead>
							<th><?php echo translate_str_by_id(2750); ?></th>
							<th><?php echo translate_str_by_id(2751); ?></th>
							<th><?php echo translate_str_by_id(3624); ?></th>
							<th><?php echo translate_str_by_id(2752); ?></th>
							<th><?php echo translate_str_by_id(3625); ?></th>
							<th><?php echo translate_str_by_id(3626); ?></th>
						</thead>
						<tbody>
						
							<tr>
								<td>
									<select id="inp_storage_id" class="form-control">
								<?php 
									foreach($storages_list as $k => $v){
										echo '<option value="'.$k.'">'.$v.' ('.$k.')</option>';
									}
								?>
									</select>
								<td><input class="form-control" type="text" id="inp_price" value="<?php echo number_format(0, 2, '.', ''); ?>" /> </td>
								<td><input class="form-control" type="text" id="inp_price_zakup" value="<?php echo number_format(0, 2, '.', ''); ?>" /> </td>
								<td><input class="form-control" type="text" id="inp_count_need" value="1" /></td>
								
								
								<td><input class="form-control" id="time_to_exe_item_inp" type="text" value="1"/></td>
								<td><input class="form-control" id="time_to_exe_guaranteed_item_inp" type="text" value="5"/></td>
								
							</tr>
						
							
						</tbody>
					</table>
				</div>
			</div>
			</td>
		</tr>
	</table>


	<?php
	// Получаем id пользователя из заказа
	$sql = 'SELECT `user_id`, `office_id` FROM `shop_orders` WHERE `id` = ?;';
	
	$query = $db_link->prepare($sql);
	$query->execute( array($order_id) );
	$record = $query->fetch();
	$customer_id = $record['user_id'];
	$office_id = $record['office_id'];
	?>

	<form id="save_form" name="save_form" method="POST" style="display:none;">
		<input type="hidden" name="save_action" id="save_action" value="insert" />
		<input type="hidden" id="order_id" name="order_id" value="<?=$order_id;?>" />
		<input type="hidden" id="price" name="price" value="" />
		<input type="hidden" id="price_zakup" name="price_zakup" value="" />
		<input type="hidden" id="count_need" name="count_need" value="" />
		<input type="hidden" id="user_id" name="user_id" value="<?=$customer_id;?>" />
		<input type="hidden" id="storage_id" name="storage_id" value="" />
		<input type="hidden" id="art" name="art" value="" />
		<input type="hidden" id="man" name="man" value="" />
		<input type="hidden" id="name" name="name" value="" />
		<input type="hidden" id="item_product_type" name="item_product_type" value="2" />
		
		<input type="hidden" id="time_to_exe" name="time_to_exe" value="" />
		<input type="hidden" id="time_to_exe_guaranteed" name="time_to_exe_guaranteed" value="" />
		
		<input type="hidden" id="office_id" name="office_id" value="<?=$office_id;?>" />
		<input type="hidden" name="csrf_guard_key" value="<?php echo $user_session["csrf_guard_key"]; ?>" />
	</form>
	<script>
	//Функция сохранения (отправка формы)

	document.getElementById('order_id_a').href = '/<?php echo $DP_Config->backend_dir; ?>/shop/orders/order?order_id=<?=$order_id;?>';
	function save_action(){
		document.getElementById('price').value = document.getElementById('inp_price').value;
		document.getElementById('price_zakup').value = document.getElementById('inp_price_zakup').value;
		document.getElementById('count_need').value = document.getElementById('inp_count_need').value;

		document.getElementById('storage_id').value = document.getElementById('inp_storage_id').value;
		
		document.getElementById('art').value = document.getElementById('art_item_inp').value;
		document.getElementById('man').value = document.getElementById('man_item_inp').value;
		document.getElementById('name').value = document.getElementById('name_item_inp').value;
		
		document.getElementById('time_to_exe').value = document.getElementById('time_to_exe_item_inp').value;
		document.getElementById('time_to_exe_guaranteed').value = document.getElementById('time_to_exe_guaranteed_item_inp').value;
		
		document.forms["save_form"].submit();
	}
	</script>
	</div>
	</div>
	</div>
	</div>
<?php
}
?>