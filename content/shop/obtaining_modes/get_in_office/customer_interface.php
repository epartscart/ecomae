<?php
defined('_ASTEXE_') or die('No access');
//Скрипт графического интерфеса покупателя


//Для работы с пользователем:
require_once($_SERVER["DOCUMENT_ROOT"]."/content/users/dp_user.php");
$user_id = DP_User::getUserId();


//Функция обработки специальных знаков
function handleSpecialChars($str)
{
	return trim(str_replace( array("'",'"',"\t","\r","\n"), "", $str));
}//~function handleSpecialChars()
?>

<!-- START БЛОК ДЛЯ ВЫВОДА СХЕМ РАСПОЛОЖЕНИЯ ОФИСОВ -->
<?php
$yandex_lang = 'ru-RU';
if( $multilang_params['lang'] != 'ru' )
{
	$yandex_lang = 'en_US';
}
?>
<script src="https://api-maps.yandex.ru/2.0-stable/?load=package.standard&lang=<?php echo $yandex_lang; ?>" type="text/javascript"></script>
<script type="text/javascript">
	var myMap, myPlacemark;
	var ymaps_init_flag = 0;
	// --------------------------------------------------------------------------------------
	function map_init()
	{
	    //Получаем объекта офиса по его id
        var office = undefined;
        for(var i=0; i < customer_offices.length; i++)
        {
            if(customer_offices[i].id == current_selected_office)
            {
                office = customer_offices[i];
                break;
            }
        }
	
		myMap = new ymaps.Map ("map", {
			center: JSON.parse("["+office.coordinates+"]"),
			zoom: 16
		}); 
		
		var myPlacemark = new ymaps.Placemark(JSON.parse("["+office.coordinates+"]"), {balloonContent: office.caption}, {
			iconImageHref: '/content/files/images/maps-marker.png',
			iconImageSize: [27, 44],
			iconImageOffset: [-13, -44]
		});
		
		myMap.geoObjects.add(myPlacemark);
		
		
		myMap.controls.add(new ymaps.control.MapTools());
		myMap.controls.add('typeSelector');
		myMap.controls.add('zoomControl');
	}
	// --------------------------------------------------------------------------------------
	//Скрыть и показать карту и режим работы
    function show_hide_timetable_map()
    {
        var timetable_map = document.getElementById("timetable_map");
        if(timetable_map.getAttribute("state") == "hidden")
        {
            timetable_map.setAttribute("state", "shown");
            $("#timetable_map").show("slow");
        }
        else
        {
            timetable_map.setAttribute("state", "hidden");
            $("#timetable_map").hide(300);
        }
    }
    // --------------------------------------------------------------------------------------
	function ymaps_init(){
		if(ymaps_init_flag == 0){
			map_init();
			ymaps_init_flag = 1;
		}
	}
</script>
<!-- END БЛОК ДЛЯ ВЫВОДА СХЕМ РАСПОЛОЖЕНИЯ ОФИСОВ -->

<br/>

