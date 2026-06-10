<?php
/**
 * Скрипт для страницы восстановления пароля.
 * Сюда пользователь переадресуется по ссылке из письма восстановления
*/

defined('_ASTEXE_') or die('No access');

//Входные данные
if( !isset($_GET["code"]) && !isset($_GET["type"]) && !isset($_GET["contact"]) )
{
	?>
	<script>
		location="<?php echo $DP_Config->domain_path; ?>";
	</script>
	<?php
	exit;
}
$code = $_GET["code"];
$type = $_GET["type"];
$contact = htmlentities($_GET["contact"]);


//$type используется в SQL-запросах. Проверяем значение.
if( $type != 'email' && $type != 'phone' )
{
	?>
	<script>
		location="<?php echo $DP_Config->domain_path; ?>";
	</script>
	<?php
	exit;
}



//Сначала проверяем, запрашивал ли вообще данный пользователь восстановление пароля
$check_query = $db_link->prepare('SELECT * FROM `users` WHERE `'.$type.'` = ? AND `'.$type.'_confirmed` = ?;');
$check_query->execute( array($contact, 1) );
$user_record = $check_query->fetch();
if( $user_record == false )
{
	?>
	<script>
		location="<?php echo $DP_Config->domain_path; ?>";
	</script>
	<?php
	exit;
}
else//Такой пользователь существует - Проверяем, делал ли он запрос на восстановление пароля
{
	//Запроса на восстановление пароля не было
	if( $user_record["forgot_password_code"] == "" )
	{
		?>
		<script>
			location="<?php echo $DP_Config->domain_path; ?>";
		</script>
		<?php
		exit;
	}
	
	
	if($code != $user_record["forgot_password_code"])
	{
		//Код передан некорректный - возможно злоумышленник (либо код из SMS указан не правильно) - удаляем код и время, чтобы не дать злоумышленнику вторую попытку.
		$db_link->prepare('UPDATE `users` SET `forgot_password_time`=?, `forgot_password_code` = ? WHERE `user_id` = ?;')->execute( array(0, '', $user_record['user_id']) );
		?>
		<?php echo translate_str_by_id(4729); ?>
		<?php
	}
	else//Код указан верно. Проверяем, не истекло ли время
	{
		$limit_time = time() - 1800;//Полчаса назад
		
		if($user_record["forgot_password_time"] < $limit_time)
		{
			//Время истекло
			//Сбрасываем восстановление
			$db_link->prepare('UPDATE `users` SET `forgot_password_time`=?, `forgot_password_code` = ? WHERE `user_id` = ?;')->execute( array(0, '', $user_record['user_id']) );
			?>
			<?php echo translate_str_by_id(4730); ?>
			<?php
		}
		else
		{
			//Все правильно - генерируем новый пароль
			$new_password = substr(md5( $DP_Config->secret_succession." ".time()." ".rand(0,1000) ), 0, 8);
			
			if( $db_link->prepare('UPDATE `users` SET `password`=?, `forgot_password_time`=?, `forgot_password_code` = ? WHERE `user_id` = ?;')->execute( array( md5($new_password.$DP_Config->secret_succession), 0, '', $user_record['user_id'] ) ) != true)
			{
				echo translate_str_by_id(4731);
			}
			else
			{
				//3. Отображаем пароль на странице
				?>
				<h2><?php echo translate_str_by_id(4732); ?></h2>
				<?php
				echo $new_password;
			}
		}
	}
}
?>