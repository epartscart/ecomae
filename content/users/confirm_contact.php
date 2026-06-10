<?php
/**
 * Скрипт для подтверждения контактов пользователей.
 За основу взят старый скрипт activate.php, который использовался для "активации" учетной записи пользователя.

 ДАННЫЙ СКРИПТ может работать в страничном режиме и в AJAX-режиме.
 
 
 Описание работы:
 Скрипт используется для подтверждения контактов:
 - при регистрации пользователя
 - при смене контакта зарегистрированным пользователем
 
 Входные параметры:
 - u_id (ID пользователя)
 - code (код подтверждения контакта)
 - type (тип контакта - email/phone)
 
Скрипт сам определяет событие - регистрация или смена контакта.

Работа скрипта не зависит от того, авторизован ли пользователь или нет.
 
*/
//defined('_ASTEXE_') or die('No access');//Закомменчено, т.к. скрипт может работать и в AJAX-режиме



$can_try_again = false;//Флаг - Вывести форму повторного ввода кода подтверждения (только для телефона)


//Определяем режим работы скрипта
$mode = 'page';//Режим СТРАНИЦА (пользователь перешел по ссылке из email или ввел код в форму)
if( !isset($db_link) )
{
	$mode = 'ajax';//Режим AJAX (пользователь вводит код в модальное окно)
	
	//Конфигурация CMS
	require_once($_SERVER["DOCUMENT_ROOT"]."/config.php");
	$DP_Config = new DP_Config;

	//Подключение к БД
	try
	{
		$db_link = new PDO('mysql:host='.$DP_Config->host.';dbname='.$DP_Config->db, $DP_Config->user, $DP_Config->password);
	}
	catch (PDOException $e) 
	{
		$answer = array();
		$answer['status'] = false;
		$answer['message'] = 'No DB connect';
		$answer['can_try_again'] = $can_try_again;
		exit( json_encode($answer) );
	}
	$db_link->query("SET NAMES utf8;");
}


// -------------------------------------------------------------------------------
//Подключение мультиязычности
require_once($_SERVER["DOCUMENT_ROOT"]."/lang/dp_lang.php");
$multilang_params = multilang_init();
// -------------------------------------------------------------------------------


