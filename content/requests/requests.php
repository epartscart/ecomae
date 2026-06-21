<?php
/**
 * Страничный скрипт - управление запросами
*/
defined('_ASTEXE_') or die('No access');


//Для работы с пользователем
require_once($_SERVER["DOCUMENT_ROOT"]."/content/users/dp_user.php");
$user_id = DP_User::getUserId();


if($user_id > 0)
{
?>



	<div class="row">
		
		<?php
		$viewed = -1;

		//Получаем текущие значения фильтра:
		$vin_filter = NULL;
		if(isset($_COOKIE["vin_filter_user"])){
			$vin_filter = $_COOKIE["vin_filter_user"];
		}
		if($vin_filter != NULL)
		{
			$vin_filter = json_decode($vin_filter, true);
			$viewed = $vin_filter["viewed"];
		}
		?>
		
		<div class="col-md-2">
			<div>
				<label style="margin-bottom: 0;" for="viewed"><?php echo translate_str_by_id(3581); ?></label>
			</div>
			<div>
				<select <?php echo ((int)$viewed !== -1)?'style="background:#b9fcab;"':''; ?> id="viewed" class="form-control">
					<option value="-1"><?php echo translate_str_by_id(2094); ?></option>
					<option value="1"><?php echo translate_str_by_id(3581); ?></option>
					<option value="0"><?php echo translate_str_by_id(3582); ?></option>
				</select>
				<script>
					document.getElementById("viewed").value = <?php echo $viewed; ?>;
				</script>
			</div>
		</div>
		
	</div>





	<div class="box_btn_filter" style="margin:20px 0px 15px;">
		<button style="margin-right: 2px; margin-bottom:5px;" class="btn btn-ar btn-primary" onclick="filterUsers();"><?php echo translate_str_by_id(2232); ?></button>
		<button style="margin-right: 2px; margin-bottom:5px;" class="btn btn-ar btn-primary" onclick="unsetFilterUsers();"><?php echo translate_str_by_id(2555); ?></button>
		<button style="margin-right: 2px; margin-bottom:5px;" class="btn btn-ar btn-primary" onclick="location='<?php echo $multilang_params['lang_href']; ?>/zapros-prodavczu';"><?php echo translate_str_by_id(5606); ?></button>
	</div>
	<style>
	@media screen and (min-width: 768px) {
		.box_btn_filter .btn{
			display:inline-block;
		}
		.box_btn_filter .btn[onclick="location='<?php echo $multilang_params['lang_href']; ?>/zapros-prodavczu';"]{
			float:right;
		}
	}
	@media screen and (max-width: 767px) {
		.box_btn_filter .btn{
			display:block;
			float:none;
			width: 100%;
		}
	}
	</style>
	<script>
	// ------------------------------------------------------------------------------------------------
	//Устновка cookie в соответствии с фильтром
	function filterUsers()
	{
		var vin_filter = new Object;
		
		vin_filter.viewed = document.getElementById("viewed").value;
		
		//Устанавливаем cookie (на полгода)
		var date = new Date(new Date().getTime() + 15552000 * 1000);
		document.cookie = "vin_filter_user="+JSON.stringify(vin_filter)+"; path=/; expires=" + date.toUTCString();
		
		//Обновляем страницу
		location='<?php echo $multilang_params['lang_href']; ?>/requests';
	}
	// ------------------------------------------------------------------------------------------------
	//Снять все фильтры
	function unsetFilterUsers()
	{
		var vin_filter = new Object;
		
		
		vin_filter.viewed = -1;

		//Устанавливаем cookie (на полгода)
		var date = new Date(new Date().getTime() + 15552000 * 1000);
		document.cookie = "vin_filter_user="+JSON.stringify(vin_filter)+"; path=/; expires=" + date.toUTCString();
		
		//Обновляем страницу
		location='<?php echo $multilang_params['lang_href']; ?>/requests';
	}
	// ------------------------------------------------------------------------------------------------
	</script>










	<div class="row"> 
		<div class="col-lg-12">
			<table cellpadding="1" cellspacing="1" class="table"> 
				<tr> 
					<th>ID</th>
					<th><?php echo translate_str_by_id(3250); ?></th>
					<th></th>
				</tr>
				<?php
				
				//Подстрока с условиями фильтрования
				$WHERE_CONDITIONS = " WHERE `user_id` = ". (int) $user_id;

				//По куки фильтра:
				$vin_filter = NULL;
				if( isset($_COOKIE["vin_filter_user"]) )
				{
					$vin_filter = $_COOKIE["vin_filter_user"];
				}
				if($vin_filter != NULL)
				{
					$vin_filter = json_decode($vin_filter, true);
					
					// 1.
					if($vin_filter["viewed"] != -1 )
					{
						if($WHERE_CONDITIONS != "")
						{
							$WHERE_CONDITIONS .= " AND ";
						}
						$WHERE_CONDITIONS .= " `viewed_customer` = ". (int) $vin_filter["viewed"];
					}
					
				}//~if($users_filter != NULL)
				
				
				
				//Получаем список зарегистрированных пользователей
				$vin_list_SQL = "SELECT SQL_CALC_FOUND_ROWS * FROM `users_vin` ".$WHERE_CONDITIONS." ORDER BY `viewed_customer` ASC, `id` DESC;";
				$vin_list_query = $db_link->prepare($vin_list_SQL);
				$vin_list_query->execute();
				
				$elements_count_rows_query = $db_link->prepare('SELECT FOUND_ROWS();');
				$elements_count_rows_query->execute();
				$elements_count_rows = $elements_count_rows_query->fetchColumn();
				
				if($elements_count_rows == 0){
					echo '<tr><td colspan="4">'.translate_str_by_id(5607).'</td></tr>';
				}
				
				$vin_list = array();
				while($vin_list_array = $vin_list_query->fetch())
				{
					$vin_list[] = $vin_list_array;
				}
				
				//ОБЕСПЕЧИВАЕМ ПОСТРАНИЧНЫЙ ВЫВОД:
				//---------------------------------------------------------------------------------------------->
				//Определяем количество страниц для вывода:
				$p = $DP_Config->list_page_limit;//Штук на страницу
				$count_pages = (int)(count($vin_list) / $p);//Количество страниц
				if(count($vin_list)%$p)//Если остались еще пользователи
				{
					$count_pages++;
				}
				//Определяем, с какой страницы начать вывод:
				$s_page = 0;
				if(!empty($_GET['s_page']))
				{
					$s_page = $_GET['s_page'];
				}
				//----------------------------------------------------------------------------------------------|
				
				for($i=0, $d=0; $i<count($vin_list) && $d<$p; $i++, $d++)//Цикл по всех пользователям
				{
					$vin_list_array = $vin_list[$i];
					
					
					$viewed_class = "";
					$viewed_flag = $vin_list_array["viewed_customer"];
					if( $viewed_flag == 0)
					{
						$viewed_class = " not_viewed";
					}
					
					//Пропускаем нужное количество блоков в соответствии с номером требуемой страницы
					if($i < $s_page*$p)
					{
						$d--;
						continue;
					}
					
					?>
					<tr class="<?php echo $viewed_class; ?>">
						<td><a href="<?php echo $multilang_params['lang_href']; ?>/requests/request?id=<?php echo $vin_list_array["id"]; ?>"><i class="fa fa-sign-in" aria-hidden="true"></i> <?php echo $vin_list_array["id"]; ?></td>
						<td><?php echo date("d.m.Y H:i",$vin_list_array["time"]);?></td>
						<td class="text-center"><a href="<?php echo $multilang_params['lang_href']; ?>/requests/request?id=<?php echo $vin_list_array["id"]; ?>"><?php echo ($vin_list_array["viewed_customer"])?'':'<i class="fa fa-envelope" aria-hidden="true"></i>'; ?></a></td>
					</tr>
					<?php
				}//for($i)
				?>
			</table>
		</div>
		
		<div class="col-lg-12">
			<?php
			//START ВЫВОД ПЕРЕКЛЮЧАТЕЛЕЙ СТРАНИЦ ТАБЛИЦЫ
			if( $count_pages > 1 )
			{
				?>
				<div class="row">
					<div class="col-lg-12 text-center">
						<div class="dataTables_paginate paging_simple_numbers">
							<ul class="pagination">
							<?php
							for($i=0; $i < $count_pages; $i++)
							{
								//Класс первой страницы
								$previous = "";
								if($i == 0) $previous = "previous";
								
								//Класс последней страницы
								$next = "";
								if($i == $count_pages-1) $next = "next";
								
								if($i == $s_page)//Текущая страница
								{
									?>
									<li class="paginate_button active <?php echo $previous; ?> <?php echo $next; ?>"><a href="javascript:void(0);"><?php echo $i; ?></a></li>
									<?php
								}
								else
								{
									?>
									<li class="paginate_button <?php echo $previous; ?> <?php echo $next; ?>"><a href="<?php echo $multilang_params['lang_href']; ?><?php echo "/requests?s_page=$i"; ?>"><?php echo $i; ?></a></li>
									<?php
								}
							}
							?>
							</ul>
						</div>
					</div>
				</div>
			<?php
			}
			//END ВЫВОД ПЕРЕКЛЮЧАТЕЛЕЙ СТРАНИЦ ТАБЛИЦЫ
			?>
		</div>
	</div>
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