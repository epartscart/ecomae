<?php
/*Страница баланса покупателя*/

defined('_ASTEXE_') or die('No access');

//Для работы с пользователем
require_once($_SERVER["DOCUMENT_ROOT"]."/content/users/dp_user.php");
$user_id = DP_User::getUserId();


//Магазины пользователя
require_once( $_SERVER['DOCUMENT_ROOT'].'/content/shop/order_process/get_customer_offices.php' );


//Для работы с пользователем
require_once( $_SERVER['DOCUMENT_ROOT']."/content/users/dp_user.php" );
$user_session = DP_User::getUserSession();

// -------------------------------------------------------------------------------------------------------------------------

// формируем пагинацию (определение функции)
// $all 		= количество постов в категории (определяем количество постов в базе данных)
// $lim 		= количество постов, размещаемых на одной странице
// $prev 		= количество отображаемых ссылок до и после номера текущей страницы
// $curr_link 	= номер текущей страницы (получаем из URL)
// $curr_css 	= css-стиль для ссылки на "текущую (активную)" страницу
// $link 		= часть адреса, используемый для формирования линков на другие страницы
function pagination($all, $lim, $prev, $curr_link, $curr_css, $link='')
{
    global $DP_Content;
    global $multilang_params;
	
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
    if ($first > 1) $html .= "<li><a href='".$multilang_params['lang_href']."/{$DP_Content->url}'>1</a></li>";
    // Если текущая страница далеко от 1-й (>10), то часть предыдущих страниц
    // скрываем троеточием
    // Если текущая страница имеет номер до 10, то выводим все номера
    // перед заданным диапазоном без скрытия
	// $prev
    $y = $first - 1;
    if ($first > $prev) {
        $html .= "<li><a href='".$multilang_params['lang_href']."/{$DP_Content->url}?page={$y}' >...</a></li>";
    } else {
        for($i = 2;$i < $first;$i++){
            $html .=  "<li><a href='".$multilang_params['lang_href']."/{$DP_Content->url}?page={$y}' >$i</a></li>";
        }
    }
    // отображаем заданный диапазон: текущая страница +-$prev
    for($i = $first;$i < $last + 1;$i++){
        // если выводится текущая страница, то ей назначается особый стиль css
        if($i == $curr_link) {
			$html .= '<li class="'.$curr_css.'"><a>'. $i .'</a></li>';
        } else {
            $alink = "<li><a href='".$multilang_params['lang_href']."/{$DP_Content->url}";
            if($i != 1) $alink .= "?page={$i}";
            $alink .= "'>$i</a></li>";
            $html .= $alink;
        }
    }
    $y = $last + 1;
    // часть страниц скрываем троеточием
    if ($last < ceil($all / $lim) && ceil($all / $lim) - $last > 2) $html .=  "<li><a href='".$multilang_params['lang_href']."/{$DP_Content->url}?page={$y}' >...</a></li>";
    // выводим последнюю страницу
    $e = ceil($all / $lim);
    if ($last < ceil($all / $lim)) $html .=  "<li><a href='".$multilang_params['lang_href']."/{$DP_Content->url}?page={$e}' >$e</a></li>";
	
	return $html;
}


// -------------------------------------------------------------------------------------------------------------------------



