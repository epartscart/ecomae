<?php
/**
 * Страничный скрипт формы редактирования своих данных
 
 Здесь можно отредактировать поля профиля и поменять пароль.
 Контакты email и phone здесь не меняются
 
*/
defined('_ASTEXE_') or die('No access');

require_once( $_SERVER["DOCUMENT_ROOT"]."/content/users/dp_user.php" );//Класс пользователя

if(DP_User::getUserId() == 0)
{
    echo translate_str_by_id(4709);
}
else//Пользователь авторизован
{
	//Если передана форма редактирования профиля - записываем данные.
	if( isset($_POST["edit_user"]) )
	{
		//Через транзакцию
		try
		{
			//Старт транзакции
			if( ! $db_link->beginTransaction()  )
			{
				throw new Exception(translate_str_by_id(2132));
			}
			
			
			//Защита от CSRF-атак
			$user_session = DP_User::getUserSession();
			if( !isset( $_POST["csrf_guard_key"] ) ||  $user_session["csrf_guard_key"] != $_POST["csrf_guard_key"] )
			{
				throw new Exception(translate_str_by_id(4691));
			}
			
			
			
			//1.1 ПАРОЛЬ
			//Если был изменен пароль
			if(!empty($_POST['password']))
			{
				if( ! $db_link->prepare('UPDATE `users` SET `password`=? WHERE `user_id`=?;')->execute( array( md5($_POST['password'].$DP_Config->secret_succession), $user_profile["user_id"] ) ) )
				{
					throw new Exception(translate_str_by_id(4710));
				}
			}
			
			//1.2 Поля в таблице users_profiles
			//Получаем регистрационные поля (кроме основных)
			$reg_fields_query = $db_link->prepare('SELECT * FROM `reg_fields` WHERE `main_flag`=0;');
			$reg_fields_query->execute();
			while( $reg_fields_record = $reg_fields_query->fetch() )
			{
				$show_for = json_decode($reg_fields_record["show_for"], true);
				//Есть ли данное поле в этом Регистрационном Варианте
				if(array_search($_POST["reg_variant"], $show_for) !== false)
				{
					$check_record_exist_query = $db_link->prepare('SELECT COUNT(*) FROM `users_profiles` WHERE `user_id`= ? AND `data_key`= ?;');
					$check_record_exist_query->execute( array($user_profile["user_id"], $reg_fields_record["name"]) );
					//Если запись уже была ранее, то обновляем
					if($check_record_exist_query->fetchColumn() == 1)
					{
						if( $db_link->prepare('UPDATE `users_profiles` SET `data_value`=? WHERE `data_key`=? AND `user_id`=?;')->execute( array( htmlentities($_POST[$reg_fields_record["name"]]), $reg_fields_record["name"], $user_profile["user_id"]) ) != true)
						{
							throw new Exception(translate_str_by_id(4711));
						}
					}
					else//Если записи не было, то добавляем
					{
						if( $db_link->prepare('INSERT INTO `users_profiles` (`user_id`, `data_key`, `data_value`) VALUES (?, ?, ?);')->execute( array($user_profile["user_id"], $reg_fields_record["name"], htmlentities($_POST[$reg_fields_record["name"]])) ) != true)
						{
							throw new Exception(translate_str_by_id(4712));
						}
					}
				}//if//Есть ли данное поле в этом Регистрационном Варианте
			}
			
			
			//Теперь проходим по всем полям таблицы users_profiles и удаляем те записи, которых нет в переданном наборе
			$user_profile_query = $db_link->prepare('SELECT * FROM `users_profiles` WHERE `user_id`=?;');
			$user_profile_query->execute( array($user_profile["user_id"]) );
			while( $user_profile_record = $user_profile_query->fetch() )
			{
				if(empty($_POST[$user_profile_record["data_key"]]))
				{
					if( !$db_link->prepare('DELETE FROM `users_profiles` WHERE `user_id`=? AND `data_key`=?;')->execute( array($user_profile["user_id"], $user_profile_record["data_key"]) ) )
					{
						throw new Exception(translate_str_by_id(4713));
					}
				}
			}
			
			
			//1.3 РЕГИСТРАЦИОННЫЙ ВАРИАНТ
			//Получаем текущий регистрационный вариант
			$current_reg_variant_query = $db_link->prepare('SELECT * FROM `users` WHERE `user_id`=?;');
			$current_reg_variant_query->execute( array($user_profile["user_id"]) );
			$current_reg_variant_record = $current_reg_variant_query->fetch();
			$current_reg_variant = $current_reg_variant_record["reg_variant"];
			if($current_reg_variant != $_POST['reg_variant'])
			{
				if( !$db_link->prepare('UPDATE `users` SET `reg_variant`=? WHERE `user_id`=?;')->execute( array($_POST['reg_variant'], $user_profile["user_id"]) ) )
				{
					throw new Exception(translate_str_by_id(4714));
				}
			}
		}
		catch (Exception $e)
		{
			//Откатываем все изменения
			$db_link->rollBack();
			
			
			?>
			<script>
				location="<?php echo $multilang_params['lang_href']; ?>/users/editform?error_message=<?php echo $e->getMessage(); ?>";
			</script>
			<?php
			exit();
		}
		
		
		//Дошли до сюда, значит выполнено ОК
		$db_link->commit();//Коммитим все изменения и закрываем транзакцию
		?>
		<script>
			location="<?php echo $multilang_params['lang_href']; ?>/users/editform?success_message=<?php echo translate_str_by_id(4715); ?>";
		</script>
		<?php
		exit();
	}
	else//Действий нет - выводим страницу формы редактирования профиля
	{
		//Для работы с пользователем
		require_once( $_SERVER['DOCUMENT_ROOT']."/content/users/dp_user.php" );
		$user_session = DP_User::getUserSession();
		
		
		//СПИСОК ДОПОЛНИТЕЛЬНЫХ ПОЛЕЙ РЕГИСТРАЦИИ - для буферизации введеных значений при переключении регистрационных вариантов
		$all_fields_query = $db_link->prepare('SELECT * FROM `reg_fields` WHERE `main_flag` = ? ORDER BY `order` ASC;');
		$all_fields_query->execute( array(0) );
		?>
		<script>
		var reg_fields = new Array();//Массив с объектами всех полей
		<?php
		while( $additional_field = $all_fields_query->fetch() )
		{
			?>
			reg_fields[reg_fields.length] = new Object();//Создаем новый объект поля. И инициализируем его поля:
			reg_fields[reg_fields.length - 1].main_flag = <?php echo $additional_field["main_flag"]; ?>;
			reg_fields[reg_fields.length - 1].name = "<?php echo $additional_field["name"]; ?>";
			reg_fields[reg_fields.length - 1].caption = "<?php echo translate_str_by_id($additional_field["caption"]); ?>";
			reg_fields[reg_fields.length - 1].show_for = <?php echo $additional_field["show_for"]; ?>;
			reg_fields[reg_fields.length - 1].required_for = <?php echo $additional_field["required_for"]; ?>;
			reg_fields[reg_fields.length - 1].maxlen = <?php echo $additional_field["maxlen"]; ?>;
			reg_fields[reg_fields.length - 1].regexp = "<?php echo $additional_field["regexp"]; ?>";
			reg_fields[reg_fields.length - 1].widget_type = "<?php echo $additional_field["widget_type"]; ?>";
			reg_fields[reg_fields.length - 1].widget_options = <?php echo $additional_field["widget_options"]; ?>;
			reg_fields[reg_fields.length - 1].value_buffer = "";//Текущее значения - для сохранения при переключении регистрационных вариантов
			<?php
		}
		?>
		</script>
		

		<!-- САМА ФОРМА -->
		<form id="regform" onsubmit="return onSubmitCheck();" method="post">
			<input type="hidden" name="edit_user" value="edit_user" />
			
			<?php
			//Защита от CSRF-атак
			$user_session = DP_User::getUserSession();
			?>
			<input type="hidden" name="csrf_guard_key" value="<?php echo $user_session["csrf_guard_key"]; ?>" />
			
			<!--Блок для выбора Регистрационного Варианта-->
			<div id="RegVariantsSelector">
				<?php
				//Выводим в JavaScript Регистрационные Варианты:
				$reg_variants_query = $db_link->prepare('SELECT COUNT(*) FROM `reg_variants` ORDER BY `order` ASC;');
				$reg_variants_query->execute();
				if( $reg_variants_query->fetchColumn() == 1)
				{
					$reg_variants_query = $db_link->prepare('SELECT * FROM `reg_variants` ORDER BY `order` ASC;');
					$reg_variants_query->execute();
					
					$reg_variant_record = $reg_variants_query->fetch();
					?>
					<select id="reg_variant_selector" name="reg_variant" style="display:none" onchange="regenerateFields();">
						<option value="<?php echo $reg_variant_record["id"]; ?>"><?php echo translate_str_by_id($reg_variant_record["caption"]); ?></option>
					</select>
					<?php
				}
				else//Регистрационных вариантов много
				{
					$reg_variants_query = $db_link->prepare('SELECT * FROM `reg_variants` ORDER BY `order` ASC;');
					$reg_variants_query->execute();
					?>
					<div class="panel panel-primary">
						<div class="panel-heading"><?php echo translate_str_by_id(4716); ?></div>
						<div class="panel-body">
							  <div class="form-group">
								<select id="reg_variant_selector" name="reg_variant" onchange="regenerateFields();" class="form-control" />
								<?php
								while( $reg_variant_record = $reg_variants_query->fetch() )
								{
									?>
									<option value="<?php echo $reg_variant_record["id"]; ?>"><?php echo translate_str_by_id($reg_variant_record["caption"]); ?></option>
									<?php
								}
								?>
								</select>
							  </div>
						</div>
					</div>
					<?php
				}
				?>
			</div>
			
			
			
			
			
			<!-- БЛОК ДОПОЛНИТЕЛЬНЫХ ПОЛЕЙ -->
			<div id="additional_fields_div">
			</div>
			
			
			<!-- Блок для пароля -->
			<div class="panel panel-primary">
				<!--<div class="panel-heading">Пароль</div>-->
				<div class="panel-body">

					<div class="form-group col-md-6">
						<label for="password" class="col-sm-4 control-label"><?php echo translate_str_by_id(4717); ?></label>
						<div class="col-sm-8" style="padding:5px;">
							<input type="password" name="password" class="form-control" id="password" placeholder="<?php echo translate_str_by_id(4717); ?>" />
						</div>
					</div>
					
					
					<div class="form-group col-md-6">
						<label for="password_repeat" class="col-sm-4 control-label"><?php echo translate_str_by_id(3927); ?></label>
						<div class="col-sm-8" style="padding:5px;">
							<input type="password" class="form-control" name="password_repeat" id="password_repeat" value="" placeholder="<?php echo translate_str_by_id(3927); ?>">
						</div>
					</div>
					
					<div class="text-center"><?php echo translate_str_by_id(4718); ?>.</div>
					
				</div>
			</div>
			
			
			<script>
			//Перегенировать поля
			function regenerateFields()
			{
				var current_reg_variant = document.getElementById("reg_variant_selector").value;
				
				var additional_html = "";//HTML для дополнительных полей регистрации
				for(var i=0; i < reg_fields.length; i++)
				{
					//Обработка show_for:
					if(reg_fields[i].show_for.indexOf(parseInt(current_reg_variant)) < 0)
					{
						continue;//Это поле не показываем
					}
					
					//Обработка required_for
					var required_for = "";//Для звездочки
					if(reg_fields[i].required_for.indexOf(parseInt(current_reg_variant)) >= 0)
					{
						required_for = "*";//Это поле не показываем
					}
					
					
					additional_html += "<div class=\"form-group\"><label for=\""+reg_fields[i].name+"\" class=\"col-sm-2 control-label\">"+reg_fields[i].caption+required_for+"</label><div class=\"col-sm-10\" style=\"padding:5px;\">";
					
					//Виджет:
					switch(reg_fields[i].widget_type)
					{
						case "text":
							additional_html += "<input onKeyUp=\"dynamicApplying('"+reg_fields[i].name+"');\" type=\"text\" name=\""+reg_fields[i].name+"\" id=\""+reg_fields[i].name+"\" value='"+reg_fields[i].value_buffer.replace('/(["\'\])/g', "\\$1")+"' class=\"form-control\" placeholder=\""+reg_fields[i].caption+"\" />";
							break;
					};
					
					additional_html += "</div></div>";
				}
				
				additional_html = "<div class=\"panel panel-primary\"><div class=\"panel-heading\"><?php echo translate_str_by_id(3928); ?></div><div class=\"panel-body\">" + additional_html + "</div></div>";
				
				document.getElementById("additional_fields_div").innerHTML = "";
				document.getElementById("additional_fields_div").innerHTML = additional_html;
			}//~function regenerateFields()
			
			
			
			// --------------------------------------------------------------------------
			//Функция динамическиго применния значений для текстовых строк
			function dynamicApplying(attribute)
			{
				var str_value = document.getElementById(attribute).value;//Текущее значение
				//Ищем поле
				for(var i=0; i < reg_fields.length; i++)
				{
					if(reg_fields[i].name == attribute)
					{
						reg_fields[i].value_buffer = str_value;
						console.log(reg_fields[i].value_buffer);
						break;
					}
				}
			}
			regenerateFields();//Генерируем после загрузки страницы
			</script>
			
			
			<button class="btn btn-ar btn-primary" type="submit"><?php echo translate_str_by_id(2114); ?></button>
		</form>
		
		





		<?php
		// ---------------------------------------------------------------------------------------------
		//ИНИЦИАЛИЗАЦИЯ ТЕКУЩИХ ДАННЫХ ПОСЛЕ ЗАГРУЗКИ СТРАНИЦЫ
		//1. Получить регистрационный вариант
		$user_record_query = $db_link->prepare('SELECT * FROM `users` WHERE `user_id` = ?;');
		$user_record_query->execute( array(DP_User::getUserId()) );
		$user_record = $user_record_query->fetch();
		$current_reg_variant = $user_record["reg_variant"];

		//2. Получить профиль
		?>
		<script>
		<?php
		$user_profile_query = $db_link->prepare('SELECT * FROM `users_profiles` WHERE `user_id` = ?;');
		$user_profile_query->execute( array(DP_User::getUserId()) );
		while( $user_profile_record = $user_profile_query->fetch() )
		{
			//Задаем значение в поле буферизации списка JavaScript
			?>
			for(var i=0; i < reg_fields.length; i++)
			{
				if(reg_fields[i].name == '<?php echo $user_profile_record["data_key"]; ?>')
				{
					reg_fields[i].value_buffer = '<?php echo $user_profile_record["data_value"]; ?>';
				}
			}
			<?
		}
		?>
		</script>
		<?php
		//3. ИНИЦИАЛИЗАЦИЯ НА JavaScript
		?>
		<script>
		//1. Текущий регистрационный вариант:
		document.getElementById("reg_variant_selector").value = <?php echo $current_reg_variant; ?>;
		regenerateFields();//Генерируем после загрузки страницы
		</script>
		<?php
		// ---------------------------------------------------------------------------------------------
		?>


		
		
		
		<script>
		// ---------------------------------------------------------------------------------------------
		//ПРОВЕРКА КОРРЕКСТНОСТИ ЗАПОЛНЕНИЯ ФОРМЫ:
		//Капча здесь больше не используется, т.к. при сохранении этой формы нет отправки ссылок и кодов подтверждения контактов
		function onSubmitCheck()
		{
			//Получаем текущий выбранный Регистрационный Вариант:
			var currentRegVariant = document.getElementById("reg_variant_selector").value;
			
			//Проверка факта заполнения полей какими-либо значениями
			for(var i=0; i<reg_fields.length; i++)
			{
				if(reg_fields[i].required_for.indexOf(parseInt(currentRegVariant)) != -1)//Заполнение требуется для данного Регистрационного Варианта
				{
					if(document.getElementById(reg_fields[i].name).value == "")//Но поле не заполнено
					{
						alert("Заполните поле "+reg_fields[i].caption);
						return false;
					}
				}
			}//for(i)
			
			//Обработка заполнения пароля:
			if(document.getElementById("password").value != document.getElementById("password_repeat").value)//Пароли должны совпадать
			{
				alert("<?php echo translate_str_by_id(3933); ?>");
				return false;
			}
			else if(document.getElementById("password").value.length < <?php echo $DP_Config->min_password_len; ?> && document.getElementById("password").value.length !=0)//Совпадает. Теперь проверям минимально допустимую длину
			{
				alert("<?php echo translate_str_by_id(3934); ?> <?php echo $DP_Config->min_password_len; ?> <?php echo translate_str_by_id(3935); ?>");
				return false;
			}
			
			
			//Проверка соответствия заполненных значений регулярным выражениям
			//Если поле пустое - значит его можно было не заполнять (проверка на факт заполнения следует раньше). Но есть там есть значение, то оно обязательно должно соответствовать RegExp, даже если оно не обязательно к заполнению
			for(var i=0; i<reg_fields.length; i++)
			{
				if(reg_fields[i].show_for.indexOf(parseInt(currentRegVariant)) == -1)//У этого поля не указан текущий Регистрационный Вариант - его нет в форме
				{
					continue;
				}
				
				//Если регулярное выражение пустое - значит пропускаем, т.к. требований к содержимому нет
				if(reg_fields[i].regexp == "")
				{
					continue;
				}
				
				if(String(document.getElementById(reg_fields[i].name).value) != "")
				{
					var current_value = String(document.getElementById(reg_fields[i].name).value);//Заполненное значение
					var regex = new RegExp(reg_fields[i].regexp);//Регулярное выражение для поля
					//Далее ищем подстроку по регулярному выражению
					var match = regex.exec(String(current_value));
					if(match == null)
					{
						alert("<?php echo translate_str_by_id(3885); ?> "+reg_fields[i].caption+" <?php echo translate_str_by_id(3930); ?>");
						return false;
					}
					else
					{
						var match_value = String(match[0]);//Подходящая подстрока
						if(match_value != current_value)
						{
							alert("<?php echo translate_str_by_id(3893); ?> "+reg_fields[i].caption+" <?php echo translate_str_by_id(3931); ?>");
							return false;
						}
					}
					//Заполнено правильно, если: есть подстрока по регулярному выражению и она полностью равна самой строке
				}
			}

			return true;
		}//~function onSubmitCheck()
		// ---------------------------------------------------------------------------------------------
		</script>
		<?php
	}//else - Действий нет - выводим страницу формы редактирования профиля
}//else - Пользователь авторизован
?>