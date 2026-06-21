<?php
/**
 * Страничный скрипт для страницы запроса
*/
defined('_ASTEXE_') or die('No access');

require_once($_SERVER["DOCUMENT_ROOT"]."/content/general/actions_alert.php");//Вывод сообщений о результатах выполнения действий

//Для работы с пользователями
require_once($_SERVER["DOCUMENT_ROOT"]."/content/users/dp_user.php");
$user_id = DP_User::getUserId();

if($user_id > 0)
{
	$vin_id = (int) $_GET["id"];

	//Информация о запросе
	$vin_list_query = $db_link->prepare("SELECT * FROM `users_vin` WHERE `id` = ? AND `user_id` = ?;");
	$vin_list_query->execute( array($vin_id, $user_id) );
	$vin_list_array = $vin_list_query->fetch();

	if( empty($vin_list_array) )
	{
		
		echo translate_str_by_id(5603);
		
	}else{
		
		//Ставим флаг "Просмотрен"
		$db_link->prepare("UPDATE `users_vin` SET `viewed_customer` = 1 WHERE `id` = ?;")->execute( array($vin_id) );

	?>

		<div class="row">
		<div class="row">
		<div class="col-lg-12">
			<div class="hpanel">
				
				<div class="panel-body control-label">
					<div class="row">
					<div class="col-lg-12">
					
						<!-- Переписка с покупателем -->
						
						<p class="lead"><?php echo translate_str_by_id(4548); ?></p>
						
						<div>
							<div class="chat_block" id="chat_block" style="height: auto; max-height:600px;">
							</div>
							
							<br>
							<?php echo translate_str_by_id(4549); ?>:
							<textarea class="form-control" id="new_message_area"></textarea>
							<a style="margin-top:10px;" class="btn btn-ar btn-primary" onclick="sendMessage();"><?php echo translate_str_by_id(3211); ?></a>
						</div>
						<script>
						// --------------------------------------------------------------------------
						//Получить сообщения по заказу
						function getOrderMessages()
						{
							jQuery.ajax({
								type: "GET",
								async: true,
								url: "/content/requests/ajax_get_message.php",
								dataType: "json",//Тип возвращаемого значения
								data: "vin_id=<?php echo $vin_id; ?>&csrf_guard_key=<?php echo $user_session["csrf_guard_key"]; ?>",
								success: function(answer)
								{
									var html = "";
									for(var i=0; i < answer.length; i++)
									{
										var class_str = "bubble";
										var sender = "<?php echo translate_str_by_id(4550); ?>";
										if(answer[i].is_customer == false)
										{
											class_str += "2";
											sender = "<?php echo translate_str_by_id(3565); ?>";
										}
										html += "<div class=\""+class_str+"\">"+sender+" "+answer[i].time+"<br>"+answer[i].text+"</div>";	
									}
									if(html == "") html = "<div align=\"center\"><?php echo translate_str_by_id(5220); ?></div>";
									document.getElementById("chat_block").innerHTML = html;
									
									document.getElementById("chat_block").scrollTop = document.getElementById("chat_block").scrollHeight;
								}
							});
						}
						// --------------------------------------------------------------------------
						//Отправить сообщение
						function sendMessage()
						{
							var text = document.getElementById("new_message_area").value;
							if(text == "")
							{
								alert("<?php echo translate_str_by_id(3567); ?>");
								return;
							}
							
							jQuery.ajax({
								type: "GET",
								async: true,
								url: "/content/requests/ajax_send_message.php",
								dataType: "json",//Тип возвращаемого значения
								data: "vin_id=<?php echo $vin_id; ?>&text="+encodeURI(text)+"&csrf_guard_key=<?php echo $user_session["csrf_guard_key"]; ?>",
								success: function(answer)
								{
									if(answer.status == true)
									{
										document.getElementById("new_message_area").value = "";
										getOrderMessages();
									}
									else
									{
										alert("<?php echo translate_str_by_id(3568); ?>");
									}
								}
							});
						}
						// --------------------------------------------------------------------------
						
						//Запрашиваем переписку по заказу
						setInterval(function(){
							getOrderMessages();
						}, 180000);
						getOrderMessages();
						</script>
					
					</div>
					</div>
					
					<div class="col-lg-12">
						<br/>
						<br/>
						<br/>
					</div>
					
				</div>
				
				
				
				
				
				
				
				
				
				
				<div class="col-lg-12">
					<p class="lead"><?php echo translate_str_by_id(5604); ?></p>
					<?php echo $vin_list_array['text']; ?>
				</div>
				
			</div>
		</div>
		</div>
		</div>
		
	<?php
	}
	?>

<link href="/lib/Lightbox/css/lightbox.css" rel="stylesheet" type="text/css" />
<script type="text/javascript" src="/lib/Lightbox/js/lightbox.js"></script>
<style>
.vin-file-list-img
{
	display: inline-block; 
	border: 1px solid #ddd; 
	border-radius: 5px; 
	padding:15px;
}

.vin-file-list-img > div
{
	background-size: contain; 
	background-repeat: no-repeat; 
	background-position: center center;
	width: 100px;
	height: 100px;
}
</style>

<?php
}//if($user_id > 0)
else//Если покупатель не авторизован
{
?>
	<p><?php echo translate_str_by_id(5605); ?></p>
    
	<div class="panel panel-primary">
	<?php
	//Единый механизм формы авторизации
	$login_form_postfix = "my_vin";
	require($_SERVER["DOCUMENT_ROOT"]."/modules/login/login_form_general.php");
	?>
	</div>
<?php
}
?>