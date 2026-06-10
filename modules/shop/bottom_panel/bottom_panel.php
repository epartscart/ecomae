<?php
/*
Модуль нижней панели
*/
defined('_ASTEXE_') or die('No access');

//Маска ввода телефона
require_once($_SERVER["DOCUMENT_ROOT"]."/lib/inputmask/phone_mask.php");

//Для работы с пользователем
require_once( $_SERVER['DOCUMENT_ROOT']."/content/users/dp_user.php" );
$user_session = DP_User::getUserSession();
?>

<div class="bottom-border">
</div>
<nav class=" navbar navbar-fixed-bottom">
	<div class="container-fluid">
		
		<button class="btn btn-ar btn-sm btn-gray bottom-button hidden-sm hidden-xs" onclick="location = '<?php echo $multilang_params['lang_href']; ?>/zapros-prodavczu';">
			<i class="fa fa-sm fa-rocket text-primary"></i>
			<?php echo function_exists('epc_portal_seller_request_label') ? htmlspecialchars(epc_portal_seller_request_label(), ENT_QUOTES, 'UTF-8') : translate_str_by_id(4115); ?>
		</button>
		
		<?php
		// ------------------------------------------------------------------------------------------------
		// START РЕДАКТИРОВАНИЕ ДОПОЛНИТЕЛЬНОГО ТЕКСТА ДЛЯ АДМИНИСТРАТОРА
		//Для администратора добавляем возможность перехода на редактирование текста
		require_once($_SERVER["DOCUMENT_ROOT"]."/content/users/dp_user.php");//Класс пользователя
		$user_id = DP_User::getUserId();

		if( $user_id > 0 )
		{
			//Получаем список групп, допущенных к управлению бэкэндом
			$backend_groups_list = array();//Список групп, допущенных до бэкэнда
			$backend_groups_list = getBackendGroups(NULL, $backend_groups_list);//ПОЛУЧАЕМ СПИСОК ГРУПП, ДОПУЩЕННЫХ К БЭКЭНДУ


			//Получаем список групп, к котором относится данный пользователь
			$user_groups_list = array();
			$stmt = $db_link->prepare('SELECT * FROM `users_groups_bind` WHERE `user_id` = :user_id;');
			$stmt->bindValue(':user_id', $user_id);
			$stmt->execute();
			while($user_group_record = $stmt->fetch(PDO::FETCH_ASSOC))
			{
				array_push($user_groups_list, $user_group_record["group_id"]);
			}
			
			//Теперь ищем первое совпадение элементов списка backend_groups_list и user_groups_list
			//Если есть совпадение - есть допуск, если совпадения нет $auth_result = false;
			$access_denied = true;//Доступ запрещен (в бэкенд)
			for($i = 0 ; $i < count($backend_groups_list); $i++)
			{
				for($j = 0 ; $j < count($user_groups_list) ; $j++)
				{
					if($backend_groups_list[$i] == $user_groups_list[$j])
					{
						//!!!Есть допуск!
						$access_denied = false;
						//...ЗДЕСЬ ВЫВОДИМ ВОЗМОЖНОСТЬ ПЕРЕХОДА НА РЕДАКТИРОВАНИЕ ДОПОЛНИТЕЛЬНОГО ТЕКСТА ДЛЯ ДАННОЙ СТРАНИЦЫ
						?>
						<button class="btn btn-ar btn-sm btn-gray bottom-button hidden-sm hidden-xs" onclick="document.getElementById('link_to_url_text_edit').click();">
							<i class="fa fa-sm fa-edit text-primary"></i>
							<?php echo translate_str_by_id(4764); ?>
						</button>
						<a id="link_to_url_text_edit" class="hidden" href="/<?php echo $DP_Config->backend_dir; ?>/content/dopolnitelnye-teksty/dopolnitelnyj-tekst?url=<?php echo urlencode( getPageUrl() ); ?>" target="_blank"><?php echo translate_str_by_id(4764); ?></a>
						<?php
						break;
					}
				}
				if(!$access_denied)break;
			}//~for($i)
		}


		// -----------------------------
		//Рекурсивная функция получения линейного списка групп для бэкэнда
		function getBackendGroups($parent_group_id, $backend_groups_list)
		{
			global $db_link;
			global $DP_Config;
			
			//Первый вызов метода - получаем верхнюю группу бэкэнда
			if($parent_group_id == NULL)
			{
				$stmt = $db_link->prepare('SELECT * FROM `groups` WHERE `for_backend` = :for_backend;');
				$stmt->bindValue(':for_backend', 1);
				$stmt->execute();
				$group_for_backend_record = $stmt->fetch(PDO::FETCH_ASSOC);
				array_push($backend_groups_list, $group_for_backend_record["id"]);//Добавляем основную группу для бэкэнда
				
				if($group_for_backend_record["count"] == 0)
				{
					return $backend_groups_list;
				}
				else
				{
					return getBackendGroups($group_for_backend_record["id"], $backend_groups_list);//Рекурсивный вызов для вложенных
				}
			}
			else//Был рекурсивный вызов - добавляем влоеженные группы
			{
				$stmt = $db_link->prepare('SELECT * FROM `groups` WHERE `parent` = :parent;');
				$stmt->bindValue(':parent', $parent_group_id);
				$stmt->execute();
				while($group_record = $stmt->fetch(PDO::FETCH_ASSOC))
				{
					array_push($backend_groups_list, $group_record["id"]);//Добавляем вложенную группу
					
					if($group_record["count"] > 0)
					{
						$backend_groups_list = getBackendGroups($group_record["id"], $backend_groups_list);//Рекурсивный вызов для вложенных
					}
				}
			}
			
			return $backend_groups_list;//Возвращаем рекурсивно заполненный список групп
		}
		// -----------------------------


		// END РЕДАКТИРОВАНИЕ ДОПОЛНИТЕЛЬНОГО ТЕКСТА ДЛЯ АДМИНИСТРАТОРА
		// ------------------------------------------------------------------------------------------------
		?>
		
		
		
		
		
		
		<ul class="nav navbar-nav navbar-right bottom-ul">
			
			<li>
				<span id="mark_compare_popup_added" class="badge-primary badge-round panel-primary hidden ">
					<?php echo translate_str_by_id(4225); ?>
				</span>
			
				<a href="<?php echo $multilang_params['lang_href']; ?>/shop/sravneniya">
					<i class="fa fa-copy fa-flip-horizontal bottom-icon"></i>
					<span class="bottom-label hidden-xs">
						<?php echo translate_str_by_id(4765); ?>
					</span>
					<span class="badge badge-primary badge-round " id="compare_count"></span>
				</a>
			</li>
			<script>
			//--------------------------------------------
			//Функция отображения количества товаров в сравнении
			function compareReview()
			{
				//Получаем уже добавленные товары
				var compare = getCookie('compare');
				if(compare == undefined)
				{
					compare = new Array();
				}
				else
				{
					compare = JSON.parse(compare);
				}
				
				if(document.getElementById("compare_count")){
					if(compare.length == 0)
					{
						document.getElementById("compare_count").setAttribute("class", "badge badge-default badge-round ");
					}
					else
					{
						document.getElementById("compare_count").setAttribute("class", "badge badge-primary badge-round ");
					}
					
					document.getElementById("compare_count").innerHTML = compare.length;
				}
				
				if(document.getElementById("header_compare_count")){
					document.getElementById("header_compare_count").innerHTML = compare.length;
					
					if( compare.length == 0 ){
						document.getElementById("header_compare_count").setAttribute("class", "hidden badge badge-default badge-round ");//Указатель количества
					}
					else{
						document.getElementById("header_compare_count").setAttribute("class", "badge badge-primary badge-round");//Указатель количества
					}
				}
			}
			//--------------------------------------------
			//Функция добавления в сравнения
			function addToCompare(product_id, link)
			{
				//Получаем уже добавленные закладки
				var compare = getCookie('compare');
				if(compare == undefined)
				{
					compare = new Array();
				}
				else
				{
					compare = JSON.parse(compare);
				}
				
				compare.push(product_id);//Добавляем в сравнения
				
				//Устанавливаем cookie (на полгода)
				var date = new Date(new Date().getTime() + 15552000 * 1000);
				document.cookie = "compare="+JSON.stringify(compare)+"; path=/; expires=" + date.toUTCString();
				
				compareReview();//Переотображаем указатель сравнений
				
				showAdded_compare();//Показываем лэйбл "Добавлено"
				
				//Обрабытываем вызывающую ссылку
				link.innerHTML = "<i class=\"glyphicon glyphicon-duplicate\"></i> <span><?php echo translate_str_by_id(4109); ?></span>";
				link.setAttribute("onclick", "location = '<?php echo $multilang_params['lang_href']; ?>/shop/sravneniya'; ");
				link.setAttribute("title", "<?php echo translate_str_by_id(4108); ?>");
				link.blur();
			}
			//--------------------------------------------
			//Удалить товар из сравнений
			function removeCompare(product_id, link)
			{
				//Получаем уже добавленные товары
				var compare = getCookie('compare');
				compare = JSON.parse(compare);
				
				//Удаляем элемент массива
				for(var i=0; i < compare.length; i++)
				{
					if(compare[i] == product_id)
					{
						compare.splice(i,1);
						break;
					}
				}
				
				
				//Устанавливаем cookie (на полгода)
				var date = new Date(new Date().getTime() + 15552000 * 1000);
				document.cookie = "compare="+JSON.stringify(compare)+"; path=/; expires=" + date.toUTCString();
				
				compareReview();//Переотображаем указатель
				
				
				//Удаляем объект товара из локальных переменных на странице сравнений
				for(var i=0; i < products_objects.length; i++)
				{
					if( parseInt(products_objects[i].id) == parseInt(product_id) )
					{
						products_objects.splice(i,1);
						break;
					}
				}
				
				
				//Если сравнений не осталось
				if(compare.length == 0 && document.getElementById("work_area"))
				{
					document.getElementById("work_area").innerHTML = '<?php echo translate_str_by_id(4766); ?>';
					
					return;
				}
				

				allReview();	
			}
			//--------------------------------------------
			//Функция показа лэйбла "Добавлено"
			function showAdded_compare()
			{
				if(document.getElementById("mark_compare_popup_added")){
					document.getElementById("mark_compare_popup_added").setAttribute("class", "badge-primary badge-round panel-primary");
					
					setTimeout(function() {
						hideAdded_compare();
					}, 5000);
				}
			}
			//--------------------------------------------
			//Функция скрытия лэйбла "Добавлено"
			function hideAdded_compare()
			{
				if(document.getElementById("mark_compare_popup_added")){
					document.getElementById("mark_compare_popup_added").setAttribute("class", "badge-primary badge-round panel-primary hidden");
				}
			}
			//--------------------------------------------
			</script>
			
			<?php
			//-------------------------------------------------------------------------------------------------
			?>
			
			
			<li>
				<span id="mark_bookmarks_popup_added" class="badge-primary badge-round panel-primary hidden ">
					<?php echo translate_str_by_id(4225); ?>
				</span>
				
				<a href="<?php echo $multilang_params['lang_href']; ?>/shop/zakladki">
					<i class="fa fa-bookmark-o bottom-icon"></i>
					<span class="bottom-label hidden-xs">
						<?php echo translate_str_by_id(4767); ?>
					</span>
					<span class="badge badge-primary badge-round " id="bookmarks_count"></span>
				</a>
			</li>
			<script>
			//--------------------------------------------
			//Функция отображения количества закладок
			function bookmarksReview()
			{
				//Получаем уже добавленные закладки
				var bookmarks = getCookie('bookmarks');
				if(bookmarks == undefined)
				{
					bookmarks = new Array();
				}
				else
				{
					bookmarks = JSON.parse(bookmarks);
				}
				
				if(document.getElementById("bookmarks_count")){
					if(bookmarks.length == 0)
					{
						document.getElementById("bookmarks_count").setAttribute("class", "badge badge-default badge-round ");
					}
					else
					{
						document.getElementById("bookmarks_count").setAttribute("class", "badge badge-primary badge-round ");
					}
					
					document.getElementById("bookmarks_count").innerHTML = bookmarks.length;
				}
				
				if(document.getElementById("header_bookmarks_count")){
					document.getElementById("header_bookmarks_count").innerHTML = bookmarks.length;
					
					if( bookmarks.length == 0 ){
						document.getElementById("header_bookmarks_count").setAttribute("class", "hidden badge badge-default badge-round ");//Указатель количества
					}
					else{
						document.getElementById("header_bookmarks_count").setAttribute("class", "badge badge-primary badge-round");//Указатель количества
					}
				}
			}
			//--------------------------------------------
			//Функция добавления в закладки
			function addToBookmarks(product_id, link)
			{
				//Получаем уже добавленные закладки
				var bookmarks = getCookie('bookmarks');
				if(bookmarks == undefined)
				{
					bookmarks = new Array();
				}
				else
				{
					bookmarks = JSON.parse(bookmarks);
				}
				
				bookmarks.push(product_id);//Добавляем закладку
				
				//Устанавливаем cookie (на полгода)
				var date = new Date(new Date().getTime() + 15552000 * 1000);
				document.cookie = "bookmarks="+JSON.stringify(bookmarks)+"; path=/; expires=" + date.toUTCString();
				
				bookmarksReview();//Переотображаем указатель закладок
				
				showAdded_bookmarks();//Показываем лэйбл "Добавлено"
				
				//Обрабытываем вызывающую ссылку
				link.innerHTML = "<i class=\"fa fa-bookmark\"></i> <span><?php echo translate_str_by_id(4104); ?></span>";
				link.setAttribute("onclick", "location = '<?php echo $multilang_params['lang_href']; ?>/shop/zakladki'; ");
				link.setAttribute("title", "<?php echo translate_str_by_id(4103); ?>");
				link.blur();
			}
			//--------------------------------------------
			//Удалить закладку
			function removeBookmark(product_id, link)
			{
				//Получаем уже добавленные закладки
				var bookmarks = getCookie('bookmarks');
				bookmarks = JSON.parse(bookmarks);
				
				//Удаляем элемент массива
				for(var i=0; i < bookmarks.length; i++)
				{
					if(bookmarks[i] == product_id)
					{
						bookmarks.splice(i,1);
						break;
					}
				}
				
				
				//Устанавливаем cookie (на полгода)
				var date = new Date(new Date().getTime() + 15552000 * 1000);
				document.cookie = "bookmarks="+JSON.stringify(bookmarks)+"; path=/; expires=" + date.toUTCString();
				
				bookmarksReview();//Переотображаем указатель закладок
				
				
				//Удаляем сам блок товара
				var area = link.parentNode.parentNode.parentNode;
				area.removeChild(link.parentNode.parentNode);
				
				//Если закладок не осталось
				if(bookmarks.length == 0)
				{
					area.innerHTML = '<?php echo translate_str_by_id(4768); ?>';
					
					if(document.getElementById("products_area_turning")){
						document.getElementById("products_area_turning").innerHTML = "";
					}
				}
			}
			//--------------------------------------------
			// возвращает cookie с именем name, если есть, если нет, то undefined
			function getCookie(name) 
			{
				var matches = document.cookie.match(new RegExp(
					"(?:^|; )" + name.replace(/([\.$?*|{}\(\)\[\]\\\/\+^])/g, '\\$1') + "=([^;]*)"
				));
				return matches ? decodeURIComponent(matches[1]) : undefined;
			}
			//--------------------------------------------
			//Функция показа лэйбла "Добавлено"
			function showAdded_bookmarks()
			{
				if(document.getElementById("mark_bookmarks_popup_added")){
					document.getElementById("mark_bookmarks_popup_added").setAttribute("class", "badge-primary badge-round panel-primary");
					
					setTimeout(function() {
						hideAdded_bookmarks();
					}, 5000);
				}
			}
			//--------------------------------------------
			//Функция скрытия лэйбла "Добавлено"
			function hideAdded_bookmarks()
			{
				if(document.getElementById("mark_bookmarks_popup_added")){
					document.getElementById("mark_bookmarks_popup_added").setAttribute("class", "badge-primary badge-round panel-primary hidden");
				}
			}
			//--------------------------------------------
			<?php
			if(isset($_COOKIE["session"])){
			?>
			bookmarksReview();//После загрузки страницы указываем количество закладок
			compareReview();//После загрузки страницы указываем количество товаров в сравнениях
			<?php
			}
			?>
			</script>
			
			<?php
			//-------------------------------------------------------------------------------------------------
			?>
			
			<li>	
				<span id="mark_popup_added" class="badge-primary badge-round panel-primary hidden ">
					<?php echo translate_str_by_id(4225); ?>
				</span>

				<a href="<?php echo $multilang_params['lang_href']; ?>/shop/cart">
					<i class="fa fa-shopping-cart bottom-icon"></i>
					<span class="bottom-label hidden-xs">
						<?php echo translate_str_by_id(4410); ?>
					</span>
					<span class="badge badge-primary badge-round " id="cart_items_count"></span>
					<span id="cart_items_sum" class="hidden-xs"></span>
				</a>
			</li>
			<script>
			//Функция обновления информации по корзине
			function updateCartInfo()
			{
				//updateCartInfoHeader();
				
				jQuery.ajax({
					type: "POST",
					async: true,
					url: "/content/shop/order_process/ajax_get_cart_info.php",
					dataType: "json",
					data: "csrf_guard_key=<?php echo $user_session["csrf_guard_key"]; ?>",
					success: function(answer)
					{
						if(document.getElementById("cart_items_sum")){
							document.getElementById("cart_items_sum").innerHTML = answer.cart_items_sum;
						}
						
						if(document.getElementById("cart_items_count")){
							document.getElementById("cart_items_count").innerHTML = answer.cart_items_count;
							
							if( answer.cart_items_count == 0 )
							{
								document.getElementById("cart_items_count").setAttribute("class", "badge badge-default badge-round ");//Указатель количества
							}
							else
							{
								document.getElementById("cart_items_count").setAttribute("class", "badge badge-primary badge-round ");//Указатель количества
							}
						}
						
						if(document.getElementById("header_cart_items_sum")){
							document.getElementById("header_cart_items_sum").innerHTML = answer.cart_items_sum;
						}
						
						if(document.getElementById("header_cart_items_count")){
							
							document.getElementById("header_cart_items_count").innerHTML = answer.cart_items_count;
							
							<?php
							if($DP_Template->id == 63){
							?>
							if( answer.cart_items_count == 0 ){
								document.getElementById("header_cart_items_count").setAttribute("class", "hidden badge badge-default badge-round ");//Указатель количества
							}
							else{
								document.getElementById("header_cart_items_count").setAttribute("class", "badge badge-primary badge-round");//Указатель количества
							}
							<?php
							}
							?>
						}
						
						if(document.getElementById("header_cart_items_count_mobile")){
							document.getElementById("header_cart_items_count_mobile").innerHTML = answer.cart_items_count;
							
							if( answer.cart_items_count == 0 ){
								document.getElementById("header_cart_items_count_mobile").setAttribute("class", "hidden badge badge-default badge-round ");//Указатель количества
							}
							else{
								document.getElementById("header_cart_items_count_mobile").setAttribute("class", "badge badge-primary badge-round");//Указатель количества
							}
						}
					}
				});
			}
			<?php
			if(isset($_COOKIE["session"])){
			?>
			updateCartInfo();//После загрузки страницы обновляем модуль корзины
			<?php
			}
			?>
			
			//Функция показа лэйбла "Добавлено"
			function showAdded()
			{
				if(document.getElementById("mark_popup_added")){
					document.getElementById("mark_popup_added").setAttribute("class", "badge-primary badge-round panel-primary");
					
					setTimeout(function() {
						hideAdded();
					}, 5000);
				}
			}
			//Функция скрытия лэйбла "Добавлено"
			function hideAdded()
			{
				if(document.getElementById("mark_popup_added")){
					document.getElementById("mark_popup_added").setAttribute("class", "badge-primary badge-round panel-primary hidden");
				}
			}
			</script>
		</ul>

	</div>
