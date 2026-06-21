<?php
//Подключаемый скрипт для реализации протокола выполнения действий на страницах "Заказы" и "Позиции заказов"
defined('_ASTEXE_') or die('No access');

//Для работы с пользователем
require_once( $_SERVER['DOCUMENT_ROOT']."/content/users/dp_user.php" );
$user_session = DP_User::getAdminSession();
?>
<script>
var actions_semaphores = new Array();//Массив семафоров. Чтобы для одной позиции не запускали одновременно несколько действий
// ---------------------------------------------------------------------------------------------------------
function exec_action(order_item_id, sao_action_id)
{
	//Проверяем семафор
	if(actions_semaphores[order_item_id] == undefined)
	{
		actions_semaphores[order_item_id] = true;
	}
	else if( actions_semaphores[order_item_id] == true )
	{
		alert("<?php echo translate_str_by_id(3821); ?>");
		return;
	}
	else
	{
		actions_semaphores[order_item_id] = true;
	}
	
	
	console.log("<?php echo translate_str_by_id(3822); ?>: "+order_item_id+", <?php echo translate_str_by_id(2755); ?>: "+sao_action_id);
	
	
	jQuery.ajax({
		type: "GET",
		async: true, //Запрос асинхронный
		url: "/<?php echo $DP_Config->backend_dir; ?>/content/shop/sao/ajax_exec_action.php",
		dataType: "json",//Тип возвращаемого значения
		data: "order_item_id="+order_item_id+"&sao_action_id="+sao_action_id+"&key=<?php echo urlencode($DP_Config->tech_key); ?>"+"&csrf_guard_key=<?php echo $user_session["csrf_guard_key"]; ?>",
		success: function(answer)
		{
			actions_semaphores[answer.order_item_id] = false;//Снимаем семафор
			
			console.log(answer);
			if(answer.status == true)
			{
				<?php
				//В зависимости от режима работы протокола ($sao_propocol_mode указывается перед подключением данного скрипта)
				//Перезагружаем страницу
				if( $sao_propocol_mode == 1 )
				{
					?>
					location = '/<?php echo $DP_Config->backend_dir; ?>/shop/orders/order?order_id=<?php echo $_GET["order_id"]; ?>';
					<?php
				}
				else if( $sao_propocol_mode == 2 )//Страница "Позиции заказов" - нужно только перерисовать позицию
				{
					?>
					redraw_order_item(answer.order_item_id);
					<?php
				}
				?>
			}
			else
			{
				alert("<?php echo translate_str_by_id(2122); ?>! " + answer["sao_action_message"]);
				alert("<?php echo translate_str_by_id(3823); ?>");
				
				
				
				<?php
				//В зависимости от режима работы протокола ($sao_propocol_mode указывается перед подключением данного скрипта)
				//Перезагружаем страницу
				if( $sao_propocol_mode == 1 )
				{
					?>
					location = '/<?php echo $DP_Config->backend_dir; ?>/shop/orders/order?order_id=<?php echo $_GET["order_id"]; ?>';
					<?php
				}
				else if( $sao_propocol_mode == 2 )//Страница "Позиции заказов" - нужно только перерисовать позицию
				{
					?>
					redraw_order_item(answer.order_item_id);
					<?php
				}
				?>
			}
		}
	});
	
}
// ---------------------------------------------------------------------------------------------------------
//Функция для перерисовки позиции заказа. Это актуально на страницы "Позиции заказов", т.к. на ней не следует полностью перезагружать страницу
/*
При перерисовке менять:
- цвет позиции
- название статуса
- SAO состояние: наименование, цвет, фон
- SAO инфо
- SAO действия
*/
function redraw_order_item(order_item_id)
{
	//console.log("Запрос данных для перерисовки позиции");
	
	jQuery.ajax({
		type: "GET",
		async: true, //Запрос асинхронный
		url: "/<?php echo $DP_Config->backend_dir; ?>/content/shop/sao/ajax_get_order_item_object.php",
		dataType: "json",//Тип возвращаемого значения
		data: "order_item_id="+order_item_id+"&key=<?php echo urlencode($DP_Config->tech_key); ?>"+"&csrf_guard_key=<?php echo $user_session["csrf_guard_key"]; ?>",
		success: function(answer)
		{
			console.log(answer);
			if(answer.status == true)
			{
				//Перерисовываем позицию
				//Цвет позиции
				document.getElementById("order_item_record_"+answer.order_item_id).setAttribute("style", "background-color:"+answer.item.status_color+";");
				//Название статуса
				document.getElementById("order_item_status_name_td_"+answer.order_item_id).innerHTML = answer.item.status_name;
				//SAO состояние: наименование
				document.getElementById("order_item_sao_state_td_"+answer.order_item_id).innerHTML = answer.item.sao.state_name;
				//SAO состояние: цвет и фон
				document.getElementById("order_item_sao_state_td_"+answer.order_item_id).setAttribute("style", "background-color:"+answer.item.sao.state_color_background+";color:"+answer.item.sao.state_color_text+";vertical-align:middle;");
				//SAO инфо
				document.getElementById("order_item_sao_info_td_"+answer.order_item_id).innerHTML = answer.item.sao.message;
				//SAO действия
				if(answer.item.sao.actions.length > 0)
				{
					var actions_html = "";
					for(var i=0; i < answer.item.sao.actions.length; i++)
					{
						actions_html += "<button onclick=\"exec_action("+answer.order_item_id+", "+answer.item.sao.actions[i].id+");\" class=\"btn "+answer.item.sao.actions[i].btn_class+" \" type=\"button\"><i class=\"fa "+answer.item.sao.actions[i].fontawesome+" \"></i> <span class=\"bold\">"+answer.item.sao.actions[i].name+"</span></button> ";
					}
					document.getElementById("order_item_sao_actions_td_"+answer.order_item_id).innerHTML = actions_html;
				}
				else
				{
					document.getElementById("order_item_sao_actions_td_"+answer.order_item_id).innerHTML = "<?php echo translate_str_by_id(3554); ?>";
				}
			}
			else
			{
				alert("<?php echo translate_str_by_id(3824); ?>");
			}
		}
	});
}
// ---------------------------------------------------------------------------------------------------------
</script>