if($user_id > 0)
{
	require_once($_SERVER["DOCUMENT_ROOT"]."/content/general/actions_alert.php");//Вывод сообщений о результатах выполнения действий
	
	$time_from = "";//1. Время с
	$time_to = "";//2. Время по
	$operation_code = -1;//3. Код операции
	$income = -1;//4. Напрвление операции
	$office_id = -1;//5. Магазин
	$order_id = "";//6. Заказ (привязка операции к заказу)
	
	//Получаем текущие значения фильтра:
	$account_operations_filter = NULL;
	if( isset($_COOKIE["my_account_operations_filter"]) )
	{
		$account_operations_filter = $_COOKIE["my_account_operations_filter"];
	}
	if($account_operations_filter != NULL)
	{
		$account_operations_filter = json_decode($account_operations_filter, true);
		$time_from = $account_operations_filter["time_from"];
		$time_to = $account_operations_filter["time_to"];
		$operation_code = $account_operations_filter["operation_code"];
		$income = $account_operations_filter["income"];
		$order_id = $account_operations_filter["order_id"];
		
		if( isset($DP_Config->wholesaler) )
		{
			if( isset($account_operations_filter["office_id"]) )
			{
				$office_id = $account_operations_filter["office_id"];
			}
		}
	}
	?>
	
	
	<div class="panel panel-primary">
		<div class="panel-heading"><?php echo translate_str_by_id(4338); ?></div>
		<div class="panel-body">
			
			
			<?php
			$col_value = '12';
			if( isset($DP_Config->wholesaler) )
			{
				$col_value = '6';
				?>
				<div class="col-md-6">
					<div>
						<label><?php echo translate_str_by_id(4339); ?></label>
					</div>
					<div>
						<?php
						if( count($customer_offices) == 0 )
						{
							?>
							<?php echo translate_str_by_id(4340); ?>
							<input type="hidden" id="office_select" value="0" />
							<?php
						}
						else
						{
							?>
							<select class="form-control" id="office_select">
							<?php
							$customer_offices_query = $db_link->prepare("SELECT * FROM `shop_offices` WHERE `id` IN (".str_repeat('?,', count($customer_offices)-1 ).'?);' );
							$customer_offices_query->execute( $customer_offices );
							while( $office = $customer_offices_query->fetch() )
							{
								?>
								<option value="<?php echo $office['id']; ?>"><?php echo translate_str_by_id($office['caption']).', '.translate_str_by_id($office['city']).', '.translate_str_by_id($office['address']).'. '.$office['phone']; ?></option>
								<?php
							}
							?>
							</select>
							<?php
						}
						?>
					</div>
				</div>
				<?php
			}
			?>
			
			
			<div class="col-md-<?php echo $col_value; ?>">
				<div>
					<label for="money_value"><?php echo translate_str_by_id(3251); ?></label>
				</div>
				<div>
					<input type="number" class="form-control" id="money_value" placeholder="<?php echo translate_str_by_id(3251); ?>" />
				</div>
			</div>

			
			<div class="col-md-12">
				<?php require $_SERVER['DOCUMENT_ROOT'] . '/content/shop/payments/epc_payment_method_picker.php'; ?>
				<a class="btn btn-ar btn-primary" style="margin-top:7px;" href="javascript:void(0);" onclick="onIncomeButtonClicked()"><?php echo translate_str_by_id(4341); ?></a>
			</div>
		</div>
	</div>
	
	<script>
	function onIncomeButtonClicked()
	{
		//Сумма из поля ввода
		var pay_value = document.getElementById('money_value').value;						
		pay_value = parseFloat(pay_value).toFixed(2);
		
		//Локальные проверки:
		
		//1. Должна быть указана сумма
		if( pay_value == '' || pay_value == 'NaN' )
		{
			alert('<?php echo translate_str_by_id(3526); ?>');
			return;
		}
		//2. Сумма не должна быть отрицательной, не должна быть равна 0
		if( pay_value <= 0 )
		{
			alert('<?php echo translate_str_by_id(3275); ?>');
			return;
		}
		
		
		var request_object = new Object;
		request_object.amount = pay_value;
		request_object.office_id = 0;
		if (typeof window.epcSelectedPayHandler === 'function') {
			request_object.pay_handler = window.epcSelectedPayHandler();
		}
		
		
		<?php
		if( isset($DP_Config->wholesaler) )
		{
			?>			
			var office_id = document.getElementById('office_select').value;
			if( parseInt(office_id) == 0 )
			{
				alert('<?php echo translate_str_by_id(4342); ?>');
				return;
			}
			request_object.office_id = office_id;
			<?php
		}
		?>
		
		
		
		jQuery.ajax({
			type: "POST",
			async: false, //Запрос синхронный
			url: "/content/shop/finance/ajax_create_operation.php",
			dataType: "text",//Тип возвращаемого значения
			data: "request_object="+encodeURI(JSON.stringify(request_object))+"&csrf_guard_key=<?php echo $user_session["csrf_guard_key"]; ?>",
			success: function(answer)
			{
				console.log(answer);
				
				var answer_ob = JSON.parse(answer);
				
				if( typeof answer_ob.result == 'undefined' )
				{
					alert("<?php echo translate_str_by_id(4343); ?>");
				}
				else
				{
					if(answer_ob.result == true)
					{					
						if( answer_ob.pay_system == 0 )
						{
							alert("<?php echo translate_str_by_id(4344); ?>");
							return;
						}
						else
						{
							location = "/content/shop/finance/payment_systems/"+answer_ob.pay_system+"/go_to_pay.php?operation="+answer_ob.operation+"&csrf_guard_key=<?php echo $user_session["csrf_guard_key"]; ?>";
						}
					}
					else
					{
						alert("<?php echo translate_str_by_id(4345); ?>");
					}
				}
			}
		});
	}

	</script>
	<br>
	
	
	<p class="lead"><?php echo translate_str_by_id(4346); ?></p>
	
	<div class="row">
			<div class="col-md-2">
				<div>
					<label style="margin-bottom: 0;"><?php echo translate_str_by_id(3237); ?></label>
				</div>
				<div style="position: relative; height: 36px;">
					<input style="position:absolute; z-index:2; opacity:0;width:100%;" type="text" autocomplete="off" id="time_from" value="<?php echo $time_from; ?>" class="form-control" />
					<input style=" <?=($time_from !== '')?'background:#b9fcab;':'';?> position:absolute; z-index:1;width:100%;" autocomplete="off" type="text" id="time_from_show" class="form-control" />
					<script>
					//Инициализируем datetimepicker
					jQuery("#time_from").datetimepicker({
						lang:"ru",
						closeOnDateSelect:true,
						closeOnTimeSelect:false,
						dayOfWeekStart:1,
						format:'unixtime',
						onClose:function(current_time, input)//При закрытии datetimepicker - отображаем в поле индикации
						{
							var time_string = "";
							var date_ob = new Date(current_time);
							time_string += date_ob.getDate()+".";
							time_string += (date_ob.getMonth() + 1)+".";
							time_string += date_ob.getFullYear()+" ";
							time_string += date_ob.getHours()+":"+date_ob.getMinutes();
							document.getElementById("time_from_show").value = time_string;//Показываем время в понятном виде
						}
						<?php
						if($time_from != "")
						{
							?>
							,
							onGenerate:function(current_time, input)//При закрытии datetimepicker - отображаем в поле индикации
							{
								var time_string = "";
								var date_ob = new Date(current_time);
								time_string += date_ob.getDate()+".";
								time_string += (date_ob.getMonth() + 1)+".";
								time_string += date_ob.getFullYear()+" ";
								time_string += date_ob.getHours()+":"+date_ob.getMinutes();
								document.getElementById("time_from_show").value = time_string;//Показываем время в понятном виде
							}
							<?php
						}
						?>
					});
					</script>
				</div>
			</div>
			
			
			
			<div class="col-md-2">
				<div>
					<label style="margin-bottom: 0;"><?php echo translate_str_by_id(3238); ?></label>
				</div>
				<div style="position: relative; height: 36px;">
					<input style="position:absolute; z-index:2; opacity:0;width:100%;" type="text" autocomplete="off" id="time_to" value="<?php echo $time_to; ?>" class="form-control" />
					<input style=" <?=($time_to !== '')?'background:#b9fcab;':'';?> position:absolute; z-index:1;width:100%;" autocomplete="off" type="text" id="time_to_show" class="form-control" />
					<script>
					//Инициализируем datetimepicker
					jQuery("#time_to").datetimepicker({
						lang:"ru",
						closeOnDateSelect:true,
						closeOnTimeSelect:false,
						dayOfWeekStart:1,
						format:'unixtime',
						onClose:function(current_time, input)//При закрытии datetimepicker - отображаем в поле индикации
						{
							var time_string = "";
							var date_ob = new Date(current_time);
							time_string += date_ob.getDate()+".";
							time_string += (date_ob.getMonth() + 1)+".";
							time_string += date_ob.getFullYear()+" ";
							time_string += date_ob.getHours()+":"+date_ob.getMinutes();
							document.getElementById("time_to_show").value = time_string;//Показываем время в понятном виде
						}
						<?php
						if($time_to != "")
						{
							?>
							,
							onGenerate:function(current_time, input)//При закрытии datetimepicker - отображаем в поле индикации
							{
								var time_string = "";
								var date_ob = new Date(current_time);
								time_string += date_ob.getDate()+".";
								time_string += (date_ob.getMonth() + 1)+".";
								time_string += date_ob.getFullYear()+" ";
								time_string += date_ob.getHours()+":"+date_ob.getMinutes();
								document.getElementById("time_to_show").value = time_string;//Показываем время в понятном виде
							}
							<?php
						}
						?>
					});
					</script>
				</div>
			</div>
			
			
			
			
			<div class="col-md-2">
				<div>
					<label style="margin-bottom: 0;"><?php echo translate_str_by_id(3239); ?></label>
				</div>
				<div>
					<select <?=((int)$income !== -1)?'style="background:#b9fcab;"':'';?> id="income" class="form-control">
						<option value="-1"><?php echo translate_str_by_id(2094); ?></option>
						<option value="1"><?php echo translate_str_by_id(3240); ?></option>
						<option value="0"><?php echo translate_str_by_id(3241); ?></option>
					</select>
					<script>
						document.getElementById("income").value = <?php echo $income; ?>;
					</script>
				</div>
			</div>
			
			
			
			
			
			<div class="col-md-2">
				<div>
					<label style="margin-bottom: 0;"><?php echo translate_str_by_id(3252); ?></label>
				</div>
				<div>
					<select <?=((int)$operation_code !== -1)?'style="background:#b9fcab;"':'';?> id="operation_code" class="form-control">
						<option value="-1"><?php echo translate_str_by_id(2094); ?></option>
					<?php
					$accounting_codes_query = $db_link->prepare('SELECT * FROM `shop_accounting_codes`;');
					$accounting_codes_query->execute();
					while($accounting_code = $accounting_codes_query->fetch() )
					{
						$selected = "";
						if($operation_code == $accounting_code["id"])
						{
							$selected = "selected=\"selected\"";
						}
						
						?>
						<option value="<?php echo $accounting_code["id"]; ?>" <?php echo $selected; ?>><?php echo translate_str_by_id($accounting_code["name"]); ?></option>
						<?php
					}
					?>
					</select>
				</div>
			</div>
			
			
			
			
			
			<div class="col-md-2">
				<div>
					<label style="margin-bottom: 0;"><?php echo translate_str_by_id(3243); ?></label>
				</div>
				<div>
					<input type="text" id="order_id" value="<?php echo $order_id; ?>" class="form-control" placeholder="<?php echo translate_str_by_id(3244); ?>" />
				</div>
			</div>
			
			
			
			
			<?php
			if( isset( $DP_Config->wholesaler ) )
			{
				?>
				
				<div class="col-md-2">
					<div>
						<label style="margin-bottom: 0;"><?php echo translate_str_by_id(3248); ?></label>
					</div>
					<div>
						<select id="office_id" class="form-control">
							<option value="-1"><?php echo translate_str_by_id(2094); ?></option>
							<?php
							//Показываем все магазины, на которые были платежи от этого покупателя
							$customer_offices_query = $db_link->prepare("SELECT * FROM `shop_offices` WHERE `id` IN ( SELECT `office_id` FROM `shop_users_accounting` WHERE `user_id` = ? );" );
							$customer_offices_query->execute( array( $user_id ) );
							while( $office = $customer_offices_query->fetch() )
							{
								?>
								<option value="<?php echo $office['id']; ?>"><?php echo translate_str_by_id($office['caption']).', '.translate_str_by_id($office['city']).', '.translate_str_by_id($office['address']).'. '.$office['phone']; ?></option>
								<?php
							}
							?>
						</select>
						<script>
							document.getElementById("office_id").value = <?php echo $office_id; ?>;
						</script>
					</div>
				</div>
				
				<?php
			}
			?>
			
			
			
		</div>
		
		<div style="margin:20px 0px 15px;">
			<button style="margin-right: 2px; margin-bottom:5px;" class="btn btn-ar btn-primary" onclick="filterOperations();"><?php echo translate_str_by_id(2232); ?></button>
			<button style="margin-right: 2px; margin-bottom:5px;" class="btn btn-ar btn-primary" onclick="unsetFilterOperations();"><?php echo translate_str_by_id(2555); ?></button>
		</div>
	<script>
	// ------------------------------------------------------------------------------------------------
	//Устновка cookie в соответствии с фильтром
	function filterOperations()
	{
		var account_operations_filter = new Object;
		
		account_operations_filter.time_from = document.getElementById("time_from").value;
		account_operations_filter.time_to = document.getElementById("time_to").value;
		account_operations_filter.income = document.getElementById("income").value;
		account_operations_filter.operation_code = document.getElementById("operation_code").value;
		account_operations_filter.order_id = document.getElementById("order_id").value;
		<?php
		if( isset( $DP_Config->wholesaler ) )
		{
			?>
			account_operations_filter.office_id = document.getElementById("office_id").value;
			<?php
		}
		?>
		
		
		//Устанавливаем cookie (на полгода)
		var date = new Date(new Date().getTime() + 15552000 * 1000);
		document.cookie = "my_account_operations_filter="+JSON.stringify(account_operations_filter)+"; path=/; expires=" + date.toUTCString();
		
		//Обновляем страницу
		location='<?php echo $multilang_params['lang_href']; ?>/shop/balans';
	}
	// ------------------------------------------------------------------------------------------------
	//Снять все фильтры
	function unsetFilterOperations()
	{
		var account_operations_filter = new Object;

		account_operations_filter.time_from = "";
		account_operations_filter.time_to = "";
		account_operations_filter.income = -1;
		account_operations_filter.operation_code = -1;
		account_operations_filter.order_id = "";
		<?php
		if( isset( $DP_Config->wholesaler ) )
		{
			?>
			account_operations_filter.office_id = -1;
			<?php
		}
		?>
		
		//Устанавливаем cookie (на полгода)
		var date = new Date(new Date().getTime() + 15552000 * 1000);
		document.cookie = "my_account_operations_filter="+JSON.stringify(account_operations_filter)+"; path=/; expires=" + date.toUTCString();
		
		//Обновляем страницу
		location='<?php echo $multilang_params['lang_href']; ?>/shop/balans';
	}
	// ------------------------------------------------------------------------------------------------
	</script>
	
	
	
	
	
	
	
	
	
	
	<script>
	// ------------------------------------------------------------------------------------------------
	//Установка куки сортировки
	function sortOperationsItems(field)
	{
		var asc_desc = "asc";//Направление по умолчанию
		
		//Берем из куки текущий вариант сортировки
		var current_sort_cookie = getCookie("my_account_operations_sort");
		if(current_sort_cookie != undefined)
		{
			current_sort_cookie = JSON.parse(getCookie("my_account_operations_sort"));
			//Если поле это же - обращаем направление
			if(current_sort_cookie.field == field)
			{
				if(current_sort_cookie.asc_desc == "asc")
				{
					asc_desc = "desc";
				}
				else
				{
					asc_desc = "asc";
				}
			}
		}
		
		
		var account_operations_sort = new Object;
		account_operations_sort.field = field;//Поле, по которому сортировать
		account_operations_sort.asc_desc = asc_desc;//Направление сортировки
		
		//Устанавливаем cookie (на полгода)
		var date = new Date(new Date().getTime() + 15552000 * 1000);
		document.cookie = "my_account_operations_sort="+JSON.stringify(account_operations_sort)+"; path=/; expires=" + date.toUTCString();
		
		//Обновляем страницу
		location='<?php echo $multilang_params['lang_href']; ?>/shop/balans';
	}
	// ------------------------------------------------------------------------------------------------
	// возвращает cookie с именем name, если есть, если нет, то undefined
	function getCookie(name) 
	{
		var matches = document.cookie.match(new RegExp(
			"(?:^|; )" + name.replace(/([\.$?*|{}\(\)\[\]\\\/\+^])/g, '\\$1') + "=([^;]*)"
		));
		return matches ? decodeURIComponent(matches[1]) : undefined;
	}
	// ------------------------------------------------------------------------------------------------
	</script>
	
	<div style="overflow: hidden; overflow-x: auto;">
	<table class="table" style="margin-bottom:0;">
	<tr>
		<th style="vertical-align: middle; white-space: nowrap;"><a href="javascript:void(0);" onclick="sortOperationsItems('id');" id="id_sorter">ID</a></th>
		<th style="vertical-align: middle; white-space: nowrap;"><a href="javascript:void(0);" onclick="sortOperationsItems('time');" id="time_sorter"><?php echo translate_str_by_id(3250); ?></a></th>
		<th style="vertical-align: middle; white-space: nowrap;"><a href="javascript:void(0);" onclick="sortOperationsItems('amount');" id="amount_sorter"><?php echo translate_str_by_id(3251); ?></a></th>
		<th style="vertical-align: middle; white-space: nowrap;"><a href="javascript:void(0);" onclick="sortOperationsItems('order_id');" id="order_id_sorter"><?php echo translate_str_by_id(3243); ?></a></th>
		<th style="vertical-align: middle;"><a href="javascript:void(0);" onclick="sortOperationsItems('name');" id="name_sorter"><?php echo translate_str_by_id(2102); ?></a></th>
		
		<?php
		if( isset( $DP_Config->wholesaler ) )
		{
			?>
			<th style="vertical-align: middle;"><a href="javascript:void(0);" onclick="sortOperationsItems('office_caption');" id="office_caption_sorter"><?php echo translate_str_by_id(3383); ?></a></th>
			<?php
		}
		?>
		
	</tr>
	<script>
		<?php
		//Определяем текущую сортировку и обозначаем ее:
		$account_operations_sort = NULL;
		if( isset( $_COOKIE["my_account_operations_sort"] ) )
		{
			$account_operations_sort = $_COOKIE["my_account_operations_sort"];
		}
		$sort_field = "id";
		$sort_asc_desc = "desc";
		if($account_operations_sort != NULL)
		{
			$account_operations_sort = json_decode($account_operations_sort, true);
			$sort_field = $account_operations_sort["field"];
			$sort_asc_desc = $account_operations_sort["asc_desc"];
		}
		
		//Защита от SQL-инъекций:
		if($sort_asc_desc == 'asc')
		{
			$sort_asc_desc = 'asc';
		}
		else
		{
			$sort_asc_desc = 'desc';
		}
		
		if( isset( $DP_Config->wholesaler ) )
		{
			if( array_search($sort_field, array('id','time','amount','name', 'office_caption', 'order_id') ) === false )
			{
				$sort_field = 'id';
			}
		}
		else
		{
			if( array_search($sort_field, array('id','time','amount','name', 'order_id') ) === false )
			{
				$sort_field = 'id';
			}
		}
		
		
		
		?>
		document.getElementById("<?php echo $sort_field; ?>_sorter").innerHTML += "<img src=\"/content/files/images/sort_<?php echo $sort_asc_desc; ?>.png\" style=\"width:15px; vertical-align: initial;\" />";
		<?php
		if($sort_field == "amount_income" || $sort_field == "amount_issue")
		{
			$sort_field = "amount";
		}
		?>
	</script>
	
	
	<?php
	$items_counter = 0;
	
	$binding_values_all = array();
	$binding_values_where_conditions = array();
	$binding_values_where_conditions_balance = array();
	
	
	
	$WHERE_CONDITIONS = ' `user_id` = ? AND `active`=1 ';
	array_push($binding_values_where_conditions, $user_id);
	
	$WHERE_CONDITIONS_BALANCE = ' `user_id` = ? AND `active`=1 ';//Отдельная строка для условия - для подсчета баланса. Здесь нет условий по полю income
	
	//Биндим два раза, т.к это строка WHERE_CONDITIONS_BALANCE используется в двух подзапросах.
	//array_push($binding_values_where_conditions, $user_id);
	//array_push($binding_values_where_conditions, $user_id);
	array_push($binding_values_where_conditions_balance, $user_id);
	
	
	//Ставим ПОЛЬЗОВАТЕЛЬСКИЕ фильтры
	$account_operations_filter = NULL;
	if( isset($_COOKIE["my_account_operations_filter"]) )
	{
		$account_operations_filter = $_COOKIE["my_account_operations_filter"];
	}
	if($account_operations_filter != NULL)
	{
		$account_operations_filter = json_decode($account_operations_filter, true);
		
		//1. Время с
		if($account_operations_filter["time_from"] != "")
		{
			$WHERE_CONDITIONS .= ' AND ';
			$WHERE_CONDITIONS .= ' `time` > ?';
			
			array_push($binding_values_where_conditions, (int) $account_operations_filter["time_from"]);
			
			$WHERE_CONDITIONS_BALANCE .= ' AND ';
			$WHERE_CONDITIONS_BALANCE .= ' `time` > ? ';
			
			array_push($binding_values_where_conditions_balance, (int) $account_operations_filter["time_from"]);
		}

		//2. Время по
		if($account_operations_filter["time_to"] != "")
		{
			$WHERE_CONDITIONS .= ' AND ';
			$WHERE_CONDITIONS .= ' `time` < ?';
			array_push($binding_values_where_conditions, (int) $account_operations_filter["time_to"]);
			
			$WHERE_CONDITIONS_BALANCE .= ' AND ';
			$WHERE_CONDITIONS_BALANCE .= ' `time` < ?';
			array_push($binding_values_where_conditions_balance, (int) $account_operations_filter["time_to"]);
		}

		//3. income
		if($account_operations_filter["income"] != "" && $account_operations_filter["income"] != -1)
		{
			$WHERE_CONDITIONS .= ' AND ';
			$WHERE_CONDITIONS .= ' `income` = ?';
			array_push($binding_values_where_conditions, (int) $account_operations_filter["income"]);
			
			
			$WHERE_CONDITIONS_BALANCE .= ' AND ';
			$WHERE_CONDITIONS_BALANCE .= ' `income` = ?';
			array_push($binding_values_where_conditions_balance, (int) $account_operations_filter["income"]);
		}
		
		//4. operation_code
		if($account_operations_filter["operation_code"] != 0 && $account_operations_filter["operation_code"] != -1)
		{
			$WHERE_CONDITIONS .= ' AND ';
			$WHERE_CONDITIONS .= ' `operation_code` = ?';
			
			array_push($binding_values_where_conditions, (int) $account_operations_filter["operation_code"]);
			
			
			$WHERE_CONDITIONS_BALANCE .= ' AND ';
			$WHERE_CONDITIONS_BALANCE .= ' `operation_code` = ?';
			
			array_push($binding_values_where_conditions_balance, (int) $account_operations_filter["operation_code"]);
		}
		
		
		
		//5. office_id
		if( isset( $account_operations_filter["office_id"] ) )
		{
			if($account_operations_filter["office_id"] != "" && $account_operations_filter["office_id"] != -1)
			{
				$WHERE_CONDITIONS .= ' AND ';
				$WHERE_CONDITIONS .= ' `office_id` = ?';
				
				array_push($binding_values_where_conditions, (int) $account_operations_filter["office_id"]);
				
				
				$WHERE_CONDITIONS_BALANCE .= ' AND ';
				$WHERE_CONDITIONS_BALANCE .= ' `office_id` = ?';
				
				array_push($binding_values_where_conditions_balance, (int) $account_operations_filter["office_id"]);
			}
		}
		
		
		
		//6. order_id
		if($account_operations_filter["order_id"] != "" && $account_operations_filter["order_id"] != 0)
		{
			$WHERE_CONDITIONS .= ' AND ';
			$WHERE_CONDITIONS .= ' `order_id` = ?';
			
			array_push($binding_values_where_conditions, (int) $account_operations_filter["order_id"]);
			
			
			$WHERE_CONDITIONS_BALANCE .= ' AND ';
			$WHERE_CONDITIONS_BALANCE .= ' `order_id` = ?';
			
			array_push($binding_values_where_conditions_balance, (int) $account_operations_filter["order_id"]);
		}
	}
	
	$WHERE_CONDITIONS = 'WHERE '.$WHERE_CONDITIONS;
	
	$WHERE_CONDITIONS_BALANCE_INCOME = 'WHERE '.$WHERE_CONDITIONS_BALANCE.' AND `income` = 1';
	$WHERE_CONDITIONS_BALANCE_ISSUE = 'WHERE '.$WHERE_CONDITIONS_BALANCE.' AND `income` = 0';
	
	$binding_values_where_conditions_balance = array_merge($binding_values_where_conditions_balance, $binding_values_where_conditions_balance);

	//Формируем запрос
	$SQL_operation_name = '(SELECT `name` FROM `shop_accounting_codes` WHERE `id` = `shop_users_accounting`.`operation_code`)';
	$SQL_operation_name_lang = '(SELECT `value` FROM `lang_text_strings_translation` WHERE `str_id` = (SELECT `name` FROM `shop_accounting_codes` WHERE `id` = `shop_users_accounting`.`operation_code`) AND `lang_code` = "'.$multilang_params['lang'].'")';
	
	//Подсчет сальдо
	$INCOME_SQL = 'IFNULL((SELECT SUM(`amount`) FROM `shop_users_accounting` '.$WHERE_CONDITIONS_BALANCE_INCOME.'), 0)';
	$ISSUE_SQL = 'IFNULL((SELECT SUM(`amount`) FROM `shop_users_accounting` '.$WHERE_CONDITIONS_BALANCE_ISSUE.'),0)';
	
	
	// Текущая страница
	$page = 1;
	if( isset($_GET['page']) )
	{
		$page = (int) $_GET['page'];
	}
	if(empty($page))
	{
		$page = 1;
	}
	$lim_rows = $DP_Config->list_page_limit;// Количество строк на страницу
	$from_rows = ($page * $lim_rows) - $lim_rows;// С какой записи выводить
	
	
	//Сортировка с учетом мультиязычности
	if( $sort_field == 'name' )
	{
		$sort_field = 'name_lang';
	}
	
	
	//Запрос записей
	$SQL_SELECT = 'SELECT SQL_CALC_FOUND_ROWS *, 
	'.$SQL_operation_name.' AS `name`, 
	'.$SQL_operation_name_lang.' AS `name_lang`, 
	('.$INCOME_SQL.'-'.$ISSUE_SQL.') AS `balance`,
	IFNULL( (SELECT CONCAT(`caption`, \', \', `city`, \', \', `address`, \', \', `phone`) FROM `shop_offices` WHERE `id` = `shop_users_accounting`.`office_id`), \''.translate_str_by_id(4348).'\' ) AS `office_caption`
	FROM `shop_users_accounting` '.$WHERE_CONDITIONS.' ORDER BY `'.$sort_field.'` '.$sort_asc_desc.' LIMIT '.$from_rows.', '.$lim_rows;
	
	$binding_values_all = array_merge($binding_values_where_conditions_balance, $binding_values_where_conditions);
	
	//echo $SQL_SELECT;
	
	$elements_query = $db_link->prepare($SQL_SELECT);
	$elements_query->execute($binding_values_all);
	
	$elements_count_rows_query = $db_link->prepare('SELECT FOUND_ROWS();');
	$elements_count_rows_query->execute();
	
	$all_rows = $elements_count_rows_query->fetchColumn();

	//----------------------------------------------------------------------------------------------|
	
	$items_counter = 0;
	$saldo = "no";
	while( $element_record = $elements_query->fetch() )
	{
		$items_counter++;
		
		//Получаем сальдо по данным условиям:
		if($saldo == "no")
		{
			$saldo = $element_record["balance"];
		}
	
		$css_sub_color = "";
		$plus_minus = "";
		if($element_record["income"] == 1)
		{
			$css_sub_color = "background-color:#b4fed4;";
			$plus_minus = "+";
		}
		else
		{
			$css_sub_color = "background-color:#ffe4e4;";
			$plus_minus = "-";
		}
		
		$id = $element_record["id"];
		$time = $element_record["time"];
		$amount = number_format($element_record["amount"], 2, '.', ' ');
		$name = $element_record["name"];
		$office_caption = $element_record["office_caption"];
		
		if( !empty( $element_record["order_id"] ) )
		{
			$name = $name.' ('.translate_str_by_id(3243).' ID '.$element_record["order_id"].')';
		}
		
	?>
		
		<tr style="<?php echo $css_sub_color; ?>" >
			<td style="vertical-align: middle; white-space: nowrap;"><?php echo $id; ?></td>
			<td style="vertical-align: middle; white-space: nowrap;"><?php echo date("d.m.Y", $time)."<br>".date("G:i", $time); ?></td>
			<td style="vertical-align: middle; white-space: nowrap;"><?php echo $plus_minus.$amount; ?></td>
			
			<td style="vertical-align: middle; white-space: nowrap;">
			<?php
			if( $element_record["order_id"] > 0 )
			{
				?>
				<a href="<?php echo $multilang_params['lang_href']; ?>/shop/orders/order?order_id=<?php echo $element_record["order_id"]; ?>" target="_blank" style="color:#000;font-weight:bold;text-decoration:underline;"><?php echo $element_record["order_id"]; ?></a>
				<?php
			}
			else
			{
				?>
				-
				<?php
			}
			?>
			</td>
			
			<td style="vertical-align: middle; white-space: nowrap;"><?php echo wordwrap(translate_str_by_id($name), 80,'<br>'); ?></td>
			
			<?php
			if( isset( $DP_Config->wholesaler ) )
			{
				?>
				<td style="vertical-align: middle; white-space: nowrap;"><?php echo wordwrap(translate_str_by_id($office_caption),50,'<br>'); ?></td>
				<?php
			}
			?>
			
		</tr>
		
	<?php
	}//while()
	if($items_counter == 0){
		echo '<tr><td colspan="5">'.translate_str_by_id(4349).'</td></tr>';
	}else{
		if($saldo !== 'no'){
			$saldo = number_format($saldo, 2, '.', ' ');
		}else{
			$saldo = 0;
		}
		echo '<tr style="font-weight:bold;"><td colspan="5" style="padding-left:0;"><br><p class="lead">'.translate_str_by_id(3258).': '. $saldo .'</p></td></tr>';
	}
	?>
	</table>
	</div>
	
	
	
	<?php
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
}
else
{
	?>
	<div class="epc-balance-panel epc-balance-panel--login">
		<div class="epc-balance-panel__icon"><i class="fa fa-lock" aria-hidden="true"></i></div>
		<div class="epc-balance-panel__content">
			<span class="epc-balance-panel__eyebrow">Secure customer area</span>
			<h2>Login required to view balance</h2>
			<p>Your balance is private account information. Please log in or register below to see your available credit, payments and account status.</p>
			<div class="epc-balance-panel__chips">
				<span><i class="fa fa-shield" aria-hidden="true"></i> Protected account data</span>
				<span><i class="fa fa-user" aria-hidden="true"></i> Customer only</span>
			</div>
		</div>
	</div>
	<div class="panel panel-primary">
	<?php
	//Единый механизм формы авторизации
	$login_form_postfix = "balance";
	$login_form_target = "";
	require($_SERVER["DOCUMENT_ROOT"]."/modules/login/login_form_general.php");
	?>
	</div>
	<?php
}
?>