<div><p class="lead"><?php echo translate_str_by_id(4448); ?>:</p></div>
<div class="list-group">
<script>
var customer_offices = new Array();//Офисы обслуживания
<?php
//Получить список магазинов покупателя
require_once($_SERVER["DOCUMENT_ROOT"]."/content/shop/order_process/get_customer_offices.php");
for($i=0; $i < count($customer_offices); $i++)
{
	$office_query = $db_link->prepare('SELECT * FROM `shop_offices` WHERE `id` = ?;');
	$office_query->execute( array($customer_offices[$i]) );
    $office = $office_query->fetch();
    ?>
    customer_offices[customer_offices.length] = new Object;
    customer_offices[customer_offices.length-1].id = <?php echo $office["id"]; ?>;
    customer_offices[customer_offices.length-1].caption = "<?php echo handleSpecialChars(translate_str_by_id($office["caption"])); ?>";
    customer_offices[customer_offices.length-1].country = "<?php echo translate_str_by_id($office["country"]); ?>";
    customer_offices[customer_offices.length-1].region = "<?php echo translate_str_by_id($office["region"]); ?>";
    customer_offices[customer_offices.length-1].city = "<?php echo translate_str_by_id($office["city"]); ?>";
    customer_offices[customer_offices.length-1].address = "<?php echo handleSpecialChars(translate_str_by_id($office["address"])); ?>";
    customer_offices[customer_offices.length-1].coordinates = "<?php echo $office["coordinates"]; ?>";
    customer_offices[customer_offices.length-1].description = "<?php echo handleSpecialChars(translate_str_by_id($office["description"])); ?>";
    
	</script>
	<div onclick="showOfficeInfo(<?php echo $office["id"]; ?>);" class="office_box list-group-item" id="office_box_<?php echo $office["id"]; ?>">
		<span style="border:0;"><?php echo handleSpecialChars(translate_str_by_id($office["caption"])); ?>, <?php echo translate_str_by_id(3376); ?>: <?php echo translate_str_by_id($office["city"]); ?>, <?php echo handleSpecialChars(translate_str_by_id($office["address"])); ?></span>
    </div>
    <div class="office_info hidden_info" id="office_info_<?php echo $office["id"]; ?>"></div>
	<script>
	
	
	<?php
}
?>
</script>
</div>
<script>
// ------------------------------------------------------------------------------
//Показать информацию по офису
var current_selected_office = 0;//Текущий выбранный офис
function showOfficeInfo(id)
{
    //Предварительно возвращаем все виджеты в исходное состояние
    for(var i=0; i < customer_offices.length; i++)
    {
        //Информационные блоки
        document.getElementById("office_info_"+customer_offices[i].id).setAttribute("class", "office_info hidden_info");//Невидимость
        document.getElementById("office_info_"+customer_offices[i].id).innerHTML = "";
        
        //Блоки с указание офиса
        document.getElementById("office_box_"+customer_offices[i].id).setAttribute("class", "office_box");
        document.getElementById("office_box_"+customer_offices[i].id).setAttribute("onclick", "showOfficeInfo("+customer_offices[i].id+");");
    }
    

    //Получаем объекта офиса по его id
    var office = undefined;
    for(var i=0; i < customer_offices.length; i++)
    {
        if(customer_offices[i].id == id)
        {
            office = customer_offices[i];
            break;
        }
    }
    
    
    //Настраиваем классы для блока?
    document.getElementById("office_info_"+customer_offices[i].id).setAttribute("class", "office_info");//Делаем видимым информационный блок
    document.getElementById("office_box_"+customer_offices[i].id).setAttribute("class", "office_box selected_office");//Выдяем выбранный офис
    document.getElementById("office_box_"+customer_offices[i].id).setAttribute("onclick", "");//Не кликабелен


    //Формируем содержимо информационного блока
    officeInfoHtml = "";//HTML с информацией по офису
    officeInfoHtml += "<div class=\"loading_info\">";
        officeInfoHtml += "<img src=\"/content/files/images/ajax-loader.gif\" />";
    officeInfoHtml += "<div>";
    document.getElementById("office_info_"+id).innerHTML = officeInfoHtml;
    

    current_selected_office = id;//Запомнили последний выбранный офис
    //Теперь делаем запрос для уточнения условий получения в данном офисе
    jQuery.ajax({
        type: "POST",
        async: false, //Запрос синхронный
        url: "/content/shop/obtaining_modes/get_in_office/ajax_specify_office_info.php",
        dataType: "text",//Тип возвращаемого значения
        data: "office_id="+id,
        success: function(answer)
        {
            document.getElementById("office_info_"+current_selected_office).innerHTML = answer;
        }
    });
}
// ------------------------------------------------------------------------------
//Переход к следующему шагу:
function nextStep()
{
	//Объект способа доставки
    var how_get = new Object;
    how_get.mode = <?php echo $current_obtain_mode; ?>;
	how_get.office_id = current_selected_office;
	
	//Устанавливаем cookie (на полгода)
    var date = new Date(new Date().getTime() + 15552000 * 1000);
    document.cookie = "how_get="+JSON.stringify(how_get)+"; path=/; expires=" + date.toUTCString();
	
	location = "<?php echo $multilang_params['lang_href']; ?>/shop/checkout/confirm";
}
// ------------------------------------------------------------------------------
//Если офис - единственный, то сразу его выбираем
jQuery( window ).load(function() {
    if( customer_offices.length == 1 )
	{
		showOfficeInfo( customer_offices[0].id );
	}
});
</script>