</nav>


<?php
if($DP_Template->id == 63){
//Панель доступна только для нового шаблона потому что под старые шаблоны не написаны стили отображения
//Нельзя переносить код в отдельный JS файл так как внутри часть кода формируется с помощью PHP
?>
<script>
//Панель быстрого выбора бренда при вводе артикула в строку поиска
$(document).ready(function() {
	//Убираем автозаполнение
	$('input[name="article"]').attr('autocomplete','off');
	
	//Убираем панель после потери фокуса
	$(document).mouseup(function (e)
	{
		let container = $(".search-simple-bar");

		if (!container.is(e.target) && container.has(e.target).length === 0) {
			$('.search-simple-bar').remove();
		}
	});
	
	//Устанавливаем панель при клике и вводе
	$('form').find('input').on("click input", function () {
		if($(this).attr('name') === 'article'){
			let request_object = new Object;
				request_object.value = $(this).val();
			
			let input = $(this);
			jQuery.ajax({
				type: "POST",
				async: true,
				url: "/content/shop/docpart/ajax_get_article_list.php",
				dataType: "json",//Тип возвращаемого значения
				data: "request_object="+encodeURIComponent(JSON.stringify(request_object))+"&csrf_guard_key=<?php echo $user_session["csrf_guard_key"]; ?>",
				success: function(answer)
				{
					//console.log(answer);
					answer = answer.list;
					if(answer.length > 0){
						let html = '';
						for(let i = 0; i < answer.length; i++){
							if(answer[i].manufacturer == null){
								continue;
							}
							let manufacturer_alias = answer[i].manufacturer;
							if(manufacturer_alias == '')
							{
								manufacturer_alias = '<?php echo $DP_Config->chpu_search_config["level_2"]["mode_2"]["url"]; ?>';
							}
							manufacturer_alias = manufacturer_alias.split('/').join('<?php echo $DP_Config->chpu_search_config["slash_code"]; ?>');
							let url = '/<?php echo $DP_Config->chpu_search_config["level_1"]["url"]; ?>/'+manufacturer_alias+'/'+answer[i].article;
							
							if(answer[i].name != ''){
								answer[i].name = '<div style="font-size: 12px; color: #999;">'+ answer[i].name +'</div>';
							}
							
							html += '<tr style="cursor:pointer;" onClick="location=\'<?php echo $multilang_params['lang_href']; ?>'+url+'\'"><td style="vertical-align: top;">'+answer[i].article+'</td>'+'<td style="width:100%; vertical-align: top;">'+answer[i].manufacturer + answer[i].name+'</td>'+'</tr>';

						}
						html = '<div class="search-simple-bar-content"><div class="table-div"><table class="table">'+html+'</table></div></div>';
						html += '<div style="border-top: 1px solid #f5f5f5;" class="hidden-md hidden-lg search-simple-bar-footer"><a style="border: 1px solid #f5f5f5; padding: 5px 15px; display: inline-block; border-radius: 6px; line-height: 20px; margin: 5px; font-size: 14px; color: #555; opacity: 1;" class="close" onClick="$(\'.search-simple-bar\').remove();"><?php echo translate_str_by_id(2190); ?></a></div>';
						$('.search-simple-bar').remove();
						input.after($('<div>', {class: 'search-simple-bar'}));
						$('.search-simple-bar').html(html);
					}else{
						$('.search-simple-bar').remove();
					}
					
				},
				error: function (e, ajaxOptions, thrownError){
					console.log('Ошибка: '+ e.status +' - '+ thrownError);
					return;
				}
			});
		}
	});
});
</script>
<?php
}
?>