try
{
	//Проверка наличия параметров
	if( empty($_GET["code"]) || empty($_GET["u_id"]) || empty($_GET["type"]) )
	{
		throw new Exception(translate_str_by_id(4702));
	}
	//Поле $type будет подставляться в SQL-запросы. Проверяем значение.
	$type = $_GET['type'];
	if( $type != 'email' && $type != 'phone' )
	{
		throw new Exception(translate_str_by_id(2122)." 1");
	}
	//Название контакта для текста
	$contact_caption = translate_str_by_id(1312);
	if( $type == 'email' )
	{
		$contact_caption = 'E-mail';
	}
	
	
	//Ищем учетную запись по u_id
	$user_query = $db_link->prepare('SELECT * FROM `users` WHERE `user_id` = ?;');
	$user_query->execute( array($_GET["u_id"]) );
	$user_record = $user_query->fetch();
	//Если учетной записи нет
	if( $user_record == false )
	{
		throw new Exception(translate_str_by_id(2122)." 1.1");
	}
	
	//Учетная запись есть
	//Если для нее не было запросов на подтверждение контакта с указанным типом:
	if( $user_record[$type.'_code'] == '' )
	{
		throw new Exception(translate_str_by_id(2122)." 1.1");
	}
	
	//Учетная запись есть.
	//Для нее есть запрос на подтверждение контакта
	
	
	//Проверяем код подтверждения
	if( $user_record[$type.'_code'] != $_GET["code"] )
	{
		//Код подтверждения не правильный
		
		//Проверяем, не исчерпаны ли попытки
		if( $user_record[$type.'_code_attempts'] < 3 )
		{
			//Попытки не исчерпаны - прибавляем счетчик попыток
			$db_link->prepare('UPDATE `users` SET `'.$type.'_code_attempts` = `'.$type.'_code_attempts`+1 WHERE `user_id` = ?;')->execute( array($_GET["u_id"]) );
			
			if( $type == 'phone' )
			{
				$can_try_again = true;//Будет показана форма повторного ввода кода
			}
			
			throw new Exception(translate_str_by_id(4703).": ".(3 - $user_record[$type.'_code_attempts'] ) );
		}
		else
		{
			//Попытки исчерпаны. Очищаем поля, по которым можно подтвердить контакт. Таким образом, сделать еще одну попытку у пользователя не получится
			$db_link->prepare('UPDATE `users` SET `'.$type.'_new`= ?, `'.$type.'_code`= ?, `'.$type.'_code_expired`= ?, `'.$type.'_code_attempts`= ? WHERE `user_id` = ?;')->execute( array('', '', 0, 0, $_GET['u_id']) );
			
			throw new Exception(translate_str_by_id(4704));
		}
	}
	
	//Код указан верно
	//Проверяем срок действия кода
	if( time() > $user_record[$type.'_code_expired'] )
	{
		//Срок действия кода подтверждения истек
		
		//Очищаем поля, по которым можно подтвердить контакт. Таким образом, сделать еще одну попытку у пользователя не получится
		$db_link->prepare('UPDATE `users` SET `'.$type.'_new`= ?, `'.$type.'_code`= ?, `'.$type.'_code_expired`= ?, `'.$type.'_code_attempts`= ? WHERE `user_id` = ?;')->execute( array('', '', 0, 0, $_GET['u_id']) );
		
		
		throw new Exception(translate_str_by_id(4705));
		
		/*
		РЕГИСТРАЦИЯ
		Пользователь уже не сможет отправить новый запрос (форму регистрации), т.к. учетная запись с таким контактом уже существует.
		Т.е. ему теперь нужно обращаться к администратору сайта, чтобы тот подтвердил контакт через панель управления сайта.
		
		СМЕНА КОНТАКТА
		Пользователь может по-прежнему заходить на сайт под своей учеткой, используя старый контакт, и, затем отправлять новые запросы на смену контакта.
		*/
	}
	
	
	//Если срок действия не истек - подтверждаем контакт
	//Готовим набор полей для обновления учетной записи пользователя
	$contact = '';
	$contact_confirmed = '1';//*
	$contact_new = '';//*
	$contact_code = '';//*
	$contact_code_expired = '0';//*
	$contact_code_attempts = '0';//*
	
	//В зависимости от события (например, регистрация пользователя, смена контакта, добавление контакта), определяем, откуда брать значение $contact
	if( !empty($user_record[$type.'_new']) )
	{
		$contact = $user_record[$type.'_new'];//При смене контакта
	}
	else
	{
		$contact = $user_record[$type];//При регистрации пользователя
	}

	
	
	//Обновляем учетную запись пользователя (подтверждаем контакт)
	if( !$db_link->prepare('UPDATE `users` SET `'.$type.'`=?, `'.$type.'_confirmed`=?, `'.$type.'_new`=?, `'.$type.'_code`=?, `'.$type.'_code_expired`=?, `'.$type.'_code_attempts`=? WHERE `user_id`=? ;')->execute( array($contact, $contact_confirmed, $contact_new, $contact_code, $contact_code_expired, $contact_code_attempts, $_GET['u_id']) ) )
	{
		//Ошибка SQL.
		//Хотя, пользователь указал все правильно, поэтому показываем форму повторного ввода кода (только для телефона)
		if( $type == 'phone' )
		{
			$can_try_again = true;//Будет показана форма повторного ввода кода
		}
		
		throw new Exception(translate_str_by_id(2122)." 3");
	}
	
	
	//В зависимости от режима работы - выдаем результат об успехе соответствующим образом
	if( $mode == 'page' )
	{
		//Для режима страницы
		?>
		<p><?php echo translate_str_by_id(4706); ?> <?php echo $contact_caption; ?> <?php echo translate_str_by_id(4707); ?>.</p>
		<?php
	}
	else
	{
		//Для режима AJAX
		$answer = array();
		$answer['status'] = true;
		$answer['message'] = translate_str_by_id(4706).' '.$contact_caption.' '.translate_str_by_id(4707).'.';
		exit( json_encode($answer) );
	}
}
catch (Exception $e)
{
	//В зависимости от режима скрипта, выдаем ошибку соответствующим образом
	
	if( $mode == 'page' )
	{
		//Для режима страницы
		
		//Выводим сообщение об ошибке
		echo $e->getMessage();
		
		//Отдельно для подтверждения телефона. Пользователь вводит код вручную и поэтому может ошибиться. Чтобы у него была возможность ввести код заново - выводим форму.
		if( $can_try_again )
		{
			?>
			
			<form method="GET">
				<input type="hidden" name="u_id" value="<?php echo $_GET["u_id"]; ?>" />
				<input type="hidden" name="type" value="phone" />
				<div class="form-group">
					<label for="" class="col-sm-2 control-label"><?php echo translate_str_by_id(4708); ?></label>
					<div class="col-sm-6" style="padding:5px;">
						<input type="text" class="form-control" name="code" id="code" value="" placeholder="<?php echo translate_str_by_id(4708); ?>">
					</div>
					<div class="col-sm-4" style="padding:5px;">
						<button type="submit"><?php echo translate_str_by_id(4521); ?></button>
					</div>
				</div>
			</form>
			
			<?php
		}
	}
	else
	{
		//Для режима AJAX
		$answer = array();
		$answer['status'] = false;
		$answer['message'] = $e->getMessage();
		$answer['can_try_again'] = $can_try_again;
		exit( json_encode($answer) );
	}
}
?>