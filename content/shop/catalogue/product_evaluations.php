<?php
//----------------------------------------------------------------------------------------------------------------
//START БЛОК ОТЗЫВОВ

// -------------------------------------------------------------------------------
//Подключение мультиязычности
require_once($_SERVER["DOCUMENT_ROOT"]."/lang/dp_lang.php");
$multilang_params = multilang_init();
// -------------------------------------------------------------------------------
?>
<h3><?php echo translate_str_by_id(4148); ?></h3>

<!-- Start Блок добавления отзыва -->
<div id="make_evaluation" class="col-xs-12 col-sm-12 col-md-12 col-lg-12">	
<?php
//Для работы с пользователем
require_once( $_SERVER['DOCUMENT_ROOT']."/content/users/dp_user.php" );
$user_session = DP_User::getUserSession();

if($user_id > 0)
{
	//Проверяем наличие отзыва от данного пользователя
	$check_evaluation_query = $db_link->prepare('SELECT COUNT(*) FROM `shop_products_evaluations` WHERE `product_id` = :product_id AND `user_id` = :user_id;');
	$check_evaluation_query->bindValue(':product_id', $product_id);
	$check_evaluation_query->bindValue(':user_id', $user_id);
	$check_evaluation_query->execute();
	
	if( $check_evaluation_query->fetchColumn() == 0 )
	{
		//Выводим "Добавить отзыв"
		?>
		<div class="panel panel-primary" id="evaluations_form">
			<div class="panel-heading"><?php echo translate_str_by_id(4149); ?></div>
			<div class="panel-body">
				<div class="form-group">
					<label for="exampleInputPassword1"><?php echo translate_str_by_id(4150); ?></label>
					<div class="evaluations_mark">
						<i onclick="onStarPush(1);" class="fa fa-star-o em-primary star_evaluation"></i>
						<i onclick="onStarPush(2);" class="fa fa-star-o em-primary star_evaluation"></i>
						<i onclick="onStarPush(3);" class="fa fa-star-o em-primary star_evaluation"></i>
						<i onclick="onStarPush(4);" class="fa fa-star-o em-primary star_evaluation"></i>
						<i onclick="onStarPush(5);" class="fa fa-star-o em-primary star_evaluation"></i>
					</div>
				</div>
				<div class="form-group">
					<label for="exampleInputPassword1"><?php echo translate_str_by_id(3004); ?></label>
					<textarea class="form-control" rows="2" id="text_plus"></textarea>
				</div>
				<div class="form-group">
					<label for="exampleInputPassword1"><?php echo translate_str_by_id(3005); ?></label>
					<textarea class="form-control" rows="2" id="text_minus"></textarea>
				</div>
				<div class="form-group">
					<label for="exampleInputPassword1"><?php echo translate_str_by_id(3006); ?></label>
					<textarea class="form-control" rows="2" id="text"></textarea>
				</div>
				<div class="checkbox">
					<input type="checkbox" id="hide_user_data" />
					<label for="checkbox5"><?php echo translate_str_by_id(4151); ?></label>
				</div>
				<button id="sendEvaluation_Button" onclick="sendEvaluation();" class="btn btn-ar btn-primary"><?php echo translate_str_by_id(4152); ?></button>
			</div>
		</div>
		<script>
		var evaluation_object = new Object;//Объект отзыва
		// ------------------------------------------------------
		//Обработка нажатия звезды
		function onStarPush(mark)
		{
			evaluation_object.mark = mark;
			
			evaluationReview();
		}
		// ------------------------------------------------------
		//Переотображение формы оценок
		function evaluationReview()
		{
			var fa_stars = document.getElementsByClassName("star_evaluation");
			for(var i=0; i < fa_stars.length; i++)
			{
				if(i+1 <= evaluation_object.mark)
				{
					fa_stars[i].setAttribute("class", "fa fa-star em-primary star_evaluation");
				}
				else
				{
					fa_stars[i].setAttribute("class", "fa fa-star-o em-primary star_evaluation");
				}
			}
		}
		// ------------------------------------------------------
		//Функция отправки отзыва
		function sendEvaluation()
		{
			if(evaluation_object.mark == undefined)
			{
				alert("<?php echo translate_str_by_id(4153); ?>");
				return;
			}
			
			//Записываем достоинства, недостатки и общие впечатления
			evaluation_object.text_plus = document.getElementById("text_plus").value;
			evaluation_object.text_minus = document.getElementById("text_minus").value;
			evaluation_object.text = document.getElementById("text").value;
			
			if(evaluation_object.text_plus == "" && evaluation_object.text_minus == "" && evaluation_object.text == "")
			{
				alert('<?php echo translate_str_by_id(4154); ?>');
				return;
			}
			
			//Скрыть данные пользователя
			if(document.getElementById("hide_user_data").checked)
			{
				evaluation_object.hide_user_data = 1;
			}
			else
			{
				evaluation_object.hide_user_data = 0;
			}
			
			evaluation_object.product_id = <?php echo $product_id; ?>;
			
			document.getElementById("sendEvaluation_Button").setAttribute("disabled", "disabled");
			
			jQuery.ajax({
				type: "POST",
				async: true,
				url: "/content/shop/catalogue/evaluations/ajax_add_evaluation.php",
				dataType: "json",
				data: "evaluation_object="+encodeURI(JSON.stringify(evaluation_object))+"&csrf_guard_key=<?php echo $user_session["csrf_guard_key"]; ?>",
				success: function(answer)
				{
					if(answer.status == true)//Отзыв добавлен
					{
						//Выдаем сообщение, что отзыв добавлен
						alert("<?php echo translate_str_by_id(4155); ?>");
						
						//Убираем форму отправки отзыва
						var evaluations_form = document.getElementById("evaluations_form");
						evaluations_form.parentNode.removeChild(evaluations_form);
						
						//Обновляем список отзывов
						getProductEvaluations(0, "desc", 0);
						
						//Обновляем среднюю оценку
						getGeneralMark();
						
						//Показываем сообщение
						document.getElementById("make_evaluation").innerHTML = "<p><?php echo translate_str_by_id(4156); ?></p>";
					}
					else
					{
						alert("<?php echo translate_str_by_id(4157); ?>");
					}
				}
			});
		}
		// ------------------------------------------------------
		</script>
		<?php
	}
	else
	{
		?>
		<p><?php echo translate_str_by_id(4156); ?></p>
		<?php
	}
}
else//Пользователь не авторизован
{
	?>
	<p><?php echo translate_str_by_id(4158); ?></p>
	<?php
}
?>
</div>
<!-- End Блок добавления отзыва -->



<!-- Start Блок общей оценки -->
<div id="evaluations_general_mark">
</div>
<script>
//Функция обновления средней оценки
function getGeneralMark()
{
	jQuery.ajax({
		type: "POST",
		async: true,
		url: "/content/shop/catalogue/evaluations/ajax_get_product_general_mark.php",
		dataType: "json",
		data: "product_id=<?php echo $product_id; ?>"+"&csrf_guard_key=<?php echo $user_session["csrf_guard_key"]; ?>",
		success: function(answer)
		{
			if(answer.status == true)
			{
				var general_mark_html = "";
				general_mark_html += "<h3><?php echo translate_str_by_id(4159); ?></h3> ";
				
				general_mark_html += "<div class=\"evaluations_mark\">";
					
					for(var i=0; i < 5; i++)
					{
						if( answer.general_mark < i+1 )
						{
							general_mark_html += "<i class=\"fa fa-star-o em-primary star_evaluation\"></i> ";
						}
						else
						{
							general_mark_html += "<i class=\"fa fa-star em-primary star_evaluation\"></i> ";
						}
					}
				general_mark_html += "<?php echo translate_str_by_id(4160); ?>: " + answer.marks_count + "</div>";
				
				general_mark_html += "<div><i class=\"fa fa-star em-primary\"></i> <i class=\"fa fa-star-o em-primary\"></i> <i class=\"fa fa-star-o em-primary\"></i> <i class=\"fa fa-star-o em-primary\"></i> <i class=\"fa fa-star-o em-primary\"></i> " + answer.mark_1_count + "</div>";
				
				general_mark_html += "<div><i class=\"fa fa-star em-primary\"></i> <i class=\"fa fa-star em-primary\"></i> <i class=\"fa fa-star-o em-primary\"></i> <i class=\"fa fa-star-o em-primary\"></i> <i class=\"fa fa-star-o em-primary\"></i> " + answer.mark_2_count + "</div>";
				
				general_mark_html += "<div><i class=\"fa fa-star em-primary\"></i> <i class=\"fa fa-star em-primary\"></i> <i class=\"fa fa-star em-primary\"></i> <i class=\"fa fa-star-o em-primary\"></i> <i class=\"fa fa-star-o em-primary\"></i> " + answer.mark_3_count + "</div>";
				
				general_mark_html += "<div><i class=\"fa fa-star em-primary\"></i> <i class=\"fa fa-star em-primary\"></i> <i class=\"fa fa-star em-primary\"></i> <i class=\"fa fa-star em-primary\"></i> <i class=\"fa fa-star-o em-primary\"></i> " + answer.mark_4_count + "</div>";
				
				general_mark_html += "<div><i class=\"fa fa-star em-primary\"></i> <i class=\"fa fa-star em-primary\"></i> <i class=\"fa fa-star em-primary\"></i> <i class=\"fa fa-star em-primary\"></i> <i class=\"fa fa-star em-primary\"></i> " + answer.mark_5_count + "</div>";
			
				document.getElementById("evaluations_general_mark").innerHTML = general_mark_html;
			}
			else
			{
				alert("<?php echo translate_str_by_id(4161); ?>");
			}
		}
	});
}
getGeneralMark();
</script>
<!-- End Блок общей оценки -->	



<!-- Start Блок списка отзывов покупателей -->
<div id="evaluations_area">
</div>
<script>
var CurrentPage = 0;//Текущая страница

//Функция получения всех отзывов о товаре
function getProductEvaluations(page, asc_desc, mark)
{
	var evaluation_query = new Object;
	
	evaluation_query.page = page;//Страница
	evaluation_query.product_id = <?php echo $product_id; ?>;
	evaluation_query.asc_desc = asc_desc;//Сортировка
	evaluation_query.mark = mark;//Оценка. 0 - все
	
	jQuery.ajax({
		type: "POST",
		async: true,
		url: "/content/shop/catalogue/evaluations/ajax_get_product_evaluations.php",
		dataType: "json",
		data: "evaluation_query="+encodeURI(JSON.stringify(evaluation_query))+"&csrf_guard_key=<?php echo $user_session["csrf_guard_key"]; ?>",
		success: function(answer)
		{
			if(answer.status == true)
			{
				var evaluations = answer.evaluations;
				var evaluations_html = "<h3><?php echo translate_str_by_id(4162); ?></h3>";
				
				for(var e = 0; e < evaluations.length; e++)
				{
					//Начало отзыва
					evaluations_html += "<div class=\"panel panel-default\">";
					
						//Заголовок
						evaluations_html += "<div class=\"panel-heading\">";
							//Пользовател и время
							evaluations_html += evaluations[e].time + ", " + evaluations[e].user_name + "<br>";
							//Оценка
							for(var i=0; i < 5; i++)
							{
								if( evaluations[e].mark < i+1 )
								{
									evaluations_html += "<i class=\"fa fa-star-o em-primary star_evaluation\"></i> ";
								}
								else
								{
									evaluations_html += "<i class=\"fa fa-star em-primary star_evaluation\"></i> ";
								}
							}
						evaluations_html +=  "</div>";
						
						evaluations_html += "<div class=\"panel-body\">";
							evaluations_html += "<strong><?php echo translate_str_by_id(3004); ?>:</strong> "+evaluations[e].text_plus + "<br>";
							evaluations_html += "<strong><?php echo translate_str_by_id(3005); ?>:</strong> "+evaluations[e].text_minus + "<br>";
							evaluations_html += "<strong><?php echo translate_str_by_id(3006); ?>:</strong> "+evaluations[e].text + "<br>";
						evaluations_html += "</div>";
					evaluations_html += "</div>";
				}
				
				//HTML-код переключателя страниц
				//Первая страница
				var first_page = "<li><a onclick=\"go_to_page(0);\" href=\"javascript:void(0);\">0</a></li>";
				if(CurrentPage == 0) first_page = "";
				//Последняя страница
				var pages_total = answer.pages_total;//Всего страниц
				var last_page = "<li><a onclick=\"go_to_page("+(pages_total-1)+");\" href=\"javascript:void(0);\">"+(pages_total-1)+"</a></li>";
				if(CurrentPage == (pages_total - 1)) last_page = "";
				//Текущая страница
				var current_page = "<li class=\"active\"><a onclick=\"\" href=\"javascript:void(0);\">"+CurrentPage+"</a></li>";
				//Пара от текущей справа
				var right_pages = "";
				for(var i = CurrentPage+1; i < (pages_total - 1) && i < CurrentPage + 4; i++)
				{
					if(i == CurrentPage+3)
					{
						right_pages += "<li><a onclick=\"go_to_page("+i+");\" href=\"javascript:void(0);\">...</a></li>";
					}
					else
					{
						right_pages += "<li><a onclick=\"go_to_page("+i+");\" href=\"javascript:void(0);\">"+i+"</a></li>";
					}
				}
				//Пара от текущей слева
				var left_pages = "";
				for(var i = CurrentPage-1; i > 0 && i > CurrentPage-4; i--)
				{
					if(i == CurrentPage-3)
					{
						left_pages = "<li><a onclick=\"go_to_page("+i+");\" href=\"javascript:void(0);\">...</a></li>" + left_pages;
					}
					else
					{
						left_pages = "<li><a onclick=\"go_to_page("+i+");\" href=\"javascript:void(0);\">"+i+"</a></li>" + left_pages;
					}
				}
				//Компонуем:
				var pages_selector_container = "<div class=\"col-lg-12 text-center\"><ul class=\"pagination pagination-sm\">"+first_page + left_pages + current_page + right_pages + last_page+"</ul></div>";
				
				if(pages_total == 1)
				{
					pages_selector_container = "";
				}
				
				if(evaluations.length == 0)
				{
					document.getElementById("evaluations_area").innerHTML = "<h3><?php echo translate_str_by_id(4162); ?></h3><?php echo translate_str_by_id(4163); ?>";
				}
				else
				{
					document.getElementById("evaluations_area").innerHTML = evaluations_html + pages_selector_container;
				}
			}
			else
			{
				alert("<?php echo translate_str_by_id(4157); ?>");
			}
		}
	});
}
// ------------------------------------------------------------------------------
//Переход на требуемую страницу
function go_to_page(need_page)
{
	CurrentPage = need_page;
	getProductEvaluations(need_page, 'desc', 0);
}
// ------------------------------------------------------------------------------
getProductEvaluations(0, 'desc', 0);//После загрузки страницы получаем отзывы
</script>
<!-- End Блок списка отзывов покупателей -->
<?php
//END БЛОК ОТЗЫВОВ
//----------------------------------------------------------------------------------------------------------------
?>